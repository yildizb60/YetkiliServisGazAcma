namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminYetkiBelgesiOnayFiltreDto
    {
        public int? SirketId { get; set; }
    }

    public class AdminYetkiBelgesiOnayGecmisiFiltreDto
    {
        public int? SirketId { get; set; }
        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public string? Q { get; set; }
        public int? Durum { get; set; }
    }

    public class AdminYetkiBelgesiOnayGecmisiListeDto
    {
        public List<AdminYetkiBelgesiOnayDto> Islemler { get; set; } = new();
    }

    public class AdminYetkiBelgesiOnayListeDto
    {
        public List<AdminYetkiBelgesiOnayDto> Bekleyenler { get; set; } = new();
        public List<AdminYetkiBelgesiOnayDto> SuresiDolanlar { get; set; } = new();
        public List<AdminYetkiBelgesiOnayDto> Onaylananlar { get; set; } = new();
        public List<AdminYetkiBelgesiOnayDto> Reddedilenler { get; set; } = new();
    }
    public class YetkiBelgesiFiltreDto
    {
        public int? SirketId { get; set; }
    }

    public class YetkiBelgesiRedDto
    {
        public int Id { get; set; }
        public string? Gerekce { get; set; }
    }

    public class YetkiBelgesiDto : YetkiBelgesiKayitDto
    {
        public int? SirketId { get; set; }
    }

    public class YetkiBelgesiFirmaEkraniDto
    {
        public YetkiBelgesiFirmaDto? Firma { get; set; }
        public List<YetkiBelgesiDto> Belgeler { get; set; } = new();
        public List<string> Bildirimler { get; set; } = new();
    }

    public class YetkiBelgesiFirmaDto
    {
        public int Id { get; set; }
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? VergiNo { get; set; }
        public string? FaaliyetIli { get; set; }
    }

    public class YetkiBelgesiOnayEkraniDto
    {
        public List<YetkiBelgesiDto> Bekleyenler { get; set; } = new();
        public List<YetkiBelgesiDto> SuresiDolanlar { get; set; } = new();
        public List<YetkiBelgesiDto> Onaylananlar { get; set; } = new();
        public List<YetkiBelgesiDto> Reddedilenler { get; set; } = new();
    }
    public class YetkiBelgesiIdDto { public int Id { get; set; } }
    public class YetkiBelgesiKayitDto
    {
    public int Id { get; set; }
    public int FirmaId { get; set; }
    public string? FirmaAdi { get; set; }
    public string? VergiNo { get; set; }
    public string? FirmaYetkiliKisi { get; set; }
    public string? FirmaTelefon { get; set; }
    public string? FirmaAdres { get; set; }
    public string? FirmaFaaliyetIli { get; set; }
    public string? SirketAdi { get; set; }
    public int Durum { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
    public DateTime? YetkiBelgesiBaslangicTarihi { get; set; }
    public DateTime? YetkiBelgesiBitisTarihi { get; set; }
    public string? DosyaYolu { get; set; }
    public string? OnaylayanKullanici { get; set; }
    public DateTime? OnayTarihi { get; set; }
    public string? RedGerekce { get; set; }

    public bool Onaylanabilir { get; set; }
    }
}
