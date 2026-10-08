using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using Microsoft.AspNetCore.Identity;
using System.Globalization;
using System.Text;
using YetkiliServisGazAcma.Business.Services.Online;

namespace YetkiliServisGazAcma.API.Services;

public sealed class YkcTesisatApiService(AppDbContext context, UserManager<AppKullanici> userManager, OnlineCihazBilgileriClient online,
    SehirFirmaKoduService kodlar, IYkcSorguKaydiService sorguKayitlari)
{
    private readonly AppDbContext _context = context;
    private readonly UserManager<AppKullanici> _userManager = userManager;
    private readonly OnlineCihazBilgileriClient _onlineCihazBilgileriClient = online;
    private readonly SehirFirmaKoduService _sehirFirmaKoduService = kodlar;
    private readonly IYkcSorguKaydiService _sorguKayitlari = sorguKayitlari;

    public async Task<YkcTesisatSorguSonuc> SorgulaAsync(YkcTesisatSorguIstek? istek, AppKullanici kullanici, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(istek?.TesisatNo))
            return YkcTesisatSorguSonuc.Basarisiz("Tesisat no zorunludur.");

        if (string.IsNullOrWhiteSpace(istek.SozlesmeNo))
            return YkcTesisatSorguSonuc.Basarisiz("Sözleşme no zorunludur.");

        if (!long.TryParse(istek.TesisatNo.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var tesisatNo) || tesisatNo <= 0)
            return YkcTesisatSorguSonuc.Basarisiz("Tesisat no sıfırdan büyük ve yalnızca rakamlardan oluşmalıdır.");

        if (!long.TryParse(istek.SozlesmeNo.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var sozlesmeNo) || sozlesmeNo <= 0)
            return YkcTesisatSorguSonuc.Basarisiz("Sözleşme no sıfırdan büyük ve yalnızca rakamlardan oluşmalıdır.");

        var firma = kullanici.FirmaId.HasValue
            ? await _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId.Value && !x.SilindiMi, cancellationToken)
            : null;

        var sirket = kullanici.SirketId.HasValue
            ? await _context.Dag_Sirketler
                .FirstOrDefaultAsync(x => x.Id == kullanici.SirketId.Value && !x.SilindiMi, cancellationToken)
            : firma?.Sirket;

        var roller = await _userManager.GetRolesAsync(kullanici);
        var firmaKodu = OnlineFirmaKodu(firma, sirket);
        var firmaKoduAdaylari = FirmaKoduAdaylari(
            firmaKodu,
            roller.Contains("GenelSistemAdmin") || roller.Contains("SuperAdmin"));

        if (firmaKoduAdaylari.Count == 0)
            return YkcTesisatSorguSonuc.Basarisiz("Online servis firma kodu belirlenemedi. Lütfen aktif şirket/firma bağlamını kontrol edin.");

        OnlineCihazBilgileriSonuc? servisSonuc = null;
        string? kullanilanFirmaKodu = null;
        OnlineCihazBilgileriSonuc? ilkBasariliSonuc = null;
        string? ilkBasariliFirmaKodu = null;

        foreach (var adayFirmaKodu in firmaKoduAdaylari)
        {
            var adaySonuc = await _onlineCihazBilgileriClient.YSCihazBilgileriGetirAsync(
                adayFirmaKodu,
                tesisatNo,
                sozlesmeNo,
                cancellationToken);

            servisSonuc = adaySonuc;
            kullanilanFirmaKodu = adayFirmaKodu;

            if (adaySonuc.Basarili && ilkBasariliSonuc == null)
            {
                ilkBasariliSonuc = adaySonuc;
                ilkBasariliFirmaKodu = adayFirmaKodu;
            }

            if (adaySonuc.Basarili && adaySonuc.Cihazlar.Count > 0)
                break;
        }

        if ((servisSonuc == null || !servisSonuc.Basarili || servisSonuc.Cihazlar.Count == 0)
            && ilkBasariliSonuc != null)
        {
            servisSonuc = ilkBasariliSonuc;
            kullanilanFirmaKodu = ilkBasariliFirmaKodu;
        }

        if (servisSonuc == null || !servisSonuc.Basarili)
        {
            return YkcTesisatSorguSonuc.Basarisiz(
                servisSonuc?.HataMesaji ?? "Servisten bilgi alınamadı. Lütfen daha sonra yeniden sorgulayın.");
        }

        if (servisSonuc.TesisatNo != tesisatNo || servisSonuc.SozlesmeNo != sozlesmeNo
            || servisSonuc.Cihazlar.Any(c => c.TesisatNo.HasValue && c.TesisatNo != tesisatNo))
        {
            return YkcTesisatSorguSonuc.Basarisiz("Servisten gelen tesisat veya sözleşme bilgileri sorguyla eşleşmiyor. Lütfen yeniden sorgulayın.");
        }

        var cihazlar = servisSonuc.Cihazlar.Select(c => new YkcTesisatCihazDto
        {
            CihazKapasite = c.CihazKapasite?.ToString(CultureInfo.InvariantCulture) ?? "",
            CihazMarka = c.CihazMarka ?? "",
            CihazTipi = c.CihazTipi ?? "",
            CihazTipKodu = c.CihazTipKodu ?? "",
            ProjeNo = c.ProjeNo ?? "",
            TesisatNo = c.TesisatNo?.ToString(CultureInfo.InvariantCulture) ?? ""
        }).ToList();
        var izinliYeniCihazTipleri = cihazlar
            .Where(x => !string.IsNullOrWhiteSpace(x.CihazTipi))
            .GroupBy(x => x.CihazTipi!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.CihazTipKodu?.Trim()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);
        var il = firma?.FaaliyetIli ?? sirket?.Il ?? IlFromFirmaKodu(kullanilanFirmaKodu);

        foreach (var cihaz in cihazlar)
        {
            cihaz.SorguReferansi = await _sorguKayitlari.EkleAsync(kullanici.Id, new YkcTalepKaydetDto
            {
                FirmaId = firma?.Id,
                SirketId = sirket?.Id,
                FirmaKodu = kullanilanFirmaKodu,
                TesisatNo = tesisatNo.ToString(CultureInfo.InvariantCulture),
                SozlesmeNo = sozlesmeNo.ToString(CultureInfo.InvariantCulture),
                AboneNo = servisSonuc.CariKod?.ToString(CultureInfo.InvariantCulture),
                SayacNo = servisSonuc.SayacNo?.ToString(CultureInfo.InvariantCulture),
                ProjeNo = cihaz.ProjeNo,
                MusteriAdi = servisSonuc.CariAd,
                Adres = servisSonuc.Adres,
                Il = il,
                Bolge = il,
                EskiCihazTipi = cihaz.CihazTipi,
                EskiCihazTipiKodu = cihaz.CihazTipKodu,
                EskiMarka = cihaz.CihazMarka,
                EskiKapasite = cihaz.CihazKapasite,
                IzinliYeniCihazTipleri = new Dictionary<string, string?>(izinliYeniCihazTipleri, StringComparer.OrdinalIgnoreCase)
            }, cancellationToken);
            if (roller.Contains("SertifikaliFirma"))
            {
                cihaz.CihazMarka = null;
                cihaz.CihazKapasite = null;
                cihaz.ProjeNo = null;
                cihaz.CihazTipKodu = null;
            }
        }

        return new YkcTesisatSorguSonuc
        {
            Basarili = cihazlar.Count > 0,
            ManuelGirisSerbest = false,
            Mesaj = cihazlar.Count > 0
                ? "Tesisat ve cihaz bilgileri alindi."
                : "Tesisata ait cihaz bulunamadı. Talep için cihaz kaydı gerekiyor.",
            FirmaKodu = kullanilanFirmaKodu,
            TesisatNo = (servisSonuc.TesisatNo ?? tesisatNo).ToString(CultureInfo.InvariantCulture),
            SozlesmeNo = (servisSonuc.SozlesmeNo ?? sozlesmeNo).ToString(CultureInfo.InvariantCulture),
            AboneNo = servisSonuc.CariKod?.ToString(CultureInfo.InvariantCulture) ?? "",
            SayacNo = servisSonuc.SayacNo?.ToString(CultureInfo.InvariantCulture) ?? "",
            MusteriAdi = servisSonuc.CariAd ?? "",
            MusteriTelefon = "",
            Il = il,
            Ilce = "",
            Bolge = il,
            Adres = servisSonuc.Adres ?? "",
            Durum = cihazlar.Count > 0 ? "Cihaz bilgisi bulundu" : "Tesisat bulundu",
            Cihazlar = cihazlar
        };
    }

    public async Task<YkcCihazKarsilastirmaSonuc> KarsilastirAsync(YkcCihazKarsilastirmaIstek istek, AppKullanici kullanici, CancellationToken cancellationToken)
    {
        var cihaz = new YkcTalepKaydetDto
        {
            SorguReferansi = istek.SorguReferansi,
            TesisatNo = istek.TesisatNo,
            SozlesmeNo = istek.SozlesmeNo,
            YeniCihazTipi = istek.YeniCihazTipi,
            YeniMarka = istek.YeniMarka,
            YeniBacaTipi = istek.YeniBacaTipi,
            YeniKapasite = istek.YeniKapasite
        };
        if (!await _sorguKayitlari.UygulaAsync(kullanici.Id, cihaz, cancellationToken)
            || cihaz.FirmaId != kullanici.FirmaId
            || (kullanici.SirketId.HasValue && cihaz.SirketId != kullanici.SirketId)
            || !await _context.Ys_Firmalar.AnyAsync(x => x.Id == cihaz.FirmaId && !x.SilindiMi
                && x.SirketId == cihaz.SirketId, cancellationToken))
        {
            return new YkcCihazKarsilastirmaSonuc
            {
                Mesaj = "Cihaz bilgileri karşılaştırılamadı. Tesisatı yeniden sorgulayıp cihazı seçin."
            };
        }

        // Only advisory messages leave the API; the source snapshot stays private and unchanged.
        return new YkcCihazKarsilastirmaSonuc
        {
            Basarili = true,
            Uyarilar = YkcCihazUyumKurali.TalepOncesiUyarilar(cihaz)
        };
    }

    private string? OnlineFirmaKodu(Ys_Firma? firma, Dag_Sirket? sirket)
    {
        return _sehirFirmaKoduService.FirmaKodu(firma?.FaaliyetIli)
            ?? _sehirFirmaKoduService.FirmaKodu(firma?.Sirket?.Il)
            ?? _sehirFirmaKoduService.FirmaKodu(sirket?.Il)
            ?? FirmaKoduFromSirketAdi(firma?.Sirket?.SirketAdi)
            ?? FirmaKoduFromSirketAdi(sirket?.SirketAdi);
    }

    private List<string> FirmaKoduAdaylari(string? tercihliFirmaKodu, bool genelYetkili)
    {
        var adaylar = new List<string>();

        if (!string.IsNullOrWhiteSpace(tercihliFirmaKodu))
            adaylar.Add(tercihliFirmaKodu.Trim());

        if (genelYetkili && adaylar.Count == 0)
        {
            adaylar.AddRange(_sehirFirmaKoduService
                .TumKodlar()
                .Values
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()));
        }

        return adaylar
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string? IlFromFirmaKodu(string? firmaKodu)
    {
        if (string.IsNullOrWhiteSpace(firmaKodu))
            return null;

        return _sehirFirmaKoduService
            .TumKodlar()
            .FirstOrDefault(x => string.Equals(x.Value, firmaKodu.Trim(), StringComparison.OrdinalIgnoreCase))
            .Key;
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
            .ToArray();

        return new string(chars)
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant()
            .Replace('İ', 'I')
            .Replace('Ğ', 'G')
            .Replace('Ü', 'U')
            .Replace('Ş', 'S')
            .Replace('Ö', 'O')
            .Replace('Ç', 'C')
            .Replace(" ", "");
    }
}
