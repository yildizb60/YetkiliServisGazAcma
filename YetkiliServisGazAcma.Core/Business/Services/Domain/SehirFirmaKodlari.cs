using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Text;

namespace YetkiliServisGazAcma.Business.Services;

public class SehirFirmaKodlari(IConfiguration configuration)
{
    private readonly IConfiguration _configuration = configuration;
        private static readonly Dictionary<string, string> VarsayilanKodlar =
            new(StringComparer.CurrentCultureIgnoreCase)
            {
                ["Çorum"] = "CORUMGAZ",
                ["Kastamonu"] = "KARGAZ",
                ["Karabük"] = "KARGAZ",
                ["Yozgat"] = "SURMELIGAZ",
                ["Yalova"] = "MARMARAGAZ_YALOVA",
                ["Tekirdağ"] = "MARMARAGAZ_CORLU"
            };

        public Dictionary<string, string> TumKodlar()
        {
            var appSettingsKodlari = _configuration
                .GetSection("SehirFirmaKodlari")
                .Get<Dictionary<string, string>>();

            var kaynak = appSettingsKodlari?.Count > 0 ? appSettingsKodlari : VarsayilanKodlar;

            return kaynak
                .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value))
                .ToDictionary(x => x.Key.Trim(), x => x.Value.Trim(), StringComparer.CurrentCultureIgnoreCase);
        }

        public List<string> Sehirler()
        {
            return TumKodlar()
                .Keys
                .OrderBy(x => x)
                .ToList();
        }

        public string? FirmaKodu(string? sehir)
        {
            if (string.IsNullOrWhiteSpace(sehir))
                return null;

            var kodlar = TumKodlar();
            return kodlar.TryGetValue(sehir.Trim(), out var kod) ? kod : null;
        }

        public static string? SirketAdindanFirmaKodu(string? sirketAdi,
            FirmaKoduNormalizasyonu normalizasyon = FirmaKoduNormalizasyonu.BosluklariSil)
        {
            if (string.IsNullOrWhiteSpace(sirketAdi))
                return null;

            var karakterler = sirketAdi.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
            var normalized = normalizasyon == FirmaKoduNormalizasyonu.AyiraclariAltCizgiYap
                ? new string(karakterler.Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_').ToArray())
                    .Normalize(NormalizationForm.FormC).Trim('_')
                : new string(karakterler.ToArray()).Normalize(NormalizationForm.FormC).ToUpperInvariant()
                    .Replace('İ', 'I').Replace('Ğ', 'G').Replace('Ü', 'U')
                    .Replace('Ş', 'S').Replace('Ö', 'O').Replace('Ç', 'C').Replace(" ", "");

            if (normalized.Contains("CORUM")) return "CORUMGAZ";
            if (normalized.Contains("KARGAZ") || normalized.Contains("KASTAMONU") || normalized.Contains("KARABUK"))
                return "KARGAZ";
            if (normalized.Contains("SURMELI") || normalized.Contains("YOZGAT")) return "SURMELIGAZ";
            if (normalized.Contains("YALOVA")) return "MARMARAGAZ_YALOVA";
            if (normalized.Contains("CORLU") || normalized.Contains("TEKIRDAG")) return "MARMARAGAZ_CORLU";
            return normalized;
        }

}

public enum FirmaKoduNormalizasyonu
{
    BosluklariSil,
    AyiraclariAltCizgiYap
}
