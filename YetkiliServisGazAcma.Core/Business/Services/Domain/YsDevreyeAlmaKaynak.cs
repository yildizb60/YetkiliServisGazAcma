using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;

namespace YetkiliServisGazAcma.Business.Services;

public sealed class YsDevreyeAlmaKaynak
{
    public string TesisatNo { get; set; } = string.Empty;
    public string SozlesmeNo { get; set; } = string.Empty;
    public string AboneNo { get; set; } = string.Empty;
    public string MusteriAdi { get; set; } = string.Empty;
    public string MusteriTelefon { get; set; } = string.Empty;
    public string Adres { get; set; } = string.Empty;
    public string CihazTipi { get; set; } = string.Empty;
    public string CihazMarka { get; set; } = string.Empty;
    public string CihazKapasite { get; set; } = string.Empty;

    public static string CihazAnahtari(int dagitimSirketiId, YsDevreyeAlmaKaynak kaynak,
        string? projeNo, string? cihazTipKodu, int ayniCihazSirasi)
    {
        return Hash(dagitimSirketiId, Normal(kaynak.TesisatNo), Normal(projeNo), Normal(cihazTipKodu),
            Normal(kaynak.CihazTipi), Normal(kaynak.CihazMarka), Normal(kaynak.CihazKapasite), ayniCihazSirasi);
    }

    public static string SeriAnahtari(int dagitimSirketiId, string? tesisatNo, string? seriNo)
    {
        return Hash(dagitimSirketiId, Normal(tesisatNo), Normal(seriNo));
    }

    public static string CihazImzasi(YsDevreyeAlmaKaynak kaynak, string? projeNo, string? cihazTipKodu)
    {
        return JsonSerializer.Serialize(new[] { Normal(projeNo), Normal(cihazTipKodu),
            Normal(kaynak.CihazTipi), Normal(kaynak.CihazMarka), Normal(kaynak.CihazKapasite) });
    }

    private static string Hash(params object[] parts)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parts)));
        return Convert.ToHexString(bytes);
    }

    private static string Normal(string? value)
    {
        var upper = (value ?? string.Empty).Trim().ToUpper(new CultureInfo("tr-TR"))
            .Normalize(NormalizationForm.FormD);
        return new string(upper.Where(c => CharUnicodeInfo.GetUnicodeCategory(c)
            != UnicodeCategory.NonSpacingMark).ToArray());
    }
}
