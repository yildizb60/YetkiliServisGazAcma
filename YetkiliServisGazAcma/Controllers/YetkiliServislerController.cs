using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models.ViewModels;

namespace YetkiliServisGazAcma.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("yetkili-servisler")]
    public class YetkiliServislerController : Controller
    {
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
                    .Where(x => x.AktifMi)
                    .OrderBy(x => x.SiraNo)
                    .ThenBy(x => x.Ad)
                    .ToList();

                if (kategoriId.HasValue)
                {
                    var secilenKategori = filtreSecenekleri.Kategoriler.FirstOrDefault(x => x.Id == kategoriId.Value);
                    if (secilenKategori == null)
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

    }
}

