using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.API.Controllers
{
    public partial class AdminPanelApiController
    {
        [HttpPost("personeller/ekle")]
        public async Task<IActionResult> PersonelEkle([FromBody] AdminPersonelKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized();

            var kapsam = await KapsamSirketIdAsync(dto?.KapsamSirketId ?? dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            if (!await KullaniciYonetebilirMi(kullanici, kapsam.sirketId))
                return Forbid();

            if (dto == null)
                return Ok(AdminIslemSonucDto.Basarisiz("Personel bilgileri zorunludur."));

            if (dto.SirketId <= 0)
                return Ok(AdminIslemSonucDto.Basarisiz("Personel icin sirket secilmelidir."));

            if (!await SirketYonetimKapsamindaMi(kullanici, dto.SirketId, kapsam.sirketId))
                return Forbid();

            if (string.IsNullOrWhiteSpace(dto.AdSoyad))
                return Ok(AdminIslemSonucDto.Basarisiz("Ad soyad zorunludur."));

            if (string.IsNullOrWhiteSpace(dto.Email))
                return Ok(AdminIslemSonucDto.Basarisiz("E-posta zorunludur."));

            var sifreHatalari = ValidatePassword(dto.Sifre);
            if (sifreHatalari.Count > 0)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(" ", sifreHatalari)));

            var email = dto.Email.Trim();
            var mevcut = await _userManager.FindByEmailAsync(email);
            if (mevcut != null)
                return Ok(AdminIslemSonucDto.Basarisiz("Bu e-posta ile kayitli bir kullanici zaten var."));

            var yeni = new AppKullanici
            {
                UserName = email,
                Email = email,
                PhoneNumber = dto.Telefon,
                AdSoyad = dto.AdSoyad.Trim(),
                KullaniciTipi = KullaniciTipiDegerleri.Personel,
                SirketId = dto.SirketId,
                AktifMi = true,
                EmailConfirmed = true
            };

            var sonuc = await _userManager.CreateAsync(yeni, dto.Sifre ?? string.Empty);
            if (!sonuc.Succeeded)
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", sonuc.Errors.Select(x => x.Description))));

            var rolSonuc = await _userManager.AddToRoleAsync(yeni, "Personel");
            if (!rolSonuc.Succeeded)
            {
                await _userManager.DeleteAsync(yeni);
                return Ok(AdminIslemSonucDto.Basarisiz(string.Join(", ", rolSonuc.Errors.Select(x => x.Description))));
            }

            _logger.LogInformation("Admin personel olusturdu. YapanId: {YapanId}, YeniKullaniciId: {YeniKullaniciId}", kullanici.Id, yeni.Id);
            return Ok(AdminIslemSonucDto.BasariliSonuc("Personel basariyla olusturuldu."));
        }

    }
}
