using Microsoft.AspNetCore.Identity;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Infrastructure
{
    public static class SeedData
    {
        public static async Task Initialize(
            UserManager<AppKullanici> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            // 1. Rolleri oluştur
            string[] roller = { "GenelSistemAdmin", "SirketAdmin", "SuperAdmin", "Personel", "YetkiliServis" };
            foreach (var rol in roller)
            {
                if (!await roleManager.RoleExistsAsync(rol))
                    await roleManager.CreateAsync(new IdentityRole(rol));
            }

            // 2. Süper Admin
            string adminEmail = "admin@corumgaz.com";
            var mevcutAdmin = await userManager.FindByEmailAsync(adminEmail);
            if (mevcutAdmin == null)
            {
                var admin = new AppKullanici
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    AdSoyad = "Sistem Yöneticisi",
                    KullaniciTipi = KullaniciTipiDegerleri.GenelSistemAdmin,
                    AktifMi = true,
                    EmailConfirmed = true
                };
                var sonuc = await userManager.CreateAsync(admin, "Admin123!");
                if (sonuc.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "GenelSistemAdmin");
                    await userManager.AddToRoleAsync(admin, "SuperAdmin");
                }
            }
            else if (mevcutAdmin.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !mevcutAdmin.SirketId.HasValue)
            {
                mevcutAdmin.KullaniciTipi = KullaniciTipiDegerleri.GenelSistemAdmin;
                await userManager.UpdateAsync(mevcutAdmin);

                if (!await userManager.IsInRoleAsync(mevcutAdmin, "GenelSistemAdmin"))
                    await userManager.AddToRoleAsync(mevcutAdmin, "GenelSistemAdmin");
            }

            // 3. Test Personel
            // Not: SirketId = 3 (Kargaz) — veritabanındaki mevcut Sirket
            // İleride Sirket kodları eklenince burası güncellenecek
            string personelEmail = "personel@corumgaz.com";
            if (await userManager.FindByEmailAsync(personelEmail) == null)
            {
                var personel = new AppKullanici
                {
                    UserName = personelEmail,
                    Email = personelEmail,
                    AdSoyad = "Test Personel",
                    KullaniciTipi = KullaniciTipiDegerleri.Personel,
                    SirketId = 3, // Kargaz (Id=3)
                    AktifMi = true,
                    EmailConfirmed = true
                };
                var sonuc = await userManager.CreateAsync(personel, "Personel123!");
                if (sonuc.Succeeded)
                    await userManager.AddToRoleAsync(personel, "Personel");
            }
        }
    }
}
