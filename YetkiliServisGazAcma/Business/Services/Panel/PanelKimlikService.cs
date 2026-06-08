using System.Globalization;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class PanelKimlikBilgisi
    {
        public string SirketAdi { get; set; } = "Genel Yönetim";
        public string? LogoUrl { get; set; }
    }

    public class PanelKimlikService
    {
        private const string GrupLogo = "/images/company/corumgaz-kargaz-surmeligaz-logo.png";
        private const string MarmaraLogo = "/images/company/marmaragaz-logo.png";

        private readonly IConfiguration _configuration;
        private readonly AktifSirketService _aktifSirketService;
        private readonly PanelKapsamApiClient _panelKapsamApiClient;

        public PanelKimlikService(
            IConfiguration configuration,
            AktifSirketService aktifSirketService,
            PanelKapsamApiClient panelKapsamApiClient)
        {
            _configuration = configuration;
            _aktifSirketService = aktifSirketService;
            _panelKapsamApiClient = panelKapsamApiClient;
        }

        public async Task<PanelKimlikBilgisi> KullaniciIcinOlustur(AppKullanici? kullanici)
        {
            if (kullanici == null)
                return new PanelKimlikBilgisi();

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var kimlik = await _panelKapsamApiClient.PanelKimlikAsync(kullanici, aktifSirketId);

            var sirketAdi = kimlik?.SirketAdi;
            var firmaKodu = kimlik?.FirmaKodu;

            if (string.IsNullOrWhiteSpace(sirketAdi))
                sirketAdi = firmaKodu;

            if (string.IsNullOrWhiteSpace(sirketAdi))
                return new PanelKimlikBilgisi();

            return new PanelKimlikBilgisi
            {
                SirketAdi = GorunenAd(sirketAdi),
                LogoUrl = LogoUrlBul(sirketAdi) ?? LogoUrlBul(firmaKodu)
            };
        }

        private string? LogoUrlBul(string? sirketAdi)
        {
            if (string.IsNullOrWhiteSpace(sirketAdi))
                return null;

            var logolar = _configuration
                .GetSection("SirketLogolari")
                .Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();

            var temiz = sirketAdi.Trim();
            if (logolar.TryGetValue(temiz, out var birebir))
                return birebir;

            var anahtar = Normalize(temiz);
            var eslesen = logolar.FirstOrDefault(x => Normalize(x.Key) == anahtar);
            if (!string.IsNullOrWhiteSpace(eslesen.Value))
                return eslesen.Value;

            if (anahtar.Contains("MARMARAGAZ"))
                return MarmaraLogo;

            if (anahtar.Contains("CORUMGAZ") || anahtar.Contains("KARGAZ") || anahtar.Contains("SURMELIGAZ"))
                return GrupLogo;

            return null;
        }

        private static string GorunenAd(string sirketAdi)
        {
            var temiz = sirketAdi.Trim().Replace("_", " ");
            if (temiz.Length == 0)
                return "Genel Yönetim";

            var tr = new CultureInfo("tr-TR");
            return tr.TextInfo.ToTitleCase(temiz.ToLower(tr));
        }

        private static string Normalize(string value)
        {
            return value.Trim()
                .ToUpperInvariant()
                .Replace("Ç", "C")
                .Replace("Ğ", "G")
                .Replace("İ", "I")
                .Replace("Ö", "O")
                .Replace("Ş", "S")
                .Replace("Ü", "U")
                .Replace(" ", "")
                .Replace("_", "")
                .Replace("-", "");
        }
    }
}
