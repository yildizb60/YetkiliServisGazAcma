namespace YetkiliServisGazAcma.Business.Services;

public sealed class MarkaListeFiltreDto
{
    public bool TumunuGetir { get; set; }
    public bool? AktifMi { get; set; }
    public string? Q { get; set; }
}

public sealed class MarkaKaydetDto
{
    public int? Id { get; set; }
    public string? MarkaAdi { get; set; }
    public string? Aciklama { get; set; }
    public bool AktifMi { get; set; } = true;

}

public sealed class MarkaApiDto
{
    public int Id { get; set; }
    public string? MarkaAdi { get; set; }
    public string? Aciklama { get; set; }
    public bool AktifMi { get; set; }

}
