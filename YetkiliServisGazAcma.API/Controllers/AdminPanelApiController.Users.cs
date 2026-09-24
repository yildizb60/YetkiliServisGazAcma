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

            var genelSistemAdmin = GenelSistemAdminMi(yapan);
            var kullaniciQuery = _context.Users
                .Include(x => x.Sirket)
                .Include(x => x.Firma)
                .AsQueryable();

            if (!genelSistemAdmin)
            {
                kullaniciQuery = kullaniciQuery.Where(x =>
                    ((x.KullaniciTipi == KullaniciTipiDegerleri.Personel) && kapsam.sirketId.HasValue && x.SirketId == kapsam.sirketId.Value) ||
                    ((x.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis || x.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma) && x.Firma != null && kapsam.sirketId.HasValue && x.Firma.SirketId == kapsam.sirketId.Value));
            }
            else if (kapsam.sirketId.HasValue)
            {
                kullaniciQuery = kullaniciQuery.Where(x =>
                    ((x.KullaniciTipi == KullaniciTipiDegerleri.Personel || x.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin) && kapsam.sirketId.HasValue && x.SirketId == kapsam.sirketId.Value) ||
                    ((x.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis || x.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma) && x.Firma != null && kapsam.sirketId.HasValue && x.Firma.SirketId == kapsam.sirketId.Value));
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
                    "SertifikaliFirma" => kullanicilar.Where(x => x.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma).ToList(),
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
                        ((x.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis || x.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma) && x.Firma != null && !string.IsNullOrWhiteSpace(x.Firma.FirmaAdi) &&
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

            if (kullanici.Id == hedef.Id && !dto.AktifMi)
                return Ok(AdminIslemSonucDto.Basarisiz("Kendi hesabinizi pasiflestiremezsiniz."));

            if (!CepTelefonuKurali.GecerliMi(dto.Telefon))
                return Ok(AdminIslemSonucDto.Basarisiz("Telefon numarasi 05XXXXXXXXX veya 90XXXXXXXXXX formatinda olmalidir."));

            if (!GenelSistemAdminMi(kullanici) &&
                (hedef.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin || hedef.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin))
                return Ok(AdminIslemSonucDto.Basarisiz("Sirket admini genel sistem admini veya sirket admini hesabini duzenleyemez."));

            var sifreDegisecek = !string.IsNullOrWhiteSpace(dto.YeniSifre) || !string.IsNullOrWhiteSpace(dto.YeniSifreTekrar);
            if (sifreDegisecek)
            {
                if (dto.YeniSifre != dto.YeniSifreTekrar)
                    return Ok(AdminIslemSonucDto.Basarisiz("Yeni sifreler eslesmiyor."));

                var sifreHatalari = ValidatePassword(dto.YeniSifre);
                if (sifreHatalari.Count > 0)
                    return Ok(AdminIslemSonucDto.Basarisiz(string.Join(" ", sifreHatalari)));
            }

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
            else if (hedef.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis ||
                     hedef.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma)
            {
                if (!dto.FirmaId.HasValue || dto.FirmaId.Value <= 0)
                    return Ok(AdminIslemSonucDto.Basarisiz("Firma kullanicisi icin firma secilmelidir."));

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

            var oncekiEmail = hedef.Email;
            var telefonDegisti = !string.Equals(hedef.PhoneNumber?.Trim(), dto.Telefon?.Trim(), StringComparison.Ordinal);
            hedef.AdSoyad = dto.AdSoyad;
            hedef.Email = dto.Email;
            if (string.IsNullOrWhiteSpace(hedef.UserName)
                || string.Equals(hedef.UserName, oncekiEmail, StringComparison.OrdinalIgnoreCase))
                hedef.UserName = dto.Email;
            hedef.PhoneNumber = dto.Telefon?.Trim();
            if (telefonDegisti)
                hedef.PhoneNumberConfirmed = false;
            hedef.AktifMi = dto.AktifMi;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var sonuc = await _userManager.UpdateAsync(hedef);
            if (!sonuc.Succeeded)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", sonuc.Errors.Select(x => x.Description))));

            if (sifreDegisecek)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(hedef);
                var sifreSonuc = await _userManager.ResetPasswordAsync(hedef, token, dto.YeniSifre ?? "");
                if (!sifreSonuc.Succeeded)
                    return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", sifreSonuc.Errors.Select(x => x.Description))));
            }

            await transaction.CommitAsync();
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

            var gecerliRoller = new[] { "GenelSistemAdmin", "SirketAdmin", "Personel", "YetkiliServis", "SertifikaliFirma" };
            if (!gecerliRoller.Any(x => string.Equals(x, rol, StringComparison.OrdinalIgnoreCase)))
                return Ok(AdminIslemSonucDto.Basarisiz("Rol secilmelidir."));

            rol = gecerliRoller.First(x => string.Equals(x, rol, StringComparison.OrdinalIgnoreCase));

            var genelSistemAdmin = GenelSistemAdminMi(kullanici);
            if (!genelSistemAdmin && (rol == "GenelSistemAdmin" || rol == "SirketAdmin"))
                return Ok(AdminIslemSonucDto.Basarisiz("Sirket admini sadece kendi sirketine bagli personel, yetkili servis ve sertifikali firma kullanicisi olusturabilir."));

            var sifreHatalari = ValidatePassword(dto.Sifre);
            if (sifreHatalari.Count > 0)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(" ", sifreHatalari)));

            var kullaniciTipi = rol == "GenelSistemAdmin"
                ? KullaniciTipiDegerleri.GenelSistemAdmin
                : rol == "SirketAdmin"
                    ? KullaniciTipiDegerleri.SirketAdmin
                    : rol == "Personel"
                        ? KullaniciTipiDegerleri.Personel
                        : rol == "SertifikaliFirma"
                            ? KullaniciTipiDegerleri.SertifikaliFirma
                            : KullaniciTipiDegerleri.YetkiliServis;
            if ((kullaniciTipi == KullaniciTipiDegerleri.SirketAdmin ||
                 kullaniciTipi == KullaniciTipiDegerleri.Personel ||
                 kullaniciTipi == KullaniciTipiDegerleri.YetkiliServis ||
                 kullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma) &&
                (!dto.SirketId.HasValue || dto.SirketId.Value <= 0))
            {
                var mesaj = (kullaniciTipi == KullaniciTipiDegerleri.YetkiliServis ||
                             kullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma)
                    ? "Firma kullanicisi icin bagli dagitim sirketi secilmelidir."
                    : kullaniciTipi == KullaniciTipiDegerleri.Personel
                        ? "Personel icin sirket secilmelidir."
                        : "Sirket admini icin sirket secilmelidir.";
                return Ok(AdminIslemSonucDto.Basarisiz(mesaj));
            }

            if (kullaniciTipi == KullaniciTipiDegerleri.SirketAdmin ||
                kullaniciTipi == KullaniciTipiDegerleri.Personel ||
                kullaniciTipi == KullaniciTipiDegerleri.YetkiliServis ||
                kullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma)
            {
                if (!await SirketYonetimKapsamindaMi(kullanici, dto.SirketId!.Value, kapsam.sirketId))
                    return Forbid();
            }

            var email = (dto.Email ?? "").Trim();
            if (string.IsNullOrWhiteSpace(email))
                return Ok(AdminIslemSonucDto.Basarisiz("E-posta zorunludur."));

            if (!CepTelefonuKurali.GecerliMi(dto.Telefon))
                return Ok(AdminIslemSonucDto.Basarisiz("Telefon numarasi 05XXXXXXXXX veya 90XXXXXXXXXX formatinda olmalidir."));

            var mevcut = await _userManager.FindByEmailAsync(email);
            if (mevcut != null)
                return Ok(AdminIslemSonucDto.Basarisiz("Bu e-posta ile kayitli bir kullanici zaten var."));

            Ys_Firma? secilenFirma = null;
            if (dto.FirmaId.HasValue)
            {
                if (kullaniciTipi != KullaniciTipiDegerleri.YetkiliServis)
                    return Ok(AdminIslemSonucDto.Basarisiz("Mevcut firma yalnizca yetkili servis hesabina baglanabilir."));

                secilenFirma = await _context.Ys_Firmalar.FirstOrDefaultAsync(x =>
                    x.Id == dto.FirmaId.Value && !x.SilindiMi && x.AktifMi);
                if (secilenFirma == null || secilenFirma.SirketId != dto.SirketId)
                    return Ok(AdminIslemSonucDto.Basarisiz("Secilen yetkili servis bulunamadi veya farkli bir sirkete bagli."));

                if (await _context.Users.AnyAsync(x => x.FirmaId == secilenFirma.Id
                    && (x.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis
                        || x.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma)))
                    return Ok(AdminIslemSonucDto.Basarisiz("Bu firmaya ait bir giris hesabi zaten var. Kullanicilar ekranindan duzenleyin."));
            }

            var yeni = new AppKullanici
            {
                UserName = !string.IsNullOrWhiteSpace(secilenFirma?.VergiNo) ? secilenFirma.VergiNo.Trim() : email,
                Email = email,
                PhoneNumber = dto.Telefon?.Trim(),
                AdSoyad = dto.AdSoyad,
                KullaniciTipi = kullaniciTipi,
                SirketId = (kullaniciTipi == KullaniciTipiDegerleri.SirketAdmin ||
                            kullaniciTipi == KullaniciTipiDegerleri.Personel ||
                            kullaniciTipi == KullaniciTipiDegerleri.YetkiliServis ||
                            kullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma) ? dto.SirketId : null,
                FirmaId = secilenFirma?.Id,
                AktifMi = true,
                EmailConfirmed = true
            };

            var createSonuc = await _userManager.CreateAsync(yeni, dto.Sifre ?? string.Empty);
            if (!createSonuc.Succeeded)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", createSonuc.Errors.Select(x => x.Description))));

            Ys_Firma? firma = secilenFirma;
            var yeniFirmaOlusturuldu = false;
            if ((kullaniciTipi == KullaniciTipiDegerleri.YetkiliServis ||
                kullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma) && firma == null)
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
                        OlusturmaTipi = YetkiliServisOlusturmaTipleri.Admin,
                        AktifMi = true
                    };

                    _context.Ys_Firmalar.Add(firma);
                    await _context.SaveChangesAsync();
                    yeniFirmaOlusturuldu = true;

                    yeni.FirmaId = firma.Id;
                    yeni.SirketId = firma.SirketId;
                    var baglantiSonucu = await _userManager.UpdateAsync(yeni);
                    if (!baglantiSonucu.Succeeded)
                        throw new InvalidOperationException("Firma hesabı kullanıcıya bağlanamadı.");
                }
                catch
                {
                    await _userManager.DeleteAsync(yeni);
                    if (yeniFirmaOlusturuldu && firma != null)
                    {
                        _context.Ys_Firmalar.Remove(firma);
                        await _context.SaveChangesAsync();
                    }
                    return Ok(AdminIslemSonucDto.Basarisiz("Firma kullanicisi kaydi olusturulurken hata olustu. Lutfen tekrar deneyin."));
                }
            }

            var atanacakRol = rol;
            if (rol == "YetkiliServis")
            {
                var ysRol = await YetkiliServisRolAdiAsync();
                if (string.IsNullOrWhiteSpace(ysRol))
                {
                    await _userManager.DeleteAsync(yeni);
                    if (yeniFirmaOlusturuldu && firma != null)
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
                if (yeniFirmaOlusturuldu && firma != null)
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
                if (yeniFirmaOlusturuldu && firma != null)
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

            if (!GenelSistemAdminMi(kullanici) &&
                (hedef.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin || hedef.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin))
                return Ok(AdminIslemSonucDto.Basarisiz("Sirket admini genel sistem admini veya sirket admini hesabinin durumunu degistiremez."));

            if (kullanici.Id == hedef.Id && !dto.AktifMi)
                return Ok(AdminIslemSonucDto.Basarisiz("Kendi hesabinizi pasiflestiremezsiniz."));

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

            if (!GenelSistemAdminMi(kullanici) &&
                (hedef.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin || hedef.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin))
                return Ok(AdminIslemSonucDto.Basarisiz("Sirket admini genel sistem admini veya sirket admini hesabini silemez."));

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
