using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class MobilImzaOptions
{
    public bool Enabled { get; set; }
    public List<MobilImzaIstemci> Istemciler { get; set; } = [];
    public string SablonSurumu { get; set; } = "";
    public List<MobilImzaAlani> ImzaAlanlari { get; set; } = [];

    public bool SablonGecerli(int kontrolNo) =>
        SablonSurumu == Business.Services.YkcFr265PdfService.TasarimSurumu
        && kontrolNo is >= 1 and <= 5
        && ImzaAlanlari.Where(x => x.KontrolNo == kontrolNo).Select(x => x.ImzaciSiraNo).Distinct().Order().SequenceEqual(new[] { 1, 2, 3 })
        && Alanlar(kontrolNo).All(x => x.Sayfa is 1 or 2
            && double.IsFinite(x.X) && double.IsFinite(x.Y) && double.IsFinite(x.Genislik) && double.IsFinite(x.Yukseklik)
            && x.X >= 0 && x.Y >= 0 && x.Genislik > 0 && x.Yukseklik > 0
            && x.X + x.Genislik <= 595.3 && x.Y + x.Yukseklik <= 841.9);

    public List<MobilImzaAlani> Alanlar(int kontrolNo) => ImzaAlanlari.Where(x => x.KontrolNo == 0 || x.KontrolNo == kontrolNo).ToList();
}

public sealed class MobilImzaIstemci
{
    public string Ad { get; set; } = "";
    public string ApiKeySha256 { get; set; } = "";
    public int[] SirketIdleri { get; set; } = [];
}
