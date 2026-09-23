using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(UserManager<AppKullanici> users, SignInManager<AppKullanici> signIn,
    SmsDogrulamaService sms, ISertifikaliFirmaKimlikProvider external,
    IOptions<SertifikaliFirmaKimlikOptions> externalOptions, IOptions<SmsOptions> smsOptions,
    IDataProtectionProvider protection, OturumTokenService tokens, AppDbContext context) : ControllerBase
{
    private const string LoginError = "Kullanıcı adı veya şifre hatalı.";
    private readonly ITimeLimitedDataProtector _challengeProtector = protection.CreateProtector("API.Auth.Challenge.v1").ToTimeLimitedDataProtector();
    private sealed record VerificationChallenge(string UserId, string Purpose, string SmsPurpose, string Stamp, string? ExternalReference);

    [HttpPost("token"), EnableRateLimiting("Authentication")]
    public async Task<IActionResult> Token(GirisIstegi dto)
    {
        var user = await FindAsync(dto.Email);
        SertifikaliFirmaKimlikSonucu? identity = null;
        if (externalOptions.Value.Enabled && (user == null || user.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma))
        {
            if (!external.KullanilabilirMi) return Error("Firma giriş servisi henüz yapılandırılmadı.", 503);
            identity = await external.KimlikDogrulaAsync(dto.Email.Trim(), dto.Sifre, HttpContext.RequestAborted);
            if (!identity.Basarili) return Error(LoginError, 401);
            user = await FindAsync(string.IsNullOrWhiteSpace(identity.YerelKullaniciAdi) ? dto.Email : identity.YerelKullaniciAdi);
            if (user?.KullaniciTipi != KullaniciTipiDegerleri.SertifikaliFirma) return Error(LoginError, 401);
        }
        if (user == null || !user.AktifMi || await users.IsLockedOutAsync(user)) return Error(LoginError, 401);
        if (identity == null)
        {
            var result = await signIn.CheckPasswordSignInAsync(user, dto.Sifre, lockoutOnFailure: true);
            if (!result.Succeeded) return Error(result.IsLockedOut
                ? "Çok fazla hatalı giriş denemesi. Lütfen 15 dakika sonra tekrar deneyin." : LoginError, 401);
        }
        if (!KullaniciRolTutarlilikKurali.FirmaRolleriUyumlu(
                user.KullaniciTipi, await users.GetRolesAsync(user), user.SirketId))
            return Error("Hesap yetkileriniz tutarsız. Sistem yöneticinizle iletişime geçin.", 403);
        if (sms.SmsGirisAktifMi || identity?.TelefonDogrulamasiGerekliMi == true)
        {
            if (!sms.SmsGirisAktifMi) return Error("Telefon doğrulaması için SMS servisi yapılandırılmalıdır.", 503);
            if (identity != null && string.IsNullOrWhiteSpace(identity.DogrulamaReferansi))
                return Error("Kimlik servisi doğrulama işlem referansı döndürmedi.", 503);
            var phone = identity?.Telefon;
            if (identity == null && user.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis
                && string.IsNullOrWhiteSpace(user.PhoneNumber) && user.FirmaId.HasValue)
            {
                var firmPhone = await context.Ys_Firmalar.AsNoTracking()
                    .Where(f => f.Id == user.FirmaId.Value && !f.SilindiMi)
                    .Select(f => f.Telefon)
                    .FirstOrDefaultAsync();
                var digits = new string((firmPhone ?? "").Where(char.IsDigit).ToArray());
                if ((digits.Length == 10 && digits.StartsWith('5'))
                    || (digits.Length == 11 && digits.StartsWith("05", StringComparison.Ordinal))
                    || (digits.Length == 12 && digits.StartsWith("905", StringComparison.Ordinal)))
                    phone = firmPhone;
            }
            return await ChallengeAsync(user, "GIRIS", identity?.DogrulamaReferansi, phone);
        }
        if (!string.IsNullOrWhiteSpace(identity?.DogrulamaReferansi))
        {
            var completion = await external.DogrulamayiTamamlaAsync(identity.DogrulamaReferansi, HttpContext.RequestAborted);
            if (!completion.Basarili) return Error("Firma giriş işlemi tamamlanamadı.", 503);
        }
        return await CompleteLoginAsync(user);
    }

    [HttpPost("sms-dogrula"), EnableRateLimiting("Authentication")]
    public async Task<IActionResult> Verify(SmsDogrulamaIstegi dto)
    {
        var (challenge, user) = await ReadChallengeAsync(dto.Dogrulama, "GIRIS");
        if (challenge == null || user == null) return Error("Doğrulama süresi doldu. Yeniden giriş yapın.");
        var result = await sms.KodDogrulaAsync(user.Id, dto.Kod, challenge.SmsPurpose);
        if (!result.Basarili) return Error(result.Mesaj);
        if (!string.IsNullOrWhiteSpace(challenge.ExternalReference))
        {
            if (!external.KullanilabilirMi) return Error("Firma doğrulama servisine ulaşılamadı. Yeniden giriş yapın.", 503);
            var completion = await external.DogrulamayiTamamlaAsync(challenge.ExternalReference, HttpContext.RequestAborted);
            if (!completion.Basarili) return Error("Firma doğrulaması tamamlanamadı. Yeniden giriş yapın.", 503);
        }
        return await CompleteLoginAsync(user);
    }

    [HttpPost("sifre-unuttum"), EnableRateLimiting("Authentication")]
    public async Task<IActionResult> Forgot(SifreUnuttumIstegi dto)
    {
        var user = await FindAsync(dto.KullaniciAdi);
        // Unknown accounts never receive a usable verification challenge.
        if (user == null || !user.AktifMi)
            return Ok(new OturumSonucu { Basarili = true, Dogrulama = Convert.ToHexString(RandomNumberGenerator.GetBytes(64)),
                Mesaj = "Bilgileriniz kayıtlıysa telefonunuza doğrulama kodu gönderildi." });
        return await ChallengeAsync(user, "SIFRE_SIFIRLA", null, null);
    }

    [HttpPost("sifre-yenile"), EnableRateLimiting("Authentication")]
    public async Task<IActionResult> Reset(SifreYenileIstegi dto)
    {
        var (challenge, user) = await ReadChallengeAsync(dto.Dogrulama, "SIFRE_SIFIRLA");
        if (challenge == null || user == null) return Error("Doğrulama süresi doldu. Yeniden kod isteyin.");
        foreach (var validator in users.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(users, user, dto.YeniSifre);
            if (!validation.Succeeded) return IdentityError(validation);
        }
        var result = await sms.KodDogrulaAsync(user.Id, dto.Kod, challenge.SmsPurpose);
        if (!result.Basarili) return Error(result.Mesaj);
        var reset = await users.ResetPasswordAsync(user, await users.GeneratePasswordResetTokenAsync(user), dto.YeniSifre);
        return reset.Succeeded ? Ok(new OturumSonucu { Basarili = true, Mesaj = "Şifreniz değiştirildi. Yeni şifrenizle giriş yapabilirsiniz." }) : IdentityError(reset);
    }

    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await users.GetUserAsync(User);
        return user?.AktifMi == true ? Ok(new OturumSonucu { Basarili = true, Kullanici = await tokens.KullaniciAsync(user) }) : Unauthorized();
    }

    [Authorize, HttpPut("profil")]
    public async Task<IActionResult> Profile(ProfilGuncelleIstegi dto)
    {
        var user = await users.GetUserAsync(User);
        if (user?.AktifMi != true) return Unauthorized();
        user.AdSoyad = dto.AdSoyad.Trim();
        var emailChanged = !string.Equals(user.Email, dto.Email.Trim(), StringComparison.OrdinalIgnoreCase);
        var phoneChanged = user.PhoneNumber != dto.PhoneNumber?.Trim();
        user.Email = dto.Email.Trim();
        user.UserName = user.Email;
        user.PhoneNumber = dto.PhoneNumber?.Trim();
        if (emailChanged) user.EmailConfirmed = false;
        if (phoneChanged) user.PhoneNumberConfirmed = false;
        var result = await users.UpdateAsync(user);
        return result.Succeeded ? Ok(await tokens.OlusturAsync(user)) : IdentityError(result);
    }

    [Authorize, HttpPost("sifre-degistir"), EnableRateLimiting("Authentication")]
    public async Task<IActionResult> Password(SifreDegistirIstegi dto)
    {
        var user = await users.GetUserAsync(User);
        if (user?.AktifMi != true) return Unauthorized();
        var result = await users.ChangePasswordAsync(user, dto.MevcutSifre, dto.YeniSifre);
        return result.Succeeded ? Ok(await tokens.OlusturAsync(user)) : IdentityError(result);
    }

    private async Task<IActionResult> ChallengeAsync(AppKullanici user, string purpose, string? reference, string? phone)
    {
        var smsPurpose = (purpose == "GIRIS" ? "G:" : "S:") + Convert.ToHexString(RandomNumberGenerator.GetBytes(10));
        var result = await sms.KodGonderAsync(user, smsPurpose, phone);
        if (!result.Basarili) return Error(result.Mesaj, 503);
        var challenge = new VerificationChallenge(user.Id, purpose, smsPurpose, await users.GetSecurityStampAsync(user), reference);
        var protectedValue = _challengeProtector.Protect(JsonSerializer.Serialize(challenge), TimeSpan.FromMinutes(Math.Clamp(smsOptions.Value.CodeExpireMinutes, 1, 30)));
        return Ok(new OturumSonucu { Basarili = true, Dogrulama = protectedValue,
            Mesaj = purpose == "SIFRE_SIFIRLA" && !smsOptions.Value.TestMode
                ? "Bilgileriniz kayıtlıysa telefonunuza doğrulama kodu gönderildi." : result.Mesaj });
    }

    private async Task<(VerificationChallenge?, AppKullanici?)> ReadChallengeAsync(string value, string purpose)
    {
        VerificationChallenge? challenge;
        try { challenge = JsonSerializer.Deserialize<VerificationChallenge>(_challengeProtector.Unprotect(value)); }
        catch (Exception ex) when (ex is CryptographicException or JsonException) { return (null, null); }
        if (challenge?.Purpose != purpose) return (null, null);
        var user = await users.FindByIdAsync(challenge.UserId);
        if (user?.AktifMi != true || await users.IsLockedOutAsync(user) || await users.GetSecurityStampAsync(user) != challenge.Stamp)
            return (null, null);
        return (challenge, user);
    }

    private async Task<AppKullanici?> FindAsync(string login)
    {
        var value = login.Trim();
        var user = await users.FindByEmailAsync(value) ?? await users.FindByNameAsync(value);
        if (user != null || value.Length != 11 || !value.All(char.IsDigit))
            return user;

        var firmIds = await context.Ys_Firmalar.AsNoTracking()
            .Where(f => !f.SilindiMi && f.TcKimlikNo == value)
            .Select(f => f.Id)
            .Take(2)
            .ToListAsync();
        if (firmIds.Count != 1)
            return null;

        var serviceUsers = await users.Users
            .Where(u => u.FirmaId == firmIds[0] && u.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis)
            .Take(2)
            .ToListAsync();
        return serviceUsers.Count == 1 ? serviceUsers[0] : null;
    }
    private IActionResult Error(string message, int status = 400) => StatusCode(status, new OturumSonucu { Mesaj = message });
    private IActionResult IdentityError(IdentityResult result) => Error(string.Join(" ", result.Errors.Select(x => x.Description)));

    private async Task<IActionResult> CompleteLoginAsync(AppKullanici user)
    {
        if (!KullaniciRolTutarlilikKurali.FirmaRolleriUyumlu(
                user.KullaniciTipi, await users.GetRolesAsync(user), user.SirketId))
            return Error("Hesap yetkileriniz tutarsız. Sistem yöneticinizle iletişime geçin.", 403);

        var systemAdmin = user.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
            || (user.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !user.SirketId.HasValue);
        var role = user.KullaniciTipi switch
        {
            KullaniciTipiDegerleri.YetkiliServis => KullaniciRolAdlari.YetkiliServis,
            KullaniciTipiDegerleri.SertifikaliFirma => KullaniciRolAdlari.SertifikaliFirma,
            KullaniciTipiDegerleri.Personel => KullaniciRolAdlari.Personel,
            KullaniciTipiDegerleri.SirketAdmin => systemAdmin ? KullaniciRolAdlari.GenelSistemAdmin : KullaniciRolAdlari.SirketAdmin,
            KullaniciTipiDegerleri.GenelSistemAdmin => KullaniciRolAdlari.GenelSistemAdmin,
            _ => null
        };
        if (role == null) return Error(LoginError, 401);
        if (!await users.IsInRoleAsync(user, role))
        {
            var result = await users.AddToRoleAsync(user, role);
            if (!result.Succeeded) return IdentityError(result);
        }
        if (systemAdmin && !await users.IsInRoleAsync(user, KullaniciRolAdlari.EskiSuperAdmin))
        {
            var result = await users.AddToRoleAsync(user, KullaniciRolAdlari.EskiSuperAdmin);
            if (!result.Succeeded) return IdentityError(result);
        }
        if (!systemAdmin && user.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && await users.IsInRoleAsync(user, KullaniciRolAdlari.EskiSuperAdmin))
        {
            var result = await users.RemoveFromRoleAsync(user, KullaniciRolAdlari.EskiSuperAdmin);
            if (!result.Succeeded) return IdentityError(result);
        }
        return Ok(await tokens.OlusturAsync(user));
    }
}
