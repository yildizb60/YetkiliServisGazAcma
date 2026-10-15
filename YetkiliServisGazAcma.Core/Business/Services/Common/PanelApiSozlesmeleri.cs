namespace YetkiliServisGazAcma.Business.Services;

public class AdminYetkiListeFiltreDto
{
    public int? SirketId { get; set; }
    public string? Q { get; set; }
    public int Sayfa { get; set; } = 1;
}

public class PersonelYetkiListeOzeti
{
    public string? Arama { get; set; }
    public int Sayfa { get; set; } = 1;
    public int SayfaBoyutu { get; set; } = 10;
    public int ToplamSayfa { get; set; } = 1;
    public int ToplamPersonel { get; set; }
    public int EslesenPersonel { get; set; }
    public int YetkiliPersonel { get; set; }
    public int TamYetkiAtamalari { get; set; }
    public List<YetkiSirketBasligi> Sirketler { get; set; } = new();
}

public class YetkiSirketBasligi
{
    public int SirketId { get; set; }
    public string? SirketAdi { get; set; }
}

public class YetkiBelgesiRaporFiltre
{
    public int? SirketId { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public string? Tip { get; set; }
}
