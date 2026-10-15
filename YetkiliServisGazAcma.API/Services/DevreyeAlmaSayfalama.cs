using System.Globalization;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public static class DevreyeAlmaSayfalama
{
    private const int AramaPaketBoyutu = 256;

    public static async Task<DevreyeAlmaSayfaSonucu> GetirAsync(AppDbContext context,
        IQueryable<Ys_DevreyeAlma> query, string? siralama, string? sozlesmeNo, string? arama,
        int sayfa, int sayfaBoyutu, int? seciliDurum = null, int? odakKayitId = null)
    {
        sayfaBoyutu = Math.Clamp(sayfaBoyutu, 1, 100);
        sayfa = Math.Max(1, sayfa);
        return string.IsNullOrWhiteSpace(sozlesmeNo) && string.IsNullOrWhiteSpace(arama)
            ? await SqlSayfasiAsync(context, query, siralama, sayfa, sayfaBoyutu, seciliDurum, odakKayitId)
            : await KaynakAramaSayfasiAsync(context, query, siralama, sozlesmeNo, arama,
                sayfa, sayfaBoyutu, seciliDurum, odakKayitId);
    }

    private static async Task<DevreyeAlmaSayfaSonucu> SqlSayfasiAsync(AppDbContext context,
        IQueryable<Ys_DevreyeAlma> query, string? siralama, int sayfa, int boyut, int? seciliDurum, int? odakKayitId)
    {
        var ozet = await query.GroupBy(x => x.Durum)
            .Select(g => new { Durum = g.Key, Sayi = g.Count() })
            .ToDictionaryAsync(x => x.Durum, x => x.Sayi);
        if (seciliDurum.HasValue) query = query.Where(x => x.Durum == seciliDurum.Value);
        var toplam = seciliDurum.HasValue ? ozet.GetValueOrDefault(seciliDurum.Value) : ozet.Values.Sum();
        var sirali = DevreyeAlmaKaynakBilgisi.Sirala(query, siralama);
        if (odakKayitId.HasValue)
        {
            // A bookmarked detail can belong to any page. Stream IDs, never the full records.
            var sira = 0;
            await foreach (var id in sirali.Select(x => x.Id).AsAsyncEnumerable())
            {
                if (id == odakKayitId) { sayfa = sira / boyut + 1; break; }
                sira++;
            }
        }
        sayfa = Math.Min(sayfa, Math.Max(1, (int)Math.Ceiling(toplam / (double)boyut)));
        var kayitlar = await sirali.Skip((sayfa - 1) * boyut).Take(boyut).ToListAsync();
        await DevreyeAlmaKaynakBilgisi.TamamlaAsync(context, kayitlar);
        var aktarimIdleri = await sirali.Select(x => x.Id).Take(DevreyeAlmaSayfalamaDto.AktarimSiniri + 1).ToListAsync();
        return new(kayitlar, new() { Sayfa = sayfa, SayfaBoyutu = boyut, ToplamKayit = toplam,
            AktarimIdleri = aktarimIdleri }, ozet);
    }

    private static async Task<DevreyeAlmaSayfaSonucu> KaynakAramaSayfasiAsync(AppDbContext context,
        IQueryable<Ys_DevreyeAlma> query, string? siralama, string? sozlesmeNo, string? arama,
        int sayfa, int boyut, int? seciliDurum, int? odakKayitId)
    {
        var sozlesme = sozlesmeNo?.Trim();
        var aranacak = arama?.Trim();
        var kayitQuery = query;
        if (!string.IsNullOrEmpty(sozlesme))
        {
            var adayQuery = DevreyeAlmaKaynakBilgisi.SozlesmeAdaylari(context, query, sozlesme);
            // Probe once, with bounded IDs. Broad searches keep the batched path instead of
            // parsing the same SQL JSON documents again for each page of candidates.
            var adayIdleri = await adayQuery.Select(x => x.Id).Take(AramaPaketBoyutu * 2 + 1).ToListAsync();
            if (adayIdleri.Count <= AramaPaketBoyutu * 2)
                query = query.Where(x => adayIdleri.Contains(x.Id));
        }
        // Source validation remains authoritative, including conflicting candidate contracts.
        var inceQuery = DevreyeAlmaKaynakBilgisi.Sirala(query, siralama).Select(x => new Ys_DevreyeAlma
        {
            Id = x.Id, FirmaId = x.FirmaId, TesistatNo = x.TesistatNo, AboneNo = x.AboneNo,
            MusteriAdi = x.MusteriAdi, Durum = x.Durum
        });
        var ozet = new Dictionary<int, int>();
        var aktarim = new List<int>();
        var mevcutSayfa = new List<int>();
        var secilenSayfa = new List<int>();
        int toplam = 0, odakSayfa = 0;
        var karsilastirma = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;
        bool Icerir(string? deger) => deger != null && karsilastirma.IndexOf(deger, aranacak!, CompareOptions.IgnoreCase) >= 0;
        for (var atla = 0; ; atla += AramaPaketBoyutu)
        {
            var paket = await inceQuery.Skip(atla).Take(AramaPaketBoyutu).ToListAsync();
            if (paket.Count == 0) break;
            var kaynakGerekenler = !string.IsNullOrEmpty(sozlesme) ? paket
                : paket.Where(x => !Icerir(x.MusteriAdi) && !Icerir(x.TesistatNo)).ToList();
            await DevreyeAlmaKaynakBilgisi.TamamlaAsync(context, kaynakGerekenler);
            foreach (var kayit in paket)
            {
                if (!string.IsNullOrEmpty(sozlesme) && kayit.SozlesmeNo?.Contains(sozlesme, StringComparison.Ordinal) != true) continue;
                if (!string.IsNullOrEmpty(aranacak) && !Icerir(kayit.MusteriAdi) && !Icerir(kayit.TesistatNo) && !Icerir(kayit.SozlesmeNo)) continue;
                ozet[kayit.Durum] = ozet.GetValueOrDefault(kayit.Durum) + 1;
                if (seciliDurum.HasValue && kayit.Durum != seciliDurum.Value) continue;
                if (toplam % boyut == 0) mevcutSayfa = [];
                mevcutSayfa.Add(kayit.Id);
                toplam++;
                var mevcutSayfaNo = (toplam - 1) / boyut + 1;
                if (kayit.Id == odakKayitId) odakSayfa = mevcutSayfaNo;
                if (mevcutSayfaNo == (odakSayfa > 0 ? odakSayfa : sayfa)) secilenSayfa = mevcutSayfa;
                if (aktarim.Count <= DevreyeAlmaSayfalamaDto.AktarimSiniri) aktarim.Add(kayit.Id);
            }
            if (paket.Count < AramaPaketBoyutu) break;
        }
        var sonSayfa = Math.Max(1, (int)Math.Ceiling(toplam / (double)boyut));
        sayfa = odakSayfa > 0 ? odakSayfa : Math.Min(sayfa, sonSayfa);
        if (secilenSayfa.Count == 0 && toplam > 0) secilenSayfa = mevcutSayfa;
        var kayitlar = await DevreyeAlmaKaynakBilgisi.Sirala(kayitQuery.Where(x => secilenSayfa.Contains(x.Id)), siralama).ToListAsync();
        await DevreyeAlmaKaynakBilgisi.TamamlaAsync(context, kayitlar);
        return new(kayitlar, new() { Sayfa = sayfa, SayfaBoyutu = boyut, ToplamKayit = toplam,
            AktarimIdleri = aktarim }, ozet);
    }
}

public sealed record DevreyeAlmaSayfaSonucu(List<Ys_DevreyeAlma> Kayitlar,
    DevreyeAlmaSayfalamaDto Sayfalama, Dictionary<int, int> DurumOzet);
