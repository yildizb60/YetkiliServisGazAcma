using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services;

public class AdminKullaniciListeFiltreDto
{
    public int? SirketId { get; set; }
    public string? Q { get; set; }
    public string? Tip { get; set; }
    public string? Durum { get; set; }
    public string? Bagli { get; set; }
}

public class AdminKullaniciSirketSecenekFiltreDto
{
    public int? SirketId { get; set; }
}

public class AdminKullaniciFirmaSecenekFiltreDto
{
    public int? SirketId { get; set; }
}

public class AdminKullaniciYonetimYetkiDto
{
    public int? SirketId { get; set; }
}

public class AdminKullaniciYonetimYetkiSonucDto
{
    public bool YetkiliMi { get; set; }
}

public class AdminKullaniciGetirDto
{
    public string Id { get; set; } = string.Empty;
    public int? SirketId { get; set; }
}

public class AdminKullaniciGuncelleDto
{
    public string Id { get; set; } = string.Empty;
    public int? KapsamSirketId { get; set; }
    public string? AdSoyad { get; set; }
    public string? Email { get; set; }
    public string? Telefon { get; set; }
    public bool AktifMi { get; set; }
    public int? SirketId { get; set; }
    public int? FirmaId { get; set; }
    public string? YeniSifre { get; set; }
    public string? YeniSifreTekrar { get; set; }
}

public class AdminKullaniciKaydetDto
{
    public int? KapsamSirketId { get; set; }
    public string? AdSoyad { get; set; }
    public string? Email { get; set; }
    public string? Telefon { get; set; }
    public string? Sifre { get; set; }
    public string? Rol { get; set; }
    public int? SirketId { get; set; }
    public int? FirmaId { get; set; }
}

public class AdminPersonelKaydetDto
{
    public int? KapsamSirketId { get; set; }
    public string? AdSoyad { get; set; }
    public string? Email { get; set; }
    public string? Telefon { get; set; }
    public int SirketId { get; set; }
    public string? Sifre { get; set; }
}

public class AdminKullaniciDurumDto
{
    public string Id { get; set; } = string.Empty;
    public int? SirketId { get; set; }
    public bool AktifMi { get; set; }
    public bool SadecePersonel { get; set; }
}

public class AdminKullaniciSilDto
{
    public string Id { get; set; } = string.Empty;
    public int? SirketId { get; set; }
    public bool SadecePersonel { get; set; }
}

public class AdminYetkiGetirDto
{
    public string Id { get; set; } = string.Empty;
    public int? SirketId { get; set; }
}

public class AdminYetkiGuncelleDto
{
    public string Id { get; set; } = string.Empty;
    public int? SirketId { get; set; }
    public List<int> SirketIds { get; set; } = new();
    public Dictionary<int, List<string>> Yetkiler { get; set; } = new();
}

public class AdminYetkiListeDto
{
    public PersonelYetkiListeOzeti Ozet { get; set; } = new();
    public List<AdminKullaniciListeDto> Personeller { get; set; } = new();
    public Dictionary<string, List<string>> YetkiMap { get; set; } = new();
    public Dictionary<string, List<string>> YetkiSirketAdlariMap { get; set; } = new();
    public Dictionary<string, List<AdminSirketYetkiOzetDto>> SirketYetkileri { get; set; } = new();
}

public class AdminSirketYetkiOzetDto
{
    public int SirketId { get; set; }
    public string? SirketAdi { get; set; }
    public List<string> Yetkiler { get; set; } = new();
}

public class AdminYetkiDuzenleDto
{
    public AdminKullaniciListeDto? Personel { get; set; }
    public List<AdminSirketSecenekDto> Sirketler { get; set; } = new();
    public List<string> MevcutYetkiler { get; set; } = new();
    public Dictionary<int, List<string>> YetkiSirketMap { get; set; } = new();
    public List<int> SeciliSirketIds { get; set; } = new();
}

public class AdminSirketSecenekDto
{
    public int Id { get; set; }
    public string? SirketAdi { get; set; }
}

public class AdminFirmaSecenekDto
{
    public int Id { get; set; }
    public string? FirmaAdi { get; set; }
    public int SirketId { get; set; }
    public string? SirketAdi { get; set; }
}

public class AdminKullaniciListeDto
{
    public string Id { get; set; } = string.Empty;
    public string? AdSoyad { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public int KullaniciTipi { get; set; }
    public bool AktifMi { get; set; }
    public int? SirketId { get; set; }
    public string? SirketAdi { get; set; }
    public int? FirmaId { get; set; }
    public string? FirmaAdi { get; set; }
    public string? FirmaYetkiliKisi { get; set; }
    public string? FirmaEmail { get; set; }
    public string? FirmaTelefon { get; set; }
}
