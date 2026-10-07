using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.API.Services;
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
                Servisler = sonuc.Servisler.Select(AdminYetkiliServisDto.FromEntity).ToList(),
                DevreyeSayilari = sonuc.DevreyeSayilari,
                Sehirler = sonuc.Sehirler,
                Ilceler = sonuc.Ilceler
            });
        }

        [HttpPost("yetkili-servisler/editor")]
        public async Task<IActionResult> YetkiliServisEditor([FromBody] AdminYetkiliServisGetirFiltreDto? dto)
        {
            if (dto == null || dto.Id < 0) return BadRequest();
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null) return Unauthorized();
            var kapsam = await KapsamSirketIdAsync(dto.SirketId);
            if (kapsam.gecersiz || !await KullaniciYonetebilirMi(kullanici, kapsam.sirketId)) return Forbid();
            var editor = await _adminYetkiliServisYonetimApiService.EditorAsync(dto.Id, kapsam.sirketId);
            return editor == null ? NotFound() : Ok(editor);
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
                Servis = AdminYetkiliServisDto.FromEntity(sonuc.Servis),
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

        [HttpPost("yetkili-servisler/pdf")]
        public Task<IActionResult> YetkiliServisPdf([FromBody] AdminYetkiliServisGetirFiltreDto? dto)
            => YetkiliServisDosyasi(dto, true);

        [HttpPost("yetkili-servisler/excel")]
        public Task<IActionResult> YetkiliServisExcel([FromBody] AdminYetkiliServisGetirFiltreDto? dto)
            => YetkiliServisDosyasi(dto, false);

        private async Task<IActionResult> YetkiliServisDosyasi(AdminYetkiliServisGetirFiltreDto? dto, bool pdf)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();
            if (dto == null || dto.Id <= 0)
                return BadRequest(new ApiIslemSonuc { Mesaj = "Yetkili servis id zorunludur." });

            var kapsam = await KapsamSirketIdAsync(dto.SirketId);
            if (kapsam.gecersiz || !await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            var detay = await _yetkiliServisListeService.GetirAsync(dto.Id, kapsam.sirketId);
            if (detay.Servis == null)
                return NotFound(new ApiIslemSonuc { Mesaj = "Yetkili servis kaydı bulunamadı." });

            var icerik = pdf ? YetkiliServisKayitDosyasi.PdfOlustur(detay) : YetkiliServisKayitDosyasi.ExcelOlustur(detay);
            var tur = pdf ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return this.HassasDosya(icerik, tur, $"Yetkili_Servis_{dto.Id}.{(pdf ? "pdf" : "xlsx")}");
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

        [HttpPost("yetki-belgeleri/rapor/pdf")]
        public Task<IActionResult> YetkiBelgesiRaporPdf([FromBody] YetkiBelgesiRaporFiltre? dto)
            => YetkiBelgesiRaporDosyasi(dto, false);

        [HttpPost("yetki-belgeleri/rapor/excel")]
        public Task<IActionResult> YetkiBelgesiRaporExcel([FromBody] YetkiBelgesiRaporFiltre? dto)
            => YetkiBelgesiRaporDosyasi(dto, true);

        private async Task<IActionResult> YetkiBelgesiRaporDosyasi(YetkiBelgesiRaporFiltre? dto, bool excelMi)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz || !await RaporGorebilirMi(kapsam.sirketId)
                || !await YetkiBelgesiOnaylayabilirMi(kapsam.sirketId)) return Forbid();
            try
            {
                var dosya = await _adminYetkiBelgesiOnayApiService.RaporAsync(dto ?? new(), kapsam.sirketId, excelMi);
                if (dosya == null) return NotFound(new ApiIslemSonuc { Mesaj = "Seçilen dönemde dışa aktarılacak yetki belgesi bulunamadı." });
                return File(dosya.Value.Bytes, dosya.Value.ContentType, dosya.Value.DosyaAdi);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiIslemSonuc { Mesaj = ex.Message });
            }
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

            try
            {
                var dosya = await _devreyeAlmaExportApiService.AdminRaporPdfAsync(
                    kapsam.sirketId, dto?.BaslangicTarihi, dto?.BitisTarihi, dto?.Ids);
                return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
            }
            catch (DevreyeAlmaRaporLimitException ex)
            {
                return BadRequest(new { basarili = false, mesaj = ex.Message });
            }
        }

        [HttpPost("devreye-almalar/rapor/excel")]
        public async Task<IActionResult> DevreyeAlmaRaporExcel([FromBody] AdminDevreyeAlmaRaporExportFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await RaporGorebilirMi(kapsam.sirketId))
                return Forbid();

            try
            {
                var dosya = await _devreyeAlmaExportApiService.AdminRaporExcelAsync(
                    kapsam.sirketId, dto?.BaslangicTarihi, dto?.BitisTarihi, dto?.Ids);
                return this.HassasDosya(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
            }
            catch (DevreyeAlmaRaporLimitException ex)
            {
                return BadRequest(new { basarili = false, mesaj = ex.Message });
            }
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

        [HttpPost("personel-rapor")]
        public async Task<IActionResult> PersonelRapor(
            [FromBody] PersonelRaporFiltreDto? dto,
            [FromServices] PersonelRaporApiService service)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            var yonetici = GenelSistemAdminMi(kullanici) || User.IsInRole("SirketAdmin")
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin;
            var rapor = await service.GetirAsync(kullanici, kapsam.sirketId, yonetici, dto);
            return rapor == null ? Forbid() : Ok(rapor);
        }

        [HttpPost("raporlar/ozet")]
        public async Task<IActionResult> RaporlarOzet([FromBody] AdminRaporOzetFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();
            if (!await RaporGorebilirMi(kapsam.sirketId))
                return Forbid();

            var operasyonGorebilir = await PersonelYetkisiVarMi(kapsam.sirketId, YetkiTipleri.YKC_RAPOR_GOR);
            var belgeGorebilir = await YetkiBelgesiOnaylayabilirMi(kapsam.sirketId);
            var tip = dto?.Tip?.Trim().ToLowerInvariant();
            if ((tip is "onayli" or "bekleyen" or "reddedilen") && !belgeGorebilir
                || (tip is "operasyon" or "ykc") && !operasyonGorebilir)
                return Forbid();

            return Ok(await _adminRaporApiService.RaporlarOzetAsync(
                dto, kapsam.sirketId, operasyonGorebilir, belgeGorebilir));
        }
    }
}
