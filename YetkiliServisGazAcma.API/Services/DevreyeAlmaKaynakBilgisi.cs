using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public static class DevreyeAlmaKaynakBilgisi
{
    public static IQueryable<Ys_DevreyeAlma> SozlesmeAdaylari(AppDbContext context,
        IQueryable<Ys_DevreyeAlma> query, string aranan)
    {
        // This is only a prefilter. TamamlaAsync still validates every source and rejects
        // ambiguous contracts. OPENJSON preserves escaped strings and duplicate properties.
        if (aranan.Length > 4000 || aranan.Contains('\0')) return query;
        var kaynakIdleri = context.Database.SqlQuery<int>($$"""
            SELECT s.DevreyeAlmaId AS [Value]
            FROM dbo.Ys_DevreyeAlmaSorguKayitlari AS s
            CROSS APPLY OPENJSON(CASE WHEN ISJSON(s.KaynakJson) = 1 THEN s.KaynakJson ELSE N'{}' END) AS j
            WHERE s.DevreyeAlmaId IS NOT NULL
                AND j.[key] COLLATE Latin1_General_100_BIN2 = N'SozlesmeNo'
                AND j.[type] = 1
                AND CHARINDEX({{aranan}} COLLATE Latin1_General_100_BIN2, j.[value] COLLATE Latin1_General_100_BIN2) > 0
            """);
        return query.Where(kayit => kaynakIdleri.Contains(kayit.Id)
            || (kayit.KaynakCihazAnahtari == null && kayit.Firma != null
                && kayit.TesistatNo != null && kayit.TesistatNo.Trim() != ""
                && kayit.AboneNo != null && kayit.AboneNo.Trim() != ""
                && !context.Ys_DevreyeAlmaSorguKayitlari.Any(x => x.DevreyeAlmaId == kayit.Id)
                && context.Ykc_Talepler.Any(talep => !talep.SilindiMi && talep.KaynakTipi == "OnlineServis"
                    && talep.SirketId == kayit.Firma.SirketId
                    && talep.TesisatNo != null && talep.TesisatNo.Trim() == kayit.TesistatNo.Trim()
                    && talep.AboneNo != null && talep.AboneNo.Trim() == kayit.AboneNo.Trim()
                    && talep.SozlesmeNo != null
                    && EF.Functions.Collate(talep.SozlesmeNo, "Latin1_General_100_BIN2").Contains(aranan))));
    }

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
