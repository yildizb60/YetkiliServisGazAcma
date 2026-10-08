using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class DevreyeAlmaOkumaApiService(AppDbContext context, YetkiliServisIlkKurulumService ilkKurulum, DevreyeAlmaYetkiDogrulamaService yetki)
{
    private readonly AppDbContext _context = context;
    private readonly YetkiliServisIlkKurulumService _ilkKurulumService = ilkKurulum;
    private readonly DevreyeAlmaYetkiDogrulamaService _yetki = yetki;

    public async Task<YsDevreyeAlmaGecmisDto> GecmisAsync(int firmaId, YsDevreyeAlmaGecmisFiltreDto? dto)
    {
        var query = DevreyeAlmaQuery()
            .Where(x => x.FirmaId == firmaId);

        if (!string.IsNullOrWhiteSpace(dto?.Marka))
        {
            var marka = dto.Marka.Trim();
            query = query.Where(x =>
                (x.Marka != null && x.Marka.MarkaAdi == marka) ||
                (x.CihazMarka != null && x.CihazMarka == marka));
        }

        var basTarih = dto?.BaslangicTarihi?.Date;
        var bitTarih = dto?.BitisTarihi?.Date;
        if (basTarih > bitTarih)
            (basTarih, bitTarih) = (bitTarih, basTarih);
        if (basTarih.HasValue)
        {
            var baslangic = basTarih.Value;
            query = query.Where(x => x.DevreyeAlmaTarihi >= baslangic);
        }

        if (bitTarih.HasValue)
        {
            var bitis = bitTarih.Value.AddDays(1);
            query = query.Where(x => x.DevreyeAlmaTarihi < bitis);
        }

        if (!string.IsNullOrWhiteSpace(dto?.TesisatNo))
        {
            var tesisat = dto.TesisatNo.Trim();
            query = query.Where(x => x.TesistatNo != null && x.TesistatNo.Contains(tesisat));
        }

        if (!string.IsNullOrWhiteSpace(dto?.Musteri))
        {
            var aranacak = dto.Musteri.Trim();
            query = query.Where(x =>
                (x.MusteriAdi != null && x.MusteriAdi.Contains(aranacak)) ||
                (x.AboneNo != null && x.AboneNo.Contains(aranacak)) ||
                (x.MusteriTelefon != null && x.MusteriTelefon.Contains(aranacak)));
        }

        var durumOzet = await query
            .GroupBy(x => x.Durum)
            .Select(g => new { Durum = g.Key, Sayi = g.Count() })
            .ToListAsync();
        var tamamlanan = durumOzet.FirstOrDefault(x => x.Durum == DevreyeAlmaDurumDegerleri.Tamamlandi)?.Sayi ?? 0;
        var bekleyen = durumOzet.FirstOrDefault(x => x.Durum == DevreyeAlmaDurumDegerleri.Bekliyor)?.Sayi ?? 0;
        var iptal = durumOzet.FirstOrDefault(x => x.Durum == DevreyeAlmaDurumDegerleri.Iptal)?.Sayi ?? 0;

        if (!string.IsNullOrWhiteSpace(dto?.Durum))
        {
            var durum = dto.Durum.Trim();
            if (string.Equals(durum, "tamamlandi", StringComparison.OrdinalIgnoreCase) || durum == "1")
                query = query.Where(x => x.Durum == DevreyeAlmaDurumDegerleri.Tamamlandi);
            else if (string.Equals(durum, "bekliyor", StringComparison.OrdinalIgnoreCase) || durum == "0")
                query = query.Where(x => x.Durum == DevreyeAlmaDurumDegerleri.Bekliyor);
            else if (string.Equals(durum, "iptal", StringComparison.OrdinalIgnoreCase) || durum == "2")
                query = query.Where(x => x.Durum == DevreyeAlmaDurumDegerleri.Iptal);
        }

        var islemler = await query
            .OrderByDescending(x => x.DevreyeAlmaTarihi).ThenByDescending(x => x.Id)
            .ToListAsync();
        await DevreyeAlmaKaynakBilgisi.TamamlaAsync(_context, islemler);

        var firma = await FirmaQuery().FirstOrDefaultAsync(x => x.Id == firmaId);
        var markaList = await _context.Ys_DevreyeAlmalar
            .Where(x => x.FirmaId == firmaId && !x.SilindiMi)
            .Select(x => x.Marka != null && x.Marka.MarkaAdi != null ? x.Marka.MarkaAdi : x.CihazMarka)
            .Where(x => x != null && x != "")
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return new YsDevreyeAlmaGecmisDto
        {
            Islemler = islemler.Select(YsDevreyeAlmaDto.FromEntity).ToList(),
            Firma = firma == null ? null : YsFirmaDto.FromEntity(firma),
            MarkaList = markaList!,
            Toplam = durumOzet.Sum(x => x.Sayi),
            Tamamlanan = tamamlanan,
            Bekleyen = bekleyen,
            Iptal = iptal
        };
    }

    public async Task<YsDevreyeAlmaDto?> GetirAsync(int firmaId, int id)
    {
        var islem = await DevreyeAlmaQuery()
            .FirstOrDefaultAsync(x => x.Id == id && x.FirmaId == firmaId);
        if (islem == null) return null;
        await DevreyeAlmaKaynakBilgisi.TamamlaAsync(_context, new[] { islem });
        return YsDevreyeAlmaDto.FromEntity(islem);
    }

    public async Task<YsDevreyeAlmaEkranDto> EkranAsync(int firmaId)
    {
        var kurulum = await _ilkKurulumService.GetirAsync(firmaId);
        if (kurulum.zorunluMu && !kurulum.tamamlandiMi)
        {
            return new YsDevreyeAlmaEkranDto
            {
                Erisilebilir = false,
                Hata = "Ilk kurulum tamamlanmadan cihaz devreye alma islemi yapilamaz.",
                RedirectUrl = "/ys-panel/ilk-kurulum"
            };
        }

        if (!await _yetki.GecerliYetkiBelgesiVarAsync(firmaId))
        {
            var onayliYetkiBelgesiVar = await _yetki.OnayliYetkiBelgesiVarAsync(firmaId);
            return new YsDevreyeAlmaEkranDto
            {
                Erisilebilir = false,
                Hata = onayliYetkiBelgesiVar
                    ? "Gecerli tarih araliginda onayli yetki belgeniz bulunmuyor."
                    : "Onayli yetki belgeniz bulunmuyor.",
                RedirectUrl = onayliYetkiBelgesiVar ? "/ys-yetki-belgesi" : "/ys-panel"
            };
        }

        var markalar = await _context.Ys_FirmaMarkalar
            .Include(x => x.Marka)
            .Where(x => x.FirmaId == firmaId && !x.SilindiMi)
            .Select(x => x.Marka)
            .ToListAsync();

        var firma = await FirmaQuery().FirstOrDefaultAsync(x => x.Id == firmaId);

        return new YsDevreyeAlmaEkranDto
        {
            Erisilebilir = true,
            Markalar = markalar
                .Where(x => x != null)
                .Select(x => YsMarkaDto.FromEntity(x!))
                .ToList(),
            Firma = firma == null ? null : YsFirmaDto.FromEntity(firma)
        };
    }

    public async Task<YsDevreyeAlmaBildirimDto> BildirimlerAsync(int firmaId)
    {
        var bildirimler = new List<string>();

        var firma = await _context.Ys_Firmalar
            .Include(x => x.YetkiBelgeleri)
            .FirstOrDefaultAsync(x => x.Id == firmaId);

        var bugun = DateTime.Now.Date;
        var onayli = firma?.YetkiBelgeleri?
            .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi
                && !x.SilindiMi
                && (!x.YetkiBelgesiBaslangicTarihi.HasValue || x.YetkiBelgesiBaslangicTarihi.Value.Date <= bugun)
                && x.YetkiBelgesiBitisTarihi.Date >= bugun)
            .OrderBy(x => x.YetkiBelgesiBitisTarihi)
            .FirstOrDefault();

        var bekleyenVar = firma?.YetkiBelgeleri?.Any(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
            && !x.SilindiMi && x.YetkiBelgesiBitisTarihi.Date >= bugun) ?? false;
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

        return new YsDevreyeAlmaBildirimDto
        {
            Bildirimler = bildirimler,
            BildirimSayisi = bildirimler.Count
        };
    }

    private IQueryable<Ys_DevreyeAlma> DevreyeAlmaQuery()
    {
        return _context.Ys_DevreyeAlmalar
            .Include(x => x.Marka)
            .Include(x => x.Firma)
                .ThenInclude(x => x!.Sirket)
            .Where(x => !x.SilindiMi);
    }

    private IQueryable<Ys_Firma> FirmaQuery()
    {
        return _context.Ys_Firmalar
            .Include(x => x.Sirket)
            .Where(x => !x.SilindiMi);
    }
}
