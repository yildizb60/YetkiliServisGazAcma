using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services
{
    public class MarkaService
    {
        private readonly AppDbContext _context;

        public MarkaService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Ys_Marka>> TumunuGetir()
        {
            return await _context.Ys_Markalar
                .Where(x => !x.SilindiMi)
                .OrderBy(x => x.MarkaAdi)
                .ToListAsync();
        }

        public async Task<Ys_Marka?> IdIleGetir(int id)
        {
            return await _context.Ys_Markalar
                .FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
        }

        public async Task<bool> Ekle(Ys_Marka marka, string? kullanici)
        {
            marka.OlusturmaTarihi = DateTime.Now;
            marka.OlusturanKullanici = kullanici ?? "sistem";
            marka.SilindiMi = false;
            marka.AktifMi = true;

            _context.Ys_Markalar.Add(marka);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> Guncelle(Ys_Marka marka, string? kullanici)
        {
            var mevcut = await IdIleGetir(marka.Id);
            if (mevcut == null) return false;

            mevcut.MarkaAdi = marka.MarkaAdi;
            mevcut.Aciklama = marka.Aciklama;
            mevcut.AktifMi = marka.AktifMi;
            mevcut.GuncellemeTarihi = DateTime.Now;
            mevcut.GuncelleyenKullanici = kullanici ?? "sistem";

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> KullaniliyorMu(int id)
        {
            var devreyeAlmaVar = await _context.Ys_DevreyeAlmalar
                .AnyAsync(x => !x.SilindiMi && x.MarkaId == id);

            if (devreyeAlmaVar)
                return true;

            return await _context.Ys_FirmaMarkalar
                .AnyAsync(x => !x.SilindiMi && x.MarkaId == id);
        }

        public async Task<bool> Sil(int id, string? kullanici)
        {
            var mevcut = await IdIleGetir(id);
            if (mevcut == null) return false;

            if (await KullaniliyorMu(id))
                return false;

            mevcut.SilindiMi = true;
            mevcut.SilinmeTarihi = DateTime.Now;
            mevcut.SilenKullanici = kullanici ?? "sistem";

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
