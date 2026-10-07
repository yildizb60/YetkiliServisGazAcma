using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/ic-tesisat")]
    [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
    public class IcTesisatApiController : ControllerBase
    {
        private readonly IcTesisatDevreyeAlmaApiService _service;
        private readonly UserManager<AppKullanici> _userManager;

        public IcTesisatApiController(IcTesisatDevreyeAlmaApiService service, UserManager<AppKullanici> userManager)
        {
            _service = service;
            _userManager = userManager;
        }

        [HttpPost("devreye-almalar/liste")]
        public async Task<IActionResult> DevreyeAlmaListesi([FromBody] IcTesisatDevreyeAlmaFiltreDto? dto)
        {
            dto ??= new IcTesisatDevreyeAlmaFiltreDto();

            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadi." });

            var roller = await _userManager.GetRolesAsync(kullanici);
            var sonuc = await _service.ListeleAsync(dto, kullanici, roller);
            return sonuc == null ? Forbid() : Ok(sonuc);
        }
    }

    public class IcTesisatDevreyeAlmaFiltreDto
    {
        public int? SirketId { get; set; }
        public string? TesisatNo { get; set; }
        public string? YetkiliServis { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public string? Marka { get; set; }
        public int Sayfa { get; set; } = 1;
        public int SayfaBoyutu { get; set; } = 100;
    }

    public class IcTesisatDevreyeAlmaListeDto
    {
        public int Toplam { get; set; }
        public int Sayfa { get; set; }
        public int SayfaBoyutu { get; set; }
        public List<IcTesisatDevreyeAlmaDto> Islemler { get; set; } = new();
    }

    public class IcTesisatDevreyeAlmaDto
    {
        public int Id { get; set; }
        public string? TesisatNo { get; set; }
        public string? YetkiliServis { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public DateTime Tarih { get; set; }
        public string? Marka { get; set; }
        public string? CihazTipi { get; set; }
        public string? CihazModeli { get; set; }
        public string? CihazKapasite { get; set; }
        public string? MusteriAdi { get; set; }
        public int Durum { get; set; }
    }
}
