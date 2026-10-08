using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/yetkili-servisler")]
    public class YetkiliServislerController : ControllerBase
    {
        private readonly YetkiliServisRehberApiService _rehber;
        private readonly YetkiliServisBasvuruApiService _basvuru;
        private readonly YetkiliServisKayitYonetimApiService _yonetim;

        public YetkiliServislerController(
            YetkiliServisRehberApiService rehber,
            YetkiliServisBasvuruApiService basvuru,
            YetkiliServisKayitYonetimApiService yonetim)
        {
            _rehber = rehber;
            _basvuru = basvuru;
            _yonetim = yonetim;
        }

        [HttpPost("liste")]
        [AllowAnonymous]
        [EnableRateLimiting("PublicApi")]
        public async Task<IActionResult> Liste([FromBody] YetkiliServisFiltreDto? dto)
            => Ok(await _rehber.ListeleAsync(dto));

        [HttpGet]
        [AllowAnonymous]
        [EnableRateLimiting("PublicApi")]
        public async Task<IActionResult> ListeGet([FromQuery] YetkiliServisFiltreDto? dto)
            => Ok(await _rehber.ListeleAsync(dto));

        [HttpPost("filtre-secenekleri")]
        [AllowAnonymous]
        [EnableRateLimiting("PublicApi")]
        public async Task<IActionResult> FiltreSecenekleri([FromBody] YetkiliServisFiltreSecenekleriIstek? dto)
            => Ok(await _rehber.FiltreSecenekleriAsync(dto?.Il));

        [HttpPost("rehber-ekrani")]
        [AllowAnonymous]
        [EnableRateLimiting("PublicApi")]
        [ProducesResponseType(typeof(YetkiliServisRehberEkranDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> RehberEkrani([FromBody] YetkiliServisFiltreDto? dto)
            => Ok(await _rehber.EkranAsync(dto));

        [HttpPost("basvuru-secenekleri")]
        [AllowAnonymous]
        [EnableRateLimiting("PublicApi")]
        [ProducesResponseType(typeof(YetkiliServisBasvuruSecenekleriDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> BasvuruSecenekleri()
            => Ok(await _basvuru.SeceneklerAsync());

        [HttpPost("kayit")]
        [AllowAnonymous]
        [EnableRateLimiting("PublicApi")]
        [ProducesResponseType(typeof(YetkiliServisKayitSonuc), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(YetkiliServisKayitSonuc), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Kayit([FromBody] YetkiliServisBasvuruDto? dto)
        {
            var sonuc = await _basvuru.KayitAsync(dto);
            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        [HttpPost("getir")]
        [Authorize]
        public async Task<IActionResult> Getir([FromBody] IdDto dto)
            => YonetimSonucu(await _yonetim.GetirAsync(User, dto.Id));

        [HttpPost("guncelle")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin")]
        public async Task<IActionResult> Guncelle([FromBody] YetkiliServisKaydetDto dto)
            => YonetimSonucu(await _yonetim.GuncelleAsync(User, dto));

        [HttpPost("sil")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin")]
        public async Task<IActionResult> Sil([FromBody] IdDto dto)
            => YonetimSonucu(await _yonetim.SilAsync(User, dto.Id));

        private IActionResult YonetimSonucu(YetkiliServisKayitYonetimSonuc sonuc)
            => sonuc.Durum switch
            {
                YetkiliServisKayitYonetimDurumu.OturumGecersiz => Unauthorized(),
                YetkiliServisKayitYonetimDurumu.Bulunamadi =>
                    NotFound(new { basarili = false, mesaj = sonuc.Mesaj }),
                YetkiliServisKayitYonetimDurumu.GecersizIstek =>
                    BadRequest(new { basarili = false, mesaj = sonuc.Mesaj }),
                YetkiliServisKayitYonetimDurumu.Basarili when sonuc.Servis != null => Ok(sonuc.Servis),
                YetkiliServisKayitYonetimDurumu.Basarili =>
                    Ok(new { basarili = true, mesaj = sonuc.Mesaj }),
                _ => throw new InvalidOperationException("Bilinmeyen yetkili servis islem sonucu.")
            };

    }

    public class YetkiliServisDetayDto : YetkiliServisDto
    {
        public string? VergiNo { get; set; }
        public string? VergiDairesi { get; set; }
        public bool AktifMi { get; set; }
        public List<int> MarkaIds { get; set; } = new();
        public List<int> KategoriIds { get; set; } = new();
    }

    public class YetkiliServisKaydetDto
    {
        public int Id { get; set; }
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? FaaliyetIli { get; set; }
        public string? VergiNo { get; set; }
        public string? VergiDairesi { get; set; }
        public bool AktifMi { get; set; } = true;
        public List<int>? MarkaIds { get; set; }
        public List<int>? KategoriIds { get; set; }
    }

}
