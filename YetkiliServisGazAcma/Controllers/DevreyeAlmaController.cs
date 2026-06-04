using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Business.Services.Online;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize(Roles = "YetkiliServis")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("ys-devreyeal")]
    public class DevreyeAlmaController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly OnlineCihazBilgileriClient _onlineCihazBilgileriClient;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;

        public DevreyeAlmaController(
            AppDbContext context,
            UserManager<AppKullanici> userManager,
            OnlineCihazBilgileriClient onlineCihazBilgileriClient,
            SehirFirmaKoduService sehirFirmaKoduService)
        {
            _context = context;
            _userManager = userManager;
            _onlineCihazBilgileriClient = onlineCihazBilgileriClient;
            _sehirFirmaKoduService = sehirFirmaKoduService;
        }

        private string? OnlineFirmaKodu(Ys_Firma? firma)
        {
            return _sehirFirmaKoduService.FirmaKodu(firma?.FaaliyetIli)
                ?? _sehirFirmaKoduService.FirmaKodu(firma?.Sirket?.Il)
                ?? FirmaKoduFromSirketAdi(firma?.Sirket?.SirketAdi);
        }

        private static string? FirmaKoduFromSirketAdi(string? sirketAdi)
        {
            if (string.IsNullOrWhiteSpace(sirketAdi))
                return null;

            var normalized = NormalizeFirmaText(sirketAdi);
            if (normalized.Contains("CORUM") || normalized.Contains("CORUMGAZ"))
                return "CORUMGAZ";
            if (normalized.Contains("KARGAZ") || normalized.Contains("KASTAMONU") || normalized.Contains("KARABUK"))
                return "KARGAZ";
            if (normalized.Contains("SURMELI") || normalized.Contains("SURMELIGAZ") || normalized.Contains("YOZGAT"))
                return "SURMELIGAZ";
            if (normalized.Contains("YALOVA"))
                return "MARMARAGAZ_YALOVA";
            if (normalized.Contains("CORLU") || normalized.Contains("TEKIRDAG"))
                return "MARMARAGAZ_CORLU";

            return normalized;
        }

        private static string NormalizeFirmaText(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);
            var chars = normalized
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_')
                .ToArray();

            return new string(chars)
                .Normalize(NormalizationForm.FormC)
                .Trim('_');
        }

        private async Task SetBildirimler(AppKullanici kullanici)
        {
            var bildirimler = new List<string>();
            var firmaId = kullanici.FirmaId ?? 0;

            var firma = await _context.Ys_Firmalar
                .Include(x => x.YetkiBelgeleri)
                .FirstOrDefaultAsync(x => x.Id == firmaId);

            var bugun = DateTime.Now.Date;
            var onayli = firma?.YetkiBelgeleri?
                .Where(x => x.Durum == 1
                    && !x.SilindiMi
                    && (!x.YetkiBelgesiBaslangicTarihi.HasValue || x.YetkiBelgesiBaslangicTarihi.Value.Date <= bugun)
                    && x.YetkiBelgesiBitisTarihi.Date >= bugun)
                .OrderBy(x => x.YetkiBelgesiBitisTarihi)
                .FirstOrDefault();
            var bekleyenVar = firma?.YetkiBelgeleri?.Any(x => x.Durum == 0 && !x.SilindiMi) ?? false;
            if (onayli != null)
            {
                bildirimler.Add("Yetki belgeniz onaylandı. Cihaz devreye alabilirsiniz.");
                var kalan = (onayli.YetkiBelgesiBitisTarihi.Date - bugun).Days;
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
            var yetkiBelgesiVar = await _context.Ys_YetkiBelgeleri
                .AnyAsync(x => x.FirmaId == kullanici.FirmaId && !x.SilindiMi);

            if (!markaVar) eksikler.Add("Marka seçimi");
            if (!kategoriVar) eksikler.Add("Kategori seçimi");
            if (!subeVar) eksikler.Add("Şube kaydı");
            if (!yetkiBelgesiVar) eksikler.Add("Yetki belgesi yükleme");

            return (true, eksikler.Count == 0, eksikler);
        }

        [HttpGet]
        [Route("")]
        public async Task<IActionResult> Index()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var kurulum = await GetIlkKurulumDurumu(kullanici);
            if (kurulum.zorunluMu && !kurulum.tamamlandiMi)
            {
                TempData["Hata"] = "İlk kurulum tamamlanmadan cihaz devreye alma işlemi yapılamaz.";
                return Redirect("/ys-panel/ilk-kurulum");
            }

            var bugun = DateTime.Now.Date;

            var yetkiBelgesi = await _context.Ys_YetkiBelgeleri
                .Where(x => x.FirmaId == kullanici.FirmaId
                    && !x.SilindiMi
                    && x.Durum == 1
                    && (!x.YetkiBelgesiBaslangicTarihi.HasValue || x.YetkiBelgesiBaslangicTarihi.Value.Date <= bugun)
                    && x.YetkiBelgesiBitisTarihi.Date >= bugun)
                .OrderByDescending(x => x.YetkiBelgesiBitisTarihi)
                .FirstOrDefaultAsync();

            if (yetkiBelgesi == null)
            {
                var onayliYetkiBelgesiVar = await _context.Ys_YetkiBelgeleri
                    .AnyAsync(x => x.FirmaId == kullanici.FirmaId && !x.SilindiMi && x.Durum == 1);

                TempData["Hata"] = onayliYetkiBelgesiVar
                    ? "Geçerli tarih aralığında onaylı yetki belgeniz bulunmuyor."
                    : "Onaylı yetki belgeniz bulunmuyor.";
                return Redirect(onayliYetkiBelgesiVar ? "/ys-yetki-belgesi" : "/ys-panel");
            }

            var markalar = await _context.Ys_FirmaMarkalar
                .Include(x => x.Marka)
                .Where(x => x.FirmaId == kullanici.FirmaId && !x.SilindiMi)
                .Select(x => x.Marka)
                .ToListAsync();

            ViewBag.Markalar = markalar;
            ViewBag.Firma = await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId);
            ViewBag.Kullanici = kullanici;
            await SetBildirimler(kullanici);

            return View("~/Views/DevreyeAlma/Index.cshtml");
        }

        [HttpPost]
        [Route("tesisat-sorgula")]
        public async Task<IActionResult> TesistatSorgula([FromBody] TesistatSorguDto dto)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Json(new { basarili = false, mesaj = "Oturum süresi dolmuş." });

            var kurulum = await GetIlkKurulumDurumu(kullanici);
            if (kurulum.zorunluMu && !kurulum.tamamlandiMi)
                return Json(new { basarili = false, mesaj = "İlk kurulum tamamlanmadan işlem yapılamaz. Lütfen önce şube, marka ve kategori seçimini tamamlayın." });

            if (string.IsNullOrWhiteSpace(dto.TesistatNo))
                return Json(new { basarili = false, mesaj = "Tesisat no boş olamaz." });

            if (string.IsNullOrWhiteSpace(dto.SozlesmeNo))
                return Json(new { basarili = false, mesaj = "Sözleşme no boş olamaz." });

            if (!long.TryParse(dto.TesistatNo.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tesisatNo))
                return Json(new { basarili = false, mesaj = "Tesisat no sayısal olmalıdır." });

            if (!long.TryParse(dto.SozlesmeNo.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sozlesmeNo))
                return Json(new { basarili = false, mesaj = "Sözleşme no sayısal olmalıdır." });

            var firma = await _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId && !x.SilindiMi);

            var firmaKodu = OnlineFirmaKodu(firma);

            var servisSonuc = await _onlineCihazBilgileriClient.YSCihazBilgileriGetirAsync(
                firmaKodu,
                tesisatNo,
                sozlesmeNo,
                HttpContext.RequestAborted);

            if (!servisSonuc.Basarili)
            {
                return Json(new
                {
                    basarili = false,
                    mesaj = servisSonuc.HataMesaji ?? "Cihaz bilgileri alınamadı."
                });
            }

            var ilkCihaz = servisSonuc.Cihazlar.FirstOrDefault();
            var cariKod = servisSonuc.CariKod?.ToString(CultureInfo.InvariantCulture) ?? "";
            return Json(new
            {
                basarili = true,
                tesistatNo = (servisSonuc.TesisatNo ?? tesisatNo).ToString(CultureInfo.InvariantCulture),
                sozlesmeNo = (servisSonuc.SozlesmeNo ?? sozlesmeNo).ToString(CultureInfo.InvariantCulture),
                aboneNo = cariKod,
                sayacNo = servisSonuc.SayacNo?.ToString(CultureInfo.InvariantCulture) ?? "",
                musteriAdi = servisSonuc.CariAd ?? "",
                musteriTcNo = cariKod,
                musteriTelefon = "",
                adres = servisSonuc.Adres ?? "",
                uygunlukBelgeNo = ilkCihaz?.ProjeNo ?? "",
                uygunlukTarihi = "",
                durum = servisSonuc.Cihazlar.Count > 0 ? "Cihaz bilgisi bulundu" : "Tesisat bulundu",
                cihazlar = servisSonuc.Cihazlar.Select(c => new
                {
                    cihazMarka = c.CihazMarka ?? "",
                    cihazTipi = c.CihazTipi ?? "",
                    cihazTipKodu = c.CihazTipKodu ?? "",
                    cihazKapasite = c.CihazKapasite?.ToString(CultureInfo.InvariantCulture) ?? "",
                    projeNo = c.ProjeNo ?? "",
                    tesisatNo = c.TesisatNo?.ToString(CultureInfo.InvariantCulture) ?? ""
                }).ToList()
            });
        }

        [HttpPost]
        [Route("marka-kontrol")]
        public async Task<IActionResult> MarkaKontrol([FromBody] MarkaKontrolDto dto)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Json(new { yetkili = false, mesaj = "Oturum süresi dolmuş." });

            var kurulum = await GetIlkKurulumDurumu(kullanici);
            if (kurulum.zorunluMu && !kurulum.tamamlandiMi)
                return Json(new { yetkili = false, mesaj = "İlk kurulum tamamlanmadan işlem yapılamaz." });

            var yetkiVar = await _context.Ys_FirmaMarkalar
                .AnyAsync(x => x.FirmaId == kullanici.FirmaId
                    && x.MarkaId == dto.MarkaId
                    && !x.SilindiMi);

            if (!yetkiVar)
                return Json(new { yetkili = false, mesaj = "Bu marka için yetkiniz bulunmamaktadır!" });

            var marka = await _context.Ys_Markalar
                .FirstOrDefaultAsync(x => x.Id == dto.MarkaId);

            return Json(new { yetkili = true, markaAdi = marka?.MarkaAdi });
        }

        [HttpGet]
        [Route("detay/{id}")]
        public async Task<IActionResult> Detay(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var islem = await _context.Ys_DevreyeAlmalar
                .Include(x => x.Marka)
                .Include(x => x.Firma)
                .FirstOrDefaultAsync(x => x.Id == id
                    && x.FirmaId == kullanici.FirmaId
                    && !x.SilindiMi);

            if (islem == null) return Redirect("/ys-devreyeal/gecmis");

            ViewBag.Firma = await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId);
            ViewBag.Kullanici = kullanici;
            await SetBildirimler(kullanici);
            return View("~/Views/DevreyeAlma/Detay.cshtml", islem);
        }

        [HttpGet]
        [Route("pdf/{id}")]
        public async Task<IActionResult> PdfIndir(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var islem = await _context.Ys_DevreyeAlmalar
                .Include(x => x.Marka)
                .Include(x => x.Firma)
                    .ThenInclude(x => x!.Sirket)
                .FirstOrDefaultAsync(x => x.Id == id
                    && x.FirmaId == kullanici.FirmaId
                    && !x.SilindiMi);

            if (islem == null) return NotFound();

            var pdf = DevreyeAlmaPdfService.Olustur(islem);
            return File(pdf, "application/pdf",
                $"DevreyeAlma_{islem.TesistatNo ?? id.ToString()}_{id}.pdf");
        }

        [HttpGet]
        [Route("excel/{id}")]
        public async Task<IActionResult> ExcelIndir(int id)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var islem = await _context.Ys_DevreyeAlmalar
                .Include(x => x.Marka)
                .Include(x => x.Firma)
                    .ThenInclude(x => x!.Sirket)
                .FirstOrDefaultAsync(x => x.Id == id
                    && x.FirmaId == kullanici.FirmaId
                    && !x.SilindiMi);

            if (islem == null) return NotFound();

            var bytes = DevreyeAlmaExcelService.Olustur(new[] { islem });
            return File(bytes, "text/csv; charset=windows-1254",
                $"DevreyeAlma_{islem.TesistatNo ?? id.ToString()}_{id}.csv");
        }

        [HttpPost]
        [Route("kaydet")]
        public async Task<IActionResult> Kaydet(Ys_DevreyeAlma model)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var kurulum = await GetIlkKurulumDurumu(kullanici);
            if (kurulum.zorunluMu && !kurulum.tamamlandiMi)
            {
                TempData["Hata"] = "İlk kurulum tamamlanmadan cihaz devreye alma işlemi yapılamaz.";
                return Redirect("/ys-panel/ilk-kurulum");
            }

            var bugun = DateTime.Now.Date;
            var gecerliYetkiBelgesiVar = await _context.Ys_YetkiBelgeleri
                .AnyAsync(x => x.FirmaId == kullanici.FirmaId
                    && !x.SilindiMi
                    && x.Durum == 1
                    && (!x.YetkiBelgesiBaslangicTarihi.HasValue || x.YetkiBelgesiBaslangicTarihi.Value.Date <= bugun)
                    && x.YetkiBelgesiBitisTarihi.Date >= bugun);

            if (!gecerliYetkiBelgesiVar)
            {
                TempData["Hata"] = "Cihaz devreye alma işlemi için geçerli onaylı yetki belgeniz bulunmalıdır.";
                return Redirect("/ys-yetki-belgesi");
            }

            var yetkiVar = await _context.Ys_FirmaMarkalar
                .AnyAsync(x => x.FirmaId == kullanici.FirmaId
                    && x.MarkaId == model.MarkaId
                    && !x.SilindiMi);

            if (!yetkiVar)
            {
                TempData["Hata"] = "Bu marka için yetkiniz bulunmamaktadır!";
                return Redirect("/ys-devreyeal");
            }

            model.FirmaId = kullanici.FirmaId ?? 0;
            model.DevreyeAlmaTarihi = DateTime.Now;
            model.Durum = 1;
            model.OlusturmaTarihi = DateTime.Now;
            model.OlusturanKullanici = kullanici.UserName ?? "";
            model.SilindiMi = false;

            _context.Ys_DevreyeAlmalar.Add(model);
            await _context.SaveChangesAsync();

            TempData["Basarili"] = "Cihaz devreye alma işlemi tamamlandı!";
            return Redirect("/ys-devreyeal/gecmis");
        }

        [HttpGet]
        [Route("gecmis")]
        public async Task<IActionResult> Gecmis(string? marka, DateTime? bas, DateTime? bit, string? musteri, string? durum)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return Redirect("/giris");

            var query = _context.Ys_DevreyeAlmalar
                .Include(x => x.Marka)
                .Where(x => x.FirmaId == kullanici.FirmaId && !x.SilindiMi)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(marka))
                query = query.Where(x => x.Marka != null && x.Marka.MarkaAdi == marka);

            if (bas.HasValue)
            {
                var baslangic = bas.Value.Date;
                query = query.Where(x => x.DevreyeAlmaTarihi >= baslangic);
            }

            if (bit.HasValue)
            {
                var bitis = bit.Value.Date.AddDays(1);
                query = query.Where(x => x.DevreyeAlmaTarihi < bitis);
            }

            if (!string.IsNullOrWhiteSpace(musteri))
            {
                var aranacak = musteri.Trim();
                query = query.Where(x => x.MusteriAdi != null && x.MusteriAdi.Contains(aranacak));
            }

            if (!string.IsNullOrWhiteSpace(durum))
            {
                if (string.Equals(durum, "tamamlandi", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(x => x.Durum == 1);
                else if (string.Equals(durum, "bekliyor", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(x => x.Durum == 0);
            }

            var islemler = await query
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ToListAsync();

            ViewBag.Firma = await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId);
            ViewBag.MarkaList = await _context.Ys_DevreyeAlmalar
                .Include(x => x.Marka)
                .Where(x => x.FirmaId == kullanici.FirmaId && !x.SilindiMi && x.Marka != null && x.Marka.MarkaAdi != null)
                .Select(x => x.Marka!.MarkaAdi!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            ViewBag.SeciliMarka = marka;
            ViewBag.SeciliBas = bas?.ToString("yyyy-MM-dd");
            ViewBag.SeciliBit = bit?.ToString("yyyy-MM-dd");
            ViewBag.SeciliMusteri = musteri;
            ViewBag.SeciliDurum = durum;
            ViewBag.Kullanici = kullanici;
            await SetBildirimler(kullanici);
            return View("~/Views/DevreyeAlma/Gecmis.cshtml", islemler);
        }
    }

    public class TesistatSorguDto
    {
        public string? TesistatNo { get; set; }
        public string? SozlesmeNo { get; set; }
    }

    public class MarkaKontrolDto
    {
        public int MarkaId { get; set; }
    }
}


