using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services
{
    public class SehirFirmaKoduService
    {
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

        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;

        public SehirFirmaKoduService(IConfiguration configuration, AppDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }

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

        public async Task<int> SirketIdBulVeyaOlustur(string? sehir, string? kullanici)
        {
            var temizSehir = string.IsNullOrWhiteSpace(sehir) ? "Genel" : sehir.Trim();
            var firmaKodu = FirmaKodu(temizSehir) ?? temizSehir.ToUpperInvariant().Replace(" ", "_");

            var mevcut = await _context.Dag_Sirketler
                .FirstOrDefaultAsync(x => !x.SilindiMi
                    && ((x.Il != null && x.Il == temizSehir)
                        || (x.SirketAdi != null && x.SirketAdi == firmaKodu)));

            if (mevcut != null)
            {
                if (!mevcut.AktifMi)
                    mevcut.AktifMi = true;

                if (string.IsNullOrWhiteSpace(mevcut.Il))
                    mevcut.Il = temizSehir;

                if (string.IsNullOrWhiteSpace(mevcut.SirketAdi))
                    mevcut.SirketAdi = firmaKodu;

                await _context.SaveChangesAsync();
                return mevcut.Id;
            }

            var yeni = new Dag_Sirket
            {
                SirketAdi = firmaKodu,
                Il = temizSehir,
                AktifMi = true,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici ?? "sistem",
                SilindiMi = false
            };

            _context.Dag_Sirketler.Add(yeni);
            await _context.SaveChangesAsync();
            return yeni.Id;
        }
    }
}

