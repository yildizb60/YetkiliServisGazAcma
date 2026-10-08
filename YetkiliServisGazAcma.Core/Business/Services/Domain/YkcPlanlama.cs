using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services;

public sealed class YkcPlanlamaOptions
{
    public int AsgariAralikDakika { get; set; } = 30;
    public int RandevuDilimDakika { get; set; } = 30;
    public List<YkcEkipSecenegi> Ekipler { get; set; } = new();
}

public sealed class YkcEkipSecenegi
{
    public string Id { get; set; } = "";
    public int SirketId { get; set; }
    public string Il { get; set; } = "";
    public string Bolge { get; set; } = "";
    public string Ad { get; set; } = "";
    public string YonlendirmeTipi { get; set; } = "";
    public string? KullaniciId { get; set; }
    public bool Secili { get; set; }
}

public sealed class YkcTakvimFiltre
{
    public int? AktifSirketId { get; set; }
    public DateTime Baslangic { get; set; } = DateTime.Today;
    public DateTime Bitis { get; set; } = DateTime.Today;
    public string? Il { get; set; }
    public string? Bolge { get; set; }
    public string? Personel { get; set; }
    public string? Musteri { get; set; }
    public string? TesisatNo { get; set; }
    public int Sayfa { get; set; } = 1;
    public bool GorunumKayitlariniGetir { get; set; }
}

public sealed class YkcTakvimKayit
{
    public int Id { get; set; }
    public DateTime Tarih { get; set; }
    public string? Saat { get; set; }
    public string? Musteri { get; set; }
    public string? TesisatNo { get; set; }
    public string? Adres { get; set; }
    public string? Il { get; set; }
    public string? Bolge { get; set; }
    public string? Personel { get; set; }
    public string? Ekip { get; set; }
    public string? Yonlendirme { get; set; }
    public int Durum { get; set; }
}

public sealed class YkcTakvimSonuc
{
    public YkcTakvimFiltre Filtre { get; set; } = new();
    public int Toplam { get; set; }
    public int SayfaBoyutu { get; set; } = 25;
    public List<YkcTakvimKayit> Kayitlar { get; set; } = new();
    public List<YkcTakvimKayit> GorunumKayitlari { get; set; } = new();
    public List<YkcTakvimGunOzeti> Gunler { get; set; } = new();
    public List<string> IlSecenekleri { get; set; } = new();
    public List<string> BolgeSecenekleri { get; set; } = new();
    public List<string> PersonelEkipSecenekleri { get; set; } = new();
}

public sealed class YkcTakvimGunOzeti
{
    public DateTime Tarih { get; set; }
    public int Toplam { get; set; }
}

public static class YkcRandevuKurali
{
    public static bool Cakisiyor(DateTime birinci, DateTime ikinci, int asgariAralikDakika)
        => birinci == ikinci || Math.Abs((birinci - ikinci).TotalMinutes) < Math.Clamp(asgariAralikDakika, 0, 240);

    public static bool MesaiSaatindeMi(TimeSpan saat)
        => saat >= TimeSpan.FromHours(8) && saat < TimeSpan.FromHours(18);

    public static bool GecerliSaatDilimi(TimeSpan saat, int dilimDakika)
    {
        var dilim = Math.Clamp(dilimDakika, 1, 240);
        return saat.Seconds == 0 && saat.Milliseconds == 0 && (int)saat.TotalMinutes % dilim == 0;
    }
}

public static class YkcTakvimGorunumKurali
{
    public static bool DoneminTumKayitlariGerekli(DateTime baslangic, DateTime bitis)
    {
        baslangic = baslangic.Date;
        bitis = bitis.Date;
        var gunSayisi = (bitis - baslangic).Days + 1;
        var tamHafta = gunSayisi == 7 && baslangic.DayOfWeek == DayOfWeek.Monday;
        var tamAy = baslangic.Day == 1 && bitis == baslangic.AddMonths(1).AddDays(-1);
        return tamHafta || tamAy;
    }
}

public static class YkcKontrolAkisKurali
{
    public const int DonemKontrolSayisi = 5;

    // KontrolNo talep boyunca tekildir; resmi formda her donem yeniden 1-5 kullanilir.
    public static int DonemNo(int kontrolNo) => (Math.Max(1, kontrolNo) - 1) / DonemKontrolSayisi + 1;
    public static int FormKontrolNo(int kontrolNo) => (Math.Max(1, kontrolNo) - 1) % DonemKontrolSayisi + 1;
    public static int DonemBaslangici(IEnumerable<int> kontrolNumaralari)
        => (DonemNo(kontrolNumaralari.DefaultIfEmpty(1).Max()) - 1) * DonemKontrolSayisi + 1;

    public static List<Ykc_Fr265Kontrol> AktifKontroller(IEnumerable<Ykc_Fr265Kontrol> kontroller)
    {
        var kayitlar = kontroller.Where(x => !x.SilindiMi && x.KontrolNo > 0).ToList();
        var baslangic = DonemBaslangici(kayitlar.Select(x => x.KontrolNo));
        return kayitlar.Where(x => x.KontrolNo >= baslangic)
            .GroupBy(x => x.KontrolNo)
            .Select(x => x.OrderByDescending(k => k.KontrolTarihi ?? DateTime.MinValue).ThenByDescending(k => k.Id).First())
            .OrderBy(x => x.KontrolNo).ToList();
    }

    public static bool YeniRandevuGerekli(string? kontrolSonucu)
        => string.Equals(
            kontrolSonucu?.Trim(),
            YkcFr265KontrolSonucDegerleri.UygunDegil,
            StringComparison.Ordinal);

    public static int? SiradakiKontrolNo(IEnumerable<Ykc_Fr265Kontrol> kontroller)
    {
        var aktifKontroller = AktifKontroller(kontroller);
        var sonKontrol = aktifKontroller
            .Where(x => (x.Sonuc == YkcFr265KontrolSonucDegerleri.Uygun
                    || x.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil))
            .OrderBy(x => x.KontrolNo)
            .LastOrDefault();

        if (sonKontrol == null)
            return aktifKontroller.Any(x => x.KontrolNo > DonemKontrolSayisi) ? 1 : null;
        if (sonKontrol.Sonuc != YkcFr265KontrolSonucDegerleri.UygunDegil) return null;

        var siradaki = sonKontrol.KontrolNo + 1;
        return FormKontrolNo(siradaki);
    }

    public static bool KontrolAlaniDolduMu(IEnumerable<Ykc_Fr265Kontrol> kontroller)
    {
        var sonuclar = AktifKontroller(kontroller);

        return sonuclar.Count == 5
            && sonuclar.All(x => x.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil);
    }
}
