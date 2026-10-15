using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class YetkiliServisRaporApiService(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<YsPanelRaporSonucDto> GetirAsync(int firmaId, YsPanelRaporFiltreDto? dto)
    {
        var firma = await _context.Ys_Firmalar.AsNoTracking().Include(x => x.Sirket).Where(x => !x.SilindiMi)
            .FirstOrDefaultAsync(x => x.Id == firmaId);

        DateTime basTarih;
        DateTime bitTarih;
        List<Ys_DevreyeAlma> islemler;
        var limit = Math.Clamp(dto?.Limit is > 0 ? dto.Limit.Value : 10, 1, 100);

        if (dto?.Ids?.Count > 0)
        {
            var secilenler = _context.Ys_DevreyeAlmalar.AsNoTracking()
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi && dto.Ids.Contains(x.Id));
            var aralik = await secilenler.GroupBy(_ => 1)
                .Select(g => new { Bas = g.Min(x => x.DevreyeAlmaTarihi), Bit = g.Max(x => x.DevreyeAlmaTarihi) })
                .FirstOrDefaultAsync();
            basTarih = aralik?.Bas.Date ?? DateTime.Today;
            bitTarih = aralik?.Bit.Date ?? DateTime.Today;
            islemler = await secilenler
                .Include(x => x.Marka)
                .Include(x => x.Firma)
                    .ThenInclude(x => x!.Sirket)
                .OrderByDescending(x => x.DevreyeAlmaTarihi).ThenByDescending(x => x.Id)
                .Take(limit)
                .ToListAsync();
        }
        else
        {
            var tarihAraligi = await GetRaporTarihAraligiAsync(firmaId, dto?.Bas, dto?.Bit);
            basTarih = tarihAraligi.Bas;
            bitTarih = tarihAraligi.Bit;
            var bitSonrasi = bitTarih.AddDays(1);

            var query = _context.Ys_DevreyeAlmalar.AsNoTracking()
                .Include(x => x.Marka)
                .Include(x => x.Firma)
                    .ThenInclude(x => x!.Sirket)
                .Where(x => x.FirmaId == firmaId
                    && !x.SilindiMi
                    && x.DevreyeAlmaTarihi >= basTarih
                    && x.DevreyeAlmaTarihi < bitSonrasi)
                .OrderByDescending(x => x.DevreyeAlmaTarihi).ThenByDescending(x => x.Id);

            islemler = await query.Take(limit).ToListAsync();
        }

        var bitSonrasiRapor = bitTarih.AddDays(1);
        var devreyeTemelQuery = _context.Ys_DevreyeAlmalar
            .Include(x => x.Marka)
            .Where(x => x.FirmaId == firmaId
                && !x.SilindiMi
                && x.DevreyeAlmaTarihi >= basTarih
                && x.DevreyeAlmaTarihi < bitSonrasiRapor);
        if (dto?.Ids?.Count > 0)
            devreyeTemelQuery = devreyeTemelQuery.Where(x => dto.Ids.Contains(x.Id));

        var yetkiBelgesiTemelQuery = _context.Ys_YetkiBelgeleri
            .Where(x => x.FirmaId == firmaId
                && !x.SilindiMi
                && x.OlusturmaTarihi >= basTarih
                && x.OlusturmaTarihi < bitSonrasiRapor);

        var durumlar = await devreyeTemelQuery.GroupBy(x => x.Durum)
            .Select(g => new { Durum = g.Key, Sayi = g.Count() }).ToDictionaryAsync(x => x.Durum, x => x.Sayi);
        var devreyeSayisi = durumlar.Values.Sum();
        var tamamlanan = durumlar.GetValueOrDefault(DevreyeAlmaDurumDegerleri.Tamamlandi);
        var bekleyen = durumlar.GetValueOrDefault(DevreyeAlmaDurumDegerleri.Bekliyor);
        var iptal = durumlar.GetValueOrDefault(DevreyeAlmaDurumDegerleri.Iptal);

        var bugun = DateTime.Today;
        var belgeDurumlari = await yetkiBelgesiTemelQuery
            .Where(x => x.Durum != YetkiBelgesiDurumDegerleri.OnaydaBekliyor || x.YetkiBelgesiBitisTarihi >= bugun)
            .GroupBy(x => x.Durum)
            .Select(g => new { Durum = g.Key, Sayi = g.Count() }).ToDictionaryAsync(x => x.Durum, x => x.Sayi);
        var yetkiBelgesiOnayli = belgeDurumlari.GetValueOrDefault(YetkiBelgesiDurumDegerleri.Onaylandi);
        var yetkiBelgesiBekleyen = belgeDurumlari.GetValueOrDefault(YetkiBelgesiDurumDegerleri.OnaydaBekliyor);
        var yetkiBelgesiReddedilen = belgeDurumlari.GetValueOrDefault(YetkiBelgesiDurumDegerleri.Reddedildi);

        var aylikBaslangic = new DateTime(basTarih.Year, basTarih.Month, 1);
        var aylikBitis = new DateTime(bitTarih.Year, bitTarih.Month, 1);
        var aySayisi = ((aylikBitis.Year - aylikBaslangic.Year) * 12) + aylikBitis.Month - aylikBaslangic.Month + 1;
        if (aySayisi < 1) aySayisi = 1;

        var aylikEtiketler = Enumerable.Range(0, aySayisi)
            .Select(i => aylikBaslangic.AddMonths(i))
            .ToList();

        var aylikHam = await devreyeTemelQuery
            .GroupBy(x => new { x.DevreyeAlmaTarihi.Year, x.DevreyeAlmaTarihi.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        var aylikMap = aylikHam.ToDictionary(x => $"{x.Year:D4}-{x.Month:D2}", x => x.Count);
        var chartAylikLabels = aylikEtiketler.Select(x => x.ToString("MM.yyyy")).ToList();
        var chartAylikData = aylikEtiketler
            .Select(x => aylikMap.TryGetValue($"{x.Year:D4}-{x.Month:D2}", out var value) ? value : 0)
            .ToList();

        var chartMarka = await devreyeTemelQuery
            .Select(x => x.Marka != null && !string.IsNullOrWhiteSpace(x.Marka.MarkaAdi)
                ? x.Marka.MarkaAdi!.Trim()
                : !string.IsNullOrWhiteSpace(x.CihazMarka) ? x.CihazMarka.Trim() : "Marka belirtilmemiş")
            .GroupBy(x => x)
            .Select(g => new { Marka = g.Key, Sayi = g.Count() })
            .OrderByDescending(x => x.Sayi)
            .Take(6)
            .ToListAsync();

        return new YsPanelRaporSonucDto
        {
            Firma = firma == null ? null : YsPanelFirmaDto.FromEntity(firma),
            BasTarih = basTarih,
            BitTarih = bitTarih,
            DevreyeSayisi = devreyeSayisi,
            Tamamlanan = tamamlanan,
            Bekleyen = bekleyen,
            YetkiBelgesiOnayli = yetkiBelgesiOnayli,
            YetkiBelgesiBekleyen = yetkiBelgesiBekleyen,
            YetkiBelgesiReddedilen = yetkiBelgesiReddedilen,
            SonIslemler = islemler.Select(YsPanelDevreyeAlmaDto.FromEntity).ToList(),
            ChartAylikLabels = chartAylikLabels,
            ChartAylikData = chartAylikData,
            ChartDurumData = new List<int> { tamamlanan, bekleyen, iptal },
            ChartMarkaLabels = chartMarka.Select(x => x.Marka ?? "-").ToList(),
            ChartMarkaData = chartMarka.Select(x => x.Sayi).ToList()
        };
    }

    private async Task<(DateTime Bas, DateTime Bit)> GetRaporTarihAraligiAsync(int firmaId, DateTime? bas, DateTime? bit)
    {
        if (!bas.HasValue && !bit.HasValue)
        {
            var mevcutAralik = await _context.Ys_DevreyeAlmalar
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Bas = g.Min(x => x.DevreyeAlmaTarihi),
                    Bit = g.Max(x => x.DevreyeAlmaTarihi)
                })
                .FirstOrDefaultAsync();

            if (mevcutAralik != null)
                return (mevcutAralik.Bas.Date, mevcutAralik.Bit.Date);
        }

        var bitTarih = bit?.Date ?? DateTime.Now.Date;
        var basTarih = bas?.Date ?? bitTarih.AddDays(-30);
        return basTarih <= bitTarih ? (basTarih, bitTarih) : (bitTarih, basTarih);
    }
}
