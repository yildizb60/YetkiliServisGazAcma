using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/ys-devreyeal")]
    [Authorize(Roles = "YetkiliServis")]
    public class YetkiliServisDevreyeAlmaApiController : ControllerBase
    {
        private readonly DevreyeAlmaKayitApiService _kayit;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly DevreyeAlmaExportApiService _devreyeAlmaExportApiService;
        private readonly DevreyeAlmaOkumaApiService _okuma;
        private readonly DevreyeAlmaSorguApiService _sorgu;

        public YetkiliServisDevreyeAlmaApiController(
            UserManager<AppKullanici> userManager, DevreyeAlmaKayitApiService kayit,
            DevreyeAlmaExportApiService export, DevreyeAlmaOkumaApiService okuma, DevreyeAlmaSorguApiService sorgu)
        {
            _userManager = userManager;
            _kayit = kayit;
            _devreyeAlmaExportApiService = export;
            _okuma = okuma;
            _sorgu = sorgu;
        }

        [HttpPost("gecmis")]
        public async Task<IActionResult> Gecmis([FromBody] YsDevreyeAlmaGecmisFiltreDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _okuma.GecmisAsync(kullanici.FirmaId.Value, dto));
        }

        [HttpPost("getir")]
        public async Task<IActionResult> Getir([FromBody] YsDevreyeAlmaGetirDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            if (dto == null || dto.Id <= 0)
                return NotFound();

            var islem = await _okuma.GetirAsync(kullanici.FirmaId.Value, dto.Id);
            return islem == null ? NotFound() : Ok(islem);
        }

        [HttpPost("pdf")]
        public async Task<IActionResult> Pdf([FromBody] YsDevreyeAlmaGetirDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            if (dto == null || dto.Id <= 0)
                return NotFound();

            var dosya = await _devreyeAlmaExportApiService.YetkiliServisPdfAsync(dto.Id, kullanici.FirmaId.Value);
            if (dosya == null)
                return NotFound();

            return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpPost("excel")]
        public async Task<IActionResult> Excel([FromBody] YsDevreyeAlmaGetirDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            if (dto == null || dto.Id <= 0)
                return NotFound();

            var dosya = await _devreyeAlmaExportApiService.YetkiliServisExcelAsync(dto.Id, kullanici.FirmaId.Value);
            if (dosya == null)
                return NotFound();

            return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpPost("ekran")]
        public async Task<IActionResult> Ekran()
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _okuma.EkranAsync(kullanici.FirmaId.Value));
        }

        [HttpPost("tesisat-sorgula")]
        public async Task<IActionResult> TesisatSorgula([FromBody] YsTesisatSorguDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized(new YsTesisatSorguSonucDto { Basarili = false, Mesaj = "Oturum suresi dolmus." });

            return Ok(await _sorgu.SorgulaAsync(dto, kullanici, HttpContext.RequestAborted));
        }

        [HttpPost("bildirimler")]
        public async Task<IActionResult> Bildirimler()
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _okuma.BildirimlerAsync(kullanici.FirmaId.Value));
        }

        [HttpPost("marka-kontrol")]
        public async Task<IActionResult> MarkaKontrol([FromBody] YsMarkaKontrolDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized(new YsMarkaKontrolSonucDto { Yetkili = false, Mesaj = "Oturum suresi dolmus." });

            return Ok(await _sorgu.MarkaKontrolAsync(dto, kullanici));
        }

        [HttpPost("kaydet")]
        public async Task<IActionResult> Kaydet([FromBody] YsDevreyeAlmaKaydetDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized(new YsDevreyeAlmaIslemSonucDto { Basarili = false, Mesaj = "Oturum suresi dolmus.", RedirectUrl = "/giris" });

            return Ok(await _kayit.KaydetAsync(dto, kullanici));
        }

        private async Task<AppKullanici?> AktifYetkiliServisKullaniciAsync()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            return kullanici?.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis ? kullanici : null;
        }

    }

}
