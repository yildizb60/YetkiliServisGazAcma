using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services;

public sealed class YetkiliServisIlkKurulumService(AppDbContext context)
{
    public async Task<(bool zorunluMu, bool tamamlandiMi, List<string> eksikler)> GetirAsync(int firmaId)
    {
        var bugun = DateTime.Today;
        var firma = await context.Ys_Firmalar.AsNoTracking()
            .Where(x => x.Id == firmaId && !x.SilindiMi)
            .Select(x => new
            {
                x.OlusturmaTipi,
                MarkaVar = x.FirmaMarkalar!.Any(m => !m.SilindiMi && m.YetkiBitisTarihi >= bugun
                    && m.Marka != null && !m.Marka.SilindiMi && m.Marka.AktifMi),
                KategoriVar = x.FirmaKategoriler!.Any(k => !k.SilindiMi && k.YetkiBitisTarihi >= bugun
                    && k.Kategori != null && !k.Kategori.SilindiMi && k.Kategori.AktifMi),
                SubeVar = x.Subeler!.Any(s => !s.SilindiMi && s.AktifMi),
                YetkiBelgesiVar = x.YetkiBelgeleri!.Any(b => !b.SilindiMi)
            })
            .FirstOrDefaultAsync();

        if (firma == null)
            return (true, false, new List<string> { "Firma kaydi" });
        return Degerlendir(firma.OlusturmaTipi, firma.MarkaVar, firma.KategoriVar,
            firma.SubeVar, firma.YetkiBelgesiVar);
    }

    public static (bool zorunluMu, bool tamamlandiMi, List<string> eksikler) Degerlendir(
        byte olusturmaTipi, bool markaVar, bool kategoriVar, bool subeVar, bool yetkiBelgesiVar)
    {
        if (olusturmaTipi == YetkiliServisOlusturmaTipleri.Kayit)
            return (false, true, new List<string>());

        var eksikler = new List<string>();
        if (!markaVar) eksikler.Add("Gecerli marka yetkisi");
        if (!kategoriVar) eksikler.Add("Gecerli kategori yetkisi");
        if (!subeVar) eksikler.Add("Aktif sube kaydi");
        if (!yetkiBelgesiVar) eksikler.Add("Yetki belgesi yukleme");
        return (true, eksikler.Count == 0, eksikler);
    }
}
