using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services;

public class YsDevreyeAlmaGecmisFiltreDto
{
    public string? Marka { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public string? Musteri { get; set; }
    public string? TesisatNo { get; set; }
    public string? Durum { get; set; }
}

public class YsDevreyeAlmaGetirDto
{
    public int Id { get; set; }
}

public class YsDevreyeAlmaEkranDto
{
    public bool Erisilebilir { get; set; }
    public string? Hata { get; set; }
    public string? RedirectUrl { get; set; }
    public YsFirmaDto? Firma { get; set; }
    public List<YsMarkaDto> Markalar { get; set; } = new();
}

public class YsMarkaKontrolDto
{
    public string? SorguReferansi { get; set; }
}

public class YsDevreyeAlmaBildirimDto
{
    public List<string> Bildirimler { get; set; } = new();
    public int BildirimSayisi { get; set; }
}

public class YsTesisatSorguDto
{
    public string? TesistatNo { get; set; }
    public string? SozlesmeNo { get; set; }
}

public class YsTesisatSorguSonucDto
{
    public bool Basarili { get; set; }
    public string? Mesaj { get; set; }
    public string? TesistatNo { get; set; }
    public string? SozlesmeNo { get; set; }
    public string? AboneNo { get; set; }
    public string? SayacNo { get; set; }
    public string? MusteriAdi { get; set; }
    public string? MusteriTcNo { get; set; }
    public string? MusteriTelefon { get; set; }
    public string? Adres { get; set; }
    public string? UygunlukBelgeNo { get; set; }
    public string? UygunlukTarihi { get; set; }
    public string? Durum { get; set; }
    public List<YsTesisatCihazDto> Cihazlar { get; set; } = new();
}

public class YsTesisatCihazDto
{
    public string? SorguReferansi { get; set; }
    public bool KaydedildiMi { get; set; }
    public string? CihazMarka { get; set; }
    public string? CihazTipi { get; set; }
    public string? CihazKapasite { get; set; }
}

public class YsMarkaKontrolSonucDto
{
    public bool Yetkili { get; set; }
    public string? Mesaj { get; set; }
    public int? MarkaId { get; set; }
    public string? MarkaAdi { get; set; }
}

public class YsDevreyeAlmaKaydetDto
{
    public string? SorguReferansi { get; set; }
    public string? CihazModeli { get; set; }
    public string? SeriNo { get; set; }
    public string? TeknisyenAdi { get; set; }
    public string? TeknisyenYetkiBelgesiNo { get; set; }
    public string? Notlar { get; set; }
}

public class YsDevreyeAlmaIslemSonucDto : ApiIslemSonuc
{
    public string? RedirectUrl { get; set; }
}

public class YsDevreyeAlmaGecmisDto
{
    public List<YsDevreyeAlmaDto> Islemler { get; set; } = new();
    public YsFirmaDto? Firma { get; set; }
    public List<string> MarkaList { get; set; } = new();
    public int Toplam { get; set; }
    public int Tamamlanan { get; set; }
    public int Bekleyen { get; set; }
    public int Iptal { get; set; }
}

public class YsFirmaDto
{
    public int Id { get; set; }
    public string? FirmaAdi { get; set; }
    public string? YetkiliKisi { get; set; }
    public string? Telefon { get; set; }
    public string? Email { get; set; }
    public string? Adres { get; set; }
    public string? FaaliyetIli { get; set; }
    public int SirketId { get; set; }
    public string? SirketAdi { get; set; }
    public string? SirketIl { get; set; }

