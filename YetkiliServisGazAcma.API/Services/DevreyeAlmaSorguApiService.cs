using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YetkiliServisGazAcma.Business.Services.Online;

namespace YetkiliServisGazAcma.API.Services;

public sealed class DevreyeAlmaSorguApiService(AppDbContext context, OnlineCihazBilgileriClient online, SehirFirmaKoduService kodlar,
    YetkiliServisIlkKurulumService ilkKurulum, DevreyeAlmaYetkiDogrulamaService yetki)
{
    private readonly AppDbContext _context = context;
    private readonly OnlineCihazBilgileriClient _onlineCihazBilgileriClient = online;
    private readonly SehirFirmaKoduService _sehirFirmaKoduService = kodlar;
    private readonly YetkiliServisIlkKurulumService _ilkKurulumService = ilkKurulum;
    private readonly DevreyeAlmaYetkiDogrulamaService _yetki = yetki;

    public async Task<YsTesisatSorguSonucDto> SorgulaAsync(YsTesisatSorguDto? dto, AppKullanici kullanici, CancellationToken cancellationToken)
    {
        var kurulum = await _ilkKurulumService.GetirAsync(kullanici.FirmaId!.Value);
        if (kurulum.zorunluMu && !kurulum.tamamlandiMi)
        {
            return new YsTesisatSorguSonucDto
            {
                Basarili = false,
                Mesaj = "Ilk kurulum tamamlanmadan islem yapilamaz. Lutfen once sube, marka ve kategori secimini tamamlayin."
            };
        }

        if (string.IsNullOrWhiteSpace(dto?.TesistatNo))
            return new YsTesisatSorguSonucDto { Basarili = false, Mesaj = "Tesisat no bos olamaz." };

        if (string.IsNullOrWhiteSpace(dto.SozlesmeNo))
            return new YsTesisatSorguSonucDto { Basarili = false, Mesaj = "Sozlesme no bos olamaz." };

        if (!long.TryParse(dto.TesistatNo.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tesisatNo))
            return new YsTesisatSorguSonucDto { Basarili = false, Mesaj = "Tesisat no sayisal olmalidir." };

        if (!long.TryParse(dto.SozlesmeNo.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sozlesmeNo))
            return new YsTesisatSorguSonucDto { Basarili = false, Mesaj = "Sozlesme no sayisal olmalidir." };

        var firma = await FirmaQuery()
            .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId!.Value);
        if (firma == null)
            return new YsTesisatSorguSonucDto { Basarili = false, Mesaj = "Yetkili servis firma kaydı bulunamadı." };
        if (!firma.AktifMi)
            return new YsTesisatSorguSonucDto { Basarili = false, Mesaj = "Firma kaydınız pasif olduğu için cihaz sorgulanamaz." };

        var firmaKodu = OnlineFirmaKodu(firma);
        var servisSonuc = await _onlineCihazBilgileriClient.YSCihazBilgileriGetirAsync(
            firmaKodu,
            tesisatNo,
            sozlesmeNo,
            cancellationToken);

        if (!servisSonuc.Basarili)
        {
            return new YsTesisatSorguSonucDto
            {
                Basarili = false,
                Mesaj = servisSonuc.HataMesaji ?? "Cihaz bilgileri alinamadi."
            };
        }

        if ((servisSonuc.TesisatNo.HasValue && servisSonuc.TesisatNo.Value != tesisatNo)
            || (servisSonuc.SozlesmeNo.HasValue && servisSonuc.SozlesmeNo.Value != sozlesmeNo))
        {
            return new YsTesisatSorguSonucDto
            {
                Basarili = false,
                Mesaj = "Servis yanıtındaki tesisat veya sözleşme numarası sorguyla eşleşmedi. Kayıt oluşturulmadı."
            };
        }

        var cariKod = servisSonuc.CariKod?.ToString(CultureInfo.InvariantCulture) ?? "";
        var kaynakTesisatNo = (servisSonuc.TesisatNo ?? tesisatNo).ToString(CultureInfo.InvariantCulture);
        var kaynakSozlesmeNo = (servisSonuc.SozlesmeNo ?? sozlesmeNo).ToString(CultureInfo.InvariantCulture);
        var cihazlar = new List<YsTesisatCihazDto>();
        var kaynakAnahtarlari = new List<string>();
        var ayniCihazSayilari = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cihaz in servisSonuc.Cihazlar)
        {
            var kaynak = new YsDevreyeAlmaKaynak
            {
                TesisatNo = kaynakTesisatNo,
                SozlesmeNo = kaynakSozlesmeNo,
                AboneNo = cariKod,
                MusteriAdi = servisSonuc.CariAd ?? "",
                Adres = servisSonuc.Adres ?? "",
                CihazTipi = cihaz.CihazTipi ?? "",
                CihazMarka = cihaz.CihazMarka ?? "",
                CihazKapasite = cihaz.CihazKapasite?.ToString(CultureInfo.InvariantCulture) ?? ""
            };
            var imza = YsDevreyeAlmaKaynak.CihazImzasi(kaynak, cihaz.ProjeNo, cihaz.CihazTipKodu);
            ayniCihazSayilari.TryGetValue(imza, out var ayniCihazSirasi);
            ayniCihazSayilari[imza] = ayniCihazSirasi + 1;
            var referans = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var kaynakAnahtari = YsDevreyeAlmaKaynak.CihazAnahtari(
                firma.SirketId, kaynak, cihaz.ProjeNo, cihaz.CihazTipKodu, ayniCihazSirasi);
            kaynakAnahtarlari.Add(kaynakAnahtari);
            _context.Ys_DevreyeAlmaSorguKayitlari.Add(new Ys_DevreyeAlmaSorguKaydi
            {
                Referans = referans,
                KullaniciId = kullanici.Id,
                FirmaId = kullanici.FirmaId!.Value,
                DagitimSirketiId = firma.SirketId,
                KaynakJson = JsonSerializer.Serialize(kaynak),
                KaynakCihazAnahtari = kaynakAnahtari,
                GecerlilikTarihi = DateTime.UtcNow.AddMinutes(20)
            });
            cihazlar.Add(new YsTesisatCihazDto
            {
                SorguReferansi = referans,
                CihazMarka = kaynak.CihazMarka,
                CihazTipi = kaynak.CihazTipi,
                CihazKapasite = kaynak.CihazKapasite
            });
        }

        await _context.Database.ExecuteSqlRawAsync(
            "DELETE TOP (200) FROM dbo.Ys_DevreyeAlmaSorguKayitlari WHERE GecerlilikTarihi <= SYSUTCDATETIME() AND DevreyeAlmaId IS NULL");

        var tamamlananAnahtarlar = await _context.Ys_DevreyeAlmalar
            .Where(x => !x.SilindiMi
                && x.TesistatNo == kaynakTesisatNo && x.KaynakCihazAnahtari != null)
            .Select(x => x.KaynakCihazAnahtari!)
            .ToListAsync();
        var tamamlananlar = tamamlananAnahtarlar.ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < cihazlar.Count; index++)
            cihazlar[index].KaydedildiMi = tamamlananlar.Contains(kaynakAnahtarlari[index]);

        await _context.SaveChangesAsync();
        return new YsTesisatSorguSonucDto
        {
            Basarili = true,
            TesistatNo = kaynakTesisatNo,
            SozlesmeNo = kaynakSozlesmeNo,
            AboneNo = cariKod,
            SayacNo = servisSonuc.SayacNo?.ToString(CultureInfo.InvariantCulture) ?? "",
            MusteriAdi = servisSonuc.CariAd ?? "",
            // Online servis bu akista TC kimlik numarasi dondurmuyor.
            // Cari kodu TC alani olarak etiketlemek veri dogrulugunu bozar.
            MusteriTcNo = "",
            MusteriTelefon = "",
            Adres = servisSonuc.Adres ?? "",
            UygunlukBelgeNo = "",
            UygunlukTarihi = "",
            Durum = servisSonuc.Cihazlar.Count > 0 ? "Cihaz bilgisi bulundu" : "Tesisat bulundu",
            Cihazlar = cihazlar
        };
    }

    public async Task<YsMarkaKontrolSonucDto> MarkaKontrolAsync(YsMarkaKontrolDto? dto, AppKullanici kullanici)
    {
        var kurulum = await _ilkKurulumService.GetirAsync(kullanici.FirmaId!.Value);
        if (kurulum.zorunluMu && !kurulum.tamamlandiMi)
            return new YsMarkaKontrolSonucDto { Yetkili = false, Mesaj = "Ilk kurulum tamamlanmadan islem yapilamaz." };

        if (string.IsNullOrWhiteSpace(dto?.SorguReferansi) || dto.SorguReferansi.Length != 64)
            return new YsMarkaKontrolSonucDto { Yetkili = false, Mesaj = "Önce tesisatı sorgulayıp servisten gelen cihazı seçin." };

        var sorguKaydi = await _context.Ys_DevreyeAlmaSorguKayitlari.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Referans == dto.SorguReferansi
                && x.KullaniciId == kullanici.Id && x.FirmaId == kullanici.FirmaId!.Value
                && x.GecerlilikTarihi > DateTime.UtcNow && x.DevreyeAlmaId == null);
        if (sorguKaydi == null)
            return new YsMarkaKontrolSonucDto { Yetkili = false, Mesaj = "Cihaz sorgusu geçersiz, süresi dolmuş veya bu cihaz zaten kaydedilmiş. Tesisatı yeniden sorgulayın." };

        if (!await _context.Ys_Firmalar.AnyAsync(x => x.Id == kullanici.FirmaId!.Value
            && !x.SilindiMi && x.AktifMi && x.SirketId == sorguKaydi.DagitimSirketiId))
            return new YsMarkaKontrolSonucDto { Yetkili = false, Mesaj = "Firma kaydınız aktif değil veya firma kapsamı değişmiş. Tesisatı yeniden sorgulayın." };

        YsDevreyeAlmaKaynak? kaynak;
        try { kaynak = JsonSerializer.Deserialize<YsDevreyeAlmaKaynak>(sorguKaydi.KaynakJson); }
        catch (JsonException) { kaynak = null; }
        if (kaynak == null || string.IsNullOrWhiteSpace(kaynak.CihazTipi))
            return new YsMarkaKontrolSonucDto { Yetkili = false, Mesaj = "Kaynak cihaz bilgisi doğrulanamadı. Tesisatı yeniden sorgulayın." };

        if (!await _yetki.FirmaKategoriYetkisiVarAsync(kullanici.FirmaId!.Value, kaynak.CihazTipi))
            return new YsMarkaKontrolSonucDto { Yetkili = false, Mesaj = $"{kaynak.CihazTipi} cihaz tipinde işlem yetkiniz yok." };

        if (string.IsNullOrWhiteSpace(kaynak.CihazMarka))
            return new YsMarkaKontrolSonucDto { Yetkili = false, Mesaj = "Cihaz marka bilgisi bulunmadığından devreye alma yapılamaz." };

        var marka = await _yetki.MarkaBulAsync(kaynak.CihazMarka);
        if (marka == null)
        {
            return new YsMarkaKontrolSonucDto
            {
                Yetkili = false,
                Mesaj = $"{kaynak.CihazMarka} markasında işlem yetkiniz yok."
            };
        }

        var yetkiVar = await _yetki.FirmaMarkaYetkisiVarAsync(kullanici.FirmaId!.Value, marka.Id);
        if (!yetkiVar)
            return new YsMarkaKontrolSonucDto { Yetkili = false, Mesaj = $"{marka.MarkaAdi} markasında işlem yetkiniz yok." };

        return new YsMarkaKontrolSonucDto
        {
            Yetkili = true,
            MarkaId = marka.Id,
            MarkaAdi = marka.MarkaAdi
        };
    }

    private IQueryable<Ys_Firma> FirmaQuery()
    {
        return _context.Ys_Firmalar
            .Include(x => x.Sirket)
            .Where(x => !x.SilindiMi);
    }

    private string? OnlineFirmaKodu(Ys_Firma? firma)
    {
        return _sehirFirmaKoduService.FirmaKodu(firma?.FaaliyetIli)
            ?? _sehirFirmaKoduService.FirmaKodu(firma?.Sirket?.Il)
            ?? FirmaKoduFromSirketAdi(firma?.Sirket?.SirketAdi);
    }

    private static string? FirmaKoduFromSirketAdi(string? sirketAdi)
    {
        if (string.IsNullOrWhiteSpace(sirketAdi))
            return null;

        var normalized = NormalizeFirmaText(sirketAdi);
        if (normalized.Contains("CORUM") || normalized.Contains("CORUMGAZ"))
            return "CORUMGAZ";
        if (normalized.Contains("KARGAZ") || normalized.Contains("KASTAMONU") || normalized.Contains("KARABUK"))
            return "KARGAZ";
        if (normalized.Contains("SURMELI") || normalized.Contains("SURMELIGAZ") || normalized.Contains("YOZGAT"))
            return "SURMELIGAZ";
        if (normalized.Contains("YALOVA"))
            return "MARMARAGAZ_YALOVA";
        if (normalized.Contains("CORLU") || normalized.Contains("TEKIRDAG"))
            return "MARMARAGAZ_CORLU";

        return normalized;
    }

    private static string NormalizeFirmaText(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var chars = normalized
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_')
            .ToArray();

        return new string(chars)
            .Normalize(NormalizationForm.FormC)
            .Trim('_');
    }
}
