using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("ys-panel")]
    public class YetkiliServisPanelController : Controller
    {
        private readonly UserManager<AppKullanici> _userManager;
        private readonly AppDbContext _context;

        public YetkiliServisPanelController(
            UserManager<AppKullanici> userManager,
            AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        private async Task SetBildirimler(AppKullanici kullanici)
        {
            var bildirimler = new List<string>();
            var firmaId = kullanici.FirmaId ?? 0;

            var firma = await _context.Ys_Firmalar
                .Include(x => x.Sertifikalar)
                .FirstOrDefaultAsync(x => x.Id == firmaId);

            var bugun = DateTime.Now.Date;
            var onayli = firma?.Sertifikalar?
                .Where(x => x.Durum == 1
                    && !x.SilindiMi
                    && (!x.SertifikaBaslangicTarihi.HasValue || x.SertifikaBaslangicTarihi.Value.Date <= bugun)
                    && x.SertifikaBitisTarihi.Date >= bugun)
                .OrderBy(x => x.SertifikaBitisTarihi)
                .FirstOrDefault();

            var bekleyenVar = firma?.Sertifikalar?.Any(x => x.Durum == 0 && !x.SilindiMi) ?? false;
            if (onayli != null)
            {
                bildirimler.Add("Yetki belgeniz onaylandı. Cihaz devreye alabilirsiniz.");
                var kalan = (onayli.SertifikaBitisTarihi.Date - bugun).Days;
                if (kalan <= 30)
                {
                    bildirimler.Add($"Yetki belgenizin bitmesine {kalan} gün kaldı. Lütfen yenileyin.");
                }
            }
            if (bekleyenVar)
            {
                bildirimler.Add("Yetki belgeniz onay bekliyor. Yetkili onayladıktan sonra işlem yapabilirsiniz.");
            }

            var son7Gun = DateTime.Now.AddDays(-7);
            var sonDevreye = await _context.Ys_DevreyeAlmalar
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.OlusturmaTarihi >= son7Gun)
                .CountAsync();
            if (sonDevreye > 0)
            {
                bildirimler.Add($"Son 7 günde {sonDevreye} cihaz devreye alındı.");
            }

            var sonSube = await _context.Ys_Subeler
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.OlusturmaTarihi >= son7Gun)
                .CountAsync();
            if (sonSube > 0)
            {
                bildirimler.Add($"Son 7 günde {sonSube} şube kaydı eklendi.");
            }

            ViewBag.Bildirimler = bildirimler;
            ViewBag.BildirimSayisi = bildirimler.Count;
        }

        private async Task<AppKullanici?> GetYetkiliServisKullanici()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return null;
            if (kullanici.KullaniciTipi != 1) return null;
            return kullanici;
        }

        private async Task<(DateTime Bas, DateTime Bit)> GetRaporTarihAraligi(int firmaId, DateTime? bas, DateTime? bit)
        {
            if (!bas.HasValue && !bit.HasValue)
            {
                var mevcutAralik = await _context.Ys_DevreyeAlmalar
                    .Where(x => x.FirmaId == firmaId && !x.SilindiMi)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Bas = g.Min(x => x.DevreyeAlmaTarihi),
                        Bit = g.Max(x => x.DevreyeAlmaTarihi)
                    })
                    .FirstOrDefaultAsync();

                if (mevcutAralik != null)
                    return (mevcutAralik.Bas.Date, mevcutAralik.Bit.Date);
            }

            var bitTarih = bit?.Date ?? DateTime.Now.Date;
            var basTarih = bas?.Date ?? bitTarih.AddDays(-30);
            return (basTarih, bitTarih);
        }

        private async Task<(bool zorunluMu, bool tamamlandiMi, List<string> eksikler)> GetIlkKurulumDurumu(AppKullanici kullanici)
        {
            var firma = await _context.Ys_Firmalar
                .Include(x => x.FirmaMarkalar)
                .Include(x => x.FirmaKategoriler)
                .Include(x => x.Subeler)
                .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId);

            // Kendi kayıt olanlarda (username = VKN) zorunlu onboarding yok.
            var adminOlusturmus = firma != null
                && !string.IsNullOrWhiteSpace(firma.VergiNo)
                && !string.Equals((kullanici.UserName ?? "").Trim(), (firma.VergiNo ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

            if (!adminOlusturmus)
                return (false, true, new List<string>());

            var eksikler = new List<string>();
            var markaVar = firma?.FirmaMarkalar?.Any(x => !x.SilindiMi) == true;
            var kategoriVar = firma?.FirmaKategoriler?.Any(x => !x.SilindiMi) == true;
            var subeVar = firma?.Subeler?.Any(x => !x.SilindiMi) == true;
            var sertifikaVar = await _context.Ys_Sertifikalar
                .AnyAsync(x => x.FirmaId == kullanici.FirmaId && !x.SilindiMi);

            if (!markaVar) eksikler.Add("Marka seçimi");
            if (!kategoriVar) eksikler.Add("Kategori seçimi");
            if (!subeVar) eksikler.Add("Şube kaydı");
            if (!sertifikaVar) eksikler.Add("Yetki belgesi yükleme");

            return (true, eksikler.Count == 0, eksikler);
        }

        [HttpGet]
        [Route("")]
        [Route("index")]
        public async Task<IActionResult> Index()
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var kurulum = await GetIlkKurulumDurumu(kullanici);

            var firma = await _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .Include(x => x.FirmaMarkalar!).ThenInclude(x => x.Marka)
                .Include(x => x.Sertifikalar)
                .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId);

            var buAy = await _context.Ys_DevreyeAlmalar
                .Where(x => x.FirmaId == kullanici.FirmaId
                    && x.OlusturmaTarihi.Month == DateTime.Now.Month
                    && x.OlusturmaTarihi.Year == DateTime.Now.Year
                    && !x.SilindiMi)
                .CountAsync();

            var toplam = await _context.Ys_DevreyeAlmalar
                .Where(x => x.FirmaId == kullanici.FirmaId && !x.SilindiMi)
                .CountAsync();

            var sonIslemler = await _context.Ys_DevreyeAlmalar
                .Include(x => x.Marka)
                .Where(x => x.FirmaId == kullanici.FirmaId && !x.SilindiMi)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .Take(5)
                .ToListAsync();

            ViewBag.Firma = firma;
            ViewBag.BuAy = buAy;
            ViewBag.Toplam = toplam;
            ViewBag.SonIslemler = sonIslemler;
            ViewBag.Kullanici = kullanici;
            ViewBag.IlkKurulumZorunlu = kurulum.zorunluMu;
            ViewBag.IlkKurulumTamamlandi = kurulum.tamamlandiMi;
            ViewBag.IlkKurulumEksikler = kurulum.eksikler;
            await SetBildirimler(kullanici);
            if (firma?.Sertifikalar != null)
            {
                var onayli = firma.Sertifikalar
                    .Where(x => x.Durum == 1)
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .FirstOrDefault();
                if (onayli != null)
                {
                    var kalan = (onayli.SertifikaBitisTarihi.Date - DateTime.Now.Date).Days;
                    if (kalan >= 0)
                    {
                        ViewBag.SertifikaUyariGun = kalan;
                    }
                }
            }

            return View("~/Views/YetkiliServisPanel/Index.cshtml");
        }

        [HttpGet]
        [Route("ilk-kurulum")]
        public async Task<IActionResult> IlkKurulum()
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var kurulum = await GetIlkKurulumDurumu(kullanici);
            if (!kurulum.zorunluMu) return Redirect("/ys-panel");
            if (kurulum.tamamlandiMi)
            {
                TempData["Basarili"] = "İlk kurulum zaten tamamlanmış.";
                return Redirect("/ys-panel");
            }

            var firmaId = kullanici.FirmaId ?? 0;
            if (firmaId <= 0)
            {
                TempData["Hata"] = "Kullanıcı hesabı bir firmaya bağlı olmadığı için ilk kurulum yapılamadı.";
                return Redirect("/ys-panel");
            }

            var firma = await _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .FirstOrDefaultAsync(x => x.Id == firmaId && !x.SilindiMi);
            if (firma == null)
            {
                TempData["Hata"] = "Firma kaydı bulunamadı. Lütfen yönetici ile görüşün.";
                return Redirect("/ys-panel");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.Firma = firma;
            ViewBag.TumMarkalar = await _context.Ys_Markalar.Where(x => !x.SilindiMi && x.AktifMi).OrderBy(x => x.MarkaAdi).ToListAsync();
            ViewBag.TumKategoriler = await _context.UrunKategoriler.Where(x => !x.SilindiMi && x.AktifMi).OrderBy(x => x.SiraNo).ThenBy(x => x.Ad).ToListAsync();
            ViewBag.SeciliMarkaIds = await _context.Ys_FirmaMarkalar.Where(x => x.FirmaId == firmaId && !x.SilindiMi).Select(x => x.MarkaId).ToListAsync();
            ViewBag.SeciliKategoriIds = await _context.Ys_FirmaKategoriler.Where(x => x.FirmaId == firmaId && !x.SilindiMi).Select(x => x.KategoriId).ToListAsync();
            ViewBag.AktifSubeSayisi = await _context.Ys_Subeler.Where(x => x.FirmaId == firmaId && !x.SilindiMi).CountAsync();
            ViewBag.SertifikaVar = await _context.Ys_Sertifikalar.AnyAsync(x => x.FirmaId == firmaId && !x.SilindiMi);
            ViewBag.OnayliSertifikaVar = await _context.Ys_Sertifikalar.AnyAsync(x => x.FirmaId == firmaId && !x.SilindiMi && x.Durum == 1);
            ViewBag.IlkKurulumEksikler = kurulum.eksikler;
            await SetBildirimler(kullanici);
            return View("~/Views/YetkiliServisPanel/IlkKurulum.cshtml");
        }

        [HttpPost]
        [Route("ilk-kurulum")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IlkKurulumKaydet(List<int> markaIds, List<int> kategoriIds, string? subeAdi, string? il, string? ilce, string? telefon, string? adres)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var kurulum = await GetIlkKurulumDurumu(kullanici);
            if (!kurulum.zorunluMu) return Redirect("/ys-panel");

            var firmaId = kullanici.FirmaId ?? 0;
            if (firmaId <= 0)
            {
                TempData["Hata"] = "Kullanıcı hesabı bir firmaya bağlı olmadığı için ilk kurulum kaydedilemedi.";
                return Redirect("/ys-panel");
            }

            var firma = await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.Id == firmaId && !x.SilindiMi);
            if (firma == null)
            {
                TempData["Hata"] = "Firma kaydı bulunamadı. Lütfen yönetici ile görüşün.";
                return Redirect("/ys-panel");
            }

            if (markaIds == null || markaIds.Count == 0)
            {
                TempData["Hata"] = "En az bir marka seçmelisiniz.";
                return Redirect("/ys-panel/ilk-kurulum");
            }

            if (kategoriIds == null || kategoriIds.Count == 0)
            {
                TempData["Hata"] = "En az bir kategori seçmelisiniz.";
                return Redirect("/ys-panel/ilk-kurulum");
            }

            var aktifSubeSayisi = await _context.Ys_Subeler.Where(x => x.FirmaId == firmaId && !x.SilindiMi).CountAsync();
            if (aktifSubeSayisi == 0 && string.IsNullOrWhiteSpace(subeAdi))
            {
                TempData["Hata"] = "En az bir şube tanımı zorunludur.";
                return Redirect("/ys-panel/ilk-kurulum");
            }

            var gecerliMarkaIds = await _context.Ys_Markalar
                .Where(x => !x.SilindiMi && x.AktifMi && markaIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();
            if (gecerliMarkaIds.Count == 0)
            {
                TempData["Hata"] = "Seçilen markalar geçerli değil.";
                return Redirect("/ys-panel/ilk-kurulum");
            }

            var gecerliKategoriIds = await _context.UrunKategoriler
                .Where(x => !x.SilindiMi && x.AktifMi && kategoriIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();
            if (gecerliKategoriIds.Count == 0)
            {
                TempData["Hata"] = "Seçilen kategoriler geçerli değil.";
                return Redirect("/ys-panel/ilk-kurulum");
            }

            var mevcutMarkalar = await _context.Ys_FirmaMarkalar.Where(x => x.FirmaId == firmaId).ToListAsync();
            _context.Ys_FirmaMarkalar.RemoveRange(mevcutMarkalar);
            foreach (var mid in gecerliMarkaIds.Distinct())
            {
                _context.Ys_FirmaMarkalar.Add(new Ys_FirmaMarka
                {
                    FirmaId = firmaId,
                    MarkaId = mid,
                    YetkiBitisTarihi = DateTime.Now.AddYears(5),
                    OlusturmaTarihi = DateTime.Now,
                    OlusturanKullanici = kullanici.UserName ?? "sistem",
                    SilindiMi = false
                });
            }

            var mevcutKategoriler = await _context.Ys_FirmaKategoriler.Where(x => x.FirmaId == firmaId).ToListAsync();
            _context.Ys_FirmaKategoriler.RemoveRange(mevcutKategoriler);
            foreach (var kid in gecerliKategoriIds.Distinct())
            {
                _context.Ys_FirmaKategoriler.Add(new Ys_FirmaKategori
                {
                    FirmaId = firmaId,
                    KategoriId = kid,
                    YetkiBitisTarihi = DateTime.Now.AddYears(5),
                    OlusturmaTarihi = DateTime.Now,
                    OlusturanKullanici = kullanici.UserName ?? "sistem",
                    SilindiMi = false
                });
            }

            if (!string.IsNullOrWhiteSpace(subeAdi))
            {
                var temizSubeAdi = subeAdi.Trim();
                var mevcutSube = await _context.Ys_Subeler
                    .FirstOrDefaultAsync(x => x.FirmaId == firmaId && !x.SilindiMi && x.SubeAdi == temizSubeAdi);

                if (mevcutSube == null)
                {
                    _context.Ys_Subeler.Add(new Ys_Sube
                    {
                        FirmaId = firmaId,
                        SubeAdi = temizSubeAdi,
                        Il = il,
                        Ilce = ilce,
                        Telefon = telefon,
                        Adres = adres,
                        AktifMi = true,
                        OlusturanKullanici = kullanici.UserName ?? "sistem",
                        SilindiMi = false
                    });
                }
                else
                {
                    mevcutSube.Il = il;
                    mevcutSube.Ilce = ilce;
                    mevcutSube.Telefon = telefon;
                    mevcutSube.Adres = adres;
                    mevcutSube.AktifMi = true;
                    mevcutSube.GuncellemeTarihi = DateTime.Now;
                    mevcutSube.GuncelleyenKullanici = kullanici.UserName ?? "sistem";
                }
            }

            await _context.SaveChangesAsync();

            var kayitliMarkaSayisi = await _context.Ys_FirmaMarkalar.CountAsync(x => x.FirmaId == firmaId && !x.SilindiMi);
            var kayitliKategoriSayisi = await _context.Ys_FirmaKategoriler.CountAsync(x => x.FirmaId == firmaId && !x.SilindiMi);
            var kayitliSubeSayisi = await _context.Ys_Subeler.CountAsync(x => x.FirmaId == firmaId && !x.SilindiMi);
            var kayitliSertifikaSayisi = await _context.Ys_Sertifikalar.CountAsync(x => x.FirmaId == firmaId && !x.SilindiMi);
            if (kayitliMarkaSayisi == 0 || kayitliKategoriSayisi == 0 || kayitliSubeSayisi == 0)
            {
                TempData["Hata"] = "İlk kurulum kayıtları eksik görünüyor. Lütfen tekrar deneyin.";
                return Redirect("/ys-panel/ilk-kurulum");
            }

            if (kayitliSertifikaSayisi == 0)
            {
                TempData["Hata"] = "Marka, kategori ve şube kaydedildi. İlk kurulumun tamamlanması için lütfen yetki belgenizi yükleyin.";
                return Redirect("/ys-yetki-belgesi");
            }

            TempData["Basarili"] = $"İlk kurulum tamamlandı. Marka: {kayitliMarkaSayisi}, Kategori: {kayitliKategoriSayisi}, Şube: {kayitliSubeSayisi}, Yetki Belgesi: {kayitliSertifikaSayisi}.";
            return Redirect("/ys-panel");
        }

        [HttpGet]
        [Route("profil")]
        public async Task<IActionResult> Profil()
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var firma = await _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .Include(x => x.FirmaMarkalar!).ThenInclude(x => x.Marka)
                .Include(x => x.FirmaKategoriler)
                .Include(x => x.Subeler)
                .Include(x => x.Sertifikalar)
                .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId);

            ViewBag.Firma = firma;
            ViewBag.Kullanici = kullanici;
            await SetBildirimler(kullanici);

            return View("~/Views/YetkiliServisPanel/Profil.cshtml");
        }

        [HttpGet]
        [Route("markalar")]
        public async Task<IActionResult> Markalar()
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var firma = await _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .Include(x => x.FirmaMarkalar!).ThenInclude(x => x.Marka)
                .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId);

            ViewBag.Firma = firma;
            ViewBag.Kullanici = kullanici;
            await SetBildirimler(kullanici);
            ViewBag.TumMarkalar = await _context.Ys_Markalar
                .Where(x => !x.SilindiMi && x.AktifMi)
                .OrderBy(x => x.MarkaAdi)
                .ToListAsync();
            ViewBag.FirmaMarkalar = await _context.Ys_FirmaMarkalar
                .Include(x => x.Marka)
                .Where(x => x.FirmaId == kullanici.FirmaId && !x.SilindiMi)
                .OrderBy(x => x.Marka!.MarkaAdi)
                .ToListAsync();
            ViewBag.SeciliMarkaIds = await _context.Ys_FirmaMarkalar
                .Where(x => x.FirmaId == kullanici.FirmaId && !x.SilindiMi)
                .Select(x => x.MarkaId)
                .ToListAsync();

            return View("~/Views/YetkiliServisPanel/Markalar.cshtml");
        }

        [HttpGet]
        [Route("subeler")]
        public async Task<IActionResult> Subeler()
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var firma = await _context.Ys_Firmalar
                .Include(x => x.Subeler)
                .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId);

            ViewBag.Firma = firma;
            ViewBag.Kullanici = kullanici;
            ViewBag.Subeler = firma?.Subeler?
                .Where(x => !x.SilindiMi)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ToList() ?? new List<Ys_Sube>();
            await SetBildirimler(kullanici);

            return View("~/Views/YetkiliServisPanel/Subeler.cshtml");
        }

        [HttpGet]
        [Route("subeler/duzenle/{id:int}")]
        public async Task<IActionResult> SubeDuzenle(int id)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sube = await _context.Ys_Subeler
                .FirstOrDefaultAsync(x => x.Id == id && x.FirmaId == kullanici.FirmaId);
            if (sube == null) return Redirect("/ys-panel/subeler");

            var firma = await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId);

            ViewBag.Sube = sube;
            ViewBag.Firma = firma;
            await SetBildirimler(kullanici);
            return View("~/Views/YetkiliServisPanel/SubeDuzenle.cshtml");
        }

        [HttpPost]
        [Route("subeler/duzenle/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeDuzenleKaydet(
            int id,
            string? subeAdi,
            string? il,
            string? ilce,
            string? telefon,
            string? adres,
            bool aktifMi)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sube = await _context.Ys_Subeler
                .FirstOrDefaultAsync(x => x.Id == id && x.FirmaId == kullanici.FirmaId);
            if (sube == null) return Redirect("/ys-panel/subeler");

            sube.SubeAdi = subeAdi;
            sube.Il = il;
            sube.Ilce = ilce;
            sube.Telefon = telefon;
            sube.Adres = adres;
            sube.AktifMi = aktifMi;
            sube.GuncellemeTarihi = DateTime.Now;
            sube.GuncelleyenKullanici = kullanici.UserName ?? "sistem";

            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Şube güncellendi.";
            return Redirect("/ys-panel/subeler");
        }

        [HttpPost]
        [Route("subeler/ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeEkle(
            string? subeAdi,
            string? il,
            string? ilce,
            string? telefon,
            string? adres,
            bool aktifMi)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            if (string.IsNullOrWhiteSpace(subeAdi))
            {
                TempData["Hata"] = "Şube adı zorunludur.";
                return Redirect("/ys-panel/subeler");
            }

            var sube = new Ys_Sube
            {
                FirmaId = kullanici.FirmaId ?? 0,
                SubeAdi = subeAdi,
                Il = il,
                Ilce = ilce,
                Telefon = telefon,
                Adres = adres,
                AktifMi = aktifMi,
                OlusturanKullanici = kullanici.UserName ?? "sistem"
            };

            _context.Ys_Subeler.Add(sube);
            await _context.SaveChangesAsync();

            TempData["Basarili"] = "Şube kaydı eklendi.";
            return Redirect("/ys-panel/subeler");
        }

        [HttpPost]
        [Route("subeler/durum")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeDurum(int id)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sube = await _context.Ys_Subeler
                .FirstOrDefaultAsync(x => x.Id == id && x.FirmaId == kullanici.FirmaId);

            if (sube == null)
            {
                TempData["Hata"] = "Şube bulunamadı.";
                return Redirect("/ys-panel/subeler");
            }

            sube.AktifMi = !sube.AktifMi;
            sube.GuncellemeTarihi = DateTime.Now;
            sube.GuncelleyenKullanici = kullanici.UserName ?? "sistem";
            await _context.SaveChangesAsync();

            TempData["Basarili"] = "Şube durumu güncellendi.";
            return Redirect("/ys-panel/subeler");
        }

        [HttpPost]
        [Route("subeler/sil")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeSil(int id)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sube = await _context.Ys_Subeler
                .FirstOrDefaultAsync(x => x.Id == id && x.FirmaId == kullanici.FirmaId);

            if (sube == null)
            {
                TempData["Hata"] = "Şube bulunamadı.";
                return Redirect("/ys-panel/subeler");
            }

            sube.SilindiMi = true;
            sube.SilinmeTarihi = DateTime.Now;
            sube.SilenKullanici = kullanici.UserName ?? "sistem";
            await _context.SaveChangesAsync();

            TempData["Basarili"] = "Şube kaydı silindi.";
            return Redirect("/ys-panel/subeler");
        }

        [HttpPost]
        [Route("marka-guncelle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaGuncelle(List<int> markaIds)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var firmaId = kullanici.FirmaId ?? 0;
            var mevcut = await _context.Ys_FirmaMarkalar
                .Where(x => x.FirmaId == firmaId)
                .ToListAsync();
            _context.Ys_FirmaMarkalar.RemoveRange(mevcut);

            if (markaIds != null && markaIds.Count > 0)
            {
                foreach (var mid in markaIds.Distinct())
                {
                    _context.Ys_FirmaMarkalar.Add(new Ys_FirmaMarka
                    {
                        FirmaId = firmaId,
                        MarkaId = mid,
                        YetkiBitisTarihi = DateTime.Now.AddYears(5),
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici.UserName ?? "sistem",
                        SilindiMi = false
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Marka yetkileri güncellendi.";
            return Redirect("/ys-panel/markalar");
        }

        [HttpPost]
        [Route("marka-ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaEkle(string? markaAdi, string? aciklama)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            if (string.IsNullOrWhiteSpace(markaAdi))
            {
                TempData["Hata"] = "Marka adı zorunludur.";
                return Redirect("/ys-panel/markalar");
            }

            var temizAdi = markaAdi.Trim();
            var mevcutMarka = await _context.Ys_Markalar
                .FirstOrDefaultAsync(x => !x.SilindiMi && x.MarkaAdi != null && x.MarkaAdi.ToLower() == temizAdi.ToLower());

            if (mevcutMarka == null)
            {
                mevcutMarka = new Ys_Marka
                {
                    MarkaAdi = temizAdi,
                    Aciklama = aciklama,
                    AktifMi = true,
                    OlusturanKullanici = kullanici.UserName ?? "sistem"
                };
                _context.Ys_Markalar.Add(mevcutMarka);
                await _context.SaveChangesAsync();
            }

            var firmaId = kullanici.FirmaId ?? 0;
            var bag = await _context.Ys_FirmaMarkalar
                .FirstOrDefaultAsync(x => x.FirmaId == firmaId && x.MarkaId == mevcutMarka.Id);
            if (bag == null)
            {
                _context.Ys_FirmaMarkalar.Add(new Ys_FirmaMarka
                {
                    FirmaId = firmaId,
                    MarkaId = mevcutMarka.Id,
                    YetkiBitisTarihi = DateTime.Now.AddYears(5),
                    OlusturmaTarihi = DateTime.Now,
                    OlusturanKullanici = kullanici.UserName ?? "sistem"
                });
                await _context.SaveChangesAsync();
            }

            TempData["Basarili"] = "Marka eklendi.";
            return Redirect("/ys-panel/markalar");
        }

        [HttpPost]
        [Route("marka-duzenle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaDuzenle(int id, string? markaAdi, string? aciklama)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var firmaId = kullanici.FirmaId ?? 0;
            var bag = await _context.Ys_FirmaMarkalar
                .Include(x => x.Marka)
                .FirstOrDefaultAsync(x => x.FirmaId == firmaId && x.MarkaId == id && !x.SilindiMi);
            if (bag?.Marka == null)
            {
                TempData["Hata"] = "Marka bulunamadı.";
                return Redirect("/ys-panel/markalar");
            }

            if (!string.IsNullOrWhiteSpace(markaAdi))
            {
                bag.Marka.MarkaAdi = markaAdi.Trim();
            }
            bag.Marka.Aciklama = aciklama;
            bag.Marka.GuncellemeTarihi = DateTime.Now;
            bag.Marka.GuncelleyenKullanici = kullanici.UserName ?? "sistem";
            await _context.SaveChangesAsync();

            TempData["Basarili"] = "Marka güncellendi.";
            return Redirect("/ys-panel/markalar");
        }

        [HttpPost]
        [Route("marka-sil")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaSil(int id)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var firmaId = kullanici.FirmaId ?? 0;
            var bag = await _context.Ys_FirmaMarkalar
                .FirstOrDefaultAsync(x => x.FirmaId == firmaId && x.MarkaId == id && !x.SilindiMi);
            if (bag != null)
            {
                bag.SilindiMi = true;
                bag.SilinmeTarihi = DateTime.Now;
                bag.SilenKullanici = kullanici.UserName ?? "sistem";
            }

            await _context.SaveChangesAsync();
            TempData["Basarili"] = "Marka yetkisi kaldırıldı.";
            return Redirect("/ys-panel/markalar");
        }

        [HttpPost]
        [Route("profil-guncelle")]
        public async Task<IActionResult> ProfilGuncelle(
            string? adSoyad, string? telefon, string? email)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            kullanici.AdSoyad = adSoyad ?? kullanici.AdSoyad;
            kullanici.PhoneNumber = telefon ?? kullanici.PhoneNumber;

            if (!string.IsNullOrEmpty(email) && email != kullanici.Email)
            {
                kullanici.Email = email;
                kullanici.UserName = email;
            }

            var sonuc = await _userManager.UpdateAsync(kullanici);
            if (sonuc.Succeeded)
            {
                var firmaId = kullanici.FirmaId ?? 0;
                if (firmaId > 0 && !string.IsNullOrWhiteSpace(adSoyad))
                {
                    var firma = await _context.Ys_Firmalar.FirstOrDefaultAsync(x => x.Id == firmaId);
                    if (firma != null)
                    {
                        firma.YetkiliKisi = adSoyad;
                        await _context.SaveChangesAsync();
                    }
                }
                TempData["Basarili"] = "Profil bilgileri güncellendi.";
            }
            else
                TempData["Hata"] = "Güncelleme sırasında hata oluştu.";

            return Redirect("/ys-panel/profil");
        }

        [HttpPost]
        [Route("sifre-degistir")]
        public async Task<IActionResult> SifreDegistir(
            string mevcutSifre, string yeniSifre, string yeniSifreTekrar)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            if (yeniSifre != yeniSifreTekrar)
            {
                TempData["SifreHata"] = "Yeni şifreler eşleşmiyor.";
                return Redirect("/ys-panel/profil");
            }

            if (yeniSifre.Length < 6)
            {
                TempData["SifreHata"] = "Şifre en az 6 karakter olmalıdır.";
                return Redirect("/ys-panel/profil");
            }

            var sonuc = await _userManager.ChangePasswordAsync(
                kullanici, mevcutSifre, yeniSifre);

            if (sonuc.Succeeded)
                TempData["SifreBasarili"] = "Şifreniz başarıyla değiştirildi.";
            else
                TempData["SifreHata"] = "Mevcut şifre hatalı.";

            return Redirect("/ys-panel/profil");
        }

        [HttpGet]
        [Route("raporlar")]
        public async Task<IActionResult> Raporlar(DateTime? bas, DateTime? bit)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var firmaId = kullanici.FirmaId ?? 0;
            var firma = await _context.Ys_Firmalar.FirstOrDefaultAsync(x => x.Id == firmaId);
            var tarihAraligi = await GetRaporTarihAraligi(firmaId, bas, bit);
            var basTarih = tarihAraligi.Bas;
            var bitTarih = tarihAraligi.Bit;
            var bitSonrasi = bitTarih.AddDays(1);

            var devreyeTemelQuery = _context.Ys_DevreyeAlmalar
                .Include(x => x.Marka)
                .Where(x => x.FirmaId == firmaId
                    && !x.SilindiMi
                    && x.DevreyeAlmaTarihi >= basTarih
                    && x.DevreyeAlmaTarihi < bitSonrasi);

            var sertifikaTemelQuery = _context.Ys_Sertifikalar
                .Where(x => x.FirmaId == firmaId
                    && !x.SilindiMi
                    && x.OlusturmaTarihi >= basTarih
                    && x.OlusturmaTarihi < bitSonrasi);

            var devreyeSayisi = await devreyeTemelQuery.CountAsync();
            var tamamlanan = await devreyeTemelQuery.Where(x => x.Durum == 1).CountAsync();
            var bekleyen = await devreyeTemelQuery.Where(x => x.Durum == 0).CountAsync();

            var sertifikaOnayli = await sertifikaTemelQuery.Where(x => x.Durum == 1).CountAsync();
            var sertifikaBekleyen = await sertifikaTemelQuery.Where(x => x.Durum == 0).CountAsync();
            var sertifikaReddedilen = await sertifikaTemelQuery.Where(x => x.Durum == 2).CountAsync();

            var sonIslemler = await devreyeTemelQuery
                .OrderByDescending(x => x.DevreyeAlmaTarihi)
                .Take(10)
                .ToListAsync();

            var aylikBaslangic = new DateTime(basTarih.Year, basTarih.Month, 1);
            var aylikBitis = new DateTime(bitTarih.Year, bitTarih.Month, 1);
            var aySayisi = ((aylikBitis.Year - aylikBaslangic.Year) * 12) + aylikBitis.Month - aylikBaslangic.Month + 1;
            if (aySayisi < 1) aySayisi = 1;

            var aylikEtiketler = Enumerable.Range(0, aySayisi)
                .Select(i => aylikBaslangic.AddMonths(i))
                .ToList();

            var aylikHam = await devreyeTemelQuery
                .GroupBy(x => new { x.DevreyeAlmaTarihi.Year, x.DevreyeAlmaTarihi.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();

            var aylikMap = aylikHam.ToDictionary(x => $"{x.Year:D4}-{x.Month:D2}", x => x.Count);
            var chartAylikLabels = aylikEtiketler.Select(x => x.ToString("MM.yyyy")).ToList();
            var chartAylikData = aylikEtiketler
                .Select(x => aylikMap.TryGetValue($"{x.Year:D4}-{x.Month:D2}", out var value) ? value : 0)
                .ToList();

            var chartMarka = await devreyeTemelQuery
                .Where(x => x.Marka != null && !string.IsNullOrEmpty(x.Marka.MarkaAdi))
                .GroupBy(x => x.Marka!.MarkaAdi)
                .Select(g => new { Marka = g.Key, Sayi = g.Count() })
                .OrderByDescending(x => x.Sayi)
                .Take(6)
                .ToListAsync();

            ViewBag.BasTarih = basTarih;
            ViewBag.BitTarih = bitTarih;
            ViewBag.DevreyeSayisi = devreyeSayisi;
            ViewBag.Tamamlanan = tamamlanan;
            ViewBag.Bekleyen = bekleyen;
            ViewBag.SertifikaOnayli = sertifikaOnayli;
            ViewBag.SertifikaBekleyen = sertifikaBekleyen;
            ViewBag.SertifikaReddedilen = sertifikaReddedilen;
            ViewBag.SonIslemler = sonIslemler;
            ViewBag.ChartAylikLabels = chartAylikLabels;
            ViewBag.ChartAylikData = chartAylikData;
            ViewBag.ChartDurumData = new List<int> { sertifikaOnayli, sertifikaBekleyen, sertifikaReddedilen };
            ViewBag.ChartMarkaLabels = chartMarka.Select(x => x.Marka).ToList();
            ViewBag.ChartMarkaData = chartMarka.Select(x => x.Sayi).ToList();
            ViewBag.Firma = firma;
            await SetBildirimler(kullanici);
            return View("~/Views/YetkiliServisPanel/Raporlar.cshtml");
        }

        [HttpGet]
        [Route("raporlar/pdf")]
        public async Task<IActionResult> RaporlarPdf(DateTime? bas, DateTime? bit, List<int>? ids)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var firmaId = kullanici.FirmaId ?? 0;
            List<Ys_DevreyeAlma> sonIslemler;
            DateTime basTarih;
            DateTime bitTarih;

            if (ids != null && ids.Count > 0)
            {
                sonIslemler = await _context.Ys_DevreyeAlmalar
                    .Include(x => x.Marka)
                    .Where(x => x.FirmaId == firmaId && !x.SilindiMi && ids.Contains(x.Id))
                    .OrderByDescending(x => x.DevreyeAlmaTarihi)
                    .ToListAsync();

                basTarih = sonIslemler.Count > 0 ? sonIslemler.Min(x => x.DevreyeAlmaTarihi).Date : DateTime.Now.Date;
                bitTarih = sonIslemler.Count > 0 ? sonIslemler.Max(x => x.DevreyeAlmaTarihi).Date : DateTime.Now.Date;
            }
            else
            {
                var tarihAraligi = await GetRaporTarihAraligi(firmaId, bas, bit);
                basTarih = tarihAraligi.Bas;
                bitTarih = tarihAraligi.Bit;
                var bitSonrasi = bitTarih.AddDays(1);

                sonIslemler = await _context.Ys_DevreyeAlmalar
                    .Include(x => x.Marka)
                    .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.DevreyeAlmaTarihi >= basTarih && x.DevreyeAlmaTarihi < bitSonrasi)
                    .OrderByDescending(x => x.DevreyeAlmaTarihi)
                    .Take(10)
                    .ToListAsync();
            }

            var devreyeSayisi = sonIslemler.Count;
            var tamamlanan = sonIslemler.Count(x => x.Durum == 1);
            var bekleyen = sonIslemler.Count(x => x.Durum == 0);

            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Yetkili Servis Raporları").FontSize(16).SemiBold();
                            col.Item().Text($"Rapor Aralığı: {basTarih:dd.MM.yyyy} - {bitTarih:dd.MM.yyyy}")
                                .FontSize(10).FontColor("#555555");
                        });
                        row.ConstantItem(160).AlignRight().Text(DateTime.Now.ToString("dd.MM.yyyy HH:mm"))
                            .FontSize(10).FontColor("#777777");
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(12);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn();
                                c.RelativeColumn();
                                c.RelativeColumn();
                            });

                            void Cell(string title, string value)
                            {
                                table.Cell().Element(cell =>
                                {
                                    cell.Border(1).BorderColor("#E5E7EB").Padding(8).Background("#F8FAFC")
                                        .Column(column =>
                                        {
                                            column.Item().Text(title).FontSize(9).FontColor("#6B7280");
                                            column.Item().Text(value).FontSize(14).SemiBold().FontColor("#111827");
                                        });
                                });
                            }

                            Cell("Toplam İşlem", devreyeSayisi.ToString());
                            Cell("Tamamlanan", tamamlanan.ToString());
                            Cell("Bekleyen", bekleyen.ToString());
                        });

                        col.Item().Text("Son İşlemler").FontSize(12).SemiBold();

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(1f);
                                c.RelativeColumn(1f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background("#F3F4F6").Padding(6).Text("Tesisat No").SemiBold().FontSize(10);
                                header.Cell().Background("#F3F4F6").Padding(6).Text("Müşteri").SemiBold().FontSize(10);
                                header.Cell().Background("#F3F4F6").Padding(6).Text("Marka").SemiBold().FontSize(10);
                                header.Cell().Background("#F3F4F6").Padding(6).Text("Tarih").SemiBold().FontSize(10);
                            });

                            foreach (var d in sonIslemler)
                            {
                                table.Cell().Padding(6).Text(d.TesistatNo ?? "-").FontSize(10);
                                table.Cell().Padding(6).Text(d.MusteriAdi ?? "-").FontSize(10);
                                table.Cell().Padding(6).Text(d.Marka?.MarkaAdi ?? "-").FontSize(10);
                                table.Cell().Padding(6).Text(d.DevreyeAlmaTarihi.ToString("dd.MM.yyyy")).FontSize(10);
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text("Yetkili Servis Gaz Açma Sistemi").FontSize(9).FontColor("#888888");
                });
            });

            var pdfBytes = document.GeneratePdf();
            var dosyaAdi = $"raporlar_{basTarih:yyyyMMdd}_{bitTarih:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", dosyaAdi);
        }

        [HttpGet]
        [Route("raporlar/pdf-toplu")]
        public async Task<IActionResult> RaporlarPdfToplu()
        {
            var bit = DateTime.Now.Date;
            var bas = bit.AddDays(-30);
            return await RaporlarPdf(bas, bit, null);
        }

        [HttpGet]
        [Route("raporlar/excel")]
        public async Task<IActionResult> RaporlarExcel(DateTime? bas, DateTime? bit, List<int>? ids)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var firmaId = kullanici.FirmaId ?? 0;
            List<Ys_DevreyeAlma> islemler;
            DateTime basTarih;
            DateTime bitTarih;

            if (ids != null && ids.Count > 0)
            {
                islemler = await _context.Ys_DevreyeAlmalar
                    .Include(x => x.Marka)
                    .Include(x => x.Firma)
                        .ThenInclude(x => x!.Sirket)
                    .Where(x => x.FirmaId == firmaId && !x.SilindiMi && ids.Contains(x.Id))
                    .OrderByDescending(x => x.DevreyeAlmaTarihi)
                    .ToListAsync();

                basTarih = islemler.Count > 0 ? islemler.Min(x => x.DevreyeAlmaTarihi).Date : DateTime.Now.Date;
                bitTarih = islemler.Count > 0 ? islemler.Max(x => x.DevreyeAlmaTarihi).Date : DateTime.Now.Date;
            }
            else
            {
                var tarihAraligi = await GetRaporTarihAraligi(firmaId, bas, bit);
                basTarih = tarihAraligi.Bas;
                bitTarih = tarihAraligi.Bit;
                var bitSonrasi = bitTarih.AddDays(1);

                islemler = await _context.Ys_DevreyeAlmalar
                    .Include(x => x.Marka)
                    .Include(x => x.Firma)
                        .ThenInclude(x => x!.Sirket)
                    .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.DevreyeAlmaTarihi >= basTarih && x.DevreyeAlmaTarihi < bitSonrasi)
                    .OrderByDescending(x => x.DevreyeAlmaTarihi)
                    .ToListAsync();
            }

            var bytes = DevreyeAlmaExcelService.Olustur(islemler);
            var dosyaAdi = $"raporlar_{basTarih:yyyyMMdd}_{bitTarih:yyyyMMdd}.csv";
            return File(bytes, "text/csv; charset=windows-1254", dosyaAdi);
        }

        [HttpGet]
        [Route("raporlar/excel-toplu")]
        public async Task<IActionResult> RaporlarExcelToplu()
        {
            var bit = DateTime.Now.Date;
            var bas = bit.AddDays(-30);
            return await RaporlarExcel(bas, bit, null);
        }
    }
}


