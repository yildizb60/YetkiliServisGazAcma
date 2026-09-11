using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services;

public sealed class AuthApiClient(HttpClient client)
{
    public Task<OturumSonucu> GirisAsync(string login, string password) =>
        SendAsync(HttpMethod.Post, "token", new GirisIstegi(login, password));
    public Task<OturumSonucu> SmsAsync(string challenge, string code) =>
        SendAsync(HttpMethod.Post, "sms-dogrula", new SmsDogrulamaIstegi(challenge, code));
    public Task<OturumSonucu> SifreUnuttumAsync(string login) =>
        SendAsync(HttpMethod.Post, "sifre-unuttum", new SifreUnuttumIstegi(login));
    public Task<OturumSonucu> SifreYenileAsync(string challenge, string code, string password) =>
        SendAsync(HttpMethod.Post, "sifre-yenile", new SifreYenileIstegi(challenge, code, password));
    public Task<OturumSonucu> KullaniciAsync(string token) => SendAsync(HttpMethod.Get, "me", null, token);
    public Task<OturumSonucu> ProfilAsync(ProfilGuncelleIstegi dto, string token) => SendAsync(HttpMethod.Put, "profil", dto, token);
    public Task<OturumSonucu> SifreAsync(SifreDegistirIstegi dto, string token) => SendAsync(HttpMethod.Post, "sifre-degistir", dto, token);

    private async Task<OturumSonucu> SendAsync(HttpMethod method, string path, object? dto, string? token = null)
    {
        using var request = new HttpRequestMessage(method, "api/auth/" + path);
        if (dto != null) request.Content = JsonContent.Create(dto);
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await client.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.Unauthorized && token != null)
                throw new ApiOturumSuresiDolduException();
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OturumSonucu>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (result == null) return Failure();
            if (!response.IsSuccessStatusCode) result.Basarili = false;
            if (!result.Basarili && string.IsNullOrWhiteSpace(result.Mesaj))
                result.Mesaj = response.StatusCode == HttpStatusCode.TooManyRequests
                    ? "Çok fazla deneme yapıldı. Lütfen bir dakika sonra tekrar deneyin."
                    : "Bilgileri kontrol edip tekrar deneyin.";
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Failure();
        }
    }

    private static OturumSonucu Failure() => new() { Mesaj = "Kimlik servisine ulaşılamadı. Lütfen tekrar deneyin." };
}

public sealed class ApiOturumSuresiDolduException : Exception;
