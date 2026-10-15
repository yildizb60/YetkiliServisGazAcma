using Microsoft.Extensions.Logging.Abstractions;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services;

public sealed class AuthApiClient(HttpClient client)
{
    // Authentication always uses the API, independently of optional data-client fallback settings.
    private readonly ApiHttpClient _api = new(client, new ApiIntegrationOptions { Enabled = true }, null, NullLogger.Instance);

    public Task<OturumSonucu> GirisAsync(string login, string password) =>
        SendAsync(HttpMethod.Post, "token", new GirisIstegi(login, password));
    public Task<OturumSonucu> SmsAsync(string challenge, string code) =>
        SendAsync(HttpMethod.Post, "sms-dogrula", new SmsDogrulamaIstegi(challenge, code));
    public Task<OturumSonucu> SifreUnuttumAsync(string login) =>
        SendAsync(HttpMethod.Post, "sifre-unuttum", new SifreUnuttumIstegi(login));
    public Task<OturumSonucu> SifreYenileAsync(string challenge, string code, string password) =>
        SendAsync(HttpMethod.Post, "sifre-yenile", new SifreYenileIstegi(challenge, code, password));
    public Task<OturumSonucu> KullaniciAsync(string token) => AuthenticatedAsync(HttpMethod.Get, "me", null, token);
    public Task<OturumSonucu> ProfilAsync(ProfilGuncelleIstegi dto, string token) => AuthenticatedAsync(HttpMethod.Put, "profil", dto, token);
    public Task<OturumSonucu> SifreAsync(SifreDegistirIstegi dto, string token) => AuthenticatedAsync(HttpMethod.Post, "sifre-degistir", dto, token);

    private Task<OturumSonucu> AuthenticatedAsync(HttpMethod method, string path, object? dto, string token)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ApiOturumSuresiDolduException();
        return SendAsync(method, path, dto, token);
    }

    private Task<OturumSonucu> SendAsync(HttpMethod method, string path, object? dto, string? token = null)
        => _api.SendAuthAsync(method, "api/auth/" + path, dto, token);
}

public sealed class ApiOturumSuresiDolduException : Exception;
