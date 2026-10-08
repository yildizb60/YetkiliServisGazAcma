using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services;

public class YkcTalepListeFiltre
{
    public string? BekleyenIs { get; set; }
    public int? SirketId { get; set; }
    public int? FirmaId { get; set; }
    public string? TesisatNo { get; set; }
    public string? MusteriAdi { get; set; }
    public string? SozlesmeNo { get; set; }
    public string? AboneNo { get; set; }
    public string? Firma { get; set; }
    public string? Il { get; set; }
    public string? Ilce { get; set; }
    public string? Bolge { get; set; }
    public string? Ekip { get; set; }
    public string? Marka { get; set; }
    public string? HedefUygulama { get; set; }
    public int? Durum { get; set; }
    public int? KontrolNo { get; set; }
    public List<int>? KayitIdleri { get; set; }
    public int? DetayTalepId { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public int Sayfa { get; set; } = 1;
    public int SayfaBoyutu { get; set; } = 10;
}

public class YkcTalepListeSonuc
{
    public int Toplam { get; set; }
    public int Sayfa { get; set; }
    public int SayfaBoyutu { get; set; }
    public List<YkcTalepDto> Talepler { get; set; } = new();
}

public class YkcDashboardOzetDto
{
    public int IncelemeBekleyen { get; set; }
    public int RandevuBekleyen { get; set; }
    public int TamamlamaBekleyen { get; set; }
    public int Toplam { get; set; }
    public int Incelemede { get; set; }
    public int RandevuSaha { get; set; }
    public int Tamamlanan { get; set; }
    public int ImzaBekleyen { get; set; }
    public int ImzaliNihai { get; set; }
    public int RedIptal { get; set; }
    public List<YkcTalepDto> SonTalepler { get; set; } = new();
}

public class YkcRaporSonuc
{
    public int Toplam { get; set; }
    public int Sayfa { get; set; } = 1;
    public int SayfaBoyutu { get; set; } = 10;
    public int KayitLimiti { get; set; }
    public List<YkcRaporDurumOzetDto> DurumOzetleri { get; set; } = new();
    public List<YkcRaporMetinOzetDto> HedefOzetleri { get; set; } = new();
    public List<YkcRaporMetinOzetDto> EkipOzetleri { get; set; } = new();
    public List<YkcRaporMetinOzetDto> FirmaOzetleri { get; set; } = new();
    public List<YkcRaporKayitDto> Kayitlar { get; set; } = new();
}

public class YkcRaporDurumOzetDto
{
    public int Durum { get; set; }
    public int Sayi { get; set; }
}

public class YkcRaporMetinOzetDto
{
    public string? Ad { get; set; }
    public int Sayi { get; set; }
}

public class YkcTalepKaydetDto
{
    public string? SorguReferansi { get; set; }
    public int? FirmaId { get; set; }
    public int? SirketId { get; set; }
    public string? FirmaKodu { get; set; }
    public string? KaynakTipi { get; set; }
    public string? TesisatNo { get; set; }
    public string? SozlesmeNo { get; set; }
    public string? AboneNo { get; set; }
    public string? ProjeNo { get; set; }
    public string? SayacNo { get; set; }
    public string? MusteriAdi { get; set; }
    public string? MusteriTelefon { get; set; }
    public string? Il { get; set; }
    public string? Ilce { get; set; }
    public string? Bolge { get; set; }
    public string? Adres { get; set; }
    public string? EskiCihazTipiKodu { get; set; }
    public string? EskiCihazTipi { get; set; }
    public string? EskiMarkaKodu { get; set; }
    public string? EskiMarka { get; set; }
    public string? EskiBacaTipiKodu { get; set; }
    public string? EskiBacaTipi { get; set; }
    public string? EskiKapasite { get; set; }
    public string? YeniCihazTipiKodu { get; set; }
    public string? YeniCihazTipi { get; set; }
    public string? YeniMarkaKodu { get; set; }
    public string? YeniMarka { get; set; }
    public string? YeniBacaTipiKodu { get; set; }
    public string? YeniBacaTipi { get; set; }
    public string? YeniKapasite { get; set; }
    public string? YeniModel { get; set; }
    public string? YeniSeriNo { get; set; }
    public bool? IkinciElCihazMi { get; set; }

