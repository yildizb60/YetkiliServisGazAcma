using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class YetkiBelgesiOkumaApiService(AppDbContext context, YetkiBelgesiService service)
{
    private readonly AppDbContext _context = context;
    private readonly YetkiBelgesiService _service = service;

    public async Task<IEnumerable<YetkiBelgesiDto>> FirmaListeAsync(int firmaId)
    {
        var belgeler = await _service.FirmaninYetkiBelgeleri(firmaId);

        return belgeler.Select(x => new YetkiBelgesiDto
        {
            Id = x.Id,
            FirmaId = x.FirmaId,
            DosyaYolu = string.IsNullOrWhiteSpace(x.DosyaYolu) ? null : YetkiBelgesiService.GuvenliDosyaLinki(x.Id),
            Durum = x.Durum,
            OlusturmaTarihi = x.OlusturmaTarihi,
            YetkiBelgesiBaslangicTarihi = x.YetkiBelgesiBaslangicTarihi,
            YetkiBelgesiBitisTarihi = x.YetkiBelgesiBitisTarihi,
            OnayTarihi = x.OnayTarihi,
            OnaylayanKullanici = x.OnaylayanKullanici,
            RedGerekce = x.RedGerekce
        });
    }

    public async Task<YetkiBelgesiFirmaEkraniDto?> FirmaEkraniAsync(int firmaId)
    {
        var firma = await _context.Ys_Firmalar
            .Include(x => x.YetkiBelgeleri)
            .FirstOrDefaultAsync(x => x.Id == firmaId && !x.SilindiMi);

        if (firma == null)
            return null;

        var belgeler = await _service.FirmaninYetkiBelgeleri(firmaId);
        var bildirimler = await FirmaBildirimleriAsync(firmaId, firma);

        return new YetkiBelgesiFirmaEkraniDto
        {
            Firma = new YetkiBelgesiFirmaDto
            {
                Id = firma.Id,
                FirmaAdi = firma.FirmaAdi,
                YetkiliKisi = firma.YetkiliKisi,
                VergiNo = firma.VergiNo,
                FaaliyetIli = firma.FaaliyetIli
            },
            Belgeler = belgeler.Select(MapYetkiBelgesi).ToList(),
            Bildirimler = bildirimler
        };
    }

    public async Task<IEnumerable<YetkiBelgesiDto>> OnayBekleyenlerAsync(int? sirketId)
    {
        var yetkiBelgeleri = await _service.OnayBekleyenler(sirketId);

        return yetkiBelgeleri.Select(MapYetkiBelgesi);
    }

    public async Task<YetkiBelgesiOnayEkraniDto> OnayEkraniAsync(int? sirketId)
    {
        var sorgu = _context.Ys_YetkiBelgeleri
            .Include(x => x.Firma)
                .ThenInclude(x => x!.Sirket)
            .Where(x => !x.SilindiMi
                && x.Firma != null
                && !x.Firma.SilindiMi
                && (sirketId == null || x.Firma.SirketId == sirketId));

        var bugun = DateTime.Today;
        var bekleyenler = await sorgu
            .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
                && x.YetkiBelgesiBitisTarihi >= bugun)
            .OrderByDescending(x => x.OlusturmaTarihi)
            .ToListAsync();

        var suresiDolanlar = await sorgu
            .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
                && x.YetkiBelgesiBitisTarihi < bugun)
            .OrderByDescending(x => x.YetkiBelgesiBitisTarihi)
            .ToListAsync();

        var onaylananlar = await sorgu
            .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi)
            .OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi)
            .Take(100)
            .ToListAsync();

        var reddedilenler = await sorgu
            .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Reddedildi)
            .OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi)
            .Take(100)
            .ToListAsync();

        return new YetkiBelgesiOnayEkraniDto
        {
            Bekleyenler = bekleyenler.Select(MapYetkiBelgesi).ToList(),
            SuresiDolanlar = suresiDolanlar.Select(MapYetkiBelgesi).ToList(),
            Onaylananlar = onaylananlar.Select(MapYetkiBelgesi).ToList(),
            Reddedilenler = reddedilenler.Select(MapYetkiBelgesi).ToList()
        };
    }

    private static YetkiBelgesiDto MapYetkiBelgesi(Ys_YetkiBelgesi x)
    {
        return new YetkiBelgesiDto
        {
            Onaylanabilir = YetkiBelgesiService.OnaylanabilirMi(x, DateTime.Today),
            Id = x.Id,
            FirmaId = x.FirmaId,
            FirmaAdi = x.Firma?.FirmaAdi,
            SirketId = x.Firma?.SirketId,
            SirketAdi = x.Firma?.Sirket?.SirketAdi,
            DosyaYolu = string.IsNullOrWhiteSpace(x.DosyaYolu) ? null : YetkiBelgesiService.GuvenliDosyaLinki(x.Id),
            Durum = x.Durum,
            OlusturmaTarihi = x.OlusturmaTarihi,
            YetkiBelgesiBaslangicTarihi = x.YetkiBelgesiBaslangicTarihi,
            YetkiBelgesiBitisTarihi = x.YetkiBelgesiBitisTarihi,
            OnayTarihi = x.OnayTarihi,
            OnaylayanKullanici = x.OnaylayanKullanici,
            RedGerekce = x.RedGerekce
        };
    }

    private async Task<List<string>> FirmaBildirimleriAsync(int firmaId, Ys_Firma firma)
    {
        var bildirimler = new List<string>();
        var onayli = firma.YetkiBelgeleri?
            .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi && !x.SilindiMi)
            .OrderByDescending(x => x.OlusturmaTarihi)
            .FirstOrDefault();

        var bekleyenVar = firma.YetkiBelgeleri?.Any(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
            && x.YetkiBelgesiBitisTarihi.Date >= DateTime.Today && !x.SilindiMi) ?? false;
        if (onayli != null)
        {
            bildirimler.Add("Yetki belgeniz onaylandı. Cihaz devreye alabilirsiniz.");
            var kalan = (onayli.YetkiBelgesiBitisTarihi.Date - DateTime.Now.Date).Days;
            if (kalan <= 30)
                bildirimler.Add($"Yetki belgenizin bitmesine {kalan} gün kaldı. Lütfen yenileyin.");
        }

        if (bekleyenVar)
            bildirimler.Add("Yetki belgeniz onay bekliyor. Yetkili onayladıktan sonra işlem yapabilirsiniz.");

        var son7Gun = DateTime.Now.AddDays(-7);
        var sonDevreye = await _context.Ys_DevreyeAlmalar
            .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.OlusturmaTarihi >= son7Gun)
            .CountAsync();
        if (sonDevreye > 0)
            bildirimler.Add($"Son 7 günde {sonDevreye} cihaz devreye alındı.");

        var sonSube = await _context.Ys_Subeler
            .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.OlusturmaTarihi >= son7Gun)
            .CountAsync();
        if (sonSube > 0)
            bildirimler.Add($"Son 7 günde {sonSube} şube kaydı eklendi.");

        return bildirimler;
    }
}
