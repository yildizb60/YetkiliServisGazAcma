using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace YetkiliServisGazAcma.Business.Services
{
    public class ApiDosyaSonuc
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";
        public string DosyaAdi { get; set; } = "dosya";

        internal static async Task DogrulamaHatasiniYukseltAsync(HttpResponseMessage response, string operasyon)
        {
            if (response.StatusCode != System.Net.HttpStatusCode.BadRequest) return;

            var mesaj = "Rapor oluşturulamadı. Tarih aralığını ve kayıt seçimini kontrol edin.";
            try
            {
                var hata = await response.Content.ReadFromJsonAsync<DosyaHataCevabi>();
                if (!string.IsNullOrWhiteSpace(hata?.Mesaj)) mesaj = hata.Mesaj;
            }
            catch (JsonException)
            {
                // Non-JSON validation responses must not turn into service-outage messages.
            }
            throw new ApiIntegrationException(operasyon, mesaj, StatusCodes.Status400BadRequest);
        }

        private sealed class DosyaHataCevabi
        {
            public string? Mesaj { get; set; }
        }

        public static async Task<ApiDosyaSonuc> FromResponseAsync(HttpResponseMessage response, string varsayilanDosyaAdi)
        {
            var contentDisposition = response.Content.Headers.ContentDisposition;
            var dosyaAdi = contentDisposition?.FileNameStar
                ?? Temizle(contentDisposition?.FileName)
                ?? varsayilanDosyaAdi;

            return new ApiDosyaSonuc
            {
                Bytes = await response.Content.ReadAsByteArrayAsync(),
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
                DosyaAdi = dosyaAdi
            };
        }

        private static string? Temizle(string? fileName)
        {
            return string.IsNullOrWhiteSpace(fileName)
                ? null
                : fileName.Trim().Trim('"');
        }
    }
}
