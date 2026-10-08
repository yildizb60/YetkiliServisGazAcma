namespace YetkiliServisGazAcma.Models;

public static class DevreyeAlmaCihazTipiSunumu
{
    public static string Etiket(string? cihazTipi)
    {
        if (string.IsNullOrWhiteSpace(cihazTipi)) return "-";

        return cihazTipi.Trim().ToLowerInvariant() switch
        {
            "diger" => "Diğer",
            "firin" => "Fırın",
            "sofben" => "Şofben",
            _ => cihazTipi.Trim()
        };
    }
}
