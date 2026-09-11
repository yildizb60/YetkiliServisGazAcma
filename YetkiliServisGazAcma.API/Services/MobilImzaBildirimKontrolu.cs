using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public static class MobilImzaBildirimKontrolu
{
    public static (byte[]? Pdf, string? Hata) Dogrula(MobilImzaBildirimi input)
    {
        if (string.IsNullOrWhiteSpace(input.DosyaAdi) || input.DosyaAdi.Length > 180
            || input.DosyaAdi.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || input.DosyaAdi.Contains('/') || input.DosyaAdi.Contains('\\')
            || !input.DosyaAdi.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return (null, "Geçerli bir PDF dosya adı gerekli.");
        if (!string.IsNullOrWhiteSpace(input.DosyaUrl) && (!Uri.TryCreate(input.DosyaUrl, UriKind.Absolute, out var url)
            || url.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(url.UserInfo)))
            return (null, "Dosya URL'si HTTPS olmalı.");
        if (input.PdfBase64 is not { Length: > 0 and <= 14_000_000 }) return (null, "PDF verisi gerekli veya boyut sınırı aşıldı.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(input.PdfBase64); }
        catch (FormatException) { return (null, "PDF Base64 verisi geçersiz."); }
        if (bytes.Length is < 8 or > 10_000_000 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8)
            || !System.Text.Encoding.ASCII.GetString(bytes, Math.Max(0, bytes.Length - 1024), Math.Min(1024, bytes.Length)).Contains("%%EOF", StringComparison.Ordinal))
            return (null, "Geçerli bir PDF dosyası gerekli.");
        if (input.Imzalar == null || input.Imzalar.Count is 0 or > 10 || input.Imzalar.Any(x => x == null || x.SiraNo <= 0 || x.ImzaTarihi.Year < 2000 || x.ImzaTarihi > DateTimeOffset.UtcNow.AddMinutes(5))
            || input.Imzalar.Select(x => x.SiraNo).Distinct().Count() != input.Imzalar.Count)
            return (null, "İmzacı sıraları ve imza zamanları geçersiz.");
        return (bytes, null);
    }
}
