using Microsoft.EntityFrameworkCore;
using System.Globalization;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

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
        private readonly AppDbContext _context;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;
        private readonly AktifSirketService _aktifSirketService;

        public PanelKimlikService(
            IConfiguration configuration,
            AppDbContext context,
            SehirFirmaKoduService sehirFirmaKoduService,
            AktifSirketService aktifSirketService)
        {
            _configuration = configuration;
            _context = context;
            _sehirFirmaKoduService = sehirFirmaKoduService;
            _aktifSirketService = aktifSirketService;
        }

        public async Task<PanelKimlikBilgisi> KullaniciIcinOlustur(AppKullanici? kullanici)
        {
            if (kullanici == null)
                return new PanelKimlikBilgisi();

            string? sirketAdi = null;
            string? sehir = null;

            if (kullanici.FirmaId.HasValue)
            {
                var firma = await _context.Ys_Firmalar
                    .Include(x => x.Sirket)
                    .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId.Value && !x.SilindiMi);

                sirketAdi = firma?.Sirket?.SirketAdi;
                sehir = firma?.FaaliyetIli ?? firma?.Sirket?.Il;
            }

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            if (string.IsNullOrWhiteSpace(sirketAdi) && aktifSirketId.HasValue)
            {
                var sirket = await _context.Dag_Sirketler
                    .FirstOrDefaultAsync(x => x.Id == aktifSirketId.Value && !x.SilindiMi);

                sirketAdi = sirket?.SirketAdi;
                sehir = sirket?.Il;
            }

            if (string.IsNullOrWhiteSpace(sirketAdi) && kullanici.SirketId.HasValue)
            {
                var sirket = await _context.Dag_Sirketler
                    .FirstOrDefaultAsync(x => x.Id == kullanici.SirketId.Value && !x.SilindiMi);

                sirketAdi = sirket?.SirketAdi;
                sehir = sirket?.Il;
            }

            if (string.IsNullOrWhiteSpace(sirketAdi))
                sirketAdi = _sehirFirmaKoduService.FirmaKodu(sehir);

            if (string.IsNullOrWhiteSpace(sirketAdi))
                return new PanelKimlikBilgisi();

            var firmaKodu = _sehirFirmaKoduService.FirmaKodu(sehir);
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

