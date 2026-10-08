namespace YetkiliServisGazAcma.Business.Services;

public sealed class DagitimSirketListeFiltreDto
{
    public bool TumunuGetir { get; set; }
    public bool? AktifMi { get; set; }
}

public sealed class DagitimSirketApiDto
{
    public int Id { get; set; }
    public string? SirketAdi { get; set; }
    public string? Il { get; set; }
    public string? Telefon { get; set; }
    public string? Email { get; set; }
    public string? Adres { get; set; }
    public bool AktifMi { get; set; }
}

public sealed class PanelSirketDto
{
    public int Id { get; set; }
    public string? SirketAdi { get; set; }
    public string? Il { get; set; }
    public bool AktifMi { get; set; }
}

public sealed class PanelKimlikIstekDto
{
    public int? AktifSirketId { get; set; }
}

public sealed class PanelKimlikApiSonuc
{
    public string? SirketAdi { get; set; }
    public string? Sehir { get; set; }
    public string? FirmaKodu { get; set; }
}

public sealed class UrunKategoriApiDto
{
    public int Id { get; set; }
    public string? Ad { get; set; }
    public string? IconUrl { get; set; }
    public int SiraNo { get; set; }
    public bool AktifMi { get; set; }
}
