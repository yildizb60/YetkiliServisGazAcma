using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services
{
    public sealed class YetkiliServisBasvuruApiService(
        AppDbContext context,
        YetkiliServisService yetkiliServisService,
        SehirFirmaKoduService sehirFirmaKoduService)
    {
        public async Task<YetkiliServisBasvuruSecenekleriDto> SeceneklerAsync()
        {
            var markalar = await context.Ys_Markalar.AsNoTracking()
                .Where(x => !x.SilindiMi && x.AktifMi)
                .OrderBy(x => x.MarkaAdi)
                .Select(x => new YetkiliServisMarkaSecenekDto { Id = x.Id, MarkaAdi = x.MarkaAdi })
                .ToListAsync();
            var kategoriler = await context.UrunKategoriler.AsNoTracking()
                .Where(x => !x.SilindiMi && x.AktifMi)
                .OrderBy(x => x.SiraNo)
                .ThenBy(x => x.Ad)
                .Select(x => new UrunKategoriApiDto
                {
                    Id = x.Id,
                    Ad = x.Ad,
                    IconUrl = x.IconUrl,
                    SiraNo = x.SiraNo,
                    AktifMi = x.AktifMi
                })
                .ToListAsync();

            return new YetkiliServisBasvuruSecenekleriDto
            {
                Markalar = markalar,
                Kategoriler = kategoriler,
                Sehirler = sehirFirmaKoduService.Sehirler(),
                SehirFirmaKodlari = sehirFirmaKoduService.TumKodlar()
            };
        }

        public async Task<YetkiliServisKayitSonuc> KayitAsync(YetkiliServisBasvuruDto? dto)
        {
            var hata = DogrulamaHatasi(dto);
            if (hata != null)
                return new() { Mesaj = hata };

            var sirketId = await sehirFirmaKoduService.AktifSirketIdBulAsync(dto!.FaaliyetIli);
            if (!sirketId.HasValue)
                return new() { Mesaj = "Seçilen il için aktif dağıtım şirketi bulunamadı veya şirket eşleşmesi belirsiz. Lütfen sistem yöneticisiyle iletişime geçin." };

            var firma = new Ys_Firma
            {
                FirmaAdi = dto.FirmaAdi,
                YetkiliKisi = dto.YetkiliKisi,
                Telefon = dto.Telefon,
                Email = dto.Email,
                Adres = dto.Adres,
                FaaliyetIli = dto.FaaliyetIli,
                VergiNo = dto.VergiNo,
                VergiDairesi = dto.VergiDairesi,
                TcKimlikNo = dto.TcKimlikNo,
                SirketId = sirketId.Value
            };

            var sonuc = await yetkiliServisService.Kayit(
                firma,
                dto.Sifre,
                dto.MarkaIdleri ?? new List<int>(),
                dto.KategoriIdleri ?? new List<int>(),
                dto.Ilce);

            return new YetkiliServisKayitSonuc
            {
                Basarili = sonuc.basarili,
                Mesaj = sonuc.mesaj,
                FirmaId = sonuc.basarili ? firma.Id : null
            };
        }

        private static string? DogrulamaHatasi(YetkiliServisBasvuruDto? dto)
        {
            if (dto == null)
                return "Kayit bilgileri zorunludur";

            if (string.IsNullOrWhiteSpace(dto.FirmaAdi))
                return "Firma adi zorunludur";

            if (string.IsNullOrWhiteSpace(dto.VergiNo))
                return "VKN zorunludur";

            var vergiNoDigits = new string(dto.VergiNo.Where(char.IsAsciiDigit).ToArray());
            if (vergiNoDigits.Length is not (10 or 11))
                return "VKN/TCKN 10 veya 11 haneli olmalidir";

            if (string.IsNullOrWhiteSpace(dto.Sifre))
                return "Sifre zorunludur";

            if (dto.Sifre.Length < 6)
                return "Sifre en az 6 karakter olmalidir";

            if (string.IsNullOrWhiteSpace(dto.FaaliyetIli))
                return "Il bilgisi zorunludur";

            if (dto.Ilce?.Length > 100)
                return "Ilce en fazla 100 karakter olabilir";

            if (!string.IsNullOrWhiteSpace(dto.Email) && !new EmailAddressAttribute().IsValid(dto.Email))
                return "E-posta formati gecersiz";

            if (!CepTelefonuKurali.GecerliMi(dto.Telefon))
                return "Telefon numarasi 05XXXXXXXXX veya 90XXXXXXXXXX formatinda olmalidir";

            if (dto.TcKimlikNo == null || dto.TcKimlikNo.Length != 11
                || dto.TcKimlikNo.Any(ch => ch < '0' || ch > '9'))
            {
                return "TC kimlik no 11 haneli ve sayisal olmalidir";
            }

            return null;
        }
    }
}
