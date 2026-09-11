using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services
{
    public class SehirFirmaKoduService(IConfiguration configuration, AppDbContext context) : SehirFirmaKodlari(configuration)
    {
        private readonly AppDbContext _context = context;
        public async Task<int> SirketIdBulVeyaOlustur(string? sehir, string? kullanici)
        {
            var temizSehir = string.IsNullOrWhiteSpace(sehir) ? "Genel" : sehir.Trim();
            var firmaKodu = FirmaKodu(temizSehir) ?? temizSehir.ToUpperInvariant().Replace(" ", "_");

            var mevcut = await _context.Dag_Sirketler
                .FirstOrDefaultAsync(x => !x.SilindiMi
                    && ((x.Il != null && x.Il == temizSehir)
                        || (x.SirketAdi != null && x.SirketAdi == firmaKodu)));

            if (mevcut != null)
            {
                if (!mevcut.AktifMi)
                    mevcut.AktifMi = true;

                if (string.IsNullOrWhiteSpace(mevcut.Il))
                    mevcut.Il = temizSehir;

                if (string.IsNullOrWhiteSpace(mevcut.SirketAdi))
                    mevcut.SirketAdi = firmaKodu;

                await _context.SaveChangesAsync();
                return mevcut.Id;
            }

            var yeni = new Dag_Sirket
            {
                SirketAdi = firmaKodu,
                Il = temizSehir,
                AktifMi = true,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici ?? "sistem",
                SilindiMi = false
            };

            _context.Dag_Sirketler.Add(yeni);
            await _context.SaveChangesAsync();
            return yeni.Id;
        }
    }
}

