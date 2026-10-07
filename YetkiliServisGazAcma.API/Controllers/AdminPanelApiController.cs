using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/admin-panel")]
    [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
    public partial class AdminPanelApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AdminKullaniciOkumaApiService _kullaniciOkuma;
        private readonly AdminKullaniciYonetimApiService _kullaniciYonetim;
        private readonly AdminDashboardService _dashboardService;
        private readonly AdminYetkiliServisListeService _yetkiliServisListeService;
        private readonly AdminYetkiliServisYonetimApiService _adminYetkiliServisYonetimApiService;
        private readonly AdminSubeApiService _adminSubeApiService;
        private readonly AdminRaporApiService _adminRaporApiService;
        private readonly AdminYetkiBelgesiOnayApiService _adminYetkiBelgesiOnayApiService;
        private readonly AdminPersonelYetkiApiService _adminPersonelYetkiApiService;
        private readonly DevreyeAlmaExportApiService _devreyeAlmaExportApiService;

        public AdminPanelApiController(
            AppDbContext context,
            AdminDashboardService dashboardService,
            AdminYetkiliServisListeService yetkiliServisListeService,
            AdminYetkiliServisYonetimApiService adminYetkiliServisYonetimApiService,
            AdminSubeApiService adminSubeApiService,
            AdminRaporApiService adminRaporApiService,
            AdminYetkiBelgesiOnayApiService adminYetkiBelgesiOnayApiService,
            AdminPersonelYetkiApiService adminPersonelYetkiApiService,
            DevreyeAlmaExportApiService devreyeAlmaExportApiService,
            AdminKullaniciOkumaApiService kullaniciOkuma,
            AdminKullaniciYonetimApiService kullaniciYonetim)
        {
            _context = context;
            _kullaniciOkuma = kullaniciOkuma;
            _kullaniciYonetim = kullaniciYonetim;
            _dashboardService = dashboardService;
            _yetkiliServisListeService = yetkiliServisListeService;
            _adminYetkiliServisYonetimApiService = adminYetkiliServisYonetimApiService;
            _adminSubeApiService = adminSubeApiService;
            _adminRaporApiService = adminRaporApiService;
            _adminYetkiBelgesiOnayApiService = adminYetkiBelgesiOnayApiService;
            _adminPersonelYetkiApiService = adminPersonelYetkiApiService;
            _devreyeAlmaExportApiService = devreyeAlmaExportApiService;
        }

        private async Task<(int? sirketId, bool gecersiz)> KapsamSirketIdAsync(int? istenenSirketId)
        {
            var kullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var kullanici = await _context.Users.FirstOrDefaultAsync(x => x.Id == kullaniciId);
            if (kullanici == null)
                return (null, true);

            var genelSistemAdminMi = User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
                || (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !kullanici.SirketId.HasValue);

            if (genelSistemAdminMi)
                return (istenenSirketId, false);

            var sirketAdminMi = User.IsInRole("SirketAdmin")
                || (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && kullanici.SirketId.HasValue);

            if (sirketAdminMi)
            {
                if (!kullanici.SirketId.HasValue)
                    return (null, true);

                if (istenenSirketId.HasValue && istenenSirketId.Value != kullanici.SirketId.Value)
                    return (null, true);

                return (kullanici.SirketId.Value, false);
            }

            var yetkiQuery = _context.Dag_PersonelYetkiler
                .Where(x => x.KullaniciId == kullanici.Id && !x.SilindiMi);

            if (istenenSirketId.HasValue)
            {
                var yetkiliMi = await yetkiQuery.AnyAsync(x => x.SirketId == istenenSirketId.Value);
                return (istenenSirketId.Value, !yetkiliMi);
            }

            var ilkSirketId = await yetkiQuery
                .OrderBy(x => x.SirketId)
                .Select(x => (int?)x.SirketId)
                .FirstOrDefaultAsync();

            return (ilkSirketId, !ilkSirketId.HasValue);
        }

        private async Task<AppKullanici?> AktifKullaniciAsync()
        {
            var kullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(kullaniciId))
                return null;

            return await _context.Users.FirstOrDefaultAsync(x => x.Id == kullaniciId && x.AktifMi && x.ArsivlemeTarihi == null);
        }

        private bool GenelSistemAdminMi(AppKullanici kullanici)
        {
            return User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
                || (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !kullanici.SirketId.HasValue);
        }

        private static AdminKullaniciListeDto MapKullanici(AppKullanici kullanici)
        {
            return new AdminKullaniciListeDto
            {
                Id = kullanici.Id,
                AdSoyad = kullanici.AdSoyad,
                Email = kullanici.Email,
                PhoneNumber = kullanici.PhoneNumber,
                KullaniciTipi = kullanici.KullaniciTipi,
                AktifMi = kullanici.AktifMi,
                SirketId = kullanici.SirketId,
                SirketAdi = kullanici.Sirket?.SirketAdi,
                FirmaId = kullanici.FirmaId,
                FirmaAdi = kullanici.Firma?.FirmaAdi,
                FirmaYetkiliKisi = kullanici.Firma?.YetkiliKisi,
                FirmaEmail = kullanici.Firma?.Email,
                FirmaTelefon = kullanici.Firma?.Telefon
            };
        }

        private async Task<bool> KullaniciYonetebilirMi(AppKullanici kullanici, int? sirketId)
        {
            if (User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || User.IsInRole("SirketAdmin")
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin)
                return true;

            if (sirketId == null)
                return false;

            return await _context.Dag_PersonelYetkiler.AnyAsync(x =>
                x.KullaniciId == kullanici.Id &&
                !x.SilindiMi &&
                x.SirketId == sirketId.Value &&
                (x.YetkiTipi == YetkiTipleri.TAM_YETKI || x.YetkiTipi == YetkiTipleri.KULLANICI_YONET));
        }

        private Task<bool> RaporGorebilirMi(int? sirketId) => PersonelYetkisiVarMi(sirketId, YetkiTipleri.RAPOR_GOR);

        private Task<bool> YetkiBelgesiOnaylayabilirMi(int? sirketId) => PersonelYetkisiVarMi(sirketId, YetkiTipleri.YETKI_BELGESI_ONAY);

        private async Task<bool> PersonelYetkisiVarMi(int? sirketId, string yetkiTipi)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null || !kullanici.AktifMi)
                return false;

            if (User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || User.IsInRole("SirketAdmin")
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin)
                return true;

            if (!sirketId.HasValue)
                return false;

            return await _context.Dag_PersonelYetkiler.AnyAsync(x =>
                x.KullaniciId == kullanici.Id
                && !x.SilindiMi
                && x.SirketId == sirketId.Value
                && (x.YetkiTipi == YetkiTipleri.TAM_YETKI || x.YetkiTipi == yetkiTipi));
        }

        private async Task<bool> KullaniciKapsamindaMi(AppKullanici yapan, AppKullanici hedef, int? sirketId)
        {
            if (yapan.Id == hedef.Id)
                return true;

            var genelSistemAdminMi = User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || yapan.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin
                || (yapan.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !yapan.SirketId.HasValue);

            if (genelSistemAdminMi && !sirketId.HasValue)
                return true;

            if (!sirketId.HasValue)
                return false;

            if ((hedef.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis ||
                 hedef.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma) &&
                hedef.FirmaId.HasValue)
            {
                return await _context.Ys_Firmalar.AnyAsync(x =>
                    x.Id == hedef.FirmaId.Value &&
                    !x.SilindiMi &&
                    x.SirketId == sirketId.Value);
            }

            return (hedef.KullaniciTipi == KullaniciTipiDegerleri.Personel || hedef.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin) && hedef.SirketId == sirketId.Value;
        }


    }
}
