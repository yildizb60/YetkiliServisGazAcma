using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<AppKullanici> _users;
    private readonly OturumTokenService _tokens;
    private readonly OturumAkisApiService _oturum;

    public AuthController(UserManager<AppKullanici> users, OturumTokenService tokens, OturumAkisApiService oturum)
    {
        _users = users;
        _tokens = tokens;
        _oturum = oturum;
    }

    [HttpPost("token"), EnableRateLimiting("Authentication")]
    public async Task<IActionResult> Token(GirisIstegi dto)
        => Yanit(await _oturum.TokenAsync(dto, HttpContext.RequestAborted));

    [HttpPost("sms-dogrula"), EnableRateLimiting("Authentication")]
    public async Task<IActionResult> Verify(SmsDogrulamaIstegi dto)
        => Yanit(await _oturum.VerifyAsync(dto, HttpContext.RequestAborted));

    [HttpPost("sifre-unuttum"), EnableRateLimiting("Authentication")]
    public async Task<IActionResult> Forgot(SifreUnuttumIstegi dto)
        => Yanit(await _oturum.ForgotAsync(dto));

    [HttpPost("sifre-yenile"), EnableRateLimiting("Authentication")]
    public async Task<IActionResult> Reset(SifreYenileIstegi dto)
        => Yanit(await _oturum.ResetAsync(dto));

    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await _users.GetUserAsync(User);
        return (user?.AktifMi == true && user.ArsivlemeTarihi == null)
            ? Ok(new OturumSonucu { Basarili = true, Kullanici = await _tokens.KullaniciAsync(user) })
            : Unauthorized();
    }

    [Authorize, HttpPut("profil")]
    public async Task<IActionResult> Profile(ProfilGuncelleIstegi dto)
    {
        var user = await _users.GetUserAsync(User);
        if (user?.AktifMi != true || user.ArsivlemeTarihi != null) return Unauthorized();
        return Yanit(await _oturum.ProfileAsync(user, dto));
    }

    [Authorize, HttpPost("sifre-degistir"), EnableRateLimiting("Authentication")]
    public async Task<IActionResult> Password(SifreDegistirIstegi dto)
    {
        var user = await _users.GetUserAsync(User);
        if (user?.AktifMi != true || user.ArsivlemeTarihi != null) return Unauthorized();
        return Yanit(await _oturum.PasswordAsync(user, dto));
    }

    private IActionResult Yanit(OturumAkisSonucu sonuc) => sonuc.Hata switch
    {
        OturumAkisHatasi.Yok => Ok(sonuc.Sonuc),
        OturumAkisHatasi.GirisReddedildi => StatusCode(StatusCodes.Status401Unauthorized, sonuc.Sonuc),
        OturumAkisHatasi.YetkiTutarsiz => StatusCode(StatusCodes.Status403Forbidden, sonuc.Sonuc),
        OturumAkisHatasi.ServisHazirDegil => StatusCode(StatusCodes.Status503ServiceUnavailable, sonuc.Sonuc),
        _ => StatusCode(StatusCodes.Status400BadRequest, sonuc.Sonuc)
    };
}
