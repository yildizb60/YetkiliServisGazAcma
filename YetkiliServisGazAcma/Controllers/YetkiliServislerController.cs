using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;

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
            var ekran = new YetkiliServisRehberEkranDto
            {
                Sorgu = new() { Il = il, Ilce = ilce, MarkaId = markaId, Q = q }
            };
            try
            {
                ekran = await _yetkiliServisApiClient.RehberEkraniAsync(new YetkiliServisFiltreDto
                {
                    Il = il,
                    Ilce = ilce,
                    MarkaId = markaId,
                    KategoriId = kategoriId,
                    Q = q,
                    Page = page,
                    PageSize = pageSize
                }) ?? ekran;
            }
            catch (ApiIntegrationException ex)
            {
                TempData["Hata"] = ex.Message;
            }

            return View("~/Views/YetkiliServisler/Index.cshtml", ekran);
        }

    }
}

