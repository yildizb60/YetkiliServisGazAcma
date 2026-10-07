using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/ykc")]
    [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel,SertifikaliFirma")]
    public class YkcApiController : ControllerBase
    {
        private readonly YkcTalepService _ykcTalepService;
        private readonly YkcTalepOkumaService _okuma;
        private readonly YkcPlanlamaOkumaService _planlamaOkuma;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly AppDbContext _context;
        private readonly YkcTesisatApiService _tesisat;
        private readonly YkcBelgeYuklemeApiService _belgeYukleme;
        private readonly YkcImzaAkisService _ykcImzaAkisService;
        private readonly YkcYetkiService _ykcYetkiService;
        private readonly IConfiguration? _configuration;

        public YkcApiController(
            YkcTalepService ykcTalepService,
            YkcTalepOkumaService okuma,
            UserManager<AppKullanici> userManager,
            IWebHostEnvironment environment,
            AppDbContext context,
            YkcImzaAkisService ykcImzaAkisService,
            YkcYetkiService ykcYetkiService,
            YkcTesisatApiService tesisat,
            YkcPlanlamaOkumaService planlamaOkuma,
            YkcBelgeYuklemeApiService belgeYukleme,
            IConfiguration? configuration = null)
        {
            _ykcTalepService = ykcTalepService;
            _okuma = okuma;
            _planlamaOkuma = planlamaOkuma;
            _userManager = userManager;
            _environment = environment;
            _context = context;
            _tesisat = tesisat;
            _ykcImzaAkisService = ykcImzaAkisService;
            _ykcYetkiService = ykcYetkiService;
            _configuration = configuration;
            _belgeYukleme = belgeYukleme;
        }

        [HttpPost("tesisat-sorgula")]
        public async Task<IActionResult> TesisatSorgula([FromBody] YkcTesisatSorguIstek? istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            var ykcYetkileri = await _ykcYetkiService.OzetAsync(kullanici, kullanici.SirketId, HttpContext.RequestAborted);
            if (!ykcYetkileri.TalepOlusturabilir)
                return YkcYetkisiz("Tesisat sorgulama ve talep oluşturma yetkiniz bulunmuyor.");

            return Ok(await _tesisat.SorgulaAsync(istek, kullanici, HttpContext.RequestAborted));
        }

        [HttpPost("cihaz-karsilastir")]
        public async Task<IActionResult> CihazKarsilastir([FromBody] YkcCihazKarsilastirmaIstek? istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null) return Unauthorized();
            var yetkiler = await _ykcYetkiService.OzetAsync(kullanici, kullanici.SirketId, HttpContext.RequestAborted);
            if (!yetkiler.TalepOlusturabilir)
                return YkcYetkisiz("Cihaz karşılaştırma yetkiniz bulunmuyor.");
            if (istek == null) return BadRequest();

            return Ok(await _tesisat.KarsilastirAsync(istek, kullanici, HttpContext.RequestAborted));
        }

        [HttpPost("talepler/liste")]
        public async Task<IActionResult> TaleplerListe([FromBody] YkcTalepListeFiltre? filtre)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            filtre ??= new YkcTalepListeFiltre();
            if (!await OkumaSirketineYetkiliMiAsync(kullanici, filtre.SirketId, YetkiTipleri.YKC_TALEP_GOR))
                return YkcYetkisiz("YKC taleplerini görüntüleme yetkiniz bulunmuyor.");

            var sonuc = await _okuma.ListeAsync(
                filtre,
                kullanici,
                await GenelYetkiliMiAsync(kullanici),
                filtre.SirketId);

            return Ok(sonuc);
        }

        [HttpPost("dashboard/ozet")]
        public async Task<IActionResult> DashboardOzet([FromBody] PanelKimlikIstekDto? filtre)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (!await OkumaSirketineYetkiliMiAsync(kullanici, filtre?.AktifSirketId))
                return YkcYetkisiz("Bu şirketin cihaz değişim özetini görüntüleme yetkiniz bulunmuyor.");

            var sonuc = await _okuma.DashboardOzetAsync(
                kullanici,
                await GenelYetkiliMiAsync(kullanici), filtre?.AktifSirketId);

            return Ok(sonuc);
        }

        [HttpPost("talepler/rapor")]
        public async Task<IActionResult> TaleplerRapor([FromBody] YkcTalepListeFiltre? filtre)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            filtre ??= new YkcTalepListeFiltre();
            if (!await OkumaSirketineYetkiliMiAsync(kullanici, filtre.SirketId, YetkiTipleri.YKC_RAPOR_GOR))
                return YkcYetkisiz("YKC raporlarını görüntüleme yetkiniz bulunmuyor.");

            var sonuc = await _okuma.RaporAsync(
                filtre,
                kullanici,
                await GenelYetkiliMiAsync(kullanici),
                filtre.SirketId);

            return Ok(sonuc);
        }

        [HttpPost("talepler/rapor/pdf")]
        public async Task<IActionResult> TaleplerRaporPdf([FromBody] YkcTalepListeFiltre? filtre)
        {
            return await RaporDosyasi(filtre, excelMi: false);
        }

        [HttpPost("talepler/rapor/excel")]
        public async Task<IActionResult> TaleplerRaporExcel([FromBody] YkcTalepListeFiltre? filtre)
        {
            return await RaporDosyasi(filtre, excelMi: true);
        }

        private async Task<IActionResult> RaporDosyasi(YkcTalepListeFiltre? filtre, bool excelMi)
        {
            const int disAktarimLimiti = 5000;
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            filtre ??= new YkcTalepListeFiltre();
            if (!await OkumaSirketineYetkiliMiAsync(kullanici, filtre.SirketId, YetkiTipleri.YKC_RAPOR_GOR))
                return YkcYetkisiz("YKC raporlarını dışa aktarma yetkiniz bulunmuyor.");

            var kayitlar = await _okuma.RaporKayitlariAsync(
                filtre,
                kullanici,
                await GenelYetkiliMiAsync(kullanici),
                disAktarimLimiti + 1,
                filtre.SirketId);

            if (kayitlar.Count > disAktarimLimiti)
            {
                return BadRequest(new
                {
                    basarili = false,
                    mesaj = $"Tek dosyada en fazla {disAktarimLimiti} kayıt dışa aktarılabilir. Filtreleri daraltın."
                });
            }

            if (kayitlar.Count == 0)
                return BadRequest(new { basarili = false, mesaj = "Filtrelere uygun rapor kaydı bulunamadı." });

            var icOperasyon = kullanici.KullaniciTipi != KullaniciTipiDegerleri.SertifikaliFirma;
            var zaman = DateTime.Now.ToString("yyyyMMdd_HHmm");
            if (excelMi)
            {
                return this.HassasDosya(
                    YkcRaporExcelService.Olustur(kayitlar, icOperasyon),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Cihaz_Degisim_Raporu_{zaman}.xlsx");
            }

            return this.HassasDosya(
                YkcRaporPdfService.Olustur(kayitlar, icOperasyon),
                "application/pdf",
                $"Cihaz_Degisim_Raporu_{zaman}.pdf");
        }

        [HttpPost("imza/entegrasyon")]
        public IActionResult ImzaEntegrasyonBilgisi()
        {
            return Ok(_ykcImzaAkisService.EntegrasyonBilgisi());
        }

        [HttpPost("talepler/imzaya-gonder")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> ImzayaGonder([FromBody] YkcTalepGetirIstek? istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (istek == null || istek.Id <= 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("İmza gönderimi için talep id zorunludur."));

            var sirketId = await TalepSirketIdAsync(istek.Id);
            if (!await OkumaSirketineYetkiliMiAsync(kullanici, sirketId, YetkiTipleri.YKC_FR265_IMZA_ISLEM))
                return YkcYetkisiz("FR265 ve dijital imza işlemi yetkiniz bulunmuyor.");

            if (!_ykcImzaAkisService.EntegrasyonBilgisi().KullanilabilirMi)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    YkcIslemSonuc.HataliSonuc("Dijital imza sağlayıcısı henüz yapılandırılmadı; belge gönderilmedi."));
            }

            var sonuc = await _ykcImzaAkisService.ImzayaGonderAsync(
                istek.Id,
                kullanici,
                await GenelYetkiliMiAsync(kullanici),
                HttpContext.RequestAborted,
                sirketId);

            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        [HttpPost("talepler/imza-durum-sorgula")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> ImzaDurumSorgula([FromBody] YkcTalepGetirIstek? istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (istek == null || istek.Id <= 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("İmza durumu için talep id zorunludur."));

            var sirketId = await TalepSirketIdAsync(istek.Id);
            if (!await OkumaSirketineYetkiliMiAsync(kullanici, sirketId, YetkiTipleri.YKC_FR265_IMZA_ISLEM))
                return YkcYetkisiz("FR265 ve dijital imza işlemi yetkiniz bulunmuyor.");

            if (!_ykcImzaAkisService.EntegrasyonBilgisi().KullanilabilirMi)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    YkcIslemSonuc.HataliSonuc("Dijital imza sağlayıcısı henüz yapılandırılmadı."));
            }

            var sonuc = await _ykcImzaAkisService.ImzaDurumunuSorgulaAsync(
                istek.Id,
                kullanici,
                await GenelYetkiliMiAsync(kullanici),
                HttpContext.RequestAborted,
                sirketId);

            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        [HttpPost("talepler/dosya-indir")]
        public async Task<IActionResult> DosyaIndir([FromBody] YkcDosyaGetirIstek? istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (istek == null || istek.Id <= 0)
                return BadRequest(new { basarili = false, mesaj = "Dosya id zorunludur." });

            var dosya = await _context.Ykc_FormDosyalari
                .Include(x => x.Talep)
                    .ThenInclude(x => x!.ImzaSurecleri)
                .FirstOrDefaultAsync(x => x.Id == istek.Id && !x.SilindiMi && x.Talep != null && !x.Talep.SilindiMi);

            if (dosya?.Talep == null)
                return NotFound(new { basarili = false, mesaj = "Cihaz değişim form dosyası bulunamadı." });

            if (!await OkumaSirketineYetkiliMiAsync(kullanici, dosya.Talep.SirketId))
                return YkcYetkisiz("YKC belge görüntüleme yetkiniz bulunmuyor.");

            if (!await TalepDosyasinaYetkiliMiAsync(dosya.Talep, kullanici))
                return Forbid();

            if (!YkcDosyasiIndirmeyeAcikMi(dosya))
                return Forbid();

            if (string.IsNullOrWhiteSpace(dosya.DosyaYolu))
                return NotFound(new { basarili = false, mesaj = "Dosya yolu bulunamadı." });

            var fizikselYol = ResolveYkcBelgeYolu(dosya);
            if (string.IsNullOrWhiteSpace(fizikselYol))
                return NotFound(new { basarili = false, mesaj = "Dosya yolu gecersiz." });

            var kokYol = Path.GetFullPath(BelgeKokYolu(dosya, fizikselYol));

            if (!fizikselYol.StartsWith(kokYol + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            if (!System.IO.File.Exists(fizikselYol))
                return NotFound(new { basarili = false, mesaj = "Dosya fiziksel olarak bulunamadı." });

            var bytes = await System.IO.File.ReadAllBytesAsync(fizikselYol, HttpContext.RequestAborted);
            var contentType = string.IsNullOrWhiteSpace(dosya.IcerikTipi)
                ? "application/octet-stream"
                : dosya.IcerikTipi.Trim();
            var dosyaAdi = string.IsNullOrWhiteSpace(dosya.DosyaAdi)
                ? Path.GetFileName(fizikselYol)
                : dosya.DosyaAdi.Trim();

            return this.HassasDosya(bytes, contentType, dosyaAdi);
        }

        [HttpPost("dogalgaz-mobile/talepler/liste")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> DogalgazMobileTaleplerListe([FromBody] YkcTalepListeFiltre? filtre)
        {
            filtre ??= new YkcTalepListeFiltre();
            filtre.HedefUygulama = YkcHedefUygulamaDegerleri.DogalgazMobileApp;
            return await TaleplerListe(filtre);
        }

        [HttpPost("crm187/talepler/liste")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> Crm187TaleplerListe([FromBody] YkcTalepListeFiltre? filtre)
        {
            filtre ??= new YkcTalepListeFiltre();
            filtre.HedefUygulama = YkcHedefUygulamaDegerleri.Crm187;
            return await TaleplerListe(filtre);
        }

        [HttpPost("talepler/getir")]
        [HttpPost("talepler/form-verisi")]
        public async Task<IActionResult> TalepGetir([FromBody] YkcTalepGetirIstek? istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (istek == null || istek.Id <= 0)
                return BadRequest(new { basarili = false, mesaj = "Talep id zorunludur." });

            var sirketId = await TalepSirketIdAsync(istek.Id);
            if (!await OkumaSirketineYetkiliMiAsync(kullanici, sirketId))
                return YkcYetkisiz("YKC talep detayını görüntüleme yetkiniz bulunmuyor.");

            var sonuc = await _okuma.GetirAsync(
                istek.Id, kullanici, await GenelYetkiliMiAsync(kullanici), sirketId);
            if (sonuc == null)
                return NotFound(new { basarili = false, mesaj = "Cihaz değişim talebi bulunamadı." });

            var firma = User.IsInRole("SertifikaliFirma")
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma;
            var formVerisi = Request.Path.Value?.EndsWith("/form-verisi", StringComparison.Ordinal) == true;
            if (firma)
            {
                YkcFirmaSunumu.Hazirla(sonuc, formVerisi);
            }
            if (!formVerisi)
            {
                var yetkiler = await _ykcYetkiService.OzetAsync(kullanici, sirketId, HttpContext.RequestAborted);
                var ekipler = !firma && yetkiler.AtamaYapabilir
                    ? await _planlamaOkuma.EkiplerAsync(istek.Id, kullanici, await GenelYetkiliMiAsync(kullanici), sirketId)
                    : new List<YkcEkipSecenegi>();
                sonuc.Ekran = YkcTalepIslemKurali.EkranHazirla(sonuc, yetkiler, !firma,
                    _ykcImzaAkisService.EntegrasyonBilgisi(), ekipler, DateTime.Now);
            }
            return Ok(sonuc);
        }

        [HttpPost("talepler/form-pdf")]
        [Produces("application/pdf")]
        public async Task<IActionResult> FormPdf([FromBody] YkcTalepGetirIstek? istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null) return Unauthorized();
            if (istek == null || istek.Id <= 0) return BadRequest();
            var sirketId = await TalepSirketIdAsync(istek.Id);
            if (!await OkumaSirketineYetkiliMiAsync(kullanici, sirketId))
                return YkcYetkisiz("Form görüntüleme yetkiniz bulunmuyor.");
            var detay = await _okuma.GetirAsync(
                istek.Id, kullanici, await GenelYetkiliMiAsync(kullanici), sirketId);
            if (detay == null) return NotFound();

            // A signed document is immutable: preview the stored bytes, never regenerate it.
            if (detay.ImzaSureci?.Durum == YkcImzaDurumDegerleri.Tamamlandi
                && detay.ImzaSureci.NihaiDosyaId is int dosyaId)
                return await DosyaIndir(new YkcDosyaGetirIstek { Id = dosyaId });

            var pdf = YkcFr265PdfService.Olustur(detay);
            return this.HassasDosya(pdf.Bytes, pdf.ContentType, pdf.DosyaAdi);
        }

        [HttpPost("takvim")]
        [ProducesResponseType(typeof(YkcTakvimSonuc), StatusCodes.Status200OK)]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel,SertifikaliFirma")]
        public async Task<IActionResult> Takvim([FromBody] YkcTakvimFiltre? filtre)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null) return Unauthorized();
            if (!await OkumaSirketineYetkiliMiAsync(kullanici, filtre?.AktifSirketId))
                return YkcYetkisiz("Randevu takvimini görüntüleme yetkiniz bulunmuyor.");
            return Ok(await _planlamaOkuma.TakvimAsync(filtre ?? new(), kullanici, await GenelYetkiliMiAsync(kullanici)));
        }

        [HttpPost("talepler/ekipler")]
        [ProducesResponseType(typeof(List<YkcEkipSecenegi>), StatusCodes.Status200OK)]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> Ekipler([FromBody] YkcTalepGetirIstek istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null) return Unauthorized();
            var sirketId = await TalepSirketIdAsync(istek.Id);
            if (!await OkumaSirketineYetkiliMiAsync(kullanici, sirketId, YetkiTipleri.YKC_ATAMA_YAP))
                return YkcYetkisiz("Atama yetkiniz bulunmuyor.");
            return Ok(await _planlamaOkuma.EkiplerAsync(istek.Id, kullanici, await GenelYetkiliMiAsync(kullanici), sirketId));
        }

        [HttpPost("talepler/olustur")]
        public async Task<IActionResult> TalepOlustur([FromBody] YkcTalepKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (dto == null)
                return BadRequest(YkcIslemSonuc.HataliSonuc("Talep bilgileri zorunludur."));

            var ykcYetkileri = await _ykcYetkiService.OzetAsync(kullanici, kullanici.SirketId, HttpContext.RequestAborted);
            if (!ykcYetkileri.TalepOlusturabilir)
                return YkcYetkisiz("YKC talebi oluşturma yetkiniz bulunmuyor.");

            var sonuc = await _ykcTalepService.OlusturAsync(dto, kullanici);
            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        [HttpPost("talepler/atama-yap")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> AtamaYap([FromBody] YkcAtamaKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (dto == null || dto.TalepId <= 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("Atama için talep id zorunludur."));

            var sirketId = await TalepSirketIdAsync(dto.TalepId);
            if (!await OkumaSirketineYetkiliMiAsync(kullanici, sirketId, YetkiTipleri.YKC_ATAMA_YAP))
                return YkcYetkisiz("YKC atama ve randevu işlemi yetkiniz bulunmuyor.");

            var sonuc = await _ykcTalepService.AtamaYapAsync(dto, kullanici, await GenelYetkiliMiAsync(kullanici), sirketId);
            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        [HttpPost("talepler/durum-guncelle")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> DurumGuncelle([FromBody] YkcDurumGuncelleDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (dto == null || dto.TalepId <= 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("Durum güncelleme için talep id zorunludur."));

            var sirketId = await TalepSirketIdAsync(dto.TalepId);
            if (!await DurumGuncellemeYetkiliMiAsync(kullanici, dto.Durum, sirketId))
                return YkcYetkisiz("Bu YKC durum işlemi için yetkiniz bulunmuyor.");

            var sonuc = await _ykcTalepService.DurumGuncelleAsync(dto, kullanici, await GenelYetkiliMiAsync(kullanici), sirketId);
            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        [HttpPost("talepler/kontroller-kaydet")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> KontrollerKaydet([FromBody] YkcKontrolKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (dto == null || dto.TalepId <= 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("Kontrol kaydı için talep id zorunludur."));

            var sirketId = await TalepSirketIdAsync(dto.TalepId);
            if (!await OkumaSirketineYetkiliMiAsync(kullanici, sirketId, YetkiTipleri.YKC_FR265_IMZA_ISLEM))
                return YkcYetkisiz("FR265 kontrol işlemi yetkiniz bulunmuyor.");

            var sonuc = await _ykcTalepService.KontrolleriKaydetAsync(dto, kullanici, await GenelYetkiliMiAsync(kullanici), sirketId);
            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        [HttpPost("talepler/dosya-kaydet")]
        public async Task<IActionResult> DosyaKaydet([FromBody] YkcDosyaKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (dto == null || dto.TalepId <= 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("Dosya kaydı için talep id zorunludur."));

            var sirketId = await TalepSirketIdAsync(dto.TalepId);
            if (!await OkumaSirketineYetkiliMiAsync(kullanici, sirketId, YetkiTipleri.YKC_FR265_IMZA_ISLEM))
                return YkcYetkisiz("YKC teknik belge işlemi yetkiniz bulunmuyor.");

            var sonuc = await _belgeYukleme.KaydetAsync(dto, kullanici, await GenelYetkiliMiAsync(kullanici), sirketId);
            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        [HttpPost("talepler/form-yukle")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> FormYukle([FromForm] YkcFormYukleIstek istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (istek.TalepId <= 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("Form yükleme için talep id zorunludur."));

            var sirketId = await TalepSirketIdAsync(istek.TalepId);
            if (!await OkumaSirketineYetkiliMiAsync(kullanici, sirketId, YetkiTipleri.YKC_FR265_IMZA_ISLEM))
                return YkcYetkisiz("YKC teknik belge işlemi yetkiniz bulunmuyor.");

            var sonuc = await _belgeYukleme.YukleAsync(istek.TalepId, istek.DosyaTuru, istek.Dosya,
                kullanici, await GenelYetkiliMiAsync(kullanici), sirketId);
            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        private async Task<bool> OkumaSirketineYetkiliMiAsync(
            AppKullanici kullanici, int? sirketId, string yetkiTipi = YetkiTipleri.YKC_TALEP_GOR)
        {
            if ((User.IsInRole("SertifikaliFirma")
                    || kullanici.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma)
                && !kullanici.FirmaId.HasValue)
                return false;

            if (sirketId.HasValue)
            {
                if (!await _context.Dag_Sirketler.AnyAsync(x => x.Id == sirketId.Value && x.AktifMi && !x.SilindiMi))
                    return false;
                if (!await GenelYetkiliMiAsync(kullanici))
                {
                    if (kullanici.FirmaId.HasValue)
                    {
                        if (!await _context.Ys_Firmalar.AnyAsync(x => x.Id == kullanici.FirmaId.Value
                            && x.SirketId == sirketId.Value && !x.SilindiMi)) return false;
                    }
                    else if (User.IsInRole("SirketAdmin") || kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin)
                    {
                        if (kullanici.SirketId != sirketId) return false;
                    }
                    else if (kullanici.SirketId != sirketId && !await _context.Dag_PersonelYetkiler.AnyAsync(x =>
                        x.KullaniciId == kullanici.Id && x.SirketId == sirketId.Value && !x.SilindiMi))
                        return false;
                }
            }
            return await _ykcYetkiService.YetkiliMiAsync(kullanici, yetkiTipi,
                sirketId ?? kullanici.SirketId, HttpContext.RequestAborted);
        }

        private Task<int?> TalepSirketIdAsync(int talepId)
        {
            return _context.Ykc_Talepler.AsNoTracking()
                .Where(x => x.Id == talepId && !x.SilindiMi)
                .Select(x => x.SirketId)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);
        }

        private async Task<bool> DurumGuncellemeYetkiliMiAsync(AppKullanici kullanici, int yeniDurum, int? sirketId)
        {
            var atamaYetkili = await OkumaSirketineYetkiliMiAsync(kullanici, sirketId, YetkiTipleri.YKC_ATAMA_YAP);
            if (yeniDurum is not (YkcDurumDegerleri.SahaIsleminde or YkcDurumDegerleri.Tamamlandi))
                return atamaYetkili;

            var imzaYetkili = await OkumaSirketineYetkiliMiAsync(kullanici, sirketId, YetkiTipleri.YKC_FR265_IMZA_ISLEM);
            return yeniDurum switch
            {
                YkcDurumDegerleri.SahaIsleminde => atamaYetkili || imzaYetkili,
                _ => imzaYetkili
            };
        }

        private ObjectResult YkcYetkisiz(string mesaj)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                YkcIslemSonuc.HataliSonuc(mesaj));
        }

        private Task<AppKullanici?> AktifKullaniciAsync()
        {
            return _userManager.GetUserAsync(User);
        }

        private async Task<bool> GenelYetkiliMiAsync(AppKullanici kullanici)
        {
            var roller = await _userManager.GetRolesAsync(kullanici);
            return roller.Contains("GenelSistemAdmin") || roller.Contains("SuperAdmin");
        }

        private async Task<bool> TalepDosyasinaYetkiliMiAsync(Ykc_Talep talep, AppKullanici kullanici)
        {
            if (User.IsInRole("SertifikaliFirma")
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma)
                return YkcYetkiService.FirmaDosyasinaErisimVarMi(kullanici, talep);

            if (kullanici.FirmaId.HasValue)
                return YkcYetkiService.FirmaDosyasinaErisimVarMi(kullanici, talep);

            if (await GenelYetkiliMiAsync(kullanici))
                return true;

            if (kullanici.SirketId.HasValue && talep.SirketId == kullanici.SirketId.Value)
                return true;

            if (User.IsInRole("Personel") && talep.SirketId.HasValue)
                return await OkumaSirketineYetkiliMiAsync(kullanici, talep.SirketId);

            return false;
        }

        private string WebRootPath()
        {
            return string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                : _environment.WebRootPath;
        }

        private string PrivateYkcBelgeRoot()
        {
            return PrivateDocumentStorage.Root(_environment, _configuration, "ykc-belgeler");
        }

        private string BelgeKokYolu(Ykc_FormDosya dosya, string fizikselYol)
        {
            var yol = dosya.DosyaYolu?.Trim().Replace('\\', '/').TrimStart('/') ?? "";
            if (yol.StartsWith("uploads/ykc/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(dosya.DepolamaTuru, YkcDepolamaTuruDegerleri.LegacyWwwroot, StringComparison.OrdinalIgnoreCase))
            {
                return WebRootPath();
            }

            var legacyRoot = PrivateDocumentStorage.LegacyRoot(_environment, "ykc-belgeler");
            return PrivateDocumentStorage.IsInRoot(fizikselYol, legacyRoot)
                ? legacyRoot
                : PrivateYkcBelgeRoot();
        }

        private string? ResolveYkcBelgeYolu(Ykc_FormDosya dosya)
        {
            var yol = dosya.DosyaYolu?.Trim().Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(yol))
                return null;

            if (yol.StartsWith("uploads/ykc/", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(Path.Combine(
                    WebRootPath(),
                    yol.Replace('/', Path.DirectorySeparatorChar)));
            }

            if (yol.StartsWith("ykc/", StringComparison.OrdinalIgnoreCase))
                yol = yol["ykc/".Length..];

            var relative = yol.Replace('/', Path.DirectorySeparatorChar);
            return PrivateDocumentStorage.ExistingFile(_environment, _configuration, "ykc-belgeler", relative)
                ?? Path.GetFullPath(Path.Combine(PrivateYkcBelgeRoot(), relative));
        }

        private static bool YkcDosyasiIndirmeyeAcikMi(Ykc_FormDosya dosya)
        {
            if (dosya.DosyaTuru == YkcFormDosyaTuruDegerleri.TeknikEk)
                return true;

            if (dosya.DosyaTuru != YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai)
                return false;

            return dosya.Talep?.ImzaSurecleri.Any(s =>
                !s.SilindiMi
                && s.Durum == YkcImzaDurumDegerleri.Tamamlandi
                && !string.IsNullOrWhiteSpace(s.ProviderDocumentId)
                && s.NihaiDosyaId == dosya.Id) == true;
        }


    }

    public class YkcFormYukleIstek
    {
        public int TalepId { get; set; }
        public string? DosyaTuru { get; set; }
        public IFormFile? Dosya { get; set; }
    }
}
