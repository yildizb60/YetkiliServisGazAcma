using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services
{
    public class AdminPersonelYetkiApiService
    {
        private static readonly HashSet<string> GecerliYetkiTipleri = new(StringComparer.OrdinalIgnoreCase)
        {
            YetkiTipleri.YETKI_BELGESI_ONAY,
            YetkiTipleri.RAPOR_GOR,
            YetkiTipleri.KULLANICI_YONET,
            YetkiTipleri.MARKA_YONET,
            YetkiTipleri.YKC_TALEP_GOR,
            YetkiTipleri.YKC_ATAMA_YAP,
            YetkiTipleri.YKC_FR265_IMZA_ISLEM,
            YetkiTipleri.YKC_RAPOR_GOR,
            YetkiTipleri.TAM_YETKI
        };

        private readonly AppDbContext _context;

        public AdminPersonelYetkiApiService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AdminYetkiListeDto> ListeleAsync(AppKullanici kullanici, int? sirketId, bool genelSistemAdminMi,
            AdminYetkiListeFiltreDto? filtre = null)
        {
            if (!PersonelYetkiYonetimKurali.YonetebilirMi(kullanici, sirketId))
                return new AdminYetkiListeDto();
            var personelQuery = _context.Users
                .Include(x => x.Sirket)
                .Where(x => x.KullaniciTipi == KullaniciTipiDegerleri.Personel && x.ArsivlemeTarihi == null)
                .AsQueryable();

            if (!(genelSistemAdminMi && !sirketId.HasValue))
            {
                if (!sirketId.HasValue)
                    return new AdminYetkiListeDto();

                personelQuery = personelQuery.Where(x =>
                    x.SirketId == sirketId.Value ||
                    _context.Dag_PersonelYetkiler.Any(y =>
                        y.KullaniciId == x.Id &&
                        y.SirketId == sirketId.Value &&
                        !y.SilindiMi));
            }

            var personeller = await personelQuery
                .OrderBy(x => x.AdSoyad)
                .ToListAsync();

            var personelIds = personeller.Select(x => x.Id).ToList();
            var yetkiQuery = _context.Dag_PersonelYetkiler
                .Include(x => x.Sirket)
                .Where(x => personelIds.Contains(x.KullaniciId) && !x.SilindiMi)
                .AsQueryable();

            if (sirketId.HasValue)
                yetkiQuery = yetkiQuery.Where(x => x.SirketId == sirketId.Value);

            var yetkiKayitlari = await yetkiQuery.ToListAsync();
            var sirketYetkileri = yetkiKayitlari
                .GroupBy(x => x.KullaniciId)
                .ToDictionary(g => g.Key, g => g.GroupBy(x => x.SirketId)
                    .Select(sirket => new AdminSirketYetkiOzetDto
                    {
                        SirketId = sirket.Key,
                        SirketAdi = sirket.First().Sirket?.SirketAdi,
                        Yetkiler = NormalizeYetkiListesi(sirket.Select(x => x.YetkiTipi))
                    })
                    .OrderBy(x => x.SirketAdi)
                    .ToList());
            var yetkiMap = yetkiKayitlari
                .GroupBy(x => x.KullaniciId)
                .ToDictionary(
                    g => g.Key,
                    g => NormalizeYetkiListesi(g.Select(x => x.YetkiTipi)));

            var yetkiSirketAdlariMap = yetkiKayitlari
                .Where(x => x.Sirket != null && !string.IsNullOrWhiteSpace(x.Sirket.SirketAdi))
                .GroupBy(x => x.KullaniciId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.Sirket!.SirketAdi!)
                        .Distinct()
                        .OrderBy(x => x)
                        .ToList());

            foreach (var personel in personeller)
            {
                if (!sirketYetkileri.ContainsKey(personel.Id) && personel.SirketId.HasValue
                    && (!sirketId.HasValue || personel.SirketId == sirketId))
                {
                    sirketYetkileri[personel.Id] = new List<AdminSirketYetkiOzetDto>
                    {
                        new() { SirketId = personel.SirketId.Value, SirketAdi = personel.Sirket?.SirketAdi }
                    };
                }
                if (!yetkiSirketAdlariMap.ContainsKey(personel.Id)
                    && personel.Sirket != null
                    && !string.IsNullOrWhiteSpace(personel.Sirket.SirketAdi))
                {
                    yetkiSirketAdlariMap[personel.Id] = new List<string> { personel.Sirket.SirketAdi };
                }
            }

            var q = filtre?.Q?.Trim();
            if (q?.Length > 120) q = q[..120];
            var karsilastirma = System.Globalization.CultureInfo.GetCultureInfo("tr-TR").CompareInfo;
            bool Eslesiyor(string? metin) => string.IsNullOrEmpty(q)
                || karsilastirma.IndexOf(metin ?? "", q, System.Globalization.CompareOptions.IgnoreCase) >= 0;
            var eslesenler = personeller.Where(p => Eslesiyor(p.AdSoyad) || Eslesiyor(p.Email)
                || (sirketYetkileri.TryGetValue(p.Id, out var kapsamlar) && kapsamlar.Any(x => Eslesiyor(x.SirketAdi))))
                .ToList();
            const int sayfaBoyutu = 10;
            var toplamSayfa = Math.Max(1, (eslesenler.Count + sayfaBoyutu - 1) / sayfaBoyutu);
            var sayfa = Math.Clamp(filtre?.Sayfa ?? 1, 1, toplamSayfa);
            var sayfadakiler = eslesenler.Skip((sayfa - 1) * sayfaBoyutu).Take(sayfaBoyutu).ToList();
            var sayfaIds = sayfadakiler.Select(x => x.Id).ToHashSet();
            var tumKapsamlar = sirketYetkileri.Values.SelectMany(x => x).ToList();

            return new AdminYetkiListeDto
            {
                Ozet = new PersonelYetkiListeOzeti
                {
                    Arama = q, Sayfa = sayfa, SayfaBoyutu = sayfaBoyutu, ToplamSayfa = toplamSayfa,
                    ToplamPersonel = personeller.Count, EslesenPersonel = eslesenler.Count,
                    YetkiliPersonel = sirketYetkileri.Values.Count(x => x.Any(y => y.Yetkiler.Count > 0)),
                    TamYetkiAtamalari = tumKapsamlar.Count(x => x.Yetkiler.Contains(YetkiTipleri.TAM_YETKI)),
                    Sirketler = tumKapsamlar.GroupBy(x => x.SirketId)
                        .Select(x => new YetkiSirketBasligi { SirketId = x.Key, SirketAdi = x.First().SirketAdi })
                        .OrderBy(x => x.SirketAdi, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), true))
                        .ToList()
                },
                Personeller = sayfadakiler.Select(MapKullanici).ToList(),
                YetkiMap = yetkiMap.Where(x => sayfaIds.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value),
                YetkiSirketAdlariMap = yetkiSirketAdlariMap.Where(x => sayfaIds.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value),
                SirketYetkileri = sirketYetkileri.Where(x => sayfaIds.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value)
            };
        }

        public async Task<AdminYetkiDuzenleDto> GetirAsync(AdminYetkiGetirDto? dto, AppKullanici kullanici, int? sirketId, bool genelSistemAdminMi)
        {
            if (!PersonelYetkiYonetimKurali.YonetebilirMi(kullanici, sirketId))
                return new AdminYetkiDuzenleDto();
            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return new AdminYetkiDuzenleDto();

            var personel = await _context.Users
                .Include(x => x.Sirket)
                .FirstOrDefaultAsync(x => x.Id == dto.Id && x.KullaniciTipi == KullaniciTipiDegerleri.Personel && x.ArsivlemeTarihi == null);

            if (personel == null || !await KullaniciKapsamindaMi(kullanici, personel, sirketId, genelSistemAdminMi))
                return new AdminYetkiDuzenleDto();

            var sirketler = await YonetilebilirSirketlerAsync(kullanici, sirketId, genelSistemAdminMi);
            var sirketIds = sirketler.Select(x => x.Id).ToHashSet();
            var mevcutKayitlar = await _context.Dag_PersonelYetkiler
                .Where(x => x.KullaniciId == personel.Id && !x.SilindiMi)
                .Where(x => sirketIds.Contains(x.SirketId))
                .ToListAsync();

            var yetkiSirketMap = mevcutKayitlar
                .GroupBy(x => x.SirketId)
                .ToDictionary(
                    g => g.Key,
                    g => NormalizeYetkiListesi(g.Select(x => x.YetkiTipi)));

            var mevcut = NormalizeYetkiListesi(mevcutKayitlar.Select(x => x.YetkiTipi));
            var seciliSirketIds = mevcutKayitlar
                .Select(x => x.SirketId)
                .Distinct()
                .ToList();

            if (seciliSirketIds.Count == 0 && personel.SirketId.HasValue && sirketIds.Contains(personel.SirketId.Value))
                seciliSirketIds.Add(personel.SirketId.Value);

            return new AdminYetkiDuzenleDto
            {
                Personel = MapKullanici(personel),
                Sirketler = sirketler,
                MevcutYetkiler = mevcut,
                YetkiSirketMap = yetkiSirketMap,
                SeciliSirketIds = seciliSirketIds
            };
        }

        public async Task<ApiIslemSonuc> GuncelleAsync(AdminYetkiGuncelleDto? dto, AppKullanici kullanici, int? sirketId, bool genelSistemAdminMi)
            => await _context.Database.CreateExecutionStrategy().ExecuteAsync(() => YetkileriKaydetAsync(dto, kullanici, sirketId, genelSistemAdminMi));

        private async Task<ApiIslemSonuc> YetkileriKaydetAsync(AdminYetkiGuncelleDto? dto, AppKullanici kullanici, int? sirketId, bool genelSistemAdminMi)
        {
            if (!PersonelYetkiYonetimKurali.YonetebilirMi(kullanici, sirketId))
                return ApiIslemSonuc.Basarisiz("Personel yetkilerini yalnızca yöneticiler düzenleyebilir.");
            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return ApiIslemSonuc.Basarisiz("Personel id zorunludur.");

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var personel = await _context.Users
                .FromSqlInterpolated($"SELECT * FROM dbo.Ys_AspNetUsers WITH (UPDLOCK, HOLDLOCK) WHERE Id = {dto.Id}")
                .FirstOrDefaultAsync(x => x.KullaniciTipi == KullaniciTipiDegerleri.Personel && x.ArsivlemeTarihi == null);
            if (personel == null)
                return ApiIslemSonuc.Basarisiz("Personel bulunamadi.");

            if (!await KullaniciKapsamindaMi(kullanici, personel, sirketId, genelSistemAdminMi))
                return ApiIslemSonuc.Basarisiz("Personel bu kapsamda yonetilemez.");

            var yonetilebilirSirketIds = (await YonetilebilirSirketlerAsync(kullanici, sirketId, genelSistemAdminMi))
                .Select(x => x.Id)
                .ToHashSet();

            var secilenSirketIds = (dto.SirketIds ?? new List<int>())
                .Where(yonetilebilirSirketIds.Contains)
                .Distinct()
                .ToList();

            if (secilenSirketIds.Count == 0 && personel.SirketId.HasValue && yonetilebilirSirketIds.Contains(personel.SirketId.Value))
                secilenSirketIds.Add(personel.SirketId.Value);

            var mevcut = await _context.Dag_PersonelYetkiler
                .Where(x => x.KullaniciId == personel.Id && !x.SilindiMi)
                .Where(x => yonetilebilirSirketIds.Contains(x.SirketId))
                .ToListAsync();

            var istenen = new HashSet<(int SirketId, string YetkiTipi)>();
            foreach (var hedefSirketId in secilenSirketIds)
            {
                dto.Yetkiler.TryGetValue(hedefSirketId, out var secilenYetkiler);
                secilenYetkiler = NormalizeYetkiListesi(secilenYetkiler ?? new List<string>());

                foreach (var yetki in secilenYetkiler)
                    istenen.Add((hedefSirketId, yetki));
            }

            var zaman = DateTime.Now;
            var yapan = kullanici.UserName ?? kullanici.Id;
            foreach (var kayit in mevcut.OrderBy(x => x.Id))
            {
                if (istenen.Remove((kayit.SirketId, kayit.YetkiTipi.Trim().ToUpperInvariant())))
                    continue;

                kayit.SilindiMi = true;
                kayit.SilinmeTarihi = zaman;
                kayit.SilenKullanici = yapan;
                kayit.GuncellemeTarihi = zaman;
                kayit.GuncelleyenKullanici = yapan;
            }

            foreach (var (hedefSirketId, yetki) in istenen)
            {
                _context.Dag_PersonelYetkiler.Add(new Dag_PersonelYetki
                {
                    KullaniciId = personel.Id,
                    SirketId = hedefSirketId,
                    YetkiTipi = yetki,
                    OlusturmaTarihi = zaman,
                    OlusturanKullanici = yapan,
                    SilindiMi = false
                });
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return ApiIslemSonuc.BasariliSonuc("Yetkiler guncellendi.");
        }

        private async Task<List<AdminSirketSecenekDto>> YonetilebilirSirketlerAsync(AppKullanici kullanici, int? kapsamSirketId, bool genelSistemAdminMi)
        {
            var query = _context.Dag_Sirketler
                .Where(x => !x.SilindiMi)
                .AsQueryable();

            if (!(genelSistemAdminMi && !kapsamSirketId.HasValue))
            {
                if (kapsamSirketId.HasValue)
                {
                    query = query.Where(x => x.Id == kapsamSirketId.Value);
                }
                else if (kullanici.SirketId.HasValue)
                {
                    query = query.Where(x => x.Id == kullanici.SirketId.Value);
                }
                else
                {
                    var yetkiliSirketIds = await _context.Dag_PersonelYetkiler
                        .Where(x => x.KullaniciId == kullanici.Id && !x.SilindiMi)
                        .Select(x => x.SirketId)
                        .Distinct()
                        .ToListAsync();

                    query = query.Where(x => yetkiliSirketIds.Contains(x.Id));
                }
            }

            return await query
                .OrderBy(x => x.SirketAdi)
                .Select(x => new AdminSirketSecenekDto
                {
                    Id = x.Id,
                    SirketAdi = x.SirketAdi
                })
                .ToListAsync();
        }

        private async Task<bool> KullaniciKapsamindaMi(AppKullanici yapan, AppKullanici hedef, int? sirketId, bool genelSistemAdminMi)
        {
            if (yapan.Id == hedef.Id)
                return true;

            if (genelSistemAdminMi && !sirketId.HasValue)
                return true;

            if (!sirketId.HasValue)
                return false;

            if (hedef.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis && hedef.FirmaId.HasValue)
            {
                return await _context.Ys_Firmalar.AnyAsync(x =>
                    x.Id == hedef.FirmaId.Value &&
                    !x.SilindiMi &&
                    x.SirketId == sirketId.Value);
            }

            return (hedef.KullaniciTipi == KullaniciTipiDegerleri.Personel || hedef.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin) && hedef.SirketId == sirketId.Value;
        }

        private static List<string> NormalizeYetkiListesi(IEnumerable<string?> yetkiler)
        {
            var liste = yetkiler
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim().ToUpperInvariant())
                .Where(GecerliYetkiTipleri.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (liste.Contains(YetkiTipleri.TAM_YETKI, StringComparer.OrdinalIgnoreCase))
                return new List<string> { YetkiTipleri.TAM_YETKI };

            if (liste.Any(x => x is YetkiTipleri.YKC_ATAMA_YAP or YetkiTipleri.YKC_FR265_IMZA_ISLEM or YetkiTipleri.YKC_RAPOR_GOR)
                && !liste.Contains(YetkiTipleri.YKC_TALEP_GOR, StringComparer.OrdinalIgnoreCase))
            {
                liste.Add(YetkiTipleri.YKC_TALEP_GOR);
            }

            return liste;
        }

        private static AdminKullaniciListeDto MapKullanici(AppKullanici kullanici)
        {
            return new AdminKullaniciListeDto
            {
                Id = kullanici.Id,
                AdSoyad = kullanici.AdSoyad,
                Email = kullanici.Email,
                PhoneNumber = kullanici.PhoneNumber,
                KullaniciTipi = kullanici.KullaniciTipi,
                AktifMi = kullanici.AktifMi,
                SirketId = kullanici.SirketId,
                SirketAdi = kullanici.Sirket?.SirketAdi,
                FirmaId = kullanici.FirmaId,
                FirmaAdi = kullanici.Firma?.FirmaAdi,
                FirmaYetkiliKisi = kullanici.Firma?.YetkiliKisi,
                FirmaEmail = kullanici.Firma?.Email,
                FirmaTelefon = kullanici.Firma?.Telefon
            };
        }
    }
}
