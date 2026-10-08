using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

internal static class FirmaYetkiIliskileri
{
    public static async Task<bool> GecerliMiAsync(AppDbContext db, List<int>? kategoriIds, List<int>? markaIds)
    {
        if (kategoriIds != null)
        {
            var ids = kategoriIds.Distinct().ToList();
            if (await db.UrunKategoriler.CountAsync(x => ids.Contains(x.Id) && !x.SilindiMi && x.AktifMi) != ids.Count)
                return false;
        }
        if (markaIds != null)
        {
            var ids = markaIds.Distinct().ToList();
            if (await db.Ys_Markalar.CountAsync(x => ids.Contains(x.Id) && !x.SilindiMi) != ids.Count)
                return false;
        }
        return true;
    }

    public static async Task GuncelleAsync(AppDbContext db, int firmaId, List<int>? kategoriIds,
        List<int>? markaIds, string kullanici)
    {
        var simdi = DateTime.Now;
        if (kategoriIds != null)
        {
            var mevcut = await db.Ys_FirmaKategoriler.Where(x => x.FirmaId == firmaId && !x.SilindiMi).ToListAsync();
            Uzlastir(mevcut, kategoriIds, x => x.KategoriId, id => new Ys_FirmaKategori
            {
                FirmaId = firmaId, KategoriId = id, YetkiBitisTarihi = simdi.AddYears(1),
                OlusturmaTarihi = simdi, OlusturanKullanici = kullanici
            }, x => db.Ys_FirmaKategoriler.Add(x), kullanici, simdi);
        }
        if (markaIds != null)
        {
            var mevcut = await db.Ys_FirmaMarkalar.Where(x => x.FirmaId == firmaId && !x.SilindiMi).ToListAsync();
            Uzlastir(mevcut, markaIds, x => x.MarkaId, id => new Ys_FirmaMarka
            {
                FirmaId = firmaId, MarkaId = id, YetkiBitisTarihi = simdi.AddYears(1),
                OlusturmaTarihi = simdi, OlusturanKullanici = kullanici
            }, x => db.Ys_FirmaMarkalar.Add(x), kullanici, simdi);
        }
    }

    private static void Uzlastir<T>(List<T> mevcut, List<int> secilen, Func<T, int> anahtar,
        Func<int, T> olustur, Action<T> ekle, string kullanici, DateTime simdi) where T : BaseEntity
    {
        var ids = secilen.ToHashSet();
        foreach (var kayit in mevcut.Where(x => !ids.Contains(anahtar(x))))
        {
            kayit.SilindiMi = true;
            kayit.SilinmeTarihi = simdi;
            kayit.SilenKullanici = kullanici;
        }
        // Unchanged grants keep their identifiers, expiry dates and creation history.
        foreach (var id in ids.Except(mevcut.Select(anahtar)))
            ekle(olustur(id));
    }
}
