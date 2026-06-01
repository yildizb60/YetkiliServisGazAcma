using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AhlatciSmsProvider : ISmsProvider
    {
        private static readonly HashSet<string> GecerliBasliklar =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "CORUMGAZ",
                "KARGAZ",
                "SURMELIGAZ",
                "YALOVAGAZ",
                "CORLUGAZ"
            };

        private readonly HttpClient _httpClient;
        private readonly SmsOptions _options;
        private readonly ILogger<AhlatciSmsProvider> _logger;
        private string? _cachedToken;
        private DateTime _cachedTokenExpiresUtc;

        public AhlatciSmsProvider(
            HttpClient httpClient,
            IOptions<SmsOptions> options,
            ILogger<AhlatciSmsProvider> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public string ProviderName => "AhlatciSms";

        public async Task<SmsGonderimSonucu> GonderAsync(string telefon, string mesaj, string? firmaKodu = null)
        {
            if (!_options.Enabled)
                return new SmsGonderimSonucu(false, Hata: "SMS dogrulama devre disi.");

            if (string.Equals(_options.Provider, "Development", StringComparison.OrdinalIgnoreCase))
            {
                return new SmsGonderimSonucu(
                    true,
                    MesajId: $"DEV-{DateTime.Now:yyyyMMddHHmmss}");
            }

            var token = await TokenAlAsync();
            if (string.IsNullOrWhiteSpace(token))
                return new SmsGonderimSonucu(false, Hata: "SMS API token bilgisi alinamadi.");

            var payload = new SmsGonderIstek
            {
                Numbers = SmsNumarasi(telefon),
                Message = mesaj,
                Header = BaslikBul(firmaKodu),
                CountryCode = string.IsNullOrWhiteSpace(_options.CountryCode) ? "90" : _options.CountryCode.Trim(),
                Info = new SmsGonderBilgi
                {
                    Company = string.IsNullOrWhiteSpace(_options.InfoCompany) ? "SCADA" : _options.InfoCompany.Trim(),
                    UserName = string.IsNullOrWhiteSpace(_options.InfoUserName) ? "ACEX" : _options.InfoUserName.Trim(),
                    AccessIP = string.IsNullOrWhiteSpace(_options.InfoAccessIP) ? "127.0.0.1" : _options.InfoAccessIP.Trim()
                }
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, PathTemizle(_options.SendPath));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(payload);

                using var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var hata = $"SMS API hata dondu ({(int)response.StatusCode}).";
                    _logger.LogWarning("SMS API request failed. StatusCode: {StatusCode}, Body: {Body}",
                        (int)response.StatusCode,
                        Kisa(responseBody));
                    return new SmsGonderimSonucu(false, Hata: hata);
                }

                if (ApiBasarisiz(responseBody, out var apiHata))
                    return new SmsGonderimSonucu(false, Hata: apiHata ?? "SMS API gonderimi basarisiz dondu.");

                return new SmsGonderimSonucu(true, MesajId: MesajIdBul(responseBody) ?? $"AHL-{DateTime.Now:yyyyMMddHHmmss}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS API gonderimi sirasinda hata olustu.");
                return new SmsGonderimSonucu(false, Hata: "SMS API cagrisi sirasinda hata olustu.");
            }
        }

        private async Task<string?> TokenAlAsync()
        {
            if (!string.IsNullOrWhiteSpace(_options.BearerToken))
                return _options.BearerToken.Trim();

            if (!string.IsNullOrWhiteSpace(_cachedToken) &&
                _cachedTokenExpiresUtc > DateTime.UtcNow.AddMinutes(1))
                return _cachedToken;

            if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
                return null;

            var payload = new
            {
                username = _options.Username,
                password = _options.Password,
                grant_type = string.IsNullOrWhiteSpace(_options.GrantType) ? "password" : _options.GrantType,
                scope = string.IsNullOrWhiteSpace(_options.Scope) ? "SMSApiLocal" : _options.Scope
            };

            try
            {
                using var response = await _httpClient.PostAsJsonAsync(PathTemizle(_options.TokenPath), payload);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("SMS token request failed. StatusCode: {StatusCode}, Body: {Body}",
                        (int)response.StatusCode,
                        Kisa(body));
                    return null;
                }

                var token = TokenBul(body);
                if (string.IsNullOrWhiteSpace(token))
                    return null;

                _cachedToken = token;
                _cachedTokenExpiresUtc = TokenSonKullanmaUtc(body, token) ?? DateTime.UtcNow.AddMinutes(30);
                return _cachedToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS API token cagrisi sirasinda hata olustu.");
                return null;
            }
        }

        private string BaslikBul(string? firmaKodu)
        {
            var aday = firmaKodu?.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(aday))
            {
                if (_options.CompanyHeaders.TryGetValue(aday, out var ayarBaslik) &&
                    GecerliBasliklar.Contains(ayarBaslik))
                {
                    return ayarBaslik;
                }

                if (GecerliBasliklar.Contains(aday))
                    return aday;

                if (aday.Contains("YALOVA", StringComparison.OrdinalIgnoreCase))
                    return "YALOVAGAZ";

                if (aday.Contains("CORLU", StringComparison.OrdinalIgnoreCase) ||
                    aday.Contains("CERKEZKOY", StringComparison.OrdinalIgnoreCase) ||
                    aday.Contains("TEKIRDAG", StringComparison.OrdinalIgnoreCase))
                    return "CORLUGAZ";
            }

            if (GecerliBasliklar.Contains(_options.DefaultHeader))
                return _options.DefaultHeader;

            if (GecerliBasliklar.Contains(_options.Sender))
                return _options.Sender;

            return "CORUMGAZ";
        }

        private static string SmsNumarasi(string telefon)
        {
            var rakamlar = new string((telefon ?? string.Empty).Where(char.IsDigit).ToArray());

            if (rakamlar.StartsWith("90") && rakamlar.Length == 12)
                return rakamlar[2..];

            if (rakamlar.StartsWith("0") && rakamlar.Length == 11)
                return rakamlar[1..];

            return rakamlar;
        }

        private static string PathTemizle(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "/";

            return path.TrimStart('/');
        }

        private static string? TokenBul(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    var token = StringProperty(root, "access_token", "token", "bearerToken", "bearer_token", "Token", "AccessToken");
                    if (!string.IsNullOrWhiteSpace(token))
                        return token;
                }
            }
            catch (JsonException)
            {
            }

            var temiz = body.Trim().Trim('"');
            return temiz.Contains('.') ? temiz : null;
        }

        private static DateTime? TokenSonKullanmaUtc(string body, string token)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object &&
                    IntProperty(root, out var expiresIn, "expires_in", "expiresIn", "ExpiresIn"))
                    return DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn));
            }
            catch (JsonException)
            {
            }

            return JwtExpUtc(token);
        }

        private static DateTime? JwtExpUtc(string token)
        {
            var parts = token.Split('.');
            if (parts.Length < 2)
                return null;

            try
            {
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                using var doc = JsonDocument.Parse(json);
                if (LongProperty(doc.RootElement, out var exp, "exp"))
                    return DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
            }
            catch
            {
            }

            return null;
        }

        private static bool ApiBasarisiz(string body, out string? hata)
        {
            hata = null;
            if (string.IsNullOrWhiteSpace(body))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object &&
                    BoolProperty(root, out var basarili, "success", "isSuccess", "basarili", "Basarili") && !basarili)
                {
                    hata = StringProperty(root, "message", "Message", "error", "Error", "hata", "Hata");
                    return true;
                }
            }
            catch (JsonException)
            {
            }

            return false;
        }

        private static string? MesajIdBul(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.ValueKind == JsonValueKind.Object
                    ? StringProperty(doc.RootElement, "messageId", "MessageId", "id", "Id", "smsId", "SmsId")
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? StringProperty(JsonElement root, params string[] names)
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (names.Any(x => string.Equals(x, prop.Name, StringComparison.OrdinalIgnoreCase)))
                    return prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
            }

            return null;
        }

        private static bool BoolProperty(JsonElement root, out bool value, params string[] names)
        {
            value = false;
            foreach (var prop in root.EnumerateObject())
            {
                if (!names.Any(x => string.Equals(x, prop.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False)
                {
                    value = prop.Value.GetBoolean();
                    return true;
                }
            }

            return false;
        }

        private static bool IntProperty(JsonElement root, out int value, params string[] names)
        {
            value = 0;
            foreach (var prop in root.EnumerateObject())
            {
                if (!names.Any(x => string.Equals(x, prop.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (prop.Value.TryGetInt32(out value))
                    return true;
            }

            return false;
        }

        private static bool LongProperty(JsonElement root, out long value, params string[] names)
        {
            value = 0;
            foreach (var prop in root.EnumerateObject())
            {
                if (!names.Any(x => string.Equals(x, prop.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (prop.Value.TryGetInt64(out value))
                    return true;
            }

            return false;
        }

        private static string Kisa(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin))
                return "";

            return metin.Length <= 400 ? metin : metin[..400];
        }

        private class SmsGonderIstek
        {
            public string Numbers { get; set; } = "";
            public string Message { get; set; } = "";
            public string Header { get; set; } = "";
            public string CountryCode { get; set; } = "90";
            public SmsGonderBilgi Info { get; set; } = new();
        }

        private class SmsGonderBilgi
        {
            public string Company { get; set; } = "SCADA";
            public string UserName { get; set; } = "ACEX";
            public string AccessIP { get; set; } = "127.0.0.1";
        }
    }
}
