using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/urun-kategorileri")]
    public class UrunKategorilerController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UrunKategorilerController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("liste")]
        public async Task<IActionResult> Liste([FromBody] UrunKategoriListeFiltreDto? dto)
        {
            var kategoriler = await _context.UrunKategoriler
                .Where(x => !x.SilindiMi && x.AktifMi)
                .OrderBy(x => x.SiraNo)
                .ThenBy(x => x.Ad)
                .ToListAsync();

            if (dto?.TumunuGetir != true)
            {
                kategoriler = kategoriler
                    .Where(x => KullanilanKategoriMi(x.Ad))
                    .GroupBy(x => NormalizeKategori(x.Ad))
                    .Select(g => g
                        .OrderByDescending(x => x.AktifMi)
                        .ThenBy(x => string.IsNullOrWhiteSpace(x.IconUrl) ? 1 : 0)
                        .ThenBy(x => x.SiraNo)
                        .ThenBy(x => x.Ad)
                        .First())
                    .OrderBy(x => x.SiraNo)
                    .ThenBy(x => x.Ad)
                    .ToList();
            }

            var list = kategoriler
                .Select(x => new
                {
                    x.Id,
                    x.Ad,
                    x.IconUrl,
                    x.SiraNo,
                    x.AktifMi
                })
                .ToList();

            return Ok(list);
        }

        private static bool KullanilanKategoriMi(string? ad)
        {
            var key = NormalizeKategori(ad);

            return key == "kombi"
                || key.Contains("merkezikazan")
                || key.Contains("sofben")
                || key.Contains("sohben")
                || key == "ocak"
                || key.Contains("gazkullanicicihaz");
        }

        private static string NormalizeKategori(string? ad)
        {
            if (string.IsNullOrWhiteSpace(ad))
                return string.Empty;

            var normalized = ad.Trim().ToLower(new CultureInfo("tr-TR")).Normalize(NormalizationForm.FormD);
            var chars = normalized
                .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch))
                .ToArray();

            return new string(chars)
                .Replace("ı", "i")
                .Replace("ş", "s")
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ö", "o")
                .Replace("ç", "c");
        }
    }

    public class UrunKategoriListeFiltreDto
    {
        public bool TumunuGetir { get; set; }
    }
}
