using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using Microsoft.AspNetCore.Identity;
using YetkiliServisGazAcma.API.Controllers;

namespace YetkiliServisGazAcma.API.Services;

public sealed class AdminKullaniciYonetimApiService(AppDbContext context, UserManager<AppKullanici> userManager, ILogger<AdminPanelApiController> logger)
{
    private readonly AppDbContext _context = context;
    private readonly UserManager<AppKullanici> _userManager = userManager;
    private readonly ILogger<AdminPanelApiController> _logger = logger;

    public async Task<(bool Yetkisiz, ApiIslemSonuc? Sonuc)> KullaniciGuncelleAsync(
        AdminKullaniciGuncelleDto? dto, AppKullanici kullanici, int? sirketId, bool genelSistemAdmin)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
            return (false, ApiIslemSonuc.Basarisiz("Kullanici id zorunludur."));

        var hedef = await _context.Users.FirstOrDefaultAsync(x => x.Id == dto.Id && x.ArsivlemeTarihi == null);
        if (hedef == null)
            return (false, ApiIslemSonuc.Basarisiz("Kullanici bulunamadi."));

        if (!await KullaniciKapsamindaMi(kullanici, hedef, sirketId, genelSistemAdmin))
            return (true, null);

        if (kullanici.Id == hedef.Id && !dto.AktifMi)
            return (false, ApiIslemSonuc.Basarisiz("Kendi hesabinizi pasiflestiremezsiniz."));

        if (!CepTelefonuKurali.GecerliMi(dto.Telefon))
            return (false, ApiIslemSonuc.Basarisiz("Telefon numarasi 05XXXXXXXXX veya 90XXXXXXXXXX formatinda olmalidir."));

        if (!genelSistemAdmin &&
            (hedef.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin || hedef.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin))
            return (false, ApiIslemSonuc.Basarisiz("Sirket admini genel sistem admini veya sirket admini hesabini duzenleyemez."));

        var sifreDegisecek = !string.IsNullOrWhiteSpace(dto.YeniSifre) || !string.IsNullOrWhiteSpace(dto.YeniSifreTekrar);
        if (sifreDegisecek)
        {
            if (dto.YeniSifre != dto.YeniSifreTekrar)
                return (false, ApiIslemSonuc.Basarisiz("Yeni sifreler eslesmiyor."));

            var sifreHatalari = ValidatePassword(dto.YeniSifre);
            if (sifreHatalari.Count > 0)
                return (false, ApiIslemSonuc.Basarisiz(string.Join(" ", sifreHatalari)));
        }

        if ((hedef.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin || hedef.KullaniciTipi == KullaniciTipiDegerleri.Personel) && (!dto.SirketId.HasValue || dto.SirketId.Value <= 0))
        {
            return (false, ApiIslemSonuc.Basarisiz(hedef.KullaniciTipi == KullaniciTipiDegerleri.Personel
                ? "Personel icin sirket secilmelidir."
                : "Sirket admini icin sirket secilmelidir."));
        }

        if (hedef.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin || hedef.KullaniciTipi == KullaniciTipiDegerleri.Personel)
        {
            if (!await SirketYonetimKapsamindaMi(kullanici, dto.SirketId!.Value, sirketId, genelSistemAdmin))
                return (true, null);

            hedef.SirketId = dto.SirketId;
            hedef.FirmaId = null;
        }
        else if (hedef.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis ||
                 hedef.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma)
        {
            if (!dto.FirmaId.HasValue || dto.FirmaId.Value <= 0)
                return (false, ApiIslemSonuc.Basarisiz("Firma kullanicisi icin firma secilmelidir."));

            var firma = await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.Id == dto.FirmaId.Value && !x.SilindiMi);
            if (firma == null)
                return (false, ApiIslemSonuc.Basarisiz("Secilen firma bulunamadi."));

            if (!await SirketYonetimKapsamindaMi(kullanici, firma.SirketId, sirketId, genelSistemAdmin))
                return (true, null);

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
            return (false, ApiIslemSonuc.Basarisiz(string.Join(", ", sonuc.Errors.Select(x => x.Description))));

