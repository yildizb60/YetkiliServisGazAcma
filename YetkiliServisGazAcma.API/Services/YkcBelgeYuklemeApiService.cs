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
        AppKullanici kullanici, bool genelYetkili, int? sirketId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (talepId <= 0)
            return YkcIslemSonuc.HataliSonuc("Form yükleme için talep id zorunludur.");

        if (dosya == null || dosya.Length == 0)
            return YkcIslemSonuc.HataliSonuc("Yüklenecek form dosyası zorunludur.");

        if (!YkcFormDosyasiGecerliMi(dosya.FileName, dosya.ContentType, icerikTipiZorunlu: true))
            return YkcIslemSonuc.HataliSonuc("Sadece PDF, JPG veya PNG form dosyasi yuklenebilir.");

        if (!await YkcFormDosyaIcerigiGecerliMiAsync(dosya, cancellationToken))
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
        // The original name is display metadata only, never part of the storage path.
        var uzanti = dosya.ContentType.Trim().ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => throw new InvalidOperationException("Doğrulanmamış dosya türü.")
        };
        var kayitAdi = $"{Guid.NewGuid():N}{uzanti}";
        var fizikselYol = Path.Combine(klasor, kayitAdi);

        var tamamlandi = false;
        var dosyaOlusturuldu = false;
        try
        {
            string belgeHash;
            await using (var stream = new FileStream(fizikselYol, FileMode.CreateNew, FileAccess.ReadWrite,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                dosyaOlusturuldu = true;
                await dosya.CopyToAsync(stream, cancellationToken);
                stream.Position = 0;
                belgeHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            }

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
            }, kullanici, genelYetkili, sirketId, cancellationToken);

            tamamlandi = sonuc.Basarili;
            return sonuc;
        }
        finally
        {
            if (dosyaOlusturuldu && !tamamlandi && PrivateDocumentStorage.IsInRoot(fizikselYol, kok)
                && File.Exists(fizikselYol))
                File.Delete(fizikselYol);
        }
    }

    private static string GuvenliDosyaAdi(string dosyaAdi)
    {
        var sadeceAd = Path.GetFileName(dosyaAdi.Replace('\\', '/'));
        foreach (var karakter in Path.GetInvalidFileNameChars())
            sadeceAd = sadeceAd.Replace(karakter, '_');

        return string.IsNullOrWhiteSpace(sadeceAd) ? "ykc-form" : sadeceAd;
    }

    private string PrivateYkcBelgeRoot() => PrivateDocumentStorage.Root(_environment, _configuration, "ykc-belgeler");

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

    private static async Task<bool> YkcFormDosyaIcerigiGecerliMiAsync(IFormFile dosya, CancellationToken cancellationToken)
    {
        var uzanti = Path.GetExtension(dosya.FileName).ToLowerInvariant();
        var header = new byte[8];
        await using var stream = dosya.OpenReadStream();
        var okunan = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false,
            cancellationToken: cancellationToken);

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
