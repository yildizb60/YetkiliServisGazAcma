using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services;

internal sealed class ApiHttpClient(HttpClient http, ApiIntegrationOptions options,
    ApiJwtTokenService? tokens, ILogger logger)
{
    public async Task<OturumSonucu> SendAuthAsync(HttpMethod method, string url, object? istek, string? token)
    {
        const string operasyon = nameof(AuthApiClient);
        try
        {
            return await SendAsync(null, url, () => istek == null ? null : JsonContent.Create(istek), operasyon,
                async response =>
                {
                    // Login 401 is a business response; only an explicit bearer 401 expires the session.
                    if (response.StatusCode == HttpStatusCode.Unauthorized && token != null)
                        throw new ApiOturumSuresiDolduException();
                    OturumSonucu sonuc;
                    try { sonuc = await OkuAsync<OturumSonucu>(response, operasyon, default); }
                    catch (Exception ex) when (response.StatusCode == HttpStatusCode.TooManyRequests
                        && ex is JsonException or NotSupportedException or ApiIntegrationException)
                    {
                        // Rate-limit middleware can reject a request without a JSON body.
                        sonuc = new();
                    }
                    if (!response.IsSuccessStatusCode) sonuc.Basarili = false;
                    if (!sonuc.Basarili && string.IsNullOrWhiteSpace(sonuc.Mesaj))
                        sonuc.Mesaj = response.StatusCode == HttpStatusCode.TooManyRequests
                            ? "Çok fazla deneme yapıldı. Lütfen bir dakika sonra tekrar deneyin."
                            : "Bilgileri kontrol edip tekrar deneyin.";
                    return sonuc;
                }, false, false, default, method, token, authResponse: true) ?? AuthFailure();
        }
        catch (ApiIntegrationException)
        {
            return AuthFailure();
        }
    }

    private static OturumSonucu AuthFailure() => new() { Mesaj = "Kimlik servisine ulaşılamadı. Lütfen tekrar deneyin." };

    public Task<ApiIslemSonuc?> PostIslemAsync<TRequest>(AppKullanici? kullanici,
        string url, TRequest istek, string operasyon)
        => PostAsync<TRequest, ApiIslemSonuc>(kullanici, url, istek, operasyon);

    public Task<TResponse?> PostAsync<TRequest, TResponse>(AppKullanici? kullanici,
        string url, TRequest istek, string operasyon, CancellationToken cancellationToken = default,
        bool retryTransient = false)
        => SendAsync(kullanici, url, () => JsonContent.Create(istek), operasyon,
            response => OkuAsync<TResponse>(response, operasyon, cancellationToken),
            typeof(ApiIslemSonuc).IsAssignableFrom(typeof(TResponse)), retryTransient, cancellationToken);

    public Task<TResponse?> PostFormAsync<TResponse>(AppKullanici kullanici, string url,
        Func<MultipartFormDataContent> formOlustur, string operasyon, CancellationToken cancellationToken = default)
        => SendAsync(kullanici, url, formOlustur, operasyon,
            response => OkuAsync<TResponse>(response, operasyon, cancellationToken),
            typeof(ApiIslemSonuc).IsAssignableFrom(typeof(TResponse)), false, cancellationToken);

    public Task<ApiDosyaSonuc?> PostFileAsync<TRequest>(AppKullanici kullanici, string url,
        TRequest istek, string dosyaAdi, string operasyon, bool retryTransient = false)
        => SendAsync(kullanici, url, () => JsonContent.Create(istek), operasyon,
            response => ApiDosyaSonuc.FromResponseAsync(response, dosyaAdi), false, retryTransient, default);

    private static async Task<TResponse> OkuAsync<TResponse>(HttpResponseMessage response,
        string operasyon, CancellationToken cancellationToken)
    {
        var sonuc = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
        if (sonuc is null)
            throw new ApiIntegrationException(operasyon, "Veri servisinden geçerli yanıt alınamadı.", 502);
        if (!response.IsSuccessStatusCode && sonuc is ApiIslemSonuc islem)
        {
            islem.Basarili = false;
            if (string.IsNullOrWhiteSpace(islem.Mesaj))
                islem.Mesaj = "İşlem tamamlanamadı. Girdiğiniz bilgileri kontrol edin.";
        }
        return sonuc;
    }

    private async Task<TResponse?> SendAsync<TResponse>(AppKullanici? kullanici, string url,
        Func<HttpContent?> contentOlustur, string operasyon, Func<HttpResponseMessage, Task<TResponse>> oku,
        bool islemYaniti, bool retryTransient, CancellationToken cancellationToken,
        HttpMethod? method = null, string? bearerToken = null, bool authResponse = false)
    {
        if (!options.Enabled)
        {
            ApiClientFallback.EnsureAllowed(options, operasyon);
            return default;
        }
        // Only explicitly marked read operations may be retried. Writes and uploads run once.
        var denemeSayisi = retryTransient ? 3 : 1;
        try
        {
            var token = kullanici == null ? bearerToken : await (tokens
                ?? throw new InvalidOperationException("Authenticated API client requires a token service."))
                .OlusturAsync(kullanici);
            if (kullanici != null && string.IsNullOrWhiteSpace(token))
                throw new ApiIntegrationException(operasyon, "Oturumunuzun süresi doldu. Lütfen yeniden giriş yapın.", 401);

            for (var deneme = 1; ; deneme++)
            {
                try
                {
                    using var request = new HttpRequestMessage(method ?? HttpMethod.Post, url) { Content = contentOlustur() };
                    if (token != null)
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    else if (authResponse)
                        // An empty value suppresses inherited defaults without changing the shared HttpClient.
                        request.Headers.TryAddWithoutValidation("Authorization", string.Empty);
                    using var response = await http.SendAsync(request, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        logger.LogWarning("{Operation} API yaniti: {StatusCode}", operasyon, response.StatusCode);
                        if (deneme < denemeSayisi && ((int)response.StatusCode >= 500
                            || response.StatusCode == HttpStatusCode.RequestTimeout))
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(500 * deneme, 2000)), cancellationToken);
                            continue;
                        }
                        if (!authResponse && (!islemYaniti || response.StatusCode is not (HttpStatusCode.BadRequest or HttpStatusCode.NotFound
                            or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)))
                            await HataYukseltAsync(response, operasyon, typeof(TResponse) == typeof(ApiDosyaSonuc));
                    }
                    return await oku(response);
                }
                catch (Exception ex) when (deneme < denemeSayisi && !cancellationToken.IsCancellationRequested
                    && ex is HttpRequestException or OperationCanceledException)
                {
                    logger.LogWarning(ex, "{Operation} API gecici baglanti hatasi, deneme {Attempt}", operasyon, deneme);
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(500 * deneme, 2000)), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning(ex, "{Operation} API baglanti hatasi", operasyon);
            ApiClientFallback.EnsureAllowed(options, operasyon);
            return default;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "{Operation} API yanit bicimi gecersiz", operasyon);
            throw new ApiIntegrationException(operasyon, "Veri servisinden geçerli yanıt alınamadı.", 502);
        }
    }

    private static async Task HataYukseltAsync(HttpResponseMessage response, string operasyon, bool dosyaYaniti)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var dogrulamaMesaji = dosyaYaniti
                ? "Rapor oluşturulamadı. Tarih aralığını ve kayıt seçimini kontrol edin."
                : "İşlem tamamlanamadı. Girdiğiniz bilgileri kontrol edin.";
            try
            {
                var hata = await response.Content.ReadFromJsonAsync<ApiIslemSonuc>();
                if (!string.IsNullOrWhiteSpace(hata?.Mesaj)) dogrulamaMesaji = hata.Mesaj;
            }
            catch (JsonException) { }
            catch (NotSupportedException) { }
            throw new ApiIntegrationException(operasyon, dogrulamaMesaji, (int)response.StatusCode);
        }
        var mesaj = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Oturumunuzun süresi doldu. Lütfen yeniden giriş yapın.",
            HttpStatusCode.Forbidden => "Bu işlem veya dosya için erişim yetkiniz yok.",
            HttpStatusCode.NotFound => "İstenen kayıt veya dosya bulunamadı.",
            HttpStatusCode.Conflict => "Kayıt durumu değişti. Sayfayı yenileyip tekrar deneyin.",
            HttpStatusCode.TooManyRequests => "Çok fazla istek gönderildi. Kısa bir süre sonra tekrar deneyin.",
            _ => "Veri servisine şu anda ulaşılamıyor. Lütfen kısa bir süre sonra yeniden deneyin."
        };
        throw new ApiIntegrationException(operasyon, mesaj, (int)response.StatusCode);
    }
}