    public static YsFirmaDto FromEntity(Ys_Firma firma)
    {
        return new YsFirmaDto
        {
            Id = firma.Id,
            FirmaAdi = firma.FirmaAdi,
            YetkiliKisi = firma.YetkiliKisi,
            Telefon = firma.Telefon,
            Email = firma.Email,
            Adres = firma.Adres,
            FaaliyetIli = firma.FaaliyetIli,
            SirketId = firma.SirketId,
            SirketAdi = firma.Sirket?.SirketAdi,
            SirketIl = firma.Sirket?.Il
        };
    }
}

public class DevreyeAlmaKayitDto
{
    public int Id { get; set; }
    public int FirmaId { get; set; }
    public int? MarkaId { get; set; }
    public string? TesistatNo { get; set; }
    public string? AboneNo { get; set; }
    public string? SozlesmeNo { get; set; }
    public string? UygunlukBelgeNo { get; set; }
    public DateTime? UygunlukTarihi { get; set; }
    public string? MusteriAdi { get; set; }
    public string? MusteriTcNo { get; set; }
    public string? MusteriTelefon { get; set; }
    public string? Adres { get; set; }
    public string? CihazTipi { get; set; }
    public string? CihazMarka { get; set; }
    public string? CihazModeli { get; set; }
    public string? CihazKapasite { get; set; }
    public string? SeriNo { get; set; }
    public string? TeknisyenAdi { get; set; }
    public string? TeknisyenYetkiBelgesiNo { get; set; }
    public DateTime DevreyeAlmaTarihi { get; set; }
    public string? Notlar { get; set; }
    public int Durum { get; set; }
    public string? PdfYolu { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
    public string? FirmaAdi { get; set; }
    public string? FirmaFaaliyetIli { get; set; }
    public string? FirmaAdres { get; set; }
    public int FirmaSirketId { get; set; }
    public string? SirketAdi { get; set; }
    public string? MarkaAdi { get; set; }

}

public class YsDevreyeAlmaDto : DevreyeAlmaKayitDto
{
    public string? FirmaYetkiliKisi { get; set; }
    public string? FirmaTelefon { get; set; }
    public string? FirmaEmail { get; set; }
    public string? SirketIl { get; set; }

    public static YsDevreyeAlmaDto FromEntity(Ys_DevreyeAlma islem)
    {
        return new YsDevreyeAlmaDto
        {
            Id = islem.Id,
            FirmaId = islem.FirmaId,
            MarkaId = islem.MarkaId,
            TesistatNo = islem.TesistatNo,
            AboneNo = islem.AboneNo,
            SozlesmeNo = islem.SozlesmeNo,
            UygunlukBelgeNo = islem.UygunlukBelgeNo,
            UygunlukTarihi = islem.UygunlukTarihi,
            MusteriAdi = islem.MusteriAdi,
            MusteriTcNo = islem.MusteriTcNo,
            MusteriTelefon = islem.MusteriTelefon,
            Adres = islem.Adres,
            CihazTipi = islem.CihazTipi,
            CihazMarka = islem.CihazMarka,
            CihazModeli = islem.CihazModeli,
            CihazKapasite = islem.CihazKapasite,
            SeriNo = islem.SeriNo,
            TeknisyenAdi = islem.TeknisyenAdi,
            TeknisyenYetkiBelgesiNo = islem.TeknisyenYetkiBelgesiNo,
            DevreyeAlmaTarihi = islem.DevreyeAlmaTarihi,
            Notlar = islem.Notlar,
            Durum = islem.Durum,
            PdfYolu = islem.PdfYolu,
            OlusturmaTarihi = islem.OlusturmaTarihi,
            FirmaAdi = islem.Firma?.FirmaAdi,
            FirmaYetkiliKisi = islem.Firma?.YetkiliKisi,
            FirmaTelefon = islem.Firma?.Telefon,
            FirmaEmail = islem.Firma?.Email,
            FirmaAdres = islem.Firma?.Adres,
            FirmaFaaliyetIli = islem.Firma?.FaaliyetIli,
            FirmaSirketId = islem.Firma?.SirketId ?? 0,
            SirketAdi = islem.Firma?.Sirket?.SirketAdi,
            SirketIl = islem.Firma?.Sirket?.Il,
            MarkaAdi = islem.Marka?.MarkaAdi
        };
    }
}

public class YsMarkaDto
{
    public int Id { get; set; }
    public string? MarkaAdi { get; set; }
    public string? Aciklama { get; set; }
    public bool AktifMi { get; set; }

    public static YsMarkaDto FromEntity(Ys_Marka marka)
    {
        return new YsMarkaDto
        {
            Id = marka.Id,
            MarkaAdi = marka.MarkaAdi,
            Aciklama = marka.Aciklama,
            AktifMi = marka.AktifMi
        };
    }
}
