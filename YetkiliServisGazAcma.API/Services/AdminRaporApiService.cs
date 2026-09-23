using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services
{
    public class AdminRaporApiService
    {
        private readonly AppDbContext _context;

        public AdminRaporApiService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AdminDevreyeAlmaListeDto> DevreyeAlmalarAsync(AdminDevreyeAlmaListeFiltreDto? dto, int? sirketId)
        {
            var query = DevreyeAlmaTemelQuery(sirketId);

            if (!string.IsNullOrWhiteSpace(dto?.TesisatNo))
                query = query.Where(x => x.TesistatNo != null && x.TesistatNo.Contains(dto.TesisatNo));
            if (!string.IsNullOrWhiteSpace(dto?.Marka))
                query = query.Where(x =>
                    (x.CihazMarka != null && x.CihazMarka.Contains(dto.Marka)) ||
                    (x.Marka != null && x.Marka.MarkaAdi != null && x.Marka.MarkaAdi.Contains(dto.Marka)));
            if (!string.IsNullOrWhiteSpace(dto?.Servis))
                query = query.Where(x => x.Firma != null && x.Firma.FirmaAdi != null && x.Firma.FirmaAdi.Contains(dto.Servis));
            if (!string.IsNullOrWhiteSpace(dto?.Il))
                query = query.Where(x => x.Firma != null && x.Firma.FaaliyetIli != null && x.Firma.FaaliyetIli.Contains(dto.Il));
            if (!string.IsNullOrWhiteSpace(dto?.Ilce))
                query = query.Where(x => _context.Ys_Subeler.Any(s => !s.SilindiMi && s.FirmaId == x.FirmaId && s.Ilce != null && s.Ilce.Contains(dto.Ilce)));
            if (dto?.Durum.HasValue == true)
                query = query.Where(x => x.Durum == dto.Durum.Value);
            if (dto?.BaslangicTarihi.HasValue == true)
                query = query.Where(x => x.OlusturmaTarihi >= dto.BaslangicTarihi.Value.Date);
            if (dto?.BitisTarihi.HasValue == true)
                query = query.Where(x => x.OlusturmaTarihi < dto.BitisTarihi.Value.Date.AddDays(1));

            var islemler = await query.OrderByDescending(x => x.OlusturmaTarihi).ToListAsync();
            var firmaIds = islemler.Select(x => x.FirmaId).Distinct().ToList();
            var subeler = await _context.Ys_Subeler
                .Where(x => !x.SilindiMi && firmaIds.Contains(x.FirmaId))
                .OrderBy(x => x.SubeAdi)
                .ToListAsync();

            var markalar = await _context.Ys_Markalar
                .Where(x => !x.SilindiMi)
                .OrderBy(x => x.MarkaAdi)
                .Select(x => new AdminMarkaSecenekDto
                {
                    Id = x.Id,
                    MarkaAdi = x.MarkaAdi
                })
                .ToListAsync();

            return new AdminDevreyeAlmaListeDto
            {
                Islemler = islemler.Select(AdminDevreyeAlmaDto.FromEntity).ToList(),
                Markalar = markalar,
                FirmaIlceleri = subeler
                    .GroupBy(x => x.FirmaId)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Select(s => s.Ilce).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "-")
            };
        }

        public async Task<AdminDevreyeAlmaDto?> DevreyeAlmaGetirAsync(int id, int? sirketId)
        {
            var kayit = await DevreyeAlmaTemelQuery(sirketId)
                .FirstOrDefaultAsync(x => x.Id == id);

            return kayit == null ? null : AdminDevreyeAlmaDto.FromEntity(kayit);
        }

        public async Task<AdminYetkiBelgesiUyariListeDto> YetkiBelgesiUyarilariAsync(int? sirketId)
        {
            var bugun = DateTime.Now.Date;
            var bitisSinir = bugun.AddDays(30);
            var query = YetkiBelgesiTemelQuery(sirketId)
                .Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi);

            var yaklasan = await query
                .Where(x => x.YetkiBelgesiBitisTarihi >= bugun && x.YetkiBelgesiBitisTarihi <= bitisSinir)
                .OrderBy(x => x.YetkiBelgesiBitisTarihi)
                .ToListAsync();

            var gecmis = await query
                .Where(x => x.YetkiBelgesiBitisTarihi < bugun)
                .OrderByDescending(x => x.YetkiBelgesiBitisTarihi)
                .ToListAsync();

            return new AdminYetkiBelgesiUyariListeDto
            {
                Yaklasan = yaklasan.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList(),
                Gecmis = gecmis.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList()
            };
        }

        public async Task<AdminRaporOzetDto> RaporlarOzetAsync(AdminRaporOzetFiltreDto? dto, int? sirketId)
        {
            var basTarih = dto?.BaslangicTarihi?.Date ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var bitTarih = dto?.BitisTarihi?.Date ?? DateTime.Now.Date;
            if (basTarih > bitTarih)
                (basTarih, bitTarih) = (bitTarih, basTarih);

            var bitSonrasi = bitTarih.AddDays(1);
            var raporTipi = string.IsNullOrWhiteSpace(dto?.Tip) ? "devreye" : dto.Tip.Trim().ToLowerInvariant();
            var belgeRaporu = raporTipi is "onayli" or "bekleyen" or "reddedilen";

            var operasyonQuery = YkcTalepTemelQuery(sirketId)
                .Where(x => x.TalepTarihi >= basTarih && x.TalepTarihi < bitSonrasi);
            var operasyonTalepleri = await operasyonQuery
                .Select(x => new
                {
                    x.Id,
                    x.TalepTarihi,
                    x.Durum,
                    FirmaAdi = x.Firma != null ? x.Firma.FirmaAdi : null,
                    x.Il,
                    x.Ilce,
                    x.AtananEkip,
                    x.RedAciklama
                })
                .ToListAsync();
            var operasyonTalepIds = operasyonTalepleri.Select(x => x.Id).ToList();

            var tamamlanmaGecmisi = operasyonTalepIds.Count == 0
                ? new List<OperasyonTamamlanmaSatiri>()
                : await _context.Ykc_IslemGecmisi
                    .AsNoTracking()
                    .Where(x => !x.SilindiMi
                        && operasyonTalepIds.Contains(x.TalepId)
                        && x.YeniDurum == YkcDurumDegerleri.Tamamlandi)
                    .GroupBy(x => x.TalepId)
                    .Select(x => new OperasyonTamamlanmaSatiri
                    {
                        TalepId = x.Key,
                        TamamlanmaTarihi = x.Min(y => y.OlusturmaTarihi)
                    })
                    .ToListAsync();
            var tamamlanmaMap = tamamlanmaGecmisi.ToDictionary(x => x.TalepId, x => x.TamamlanmaTarihi);
            var tamamlanmaSureleri = operasyonTalepleri
                .Where(x => tamamlanmaMap.ContainsKey(x.Id) && tamamlanmaMap[x.Id] >= x.TalepTarihi)
                .Select(x => (tamamlanmaMap[x.Id] - x.TalepTarihi).TotalHours)
                .ToList();

            var kontrolKayitlari = operasyonTalepIds.Count == 0
                ? new List<OperasyonKontrolSatiri>()
                : await _context.Ykc_Fr265Kontroller
                    .AsNoTracking()
                    .Where(x => !x.SilindiMi
                        && operasyonTalepIds.Contains(x.TalepId)
                        && (x.Sonuc == YkcFr265KontrolSonucDegerleri.Uygun
                            || x.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil))
                    .Select(x => new OperasyonKontrolSatiri
                    {
                        Id = x.Id,
                        TalepId = x.TalepId,
                        KontrolNo = x.KontrolNo,
                        Sonuc = x.Sonuc,
                        KontrolTarihi = x.KontrolTarihi
                    })
                    .ToListAsync();
            var sonKontroller = kontrolKayitlari
                .GroupBy(x => new { x.TalepId, x.KontrolNo })
                .Select(x => x.OrderByDescending(y => y.KontrolTarihi ?? DateTime.MinValue).ThenByDescending(y => y.Id).First())
                .ToList();
            var ilkKontroller = sonKontroller.Where(x => x.KontrolNo == 1).ToList();
            var kontrolEdilenTalepSayisi = sonKontroller.Select(x => x.TalepId).Distinct().Count();
            var tekrarRandevuTalepSayisi = sonKontroller
                .Where(x => x.Sonuc == YkcFr265KontrolSonucDegerleri.UygunDegil)
                .Select(x => x.TalepId)
                .Distinct()
                .Count();

            var aylikBitis = new DateTime(bitTarih.Year, bitTarih.Month, 1);
            var aylikBaslangic = aylikBitis.AddMonths(-5);
            var operasyonAylikHam = await YkcTalepTemelQuery(sirketId)
                .Where(x => x.TalepTarihi >= aylikBaslangic && x.TalepTarihi < aylikBitis.AddMonths(1))
                .GroupBy(x => new { x.TalepTarihi.Year, x.TalepTarihi.Month })
                .Select(x => new { x.Key.Year, x.Key.Month, Sayi = x.Count() })
                .ToListAsync();
            var operasyonAylikMap = operasyonAylikHam.ToDictionary(x => $"{x.Year:D4}-{x.Month:D2}", x => x.Sayi);
            var operasyonAylar = Enumerable.Range(0, 6).Select(aylikBaslangic.AddMonths).ToList();

            var operasyonFirmaKirilimi = operasyonTalepleri
                .GroupBy(x => string.IsNullOrWhiteSpace(x.FirmaAdi) ? "Firma belirtilmemiş" : x.FirmaAdi!.Trim())
                .Select(x => new { Etiket = x.Key, Sayi = x.Count() })
                .OrderByDescending(x => x.Sayi)
                .ThenBy(x => x.Etiket)
                .Take(6)
                .ToList();
            var lokasyonKirilimi = operasyonTalepleri
                .GroupBy(x => LokasyonEtiketi(x.Il, x.Ilce))
                .Select(x => new { Etiket = x.Key, Sayi = x.Count() })
                .OrderByDescending(x => x.Sayi)
                .ThenBy(x => x.Etiket)
                .Take(6)
                .ToList();
            var ekipKirilimi = operasyonTalepleri
                .GroupBy(x => string.IsNullOrWhiteSpace(x.AtananEkip) ? "Ekip atanmamış" : x.AtananEkip!.Trim())
                .Select(x => new { Etiket = x.Key, Sayi = x.Count() })
                .OrderByDescending(x => x.Sayi)
                .ThenBy(x => x.Etiket)
                .Take(6)
                .ToList();
            var redKirilimi = operasyonTalepleri
                .Where(x => x.Durum == YkcDurumDegerleri.Reddedildi)
                .GroupBy(x => KisaEtiket(x.RedAciklama, "Gerekçe belirtilmemiş"))
                .Select(x => new { Etiket = x.Key, Sayi = x.Count() })
                .OrderByDescending(x => x.Sayi)
                .ThenBy(x => x.Etiket)
                .Take(6)
                .ToList();

            var devreyeTemelQuery = DevreyeAlmaTemelQuery(sirketId)
                .Where(x => x.OlusturmaTarihi >= basTarih && x.OlusturmaTarihi < bitSonrasi);

            var yetkiBelgesiTemelQuery = YetkiBelgesiTemelQuery(sirketId)
                .Where(x => x.OlusturmaTarihi >= basTarih && x.OlusturmaTarihi < bitSonrasi);
            var seciliYetkiBelgesiQuery = raporTipi switch
            {
                "onayli" => yetkiBelgesiTemelQuery.Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi),
                "bekleyen" => yetkiBelgesiTemelQuery.Where(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
                    && x.YetkiBelgesiBitisTarihi >= DateTime.Today),
                "reddedilen" => yetkiBelgesiTemelQuery.Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Reddedildi),
                _ => yetkiBelgesiTemelQuery
            };

            var devreyeSayisi = await devreyeTemelQuery.CountAsync();
            var devreyeTamamlanan = await devreyeTemelQuery.Where(x => x.Durum == DevreyeAlmaDurumDegerleri.Tamamlandi).CountAsync();
            var devreyeBekleyen = await devreyeTemelQuery.Where(x => x.Durum == DevreyeAlmaDurumDegerleri.Bekliyor).CountAsync();
            var devreyeIptal = await devreyeTemelQuery.Where(x => x.Durum == DevreyeAlmaDurumDegerleri.Iptal).CountAsync();
            var yetkiBelgesiOnayli = await yetkiBelgesiTemelQuery.Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Onaylandi).CountAsync();
            var yetkiBelgesiBekleyen = await yetkiBelgesiTemelQuery.Where(x => x.Durum == YetkiBelgesiDurumDegerleri.OnaydaBekliyor
                && x.YetkiBelgesiBitisTarihi >= DateTime.Today).CountAsync();
            var yetkiBelgesiReddedilen = await yetkiBelgesiTemelQuery.Where(x => x.Durum == YetkiBelgesiDurumDegerleri.Reddedildi).CountAsync();

            var devreyeAylikBaslangic = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-5);
            var aylikEtiketler = Enumerable.Range(0, 6)
                .Select(i => devreyeAylikBaslangic.AddMonths(i))
                .ToList();

            Dictionary<string, int> aylikMap;
            if (belgeRaporu)
            {
                var aylikHam = await seciliYetkiBelgesiQuery
                    .Where(x => x.OlusturmaTarihi >= devreyeAylikBaslangic)
                    .GroupBy(x => new { x.OlusturmaTarihi.Year, x.OlusturmaTarihi.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                    .ToListAsync();
                aylikMap = aylikHam.ToDictionary(x => $"{x.Year:D4}-{x.Month:D2}", x => x.Count);
            }
            else
            {
                var aylikHam = await devreyeTemelQuery
                    .Where(x => x.OlusturmaTarihi >= devreyeAylikBaslangic)
                    .GroupBy(x => new { x.OlusturmaTarihi.Year, x.OlusturmaTarihi.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                    .ToListAsync();
                aylikMap = aylikHam.ToDictionary(x => $"{x.Year:D4}-{x.Month:D2}", x => x.Count);
            }
            var chartAylikLabels = aylikEtiketler.Select(x => x.ToString("MM.yyyy")).ToList();
            var chartAylikData = aylikEtiketler
                .Select(x => aylikMap.TryGetValue($"{x.Year:D4}-{x.Month:D2}", out var value) ? value : 0)
                .ToList();

            List<string?> chartSirketLabels;
            List<int> chartSirketData;
            List<string?> chartKirilimLabels;
            List<int> chartKirilimData;
            if (belgeRaporu)
            {
                var sirketKirilimi = await seciliYetkiBelgesiQuery
                    .Where(x => x.Firma != null && x.Firma.Sirket != null)
                    .GroupBy(x => x.Firma!.Sirket!.SirketAdi)
                    .Select(g => new { Ad = g.Key, Sayi = g.Count() })
                    .OrderByDescending(x => x.Sayi)
                    .Take(6)
                    .ToListAsync();
                var firmaKirilimi = await seciliYetkiBelgesiQuery
                    .Where(x => x.Firma != null)
                    .GroupBy(x => x.Firma!.FirmaAdi)
                    .Select(g => new { Ad = g.Key, Sayi = g.Count() })
                    .OrderByDescending(x => x.Sayi)
                    .Take(6)
                    .ToListAsync();
                chartSirketLabels = sirketKirilimi.Select(x => x.Ad).ToList();
                chartSirketData = sirketKirilimi.Select(x => x.Sayi).ToList();
                chartKirilimLabels = firmaKirilimi.Select(x => x.Ad).ToList();
                chartKirilimData = firmaKirilimi.Select(x => x.Sayi).ToList();
            }
            else
            {
                var sirketKirilimi = await devreyeTemelQuery
                    .Where(x => x.Firma != null && x.Firma.Sirket != null)
                    .GroupBy(x => x.Firma!.Sirket!.SirketAdi)
                    .Select(g => new { Ad = g.Key, Sayi = g.Count() })
                    .OrderByDescending(x => x.Sayi)
                    .Take(6)
                    .ToListAsync();
                var markaKirilimi = await devreyeTemelQuery
                    .Where(x => x.Marka != null)
                    .GroupBy(x => x.Marka!.MarkaAdi)
                    .Select(g => new { Ad = g.Key, Sayi = g.Count() })
                    .OrderByDescending(x => x.Sayi)
                    .Take(6)
                    .ToListAsync();
                chartSirketLabels = sirketKirilimi.Select(x => x.Ad).ToList();
                chartSirketData = sirketKirilimi.Select(x => x.Sayi).ToList();
                chartKirilimLabels = markaKirilimi.Select(x => x.Ad).ToList();
                chartKirilimData = markaKirilimi.Select(x => x.Sayi).ToList();
            }

            var sonuc = new AdminRaporOzetDto
            {
                BasTarih = basTarih,
                BitTarih = bitTarih,
                RaporTipi = raporTipi,
                DevreyeSayisi = devreyeSayisi,
                DevreyeTamamlanan = devreyeTamamlanan,
                DevreyeBekleyen = devreyeBekleyen,
                DevreyeIptal = devreyeIptal,
                YetkiBelgesiOnayli = yetkiBelgesiOnayli,
                YetkiBelgesiBekleyen = yetkiBelgesiBekleyen,
                YetkiBelgesiReddedilen = yetkiBelgesiReddedilen,
                OperasyonTalepSayisi = operasyonTalepleri.Count,
                OperasyonTamamlanan = operasyonTalepleri.Count(x => x.Durum == YkcDurumDegerleri.Tamamlandi),
                OperasyonAktif = operasyonTalepleri.Count(x => x.Durum is YkcDurumDegerleri.TalepAlindi
                    or YkcDurumDegerleri.AtamaBekliyor
                    or YkcDurumDegerleri.Atandi
                    or YkcDurumDegerleri.SahaIsleminde),
                OperasyonReddedilen = operasyonTalepleri.Count(x => x.Durum == YkcDurumDegerleri.Reddedildi),
                OperasyonIptal = operasyonTalepleri.Count(x => x.Durum == YkcDurumDegerleri.Iptal),
                OrtalamaTamamlanmaSaati = tamamlanmaSureleri.Count == 0 ? 0 : Math.Round(tamamlanmaSureleri.Average(), 1),
                TamamlanmaSuresiKayitSayisi = tamamlanmaSureleri.Count,
                IlkKontrolUygunlukOrani = ilkKontroller.Count == 0
                    ? 0
                    : Math.Round(ilkKontroller.Count(x => x.Sonuc == YkcFr265KontrolSonucDegerleri.Uygun) * 100d / ilkKontroller.Count, 1),
                IlkKontrolKayitSayisi = ilkKontroller.Count,
                TekrarRandevuOrani = kontrolEdilenTalepSayisi == 0
                    ? 0
                    : Math.Round(tekrarRandevuTalepSayisi * 100d / kontrolEdilenTalepSayisi, 1),
                KontrolEdilenTalepSayisi = kontrolEdilenTalepSayisi,
                OperasyonAylikLabels = operasyonAylar.Select(x => x.ToString("MMM yyyy")).ToList(),
                OperasyonAylikData = operasyonAylar
                    .Select(x => operasyonAylikMap.TryGetValue($"{x.Year:D4}-{x.Month:D2}", out var value) ? value : 0)
                    .ToList(),
                OperasyonFirmaLabels = operasyonFirmaKirilimi.Select(x => x.Etiket).ToList(),
                OperasyonFirmaData = operasyonFirmaKirilimi.Select(x => x.Sayi).ToList(),
                OperasyonLokasyonLabels = lokasyonKirilimi.Select(x => x.Etiket).ToList(),
                OperasyonLokasyonData = lokasyonKirilimi.Select(x => x.Sayi).ToList(),
                OperasyonEkipLabels = ekipKirilimi.Select(x => x.Etiket).ToList(),
                OperasyonEkipData = ekipKirilimi.Select(x => x.Sayi).ToList(),
                OperasyonRedNedeniLabels = redKirilimi.Select(x => x.Etiket).ToList(),
                OperasyonRedNedeniData = redKirilimi.Select(x => x.Sayi).ToList(),
                ChartAylikLabels = chartAylikLabels,
                ChartAylikData = chartAylikData,
                ChartDurumData = belgeRaporu
                    ? new List<int> { yetkiBelgesiOnayli, yetkiBelgesiBekleyen, yetkiBelgesiReddedilen }
                    : new List<int> { devreyeTamamlanan, devreyeBekleyen, devreyeIptal },
                ChartSirketLabels = chartSirketLabels,
                ChartSirketData = chartSirketData,
                ChartMarkaLabels = chartKirilimLabels,
                ChartMarkaData = chartKirilimData,
                Sirketler = await SirketSecenekleriAsync(sirketId)
            };

            if (raporTipi == "onayli" || raporTipi == "bekleyen" || raporTipi == "reddedilen")
            {
                var yetkiBelgesiIslemler = await seciliYetkiBelgesiQuery
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .Take(12)
                    .ToListAsync();

                sonuc.ListeTipi = "yetkiBelgesi";
                sonuc.YetkiBelgesiIslemler = yetkiBelgesiIslemler.Select(AdminYetkiBelgesiOnayDto.FromEntity).ToList();
            }
            else
            {
                var sonIslemler = await devreyeTemelQuery
                    .OrderByDescending(x => x.OlusturmaTarihi)
                    .Take(12)
                    .ToListAsync();

                sonuc.ListeTipi = "devreye";
                sonuc.SonIslemler = sonIslemler.Select(AdminDevreyeAlmaDto.FromEntity).ToList();
            }

            return sonuc;
        }

        private IQueryable<Ykc_Talep> YkcTalepTemelQuery(int? sirketId)
        {
            return _context.Ykc_Talepler
                .AsNoTracking()
                .Where(x => !x.SilindiMi
                    && (sirketId == null
                        || x.SirketId == sirketId
                        || (x.SirketId == null && x.Firma != null && x.Firma.SirketId == sirketId)));
        }

        private static string LokasyonEtiketi(string? il, string? ilce)
        {
            var temizIl = string.IsNullOrWhiteSpace(il) ? null : il.Trim();
            var temizIlce = string.IsNullOrWhiteSpace(ilce) ? null : ilce.Trim();
            if (temizIl == null && temizIlce == null) return "Konum belirtilmemiş";
            if (temizIl == null) return temizIlce!;
            if (temizIlce == null) return temizIl;
            return $"{temizIl} / {temizIlce}";
        }

        private static string KisaEtiket(string? value, string fallback)
        {
            var temiz = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            return temiz.Length <= 54 ? temiz : temiz[..51] + "...";
        }

        private sealed class OperasyonTamamlanmaSatiri
        {
            public int TalepId { get; set; }
            public DateTime TamamlanmaTarihi { get; set; }
        }

        private sealed class OperasyonKontrolSatiri
        {
            public int Id { get; set; }
            public int TalepId { get; set; }
            public int KontrolNo { get; set; }
            public string Sonuc { get; set; } = string.Empty;
            public DateTime? KontrolTarihi { get; set; }
        }

        private IQueryable<Ys_DevreyeAlma> DevreyeAlmaTemelQuery(int? sirketId)
        {
            return _context.Ys_DevreyeAlmalar
                .Include(x => x.Firma)
                .ThenInclude(x => x!.Sirket)
                .Include(x => x.Marka)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId));
        }

        private IQueryable<Ys_YetkiBelgesi> YetkiBelgesiTemelQuery(int? sirketId)
        {
            return _context.Ys_YetkiBelgeleri
                .Include(x => x.Firma).ThenInclude(x => x!.Sirket)
                .Where(x => !x.SilindiMi
                    && x.Firma != null
                    && !x.Firma.SilindiMi
                    && (sirketId == null || x.Firma.SirketId == sirketId));
        }

        private async Task<List<AdminSirketSecenekDto>> SirketSecenekleriAsync(int? sirketId)
        {
            var query = _context.Dag_Sirketler
                .Where(x => !x.SilindiMi)
                .AsQueryable();

            if (sirketId.HasValue)
                query = query.Where(x => x.Id == sirketId.Value);

            return await query
                .OrderBy(x => x.SirketAdi)
                .Select(x => new AdminSirketSecenekDto
                {
                    Id = x.Id,
                    SirketAdi = x.SirketAdi
                })
                .ToListAsync();
        }
    }
}
