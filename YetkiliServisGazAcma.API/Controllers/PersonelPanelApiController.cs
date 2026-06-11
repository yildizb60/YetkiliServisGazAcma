using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/personel-panel")]
    [Authorize(Roles = "Personel,GenelSistemAdmin,SirketAdmin,SuperAdmin")]
    public class PersonelPanelApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppKullanici> _userManager;

        public PersonelPanelApiController(AppDbContext context, UserManager<AppKullanici> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost("yetkilerim")]
        public async Task<IActionResult> Yetkilerim([FromBody] PersonelYetkilerimIstek? dto)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null)
                return Unauthorized(new PersonelYetkilerimCevap());

            var roller = await _userManager.GetRolesAsync(kullanici);
            var tamYetkili = roller.Contains("GenelSistemAdmin")
                || roller.Contains("SuperAdmin")
                || roller.Contains("SirketAdmin")
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin;

            if (tamYetkili)
            {
                return Ok(new PersonelYetkilerimCevap
                {
                    Yetkiler = new List<string>
                    {
                        YetkiTipleri.TAM_YETKI,
                        YetkiTipleri.YETKI_BELGESI_ONAY,
                        YetkiTipleri.RAPOR_GOR,
                        YetkiTipleri.KULLANICI_YONET,
                        YetkiTipleri.DAGITIM_SIRKET_YONET,
                        YetkiTipleri.MARKA_YONET
                    }
                });
            }

            var sirketId = dto?.SirketId ?? kullanici.SirketId;
            var query = _context.Dag_PersonelYetkiler
                .Where(x => x.KullaniciId == kullanici.Id && !x.SilindiMi);

            if (sirketId.HasValue)
                query = query.Where(x => x.SirketId == sirketId.Value);

            var yetkiler = await query
                .Select(x => x.YetkiTipi)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToListAsync();

            if (yetkiler.Contains(YetkiTipleri.TAM_YETKI))
                yetkiler = new List<string> { YetkiTipleri.TAM_YETKI };

            return Ok(new PersonelYetkilerimCevap { Yetkiler = yetkiler });
        }
    }

    public class PersonelYetkilerimIstek
    {
        public int? SirketId { get; set; }
    }

    public class PersonelYetkilerimCevap
    {
        public List<string> Yetkiler { get; set; } = new();
    }
}
