using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services;

public sealed class ApiKullaniciOturumu(AuthApiClient api, IHttpContextAccessor accessor)
{
    internal const string TokenKey = "API.AccessToken";
    internal const string UserKey = "API.UserId";
    internal const string ExpiresKey = "API.Expires";
    private OturumKullaniciDto? _current;
    private HttpContext Context => accessor.HttpContext ?? throw new InvalidOperationException("HTTP oturumu yok.");

    public async Task<AppKullanici?> GetUserAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return null;
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (_current == null)
        {
            if (!TryToken(Context, id, out var token))
            {
                await BitirAsync();
                return null;
            }
            var response = await api.KullaniciAsync(token);
            if (!response.Basarili || response.Kullanici == null)
                throw new ApiOturumSuresiDolduException();
            if (response.Kullanici.Id != id) throw new ApiOturumSuresiDolduException();
            _current = response.Kullanici;
        }
        return Entity(_current);
    }

    public async Task<bool> IsInRoleAsync(AppKullanici user, string role)
    {
        await GetUserAsync(Context.User);
        return _current?.Id == user.Id && _current.Roller.Contains(role);
    }

    public async Task<IdentityResult> UpdateAsync(AppKullanici user)
    {
        var result = await api.ProfilAsync(new ProfilGuncelleIstegi(user.AdSoyad ?? "", user.Email ?? "", user.PhoneNumber), Token(Context, user.Id));
        return await UpdatedAsync(result);
    }

    public async Task<IdentityResult> ChangePasswordAsync(AppKullanici user, string current, string password)
    {
        var result = await api.SifreAsync(new SifreDegistirIstegi(current, password), Token(Context, user.Id));
        return await UpdatedAsync(result);
    }

    private async Task<IdentityResult> UpdatedAsync(OturumSonucu result)
    {
        if (!result.Basarili) return IdentityResult.Failed(new IdentityError { Description = result.Mesaj ?? "İşlem tamamlanamadı." });
        await BaslatAsync(result);
        return IdentityResult.Success;
    }

    public async Task<AppKullanici> BaslatAsync(OturumSonucu result)
    {
        if (!result.Basarili || result.Kullanici == null || string.IsNullOrWhiteSpace(result.Token) || !result.Bitis.HasValue || result.Bitis <= DateTimeOffset.UtcNow)
            throw new ApiOturumSuresiDolduException();
        _current = result.Kullanici;
        Context.Session.SetString(TokenKey, result.Token);
        Context.Session.SetString(UserKey, _current.Id);
        Context.Session.SetString(ExpiresKey, result.Bitis!.Value.ToString("O"));
        var claims = new List<Claim> {
            new(ClaimTypes.NameIdentifier, _current.Id), new(ClaimTypes.Name, _current.AdSoyad ?? ""),
            new(ClaimTypes.Email, _current.Email ?? ""), new("KullaniciTipi", _current.KullaniciTipi.ToString())
        };
        claims.AddRange(_current.Roller.Select(x => new Claim(ClaimTypes.Role, x)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await Context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = result.Bitis, AllowRefresh = false });
        Context.User = principal;
        return Entity(_current);
    }

    public async Task BitirAsync()
    {
        Context.Session.Clear();
        _current = null;
        await Context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Context.User = new ClaimsPrincipal(new ClaimsIdentity());
    }

    internal static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        // The browser cookie can survive a restart while the in-memory API session does not.
        await context.HttpContext.Session.LoadAsync(context.HttpContext.RequestAborted);
        if (TryToken(context.HttpContext, context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out _))
            return;

        context.RejectPrincipal();
        context.HttpContext.Session.Clear();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }

    internal static bool TryToken(HttpContext context, string? userId, out string token)
    {
        token = string.Empty;
        var session = context.Session;
        var storedToken = session.GetString(TokenKey);
        if (string.IsNullOrEmpty(userId) || session.GetString(UserKey) != userId
            || !DateTimeOffset.TryParse(session.GetString(ExpiresKey), out var expires) || expires <= DateTimeOffset.UtcNow
            || string.IsNullOrWhiteSpace(storedToken))
            return false;
        token = storedToken;
        return true;
    }

    internal static string Token(HttpContext context, string? userId)
        => TryToken(context, userId, out var token) ? token : throw new ApiOturumSuresiDolduException();

    private static AppKullanici Entity(OturumKullaniciDto dto) => new() { Id = dto.Id, UserName = dto.UserName,
        Email = dto.Email, AdSoyad = dto.AdSoyad, PhoneNumber = dto.PhoneNumber, AktifMi = true,
        KullaniciTipi = dto.KullaniciTipi, FirmaId = dto.FirmaId, SirketId = dto.SirketId };
}
