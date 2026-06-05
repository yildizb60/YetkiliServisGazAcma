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
    [Route("api/ys-devreyeal")]
    [Authorize(Roles = "YetkiliServis")]
    public class YetkiliServisDevreyeAlmaApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly OnlineCihazBilgileriClient _onlineCihazBilgileriClient;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;

        public YetkiliServisDevreyeAlmaApiController(
            AppDbContext context,
            UserManager<AppKullanici> userManager,
            OnlineCihazBilgileriClient onlineCihazBilgileriClient,
            SehirFirmaKoduService sehirFirmaKoduService)
        {
            _context = context;
            _userManager = userManager;
            _onlineCihazBilgileriClient = onlineCihazBilgileriClient;
            _sehirFirmaKoduService = sehirFirmaKoduService;
        }

        [HttpPost("gecmis")]
        public async Task<IActionResult> Gecmis([FromBody] YsDevreyeAlmaGecmisFiltreDto? dto)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            var firmaId = kullanici.FirmaId.Value;
            var query = DevreyeAlmaQuery()
                .Where(x => x.FirmaId == firmaId);

            if (!string.IsNullOrWhiteSpace(dto?.Marka))
                query = query.Where(x => x.Marka != null && x.Marka.MarkaAdi == dto.Marka);

            if (dto?.BaslangicTarihi.HasValue == true)
            {
                var baslangic = dto.BaslangicTarihi.Value.Date;
                query = query.Where(x => x.DevreyeAlmaTarihi >= baslangic);
            }

            if (dto?.BitisTarihi.HasValue == true)
            {
                var bitis = dto.BitisTarihi.Value.Date.AddDays(1);
                query = query.Where(x => x.DevreyeAlmaTarihi < bitis);
            }

            if (!string.IsNullOrWhiteSpace(dto?.Musteri))
            {
                var aranacak = dto.Musteri.Trim();
                query = query.Where(x => x.MusteriAdi != null && x.MusteriAdi.Contains(aranacak));
            }

            if (!string.IsNullOrWhiteSpace(dto?.Durum))
            {
                if (string.Equals(dto.Durum, "tamamlandi", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(x => x.Durum == 1);
                else if (string.Equals(dto.Durum, "bekliyor", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(x => x.Durum == 0);
            }

            var islemler = await query
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ToListAsync();

            var firma = await FirmaQuery().FirstOrDefaultAsync(x => x.Id == firmaId);
            var markaList = await _context.Ys_DevreyeAlmalar
                .Include(x => x.Marka)
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.Marka != null && x.Marka.MarkaAdi != null)
                .Select(x => x.Marka!.MarkaAdi!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            return Ok(new YsDevreyeAlmaGecmisDto
            {
                Islemler = islemler.Select(YsDevreyeAlmaDto.FromEntity).ToList(),
                Firma = firma == null ? null : YsFirmaDto.FromEntity(firma),
                MarkaList = markaList
            });
        }

        [HttpPost("getir")]
        public async Task<IActionResult> Getir([FromBody] YsDevreyeAlmaGetirDto? dto)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            if (dto == null || dto.Id <= 0)
                return NotFound();

            var islem = await DevreyeAlmaQuery()
                .FirstOrDefaultAsync(x => x.Id == dto.Id && x.FirmaId == kullanici.FirmaId.Value);

            if (islem == null)
                return NotFound();

            return Ok(YsDevreyeAlmaDto.FromEntity(islem));
        }

        [HttpPost("ekran")]
        public async Task<IActionResult> Ekran()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            var kurulum = await GetIlkKurulumDurumuAsync(kullanici);
            if (kurulum.zorunluMu && !kurulum.tamamlandiMi)
            {
                return Ok(new YsDevreyeAlmaEkranDto
                {
                    Erisilebilir = false,
                    Hata = "Ilk kurulum tamamlanmadan cihaz devreye alma islemi yapilamaz.",
                    RedirectUrl = "/ys-panel/ilk-kurulum"
                });
            }

            var firmaId = kullanici.FirmaId.Value;
            if (!await GecerliYetkiBelgesiVarAsync(firmaId))
            {
                var onayliYetkiBelgesiVar = await OnayliYetkiBelgesiVarAsync(firmaId);
                return Ok(new YsDevreyeAlmaEkranDto
                {
                    Erisilebilir = false,
                    Hata = onayliYetkiBelgesiVar
                        ? "Gecerli tarih araliginda onayli yetki belgeniz bulunmuyor."
                        : "Onayli yetki belgeniz bulunmuyor.",
                    RedirectUrl = onayliYetkiBelgesiVar ? "/ys-yetki-belgesi" : "/ys-panel"
                });
            }

            var markalar = await _context.Ys_FirmaMarkalar
                .Include(x => x.Marka)
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi)
                .Select(x => x.Marka)
                .ToListAsync();

            var firma = await FirmaQuery().FirstOrDefaultAsync(x => x.Id == firmaId);

            return Ok(new YsDevreyeAlmaEkranDto
            {
                Erisilebilir = true,
                Markalar = markalar
                    .Where(x => x != null)
                    .Select(x => YsMarkaDto.FromEntity(x!))
                    .ToList(),
                Firma = firma == null ? null : YsFirmaDto.FromEntity(firma)
            });
        }

        [HttpPost("tesisat-sorgula")]
        public async Task<IActionResult> TesisatSorgula([FromBody] YsTesisatSorguDto? dto)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici?.FirmaId == null)
                return Unauthorized(new YsTesisatSorguSonucDto { Basarili = false, Mesaj = "Oturum suresi dolmus." });

            var kurulum = await GetIlkKurulumDurumuAsync(kullanici);
            if (kurulum.zorunluMu && !kurulum.tamamlandiMi)
            {
                return Ok(new YsTesisatSorguSonucDto
                {
                    Basarili = false,
                    Mesaj = "Ilk kurulum tamamlanmadan islem yapilamaz. Lutfen once sube, marka ve kategori secimini tamamlayin."
                });
            }

            if (string.IsNullOrWhiteSpace(dto?.TesistatNo))
                return Ok(new YsTesisatSorguSonucDto { Basarili = false, Mesaj = "Tesisat no bos olamaz." });

            if (string.IsNullOrWhiteSpace(dto.SozlesmeNo))
                return Ok(new YsTesisatSorguSonucDto { Basarili = false, Mesaj = "Sozlesme no bos olamaz." });

            if (!long.TryParse(dto.TesistatNo.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tesisatNo))
                return Ok(new YsTesisatSorguSonucDto { Basarili = false, Mesaj = "Tesisat no sayisal olmalidir." });

            if (!long.TryParse(dto.SozlesmeNo.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sozlesmeNo))
                return Ok(new YsTesisatSorguSonucDto { Basarili = false, Mesaj = "Sozlesme no sayisal olmalidir." });

            var firma = await FirmaQuery()
                .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId.Value);

            var firmaKodu = OnlineFirmaKodu(firma);
            var servisSonuc = await _onlineCihazBilgileriClient.YSCihazBilgileriGetirAsync(
                firmaKodu,
                tesisatNo,
                sozlesmeNo,
                HttpContext.RequestAborted);

            if (!servisSonuc.Basarili)
            {
                return Ok(new YsTesisatSorguSonucDto
                {
                    Basarili = false,
                    Mesaj = servisSonuc.HataMesaji ?? "Cihaz bilgileri alinamadi."
                });
            }

            var cariKod = servisSonuc.CariKod?.ToString(CultureInfo.InvariantCulture) ?? "";
            return Ok(new YsTesisatSorguSonucDto
            {
                Basarili = true,
                TesistatNo = (servisSonuc.TesisatNo ?? tesisatNo).ToString(CultureInfo.InvariantCulture),
                SozlesmeNo = (servisSonuc.SozlesmeNo ?? sozlesmeNo).ToString(CultureInfo.InvariantCulture),
                AboneNo = cariKod,
                SayacNo = servisSonuc.SayacNo?.ToString(CultureInfo.InvariantCulture) ?? "",
                MusteriAdi = servisSonuc.CariAd ?? "",
                MusteriTcNo = cariKod,
                MusteriTelefon = "",
                Adres = servisSonuc.Adres ?? "",
                UygunlukBelgeNo = "",
                UygunlukTarihi = "",
                Durum = servisSonuc.Cihazlar.Count > 0 ? "Cihaz bilgisi bulundu" : "Tesisat bulundu",
                Cihazlar = servisSonuc.Cihazlar.Select(c => new YsTesisatCihazDto
                {
                    CihazMarka = c.CihazMarka ?? "",
                    CihazTipi = c.CihazTipi ?? "",
                    CihazKapasite = c.CihazKapasite?.ToString(CultureInfo.InvariantCulture) ?? ""
                }).ToList()
            });
        }

        [HttpPost("bildirimler")]
        public async Task<IActionResult> Bildirimler()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            var firmaId = kullanici.FirmaId.Value;
            var bildirimler = new List<string>();

            var firma = await _context.Ys_Firmalar
                .Include(x => x.YetkiBelgeleri)
                .FirstOrDefaultAsync(x => x.Id == firmaId);

            var bugun = DateTime.Now.Date;
            var onayli = firma?.YetkiBelgeleri?
                .Where(x => x.Durum == 1
                    && !x.SilindiMi
                    && (!x.YetkiBelgesiBaslangicTarihi.HasValue || x.YetkiBelgesiBaslangicTarihi.Value.Date <= bugun)
                    && x.YetkiBelgesiBitisTarihi.Date >= bugun)
                .OrderBy(x => x.YetkiBelgesiBitisTarihi)
                .FirstOrDefault();

            var bekleyenVar = firma?.YetkiBelgeleri?.Any(x => x.Durum == 0 && !x.SilindiMi) ?? false;
            if (onayli != null)
            {
                bildirimler.Add("Yetki belgeniz onaylandi. Cihaz devreye alabilirsiniz.");
                var kalan = (onayli.YetkiBelgesiBitisTarihi.Date - bugun).Days;
                if (kalan <= 30)
                    bildirimler.Add($"Yetki belgenizin bitmesine {kalan} gun kaldi. Lutfen yenileyin.");
            }

            if (bekleyenVar)
                bildirimler.Add("Yetki belgeniz onay bekliyor. Yetkili onayladiktan sonra islem yapabilirsiniz.");

            var son7Gun = DateTime.Now.AddDays(-7);
            var sonDevreye = await _context.Ys_DevreyeAlmalar
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.OlusturmaTarihi >= son7Gun)
                .CountAsync();
            if (sonDevreye > 0)
                bildirimler.Add($"Son 7 gunde {sonDevreye} cihaz devreye alindi.");

            var sonSube = await _context.Ys_Subeler
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi && x.OlusturmaTarihi >= son7Gun)
                .CountAsync();
            if (sonSube > 0)
                bildirimler.Add($"Son 7 gunde {sonSube} sube kaydi eklendi.");

            return Ok(new YsDevreyeAlmaBildirimDto
            {
                Bildirimler = bildirimler,
                BildirimSayisi = bildirimler.Count
            });
        }

        [HttpPost("marka-kontrol")]
        public async Task<IActionResult> MarkaKontrol([FromBody] YsMarkaKontrolDto? dto)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici?.FirmaId == null)
                return Unauthorized(new YsMarkaKontrolSonucDto { Yetkili = false, Mesaj = "Oturum suresi dolmus." });

            var kurulum = await GetIlkKurulumDurumuAsync(kullanici);
            if (kurulum.zorunluMu && !kurulum.tamamlandiMi)
                return Ok(new YsMarkaKontrolSonucDto { Yetkili = false, Mesaj = "Ilk kurulum tamamlanmadan islem yapilamaz." });

            if (string.IsNullOrWhiteSpace(dto?.CihazMarka))
                return Ok(new YsMarkaKontrolSonucDto { Yetkili = false, Mesaj = "Servisten gelen cihaz marka bilgisi bulunamadi." });

            var marka = await MarkaBulAsync(dto.CihazMarka);
            if (marka == null)
            {
                return Ok(new YsMarkaKontrolSonucDto
                {
                    Yetkili = false,
                    Mesaj = $"Servisten gelen '{dto.CihazMarka}' markasi sistem markalari ile eslesmedi."
                });
            }

            var yetkiVar = await FirmaMarkaYetkisiVarAsync(kullanici.FirmaId.Value, marka.Id);
            if (!yetkiVar)
                return Ok(new YsMarkaKontrolSonucDto { Yetkili = false, Mesaj = "Bu marka icin yetkiniz bulunmamaktadir!" });

            return Ok(new YsMarkaKontrolSonucDto
            {
                Yetkili = true,
                MarkaId = marka.Id,
                MarkaAdi = marka.MarkaAdi
            });
        }

        [HttpPost("kaydet")]
        public async Task<IActionResult> Kaydet([FromBody] YsDevreyeAlmaKaydetDto? dto)
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici?.FirmaId == null)
                return Unauthorized(new YsDevreyeAlmaIslemSonucDto { Basarili = false, Mesaj = "Oturum suresi dolmus.", RedirectUrl = "/giris" });

            var kurulum = await GetIlkKurulumDurumuAsync(kullanici);
            if (kurulum.zorunluMu && !kurulum.tamamlandiMi)
            {
                return Ok(new YsDevreyeAlmaIslemSonucDto
                {
                    Basarili = false,
                    Mesaj = "Ilk kurulum tamamlanmadan cihaz devreye alma islemi yapilamaz.",
                    RedirectUrl = "/ys-panel/ilk-kurulum"
                });
            }

            if (!await GecerliYetkiBelgesiVarAsync(kullanici.FirmaId.Value))
            {
                return Ok(new YsDevreyeAlmaIslemSonucDto
                {
                    Basarili = false,
                    Mesaj = "Cihaz devreye alma islemi icin gecerli onayli yetki belgeniz bulunmalidir.",
                    RedirectUrl = "/ys-yetki-belgesi"
                });
            }

            if (string.IsNullOrWhiteSpace(dto?.CihazMarka))
            {
                return Ok(new YsDevreyeAlmaIslemSonucDto
                {
                    Basarili = false,
                    Mesaj = "Servisten gelen cihazlardan biri secilmeden devreye alma tamamlanamaz.",
                    RedirectUrl = "/ys-devreyeal"
                });
            }

            var marka = await MarkaBulAsync(dto.CihazMarka);
            if (marka == null)
            {
                return Ok(new YsDevreyeAlmaIslemSonucDto
                {
                    Basarili = false,
                    Mesaj = "Servisten gelen cihaz markasi sistem markalari ile eslesmedi.",
                    RedirectUrl = "/ys-devreyeal"
                });
            }

            var yetkiVar = await FirmaMarkaYetkisiVarAsync(kullanici.FirmaId.Value, marka.Id);
            if (!yetkiVar)
            {
                return Ok(new YsDevreyeAlmaIslemSonucDto
                {
                    Basarili = false,
                    Mesaj = "Bu marka icin yetkiniz bulunmamaktadir!",
                    RedirectUrl = "/ys-devreyeal"
                });
            }

            var islem = new Ys_DevreyeAlma
            {
                FirmaId = kullanici.FirmaId.Value,
                MarkaId = marka.Id,
                TesistatNo = dto.TesistatNo,
                AboneNo = dto.AboneNo,
                UygunlukBelgeNo = dto.UygunlukBelgeNo,
                UygunlukTarihi = dto.UygunlukTarihi,
                MusteriAdi = dto.MusteriAdi,
                MusteriTcNo = dto.MusteriTcNo,
                MusteriTelefon = dto.MusteriTelefon,
                Adres = dto.Adres,
                CihazTipi = dto.CihazTipi,
                CihazMarka = marka.MarkaAdi ?? dto.CihazMarka,
                CihazModeli = dto.CihazModeli,
                CihazKapasite = dto.CihazKapasite,
                SeriNo = dto.SeriNo,
                TeknisyenAdi = dto.TeknisyenAdi,
                TeknisyenYetkiBelgesiNo = dto.TeknisyenYetkiBelgesiNo,
                DevreyeAlmaTarihi = DateTime.Now,
                Notlar = dto.Notlar,
                Durum = 1,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName ?? "",
                SilindiMi = false
            };

            _context.Ys_DevreyeAlmalar.Add(islem);
            await _context.SaveChangesAsync();

            return Ok(new YsDevreyeAlmaIslemSonucDto
            {
                Basarili = true,
                Mesaj = "Cihaz devreye alma islemi tamamlandi!",
                Id = islem.Id,
                RedirectUrl = "/ys-devreyeal/gecmis"
            });
        }

        private IQueryable<Ys_DevreyeAlma> DevreyeAlmaQuery()
        {
            return _context.Ys_DevreyeAlmalar
                .Include(x => x.Marka)
                .Include(x => x.Firma)
                    .ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi);
        }

        private IQueryable<Ys_Firma> FirmaQuery()
        {
            return _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .Where(x => !x.SilindiMi);
        }

        private string? OnlineFirmaKodu(Ys_Firma? firma)
        {
            return _sehirFirmaKoduService.FirmaKodu(firma?.FaaliyetIli)
                ?? _sehirFirmaKoduService.FirmaKodu(firma?.Sirket?.Il)
                ?? FirmaKoduFromSirketAdi(firma?.Sirket?.SirketAdi);
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
                .Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_')
                .ToArray();

            return new string(chars)
                .Normalize(NormalizationForm.FormC)
                .Trim('_');
        }

        private async Task<(bool zorunluMu, bool tamamlandiMi, List<string> eksikler)> GetIlkKurulumDurumuAsync(AppKullanici kullanici)
        {
            var firma = await _context.Ys_Firmalar
                .Include(x => x.FirmaMarkalar)
                .Include(x => x.FirmaKategoriler)
                .Include(x => x.Subeler)
                .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId);

            var adminOlusturmus = firma != null
                && !string.IsNullOrWhiteSpace(firma.VergiNo)
                && !string.Equals((kullanici.UserName ?? "").Trim(), (firma.VergiNo ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

            if (!adminOlusturmus)
                return (false, true, new List<string>());

            var eksikler = new List<string>();
            var markaVar = firma?.FirmaMarkalar?.Any(x => !x.SilindiMi) == true;
            var kategoriVar = firma?.FirmaKategoriler?.Any(x => !x.SilindiMi) == true;
            var subeVar = firma?.Subeler?.Any(x => !x.SilindiMi) == true;
            var yetkiBelgesiVar = await _context.Ys_YetkiBelgeleri
                .AnyAsync(x => x.FirmaId == kullanici.FirmaId && !x.SilindiMi);

            if (!markaVar) eksikler.Add("Marka secimi");
            if (!kategoriVar) eksikler.Add("Kategori secimi");
            if (!subeVar) eksikler.Add("Sube kaydi");
            if (!yetkiBelgesiVar) eksikler.Add("Yetki belgesi yukleme");

            return (true, eksikler.Count == 0, eksikler);
        }

        private async Task<bool> GecerliYetkiBelgesiVarAsync(int firmaId)
        {
            var bugun = DateTime.Now.Date;
            return await _context.Ys_YetkiBelgeleri
                .AnyAsync(x => x.FirmaId == firmaId
                    && !x.SilindiMi
                    && x.Durum == 1
                    && (!x.YetkiBelgesiBaslangicTarihi.HasValue || x.YetkiBelgesiBaslangicTarihi.Value.Date <= bugun)
                    && x.YetkiBelgesiBitisTarihi.Date >= bugun);
        }

        private async Task<bool> OnayliYetkiBelgesiVarAsync(int firmaId)
        {
            return await _context.Ys_YetkiBelgeleri
                .AnyAsync(x => x.FirmaId == firmaId
                    && !x.SilindiMi
                    && x.Durum == 1);
        }

        private async Task<bool> FirmaMarkaYetkisiVarAsync(int firmaId, int markaId)
        {
            return await _context.Ys_FirmaMarkalar
                .AnyAsync(x => x.FirmaId == firmaId
                    && x.MarkaId == markaId
                    && !x.SilindiMi);
        }

        private async Task<Ys_Marka?> MarkaBulAsync(string? cihazMarka)
        {
            var aranan = NormalizeMarka(cihazMarka);
            if (string.IsNullOrWhiteSpace(aranan))
                return null;

            var markalar = await _context.Ys_Markalar
                .Where(x => !x.SilindiMi)
                .ToListAsync();

            return markalar.FirstOrDefault(x => NormalizeMarka(x.MarkaAdi) == aranan);
        }

        private static string NormalizeMarka(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            var normalized = value.Trim()
                .ToLower(new CultureInfo("tr-TR"))
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

    public class YsDevreyeAlmaGecmisFiltreDto
    {
        public string? Marka { get; set; }
        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public string? Musteri { get; set; }
        public string? Durum { get; set; }
    }

    public class YsDevreyeAlmaGetirDto
    {
        public int Id { get; set; }
    }

    public class YsDevreyeAlmaEkranDto
    {
        public bool Erisilebilir { get; set; }
        public string? Hata { get; set; }
        public string? RedirectUrl { get; set; }
        public YsFirmaDto? Firma { get; set; }
        public List<YsMarkaDto> Markalar { get; set; } = new();
    }

    public class YsMarkaKontrolDto
    {
        public string? CihazMarka { get; set; }
    }

    public class YsDevreyeAlmaBildirimDto
    {
        public List<string> Bildirimler { get; set; } = new();
        public int BildirimSayisi { get; set; }
    }

    public class YsTesisatSorguDto
    {
        public string? TesistatNo { get; set; }
        public string? SozlesmeNo { get; set; }
    }

    public class YsTesisatSorguSonucDto
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }
        public string? TesistatNo { get; set; }
        public string? SozlesmeNo { get; set; }
        public string? AboneNo { get; set; }
        public string? SayacNo { get; set; }
        public string? MusteriAdi { get; set; }
        public string? MusteriTcNo { get; set; }
        public string? MusteriTelefon { get; set; }
        public string? Adres { get; set; }
        public string? UygunlukBelgeNo { get; set; }
        public string? UygunlukTarihi { get; set; }
        public string? Durum { get; set; }
        public List<YsTesisatCihazDto> Cihazlar { get; set; } = new();
    }

    public class YsTesisatCihazDto
    {
        public string? CihazMarka { get; set; }
        public string? CihazTipi { get; set; }
        public string? CihazKapasite { get; set; }
    }

    public class YsMarkaKontrolSonucDto
    {
        public bool Yetkili { get; set; }
        public string? Mesaj { get; set; }
        public int? MarkaId { get; set; }
        public string? MarkaAdi { get; set; }
    }

    public class YsDevreyeAlmaKaydetDto
    {
        public string? TesistatNo { get; set; }
        public string? AboneNo { get; set; }
        public string? UygunlukBelgeNo { get; set; }
        public DateTime? UygunlukTarihi { get; set; }
        public string? MusteriAdi { get; set; }
        public string? MusteriTcNo { get; set; }
        public string? MusteriTelefon { get; set; }
        public string? Adres { get; set; }
        public string? CihazTipi { get; set; }
        public string? CihazMarka { get; set; }
        public string? CihazModeli { get; set; }
        public string? CihazKapasite { get; set; }
        public string? SeriNo { get; set; }
        public string? TeknisyenAdi { get; set; }
        public string? TeknisyenYetkiBelgesiNo { get; set; }
        public string? Notlar { get; set; }
    }

    public class YsDevreyeAlmaIslemSonucDto
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }
        public int? Id { get; set; }
        public string? RedirectUrl { get; set; }
    }

    public class YsDevreyeAlmaGecmisDto
    {
        public List<YsDevreyeAlmaDto> Islemler { get; set; } = new();
        public YsFirmaDto? Firma { get; set; }
        public List<string> MarkaList { get; set; } = new();
    }

    public class YsFirmaDto
    {
        public int Id { get; set; }
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? FaaliyetIli { get; set; }
        public int SirketId { get; set; }
        public string? SirketAdi { get; set; }
        public string? SirketIl { get; set; }

        public static YsFirmaDto FromEntity(Ys_Firma firma)
        {
            return new YsFirmaDto
            {
                Id = firma.Id,
                FirmaAdi = firma.FirmaAdi,
                YetkiliKisi = firma.YetkiliKisi,
                Telefon = firma.Telefon,
                Email = firma.Email,
                Adres = firma.Adres,
                FaaliyetIli = firma.FaaliyetIli,
                SirketId = firma.SirketId,
                SirketAdi = firma.Sirket?.SirketAdi,
                SirketIl = firma.Sirket?.Il
            };
        }
    }

    public class YsDevreyeAlmaDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public int? MarkaId { get; set; }
        public string? TesistatNo { get; set; }
        public string? AboneNo { get; set; }
        public string? UygunlukBelgeNo { get; set; }
        public DateTime? UygunlukTarihi { get; set; }
        public string? MusteriAdi { get; set; }
        public string? MusteriTcNo { get; set; }
        public string? MusteriTelefon { get; set; }
        public string? Adres { get; set; }
        public string? CihazTipi { get; set; }
        public string? CihazMarka { get; set; }
        public string? CihazModeli { get; set; }
        public string? CihazKapasite { get; set; }
        public string? SeriNo { get; set; }
        public string? TeknisyenAdi { get; set; }
        public string? TeknisyenYetkiBelgesiNo { get; set; }
        public DateTime DevreyeAlmaTarihi { get; set; }
        public string? Notlar { get; set; }
        public int Durum { get; set; }
        public string? PdfYolu { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public string? FirmaAdi { get; set; }
        public string? FirmaYetkiliKisi { get; set; }
        public string? FirmaTelefon { get; set; }
        public string? FirmaEmail { get; set; }
        public string? FirmaAdres { get; set; }
        public string? FirmaFaaliyetIli { get; set; }
        public int FirmaSirketId { get; set; }
        public string? SirketAdi { get; set; }
        public string? SirketIl { get; set; }
        public string? MarkaAdi { get; set; }

        public static YsDevreyeAlmaDto FromEntity(Ys_DevreyeAlma islem)
        {
            return new YsDevreyeAlmaDto
            {
                Id = islem.Id,
                FirmaId = islem.FirmaId,
                MarkaId = islem.MarkaId,
                TesistatNo = islem.TesistatNo,
                AboneNo = islem.AboneNo,
                UygunlukBelgeNo = islem.UygunlukBelgeNo,
                UygunlukTarihi = islem.UygunlukTarihi,
                MusteriAdi = islem.MusteriAdi,
                MusteriTcNo = islem.MusteriTcNo,
                MusteriTelefon = islem.MusteriTelefon,
                Adres = islem.Adres,
                CihazTipi = islem.CihazTipi,
                CihazMarka = islem.CihazMarka,
                CihazModeli = islem.CihazModeli,
                CihazKapasite = islem.CihazKapasite,
                SeriNo = islem.SeriNo,
                TeknisyenAdi = islem.TeknisyenAdi,
                TeknisyenYetkiBelgesiNo = islem.TeknisyenYetkiBelgesiNo,
                DevreyeAlmaTarihi = islem.DevreyeAlmaTarihi,
                Notlar = islem.Notlar,
                Durum = islem.Durum,
                PdfYolu = islem.PdfYolu,
                OlusturmaTarihi = islem.OlusturmaTarihi,
                FirmaAdi = islem.Firma?.FirmaAdi,
                FirmaYetkiliKisi = islem.Firma?.YetkiliKisi,
                FirmaTelefon = islem.Firma?.Telefon,
                FirmaEmail = islem.Firma?.Email,
                FirmaAdres = islem.Firma?.Adres,
                FirmaFaaliyetIli = islem.Firma?.FaaliyetIli,
                FirmaSirketId = islem.Firma?.SirketId ?? 0,
                SirketAdi = islem.Firma?.Sirket?.SirketAdi,
                SirketIl = islem.Firma?.Sirket?.Il,
                MarkaAdi = islem.Marka?.MarkaAdi
            };
        }
    }

    public class YsMarkaDto
    {
        public int Id { get; set; }
        public string? MarkaAdi { get; set; }
        public string? Aciklama { get; set; }
        public bool AktifMi { get; set; }

        public static YsMarkaDto FromEntity(Ys_Marka marka)
        {
            return new YsMarkaDto
            {
                Id = marka.Id,
                MarkaAdi = marka.MarkaAdi,
                Aciklama = marka.Aciklama,
                AktifMi = marka.AktifMi
            };
        }
    }
}
