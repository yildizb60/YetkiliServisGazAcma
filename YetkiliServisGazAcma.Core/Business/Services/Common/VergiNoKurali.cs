namespace YetkiliServisGazAcma.Business.Services;

public static class VergiNoKurali
{
    public static string Normalize(string? value)
        => new string((value ?? "").Where(char.IsAsciiDigit).ToArray());
}
