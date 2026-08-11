using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Business.Services.Online;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/ykc")]
    [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel,YetkiliServis")]
    public class YkcApiController : ControllerBase
    {
        private readonly YkcTalepService _ykcTalepService;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly AppDbContext _context;
        private readonly OnlineCihazBilgileriClient _onlineCihazBilgileriClient;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;
        private readonly YkcFr265FormService _ykcFr265FormService;

        public YkcApiController(
            YkcTalepService ykcTalepService,
            UserManager<AppKullanici> userManager,
            IWebHostEnvironment environment,
            AppDbContext context,
            OnlineCihazBilgileriClient onlineCihazBilgileriClient,
            SehirFirmaKoduService sehirFirmaKoduService,
            YkcFr265FormService ykcFr265FormService)
        {
            _ykcTalepService = ykcTalepService;
            _userManager = userManager;
            _environment = environment;
            _context = context;
            _onlineCihazBilgileriClient = onlineCihazBilgileriClient;
            _sehirFirmaKoduService = sehirFirmaKoduService;
            _ykcFr265FormService = ykcFr265FormService;
        }

        [HttpPost("tesisat-sorgula")]
        public async Task<IActionResult> TesisatSorgula([FromBody] YkcTesisatSorguIstek? istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadi." });

            if (string.IsNullOrWhiteSpace(istek?.TesisatNo))
                return Ok(YkcTesisatSorguSonuc.Basarisiz("Tesisat no zorunludur."));

            if (string.IsNullOrWhiteSpace(istek.SozlesmeNo))
                return Ok(YkcTesisatSorguSonuc.Basarisiz("Sozlesme no zorunludur."));

            if (!long.TryParse(istek.TesisatNo.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tesisatNo))
                return Ok(YkcTesisatSorguSonuc.Basarisiz("Tesisat no sayisal olmalidir."));

            if (!long.TryParse(istek.SozlesmeNo.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sozlesmeNo))
                return Ok(YkcTesisatSorguSonuc.Basarisiz("Sozlesme no sayisal olmalidir."));

            var firma = kullanici.FirmaId.HasValue
                ? await _context.Ys_Firmalar
                    .Include(x => x.Sirket)
                    .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId.Value && !x.SilindiMi)
                : null;

            var sirket = kullanici.SirketId.HasValue
                ? await _context.Dag_Sirketler
                    .FirstOrDefaultAsync(x => x.Id == kullanici.SirketId.Value && !x.SilindiMi)
                : firma?.Sirket;

            var roller = await _userManager.GetRolesAsync(kullanici);
            var firmaKodu = OnlineFirmaKodu(firma, sirket);
            var firmaKoduAdaylari = FirmaKoduAdaylari(
                firmaKodu,
                roller.Contains("GenelSistemAdmin") || roller.Contains("SuperAdmin"));

            if (firmaKoduAdaylari.Count == 0)
                return Ok(YkcTesisatSorguSonuc.Basarisiz("Online servis firma kodu belirlenemedi. Lütfen aktif şirket/firma bağlamını kontrol edin."));

            OnlineCihazBilgileriSonuc? servisSonuc = null;
            string? kullanilanFirmaKodu = null;
            OnlineCihazBilgileriSonuc? ilkBasariliSonuc = null;
            string? ilkBasariliFirmaKodu = null;

            foreach (var adayFirmaKodu in firmaKoduAdaylari)
            {
                var adaySonuc = await _onlineCihazBilgileriClient.YSCihazBilgileriGetirAsync(
                    adayFirmaKodu,
                    tesisatNo,
                    sozlesmeNo,
                    HttpContext.RequestAborted);

                servisSonuc = adaySonuc;
                kullanilanFirmaKodu = adayFirmaKodu;

                if (adaySonuc.Basarili && ilkBasariliSonuc == null)
                {
                    ilkBasariliSonuc = adaySonuc;
                    ilkBasariliFirmaKodu = adayFirmaKodu;
                }

                if (adaySonuc.Basarili && adaySonuc.Cihazlar.Count > 0)
                    break;
            }

            if ((servisSonuc == null || !servisSonuc.Basarili || servisSonuc.Cihazlar.Count == 0)
                && ilkBasariliSonuc != null)
            {
                servisSonuc = ilkBasariliSonuc;
                kullanilanFirmaKodu = ilkBasariliFirmaKodu;
            }

            if (servisSonuc == null || !servisSonuc.Basarili)
            {
                return Ok(YkcTesisatSorguSonuc.Basarisiz(
                    servisSonuc?.HataMesaji ?? "Servisten bilgi alinamadi. Manuel giris yapabilirsiniz."));
            }

            var cihazlar = servisSonuc.Cihazlar.Select(c => new YkcTesisatCihazDto
            {
                CihazKapasite = c.CihazKapasite?.ToString(CultureInfo.InvariantCulture) ?? "",
                CihazMarka = c.CihazMarka ?? "",
                CihazTipi = c.CihazTipi ?? "",
                CihazTipKodu = c.CihazTipKodu ?? "",
                ProjeNo = c.ProjeNo ?? "",
                TesisatNo = c.TesisatNo?.ToString(CultureInfo.InvariantCulture) ?? ""
            }).ToList();

            return Ok(new YkcTesisatSorguSonuc
            {
                Basarili = cihazlar.Count > 0,
                ManuelGirisSerbest = cihazlar.Count == 0,
                Mesaj = cihazlar.Count > 0
                    ? "Tesisat ve cihaz bilgileri alindi."
                    : "Tesisat bulundu ancak cihaz listesi bos geldi. Manuel giris yapabilirsiniz.",
                FirmaKodu = kullanilanFirmaKodu,
                TesisatNo = (servisSonuc.TesisatNo ?? tesisatNo).ToString(CultureInfo.InvariantCulture),
                SozlesmeNo = (servisSonuc.SozlesmeNo ?? sozlesmeNo).ToString(CultureInfo.InvariantCulture),
                AboneNo = servisSonuc.CariKod?.ToString(CultureInfo.InvariantCulture) ?? "",
                SayacNo = servisSonuc.SayacNo?.ToString(CultureInfo.InvariantCulture) ?? "",
                MusteriAdi = servisSonuc.CariAd ?? "",
                MusteriTelefon = "",
                Il = firma?.FaaliyetIli ?? sirket?.Il ?? IlFromFirmaKodu(kullanilanFirmaKodu),
                Ilce = "",
                Bolge = "",
                Adres = servisSonuc.Adres ?? "",
                Durum = cihazlar.Count > 0 ? "Cihaz bilgisi bulundu" : "Tesisat bulundu",
                Cihazlar = cihazlar
            });
        }

        [HttpPost("talepler/liste")]
        public async Task<IActionResult> TaleplerListe([FromBody] YkcTalepListeFiltre? filtre)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            var sonuc = await _ykcTalepService.ListeAsync(
                filtre ?? new YkcTalepListeFiltre(),
                kullanici,
                await GenelYetkiliMiAsync(kullanici));

            return Ok(sonuc);
        }

        [HttpPost("talepler/rapor")]
        public async Task<IActionResult> TaleplerRapor([FromBody] YkcTalepListeFiltre? filtre)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            var sonuc = await _ykcTalepService.RaporAsync(
                filtre ?? new YkcTalepListeFiltre(),
                kullanici,
                await GenelYetkiliMiAsync(kullanici));

            return Ok(sonuc);
        }

        [HttpPost("talepler/fr265-word")]
        public async Task<IActionResult> Fr265Word([FromBody] YkcTalepGetirIstek? istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (istek == null || istek.Id <= 0)
                return BadRequest(new { basarili = false, mesaj = "Talep id zorunludur." });

            var genelYetkili = await GenelYetkiliMiAsync(kullanici);
            var detay = await _ykcTalepService.GetirAsync(istek.Id, kullanici, genelYetkili);
            if (detay == null)
                return NotFound(new { basarili = false, mesaj = "Cihaz değişim talebi bulunamadı." });

            var belge = _ykcFr265FormService.WordOlustur(detay);
            await _ykcTalepService.IslemGecmisiEkleAsync(
                detay.Id,
                kullanici,
                genelYetkili,
                "FR265WordIndirildi",
                "FR265 cihaz değişim formu Word olarak üretildi.");

            return File(belge.Bytes, belge.ContentType, belge.DosyaAdi);
        }

        [HttpPost("talepler/dosya-indir")]
        public async Task<IActionResult> DosyaIndir([FromBody] YkcDosyaGetirIstek? istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadi." });

            if (istek == null || istek.Id <= 0)
                return BadRequest(new { basarili = false, mesaj = "Dosya id zorunludur." });

            var dosya = await _context.Ykc_FormDosyalari
                .Include(x => x.Talep)
                .FirstOrDefaultAsync(x => x.Id == istek.Id && !x.SilindiMi && x.Talep != null && !x.Talep.SilindiMi);

            if (dosya?.Talep == null)
                return NotFound(new { basarili = false, mesaj = "Cihaz degisim form dosyasi bulunamadi." });

            if (!await TalepDosyasinaYetkiliMiAsync(dosya.Talep, kullanici))
                return Forbid();

            if (string.IsNullOrWhiteSpace(dosya.DosyaYolu))
                return NotFound(new { basarili = false, mesaj = "Dosya yolu bulunamadi." });

            var webRoot = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
                webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");

            var goreliYol = dosya.DosyaYolu.Trim().Replace('\\', '/').TrimStart('/');
            if (!goreliYol.StartsWith("uploads/ykc/", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { basarili = false, mesaj = "Dosya yolu gecersiz." });

            var fizikselYol = Path.GetFullPath(Path.Combine(
                webRoot,
                goreliYol.Replace('/', Path.DirectorySeparatorChar)));
            var kokYol = Path.GetFullPath(webRoot);

            if (!fizikselYol.StartsWith(kokYol + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            if (!System.IO.File.Exists(fizikselYol))
                return NotFound(new { basarili = false, mesaj = "Dosya fiziksel olarak bulunamadi." });

            var bytes = await System.IO.File.ReadAllBytesAsync(fizikselYol, HttpContext.RequestAborted);
            var contentType = string.IsNullOrWhiteSpace(dosya.IcerikTipi)
                ? "application/octet-stream"
                : dosya.IcerikTipi.Trim();
            var dosyaAdi = string.IsNullOrWhiteSpace(dosya.DosyaAdi)
                ? Path.GetFileName(fizikselYol)
                : dosya.DosyaAdi.Trim();

            return File(bytes, contentType, dosyaAdi);
        }

        [HttpPost("dogalgaz-mobile/talepler/liste")]
        public async Task<IActionResult> DogalgazMobileTaleplerListe([FromBody] YkcTalepListeFiltre? filtre)
        {
            filtre ??= new YkcTalepListeFiltre();
            filtre.HedefUygulama = YkcHedefUygulamaDegerleri.DogalgazMobileApp;
            return await TaleplerListe(filtre);
        }

        [HttpPost("crm187/talepler/liste")]
        public async Task<IActionResult> Crm187TaleplerListe([FromBody] YkcTalepListeFiltre? filtre)
        {
            filtre ??= new YkcTalepListeFiltre();
            filtre.HedefUygulama = YkcHedefUygulamaDegerleri.Crm187;
            return await TaleplerListe(filtre);
        }

        [HttpPost("talepler/getir")]
        public async Task<IActionResult> TalepGetir([FromBody] YkcTalepGetirIstek? istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (istek == null || istek.Id <= 0)
                return BadRequest(new { basarili = false, mesaj = "Talep id zorunludur." });

            var sonuc = await _ykcTalepService.GetirAsync(istek.Id, kullanici, await GenelYetkiliMiAsync(kullanici));
            if (sonuc == null)
                return NotFound(new { basarili = false, mesaj = "Cihaz değişim talebi bulunamadı." });

            return Ok(sonuc);
        }

        [HttpPost("talepler/olustur")]
        public async Task<IActionResult> TalepOlustur([FromBody] YkcTalepKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (dto == null)
                return BadRequest(YkcIslemSonuc.HataliSonuc("Talep bilgileri zorunludur."));

            var sonuc = await _ykcTalepService.OlusturAsync(dto, kullanici);
            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        [HttpPost("talepler/atama-yap")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> AtamaYap([FromBody] YkcAtamaKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (dto == null || dto.TalepId <= 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("Atama icin talep id zorunludur."));

            var sonuc = await _ykcTalepService.AtamaYapAsync(dto, kullanici, await GenelYetkiliMiAsync(kullanici));
            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        [HttpPost("talepler/durum-guncelle")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> DurumGuncelle([FromBody] YkcDurumGuncelleDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (dto == null || dto.TalepId <= 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("Durum güncelleme için talep id zorunludur."));

            var sonuc = await _ykcTalepService.DurumGuncelleAsync(dto, kullanici, await GenelYetkiliMiAsync(kullanici));
            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        [HttpPost("talepler/dosya-kaydet")]
        public async Task<IActionResult> DosyaKaydet([FromBody] YkcDosyaKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (dto == null || dto.TalepId <= 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("Dosya kaydi icin talep id zorunludur."));

            if (!YkcFormDosyasiGecerliMi(dto.DosyaAdi ?? dto.DosyaYolu, dto.IcerikTipi, icerikTipiZorunlu: false))
                return BadRequest(YkcIslemSonuc.HataliSonuc("Sadece PDF, JPG veya PNG form dosyasi kaydedilebilir."));

            var roller = await _userManager.GetRolesAsync(kullanici);
            var dosyaTuru = string.IsNullOrWhiteSpace(dto.DosyaTuru)
                ? YkcFormDosyaTuruDegerleri.FirmaFormu
                : dto.DosyaTuru.Trim();

            if (roller.Contains("YetkiliServis") && dosyaTuru != YkcFormDosyaTuruDegerleri.FirmaFormu)
                return Forbid();

            dto.DosyaTuru = dosyaTuru;
            var sonuc = await _ykcTalepService.DosyaEkleAsync(dto, kullanici, await GenelYetkiliMiAsync(kullanici));
            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        [HttpPost("talepler/form-yukle")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> FormYukle([FromForm] YkcFormYukleIstek istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (istek.TalepId <= 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("Form yukleme icin talep id zorunludur."));

            if (istek.Dosya == null || istek.Dosya.Length == 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("Yüklenecek form dosyası zorunludur."));

            if (!YkcFormDosyasiGecerliMi(istek.Dosya.FileName, istek.Dosya.ContentType, icerikTipiZorunlu: true))
                return BadRequest(YkcIslemSonuc.HataliSonuc("Sadece PDF, JPG veya PNG form dosyasi yuklenebilir."));

            var roller = await _userManager.GetRolesAsync(kullanici);
            var dosyaTuru = string.IsNullOrWhiteSpace(istek.DosyaTuru)
                ? YkcFormDosyaTuruDegerleri.FirmaFormu
                : istek.DosyaTuru.Trim();

            if (roller.Contains("YetkiliServis") && dosyaTuru != YkcFormDosyaTuruDegerleri.FirmaFormu)
                return Forbid();

            var webRoot = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
                webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");

            var klasor = Path.Combine(webRoot, "uploads", "ykc", istek.TalepId.ToString());
            Directory.CreateDirectory(klasor);

            var dosyaAdi = GuvenliDosyaAdi(istek.Dosya.FileName);
            var kayitAdi = $"{Guid.NewGuid():N}_{dosyaAdi}";
            var fizikselYol = Path.Combine(klasor, kayitAdi);

            await using (var stream = System.IO.File.Create(fizikselYol))
            {
                await istek.Dosya.CopyToAsync(stream);
            }

            var sanalYol = $"/uploads/ykc/{istek.TalepId}/{kayitAdi}";
            var sonuc = await _ykcTalepService.DosyaEkleAsync(new YkcDosyaKaydetDto
            {
                TalepId = istek.TalepId,
                DosyaTuru = dosyaTuru,
                DosyaAdi = dosyaAdi,
                DosyaYolu = sanalYol,
                IcerikTipi = istek.Dosya.ContentType,
                DosyaBoyutu = istek.Dosya.Length
            }, kullanici, await GenelYetkiliMiAsync(kullanici));

            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        private Task<AppKullanici?> AktifKullaniciAsync()
        {
            return _userManager.GetUserAsync(User);
        }

        private async Task<bool> GenelYetkiliMiAsync(AppKullanici kullanici)
        {
            var roller = await _userManager.GetRolesAsync(kullanici);
            return roller.Contains("GenelSistemAdmin") || roller.Contains("SuperAdmin");
        }

        private async Task<bool> TalepDosyasinaYetkiliMiAsync(Ykc_Talep talep, AppKullanici kullanici)
        {
            if (await GenelYetkiliMiAsync(kullanici))
                return true;

            if (kullanici.FirmaId.HasValue && talep.FirmaId == kullanici.FirmaId.Value)
                return true;

            if (kullanici.SirketId.HasValue && talep.SirketId == kullanici.SirketId.Value)
                return true;

            return false;
        }

        private static string GuvenliDosyaAdi(string dosyaAdi)
        {
            var sadeceAd = Path.GetFileName(dosyaAdi);
            foreach (var karakter in Path.GetInvalidFileNameChars())
                sadeceAd = sadeceAd.Replace(karakter, '_');

            return string.IsNullOrWhiteSpace(sadeceAd) ? "ykc-form" : sadeceAd;
        }

        private static bool YkcFormDosyasiGecerliMi(string? dosyaAdi, string? icerikTipi, bool icerikTipiZorunlu)
        {
            var uzanti = Path.GetExtension(dosyaAdi ?? string.Empty).ToLowerInvariant();
            var izinliTipler = uzanti switch
            {
                ".pdf" => new[] { "application/pdf" },
                ".jpg" or ".jpeg" => new[] { "image/jpeg" },
                ".png" => new[] { "image/png" },
                _ => Array.Empty<string>()
            };

            if (izinliTipler.Length == 0)
                return false;

            if (string.IsNullOrWhiteSpace(icerikTipi))
                return !icerikTipiZorunlu;

            return izinliTipler.Contains(icerikTipi.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        private string? OnlineFirmaKodu(Ys_Firma? firma, Dag_Sirket? sirket)
        {
            return _sehirFirmaKoduService.FirmaKodu(firma?.FaaliyetIli)
                ?? _sehirFirmaKoduService.FirmaKodu(firma?.Sirket?.Il)
                ?? _sehirFirmaKoduService.FirmaKodu(sirket?.Il)
                ?? FirmaKoduFromSirketAdi(firma?.Sirket?.SirketAdi)
                ?? FirmaKoduFromSirketAdi(sirket?.SirketAdi);
        }

        private List<string> FirmaKoduAdaylari(string? tercihliFirmaKodu, bool genelYetkili)
        {
            var adaylar = new List<string>();

            if (!string.IsNullOrWhiteSpace(tercihliFirmaKodu))
                adaylar.Add(tercihliFirmaKodu.Trim());

            if (genelYetkili && adaylar.Count == 0)
            {
                adaylar.AddRange(_sehirFirmaKoduService
                    .TumKodlar()
                    .Values
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim()));
            }

            return adaylar
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string? IlFromFirmaKodu(string? firmaKodu)
        {
            if (string.IsNullOrWhiteSpace(firmaKodu))
                return null;

            return _sehirFirmaKoduService
                .TumKodlar()
                .FirstOrDefault(x => string.Equals(x.Value, firmaKodu.Trim(), StringComparison.OrdinalIgnoreCase))
                .Key;
        }

        private static string? FirmaKoduFromSirketAdi(string? sirketAdi)
        {
            if (string.IsNullOrWhiteSpace(sirketAdi))
                return null;

            var normalized = NormalizeFirmaText(sirketAdi);
            if (normalized.Contains("CORUM") || normalized.Contains("CORUMGAZ"))
                return "CORUMGAZ";
            if (normalized.Contains("KARGAZ") || normalized.Contains("KASTAMONU") || normalized.Contains("KARABUK"))
                return "KARGAZ";
            if (normalized.Contains("SURMELI") || normalized.Contains("SURMELIGAZ") || normalized.Contains("YOZGAT"))
                return "SURMELIGAZ";
            if (normalized.Contains("YALOVA"))
                return "MARMARAGAZ_YALOVA";
            if (normalized.Contains("CORLU") || normalized.Contains("TEKIRDAG"))
                return "MARMARAGAZ_CORLU";

            return normalized;
        }

        private static string NormalizeFirmaText(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);
            var chars = normalized
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray();

            return new string(chars)
                .Normalize(NormalizationForm.FormC)
                .ToUpperInvariant()
                .Replace('İ', 'I')
                .Replace('Ğ', 'G')
                .Replace('Ü', 'U')
                .Replace('Ş', 'S')
                .Replace('Ö', 'O')
                .Replace('Ç', 'C')
                .Replace(" ", "");
        }
    }

    public class YkcTalepGetirIstek
    {
        public int Id { get; set; }
    }

    public class YkcDosyaGetirIstek
    {
        public int Id { get; set; }
    }

    public class YkcFormYukleIstek
    {
        public int TalepId { get; set; }
        public string? DosyaTuru { get; set; }
        public IFormFile? Dosya { get; set; }
    }
}
