using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services;

public sealed class YkcPlanlamaOkumaService

{
    private readonly AppDbContext _context;
    private readonly YkcPlanlamaOptions _planlama;

    public YkcPlanlamaOkumaService(AppDbContext context, IOptions<YkcPlanlamaOptions>? planlama = null)
    {
        _context = context;
        _planlama = planlama?.Value ?? new YkcPlanlamaOptions();
    }

    public async Task<List<YkcEkipSecenegi>> EkiplerAsync(int id, AppKullanici kullanici, bool genelYetkili, int? dogrulanmisSirketId = null)
    {
        var talep = await YkcTalepKapsami.Uygula(_context.Ykc_Talepler.AsNoTracking().Where(x => !x.SilindiMi), kullanici, genelYetkili, dogrulanmisSirketId)
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
        var firmaGorunumu = YkcFirmaSunumu.FirmaKullanicisiMi(kullanici);
        if (filtre.Baslangic.Date == DateTime.MaxValue.Date || filtre.Bitis.Date == DateTime.MaxValue.Date)
            throw new ArgumentException("Takvim tarih aralığı geçersiz.");
        filtre.Baslangic = filtre.Baslangic.Date;
        var azamiBitis = filtre.Baslangic.AddDays(Math.Min(31, (DateTime.MaxValue.Date.AddDays(-1) - filtre.Baslangic).Days));
        filtre.Bitis = filtre.Bitis.Date < filtre.Baslangic
            ? filtre.Baslangic.AddDays(Math.Min(6, (azamiBitis - filtre.Baslangic).Days)) : filtre.Bitis.Date;
        if (filtre.Bitis > azamiBitis) filtre.Bitis = azamiBitis;
        var son = filtre.Bitis.AddDays(1);
        filtre.Il = filtre.Il?.Trim();
        filtre.Bolge = filtre.Bolge?.Trim();
        filtre.Personel = filtre.Personel?.Trim();
        if (firmaGorunumu)
        {
            filtre.Il = null;
            filtre.Bolge = null;
            filtre.Personel = null;
        }
        var kapsamQuery = YkcTalepKapsami.Uygula(_context.Ykc_Talepler.AsNoTracking().Where(x => !x.SilindiMi), kullanici, genelYetkili, filtre.AktifSirketId)
            .Where(x => x.RandevuTarihi >= filtre.Baslangic && x.RandevuTarihi < son
                && x.Durum != YkcDurumDegerleri.Iptal && x.Durum != YkcDurumDegerleri.Reddedildi);

        var ilSecenekleri = firmaGorunumu
            ? new List<string>()
            : await kapsamQuery
                .Where(x => x.Il != null && x.Il != "")
                .Select(x => x.Il!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

        var query = kapsamQuery;
        if (!string.IsNullOrWhiteSpace(filtre.Il)) query = query.Where(x => x.Il == filtre.Il);

        var bolgeSecenekleri = firmaGorunumu
            ? new List<string>()
            : await query
                .Where(x => x.Bolge != null && x.Bolge != "")
                .Select(x => x.Bolge!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

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
                            Ekip = t.AtananEkip,
                            Yonlendirme = t.AtananKullaniciTipi == "CRM187" ? "187 Acil"
                                : t.AtananKullaniciTipi == "Mühendis" ? "Mühendis" : null,
                            Durum = t.Durum
                        };

        var personelEkipDegerleri = firmaGorunumu
            ? new List<string?>()
            : await projected
                .Where(x => (x.Ekip != null && x.Ekip != "") || (x.Personel != null && x.Personel != ""))
                .Select(x => x.Ekip != null && x.Ekip != "" ? x.Ekip : x.Personel)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
        var personelEkipSecenekleri = personelEkipDegerleri.OfType<string>().ToList();

        if (!string.IsNullOrWhiteSpace(filtre.Personel))
            projected = projected.Where(x => (x.Personel != null && x.Personel.Contains(filtre.Personel))
                || (x.Ekip != null && x.Ekip.Contains(filtre.Personel)));
        var toplam = await projected.CountAsync();
        var gunler = await projected.GroupBy(x => x.Tarih.Date)
            .Select(x => new YkcTakvimGunOzeti { Tarih = x.Key, Toplam = x.Count() }).ToListAsync();
        const int sayfaBoyutu = 25;
        filtre.Sayfa = Math.Clamp(filtre.Sayfa, 1, Math.Max(1, (int)Math.Ceiling(toplam / (double)sayfaBoyutu)));
        var siraliKayitlar = projected.OrderBy(x => x.Tarih).ThenBy(x => x.Saat).ThenBy(x => x.Id);
        var takvimDonemi = filtre.GorunumKayitlariniGetir
            && YkcTakvimGorunumKurali.DoneminTumKayitlariGerekli(filtre.Baslangic, filtre.Bitis);
        List<YkcTakvimKayit> gorunumKayitlari;
        List<YkcTakvimKayit> kayitlar;
        if (takvimDonemi)
        {
            gorunumKayitlari = await siraliKayitlar.ToListAsync();
            kayitlar = gorunumKayitlari
                .Skip((filtre.Sayfa - 1) * sayfaBoyutu)
                .Take(sayfaBoyutu)
                .ToList();
        }
        else
        {
            gorunumKayitlari = new List<YkcTakvimKayit>();
            kayitlar = await siraliKayitlar
                .Skip((filtre.Sayfa - 1) * sayfaBoyutu)
                .Take(sayfaBoyutu)
                .ToListAsync();
        }
        if (firmaGorunumu)
        {
            foreach (var kayit in takvimDonemi ? gorunumKayitlari : kayitlar)
            {
                kayit.Personel = null;
                kayit.Ekip = null;
            }
        }
        return new YkcTakvimSonuc {
            Filtre = filtre, Toplam = toplam, SayfaBoyutu = sayfaBoyutu, Gunler = gunler,
            Kayitlar = kayitlar, GorunumKayitlari = gorunumKayitlari,
            IlSecenekleri = ilSecenekleri, BolgeSecenekleri = bolgeSecenekleri,
            PersonelEkipSecenekleri = personelEkipSecenekleri
        };
    }
}
