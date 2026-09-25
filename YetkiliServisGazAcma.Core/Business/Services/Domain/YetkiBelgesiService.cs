using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YetkiBelgesiService
    {
        private const string PrivateStoragePrefix = "private:yetki-belgeleri/";
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration? _configuration;

        public YetkiBelgesiService(AppDbContext context, IWebHostEnvironment env, IConfiguration? configuration = null)
        {
            _context = context;
            _env = env;
            _configuration = configuration;
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
            var bugun = DateTime.Today;
            var sorgu = _context.Ys_YetkiBelgeleri
                .Include(x => x.Firma)
                .ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi && x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
                    && x.YetkiBelgesiBitisTarihi >= bugun);

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

            var firma = await _context.Ys_Firmalar
                .AsNoTracking()
                .Where(x => x.Id == firmaId && !x.SilindiMi)
                .Select(x => new { x.Id, x.SirketId })
                .FirstOrDefaultAsync();

            if (firma == null)
                return (false, "Yetki belgesi yuklenecek firma bulunamadi.");

            var yil = DateTime.UtcNow.Year.ToString();
            var goreliKlasor = Path.Combine($"sirket-{firma.SirketId}", $"firma-{firma.Id}", yil);
            var klasor = Path.Combine(PrivateYetkiBelgesiRoot(), goreliKlasor);
            Directory.CreateDirectory(klasor);

            var dosyaAdi = $"yb_{firmaId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{uzanti}";
            var dosyaYolu = Path.Combine(klasor, dosyaAdi);

            await using (var stream = new FileStream(dosyaYolu, FileMode.CreateNew))
                await dosya.CopyToAsync(stream);

            var yetkiBelgesi = new Ys_YetkiBelgesi
            {
                FirmaId = firmaId,
                DosyaYolu = $"{PrivateStoragePrefix}{goreliKlasor.Replace('\\', '/')}/{dosyaAdi}",
                YetkiBelgesiBaslangicTarihi = baslangic,
                YetkiBelgesiBitisTarihi = bitis,
                Durum = YetkiBelgesiDurumDegerleri.OnaydaBekliyor,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici ?? "sistem",
                SilindiMi = false
            };

            _context.Ys_YetkiBelgeleri.Add(yetkiBelgesi);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                if (File.Exists(dosyaYolu))
                    File.Delete(dosyaYolu);
                throw;
            }

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
                return PrivateDocumentStorage.ExistingFile(_env, _configuration, "yetki-belgeleri", relative)
                    ?? SafeCombine(PrivateYetkiBelgesiRoot(), relative);
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
                var privatePath = PrivateDocumentStorage.ExistingFile(_env, _configuration, "yetki-belgeleri", relative);
                if (privatePath != null)
                    return privatePath;

                return SafeCombine(WebRootPath(), relative);
            }

            var fullPath = Path.GetFullPath(temiz);
            if (PrivateDocumentStorage.IsInRoot(fullPath, PrivateYetkiBelgesiRoot())
                || PrivateDocumentStorage.IsInRoot(fullPath, PrivateDocumentStorage.LegacyRoot(_env, "yetki-belgeleri"))
                || PrivateDocumentStorage.IsInRoot(fullPath, WebRootPath()))
                return fullPath;

            return null;
        }

        private string PrivateYetkiBelgesiRoot()
        {
            return PrivateDocumentStorage.Root(_env, _configuration, "yetki-belgeleri");
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
            return PrivateDocumentStorage.IsInRoot(fullPath, fullRoot) ? fullPath : null;
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

        public static bool OnaylanabilirMi(Ys_YetkiBelgesi? belge, DateTime tarih)
        {
            return belge != null && !belge.SilindiMi
                && belge.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
                && belge.YetkiBelgesiBitisTarihi.Date >= tarih.Date;
        }

        public static bool SilinebilirMi(Ys_YetkiBelgesi? belge)
        {
            return belge != null && !belge.SilindiMi
                && belge.Durum != YetkiBelgesiDurumDegerleri.Onaylandi;
        }

        public static bool GecerliMi(Ys_YetkiBelgesi? belge, DateTime tarih)
        {
            var gun = tarih.Date;
            return belge != null && !belge.SilindiMi
                && belge.Durum == YetkiBelgesiDurumDegerleri.Onaylandi
                && (!belge.YetkiBelgesiBaslangicTarihi.HasValue
                    || belge.YetkiBelgesiBaslangicTarihi.Value.Date <= gun)
                && belge.YetkiBelgesiBitisTarihi.Date >= gun;
        }

        public async Task<bool> Onayla(int yetkiBelgesiId, string? kullanici)
        {
            var simdi = DateTime.Now;
            var yapan = kullanici ?? "sistem";
            var guncellenen = await _context.Ys_YetkiBelgeleri
                .Where(x => x.Id == yetkiBelgesiId
                    && !x.SilindiMi
                    && x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
                    && x.YetkiBelgesiBitisTarihi >= simdi.Date)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Durum, YetkiBelgesiDurumDegerleri.Onaylandi)
                    .SetProperty(x => x.RedGerekce, (string?)null)
                    .SetProperty(x => x.OnayTarihi, simdi)
                    .SetProperty(x => x.OnaylayanKullanici, yapan)
                    .SetProperty(x => x.GuncellemeTarihi, simdi)
                    .SetProperty(x => x.GuncelleyenKullanici, yapan));

            return guncellenen == 1;
        }

        public async Task<bool> Reddet(int yetkiBelgesiId, string? gerekce, string? kullanici)
        {
            var simdi = DateTime.Now;
            var yapan = kullanici ?? "sistem";
            var redGerekce = string.IsNullOrWhiteSpace(gerekce) ? "Belirtilmedi." : gerekce.Trim();
            var guncellenen = await _context.Ys_YetkiBelgeleri
                .Where(x => x.Id == yetkiBelgesiId
                    && !x.SilindiMi
                    && x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Durum, YetkiBelgesiDurumDegerleri.Reddedildi)
                    .SetProperty(x => x.RedGerekce, redGerekce)
                    .SetProperty(x => x.OnayTarihi, simdi)
                    .SetProperty(x => x.OnaylayanKullanici, yapan)
                    .SetProperty(x => x.GuncellemeTarihi, simdi)
                    .SetProperty(x => x.GuncelleyenKullanici, yapan));

            return guncellenen == 1;
        }
    }

    public class YetkiBelgesiDosyaSonuc
    {
        public string FizikselYol { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public string DosyaAdi { get; set; } = "yetki-belgesi";
    }
}
