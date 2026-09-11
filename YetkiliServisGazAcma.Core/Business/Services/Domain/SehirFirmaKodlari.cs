using Microsoft.Extensions.Configuration;

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

}
