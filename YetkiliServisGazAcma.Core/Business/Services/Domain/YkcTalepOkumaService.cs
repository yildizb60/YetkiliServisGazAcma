using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services;

public sealed class YkcTalepOkumaService(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<YkcTalepListeSonuc> ListeAsync(
        YkcTalepListeFiltre filtre,
        AppKullanici kullanici,
        bool genelYetkili,
        int? dogrulanmisSirketId = null)
    {
        var query = ListeQuery();
        query = await FiltreleriUygulaAsync(query, filtre, kullanici, genelYetkili, dogrulanmisSirketId);

        var toplam = await query.CountAsync();
        var sayfa = Math.Max(filtre.Sayfa, 1);
        var sayfaBoyutu = Math.Clamp(filtre.SayfaBoyutu <= 0 ? 10 : filtre.SayfaBoyutu, 1, 100);
        var toplamSayfa = Math.Max(1, (int)Math.Ceiling(toplam / (double)sayfaBoyutu));
        sayfa = Math.Min(sayfa, toplamSayfa);

        var talepler = await Sirala(query, filtre.Sirala)
            .Skip((sayfa - 1) * sayfaBoyutu)
            .Take(sayfaBoyutu)
            .ToListAsync();

        return new YkcTalepListeSonuc
        {
            Toplam = toplam,
            Sayfa = sayfa,
            SayfaBoyutu = sayfaBoyutu,
            Talepler = talepler.Select(x => ListeGorunumu(x, kullanici)).ToList()
        };
    }

    public async Task<YkcRaporSonuc> RaporAsync(
        YkcTalepListeFiltre filtre,
        AppKullanici kullanici,
        bool genelYetkili,
        int? dogrulanmisSirketId = null)
    {
        var query = await FiltreleriUygulaAsync(RaporQuery(), filtre, kullanici, genelYetkili, dogrulanmisSirketId);

        var toplam = await query.CountAsync();
        var durumOzetleri = await query
            .GroupBy(x => x.Durum)
            .Select(x => new YkcRaporDurumOzetDto { Durum = x.Key, Sayi = x.Count() })
            .ToListAsync();

        var firmaGorunumu = YkcFirmaSunumu.FirmaKullanicisiMi(kullanici);
        var hedefOzetleri = firmaGorunumu ? new List<YkcRaporMetinOzetDto>() : await query
            .GroupBy(x => x.HedefUygulama ?? "")
            .Select(x => new YkcRaporMetinOzetDto { Ad = x.Key, Sayi = x.Count() })
            .ToListAsync();

        var ekipOzetleri = firmaGorunumu ? new List<YkcRaporMetinOzetDto>() : await query
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

        var sayfa = Math.Max(filtre.Sayfa, 1);
        var sayfaBoyutu = Math.Clamp(filtre.SayfaBoyutu <= 0 ? 10 : filtre.SayfaBoyutu, 10, 100);
        var toplamSayfa = Math.Max(1, (int)Math.Ceiling(toplam / (double)sayfaBoyutu));
        sayfa = Math.Min(sayfa, toplamSayfa);

        if (filtre.DetayTalepId is > 0)
        {
            // Locate the record within the already-authorized result without filtering out its neighbours.
            var detayKaydi = await query.Where(x => x.Id == filtre.DetayTalepId.Value)
                .Select(x => new { x.Id, x.TalepTarihi, x.MusteriAdi }).SingleOrDefaultAsync();
            if (detayKaydi != null)
            {
                var musteriAdi = detayKaydi.MusteriAdi ?? "";
                var oncekiKayitlar = filtre.Sirala switch
                {
                    "tarih-artan" => await query.CountAsync(x => x.TalepTarihi < detayKaydi.TalepTarihi
                        || (x.TalepTarihi == detayKaydi.TalepTarihi && x.Id < detayKaydi.Id)),
                    "abone-artan" => await query.CountAsync(x =>
                        string.Compare(EF.Functions.Collate(x.MusteriAdi ?? "", "Turkish_CI_AS"), musteriAdi) < 0
                        || (EF.Functions.Collate(x.MusteriAdi ?? "", "Turkish_CI_AS") == musteriAdi && x.Id > detayKaydi.Id)),
                    "abone-azalan" => await query.CountAsync(x =>
                        string.Compare(EF.Functions.Collate(x.MusteriAdi ?? "", "Turkish_CI_AS"), musteriAdi) > 0
                        || (EF.Functions.Collate(x.MusteriAdi ?? "", "Turkish_CI_AS") == musteriAdi && x.Id > detayKaydi.Id)),
                    _ => await query.CountAsync(x => x.TalepTarihi > detayKaydi.TalepTarihi
                        || (x.TalepTarihi == detayKaydi.TalepTarihi && x.Id > detayKaydi.Id))
                };
                sayfa = (oncekiKayitlar / sayfaBoyutu) + 1;
            }
        }

        var kayitlar = await Sirala(query, filtre.Sirala)
            .Skip((sayfa - 1) * sayfaBoyutu)
            .Take(sayfaBoyutu)
            .ToListAsync();

        return new YkcRaporSonuc
        {
            Toplam = toplam,
            Sayfa = sayfa,
            SayfaBoyutu = sayfaBoyutu,
            KayitLimiti = sayfaBoyutu,
            DurumOzetleri = durumOzetleri,
            HedefOzetleri = hedefOzetleri,
            EkipOzetleri = ekipOzetleri,
            FirmaOzetleri = firmaOzetleri,
            Kayitlar = kayitlar.Select(x => RaporGorunumu(x, kullanici)).ToList()
        };
    }

    public async Task<List<YkcRaporKayitDto>> RaporKayitlariAsync(
        YkcTalepListeFiltre filtre,
        AppKullanici kullanici,
        bool genelYetkili,
        int kayitLimiti,
        int? dogrulanmisSirketId = null)
    {
        var limit = Math.Clamp(kayitLimiti, 1, 5001);
        var query = await FiltreleriUygulaAsync(RaporQuery(), filtre, kullanici, genelYetkili, dogrulanmisSirketId);
        var kayitlar = await Sirala(query, filtre.Sirala)
            .Take(limit)
            .ToListAsync();

        return kayitlar.Select(x => RaporGorunumu(x, kullanici)).ToList();
    }

    public async Task<YkcDashboardOzetDto> DashboardOzetAsync(AppKullanici kullanici, bool genelYetkili, int? aktifSirketId = null)
    {
        var query = YkcTalepKapsami.Uygula(ListeQuery(), kullanici, genelYetkili, aktifSirketId);

        var toplam = await query.CountAsync();
        var incelemeBekleyen = await (await BekleyenIsFiltresiAsync(query, YkcBekleyenIsDegerleri.Inceleme)).CountAsync();
        var randevuBekleyen = await (await BekleyenIsFiltresiAsync(query, YkcBekleyenIsDegerleri.Randevu)).CountAsync();
        var tamamlamaBekleyen = await (await BekleyenIsFiltresiAsync(query, YkcBekleyenIsDegerleri.Tamamlama)).CountAsync();
        var incelemede = await query.CountAsync(x =>
            x.Durum == YkcDurumDegerleri.TalepAlindi ||
            x.Durum == YkcDurumDegerleri.AtamaBekliyor);
        var randevuSaha = await query.CountAsync(x =>
            x.Durum == YkcDurumDegerleri.Atandi ||
            x.Durum == YkcDurumDegerleri.SahaIsleminde);
        var tamamlanan = await query.CountAsync(x => x.Durum == YkcDurumDegerleri.Tamamlandi);
        var imzaliNihai = await query.CountAsync(x =>
            x.ImzaSurecleri.Any(s =>
                !s.SilindiMi
                && s.Durum == YkcImzaDurumDegerleri.Tamamlandi
                && s.ProviderDocumentId != null
                && s.ProviderDocumentId != ""
                && s.NihaiDosyaId != null
                && s.NihaiDosya != null
                && !s.NihaiDosya.SilindiMi
                && s.NihaiDosya.DosyaTuru == YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai));
        var redIptal = await query.CountAsync(x =>
            x.Durum == YkcDurumDegerleri.Reddedildi ||
            x.Durum == YkcDurumDegerleri.Iptal);
        var imzaBekleyen = await query.CountAsync(x =>
            x.ImzaSurecleri.Any(s =>
                !s.SilindiMi
                && s.ProviderDocumentId != null
                && s.ProviderDocumentId != ""
                && (s.Durum == YkcImzaDurumDegerleri.ImzayaGonderildi
                    || s.Durum == YkcImzaDurumDegerleri.ImzaBekliyor
                    || s.Durum == YkcImzaDurumDegerleri.KismiImzali)));

        var sonTalepler = await query
            .OrderByDescending(x => x.TalepTarihi)
            .ThenByDescending(x => x.Id)
            .Take(5)
            .ToListAsync();

        return new YkcDashboardOzetDto
        {
            Toplam = toplam,
            Incelemede = incelemede,
            IncelemeBekleyen = incelemeBekleyen,
            RandevuBekleyen = randevuBekleyen,
            TamamlamaBekleyen = tamamlamaBekleyen,
            RandevuSaha = randevuSaha,
            Tamamlanan = tamamlanan,
            ImzaliNihai = imzaliNihai,
            ImzaBekleyen = imzaBekleyen,
            RedIptal = redIptal,
            SonTalepler = sonTalepler.Select(x => ListeGorunumu(x, kullanici)).ToList()
        };
    }

    public async Task<YkcTalepDetayDto?> GetirAsync(
        int id, AppKullanici kullanici, bool genelYetkili, int? dogrulanmisSirketId = null)
    {
        var talep = await YkcTalepKapsami.Uygula(TalepOkumaQuery(), kullanici, genelYetkili, dogrulanmisSirketId)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (talep == null)
            return null;

        var dto = YkcTalepDetayDto.FromEntity(talep);
        var kontrolcuIdleri = dto.Kontroller.Where(x => x.KontrolEdenKullaniciId != null)
            .Select(x => x.KontrolEdenKullaniciId!).Distinct().ToList();
        var kontrolcuAdlari = await _context.Users.AsNoTracking()
            .Where(x => kontrolcuIdleri.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.AdSoyad);
        foreach (var kontrol in dto.Kontroller)
            if (kontrol.KontrolEdenKullaniciId != null && kontrolcuAdlari.TryGetValue(kontrol.KontrolEdenKullaniciId, out var ad))
                kontrol.KontrolEdenAdi = ad;
        var sonKontrol = talep.Kontroller
            .Where(x => !x.SilindiMi && !string.IsNullOrWhiteSpace(x.KontrolEdenKullaniciId))
            .OrderByDescending(x => x.KontrolTarihi ?? x.OlusturmaTarihi)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();
        var sahaKaydi = talep.IslemGecmisi
            .Where(x => !x.SilindiMi
                && x.YeniDurum == YkcDurumDegerleri.SahaIsleminde
                && !string.IsNullOrWhiteSpace(x.KullaniciId))
            .OrderByDescending(x => x.OlusturmaTarihi)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();
        var yetkiliKullaniciId = sonKontrol?.KontrolEdenKullaniciId
            ?? sahaKaydi?.KullaniciId
            ?? talep.AtananKullaniciId;

        if (!string.IsNullOrWhiteSpace(yetkiliKullaniciId))
        {
            dto.GazDagitimYetkilisiAdi = await _context.Users
                .AsNoTracking()
                .Where(x => x.Id == yetkiliKullaniciId)
                .Select(x => x.AdSoyad)
                .FirstOrDefaultAsync();
        }

        dto.GazDagitimIslemTarihi = sonKontrol?.KontrolTarihi ?? sahaKaydi?.OlusturmaTarihi;
        return dto;
    }

    private static IOrderedQueryable<Ykc_Talep> Sirala(IQueryable<Ykc_Talep> query, string? sirala)
    {
        return sirala switch
        {
            "tarih-artan" => query.OrderBy(x => x.TalepTarihi).ThenBy(x => x.Id),
            "abone-artan" => query.OrderBy(x => EF.Functions.Collate(x.MusteriAdi ?? "", "Turkish_CI_AS")).ThenByDescending(x => x.Id),
            "abone-azalan" => query.OrderByDescending(x => EF.Functions.Collate(x.MusteriAdi ?? "", "Turkish_CI_AS")).ThenByDescending(x => x.Id),
            _ => query.OrderByDescending(x => x.TalepTarihi).ThenByDescending(x => x.Id)
        };
    }

    private IQueryable<Ykc_Talep> ListeQuery()
    {
        return _context.Ykc_Talepler
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Firma)
            .Include(x => x.Sirket)
            .Include(x => x.Kontroller.Where(k => !k.SilindiMi))
            .Where(x => !x.SilindiMi)
            .Where(x =>
                (x.TesisatNo == null || x.TesisatNo != "string") &&
                (x.MusteriAdi == null || x.MusteriAdi != "string"));
    }

    private IQueryable<Ykc_Talep> RaporQuery()
    {
        return ListeQuery()
            .AsSplitQuery()
            .Include(x => x.FormDosyalari.Where(d => !d.SilindiMi))
            .Include(x => x.ImzaSurecleri.Where(s => !s.SilindiMi));
    }

    private IQueryable<Ykc_Talep> TalepOkumaQuery()
    {
        return RaporQuery()
            .AsSplitQuery()
            .Include(x => x.Firma).ThenInclude(x => x!.YetkiBelgeleri)
            .Include(x => x.Atamalar.Where(a => !a.SilindiMi))
            .Include(x => x.IslemGecmisi.Where(g => !g.SilindiMi))
            .Include(x => x.ImzaSurecleri.Where(s => !s.SilindiMi))
                .ThenInclude(x => x.Imzacilar.Where(i => !i.SilindiMi))
            .Include(x => x.ImzaSurecleri.Where(s => !s.SilindiMi))
                .ThenInclude(x => x.NihaiDosya);
    }

    private static async Task<IQueryable<Ykc_Talep>> FiltreleriUygulaAsync(
        IQueryable<Ykc_Talep> query,
        YkcTalepListeFiltre filtre,
        AppKullanici kullanici,
        bool genelYetkili,
        int? dogrulanmisSirketId = null)
    {
        query = YkcTalepKapsami.Uygula(query, kullanici, genelYetkili, dogrulanmisSirketId);

        var kayitIdleri = filtre.KayitIdleri?
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        if (kayitIdleri is not null)
            query = query.Where(x => kayitIdleri.Contains(x.Id));

        var swaggerOrnekFiltre = SwaggerOrnekFiltreMi(filtre);
        var sirketId = PozitifId(filtre.SirketId);
        var firmaId = PozitifId(filtre.FirmaId);
        var talepId = PozitifId(filtre.TalepId);
        var q = FiltreMetni(filtre.Q);
        var cihazTipi = FiltreMetni(filtre.CihazTipi);
        var tesisatNo = FiltreMetni(filtre.TesisatNo);
        var musteriAdi = FiltreMetni(filtre.MusteriAdi);
        var sozlesmeNo = FiltreMetni(filtre.SozlesmeNo);
        var aboneNo = FiltreMetni(filtre.AboneNo);
        var firma = FiltreMetni(filtre.Firma);
        var il = FiltreMetni(filtre.Il);
        var ilce = FiltreMetni(filtre.Ilce);
        var bolge = FiltreMetni(filtre.Bolge);
        var ekip = FiltreMetni(filtre.Ekip);
        var marka = FiltreMetni(filtre.Marka);
        var hedefUygulama = FiltreMetni(filtre.HedefUygulama);
        var durum = filtre.Durum.GetValueOrDefault() > 0 ? filtre.Durum : null;
        var kontrolNo = filtre.KontrolNo is >= 1 and <= 5 ? filtre.KontrolNo : null;
        var baslangicTarihi = swaggerOrnekFiltre ? null : filtre.BaslangicTarihi;
        var bitisTarihi = swaggerOrnekFiltre ? null : filtre.BitisTarihi;

        if (sirketId.HasValue && genelYetkili)
            query = query.Where(x => x.SirketId == sirketId.Value);

        if (firmaId.HasValue && genelYetkili)
            query = query.Where(x => x.FirmaId == firmaId.Value);

        if (talepId.HasValue)
            query = query.Where(x => x.Id == talepId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var arananTalepId = int.TryParse(q.TrimStart('#'), out var id) && id > 0 ? id : 0;
            query = query.Where(x => x.Id == arananTalepId
                || (x.MusteriAdi != null && EF.Functions.Collate(x.MusteriAdi, "Turkish_CI_AS").Contains(q))
                || (x.TesisatNo != null && EF.Functions.Collate(x.TesisatNo, "Turkish_CI_AS").Contains(q))
                || (x.SozlesmeNo != null && EF.Functions.Collate(x.SozlesmeNo, "Turkish_CI_AS").Contains(q))
                || (x.Firma != null && x.Firma.FirmaAdi != null
                    && EF.Functions.Collate(x.Firma.FirmaAdi, "Turkish_CI_AS").Contains(q)));
        }

        if (!string.IsNullOrWhiteSpace(cihazTipi))
        {
            (string cihazTipiArama, string? cihazTipiAlias) = cihazTipi.ToLowerInvariant() switch
            {
                "sofben" or "\u015fofben" => ("Sofben", "\u015eofben"),
                "diger" or "di\u011fer" => ("Diger", "Di\u011fer"),
                _ => (cihazTipi, null)
            };
            query = query.Where(x => x.YeniCihazTipi != null
                && (EF.Functions.Collate(x.YeniCihazTipi, "Turkish_CI_AS").Contains(cihazTipiArama)
                    || (cihazTipiAlias != null && EF.Functions.Collate(x.YeniCihazTipi, "Turkish_CI_AS").Contains(cihazTipiAlias))));
        }

        if (!string.IsNullOrWhiteSpace(tesisatNo))
            query = query.Where(x => x.TesisatNo != null && x.TesisatNo.Contains(tesisatNo));

        if (!string.IsNullOrWhiteSpace(musteriAdi))
            query = query.Where(x => x.MusteriAdi != null && x.MusteriAdi.Contains(musteriAdi));

        if (!string.IsNullOrWhiteSpace(sozlesmeNo))
            query = query.Where(x => x.SozlesmeNo != null && x.SozlesmeNo.Contains(sozlesmeNo));

        if (!string.IsNullOrWhiteSpace(aboneNo))
            query = query.Where(x => x.AboneNo != null && x.AboneNo.Contains(aboneNo));

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
            var firmaKullanicisi = YkcFirmaSunumu.FirmaKullanicisiMi(kullanici);
            query = query.Where(x =>
                (!firmaKullanicisi && x.EskiMarka != null && x.EskiMarka.Contains(marka)) ||
                (x.YeniMarka != null && x.YeniMarka.Contains(marka)));
        }

        if (!string.IsNullOrWhiteSpace(hedefUygulama))
            query = query.Where(x => x.HedefUygulama == hedefUygulama);

        if (durum.HasValue)
            query = query.Where(x => x.Durum == durum.Value);

        if (kontrolNo.HasValue)
        {
            query = query.Where(x =>
                (kontrolNo == 1 && !x.Kontroller.Any(k => !k.SilindiMi
                    && (k.Sonuc == YkcFr265KontrolSonucDegerleri.Uygun || k.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil)))
                || x.Kontroller.Any(k =>
                    !k.SilindiMi
                    && k.KontrolNo % 5 + 1 == kontrolNo.Value
                    && k.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil
                    && !x.Kontroller.Any(sonraki => !sonraki.SilindiMi && sonraki.KontrolNo > k.KontrolNo
                        && (sonraki.Sonuc == YkcFr265KontrolSonucDegerleri.Uygun
                            || sonraki.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil))));
        }

        if (baslangicTarihi.HasValue)
            query = query.Where(x => x.TalepTarihi >= baslangicTarihi.Value.Date);

        if (bitisTarihi.HasValue)
            query = query.Where(x => x.TalepTarihi < bitisTarihi.Value.Date.AddDays(1));

        if (filtre.RandevuBaslangicTarihi.HasValue)
        {
            var randevuBaslangic = filtre.RandevuBaslangicTarihi.Value.Date;
            query = query.Where(x => x.RandevuTarihi.HasValue && x.RandevuTarihi.Value >= randevuBaslangic);
        }
        if (filtre.RandevuBitisTarihi.HasValue)
        {
            var randevuBitis = filtre.RandevuBitisTarihi.Value.Date.AddDays(1);
            query = query.Where(x => x.RandevuTarihi.HasValue && x.RandevuTarihi.Value < randevuBitis);
        }

        return await BekleyenIsFiltresiAsync(query, filtre.BekleyenIs);
    }

    private static async Task<IQueryable<Ykc_Talep>> BekleyenIsFiltresiAsync(IQueryable<Ykc_Talep> query, string? bekleyenIs)
    {
        if (string.IsNullOrEmpty(bekleyenIs)) return query;
        if (bekleyenIs == YkcBekleyenIsDegerleri.Inceleme)
            return query.Where(x => x.Durum == YkcDurumDegerleri.TalepAlindi);
        if (bekleyenIs == YkcBekleyenIsDegerleri.Randevu)
            return query.Where(x => x.Durum == YkcDurumDegerleri.AtamaBekliyor);
        if (bekleyenIs != YkcBekleyenIsDegerleri.Tamamlama)
            return query.Where(x => false);

        var adaylar = await query.Where(x => x.Durum == YkcDurumDegerleri.SahaIsleminde
                && x.ImzaSurecleri.Any(s => !s.SilindiMi
                    && s.Durum == YkcImzaDurumDegerleri.Tamamlandi
                    && s.ProviderDocumentId != null && s.ProviderDocumentId != ""
                    && s.NihaiDosyaId != null && s.NihaiDosya != null && !s.NihaiDosya.SilindiMi
                    && s.NihaiDosya.DosyaTuru == YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai))
            .Select(x => new { x.Id, x.RandevuTarihi, x.RandevuSaati }).ToListAsync();
        // Reuse the completion guard, including its handling of missing or invalid times.
        var idler = adaylar.Where(x => YkcTalepIslemKurali.RandevuZamaniGeldiMi(x.RandevuTarihi, x.RandevuSaati))
            .Select(x => x.Id).ToArray();
        return query.Where(x => idler.Contains(x.Id));
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
            filtre.Q,
            filtre.CihazTipi,
            filtre.TesisatNo,
            filtre.MusteriAdi,
            filtre.SozlesmeNo,
            filtre.AboneNo,
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
            && filtre.TalepId.GetValueOrDefault() <= 0
            && filtre.Durum.GetValueOrDefault() <= 0
            && filtre.Sayfa <= 0
            && filtre.SayfaBoyutu <= 0;
    }

    private static YkcTalepDto ListeGorunumu(Ykc_Talep talep, AppKullanici kullanici)
    {
        var dto = YkcTalepDto.FromEntity(talep);
        if (YkcFirmaSunumu.FirmaKullanicisiMi(kullanici))
        {
            dto.ProjeNo = null;
            dto.EskiCihaz = null;
            dto.ProjedekiCihazBilgisi = null;
            dto.AtananEkip = null;
            dto.HedefUygulama = null;
        }
        return dto;
    }

    private static YkcRaporKayitDto RaporGorunumu(Ykc_Talep talep, AppKullanici kullanici)
    {
        var dto = YkcRaporKayitDto.FromEntity(talep);
        if (YkcFirmaSunumu.FirmaKullanicisiMi(kullanici))
        {
            dto.ProjeNo = null;
            dto.EskiCihazTipi = null;
            dto.EskiMarka = null;
            dto.EskiKapasite = null;
            dto.EskiBacaTipi = null;
            dto.AtananEkip = null;
            dto.HedefUygulama = null;
        }

        return dto;
    }

    private static bool PlaceholderDegerMi(string? deger)
        => string.Equals(deger?.Trim(), "string", StringComparison.OrdinalIgnoreCase);
}
