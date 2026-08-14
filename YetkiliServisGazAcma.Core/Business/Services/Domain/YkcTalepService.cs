using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YkcTalepService
    {
        private readonly AppDbContext _context;

        public YkcTalepService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<YkcTalepListeSonuc> ListeAsync(
            YkcTalepListeFiltre filtre,
            AppKullanici kullanici,
            bool genelYetkili)
        {
            var query = TalepQuery();
            query = FiltreleriUygula(query, filtre, kullanici, genelYetkili);

            var toplam = await query.CountAsync();
            var sayfa = Math.Max(filtre.Sayfa, 1);
            var sayfaBoyutu = Math.Clamp(filtre.SayfaBoyutu <= 0 ? 50 : filtre.SayfaBoyutu, 1, 250);

            var talepler = await query
                .OrderByDescending(x => x.TalepTarihi)
                .ThenByDescending(x => x.Id)
                .Skip((sayfa - 1) * sayfaBoyutu)
                .Take(sayfaBoyutu)
                .ToListAsync();

            return new YkcTalepListeSonuc
            {
                Toplam = toplam,
                Sayfa = sayfa,
                SayfaBoyutu = sayfaBoyutu,
                Talepler = talepler.Select(YkcTalepDto.FromEntity).ToList()
            };
        }

        public async Task<YkcRaporSonuc> RaporAsync(
            YkcTalepListeFiltre filtre,
            AppKullanici kullanici,
            bool genelYetkili)
        {
            var query = FiltreleriUygula(TalepQuery(), filtre, kullanici, genelYetkili);

            var toplam = await query.CountAsync();
            var durumOzetleri = await query
                .GroupBy(x => x.Durum)
                .Select(x => new YkcRaporDurumOzetDto { Durum = x.Key, Sayi = x.Count() })
                .ToListAsync();

            var hedefOzetleri = await query
                .GroupBy(x => x.HedefUygulama ?? "")
                .Select(x => new YkcRaporMetinOzetDto { Ad = x.Key, Sayi = x.Count() })
                .ToListAsync();

            var ekipOzetleri = await query
                .Where(x => x.AtananEkip != null && x.AtananEkip != "")
                .GroupBy(x => x.AtananEkip!)
                .Select(x => new YkcRaporMetinOzetDto { Ad = x.Key, Sayi = x.Count() })
                .OrderByDescending(x => x.Sayi)
                .Take(8)
                .ToListAsync();

            var firmaOzetleri = await query
                .Where(x => x.Firma != null && x.Firma.FirmaAdi != null)
                .GroupBy(x => x.Firma!.FirmaAdi!)
                .Select(x => new YkcRaporMetinOzetDto { Ad = x.Key, Sayi = x.Count() })
                .OrderByDescending(x => x.Sayi)
                .Take(8)
                .ToListAsync();

            var kayitlar = await query
                .OrderByDescending(x => x.TalepTarihi)
                .ThenByDescending(x => x.Id)
                .Take(500)
                .ToListAsync();

            return new YkcRaporSonuc
            {
                Toplam = toplam,
                KayitLimiti = 500,
                DurumOzetleri = durumOzetleri,
                HedefOzetleri = hedefOzetleri,
                EkipOzetleri = ekipOzetleri,
                FirmaOzetleri = firmaOzetleri,
                Kayitlar = kayitlar.Select(YkcRaporKayitDto.FromEntity).ToList()
            };
        }

        public async Task<YkcTalepDetayDto?> GetirAsync(int id, AppKullanici kullanici, bool genelYetkili)
        {
            var talep = await YetkiKapsamiUygula(TalepQuery(), kullanici, genelYetkili)
                .FirstOrDefaultAsync(x => x.Id == id);

            return talep == null ? null : YkcTalepDetayDto.FromEntity(talep);
        }

        public async Task<YkcIslemSonuc> OlusturAsync(YkcTalepKaydetDto dto, AppKullanici kullanici)
        {
            var kontrol = TalepDogrula(dto);
            if (!kontrol.Basarili)
                return kontrol;

            var firma = kullanici.FirmaId.HasValue
                ? await _context.Ys_Firmalar
                    .Include(x => x.Sirket)
                    .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId.Value && !x.SilindiMi)
                : null;

            var talep = new Ykc_Talep
            {
                FirmaId = kullanici.FirmaId ?? dto.FirmaId,
                SirketId = kullanici.SirketId ?? firma?.SirketId ?? dto.SirketId,
                Vkn = firma?.VergiNo ?? dto.Vkn,
                FirmaKodu = dto.FirmaKodu,
                KaynakTipi = string.IsNullOrWhiteSpace(dto.KaynakTipi) ? "Manuel" : dto.KaynakTipi.Trim(),
                TesisatNo = dto.TesisatNo?.Trim(),
                SozlesmeNo = dto.SozlesmeNo?.Trim(),
                AboneNo = dto.AboneNo?.Trim(),
                ProjeNo = dto.ProjeNo?.Trim(),
                SayacNo = dto.SayacNo?.Trim(),
                MusteriAdi = dto.MusteriAdi?.Trim(),
                MusteriTelefon = dto.MusteriTelefon?.Trim(),
                Il = dto.Il?.Trim(),
                Ilce = dto.Ilce?.Trim(),
                Bolge = dto.Bolge?.Trim(),
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
                Aufnr = dto.Aufnr?.Trim(),
                Durum = YkcDurumDegerleri.TalepAlindi,
                TalepTarihi = DateTime.Now,
                HedefUygulama = YkcHedefUygulamaDegerleri.YonetimPaneli,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName
            };

            _context.Ykc_Talepler.Add(talep);
            await _context.SaveChangesAsync();

            _context.Ykc_IslemGecmisi.Add(new Ykc_IslemGecmisi
            {
                TalepId = talep.Id,
                IslemTipi = "TalepOlusturuldu",
                YeniDurum = talep.Durum,
                Aciklama = "Cihaz değişim talebi oluşturuldu.",
                KullaniciId = kullanici.Id,
                KullaniciAdi = kullanici.UserName,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName
            });
            await _context.SaveChangesAsync();

            return YkcIslemSonuc.BasariliSonuc("Cihaz değişim talebi oluşturuldu.", talep.Id);
        }

        public async Task<YkcIslemSonuc> AtamaYapAsync(
            YkcAtamaKaydetDto dto,
            AppKullanici kullanici,
            bool genelYetkili)
        {
            var talep = await YetkiKapsamiUygula(_context.Ykc_Talepler.Where(x => !x.SilindiMi), kullanici, genelYetkili)
                .FirstOrDefaultAsync(x => x.Id == dto.TalepId);

            if (talep == null)
                return YkcIslemSonuc.HataliSonuc("Cihaz değişim talebi bulunamadı.");

            if (DurumTerminalMi(talep.Durum))
                return YkcIslemSonuc.HataliSonuc("Tamamlanan, reddedilen veya iptal edilen talep icin atama yapilamaz.");

            if (talep.Durum == YkcDurumDegerleri.TalepAlindi)
                return YkcIslemSonuc.HataliSonuc("Randevu ve atama icin talep once Ic Tesisat Incelemesinde durumuna alinmalidir.");

            if (!AtamaYapilabilirMi(talep.Durum))
                return YkcIslemSonuc.HataliSonuc("Bu durumdaki talep icin randevu ve atama yapilamaz.");

            var firmaFormuVar = await FormDosyasiVarMiAsync(talep.Id, YkcFormDosyaTuruDegerleri.FirmaFormu);
            if (!firmaFormuVar)
                return YkcIslemSonuc.HataliSonuc("Randevu ve atama icin once firma imzali FR265 formu yuklenmelidir.");

            if (string.IsNullOrWhiteSpace(dto.AtananEkip))
                return YkcIslemSonuc.HataliSonuc("Randevu icin ekip secimi zorunludur.");

            if (string.IsNullOrWhiteSpace(dto.Bolge) && string.IsNullOrWhiteSpace(talep.Bolge))
                return YkcIslemSonuc.HataliSonuc("Randevu icin bolge bilgisi zorunludur.");

            if (!dto.RandevuTarihi.HasValue)
                return YkcIslemSonuc.HataliSonuc("Randevu tarihi zorunludur.");

            if (string.IsNullOrWhiteSpace(dto.RandevuSaati))
                return YkcIslemSonuc.HataliSonuc("Randevu saati zorunludur.");

            var eskiDurum = talep.Durum;
            var hedef = HedefUygulamaBelirle(dto.AtananKullaniciTipi, dto.HedefUygulama, dto.CallCenterTetiklenecekMi);

            talep.AtananKullaniciId = dto.AtananKullaniciId;
            talep.AtananKullaniciTipi = dto.AtananKullaniciTipi?.Trim();
            talep.AtananEkip = dto.AtananEkip?.Trim();
            talep.Bolge = string.IsNullOrWhiteSpace(dto.Bolge) ? talep.Bolge : dto.Bolge.Trim();
            talep.HedefUygulama = hedef;
            talep.RandevuTarihi = dto.RandevuTarihi;
            talep.RandevuSaati = dto.RandevuSaati?.Trim();
            talep.CallCenterTetiklenecekMi = dto.CallCenterTetiklenecekMi || hedef == YkcHedefUygulamaDegerleri.Crm187;
            talep.Durum = talep.Durum == YkcDurumDegerleri.SahaIsleminde
                ? YkcDurumDegerleri.SahaIsleminde
                : YkcDurumDegerleri.Atandi;
            talep.GuncellemeTarihi = DateTime.Now;
            talep.GuncelleyenKullanici = kullanici.UserName;

            _context.Ykc_Atamalar.Add(new Ykc_Atama
            {
                TalepId = talep.Id,
                AtananKullaniciId = dto.AtananKullaniciId,
                AtananKullaniciTipi = dto.AtananKullaniciTipi?.Trim(),
                AtananEkip = dto.AtananEkip?.Trim(),
                Bolge = dto.Bolge?.Trim(),
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
            return YkcIslemSonuc.BasariliSonuc("Cihaz değişim talebi için randevu ve atama kaydedildi.", talep.Id);
        }

        public async Task<YkcIslemSonuc> DurumGuncelleAsync(
            YkcDurumGuncelleDto dto,
            AppKullanici kullanici,
            bool genelYetkili)
        {
            var talep = await YetkiKapsamiUygula(_context.Ykc_Talepler.Where(x => !x.SilindiMi), kullanici, genelYetkili)
                .FirstOrDefaultAsync(x => x.Id == dto.TalepId);

            if (talep == null)
                return YkcIslemSonuc.HataliSonuc("Cihaz değişim talebi bulunamadı.");

            if (dto.Durum == YkcDurumDegerleri.Reddedildi && string.IsNullOrWhiteSpace(dto.Aciklama))
                return YkcIslemSonuc.HataliSonuc("Red islemi icin aciklama zorunludur.");

            if (dto.Durum == YkcDurumDegerleri.Iptal && string.IsNullOrWhiteSpace(dto.Aciklama))
                return YkcIslemSonuc.HataliSonuc("Iptal islemi icin aciklama zorunludur.");

            var eskiDurum = talep.Durum;
            if (eskiDurum == dto.Durum)
                return YkcIslemSonuc.BasariliSonuc("Talep zaten secilen durumda.", talep.Id);

            var sahaFormuVar = await FormDosyasiVarMiAsync(talep.Id, YkcFormDosyaTuruDegerleri.SahaIslakImzaliForm);

            if (dto.Durum == YkcDurumDegerleri.Tamamlandi && !sahaFormuVar)
                return YkcIslemSonuc.HataliSonuc("Talebi tamamlamak icin once saha islak imzali formu yuklenmelidir.");

            if (dto.Durum == YkcDurumDegerleri.Tamamlandi && !RandevuZamaniGeldiMi(talep.RandevuTarihi, talep.RandevuSaati))
                return YkcIslemSonuc.HataliSonuc("Randevu zamani gelmeden talep tamamlandi durumuna alinamaz.");

            if (!DurumGecisiGecerliMi(eskiDurum, dto.Durum, sahaFormuVar))
                return YkcIslemSonuc.HataliSonuc("Bu durum gecisi icin onceki adimlar tamamlanmalidir.");

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
            return YkcIslemSonuc.BasariliSonuc("Cihaz değişim talebi durumu güncellendi.", talep.Id);
        }

        public async Task<YkcIslemSonuc> DosyaEkleAsync(
            YkcDosyaKaydetDto dto,
            AppKullanici kullanici,
            bool genelYetkili)
        {
            var talep = await YetkiKapsamiUygula(_context.Ykc_Talepler.Where(x => !x.SilindiMi), kullanici, genelYetkili)
                .FirstOrDefaultAsync(x => x.Id == dto.TalepId);

            if (talep == null)
                return YkcIslemSonuc.HataliSonuc("Cihaz değişim talebi bulunamadı.");

            if (string.IsNullOrWhiteSpace(dto.DosyaYolu))
                return YkcIslemSonuc.HataliSonuc("Dosya yolu zorunludur.");

            var dosyaTuru = string.IsNullOrWhiteSpace(dto.DosyaTuru)
                ? YkcFormDosyaTuruDegerleri.FirmaFormu
                : dto.DosyaTuru.Trim();

            _context.Ykc_FormDosyalari.Add(new Ykc_FormDosya
            {
                TalepId = talep.Id,
                DosyaTuru = dosyaTuru,
                DosyaAdi = dto.DosyaAdi?.Trim(),
                DosyaYolu = dto.DosyaYolu.Trim(),
                IcerikTipi = dto.IcerikTipi?.Trim(),
                DosyaBoyutu = dto.DosyaBoyutu,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName
            });

            if (dosyaTuru == YkcFormDosyaTuruDegerleri.SahaIslakImzaliForm
                && talep.Durum == YkcDurumDegerleri.Atandi)
            {
                var eskiDurum = talep.Durum;
                talep.Durum = YkcDurumDegerleri.SahaIsleminde;
                talep.GuncellemeTarihi = DateTime.Now;
                talep.GuncelleyenKullanici = kullanici.UserName;

                _context.Ykc_IslemGecmisi.Add(new Ykc_IslemGecmisi
                {
                    TalepId = talep.Id,
                    IslemTipi = "DurumGuncellendi",
                    EskiDurum = eskiDurum,
                    YeniDurum = talep.Durum,
                    Aciklama = "Saha islak imzali form yuklendigi icin saha islemi baslatildi.",
                    KullaniciId = kullanici.Id,
                    KullaniciAdi = kullanici.UserName,
                    OlusturmaTarihi = DateTime.Now,
                    OlusturanKullanici = kullanici.UserName
                });
            }

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

            await _context.SaveChangesAsync();
            return YkcIslemSonuc.BasariliSonuc("Cihaz değişim form dosyası kaydedildi.", talep.Id);
        }

        public async Task<bool> IslemGecmisiEkleAsync(
            int talepId,
            AppKullanici kullanici,
            bool genelYetkili,
            string islemTipi,
            string? aciklama)
        {
            var talep = await YetkiKapsamiUygula(_context.Ykc_Talepler.Where(x => !x.SilindiMi), kullanici, genelYetkili)
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

        private Task<bool> FormDosyasiVarMiAsync(int talepId, string dosyaTuru)
        {
            return _context.Ykc_FormDosyalari.AnyAsync(x =>
                x.TalepId == talepId &&
                !x.SilindiMi &&
                x.DosyaTuru == dosyaTuru);
        }

        private static bool RandevuZamaniGeldiMi(DateTime? randevuTarihi, string? randevuSaati)
        {
            if (!randevuTarihi.HasValue || string.IsNullOrWhiteSpace(randevuSaati))
                return false;

            if (!TimeSpan.TryParse(randevuSaati.Trim(), out var saat))
                return false;

            return randevuTarihi.Value.Date.Add(saat) <= DateTime.Now;
        }

        private static bool DurumGecisiGecerliMi(int eskiDurum, int yeniDurum, bool sahaFormuVar)
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
                YkcDurumDegerleri.Atandi => yeniDurum == YkcDurumDegerleri.SahaIsleminde
                    || (sahaFormuVar && yeniDurum == YkcDurumDegerleri.Tamamlandi),
                YkcDurumDegerleri.SahaIsleminde => yeniDurum == YkcDurumDegerleri.Tamamlandi,
                _ => false
            };
        }

        private IQueryable<Ykc_Talep> TalepQuery()
        {
            return _context.Ykc_Talepler
                .Include(x => x.Firma)
                    .ThenInclude(x => x!.YetkiBelgeleri)
                .Include(x => x.Sirket)
                .Include(x => x.FormDosyalari.Where(d => !d.SilindiMi))
                .Include(x => x.Atamalar.Where(a => !a.SilindiMi))
                .Include(x => x.IslemGecmisi.Where(g => !g.SilindiMi))
                .Where(x => !x.SilindiMi)
                .Where(x =>
                    (x.TesisatNo == null || x.TesisatNo != "string") &&
                    (x.MusteriAdi == null || x.MusteriAdi != "string"));
        }

        private static IQueryable<Ykc_Talep> FiltreleriUygula(
            IQueryable<Ykc_Talep> query,
            YkcTalepListeFiltre filtre,
            AppKullanici kullanici,
            bool genelYetkili)
        {
            query = YetkiKapsamiUygula(query, kullanici, genelYetkili);

            var swaggerOrnekFiltre = SwaggerOrnekFiltreMi(filtre);
            var sirketId = PozitifId(filtre.SirketId);
            var firmaId = PozitifId(filtre.FirmaId);
            var tesisatNo = FiltreMetni(filtre.TesisatNo);
            var firma = FiltreMetni(filtre.Firma);
            var il = FiltreMetni(filtre.Il);
            var ilce = FiltreMetni(filtre.Ilce);
            var bolge = FiltreMetni(filtre.Bolge);
            var ekip = FiltreMetni(filtre.Ekip);
            var marka = FiltreMetni(filtre.Marka);
            var hedefUygulama = FiltreMetni(filtre.HedefUygulama);
            var durum = filtre.Durum.GetValueOrDefault() > 0 ? filtre.Durum : null;
            var baslangicTarihi = swaggerOrnekFiltre ? null : filtre.BaslangicTarihi;
            var bitisTarihi = swaggerOrnekFiltre ? null : filtre.BitisTarihi;

            if (sirketId.HasValue && genelYetkili)
                query = query.Where(x => x.SirketId == sirketId.Value);

            if (firmaId.HasValue && genelYetkili)
                query = query.Where(x => x.FirmaId == firmaId.Value);

            if (!string.IsNullOrWhiteSpace(tesisatNo))
                query = query.Where(x => x.TesisatNo != null && x.TesisatNo.Contains(tesisatNo));

            if (!string.IsNullOrWhiteSpace(firma))
                query = query.Where(x => x.Firma != null && x.Firma.FirmaAdi != null && x.Firma.FirmaAdi.Contains(firma));

            if (!string.IsNullOrWhiteSpace(il))
                query = query.Where(x => x.Il != null && x.Il.Contains(il));

            if (!string.IsNullOrWhiteSpace(ilce))
                query = query.Where(x => x.Ilce != null && x.Ilce.Contains(ilce));

            if (!string.IsNullOrWhiteSpace(bolge))
                query = query.Where(x => x.Bolge != null && x.Bolge.Contains(bolge));

            if (!string.IsNullOrWhiteSpace(ekip))
                query = query.Where(x => x.AtananEkip != null && x.AtananEkip.Contains(ekip));

            if (!string.IsNullOrWhiteSpace(marka))
            {
                query = query.Where(x =>
                    (x.EskiMarka != null && x.EskiMarka.Contains(marka)) ||
                    (x.YeniMarka != null && x.YeniMarka.Contains(marka)));
            }

            if (!string.IsNullOrWhiteSpace(hedefUygulama))
                query = query.Where(x => x.HedefUygulama == hedefUygulama);

            if (durum.HasValue)
                query = query.Where(x => x.Durum == durum.Value);

            if (baslangicTarihi.HasValue)
                query = query.Where(x => x.TalepTarihi >= baslangicTarihi.Value.Date);

            if (bitisTarihi.HasValue)
                query = query.Where(x => x.TalepTarihi < bitisTarihi.Value.Date.AddDays(1));

            return query;
        }

        private static int? PozitifId(int? value)
        {
            return value.GetValueOrDefault() > 0 ? value : null;
        }

        private static string? FiltreMetni(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || PlaceholderDegerMi(value))
                return null;

            return value.Trim();
        }

        private static bool SwaggerOrnekFiltreMi(YkcTalepListeFiltre filtre)
        {
            var metinlerdeOrnekVar = new[]
            {
                filtre.TesisatNo,
                filtre.Firma,
                filtre.Il,
                filtre.Ilce,
                filtre.Bolge,
                filtre.Ekip,
                filtre.Marka,
                filtre.HedefUygulama
            }.Any(PlaceholderDegerMi);

            return metinlerdeOrnekVar
                && filtre.SirketId.GetValueOrDefault() <= 0
                && filtre.FirmaId.GetValueOrDefault() <= 0
                && filtre.Durum.GetValueOrDefault() <= 0
                && filtre.Sayfa <= 0
                && filtre.SayfaBoyutu <= 0;
        }

        private static IQueryable<Ykc_Talep> YetkiKapsamiUygula(
            IQueryable<Ykc_Talep> query,
            AppKullanici kullanici,
            bool genelYetkili)
        {
            if (genelYetkili)
                return query;

            if (kullanici.FirmaId.HasValue)
                return query.Where(x => x.FirmaId == kullanici.FirmaId.Value);

            if (kullanici.SirketId.HasValue)
                return query.Where(x => x.SirketId == kullanici.SirketId.Value);

            return query.Where(x => false);
        }

        private static YkcIslemSonuc TalepDogrula(YkcTalepKaydetDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TesisatNo))
                return YkcIslemSonuc.HataliSonuc("Tesisat no zorunludur.");

            if (string.IsNullOrWhiteSpace(dto.YeniCihazTipi) && string.IsNullOrWhiteSpace(dto.YeniCihazTipiKodu))
                return YkcIslemSonuc.HataliSonuc("Yeni cihaz tipi zorunludur.");

            if (string.IsNullOrWhiteSpace(dto.YeniMarka) && string.IsNullOrWhiteSpace(dto.YeniMarkaKodu))
                return YkcIslemSonuc.HataliSonuc("Yeni marka zorunludur.");

            if (string.IsNullOrWhiteSpace(dto.YeniBacaTipi) && string.IsNullOrWhiteSpace(dto.YeniBacaTipiKodu))
                return YkcIslemSonuc.HataliSonuc("Yeni baca tipi zorunludur.");

            if (string.IsNullOrWhiteSpace(dto.YeniKapasite))
                return YkcIslemSonuc.HataliSonuc("Yeni kapasite zorunludur.");

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
                return YkcIslemSonuc.HataliSonuc("Test amacli placeholder degerlerle cihaz degisim talebi olusturulamaz. Lutfen gercek tesisat ve cihaz bilgilerini giriniz.");
            }

            if (!BosVeyaAyni(dto.EskiCihazTipiKodu, dto.YeniCihazTipiKodu)
                || !BosVeyaAyni(dto.EskiBacaTipiKodu, dto.YeniBacaTipiKodu)
                || !BosVeyaAyni(dto.EskiKapasite, dto.YeniKapasite))
            {
                return YkcIslemSonuc.HataliSonuc("Eski cihaz ile yeni cihaz tipi, baca tipi veya kapasite uyumlu değil. Proje tadilatı gerekebilir.");
            }

            return YkcIslemSonuc.BasariliSonuc("Uygun.");
        }

        private static bool BosVeyaAyni(string? eskiDeger, string? yeniDeger)
        {
            if (string.IsNullOrWhiteSpace(eskiDeger))
                return true;

            if (string.IsNullOrWhiteSpace(yeniDeger))
                return false;

            return string.Equals(eskiDeger.Trim(), yeniDeger.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool PlaceholderDegerVar(params string?[] degerler)
        {
            return degerler.Any(PlaceholderDegerMi);
        }

        private static bool PlaceholderDegerMi(string? deger)
        {
            return string.Equals(deger?.Trim(), "string", StringComparison.OrdinalIgnoreCase);
        }

        private static string HedefUygulamaBelirle(string? kullaniciTipi, string? hedefUygulama, bool callCenterTetiklenecekMi)
        {
            if (!string.IsNullOrWhiteSpace(hedefUygulama))
                return hedefUygulama.Trim();

            var tip = TurkceKarakterNormalize(kullaniciTipi);
            if (callCenterTetiklenecekMi || tip.Contains("187") || tip.Contains("ACIL"))
                return YkcHedefUygulamaDegerleri.Crm187;

            if (tip.Contains("MUHENDIS"))
                return YkcHedefUygulamaDegerleri.DogalgazMobileApp;

            return YkcHedefUygulamaDegerleri.YonetimPaneli;
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

    public class YkcTalepListeFiltre
    {
        public int? SirketId { get; set; }
        public int? FirmaId { get; set; }
        public string? TesisatNo { get; set; }
        public string? Firma { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? Bolge { get; set; }
        public string? Ekip { get; set; }
        public string? Marka { get; set; }
        public string? HedefUygulama { get; set; }
        public int? Durum { get; set; }
        public DateTime? BaslangicTarihi { get; set; }
        public DateTime? BitisTarihi { get; set; }
        public int Sayfa { get; set; } = 1;
        public int SayfaBoyutu { get; set; } = 50;
    }

    public class YkcTalepListeSonuc
    {
        public int Toplam { get; set; }
        public int Sayfa { get; set; }
        public int SayfaBoyutu { get; set; }
        public List<YkcTalepDto> Talepler { get; set; } = new();
    }

    public class YkcRaporSonuc
    {
        public int Toplam { get; set; }
        public int KayitLimiti { get; set; }
        public List<YkcRaporDurumOzetDto> DurumOzetleri { get; set; } = new();
        public List<YkcRaporMetinOzetDto> HedefOzetleri { get; set; } = new();
        public List<YkcRaporMetinOzetDto> EkipOzetleri { get; set; } = new();
        public List<YkcRaporMetinOzetDto> FirmaOzetleri { get; set; } = new();
        public List<YkcRaporKayitDto> Kayitlar { get; set; } = new();
    }

    public class YkcRaporDurumOzetDto
    {
        public int Durum { get; set; }
        public int Sayi { get; set; }
    }

    public class YkcRaporMetinOzetDto
    {
        public string? Ad { get; set; }
        public int Sayi { get; set; }
    }

    public class YkcTalepKaydetDto
    {
        public int? FirmaId { get; set; }
        public int? SirketId { get; set; }
        public string? Vkn { get; set; }
        public string? FirmaKodu { get; set; }
        public string? KaynakTipi { get; set; }
        public string? TesisatNo { get; set; }
        public string? SozlesmeNo { get; set; }
        public string? AboneNo { get; set; }
        public string? ProjeNo { get; set; }
        public string? SayacNo { get; set; }
        public string? MusteriAdi { get; set; }
        public string? MusteriTelefon { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? Bolge { get; set; }
        public string? Adres { get; set; }
        public string? EskiCihazTipiKodu { get; set; }
        public string? EskiCihazTipi { get; set; }
        public string? EskiMarkaKodu { get; set; }
        public string? EskiMarka { get; set; }
        public string? EskiBacaTipiKodu { get; set; }
        public string? EskiBacaTipi { get; set; }
        public string? EskiKapasite { get; set; }
        public string? YeniCihazTipiKodu { get; set; }
        public string? YeniCihazTipi { get; set; }
        public string? YeniMarkaKodu { get; set; }
        public string? YeniMarka { get; set; }
        public string? YeniBacaTipiKodu { get; set; }
        public string? YeniBacaTipi { get; set; }
        public string? YeniKapasite { get; set; }
        public string? YeniModel { get; set; }
        public string? YeniSeriNo { get; set; }
        public string? Aufnr { get; set; }
    }

    public class YkcTesisatSorguIstek
    {
        public string? TesisatNo { get; set; }
        public string? SozlesmeNo { get; set; }
    }

    public class YkcTesisatSorguSonuc
    {
        public bool Basarili { get; set; }
        public bool ManuelGirisSerbest { get; set; }
        public string? Mesaj { get; set; }
        public string? FirmaKodu { get; set; }
        public string? TesisatNo { get; set; }
        public string? SozlesmeNo { get; set; }
        public string? AboneNo { get; set; }
        public string? SayacNo { get; set; }
        public string? MusteriAdi { get; set; }
        public string? MusteriTelefon { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? Bolge { get; set; }
        public string? Adres { get; set; }
        public string? Durum { get; set; }
        public List<YkcTesisatCihazDto> Cihazlar { get; set; } = new();

        public static YkcTesisatSorguSonuc Basarisiz(string mesaj)
        {
            return new YkcTesisatSorguSonuc
            {
                Basarili = false,
                ManuelGirisSerbest = true,
                Mesaj = mesaj
            };
        }
    }

    public class YkcTesisatCihazDto
    {
        public string? CihazKapasite { get; set; }
        public string? CihazMarka { get; set; }
        public string? CihazTipi { get; set; }
        public string? CihazTipKodu { get; set; }
        public string? ProjeNo { get; set; }
        public string? TesisatNo { get; set; }
    }

    public class YkcAtamaKaydetDto
    {
        public int TalepId { get; set; }
        public string? AtananKullaniciId { get; set; }
        public string? AtananKullaniciTipi { get; set; }
        public string? AtananEkip { get; set; }
        public string? Bolge { get; set; }
        public string? HedefUygulama { get; set; }
        public DateTime? RandevuTarihi { get; set; }
        public string? RandevuSaati { get; set; }
        public bool CallCenterTetiklenecekMi { get; set; }
        public string? Aciklama { get; set; }
    }

    public class YkcDurumGuncelleDto
    {
        public int TalepId { get; set; }
        public int Durum { get; set; }
        public string? Aciklama { get; set; }
    }

    public class YkcDosyaKaydetDto
    {
        public int TalepId { get; set; }
        public string? DosyaTuru { get; set; }
        public string? DosyaAdi { get; set; }
        public string? DosyaYolu { get; set; }
        public string? IcerikTipi { get; set; }
        public long? DosyaBoyutu { get; set; }
    }

    public class YkcIslemSonuc
    {
        public bool Basarili { get; set; }
        public string? Mesaj { get; set; }
        public int? Id { get; set; }

        public static YkcIslemSonuc BasariliSonuc(string mesaj, int? id = null)
        {
            return new YkcIslemSonuc { Basarili = true, Mesaj = mesaj, Id = id };
        }

        public static YkcIslemSonuc HataliSonuc(string mesaj)
        {
            return new YkcIslemSonuc { Basarili = false, Mesaj = mesaj };
        }
    }

    public class YkcTalepDto
    {
        public int Id { get; set; }
        public string? FirmaAdi { get; set; }
        public string? SirketAdi { get; set; }
        public string? TesisatNo { get; set; }
        public string? ProjeNo { get; set; }
        public string? MusteriAdi { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? Bolge { get; set; }
        public string? EskiCihaz { get; set; }
        public string? YeniCihaz { get; set; }
        public int Durum { get; set; }
        public DateTime TalepTarihi { get; set; }
        public string? AtananEkip { get; set; }
        public string? HedefUygulama { get; set; }
        public DateTime? RandevuTarihi { get; set; }
        public string? RandevuSaati { get; set; }

        public static YkcTalepDto FromEntity(Ykc_Talep talep)
        {
            return new YkcTalepDto
            {
                Id = talep.Id,
                FirmaAdi = talep.Firma?.FirmaAdi,
                SirketAdi = talep.Sirket?.SirketAdi,
                TesisatNo = talep.TesisatNo,
                ProjeNo = talep.ProjeNo,
                MusteriAdi = talep.MusteriAdi,
                Il = talep.Il,
                Ilce = talep.Ilce,
                Bolge = talep.Bolge,
                EskiCihaz = CihazOzeti(talep.EskiMarka, talep.EskiCihazTipi, talep.EskiKapasite),
                YeniCihaz = CihazOzeti(talep.YeniMarka, talep.YeniCihazTipi, talep.YeniKapasite),
                Durum = talep.Durum,
                TalepTarihi = talep.TalepTarihi,
                AtananEkip = talep.AtananEkip,
                HedefUygulama = talep.HedefUygulama,
                RandevuTarihi = talep.RandevuTarihi,
                RandevuSaati = talep.RandevuSaati
            };
        }

        private static string CihazOzeti(params string?[] parcalar)
        {
            return string.Join(" / ", parcalar.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
        }
    }

    public class YkcRaporKayitDto
    {
        public int Id { get; set; }
        public string? FirmaAdi { get; set; }
        public string? SirketAdi { get; set; }
        public string? MusteriAdi { get; set; }
        public string? TesisatNo { get; set; }
        public string? ProjeNo { get; set; }
        public string? SayacNo { get; set; }
        public string? EskiCihazTipi { get; set; }
        public string? EskiMarka { get; set; }
        public string? YeniCihazTipi { get; set; }
        public string? YeniMarka { get; set; }
        public string? YeniModel { get; set; }
        public string? YeniKapasite { get; set; }
        public string? Bolge { get; set; }
        public string? AtananEkip { get; set; }
        public string? HedefUygulama { get; set; }
        public DateTime TalepTarihi { get; set; }
        public DateTime? RandevuTarihi { get; set; }
        public string? RandevuSaati { get; set; }
        public int Durum { get; set; }
        public bool FirmaFormuVar { get; set; }
        public bool SahaFormuVar { get; set; }

        public static YkcRaporKayitDto FromEntity(Ykc_Talep talep)
        {
            return new YkcRaporKayitDto
            {
                Id = talep.Id,
                FirmaAdi = talep.Firma?.FirmaAdi,
                SirketAdi = talep.Sirket?.SirketAdi,
                MusteriAdi = talep.MusteriAdi,
                TesisatNo = talep.TesisatNo,
                ProjeNo = talep.ProjeNo,
                SayacNo = talep.SayacNo,
                EskiCihazTipi = talep.EskiCihazTipi,
                EskiMarka = talep.EskiMarka,
                YeniCihazTipi = talep.YeniCihazTipi,
                YeniMarka = talep.YeniMarka,
                YeniModel = talep.YeniModel,
                YeniKapasite = talep.YeniKapasite,
                Bolge = talep.Bolge,
                AtananEkip = talep.AtananEkip,
                HedefUygulama = talep.HedefUygulama,
                TalepTarihi = talep.TalepTarihi,
                RandevuTarihi = talep.RandevuTarihi,
                RandevuSaati = talep.RandevuSaati,
                Durum = talep.Durum,
                FirmaFormuVar = talep.FormDosyalari.Any(x => !x.SilindiMi && x.DosyaTuru == YkcFormDosyaTuruDegerleri.FirmaFormu),
                SahaFormuVar = talep.FormDosyalari.Any(x => !x.SilindiMi && x.DosyaTuru == YkcFormDosyaTuruDegerleri.SahaIslakImzaliForm)
            };
        }
    }

    public class YkcTalepDetayDto : YkcTalepDto
    {
        public string? SozlesmeNo { get; set; }
        public string? AboneNo { get; set; }
        public string? SayacNo { get; set; }
        public string? MusteriTelefon { get; set; }
        public string? Adres { get; set; }
        public string? Vkn { get; set; }
        public string? FirmaYetkiliKisi { get; set; }
        public string? YetkiBelgesiNo { get; set; }
        public string? TuketimNoktasi { get; set; }
        public string? BaglantiNesnesi { get; set; }
        public string? FirmaKodu { get; set; }
        public string? KaynakTipi { get; set; }
        public string? EskiCihazTipiKodu { get; set; }
        public string? EskiCihazTipi { get; set; }
        public string? EskiMarkaKodu { get; set; }
        public string? EskiMarka { get; set; }
        public string? EskiBacaTipiKodu { get; set; }
        public string? EskiBacaTipi { get; set; }
        public string? EskiKapasite { get; set; }
        public string? YeniCihazTipiKodu { get; set; }
        public string? YeniCihazTipi { get; set; }
        public string? YeniMarkaKodu { get; set; }
        public string? YeniMarka { get; set; }
        public string? YeniBacaTipiKodu { get; set; }
        public string? YeniBacaTipi { get; set; }
        public string? YeniKapasite { get; set; }
        public string? YeniModel { get; set; }
        public string? YeniSeriNo { get; set; }
        public string? RedAciklama { get; set; }
        public DateTime? IptalTarihi { get; set; }
        public string? IptalEdenKullaniciId { get; set; }
        public string? IptalAciklama { get; set; }
        public string? RandevuId { get; set; }
        public string? IsEmriNo { get; set; }
        public string? Aufnr { get; set; }
        public bool CallCenterTetiklenecekMi { get; set; }
        public bool CallCenterTetiklendiMi { get; set; }
        public List<YkcDosyaDto> Dosyalar { get; set; } = new();
        public List<YkcAtamaDto> Atamalar { get; set; } = new();
        public List<YkcGecmisDto> Gecmis { get; set; } = new();

        public new static YkcTalepDetayDto FromEntity(Ykc_Talep talep)
        {
            var dto = new YkcTalepDetayDto
            {
                Id = talep.Id,
                FirmaAdi = talep.Firma?.FirmaAdi,
                SirketAdi = talep.Sirket?.SirketAdi,
                TesisatNo = talep.TesisatNo,
                ProjeNo = talep.ProjeNo,
                MusteriAdi = talep.MusteriAdi,
                Il = talep.Il,
                Ilce = talep.Ilce,
                Bolge = talep.Bolge,
                EskiCihaz = YkcTalepDto.FromEntity(talep).EskiCihaz,
                YeniCihaz = YkcTalepDto.FromEntity(talep).YeniCihaz,
                Durum = talep.Durum,
                TalepTarihi = talep.TalepTarihi,
                AtananEkip = talep.AtananEkip,
                HedefUygulama = talep.HedefUygulama,
                SozlesmeNo = talep.SozlesmeNo,
                AboneNo = talep.AboneNo,
                SayacNo = talep.SayacNo,
                MusteriTelefon = talep.MusteriTelefon,
                Adres = talep.Adres,
                Vkn = talep.Vkn,
                FirmaYetkiliKisi = talep.Firma?.YetkiliKisi,
                YetkiBelgesiNo = YetkiBelgesiNoBul(talep),
                TuketimNoktasi = "",
                BaglantiNesnesi = "",
                FirmaKodu = talep.FirmaKodu,
                KaynakTipi = talep.KaynakTipi,
                EskiCihazTipiKodu = talep.EskiCihazTipiKodu,
                EskiCihazTipi = talep.EskiCihazTipi,
                EskiMarkaKodu = talep.EskiMarkaKodu,
                EskiMarka = talep.EskiMarka,
                EskiBacaTipiKodu = talep.EskiBacaTipiKodu,
                EskiBacaTipi = talep.EskiBacaTipi,
                EskiKapasite = talep.EskiKapasite,
                YeniCihazTipiKodu = talep.YeniCihazTipiKodu,
                YeniCihazTipi = talep.YeniCihazTipi,
                YeniMarkaKodu = talep.YeniMarkaKodu,
                YeniMarka = talep.YeniMarka,
                YeniBacaTipiKodu = talep.YeniBacaTipiKodu,
                YeniBacaTipi = talep.YeniBacaTipi,
                YeniKapasite = talep.YeniKapasite,
                YeniModel = talep.YeniModel,
                YeniSeriNo = talep.YeniSeriNo,
                RedAciklama = talep.RedAciklama,
                IptalTarihi = talep.IptalTarihi,
                IptalEdenKullaniciId = talep.IptalEdenKullaniciId,
                IptalAciklama = talep.IptalAciklama,
                RandevuSaati = talep.RandevuSaati,
                RandevuTarihi = talep.RandevuTarihi,
                RandevuId = talep.RandevuId,
                IsEmriNo = talep.IsEmriNo,
                Aufnr = talep.Aufnr,
                CallCenterTetiklenecekMi = talep.CallCenterTetiklenecekMi,
                CallCenterTetiklendiMi = talep.CallCenterTetiklendiMi,
                Dosyalar = talep.FormDosyalari.OrderByDescending(x => x.OlusturmaTarihi).Select(YkcDosyaDto.FromEntity).ToList(),
                Atamalar = talep.Atamalar.OrderByDescending(x => x.OlusturmaTarihi).Select(YkcAtamaDto.FromEntity).ToList(),
                Gecmis = TekilGecmis(talep.IslemGecmisi)
            };

            return dto;
        }

        private static string? YetkiBelgesiNoBul(Ykc_Talep talep)
        {
            var yetkiBelgesi = talep.Firma?.YetkiBelgeleri?
                .Where(x => !x.SilindiMi && x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi)
                .OrderByDescending(x => x.YetkiBelgesiBitisTarihi)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();

            return yetkiBelgesi?.Id.ToString();
        }

        private static List<YkcGecmisDto> TekilGecmis(IEnumerable<Ykc_IslemGecmisi> gecmisler)
        {
            return gecmisler
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ThenByDescending(x => x.Id)
                .GroupBy(x => new
                {
                    x.IslemTipi,
                    x.EskiDurum,
                    x.YeniDurum,
                    Aciklama = x.Aciklama?.Trim() ?? "",
                    KullaniciAdi = x.KullaniciAdi?.Trim() ?? "",
                    Dakika = new DateTime(
                        x.OlusturmaTarihi.Year,
                        x.OlusturmaTarihi.Month,
                        x.OlusturmaTarihi.Day,
                        x.OlusturmaTarihi.Hour,
                        x.OlusturmaTarihi.Minute,
                        0)
                })
                .Select(x => x.First())
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ThenByDescending(x => x.Id)
                .Select(YkcGecmisDto.FromEntity)
                .ToList();
        }
    }

    public class YkcDosyaDto
    {
        public int Id { get; set; }
        public string? DosyaTuru { get; set; }
        public string? DosyaAdi { get; set; }
        public string? DosyaYolu { get; set; }
        public string? IcerikTipi { get; set; }
        public DateTime OlusturmaTarihi { get; set; }

        public static YkcDosyaDto FromEntity(Ykc_FormDosya dosya)
        {
            return new YkcDosyaDto
            {
                Id = dosya.Id,
                DosyaTuru = dosya.DosyaTuru,
                DosyaAdi = dosya.DosyaAdi,
                DosyaYolu = dosya.DosyaYolu,
                IcerikTipi = dosya.IcerikTipi,
                OlusturmaTarihi = dosya.OlusturmaTarihi
            };
        }
    }

    public class YkcAtamaDto
    {
        public int Id { get; set; }
        public string? AtananKullaniciTipi { get; set; }
        public string? AtananEkip { get; set; }
        public string? Bolge { get; set; }
        public string? HedefUygulama { get; set; }
        public DateTime? RandevuTarihi { get; set; }
        public string? RandevuSaati { get; set; }
        public string? Aciklama { get; set; }
        public DateTime OlusturmaTarihi { get; set; }

        public static YkcAtamaDto FromEntity(Ykc_Atama atama)
        {
            return new YkcAtamaDto
            {
                Id = atama.Id,
                AtananKullaniciTipi = atama.AtananKullaniciTipi,
                AtananEkip = atama.AtananEkip,
                Bolge = atama.Bolge,
                HedefUygulama = atama.HedefUygulama,
                RandevuTarihi = atama.RandevuTarihi,
                RandevuSaati = atama.RandevuSaati,
                Aciklama = atama.Aciklama,
                OlusturmaTarihi = atama.OlusturmaTarihi
            };
        }
    }

    public class YkcGecmisDto
    {
        public int Id { get; set; }
        public string? IslemTipi { get; set; }
        public int? EskiDurum { get; set; }
        public int? YeniDurum { get; set; }
        public string? Aciklama { get; set; }
        public string? KullaniciAdi { get; set; }
        public DateTime OlusturmaTarihi { get; set; }

        public static YkcGecmisDto FromEntity(Ykc_IslemGecmisi gecmis)
        {
            return new YkcGecmisDto
            {
                Id = gecmis.Id,
                IslemTipi = gecmis.IslemTipi,
                EskiDurum = gecmis.EskiDurum,
                YeniDurum = gecmis.YeniDurum,
                Aciklama = gecmis.Aciklama,
                KullaniciAdi = gecmis.KullaniciAdi,
                OlusturmaTarihi = gecmis.OlusturmaTarihi
            };
        }
    }
}
