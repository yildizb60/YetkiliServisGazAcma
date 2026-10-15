using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.Models;

public sealed class DevreyeAlmaListeViewModel
{
    public IReadOnlyList<DevreyeAlmaKayitDto> Kayitlar { get; init; } = [];
    public DevreyeAlmaSayfalamaDto? Sayfalama { get; init; }
    public string ListeUrl { get; init; } = "";
    public string TemizleUrl { get; init; } = "";
    public string PdfUrl { get; init; } = "";
    public string ExcelUrl { get; init; } = "";
    public string? YeniKayitUrl { get; init; }
    public int? SirketId { get; init; }
    public bool ServisPaneli { get; init; }
    public IReadOnlyList<string> Markalar { get; init; } = [];
    public IReadOnlyList<string> CihazTipleri { get; init; } = [];
    public IReadOnlyList<string> Sehirler { get; init; } = [];
}
