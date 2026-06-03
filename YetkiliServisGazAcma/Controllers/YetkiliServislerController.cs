using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

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

        private readonly YetkiliServisApiClient _yetkiliServisApiClient;

        public YetkiliServislerController(YetkiliServisApiClient yetkiliServisApiClient)
        {
            _yetkiliServisApiClient = yetkiliServisApiClient;
        }

        [HttpGet("")]
        [HttpGet("index")]
        public async Task<IActionResult> Index(string? il, string? ilce, int? markaId, int? kategoriId, string? q)
        {
            var firmalar = new List<Ys_Firma>();
            var filtreSecenekleri = new YetkiliServisApiClient.YetkiliServisFiltreSecenekleri();
            ViewBag.YetkiliServisVeriKaynagi = "API";

            try
            {
                filtreSecenekleri = await _yetkiliServisApiClient.FiltreSecenekleriAsync(il)
                    ?? new YetkiliServisApiClient.YetkiliServisFiltreSecenekleri();

                filtreSecenekleri.Kategoriler = filtreSecenekleri.Kategoriler
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
                    var secilenKategori = filtreSecenekleri.Kategoriler.FirstOrDefault(x => x.Id == kategoriId.Value);
                    if (secilenKategori == null || !KullanilanKategoriMi(secilenKategori.Ad))
                        kategoriId = null;
                }

                firmalar = await _yetkiliServisApiClient.ListeAsync(new YetkiliServisApiClient.YetkiliServisListeIstek
                {
                    Il = il,
                    Ilce = ilce,
                    MarkaId = markaId,
                    KategoriId = kategoriId,
                    Q = q
                }) ?? new List<Ys_Firma>();
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                ViewBag.YetkiliServisVeriKaynagi = "API kullanilamadi";
                kategoriId = null;
            }

            ViewBag.Markalar = filtreSecenekleri.Markalar;
            ViewBag.Iller = filtreSecenekleri.Iller;
            ViewBag.Ilceler = filtreSecenekleri.Ilceler;
            ViewBag.Kategoriler = filtreSecenekleri.Kategoriler;
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

