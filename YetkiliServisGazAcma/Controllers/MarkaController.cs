using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize(Roles = "GenelSistemAdmin,SirketAdmin,SuperAdmin,Personel")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class MarkaController : Controller
    {
        private readonly MarkaService _service;
        private readonly MarkaApiClient _markaApiClient;
        private readonly AppDbContext _context;
        private readonly UserManager<AppKullanici> _userManager;

        public MarkaController(
            MarkaService service,
            MarkaApiClient markaApiClient,
            AppDbContext context,
            UserManager<AppKullanici> userManager)
        {
            _service = service;
            _markaApiClient = markaApiClient;
            _context = context;
            _userManager = userManager;
        }

        private async Task<int> GetOnayBekleyenCount()
        {
            return await _context.Ys_Sertifikalar
                .Where(x => !x.SilindiMi && x.Durum == 0)
                .CountAsync();
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.SuresiBitecek = await _context.Ys_Sertifikalar
                .Where(x => !x.SilindiMi && x.Durum == 1 && x.SertifikaBitisTarihi <= DateTime.Now.AddDays(30) && x.SertifikaBitisTarihi >= DateTime.Now)
                .CountAsync();
            await next();
        }

        private async Task<AppKullanici?> GetCurrentUser()
        {
            return await _userManager.GetUserAsync(User);
        }

        public async Task<IActionResult> Index(string? q, string? durum)
        {
            var markalar = await _markaApiClient.TumunuGetirAsync();
            ViewBag.MarkaVeriKaynagi = "API";

            if (markalar == null)
            {
                markalar = await _service.TumunuGetir();
                ViewBag.MarkaVeriKaynagi = "MVC";
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var aranacak = q.Trim();
                markalar = markalar
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x.MarkaAdi) &&
                        x.MarkaAdi.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(durum) && durum != "tumu")
            {
                var aktifMi = durum == "aktif";
                markalar = markalar.Where(x => x.AktifMi == aktifMi).ToList();
            }

            ViewBag.SeciliQ = q ?? "";
            ViewBag.SeciliDurum = string.IsNullOrWhiteSpace(durum) ? "tumu" : durum;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.SuresiBitecek = await _context.Ys_Sertifikalar
                .Where(x => !x.SilindiMi && x.Durum == 1 && x.SertifikaBitisTarihi <= DateTime.Now.AddDays(30) && x.SertifikaBitisTarihi >= DateTime.Now)
                .CountAsync();
            ViewBag.Kullanici = await GetCurrentUser();
            return View(markalar);
        }

        [HttpGet]
        public async Task<IActionResult> Ekle()
        {
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Kullanici = await GetCurrentUser();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Ekle(Ys_Marka marka)
        {
            await _service.Ekle(marka, User.Identity?.Name ?? "sistem");
            TempData["Mesaj"] = "Marka başarıyla eklendi.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Duzenle(int id)
        {
            var marka = await _service.IdIleGetir(id);
            if (marka == null) return NotFound();
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Kullanici = await GetCurrentUser();
            return View(marka);
        }

        [HttpPost]
        public async Task<IActionResult> Duzenle(Ys_Marka marka)
        {
            await _service.Guncelle(marka, User.Identity?.Name ?? "sistem");
            TempData["Mesaj"] = "Marka başarıyla güncellendi.";
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Sil(int id)
        {
            var silindi = await _service.Sil(id, User.Identity?.Name ?? "sistem");
            TempData[silindi ? "Mesaj" : "Hata"] = silindi
                ? "Marka başarıyla silindi."
                : "Bu marka üzerinde devreye alma veya yetkili servis kaydı olduğu için silinemez.";
            return RedirectToAction("Index");
        }
    }
}

