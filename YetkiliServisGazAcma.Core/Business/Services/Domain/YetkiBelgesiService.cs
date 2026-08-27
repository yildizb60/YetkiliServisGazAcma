using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YetkiBelgesiService
    {
        private const string PrivateStoragePrefix = "private:yetki-belgeleri/";
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public YetkiBelgesiService(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<List<Ys_YetkiBelgesi>> FirmaninYetkiBelgeleri(int firmaId)
        {
            return await _context.Ys_YetkiBelgeleri
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ToListAsync();
        }

        public async Task<List<Ys_YetkiBelgesi>> OnayBekleyenler(int? sirketId = null)
        {
            var sorgu = _context.Ys_YetkiBelgeleri
                .Include(x => x.Firma)
                .ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi && x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor);

            if (sirketId.HasValue)
                sorgu = sorgu.Where(x => x.Firma!.SirketId == sirketId.Value);

            return await sorgu
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ToListAsync();
        }

        public async Task<(bool basarili, string mesaj)> Yukle(
            int firmaId,
            IFormFile dosya,
            DateTime bitisTarihi,
            DateTime? baslangicTarihi,
            string? kullanici,
            string? publicBaseUrl = null)
        {
            var baslangic = (baslangicTarihi ?? DateTime.Now.Date).Date;
            var bitis = bitisTarihi.Date;

            if (baslangic > bitis)
                return (false, "Yetki belgesi baslangic tarihi, bitis tarihinden buyuk olamaz.");

            if (dosya == null || dosya.Length == 0)
                return (false, "Lutfen bir dosya seciniz.");

            var izinliUzantilar = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var uzanti = Path.GetExtension(dosya.FileName).ToLowerInvariant();
            if (!izinliUzantilar.Contains(uzanti))
                return (false, "Sadece PDF, JPG veya PNG dosyasi yukleyebilirsiniz.");

            if (!await DosyaIcerigiGecerliMi(dosya, uzanti))
                return (false, "Yuklenen dosyanin icerigi PDF, JPG veya PNG formatinda degil.");

            var klasor = Path.Combine(PrivateYetkiBelgesiRoot(), firmaId.ToString());
            if (!Directory.Exists(klasor))
                Directory.CreateDirectory(klasor);

            var dosyaAdi = $"yb_{firmaId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{uzanti}";
            var dosyaYolu = Path.Combine(klasor, dosyaAdi);

            await using (var stream = new FileStream(dosyaYolu, FileMode.CreateNew))
                await dosya.CopyToAsync(stream);

            var yetkiBelgesi = new Ys_YetkiBelgesi
            {
                FirmaId = firmaId,
                DosyaYolu = $"{PrivateStoragePrefix}{firmaId}/{dosyaAdi}",
                YetkiBelgesiBaslangicTarihi = baslangic,
                YetkiBelgesiBitisTarihi = bitis,
                Durum = YetkiBelgesiDurumDegerleri.OnaydaBekliyor,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici ?? "sistem",
                SilindiMi = false
            };

            _context.Ys_YetkiBelgeleri.Add(yetkiBelgesi);
            await _context.SaveChangesAsync();

            return (true, "Yetki belgeniz basariyla yuklendi. Onay bekleniyor.");
        }

        public YetkiBelgesiDosyaSonuc? DosyaGetir(Ys_YetkiBelgesi yetkiBelgesi)
        {
            var fizikselYol = ResolveDosyaYolu(yetkiBelgesi.DosyaYolu);
            if (string.IsNullOrWhiteSpace(fizikselYol) || !File.Exists(fizikselYol))
                return null;

            var dosyaAdi = Path.GetFileName(fizikselYol);
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(dosyaAdi, out var contentType))
                contentType = "application/octet-stream";

            return new YetkiBelgesiDosyaSonuc
            {
                FizikselYol = fizikselYol,
                DosyaAdi = dosyaAdi,
                ContentType = contentType
            };
        }

        public static string GuvenliDosyaLinki(int yetkiBelgesiId)
        {
            return $"/ys-yetki-belgesi/dosya/{yetkiBelgesiId}";
        }

        private string? ResolveDosyaYolu(string? dosyaYolu)
        {
            if (string.IsNullOrWhiteSpace(dosyaYolu))
                return null;

            var temiz = dosyaYolu.Trim();
            if (temiz.StartsWith(PrivateStoragePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var relative = temiz[PrivateStoragePrefix.Length..]
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                return SafeCombine(PrivateYetkiBelgesiRoot(), relative);
            }

            if (Uri.TryCreate(temiz, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                temiz = uri.AbsolutePath;
            }

            if (temiz.StartsWith("/", StringComparison.Ordinal))
            {
                var relative = temiz.TrimStart('/')
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                return SafeCombine(WebRootPath(), relative);
            }

            if (!Path.IsPathRooted(temiz))
            {
                var relative = temiz
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                var privatePath = SafeCombine(PrivateYetkiBelgesiRoot(), relative);
                if (!string.IsNullOrWhiteSpace(privatePath) && File.Exists(privatePath))
                    return privatePath;

                return SafeCombine(WebRootPath(), relative);
            }

            var fullPath = Path.GetFullPath(temiz);
            if (PathInRoot(fullPath, PrivateYetkiBelgesiRoot()) || PathInRoot(fullPath, WebRootPath()))
                return fullPath;

            return null;
        }

        private string PrivateYetkiBelgesiRoot()
        {
            return Path.Combine(_env.ContentRootPath, "App_Data", "yetki-belgeleri");
        }

        private string WebRootPath()
        {
            return string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? Path.Combine(_env.ContentRootPath, "wwwroot")
                : _env.WebRootPath;
        }

        private static string? SafeCombine(string root, string relativePath)
        {
            var fullRoot = Path.GetFullPath(root);
            var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            return PathInRoot(fullPath, fullRoot) ? fullPath : null;
        }

        private static bool PathInRoot(string path, string root)
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<bool> DosyaIcerigiGecerliMi(IFormFile dosya, string uzanti)
        {
            var header = new byte[8];
            await using var stream = dosya.OpenReadStream();
            var read = await stream.ReadAsync(header.AsMemory(0, header.Length));

            return uzanti switch
            {
                ".pdf" => read >= 4
                    && header[0] == 0x25
                    && header[1] == 0x50
                    && header[2] == 0x44
                    && header[3] == 0x46,
                ".jpg" or ".jpeg" => read >= 3
                    && header[0] == 0xFF
                    && header[1] == 0xD8
                    && header[2] == 0xFF,
                ".png" => read >= 8
                    && header[0] == 0x89
                    && header[1] == 0x50
                    && header[2] == 0x4E
                    && header[3] == 0x47
                    && header[4] == 0x0D
                    && header[5] == 0x0A
                    && header[6] == 0x1A
                    && header[7] == 0x0A,
                _ => false
            };
        }

        public async Task<bool> Onayla(int yetkiBelgesiId, string? kullanici)
        {
            var yetkiBelgesi = await _context.Ys_YetkiBelgeleri
                .FirstOrDefaultAsync(x => x.Id == yetkiBelgesiId);

            if (yetkiBelgesi == null)
                return false;

            yetkiBelgesi.Durum = YetkiBelgesiDurumDegerleri.Onaylandi;
            yetkiBelgesi.RedGerekce = null;
            yetkiBelgesi.OnayTarihi = DateTime.Now;
            yetkiBelgesi.OnaylayanKullanici = kullanici ?? "sistem";
            yetkiBelgesi.GuncellemeTarihi = DateTime.Now;
            yetkiBelgesi.GuncelleyenKullanici = kullanici ?? "sistem";

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> Reddet(int yetkiBelgesiId, string? gerekce, string? kullanici)
        {
            var yetkiBelgesi = await _context.Ys_YetkiBelgeleri
                .FirstOrDefaultAsync(x => x.Id == yetkiBelgesiId);

            if (yetkiBelgesi == null)
                return false;

            yetkiBelgesi.Durum = YetkiBelgesiDurumDegerleri.Reddedildi;
            yetkiBelgesi.RedGerekce = string.IsNullOrWhiteSpace(gerekce) ? "Belirtilmedi." : gerekce.Trim();
            yetkiBelgesi.OnayTarihi = DateTime.Now;
            yetkiBelgesi.OnaylayanKullanici = kullanici ?? "sistem";
            yetkiBelgesi.GuncellemeTarihi = DateTime.Now;
            yetkiBelgesi.GuncelleyenKullanici = kullanici ?? "sistem";

            await _context.SaveChangesAsync();
            return true;
        }
    }

    public class YetkiBelgesiDosyaSonuc
    {
        public string FizikselYol { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public string DosyaAdi { get; set; } = "yetki-belgesi";
    }
}
