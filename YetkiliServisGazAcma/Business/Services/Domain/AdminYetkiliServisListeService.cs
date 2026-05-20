using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminYetkiliServisListeService
    {
        private readonly AppDbContext _context;

        public AdminYetkiliServisListeService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AdminYetkiliServisListeSonuc> ListeleAsync(AdminYetkiliServisListeFiltre filtre)
        {
            var query = _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .Where(x => !x.SilindiMi
                    && _context.Users.Any(u =>
                        u.KullaniciTipi == 1 &&
                        u.FirmaId == x.Id));

            if (filtre.SirketId.HasValue)
                query = query.Where(x => x.SirketId == filtre.SirketId.Value);

            var servisler = await query
                .OrderBy(x => x.FirmaAdi)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(filtre.Q))
            {
                var aranacak = filtre.Q.Trim();
                servisler = servisler
                    .Where(x => Eslesir(x.FirmaAdi, aranacak)
                        || Eslesir(x.YetkiliKisi, aranacak)
                        || Eslesir(x.VergiNo, aranacak)
                        || Eslesir(x.Telefon, aranacak))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(filtre.Il))
            {
                var ilArama = filtre.Il.Trim();
                servisler = servisler
                    .Where(x => Eslesir(x.FaaliyetIli, ilArama))
                    .ToList();
            }

            if (filtre.Durum.HasValue)
            {
                var aktifMi = filtre.Durum.Value == 1;
                servisler = servisler.Where(x => x.AktifMi == aktifMi).ToList();
            }

            var servisIds = servisler.Select(x => x.Id).ToList();
            var devreyeSayilari = await _context.Ys_DevreyeAlmalar
                .Where(x => !x.SilindiMi && servisIds.Contains(x.FirmaId))
                .GroupBy(x => x.FirmaId)
                .Select(x => new { FirmaId = x.Key, Sayisi = x.Count() })
                .ToDictionaryAsync(x => x.FirmaId, x => x.Sayisi);

            servisler = filtre.DevreyeSiralama?.ToLowerInvariant() switch
            {
                "artan" => servisler
                    .OrderBy(x => devreyeSayilari.TryGetValue(x.Id, out var sayi) ? sayi : 0)
                    .ThenBy(x => x.FirmaAdi)
                    .ToList(),
                "azalan" => servisler
                    .OrderByDescending(x => devreyeSayilari.TryGetValue(x.Id, out var sayi) ? sayi : 0)
                    .ThenBy(x => x.FirmaAdi)
                    .ToList(),
                _ => servisler
            };

            return new AdminYetkiliServisListeSonuc
            {
                Servisler = servisler,
                DevreyeSayilari = devreyeSayilari
            };
        }

        private static bool Eslesir(string? kaynak, string aranan)
        {
            if (string.IsNullOrWhiteSpace(kaynak))
                return false;

            return aranan.Length == 1
                ? kaynak.StartsWith(aranan, StringComparison.CurrentCultureIgnoreCase)
                : kaynak.IndexOf(aranan, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }
    }

    public class AdminYetkiliServisListeFiltre
    {
        public int? SirketId { get; set; }
        public string? Q { get; set; }
        public string? Il { get; set; }
        public int? Durum { get; set; }
        public string? DevreyeSiralama { get; set; }
    }

    public class AdminYetkiliServisListeSonuc
    {
        public List<Ys_Firma> Servisler { get; set; } = new();
        public Dictionary<int, int> DevreyeSayilari { get; set; } = new();
    }
}
