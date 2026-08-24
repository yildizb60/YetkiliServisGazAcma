using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Business.Services.Online;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/ykc")]
    [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel,SertifikaliFirma")]
    public class YkcApiController : ControllerBase
    {
        private readonly YkcTalepService _ykcTalepService;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly AppDbContext _context;
        private readonly OnlineCihazBilgileriClient _onlineCihazBilgileriClient;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;
        private readonly YkcImzaAkisService _ykcImzaAkisService;
        private readonly YkcYetkiService _ykcYetkiService;

        public YkcApiController(
            YkcTalepService ykcTalepService,
            UserManager<AppKullanici> userManager,
            IWebHostEnvironment environment,
            AppDbContext context,
            OnlineCihazBilgileriClient onlineCihazBilgileriClient,
            SehirFirmaKoduService sehirFirmaKoduService,
            YkcImzaAkisService ykcImzaAkisService,
            YkcYetkiService ykcYetkiService)
        {
            _ykcTalepService = ykcTalepService;
            _userManager = userManager;
            _environment = environment;
            _context = context;
            _onlineCihazBilgileriClient = onlineCihazBilgileriClient;
            _sehirFirmaKoduService = sehirFirmaKoduService;
            _ykcImzaAkisService = ykcImzaAkisService;
            _ykcYetkiService = ykcYetkiService;
        }

        [HttpPost("tesisat-sorgula")]
        public async Task<IActionResult> TesisatSorgula([FromBody] YkcTesisatSorguIstek? istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadi." });

            var ykcYetkileri = await _ykcYetkiService.OzetAsync(kullanici, kullanici.SirketId, HttpContext.RequestAborted);
            if (!ykcYetkileri.TalepOlusturabilir)
                return YkcYetkisiz("Tesisat sorgulama ve talep oluşturma yetkiniz bulunmuyor.");

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
                    servisSonuc?.HataMesaji ?? "Servisten bilgi alinamadi. Lutfen daha sonra tekrar sorgulayin."));
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

            if (!await YkcYetkiliMiAsync(kullanici, YetkiTipleri.YKC_TALEP_GOR))
                return YkcYetkisiz("YKC taleplerini görüntüleme yetkiniz bulunmuyor.");

            var sonuc = await _ykcTalepService.ListeAsync(
                filtre ?? new YkcTalepListeFiltre(),
                kullanici,
                await GenelYetkiliMiAsync(kullanici));

            return Ok(sonuc);
        }

        [HttpPost("dashboard/ozet")]
        public async Task<IActionResult> DashboardOzet()
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (!await YkcYetkiliMiAsync(kullanici, YetkiTipleri.YKC_TALEP_GOR))
                return YkcYetkisiz("YKC özetini görüntüleme yetkiniz bulunmuyor.");

            var sonuc = await _ykcTalepService.DashboardOzetAsync(
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

            if (!await YkcYetkiliMiAsync(kullanici, YetkiTipleri.YKC_RAPOR_GOR))
                return YkcYetkisiz("YKC raporlarını görüntüleme yetkiniz bulunmuyor.");

            var sonuc = await _ykcTalepService.RaporAsync(
                filtre ?? new YkcTalepListeFiltre(),
                kullanici,
                await GenelYetkiliMiAsync(kullanici));

            return Ok(sonuc);
        }

        [HttpPost("imza/entegrasyon")]
        public IActionResult ImzaEntegrasyonBilgisi()
        {
            return Ok(_ykcImzaAkisService.EntegrasyonBilgisi());
        }

        [HttpPost("talepler/imzaya-gonder")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> ImzayaGonder([FromBody] YkcTalepGetirIstek? istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (istek == null || istek.Id <= 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("İmza gönderimi için talep id zorunludur."));

            if (!await YkcYetkiliMiAsync(kullanici, YetkiTipleri.YKC_FR265_IMZA_ISLEM))
                return YkcYetkisiz("FR265 ve dijital imza işlemi yetkiniz bulunmuyor.");

            if (!_ykcImzaAkisService.EntegrasyonBilgisi().KullanilabilirMi)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    YkcIslemSonuc.HataliSonuc("Dijital imza sağlayıcısı henüz yapılandırılmadı; belge gönderilmedi."));
            }

            var sonuc = await _ykcImzaAkisService.ImzayaGonderAsync(
                istek.Id,
                kullanici,
                await GenelYetkiliMiAsync(kullanici),
                HttpContext.RequestAborted);

            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        [HttpPost("talepler/imza-durum-sorgula")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> ImzaDurumSorgula([FromBody] YkcTalepGetirIstek? istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (istek == null || istek.Id <= 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("İmza durumu için talep id zorunludur."));

            if (!await YkcYetkiliMiAsync(kullanici, YetkiTipleri.YKC_FR265_IMZA_ISLEM))
                return YkcYetkisiz("FR265 ve dijital imza işlemi yetkiniz bulunmuyor.");

            if (!_ykcImzaAkisService.EntegrasyonBilgisi().KullanilabilirMi)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    YkcIslemSonuc.HataliSonuc("Dijital imza sağlayıcısı henüz yapılandırılmadı."));
            }

            var sonuc = await _ykcImzaAkisService.ImzaDurumunuSorgulaAsync(
                istek.Id,
                kullanici,
                await GenelYetkiliMiAsync(kullanici),
                HttpContext.RequestAborted);

            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        [HttpPost("talepler/dosya-indir")]
        public async Task<IActionResult> DosyaIndir([FromBody] YkcDosyaGetirIstek? istek)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadi." });

            if (istek == null || istek.Id <= 0)
                return BadRequest(new { basarili = false, mesaj = "Dosya id zorunludur." });

            if (!await YkcYetkiliMiAsync(kullanici, YetkiTipleri.YKC_TALEP_GOR))
                return YkcYetkisiz("YKC belge görüntüleme yetkiniz bulunmuyor.");

            var dosya = await _context.Ykc_FormDosyalari
                .Include(x => x.Talep)
                    .ThenInclude(x => x!.ImzaSurecleri)
                .FirstOrDefaultAsync(x => x.Id == istek.Id && !x.SilindiMi && x.Talep != null && !x.Talep.SilindiMi);

            if (dosya?.Talep == null)
                return NotFound(new { basarili = false, mesaj = "Cihaz degisim form dosyasi bulunamadi." });

            if (!await TalepDosyasinaYetkiliMiAsync(dosya.Talep, kullanici))
                return Forbid();

            if (!YkcDosyasiIndirmeyeAcikMi(dosya))
                return Forbid();

            if (dosya.DosyaTuru == YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai)
            {
                var genelYetkili = await GenelYetkiliMiAsync(kullanici);
                var yenilendi = await _ykcImzaAkisService.DemoNihaiBelgeyiYenileAsync(
                    dosya.Id,
                    kullanici,
                    genelYetkili,
                    HttpContext.RequestAborted);

                if (yenilendi)
                {
                    dosya = await _context.Ykc_FormDosyalari
                        .Include(x => x.Talep)
                            .ThenInclude(x => x!.ImzaSurecleri)
                        .FirstOrDefaultAsync(x => x.Id == istek.Id && !x.SilindiMi && x.Talep != null && !x.Talep.SilindiMi);

                    if (dosya?.Talep == null)
                        return NotFound(new { basarili = false, mesaj = "Cihaz degisim form dosyasi bulunamadi." });
                }
            }

            if (string.IsNullOrWhiteSpace(dosya.DosyaYolu))
                return NotFound(new { basarili = false, mesaj = "Dosya yolu bulunamadi." });

            var fizikselYol = ResolveYkcBelgeYolu(dosya);
            if (string.IsNullOrWhiteSpace(fizikselYol))
                return NotFound(new { basarili = false, mesaj = "Dosya yolu gecersiz." });

            var kokYol = Path.GetFullPath(BelgeKokYolu(dosya));

            if (!fizikselYol.StartsWith(kokYol + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            if (!System.IO.File.Exists(fizikselYol))
                return NotFound(new { basarili = false, mesaj = "Dosya fiziksel olarak bulunamadi." });

            var bytes = await System.IO.File.ReadAllBytesAsync(fizikselYol, HttpContext.RequestAborted);
            Response.Headers.CacheControl = "private, no-store";
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

            if (!await YkcYetkiliMiAsync(kullanici, YetkiTipleri.YKC_TALEP_GOR))
                return YkcYetkisiz("YKC talep detayını görüntüleme yetkiniz bulunmuyor.");

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

            var ykcYetkileri = await _ykcYetkiService.OzetAsync(kullanici, kullanici.SirketId, HttpContext.RequestAborted);
            if (!ykcYetkileri.TalepOlusturabilir)
                return YkcYetkisiz("YKC talebi oluşturma yetkiniz bulunmuyor.");

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

            if (!await YkcYetkiliMiAsync(kullanici, YetkiTipleri.YKC_ATAMA_YAP))
                return YkcYetkisiz("YKC atama ve randevu işlemi yetkiniz bulunmuyor.");

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

            if (!await DurumGuncellemeYetkiliMiAsync(kullanici, dto.Durum))
                return YkcYetkisiz("Bu YKC durum işlemi için yetkiniz bulunmuyor.");

            var sonuc = await _ykcTalepService.DurumGuncelleAsync(dto, kullanici, await GenelYetkiliMiAsync(kullanici));
            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        [HttpPost("talepler/kontroller-kaydet")]
        [Authorize(Roles = "GenelSistemAdmin,SuperAdmin,SirketAdmin,Personel")]
        public async Task<IActionResult> KontrollerKaydet([FromBody] YkcKontrolKaydetDto? dto)
        {
            var kullanici = await AktifKullaniciAsync();
            if (kullanici == null)
                return Unauthorized(new { basarili = false, mesaj = "Oturum bulunamadı." });

            if (dto == null || dto.TalepId <= 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("Kontrol kaydı için talep id zorunludur."));

            if (!await YkcYetkiliMiAsync(kullanici, YetkiTipleri.YKC_FR265_IMZA_ISLEM))
                return YkcYetkisiz("FR265 kontrol işlemi yetkiniz bulunmuyor.");

            var sonuc = await _ykcTalepService.KontrolleriKaydetAsync(dto, kullanici, await GenelYetkiliMiAsync(kullanici));
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

            if (!await YkcYetkiliMiAsync(kullanici, YetkiTipleri.YKC_FR265_IMZA_ISLEM))
                return YkcYetkisiz("YKC teknik belge işlemi yetkiniz bulunmuyor.");

            if (!YkcFormDosyasiGecerliMi(dto.DosyaAdi ?? dto.DosyaYolu, dto.IcerikTipi, icerikTipiZorunlu: false))
                return BadRequest(YkcIslemSonuc.HataliSonuc("Sadece PDF, JPG veya PNG form dosyasi kaydedilebilir."));

            var roller = await _userManager.GetRolesAsync(kullanici);
            var dosyaTuru = string.IsNullOrWhiteSpace(dto.DosyaTuru)
                ? YkcFormDosyaTuruDegerleri.TeknikEk
                : dto.DosyaTuru.Trim();

            var icOperasyon = IcOperasyonRoluVarMi(roller);
            if (!ElleYuklenebilirBelgeTuruMu(dosyaTuru, icOperasyon))
                return BadRequest(YkcIslemSonuc.HataliSonuc("Bu belge türü kullanıcı yüklemesine açık değildir."));

            if (!string.Equals(dto.DepolamaTuru, YkcDepolamaTuruDegerleri.Private, StringComparison.OrdinalIgnoreCase)
                || !PrivateDepolamaAnahtariGecerliMi(dto.DosyaYolu, dto.TalepId))
            {
                return BadRequest(YkcIslemSonuc.HataliSonuc("Dosya yalnızca YKC private storage anahtarıyla kaydedilebilir."));
            }

            dto.DosyaTuru = dosyaTuru;
            dto.DepolamaTuru = YkcDepolamaTuruDegerleri.Private;
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

            if (!await YkcYetkiliMiAsync(kullanici, YetkiTipleri.YKC_FR265_IMZA_ISLEM))
                return YkcYetkisiz("YKC teknik belge işlemi yetkiniz bulunmuyor.");

            if (istek.Dosya == null || istek.Dosya.Length == 0)
                return BadRequest(YkcIslemSonuc.HataliSonuc("Yüklenecek form dosyası zorunludur."));

            if (!YkcFormDosyasiGecerliMi(istek.Dosya.FileName, istek.Dosya.ContentType, icerikTipiZorunlu: true))
                return BadRequest(YkcIslemSonuc.HataliSonuc("Sadece PDF, JPG veya PNG form dosyasi yuklenebilir."));

            var roller = await _userManager.GetRolesAsync(kullanici);
            var dosyaTuru = string.IsNullOrWhiteSpace(istek.DosyaTuru)
                ? YkcFormDosyaTuruDegerleri.TeknikEk
                : istek.DosyaTuru.Trim();

            var icOperasyon = IcOperasyonRoluVarMi(roller);
            if (!ElleYuklenebilirBelgeTuruMu(dosyaTuru, icOperasyon))
                return BadRequest(YkcIslemSonuc.HataliSonuc("Bu belge türü kullanıcı yüklemesine açık değildir."));

            var klasor = Path.Combine(PrivateYkcBelgeRoot(), istek.TalepId.ToString());
            Directory.CreateDirectory(klasor);

            var dosyaAdi = GuvenliDosyaAdi(istek.Dosya.FileName);
            var kayitAdi = $"{Guid.NewGuid():N}_{dosyaAdi}";
            var fizikselYol = Path.Combine(klasor, kayitAdi);

            await using (var stream = System.IO.File.Create(fizikselYol))
            {
                await istek.Dosya.CopyToAsync(stream);
            }

            var belgeHash = await DosyaHashAsync(fizikselYol);
            var depolamaAnahtari = $"ykc/{istek.TalepId}/{kayitAdi}";
            var sonuc = await _ykcTalepService.DosyaEkleAsync(new YkcDosyaKaydetDto
            {
                TalepId = istek.TalepId,
                DosyaTuru = dosyaTuru,
                DosyaAdi = dosyaAdi,
                DosyaYolu = depolamaAnahtari,
                IcerikTipi = istek.Dosya.ContentType,
                DosyaBoyutu = istek.Dosya.Length,
                DepolamaTuru = YkcDepolamaTuruDegerleri.Private,
                BelgeHash = belgeHash
            }, kullanici, await GenelYetkiliMiAsync(kullanici));

            if (!sonuc.Basarili && System.IO.File.Exists(fizikselYol))
                System.IO.File.Delete(fizikselYol);

            return sonuc.Basarili ? Ok(sonuc) : BadRequest(sonuc);
        }

        private async Task<bool> YkcYetkiliMiAsync(AppKullanici kullanici, string yetkiTipi)
        {
            return await _ykcYetkiService.YetkiliMiAsync(
                kullanici,
                yetkiTipi,
                kullanici.SirketId,
                HttpContext.RequestAborted);
        }

        private async Task<bool> DurumGuncellemeYetkiliMiAsync(AppKullanici kullanici, int yeniDurum)
        {
            var yetkiler = await _ykcYetkiService.OzetAsync(
                kullanici,
                kullanici.SirketId,
                HttpContext.RequestAborted);

            return yeniDurum switch
            {
                YkcDurumDegerleri.SahaIsleminde => yetkiler.AtamaYapabilir || yetkiler.Fr265ImzaIslemiYapabilir,
                YkcDurumDegerleri.Tamamlandi => yetkiler.Fr265ImzaIslemiYapabilir,
                _ => yetkiler.AtamaYapabilir
            };
        }

        private ObjectResult YkcYetkisiz(string mesaj)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                YkcIslemSonuc.HataliSonuc(mesaj));
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

        private string WebRootPath()
        {
            return string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                : _environment.WebRootPath;
        }

        private string PrivateYkcBelgeRoot()
        {
            return Path.Combine(_environment.ContentRootPath, "App_Data", "ykc-belgeler");
        }

        private string BelgeKokYolu(Ykc_FormDosya dosya)
        {
            var yol = dosya.DosyaYolu?.Trim().Replace('\\', '/').TrimStart('/') ?? "";
            if (yol.StartsWith("uploads/ykc/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(dosya.DepolamaTuru, YkcDepolamaTuruDegerleri.LegacyWwwroot, StringComparison.OrdinalIgnoreCase))
            {
                return WebRootPath();
            }

            return PrivateYkcBelgeRoot();
        }

        private string? ResolveYkcBelgeYolu(Ykc_FormDosya dosya)
        {
            var yol = dosya.DosyaYolu?.Trim().Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(yol))
                return null;

            if (yol.StartsWith("uploads/ykc/", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(Path.Combine(
                    WebRootPath(),
                    yol.Replace('/', Path.DirectorySeparatorChar)));
            }

            if (yol.StartsWith("ykc/", StringComparison.OrdinalIgnoreCase))
                yol = yol["ykc/".Length..];

            return Path.GetFullPath(Path.Combine(
                PrivateYkcBelgeRoot(),
                yol.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static async Task<string> DosyaHashAsync(string fizikselYol)
        {
            await using var stream = System.IO.File.OpenRead(fizikselYol);
            var bytes = await SHA256.HashDataAsync(stream);
            return Convert.ToHexString(bytes);
        }

        private static bool IcOperasyonRoluVarMi(IEnumerable<string> roller)
        {
            return roller.Any(x => x is "GenelSistemAdmin" or "SuperAdmin" or "SirketAdmin" or "Personel");
        }

        private static bool ElleYuklenebilirBelgeTuruMu(string dosyaTuru, bool icOperasyon)
        {
            return dosyaTuru == YkcFormDosyaTuruDegerleri.TeknikEk;
        }

        private static bool YkcDosyasiIndirmeyeAcikMi(Ykc_FormDosya dosya)
        {
            if (dosya.DosyaTuru == YkcFormDosyaTuruDegerleri.TeknikEk)
                return true;

            if (dosya.DosyaTuru != YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai)
                return false;

            return dosya.Talep?.ImzaSurecleri.Any(s =>
                !s.SilindiMi
                && s.Durum == YkcImzaDurumDegerleri.Tamamlandi
                && !string.IsNullOrWhiteSpace(s.ProviderDocumentId)
                && s.NihaiDosyaId == dosya.Id) == true;
        }

        private static bool PrivateDepolamaAnahtariGecerliMi(string? dosyaYolu, int talepId)
        {
            var yol = dosyaYolu?.Trim().Replace('\\', '/').TrimStart('/');
            return !string.IsNullOrWhiteSpace(yol)
                && !yol.Contains("..", StringComparison.Ordinal)
                && yol.StartsWith($"ykc/{talepId}/", StringComparison.OrdinalIgnoreCase);
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
