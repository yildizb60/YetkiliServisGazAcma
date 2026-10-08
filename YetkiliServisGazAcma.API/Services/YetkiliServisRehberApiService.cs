using System.Globalization;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services
{
    public sealed class YetkiliServisRehberApiService(AppDbContext context)
    {
        private static readonly CultureInfo TrCulture = new("tr-TR");
        private readonly AppDbContext _context = context;

        public async Task<YetkiliServisRehberEkranDto> EkranAsync(YetkiliServisFiltreDto? dto)
        {
            var filtreler = await FiltreSecenekleriAsync(dto?.Il);
            var sorgu = new YetkiliServisFiltreDto
            {
                Il = dto?.Il,
                Ilce = dto?.Ilce,
                MarkaId = dto?.MarkaId,
                SirketId = dto?.SirketId,
                KategoriId = filtreler.Kategoriler.Any(x => x.Id == dto?.KategoriId) ? dto?.KategoriId : null,
                Q = dto?.Q,
                Page = Math.Max(dto?.Page ?? 1, 1),
                PageSize = Math.Clamp(dto?.PageSize ?? 20, 1, 100)
            };

            var sonuc = await ListeleAsync(sorgu);
            foreach (var firma in sonuc.Items)
                firma.Kategoriler = firma.Kategoriler.OrderBy(x => x.Ad).ToList();

            return new YetkiliServisRehberEkranDto
            {
                Sorgu = sorgu,
                Filtreler = filtreler,
                Sonuc = sonuc
            };
        }

        public async Task<YetkiliServisSayfaliDto> ListeleAsync(YetkiliServisFiltreDto? dto)
        {
            var il = dto?.Il;
            var ilce = dto?.Ilce;
            var markaId = dto?.MarkaId;
            var kategoriId = dto?.KategoriId;
            var sirketId = dto?.SirketId;
            var q = dto?.Q;
            var page = Math.Max(dto?.Page ?? 1, 1);
            var pageSize = Math.Clamp(dto?.PageSize ?? 20, 1, 100);

            var query = RehberServisleri()
                .Include(x => x.FirmaMarkalar!)
                    .ThenInclude(x => x.Marka)
                .Include(x => x.FirmaKategoriler!)
                    .ThenInclude(x => x.Kategori)
                .Include(x => x.Subeler!)
                .Include(x => x.Sirket)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(il))
                query = query.Where(x =>
                    x.FaaliyetIli == il ||
                    (x.Subeler != null && x.Subeler.Any(s => !s.SilindiMi && s.AktifMi && s.Il == il)));

            if (!string.IsNullOrWhiteSpace(ilce))
                query = query.Where(x =>
                    x.Subeler != null && x.Subeler.Any(s => !s.SilindiMi && s.AktifMi && s.Ilce == ilce));

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(x =>
                    (x.FirmaAdi ?? "").Contains(q) ||
                    (x.YetkiliKisi ?? "").Contains(q));

            if (markaId.HasValue)
                query = query.Where(x => x.FirmaMarkalar!.Any(m => !m.SilindiMi && m.MarkaId == markaId.Value));

            if (kategoriId.HasValue)
                query = query.Where(x => x.FirmaKategoriler!.Any(k => !k.SilindiMi && k.KategoriId == kategoriId.Value && k.Kategori != null && !k.Kategori.SilindiMi && k.Kategori.AktifMi));

            if (sirketId.HasValue)
                query = query.Where(x => x.SirketId == sirketId.Value);

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(x => x.FirmaAdi)
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .Select(x => new YetkiliServisDto
                {
                    Id = x.Id,
                    FirmaAdi = x.FirmaAdi,
                    YetkiliKisi = x.YetkiliKisi,
                    Telefon = x.Telefon,
                    Email = x.Email,
                    Adres = x.Adres,
                    FaaliyetIli = x.FaaliyetIli,
                    Ilce = x.Subeler!
                        .Where(s => !s.SilindiMi && s.AktifMi && s.Ilce != null && s.Ilce != "")
                        .Select(s => s.Ilce)
                        .FirstOrDefault(),
                    SirketId = x.SirketId,
                    SirketAdi = x.Sirket != null ? x.Sirket.SirketAdi : null,
                    Markalar = x.FirmaMarkalar!
                        .Where(m => !m.SilindiMi)
                        .Select(m => m.Marka!.MarkaAdi)
                        .Where(m => m != null)
                        .Select(m => m!)
                        .Distinct()
                        .ToList(),
                    Kategoriler = x.FirmaKategoriler!
                        .Where(k => !k.SilindiMi && k.Kategori != null && !k.Kategori.SilindiMi && k.Kategori.AktifMi)
                        .Select(k => new UrunKategoriApiDto
                        {
                            Id = k.Kategori!.Id,
                            Ad = k.Kategori.Ad,
                            IconUrl = k.Kategori.IconUrl
                        })
                        .GroupBy(k => k.Id)
                        .Select(g => g.First())
                        .ToList()
                })
                .ToListAsync();

            return new YetkiliServisSayfaliDto
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = items
            };
        }

        public async Task<YetkiliServisFiltreSecenekleriDto> FiltreSecenekleriAsync(string? il)
        {
            var rehberServisleri = RehberServisleri();
            var markalar = await _context.Ys_Markalar
                .Where(x => !x.SilindiMi && x.AktifMi)
                .OrderBy(x => x.MarkaAdi)
                .Select(x => new YetkiliServisMarkaSecenekDto
                {
                    Id = x.Id,
                    MarkaAdi = x.MarkaAdi
                })
                .ToListAsync();

            var kategoriler = await _context.UrunKategoriler
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

            var illerRaw = await rehberServisleri
                .Where(x => x.FaaliyetIli != null && x.FaaliyetIli != "")
                .Select(x => x.FaaliyetIli!)
                .ToListAsync();

            var subeIllerRaw = await _context.Ys_Subeler
                .Where(x => !x.SilindiMi
                    && x.AktifMi
                    && x.Il != null
                    && x.Il != ""
                    && rehberServisleri.Any(f => f.Id == x.FirmaId))
                .Select(x => x.Il!)
                .ToListAsync();

            var dagitimIllerRaw = await _context.Dag_Sirketler
                .Where(x => x.AktifMi && x.Il != null && x.Il != "")
                .Select(x => x.Il!)
                .ToListAsync();

            var iller = illerRaw
                .Concat(subeIllerRaw)
                .Concat(dagitimIllerRaw)
                .Select(NormalizeKonum)
                .Where(GecerliKonumMu)
                .GroupBy(NormalizeKonumKey)
                .Select(g => TitleCaseTr(g.First()))
                .OrderBy(x => x)
                .ToList();

            var ilcelerQuery = _context.Ys_Subeler
                .Where(x => !x.SilindiMi
                    && x.AktifMi
                    && x.Ilce != null
                    && x.Ilce != ""
                    && rehberServisleri.Any(f => f.Id == x.FirmaId));

            if (!string.IsNullOrWhiteSpace(il))
                ilcelerQuery = ilcelerQuery.Where(x => x.Il == il);

            var ilceler = (await ilcelerQuery
                    .Select(x => x.Ilce!)
                    .ToListAsync())
                .Select(NormalizeKonum)
                .Where(GecerliKonumMu)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            return new YetkiliServisFiltreSecenekleriDto
            {
                Markalar = markalar,
                Kategoriler = kategoriler,
                Iller = iller,
                Ilceler = ilceler
            };
        }

        private IQueryable<Ys_Firma> RehberServisleri()
            => _context.Ys_Firmalar.AsNoTracking().Where(x => !x.SilindiMi && x.AktifMi
                && _context.Users.Any(u => u.FirmaId == x.Id
                    && u.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis));

        private static string NormalizeKonum(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var trimmed = value.Trim();
            while (trimmed.Contains("  ")) trimmed = trimmed.Replace("  ", " ");
            return trimmed;
        }

        private static bool GecerliKonumMu(string? value)
        {
            var norm = NormalizeKonum(value);
            return norm.Length >= 2 && norm.Any(char.IsLetter);
        }

        private static string NormalizeKonumKey(string? value)
        {
            var norm = NormalizeKonum(value);
            if (string.IsNullOrWhiteSpace(norm)) return "";
            var lower = norm.ToLower(TrCulture);
            var keyChars = lower.Where(char.IsLetterOrDigit).ToArray();
            return new string(keyChars);
        }

        private static string TitleCaseTr(string value)
        {
            var trimmed = NormalizeKonum(value);
            if (string.IsNullOrWhiteSpace(trimmed)) return "";
            var lower = trimmed.ToLower(TrCulture);
            return TrCulture.TextInfo.ToTitleCase(lower);
        }
    }
}
