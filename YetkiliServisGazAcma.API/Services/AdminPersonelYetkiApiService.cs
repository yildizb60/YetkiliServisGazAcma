using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.API.Controllers;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services
{
    public class AdminPersonelYetkiApiService
    {
        private readonly AppDbContext _context;

        public AdminPersonelYetkiApiService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AdminYetkiListeDto> ListeleAsync(AppKullanici kullanici, int? sirketId, bool genelSistemAdminMi)
        {
            var personelQuery = _context.Users
                .Include(x => x.Sirket)
                .Where(x => x.KullaniciTipi == 2)
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
                if (!yetkiSirketAdlariMap.ContainsKey(personel.Id)
                    && personel.Sirket != null
                    && !string.IsNullOrWhiteSpace(personel.Sirket.SirketAdi))
                {
                    yetkiSirketAdlariMap[personel.Id] = new List<string> { personel.Sirket.SirketAdi };
                }
            }

            return new AdminYetkiListeDto
            {
                Personeller = personeller.Select(MapKullanici).ToList(),
                YetkiMap = yetkiMap,
                YetkiSirketAdlariMap = yetkiSirketAdlariMap
            };
        }

        public async Task<AdminYetkiDuzenleDto> GetirAsync(AdminYetkiGetirDto? dto, AppKullanici kullanici, int? sirketId, bool genelSistemAdminMi)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return new AdminYetkiDuzenleDto();

            var personel = await _context.Users
                .Include(x => x.Sirket)
                .FirstOrDefaultAsync(x => x.Id == dto.Id && x.KullaniciTipi == 2);

            if (personel == null || !await KullaniciKapsamindaMi(kullanici, personel, sirketId, genelSistemAdminMi))
                return new AdminYetkiDuzenleDto();

            var sirketler = await YonetilebilirSirketlerAsync(kullanici, sirketId, genelSistemAdminMi);
            var sirketIds = sirketler.Select(x => x.Id).ToHashSet();
            var mevcutKayitlar = await _context.Dag_PersonelYetkiler
                .Where(x => x.KullaniciId == personel.Id)
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

        public async Task<AdminIslemSonucDto> GuncelleAsync(AdminYetkiGuncelleDto? dto, AppKullanici kullanici, int? sirketId, bool genelSistemAdminMi)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Id))
                return AdminIslemSonucDto.Basarisiz("Personel id zorunludur.");

            var personel = await _context.Users.FirstOrDefaultAsync(x => x.Id == dto.Id && x.KullaniciTipi == 2);
            if (personel == null)
                return AdminIslemSonucDto.Basarisiz("Personel bulunamadi.");

            if (!await KullaniciKapsamindaMi(kullanici, personel, sirketId, genelSistemAdminMi))
                return AdminIslemSonucDto.Basarisiz("Personel bu kapsamda yonetilemez.");

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
                .Where(x => x.KullaniciId == personel.Id)
                .Where(x => yonetilebilirSirketIds.Contains(x.SirketId))
                .ToListAsync();

            _context.Dag_PersonelYetkiler.RemoveRange(mevcut);

            foreach (var hedefSirketId in secilenSirketIds)
            {
                dto.Yetkiler.TryGetValue(hedefSirketId, out var secilenYetkiler);
                secilenYetkiler = NormalizeYetkiListesi(secilenYetkiler ?? new List<string>());

                foreach (var yetki in secilenYetkiler)
                {
                    _context.Dag_PersonelYetkiler.Add(new Dag_PersonelYetki
                    {
                        KullaniciId = personel.Id,
                        SirketId = hedefSirketId,
                        YetkiTipi = yetki,
                        OlusturmaTarihi = DateTime.Now,
                        OlusturanKullanici = kullanici.UserName ?? "api",
                        SilindiMi = false
                    });
                }
            }

            await _context.SaveChangesAsync();
            return AdminIslemSonucDto.BasariliSonuc("Yetkiler guncellendi.");
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

            if (hedef.KullaniciTipi == 1 && hedef.FirmaId.HasValue)
            {
                return await _context.Ys_Firmalar.AnyAsync(x =>
                    x.Id == hedef.FirmaId.Value &&
                    !x.SilindiMi &&
                    x.SirketId == sirketId.Value);
            }

            return (hedef.KullaniciTipi == 2 || hedef.KullaniciTipi == 3) && hedef.SirketId == sirketId.Value;
        }

        private static List<string> NormalizeYetkiListesi(IEnumerable<string?> yetkiler)
        {
            var liste = yetkiler
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != YetkiTipleri.DAGITIM_SIRKET_YONET)
                .Select(x => x!)
                .Distinct()
                .ToList();

            return liste.Contains(YetkiTipleri.TAM_YETKI)
                ? new List<string> { YetkiTipleri.TAM_YETKI }
                : liste;
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
