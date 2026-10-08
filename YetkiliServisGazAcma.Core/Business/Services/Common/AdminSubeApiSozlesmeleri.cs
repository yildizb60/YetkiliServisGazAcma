using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services;

public class AdminSubeListeFiltreDto
{
    public int? SirketId { get; set; }
    public int FirmaId { get; set; }
    public string? Q { get; set; }
}


public sealed class SubeGetirDto
{
    public int Id { get; set; }
}

public class AdminSubeGetirFiltreDto
{
    public int Id { get; set; }
    public int? SirketId { get; set; }
}

public class AdminSubeKaydetDto
{
    public int Id { get; set; }
    public int? SirketId { get; set; }
    public int FirmaId { get; set; }
    public string? SubeAdi { get; set; }
    public string? Il { get; set; }
    public string? Ilce { get; set; }
    public string? Telefon { get; set; }
    public string? Adres { get; set; }
    public bool AktifMi { get; set; }
}

public class AdminSubeDurumDto
{
    public int Id { get; set; }
    public int? SirketId { get; set; }
}

public class AdminSubeListeDto
{
    public List<AdminSubeDto> Subeler { get; set; } = new();
    public List<AdminSubeFirmaDto> Firmalar { get; set; } = new();
}

public class AdminSubeDetayDto : ApiIslemSonuc
{
    public AdminSubeDto? Sube { get; set; }
    public List<AdminSubeFirmaDto> Firmalar { get; set; } = new();
}

public class AdminSubeDto
{
    public int Id { get; set; }
    public int FirmaId { get; set; }
    public string? SubeAdi { get; set; }
    public string? Il { get; set; }
    public string? Ilce { get; set; }
    public string? Telefon { get; set; }
    public string? Adres { get; set; }
    public bool AktifMi { get; set; }
    public string? FirmaAdi { get; set; }
    public string? FirmaEmail { get; set; }
    public string? FirmaTelefon { get; set; }
    public int? FirmaSirketId { get; set; }

    public static AdminSubeDto FromEntity(Ys_Sube sube)
    {
        return new AdminSubeDto
        {
            Id = sube.Id,
            FirmaId = sube.FirmaId,
            SubeAdi = sube.SubeAdi,
            Il = sube.Il,
            Ilce = sube.Ilce,
            Telefon = sube.Telefon,
            Adres = sube.Adres,
            AktifMi = sube.AktifMi,
            FirmaAdi = sube.Firma?.FirmaAdi,
            FirmaEmail = sube.Firma?.Email,
            FirmaTelefon = sube.Firma?.Telefon,
            FirmaSirketId = sube.Firma?.SirketId
        };
    }
}

public class AdminSubeFirmaDto
{
    public int Id { get; set; }
    public string? FirmaAdi { get; set; }
    public string? Email { get; set; }
    public string? Telefon { get; set; }
    public int SirketId { get; set; }

    public static AdminSubeFirmaDto FromEntity(Ys_Firma firma)
    {
        return new AdminSubeFirmaDto
        {
            Id = firma.Id,
            FirmaAdi = firma.FirmaAdi,
            Email = firma.Email,
            Telefon = firma.Telefon,
            SirketId = firma.SirketId
        };
    }
}
