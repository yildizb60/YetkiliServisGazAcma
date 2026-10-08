using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class YetkiliServisPanelOkumaApiService(AppDbContext context, YetkiliServisIlkKurulumService ilkKurulum,
    ILogger<YetkiliServisPanelOkumaApiService> logger)
{
    private readonly AppDbContext _context = context;
    private readonly YetkiliServisIlkKurulumService _ilkKurulumService = ilkKurulum;

    public async Task<YsPanelDashboardDto> DashboardAsync(int firmaId, YsPanelDashboardFiltreDto? filtre = null)
    {
        var kurulum = await _ilkKurulumService.GetirAsync(firmaId);

        var firma = await FirmaDashboardQuery()
            .FirstOrDefaultAsync(x => x.Id == firmaId);

        var ayBasi = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var sonrakiAy = ayBasi.AddMonths(1);
        var buAy = await _context.Ys_DevreyeAlmalar
            .Where(x => x.FirmaId == firmaId
                && x.DevreyeAlmaTarihi >= ayBasi && x.DevreyeAlmaTarihi < sonrakiAy
                && !x.SilindiMi)
            .CountAsync();

        var durumSayilari = await _context.Ys_DevreyeAlmalar
            .Where(x => x.FirmaId == firmaId && !x.SilindiMi)
            .GroupBy(x => x.Durum)
            .Select(x => new { Durum = x.Key, Sayi = x.Count() })
            .ToDictionaryAsync(x => x.Durum, x => x.Sayi);
        var toplam = durumSayilari.Values.Sum();

        var sonIslemler = await _context.Ys_DevreyeAlmalar
            .Include(x => x.Marka)
            .Where(x => x.FirmaId == firmaId && !x.SilindiMi)
            .OrderByDescending(x => x.OlusturmaTarihi)
            .Take(5)
            .ToListAsync();

        int? uyariGun = null;
        var bugun = DateTime.Today;
        var onayli = firma?.YetkiBelgeleri?
            .Where(x => YetkiBelgesiService.GecerliMi(x, bugun))
            .OrderBy(x => x.YetkiBelgesiBitisTarihi)
            .FirstOrDefault();
        if (onayli != null)
        {
            var kalan = (onayli.YetkiBelgesiBitisTarihi.Date - bugun).Days;
            if (kalan >= 0)
                uyariGun = kalan;
        }

        var bildirim = await BildirimlerAsync(firmaId);

        var sonuc = new YsPanelDashboardDto
        {
            Firma = firma == null ? null : YsPanelFirmaDto.FromEntity(firma),
            BuAy = buAy,
            GecerliBelgeSayisi = firma?.YetkiBelgeleri?.Count(x => YetkiBelgesiService.GecerliMi(x, bugun)) ?? 0,
            BekleyenBelgeSayisi = firma?.YetkiBelgeleri?.Count(x => !x.SilindiMi
                && x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor && x.YetkiBelgesiBitisTarihi >= bugun) ?? 0,
            AktifMarkaSayisi = firma?.FirmaMarkalar?.Count(x => !x.SilindiMi && x.YetkiBitisTarihi.Date >= bugun
                && x.Marka is { AktifMi: true, SilindiMi: false }) ?? 0,
            AktifSubeSayisi = firma?.Subeler?.Count(x => !x.SilindiMi && x.AktifMi) ?? 0,
            Toplam = toplam,
            Bekleyen = durumSayilari.GetValueOrDefault(DevreyeAlmaDurumDegerleri.Bekliyor),
            Tamamlanan = durumSayilari.GetValueOrDefault(DevreyeAlmaDurumDegerleri.Tamamlandi),
            Iptal = durumSayilari.GetValueOrDefault(DevreyeAlmaDurumDegerleri.Iptal),
            SonIslemler = sonIslemler.Select(YsPanelDevreyeAlmaDto.FromEntity).ToList(),
            IlkKurulumZorunlu = kurulum.zorunluMu,
            IlkKurulumTamamlandi = kurulum.tamamlandiMi,
            IlkKurulumEksikler = kurulum.eksikler,
            YetkiBelgesiUyariGun = uyariGun,
            Bildirimler = bildirim.Bildirimler,
            BildirimSayisi = bildirim.BildirimSayisi
        };

        await TakvimiHazirlaAsync(sonuc, firmaId, filtre);
        return sonuc;
    }

    private async Task TakvimiHazirlaAsync(YsPanelDashboardDto sonuc, int firmaId, YsPanelDashboardFiltreDto? filtre)
    {
        var tarih = filtre?.TakvimTarih?.Date ?? DateTime.Today;
        sonuc.TakvimTarih = tarih.Year is < 2000 or > 2100 ? DateTime.Today : tarih;
        sonuc.TakvimGorunum = filtre?.TakvimGorunum is "gun" or "yil" ? filtre.TakvimGorunum : "ay";
        var ayBasi = new DateTime(sonuc.TakvimTarih.Year, sonuc.TakvimTarih.Month, 1);
        var sonrakiAy = ayBasi.AddMonths(1);

        try
        {
            var kayitlar = await _context.Ys_DevreyeAlmalar
                .TagWith("YetkiliServisPanel.Takvim")
                .AsNoTracking()
                .Include(x => x.Marka)
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi
                    && x.DevreyeAlmaTarihi >= ayBasi && x.DevreyeAlmaTarihi < sonrakiAy)
                .OrderByDescending(x => x.DevreyeAlmaTarihi)
                .ThenByDescending(x => x.Id)
                .ToListAsync();
            sonuc.TakvimIslemleri = kayitlar.Select(YsPanelDevreyeAlmaDto.FromEntity).ToList();
            sonuc.TakvimVerisiTam = true;
        }
        catch (Exception ex) when (ex is DbException or TimeoutException
            || ex.GetBaseException() is DbException or TimeoutException)
        {
            logger.LogWarning(ex, "Firma {FirmaId} icin {AyBasi} takvim verisi alinamadi.", firmaId, ayBasi);
            sonuc.TakvimIslemleri = sonuc.SonIslemler
                .Where(x => x.DevreyeAlmaTarihi >= ayBasi && x.DevreyeAlmaTarihi < sonrakiAy)
                .ToList();
            sonuc.TakvimVerisiTam = false;
        }
    }

    public async Task<YsPanelFirmaDto?> ProfilAsync(int firmaId)
    {
        var firma = await FirmaDashboardQuery().FirstOrDefaultAsync(x => x.Id == firmaId);
        return firma == null ? null : ProfilHazirla(firma, DateTime.Today);
    }

    public static YsPanelFirmaDto ProfilHazirla(Ys_Firma firma, DateTime tarih)
    {
        var bugun = tarih.Date;
        var belgeler = firma.YetkiBelgeleri?.Where(x => !x.SilindiMi)
            .OrderByDescending(x => x.OlusturmaTarihi).ToList() ?? new();
        var gecerli = belgeler.FirstOrDefault(x => YetkiBelgesiService.GecerliMi(x, bugun));
        var onayli = belgeler.FirstOrDefault(x => x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi);
        var bekleyen = belgeler.FirstOrDefault(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
            && x.YetkiBelgesiBitisTarihi.Date >= bugun);
        var gosterilen = gecerli ?? bekleyen ?? onayli ?? belgeler.FirstOrDefault();
        var sonuc = YsPanelFirmaDto.FromEntity(firma);
        sonuc.GecerliYetkiBelgesiId = gecerli?.Id;
        sonuc.GecerliYetkiBelgesiVar = gecerli != null;
        sonuc.GosterilenYetkiBelgesiId = gosterilen?.Id;
        sonuc.BekleyenYetkiBelgesiVar = bekleyen != null;
        sonuc.YetkiBelgesiDurumu = gecerli != null ? "Geçerli"
            : bekleyen != null ? "Onay Bekliyor"
            : gosterilen?.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
                && gosterilen.YetkiBelgesiBitisTarihi.Date < bugun ? "Süresi Doldu"
            : "Geçerli Belge Yok";
        return sonuc;
    }

    public async Task<YsPanelIlkKurulumDto> IlkKurulumAsync(int firmaId)
    {
        var kurulum = await _ilkKurulumService.GetirAsync(firmaId);
        if (firmaId <= 0)
        {
            return new YsPanelIlkKurulumDto
            {
                ZorunluMu = kurulum.zorunluMu,
                TamamlandiMi = kurulum.tamamlandiMi,
                Eksikler = kurulum.eksikler,
                HataMesaji = "Kullanici hesabi bir firmaya bagli olmadigi icin ilk kurulum yapilamadi."
            };
        }

        var firma = await FirmaDashboardQuery()
            .FirstOrDefaultAsync(x => x.Id == firmaId);
        if (firma == null)
        {
            return new YsPanelIlkKurulumDto
            {
                ZorunluMu = kurulum.zorunluMu,
                TamamlandiMi = kurulum.tamamlandiMi,
                Eksikler = kurulum.eksikler,
                HataMesaji = "Firma kaydi bulunamadi. Lutfen yonetici ile gorusun."
            };
        }

        var tumMarkalar = await _context.Ys_Markalar
            .Where(x => !x.SilindiMi && x.AktifMi)
            .OrderBy(x => x.MarkaAdi)
            .ToListAsync();

        var tumKategoriler = await _context.UrunKategoriler
            .Where(x => !x.SilindiMi && x.AktifMi)
            .OrderBy(x => x.SiraNo)
            .ThenBy(x => x.Ad)
            .ToListAsync();

        var seciliMarkaIds = firma.FirmaMarkalar?
            .Where(x => !x.SilindiMi)
            .Select(x => x.MarkaId)
            .ToList() ?? new List<int>();

        var seciliKategoriIds = firma.FirmaKategoriler?
            .Where(x => !x.SilindiMi && tumKategoriler.Any(k => k.Id == x.KategoriId))
            .Select(x => x.KategoriId)
            .ToList() ?? new List<int>();

        var aktifSubeSayisi = firma.Subeler?
            .Count(x => !x.SilindiMi) ?? 0;

        var yetkiBelgesiVar = firma.YetkiBelgeleri?
            .Any(x => !x.SilindiMi) ?? false;

        var onayliYetkiBelgesiVar = firma.YetkiBelgeleri?
            .Any(x => !x.SilindiMi && x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi) ?? false;

        return new YsPanelIlkKurulumDto
        {
            Firma = YsPanelFirmaDto.FromEntity(firma),
            TumMarkalar = tumMarkalar.Select(YsPanelMarkaDto.FromEntity).ToList(),
            TumKategoriler = tumKategoriler.Select(YsPanelUrunKategoriDto.FromEntity).ToList(),
            SeciliMarkaIds = seciliMarkaIds,
            SeciliKategoriIds = seciliKategoriIds,
            AktifSubeSayisi = aktifSubeSayisi,
            YetkiBelgesiVar = yetkiBelgesiVar,
            OnayliYetkiBelgesiVar = onayliYetkiBelgesiVar,
            ZorunluMu = kurulum.zorunluMu,
            TamamlandiMi = kurulum.tamamlandiMi,
            Eksikler = kurulum.eksikler
        };
    }

    public async Task<YsPanelMarkalarDto?> MarkalarAsync(int firmaId)
    {
        var firma = await FirmaDashboardQuery()
            .FirstOrDefaultAsync(x => x.Id == firmaId);

        if (firma == null)
            return null;

        var tumMarkalar = await _context.Ys_Markalar
            .Where(x => !x.SilindiMi && x.AktifMi)
            .OrderBy(x => x.MarkaAdi)
            .ToListAsync();

        var firmaMarkalar = await _context.Ys_FirmaMarkalar
            .Include(x => x.Marka)
            .Where(x => x.FirmaId == firmaId && !x.SilindiMi)
            .OrderBy(x => x.Marka!.MarkaAdi)
            .ToListAsync();

        return new YsPanelMarkalarDto
        {
            Firma = YsPanelFirmaDto.FromEntity(firma),
            TumMarkalar = tumMarkalar.Select(YsPanelMarkaDto.FromEntity).ToList(),
            FirmaMarkalar = firmaMarkalar.Select(YsPanelFirmaMarkaDto.FromEntity).ToList(),
            SeciliMarkaIds = firmaMarkalar.Select(x => x.MarkaId).ToList()
        };
    }

    private IQueryable<Ys_Firma> FirmaDashboardQuery()
    {
        return _context.Ys_Firmalar
            .AsSplitQuery()
            .Include(x => x.Sirket)
            .Include(x => x.FirmaMarkalar!)
                .ThenInclude(x => x.Marka)
            .Include(x => x.FirmaKategoriler!)
                .ThenInclude(x => x.Kategori)
            .Include(x => x.Subeler)
            .Include(x => x.YetkiBelgeleri)
            .Where(x => !x.SilindiMi);
    }

    public async Task<YsPanelBildirimDto> BildirimlerAsync(int firmaId)
    {
        var bildirimler = new List<string>();

        var firma = await _context.Ys_Firmalar
            .Include(x => x.YetkiBelgeleri)
            .FirstOrDefaultAsync(x => x.Id == firmaId);

        var bugun = DateTime.Now.Date;
        var onayli = firma?.YetkiBelgeleri?
            .Where(x => YetkiBelgesiService.GecerliMi(x, bugun))
            .OrderBy(x => x.YetkiBelgesiBitisTarihi)
            .FirstOrDefault();

        var bekleyenVar = firma?.YetkiBelgeleri?.Any(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
            && x.YetkiBelgesiBitisTarihi.Date >= bugun && !x.SilindiMi) ?? false;
        if (onayli != null)
        {
            bildirimler.Add("Yetki belgeniz onaylandi. Cihaz devreye alabilirsiniz.");
            var kalan = (onayli.YetkiBelgesiBitisTarihi.Date - bugun).Days;
            if (kalan <= 30)
                bildirimler.Add($"Yetki belgenizin bitmesine {kalan} gun kaldi. Lutfen yenileyin.");
        }

        if (bekleyenVar)
            bildirimler.Add("Yetki belgeniz onay bekliyor. Yetkili onayladiktan sonra islem yapabilirsiniz.");

        var son7Gun = DateTime.Now.AddDays(-7);
        var sonDevreye = await _context.Ys_DevreyeAlmalar
            .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.OlusturmaTarihi >= son7Gun)
            .CountAsync();
        if (sonDevreye > 0)
            bildirimler.Add($"Son 7 gunde {sonDevreye} cihaz devreye alindi.");

        var sonSube = await _context.Ys_Subeler
            .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.OlusturmaTarihi >= son7Gun)
            .CountAsync();
        if (sonSube > 0)
            bildirimler.Add($"Son 7 gunde {sonSube} sube kaydi eklendi.");

        return new YsPanelBildirimDto
        {
            Bildirimler = bildirimler,
            BildirimSayisi = bildirimler.Count
        };
    }
}
