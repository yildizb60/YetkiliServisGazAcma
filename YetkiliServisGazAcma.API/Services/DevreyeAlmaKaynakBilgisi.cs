using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public static class DevreyeAlmaKaynakBilgisi
{
    public static IOrderedQueryable<Ys_DevreyeAlma> Sirala(IQueryable<Ys_DevreyeAlma> query, string? siralama)
    {
        return siralama switch
        {
            "tarih-artan" => query.OrderBy(x => x.DevreyeAlmaTarihi).ThenBy(x => x.Id),
            "abone-artan" => query.OrderBy(x => EF.Functions.Collate(x.MusteriAdi ?? "", "Turkish_CI_AS")).ThenBy(x => x.Id),
            "abone-azalan" => query.OrderByDescending(x => EF.Functions.Collate(x.MusteriAdi ?? "", "Turkish_CI_AS")).ThenBy(x => x.Id),
            _ => query.OrderByDescending(x => x.DevreyeAlmaTarihi).ThenByDescending(x => x.Id)
        };
    }

    public static async Task<List<Ys_DevreyeAlma>> ListeleAsync(
        AppDbContext context, IQueryable<Ys_DevreyeAlma> query, string? sozlesmeNo, string? arama = null)
    {
        var kayitlar = await query.ToListAsync();
        await TamamlaAsync(context, kayitlar);
        // Contract numbers come from validated sources, not a mapped record column.
        if (!string.IsNullOrWhiteSpace(sozlesmeNo))
        {
            var aranacak = sozlesmeNo.Trim();
            kayitlar.RemoveAll(x => x.SozlesmeNo?.Contains(aranacak, StringComparison.Ordinal) != true);
        }
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var aranacak = arama.Trim();
            var karsilastirma = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;
            bool Icerir(string? deger) => deger != null && karsilastirma.IndexOf(deger, aranacak, CompareOptions.IgnoreCase) >= 0;
            kayitlar.RemoveAll(x => !Icerir(x.MusteriAdi) && !Icerir(x.TesistatNo) && !Icerir(x.SozlesmeNo));
        }
        return kayitlar;
    }

    public static async Task TamamlaAsync(AppDbContext context, IReadOnlyCollection<Ys_DevreyeAlma> kayitlar)
    {
        if (kayitlar.Count == 0) return;

        var ids = kayitlar.Select(x => x.Id).ToList();
        var kaynaklar = await (
            from sorgu in context.Ys_DevreyeAlmaSorguKayitlari.AsNoTracking()
            join kayit in context.Ys_DevreyeAlmalar.AsNoTracking() on sorgu.DevreyeAlmaId equals (int?)kayit.Id
            where ids.Contains(kayit.Id) && !kayit.SilindiMi
                && sorgu.FirmaId == kayit.FirmaId
                && kayit.Firma != null && sorgu.DagitimSirketiId == kayit.Firma.SirketId
            select new { Id = kayit.Id, sorgu.KaynakJson })
            .ToListAsync();
        var kayitSozlugu = kayitlar.ToDictionary(x => x.Id);
        var numaralar = new Dictionary<int, HashSet<string>>();
        foreach (var sorgu in kaynaklar)
        {
            var kayit = kayitSozlugu[sorgu.Id];
            YsDevreyeAlmaKaynak? kaynak;
            try { kaynak = JsonSerializer.Deserialize<YsDevreyeAlmaKaynak>(sorgu.KaynakJson); }
            catch (JsonException) { continue; }
            if (kaynak == null || string.IsNullOrWhiteSpace(kaynak.SozlesmeNo)
                || !string.Equals(kaynak.TesisatNo?.Trim(), kayit.TesistatNo?.Trim(), StringComparison.Ordinal)
                || !string.Equals(kaynak.AboneNo?.Trim(), kayit.AboneNo?.Trim(), StringComparison.Ordinal)) continue;

            if (!numaralar.TryGetValue(kayit.Id, out var adaylar))
                numaralar[kayit.Id] = adaylar = new HashSet<string>(StringComparer.Ordinal);
            adaylar.Add(kaynak.SozlesmeNo.Trim());
        }

        var eskiKayitIdleri = kayitlar.Where(x => !numaralar.ContainsKey(x.Id)).Select(x => x.Id).ToList();
        if (eskiKayitIdleri.Count > 0)
        {
            // Legacy records predate query references; only an unambiguous online source can fill the gap.
            var eskiKaynaklar = await (
                from kayit in context.Ys_DevreyeAlmalar.AsNoTracking()
                from talep in context.Ykc_Talepler.AsNoTracking()
                where eskiKayitIdleri.Contains(kayit.Id) && !kayit.SilindiMi
                    && kayit.KaynakCihazAnahtari == null && kayit.Firma != null
                    && kayit.TesistatNo != null && kayit.TesistatNo.Trim() != ""
                    && kayit.AboneNo != null && kayit.AboneNo.Trim() != ""
                    && !context.Ys_DevreyeAlmaSorguKayitlari.Any(x => x.DevreyeAlmaId == kayit.Id)
                    && !talep.SilindiMi && talep.KaynakTipi == "OnlineServis"
                    && talep.SirketId == kayit.Firma.SirketId
                    && talep.TesisatNo != null && talep.TesisatNo.Trim() == kayit.TesistatNo.Trim()
                    && talep.AboneNo != null && talep.AboneNo.Trim() == kayit.AboneNo.Trim()
                    && talep.SozlesmeNo != null && talep.SozlesmeNo.Trim() != ""
                select new { Id = kayit.Id, talep.SozlesmeNo })
                .Distinct()
                .ToListAsync();
            foreach (var kaynak in eskiKaynaklar)
            {
                if (!numaralar.TryGetValue(kaynak.Id, out var adaylar))
                    numaralar[kaynak.Id] = adaylar = new HashSet<string>(StringComparer.Ordinal);
                adaylar.Add(kaynak.SozlesmeNo!.Trim());
            }
        }

        foreach (var kayit in kayitlar)
            kayit.SozlesmeNo = numaralar.TryGetValue(kayit.Id, out var adaylar) && adaylar.Count == 1
                ? adaylar.Single() : null;
    }
}
