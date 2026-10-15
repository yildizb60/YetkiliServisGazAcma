namespace YetkiliServisGazAcma.Business.Services;

public static class YkcTalepIslemKurali
{
    internal static bool RandevuZamaniGeldiMi(DateTime? randevuTarihi, string? randevuSaati)
    {
        if (!randevuTarihi.HasValue || string.IsNullOrWhiteSpace(randevuSaati))
            return false;

        if (!TimeSpan.TryParse(randevuSaati.Trim(), out var saat))
            return false;

        return randevuTarihi.Value.Date.Add(saat) <= DateTime.Now;
    }

    public static readonly TimeSpan GonderimKilidiSuresi = TimeSpan.FromMinutes(5);

    public static YkcTalepEkranDto EkranHazirla(YkcTalepDetayDto detay, YkcYetkiOzeti yetkiler,
        bool icOperasyonGorsun, YkcImzaEntegrasyonDto imzaEntegrasyonu, List<YkcEkipSecenegi> ekipler, DateTime simdi)
    {
        var fr265ImzaYetkisi = icOperasyonGorsun && yetkiler.Fr265ImzaIslemiYapabilir;
        var terminalDurum = detay.Durum == YkcDurumDegerleri.Tamamlandi || detay.Durum == YkcDurumDegerleri.Reddedildi || detay.Durum == YkcDurumDegerleri.Iptal;
        var imzaSureci = detay.ImzaSureci;
        var imzaSureciBasladi = !string.IsNullOrWhiteSpace(imzaSureci?.ProviderDocumentId)
            || imzaSureci?.Durum == YkcImzaDurumDegerleri.ImzayaGonderildi
            || imzaSureci?.Durum == YkcImzaDurumDegerleri.ImzaBekliyor
            || imzaSureci?.Durum == YkcImzaDurumDegerleri.KismiImzali
            || imzaSureci?.Durum == YkcImzaDurumDegerleri.Tamamlandi;
        var atamaYapilabilir = icOperasyonGorsun && yetkiler.AtamaYapabilir && !imzaSureciBasladi
            && (detay.Durum == YkcDurumDegerleri.AtamaBekliyor || detay.Durum == YkcDurumDegerleri.Atandi || detay.Durum == YkcDurumDegerleri.SahaIsleminde);
        var randevuZamani = detay.RandevuTarihi.HasValue && TimeSpan.TryParse(detay.RandevuSaati, out var planlananSaat)
            ? detay.RandevuTarihi.Value.Date.Add(planlananSaat)
            : (DateTime?)null;
        var randevuZamaniGeldi = randevuZamani.HasValue && randevuZamani.Value <= simdi;
        var randevuYenidenPlanlanacak = atamaYapilabilir && randevuZamaniGeldi;
        var tesisatBolgesi = string.IsNullOrWhiteSpace(detay.Bolge) ? detay.Il : detay.Bolge;
        var sonAtamaTipi = detay.Atamalar.FirstOrDefault()?.AtananKullaniciTipi ?? string.Empty;
        var farkliRandevular = detay.Atamalar.DistinctBy(atama => new
        {
            Tarih = atama.RandevuTarihi?.Date,
            Saat = atama.RandevuSaati?.Trim(),
            atama.AtananKullaniciTipi,
            atama.AtananEkip,
            atama.Bolge,
            atama.HedefUygulama
        }).ToList();
        var acilEkipSecili = string.Equals(detay.HedefUygulama, YkcHedefUygulamaDegerleri.Crm187, StringComparison.OrdinalIgnoreCase)
            || sonAtamaTipi.Contains("187", StringComparison.OrdinalIgnoreCase)
            || (detay.AtananEkip?.Contains("187", StringComparison.OrdinalIgnoreCase) ?? false);
        var muhendisEkipSecili = !acilEkipSecili
            && (string.Equals(detay.HedefUygulama, YkcHedefUygulamaDegerleri.DogalgazMobileApp, StringComparison.OrdinalIgnoreCase)
                || sonAtamaTipi.Contains("Mühendis", StringComparison.OrdinalIgnoreCase)
                || (detay.AtananEkip?.Contains("Mühendis", StringComparison.OrdinalIgnoreCase) ?? false));
        var mevcutEkipler = ekipler.Where(x => x.Secili).ToList();
        var seciliEkipId = icOperasyonGorsun && yetkiler.AtamaYapabilir && mevcutEkipler.Count == 1 ? mevcutEkipler[0].Id : null;
        var imzaliBelge = imzaSureci?.NihaiDosyaId is int nihaiDosyaId
            ? detay.Dosyalar.FirstOrDefault(x => x.Id == nihaiDosyaId && x.DosyaTuru == YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai)
            : null;
        var imzaliBelgeHazir = imzaliBelge != null
            && !string.IsNullOrWhiteSpace(imzaSureci?.ProviderDocumentId)
            && string.Equals(imzaSureci?.Durum, YkcImzaDurumDegerleri.Tamamlandi, StringComparison.OrdinalIgnoreCase);
        var gonderimTakildi = imzaSureci?.Durum == YkcImzaDurumDegerleri.ImzayaGonderildi
            && string.IsNullOrWhiteSpace(imzaSureci.ProviderDocumentId)
            && (!imzaSureci.GonderimTarihi.HasValue || imzaSureci.GonderimTarihi < simdi.Subtract(GonderimKilidiSuresi));
        var imzaliBelgeDemoMu = imzaliBelgeHazir
            && imzaSureci?.ProviderDocumentId?.StartsWith("DEMO-YKC-", StringComparison.Ordinal) == true;
        var sonKontrolKayitlari = detay.AktifKontroller;
        var donemBaslangici = (detay.KontrolDonemi - 1) * 5 + 1;
        var kontrolSatirlari = Enumerable.Range(donemBaslangici, 5)
            .Select(no => sonKontrolKayitlari.FirstOrDefault(x => x.KontrolNo == no) ?? new YkcFr265KontrolDto { KontrolNo = no })
            .ToList();
        var sonucGirilenKontroller = kontrolSatirlari
            .Where(x => x.Sonuc == YkcFr265KontrolSonucDegerleri.Uygun || x.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil)
            .OrderBy(x => x.KontrolNo)
            .ToList();
        var sonKontrol = sonucGirilenKontroller.LastOrDefault();
        var kontrollerImzayaHazir = sonKontrol?.Sonuc == YkcFr265KontrolSonucDegerleri.Uygun;
        var kontrolAlaniDoldu = sonucGirilenKontroller.Count == 5
            && sonucGirilenKontroller.All(x => x.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil);
        var sonKontrolGecmisiId = detay.Gecmis
            .Where(x => x.IslemTipi == "FR265KontrolleriGuncellendi")
            .OrderByDescending(x => x.OlusturmaTarihi)
            .ThenByDescending(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
        var siradakiKontrolNo = (sonKontrol?.FormKontrolNo ?? 0) + 1;
        var aktifKontrolNo = kontrollerImzayaHazir || siradakiKontrolNo > 5 ? (int?)null : siradakiKontrolNo;
        var aktifKontrol = aktifKontrolNo.HasValue
            ? kontrolSatirlari.FirstOrDefault(x => x.FormKontrolNo == aktifKontrolNo.Value)
            : null;
        var tekrarKontrolBekleniyor = sonKontrol?.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil && aktifKontrolNo.HasValue;
        var tekrarRandevuBekleniyor = tekrarKontrolBekleniyor
            && detay.Durum == YkcDurumDegerleri.AtamaBekliyor;
        var talepImzayaHazir = ImzaGonderimineHazirMi(detay, simdi, out _);
        var imzayaGonderebilir = fr265ImzaYetkisi
            && imzaEntegrasyonu.KullanilabilirMi
            && talepImzayaHazir
            && string.IsNullOrWhiteSpace(imzaSureci?.ProviderDocumentId)
            && (imzaSureci?.Durum is null or YkcImzaDurumDegerleri.Hazir or YkcImzaDurumDegerleri.Hata || gonderimTakildi);
        var imzaDurumuSorgulanabilir = fr265ImzaYetkisi
            && imzaEntegrasyonu.KullanilabilirMi
            && !string.IsNullOrWhiteSpace(imzaSureci?.ProviderDocumentId)
            && !imzaliBelgeHazir;
        var kontrolBolumuAktif = icOperasyonGorsun
            && fr265ImzaYetkisi
            && randevuZamaniGeldi
            && detay.Durum == YkcDurumDegerleri.SahaIsleminde
            && !kontrollerImzayaHazir
            && aktifKontrol != null
            && !imzaliBelgeHazir
            && !imzaDurumuSorgulanabilir;
        var tamamlamayaHazir = randevuZamaniGeldi && detay.Durum == YkcDurumDegerleri.SahaIsleminde
            && imzaliBelgeHazir
            && fr265ImzaYetkisi;
        var imzaBirincilIslemVar = imzaliBelge != null || imzayaGonderebilir || imzaDurumuSorgulanabilir;
        var imzaBolumuGorsun = !icOperasyonGorsun
            || imzaliBelgeHazir
            || imzaDurumuSorgulanabilir
            || imzayaGonderebilir
            || kontrollerImzayaHazir
            || detay.Durum == YkcDurumDegerleri.Tamamlandi;
        var imzaBolumuAktif = imzaBolumuGorsun
            && (imzayaGonderebilir || imzaDurumuSorgulanabilir);
        var indirilebilirDosyalar = detay.Dosyalar
            .Where(x => x.DosyaTuru == YkcFormDosyaTuruDegerleri.TeknikEk
                || (x.DosyaTuru == YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai && imzaliBelgeHazir && imzaliBelge != null && x.Id == imzaliBelge.Id))
            .ToList();
        var cihazUyarilari = icOperasyonGorsun ? YkcCihazUyumKurali.Uyarilar(
            detay.EskiCihazTipi,
            detay.YeniCihazTipi,
            detay.EskiMarka,
            detay.YeniMarka,
            detay.EskiBacaTipi,
            detay.YeniBacaTipi,
            detay.EskiKapasite,
            detay.YeniKapasite).ToList() : new List<string>();
        return new YkcTalepEkranDto
        {
            TerminalDurum = terminalDurum,
            ImzaSureciBasladi = imzaSureciBasladi,
            AtamaYapilabilir = atamaYapilabilir,
            RandevuZamaniGeldi = randevuZamaniGeldi,
            RandevuYenidenPlanlanacak = randevuYenidenPlanlanacak,
            TesisatBolgesi = tesisatBolgesi,
            FarkliRandevular = farkliRandevular,
            AcilEkipSecili = acilEkipSecili,
            MuhendisEkipSecili = muhendisEkipSecili,
            SeciliEkipId = seciliEkipId,
            ImzaliBelge = imzaliBelge,
            ImzaliBelgeHazir = imzaliBelgeHazir,
            ImzaliBelgeDemoMu = imzaliBelgeDemoMu,
            DemoPdfGuncellenebilir = imzaliBelgeDemoMu && imzaEntegrasyonu.DemoModuMu && fr265ImzaYetkisi
                && imzaliBelge!.DosyaAdi?.Contains(YkcFr265PdfService.TasarimSurumu, StringComparison.Ordinal) != true,
            SonucGirilenKontroller = sonucGirilenKontroller,
            TumSonucluKontroller = detay.Kontroller.Where(x => x.Sonuc is YkcFr265KontrolSonucDegerleri.Uygun
                or YkcFr265KontrolSonucDegerleri.UygunDegil).OrderBy(x => x.KontrolNo).ToList(),
            SonKontrol = sonKontrol,
            SonUygunsuzKontrol = SonUygunsuzKontrol(detay),
            KontrollerImzayaHazir = kontrollerImzayaHazir,
            KontrolAlaniDoldu = kontrolAlaniDoldu,
            SonKontrolGecmisiId = sonKontrolGecmisiId,
            AktifKontrolNo = aktifKontrolNo,
            AktifKontrol = aktifKontrol,
            TekrarKontrolBekleniyor = tekrarKontrolBekleniyor,
            TekrarRandevuBekleniyor = tekrarRandevuBekleniyor,
            ImzayaGonderebilir = imzayaGonderebilir,
            ImzaDurumuSorgulanabilir = imzaDurumuSorgulanabilir,
            KontrolBolumuAktif = kontrolBolumuAktif,
            TamamlamayaHazir = tamamlamayaHazir,
            ImzaBirincilIslemVar = imzaBirincilIslemVar,
            ImzaBolumuGorsun = imzaBolumuGorsun,
            ImzaBolumuAktif = imzaBolumuAktif,
            IndirilebilirDosyalar = indirilebilirDosyalar,
            CihazUyarilari = cihazUyarilari,
            Bugun = simdi.Date,
            RandevuZamaniUnixMs = randevuZamani.HasValue ? new DateTimeOffset(randevuZamani.Value).ToUnixTimeMilliseconds() : null,
            KontrolDonemi = detay.KontrolDonemi,
            IcOperasyonGorsun = icOperasyonGorsun,
            IncelemeyeAlabilir = icOperasyonGorsun && yetkiler.AtamaYapabilir && detay.Durum == YkcDurumDegerleri.TalepAlindi,
            KontroleGecisGorsun = icOperasyonGorsun && (yetkiler.AtamaYapabilir || fr265ImzaYetkisi) && detay.Durum == YkcDurumDegerleri.Atandi,
            RedIptalYapabilir = icOperasyonGorsun && yetkiler.AtamaYapabilir && !terminalDurum,
            Yetkiler = yetkiler,
            ImzaEntegrasyonu = imzaEntegrasyonu,
            Ekipler = icOperasyonGorsun && yetkiler.AtamaYapabilir ? ekipler : new()
        };
    }

    public static YkcFr265KontrolDto? SonUygunsuzKontrol(YkcTalepDetayDto talep)
    {
        if (talep.Durum is not (YkcDurumDegerleri.AtamaBekliyor or YkcDurumDegerleri.Atandi or YkcDurumDegerleri.SahaIsleminde))
            return null;

        var sonKontrol = talep.Kontroller
            .Where(x => x.Sonuc is YkcFr265KontrolSonucDegerleri.Uygun or YkcFr265KontrolSonucDegerleri.UygunDegil)
            .OrderByDescending(x => x.KontrolNo).FirstOrDefault();
        return sonKontrol?.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil ? sonKontrol : null;
    }

    public static bool ImzaGonderimineHazirMi(YkcTalepDetayDto detay, DateTime simdi, out string mesaj)
    {
        if (detay.Durum != YkcDurumDegerleri.SahaIsleminde)
        {
            mesaj = "Form yalnız randevu gerçekleşip kontrol aşamasına geçtikten sonra imzaya gönderilebilir.";
            return false;
        }

        if (!detay.RandevuTarihi.HasValue || string.IsNullOrWhiteSpace(detay.RandevuSaati))
        {
            mesaj = "Form imzaya gönderilmeden önce randevu tarih ve saat bilgisi kaydedilmelidir.";
            return false;
        }

        var sonKontrol = detay.AktifKontroller
            .Where(x => x.Sonuc == YkcFr265KontrolSonucDegerleri.Uygun
                || x.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil)
            .OrderByDescending(x => x.KontrolNo)
            .ThenByDescending(x => x.KontrolTarihi ?? DateTime.MinValue)
            .FirstOrDefault();

        if (sonKontrol == null)
        {
            mesaj = "Form imzaya gönderilmeden önce randevu sonrası en az bir kontrol sonucu girilmelidir.";
            return false;
        }

        if (!TimeSpan.TryParse(detay.RandevuSaati, out var saat)
            || detay.RandevuTarihi.Value.Date.Add(saat) > simdi
            || detay.AktifAtamaId == null || sonKontrol.AtamaId != detay.AktifAtamaId)
        {
            mesaj = "Form için güncel randevunun gerçekleşmesi ve bu randevuya ait kontrol sonucunun kaydedilmesi gerekir.";
            return false;
        }

        if (sonKontrol.Sonuc != YkcFr265KontrolSonucDegerleri.Uygun)
        {
            mesaj = "Son kontrol uygun değil. Firma eksikliği giderdikten sonra bir sonraki kontrol sonucu uygun olmalıdır.";
            return false;
        }

        mesaj = "";
        return true;
    }
}
