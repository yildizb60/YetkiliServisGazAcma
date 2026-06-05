using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Linq;
using System.Text;

namespace YetkiliServisGazAcma.Controllers
{
    public partial class AdminPanelController
    {
        [HttpGet("devreyealmalar")]
        public async Task<IActionResult> DevreyeAlmalar(string? marka, string? servis, string? il, string? durum, DateTime? bas, DateTime? bit)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminRaporApiClient.DevreyeAlmalarAsync(kullanici, aktifSirketId, marka, servis, il, durum, bas, bit);
            if (sonuc == null)
            {
                TempData["Hata"] = "Devreye alma listesi API uzerinden alinamadi.";
                sonuc = new AdminDevreyeAlmaListeSonuc();
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Markalar = sonuc.Markalar;
            ViewBag.Sehirler = _sehirFirmaKoduService.Sehirler();
            ViewBag.SeciliIl = il ?? "";
            ViewBag.FirmaIlceleri = sonuc.FirmaIlceleri;
            return View("~/Views/AdminPanel/DevreyeAlmalar.cshtml", sonuc.Islemler);
        }

        [HttpGet("devreyealmalar/detay/{id:int}")]
        public async Task<IActionResult> DevreyeAlmaDetay(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var kayit = await _adminRaporApiClient.DevreyeAlmaDetayAsync(kullanici, id, aktifSirketId);
            if (kayit == null)
            {
                TempData["Hata"] = "Devreye alma detayi API uzerinden alinamadi.";
                return Redirect("/AdminPanel/devreyealmalar");
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            return View("~/Views/AdminPanel/DevreyeAlmaDetay.cshtml", kayit);
        }

        [HttpGet("devreyealmalar/pdf/{id:int}")]
        public async Task<IActionResult> DevreyeAlmaPdf(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var dosya = await _adminRaporApiClient.DevreyeAlmaPdfAsync(kullanici, id, aktifSirketId);
            if (dosya == null) return NotFound();

            return File(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpGet("devreyealmalar/excel/{id:int}")]
        public async Task<IActionResult> DevreyeAlmaExcel(int id)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var dosya = await _adminRaporApiClient.DevreyeAlmaExcelAsync(kullanici, id, aktifSirketId);
            if (dosya == null) return NotFound();

            return File(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        private async Task<Ys_DevreyeAlma?> AdminDevreyeAlmaKaydiBul(AppKullanici kullanici, int id, int? aktifSirketId)
        {
            return await _adminRaporApiClient.DevreyeAlmaDetayAsync(kullanici, id, aktifSirketId);
        }

        private async Task<List<Ys_DevreyeAlma>> AdminDevreyeAlmaKayitlariBul(
            AppKullanici kullanici,
            int? aktifSirketId,
            DateTime basTarih,
            DateTime bitTarih,
            List<int>? ids,
            int? take = null)
        {
            if (ids != null && ids.Count > 0)
            {
                var kayitlar = new List<Ys_DevreyeAlma>();
                foreach (var id in ids.Distinct())
                {
                    var kayit = await AdminDevreyeAlmaKaydiBul(kullanici, id, aktifSirketId);
                    if (kayit != null)
                        kayitlar.Add(kayit);
                }

                return kayitlar
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .ToList();
            }

            var sonuc = await _adminRaporApiClient.DevreyeAlmalarAsync(
                kullanici,
                aktifSirketId,
                marka: null,
                servis: null,
                il: null,
                durum: null,
                bas: basTarih,
                bit: bitTarih);

            var islemler = sonuc?.Islemler ?? new List<Ys_DevreyeAlma>();
            if (take.HasValue)
                islemler = islemler.Take(take.Value).ToList();

            return islemler;
        }

        [HttpGet("raporlar")]
        public async Task<IActionResult> Raporlar(DateTime? bas, DateTime? bit, string? tip, int? sirketId)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var sonuc = await _adminRaporApiClient.RaporlarOzetAsync(kullanici, sirketId, bas, bit, tip);
            if (sonuc == null)
            {
                TempData["Hata"] = "Rapor ozeti API uzerinden alinamadi.";
                sonuc = new AdminRaporOzetSonuc
                {
                    BasTarih = bas?.Date ?? DateTime.Now.Date.AddDays(-30),
                    BitTarih = bit?.Date ?? DateTime.Now.Date,
                    RaporTipi = string.IsNullOrWhiteSpace(tip) ? "devreye" : tip.Trim().ToLowerInvariant(),
                    ListeTipi = (tip == "onayli" || tip == "bekleyen" || tip == "reddedilen") ? "yetkiBelgesi" : "devreye"
                };
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.BasTarih = sonuc.BasTarih;
            ViewBag.BitTarih = sonuc.BitTarih;
            ViewBag.DevreyeSayisi = sonuc.DevreyeSayisi;
            ViewBag.YetkiBelgesiOnayli = sonuc.YetkiBelgesiOnayli;
            ViewBag.YetkiBelgesiBekleyen = sonuc.YetkiBelgesiBekleyen;
            ViewBag.YetkiBelgesiReddedilen = sonuc.YetkiBelgesiReddedilen;
            ViewBag.RaporTipi = sonuc.RaporTipi;
            ViewBag.ListeTipi = sonuc.ListeTipi;
            ViewBag.SonIslemler = sonuc.SonIslemler;
            ViewBag.YetkiBelgesiIslemler = sonuc.YetkiBelgesiIslemler;
            ViewBag.SeciliSirketId = sirketId;
            ViewBag.Sirketler = sonuc.Sirketler;
            ViewBag.ChartAylikLabels = sonuc.ChartAylikLabels;
            ViewBag.ChartAylikData = sonuc.ChartAylikData;
            ViewBag.ChartDurumData = sonuc.ChartDurumData;
            ViewBag.ChartSirketLabels = sonuc.ChartSirketLabels;
            ViewBag.ChartSirketData = sonuc.ChartSirketData;
            return View("~/Views/AdminPanel/Raporlar.cshtml");
        }

        [HttpGet("raporlar/pdf")]
        public async Task<IActionResult> RaporlarPdf(DateTime? bas, DateTime? bit, List<int>? ids)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var basTarih = bas?.Date ?? DateTime.Now.Date.AddDays(-30);
            var bitTarih = bit?.Date ?? DateTime.Now.Date;
            var sonIslemler = await AdminDevreyeAlmaKayitlariBul(kullanici, aktifSirketId, basTarih, bitTarih, ids, take: 20);

            if (ids != null && ids.Count > 0 && sonIslemler.Count > 0)
            {
                basTarih = sonIslemler.Min(x => x.OlusturmaTarihi).Date;
                bitTarih = sonIslemler.Max(x => x.OlusturmaTarihi).Date;
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
                            col.Item().Text("Yönetim Raporları").FontSize(16).SemiBold();
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

                        col.Item().Text("Seçili Devreye Alma Detayları").FontSize(12).SemiBold();

                        foreach (var d in sonIslemler)
                        {
                            var durumText = d.Durum == 1 ? "Tamamlandı" : d.Durum == 2 ? "İptal" : "Bekliyor";
                            var durumColor = d.Durum == 1 ? "#0f766e" : d.Durum == 2 ? "#b42318" : "#9a6700";
                            var satirBg = d.Durum == 1 ? "#ecfdf3" : d.Durum == 2 ? "#fff1f2" : "#fffbeb";
                            col.Item().PaddingBottom(8).Border(1).BorderColor("#E5E7EB").Background("#FFFFFF").Column(detail =>
                            {
                                detail.Item().Background("#F8FAFC").Padding(8).Row(r =>
                                {
                                    r.RelativeItem().Text($"Tesisat No: {d.TesistatNo ?? "-"}").FontSize(10).SemiBold();
                                    r.RelativeItem().AlignRight().Text($"Tarih: {d.OlusturmaTarihi:dd.MM.yyyy HH:mm}").FontSize(10).FontColor("#4B5563");
                                });
                                detail.Item().Background(satirBg).PaddingHorizontal(8).PaddingVertical(6).Text($"Durum: {durumText}").FontSize(10).FontColor(durumColor).SemiBold();
                                detail.Item().Padding(8).Table(t =>
                                {
                                    t.ColumnsDefinition(c =>
                                    {
                                        c.RelativeColumn();
                                        c.RelativeColumn();
                                    });

                                    void Bilgi(string etiket, string deger)
                                    {
                                        t.Cell().PaddingBottom(4).Text($"{etiket}: {deger}").FontSize(10);
                                    }

                                    Bilgi("Firma Kodu", d.Firma?.Sirket?.SirketAdi ?? "-");
                                    Bilgi("Yetkili Servis", d.Firma?.FirmaAdi ?? "-");
                                    Bilgi("Müşteri", d.MusteriAdi ?? "-");
                                    Bilgi("Telefon", d.MusteriTelefon ?? "-");
                                    Bilgi("TC", d.MusteriTcNo ?? "-");
                                    Bilgi("Adres", d.Adres ?? "-");
                                    Bilgi("Cihaz Tipi", d.CihazTipi ?? "-");
                                    Bilgi("Marka", d.Marka?.MarkaAdi ?? d.CihazMarka ?? "-");
                                    Bilgi("Model", d.CihazModeli ?? "-");
                                    Bilgi("Seri No", d.SeriNo ?? "-");
                                    Bilgi("Kapasite", d.CihazKapasite ?? "-");
                                    Bilgi("Teknisyen", d.TeknisyenAdi ?? "-");
                                    Bilgi("Teknisyen Yetki Belgesi No", d.TeknisyenYetkiBelgesiNo ?? "-");
                                });

                                if (!string.IsNullOrWhiteSpace(d.Notlar))
                                    detail.Item().PaddingHorizontal(8).PaddingBottom(8).Text($"Not: {d.Notlar}").FontSize(10).FontColor("#4B5563");
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text("Yetkili Servis Gaz Açma Sistemi").FontSize(9).FontColor("#888888");
                });
            });

            var pdfBytes = document.GeneratePdf();
            var dosyaAdi = $"raporlar_{basTarih:yyyyMMdd}_{bitTarih:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", dosyaAdi);
        }

        [HttpGet("raporlar/pdf-toplu")]
        public async Task<IActionResult> RaporlarPdfToplu()
        {
            var bit = DateTime.Now.Date;
            var bas = bit.AddDays(-30);
            return await RaporlarPdf(bas, bit, null);
        }

        [HttpGet("raporlar/excel")]
        public async Task<IActionResult> RaporlarExcel(DateTime? bas, DateTime? bit, List<int>? ids)
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var basTarih = bas?.Date ?? DateTime.Now.Date.AddDays(-30);
            var bitTarih = bit?.Date ?? DateTime.Now.Date;
            var islemler = await AdminDevreyeAlmaKayitlariBul(kullanici, aktifSirketId, basTarih, bitTarih, ids);

            if (ids != null && ids.Count > 0 && islemler.Count > 0)
            {
                basTarih = islemler.Min(x => x.OlusturmaTarihi).Date;
                bitTarih = islemler.Max(x => x.OlusturmaTarihi).Date;
            }

            var bytes = DevreyeAlmaExcelService.Olustur(islemler);
            var dosyaAdi = $"raporlar_{basTarih:yyyyMMdd}_{bitTarih:yyyyMMdd}.csv";
            return File(bytes, "text/csv; charset=windows-1254", dosyaAdi);
        }

