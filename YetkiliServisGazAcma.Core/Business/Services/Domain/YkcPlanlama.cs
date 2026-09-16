using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services;

public sealed class YkcPlanlamaOptions
{
    public int AsgariAralikDakika { get; set; }
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
    public int Durum { get; set; }
}

public sealed class YkcTakvimSonuc
{
    public YkcTakvimFiltre Filtre { get; set; } = new();
    public int Toplam { get; set; }
    public int SayfaBoyutu { get; set; } = 25;
    public List<YkcTakvimKayit> Kayitlar { get; set; } = new();
    public List<YkcTakvimGunOzeti> Gunler { get; set; } = new();
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
}

public static class YkcKontrolAkisKurali
{
    public static bool YeniRandevuGerekli(string? kontrolSonucu)
        => string.Equals(
            kontrolSonucu?.Trim(),
            YkcFr265KontrolSonucDegerleri.UygunDegil,
            StringComparison.Ordinal);

    public static int? SiradakiKontrolNo(IEnumerable<Ykc_Fr265Kontrol> kontroller)
    {
        var sonKontrol = kontroller
            .Where(x => !x.SilindiMi
                && x.KontrolNo is >= 1 and <= 5
                && (x.Sonuc == YkcFr265KontrolSonucDegerleri.Uygun
                    || x.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil))
            .OrderBy(x => x.KontrolNo)
            .LastOrDefault();

        if (sonKontrol?.Sonuc != YkcFr265KontrolSonucDegerleri.UygunDegil)
            return null;

        var siradaki = sonKontrol.KontrolNo + 1;
        return siradaki <= 5 ? siradaki : null;
    }

    public static bool KontrolAlaniDolduMu(IEnumerable<Ykc_Fr265Kontrol> kontroller)
    {
        var sonuclar = kontroller
            .Where(x => !x.SilindiMi && x.KontrolNo is >= 1 and <= 5)
            .GroupBy(x => x.KontrolNo)
            .Select(x => x.OrderByDescending(k => k.KontrolTarihi ?? DateTime.MinValue).ThenByDescending(k => k.Id).First())
            .ToList();

        return sonuclar.Count == 5
            && sonuclar.All(x => x.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil);
    }
}

public partial class YkcTalepService
{
    public async Task<List<YkcEkipSecenegi>> EkiplerAsync(int id, AppKullanici kullanici, bool genelYetkili)
    {
        var talep = await YetkiKapsamiUygula(_context.Ykc_Talepler.AsNoTracking().Where(x => !x.SilindiMi), kullanici, genelYetkili)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (talep == null) return new();
        return _planlama.Ekipler.Where(x => x.SirketId == talep.SirketId
            && string.Equals(x.Il.Trim(), talep.Il?.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Bolge.Trim(), talep.Bolge?.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(x => new YkcEkipSecenegi {
                Id = x.Id, SirketId = x.SirketId, Il = x.Il, Bolge = x.Bolge, Ad = x.Ad,
                YonlendirmeTipi = x.YonlendirmeTipi, KullaniciId = x.KullaniciId,
                Secili = x.Ad == talep.AtananEkip && x.KullaniciId == talep.AtananKullaniciId
                    && x.YonlendirmeTipi == talep.AtananKullaniciTipi
            })
            .ToList();
    }

    public async Task<YkcTakvimSonuc> TakvimAsync(YkcTakvimFiltre filtre, AppKullanici kullanici, bool genelYetkili)
    {
        filtre.Baslangic = filtre.Baslangic.Date;
        filtre.Bitis = filtre.Bitis.Date < filtre.Baslangic ? filtre.Baslangic.AddDays(6) : filtre.Bitis.Date;
        if (filtre.Bitis > filtre.Baslangic.AddDays(31)) filtre.Bitis = filtre.Baslangic.AddDays(31);
        var son = filtre.Bitis.AddDays(1);
        filtre.Il = filtre.Il?.Trim();
        filtre.Bolge = filtre.Bolge?.Trim();
        filtre.Personel = filtre.Personel?.Trim();
        var query = YetkiKapsamiUygula(_context.Ykc_Talepler.AsNoTracking().Where(x => !x.SilindiMi), kullanici, genelYetkili, filtre.AktifSirketId)
            .Where(x => x.RandevuTarihi >= filtre.Baslangic && x.RandevuTarihi < son
                && x.Durum != YkcDurumDegerleri.Iptal && x.Durum != YkcDurumDegerleri.Reddedildi);
        if (!string.IsNullOrWhiteSpace(filtre.Il)) query = query.Where(x => x.Il == filtre.Il);
        if (!string.IsNullOrWhiteSpace(filtre.Bolge)) query = query.Where(x => x.Bolge == filtre.Bolge);
        if (!string.IsNullOrWhiteSpace(filtre.Musteri))
        {
            var musteri = filtre.Musteri.Trim();
            query = query.Where(x => x.MusteriAdi != null && x.MusteriAdi.Contains(musteri));
        }
        if (!string.IsNullOrWhiteSpace(filtre.TesisatNo))
        {
            var tesisatNo = filtre.TesisatNo.Trim();
            query = query.Where(x => x.TesisatNo == tesisatNo);
        }
        var projected = from t in query
                        join u in _context.Users.AsNoTracking() on t.AtananKullaniciId equals u.Id into users
                        from u in users.DefaultIfEmpty()
                        select new YkcTakvimKayit {
                            Id = t.Id, Tarih = t.RandevuTarihi!.Value, Saat = t.RandevuSaati,
                            Musteri = t.MusteriAdi, TesisatNo = t.TesisatNo, Adres = t.Adres,
                            Il = t.Il, Bolge = t.Bolge, Personel = u == null ? null : u.AdSoyad,
                            Ekip = t.AtananEkip, Durum = t.Durum
                        };
        if (!string.IsNullOrWhiteSpace(filtre.Personel))
            projected = projected.Where(x => (x.Personel != null && x.Personel.Contains(filtre.Personel))
                || (x.Ekip != null && x.Ekip.Contains(filtre.Personel)));
        var toplam = await projected.CountAsync();
        var gunler = await projected.GroupBy(x => x.Tarih.Date)
            .Select(x => new YkcTakvimGunOzeti { Tarih = x.Key, Toplam = x.Count() }).ToListAsync();
        const int sayfaBoyutu = 25;
        filtre.Sayfa = Math.Clamp(filtre.Sayfa, 1, Math.Max(1, (int)Math.Ceiling(toplam / (double)sayfaBoyutu)));
        return new YkcTakvimSonuc {
            Filtre = filtre, Toplam = toplam, SayfaBoyutu = sayfaBoyutu, Gunler = gunler,
            Kayitlar = await projected.OrderBy(x => x.Tarih).ThenBy(x => x.Saat).ThenBy(x => x.Id)
                .Skip((filtre.Sayfa - 1) * sayfaBoyutu).Take(sayfaBoyutu).ToListAsync()
        };
    }
}
