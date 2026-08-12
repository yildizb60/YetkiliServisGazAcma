using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Controllers
{
    public class HomeController : Controller
    {
        private readonly HomeOzetApiClient _homeOzetApiClient;

        public HomeController(HomeOzetApiClient homeOzetApiClient)
        {
            _homeOzetApiClient = homeOzetApiClient;
        }

        public async Task<IActionResult> Index()
        {
            var ozet = await _homeOzetApiClient.GetirAsync();

            ViewBag.ServisCount = ozet?.ServisCount ?? 0;
            ViewBag.DevreyeCount = ozet?.DevreyeCount ?? 0;
            ViewBag.YetkiBelgesiCount = ozet?.YetkiBelgesiCount ?? 0;
            ViewBag.ZamanindaOran = ozet?.ZamanindaOran ?? 0;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet("yetkisiz-erisim")]
        public IActionResult YetkisizErisim()
        {
            return View("~/Views/Shared/YetkisizErisim.cshtml");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
