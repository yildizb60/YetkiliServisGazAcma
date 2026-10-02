using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public static class DevreyeAlmaKaynakBilgisi
{
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

        foreach (var kayit in kayitlar)
            kayit.SozlesmeNo = numaralar.TryGetValue(kayit.Id, out var adaylar) && adaylar.Count == 1
                ? adaylar.Single() : null;
    }
}
