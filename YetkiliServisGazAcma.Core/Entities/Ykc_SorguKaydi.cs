namespace YetkiliServisGazAcma.Entities;

public sealed class Ykc_SorguKaydi
{
    public string Referans { get; set; } = string.Empty;
    public string KullaniciId { get; set; } = string.Empty;
    public string KaynakJson { get; set; } = string.Empty;
    public DateTime GecerlilikTarihi { get; set; }
}
