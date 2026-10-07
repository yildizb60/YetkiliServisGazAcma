using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using System.Security.Cryptography;

namespace YetkiliServisGazAcma.API.Services;

public sealed class YkcBelgeYuklemeApiService(YkcTalepService talepService, IWebHostEnvironment environment, IConfiguration? configuration = null)
{
    private readonly YkcTalepService _ykcTalepService = talepService;
    private readonly IWebHostEnvironment _environment = environment;
    private readonly IConfiguration? _configuration = configuration;

    public async Task<YkcIslemSonuc> KaydetAsync(YkcDosyaKaydetDto dto, AppKullanici kullanici, bool genelYetkili, int? sirketId)
    {
        if (!YkcFormDosyasiGecerliMi(dto.DosyaAdi ?? dto.DosyaYolu, dto.IcerikTipi, icerikTipiZorunlu: false))
            return YkcIslemSonuc.HataliSonuc("Sadece PDF, JPG veya PNG form dosyasi kaydedilebilir.");

        var dosyaTuru = string.IsNullOrWhiteSpace(dto.DosyaTuru)
            ? YkcFormDosyaTuruDegerleri.TeknikEk
            : dto.DosyaTuru.Trim();

        if (!ElleYuklenebilirBelgeTuruMu(dosyaTuru))
            return YkcIslemSonuc.HataliSonuc("Bu belge türü kullanıcı yüklemesine açık değildir.");

        if (!string.Equals(dto.DepolamaTuru, YkcDepolamaTuruDegerleri.Private, StringComparison.OrdinalIgnoreCase)
            || !PrivateDepolamaAnahtariGecerliMi(dto.DosyaYolu, dto.TalepId))
        {
            return YkcIslemSonuc.HataliSonuc("Dosya yalnızca YKC private storage anahtarıyla kaydedilebilir.");
        }

        dto.DosyaTuru = dosyaTuru;
        dto.DepolamaTuru = YkcDepolamaTuruDegerleri.Private;
        var sonuc = await _ykcTalepService.DosyaEkleAsync(dto, kullanici, genelYetkili, sirketId);
        return sonuc;
    }

    public async Task<YkcIslemSonuc> YukleAsync(int talepId, string? istenenDosyaTuru, IFormFile? dosya,
        AppKullanici kullanici, bool genelYetkili, int? sirketId)
    {
        if (dosya == null || dosya.Length == 0)
            return YkcIslemSonuc.HataliSonuc("Yüklenecek form dosyası zorunludur.");

        if (!YkcFormDosyasiGecerliMi(dosya.FileName, dosya.ContentType, icerikTipiZorunlu: true))
            return YkcIslemSonuc.HataliSonuc("Sadece PDF, JPG veya PNG form dosyasi yuklenebilir.");

        if (!await YkcFormDosyaIcerigiGecerliMiAsync(dosya))
            return YkcIslemSonuc.HataliSonuc("Dosya içeriği seçilen PDF veya görsel türüyle uyuşmuyor.");

        var dosyaTuru = string.IsNullOrWhiteSpace(istenenDosyaTuru)
            ? YkcFormDosyaTuruDegerleri.TeknikEk
            : istenenDosyaTuru.Trim();

        if (!ElleYuklenebilirBelgeTuruMu(dosyaTuru))
            return YkcIslemSonuc.HataliSonuc("Bu belge türü kullanıcı yüklemesine açık değildir.");

        var kok = PrivateYkcBelgeRoot();
        var klasor = Path.Combine(kok, talepId.ToString());
        Directory.CreateDirectory(klasor);

        var dosyaAdi = GuvenliDosyaAdi(dosya.FileName);
        var kayitAdi = $"{Guid.NewGuid():N}_{dosyaAdi}";
        var fizikselYol = Path.Combine(klasor, kayitAdi);

        var tamamlandi = false;
        try
        {
            await using (var stream = System.IO.File.Create(fizikselYol))
            {
                await dosya.CopyToAsync(stream);
            }

            var belgeHash = await DosyaHashAsync(fizikselYol);
            var depolamaAnahtari = $"ykc/{talepId}/{kayitAdi}";
            var sonuc = await _ykcTalepService.DosyaEkleAsync(new YkcDosyaKaydetDto
            {
                TalepId = talepId,
                DosyaTuru = dosyaTuru,
                DosyaAdi = dosyaAdi,
                DosyaYolu = depolamaAnahtari,
                IcerikTipi = dosya.ContentType,
                DosyaBoyutu = dosya.Length,
                DepolamaTuru = YkcDepolamaTuruDegerleri.Private,
                BelgeHash = belgeHash
            }, kullanici, genelYetkili, sirketId);

            tamamlandi = sonuc.Basarili;
            return sonuc;
        }
        finally
        {
            if (!tamamlandi && PrivateDocumentStorage.IsInRoot(fizikselYol, kok)
                && File.Exists(fizikselYol))
                File.Delete(fizikselYol);
        }
    }

