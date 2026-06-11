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
    [Route("api/panel-kapsam")]
    [Authorize]
    public class PanelKapsamApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;

        public PanelKapsamApiController(
            AppDbContext context,
            UserManager<AppKullanici> userManager,
            SehirFirmaKoduService sehirFirmaKoduService)
        {
            _context = context;
            _userManager = userManager;
            _sehirFirmaKoduService = sehirFirmaKoduService;
        }

        [HttpPost("sirketler")]
        public async Task<IActionResult> KullaniciSirketleri()
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var sirketler = await KullaniciSirketleriAsync(kullanici);
            return Ok(sirketler.Select(PanelSirketDto.FromEntity).ToList());
        }

        [HttpPost("kimlik")]
        public async Task<IActionResult> PanelKimlik([FromBody] PanelKimlikIstekDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var sonuc = await PanelKimlikAsync(kullanici, dto?.AktifSirketId);
            return Ok(sonuc);
        }

        private async Task<AppKullanici?> AktifKullaniciAsync()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            return kullanici?.AktifMi == true ? kullanici : null;
        }

        private async Task<List<Dag_Sirket>> KullaniciSirketleriAsync(AppKullanici kullanici)
        {
            if (await GenelSistemAdminMi(kullanici))
            {
                return await _context.Dag_Sirketler
                    .Where(x => !x.SilindiMi && x.AktifMi)
                    .OrderBy(x => x.SirketAdi)
                    .ToListAsync();
            }

            var sirketIds = new HashSet<int>();

            if (kullanici.SirketId.HasValue)
                sirketIds.Add(kullanici.SirketId.Value);

            if (kullanici.FirmaId.HasValue)
            {
                var firmaSirketId = await _context.Ys_Firmalar
                    .Where(x => x.Id == kullanici.FirmaId.Value && !x.SilindiMi)
                    .Select(x => x.SirketId)
                    .FirstOrDefaultAsync();

                if (firmaSirketId > 0)
                    sirketIds.Add(firmaSirketId);
            }

            var yetkiSirketleri = await _context.Dag_PersonelYetkiler
                .Where(x => x.KullaniciId == kullanici.Id && !x.SilindiMi)
                .Select(x => x.SirketId)
                .Distinct()
                .ToListAsync();

            foreach (var id in yetkiSirketleri.Where(x => x > 0))
                sirketIds.Add(id);

            if (sirketIds.Count == 0)
                return new List<Dag_Sirket>();

            return await _context.Dag_Sirketler
                .Where(x => sirketIds.Contains(x.Id) && !x.SilindiMi && x.AktifMi)
                .OrderBy(x => x.SirketAdi)
                .ToListAsync();
        }

        private async Task<PanelKimlikDto> PanelKimlikAsync(AppKullanici kullanici, int? aktifSirketId)
        {
            string? sirketAdi = null;
            string? sehir = null;

            if (kullanici.FirmaId.HasValue)
            {
                var firma = await _context.Ys_Firmalar
                    .Include(x => x.Sirket)
                    .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId.Value && !x.SilindiMi);

                sirketAdi = firma?.Sirket?.SirketAdi;
                sehir = firma?.FaaliyetIli ?? firma?.Sirket?.Il;
            }

            if (string.IsNullOrWhiteSpace(sirketAdi) && aktifSirketId.HasValue)
            {
                var sirket = await _context.Dag_Sirketler
                    .FirstOrDefaultAsync(x => x.Id == aktifSirketId.Value && !x.SilindiMi);

                sirketAdi = sirket?.SirketAdi;
                sehir = sirket?.Il;
            }

            if (string.IsNullOrWhiteSpace(sirketAdi) && kullanici.SirketId.HasValue)
            {
                var sirket = await _context.Dag_Sirketler
                    .FirstOrDefaultAsync(x => x.Id == kullanici.SirketId.Value && !x.SilindiMi);

                sirketAdi = sirket?.SirketAdi;
                sehir = sirket?.Il;
            }

            var firmaKodu = _sehirFirmaKoduService.FirmaKodu(sehir);
            if (string.IsNullOrWhiteSpace(sirketAdi))
                sirketAdi = firmaKodu;

            return new PanelKimlikDto
            {
                SirketAdi = sirketAdi,
                Sehir = sehir,
                FirmaKodu = firmaKodu
            };
        }

        private async Task<bool> GenelSistemAdminMi(AppKullanici kullanici)
        {
            if (kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin || (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !kullanici.SirketId.HasValue))
                return true;

            return User.IsInRole(KullaniciRolAdlari.GenelSistemAdmin)
                || User.IsInRole("SuperAdmin")
                || await _userManager.IsInRoleAsync(kullanici, KullaniciRolAdlari.GenelSistemAdmin);
        }
    }

    public class PanelKimlikIstekDto
    {
        public int? AktifSirketId { get; set; }
    }

    public class PanelKimlikDto
    {
        public string? SirketAdi { get; set; }
        public string? Sehir { get; set; }
        public string? FirmaKodu { get; set; }
    }

    public class PanelSirketDto
    {
        public int Id { get; set; }
        public string? SirketAdi { get; set; }
        public string? Il { get; set; }
        public bool AktifMi { get; set; }

        public static PanelSirketDto FromEntity(Dag_Sirket sirket)
        {
            return new PanelSirketDto
            {
                Id = sirket.Id,
                SirketAdi = sirket.SirketAdi,
                Il = sirket.Il,
                AktifMi = sirket.AktifMi
            };
        }
    }
}
