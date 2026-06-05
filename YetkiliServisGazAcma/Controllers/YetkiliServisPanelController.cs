using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace YetkiliServisGazAcma.Controllers
{
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("ys-panel")]
    public class YetkiliServisPanelController : Controller
    {
        private readonly UserManager<AppKullanici> _userManager;
        private readonly YetkiliServisPanelApiClient _yetkiliServisPanelApiClient;

        public YetkiliServisPanelController(
            UserManager<AppKullanici> userManager,
            YetkiliServisPanelApiClient yetkiliServisPanelApiClient)
        {
            _userManager = userManager;
            _yetkiliServisPanelApiClient = yetkiliServisPanelApiClient;
        }

        private async Task SetBildirimler(AppKullanici kullanici)
        {
            try
            {
                var sonuc = await _yetkiliServisPanelApiClient.BildirimlerAsync(kullanici)
                    ?? new YsPanelBildirimSonuc();

                ViewBag.Bildirimler = sonuc.Bildirimler;
                ViewBag.BildirimSayisi = sonuc.BildirimSayisi;
            }
            catch (ApiIntegrationException)
            {
                ViewBag.Bildirimler = new List<string>();
                ViewBag.BildirimSayisi = 0;
            }
        }

        private async Task<AppKullanici?> GetYetkiliServisKullanici()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return null;
            if (kullanici.KullaniciTipi != 1) return null;
            return kullanici;
        }



        [HttpGet]
        [Route("")]
        [Route("index")]
        public async Task<IActionResult> Index()
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var dashboard = await _yetkiliServisPanelApiClient.DashboardAsync(kullanici)
                ?? new YsPanelDashboardSonuc();

            ViewBag.Firma = dashboard.Firma;
            ViewBag.BuAy = dashboard.BuAy;
            ViewBag.Toplam = dashboard.Toplam;
            ViewBag.SonIslemler = dashboard.SonIslemler;
            ViewBag.Kullanici = kullanici;
            ViewBag.IlkKurulumZorunlu = dashboard.IlkKurulumZorunlu;
            ViewBag.IlkKurulumTamamlandi = dashboard.IlkKurulumTamamlandi;
            ViewBag.IlkKurulumEksikler = dashboard.IlkKurulumEksikler;
            ViewBag.Bildirimler = dashboard.Bildirimler;
            ViewBag.BildirimSayisi = dashboard.BildirimSayisi;
            ViewBag.YetkiBelgesiUyariGun = dashboard.YetkiBelgesiUyariGun;

            return View("~/Views/YetkiliServisPanel/Index.cshtml");
        }

        [HttpGet]
        [Route("ilk-kurulum")]
        public async Task<IActionResult> IlkKurulum()
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var kurulum = await _yetkiliServisPanelApiClient.IlkKurulumAsync(kullanici);
            if (kurulum == null)
            {
                TempData["Hata"] = "Ilk kurulum bilgileri API uzerinden alinamadi.";
                return Redirect("/ys-panel");
            }

            if (!string.IsNullOrWhiteSpace(kurulum.HataMesaji))
            {
                TempData["Hata"] = kurulum.HataMesaji;
                return Redirect("/ys-panel");
            }

            if (!kurulum.ZorunluMu) return Redirect("/ys-panel");
            if (kurulum.TamamlandiMi)
            {
                TempData["Basarili"] = "Ilk kurulum zaten tamamlanmis.";
                return Redirect("/ys-panel");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.Firma = kurulum.Firma;
            ViewBag.TumMarkalar = kurulum.TumMarkalar;
            ViewBag.TumKategoriler = kurulum.TumKategoriler;
            ViewBag.SeciliMarkaIds = kurulum.SeciliMarkaIds;
            ViewBag.SeciliKategoriIds = kurulum.SeciliKategoriIds;
            ViewBag.AktifSubeSayisi = kurulum.AktifSubeSayisi;
            ViewBag.YetkiBelgesiVar = kurulum.YetkiBelgesiVar;
            ViewBag.OnayliYetkiBelgesiVar = kurulum.OnayliYetkiBelgesiVar;
            ViewBag.IlkKurulumEksikler = kurulum.Eksikler;
            await SetBildirimler(kullanici);
            return View("~/Views/YetkiliServisPanel/IlkKurulum.cshtml");
        }

        [HttpGet]
        [Route("subeler")]
        public async Task<IActionResult> Subeler()
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var firma = await _yetkiliServisPanelApiClient.ProfilAsync(kullanici);
            if (firma == null)
            {
                TempData["Hata"] = "Sube bilgileri API uzerinden alinamadi.";
                return Redirect("/ys-panel");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.Firma = firma;
            ViewBag.Subeler = firma.Subeler?
                .Where(x => !x.SilindiMi)
                .OrderByDescending(x => x.AktifMi)
                .ThenBy(x => x.SubeAdi)
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

            var firma = await _yetkiliServisPanelApiClient.ProfilAsync(kullanici);
            var sube = firma?.Subeler?.FirstOrDefault(x => x.Id == id && !x.SilindiMi);
            if (sube == null)
            {
                TempData["Hata"] = "Sube kaydi bulunamadi.";
                return Redirect("/ys-panel/subeler");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.Firma = firma;
            ViewBag.Sube = sube;
            await SetBildirimler(kullanici);

            return View("~/Views/YetkiliServisPanel/SubeDuzenle.cshtml");
        }

        [HttpGet]
        [Route("markalar")]
        public async Task<IActionResult> Markalar()
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.MarkalarAsync(kullanici);
            if (sonuc == null)
            {
                TempData["Hata"] = "Marka bilgileri API uzerinden alinamadi.";
                return Redirect("/ys-panel");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.Firma = sonuc.Firma;
            ViewBag.TumMarkalar = sonuc.TumMarkalar;
            ViewBag.FirmaMarkalar = sonuc.FirmaMarkalar;
            ViewBag.SeciliMarkaIds = sonuc.SeciliMarkaIds;
            await SetBildirimler(kullanici);

            return View("~/Views/YetkiliServisPanel/Markalar.cshtml");
        }

        [HttpGet]
        [Route("profil")]
        public async Task<IActionResult> Profil()
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var firma = await _yetkiliServisPanelApiClient.ProfilAsync(kullanici);
            if (firma == null)
            {
                TempData["Hata"] = "Profil bilgileri API uzerinden alinamadi.";
                return Redirect("/ys-panel");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.Firma = firma;
            await SetBildirimler(kullanici);

            return View("~/Views/YetkiliServisPanel/Profil.cshtml");
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

            var sonuc = await _yetkiliServisPanelApiClient.SubeKaydetAsync(
                kullanici,
                id,
                subeAdi,
                il,
                ilce,
                telefon,
                adres,
                aktifMi);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Sube guncellendi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Sube API uzerinden guncellenemedi.";

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

            var sonuc = await _yetkiliServisPanelApiClient.SubeKaydetAsync(
                kullanici,
                0,
                subeAdi,
                il,
                ilce,
                telefon,
                adres,
                aktifMi);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Sube kaydi eklendi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Sube API uzerinden eklenemedi.";

            return Redirect("/ys-panel/subeler");
        }
        [HttpPost]
        [Route("subeler/durum")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeDurum(int id)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.SubeDurumAsync(kullanici, id);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Sube durumu guncellendi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Sube durumu API uzerinden guncellenemedi.";

            return Redirect("/ys-panel/subeler");
        }
        [HttpPost]
        [Route("subeler/sil")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubeSil(int id)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.SubeSilAsync(kullanici, id);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Sube kaydi silindi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Sube API uzerinden silinemedi.";

            return Redirect("/ys-panel/subeler");
        }
        [HttpPost]
        [Route("marka-guncelle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaGuncelle(List<int> markaIds)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.MarkaGuncelleAsync(kullanici, markaIds ?? new List<int>());

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Marka yetkileri guncellendi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Marka yetkileri API uzerinden guncellenemedi.";

            return Redirect("/ys-panel/markalar");
        }

        [HttpPost]
        [Route("marka-ekle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaEkle(string? markaAdi, string? aciklama)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.MarkaEkleAsync(kullanici, markaAdi, aciklama);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Marka eklendi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Marka API uzerinden eklenemedi.";

            return Redirect("/ys-panel/markalar");
        }

        [HttpPost]
        [Route("marka-duzenle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaDuzenle(int id, string? markaAdi, string? aciklama)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.MarkaDuzenleAsync(kullanici, id, markaAdi, aciklama);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Marka guncellendi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Marka API uzerinden guncellenemedi.";

            return Redirect("/ys-panel/markalar");
        }

        [HttpPost]
        [Route("marka-sil")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkaSil(int id)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.MarkaSilAsync(kullanici, id);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Marka yetkisi kaldirildi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Marka yetkisi API uzerinden kaldirilamadi.";

            return Redirect("/ys-panel/markalar");
        }

        [HttpPost]
        [Route("profil-guncelle")]
        public async Task<IActionResult> ProfilGuncelle(
            string? adSoyad, string? telefon, string? email)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _yetkiliServisPanelApiClient.ProfilGuncelleAsync(kullanici, adSoyad, telefon, email);

            if (sonuc?.Basarili == true)
                TempData["Basarili"] = sonuc.Mesaj ?? "Profil bilgileri guncellendi.";
            else
                TempData["Hata"] = sonuc?.Mesaj ?? "Guncelleme sirasinda hata olustu.";

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
                TempData["SifreHata"] = "Yeni ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€¦Ã‚Â¸ifreler eÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€¦Ã‚Â¸leÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€¦Ã‚Â¸miyor.";
                return Redirect("/ys-panel/profil");
            }

            if (yeniSifre.Length < 6)
            {
                TempData["SifreHata"] = "ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Âifre en az 6 karakter olmalÃƒÆ’Ã¢â‚¬ÂÃƒâ€šÃ‚Â±dÃƒÆ’Ã¢â‚¬ÂÃƒâ€šÃ‚Â±r.";
                return Redirect("/ys-panel/profil");
            }

            var sonuc = await _userManager.ChangePasswordAsync(
                kullanici, mevcutSifre, yeniSifre);

            if (sonuc.Succeeded)
                TempData["SifreBasarili"] = "ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Âifreniz baÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€¦Ã‚Â¸arÃƒÆ’Ã¢â‚¬ÂÃƒâ€šÃ‚Â±yla deÃƒÆ’Ã¢â‚¬ÂÃƒâ€¦Ã‚Â¸iÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€¦Ã‚Â¸tirildi.";
            else
                TempData["SifreHata"] = "Mevcut ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€¦Ã‚Â¸ifre hatalÃƒÆ’Ã¢â‚¬ÂÃƒâ€šÃ‚Â±.";

            return Redirect("/ys-panel/profil");
        }

        [HttpGet]
        [Route("raporlar")]
        public async Task<IActionResult> Raporlar(DateTime? bas, DateTime? bit)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var rapor = await _yetkiliServisPanelApiClient.RaporlarAsync(kullanici, bas, bit, limit: 10);
            if (rapor == null)
            {
                TempData["Hata"] = "Rapor bilgileri API uzerinden alinamadi.";
                return Redirect("/ys-panel");
            }

            ViewBag.BasTarih = rapor.BasTarih;
            ViewBag.BitTarih = rapor.BitTarih;
            ViewBag.DevreyeSayisi = rapor.DevreyeSayisi;
            ViewBag.Tamamlanan = rapor.Tamamlanan;
            ViewBag.Bekleyen = rapor.Bekleyen;
            ViewBag.YetkiBelgesiOnayli = rapor.YetkiBelgesiOnayli;
            ViewBag.YetkiBelgesiBekleyen = rapor.YetkiBelgesiBekleyen;
            ViewBag.YetkiBelgesiReddedilen = rapor.YetkiBelgesiReddedilen;
            ViewBag.SonIslemler = rapor.SonIslemler;
            ViewBag.ChartAylikLabels = rapor.ChartAylikLabels;
            ViewBag.ChartAylikData = rapor.ChartAylikData;
            ViewBag.ChartDurumData = rapor.ChartDurumData;
            ViewBag.ChartMarkaLabels = rapor.ChartMarkaLabels;
            ViewBag.ChartMarkaData = rapor.ChartMarkaData;
            ViewBag.Firma = rapor.Firma;
            ViewBag.Kullanici = kullanici;
            await SetBildirimler(kullanici);
            return View("~/Views/YetkiliServisPanel/Raporlar.cshtml");
        }

        [HttpGet]
        [Route("raporlar/pdf")]
        public async Task<IActionResult> RaporlarPdf(DateTime? bas, DateTime? bit, List<int>? ids)
        {
            var kullanici = await GetYetkiliServisKullanici();
            if (kullanici == null) return Redirect("/giris");

            var rapor = await _yetkiliServisPanelApiClient.RaporlarAsync(
                kullanici,
                bas,
                bit,
                ids,
                ids != null && ids.Count > 0 ? null : 10);
            if (rapor == null)
            {
                TempData["Hata"] = "PDF raporu icin API yaniti alinamadi.";
                return Redirect("/ys-panel/raporlar");
            }

            var sonIslemler = rapor.SonIslemler;
            var basTarih = rapor.BasTarih;
            var bitTarih = rapor.BitTarih;
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
                            col.Item().Text("Yetkili Servis Raporlari").FontSize(16).SemiBold();
                            col.Item().Text($"Rapor Araligi: {basTarih:dd.MM.yyyy} - {bitTarih:dd.MM.yyyy}")
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

                            Cell("Toplam Islem", devreyeSayisi.ToString());
                            Cell("Tamamlanan", tamamlanan.ToString());
                            Cell("Bekleyen", bekleyen.ToString());
                        });

                        col.Item().Text("Son Islemler").FontSize(12).SemiBold();

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
                                header.Cell().Background("#F3F4F6").Padding(6).Text("Musteri").SemiBold().FontSize(10);
                                header.Cell().Background("#F3F4F6").Padding(6).Text("Marka").SemiBold().FontSize(10);
                                header.Cell().Background("#F3F4F6").Padding(6).Text("Tarih").SemiBold().FontSize(10);
                            });

                            foreach (var d in sonIslemler)
                            {
                                table.Cell().Padding(6).Text(d.TesistatNo ?? "-").FontSize(10);
                                table.Cell().Padding(6).Text(d.MusteriAdi ?? "-").FontSize(10);
                                table.Cell().Padding(6).Text(d.Marka?.MarkaAdi ?? d.CihazMarka ?? "-").FontSize(10);
                                table.Cell().Padding(6).Text(d.DevreyeAlmaTarihi.ToString("dd.MM.yyyy")).FontSize(10);
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text("Yetkili Servis Gaz Acma Sistemi").FontSize(9).FontColor("#888888");
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

            var rapor = await _yetkiliServisPanelApiClient.RaporlarAsync(kullanici, bas, bit, ids);
            if (rapor == null)
            {
                TempData["Hata"] = "Excel raporu icin API yaniti alinamadi.";
                return Redirect("/ys-panel/raporlar");
            }

            var bytes = DevreyeAlmaExcelService.Olustur(rapor.SonIslemler);
            var dosyaAdi = $"raporlar_{rapor.BasTarih:yyyyMMdd}_{rapor.BitTarih:yyyyMMdd}.csv";
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