    private static string GuvenliDosyaAdi(string dosyaAdi)
    {
        var sadeceAd = Path.GetFileName(dosyaAdi);
        foreach (var karakter in Path.GetInvalidFileNameChars())
            sadeceAd = sadeceAd.Replace(karakter, '_');

        return string.IsNullOrWhiteSpace(sadeceAd) ? "ykc-form" : sadeceAd;
    }

    private string PrivateYkcBelgeRoot() => PrivateDocumentStorage.Root(_environment, _configuration, "ykc-belgeler");

    private static async Task<string> DosyaHashAsync(string fizikselYol)
    {
        await using var stream = System.IO.File.OpenRead(fizikselYol);
        var bytes = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(bytes);
    }

    private static bool ElleYuklenebilirBelgeTuruMu(string dosyaTuru)
    {
        return dosyaTuru == YkcFormDosyaTuruDegerleri.TeknikEk;
    }

    private static bool PrivateDepolamaAnahtariGecerliMi(string? dosyaYolu, int talepId)
    {
        var yol = dosyaYolu?.Trim().Replace('\\', '/').TrimStart('/');
        return !string.IsNullOrWhiteSpace(yol)
            && !yol.Contains("..", StringComparison.Ordinal)
            && yol.StartsWith($"ykc/{talepId}/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool YkcFormDosyasiGecerliMi(string? dosyaAdi, string? icerikTipi, bool icerikTipiZorunlu)
    {
        var uzanti = Path.GetExtension(dosyaAdi ?? string.Empty).ToLowerInvariant();
        var izinliTipler = uzanti switch
        {
            ".pdf" => new[] { "application/pdf" },
            ".jpg" or ".jpeg" => new[] { "image/jpeg" },
            ".png" => new[] { "image/png" },
            _ => Array.Empty<string>()
        };

        if (izinliTipler.Length == 0)
            return false;

        if (string.IsNullOrWhiteSpace(icerikTipi))
            return !icerikTipiZorunlu;

        return izinliTipler.Contains(icerikTipi.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<bool> YkcFormDosyaIcerigiGecerliMiAsync(IFormFile dosya)
    {
        var uzanti = Path.GetExtension(dosya.FileName).ToLowerInvariant();
        var header = new byte[8];
        await using var stream = dosya.OpenReadStream();
        var okunan = await stream.ReadAsync(header.AsMemory(0, header.Length));

        return uzanti switch
        {
            ".pdf" => okunan >= 4
                && header[0] == 0x25 && header[1] == 0x50
                && header[2] == 0x44 && header[3] == 0x46,
            ".jpg" or ".jpeg" => okunan >= 3
                && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => okunan >= 8
                && header[0] == 0x89 && header[1] == 0x50
                && header[2] == 0x4E && header[3] == 0x47
                && header[4] == 0x0D && header[5] == 0x0A
                && header[6] == 0x1A && header[7] == 0x0A,
            _ => false
        };
    }
}
