using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services
{
    public class DagitimSirketService
    {
        private readonly AppDbContext _context;

        public DagitimSirketService(AppDbContext context)
        {
            _context = context;
        }

        // Tüm aktif Sirketleri getir
        public async Task<List<Dag_Sirket>> TumunuGetir()
        {
            return await _context.Dag_Sirketler
                .Where(x => !x.SilindiMi)
                .OrderBy(x => x.SirketAdi)
                .ToListAsync();
        }

        // ID ile tek Sirket getir
        public async Task<Dag_Sirket?> IdIleGetir(int id)
        {
            return await _context.Dag_Sirketler
                .FirstOrDefaultAsync(x => x.Id == id && !x.SilindiMi);
        }

        // Yeni Sirket ekle
        public async Task<bool> Ekle(Dag_Sirket sirket, string? kullanici)
        {
            sirket.OlusturmaTarihi = DateTime.Now;
            sirket.OlusturanKullanici = kullanici ?? "sistem";
            sirket.SilindiMi = false;
            sirket.AktifMi = true;

            _context.Dag_Sirketler.Add(sirket);
            await _context.SaveChangesAsync();
            return true;
        }

        // Sirket güncelle
        public async Task<bool> Guncelle(Dag_Sirket sirket, string? kullanici)
        {
            var mevcut = await IdIleGetir(sirket.Id);
            if (mevcut == null) return false;

            mevcut.SirketAdi = sirket.SirketAdi;
            mevcut.Il = sirket.Il;
            mevcut.Telefon = sirket.Telefon;
            mevcut.Email = sirket.Email;
            mevcut.Adres = sirket.Adres;
            mevcut.AktifMi = sirket.AktifMi;
            mevcut.GuncellemeTarihi = DateTime.Now;
            mevcut.GuncelleyenKullanici = kullanici ?? "sistem";

            await _context.SaveChangesAsync();
            return true;
        }

        // Sirket sil (soft delete)
        public async Task<bool> Sil(int id, string? kullanici)
        {
            var mevcut = await IdIleGetir(id);
            if (mevcut == null) return false;

            mevcut.SilindiMi = true;
            mevcut.SilinmeTarihi = DateTime.Now;
            mevcut.SilenKullanici = kullanici ?? "sistem";

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
