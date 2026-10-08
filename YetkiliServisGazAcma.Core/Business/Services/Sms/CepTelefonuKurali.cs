namespace YetkiliServisGazAcma.Business.Services
{
    public static class CepTelefonuKurali
    {
        public static bool GecerliMi(string? telefon)
        {
            if (string.IsNullOrWhiteSpace(telefon))
                return false;
            if (telefon.Any(x => x is not (>= '0' and <= '9') && !char.IsWhiteSpace(x) && x is not ('+' or '-' or '(' or ')')))
                return false;

            var rakamlar = new string(telefon.Where(x => x is >= '0' and <= '9').ToArray());
            return (rakamlar.Length == 10 && rakamlar.StartsWith('5'))
                || (rakamlar.Length == 11 && rakamlar.StartsWith("05", StringComparison.Ordinal))
                || (rakamlar.Length == 12 && rakamlar.StartsWith("905", StringComparison.Ordinal));
        }
    }
}
