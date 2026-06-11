using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services
{
    public class SmsDogrulamaService
    {
        private readonly AppDbContext _context;
        private readonly SmsOptions _options;
        private readonly ISmsProvider _smsProvider;
        private readonly SehirFirmaKoduService _sehirFirmaKoduService;

        public SmsDogrulamaService(
            AppDbContext context,
            IOptions<SmsOptions> options,
            ISmsProvider smsProvider,
            SehirFirmaKoduService sehirFirmaKoduService)
        {
            _context = context;
            _options = options.Value;
            _smsProvider = smsProvider;
            _sehirFirmaKoduService = sehirFirmaKoduService;
        }

        public bool SmsGirisAktifMi => _options.Enabled;

        public async Task<(bool Basarili, string Mesaj)> KodGonderAsync(AppKullanici kullanici, string amac = "GIRIS")
        {
            var telefon = TelefonNormalize(kullanici.PhoneNumber);
            if (string.IsNullOrWhiteSpace(telefon))
                return (false, "SMS doğrulama için kullanıcı telefon numarası tanımlı olmalıdır.");

            if (!_options.TestMode && string.Equals(_options.Provider, "Development", StringComparison.OrdinalIgnoreCase))
                return (false, "Canlı SMS için Provider AhlatciSms ve API bilgileri yapılandırılmalıdır.");

            amac = string.IsNullOrWhiteSpace(amac) ? "GIRIS" : amac.Trim().ToUpperInvariant();
            var kod = KodUret(_options.CodeLength);
            var mesaj = amac == "SIFRE_SIFIRLA"
                ? $"Yetkili Servis Devreye Alma şifre sıfırlama kodunuz: {kod}"
                : $"Yetkili Servis Devreye Alma giriş doğrulama kodunuz: {kod}";

            var dogrulama = new SmsDogrulamaKodu
            {
                KullaniciId = kullanici.Id,
                Telefon = telefon,
                KodHash = Hashle(kullanici.Id, kod),
                Amac = amac,
                GecerlilikTarihi = DateTime.Now.AddMinutes(Math.Max(1, _options.CodeExpireMinutes)),
                OlusturanKullanici = kullanici.UserName
            };

            _context.SmsDogrulamaKodlari.Add(dogrulama);

            var firmaKodu = await FirmaKoduBulAsync(kullanici);
            var sonuc = await _smsProvider.GonderAsync(telefon, mesaj, firmaKodu);
            _context.SmsGonderimLoglari.Add(new SmsGonderimLog
            {
                KullaniciId = kullanici.Id,
                Telefon = telefon,
                Mesaj = mesaj,
                Saglayici = string.IsNullOrWhiteSpace(_options.Provider) ? _smsProvider.ProviderName : _options.Provider,
                BasariliMi = sonuc.Basarili,
                SaglayiciMesajId = sonuc.MesajId,
                HataMesaji = sonuc.Hata,
                OlusturanKullanici = kullanici.UserName
            });

            await _context.SaveChangesAsync();

            if (!sonuc.Basarili)
                return (false, sonuc.Hata ?? "SMS gönderilemedi.");

            if (_options.TestMode)
                return (true, $"Test SMS modu: doğrulama kodu {kod}");

            return (true, "Doğrulama kodu SMS olarak gönderildi.");
        }

        public async Task<(bool Basarili, string Mesaj)> KodDogrulaAsync(string kullaniciId, string kod, string amac = "GIRIS")
        {
            if (string.IsNullOrWhiteSpace(kullaniciId) || string.IsNullOrWhiteSpace(kod))
                return (false, "Doğrulama kodu zorunludur.");

            amac = string.IsNullOrWhiteSpace(amac) ? "GIRIS" : amac.Trim().ToUpperInvariant();

            var kayit = await _context.SmsDogrulamaKodlari
                .Where(x => x.KullaniciId == kullaniciId
                    && x.Amac == amac
                    && !x.SilindiMi
                    && !x.KullanildiMi
                    && x.GecerlilikTarihi >= DateTime.Now)
                .OrderByDescending(x => x.OlusturmaTarihi)
                .FirstOrDefaultAsync();

            if (kayit == null)
                return (false, "Doğrulama kodu bulunamadı veya süresi doldu.");

            kayit.DenemeSayisi++;
            kayit.GuncellemeTarihi = DateTime.Now;

            if (kayit.DenemeSayisi > Math.Max(1, _options.MaxAttempts))
            {
                kayit.SilindiMi = true;
                kayit.SilinmeTarihi = DateTime.Now;
                await _context.SaveChangesAsync();
                return (false, "Çok fazla hatalı deneme yapıldı. Lütfen yeniden giriş yapın.");
            }

            if (!string.Equals(kayit.KodHash, Hashle(kullaniciId, kod.Trim()), StringComparison.Ordinal))
            {
                await _context.SaveChangesAsync();
                return (false, "Doğrulama kodu hatalı.");
            }

            kayit.KullanildiMi = true;
            kayit.KullanildiTarihi = DateTime.Now;
            await _context.SaveChangesAsync();
            return (true, "SMS doğrulama tamamlandı.");
        }

        private async Task<string?> FirmaKoduBulAsync(AppKullanici kullanici)
        {
            if (kullanici.FirmaId.HasValue)
            {
                var firma = await _context.Ys_Firmalar
                    .Include(x => x.Sirket)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId.Value && !x.SilindiMi);

                return _sehirFirmaKoduService.FirmaKodu(firma?.FaaliyetIli)
                    ?? _sehirFirmaKoduService.FirmaKodu(firma?.Sirket?.Il)
                    ?? firma?.Sirket?.SirketAdi;
            }

            if (kullanici.SirketId.HasValue)
            {
                var sirket = await _context.Dag_Sirketler
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == kullanici.SirketId.Value && !x.SilindiMi);

                return _sehirFirmaKoduService.FirmaKodu(sirket?.Il)
                    ?? sirket?.SirketAdi;
            }

            return kullanici.Sirket?.SirketAdi
                ?? kullanici.Firma?.Sirket?.SirketAdi
                ?? _sehirFirmaKoduService.FirmaKodu(kullanici.Firma?.FaaliyetIli)
                ?? _sehirFirmaKoduService.FirmaKodu(kullanici.Firma?.Sirket?.Il);
        }

        private static string KodUret(int uzunluk)
        {
            uzunluk = Math.Clamp(uzunluk, 4, 8);
            var max = (int)Math.Pow(10, uzunluk);
            var min = (int)Math.Pow(10, uzunluk - 1);
            return RandomNumberGenerator.GetInt32(min, max).ToString();
        }

        private static string Hashle(string kullaniciId, string kod)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{kullaniciId}:{kod}"));
            return Convert.ToHexString(bytes);
        }

        private static string TelefonNormalize(string? telefon)
        {
            if (string.IsNullOrWhiteSpace(telefon))
                return string.Empty;

            var rakamlar = new string(telefon.Where(char.IsDigit).ToArray());
            if (rakamlar.StartsWith("0") && rakamlar.Length == 11)
                rakamlar = "90" + rakamlar[1..];

            return rakamlar;
        }
    }
}
