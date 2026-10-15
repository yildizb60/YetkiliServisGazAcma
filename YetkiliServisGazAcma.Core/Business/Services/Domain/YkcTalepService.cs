using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YkcTalepService
    {
        private readonly AppDbContext _context;
        private readonly IYkcSorguKaydiService? _sorguKayitlari;
        private readonly YkcPlanlamaOptions _planlama;
        private readonly YkcPlanlamaOkumaService _planlamaOkuma;

        public YkcTalepService(AppDbContext context, IYkcSorguKaydiService? sorguKayitlari = null,
            IOptions<YkcPlanlamaOptions>? planlama = null, YkcPlanlamaOkumaService? planlamaOkuma = null)
        {
            _context = context;
            _sorguKayitlari = sorguKayitlari;
            _planlama = planlama?.Value ?? new YkcPlanlamaOptions();
            _planlamaOkuma = planlamaOkuma ?? new YkcPlanlamaOkumaService(context, planlama);
        }

        public async Task<YkcIslemSonuc> OlusturAsync(YkcTalepKaydetDto dto, AppKullanici kullanici)
            => await _context.Database.CreateExecutionStrategy().ExecuteAsync(() => OlusturTekilAsync(dto, kullanici));

        private async Task<YkcIslemSonuc> OlusturTekilAsync(YkcTalepKaydetDto dto, AppKullanici kullanici)
        {
            if (_sorguKayitlari is null || !await _sorguKayitlari.UygulaAsync(kullanici.Id, dto))
                return YkcIslemSonuc.HataliSonuc("Tesisatı yeniden sorgulayıp değiştirilecek cihazı seçin. Sorgu kaydının süresi dolmuş olabilir.");
            if (!dto.SirketId.HasValue || !dto.FirmaId.HasValue)
                return YkcIslemSonuc.HataliSonuc("Talep oluşturmak için aktif şirket ve sertifikalı firma kaydı gerekir.");
            var kontrol = TalepDogrula(dto);
            if (!kontrol.Basarili)
                return kontrol;

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            // Serialize retries for this source reference; the unique index also guards other writers.
            var mevcut = await _context.Ykc_Talepler
                .FromSqlInterpolated($"SELECT * FROM dbo.Ykc_Talepler WITH (UPDLOCK, HOLDLOCK) WHERE SorguReferansi = {dto.SorguReferansi}")
                .FirstOrDefaultAsync();
            if (mevcut != null)
            {
                if (mevcut.FirmaId != dto.FirmaId || mevcut.SirketId != dto.SirketId || mevcut.SilindiMi)
                    return YkcIslemSonuc.HataliSonuc("Bu sorgu kaydı daha önce kullanılmış. Tesisatı yeniden sorgulayın.");
                return YkcIslemSonuc.BasariliSonuc("Bu sorguya ait talep daha önce oluşturuldu.", mevcut.Id);
            }

            var firma = kullanici.FirmaId.HasValue
                ? await _context.Ys_Firmalar
                    .Include(x => x.Sirket)
                    .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId.Value && !x.SilindiMi)
                : null;

            var simdi = DateTime.Now;
            var talep = new Ykc_Talep
            {
                FirmaId = kullanici.FirmaId ?? dto.FirmaId,
                SirketId = kullanici.SirketId ?? firma?.SirketId ?? dto.SirketId,
                FirmaKodu = dto.FirmaKodu,
                KaynakTipi = string.IsNullOrWhiteSpace(dto.KaynakTipi) ? "Manuel" : dto.KaynakTipi.Trim(),
                SorguReferansi = dto.SorguReferansi,
                TesisatNo = dto.TesisatNo?.Trim(),
                SozlesmeNo = dto.SozlesmeNo?.Trim(),
                AboneNo = dto.AboneNo?.Trim(),
                ProjeNo = dto.ProjeNo?.Trim(),
                SayacNo = dto.SayacNo?.Trim(),
                MusteriAdi = dto.MusteriAdi?.Trim(),
                MusteriTelefon = dto.MusteriTelefon?.Trim(),
                Il = dto.Il?.Trim(),
                Ilce = dto.Ilce?.Trim(),
                Bolge = YkcBolgeAtamaKurali.BolgeBelirle(dto.Bolge, dto.Il),
                Adres = dto.Adres?.Trim(),
                EskiCihazTipiKodu = dto.EskiCihazTipiKodu?.Trim(),
                EskiCihazTipi = dto.EskiCihazTipi?.Trim(),
                EskiMarkaKodu = dto.EskiMarkaKodu?.Trim(),
                EskiMarka = dto.EskiMarka?.Trim(),
                EskiBacaTipiKodu = dto.EskiBacaTipiKodu?.Trim(),
                EskiBacaTipi = dto.EskiBacaTipi?.Trim(),
                EskiKapasite = dto.EskiKapasite?.Trim(),
                YeniCihazTipiKodu = dto.YeniCihazTipiKodu?.Trim(),
                YeniCihazTipi = dto.YeniCihazTipi?.Trim(),
                YeniMarkaKodu = dto.YeniMarkaKodu?.Trim(),
                YeniMarka = dto.YeniMarka?.Trim(),
                YeniBacaTipiKodu = dto.YeniBacaTipiKodu?.Trim(),
                YeniBacaTipi = dto.YeniBacaTipi?.Trim(),
                YeniKapasite = dto.YeniKapasite?.Trim(),
                YeniModel = dto.YeniModel?.Trim(),
                YeniSeriNo = dto.YeniSeriNo?.Trim(),
                IkinciElCihazMi = dto.IkinciElCihazMi,
                Fr265BelgeVersiyonNo = 1,
                Durum = YkcDurumDegerleri.TalepAlindi,
                TalepTarihi = simdi,
                HedefUygulama = YkcHedefUygulamaDegerleri.YonetimPaneli,
                OlusturmaTarihi = simdi,
                OlusturanKullanici = kullanici.UserName
            };

            _context.Ykc_Talepler.Add(talep);

            VarsayilanKontrollerEkle(talep, kullanici);
            ImzaSureciHazirla(talep, kullanici, firma?.YetkiliKisi);

            _context.Ykc_IslemGecmisi.Add(new Ykc_IslemGecmisi
            {
                Talep = talep,
                IslemTipi = "TalepOlusturuldu",
                YeniDurum = talep.Durum,
                Aciklama = "Cihaz değişim talebi oluşturuldu.",
                KullaniciId = kullanici.Id,
                KullaniciAdi = kullanici.UserName,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName
            });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return YkcIslemSonuc.BasariliSonuc("Cihaz değişim talebi oluşturuldu.", talep.Id);
        }

        public async Task<YkcIslemSonuc> AtamaYapAsync(
            YkcAtamaKaydetDto dto,
            AppKullanici kullanici,
            bool genelYetkili,
            int? dogrulanmisSirketId = null)
            => await _context.Database.CreateExecutionStrategy().ExecuteAsync(() => AtamaKaydetAsync(dto, kullanici, genelYetkili, dogrulanmisSirketId));

        private async Task<YkcIslemSonuc> AtamaKaydetAsync(YkcAtamaKaydetDto dto, AppKullanici kullanici, bool genelYetkili, int? dogrulanmisSirketId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var talep = await YkcTalepKapsami.Uygula(_context.Ykc_Talepler
                .FromSqlInterpolated($"SELECT * FROM dbo.Ykc_Talepler WITH (UPDLOCK, HOLDLOCK) WHERE Id = {dto.TalepId}")
                .Include(x => x.Kontroller).Where(x => !x.SilindiMi), kullanici, genelYetkili, dogrulanmisSirketId)
                .FirstOrDefaultAsync(x => x.Id == dto.TalepId);

            if (talep == null)
                return YkcIslemSonuc.HataliSonuc("Cihaz değişim talebi bulunamadı.");

            if (DurumTerminalMi(talep.Durum))
                return YkcIslemSonuc.HataliSonuc("Tamamlanan, reddedilen veya iptal edilen talep için atama yapılamaz.");

            var imzaSureciBasladi = await _context.Ykc_ImzaSurecleri.AnyAsync(x =>
                x.TalepId == talep.Id &&
                !x.SilindiMi &&
                (!string.IsNullOrWhiteSpace(x.ProviderDocumentId) ||
                 x.Durum == YkcImzaDurumDegerleri.ImzayaGonderildi ||
                 x.Durum == YkcImzaDurumDegerleri.ImzaBekliyor ||
                 x.Durum == YkcImzaDurumDegerleri.KismiImzali ||
                 x.Durum == YkcImzaDurumDegerleri.Tamamlandi));

            if (imzaSureciBasladi)
                return YkcIslemSonuc.HataliSonuc("Formun imza süreci başladıktan sonra randevu ve yönlendirme değiştirilemez.");

            if (talep.Durum == YkcDurumDegerleri.TalepAlindi)
                return YkcIslemSonuc.HataliSonuc("Randevu planlamak için talebi önce incelemeye alın.");

            if (!AtamaYapilabilirMi(talep.Durum))
                return YkcIslemSonuc.HataliSonuc("Talebin mevcut aşamasında randevu ve atama yapılamaz.");

            var bolge = YkcBolgeAtamaKurali.BolgeBelirle(talep.Bolge, talep.Il);
            if (string.IsNullOrWhiteSpace(bolge))
                return YkcIslemSonuc.HataliSonuc("Tesisatın planlama bölgesi belirlenemedi. Tesisat bilgilerini kontrol edin.");

            var yonlendirmeTipi = YonlendirmeTipiBelirle(dto);
            if (string.IsNullOrWhiteSpace(yonlendirmeTipi))
                return YkcIslemSonuc.HataliSonuc("Bölge için 187 Acil veya Mühendis yönlendirmesi seçin.");

            if (!dto.RandevuTarihi.HasValue)
                return YkcIslemSonuc.HataliSonuc("Randevu tarihi zorunludur.");

            if (string.IsNullOrWhiteSpace(dto.RandevuSaati))
                return YkcIslemSonuc.HataliSonuc("Randevu saati zorunludur.");

            if (RandevuZamaniGecmisteMi(dto.RandevuTarihi, dto.RandevuSaati))
                return YkcIslemSonuc.HataliSonuc("Geçmiş tarih veya saate randevu verilemez.");

            var eskiDurum = talep.Durum;
            var hedef = HedefUygulamaBelirle(yonlendirmeTipi);
            var ekipAdi = hedef == YkcHedefUygulamaDegerleri.Crm187 ? "187 Acil" : "Mühendis";

            if (!string.IsNullOrWhiteSpace(dto.EkipId))
            {
                var ekip = (await _planlamaOkuma.EkiplerAsync(talep.Id, kullanici, genelYetkili, dogrulanmisSirketId))
                    .SingleOrDefault(x => x.Id == dto.EkipId);
                if (ekip == null)
                    return YkcIslemSonuc.HataliSonuc("Seçilen ekip bu şirket, il ve tesisat bölgesine atanamaz.");
                if (YonlendirmeTipiBelirle(new YkcAtamaKaydetDto { AtananKullaniciTipi = ekip.YonlendirmeTipi }) != yonlendirmeTipi)
                    return YkcIslemSonuc.HataliSonuc("Ekip ile yönlendirme türü eşleşmiyor.");
                dto.AtananKullaniciId = ekip.KullaniciId;
                ekipAdi = ekip.Ad;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(dto.AtananKullaniciId))
                    return YkcIslemSonuc.HataliSonuc("Personel yalnızca bölgeye tanımlı ekip listesinden atanabilir.");
                ekipAdi = hedef == YkcHedefUygulamaDegerleri.Crm187 ? "187 Acil" : "Mühendis";
            }
            if (!string.IsNullOrWhiteSpace(dto.AtananKullaniciId)
                && !await _context.Users.AnyAsync(x => x.Id == dto.AtananKullaniciId && x.AktifMi
                    && x.FirmaId == null && (x.SirketId == talep.SirketId || _context.Dag_PersonelYetkiler.Any(p =>
                        !p.SilindiMi && p.KullaniciId == x.Id && p.SirketId == talep.SirketId))))
                return YkcIslemSonuc.HataliSonuc("Personelin bu şirkette aktif görevi bulunmuyor.");

            if (!TimeSpan.TryParseExact(dto.RandevuSaati, @"hh\:mm", CultureInfo.InvariantCulture, out var saat))
                return YkcIslemSonuc.HataliSonuc("Randevu saati SS:dd biçiminde olmalıdır.");
            if (!YkcRandevuKurali.MesaiSaatindeMi(saat))
                return YkcIslemSonuc.HataliSonuc("Randevu saati 08.00-18.00 çalışma saatleri içinde olmalıdır.");
            if (!YkcRandevuKurali.GecerliSaatDilimi(saat, _planlama.RandevuDilimDakika))
                return YkcIslemSonuc.HataliSonuc($"Randevu saati {_planlama.RandevuDilimDakika} dakikalık dilimlerden biri olmalıdır.");
            var randevu = dto.RandevuTarihi.Value.Date.Add(saat);
            var gunBas = randevu.Date.AddDays(-1);
            var gunSon = randevu.Date.AddDays(2);
            var digerleri = await _context.Ykc_Talepler.AsNoTracking()
                .Where(x => !x.SilindiMi && x.Id != talep.Id
                    && x.Durum != YkcDurumDegerleri.Iptal && x.Durum != YkcDurumDegerleri.Reddedildi
                    && x.RandevuTarihi >= gunBas && x.RandevuTarihi < gunSon
                    && ((!string.IsNullOrEmpty(dto.AtananKullaniciId) && x.AtananKullaniciId == dto.AtananKullaniciId)
                        || (x.SirketId == talep.SirketId && x.Bolge == bolge && x.AtananEkip == ekipAdi)))
                .Select(x => new { x.RandevuTarihi, x.RandevuSaati }).ToListAsync();
            if (digerleri.Any(x => TimeSpan.TryParse(x.RandevuSaati, out var digerSaat)
                && YkcRandevuKurali.Cakisiyor(randevu, x.RandevuTarihi!.Value.Date.Add(digerSaat), _planlama.AsgariAralikDakika)))
                return YkcIslemSonuc.HataliSonuc("Bu personel/ekip için seçilen saatte başka randevu var. Farklı bir saat seçin.");
            dto.RandevuTarihi = randevu.Date;

            var oncekiKontrolUygun = YkcKontrolAkisKurali.AktifKontroller(talep.Kontroller)
                .Any(x => x.Sonuc == YkcFr265KontrolSonucDegerleri.Uygun);
            if (YkcKontrolAkisKurali.KontrolAlaniDolduMu(talep.Kontroller) || oncekiKontrolUygun)
            {
                var baslangic = YkcKontrolAkisKurali.DonemBaslangici(talep.Kontroller.Where(x => !x.SilindiMi).Select(x => x.KontrolNo)) + 5;
                VarsayilanKontrollerEkle(talep, kullanici, baslangic);
                _context.Ykc_IslemGecmisi.Add(new Ykc_IslemGecmisi
                {
                    TalepId = talep.Id, IslemTipi = "KontrolDonemiAcildi",
                    Aciklama = $"{YkcKontrolAkisKurali.DonemNo(baslangic)}. kontrol dönemi açıldı. Önceki kontrol kayıtları korundu.",
                    KullaniciId = kullanici.Id, KullaniciAdi = kullanici.UserName,
                    OlusturanKullanici = kullanici.UserName
                });
            }

            talep.AtananKullaniciId = dto.AtananKullaniciId;
            talep.AtananKullaniciTipi = yonlendirmeTipi;
            talep.AtananEkip = ekipAdi;
            talep.Bolge = bolge;
            talep.HedefUygulama = hedef;
            talep.RandevuTarihi = dto.RandevuTarihi;
            talep.RandevuSaati = dto.RandevuSaati?.Trim();
            talep.CallCenterTetiklenecekMi = dto.CallCenterTetiklenecekMi || hedef == YkcHedefUygulamaDegerleri.Crm187;
            talep.Durum = YkcDurumDegerleri.Atandi;
            talep.Fr265BelgeVersiyonNo = Math.Max(1, talep.Fr265BelgeVersiyonNo) + 1;
            talep.Fr265BelgeHash = null;
            talep.Fr265BelgeOlusturmaTarihi = null;
            talep.GuncellemeTarihi = DateTime.Now;
            talep.GuncelleyenKullanici = kullanici.UserName;

            _context.Ykc_Atamalar.Add(new Ykc_Atama
            {
                TalepId = talep.Id,
                AtananKullaniciId = dto.AtananKullaniciId,
                AtananKullaniciTipi = yonlendirmeTipi,
                AtananEkip = ekipAdi,
                Bolge = bolge,
                HedefUygulama = hedef,
                RandevuTarihi = dto.RandevuTarihi,
                RandevuSaati = dto.RandevuSaati?.Trim(),
                Aciklama = dto.Aciklama?.Trim(),
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName
            });

            _context.Ykc_IslemGecmisi.Add(new Ykc_IslemGecmisi
            {
                TalepId = talep.Id,
                IslemTipi = "AtamaYapildi",
                EskiDurum = eskiDurum,
                YeniDurum = talep.Durum,
                Aciklama = dto.Aciklama,
                KullaniciId = kullanici.Id,
                KullaniciAdi = kullanici.UserName,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return YkcIslemSonuc.BasariliSonuc("Cihaz değişim talebi için randevu ve atama kaydedildi.", talep.Id);
        }

        public async Task<YkcIslemSonuc> DurumGuncelleAsync(
            YkcDurumGuncelleDto dto,
            AppKullanici kullanici,
            bool genelYetkili,
            int? dogrulanmisSirketId = null)
            => await _context.Database.CreateExecutionStrategy().ExecuteAsync(() => DurumuKaydetAsync(dto, kullanici, genelYetkili, dogrulanmisSirketId));

        private async Task<YkcIslemSonuc> DurumuKaydetAsync(YkcDurumGuncelleDto dto, AppKullanici kullanici, bool genelYetkili, int? dogrulanmisSirketId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var talep = await YkcTalepKapsami.Uygula(_context.Ykc_Talepler
                .FromSqlInterpolated($"SELECT * FROM dbo.Ykc_Talepler WITH (UPDLOCK, HOLDLOCK) WHERE Id = {dto.TalepId}")
                .Where(x => !x.SilindiMi), kullanici, genelYetkili, dogrulanmisSirketId)
                .FirstOrDefaultAsync(x => x.Id == dto.TalepId);

            if (talep == null)
                return YkcIslemSonuc.HataliSonuc("Cihaz değişim talebi bulunamadı.");

            if (dto.Durum == YkcDurumDegerleri.Reddedildi && string.IsNullOrWhiteSpace(dto.Aciklama))
                return YkcIslemSonuc.HataliSonuc("Red işlemi için açıklama zorunludur.");

            if (dto.Durum == YkcDurumDegerleri.Iptal && string.IsNullOrWhiteSpace(dto.Aciklama))
                return YkcIslemSonuc.HataliSonuc("İptal işlemi için açıklama zorunludur.");

            var eskiDurum = talep.Durum;
            if (eskiDurum == dto.Durum)
                return YkcIslemSonuc.BasariliSonuc("Talep zaten seçilen durumda.", talep.Id);

            if (dto.Durum == YkcDurumDegerleri.Atandi)
                return YkcIslemSonuc.HataliSonuc("Randevu yalnızca tarih, saat ve yönlendirme bilgileriyle planlanabilir.");

            if (dto.Durum == YkcDurumDegerleri.SahaIsleminde && !YkcTalepIslemKurali.RandevuZamaniGeldiMi(talep.RandevuTarihi, talep.RandevuSaati))
                return YkcIslemSonuc.HataliSonuc("Randevu zamanı gelmeden saha kontrolü başlatılamaz.");

            var imzaliNihaiBelgeVar = await ImzaliNihaiBelgeVarMiAsync(talep.Id);

            if (dto.Durum == YkcDurumDegerleri.Tamamlandi && !imzaliNihaiBelgeVar)
                return YkcIslemSonuc.HataliSonuc("İşlemi tamamlamak için imza/arşiv sisteminden dönmüş imzalı nihai belge gerekir. Dijital imza bağlantısı kurulmadan bu talep canlı olarak tamamlanamaz.");

            if (dto.Durum == YkcDurumDegerleri.Tamamlandi && !YkcTalepIslemKurali.RandevuZamaniGeldiMi(talep.RandevuTarihi, talep.RandevuSaati))
                return YkcIslemSonuc.HataliSonuc("Randevu saati gelmeden talep tamamlanamaz.");

            if (!DurumGecisiGecerliMi(eskiDurum, dto.Durum, imzaliNihaiBelgeVar))
                return YkcIslemSonuc.HataliSonuc("Bu işlem için önceki adımlar tamamlanmalıdır.");

            talep.Durum = dto.Durum;
            talep.RedAciklama = dto.Durum == YkcDurumDegerleri.Reddedildi ? dto.Aciklama?.Trim() : talep.RedAciklama;
            if (dto.Durum == YkcDurumDegerleri.Iptal)
            {
                talep.IptalTarihi = DateTime.Now;
                talep.IptalEdenKullaniciId = kullanici.Id;
                talep.IptalAciklama = dto.Aciklama?.Trim();
            }
            talep.GuncellemeTarihi = DateTime.Now;
            talep.GuncelleyenKullanici = kullanici.UserName;

            _context.Ykc_IslemGecmisi.Add(new Ykc_IslemGecmisi
            {
                TalepId = talep.Id,
                IslemTipi = "DurumGuncellendi",
                EskiDurum = eskiDurum,
                YeniDurum = talep.Durum,
                Aciklama = dto.Aciklama?.Trim(),
                KullaniciId = kullanici.Id,
                KullaniciAdi = kullanici.UserName,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return YkcIslemSonuc.BasariliSonuc("Cihaz değişim talebi durumu güncellendi.", talep.Id);
        }

        public async Task<YkcIslemSonuc> KontrolleriKaydetAsync(
            YkcKontrolKaydetDto dto,
            AppKullanici kullanici,
            bool genelYetkili,
            int? dogrulanmisSirketId = null)
            => await _context.Database.CreateExecutionStrategy().ExecuteAsync(() => KontrolSonucuKaydetAsync(dto, kullanici, genelYetkili, dogrulanmisSirketId));

        private async Task<YkcIslemSonuc> KontrolSonucuKaydetAsync(YkcKontrolKaydetDto dto, AppKullanici kullanici, bool genelYetkili, int? dogrulanmisSirketId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var talep = await YkcTalepKapsami.Uygula(_context.Ykc_Talepler
                .FromSqlInterpolated($"SELECT * FROM dbo.Ykc_Talepler WITH (UPDLOCK, HOLDLOCK) WHERE Id = {dto.TalepId}")
                .AsSplitQuery().Include(x => x.Kontroller).Include(x => x.ImzaSurecleri)
                .Where(x => !x.SilindiMi), kullanici, genelYetkili, dogrulanmisSirketId)
                .FirstOrDefaultAsync(x => x.Id == dto.TalepId);

            if (talep == null)
                return YkcIslemSonuc.HataliSonuc("Cihaz değişim talebi bulunamadı.");

            if (DurumTerminalMi(talep.Durum))
                return YkcIslemSonuc.HataliSonuc("Kapanmış talep için kontrol güncellenemez.");

            if (talep.Durum != YkcDurumDegerleri.SahaIsleminde)
                return YkcIslemSonuc.HataliSonuc("Kontrol sonucu yalnızca randevu gerçekleşip kontrol aşamasına geçildikten sonra girilebilir.");

            if (!YkcTalepIslemKurali.RandevuZamaniGeldiMi(talep.RandevuTarihi, talep.RandevuSaati))
                return YkcIslemSonuc.HataliSonuc("Randevu zamanı gelmeden kontrol sonucu kaydedilemez.");
            var aktifAtama = await _context.Ykc_Atamalar.Where(x => x.TalepId == talep.Id && !x.SilindiMi)
                .OrderByDescending(x => x.Id).FirstOrDefaultAsync();
            if (aktifAtama == null || aktifAtama.RandevuTarihi?.Date != talep.RandevuTarihi?.Date
                || aktifAtama.RandevuSaati != talep.RandevuSaati)
                return YkcIslemSonuc.HataliSonuc("Kontrol için geçerli randevu kaydı bulunamadı. Randevuyu yeniden planlayın.");

            var imzaSureci = talep.ImzaSurecleri
                .Where(x => !x.SilindiMi)
                .OrderByDescending(x => x.BelgeVersiyonu)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();

            if (imzaSureci != null
                && (!string.IsNullOrWhiteSpace(imzaSureci.ProviderDocumentId)
                    || imzaSureci.Durum is YkcImzaDurumDegerleri.ImzayaGonderildi
                        or YkcImzaDurumDegerleri.ImzaBekliyor
                        or YkcImzaDurumDegerleri.KismiImzali
                        or YkcImzaDurumDegerleri.Tamamlandi))
            {
                return YkcIslemSonuc.HataliSonuc("İmzaya gönderilen form üzerindeki kontroller değiştirilemez.");
            }

            var gecerliSonuclar = new[]
            {
                YkcFr265KontrolSonucDegerleri.Uygun,
                YkcFr265KontrolSonucDegerleri.UygunDegil
            };

            var degisiklikVar = false;
            var kontrolSatirlari = dto.Kontroller
                .ToList();

            if (!kontrolSatirlari.Any())
                return YkcIslemSonuc.HataliSonuc("Kontrol sonucu zorunludur.");

            var aktifKontroller = YkcKontrolAkisKurali.AktifKontroller(talep.Kontroller);
            var donemBaslangici = YkcKontrolAkisKurali.DonemBaslangici(aktifKontroller.Select(x => x.KontrolNo));
            var mevcutSonKontrol = aktifKontroller
                .Where(x => (x.Sonuc == YkcFr265KontrolSonucDegerleri.Uygun
                        || x.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil))
                .OrderBy(x => x.KontrolNo)
                .LastOrDefault();

            if (mevcutSonKontrol?.Sonuc == YkcFr265KontrolSonucDegerleri.Uygun)
                return YkcIslemSonuc.HataliSonuc("Son kontrol uygun kaydedildiği için yeni kontrol sonucu girilemez.");

            var beklenenKontrolNo = (mevcutSonKontrol?.KontrolNo ?? (donemBaslangici - 1)) + 1;
            if (beklenenKontrolNo >= donemBaslangici + 5)
                return YkcIslemSonuc.HataliSonuc("Yeni kontrol dönemi için önce yeniden randevu atayın.");

            if (kontrolSatirlari.Count != 1 || kontrolSatirlari[0].KontrolNo != beklenenKontrolNo)
                return YkcIslemSonuc.HataliSonuc($"{YkcKontrolAkisKurali.DonemNo(beklenenKontrolNo)}. dönemin {YkcKontrolAkisKurali.FormKontrolNo(beklenenKontrolNo)}. kontrol sonucu bekleniyor.");

            foreach (var satir in kontrolSatirlari)
            {
                var sonuc = satir.Sonuc?.Trim();

                if (string.IsNullOrWhiteSpace(sonuc) || !gecerliSonuclar.Contains(sonuc))
                    return YkcIslemSonuc.HataliSonuc("Kontrol sonucu geçersiz.");

                if (sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil
                    && string.IsNullOrWhiteSpace(satir.Aciklama))
                {
                    return YkcIslemSonuc.HataliSonuc("Uygun değil sonucu için açıklama zorunludur.");
                }

                var kontrol = talep.Kontroller.FirstOrDefault(x => !x.SilindiMi && x.KontrolNo == satir.KontrolNo);
                if (kontrol == null)
                {
                    kontrol = new Ykc_Fr265Kontrol
                    {
                        TalepId = talep.Id,
                        KontrolNo = satir.KontrolNo,
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici.UserName
                    };
                    _context.Ykc_Fr265Kontroller.Add(kontrol);
                    talep.Kontroller.Add(kontrol);
                    degisiklikVar = true;
                }

                var aciklama = satir.Aciklama?.Trim();
                if (!string.Equals(kontrol.Sonuc, sonuc, StringComparison.Ordinal)
                    || !string.Equals(kontrol.Aciklama?.Trim(), aciklama, StringComparison.Ordinal))
                {
                    degisiklikVar = true;
                }

                kontrol.Sonuc = sonuc;
                kontrol.AtamaId = aktifAtama.Id;
                kontrol.Aciklama = aciklama;
                kontrol.KontrolEdenKullaniciId = kullanici.Id;
                kontrol.KontrolTarihi = DateTime.Now;
                kontrol.GuncellemeTarihi = DateTime.Now;
                kontrol.GuncelleyenKullanici = kullanici.UserName;
            }

            if (!degisiklikVar)
                return YkcIslemSonuc.BasariliSonuc("Kontrol kayıtlarında değişiklik bulunmadı.", talep.Id);

            talep.Fr265BelgeVersiyonNo = Math.Max(talep.Fr265BelgeVersiyonNo, 1) + 1;
            talep.Fr265BelgeOlusturmaTarihi = null;
            talep.Fr265BelgeHash = null;
            talep.GuncellemeTarihi = DateTime.Now;
            talep.GuncelleyenKullanici = kullanici.UserName;

            var yeniRandevuGerekli = YkcKontrolAkisKurali.YeniRandevuGerekli(kontrolSatirlari[0].Sonuc);
            var kontrolAlaniDoldu = yeniRandevuGerekli && YkcKontrolAkisKurali.FormKontrolNo(kontrolSatirlari[0].KontrolNo) == 5;
            if (yeniRandevuGerekli)
            {
                // Tamamlanan atama Ykc_Atamalar tablosunda korunur; talebin guncel gorevi yeni planlamaya doner.
                talep.Durum = YkcDurumDegerleri.AtamaBekliyor;
                talep.AtananKullaniciId = null;
                talep.AtananKullaniciTipi = null;
                talep.AtananEkip = null;
                talep.HedefUygulama = null;
                talep.RandevuTarihi = null;
                talep.RandevuSaati = null;
                talep.CallCenterTetiklenecekMi = false;
            }

            if (imzaSureci != null
                && imzaSureci.Durum is YkcImzaDurumDegerleri.Hazir or YkcImzaDurumDegerleri.Hata)
            {
                imzaSureci.BelgeVersiyonu = talep.Fr265BelgeVersiyonNo;
                imzaSureci.Durum = YkcImzaDurumDegerleri.Hazir;
                imzaSureci.ProviderDocumentId = null;
                imzaSureci.BelgeHash = null;
                imzaSureci.BelgeOlusturmaTarihi = null;
                imzaSureci.GonderimTarihi = null;
                imzaSureci.SonKontrolTarihi = null;
                imzaSureci.HataKodu = null;
                imzaSureci.HataMesaji = null;
                imzaSureci.GuncellemeTarihi = DateTime.Now;
                imzaSureci.GuncelleyenKullanici = kullanici.UserName;
            }

            _context.Ykc_IslemGecmisi.Add(new Ykc_IslemGecmisi
            {
                TalepId = talep.Id,
                IslemTipi = "FR265KontrolleriGuncellendi",
                YeniDurum = talep.Durum,
                Aciklama = $"{YkcKontrolAkisKurali.DonemNo(beklenenKontrolNo)}. dönem, {YkcKontrolAkisKurali.FormKontrolNo(beklenenKontrolNo)}. kontrol: " + (kontrolAlaniDoldu
                    ? "Uygun değil. Yeni beşli kontrol dönemi için yeniden randevu bekleniyor."
                    : yeniRandevuGerekli
                        ? "Kontrol uygun değil kaydedildi; yeni kontrol randevusu bekleniyor."
                        : "Form kontrol sonucu güncellendi."),
                KullaniciId = kullanici.Id,
                KullaniciAdi = kullanici.UserName,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return YkcIslemSonuc.BasariliSonuc("Kontrol sonucu kaydedildi.", talep.Id);
        }

        public async Task<YkcIslemSonuc> DosyaEkleAsync(
            YkcDosyaKaydetDto dto,
            AppKullanici kullanici,
            bool genelYetkili,
            int? dogrulanmisSirketId = null,
            CancellationToken cancellationToken = default)
        {
            var talep = await YkcTalepKapsami.Uygula(_context.Ykc_Talepler.Where(x => !x.SilindiMi), kullanici, genelYetkili, dogrulanmisSirketId)
                .FirstOrDefaultAsync(x => x.Id == dto.TalepId, cancellationToken);

            if (talep == null)
                return YkcIslemSonuc.HataliSonuc("Cihaz değişim talebi bulunamadı.");

            if (string.IsNullOrWhiteSpace(dto.DosyaYolu))
                return YkcIslemSonuc.HataliSonuc("Dosya yolu zorunludur.");

            var dosyaTuru = string.IsNullOrWhiteSpace(dto.DosyaTuru)
                ? YkcFormDosyaTuruDegerleri.TeknikEk
                : dto.DosyaTuru.Trim();

            var dosya = new Ykc_FormDosya
            {
                TalepId = talep.Id,
                DosyaTuru = dosyaTuru,
                DosyaAdi = dto.DosyaAdi?.Trim(),
                DosyaYolu = dto.DosyaYolu.Trim(),
                IcerikTipi = dto.IcerikTipi?.Trim(),
                DosyaBoyutu = dto.DosyaBoyutu,
                DepolamaTuru = string.IsNullOrWhiteSpace(dto.DepolamaTuru)
                    ? YkcDepolamaTuruDegerleri.Private
                    : dto.DepolamaTuru.Trim(),
                BelgeHash = dto.BelgeHash?.Trim(),
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName
            };

            _context.Ykc_FormDosyalari.Add(dosya);

            _context.Ykc_IslemGecmisi.Add(new Ykc_IslemGecmisi
            {
                TalepId = talep.Id,
                IslemTipi = "DosyaEklendi",
                Aciklama = dosyaTuru,
                KullaniciId = kullanici.Id,
                KullaniciAdi = kullanici.UserName,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName
            });

            await _context.SaveChangesAsync(cancellationToken);

            return YkcIslemSonuc.BasariliSonuc("Cihaz değişim belge kaydı oluşturuldu.", talep.Id);
        }

        public async Task<bool> IslemGecmisiEkleAsync(
            int talepId,
            AppKullanici kullanici,
            bool genelYetkili,
            string islemTipi,
            string? aciklama)
        {
            var talep = await YkcTalepKapsami.Uygula(_context.Ykc_Talepler.Where(x => !x.SilindiMi), kullanici, genelYetkili)
                .FirstOrDefaultAsync(x => x.Id == talepId);

            if (talep == null)
                return false;

            _context.Ykc_IslemGecmisi.Add(new Ykc_IslemGecmisi
            {
                TalepId = talep.Id,
                IslemTipi = islemTipi,
                YeniDurum = talep.Durum,
                Aciklama = aciklama?.Trim(),
                KullaniciId = kullanici.Id,
                KullaniciAdi = kullanici.UserName,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName
            });

            await _context.SaveChangesAsync();
            return true;
        }

        private void VarsayilanKontrollerEkle(Ykc_Talep talep, AppKullanici kullanici, int baslangic = 1)
        {
            for (var kontrolNo = baslangic; kontrolNo < baslangic + 5; kontrolNo++)
            {
                var kontrol = new Ykc_Fr265Kontrol
                {
                    Talep = talep,
                    KontrolNo = kontrolNo,
                    Sonuc = YkcFr265KontrolSonucDegerleri.Bekliyor,
                    OlusturmaTarihi = DateTime.Now,
                    OlusturanKullanici = kullanici.UserName
                };

                _context.Ykc_Fr265Kontroller.Add(kontrol);
            }
        }

        private void ImzaSureciHazirla(Ykc_Talep talep, AppKullanici kullanici, string? firmaYetkilisi)
        {
            var surec = new Ykc_ImzaSureci
            {
                Talep = talep,
                BelgeVersiyonu = talep.Fr265BelgeVersiyonNo <= 0 ? 1 : talep.Fr265BelgeVersiyonNo,
                Durum = YkcImzaDurumDegerleri.Hazir,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName,
                Imzacilar = new List<Ykc_Imzaci>
                {
                    YeniImzaci("Sertifikalı Firma Yetkilisi", 1, firmaYetkilisi, kullanici.UserName),
                    YeniImzaci("Dağıtım Şirketi Yetkilisi", 2, null, kullanici.UserName),
                    YeniImzaci("Abone / Kullanıcı", 3, talep.MusteriAdi, kullanici.UserName)
                }
            };

            _context.Ykc_ImzaSurecleri.Add(surec);
        }

        private static Ykc_Imzaci YeniImzaci(string rol, int siraNo, string? adSoyad, string? olusturan)
        {
            return new Ykc_Imzaci
            {
                Rol = rol,
                SiraNo = siraNo,
                AdSoyad = adSoyad,
                Durum = YkcImzaciDurumDegerleri.Bekliyor,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = olusturan
            };
        }

        private static bool DurumTerminalMi(int durum)
        {
            return durum == YkcDurumDegerleri.Tamamlandi
                || durum == YkcDurumDegerleri.Reddedildi
                || durum == YkcDurumDegerleri.Iptal;
        }

        private static bool AtamaYapilabilirMi(int durum)
        {
            return durum == YkcDurumDegerleri.AtamaBekliyor
                || durum == YkcDurumDegerleri.Atandi
                || durum == YkcDurumDegerleri.SahaIsleminde;
        }

        private async Task<bool> ImzaliNihaiBelgeVarMiAsync(int talepId)
        {
            return await _context.Ykc_ImzaSurecleri.AnyAsync(x =>
                x.TalepId == talepId &&
                !x.SilindiMi &&
                x.Durum == YkcImzaDurumDegerleri.Tamamlandi &&
                x.ProviderDocumentId != null &&
                x.ProviderDocumentId != "" &&
                x.NihaiDosyaId != null &&
                x.NihaiDosya != null &&
                !x.NihaiDosya.SilindiMi &&
                x.NihaiDosya.DosyaTuru == YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai);
        }

        private static bool RandevuZamaniGecmisteMi(DateTime? randevuTarihi, string? randevuSaati)
        {
            if (!randevuTarihi.HasValue || string.IsNullOrWhiteSpace(randevuSaati))
                return false;

            if (!TimeSpan.TryParse(randevuSaati.Trim(), out var saat))
                return false;

            return randevuTarihi.Value.Date.Add(saat) < DateTime.Now;
        }

        private static bool DurumGecisiGecerliMi(int eskiDurum, int yeniDurum, bool imzaliNihaiBelgeVar)
        {
            if (eskiDurum == yeniDurum)
                return true;

            if (DurumTerminalMi(eskiDurum))
                return false;

            if (yeniDurum == YkcDurumDegerleri.Reddedildi || yeniDurum == YkcDurumDegerleri.Iptal)
                return true;

            return eskiDurum switch
            {
                YkcDurumDegerleri.TalepAlindi => yeniDurum == YkcDurumDegerleri.AtamaBekliyor,
                YkcDurumDegerleri.AtamaBekliyor => yeniDurum == YkcDurumDegerleri.Atandi,
                YkcDurumDegerleri.Atandi => yeniDurum == YkcDurumDegerleri.SahaIsleminde,
                YkcDurumDegerleri.SahaIsleminde => yeniDurum == YkcDurumDegerleri.Tamamlandi,
                _ => false
            };
        }

        private static YkcIslemSonuc TalepDogrula(YkcTalepKaydetDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TesisatNo))
                return YkcIslemSonuc.HataliSonuc("Tesisat no zorunludur.");

            if (string.Equals(dto.KaynakTipi?.Trim(), "ServisHatasi", StringComparison.OrdinalIgnoreCase))
                return YkcIslemSonuc.HataliSonuc("Online tesisat servisi yanıt vermeden cihaz değişim talebi oluşturulamaz. Lütfen servisi yeniden sorgulayın.");

            if (string.IsNullOrWhiteSpace(dto.YeniCihazTipi) && string.IsNullOrWhiteSpace(dto.YeniCihazTipiKodu))
                return YkcIslemSonuc.HataliSonuc("Yeni cihaz tipi zorunludur.");

            if (string.IsNullOrWhiteSpace(dto.YeniMarka) && string.IsNullOrWhiteSpace(dto.YeniMarkaKodu))
                return YkcIslemSonuc.HataliSonuc("Yeni marka zorunludur.");

            if (string.IsNullOrWhiteSpace(dto.YeniBacaTipi) && string.IsNullOrWhiteSpace(dto.YeniBacaTipiKodu))
                return YkcIslemSonuc.HataliSonuc("Yeni baca tipi zorunludur.");

            if (string.IsNullOrWhiteSpace(dto.YeniKapasite))
                return YkcIslemSonuc.HataliSonuc("Yeni kapasite zorunludur.");

            if (!YkcCihazUyumKurali.Kapasite(dto.YeniKapasite, out _))
                return YkcIslemSonuc.HataliSonuc("Kapasite sıfırdan büyük bir sayı olmalıdır.");
            if (new[] { dto.YeniCihazTipi, dto.YeniMarka, dto.YeniBacaTipi }.Any(x => x?.Length > 100))
                return YkcIslemSonuc.HataliSonuc("Cihaz tipi, marka ve baca tipi en fazla 100 karakter olabilir.");
            if (!dto.IkinciElCihazMi.HasValue)
                return YkcIslemSonuc.HataliSonuc("İkinci el cihaz bilgisini seçin.");

            if (PlaceholderDegerVar(
                    dto.TesisatNo,
                    dto.SozlesmeNo,
                    dto.AboneNo,
                    dto.ProjeNo,
                    dto.SayacNo,
                    dto.MusteriAdi,
                    dto.EskiCihazTipi,
                    dto.EskiMarka,
                    dto.EskiBacaTipi,
                    dto.EskiKapasite,
                    dto.YeniCihazTipi,
                    dto.YeniMarka,
                    dto.YeniBacaTipi,
                    dto.YeniKapasite,
                    dto.YeniModel,
                    dto.YeniSeriNo))
            {
                return YkcIslemSonuc.HataliSonuc("Geçici örnek değerlerle cihaz değişim talebi oluşturulamaz. Gerçek tesisat ve cihaz bilgilerini girin.");
            }

            return YkcIslemSonuc.BasariliSonuc("Uygun.");
        }

        private static bool PlaceholderDegerVar(params string?[] degerler)
        {
            return degerler.Any(PlaceholderDegerMi);
        }

        private static bool PlaceholderDegerMi(string? deger)
        {
            return string.Equals(deger?.Trim(), "string", StringComparison.OrdinalIgnoreCase);
        }

        private static string? YonlendirmeTipiBelirle(YkcAtamaKaydetDto dto)
        {
            var tip = TurkceKarakterNormalize(dto.AtananKullaniciTipi);
            var ekip = TurkceKarakterNormalize(dto.AtananEkip);
            var hedef = dto.HedefUygulama?.Trim();

            if (dto.CallCenterTetiklenecekMi
                || string.Equals(hedef, YkcHedefUygulamaDegerleri.Crm187, StringComparison.OrdinalIgnoreCase)
                || tip.Contains("187")
                || tip.Contains("ACIL")
                || ekip.Contains("187")
                || ekip.Contains("ACIL"))
            {
                return "CRM187";
            }

            if (string.Equals(hedef, YkcHedefUygulamaDegerleri.DogalgazMobileApp, StringComparison.OrdinalIgnoreCase)
                || tip.Contains("MUHENDIS")
                || ekip.Contains("MUHENDIS"))
            {
                return "Mühendis";
            }

            return null;
        }

        private static string HedefUygulamaBelirle(string yonlendirmeTipi)
        {
            return TurkceKarakterNormalize(yonlendirmeTipi).Contains("187")
                ? YkcHedefUygulamaDegerleri.Crm187
                : YkcHedefUygulamaDegerleri.DogalgazMobileApp;
        }

        private static string TurkceKarakterNormalize(string? value)
        {
            return (value ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Replace('İ', 'I')
                .Replace('ı', 'I')
                .Replace('Ü', 'U')
                .Replace('Ö', 'O')
                .Replace('Ş', 'S')
                .Replace('Ğ', 'G')
                .Replace('Ç', 'C');
        }
    }

    internal static class YkcBolgeAtamaKurali
    {
        public static string? BolgeBelirle(string? bolge, string? il)
        {
            var deger = string.IsNullOrWhiteSpace(bolge) ? il : bolge;
            if (string.IsNullOrWhiteSpace(deger))
                return null;

            var kultur = CultureInfo.GetCultureInfo("tr-TR");
            return kultur.TextInfo.ToTitleCase(deger.Trim().ToLower(kultur));
        }

    }

}
