namespace YetkiliServisGazAcma.Entities;

public sealed class Ys_DevreyeAlmaSorguKaydi
{
    public string Referans { get; set; } = string.Empty;
    public string KullaniciId { get; set; } = string.Empty;
    public int FirmaId { get; set; }
    public int DagitimSirketiId { get; set; }
    public string KaynakJson { get; set; } = string.Empty;
    public string KaynakCihazAnahtari { get; set; } = string.Empty;
    public DateTime GecerlilikTarihi { get; set; }
    public int? DevreyeAlmaId { get; set; }
}
