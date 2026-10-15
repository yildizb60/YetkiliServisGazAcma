using System.Globalization;

namespace YetkiliServisGazAcma.Models;

public static class AboneAdiSunumu
{
    private static readonly CultureInfo Turkce = CultureInfo.GetCultureInfo("tr-TR");

    public static string Bicimlendir(string? adSoyad, string bosDeger = "-")
        => string.IsNullOrWhiteSpace(adSoyad)
            ? bosDeger
            : Turkce.TextInfo.ToTitleCase(adSoyad.Trim().ToLower(Turkce));
}
