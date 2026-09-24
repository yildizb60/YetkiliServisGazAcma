using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.API.Controllers
{
    public partial class AdminPanelApiController
    {
        [HttpPost("yetkili-servisler/liste")]
        public async Task<IActionResult> YetkiliServisler([FromBody] AdminYetkiliServisListeFiltreDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            var sonuc = await _yetkiliServisListeService.ListeleAsync(new AdminYetkiliServisListeFiltre
            {
                SirketId = kapsam.sirketId,
                Q = dto?.Q,
                Il = dto?.Il,
                Durum = dto?.Durum,
                DevreyeSiralama = dto?.DevreyeSiralama
            });

            return Ok(new AdminYetkiliServisListeDto
            {
                Servisler = sonuc.Servisler.Select(MapYetkiliServis).ToList(),
                DevreyeSayilari = sonuc.DevreyeSayilari
            });
        }

        [HttpPost("yetkili-servisler/getir")]
        public async Task<IActionResult> YetkiliServisGetir([FromBody] AdminYetkiliServisGetirFiltreDto? dto)
        {
            if (dto == null || dto.Id <= 0)
                return BadRequest(new { basarili = false, mesaj = "Yetkili servis id zorunludur" });

            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            var sonuc = await _yetkiliServisListeService.GetirAsync(dto.Id, kapsam.sirketId);
            if (sonuc.Servis == null)
                return NotFound(new { basarili = false, mesaj = "Yetkili servis bulunamadi" });

            return Ok(new AdminYetkiliServisDetayDto
            {
                Servis = MapYetkiliServis(sonuc.Servis),
                YetkiBelgeleri = sonuc.YetkiBelgeleri.Select(x => new AdminYetkiliServisYetkiBelgesiDto
                {
                    Id = x.Id,
                    FirmaId = x.FirmaId,
                    Durum = x.Durum,
                    OlusturmaTarihi = x.OlusturmaTarihi,
                    YetkiBelgesiBaslangicTarihi = x.YetkiBelgesiBaslangicTarihi,
                    YetkiBelgesiBitisTarihi = x.YetkiBelgesiBitisTarihi
                }).ToList(),
                Subeler = sonuc.Subeler.Select(x => new AdminYetkiliServisSubeDto
                {
                    Id = x.Id,
                    FirmaId = x.FirmaId,
                    SubeAdi = x.SubeAdi,
                    Il = x.Il,
                    Ilce = x.Ilce,
                    Telefon = x.Telefon
                }).ToList(),
                Devreye = sonuc.Devreye.Select(x => new AdminYetkiliServisDevreyeDto
                {
                    Id = x.Id,
                    FirmaId = x.FirmaId,
                    TesistatNo = x.TesistatNo,
                    Durum = x.Durum,
                    OlusturmaTarihi = x.OlusturmaTarihi,
                    MarkaAdi = x.Marka?.MarkaAdi
                }).ToList()
            });
        }

        private static AdminYetkiliServisDto MapYetkiliServis(Ys_Firma servis)
        {
            return new AdminYetkiliServisDto
            {
                Id = servis.Id,
                FirmaAdi = servis.FirmaAdi,
                YetkiliKisi = servis.YetkiliKisi,
                VergiNo = servis.VergiNo,
                VergiDairesi = servis.VergiDairesi,
                Telefon = servis.Telefon,
                Email = servis.Email,
                Adres = servis.Adres,
                FaaliyetIli = servis.FaaliyetIli,
                AktifMi = servis.AktifMi,
                SirketId = servis.SirketId,
                SirketAdi = servis.Sirket?.SirketAdi,
                Kategoriler = servis.FirmaKategoriler?
                    .Where(x => !x.SilindiMi && x.Kategori != null && !x.Kategori.SilindiMi && x.Kategori.AktifMi)
                    .Select(x => new AdminYetkiliServisKategoriDto
                    {
                        Id = x.Kategori!.Id,
                        Ad = x.Kategori.Ad,
                        IconUrl = x.Kategori.IconUrl
                    })
                    .GroupBy(x => x.Id)
                    .Select(x => x.First())
                    .ToList() ?? new List<AdminYetkiliServisKategoriDto>(),
                Markalar = servis.FirmaMarkalar?
                    .Where(x => !x.SilindiMi && x.Marka != null)
                    .Select(x => new AdminYetkiliServisMarkaDto
                    {
                        Id = x.Marka!.Id,
                        MarkaAdi = x.Marka.MarkaAdi
                    })
                    .GroupBy(x => x.Id)
                    .Select(x => x.First())
                    .ToList() ?? new List<AdminYetkiliServisMarkaDto>()
            };
        }

        [HttpPost("yetkili-servisler/ekle")]
        public async Task<IActionResult> YetkiliServisEkle([FromBody] AdminYetkiliServisKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await _adminYetkiliServisYonetimApiService.EkleAsync(dto, kullanici, kapsam.sirketId));
        }

        [HttpPost("yetkili-servisler/guncelle")]
        public async Task<IActionResult> YetkiliServisGuncelle([FromBody] AdminYetkiliServisKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await _adminYetkiliServisYonetimApiService.GuncelleAsync(dto, kullanici, kapsam.sirketId));
        }

        [HttpPost("yetkili-servisler/sil")]
        public async Task<IActionResult> YetkiliServisSil([FromBody] AdminYetkiliServisDurumDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await _adminYetkiliServisYonetimApiService.SilAsync(dto, kullanici, kapsam.sirketId));
        }

        [HttpPost("yetki-belgeleri/onay-listesi")]
        public async Task<IActionResult> YetkiBelgesiOnayListesi([FromBody] AdminYetkiBelgesiOnayFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await YetkiBelgesiOnaylayabilirMi(kapsam.sirketId))
                return Forbid();

            return Ok(await _adminYetkiBelgesiOnayApiService.ListeleAsync(kapsam.sirketId));
        }

        [HttpPost("yetki-belgeleri/onay-gecmisi")]
        public async Task<IActionResult> YetkiBelgesiOnayGecmisi([FromBody] AdminYetkiBelgesiOnayGecmisiFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await YetkiBelgesiOnaylayabilirMi(kapsam.sirketId))
                return Forbid();

            return Ok(await _adminYetkiBelgesiOnayApiService.GecmisAsync(dto, kapsam.sirketId));
        }

        [HttpPost("subeler/liste")]
        public async Task<IActionResult> Subeler([FromBody] AdminSubeListeFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();
            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await _adminSubeApiService.ListeleAsync(dto, kapsam.sirketId));
        }

        [HttpPost("subeler/getir")]
        public async Task<IActionResult> SubeGetir([FromBody] AdminSubeGetirFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();
            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await _adminSubeApiService.GetirAsync(dto, kapsam.sirketId));
        }

        [HttpPost("subeler/ekle")]
        public async Task<IActionResult> SubeEkle([FromBody] AdminSubeKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await _adminSubeApiService.EkleAsync(dto, kapsam.sirketId, kullanici.UserName ?? "sistem"));
        }

        [HttpPost("subeler/guncelle")]
        public async Task<IActionResult> SubeGuncelle([FromBody] AdminSubeKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await _adminSubeApiService.GuncelleAsync(dto, kapsam.sirketId, kullanici.UserName ?? "sistem"));
        }

        [HttpPost("subeler/durum")]
        public async Task<IActionResult> SubeDurum([FromBody] AdminSubeDurumDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await _adminSubeApiService.DurumDegistirAsync(dto, kapsam.sirketId, kullanici.UserName ?? "sistem"));
        }

        [HttpPost("subeler/sil")]
        public async Task<IActionResult> SubeSil([FromBody] AdminSubeDurumDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await _adminSubeApiService.SilAsync(dto, kapsam.sirketId, kullanici.UserName ?? "sistem"));
        }

        [HttpPost("devreye-almalar/liste")]
        public async Task<IActionResult> DevreyeAlmalar([FromBody] AdminDevreyeAlmaListeFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await RaporGorebilirMi(kapsam.sirketId))
                return Forbid();

            return Ok(await _adminRaporApiService.DevreyeAlmalarAsync(dto, kapsam.sirketId));
        }

        [HttpPost("devreye-almalar/getir")]
        public async Task<IActionResult> DevreyeAlmaGetir([FromBody] AdminDevreyeAlmaGetirFiltreDto? dto)
        {
            if (dto == null || dto.Id <= 0)
                return BadRequest(new { basarili = false, mesaj = "Devreye alma id zorunludur" });

            var kapsam = await KapsamSirketIdAsync(dto.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await RaporGorebilirMi(kapsam.sirketId))
                return Forbid();

            var kayit = await _adminRaporApiService.DevreyeAlmaGetirAsync(dto.Id, kapsam.sirketId);
            if (kayit == null)
                return NotFound(new { basarili = false, mesaj = "Devreye alma kaydi bulunamadi" });

            return Ok(kayit);
        }

        [HttpPost("devreye-almalar/pdf")]
        public async Task<IActionResult> DevreyeAlmaPdf([FromBody] AdminDevreyeAlmaGetirFiltreDto? dto)
        {
            if (dto == null || dto.Id <= 0)
                return BadRequest(new { basarili = false, mesaj = "Devreye alma id zorunludur" });

            var kapsam = await KapsamSirketIdAsync(dto.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await RaporGorebilirMi(kapsam.sirketId))
                return Forbid();

            var dosya = await _devreyeAlmaExportApiService.AdminPdfAsync(dto.Id, kapsam.sirketId);
            if (dosya == null)
                return NotFound(new { basarili = false, mesaj = "Devreye alma kaydi bulunamadi" });

            return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpPost("devreye-almalar/excel")]
        public async Task<IActionResult> DevreyeAlmaExcel([FromBody] AdminDevreyeAlmaGetirFiltreDto? dto)
        {
            if (dto == null || dto.Id <= 0)
                return BadRequest(new { basarili = false, mesaj = "Devreye alma id zorunludur" });

            var kapsam = await KapsamSirketIdAsync(dto.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await RaporGorebilirMi(kapsam.sirketId))
                return Forbid();

            var dosya = await _devreyeAlmaExportApiService.AdminExcelAsync(dto.Id, kapsam.sirketId);
            if (dosya == null)
                return NotFound(new { basarili = false, mesaj = "Devreye alma kaydi bulunamadi" });

            return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpPost("devreye-almalar/rapor/pdf")]
        public async Task<IActionResult> DevreyeAlmaRaporPdf([FromBody] AdminDevreyeAlmaRaporExportFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await RaporGorebilirMi(kapsam.sirketId))
                return Forbid();

            var dosya = await _devreyeAlmaExportApiService.AdminRaporPdfAsync(
                kapsam.sirketId,
                dto?.BaslangicTarihi,
                dto?.BitisTarihi,
                dto?.Ids);

            return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpPost("devreye-almalar/rapor/excel")]
        public async Task<IActionResult> DevreyeAlmaRaporExcel([FromBody] AdminDevreyeAlmaRaporExportFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await RaporGorebilirMi(kapsam.sirketId))
                return Forbid();

            var dosya = await _devreyeAlmaExportApiService.AdminRaporExcelAsync(
                kapsam.sirketId,
                dto?.BaslangicTarihi,
                dto?.BitisTarihi,
                dto?.Ids);

            return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpPost("yetki-belgeleri/uyarilar")]
        public async Task<IActionResult> YetkiBelgesiUyarilari([FromBody] AdminYetkiBelgesiUyariFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await RaporGorebilirMi(kapsam.sirketId) && !await YetkiBelgesiOnaylayabilirMi(kapsam.sirketId))
                return Forbid();

            return Ok(await _adminRaporApiService.YetkiBelgesiUyarilariAsync(kapsam.sirketId));
        }

        [HttpPost("raporlar/ozet")]
        public async Task<IActionResult> RaporlarOzet([FromBody] AdminRaporOzetFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await RaporGorebilirMi(kapsam.sirketId))
                return Forbid();

            return Ok(await _adminRaporApiService.RaporlarOzetAsync(dto, kapsam.sirketId));
        }
    }
}
