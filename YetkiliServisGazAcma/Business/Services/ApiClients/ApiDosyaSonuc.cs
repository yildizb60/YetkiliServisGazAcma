using System.Net.Http.Headers;

namespace YetkiliServisGazAcma.Business.Services
{
    public class ApiDosyaSonuc
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";
        public string DosyaAdi { get; set; } = "dosya";

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
