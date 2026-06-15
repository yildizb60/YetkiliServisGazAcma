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

            if (!await KullaniciYonetebilirMi(yapan, kapsam.sirketId))
                return Forbid();

            var genelSistemAdmin = User.IsInRole("GenelSistemAdmin") || User.IsInRole("SuperAdmin");
            var kullaniciQuery = _context.Users
                .Include(x => x.Sirket)
                .Include(x => x.Firma)
                .AsQueryable();

            if (!genelSistemAdmin || kapsam.sirketId.HasValue)
            {
                kullaniciQuery = kullaniciQuery.Where(x =>
                    x.Id == yapan.Id ||
                    ((x.KullaniciTipi == KullaniciTipiDegerleri.Personel || x.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin) && kapsam.sirketId.HasValue && x.SirketId == kapsam.sirketId.Value) ||
                    (x.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis && x.Firma != null && kapsam.sirketId.HasValue && x.Firma.SirketId == kapsam.sirketId.Value));
            }

            var kullanicilar = await kullaniciQuery
                .OrderBy(x => x.AdSoyad)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(dto?.Q))
            {
                var aranacak = dto.Q.Trim();
                kullanicilar = kullanicilar
                    .Where(x =>
                        (!string.IsNullOrWhiteSpace(x.AdSoyad) && x.AdSoyad.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(x.Email) && x.Email.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(x.PhoneNumber) && x.PhoneNumber.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(dto?.Tip))
            {
                kullanicilar = dto.Tip switch
                {
                    "GenelSistemAdmin" => kullanicilar.Where(x => x.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin).ToList(),
                    "SirketAdmin" => kullanicilar.Where(x => x.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin).ToList(),
                    "SuperAdmin" => kullanicilar.Where(x => x.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin).ToList(),
                    "Personel" => kullanicilar.Where(x => x.KullaniciTipi == KullaniciTipiDegerleri.Personel).ToList(),
                    "Servis" => kullanicilar.Where(x => x.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis).ToList(),
                    _ => kullanicilar
                };
            }

            if (!string.IsNullOrWhiteSpace(dto?.Durum))
            {
                var aktifMi = dto.Durum.Equals("Aktif", StringComparison.OrdinalIgnoreCase);
                kullanicilar = kullanicilar.Where(x => x.AktifMi == aktifMi).ToList();
            }

            if (!string.IsNullOrWhiteSpace(dto?.Bagli))
            {
                var aranacak = dto.Bagli.Trim();
                kullanicilar = kullanicilar
                    .Where(x =>
                        (x.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis && x.Firma != null && !string.IsNullOrWhiteSpace(x.Firma.FirmaAdi) &&
                         x.Firma.FirmaAdi.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)) ||
                        ((x.KullaniciTipi == KullaniciTipiDegerleri.Personel || x.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin) && x.Sirket != null && !string.IsNullOrWhiteSpace(x.Sirket.SirketAdi) &&
                         x.Sirket.SirketAdi.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)))
                    .ToList();
            }

            return Ok(kullanicilar.Select(x => new AdminKullaniciListeDto
            {
                Id = x.Id,
                AdSoyad = !string.IsNullOrWhiteSpace(x.AdSoyad)
                    ? x.AdSoyad
                    : x.Firma?.YetkiliKisi ?? x.Firma?.FirmaAdi,
                Email = !string.IsNullOrWhiteSpace(x.Email) ? x.Email : x.Firma?.Email,
                PhoneNumber = !string.IsNullOrWhiteSpace(x.PhoneNumber) ? x.PhoneNumber : x.Firma?.Telefon,
                KullaniciTipi = x.KullaniciTipi,
                AktifMi = x.AktifMi,
                SirketId = x.SirketId,
                SirketAdi = x.Sirket?.SirketAdi,
                FirmaId = x.FirmaId,
                FirmaAdi = x.Firma?.FirmaAdi,
                FirmaYetkiliKisi = x.Firma?.YetkiliKisi,
                FirmaEmail = x.Firma?.Email,
                FirmaTelefon = x.Firma?.Telefon
            }).ToList());
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

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await SirketSecenekleriAsync(kapsam.sirketId));
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

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            return Ok(await FirmaSecenekleriAsync(kapsam.sirketId));
        }

        [HttpPost("kullanicilar/yetkili-servis-senkronize")]
        public async Task<IActionResult> YetkiliServisKullanicilariniSenkronize([FromBody] AdminKullaniciSenkronFiltreDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            await YetkiliServisKullanicilariniSenkronizeAsync(kapsam.sirketId);
            return Ok(AdminIslemSonucDto.BasariliSonuc("Yetkili servis kullanicilari senkronize edildi."));
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
                YetkiliMi = await KullaniciYonetebilirMi(kullanici, kapsam.sirketId)
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

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return NotFound();

            var hedef = await _context.Users
                .Include(x => x.Sirket)
                .Include(x => x.Firma)
                .FirstOrDefaultAsync(x => x.Id == dto.Id);

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

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return Ok(AdminIslemSonucDto.Basarisiz("Kullanici id zorunludur."));

            var hedef = await _context.Users.FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (hedef == null)
                return Ok(AdminIslemSonucDto.Basarisiz("Kullanici bulunamadi."));

            if (!await KullaniciKapsamindaMi(kullanici, hedef, kapsam.sirketId))
                return Forbid();

            if ((hedef.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin || hedef.KullaniciTipi == KullaniciTipiDegerleri.Personel) && (!dto.SirketId.HasValue || dto.SirketId.Value <= 0))
            {
                return Ok(AdminIslemSonucDto.Basarisiz(hedef.KullaniciTipi == KullaniciTipiDegerleri.Personel
                    ? "Personel icin sirket secilmelidir."
                    : "Sirket admini icin sirket secilmelidir."));
            }

            if (hedef.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin || hedef.KullaniciTipi == KullaniciTipiDegerleri.Personel)
            {
                if (!await SirketYonetimKapsamindaMi(kullanici, dto.SirketId!.Value, kapsam.sirketId))
                    return Forbid();

                hedef.SirketId = dto.SirketId;
                hedef.FirmaId = null;
            }
            else if (hedef.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis)
            {
                if (!dto.FirmaId.HasValue || dto.FirmaId.Value <= 0)
                    return Ok(AdminIslemSonucDto.Basarisiz("Yetkili servis kullanicisi icin firma secilmelidir."));

                var firma = await _context.Ys_Firmalar
                    .FirstOrDefaultAsync(x => x.Id == dto.FirmaId.Value && !x.SilindiMi);
                if (firma == null)
                    return Ok(AdminIslemSonucDto.Basarisiz("Secilen firma bulunamadi."));

                if (!await SirketYonetimKapsamindaMi(kullanici, firma.SirketId, kapsam.sirketId))
                    return Forbid();

                hedef.FirmaId = firma.Id;
                hedef.SirketId = firma.SirketId;
            }
            else
            {
                hedef.SirketId = null;
                hedef.FirmaId = null;
            }

            hedef.AdSoyad = dto.AdSoyad;
            hedef.Email = dto.Email;
            hedef.UserName = dto.Email;
            hedef.PhoneNumber = dto.Telefon;
            hedef.AktifMi = dto.AktifMi;

            var sonuc = await _userManager.UpdateAsync(hedef);
            if (!sonuc.Succeeded)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", sonuc.Errors.Select(x => x.Description))));

            if (!string.IsNullOrWhiteSpace(dto.YeniSifre) || !string.IsNullOrWhiteSpace(dto.YeniSifreTekrar))
            {
                if (dto.YeniSifre != dto.YeniSifreTekrar)
                    return Ok(AdminIslemSonucDto.Basarisiz("Yeni sifreler eslesmiyor."));

                var sifreHatalari = ValidatePassword(dto.YeniSifre);
                if (sifreHatalari.Count > 0)
                    return Ok(AdminIslemSonucDto.Basarisiz(string.Join(" ", sifreHatalari)));

                var token = await _userManager.GeneratePasswordResetTokenAsync(hedef);
                var sifreSonuc = await _userManager.ResetPasswordAsync(hedef, token, dto.YeniSifre ?? "");
                if (!sifreSonuc.Succeeded)
                    return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", sifreSonuc.Errors.Select(x => x.Description))));
            }

            _logger.LogInformation("Admin kullanici guncelledi. YapanId: {YapanId}, HedefId: {HedefId}", kullanici.Id, hedef.Id);
            return Ok(AdminIslemSonucDto.BasariliSonuc("Kullanici guncellendi."));
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

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null)
                return Ok(AdminIslemSonucDto.Basarisiz("Kullanici bilgileri zorunludur."));

            var rol = (dto.Rol ?? "").Trim();
            if (string.Equals(rol, "Servis", StringComparison.OrdinalIgnoreCase))
                rol = "YetkiliServis";
            if (string.Equals(rol, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
                rol = "SirketAdmin";

            var gecerliRoller = new[] { "GenelSistemAdmin", "SirketAdmin", "Personel", "YetkiliServis" };
            if (!gecerliRoller.Any(x => string.Equals(x, rol, StringComparison.OrdinalIgnoreCase)))
                return Ok(AdminIslemSonucDto.Basarisiz("Rol secilmelidir."));

            rol = gecerliRoller.First(x => string.Equals(x, rol, StringComparison.OrdinalIgnoreCase));

            var genelSistemAdmin = User.IsInRole("GenelSistemAdmin")
                || User.IsInRole("SuperAdmin")
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin;
            if (rol == "GenelSistemAdmin" && !genelSistemAdmin)
                return Ok(AdminIslemSonucDto.Basarisiz("Genel Sistem Admini sadece genel sistem admini tarafindan olusturulabilir."));

            var sifreHatalari = ValidatePassword(dto.Sifre);
            if (sifreHatalari.Count > 0)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(" ", sifreHatalari)));

            var kullaniciTipi = rol == "GenelSistemAdmin"
                ? KullaniciTipiDegerleri.GenelSistemAdmin
                : rol == "SirketAdmin"
                    ? KullaniciTipiDegerleri.SirketAdmin
                    : rol == "Personel"
                        ? KullaniciTipiDegerleri.Personel
                        : KullaniciTipiDegerleri.YetkiliServis;
            if ((kullaniciTipi == KullaniciTipiDegerleri.SirketAdmin || kullaniciTipi == KullaniciTipiDegerleri.Personel || kullaniciTipi == KullaniciTipiDegerleri.YetkiliServis) && (!dto.SirketId.HasValue || dto.SirketId.Value <= 0))
            {
                var mesaj = kullaniciTipi == KullaniciTipiDegerleri.YetkiliServis
                    ? "Yetkili servis icin bagli dagitim sirketi secilmelidir."
                    : kullaniciTipi == KullaniciTipiDegerleri.Personel
                        ? "Personel icin sirket secilmelidir."
                        : "Sirket admini icin sirket secilmelidir.";
                return Ok(AdminIslemSonucDto.Basarisiz(mesaj));
            }

            if (kullaniciTipi == KullaniciTipiDegerleri.SirketAdmin || kullaniciTipi == KullaniciTipiDegerleri.Personel || kullaniciTipi == KullaniciTipiDegerleri.YetkiliServis)
            {
                if (!await SirketYonetimKapsamindaMi(kullanici, dto.SirketId!.Value, kapsam.sirketId))
                    return Forbid();
            }

            var email = (dto.Email ?? "").Trim();
            if (string.IsNullOrWhiteSpace(email))
                return Ok(AdminIslemSonucDto.Basarisiz("E-posta zorunludur."));

            var mevcut = await _userManager.FindByEmailAsync(email);
            if (mevcut != null)
                return Ok(AdminIslemSonucDto.Basarisiz("Bu e-posta ile kayitli bir kullanici zaten var."));

            var yeni = new AppKullanici
            {
                UserName = email,
                Email = email,
                PhoneNumber = dto.Telefon,
                AdSoyad = dto.AdSoyad,
                KullaniciTipi = kullaniciTipi,
                SirketId = (kullaniciTipi == KullaniciTipiDegerleri.SirketAdmin || kullaniciTipi == KullaniciTipiDegerleri.Personel || kullaniciTipi == KullaniciTipiDegerleri.YetkiliServis) ? dto.SirketId : null,
                FirmaId = null,
                AktifMi = true,
                EmailConfirmed = true
            };

            var createSonuc = await _userManager.CreateAsync(yeni, dto.Sifre ?? string.Empty);
            if (!createSonuc.Succeeded)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", createSonuc.Errors.Select(x => x.Description))));

            Ys_Firma? firma = null;
            if (kullaniciTipi == KullaniciTipiDegerleri.YetkiliServis)
            {
                try
                {
                    firma = new Ys_Firma
                    {
                        FirmaAdi = dto.AdSoyad,
                        YetkiliKisi = dto.AdSoyad,
                        Telefon = dto.Telefon,
                        Email = email,
                        SirketId = dto.SirketId!.Value,
                        AktifMi = true
                    };

                    _context.Ys_Firmalar.Add(firma);
                    await _context.SaveChangesAsync();

                    yeni.FirmaId = firma.Id;
                    yeni.SirketId = firma.SirketId;
                    await _userManager.UpdateAsync(yeni);
                }
                catch
                {
                    await _userManager.DeleteAsync(yeni);
                    return Ok(AdminIslemSonucDto.Basarisiz("Yetkili servis kaydi olusturulurken hata olustu. Lutfen tekrar deneyin."));
                }
            }

            var atanacakRol = rol;
            if (rol == "YetkiliServis")
            {
                var ysRol = await YetkiliServisRolAdiAsync();
                if (string.IsNullOrWhiteSpace(ysRol))
                {
                    await _userManager.DeleteAsync(yeni);
                    if (firma != null)
                    {
                        _context.Ys_Firmalar.Remove(firma);
                        await _context.SaveChangesAsync();
                    }

                    return Ok(AdminIslemSonucDto.Basarisiz("Yetkili Servis rolu sistemde bulunamadi."));
                }

                atanacakRol = ysRol!;
            }

            var rolVarMi = await _context.Set<IdentityRole>()
                .AnyAsync(r => r.Name != null && r.Name.ToLower() == atanacakRol.ToLower());
            if (!rolVarMi)
            {
                await _userManager.DeleteAsync(yeni);
                if (firma != null)
                {
                    _context.Ys_Firmalar.Remove(firma);
                    await _context.SaveChangesAsync();
                }

                return Ok(AdminIslemSonucDto.Basarisiz($"Rol bulunamadi: {atanacakRol}"));
            }

            var rolSonuc = await _userManager.AddToRoleAsync(yeni, atanacakRol);
            if (!rolSonuc.Succeeded)
            {
                await _userManager.DeleteAsync(yeni);
                if (firma != null)
                {
                    _context.Ys_Firmalar.Remove(firma);
                    await _context.SaveChangesAsync();
                }

                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", rolSonuc.Errors.Select(x => x.Description))));
            }

            if (rol == "GenelSistemAdmin")
                await _userManager.AddToRoleAsync(yeni, KullaniciRolAdlari.EskiSuperAdmin);

            _logger.LogInformation("Admin kullanici olusturdu. YapanId: {YapanId}, YeniKullaniciId: {YeniKullaniciId}, Rol: {Rol}", kullanici.Id, yeni.Id, rol);
            return Ok(AdminIslemSonucDto.BasariliSonuc("Kullanici basariyla olusturuldu."));
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

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return Ok(AdminIslemSonucDto.Basarisiz("Kullanici id zorunludur."));

            var hedef = await _context.Users.FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (hedef == null || (dto.SadecePersonel && hedef.KullaniciTipi != KullaniciTipiDegerleri.Personel))
                return Ok(AdminIslemSonucDto.Basarisiz(dto.SadecePersonel ? "Personel bulunamadi." : "Kullanici bulunamadi."));

            if (!await KullaniciKapsamindaMi(kullanici, hedef, kapsam.sirketId))
                return Forbid();

            hedef.AktifMi = dto.AktifMi;
            var sonuc = await _userManager.UpdateAsync(hedef);
            if (!sonuc.Succeeded)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", sonuc.Errors.Select(x => x.Description))));

            _logger.LogInformation("Admin kullanici durumunu degistirdi. YapanId: {YapanId}, HedefId: {HedefId}, AktifMi: {AktifMi}", kullanici.Id, hedef.Id, dto.AktifMi);
            return Ok(AdminIslemSonucDto.BasariliSonuc(dto.AktifMi ? "Kullanici aktif edildi." : "Kullanici pasiflestirildi."));
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

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return Ok(AdminIslemSonucDto.Basarisiz("Kullanici id zorunludur."));

            var hedef = await _context.Users.FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (hedef == null || (dto.SadecePersonel && hedef.KullaniciTipi != KullaniciTipiDegerleri.Personel))
                return Ok(AdminIslemSonucDto.Basarisiz(dto.SadecePersonel ? "Personel bulunamadi." : "Kullanici bulunamadi."));

            if (!await KullaniciKapsamindaMi(kullanici, hedef, kapsam.sirketId))
                return Forbid();

            if (kullanici.Id == hedef.Id)
                return Ok(AdminIslemSonucDto.Basarisiz("Kendi hesabinizi silemezsiniz."));

            var sonuc = await _userManager.DeleteAsync(hedef);
            if (!sonuc.Succeeded)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", sonuc.Errors.Select(x => x.Description))));

            _logger.LogInformation("Admin kullanici sildi. YapanId: {YapanId}, HedefId: {HedefId}, SadecePersonel: {SadecePersonel}", kullanici.Id, hedef.Id, dto.SadecePersonel);
            return Ok(AdminIslemSonucDto.BasariliSonuc(dto.SadecePersonel ? "Personel silindi." : "Kullanici silindi."));
        }

    }
}
