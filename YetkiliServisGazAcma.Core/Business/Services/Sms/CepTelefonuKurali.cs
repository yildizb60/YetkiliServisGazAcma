namespace YetkiliServisGazAcma.Business.Services
{
    public static class CepTelefonuKurali
    {
        public static bool GecerliMi(string? telefon)
        {
            if (string.IsNullOrWhiteSpace(telefon))
                return false;
            if (telefon.Any(x => !char.IsDigit(x) && !char.IsWhiteSpace(x) && x is not ('+' or '-' or '(' or ')')))
                return false;

            var rakamlar = new string(telefon.Where(char.IsDigit).ToArray());
            return (rakamlar.Length == 10 && rakamlar.StartsWith('5'))
                || (rakamlar.Length == 11 && rakamlar.StartsWith("05", StringComparison.Ordinal))
                || (rakamlar.Length == 12 && rakamlar.StartsWith("905", StringComparison.Ordinal));
        }
    }
}
