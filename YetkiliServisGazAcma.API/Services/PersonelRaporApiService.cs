using System.Globalization;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class PersonelRaporApiService(AppDbContext context, AdminRaporApiService raporlar, YkcTalepOkumaService talepler)
{
    // Company scope is validated by the endpoint; module permissions are resolved here.
    public async Task<PersonelRaporDto?> GetirAsync(AppKullanici kullanici, int? sirketId, bool yonetici, PersonelRaporFiltreDto? filtre)
    {
        if (!kullanici.AktifMi || kullanici.ArsivlemeTarihi != null || (!yonetici && !sirketId.HasValue))
            return null;

        var yetkiler = await context.Dag_PersonelYetkiler.AsNoTracking()
            .Where(x => x.KullaniciId == kullanici.Id && !x.SilindiMi && x.SirketId == sirketId)
            .Select(x => x.YetkiTipi).Distinct().ToListAsync();
        bool Yetkili(string yetki) => yonetici || yetkiler.Contains(YetkiTipleri.TAM_YETKI) || yetkiler.Contains(yetki);
        var sonuc = new PersonelRaporDto();
        if (Yetkili(YetkiTipleri.YKC_RAPOR_GOR))
            sonuc.IzinliRaporTipleri.Add("ykc");
        if (Yetkili(YetkiTipleri.RAPOR_GOR))
        {
            sonuc.IzinliRaporTipleri.Add("devreye");
            if (Yetkili(YetkiTipleri.YETKI_BELGESI_ONAY))
                sonuc.IzinliRaporTipleri.AddRange(["onayli", "bekleyen", "reddedilen"]);
        }
        if (sonuc.IzinliRaporTipleri.Count == 0)
            return null;

        var tip = filtre?.Tip?.Trim().ToLowerInvariant();
        sonuc.RaporTipi = tip != null && sonuc.IzinliRaporTipleri.Contains(tip) ? tip : sonuc.IzinliRaporTipleri[0];
        var bugun = DateTime.Today;
        sonuc.BasTarih = filtre?.BaslangicTarihi?.Date
            ?? (sonuc.RaporTipi == "ykc" ? bugun.AddDays(-30) : new DateTime(bugun.Year, bugun.Month, 1));
        sonuc.BitTarih = filtre?.BitisTarihi?.Date ?? bugun;
        if (sonuc.BasTarih > sonuc.BitTarih)
            (sonuc.BasTarih, sonuc.BitTarih) = (sonuc.BitTarih, sonuc.BasTarih);

        if (sonuc.RaporTipi == "ykc")
        {
            var rapor = await talepler.RaporAsync(new YkcTalepListeFiltre
            {
                SirketId = sirketId, BaslangicTarihi = sonuc.BasTarih, BitisTarihi = sonuc.BitTarih,
                Sayfa = 1, SayfaBoyutu = 10
            }, kullanici, yonetici, sirketId);
            sonuc.Toplam = rapor.Toplam;
            var durumlar = rapor.DurumOzetleri.OrderByDescending(x => x.Sayi).ToList();
            sonuc.DurumEtiketleri = durumlar.Select(x => YkcDurumDegerleri.Etiket(x.Durum)).ToList();
            sonuc.DurumSayilari = durumlar.Select(x => x.Sayi).ToList();
            sonuc.DonemEtiketleri = rapor.FirmaOzetleri.Take(6).Select(x => x.Ad ?? "-").ToList();
            sonuc.DonemSayilari = rapor.FirmaOzetleri.Take(6).Select(x => x.Sayi).ToList();
            sonuc.KirilimEtiketleri = rapor.EkipOzetleri.Take(6).Select(x => x.Ad ?? "-").ToList();
            sonuc.KirilimSayilari = rapor.EkipOzetleri.Take(6).Select(x => x.Sayi).ToList();
        }
        else
        {
            var rapor = await raporlar.RaporlarOzetAsync(new AdminRaporOzetFiltreDto
            {
                BaslangicTarihi = sonuc.BasTarih, BitisTarihi = sonuc.BitTarih, Tip = sonuc.RaporTipi
            }, sirketId, operasyonGorebilir: false, belgeGorebilir: Yetkili(YetkiTipleri.YETKI_BELGESI_ONAY));
            sonuc.Toplam = sonuc.RaporTipi switch
            {
                "onayli" => rapor.YetkiBelgesiOnayli,
                "bekleyen" => rapor.YetkiBelgesiBekleyen,
                "reddedilen" => rapor.YetkiBelgesiReddedilen,
                _ => rapor.DevreyeSayisi
            };
            sonuc.Tamamlanan = rapor.DevreyeTamamlanan;
            sonuc.DurumEtiketleri = sonuc.RaporTipi == "devreye"
                ? ["Tamamlanan", "Bekleyen", "İptal"]
                : ["Onaylanan", "Onay Bekleyen", "Reddedilen"];
            sonuc.DurumSayilari = rapor.ChartDurumData;
            sonuc.KirilimEtiketleri = rapor.ChartMarkaLabels.Select(x => x ?? "-").ToList();
            sonuc.KirilimSayilari = rapor.ChartMarkaData;
            for (var i = 0; i < rapor.ChartAylikLabels.Count; i++)
            {
                var ay = DateTime.ParseExact(rapor.ChartAylikLabels[i], "MM.yyyy", CultureInfo.InvariantCulture);
                if (ay <= sonuc.BitTarih && ay.AddMonths(1) > sonuc.BasTarih)
                {
                    sonuc.DonemEtiketleri.Add(rapor.ChartAylikLabels[i]);
                    sonuc.DonemSayilari.Add(rapor.ChartAylikData[i]);
                }
            }
        }
        return sonuc;
    }
}
