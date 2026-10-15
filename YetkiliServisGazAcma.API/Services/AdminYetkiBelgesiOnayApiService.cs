using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services
{
    public class AdminYetkiBelgesiOnayApiService
    {
        private readonly AppDbContext _context;

        public AdminYetkiBelgesiOnayApiService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AdminYetkiBelgesiOnayListeDto> ListeleAsync(
            int? sirketId, YetkiBelgesiOnayFiltreDto? filtre = null, CancellationToken cancellationToken = default)
        {
            var (kayitlar, sayfalama) = await OnaySayfasiAsync(_context, sirketId, filtre, cancellationToken);
            var belgeler = kayitlar.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList();

            return new AdminYetkiBelgesiOnayListeDto
            {
                Sayfalama = sayfalama,
                Bekleyenler = sayfalama.Durum == "bekleyen" ? belgeler : [],
                SuresiDolanlar = sayfalama.Durum == "suresi-dolan" ? belgeler : [],
                Onaylananlar = sayfalama.Durum == "onaylanan" ? belgeler : [],
                Reddedilenler = sayfalama.Durum == "reddedilen" ? belgeler : []
            };
        }

        internal static async Task<(List<Ys_YetkiBelgesi> Kayitlar, YetkiBelgesiOnaySayfalamaDto Sayfalama)> OnaySayfasiAsync(
            AppDbContext context, int? sirketId, YetkiBelgesiOnayFiltreDto? filtre, CancellationToken cancellationToken)
        {
            filtre ??= new();
            var bugun = DateTime.Today;
            var query = context.Ys_YetkiBelgeleri.AsNoTracking()
                .Where(x => !x.SilindiMi && x.Firma != null && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId)
                    && (x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
                        || x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi || x.Durum == YetkiBelgesiDurumDegerleri.Reddedildi));
            if (!string.IsNullOrWhiteSpace(filtre.Firma))
            {
                var firma = filtre.Firma.Trim();
                query = query.Where(x => (x.Firma!.FirmaAdi != null && x.Firma.FirmaAdi.Contains(firma))
                    || (x.Firma.VergiNo != null && x.Firma.VergiNo.Contains(firma)));
            }
            if (!string.IsNullOrWhiteSpace(filtre.Sirket))
            {
                var sirket = filtre.Sirket.Trim();
                query = query.Where(x => x.Firma!.Sirket != null && x.Firma.Sirket.SirketAdi != null
                    && x.Firma.Sirket.SirketAdi.Contains(sirket));
            }
            if (!string.IsNullOrWhiteSpace(filtre.Adres))
            {
                var adres = filtre.Adres.Trim();
                query = query.Where(x => ((x.Firma!.Adres ?? "") + " " + (x.Firma.FaaliyetIli ?? "")).Contains(adres));
            }
            if (filtre.Yukleme.HasValue)
            {
                var tarih = filtre.Yukleme.Value.Date;
                query = query.Where(x => x.OlusturmaTarihi >= tarih);
                if (tarih < DateTime.MaxValue.Date)
                {
                    var ertesiGun = tarih.AddDays(1);
                    query = query.Where(x => x.OlusturmaTarihi < ertesiGun);
                }
            }
            if (filtre.Baslangic.HasValue)
            {
                var tarih = filtre.Baslangic.Value.Date;
                query = query.Where(x => (x.YetkiBelgesiBaslangicTarihi ?? x.OlusturmaTarihi) >= tarih);
            }
            if (filtre.Bitis.HasValue && filtre.Bitis.Value.Date < DateTime.MaxValue.Date)
            {
                var ertesiGun = filtre.Bitis.Value.Date.AddDays(1);
                query = query.Where(x => x.YetkiBelgesiBitisTarihi < ertesiGun);
            }

            var gruplar = await query.GroupBy(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
                    ? (x.YetkiBelgesiBitisTarihi >= bugun ? "bekleyen" : "suresi-dolan")
                    : x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi ? "onaylanan" : "reddedilen")
                .Select(g => new { Durum = g.Key, Sayi = g.Count() })
                .ToDictionaryAsync(x => x.Durum, x => x.Sayi, cancellationToken);
            var durum = filtre.Durum switch
            {
                "onayli" or "onaylanan" => "onaylanan",
                "reddedilen" => "reddedilen",
                "suresi-dolan" => "suresi-dolan",
                _ => "bekleyen"
            };
            var sayfalama = new YetkiBelgesiOnaySayfalamaDto
            {
                Filtre = filtre, Durum = durum,
                SayfaBoyutu = Math.Clamp(filtre.SayfaBoyutu <= 0 ? 25 : filtre.SayfaBoyutu, 1, 100),
                Toplam = gruplar.GetValueOrDefault(durum),
                Bekleyen = gruplar.GetValueOrDefault("bekleyen"),
                Onaylanan = gruplar.GetValueOrDefault("onaylanan"),
                Reddedilen = gruplar.GetValueOrDefault("reddedilen"),
                SuresiDolan = gruplar.GetValueOrDefault("suresi-dolan")
            };
            sayfalama.Sayfa = Math.Clamp(filtre.Sayfa, 1, sayfalama.ToplamSayfa);
            query = durum switch
            {
                "onaylanan" => query.Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi),
                "reddedilen" => query.Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Reddedildi),
                "suresi-dolan" => query.Where(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor && x.YetkiBelgesiBitisTarihi < bugun),
                _ => query.Where(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor && x.YetkiBelgesiBitisTarihi >= bugun)
            };
            var sirali = durum == "bekleyen" ? query.OrderByDescending(x => x.OlusturmaTarihi)
                : durum == "suresi-dolan" ? query.OrderByDescending(x => x.YetkiBelgesiBitisTarihi)
                : query.OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi);
            var kayitlar = await sirali.ThenByDescending(x => x.Id)
                .Skip((sayfalama.Sayfa - 1) * sayfalama.SayfaBoyutu).Take(sayfalama.SayfaBoyutu)
                .Include(x => x.Firma).ThenInclude(x => x!.Sirket).ToListAsync(cancellationToken);
            return (kayitlar, sayfalama);
        }

        public async Task<AdminYetkiBelgesiOnayGecmisiListeDto> GecmisAsync(AdminYetkiBelgesiOnayGecmisiFiltreDto? dto, int? sirketId)
        {
            var query = YetkiBelgesiTemelQuery(sirketId)
                .Where(x => x.Durum != YetkiBelgesiDurumDegerleri.OnaydaBekliyor);

            if (dto?.BaslangicTarihi.HasValue == true)
            {
                var baslangic = dto.BaslangicTarihi.Value.Date;
                query = query.Where(x => x.OnayTarihi.HasValue && x.OnayTarihi.Value >= baslangic);
            }

            if (dto?.BitisTarihi.HasValue == true)
            {
                var bitis = dto.BitisTarihi.Value.Date.AddDays(1);
                query = query.Where(x => x.OnayTarihi.HasValue && x.OnayTarihi.Value < bitis);
            }

            if (dto?.Durum.HasValue == true && (dto.Durum.Value == 1 || dto.Durum.Value == 2))
                query = query.Where(x => x.Durum == dto.Durum.Value);

            if (!string.IsNullOrWhiteSpace(dto?.Q))
            {
                var q = dto.Q.Trim();
                query = query.Where(x =>
                    (x.Firma != null && x.Firma.FirmaAdi != null && x.Firma.FirmaAdi.Contains(q)) ||
                    (x.Firma != null && x.Firma.Sirket != null && x.Firma.Sirket.SirketAdi != null && x.Firma.Sirket.SirketAdi.Contains(q)) ||
                    (x.OnaylayanKullanici != null && x.OnaylayanKullanici.Contains(q)));
            }

            var islemler = await query
                .OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi)
                .ToListAsync();

            return new AdminYetkiBelgesiOnayGecmisiListeDto
            {
                Islemler = islemler.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList()
            };
        }

        public async Task<(byte[] Bytes, string ContentType, string DosyaAdi)?> RaporAsync(
            YetkiBelgesiRaporFiltre filtre, int? sirketId, bool excelMi)
        {
            if (filtre.Tip is not ("bekleyen" or "onayli" or "reddedilen"))
                throw new ArgumentException("Geçerli bir yetki belgesi rapor türü seçin.");

            var bas = filtre.BaslangicTarihi?.Date ?? DateTime.Today.AddDays(-30);
            var bit = filtre.BitisTarihi?.Date ?? DateTime.Today;
            if (bas > bit) (bas, bit) = (bit, bas);
            if (bit == DateTime.MaxValue.Date)
                throw new ArgumentException("Bitiş tarihi geçerli aralığın dışında.");
            var bitSonrasi = bit.AddDays(1);
            var query = YetkiBelgesiTemelQuery(sirketId).AsNoTracking();
            if (filtre.Tip == "bekleyen")
            {
                var bugun = DateTime.Today;
                query = query.Where(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
                    && x.YetkiBelgesiBitisTarihi >= bugun
                    && x.OlusturmaTarihi >= bas && x.OlusturmaTarihi < bitSonrasi);
            }
            else
            {
                var durum = filtre.Tip == "onayli" ? YetkiBelgesiDurumDegerleri.Onaylandi : YetkiBelgesiDurumDegerleri.Reddedildi;
                query = query.Where(x => x.Durum == durum && x.OnayTarihi >= bas && x.OnayTarihi < bitSonrasi);
            }
            var belgeler = await query.OrderByDescending(x => x.OnayTarihi ?? x.OlusturmaTarihi)
                .ThenByDescending(x => x.Id).Take(5001).ToListAsync();
            if (belgeler.Count > 5000)
                throw new ArgumentException("Rapor 5000 kaydı aşıyor. Tarih aralığını daraltın.");
            if (belgeler.Count == 0) return null;
            var baslik = filtre.Tip switch
            {
                "onayli" => "Onaylanan Yetki Belgeleri",
                "reddedilen" => "Reddedilen Yetki Belgeleri",
                _ => "Onay Bekleyen Yetki Belgeleri"
            };
            return (
                excelMi ? YetkiBelgesiRaporExcelService.Olustur(belgeler, baslik) : YetkiBelgesiRaporPdfService.Olustur(belgeler, baslik),
                excelMi ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/pdf",
                $"yetki-belgesi-raporu-{DateTime.Now:yyyyMMdd-HHmm}.{(excelMi ? "xlsx" : "pdf")}");
        }

        private IQueryable<Ys_YetkiBelgesi> YetkiBelgesiTemelQuery(int? sirketId)
        {
            return _context.Ys_YetkiBelgeleri
                .AsNoTracking()
                .Include(x => x.Firma).ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId));
        }
    }
}
