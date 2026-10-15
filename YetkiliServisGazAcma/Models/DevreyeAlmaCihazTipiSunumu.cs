namespace YetkiliServisGazAcma.Models;

public static class DevreyeAlmaCihazTipiSunumu
{
    public static string? IkonUrl(string? cihazTipi) => Anahtar(cihazTipi) switch
    {
        "kombi" => "/images/icons/category-kombi.webp",
        "ocak" => "/images/icons/category-ocak.svg",
        "gaz kullanıcı cihazlar" or "gaz kullanici cihazlar" => "/images/icons/category-gaz-kullanici-cihazlar.svg",
        _ => null
    };

    public static string RenkSinifi(string? cihazTipi) => Anahtar(cihazTipi) switch
    {
        "kombi" => "is-kombi",
        "ocak" => "is-ocak",
        "şofben" or "sofben" => "is-sofben",
        _ => "is-gaz"
    };

    public static string IkonSinifi(string? cihazTipi) => Anahtar(cihazTipi) switch
    {
        "kombi" => "bi-thermometer-half",
        "ocak" => "bi-grid",
        "şofben" or "sofben" => "bi-water",
        _ => "bi-fire"
    };

    private static string Anahtar(string? cihazTipi) => cihazTipi?.Trim().ToLowerInvariant() ?? "";

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
