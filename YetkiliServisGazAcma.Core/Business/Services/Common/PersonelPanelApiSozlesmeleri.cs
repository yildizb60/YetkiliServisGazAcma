namespace YetkiliServisGazAcma.Business.Services;

public class PersonelYetkilerimIstek
{
    public int? SirketId { get; set; }
}

public class PersonelYetkilerimCevap
{
    public List<string> Yetkiler { get; set; } = new();
}

public sealed class PersonelRaporFiltreDto
{
    public int? SirketId { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public string? Tip { get; set; }
}

public sealed class PersonelRaporDto
{
    public DateTime BasTarih { get; set; }
    public DateTime BitTarih { get; set; }
    public string RaporTipi { get; set; } = "devreye";
    public List<string> IzinliRaporTipleri { get; set; } = new();
    public int Toplam { get; set; }
    public int Tamamlanan { get; set; }
    public List<string> DonemEtiketleri { get; set; } = new();
    public List<int> DonemSayilari { get; set; } = new();
    public List<string> KirilimEtiketleri { get; set; } = new();
    public List<int> KirilimSayilari { get; set; } = new();
    public List<string> DurumEtiketleri { get; set; } = new();
    public List<int> DurumSayilari { get; set; } = new();
}

public sealed class PersonelDashboardDto
{
    public DateTime Bugun { get; set; }
    public DateTime AyBaslangici { get; set; }
    public DateTime AyBitisi { get; set; }
    public bool BelgeYetkisi { get; set; }
    public bool RaporYetkisi { get; set; }
    public bool ServisYetkisi { get; set; }
    public bool MarkaYetkisi { get; set; }
    public YkcYetkiOzeti YkcYetkileri { get; set; } = new();
    public int OnayBekleyen { get; set; }
    public int SuresiBitecek { get; set; }
    public int ToplamDevreyeAlma { get; set; }
    public int BuAyDevreyeAlma { get; set; }
    public int AktifServis { get; set; }
    public YkcDashboardOzetDto? Ykc { get; set; }
}
