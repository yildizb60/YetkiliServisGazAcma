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
        var firma = await _context.Ys_Firmalar.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == firmaId && !x.SilindiMi);

        if (firma == null)
            return null;

        var belgeler = await _service.FirmaninYetkiBelgeleri(firmaId);
        firma.YetkiBelgeleri = belgeler;
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

    public async Task<YetkiBelgesiOnayEkraniDto> OnayEkraniAsync(
        int? sirketId, YetkiBelgesiOnayFiltreDto? filtre = null, CancellationToken cancellationToken = default)
    {
        var (kayitlar, sayfalama) = await AdminYetkiBelgesiOnayApiService.OnaySayfasiAsync(
            _context, sirketId, filtre, cancellationToken);
        var belgeler = kayitlar.Select(MapYetkiBelgesi).ToList();

        return new YetkiBelgesiOnayEkraniDto
        {
            Sayfalama = sayfalama,
            Bekleyenler = sayfalama.Durum == "bekleyen" ? belgeler : [],
            SuresiDolanlar = sayfalama.Durum == "suresi-dolan" ? belgeler : [],
            Onaylananlar = sayfalama.Durum == "onaylanan" ? belgeler : [],
            Reddedilenler = sayfalama.Durum == "reddedilen" ? belgeler : []
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
            VergiNo = x.Firma?.VergiNo,
            FirmaYetkiliKisi = x.Firma?.YetkiliKisi,
            FirmaTelefon = x.Firma?.Telefon,
            FirmaAdres = x.Firma?.Adres,
            FirmaFaaliyetIli = x.Firma?.FaaliyetIli,
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
        var bugun = DateTime.Today;
        var gecerli = firma.YetkiBelgeleri?
            .Where(x => YetkiBelgesiService.GecerliMi(x, bugun))
            .OrderByDescending(x => x.YetkiBelgesiBitisTarihi)
            .FirstOrDefault();
        var onayli = firma.YetkiBelgeleri?
            .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi && !x.SilindiMi)
            .OrderByDescending(x => x.OlusturmaTarihi)
            .FirstOrDefault();

        var bekleyenVar = firma.YetkiBelgeleri?.Any(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
            && x.YetkiBelgesiBitisTarihi.Date >= DateTime.Today && !x.SilindiMi) ?? false;
        if (gecerli != null)
        {
            bildirimler.Add("Yetki belgeniz onaylandı. Cihaz devreye alabilirsiniz.");
            var kalan = (gecerli.YetkiBelgesiBitisTarihi.Date - bugun).Days;
            if (kalan <= 30)
                bildirimler.Add($"Yetki belgenizin bitmesine {kalan} gün kaldı. Lütfen yenileyin.");
        }
        else if (onayli?.YetkiBelgesiBaslangicTarihi?.Date > bugun)
        {
            bildirimler.Add($"Yetki belgenizin geçerliliği {onayli.YetkiBelgesiBaslangicTarihi:dd.MM.yyyy} tarihinde başlayacak.");
        }
        else if (onayli != null)
        {
            bildirimler.Add("Yetki belgenizin süresi doldu. Lütfen yenileyin.");
        }

        if (bekleyenVar)
            bildirimler.Add(gecerli != null
                ? "Yeni yetki belgeniz onay bekliyor. Mevcut geçerli belgenizle işlem yapabilirsiniz."
                : "Yetki belgeniz onay bekliyor. Yetkili onayladıktan sonra işlem yapabilirsiniz.");

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
