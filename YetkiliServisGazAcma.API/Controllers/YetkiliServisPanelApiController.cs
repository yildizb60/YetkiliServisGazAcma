using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/ys-panel")]
    [Authorize(Roles = "YetkiliServis")]
    public class YetkiliServisPanelApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly YetkiliServisPanelYonetimApiService _yonetimApiService;
        private readonly DevreyeAlmaExportApiService _devreyeAlmaExportApiService;

        public YetkiliServisPanelApiController(
            AppDbContext context,
            UserManager<AppKullanici> userManager,
            YetkiliServisPanelYonetimApiService yonetimApiService,
            DevreyeAlmaExportApiService devreyeAlmaExportApiService)
        {
            _context = context;
            _userManager = userManager;
            _yonetimApiService = yonetimApiService;
            _devreyeAlmaExportApiService = devreyeAlmaExportApiService;
        }

        [HttpPost("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            var firmaId = kullanici.FirmaId.Value;
            var kurulum = await GetIlkKurulumDurumuAsync(kullanici);

            var firma = await FirmaDashboardQuery()
                .FirstOrDefaultAsync(x => x.Id == firmaId);

            var buAy = await _context.Ys_DevreyeAlmalar
                .Where(x => x.FirmaId == firmaId
                    && x.OlusturmaTarihi.Month == DateTime.Now.Month
                    && x.OlusturmaTarihi.Year == DateTime.Now.Year
                    && !x.SilindiMi)
                .CountAsync();

            var toplam = await _context.Ys_DevreyeAlmalar
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi)
                .CountAsync();

            var sonIslemler = await _context.Ys_DevreyeAlmalar
                .Include(x => x.Marka)
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .Take(5)
                .ToListAsync();

            int? uyariGun = null;
            var onayli = firma?.YetkiBelgeleri?
                .Where(x => x.Durum == 1)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .FirstOrDefault();
            if (onayli != null)
            {
                var kalan = (onayli.YetkiBelgesiBitisTarihi.Date - DateTime.Now.Date).Days;
                if (kalan >= 0)
                    uyariGun = kalan;
            }

            var bildirim = await BildirimlerAsync(firmaId);

            return Ok(new YsPanelDashboardDto
            {
                Firma = firma == null ? null : YsPanelFirmaDto.FromEntity(firma),
                BuAy = buAy,
                Toplam = toplam,
                SonIslemler = sonIslemler.Select(YsPanelDevreyeAlmaDto.FromEntity).ToList(),
                IlkKurulumZorunlu = kurulum.zorunluMu,
                IlkKurulumTamamlandi = kurulum.tamamlandiMi,
                IlkKurulumEksikler = kurulum.eksikler,
                YetkiBelgesiUyariGun = uyariGun,
                Bildirimler = bildirim.Bildirimler,
                BildirimSayisi = bildirim.BildirimSayisi
            });
        }

        [HttpPost("bildirimler")]
        public async Task<IActionResult> Bildirimler()
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await BildirimlerAsync(kullanici.FirmaId.Value));
        }

        [HttpPost("profil")]
        public async Task<IActionResult> Profil()
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            var firma = await FirmaDashboardQuery()
                .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId.Value);

            if (firma == null)
                return NotFound();

            return Ok(YsPanelFirmaDto.FromEntity(firma));
        }

        [HttpPost("profil/guncelle")]
        public async Task<IActionResult> ProfilGuncelle([FromBody] YsPanelProfilGuncelleDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            kullanici.AdSoyad = dto?.AdSoyad ?? kullanici.AdSoyad;
            kullanici.PhoneNumber = dto?.Telefon ?? kullanici.PhoneNumber;

            if (!string.IsNullOrWhiteSpace(dto?.Email) && dto.Email != kullanici.Email)
            {
                kullanici.Email = dto.Email;
                kullanici.UserName = dto.Email;
            }

            var sonuc = await _userManager.UpdateAsync(kullanici);
            if (!sonuc.Succeeded)
            {
                var hata = string.Join(" ", sonuc.Errors.Select(x => x.Description));
                return Ok(YsPanelIslemSonucDto.Basarisiz(string.IsNullOrWhiteSpace(hata)
                    ? "Guncelleme sirasinda hata olustu."
                    : hata));
            }

            if (!string.IsNullOrWhiteSpace(dto?.AdSoyad))
            {
                var firma = await _context.Ys_Firmalar
                    .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId.Value && !x.SilindiMi);
                if (firma != null)
                {
                    firma.YetkiliKisi = dto.AdSoyad;
                    firma.GuncellemeTarihi = DateTime.Now;
                    firma.GuncelleyenKullanici = kullanici.UserName ?? "sistem";
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(YsPanelIslemSonucDto.BasariliSonuc("Profil bilgileri guncellendi."));
        }

        [HttpPost("ilk-kurulum")]
        public async Task<IActionResult> IlkKurulum()
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            var kurulum = await GetIlkKurulumDurumuAsync(kullanici);
            var firmaId = kullanici.FirmaId.Value;
            if (firmaId <= 0)
            {
                return Ok(new YsPanelIlkKurulumDto
                {
                    ZorunluMu = kurulum.zorunluMu,
                    TamamlandiMi = kurulum.tamamlandiMi,
                    Eksikler = kurulum.eksikler,
                    HataMesaji = "Kullanici hesabi bir firmaya bagli olmadigi icin ilk kurulum yapilamadi."
                });
            }

            var firma = await FirmaDashboardQuery()
                .FirstOrDefaultAsync(x => x.Id == firmaId);
            if (firma == null)
            {
                return Ok(new YsPanelIlkKurulumDto
                {
                    ZorunluMu = kurulum.zorunluMu,
                    TamamlandiMi = kurulum.tamamlandiMi,
                    Eksikler = kurulum.eksikler,
                    HataMesaji = "Firma kaydi bulunamadi. Lutfen yonetici ile gorusun."
                });
            }

            var tumMarkalar = await _context.Ys_Markalar
                .Where(x => !x.SilindiMi && x.AktifMi)
                .OrderBy(x => x.MarkaAdi)
                .ToListAsync();

            var tumKategoriler = await _context.UrunKategoriler
                .Where(x => !x.SilindiMi && x.AktifMi)
                .OrderBy(x => x.SiraNo)
                .ThenBy(x => x.Ad)
                .ToListAsync();

            var seciliMarkaIds = firma.FirmaMarkalar?
                .Where(x => !x.SilindiMi)
                .Select(x => x.MarkaId)
                .ToList() ?? new List<int>();

            var seciliKategoriIds = firma.FirmaKategoriler?
                .Where(x => !x.SilindiMi)
                .Select(x => x.KategoriId)
                .ToList() ?? new List<int>();

            var aktifSubeSayisi = firma.Subeler?
                .Count(x => !x.SilindiMi) ?? 0;

            var yetkiBelgesiVar = firma.YetkiBelgeleri?
                .Any(x => !x.SilindiMi) ?? false;

            var onayliYetkiBelgesiVar = firma.YetkiBelgeleri?
                .Any(x => !x.SilindiMi && x.Durum == 1) ?? false;

            return Ok(new YsPanelIlkKurulumDto
            {
                Firma = YsPanelFirmaDto.FromEntity(firma),
                TumMarkalar = tumMarkalar.Select(YsPanelMarkaDto.FromEntity).ToList(),
                TumKategoriler = tumKategoriler.Select(YsPanelUrunKategoriDto.FromEntity).ToList(),
                SeciliMarkaIds = seciliMarkaIds,
                SeciliKategoriIds = seciliKategoriIds,
                AktifSubeSayisi = aktifSubeSayisi,
                YetkiBelgesiVar = yetkiBelgesiVar,
                OnayliYetkiBelgesiVar = onayliYetkiBelgesiVar,
                ZorunluMu = kurulum.zorunluMu,
                TamamlandiMi = kurulum.tamamlandiMi,
                Eksikler = kurulum.eksikler
            });
        }

        [HttpPost("markalar")]
        public async Task<IActionResult> Markalar()
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            var firmaId = kullanici.FirmaId.Value;
            var firma = await FirmaDashboardQuery()
                .FirstOrDefaultAsync(x => x.Id == firmaId);

            if (firma == null)
                return NotFound();

            var tumMarkalar = await _context.Ys_Markalar
                .Where(x => !x.SilindiMi && x.AktifMi)
                .OrderBy(x => x.MarkaAdi)
                .ToListAsync();

            var firmaMarkalar = await _context.Ys_FirmaMarkalar
                .Include(x => x.Marka)
                .Where(x => x.FirmaId == firmaId && !x.SilindiMi)
                .OrderBy(x => x.Marka!.MarkaAdi)
                .ToListAsync();

            return Ok(new YsPanelMarkalarDto
            {
                Firma = YsPanelFirmaDto.FromEntity(firma),
                TumMarkalar = tumMarkalar.Select(YsPanelMarkaDto.FromEntity).ToList(),
                FirmaMarkalar = firmaMarkalar.Select(YsPanelFirmaMarkaDto.FromEntity).ToList(),
                SeciliMarkaIds = firmaMarkalar.Select(x => x.MarkaId).ToList()
            });
        }

        [HttpPost("raporlar")]
        public async Task<IActionResult> Raporlar([FromBody] YsPanelRaporFiltreDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            var firmaId = kullanici.FirmaId.Value;
            var firma = await FirmaDashboardQuery()
                .FirstOrDefaultAsync(x => x.Id == firmaId);

            DateTime basTarih;
            DateTime bitTarih;
            List<Ys_DevreyeAlma> islemler;

            if (dto?.Ids?.Count > 0)
            {
                islemler = await _context.Ys_DevreyeAlmalar
                    .Include(x => x.Marka)
                    .Include(x => x.Firma)
                        .ThenInclude(x => x!.Sirket)
                    .Where(x => x.FirmaId == firmaId && !x.SilindiMi && dto.Ids.Contains(x.Id))
                    .OrderByDescending(x => x.DevreyeAlmaTarihi)
                    .ToListAsync();

                basTarih = islemler.Count > 0 ? islemler.Min(x => x.DevreyeAlmaTarihi).Date : DateTime.Now.Date;
                bitTarih = islemler.Count > 0 ? islemler.Max(x => x.DevreyeAlmaTarihi).Date : DateTime.Now.Date;
            }
            else
            {
                var tarihAraligi = await GetRaporTarihAraligiAsync(firmaId, dto?.Bas, dto?.Bit);
                basTarih = tarihAraligi.Bas;
                bitTarih = tarihAraligi.Bit;
                var bitSonrasi = bitTarih.AddDays(1);

                var query = _context.Ys_DevreyeAlmalar
                    .Include(x => x.Marka)
                    .Include(x => x.Firma)
                        .ThenInclude(x => x!.Sirket)
                    .Where(x => x.FirmaId == firmaId
                        && !x.SilindiMi
                        && x.DevreyeAlmaTarihi >= basTarih
                        && x.DevreyeAlmaTarihi < bitSonrasi)
                    .OrderByDescending(x => x.DevreyeAlmaTarihi);

                islemler = dto?.Limit is > 0
                    ? await query.Take(dto.Limit.Value).ToListAsync()
                    : await query.ToListAsync();
            }

            var bitSonrasiRapor = bitTarih.AddDays(1);
            var devreyeTemelQuery = _context.Ys_DevreyeAlmalar
                .Include(x => x.Marka)
                .Where(x => x.FirmaId == firmaId
                    && !x.SilindiMi
                    && x.DevreyeAlmaTarihi >= basTarih
                    && x.DevreyeAlmaTarihi < bitSonrasiRapor);

            var yetkiBelgesiTemelQuery = _context.Ys_YetkiBelgeleri
                .Where(x => x.FirmaId == firmaId
                    && !x.SilindiMi
                    && x.OlusturmaTarihi >= basTarih
                    && x.OlusturmaTarihi < bitSonrasiRapor);

            var devreyeSayisi = await devreyeTemelQuery.CountAsync();
            var tamamlanan = await devreyeTemelQuery.Where(x => x.Durum == 1).CountAsync();
            var bekleyen = await devreyeTemelQuery.Where(x => x.Durum == 0).CountAsync();

            var yetkiBelgesiOnayli = await yetkiBelgesiTemelQuery.Where(x => x.Durum == 1).CountAsync();
            var yetkiBelgesiBekleyen = await yetkiBelgesiTemelQuery.Where(x => x.Durum == 0).CountAsync();
            var yetkiBelgesiReddedilen = await yetkiBelgesiTemelQuery.Where(x => x.Durum == 2).CountAsync();

            var aylikBaslangic = new DateTime(basTarih.Year, basTarih.Month, 1);
            var aylikBitis = new DateTime(bitTarih.Year, bitTarih.Month, 1);
            var aySayisi = ((aylikBitis.Year - aylikBaslangic.Year) * 12) + aylikBitis.Month - aylikBaslangic.Month + 1;
            if (aySayisi < 1) aySayisi = 1;

            var aylikEtiketler = Enumerable.Range(0, aySayisi)
                .Select(i => aylikBaslangic.AddMonths(i))
                .ToList();

            var aylikHam = await devreyeTemelQuery
                .GroupBy(x => new { x.DevreyeAlmaTarihi.Year, x.DevreyeAlmaTarihi.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();

            var aylikMap = aylikHam.ToDictionary(x => $"{x.Year:D4}-{x.Month:D2}", x => x.Count);
            var chartAylikLabels = aylikEtiketler.Select(x => x.ToString("MM.yyyy")).ToList();
            var chartAylikData = aylikEtiketler
                .Select(x => aylikMap.TryGetValue($"{x.Year:D4}-{x.Month:D2}", out var value) ? value : 0)
                .ToList();

            var chartMarka = await devreyeTemelQuery
                .Where(x => x.Marka != null && !string.IsNullOrEmpty(x.Marka.MarkaAdi))
                .GroupBy(x => x.Marka!.MarkaAdi)
                .Select(g => new { Marka = g.Key, Sayi = g.Count() })
                .OrderByDescending(x => x.Sayi)
                .Take(6)
                .ToListAsync();

            return Ok(new YsPanelRaporSonucDto
            {
                Firma = firma == null ? null : YsPanelFirmaDto.FromEntity(firma),
                BasTarih = basTarih,
                BitTarih = bitTarih,
                DevreyeSayisi = devreyeSayisi,
                Tamamlanan = tamamlanan,
                Bekleyen = bekleyen,
                YetkiBelgesiOnayli = yetkiBelgesiOnayli,
                YetkiBelgesiBekleyen = yetkiBelgesiBekleyen,
                YetkiBelgesiReddedilen = yetkiBelgesiReddedilen,
                SonIslemler = islemler.Select(YsPanelDevreyeAlmaDto.FromEntity).ToList(),
                ChartAylikLabels = chartAylikLabels,
                ChartAylikData = chartAylikData,
                ChartDurumData = new List<int> { yetkiBelgesiOnayli, yetkiBelgesiBekleyen, yetkiBelgesiReddedilen },
                ChartMarkaLabels = chartMarka.Select(x => x.Marka ?? "-").ToList(),
                ChartMarkaData = chartMarka.Select(x => x.Sayi).ToList()
            });
        }

        [HttpPost("raporlar/pdf")]
        public async Task<IActionResult> RaporlarPdf([FromBody] YsPanelRaporFiltreDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            var dosya = await _devreyeAlmaExportApiService.YetkiliServisRaporPdfAsync(
                kullanici.FirmaId.Value,
                dto?.Bas,
                dto?.Bit,
                dto?.Ids);

            return File(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpPost("raporlar/excel")]
        public async Task<IActionResult> RaporlarExcel([FromBody] YsPanelRaporFiltreDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            var dosya = await _devreyeAlmaExportApiService.YetkiliServisRaporExcelAsync(
                kullanici.FirmaId.Value,
                dto?.Bas,
                dto?.Bit,
                dto?.Ids);

            return File(dosya.Bytes, dosya.ContentType, dosya.DosyaAdi);
        }

        [HttpPost("subeler/kaydet")]
        public async Task<IActionResult> SubeKaydet([FromBody] YsPanelSubeKaydetDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _yonetimApiService.SubeKaydetAsync(dto, kullanici));
        }

        [HttpPost("subeler/durum")]
        public async Task<IActionResult> SubeDurum([FromBody] YsPanelIdDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _yonetimApiService.SubeDurumAsync(dto, kullanici));
        }

        [HttpPost("subeler/sil")]
        public async Task<IActionResult> SubeSil([FromBody] YsPanelIdDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _yonetimApiService.SubeSilAsync(dto, kullanici));
        }

        [HttpPost("markalar/guncelle")]
        public async Task<IActionResult> MarkaGuncelle([FromBody] YsPanelMarkaGuncelleDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _yonetimApiService.MarkaGuncelleAsync(dto, kullanici));
        }

        [HttpPost("markalar/ekle")]
        public async Task<IActionResult> MarkaEkle([FromBody] YsPanelMarkaKaydetDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _yonetimApiService.MarkaEkleAsync(dto, kullanici));
        }

        [HttpPost("markalar/duzenle")]
        public async Task<IActionResult> MarkaDuzenle([FromBody] YsPanelMarkaKaydetDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _yonetimApiService.MarkaDuzenleAsync(dto, kullanici));
        }

        [HttpPost("markalar/sil")]
        public async Task<IActionResult> MarkaSil([FromBody] YsPanelIdDto? dto)
        {
            var kullanici = await AktifYetkiliServisKullaniciAsync();
            if (kullanici?.FirmaId == null)
                return Unauthorized();

            return Ok(await _yonetimApiService.MarkaSilAsync(dto, kullanici));
        }

        private async Task<AppKullanici?> AktifYetkiliServisKullaniciAsync()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null || kullanici.KullaniciTipi != 1)
                return null;

            return kullanici;
        }

        private IQueryable<Ys_Firma> FirmaDashboardQuery()
        {
            return _context.Ys_Firmalar
                .Include(x => x.Sirket)
                .Include(x => x.FirmaMarkalar!)
                    .ThenInclude(x => x.Marka)
                .Include(x => x.FirmaKategoriler!)
                    .ThenInclude(x => x.Kategori)
                .Include(x => x.Subeler)
                .Include(x => x.YetkiBelgeleri)
                .Where(x => !x.SilindiMi);
        }

        private async Task<(DateTime Bas, DateTime Bit)> GetRaporTarihAraligiAsync(int firmaId, DateTime? bas, DateTime? bit)
        {
            if (!bas.HasValue && !bit.HasValue)
            {
                var mevcutAralik = await _context.Ys_DevreyeAlmalar
                    .Where(x => x.FirmaId == firmaId && !x.SilindiMi)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Bas = g.Min(x => x.DevreyeAlmaTarihi),
                        Bit = g.Max(x => x.DevreyeAlmaTarihi)
                    })
                    .FirstOrDefaultAsync();

                if (mevcutAralik != null)
                    return (mevcutAralik.Bas.Date, mevcutAralik.Bit.Date);
            }

            var bitTarih = bit?.Date ?? DateTime.Now.Date;
            var basTarih = bas?.Date ?? bitTarih.AddDays(-30);
            return (basTarih, bitTarih);
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

        private async Task<YsPanelBildirimDto> BildirimlerAsync(int firmaId)
        {
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

            return new YsPanelBildirimDto
            {
                Bildirimler = bildirimler,
                BildirimSayisi = bildirimler.Count
            };
        }
    }

    public class YsPanelDashboardDto
    {
        public YsPanelFirmaDto? Firma { get; set; }
        public int BuAy { get; set; }
        public int Toplam { get; set; }
        public List<YsPanelDevreyeAlmaDto> SonIslemler { get; set; } = new();
        public bool IlkKurulumZorunlu { get; set; }
        public bool IlkKurulumTamamlandi { get; set; }
        public List<string> IlkKurulumEksikler { get; set; } = new();
        public int? YetkiBelgesiUyariGun { get; set; }
        public List<string> Bildirimler { get; set; } = new();
        public int BildirimSayisi { get; set; }
    }

    public class YsPanelBildirimDto
    {
        public List<string> Bildirimler { get; set; } = new();
        public int BildirimSayisi { get; set; }
    }

    public class YsPanelMarkalarDto
    {
        public YsPanelFirmaDto? Firma { get; set; }
        public List<YsPanelMarkaDto> TumMarkalar { get; set; } = new();
        public List<YsPanelFirmaMarkaDto> FirmaMarkalar { get; set; } = new();
        public List<int> SeciliMarkaIds { get; set; } = new();
    }

    public class YsPanelIlkKurulumDto
    {
        public YsPanelFirmaDto? Firma { get; set; }
        public List<YsPanelMarkaDto> TumMarkalar { get; set; } = new();
        public List<YsPanelUrunKategoriDto> TumKategoriler { get; set; } = new();
        public List<int> SeciliMarkaIds { get; set; } = new();
        public List<int> SeciliKategoriIds { get; set; } = new();
        public int AktifSubeSayisi { get; set; }
        public bool YetkiBelgesiVar { get; set; }
        public bool OnayliYetkiBelgesiVar { get; set; }
        public bool ZorunluMu { get; set; }
        public bool TamamlandiMi { get; set; }
        public List<string> Eksikler { get; set; } = new();
        public string? HataMesaji { get; set; }
    }

    public class YsPanelRaporFiltreDto
    {
        public DateTime? Bas { get; set; }
        public DateTime? Bit { get; set; }
        public List<int>? Ids { get; set; }
        public int? Limit { get; set; }
    }

    public class YsPanelRaporSonucDto
    {
        public YsPanelFirmaDto? Firma { get; set; }
        public DateTime BasTarih { get; set; }
        public DateTime BitTarih { get; set; }
        public int DevreyeSayisi { get; set; }
        public int Tamamlanan { get; set; }
        public int Bekleyen { get; set; }
        public int YetkiBelgesiOnayli { get; set; }
        public int YetkiBelgesiBekleyen { get; set; }
        public int YetkiBelgesiReddedilen { get; set; }
        public List<YsPanelDevreyeAlmaDto> SonIslemler { get; set; } = new();
        public List<string> ChartAylikLabels { get; set; } = new();
        public List<int> ChartAylikData { get; set; } = new();
        public List<int> ChartDurumData { get; set; } = new();
        public List<string> ChartMarkaLabels { get; set; } = new();
        public List<int> ChartMarkaData { get; set; } = new();
    }

    public class YsPanelIdDto
    {
        public int Id { get; set; }
    }

    public class YsPanelSubeKaydetDto
    {
        public int Id { get; set; }
        public string? SubeAdi { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? Telefon { get; set; }
        public string? Adres { get; set; }
        public bool AktifMi { get; set; }
    }

    public class YsPanelProfilGuncelleDto
    {
        public string? AdSoyad { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
    }

    public class YsPanelMarkaGuncelleDto
    {
        public List<int> MarkaIds { get; set; } = new();
    }

    public class YsPanelMarkaKaydetDto
    {
        public int Id { get; set; }
        public string? MarkaAdi { get; set; }
        public string? Aciklama { get; set; }
    }

    public class YsPanelIslemSonucDto
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }

        public static YsPanelIslemSonucDto BasariliSonuc(string mesaj)
        {
            return new YsPanelIslemSonucDto { Basarili = true, Mesaj = mesaj };
        }

        public static YsPanelIslemSonucDto Basarisiz(string mesaj)
        {
            return new YsPanelIslemSonucDto { Basarili = false, Mesaj = mesaj };
        }
    }

    public class YsPanelFirmaDto
    {
        public int Id { get; set; }
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? VergiNo { get; set; }
        public string? FaaliyetIli { get; set; }
        public int SirketId { get; set; }
        public YsPanelSirketDto? Sirket { get; set; }
        public List<YsPanelYetkiBelgesiDto> YetkiBelgeleri { get; set; } = new();
        public List<YsPanelFirmaMarkaDto> FirmaMarkalar { get; set; } = new();
        public List<YsPanelFirmaKategoriDto> FirmaKategoriler { get; set; } = new();
        public List<YsPanelSubeDto> Subeler { get; set; } = new();

        public static YsPanelFirmaDto FromEntity(Ys_Firma firma)
        {
            return new YsPanelFirmaDto
            {
                Id = firma.Id,
                FirmaAdi = firma.FirmaAdi,
                YetkiliKisi = firma.YetkiliKisi,
                Telefon = firma.Telefon,
                Email = firma.Email,
                Adres = firma.Adres,
                VergiNo = firma.VergiNo,
                FaaliyetIli = firma.FaaliyetIli,
                SirketId = firma.SirketId,
                Sirket = firma.Sirket == null ? null : YsPanelSirketDto.FromEntity(firma.Sirket),
                YetkiBelgeleri = firma.YetkiBelgeleri?.Select(YsPanelYetkiBelgesiDto.FromEntity).ToList() ?? new(),
                FirmaMarkalar = firma.FirmaMarkalar?.Select(YsPanelFirmaMarkaDto.FromEntity).ToList() ?? new(),
                FirmaKategoriler = firma.FirmaKategoriler?.Select(YsPanelFirmaKategoriDto.FromEntity).ToList() ?? new(),
                Subeler = firma.Subeler?.Select(YsPanelSubeDto.FromEntity).ToList() ?? new()
            };
        }
    }

    public class YsPanelSirketDto
    {
        public int Id { get; set; }
        public string? SirketAdi { get; set; }
        public string? Il { get; set; }

        public static YsPanelSirketDto FromEntity(Dag_Sirket sirket)
        {
            return new YsPanelSirketDto
            {
                Id = sirket.Id,
                SirketAdi = sirket.SirketAdi,
                Il = sirket.Il
            };
        }
    }

    public class YsPanelYetkiBelgesiDto
    {
        public int Id { get; set; }
        public int Durum { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public DateTime? YetkiBelgesiBaslangicTarihi { get; set; }
        public DateTime YetkiBelgesiBitisTarihi { get; set; }
        public bool SilindiMi { get; set; }

        public static YsPanelYetkiBelgesiDto FromEntity(Ys_YetkiBelgesi belge)
        {
            return new YsPanelYetkiBelgesiDto
            {
                Id = belge.Id,
                Durum = belge.Durum,
                OlusturmaTarihi = belge.OlusturmaTarihi,
                YetkiBelgesiBaslangicTarihi = belge.YetkiBelgesiBaslangicTarihi,
                YetkiBelgesiBitisTarihi = belge.YetkiBelgesiBitisTarihi,
                SilindiMi = belge.SilindiMi
            };
        }
    }

    public class YsPanelFirmaMarkaDto
    {
        public int Id { get; set; }
        public int MarkaId { get; set; }
        public bool SilindiMi { get; set; }
        public YsPanelMarkaDto? Marka { get; set; }

        public static YsPanelFirmaMarkaDto FromEntity(Ys_FirmaMarka firmaMarka)
        {
            return new YsPanelFirmaMarkaDto
            {
                Id = firmaMarka.Id,
                MarkaId = firmaMarka.MarkaId,
                SilindiMi = firmaMarka.SilindiMi,
                Marka = firmaMarka.Marka == null ? null : YsPanelMarkaDto.FromEntity(firmaMarka.Marka)
            };
        }
    }

    public class YsPanelMarkaDto
    {
        public int Id { get; set; }
        public string? MarkaAdi { get; set; }
        public bool AktifMi { get; set; }

        public static YsPanelMarkaDto FromEntity(Ys_Marka marka)
        {
            return new YsPanelMarkaDto
            {
                Id = marka.Id,
                MarkaAdi = marka.MarkaAdi,
                AktifMi = marka.AktifMi
            };
        }
    }

    public class YsPanelFirmaKategoriDto
    {
        public int Id { get; set; }
        public int KategoriId { get; set; }
        public bool SilindiMi { get; set; }
        public YsPanelUrunKategoriDto? Kategori { get; set; }

        public static YsPanelFirmaKategoriDto FromEntity(Ys_FirmaKategori kategori)
        {
            return new YsPanelFirmaKategoriDto
            {
                Id = kategori.Id,
                KategoriId = kategori.KategoriId,
                SilindiMi = kategori.SilindiMi,
                Kategori = kategori.Kategori == null ? null : YsPanelUrunKategoriDto.FromEntity(kategori.Kategori)
            };
        }
    }

    public class YsPanelUrunKategoriDto
    {
        public int Id { get; set; }
        public string? Ad { get; set; }
        public bool AktifMi { get; set; }

        public static YsPanelUrunKategoriDto FromEntity(UrunKategori kategori)
        {
            return new YsPanelUrunKategoriDto
            {
                Id = kategori.Id,
                Ad = kategori.Ad,
                AktifMi = kategori.AktifMi
            };
        }
    }

    public class YsPanelSubeDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string? SubeAdi { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? Telefon { get; set; }
        public string? Adres { get; set; }
        public bool AktifMi { get; set; }
        public bool SilindiMi { get; set; }
        public DateTime OlusturmaTarihi { get; set; }

        public static YsPanelSubeDto FromEntity(Ys_Sube sube)
        {
            return new YsPanelSubeDto
            {
                Id = sube.Id,
                FirmaId = sube.FirmaId,
                SubeAdi = sube.SubeAdi,
                Il = sube.Il,
                Ilce = sube.Ilce,
                Telefon = sube.Telefon,
                Adres = sube.Adres,
                AktifMi = sube.AktifMi,
                SilindiMi = sube.SilindiMi,
                OlusturmaTarihi = sube.OlusturmaTarihi
            };
        }
    }

    public class YsPanelDevreyeAlmaDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public int? MarkaId { get; set; }
        public string? TesistatNo { get; set; }
        public string? MusteriAdi { get; set; }
        public string? MusteriTelefon { get; set; }
        public string? MusteriTcNo { get; set; }
        public string? Adres { get; set; }
        public string? CihazTipi { get; set; }
        public string? CihazMarka { get; set; }
        public string? CihazModeli { get; set; }
        public string? SeriNo { get; set; }
        public string? CihazKapasite { get; set; }
        public string? TeknisyenAdi { get; set; }
        public string? TeknisyenYetkiBelgesiNo { get; set; }
        public int Durum { get; set; }
        public DateTime DevreyeAlmaTarihi { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public string? Notlar { get; set; }
        public YsPanelFirmaDto? Firma { get; set; }
        public YsPanelMarkaDto? Marka { get; set; }

        public static YsPanelDevreyeAlmaDto FromEntity(Ys_DevreyeAlma islem)
        {
            return new YsPanelDevreyeAlmaDto
            {
                Id = islem.Id,
                FirmaId = islem.FirmaId,
                MarkaId = islem.MarkaId,
                TesistatNo = islem.TesistatNo,
                MusteriAdi = islem.MusteriAdi,
                MusteriTelefon = islem.MusteriTelefon,
                MusteriTcNo = islem.MusteriTcNo,
                Adres = islem.Adres,
                CihazTipi = islem.CihazTipi,
                CihazMarka = islem.CihazMarka,
                CihazModeli = islem.CihazModeli,
                SeriNo = islem.SeriNo,
                CihazKapasite = islem.CihazKapasite,
                TeknisyenAdi = islem.TeknisyenAdi,
                TeknisyenYetkiBelgesiNo = islem.TeknisyenYetkiBelgesiNo,
                Durum = islem.Durum,
                DevreyeAlmaTarihi = islem.DevreyeAlmaTarihi,
                OlusturmaTarihi = islem.OlusturmaTarihi,
                Notlar = islem.Notlar,
                Firma = islem.Firma == null ? null : YsPanelFirmaDto.FromEntity(islem.Firma),
                Marka = islem.Marka == null ? null : YsPanelMarkaDto.FromEntity(islem.Marka)
            };
        }
    }
}