        if (sifreDegisecek)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(hedef);
            var sifreSonuc = await _userManager.ResetPasswordAsync(hedef, token, dto.YeniSifre ?? "");
            if (!sifreSonuc.Succeeded)
                return (false, ApiIslemSonuc.Basarisiz(string.Join(", ", sifreSonuc.Errors.Select(x => x.Description))));
        }

        await transaction.CommitAsync();
        _logger.LogInformation("Admin kullanici guncelledi. YapanId: {YapanId}, HedefId: {HedefId}", kullanici.Id, hedef.Id);
        return (false, ApiIslemSonuc.BasariliSonuc("Kullanici guncellendi."));
    }

    public async Task<(bool Yetkisiz, ApiIslemSonuc? Sonuc)> KullaniciEkleAsync(
        AdminKullaniciKaydetDto? dto, AppKullanici kullanici, int? sirketId, bool genelSistemAdmin)
    {
        if (dto == null)
            return (false, ApiIslemSonuc.Basarisiz("Kullanici bilgileri zorunludur."));

        var rol = (dto.Rol ?? "").Trim();
        if (string.Equals(rol, "Servis", StringComparison.OrdinalIgnoreCase))
            rol = "YetkiliServis";
        if (string.Equals(rol, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            rol = "SirketAdmin";

        var gecerliRoller = new[] { "GenelSistemAdmin", "SirketAdmin", "Personel", "YetkiliServis", "SertifikaliFirma" };
        if (!gecerliRoller.Any(x => string.Equals(x, rol, StringComparison.OrdinalIgnoreCase)))
            return (false, ApiIslemSonuc.Basarisiz("Rol secilmelidir."));

        rol = gecerliRoller.First(x => string.Equals(x, rol, StringComparison.OrdinalIgnoreCase));

        if (!genelSistemAdmin && (rol == "GenelSistemAdmin" || rol == "SirketAdmin"))
            return (false, ApiIslemSonuc.Basarisiz("Sirket admini sadece kendi sirketine bagli personel, yetkili servis ve sertifikali firma kullanicisi olusturabilir."));

        var sifreHatalari = ValidatePassword(dto.Sifre);
        if (sifreHatalari.Count > 0)
            return (false, ApiIslemSonuc.Basarisiz(string.Join(" ", sifreHatalari)));

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
            return (false, ApiIslemSonuc.Basarisiz(mesaj));
        }

        if (kullaniciTipi == KullaniciTipiDegerleri.SirketAdmin ||
            kullaniciTipi == KullaniciTipiDegerleri.Personel ||
            kullaniciTipi == KullaniciTipiDegerleri.YetkiliServis ||
            kullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma)
        {
            if (!await SirketYonetimKapsamindaMi(kullanici, dto.SirketId!.Value, sirketId, genelSistemAdmin))
                return (true, null);
        }

        var email = (dto.Email ?? "").Trim();
        if (string.IsNullOrWhiteSpace(email))
            return (false, ApiIslemSonuc.Basarisiz("E-posta zorunludur."));

        if (!CepTelefonuKurali.GecerliMi(dto.Telefon))
            return (false, ApiIslemSonuc.Basarisiz("Telefon numarasi 05XXXXXXXXX veya 90XXXXXXXXXX formatinda olmalidir."));

        var mevcut = await _userManager.FindByEmailAsync(email);
        if (mevcut != null)
            return (false, ApiIslemSonuc.Basarisiz(mevcut.ArsivlemeTarihi.HasValue
                ? "Bu e-posta arşivlenmiş bir hesaba aittir. İşlem geçmişini korumak için yeni hesapta farklı bir e-posta adresi kullanın."
                : "Bu e-posta ile kayitli bir kullanici zaten var."));

        Ys_Firma? secilenFirma = null;
        if (dto.FirmaId.HasValue)
        {
            if (kullaniciTipi != KullaniciTipiDegerleri.YetkiliServis)
                return (false, ApiIslemSonuc.Basarisiz("Mevcut firma yalnizca yetkili servis hesabina baglanabilir."));

            secilenFirma = await _context.Ys_Firmalar.FirstOrDefaultAsync(x =>
                x.Id == dto.FirmaId.Value && !x.SilindiMi && x.AktifMi);
            if (secilenFirma == null || secilenFirma.SirketId != dto.SirketId)
                return (false, ApiIslemSonuc.Basarisiz("Secilen yetkili servis bulunamadi veya farkli bir sirkete bagli."));

            if (await _context.Users.AnyAsync(x => x.FirmaId == secilenFirma.Id
                && x.ArsivlemeTarihi == null
                && (x.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis
                    || x.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma)))
                return (false, ApiIslemSonuc.Basarisiz("Bu firmaya ait bir giris hesabi zaten var. Kullanicilar ekranindan duzenleyin."));
        }

        var kullaniciAdi = !string.IsNullOrWhiteSpace(secilenFirma?.VergiNo) ? secilenFirma.VergiNo.Trim() : email;
        var oncekiHesap = await _userManager.FindByNameAsync(kullaniciAdi);
        if (secilenFirma != null && oncekiHesap?.ArsivlemeTarihi != null && oncekiHesap.FirmaId == secilenFirma.Id)
            kullaniciAdi = email;

        var yeni = new AppKullanici
        {
            UserName = kullaniciAdi,
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
            return (false, ApiIslemSonuc.Basarisiz(string.Join(", ", createSonuc.Errors.Select(x => x.Description))));

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
                return (false, ApiIslemSonuc.Basarisiz("Firma kullanicisi kaydi olusturulurken hata olustu. Lutfen tekrar deneyin."));
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

                return (false, ApiIslemSonuc.Basarisiz("Yetkili Servis rolu sistemde bulunamadi."));
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

            return (false, ApiIslemSonuc.Basarisiz($"Rol bulunamadi: {atanacakRol}"));
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

            return (false, ApiIslemSonuc.Basarisiz(string.Join(", ", rolSonuc.Errors.Select(x => x.Description))));
        }

        if (rol == "GenelSistemAdmin")
            await _userManager.AddToRoleAsync(yeni, KullaniciRolAdlari.EskiSuperAdmin);

        _logger.LogInformation("Admin kullanici olusturdu. YapanId: {YapanId}, YeniKullaniciId: {YeniKullaniciId}, Rol: {Rol}", kullanici.Id, yeni.Id, rol);
        return (false, ApiIslemSonuc.BasariliSonuc("Kullanici basariyla olusturuldu."));
    }

    public async Task<(bool Yetkisiz, ApiIslemSonuc? Sonuc)> KullaniciDurumAsync(
        AdminKullaniciDurumDto? dto, AppKullanici kullanici, int? sirketId, bool genelSistemAdmin)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
            return (false, ApiIslemSonuc.Basarisiz("Kullanici id zorunludur."));

        var hedef = await _context.Users.FirstOrDefaultAsync(x => x.Id == dto.Id && x.ArsivlemeTarihi == null);
        if (hedef == null || (dto.SadecePersonel && hedef.KullaniciTipi != KullaniciTipiDegerleri.Personel))
            return (false, ApiIslemSonuc.Basarisiz(dto.SadecePersonel ? "Personel bulunamadi." : "Kullanici bulunamadi."));

        if (!await KullaniciKapsamindaMi(kullanici, hedef, sirketId, genelSistemAdmin))
            return (true, null);

        if (!genelSistemAdmin &&
            (hedef.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin || hedef.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin))
            return (false, ApiIslemSonuc.Basarisiz("Sirket admini genel sistem admini veya sirket admini hesabinin durumunu degistiremez."));

        if (kullanici.Id == hedef.Id && !dto.AktifMi)
            return (false, ApiIslemSonuc.Basarisiz("Kendi hesabinizi pasiflestiremezsiniz."));

        hedef.AktifMi = dto.AktifMi;
        var sonuc = await _userManager.UpdateAsync(hedef);
        if (!sonuc.Succeeded)
            return (false, ApiIslemSonuc.Basarisiz(string.Join(", ", sonuc.Errors.Select(x => x.Description))));

        _logger.LogInformation("Admin kullanici durumunu degistirdi. YapanId: {YapanId}, HedefId: {HedefId}, AktifMi: {AktifMi}", kullanici.Id, hedef.Id, dto.AktifMi);
        return (false, ApiIslemSonuc.BasariliSonuc(dto.AktifMi ? "Kullanici aktif edildi." : "Kullanici pasiflestirildi."));
    }

    public async Task<(bool Yetkisiz, ApiIslemSonuc? Sonuc)> KullaniciSilAsync(
        AdminKullaniciSilDto? dto, AppKullanici kullanici, int? sirketId, bool genelSistemAdmin)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
            return (false, ApiIslemSonuc.Basarisiz("Kullanici id zorunludur."));

        var hedef = await _context.Users.FirstOrDefaultAsync(x => x.Id == dto.Id && x.ArsivlemeTarihi == null);
        if (hedef == null || (dto.SadecePersonel && hedef.KullaniciTipi != KullaniciTipiDegerleri.Personel))
            return (false, ApiIslemSonuc.Basarisiz(dto.SadecePersonel ? "Personel bulunamadi." : "Kullanici bulunamadi."));

        if (!await KullaniciKapsamindaMi(kullanici, hedef, sirketId, genelSistemAdmin))
            return (true, null);

        if (!genelSistemAdmin &&
            (hedef.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin || hedef.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin))
            return (false, ApiIslemSonuc.Basarisiz("Şirket yöneticisi, genel sistem veya şirket yöneticisi hesabını arşivleyemez."));

        if (kullanici.Id == hedef.Id)
            return (false, ApiIslemSonuc.Basarisiz("Kendi hesabınızı arşivleyemezsiniz."));

        hedef.AktifMi = false;
        hedef.ArsivlemeTarihi = DateTime.UtcNow;
        hedef.ArsivleyenKullaniciId = kullanici.Id;
        // Gecmisteki islemler ve yetki kayitlari korunur; mevcut oturumlar gecersizlesir.
        hedef.SecurityStamp = Guid.NewGuid().ToString();
        var sonuc = await _userManager.UpdateAsync(hedef);
        if (!sonuc.Succeeded)
            return (false, ApiIslemSonuc.Basarisiz(string.Join(", ", sonuc.Errors.Select(x => x.Description))));

        _logger.LogInformation("Admin kullanici arsivledi. YapanId: {YapanId}, HedefId: {HedefId}, SadecePersonel: {SadecePersonel}", kullanici.Id, hedef.Id, dto.SadecePersonel);
        return (false, ApiIslemSonuc.BasariliSonuc(dto.SadecePersonel ? "Personel arşivlendi. İşlem ve yetki geçmişi korundu." : "Kullanıcı arşivlendi. İşlem ve yetki geçmişi korundu."));
    }

    public async Task<(bool Yetkisiz, ApiIslemSonuc? Sonuc)> PersonelEkleAsync(
        AdminPersonelKaydetDto? dto, AppKullanici kullanici, int? sirketId, bool genelSistemAdmin)
    {
        if (dto == null)
            return (false, ApiIslemSonuc.Basarisiz("Personel bilgileri zorunludur."));

        if (dto.SirketId <= 0)
            return (false, ApiIslemSonuc.Basarisiz("Personel icin sirket secilmelidir."));

        if (!await SirketYonetimKapsamindaMi(kullanici, dto.SirketId, sirketId, genelSistemAdmin))
            return (true, null);

        if (string.IsNullOrWhiteSpace(dto.AdSoyad))
            return (false, ApiIslemSonuc.Basarisiz("Ad soyad zorunludur."));

        if (string.IsNullOrWhiteSpace(dto.Email))
            return (false, ApiIslemSonuc.Basarisiz("E-posta zorunludur."));

        if (!CepTelefonuKurali.GecerliMi(dto.Telefon))
            return (false, ApiIslemSonuc.Basarisiz("Telefon numarasi 05XXXXXXXXX veya 90XXXXXXXXXX formatinda olmalidir."));

        var sifreHatalari = ValidatePassword(dto.Sifre);
        if (sifreHatalari.Count > 0)
            return (false, ApiIslemSonuc.Basarisiz(string.Join(" ", sifreHatalari)));

        var email = dto.Email.Trim();
        var mevcut = await _userManager.FindByEmailAsync(email);
        if (mevcut != null)
            return (false, ApiIslemSonuc.Basarisiz(mevcut.ArsivlemeTarihi.HasValue
                ? "Bu e-posta arşivlenmiş bir hesaba aittir. İşlem geçmişini korumak için yeni hesapta farklı bir e-posta adresi kullanın."
                : "Bu e-posta ile kayitli bir kullanici zaten var."));

        var yeni = new AppKullanici
        {
            UserName = email,
            Email = email,
            PhoneNumber = dto.Telefon?.Trim(),
            AdSoyad = dto.AdSoyad.Trim(),
            KullaniciTipi = KullaniciTipiDegerleri.Personel,
            SirketId = dto.SirketId,
            AktifMi = true,
            EmailConfirmed = true
        };

        var sonuc = await _userManager.CreateAsync(yeni, dto.Sifre ?? string.Empty);
        if (!sonuc.Succeeded)
            return (false, ApiIslemSonuc.Basarisiz(string.Join(", ", sonuc.Errors.Select(x => x.Description))));

        var rolSonuc = await _userManager.AddToRoleAsync(yeni, "Personel");
        if (!rolSonuc.Succeeded)
        {
            await _userManager.DeleteAsync(yeni);
            return (false, ApiIslemSonuc.Basarisiz(string.Join(", ", rolSonuc.Errors.Select(x => x.Description))));
        }

        _logger.LogInformation("Admin personel olusturdu. YapanId: {YapanId}, YeniKullaniciId: {YeniKullaniciId}", kullanici.Id, yeni.Id);
        return (false, ApiIslemSonuc.BasariliSonuc("Personel basariyla olusturuldu."));
    }

    private async Task<string?> YetkiliServisRolAdiAsync()
    {
        var tumRoller = await _context.Set<IdentityRole>()
            .Select(r => r.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToListAsync();

        var adaylar = new[] { "YetkiliServis", "SERVIS", "Servis" };

        foreach (var aday in adaylar)
        {
            var eslesen = tumRoller.FirstOrDefault(r =>
                string.Equals(r, aday, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(eslesen))
                return eslesen;
        }

        return tumRoller.FirstOrDefault(r =>
            r!.Contains("yetkili", StringComparison.OrdinalIgnoreCase) &&
            r.Contains("servis", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> KullaniciKapsamindaMi(AppKullanici yapan, AppKullanici hedef, int? sirketId, bool genelSistemAdminMi)
    {
        if (yapan.Id == hedef.Id)
            return true;


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

    private async Task<bool> SirketYonetimKapsamindaMi(AppKullanici yapan, int hedefSirketId, int? kapsamSirketId, bool genelSistemAdminMi)
    {

        if (genelSistemAdminMi && !kapsamSirketId.HasValue)
            return true;

        if (kapsamSirketId.HasValue)
            return hedefSirketId == kapsamSirketId.Value;

        if (yapan.SirketId == hedefSirketId)
            return true;

        return await _context.Dag_PersonelYetkiler.AnyAsync(x =>
            x.KullaniciId == yapan.Id &&
            !x.SilindiMi &&
            x.SirketId == hedefSirketId &&
            (x.YetkiTipi == YetkiTipleri.TAM_YETKI || x.YetkiTipi == YetkiTipleri.KULLANICI_YONET));
    }

    private static List<string> ValidatePassword(string? sifre)
    {
        var hatalar = new List<string>();
        if (string.IsNullOrWhiteSpace(sifre))
        {
            hatalar.Add("Sifre zorunludur.");
            return hatalar;
        }

        if (sifre.Length < 6)
            hatalar.Add("Sifre en az 6 karakter olmalidir.");

        if (!sifre.Any(char.IsLower))
            hatalar.Add("Sifre en az bir kucuk harf icermelidir.");

        if (!sifre.Any(char.IsDigit))
            hatalar.Add("Sifre en az bir rakam icermelidir.");

        return hatalar;
    }
}
