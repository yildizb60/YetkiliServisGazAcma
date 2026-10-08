using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.Models.ViewModels;

public sealed class BranchEditorViewModel
{
    public AdminSubeDto Branch { get; init; } = new() { AktifMi = true };
    public IReadOnlyList<AdminSubeFirmaDto> Firms { get; init; } = [];
    public bool CanSelectFirm { get; init; }
    public string PostUrl { get; init; } = string.Empty;
}
