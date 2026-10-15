using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/ys-panel")]
    [Authorize(Roles = "YetkiliServis")]
    public class YetkiliServisPanelApiController : ControllerBase
    {
        private readonly YetkiliServisRaporApiService _raporApiService;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly YetkiliServisPanelYonetimApiService _yonetimApiService;
        private readonly DevreyeAlmaExportApiService _devreyeAlmaExportApiService;
        private readonly YetkiliServisPanelOkumaApiService _okuma;
        private readonly YetkiliServisProfilApiService _profil;

        public YetkiliServisPanelApiController(
            UserManager<AppKullanici> userManager, YetkiliServisPanelYonetimApiService yonetimApiService,
            DevreyeAlmaExportApiService devreyeAlmaExportApiService, YetkiliServisRaporApiService raporApiService,
            YetkiliServisPanelOkumaApiService okuma, YetkiliServisProfilApiService profil)
        {
            _userManager = userManager;
            _yonetimApiService = yonetimApiService;
            _devreyeAlmaExportApiService = devreyeAlmaExportApiService;
            _raporApiService = raporApiService;
            _okuma = okuma;
            _profil = profil;
        }

        [HttpPost("dashboard")]
        public async Task<IActionResult> Dashboard([FromBody] YsPanelDashboardFiltreDto? dto = null)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _okuma.DashboardAsync(kullanici.FirmaId.Value, dto));
        }

        [HttpPost("bildirimler")]
        public async Task<IActionResult> Bildirimler()
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _okuma.BildirimlerAsync(kullanici.FirmaId.Value));
        }

        [HttpPost("profil")]
        public async Task<IActionResult> Profil()
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            var firma = await _okuma.ProfilAsync(kullanici.FirmaId.Value);
            return firma == null ? NotFound() : Ok(firma);
        }

        [HttpPost("profil/guncelle")]
        public async Task<IActionResult> ProfilGuncelle([FromBody] YsPanelProfilGuncelleDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _profil.GuncelleAsync(dto, kullanici));
        }

        [HttpPost("ilk-kurulum")]
        public async Task<IActionResult> IlkKurulum()
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _okuma.IlkKurulumAsync(kullanici.FirmaId.Value));
        }

        [HttpPost("markalar")]
        public async Task<IActionResult> Markalar()
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            var sonuc = await _okuma.MarkalarAsync(kullanici.FirmaId.Value);
            return sonuc == null ? NotFound() : Ok(sonuc);
        }

        [HttpPost("raporlar")]
        public async Task<IActionResult> Raporlar([FromBody] YsPanelRaporFiltreDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null) return Unauthorized();
            return Ok(await _raporApiService.GetirAsync(kullanici.FirmaId.Value, dto));
        }

        [HttpPost("raporlar/pdf")]
        public async Task<IActionResult> RaporlarPdf([FromBody] YsPanelRaporFiltreDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            try
            {
                var dosya = await _devreyeAlmaExportApiService.YetkiliServisRaporPdfAsync(
                    kullanici.FirmaId.Value, dto?.Bas, dto?.Bit, dto?.Ids);
                return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
            }
            catch (DevreyeAlmaRaporLimitException ex)
            {
                return BadRequest(new { basarili = false, mesaj = ex.Message });
            }
        }

        [HttpPost("raporlar/excel")]
        public async Task<IActionResult> RaporlarExcel([FromBody] YsPanelRaporFiltreDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            try
            {
                var dosya = await _devreyeAlmaExportApiService.YetkiliServisRaporExcelAsync(
                    kullanici.FirmaId.Value, dto?.Bas, dto?.Bit, dto?.Ids);
                return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
            }
            catch (DevreyeAlmaRaporLimitException ex)
            {
                return BadRequest(new { basarili = false, mesaj = ex.Message });
            }
        }

        [HttpPost("subeler/liste")]
        public async Task<IActionResult> Subeler()
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null) return Unauthorized();
            return Ok(await _yonetimApiService.SubelerAsync(kullanici));
        }

        [HttpPost("subeler/getir")]
        public async Task<IActionResult> SubeGetir([FromBody] SubeGetirDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();
            var sube = await _yonetimApiService.SubeGetirAsync(dto?.Id ?? 0, kullanici);
            return sube == null ? NotFound() : Ok(sube);
        }

        [HttpPost("subeler/kaydet")]
        public async Task<IActionResult> SubeKaydet([FromBody] YsPanelSubeKaydetDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _yonetimApiService.SubeKaydetAsync(dto, kullanici));
        }

        [HttpPost("subeler/durum")]
        public async Task<IActionResult> SubeDurum([FromBody] YsPanelIdDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _yonetimApiService.SubeDurumAsync(dto, kullanici));
        }

        [HttpPost("subeler/sil")]
        public async Task<IActionResult> SubeSil([FromBody] YsPanelIdDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _yonetimApiService.SubeSilAsync(dto, kullanici));
        }

        [HttpPost("markalar/guncelle")]
        public async Task<IActionResult> MarkaGuncelle([FromBody] YsPanelMarkaGuncelleDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _yonetimApiService.MarkaGuncelleAsync(dto, kullanici));
        }

        [HttpPost("markalar/ekle")]
        public async Task<IActionResult> MarkaEkle([FromBody] YsPanelMarkaKaydetDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return StatusCode(StatusCodes.Status403Forbidden,
                await _yonetimApiService.MarkaEkleAsync(dto, kullanici));
        }

        [HttpPost("markalar/duzenle")]
        public async Task<IActionResult> MarkaDuzenle([FromBody] YsPanelMarkaKaydetDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return StatusCode(StatusCodes.Status403Forbidden,
                await _yonetimApiService.MarkaDuzenleAsync(dto, kullanici));
        }

        [HttpPost("markalar/sil")]
        public async Task<IActionResult> MarkaSil([FromBody] YsPanelIdDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _yonetimApiService.MarkaSilAsync(dto, kullanici));
        }

        private async Task<AppKullanici?> AktifYetkiliServisKullaniciAsync()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null || kullanici.KullaniciTipi != KullaniciTipiDegerleri.YetkiliServis)
                return null;

            return kullanici;
        }


    }

}
