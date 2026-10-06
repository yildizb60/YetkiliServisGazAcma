using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Models.ViewModels;

public sealed class BranchEditorViewModel
{
    public Ys_Sube Branch { get; init; } = new();
    public IReadOnlyList<Ys_Firma> Firms { get; init; } = [];
    public bool CanSelectFirm { get; init; }
    public string PostUrl { get; init; } = string.Empty;
}
