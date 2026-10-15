namespace YetkiliServisGazAcma.Business.Services;

// API tarafinda hazirlanir; istemci islem kurallarini yeniden hesaplamaz.
public sealed class YkcTalepEkranDto
{
    public bool TerminalDurum { get; set; }
    public bool ImzaSureciBasladi { get; set; }
    public bool AtamaYapilabilir { get; set; }
    public bool RandevuZamaniGeldi { get; set; }
    public bool RandevuYenidenPlanlanacak { get; set; }
    public string? TesisatBolgesi { get; set; }
    public List<YkcAtamaDto> FarkliRandevular { get; set; } = new();
    public bool AcilEkipSecili { get; set; }
    public bool MuhendisEkipSecili { get; set; }
    public string? SeciliEkipId { get; set; }
    public YkcDosyaDto? ImzaliBelge { get; set; }
    public bool ImzaliBelgeHazir { get; set; }
    public bool ImzaliBelgeDemoMu { get; set; }
    public bool DemoPdfGuncellenebilir { get; set; }
    public List<YkcFr265KontrolDto> SonucGirilenKontroller { get; set; } = new();
    public List<YkcFr265KontrolDto> TumSonucluKontroller { get; set; } = new();
    public YkcFr265KontrolDto? SonKontrol { get; set; }
    public YkcFr265KontrolDto? SonUygunsuzKontrol { get; set; }
    public bool KontrollerImzayaHazir { get; set; }
    public bool KontrolAlaniDoldu { get; set; }
    public int? SonKontrolGecmisiId { get; set; }
    public int? AktifKontrolNo { get; set; }
    public YkcFr265KontrolDto? AktifKontrol { get; set; }
    public bool TekrarKontrolBekleniyor { get; set; }
    public bool TekrarRandevuBekleniyor { get; set; }
    public bool ImzayaGonderebilir { get; set; }
    public bool ImzaDurumuSorgulanabilir { get; set; }
    public bool KontrolBolumuAktif { get; set; }
    public bool TamamlamayaHazir { get; set; }
    public bool ImzaBirincilIslemVar { get; set; }
    public bool ImzaBolumuGorsun { get; set; }
    public bool ImzaBolumuAktif { get; set; }
    public List<YkcDosyaDto> IndirilebilirDosyalar { get; set; } = new();
    public List<string> CihazUyarilari { get; set; } = new();
    public DateTime Bugun { get; set; }
    public long? RandevuZamaniUnixMs { get; set; }
    public int KontrolDonemi { get; set; }
    public bool IcOperasyonGorsun { get; set; }
    public bool IncelemeyeAlabilir { get; set; }
    public bool KontroleGecisGorsun { get; set; }
    public bool RedIptalYapabilir { get; set; }
    public YkcYetkiOzeti Yetkiler { get; set; } = new();
    public YkcImzaEntegrasyonDto ImzaEntegrasyonu { get; set; } = new();
    public List<YkcEkipSecenegi> Ekipler { get; set; } = new();
}
