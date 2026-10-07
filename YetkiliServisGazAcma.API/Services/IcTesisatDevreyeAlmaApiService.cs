using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using YetkiliServisGazAcma.API.Controllers;

namespace YetkiliServisGazAcma.API.Services;

public sealed class IcTesisatDevreyeAlmaApiService(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<IcTesisatDevreyeAlmaListeDto?> ListeleAsync(
        IcTesisatDevreyeAlmaFiltreDto dto, AppKullanici kullanici, IList<string> roller)
    {
        var genelYetkili = roller.Contains("GenelSistemAdmin") || roller.Contains("SuperAdmin");
        var personelMi = !genelYetkili && !roller.Contains("SirketAdmin") && roller.Contains("Personel");
        var kapsamSirketId = personelMi ? dto.SirketId ?? kullanici.SirketId : kullanici.SirketId;

        if (!genelYetkili && !kapsamSirketId.HasValue)
            return null;

        if (personelMi)
        {
            if (!kapsamSirketId.HasValue || !await _context.Dag_PersonelYetkiler.AnyAsync(x =>
                x.KullaniciId == kullanici.Id && !x.SilindiMi
                && x.SirketId == kapsamSirketId.Value
                && (x.YetkiTipi == YetkiTipleri.TAM_YETKI || x.YetkiTipi == YetkiTipleri.RAPOR_GOR)))
                return null;
        }

        var query = _context.Ys_DevreyeAlmalar
            .Include(x => x.Firma)
                .ThenInclude(x => x!.Sirket)
            .Include(x => x.Marka)
            .Where(x => !x.SilindiMi)
            .AsQueryable();

        if (!genelYetkili)
        {
            query = query.Where(x => x.Firma != null && x.Firma.SirketId == kapsamSirketId!.Value);
        }
        else if (dto.SirketId.HasValue)
        {
            query = query.Where(x => x.Firma != null && x.Firma.SirketId == dto.SirketId.Value);
        }

        if (!string.IsNullOrWhiteSpace(dto.TesisatNo))
            query = query.Where(x => x.TesistatNo != null && x.TesistatNo.Contains(dto.TesisatNo));

        if (!string.IsNullOrWhiteSpace(dto.YetkiliServis))
            query = query.Where(x => x.Firma != null && x.Firma.FirmaAdi != null && x.Firma.FirmaAdi.Contains(dto.YetkiliServis));

        if (!string.IsNullOrWhiteSpace(dto.Il))
            query = query.Where(x => x.Firma != null && x.Firma.FaaliyetIli != null && x.Firma.FaaliyetIli.Contains(dto.Il));

        if (!string.IsNullOrWhiteSpace(dto.Ilce))
        {
            query = query.Where(x => _context.Ys_Subeler
                .Any(s => !s.SilindiMi
                    && s.FirmaId == x.FirmaId
                    && s.Ilce != null
                    && s.Ilce.Contains(dto.Ilce)));
        }

        if (!string.IsNullOrWhiteSpace(dto.Marka))
        {
            query = query.Where(x =>
                (x.CihazMarka != null && x.CihazMarka.Contains(dto.Marka)) ||
                (x.Marka != null && x.Marka.MarkaAdi != null && x.Marka.MarkaAdi.Contains(dto.Marka)));
        }

        if (dto.BaslangicTarihi.HasValue)
            query = query.Where(x => x.DevreyeAlmaTarihi >= dto.BaslangicTarihi.Value.Date);

        if (dto.BitisTarihi.HasValue)
            query = query.Where(x => x.DevreyeAlmaTarihi < dto.BitisTarihi.Value.Date.AddDays(1));

        var toplam = await query.CountAsync();
        var sayfa = Math.Max(dto.Sayfa, 1);
        var sayfaBoyutu = Math.Clamp(dto.SayfaBoyutu <= 0 ? 100 : dto.SayfaBoyutu, 1, 500);

        var islemler = await query
            .OrderByDescending(x => x.DevreyeAlmaTarihi)
            .ThenByDescending(x => x.Id)
            .Skip((sayfa - 1) * sayfaBoyutu)
            .Take(sayfaBoyutu)
            .ToListAsync();

        var firmaIds = islemler.Select(x => x.FirmaId).Distinct().ToList();
        var subeIlceleri = await _context.Ys_Subeler
            .Where(x => !x.SilindiMi && firmaIds.Contains(x.FirmaId))
            .OrderBy(x => x.SubeAdi)
            .Select(x => new
            {
                x.FirmaId,
                x.Ilce
            })
            .ToListAsync();

        var ilceler = subeIlceleri
            .GroupBy(x => x.FirmaId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(s => s.Ilce).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "");

        return new IcTesisatDevreyeAlmaListeDto
        {
            Toplam = toplam,
            Sayfa = sayfa,
            SayfaBoyutu = sayfaBoyutu,
            Islemler = islemler.Select(x => new IcTesisatDevreyeAlmaDto
            {
                Id = x.Id,
                TesisatNo = x.TesistatNo,
                YetkiliServis = x.Firma?.FirmaAdi,
                Il = x.Firma?.FaaliyetIli,
                Ilce = ilceler.TryGetValue(x.FirmaId, out var ilce) ? ilce : "",
                Tarih = x.DevreyeAlmaTarihi,
                Marka = x.CihazMarka ?? x.Marka?.MarkaAdi,
                CihazTipi = x.CihazTipi,
                CihazModeli = x.CihazModeli,
                CihazKapasite = x.CihazKapasite,
                MusteriAdi = x.MusteriAdi,
                Durum = x.Durum
            }).ToList()
        };
    }
}
