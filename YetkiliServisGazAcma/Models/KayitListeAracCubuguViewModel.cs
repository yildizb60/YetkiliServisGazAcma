namespace YetkiliServisGazAcma.Models;

public sealed class KayitListeAracCubuguViewModel
{
    public string Baslik { get; init; } = "";
    public int KayitSayisi { get; init; }
    public string FormId { get; init; } = "commissioningFilters";
    public string PanelId { get; init; } = "commissioningFiltersPanel";
    public string? PdfUrl { get; init; }
    public string? ExcelUrl { get; init; }
    public bool KayitVar { get; init; }
    public bool TumFiltrelenenKayitlar { get; init; }
    public string? YeniKayitUrl { get; init; }
    public string YeniKayitEtiketi { get; init; } = "Yeni Kayıt";
}
