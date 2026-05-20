using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using YetkiliServisGazAcma.Models;
using System.Globalization;

namespace YetkiliServisGazAcma.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;

        public HomeController(AppDbContext db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            var servisCount = _db.Ys_Firmalar.Count(x => !x.SilindiMi && x.AktifMi);
            var devreyeCount = _db.Ys_DevreyeAlmalar.Count(x => !x.SilindiMi && x.Durum == 1);
            var sertifikaCount = _db.Ys_Sertifikalar.Count(x => !x.SilindiMi && x.Durum == 1);
            var toplamIslem = _db.Ys_DevreyeAlmalar.Count(x => !x.SilindiMi);
            var zamaninda = toplamIslem == 0 ? 100.0 : Math.Round(100.0 * devreyeCount / toplamIslem, 1);

            ViewBag.ServisCount = servisCount;
            ViewBag.DevreyeCount = devreyeCount;
            ViewBag.SertifikaCount = sertifikaCount;
            ViewBag.ZamanindaOran = zamaninda;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
