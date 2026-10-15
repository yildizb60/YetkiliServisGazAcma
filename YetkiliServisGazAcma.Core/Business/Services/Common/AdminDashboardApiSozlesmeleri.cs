namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminDashboardFiltreDto
    {
        public int? SirketId { get; set; }
    }

    public class PanelBildirimOzeti
    {
        public int OnayBekleyen { get; set; }
        public int SuresiBitecek { get; set; }
    }

    public class AdminDashboardApiDto
    {
        public int ToplamDevreyeAlma { get; set; }
        public int ToplamFirma { get; set; }
        public int OnayBekleyen { get; set; }
        public int SuresiBitecek { get; set; }
        public int ToplamSirket { get; set; }
        public int BuAyDevreyeAlma { get; set; }
        public List<AdminYetkiBelgesiOzetDto> SonYetkiBelgeleri { get; set; } = new();
        public List<AdminDevreyeAlmaOzetDto> SonDevreyeAlmalar { get; set; } = new();
    }

    public class AdminYetkiBelgesiOzetDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string? FirmaAdi { get; set; }
        public string? SirketAdi { get; set; }
        public int Durum { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public DateTime YetkiBelgesiBitisTarihi { get; set; }
    }

    public class AdminDevreyeAlmaOzetDto
    {
        public string? MusteriAdi { get; set; }
        public DateTime DevreyeAlmaTarihi { get; set; }
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string? FirmaAdi { get; set; }
        public string? MarkaAdi { get; set; }
        public string? TesistatNo { get; set; }
        public int Durum { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
    }
}
