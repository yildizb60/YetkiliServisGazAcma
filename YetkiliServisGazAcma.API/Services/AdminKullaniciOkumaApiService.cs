using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class AdminKullaniciOkumaApiService(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<List<AdminKullaniciListeDto>> ListeleAsync(AdminKullaniciListeFiltreDto? dto, int? sirketId, bool genelSistemAdmin)
    {
        var kullaniciQuery = _context.Users
            .AsNoTracking()
            .Where(x => x.ArsivlemeTarihi == null)
            .Include(x => x.Sirket)
            .Include(x => x.Firma)
            .AsQueryable();

        if (!genelSistemAdmin)
        {
            kullaniciQuery = kullaniciQuery.Where(x =>
                ((x.KullaniciTipi == KullaniciTipiDegerleri.Personel) && sirketId.HasValue && x.SirketId == sirketId.Value) ||
                ((x.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis || x.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma) && x.Firma != null && sirketId.HasValue && x.Firma.SirketId == sirketId.Value));
        }
        else if (sirketId.HasValue)
        {
            kullaniciQuery = kullaniciQuery.Where(x =>
                ((x.KullaniciTipi == KullaniciTipiDegerleri.Personel || x.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin) && sirketId.HasValue && x.SirketId == sirketId.Value) ||
                ((x.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis || x.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma) && x.Firma != null && sirketId.HasValue && x.Firma.SirketId == sirketId.Value));
        }

        var kullanicilar = await kullaniciQuery
            .OrderBy(x => x.AdSoyad)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(dto?.Tip))
        {
            kullanicilar = dto.Tip switch
            {
                "GenelSistemAdmin" => kullanicilar.Where(x => x.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin).ToList(),
                "SirketAdmin" => kullanicilar.Where(x => x.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin).ToList(),
                "SuperAdmin" => kullanicilar.Where(x => x.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin).ToList(),
                "Personel" => kullanicilar.Where(x => x.KullaniciTipi == KullaniciTipiDegerleri.Personel).ToList(),
                "Servis" => kullanicilar.Where(x => x.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis).ToList(),
                "SertifikaliFirma" => kullanicilar.Where(x => x.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma).ToList(),
                _ => kullanicilar
            };
        }

        if (!string.IsNullOrWhiteSpace(dto?.Durum))
        {
            var aktifMi = dto.Durum.Equals("Aktif", StringComparison.OrdinalIgnoreCase);
            kullanicilar = kullanicilar.Where(x => x.AktifMi == aktifMi).ToList();
        }

        if (!string.IsNullOrWhiteSpace(dto?.Bagli))
        {
            var aranacak = dto.Bagli.Trim();
            kullanicilar = kullanicilar
                .Where(x =>
                    ((x.KullaniciTipi == KullaniciTipiDegerleri.YetkiliServis || x.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma) && x.Firma != null && !string.IsNullOrWhiteSpace(x.Firma.FirmaAdi) &&
                     x.Firma.FirmaAdi.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)) ||
                    ((x.KullaniciTipi == KullaniciTipiDegerleri.Personel || x.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin) && x.Sirket != null && !string.IsNullOrWhiteSpace(x.Sirket.SirketAdi) &&
                     x.Sirket.SirketAdi.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)))
                .ToList();
        }

        var sonuc = kullanicilar.Select(x => new AdminKullaniciListeDto
        {
            Id = x.Id,
            AdSoyad = !string.IsNullOrWhiteSpace(x.AdSoyad)
                ? x.AdSoyad
                : !string.IsNullOrWhiteSpace(x.Firma?.YetkiliKisi) ? x.Firma.YetkiliKisi : x.Firma?.FirmaAdi,
            Email = !string.IsNullOrWhiteSpace(x.Email) ? x.Email : x.Firma?.Email,
            PhoneNumber = !string.IsNullOrWhiteSpace(x.PhoneNumber) ? x.PhoneNumber : x.Firma?.Telefon,
            KullaniciTipi = x.KullaniciTipi,
            AktifMi = x.AktifMi,
            SirketId = x.SirketId,
            SirketAdi = x.Sirket?.SirketAdi,
            FirmaId = x.FirmaId,
            FirmaAdi = x.Firma?.FirmaAdi,
            FirmaYetkiliKisi = x.Firma?.YetkiliKisi,
            FirmaEmail = x.Firma?.Email,
            FirmaTelefon = x.Firma?.Telefon
        }).ToList();

        if (!string.IsNullOrWhiteSpace(dto?.Q))
        {
            var aranacak = dto.Q.Trim();
            sonuc = sonuc.Where(x =>
                (!string.IsNullOrWhiteSpace(x.AdSoyad) && x.AdSoyad.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(x.Email) && x.Email.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(x.PhoneNumber) && x.PhoneNumber.StartsWith(aranacak, StringComparison.CurrentCultureIgnoreCase)))
                .ToList();
        }

        return sonuc;
    }

    public async Task<List<AdminSirketSecenekDto>> SirketSecenekleriAsync(int? sirketId)
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

    public async Task<List<AdminFirmaSecenekDto>> FirmaSecenekleriAsync(int? sirketId)
    {
        var query = _context.Ys_Firmalar
            .Include(x => x.Sirket)
            .Where(x => !x.SilindiMi && x.AktifMi)
            .AsQueryable();

        if (sirketId.HasValue)
            query = query.Where(x => x.SirketId == sirketId.Value);

        return await query
            .OrderBy(x => x.FirmaAdi)
            .Select(x => new AdminFirmaSecenekDto
            {
                Id = x.Id,
                FirmaAdi = x.FirmaAdi,
                SirketId = x.SirketId,
                SirketAdi = x.Sirket != null ? x.Sirket.SirketAdi : null
            })
            .ToListAsync();
    }
}
