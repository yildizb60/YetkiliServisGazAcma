using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services;

public class AdminDevreyeAlmaListeFiltreDto
{
    public int? SirketId { get; set; }
    public string? TesisatNo { get; set; }
    public string? Musteri { get; set; }
    public string? Marka { get; set; }
    public string? Servis { get; set; }
    public string? Il { get; set; }
    public string? Ilce { get; set; }
    public int? Durum { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
}

public class AdminDevreyeAlmaGetirFiltreDto
{
    public int Id { get; set; }
    public int? SirketId { get; set; }
}

public class AdminDevreyeAlmaRaporExportFiltreDto
{
    public int? SirketId { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public List<int>? Ids { get; set; }
}

public class AdminYetkiBelgesiUyariFiltreDto
{
    public int? SirketId { get; set; }
}

public class AdminRaporOzetFiltreDto
{
    public int? SirketId { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public string? Tip { get; set; }
}

public class AdminDevreyeAlmaListeDto
{
    public List<AdminDevreyeAlmaDto> Islemler { get; set; } = new();
    public List<string> Sehirler { get; set; } = new();
    public List<AdminMarkaSecenekDto> Markalar { get; set; } = new();
    public Dictionary<int, string> FirmaIlceleri { get; set; } = new();
}

public class AdminYetkiBelgesiUyariListeDto
{
    public List<AdminYetkiBelgesiOnayDto> Yaklasan { get; set; } = new();
    public List<AdminYetkiBelgesiOnayDto> Gecmis { get; set; } = new();
}

public class AdminRaporOzetDto
{
    public DateTime BasTarih { get; set; }
    public DateTime BitTarih { get; set; }
    public string RaporTipi { get; set; } = "devreye";
    public string ListeTipi { get; set; } = "devreye";
    public int DevreyeSayisi { get; set; }
    public int DevreyeTamamlanan { get; set; }
    public int DevreyeBekleyen { get; set; }
    public int DevreyeIptal { get; set; }
    public int YetkiBelgesiOnayli { get; set; }
    public int YetkiBelgesiBekleyen { get; set; }
    public int YetkiBelgesiReddedilen { get; set; }
    public int OperasyonTalepSayisi { get; set; }
    public int OperasyonTamamlanan { get; set; }
    public int OperasyonAktif { get; set; }
    public int OperasyonReddedilen { get; set; }
    public int OperasyonIptal { get; set; }
    public double OrtalamaTamamlanmaSaati { get; set; }
    public int TamamlanmaSuresiKayitSayisi { get; set; }
    public double IlkKontrolUygunlukOrani { get; set; }
    public int IlkKontrolKayitSayisi { get; set; }
    public double TekrarRandevuOrani { get; set; }
    public int KontrolEdilenTalepSayisi { get; set; }
    public List<string> OperasyonAylikLabels { get; set; } = new();
    public List<int> OperasyonAylikData { get; set; } = new();
    public List<string> OperasyonFirmaLabels { get; set; } = new();
    public List<int> OperasyonFirmaData { get; set; } = new();
    public List<string> OperasyonLokasyonLabels { get; set; } = new();
    public List<int> OperasyonLokasyonData { get; set; } = new();
    public List<string> OperasyonEkipLabels { get; set; } = new();
    public List<int> OperasyonEkipData { get; set; } = new();
    public List<string> OperasyonRedNedeniLabels { get; set; } = new();
    public List<int> OperasyonRedNedeniData { get; set; } = new();
    public List<string?> ChartSirketLabels { get; set; } = new();
    public List<int> ChartSirketData { get; set; } = new();
    public List<string> ChartAylikLabels { get; set; } = new();
    public List<int> ChartAylikData { get; set; } = new();
    public List<int> ChartDurumData { get; set; } = new();
    public List<string?> ChartMarkaLabels { get; set; } = new();
    public List<int> ChartMarkaData { get; set; } = new();
    public List<AdminDevreyeAlmaDto> SonIslemler { get; set; } = new();
    public List<AdminYetkiBelgesiOnayDto> YetkiBelgesiIslemler { get; set; } = new();
    public List<AdminSirketSecenekDto> Sirketler { get; set; } = new();
}

public class AdminMarkaSecenekDto
{
    public int Id { get; set; }
    public string? MarkaAdi { get; set; }
}

public class AdminDevreyeAlmaDto : DevreyeAlmaKayitDto
{
    public static AdminDevreyeAlmaDto FromEntity(Ys_DevreyeAlma devreyeAlma)
    {
        return new AdminDevreyeAlmaDto
        {
            Id = devreyeAlma.Id,
            FirmaId = devreyeAlma.FirmaId,
            MarkaId = devreyeAlma.MarkaId,
            TesistatNo = devreyeAlma.TesistatNo,
            AboneNo = devreyeAlma.AboneNo,
            SozlesmeNo = devreyeAlma.SozlesmeNo,
            UygunlukBelgeNo = devreyeAlma.UygunlukBelgeNo,
            UygunlukTarihi = devreyeAlma.UygunlukTarihi,
            MusteriAdi = devreyeAlma.MusteriAdi,
            MusteriTcNo = devreyeAlma.MusteriTcNo,
            MusteriTelefon = devreyeAlma.MusteriTelefon,
            Adres = devreyeAlma.Adres,
            CihazTipi = devreyeAlma.CihazTipi,
            CihazMarka = devreyeAlma.CihazMarka,
            CihazModeli = devreyeAlma.CihazModeli,
            CihazKapasite = devreyeAlma.CihazKapasite,
            SeriNo = devreyeAlma.SeriNo,
            TeknisyenAdi = devreyeAlma.TeknisyenAdi,
            TeknisyenYetkiBelgesiNo = devreyeAlma.TeknisyenYetkiBelgesiNo,
            DevreyeAlmaTarihi = devreyeAlma.DevreyeAlmaTarihi,
            Notlar = devreyeAlma.Notlar,
            Durum = devreyeAlma.Durum,
            PdfYolu = devreyeAlma.PdfYolu,
            OlusturmaTarihi = devreyeAlma.OlusturmaTarihi,
            FirmaAdi = devreyeAlma.Firma?.FirmaAdi,
            FirmaFaaliyetIli = devreyeAlma.Firma?.FaaliyetIli,
            FirmaAdres = devreyeAlma.Firma?.Adres,
            FirmaSirketId = devreyeAlma.Firma?.SirketId ?? 0,
            SirketAdi = devreyeAlma.Firma?.Sirket?.SirketAdi,
            MarkaAdi = devreyeAlma.Marka?.MarkaAdi
        };
    }
}

public class AdminYetkiBelgesiOnayDto : YetkiBelgesiKayitDto
{
    public static AdminYetkiBelgesiOnayDto FromEntity(Ys_YetkiBelgesi yetkiBelgesi)
    {
        return new AdminYetkiBelgesiOnayDto
        {
            Id = yetkiBelgesi.Id,
            Onaylanabilir = YetkiBelgesiService.OnaylanabilirMi(yetkiBelgesi, DateTime.Today),
            FirmaId = yetkiBelgesi.FirmaId,
            FirmaAdi = yetkiBelgesi.Firma?.FirmaAdi,
            VergiNo = yetkiBelgesi.Firma?.VergiNo,
            FirmaYetkiliKisi = yetkiBelgesi.Firma?.YetkiliKisi,
            FirmaTelefon = yetkiBelgesi.Firma?.Telefon,
            FirmaAdres = yetkiBelgesi.Firma?.Adres,
            FirmaFaaliyetIli = yetkiBelgesi.Firma?.FaaliyetIli,
            SirketAdi = yetkiBelgesi.Firma?.Sirket?.SirketAdi,
            Durum = yetkiBelgesi.Durum,
            OlusturmaTarihi = yetkiBelgesi.OlusturmaTarihi,
            YetkiBelgesiBaslangicTarihi = yetkiBelgesi.YetkiBelgesiBaslangicTarihi,
            YetkiBelgesiBitisTarihi = yetkiBelgesi.YetkiBelgesiBitisTarihi,
            DosyaYolu = string.IsNullOrWhiteSpace(yetkiBelgesi.DosyaYolu) ? null : YetkiBelgesiService.GuvenliDosyaLinki(yetkiBelgesi.Id),
            OnaylayanKullanici = yetkiBelgesi.OnaylayanKullanici,
            OnayTarihi = yetkiBelgesi.OnayTarihi,
            RedGerekce = yetkiBelgesi.RedGerekce
        };
    }
}
