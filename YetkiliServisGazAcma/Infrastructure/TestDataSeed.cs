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
                "Demo Çorumgaz Şirket Admini",
                "905551000002",
                3,
                corumgaz.Id,
                null,
                new[] { KullaniciRolAdlari.SirketAdmin });

            var personel = await KullaniciOlusturVeyaGuncelle(
                userManager,
                "test.personel@demo.com",
                "Demo Çok Şirketli Personel",
                "905551000003",
                2,
                corumgaz.Id,
                null,
                new[] { KullaniciRolAdlari.Personel });

            var firma = await YetkiliServisFirmasiBulVeyaOlustur(context, corumgaz);
            await DemoServisIlkKurulumuHazirla(context, firma);
            await KullaniciOlusturVeyaGuncelle(
                userManager,
                "test.servis@demo.com",
                "Demo Yetkili Servis",
                "905551000004",
                1,
                corumgaz.Id,
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
                    SirketId = corumgaz.Id,
                    YetkiTipi = YetkiTipleri.TAM_YETKI,
                    OlusturanKullanici = "demo-seed"
                },
                new Dag_PersonelYetki
                {
                    KullaniciId = personel.Id,
                    SirketId = kargaz.Id,
                    YetkiTipi = YetkiTipleri.RAPOR_GOR,
                    OlusturanKullanici = "demo-seed"
                },
                new Dag_PersonelYetki
                {
                    KullaniciId = personel.Id,
                    SirketId = surmeligaz.Id,
                    YetkiTipi = YetkiTipleri.YETKI_BELGESI_ONAY,
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
                firma.VergiDairesi = "Demo";
                firma.FaaliyetIli = sirket.Il;
                firma.SirketId = sirket.Id;
                firma.AktifMi = true;
            }

            await context.SaveChangesAsync();
            return firma;
        }

        private static async Task DemoServisIlkKurulumuHazirla(AppDbContext context, Ys_Firma firma)
        {
            var markaAdlari = new[] { "Arçelik", "Baymak", "Vaillant", "Bosch" };
            foreach (var markaAdi in markaAdlari)
            {
                var marka = await MarkaBulVeyaOlustur(context, markaAdi);
                var bagVar = await context.Ys_FirmaMarkalar.AnyAsync(x =>
                    x.FirmaId == firma.Id &&
                    x.MarkaId == marka.Id &&
                    !x.SilindiMi);

                if (!bagVar)
                {
                    context.Ys_FirmaMarkalar.Add(new Ys_FirmaMarka
                    {
                        FirmaId = firma.Id,
                        MarkaId = marka.Id,
                        YetkiBitisTarihi = DateTime.Now.AddYears(5),
                        OlusturanKullanici = "demo-seed"
                    });
                }
            }

            var kategori = await KategoriBulVeyaOlustur(context, "Kombi", 1);
            var kategoriBagVar = await context.Ys_FirmaKategoriler.AnyAsync(x =>
                x.FirmaId == firma.Id &&
                x.KategoriId == kategori.Id &&
                !x.SilindiMi);

            if (!kategoriBagVar)
            {
                context.Ys_FirmaKategoriler.Add(new Ys_FirmaKategori
                {
                    FirmaId = firma.Id,
                    KategoriId = kategori.Id,
                    YetkiBitisTarihi = DateTime.Now.AddYears(5),
                    OlusturanKullanici = "demo-seed"
                });
            }

            var subeVar = await context.Ys_Subeler.AnyAsync(x => x.FirmaId == firma.Id && !x.SilindiMi);
            if (!subeVar)
            {
                context.Ys_Subeler.Add(new Ys_Sube
                {
                    FirmaId = firma.Id,
                    SubeAdi = "Çorum Merkez Şube",
                    Il = "Çorum",
                    Ilce = "Merkez",
                    Telefon = "05551000004",
                    Adres = "Demo yetkili servis şubesi",
                    AktifMi = true,
                    OlusturanKullanici = "demo-seed"
                });
            }

            var bugun = DateTime.Now.Date;
            var gecerliYetkiBelgesi = await context.Ys_YetkiBelgeleri.FirstOrDefaultAsync(x =>
                x.FirmaId == firma.Id &&
                !x.SilindiMi &&
                x.Durum == 1 &&
                (!x.YetkiBelgesiBaslangicTarihi.HasValue || x.YetkiBelgesiBaslangicTarihi.Value.Date <= bugun) &&
                x.YetkiBelgesiBitisTarihi.Date >= bugun);

            if (gecerliYetkiBelgesi != null)
            {
                gecerliYetkiBelgesi.DosyaYolu = "/uploads/demo-yetki-belgesi.html";
                gecerliYetkiBelgesi.GuncellemeTarihi = DateTime.Now;
                gecerliYetkiBelgesi.GuncelleyenKullanici = "demo-seed";
            }
            else
            {
                context.Ys_YetkiBelgeleri.Add(new Ys_YetkiBelgesi
                {
                    FirmaId = firma.Id,
                    DosyaYolu = "/uploads/demo-yetki-belgesi.html",
                    YetkiBelgesiBaslangicTarihi = bugun.AddDays(-7),
                    YetkiBelgesiBitisTarihi = bugun.AddYears(1),
                    Durum = 1,
                    OnayTarihi = DateTime.Now,
                    OnaylayanKullanici = "demo-seed",
                    OlusturanKullanici = "demo-seed"
                });
            }

            await context.SaveChangesAsync();
        }

        private static async Task<Ys_Marka> MarkaBulVeyaOlustur(AppDbContext context, string markaAdi)
        {
            var normalized = Normalize(markaAdi);
            var marka = (await context.Ys_Markalar
                    .Where(x => !x.SilindiMi)
                    .ToListAsync())
                .FirstOrDefault(x => Normalize(x.MarkaAdi) == normalized);

            if (marka != null)
            {
                marka.AktifMi = true;
                await context.SaveChangesAsync();
                return marka;
            }

            marka = new Ys_Marka
            {
                MarkaAdi = markaAdi,
                AktifMi = true,
                OlusturanKullanici = "demo-seed"
            };
            context.Ys_Markalar.Add(marka);
            await context.SaveChangesAsync();
            return marka;
        }

        private static async Task<UrunKategori> KategoriBulVeyaOlustur(AppDbContext context, string kategoriAdi, int siraNo)
        {
            var normalized = Normalize(kategoriAdi);
            var kategori = (await context.UrunKategoriler
                    .Where(x => !x.SilindiMi)
                    .ToListAsync())
                .FirstOrDefault(x => Normalize(x.Ad) == normalized);

            if (kategori != null)
            {
                kategori.AktifMi = true;
                await context.SaveChangesAsync();
                return kategori;
            }

            kategori = new UrunKategori
            {
                Ad = kategoriAdi,
                SiraNo = siraNo,
                AktifMi = true,
                OlusturanKullanici = "demo-seed"
            };
            context.UrunKategoriler.Add(kategori);
            await context.SaveChangesAsync();
            return kategori;
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
            await userManager.SetLockoutEndDateAsync(kullanici, null);
            await userManager.ResetAccessFailedCountAsync(kullanici);

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