    [JsonIgnore]
    public Dictionary<string, string?> IzinliYeniCihazTipleri { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class YkcCihazKarsilastirmaIstek
{
    [StringLength(64)] public string? SorguReferansi { get; set; }
    [StringLength(19)] public string? TesisatNo { get; set; }
    [StringLength(19)] public string? SozlesmeNo { get; set; }
    [StringLength(100)] public string? YeniCihazTipi { get; set; }
    [StringLength(100)] public string? YeniMarka { get; set; }
    [StringLength(100)] public string? YeniBacaTipi { get; set; }
    [StringLength(30)] public string? YeniKapasite { get; set; }
}

public class YkcCihazKarsilastirmaSonuc
{
    public bool Basarili { get; set; }
    public string? Mesaj { get; set; }
    public List<string> Uyarilar { get; set; } = [];
}

public class YkcTesisatSorguIstek
{
    public string? TesisatNo { get; set; }
    public string? SozlesmeNo { get; set; }
}

public class YkcTesisatSorguSonuc
{
    public bool Basarili { get; set; }
    public bool ManuelGirisSerbest { get; set; }
    public string? Mesaj { get; set; }
    public string? FirmaKodu { get; set; }
    public string? TesisatNo { get; set; }
    public string? SozlesmeNo { get; set; }
    public string? AboneNo { get; set; }
    public string? SayacNo { get; set; }
    public string? MusteriAdi { get; set; }
    public string? MusteriTelefon { get; set; }
    public string? Il { get; set; }
    public string? Ilce { get; set; }
    public string? Bolge { get; set; }
    public string? Adres { get; set; }
    public string? Durum { get; set; }
    public List<YkcTesisatCihazDto> Cihazlar { get; set; } = new();

    public static YkcTesisatSorguSonuc Basarisiz(string mesaj)
    {
        return new YkcTesisatSorguSonuc
        {
            Basarili = false,
            ManuelGirisSerbest = false,
            Mesaj = mesaj
        };
    }

    public static YkcTesisatSorguSonuc KayitBulunamadi(string mesaj)
    {
        return new YkcTesisatSorguSonuc
        {
            Basarili = false,
            ManuelGirisSerbest = true,
            Mesaj = mesaj
        };
    }
}

public class YkcTesisatCihazDto
{
    public string? SorguReferansi { get; set; }
    public string? CihazKapasite { get; set; }
    public string? CihazMarka { get; set; }
    public string? CihazTipi { get; set; }
    public string? CihazTipKodu { get; set; }
    public string? ProjeNo { get; set; }
    public string? TesisatNo { get; set; }
}

public class YkcAtamaKaydetDto
{
    public string? EkipId { get; set; }
    public int TalepId { get; set; }
    public string? AtananKullaniciId { get; set; }
    public string? AtananKullaniciTipi { get; set; }
    public string? AtananEkip { get; set; }
    public string? Bolge { get; set; }
    public string? HedefUygulama { get; set; }
    public DateTime? RandevuTarihi { get; set; }
    public string? RandevuSaati { get; set; }
    public bool? IkinciElCihazMi { get; set; }
    public bool CallCenterTetiklenecekMi { get; set; }
    public string? Aciklama { get; set; }
}

public class YkcDurumGuncelleDto
{
    public int TalepId { get; set; }
    public int Durum { get; set; }
    public string? Aciklama { get; set; }
}

public class YkcKontrolKaydetDto
{
    public int TalepId { get; set; }
    public List<YkcKontrolSatirKaydetDto> Kontroller { get; set; } = new();
}

public class YkcKontrolSatirKaydetDto
{
    public int KontrolNo { get; set; }
    public string? Sonuc { get; set; }
    public string? Aciklama { get; set; }
}

public class YkcDosyaKaydetDto
{
    public int TalepId { get; set; }
    public string? DosyaTuru { get; set; }
    public string? DosyaAdi { get; set; }
    public string? DosyaYolu { get; set; }
    public string? IcerikTipi { get; set; }
    public long? DosyaBoyutu { get; set; }
    public string? DepolamaTuru { get; set; }
    public string? BelgeHash { get; set; }
}

public class YkcIslemSonuc : ApiIslemSonuc
{

    public static YkcIslemSonuc BasariliSonuc(string mesaj, int? id = null)
    {
        return new YkcIslemSonuc { Basarili = true, Mesaj = mesaj, Id = id };
    }

    public static YkcIslemSonuc HataliSonuc(string mesaj)
    {
        return new YkcIslemSonuc { Basarili = false, Mesaj = mesaj };
    }
}

public class YkcCihazListeBilgisi
{
    public string? Tip { get; set; }
    public string? Marka { get; set; }
    public string? BacaTipi { get; set; }
    public string? Kapasite { get; set; }
}

public class YkcTalepDto
{
    public int Id { get; set; }
    public string? FirmaAdi { get; set; }
    public string? SirketAdi { get; set; }
    public string? TesisatNo { get; set; }
    public string? SozlesmeNo { get; set; }
    public string? AboneNo { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjeNo { get; set; }
    public string? MusteriAdi { get; set; }
    public string? Il { get; set; }
    public string? Ilce { get; set; }
    public string? Bolge { get; set; }
    public string? EskiCihaz { get; set; }
    public string? YeniCihaz { get; set; }
    public YkcCihazListeBilgisi? ProjedekiCihazBilgisi { get; set; }
    public YkcCihazListeBilgisi? YeniCihazBilgisi { get; set; }
    public int Durum { get; set; }
    public DateTime TalepTarihi { get; set; }
    public string? AtananEkip { get; set; }
    public string? HedefUygulama { get; set; }
    public DateTime? RandevuTarihi { get; set; }
    public string? RandevuSaati { get; set; }
    public int? SiradakiKontrolNo { get; set; }
    public bool KontrolAlaniDolduMu { get; set; }
    public bool? IkinciElCihazMi { get; set; }

    public static YkcTalepDto FromEntity(Ykc_Talep talep)
    {
        return new YkcTalepDto
        {
            Id = talep.Id,
            FirmaAdi = talep.Firma?.FirmaAdi,
            SirketAdi = talep.Sirket?.SirketAdi,
            TesisatNo = talep.TesisatNo,
            SozlesmeNo = talep.SozlesmeNo,
            AboneNo = talep.AboneNo,
            ProjeNo = talep.ProjeNo,
            MusteriAdi = talep.MusteriAdi,
            Il = talep.Il,
            Ilce = talep.Ilce,
            Bolge = YkcBolgeAtamaKurali.BolgeBelirle(talep.Bolge, talep.Il),
            EskiCihaz = CihazOzeti(talep.EskiMarka, talep.EskiCihazTipi, talep.EskiKapasite),
            YeniCihaz = CihazOzeti(talep.YeniMarka, talep.YeniCihazTipi, talep.YeniKapasite),
            ProjedekiCihazBilgisi = new() { Tip = talep.EskiCihazTipi, Marka = talep.EskiMarka, BacaTipi = talep.EskiBacaTipi, Kapasite = talep.EskiKapasite },
            YeniCihazBilgisi = new() { Tip = talep.YeniCihazTipi, Marka = talep.YeniMarka, BacaTipi = talep.YeniBacaTipi, Kapasite = talep.YeniKapasite },
            Durum = talep.Durum,
            TalepTarihi = talep.TalepTarihi,
            AtananEkip = talep.AtananEkip,
            HedefUygulama = talep.HedefUygulama,
            RandevuTarihi = talep.RandevuTarihi,
            RandevuSaati = talep.RandevuSaati,
            SiradakiKontrolNo = YkcKontrolAkisKurali.SiradakiKontrolNo(talep.Kontroller),
            KontrolAlaniDolduMu = YkcKontrolAkisKurali.KontrolAlaniDolduMu(talep.Kontroller),
            IkinciElCihazMi = talep.IkinciElCihazMi
        };
    }

    private static string CihazOzeti(params string?[] parcalar)
    {
        return string.Join(" / ", parcalar.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
    }
}

public class YkcRaporKayitDto
{
    public int Id { get; set; }
    public string? FirmaAdi { get; set; }
    public string? FirmaVergiNo { get; set; }
    public string? FirmaFaaliyetIli { get; set; }
    public string? SirketAdi { get; set; }
    public string? MusteriAdi { get; set; }
    public string? TesisatNo { get; set; }
    public string? SozlesmeNo { get; set; }
    public string? AboneNo { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjeNo { get; set; }
    public string? SayacNo { get; set; }
    public string? Il { get; set; }
    public string? Ilce { get; set; }
    public string? Adres { get; set; }
    public string? EskiCihazTipi { get; set; }
    public string? EskiMarka { get; set; }
    public string? EskiKapasite { get; set; }
    public string? EskiBacaTipi { get; set; }
    public string? YeniCihazTipi { get; set; }
    public string? YeniMarka { get; set; }
    public string? YeniModel { get; set; }
    public string? YeniKapasite { get; set; }
    public string? YeniBacaTipi { get; set; }
    public bool? IkinciElCihazMi { get; set; }
    public string? Bolge { get; set; }
    public string? AtananEkip { get; set; }
    public string? HedefUygulama { get; set; }
    public DateTime TalepTarihi { get; set; }
    public DateTime? RandevuTarihi { get; set; }
    public string? RandevuSaati { get; set; }
    public int? SiradakiKontrolNo { get; set; }
    public int Durum { get; set; }
    public bool ImzaliNihaiBelgeVar { get; set; }
    public int? ImzaliNihaiDosyaId { get; set; }
    public string? ImzaDurumu { get; set; }

    public static YkcRaporKayitDto FromEntity(Ykc_Talep talep)
    {
        var imzaliNihaiDosyaId = ImzaliNihaiDosyaIdBul(talep);
        return new YkcRaporKayitDto
        {
            Id = talep.Id,
            FirmaAdi = talep.Firma?.FirmaAdi,
            FirmaVergiNo = talep.Firma?.VergiNo,
            FirmaFaaliyetIli = talep.Firma?.FaaliyetIli,
            SirketAdi = talep.Sirket?.SirketAdi,
            MusteriAdi = talep.MusteriAdi,
            TesisatNo = talep.TesisatNo,
            SozlesmeNo = talep.SozlesmeNo,
            AboneNo = talep.AboneNo,
            ProjeNo = talep.ProjeNo,
            SayacNo = talep.SayacNo,
            Il = talep.Il,
            Ilce = talep.Ilce,
            Adres = talep.Adres,
            EskiCihazTipi = talep.EskiCihazTipi,
            EskiMarka = talep.EskiMarka,
            EskiKapasite = talep.EskiKapasite,
            EskiBacaTipi = talep.EskiBacaTipi,
            YeniCihazTipi = talep.YeniCihazTipi,
            YeniMarka = talep.YeniMarka,
            YeniModel = talep.YeniModel,
            YeniKapasite = talep.YeniKapasite,
            YeniBacaTipi = talep.YeniBacaTipi,
            IkinciElCihazMi = talep.IkinciElCihazMi,
            Bolge = YkcBolgeAtamaKurali.BolgeBelirle(talep.Bolge, talep.Il),
            AtananEkip = talep.AtananEkip,
            HedefUygulama = talep.HedefUygulama,
            TalepTarihi = talep.TalepTarihi,
            RandevuTarihi = talep.RandevuTarihi,
            RandevuSaati = talep.RandevuSaati,
            SiradakiKontrolNo = YkcKontrolAkisKurali.SiradakiKontrolNo(talep.Kontroller),
            Durum = talep.Durum,
            ImzaDurumu = AktifImzaSureci(talep)?.Durum ?? YkcImzaDurumDegerleri.Hazir,
            ImzaliNihaiBelgeVar = imzaliNihaiDosyaId.HasValue,
            ImzaliNihaiDosyaId = imzaliNihaiDosyaId
        };
    }

    private static Ykc_ImzaSureci? AktifImzaSureci(Ykc_Talep talep)
    {
        return talep.ImzaSurecleri
            .Where(x => !x.SilindiMi)
            .OrderByDescending(x => x.BelgeVersiyonu)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();
    }

    private static int? ImzaliNihaiDosyaIdBul(Ykc_Talep talep)
    {
        var tamamlananSurec = talep.ImzaSurecleri
            .Where(x => !x.SilindiMi)
            .OrderByDescending(x => x.BelgeVersiyonu)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault(x =>
                x.Durum == YkcImzaDurumDegerleri.Tamamlandi
                && !string.IsNullOrWhiteSpace(x.ProviderDocumentId)
                && x.NihaiDosyaId.HasValue);

        return tamamlananSurec?.NihaiDosyaId is int nihaiDosyaId
            && talep.FormDosyalari.Any(x =>
                x.Id == nihaiDosyaId
                && !x.SilindiMi
                && x.DosyaTuru == YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai)
            ? nihaiDosyaId : null;
    }
}

public class YkcTalepDetayDto : YkcTalepDto
{
    public YkcTalepEkranDto Ekran { get; set; } = new();
    public string? SayacNo { get; set; }
    public string? MusteriTelefon { get; set; }
    public string? Adres { get; set; }
    public string? FirmaYetkiliKisi { get; set; }
    public string? YetkiBelgesiNo { get; set; }
    public string? TuketimNoktasi { get; set; }
    public string? BaglantiNesnesi { get; set; }
    public string? FirmaKodu { get; set; }
    public string? KaynakTipi { get; set; }
    public string? EskiCihazTipiKodu { get; set; }
    public string? EskiCihazTipi { get; set; }
    public string? EskiMarkaKodu { get; set; }
    public string? EskiMarka { get; set; }
    public string? EskiBacaTipiKodu { get; set; }
    public string? EskiBacaTipi { get; set; }
    public string? EskiKapasite { get; set; }
    public string? YeniCihazTipiKodu { get; set; }
    public string? YeniCihazTipi { get; set; }
    public string? YeniMarkaKodu { get; set; }
    public string? YeniMarka { get; set; }
    public string? YeniBacaTipiKodu { get; set; }
    public string? YeniBacaTipi { get; set; }
    public string? YeniKapasite { get; set; }
    public string? YeniModel { get; set; }
    public string? YeniSeriNo { get; set; }
    public string? GazDagitimYetkilisiAdi { get; set; }
    public DateTime? GazDagitimIslemTarihi { get; set; }
    public DateTime? Fr265BelgeOlusturmaTarihi { get; set; }
    public int Fr265BelgeVersiyonNo { get; set; }
    public string? Fr265BelgeHash { get; set; }
    public string? RedAciklama { get; set; }
    public DateTime? IptalTarihi { get; set; }
    public string? IptalEdenKullaniciId { get; set; }
    public string? IptalAciklama { get; set; }
    public bool CallCenterTetiklenecekMi { get; set; }
    public List<YkcDosyaDto> Dosyalar { get; set; } = new();
    public List<YkcAtamaDto> Atamalar { get; set; } = new();
    [JsonIgnore]
    public int? AktifAtamaId => Atamalar.OrderByDescending(x => x.Id).FirstOrDefault() is { } atama
        && atama.RandevuTarihi?.Date == RandevuTarihi?.Date && atama.RandevuSaati == RandevuSaati
            ? atama.Id : null;
    public List<YkcGecmisDto> Gecmis { get; set; } = new();
    public List<YkcFr265KontrolDto> Kontroller { get; set; } = new();
    public int KontrolDonemi => YkcKontrolAkisKurali.DonemNo(YkcKontrolAkisKurali.DonemBaslangici(Kontroller.Select(x => x.KontrolNo)));
    [JsonIgnore]
    public List<YkcFr265KontrolDto> AktifKontroller
    {
        get
        {
            var donem = KontrolDonemi;
            return Kontroller.Where(x => x.KontrolNo > 0 && x.DonemNo == donem)
                .GroupBy(x => x.KontrolNo)
                .Select(x => x.OrderByDescending(k => k.KontrolTarihi).ThenByDescending(k => k.Id).First())
                .OrderBy(x => x.KontrolNo).ToList();
        }
    }
    public YkcImzaSureciDto? ImzaSureci { get; set; }

    public new static YkcTalepDetayDto FromEntity(Ykc_Talep talep)
    {
        var dto = new YkcTalepDetayDto
        {
            Id = talep.Id,
            FirmaAdi = talep.Firma?.FirmaAdi,
            SirketAdi = talep.Sirket?.SirketAdi,
            TesisatNo = talep.TesisatNo,
            ProjeNo = talep.ProjeNo,
            MusteriAdi = talep.MusteriAdi,
            Il = talep.Il,
            Ilce = talep.Ilce,
            Bolge = YkcBolgeAtamaKurali.BolgeBelirle(talep.Bolge, talep.Il),
            EskiCihaz = YkcTalepDto.FromEntity(talep).EskiCihaz,
            YeniCihaz = YkcTalepDto.FromEntity(talep).YeniCihaz,
            Durum = talep.Durum,
            TalepTarihi = talep.TalepTarihi,
            AtananEkip = talep.AtananEkip,
            HedefUygulama = talep.HedefUygulama,
            SozlesmeNo = talep.SozlesmeNo,
            AboneNo = talep.AboneNo,
            SayacNo = talep.SayacNo,
            MusteriTelefon = talep.MusteriTelefon,
            Adres = talep.Adres,
            FirmaYetkiliKisi = talep.Firma?.YetkiliKisi,
            YetkiBelgesiNo = YetkiBelgesiNoBul(talep),
            TuketimNoktasi = "",
            BaglantiNesnesi = "",
            FirmaKodu = talep.FirmaKodu,
            KaynakTipi = talep.KaynakTipi,
            EskiCihazTipiKodu = talep.EskiCihazTipiKodu,
            EskiCihazTipi = talep.EskiCihazTipi,
            EskiMarkaKodu = talep.EskiMarkaKodu,
            EskiMarka = talep.EskiMarka,
            EskiBacaTipiKodu = talep.EskiBacaTipiKodu,
            EskiBacaTipi = talep.EskiBacaTipi,
            EskiKapasite = talep.EskiKapasite,
            YeniCihazTipiKodu = talep.YeniCihazTipiKodu,
            YeniCihazTipi = talep.YeniCihazTipi,
            YeniMarkaKodu = talep.YeniMarkaKodu,
            YeniMarka = talep.YeniMarka,
            YeniBacaTipiKodu = talep.YeniBacaTipiKodu,
            YeniBacaTipi = talep.YeniBacaTipi,
            YeniKapasite = talep.YeniKapasite,
            YeniModel = talep.YeniModel,
            YeniSeriNo = talep.YeniSeriNo,
            IkinciElCihazMi = talep.IkinciElCihazMi,
            Fr265BelgeOlusturmaTarihi = talep.Fr265BelgeOlusturmaTarihi,
            Fr265BelgeVersiyonNo = talep.Fr265BelgeVersiyonNo,
            Fr265BelgeHash = talep.Fr265BelgeHash,
            RedAciklama = talep.RedAciklama,
            IptalTarihi = talep.IptalTarihi,
            IptalEdenKullaniciId = talep.IptalEdenKullaniciId,
            IptalAciklama = talep.IptalAciklama,
            RandevuSaati = talep.RandevuSaati,
            RandevuTarihi = talep.RandevuTarihi,
            CallCenterTetiklenecekMi = talep.CallCenterTetiklenecekMi,
            Dosyalar = talep.FormDosyalari.OrderByDescending(x => x.OlusturmaTarihi).Select(YkcDosyaDto.FromEntity).ToList(),
            Atamalar = talep.Atamalar.Where(x => !x.SilindiMi).OrderByDescending(x => x.Id).Select(YkcAtamaDto.FromEntity).ToList(),
            Kontroller = talep.Kontroller.Where(x => !x.SilindiMi).OrderBy(x => x.KontrolNo).Select(YkcFr265KontrolDto.FromEntity).ToList(),
            ImzaSureci = talep.ImzaSurecleri
                .Where(x => !x.SilindiMi)
                .OrderByDescending(x => x.BelgeVersiyonu)
                .ThenByDescending(x => x.Id)
                .Select(YkcImzaSureciDto.FromEntity)
                .FirstOrDefault(),
            Gecmis = TekilGecmis(talep.IslemGecmisi)
        };

        return dto;
    }

    private static string? YetkiBelgesiNoBul(Ykc_Talep talep)
    {
        // Gerçek sertifika numarası alanı netleşmeden veritabanı Id değeri resmi forma basılmamalı.
        return null;
    }

    private static List<YkcGecmisDto> TekilGecmis(IEnumerable<Ykc_IslemGecmisi> gecmisler)
    {
        return gecmisler
            .OrderByDescending(x => x.OlusturmaTarihi)
            .ThenByDescending(x => x.Id)
            .GroupBy(x => new
            {
                x.IslemTipi,
                x.EskiDurum,
                x.YeniDurum,
                Aciklama = x.Aciklama?.Trim() ?? "",
                KullaniciAdi = x.KullaniciAdi?.Trim() ?? "",
                Dakika = new DateTime(
                    x.OlusturmaTarihi.Year,
                    x.OlusturmaTarihi.Month,
                    x.OlusturmaTarihi.Day,
                    x.OlusturmaTarihi.Hour,
                    x.OlusturmaTarihi.Minute,
                    0)
            })
            .Select(x => x.First())
            .OrderByDescending(x => x.OlusturmaTarihi)
            .ThenByDescending(x => x.Id)
            .Select(YkcGecmisDto.FromEntity)
            .ToList();
    }
}

public class YkcDosyaDto
{
    public int Id { get; set; }
    public string? DosyaTuru { get; set; }
    public string? DosyaAdi { get; set; }
    public string? DosyaYolu { get; set; }
    public string? IcerikTipi { get; set; }
    public string? DepolamaTuru { get; set; }
    public string? BelgeHash { get; set; }
    public DateTime OlusturmaTarihi { get; set; }

    public static YkcDosyaDto FromEntity(Ykc_FormDosya dosya)
    {
        return new YkcDosyaDto
        {
            Id = dosya.Id,
            DosyaTuru = dosya.DosyaTuru,
            DosyaAdi = dosya.DosyaAdi,
            DosyaYolu = dosya.DosyaYolu,
            IcerikTipi = dosya.IcerikTipi,
            DepolamaTuru = dosya.DepolamaTuru,
            BelgeHash = dosya.BelgeHash,
            OlusturmaTarihi = dosya.OlusturmaTarihi
        };
    }
}

public class YkcFr265KontrolDto
{
    public int Id { get; set; }
    public int? AtamaId { get; set; }
    public int KontrolNo { get; set; }
    public int DonemNo => YkcKontrolAkisKurali.DonemNo(KontrolNo);
    public int FormKontrolNo => YkcKontrolAkisKurali.FormKontrolNo(KontrolNo);
    public string Sonuc { get; set; } = YkcFr265KontrolSonucDegerleri.Bekliyor;
    public string? Aciklama { get; set; }
    public string? KontrolEdenKullaniciId { get; set; }
    public string? KontrolEdenAdi { get; set; }
    public DateTime? KontrolTarihi { get; set; }

    public static YkcFr265KontrolDto FromEntity(Ykc_Fr265Kontrol kontrol)
    {
        return new YkcFr265KontrolDto
        {
            Id = kontrol.Id,
            AtamaId = kontrol.AtamaId,
            KontrolNo = kontrol.KontrolNo,
            Sonuc = kontrol.Sonuc,
            Aciklama = kontrol.Aciklama,
            KontrolEdenKullaniciId = kontrol.KontrolEdenKullaniciId,
            KontrolTarihi = kontrol.KontrolTarihi
        };
    }
}

public class YkcImzaSureciDto
{
    public int Id { get; set; }
    public string? ProviderDocumentId { get; set; }
    public int BelgeVersiyonu { get; set; }
    public string Durum { get; set; } = YkcImzaDurumDegerleri.Hazir;
    public DateTime? GonderimTarihi { get; set; }
    public DateTime? TamamlanmaTarihi { get; set; }
    public DateTime? SonKontrolTarihi { get; set; }
    public string? HataKodu { get; set; }
    public string? HataMesaji { get; set; }
    public int? NihaiDosyaId { get; set; }
    public string? BelgeHash { get; set; }
    public DateTime? BelgeOlusturmaTarihi { get; set; }
    public List<YkcImzaciDto> Imzacilar { get; set; } = new();

    public static YkcImzaSureciDto FromEntity(Ykc_ImzaSureci surec)
    {
        return new YkcImzaSureciDto
        {
            Id = surec.Id,
            ProviderDocumentId = surec.ProviderDocumentId,
            BelgeVersiyonu = surec.BelgeVersiyonu,
            Durum = surec.Durum,
            GonderimTarihi = surec.GonderimTarihi,
            TamamlanmaTarihi = surec.TamamlanmaTarihi,
            SonKontrolTarihi = surec.SonKontrolTarihi,
            HataKodu = surec.HataKodu,
            HataMesaji = surec.HataMesaji,
            NihaiDosyaId = surec.NihaiDosyaId,
            BelgeHash = surec.BelgeHash,
            BelgeOlusturmaTarihi = surec.BelgeOlusturmaTarihi,
            Imzacilar = surec.Imzacilar
                .OrderBy(x => x.SiraNo)
                .Select(YkcImzaciDto.FromEntity)
                .ToList()
        };
    }
}

public class YkcImzaciDto
{
    public int Id { get; set; }
    public string? Rol { get; set; }
    public string? AdSoyad { get; set; }
    public string? KullaniciId { get; set; }
    public int SiraNo { get; set; }
    public string? Durum { get; set; }
    public DateTime? ImzaTarihi { get; set; }

    public static YkcImzaciDto FromEntity(Ykc_Imzaci imzaci)
    {
        return new YkcImzaciDto
        {
            Id = imzaci.Id,
            Rol = imzaci.Rol,
            AdSoyad = imzaci.AdSoyad,
            KullaniciId = imzaci.KullaniciId,
            SiraNo = imzaci.SiraNo,
            Durum = imzaci.Durum,
            ImzaTarihi = imzaci.ImzaTarihi
        };
    }
}

public class YkcAtamaDto
{
    public int Id { get; set; }
    public string? AtananKullaniciTipi { get; set; }
    public string? AtananEkip { get; set; }
    public string? Bolge { get; set; }
    public string? HedefUygulama { get; set; }
    public DateTime? RandevuTarihi { get; set; }
    public string? RandevuSaati { get; set; }
    public string? Aciklama { get; set; }
    public DateTime OlusturmaTarihi { get; set; }

    public static YkcAtamaDto FromEntity(Ykc_Atama atama)
    {
        return new YkcAtamaDto
        {
            Id = atama.Id,
            AtananKullaniciTipi = atama.AtananKullaniciTipi,
            AtananEkip = atama.AtananEkip,
            Bolge = atama.Bolge,
            HedefUygulama = atama.HedefUygulama,
            RandevuTarihi = atama.RandevuTarihi,
            RandevuSaati = atama.RandevuSaati,
            Aciklama = atama.Aciklama,
            OlusturmaTarihi = atama.OlusturmaTarihi
        };
    }
}

public class YkcGecmisDto
{
    public int Id { get; set; }
    public string? IslemTipi { get; set; }
    public int? EskiDurum { get; set; }
    public int? YeniDurum { get; set; }
    public string? Aciklama { get; set; }
    public string? KullaniciAdi { get; set; }
    public DateTime OlusturmaTarihi { get; set; }

    public static YkcGecmisDto FromEntity(Ykc_IslemGecmisi gecmis)
    {
        return new YkcGecmisDto
        {
            Id = gecmis.Id,
            IslemTipi = gecmis.IslemTipi,
            EskiDurum = gecmis.EskiDurum,
            YeniDurum = gecmis.YeniDurum,
            Aciklama = gecmis.Aciklama,
            KullaniciAdi = gecmis.KullaniciAdi,
            OlusturmaTarihi = gecmis.OlusturmaTarihi
        };
    }
}
