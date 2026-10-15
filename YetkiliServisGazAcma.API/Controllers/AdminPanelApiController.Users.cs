using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.API.Controllers
{
    public partial class AdminPanelApiController
    {
        [HttpPost("kullanicilar/liste")]
        public async Task<IActionResult> Kullanicilar([FromBody] AdminKullaniciListeFiltreDto? dto)
        {
            var yapan = await AktifKullaniciAsync();
            if (yapan == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!PersonelYetkiYonetimKurali.YonetebilirMi(yapan, kapsam.sirketId))
                return Forbid();

            return Ok(await _kullaniciOkuma.ListeleAsync(dto, kapsam.sirketId, GenelSistemAdminMi(yapan)));
        }

        [HttpPost("kullanicilar/sirket-secenekleri")]
        public async Task<IActionResult> KullaniciSirketSecenekleri([FromBody] AdminKullaniciSirketSecenekFiltreDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!PersonelYetkiYonetimKurali.YonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await _kullaniciOkuma.SirketSecenekleriAsync(kapsam.sirketId));
        }

        [HttpPost("kullanicilar/firma-secenekleri")]
        public async Task<IActionResult> KullaniciFirmaSecenekleri([FromBody] AdminKullaniciFirmaSecenekFiltreDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!PersonelYetkiYonetimKurali.YonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await _kullaniciOkuma.FirmaSecenekleriAsync(kapsam.sirketId));
        }

        [HttpPost("kullanicilar/yonetim-yetkisi")]
        public async Task<IActionResult> KullaniciYonetimYetkisi([FromBody] AdminKullaniciYonetimYetkiDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            return Ok(new AdminKullaniciYonetimYetkiSonucDto
            {
                YetkiliMi = PersonelYetkiYonetimKurali.YonetebilirMi(kullanici, kapsam.sirketId)
            });
        }

        [HttpPost("kullanicilar/getir")]
        public async Task<IActionResult> KullaniciGetir([FromBody] AdminKullaniciGetirDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!PersonelYetkiYonetimKurali.YonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return NotFound();

            var hedef = await _context.Users
                .Include(x => x.Sirket)
                .Include(x => x.Firma)
                .FirstOrDefaultAsync(x => x.Id == dto.Id && x.ArsivlemeTarihi == null);

            if (hedef == null)
                return NotFound();

            if (!await KullaniciKapsamindaMi(kullanici, hedef, kapsam.sirketId))
                return Forbid();

            return Ok(MapKullanici(hedef));
        }

        [HttpPost("kullanicilar/guncelle")]
        public async Task<IActionResult> KullaniciGuncelle([FromBody] AdminKullaniciGuncelleDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.KapsamSirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!PersonelYetkiYonetimKurali.YonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            var sonuc = await _kullaniciYonetim.KullaniciGuncelleAsync(dto, kullanici, kapsam.sirketId, GenelSistemAdminMi(kullanici));
            return sonuc.Yetkisiz ? Forbid() : Ok(sonuc.Sonuc);
        }

        [HttpPost("kullanicilar/ekle")]
        public async Task<IActionResult> KullaniciEkle([FromBody] AdminKullaniciKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.KapsamSirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!PersonelYetkiYonetimKurali.YonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            var sonuc = await _kullaniciYonetim.KullaniciEkleAsync(dto, kullanici, kapsam.sirketId, GenelSistemAdminMi(kullanici));
            return sonuc.Yetkisiz ? Forbid() : Ok(sonuc.Sonuc);
        }

        [HttpPost("kullanicilar/durum")]
        public async Task<IActionResult> KullaniciDurum([FromBody] AdminKullaniciDurumDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!PersonelYetkiYonetimKurali.YonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            var sonuc = await _kullaniciYonetim.KullaniciDurumAsync(dto, kullanici, kapsam.sirketId, GenelSistemAdminMi(kullanici));
            return sonuc.Yetkisiz ? Forbid() : Ok(sonuc.Sonuc);
        }

        [HttpPost("kullanicilar/sil")]
        public async Task<IActionResult> KullaniciSil([FromBody] AdminKullaniciSilDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!PersonelYetkiYonetimKurali.YonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            var sonuc = await _kullaniciYonetim.KullaniciSilAsync(dto, kullanici, kapsam.sirketId, GenelSistemAdminMi(kullanici));
            return sonuc.Yetkisiz ? Forbid() : Ok(sonuc.Sonuc);
        }

    }
}
