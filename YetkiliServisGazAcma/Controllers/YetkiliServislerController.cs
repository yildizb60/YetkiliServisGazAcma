using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("yetkili-servisler")]
    public class YetkiliServislerController : Controller
    {
        private static readonly string[] KullanilanKategoriAnahtarlari =
        {
            "merkezikazan",
            "kombi",
            "sofben"
        };

        private readonly AppDbContext _context;
        private readonly YetkiliServisApiClient _yetkiliServisApiClient;

        public YetkiliServislerController(AppDbContext context, YetkiliServisApiClient yetkiliServisApiClient)
        {
            _context = context;
            _yetkiliServisApiClient = yetkiliServisApiClient;
        }

        [HttpGet("")]
        [HttpGet("index")]
        public async Task<IActionResult> Index(string? il, string? ilce, int? markaId, int? kategoriId, string? q)
        {
            var kategoriler = (await _context.UrunKategoriler
                    .Where(x => !x.SilindiMi)
                    .OrderBy(x => x.SiraNo)
                    .ThenBy(x => x.Ad)
                    .ToListAsync())
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

            if (kategoriId.HasValue)
            {
                var secilenKategori = await _context.UrunKategoriler
                    .FirstOrDefaultAsync(x => x.Id == kategoriId.Value && !x.SilindiMi);

                if (secilenKategori == null || !KullanilanKategoriMi(secilenKategori.Ad))
                    kategoriId = null;
            }

            var firmalar = await _yetkiliServisApiClient.ListeAsync(new YetkiliServisApiClient.YetkiliServisListeIstek
            {
                Il = il,
                Ilce = ilce,
                MarkaId = markaId,
                KategoriId = kategoriId,
                Q = q
            });
            ViewBag.YetkiliServisVeriKaynagi = "API";

            if (firmalar == null)
            {
                var firmalarQuery = _context.Ys_Firmalar
                    .Include(x => x.FirmaMarkalar!)
                        .ThenInclude(x => x.Marka)
                    .Include(x => x.FirmaKategoriler!)
                        .ThenInclude(x => x.Kategori)
                    .Include(x => x.Subeler!)
                    .Include(x => x.Sirket)
                    .Where(x => !x.SilindiMi && x.AktifMi)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(il))
                {
                    firmalarQuery = firmalarQuery.Where(x =>
                        x.FaaliyetIli == il ||
                        (x.Subeler != null && x.Subeler.Any(s => !s.SilindiMi && s.AktifMi && s.Il == il)));
                }

                if (!string.IsNullOrWhiteSpace(ilce))
                {
                    firmalarQuery = firmalarQuery.Where(x =>
                        x.Subeler != null && x.Subeler.Any(s => !s.SilindiMi && s.AktifMi && s.Ilce == ilce));
                }

                if (!string.IsNullOrWhiteSpace(q))
                {
                    firmalarQuery = firmalarQuery.Where(x =>
                        (x.FirmaAdi ?? "").Contains(q) ||
                        (x.YetkiliKisi ?? "").Contains(q));
                }

                if (markaId.HasValue)
                {
                    firmalarQuery = firmalarQuery.Where(x =>
                        x.FirmaMarkalar!.Any(m => !m.SilindiMi && m.MarkaId == markaId.Value));
                }

                if (kategoriId.HasValue)
                {
                    firmalarQuery = firmalarQuery.Where(x =>
                        x.FirmaKategoriler!.Any(k => !k.SilindiMi && k.KategoriId == kategoriId.Value));
                }

                firmalar = await firmalarQuery
                    .OrderBy(x => x.FirmaAdi)
                    .ToListAsync();
                ViewBag.YetkiliServisVeriKaynagi = "MVC";
            }

            var markalar = await _context.Ys_Markalar
                .Where(x => !x.SilindiMi && x.AktifMi)
                .OrderBy(x => x.MarkaAdi)
                .ToListAsync();

            var illerRaw = await _context.Ys_Firmalar
                .Where(x => !x.SilindiMi && x.AktifMi && x.FaaliyetIli != null && x.FaaliyetIli != "")
                .Select(x => x.FaaliyetIli!)
                .ToListAsync();

            var subeIllerRaw = await _context.Ys_Subeler
                .Where(x => !x.SilindiMi
                    && x.AktifMi
                    && x.Il != null
                    && x.Il != ""
                    && _context.Ys_Firmalar.Any(f => f.Id == x.FirmaId && !f.SilindiMi && f.AktifMi))
                .Select(x => x.Il!)
                .ToListAsync();

            var dagitimIllerRaw = await _context.Dag_Sirketler
                .Where(x => x.AktifMi && x.Il != null && x.Il != "")
                .Select(x => x.Il!)
                .ToListAsync();

            var tr = new System.Globalization.CultureInfo("tr-TR");

            string NormalizeIl(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return "";
                var trimmed = value.Trim();
                // normalize multiple spaces
                while (trimmed.Contains("  ")) trimmed = trimmed.Replace("  ", " ");
                return trimmed;
            }

            bool GecerliKonumMu(string? value)
            {
                var norm = NormalizeIl(value);
                if (string.IsNullOrWhiteSpace(norm)) return false;
                // Tek harfli veya anlamsız veri (ör. "d") listede görünmesin.
                if (norm.Length < 2) return false;
                // En az bir harf içermeli.
                return norm.Any(char.IsLetter);
            }

            string NormalizeKey(string? value)
            {
                var norm = NormalizeIl(value);
                if (string.IsNullOrWhiteSpace(norm)) return "";
                var lower = norm.ToLower(tr);
                var keyChars = lower.Where(ch => char.IsLetterOrDigit(ch)).ToArray();
                return new string(keyChars);
            }

            string TitleCaseTr(string value)
            {
                var trimmed = NormalizeIl(value);
                if (string.IsNullOrWhiteSpace(trimmed)) return "";
                var lower = trimmed.ToLower(tr);
                return tr.TextInfo.ToTitleCase(lower);
            }

            var tumIller = illerRaw
                .Concat(subeIllerRaw)
                .Concat(dagitimIllerRaw)
                .Select(x => NormalizeIl(x))
                .Where(x => GecerliKonumMu(x))
                .GroupBy(x => NormalizeKey(x))
                .Select(g => TitleCaseTr(g.First()))
                .OrderBy(x => x)
                .ToList();

            var ilcelerQuery = _context.Ys_Subeler
                .Where(x => !x.SilindiMi
                    && x.AktifMi
                    && x.Ilce != null
                    && x.Ilce != ""
                    && _context.Ys_Firmalar.Any(f => f.Id == x.FirmaId && !f.SilindiMi && f.AktifMi));

            if (!string.IsNullOrWhiteSpace(il))
            {
                ilcelerQuery = ilcelerQuery.Where(x => x.Il == il);
            }

            var ilceler = await ilcelerQuery
                .Select(x => x.Ilce!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            ilceler = ilceler
                .Select(NormalizeIl)
                .Where(GecerliKonumMu)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            ViewBag.Markalar = markalar;
            ViewBag.Iller = tumIller;
            ViewBag.Ilceler = ilceler;
            ViewBag.Kategoriler = kategoriler;
            ViewBag.SeciliIl = il ?? "";
            ViewBag.SeciliIlce = ilce ?? "";
            ViewBag.SeciliMarkaId = markaId;
            ViewBag.SeciliKategoriId = kategoriId;
            ViewBag.Q = q ?? "";
            ViewBag.KullanilanKategoriAnahtarlari = KullanilanKategoriAnahtarlari.ToList();

            return View("~/Views/YetkiliServisler/Index.cshtml", firmalar);
        }

        private static bool KullanilanKategoriMi(string? ad)
        {
            var anahtar = NormalizeKategori(ad);

            return anahtar == "kombi"
                || anahtar.Contains("merkezikazan")
                || anahtar.Contains("sofben")
                || anahtar.Contains("sohben");
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
}