        [HttpGet("raporlar/excel-toplu")]
        public async Task<IActionResult> RaporlarExcelToplu()
        {
            var bit = DateTime.Now.Date;
            var bas = bit.AddDays(-30);
            return await RaporlarExcel(bas, bit, null);
        }

        [HttpGet("onay-bekleyenler")]
        public async Task<IActionResult> OnayBekleyenler()
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var onayListesi = await _adminYetkiBelgesiOnayApiClient.ListeleAsync(kullanici, aktifSirketId);
            ViewBag.AdminYetkiBelgesiOnayVeriKaynagi = "API";

            if (onayListesi == null)
            {
                TempData["Hata"] = "Yetki belgesi onay listesi API üzerinden alınamadı.";
                onayListesi = new AdminYetkiBelgesiOnaySonuc();
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Bekleyenler = onayListesi.Bekleyenler;
            ViewBag.Onaylananlar = onayListesi.Onaylananlar;
            ViewBag.Reddedilenler = onayListesi.Reddedilenler;
            return View("~/Views/AdminPanel/OnayBekleyenler.cshtml");
        }

        [HttpGet("yetki-belgesi-uyarilari")]
        public async Task<IActionResult> YetkiBelgesiUyarilari()
        {
            var kullanici = await GetCurrentUser();
            if (kullanici == null) return Redirect("/giris");

            var aktifSirketId = await _aktifSirketService.AktifSirketIdAsync(kullanici);
            var sonuc = await _adminRaporApiClient.YetkiBelgesiUyarilariAsync(kullanici, aktifSirketId);
            if (sonuc == null)
            {
                TempData["Hata"] = "Yetki belgesi uyarilari API uzerinden alinamadi.";
                sonuc = new AdminYetkiBelgesiUyariSonuc();
            }

            ViewBag.Kullanici = kullanici;
            ViewBag.OnayBekleyen = await GetOnayBekleyenCount();
            ViewBag.Yaklasan = sonuc.Yaklasan;
            ViewBag.Gecmis = sonuc.Gecmis;
            return View("~/Views/AdminPanel/YetkiBelgesiUyarilari.cshtml");
        }
    }
}
