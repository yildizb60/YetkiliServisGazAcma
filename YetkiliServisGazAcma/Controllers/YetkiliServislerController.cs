using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models.ViewModels;

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
            "sofben",
            "ocak",
            "gazkullanicicihazlar"
        };

        private readonly YetkiliServisApiClient _yetkiliServisApiClient;

        public YetkiliServislerController(YetkiliServisApiClient yetkiliServisApiClient)
        {
            _yetkiliServisApiClient = yetkiliServisApiClient;
        }

        [HttpGet("")]
        [HttpGet("index")]
        public async Task<IActionResult> Index(string? il, string? ilce, int? markaId, int? kategoriId, string? q, int page = 1, int pageSize = 20)
        {
            var firmalar = new List<Ys_Firma>();
            var filtreSecenekleri = new YetkiliServisApiClient.YetkiliServisFiltreSecenekleri();
            var veriKaynagi = "API";
            var totalCount = 0;
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

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

                var sayfaliSonuc = await _yetkiliServisApiClient.ListeSayfaliAsync(new YetkiliServisApiClient.YetkiliServisListeIstek
                {
                    Il = il,
                    Ilce = ilce,
                    MarkaId = markaId,
                    KategoriId = kategoriId,
                    Q = q,
                    Page = page,
                    PageSize = pageSize
                });

                firmalar = sayfaliSonuc?.Items ?? new List<Ys_Firma>();
                totalCount = sayfaliSonuc?.TotalCount ?? firmalar.Count;
                page = sayfaliSonuc?.Page ?? page;
                pageSize = sayfaliSonuc?.PageSize ?? pageSize;
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
                veriKaynagi = "API kullanilamadi";
                kategoriId = null;
            }

            var model = new YetkiliServislerIndexViewModel
            {
                Firmalar = firmalar,
                Markalar = filtreSecenekleri.Markalar,
                Iller = filtreSecenekleri.Iller,
                Ilceler = filtreSecenekleri.Ilceler,
                Kategoriler = filtreSecenekleri.Kategoriler,
                SeciliIl = il ?? "",
                SeciliIlce = ilce ?? "",
                SeciliMarkaId = markaId,
                SeciliKategoriId = kategoriId,
                Q = q ?? "",
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                VeriKaynagi = veriKaynagi
            };

            return View("~/Views/YetkiliServisler/Index.cshtml", model);
        }

        private static bool KullanilanKategoriMi(string? ad)
        {
            var anahtar = NormalizeKategori(ad);

            return anahtar == "kombi"
                || anahtar.Contains("merkezikazan")
                || anahtar.Contains("sofben")
                || anahtar.Contains("sohben")
                || anahtar == "ocak"
                || anahtar.Contains("gazkullanicicihaz");
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

