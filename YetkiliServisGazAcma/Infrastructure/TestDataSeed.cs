using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Infrastructure
{
    public static class TestDataSeed
    {
        public const string DemoSifre = "Demo123!";

        public static async Task Initialize(
            AppDbContext context,
            UserManager<AppKullanici> userManager)
        {
            var corumgaz = await SirketBulVeyaOlustur(context, "CORUMGAZ", "Çorum");
            var kargaz = await SirketBulVeyaOlustur(context, "KARGAZ", "Kastamonu");
            var surmeligaz = await SirketBulVeyaOlustur(context, "SURMELIGAZ", "Yozgat");

            await KullaniciOlusturVeyaGuncelle(
                userManager,
                "test.geneladmin@demo.com",
                "Demo Genel Sistem Admini",
                "905551000001",
                4,
                null,
                null,
                new[] { KullaniciRolAdlari.GenelSistemAdmin, KullaniciRolAdlari.EskiSuperAdmin });

            await KullaniciOlusturVeyaGuncelle(
                userManager,
                "test.sirketadmin@demo.com",
                "Demo Kargaz Şirket Admini",
                "905551000002",
                3,
                kargaz.Id,
                null,
                new[] { KullaniciRolAdlari.SirketAdmin });

            var personel = await KullaniciOlusturVeyaGuncelle(
                userManager,
                "test.personel@demo.com",
                "Demo Çok Şirketli Personel",
                "905551000003",
                2,
                kargaz.Id,
                null,
                new[] { KullaniciRolAdlari.Personel });

            var firma = await YetkiliServisFirmasiBulVeyaOlustur(context, kargaz);
            await KullaniciOlusturVeyaGuncelle(
                userManager,
                "test.servis@demo.com",
                "Demo Yetkili Servis",
                "905551000004",
                1,
                kargaz.Id,
                firma.Id,
                new[] { KullaniciRolAdlari.YetkiliServis });

            var eskiYetkiler = await context.Dag_PersonelYetkiler
                .Where(x => x.KullaniciId == personel.Id)
                .ToListAsync();
            context.Dag_PersonelYetkiler.RemoveRange(eskiYetkiler);

            context.Dag_PersonelYetkiler.AddRange(
                new Dag_PersonelYetki
                {
                    KullaniciId = personel.Id,
                    SirketId = kargaz.Id,
                    YetkiTipi = YetkiTipleri.TAM_YETKI,
                    OlusturanKullanici = "demo-seed"
                },
                new Dag_PersonelYetki
                {
                    KullaniciId = personel.Id,
                    SirketId = corumgaz.Id,
                    YetkiTipi = YetkiTipleri.RAPOR_GOR,
                    OlusturanKullanici = "demo-seed"
                },
                new Dag_PersonelYetki
                {
                    KullaniciId = personel.Id,
                    SirketId = surmeligaz.Id,
                    YetkiTipi = YetkiTipleri.CERTIFIKA_ONAY,
                    OlusturanKullanici = "demo-seed"
                });

            await context.SaveChangesAsync();
        }

        private static async Task<Dag_Sirket> SirketBulVeyaOlustur(AppDbContext context, string sirketKodu, string il)
        {
            var sirketler = await context.Dag_Sirketler
                .Where(x => !x.SilindiMi)
                .ToListAsync();

            var sirket = sirketler.FirstOrDefault(x =>
                Normalize(x.SirketAdi).Contains(Normalize(sirketKodu)) ||
                Normalize(x.SirketAdi) == Normalize(sirketKodu));

            if (sirket != null)
            {
                if (!sirket.AktifMi)
                    sirket.AktifMi = true;

                if (string.IsNullOrWhiteSpace(sirket.Il))
                    sirket.Il = il;

                await context.SaveChangesAsync();
                return sirket;
            }

            sirket = new Dag_Sirket
            {
                SirketAdi = sirketKodu,
                Il = il,
                AktifMi = true,
                OlusturanKullanici = "demo-seed"
            };

            context.Dag_Sirketler.Add(sirket);
            await context.SaveChangesAsync();
            return sirket;
        }

        private static async Task<Ys_Firma> YetkiliServisFirmasiBulVeyaOlustur(AppDbContext context, Dag_Sirket sirket)
        {
            const string email = "test.servis@demo.com";
            const string vergiNo = "9990000001";

            var firma = await context.Ys_Firmalar
                .FirstOrDefaultAsync(x => !x.SilindiMi && (x.Email == email || x.VergiNo == vergiNo));

            if (firma == null)
            {
                firma = new Ys_Firma
                {
                    FirmaAdi = "Demo Yetkili Servis",
                    YetkiliKisi = "Demo Yetkili Servis",
                    Telefon = "05551000004",
                    Email = email,
                    VergiNo = vergiNo,
                    VergiDairesi = "Demo",
                    FaaliyetIli = sirket.Il,
                    SirketId = sirket.Id,
                    AktifMi = true,
                    OlusturanKullanici = "demo-seed"
                };

                context.Ys_Firmalar.Add(firma);
            }
            else
            {
                firma.FirmaAdi = "Demo Yetkili Servis";
                firma.YetkiliKisi = "Demo Yetkili Servis";
                firma.Telefon = "05551000004";
                firma.Email = email;
                firma.VergiNo = vergiNo;
                firma.SirketId = sirket.Id;
                firma.AktifMi = true;
            }

            await context.SaveChangesAsync();
            return firma;
        }

        private static async Task<AppKullanici> KullaniciOlusturVeyaGuncelle(
            UserManager<AppKullanici> userManager,
            string email,
            string adSoyad,
            string telefon,
            int kullaniciTipi,
            int? sirketId,
            int? firmaId,
            IEnumerable<string> roller)
        {
            var kullanici = await userManager.FindByEmailAsync(email);
            if (kullanici == null)
            {
                kullanici = new AppKullanici
                {
                    UserName = email,
                    Email = email
                };

                await userManager.CreateAsync(kullanici, DemoSifre);
            }

            kullanici.UserName = email;
            kullanici.Email = email;
            kullanici.NormalizedUserName = email.ToUpperInvariant();
            kullanici.NormalizedEmail = email.ToUpperInvariant();
            kullanici.AdSoyad = adSoyad;
            kullanici.PhoneNumber = telefon;
            kullanici.PhoneNumberConfirmed = true;
            kullanici.KullaniciTipi = kullaniciTipi;
            kullanici.SirketId = sirketId;
            kullanici.FirmaId = firmaId;
            kullanici.AktifMi = true;
            kullanici.EmailConfirmed = true;

            await userManager.UpdateAsync(kullanici);

            var token = await userManager.GeneratePasswordResetTokenAsync(kullanici);
            await userManager.ResetPasswordAsync(kullanici, token, DemoSifre);

            foreach (var rol in roller)
            {
                if (!await userManager.IsInRoleAsync(kullanici, rol))
                    await userManager.AddToRoleAsync(kullanici, rol);
            }

            return kullanici;
        }

        private static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Trim().ToUpper(new CultureInfo("tr-TR")).Normalize(NormalizationForm.FormD);
            var chars = normalized
                .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch))
                .ToArray();

            return new string(chars)
                .Replace("İ", "I")
                .Replace("Ş", "S")
                .Replace("Ğ", "G")
                .Replace("Ü", "U")
                .Replace("Ö", "O")
                .Replace("Ç", "C");
        }
    }
}
