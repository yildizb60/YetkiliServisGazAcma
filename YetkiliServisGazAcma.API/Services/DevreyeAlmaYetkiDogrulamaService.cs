using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class DevreyeAlmaYetkiDogrulamaService(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<bool> GecerliYetkiBelgesiVarAsync(int firmaId)
    {
        var bugun = DateTime.Now.Date;
        return await _context.Ys_YetkiBelgeleri
            .AnyAsync(x => x.FirmaId == firmaId
                && !x.SilindiMi
                && x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi
                && (!x.YetkiBelgesiBaslangicTarihi.HasValue || x.YetkiBelgesiBaslangicTarihi.Value.Date <= bugun)
                && x.YetkiBelgesiBitisTarihi.Date >= bugun);
    }

    public async Task<bool> OnayliYetkiBelgesiVarAsync(int firmaId)
    {
        return await _context.Ys_YetkiBelgeleri
            .AnyAsync(x => x.FirmaId == firmaId
                && !x.SilindiMi
                && x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi);
    }

    public async Task<bool> FirmaMarkaYetkisiVarAsync(int firmaId, int markaId)
    {
        var bugun = DateTime.Now.Date;
        return await _context.Ys_FirmaMarkalar
            .Include(x => x.Marka)
            .AnyAsync(x => x.FirmaId == firmaId
                && x.MarkaId == markaId
                && !x.SilindiMi
                && x.YetkiBitisTarihi.Date >= bugun
                && x.Marka != null
                && x.Marka.AktifMi
                && !x.Marka.SilindiMi);
    }

    public async Task<Ys_Marka?> MarkaBulAsync(string? cihazMarka)
    {
        var aranan = NormalizeMarka(cihazMarka);
        if (string.IsNullOrWhiteSpace(aranan))
            return null;

        var markalar = await _context.Ys_Markalar
            .Where(x => !x.SilindiMi && x.AktifMi)
            .ToListAsync();

        return markalar.FirstOrDefault(x => NormalizeMarka(x.MarkaAdi) == aranan);
    }

    public async Task<bool> FirmaKategoriYetkisiVarAsync(int firmaId, string cihazTipi)
    {
        var bugun = DateTime.Today;
        var kategoriler = await _context.Ys_FirmaKategoriler
            .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.YetkiBitisTarihi >= bugun
                && x.Kategori != null && !x.Kategori.SilindiMi && x.Kategori.AktifMi)
            .Select(x => x.Kategori!.Ad).ToListAsync();
        var aranan = NormalizeMarka(cihazTipi);
        return aranan.Length > 0 && kategoriler.Any(x => NormalizeMarka(x) == aranan);
    }

    private static string NormalizeMarka(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var normalized = value.Trim()
            .ToUpper(new CultureInfo("tr-TR"))
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
        }

        return builder.ToString();
    }
}
