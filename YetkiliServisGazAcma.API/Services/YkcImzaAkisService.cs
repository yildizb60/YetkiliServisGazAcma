using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Security.Cryptography;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services
{
    public sealed class YkcImzaAkisService
    {
        private static readonly TimeSpan GonderimKilidiSuresi = TimeSpan.FromMinutes(5);
        private readonly AppDbContext _context;
        private readonly YkcTalepService _talepService;
        private readonly YkcFr265FormService _fr265FormService;
        private readonly IYkcImzaProvider _imzaProvider;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<YkcImzaAkisService> _logger;

        public YkcImzaAkisService(
            AppDbContext context,
            YkcTalepService talepService,
            YkcFr265FormService fr265FormService,
            IYkcImzaProvider imzaProvider,
            IWebHostEnvironment environment,
            ILogger<YkcImzaAkisService> logger)
        {
            _context = context;
            _talepService = talepService;
            _fr265FormService = fr265FormService;
            _imzaProvider = imzaProvider;
            _environment = environment;
            _logger = logger;
        }

        public YkcImzaEntegrasyonDto EntegrasyonBilgisi()
        {
            return new YkcImzaEntegrasyonDto
            {
                KullanilabilirMi = _imzaProvider.KullanilabilirMi,
                DemoModuMu = _imzaProvider.DemoModuMu,
                ProviderAdi = _imzaProvider.ProviderAdi
            };
        }

        public async Task<YkcIslemSonuc> ImzayaGonderAsync(
            int talepId,
            AppKullanici kullanici,
            bool genelYetkili,
            CancellationToken cancellationToken = default)
        {
            if (!_imzaProvider.KullanilabilirMi)
                return YkcIslemSonuc.HataliSonuc("Dijital imza sağlayıcısı henüz yapılandırılmadı; belge gönderilmedi.");

            var detay = await _talepService.GetirAsync(talepId, kullanici, genelYetkili);
            if (detay == null)
                return YkcIslemSonuc.HataliSonuc("Cihaz değişim talebi bulunamadı.");

            if (TerminalDurumMu(detay.Durum))
                return YkcIslemSonuc.HataliSonuc("Kapanmış talep dijital imzaya gönderilemez.");

            if (!ImzaGonderimineHazirMi(detay, out var hazirlikMesaji))
                return YkcIslemSonuc.HataliSonuc(hazirlikMesaji);

            var talep = await _context.Ykc_Talepler
                .Include(x => x.FormDosyalari)
                .Include(x => x.ImzaSurecleri)
                    .ThenInclude(x => x.Imzacilar)
                .FirstOrDefaultAsync(x => x.Id == talepId && !x.SilindiMi, cancellationToken);

            if (talep == null)
                return YkcIslemSonuc.HataliSonuc("Cihaz değişim talebi bulunamadı.");

            var surec = AktifSurec(talep) ?? YeniSurec(talep, detay, kullanici);
            var gonderimKilidiAktif = surec.Durum == YkcImzaDurumDegerleri.ImzayaGonderildi
                && string.IsNullOrWhiteSpace(surec.ProviderDocumentId)
                && surec.GonderimTarihi >= DateTime.Now.Subtract(GonderimKilidiSuresi);
            if (!string.IsNullOrWhiteSpace(surec.ProviderDocumentId)
                || gonderimKilidiAktif
                || surec.Durum is YkcImzaDurumDegerleri.ImzaBekliyor
                    or YkcImzaDurumDegerleri.KismiImzali
                    or YkcImzaDurumDegerleri.Tamamlandi)
            {
                return YkcIslemSonuc.HataliSonuc("FR265 daha önce imza uygulamasına gönderilmiş. Güncel durumu sorgulayın.");
            }

            ImzaciListesiniTamamla(surec, detay, kullanici.UserName);

            var taslak = await GecerliTaslakGetirAsync(talep, surec, cancellationToken);
            byte[] belgeBytes;

            if (taslak == null)
            {
                var belge = _fr265FormService.WordOlustur(detay);
                belgeBytes = belge.Bytes;
                var belgeHash = HashOlustur(belgeBytes);
                var kayit = await PrivateBelgeKaydetAsync(
                    talep.Id,
                    belge.DosyaAdi,
                    belge.ContentType,
                    belgeBytes,
                    cancellationToken);

                taslak = new Ykc_FormDosya
                {
                    TalepId = talep.Id,
                    DosyaTuru = YkcFormDosyaTuruDegerleri.Fr265Taslak,
                    DosyaAdi = belge.DosyaAdi,
                    DosyaYolu = kayit.DepolamaAnahtari,
                    IcerikTipi = belge.ContentType,
                    DosyaBoyutu = belgeBytes.LongLength,
                    DepolamaTuru = YkcDepolamaTuruDegerleri.Private,
                    BelgeHash = belgeHash,
                    OlusturmaTarihi = kayit.KayitTarihi,
                    OlusturanKullanici = kullanici.UserName
                };

                _context.Ykc_FormDosyalari.Add(taslak);
                talep.FormDosyalari.Add(taslak);
                talep.Fr265BelgeHash = belgeHash;
                talep.Fr265BelgeOlusturmaTarihi = kayit.KayitTarihi;
                surec.BelgeHash = belgeHash;
                surec.BelgeOlusturmaTarihi = kayit.KayitTarihi;
                surec.BelgeVersiyonu = Math.Max(talep.Fr265BelgeVersiyonNo, 1);
                surec.Durum = YkcImzaDurumDegerleri.Hazir;
                surec.HataKodu = null;
                surec.HataMesaji = null;

                GecmisEkle(talep, kullanici, "FR265TaslakOlusturuldu", $"FR265 sürüm {surec.BelgeVersiyonu} gerçek belge snapshot'ı oluşturuldu.");
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                belgeBytes = await PrivateBelgeOkuAsync(taslak, cancellationToken)
                    ?? throw new InvalidOperationException("Kayıtlı FR265 taslak dosyası okunamadı.");
            }

            await _context.SaveChangesAsync(cancellationToken);
            if (!await GonderimiSahiplenAsync(surec, kullanici.UserName, cancellationToken))
                return YkcIslemSonuc.HataliSonuc("FR265 için başka bir gönderim işlemi devam ediyor.");

            YkcImzaGonderSonuc providerSonucu;
            try
            {
                providerSonucu = await _imzaProvider.GonderAsync(new YkcImzaGonderIstek
                {
                    TalepId = talep.Id,
                    BelgeVersiyonu = surec.BelgeVersiyonu,
                    BelgeAdi = taslak.DosyaAdi ?? $"FR265_{talep.Id}.docx",
                    IcerikTipi = taslak.IcerikTipi ?? "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    BelgeBytes = belgeBytes,
                    BelgeHash = taslak.BelgeHash ?? HashOlustur(belgeBytes),
                    TekrarsizIstekAnahtari = TekrarsizIstekAnahtari(talep.Id, surec.BelgeVersiyonu, taslak.BelgeHash),
                    Imzacilar = surec.Imzacilar
                        .Where(x => !x.SilindiMi)
                        .OrderBy(x => x.SiraNo)
                        .Select(x => new YkcImzaProviderImzaci
                        {
                            SiraNo = x.SiraNo,
                            Rol = x.Rol,
                            AdSoyad = x.AdSoyad,
                            KullaniciId = x.KullaniciId
                        })
                        .ToList()
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "YKC FR265 imza sağlayıcısına gönderilemedi. TalepId: {TalepId}", talep.Id);
                providerSonucu = YkcImzaGonderSonuc.Basarisiz("PROVIDER_HATASI", "Dijital imza sağlayıcısına ulaşılamadı.");
            }

            surec.SonKontrolTarihi = DateTime.Now;
            if (!providerSonucu.Basarili || string.IsNullOrWhiteSpace(providerSonucu.ProviderDocumentId))
            {
                surec.Durum = YkcImzaDurumDegerleri.Hata;
                surec.HataKodu = providerSonucu.HataKodu ?? "GONDERIM_BASARISIZ";
                surec.HataMesaji = providerSonucu.HataMesaji ?? "Dijital imza sağlayıcısı belgeyi kabul etmedi.";
                surec.GuncellemeTarihi = DateTime.Now;
                surec.GuncelleyenKullanici = kullanici.UserName;
                GecmisEkle(talep, kullanici, "FR265ImzaGonderimHatasi", surec.HataMesaji);
                await _context.SaveChangesAsync(cancellationToken);
                return YkcIslemSonuc.HataliSonuc(surec.HataMesaji);
            }

            taslak.DosyaTuru = YkcFormDosyaTuruDegerleri.Fr265ImzayaGonderilen;
            taslak.GuncellemeTarihi = DateTime.Now;
            taslak.GuncelleyenKullanici = kullanici.UserName;
            surec.ProviderDocumentId = providerSonucu.ProviderDocumentId.Trim();
            surec.Durum = YkcImzaDurumDegerleri.ImzaBekliyor;
            surec.HataKodu = null;
            surec.HataMesaji = null;
            surec.GuncellemeTarihi = DateTime.Now;
            surec.GuncelleyenKullanici = kullanici.UserName;
            GecmisEkle(talep, kullanici, "FR265ImzayaGonderildi", $"FR265 sürüm {surec.BelgeVersiyonu} dijital imza sağlayıcısına gönderildi.");
            await _context.SaveChangesAsync(cancellationToken);

            return YkcIslemSonuc.BasariliSonuc("FR265 dijital imza uygulamasına gönderildi.", talep.Id);
        }

        public async Task<YkcIslemSonuc> ImzaDurumunuSorgulaAsync(
            int talepId,
            AppKullanici kullanici,
            bool genelYetkili,
            CancellationToken cancellationToken = default)
        {
            if (!_imzaProvider.KullanilabilirMi)
                return YkcIslemSonuc.HataliSonuc("Dijital imza sağlayıcısı henüz yapılandırılmadı.");

            var detay = await _talepService.GetirAsync(talepId, kullanici, genelYetkili);
            if (detay == null)
                return YkcIslemSonuc.HataliSonuc("Cihaz değişim talebi bulunamadı.");

            var talep = await _context.Ykc_Talepler
                .Include(x => x.FormDosyalari)
                .Include(x => x.ImzaSurecleri)
                    .ThenInclude(x => x.Imzacilar)
                .FirstOrDefaultAsync(x => x.Id == talepId && !x.SilindiMi, cancellationToken);

            var surec = talep == null ? null : AktifSurec(talep);
            if (talep == null || surec == null || string.IsNullOrWhiteSpace(surec.ProviderDocumentId))
                return YkcIslemSonuc.HataliSonuc("İmza uygulamasına gönderilmiş bir FR265 belgesi bulunamadı.");

            if (ImzaliNihaiBelgeHazirMi(surec, talep.FormDosyalari))
            {
                if (_imzaProvider.DemoModuMu)
                {
                    var mevcutNihaiDosya = talep.FormDosyalari.FirstOrDefault(x =>
                        x.Id == surec.NihaiDosyaId
                        && !x.SilindiMi
                        && x.DosyaTuru == YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai);

                    if (mevcutNihaiDosya != null
                        && await DemoNihaiBelgeyiYenileGerekiyorsaAsync(detay, talep, surec, mevcutNihaiDosya, kullanici, cancellationToken))
                    {
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }

                return YkcIslemSonuc.BasariliSonuc("İmzalı nihai belge zaten hazır.", talep.Id);
            }

            YkcImzaDurumSonuc providerSonucu;
            try
            {
                providerSonucu = await _imzaProvider.DurumSorgulaAsync(surec.ProviderDocumentId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "YKC imza durumu sorgulanamadı. TalepId: {TalepId}", talep.Id);
                providerSonucu = YkcImzaDurumSonuc.Basarisiz("PROVIDER_HATASI", "Dijital imza sağlayıcısına ulaşılamadı.");
            }

            surec.SonKontrolTarihi = DateTime.Now;
            surec.GuncellemeTarihi = DateTime.Now;
            surec.GuncelleyenKullanici = kullanici.UserName;

            if (!providerSonucu.Basarili)
            {
                surec.HataKodu = providerSonucu.HataKodu;
                surec.HataMesaji = providerSonucu.HataMesaji;
                await _context.SaveChangesAsync(cancellationToken);
                return YkcIslemSonuc.HataliSonuc(providerSonucu.HataMesaji ?? "İmza durumu alınamadı.");
            }

            ImzaciDurumlariniUygula(surec, providerSonucu.Imzacilar, kullanici.UserName);
            var yeniDurum = GecerliImzaDurumu(providerSonucu.Durum);

            if (yeniDurum == YkcImzaDurumDegerleri.Tamamlandi)
            {
                var nihaiBelgeBytes = providerSonucu.NihaiBelgeBytes;
                var nihaiBelgeAdi = providerSonucu.NihaiBelgeAdi;
                var nihaiIcerikTipi = providerSonucu.NihaiBelgeIcerikTipi;

                if ((nihaiBelgeBytes == null || nihaiBelgeBytes.Length == 0) && _imzaProvider.DemoModuMu)
                {
                    var demoBelge = DemoImzaliNihaiBelgeOlustur(detay, surec);
                    nihaiBelgeBytes = demoBelge.Bytes;
                    nihaiBelgeAdi = $"FR265_Imzali_Nihai_{talep.Id}.docx";
                    nihaiIcerikTipi = demoBelge.ContentType;
                }

                if (nihaiBelgeBytes == null || nihaiBelgeBytes.Length == 0)
                {
                    surec.Durum = YkcImzaDurumDegerleri.ImzaBekliyor;
                    surec.HataKodu = "NIHAI_BELGE_YOK";
                    surec.HataMesaji = "Sağlayıcı süreci tamamlandı bildirdi ancak imzalı nihai belgeyi döndürmedi.";
                    await _context.SaveChangesAsync(cancellationToken);
                    return YkcIslemSonuc.HataliSonuc(surec.HataMesaji);
                }

                nihaiBelgeAdi = GuvenliDosyaAdi(nihaiBelgeAdi ?? $"FR265_Imzali_{talep.Id}.pdf");
                nihaiIcerikTipi = string.IsNullOrWhiteSpace(nihaiIcerikTipi)
                    ? "application/pdf"
                    : nihaiIcerikTipi.Trim();
                var nihaiHash = HashOlustur(nihaiBelgeBytes);
                var kayit = await PrivateBelgeKaydetAsync(
                    talep.Id,
                    nihaiBelgeAdi,
                    nihaiIcerikTipi,
                    nihaiBelgeBytes,
                    cancellationToken);

                var nihaiDosya = new Ykc_FormDosya
                {
                    TalepId = talep.Id,
                    DosyaTuru = YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai,
                    DosyaAdi = nihaiBelgeAdi,
                    DosyaYolu = kayit.DepolamaAnahtari,
                    IcerikTipi = nihaiIcerikTipi,
                    DosyaBoyutu = nihaiBelgeBytes.LongLength,
                    DepolamaTuru = YkcDepolamaTuruDegerleri.Private,
                    BelgeHash = nihaiHash,
                    OlusturmaTarihi = kayit.KayitTarihi,
                    OlusturanKullanici = kullanici.UserName
                };

                _context.Ykc_FormDosyalari.Add(nihaiDosya);
                surec.NihaiDosya = nihaiDosya;
                surec.Durum = YkcImzaDurumDegerleri.Tamamlandi;
                surec.TamamlanmaTarihi = DateTime.Now;
                surec.HataKodu = null;
                surec.HataMesaji = null;
                GecmisEkle(talep, kullanici, "FR265ImzaliNihaiBelgeAlindi", "İmzalı nihai FR265 belgesi dijital imza sağlayıcısından alındı.");
            }
            else
            {
                surec.Durum = yeniDurum;
                surec.HataKodu = providerSonucu.HataKodu;
                surec.HataMesaji = providerSonucu.HataMesaji;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return YkcIslemSonuc.BasariliSonuc("Dijital imza durumu güncellendi.", talep.Id);
        }

        public async Task<bool> DemoNihaiBelgeyiYenileAsync(
            int dosyaId,
            AppKullanici kullanici,
            bool genelYetkili,
            CancellationToken cancellationToken = default)
        {
            if (!_imzaProvider.DemoModuMu)
                return false;

            var dosya = await _context.Ykc_FormDosyalari
                .Include(x => x.Talep)
                    .ThenInclude(x => x!.FormDosyalari)
                .Include(x => x.Talep)
                    .ThenInclude(x => x!.ImzaSurecleri)
                        .ThenInclude(x => x.Imzacilar)
                .FirstOrDefaultAsync(x =>
                    x.Id == dosyaId
                    && !x.SilindiMi
                    && x.DosyaTuru == YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai
                    && x.Talep != null
                    && !x.Talep.SilindiMi,
                    cancellationToken);

            if (dosya?.Talep == null)
                return false;

            var detay = await _talepService.GetirAsync(dosya.TalepId, kullanici, genelYetkili);
            if (detay == null)
                return false;

            var surec = AktifSurec(dosya.Talep);
            if (surec == null
                || surec.Durum != YkcImzaDurumDegerleri.Tamamlandi
                || surec.NihaiDosyaId != dosya.Id)
            {
                return false;
            }

            var yenilendi = await DemoNihaiBelgeyiYenileGerekiyorsaAsync(detay, dosya.Talep, surec, dosya, kullanici, cancellationToken);
            if (yenilendi)
                await _context.SaveChangesAsync(cancellationToken);

            return yenilendi;
        }

        private async Task<Ykc_FormDosya?> GecerliTaslakGetirAsync(
            Ykc_Talep talep,
            Ykc_ImzaSureci surec,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(surec.BelgeHash)
                || surec.BelgeVersiyonu != Math.Max(talep.Fr265BelgeVersiyonNo, 1))
            {
                return null;
            }

            var taslaklar = talep.FormDosyalari
                .Where(x => !x.SilindiMi
                    && x.DosyaTuru == YkcFormDosyaTuruDegerleri.Fr265Taslak
                    && string.Equals(x.BelgeHash, surec.BelgeHash, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.OlusturmaTarihi)
                .ThenByDescending(x => x.Id)
                .ToList();

            foreach (var taslak in taslaklar)
            {
                if (await PrivateBelgeOkuAsync(taslak, cancellationToken) != null)
                    return taslak;
            }

            return null;
        }

        private async Task<bool> DemoNihaiBelgeyiYenileGerekiyorsaAsync(
            YkcTalepDetayDto detay,
            Ykc_Talep talep,
            Ykc_ImzaSureci surec,
            Ykc_FormDosya nihaiDosya,
            AppKullanici kullanici,
            CancellationToken cancellationToken)
        {
            var mevcutBytes = await PrivateBelgeOkuAsync(nihaiDosya, cancellationToken);
            if (mevcutBytes is { Length: > 0 }
                && DocxMetniIcerir(mevcutBytes, "Dijital imza kaydı alındı"))
            {
                return false;
            }

            var belge = DemoImzaliNihaiBelgeOlustur(detay, surec);
            var belgeHash = HashOlustur(belge.Bytes);
            var kayit = await PrivateBelgeKaydetAsync(
                talep.Id,
                $"FR265_Imzali_Nihai_{talep.Id}.docx",
                belge.ContentType,
                belge.Bytes,
                cancellationToken);

            nihaiDosya.DosyaAdi = $"FR265_Imzali_Nihai_{talep.Id}.docx";
            nihaiDosya.DosyaYolu = kayit.DepolamaAnahtari;
            nihaiDosya.IcerikTipi = belge.ContentType;
            nihaiDosya.DosyaBoyutu = belge.Bytes.LongLength;
            nihaiDosya.DepolamaTuru = YkcDepolamaTuruDegerleri.Private;
            nihaiDosya.BelgeHash = belgeHash;
            nihaiDosya.GuncellemeTarihi = DateTime.Now;
            nihaiDosya.GuncelleyenKullanici = kullanici.UserName;

            surec.NihaiDosya = nihaiDosya;
            surec.NihaiDosyaId = nihaiDosya.Id;
            surec.Durum = YkcImzaDurumDegerleri.Tamamlandi;
            surec.TamamlanmaTarihi ??= DateTime.Now;
            surec.GuncellemeTarihi = DateTime.Now;
            surec.GuncelleyenKullanici = kullanici.UserName;

            GecmisEkle(talep, kullanici, "FR265DemoNihaiBelgeYenilendi", "Nihai FR265 belge kopyasına dijital imza kayıt bilgisi işlendi.");
            return true;
        }

        private YkcFr265BelgeSonuc DemoImzaliNihaiBelgeOlustur(YkcTalepDetayDto detay, Ykc_ImzaSureci surec)
        {
            var varsayilanImzaTarihi = surec.Imzacilar
                .Where(x => !x.SilindiMi && x.ImzaTarihi.HasValue)
                .Select(x => x.ImzaTarihi)
                .Max() ?? surec.TamamlanmaTarihi ?? DateTime.Now;

            return _fr265FormService.WordOlustur(detay, new YkcFr265BelgeSecenekleri
            {
                ImzaliNihaiMi = true,
                ImzaTarihi = varsayilanImzaTarihi,
                Imzalar = surec.Imzacilar
                    .Where(x => !x.SilindiMi)
                    .OrderBy(x => x.SiraNo)
                    .Select(x => new YkcFr265ImzaSatiri
                    {
                        SiraNo = x.SiraNo,
                        Rol = x.Rol,
                        AdSoyad = x.AdSoyad,
                        ImzaTarihi = x.ImzaTarihi ?? varsayilanImzaTarihi
                    })
                    .ToList()
            });
        }

        private static bool DocxMetniIcerir(byte[] bytes, string arananMetin)
        {
            try
            {
                using var stream = new MemoryStream(bytes);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                var documentEntry = archive.GetEntry("word/document.xml");
                if (documentEntry == null)
                    return false;

                using var reader = new StreamReader(documentEntry.Open());
                var xml = reader.ReadToEnd();
                return xml.Contains(arananMetin, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private async Task<SaklananBelge> PrivateBelgeKaydetAsync(
            int talepId,
            string dosyaAdi,
            string icerikTipi,
            byte[] bytes,
            CancellationToken cancellationToken)
        {
            var klasor = Path.Combine(PrivateBelgeKoku(), talepId.ToString());
            Directory.CreateDirectory(klasor);

            var guvenliAd = GuvenliDosyaAdi(dosyaAdi);
            var kayitAdi = $"{Guid.NewGuid():N}_{guvenliAd}";
            var fizikselYol = Path.Combine(klasor, kayitAdi);
            await File.WriteAllBytesAsync(fizikselYol, bytes, cancellationToken);

            return new SaklananBelge
            {
                DepolamaAnahtari = $"ykc/{talepId}/{kayitAdi}",
                KayitTarihi = DateTime.Now
            };
        }

        private async Task<byte[]?> PrivateBelgeOkuAsync(Ykc_FormDosya dosya, CancellationToken cancellationToken)
        {
            if (!string.Equals(dosya.DepolamaTuru, YkcDepolamaTuruDegerleri.Private, StringComparison.OrdinalIgnoreCase))
                return null;

            var yol = dosya.DosyaYolu?.Trim().Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(yol))
                return null;

            if (yol.StartsWith("ykc/", StringComparison.OrdinalIgnoreCase))
                yol = yol["ykc/".Length..];

            var kok = Path.GetFullPath(PrivateBelgeKoku());
            var fizikselYol = Path.GetFullPath(Path.Combine(kok, yol.Replace('/', Path.DirectorySeparatorChar)));
            if (!fizikselYol.StartsWith(kok + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(fizikselYol))
            {
                return null;
            }

            return await File.ReadAllBytesAsync(fizikselYol, cancellationToken);
        }

        private string PrivateBelgeKoku()
        {
            return Path.Combine(_environment.ContentRootPath, "App_Data", "ykc-belgeler");
        }

        private static Ykc_ImzaSureci? AktifSurec(Ykc_Talep talep)
        {
            return talep.ImzaSurecleri
                .Where(x => !x.SilindiMi)
                .OrderByDescending(x => x.BelgeVersiyonu)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();
        }

        private Ykc_ImzaSureci YeniSurec(Ykc_Talep talep, YkcTalepDetayDto detay, AppKullanici kullanici)
        {
            var surec = new Ykc_ImzaSureci
            {
                TalepId = talep.Id,
                BelgeVersiyonu = Math.Max(talep.Fr265BelgeVersiyonNo, 1),
                Durum = YkcImzaDurumDegerleri.Hazir,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName,
                Imzacilar = new List<Ykc_Imzaci>
                {
                    YeniImzaci("Sertifikalı Firma Yetkilisi", 1, detay.FirmaYetkiliKisi, kullanici.UserName),
                    YeniImzaci("Dağıtım Şirketi Yetkilisi", 2, null, kullanici.UserName),
                    YeniImzaci("Abone / Kullanıcı", 3, detay.MusteriAdi, kullanici.UserName)
                }
            };

            _context.Ykc_ImzaSurecleri.Add(surec);
            talep.ImzaSurecleri.Add(surec);
            return surec;
        }

        private async Task<bool> GonderimiSahiplenAsync(
            Ykc_ImzaSureci surec,
            string? kullaniciAdi,
            CancellationToken cancellationToken)
        {
            var simdi = DateTime.Now;
            var kilitEsigi = simdi.Subtract(GonderimKilidiSuresi);
            var guncellenen = await _context.Ykc_ImzaSurecleri
                .Where(x => x.Id == surec.Id
                    && !x.SilindiMi
                    && (x.ProviderDocumentId == null || x.ProviderDocumentId == "")
                    && (x.Durum == YkcImzaDurumDegerleri.Hazir
                        || x.Durum == YkcImzaDurumDegerleri.Hata
                        || (x.Durum == YkcImzaDurumDegerleri.ImzayaGonderildi
                            && (x.GonderimTarihi == null || x.GonderimTarihi < kilitEsigi))))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Durum, YkcImzaDurumDegerleri.ImzayaGonderildi)
                    .SetProperty(x => x.GonderimTarihi, simdi)
                    .SetProperty(x => x.HataKodu, (string?)null)
                    .SetProperty(x => x.HataMesaji, (string?)null)
                    .SetProperty(x => x.GuncellemeTarihi, simdi)
                    .SetProperty(x => x.GuncelleyenKullanici, kullaniciAdi),
                    cancellationToken);

            if (guncellenen != 1)
                return false;

            surec.Durum = YkcImzaDurumDegerleri.ImzayaGonderildi;
            surec.GonderimTarihi = simdi;
            surec.HataKodu = null;
            surec.HataMesaji = null;
            surec.GuncellemeTarihi = simdi;
            surec.GuncelleyenKullanici = kullaniciAdi;
            return true;
        }

        private static void ImzaciListesiniTamamla(
            Ykc_ImzaSureci surec,
            YkcTalepDetayDto detay,
            string? kullaniciAdi)
        {
            if (!surec.Imzacilar.Any(x => !x.SilindiMi && x.SiraNo == 1))
                surec.Imzacilar.Add(YeniImzaci("Sertifikalı Firma Yetkilisi", 1, detay.FirmaYetkiliKisi, kullaniciAdi));

            if (!surec.Imzacilar.Any(x => !x.SilindiMi && x.SiraNo == 2))
                surec.Imzacilar.Add(YeniImzaci("Dağıtım Şirketi Yetkilisi", 2, null, kullaniciAdi));

            if (!surec.Imzacilar.Any(x => !x.SilindiMi && x.SiraNo == 3))
                surec.Imzacilar.Add(YeniImzaci("Abone / Kullanıcı", 3, detay.MusteriAdi, kullaniciAdi));
        }

        private static Ykc_Imzaci YeniImzaci(string rol, int siraNo, string? adSoyad, string? kullaniciAdi)
        {
            return new Ykc_Imzaci
            {
                Rol = rol,
                SiraNo = siraNo,
                AdSoyad = adSoyad,
                Durum = YkcImzaciDurumDegerleri.Bekliyor,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullaniciAdi
            };
        }

        private static void ImzaciDurumlariniUygula(
            Ykc_ImzaSureci surec,
            IEnumerable<YkcImzaProviderImzaciDurumu> providerImzacilari,
            string? kullaniciAdi)
        {
            foreach (var providerImzaci in providerImzacilari)
            {
                var imzaci = surec.Imzacilar.FirstOrDefault(x => !x.SilindiMi && x.SiraNo == providerImzaci.SiraNo);
                if (imzaci == null)
                    continue;

                imzaci.Durum = GecerliImzaciDurumu(providerImzaci.Durum);
                imzaci.ImzaTarihi = providerImzaci.ImzaTarihi;
                imzaci.GuncellemeTarihi = DateTime.Now;
                imzaci.GuncelleyenKullanici = kullaniciAdi;
            }
        }

        private static string GecerliImzaDurumu(string? durum)
        {
            return durum switch
            {
                YkcImzaDurumDegerleri.ImzayaGonderildi => YkcImzaDurumDegerleri.ImzayaGonderildi,
                YkcImzaDurumDegerleri.ImzaBekliyor => YkcImzaDurumDegerleri.ImzaBekliyor,
                YkcImzaDurumDegerleri.KismiImzali => YkcImzaDurumDegerleri.KismiImzali,
                YkcImzaDurumDegerleri.Tamamlandi => YkcImzaDurumDegerleri.Tamamlandi,
                YkcImzaDurumDegerleri.Hata => YkcImzaDurumDegerleri.Hata,
                YkcImzaDurumDegerleri.Iptal => YkcImzaDurumDegerleri.Iptal,
                _ => YkcImzaDurumDegerleri.ImzaBekliyor
            };
        }

        private static string GecerliImzaciDurumu(string? durum)
        {
            return durum switch
            {
                YkcImzaciDurumDegerleri.Imzaladi => YkcImzaciDurumDegerleri.Imzaladi,
                YkcImzaciDurumDegerleri.Reddetti => YkcImzaciDurumDegerleri.Reddetti,
                _ => YkcImzaciDurumDegerleri.Bekliyor
            };
        }

        private static bool ImzaliNihaiBelgeHazirMi(
            Ykc_ImzaSureci surec,
            IEnumerable<Ykc_FormDosya> dosyalar)
        {
            return surec.Durum == YkcImzaDurumDegerleri.Tamamlandi
                && !string.IsNullOrWhiteSpace(surec.ProviderDocumentId)
                && surec.NihaiDosyaId.HasValue
                && dosyalar.Any(x => x.Id == surec.NihaiDosyaId.Value
                    && !x.SilindiMi
                    && x.DosyaTuru == YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai);
        }

        private static void GecmisEkle(Ykc_Talep talep, AppKullanici kullanici, string islemTipi, string? aciklama)
        {
            talep.IslemGecmisi.Add(new Ykc_IslemGecmisi
            {
                TalepId = talep.Id,
                IslemTipi = islemTipi,
                YeniDurum = talep.Durum,
                Aciklama = aciklama,
                KullaniciId = kullanici.Id,
                KullaniciAdi = kullanici.UserName,
                OlusturmaTarihi = DateTime.Now,
                OlusturanKullanici = kullanici.UserName
            });
        }

        private static bool TerminalDurumMu(int durum)
        {
            return durum == YkcDurumDegerleri.Tamamlandi
                || durum == YkcDurumDegerleri.Reddedildi
                || durum == YkcDurumDegerleri.Iptal;
        }

        private static bool ImzaGonderimineHazirMi(YkcTalepDetayDto detay, out string mesaj)
        {
            if (detay.Durum != YkcDurumDegerleri.SahaIsleminde)
            {
                mesaj = "FR265 yalnız randevu gerçekleşip kontrol aşamasına geçtikten sonra imzaya gönderilebilir.";
                return false;
            }

            if (!detay.RandevuTarihi.HasValue || string.IsNullOrWhiteSpace(detay.RandevuSaati))
            {
                mesaj = "FR265 imzaya gönderilmeden önce randevu tarih ve saat bilgisi kaydedilmelidir.";
                return false;
            }

            var kontroller = detay.Kontroller
                .Where(x => x.KontrolNo is >= 1 and <= 5)
                .GroupBy(x => x.KontrolNo)
                .Select(x => x.OrderByDescending(k => k.KontrolTarihi ?? DateTime.MinValue).First())
                .ToList();

            if (kontroller.Count != 5)
            {
                mesaj = "FR265 imzaya gönderilmeden önce 1-5 kontrol sonuçları tamamlanmalıdır.";
                return false;
            }

            var kabulEdilenSonuclar = new[]
            {
                YkcFr265KontrolSonucDegerleri.Uygun,
                YkcFr265KontrolSonucDegerleri.Uygulanmaz
            };

            if (kontroller.Any(x => !kabulEdilenSonuclar.Contains(x.Sonuc)))
            {
                mesaj = "FR265 imzaya gönderilmeden önce tüm kontroller uygun veya uygulanmaz olarak tamamlanmalıdır.";
                return false;
            }

            mesaj = "";
            return true;
        }

        private static string HashOlustur(byte[] bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        private static string TekrarsizIstekAnahtari(int talepId, int versiyon, string? hash)
        {
            var kisaHash = string.IsNullOrWhiteSpace(hash)
                ? "HASHYOK"
                : hash.Trim()[..Math.Min(hash.Trim().Length, 16)];
            return $"YKC-{talepId}-V{versiyon}-{kisaHash}";
        }

        private static string GuvenliDosyaAdi(string dosyaAdi)
        {
            var sadeceAd = Path.GetFileName(dosyaAdi);
            foreach (var karakter in Path.GetInvalidFileNameChars())
                sadeceAd = sadeceAd.Replace(karakter, '_');

            return string.IsNullOrWhiteSpace(sadeceAd) ? "FR265_Belgesi" : sadeceAd;
        }

        private sealed class SaklananBelge
        {
            public string DepolamaAnahtari { get; set; } = "";
            public DateTime KayitTarihi { get; set; }
        }

    }
}
