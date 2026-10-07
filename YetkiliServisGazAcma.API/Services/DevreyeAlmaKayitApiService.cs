using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class DevreyeAlmaKayitApiService(
    AppDbContext context, YetkiliServisIlkKurulumService ilkKurulum, DevreyeAlmaYetkiDogrulamaService yetki)
{
    private readonly AppDbContext _context = context;
    private readonly YetkiliServisIlkKurulumService _ilkKurulumService = ilkKurulum;
    private readonly DevreyeAlmaYetkiDogrulamaService _yetki = yetki;

    public async Task<YsDevreyeAlmaIslemSonucDto> KaydetAsync(YsDevreyeAlmaKaydetDto? dto, AppKullanici kullanici)
    {
        if (!kullanici.FirmaId.HasValue || kullanici.KullaniciTipi != KullaniciTipiDegerleri.YetkiliServis)
            return KayitHatasi("Yetkili servis hesabı gereklidir.");

        var kurulum = await _ilkKurulumService.GetirAsync(kullanici.FirmaId.Value);
        if (kurulum.zorunluMu && !kurulum.tamamlandiMi)
        {
            return new YsDevreyeAlmaIslemSonucDto
            {
                Basarili = false,
                Mesaj = "Ilk kurulum tamamlanmadan cihaz devreye alma islemi yapilamaz.",
                RedirectUrl = "/ys-panel/ilk-kurulum"
            };
        }

        if (string.IsNullOrWhiteSpace(dto?.SorguReferansi) || dto.SorguReferansi.Length != 64)
        {
            return new YsDevreyeAlmaIslemSonucDto
            {
                Basarili = false,
                Mesaj = "Önce tesisatı sorgulayıp servisten gelen cihazı seçin.",
                RedirectUrl = "/ys-devreyeal"
            };
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (!await _context.Ys_Firmalar.AnyAsync(x => x.Id == kullanici.FirmaId.Value && !x.SilindiMi && x.AktifMi))
            return KayitHatasi("Firma kaydınız aktif olmadığı için cihaz devreye alınamaz.");
        if (!await _yetki.GecerliYetkiBelgesiVarAsync(kullanici.FirmaId.Value))
            return KayitHatasi("Cihaz devreye alma işlemi için geçerli, onaylı yetki belgeniz bulunmalıdır.");

        var sorguKaydi = await _context.Ys_DevreyeAlmaSorguKayitlari
            .FirstOrDefaultAsync(x => x.Referans == dto.SorguReferansi
                && x.KullaniciId == kullanici.Id
                && x.FirmaId == kullanici.FirmaId.Value
                && x.GecerlilikTarihi > DateTime.UtcNow
                && x.DevreyeAlmaId == null);
        if (sorguKaydi == null)
            return KayitHatasi("Cihaz sorgusu geçersiz, süresi dolmuş veya bu cihaz zaten kaydedilmiş. Tesisatı yeniden sorgulayın.");

        var dagitimSirketiId = await _context.Ys_Firmalar
            .Where(x => x.Id == kullanici.FirmaId.Value && !x.SilindiMi)
            .Select(x => x.SirketId)
            .FirstOrDefaultAsync();
        if (dagitimSirketiId == 0 || dagitimSirketiId != sorguKaydi.DagitimSirketiId)
            return KayitHatasi("Firma kapsamı sorgu kaydıyla eşleşmiyor. Tesisatı yeniden sorgulayın.");

        YsDevreyeAlmaKaynak? kaynak;
        try { kaynak = JsonSerializer.Deserialize<YsDevreyeAlmaKaynak>(sorguKaydi.KaynakJson); }
        catch (JsonException) { kaynak = null; }
        if (kaynak == null)
            return KayitHatasi("Kaynak cihaz bilgisi doğrulanamadı. Tesisatı yeniden sorgulayın.");

        var zorunluAlanHatasi = ZorunluAlanlariKontrolEt(dto, kaynak);
        if (!string.IsNullOrWhiteSpace(zorunluAlanHatasi))
            return KayitHatasi(zorunluAlanHatasi);

        var marka = await _yetki.MarkaBulAsync(kaynak.CihazMarka);
        if (marka == null)
        {
            return KayitHatasi($"{kaynak.CihazMarka} markasında işlem yetkiniz yok.");
        }

        var yetkiVar = await _yetki.FirmaMarkaYetkisiVarAsync(kullanici.FirmaId.Value, marka.Id);
        if (!yetkiVar)
        {
            return KayitHatasi($"{marka.MarkaAdi} markasında işlem yetkiniz yok.");
        }

        var seriAnahtari = YsDevreyeAlmaKaynak.SeriAnahtari(dagitimSirketiId, kaynak.TesisatNo, dto.SeriNo);
        if (!await _yetki.FirmaKategoriYetkisiVarAsync(kullanici.FirmaId.Value, kaynak.CihazTipi))
            return KayitHatasi($"{kaynak.CihazTipi} cihaz tipinde işlem yetkiniz yok.");

        var seriNo = dto.SeriNo!.Trim();
        var mukerrer = await _context.Ys_DevreyeAlmalar.AnyAsync(x => !x.SilindiMi
            && (x.KaynakCihazAnahtari == sorguKaydi.KaynakCihazAnahtari
                || x.SeriAnahtari == seriAnahtari));
        if (!mukerrer)
        {
            var oncekiSeriler = await _context.Ys_DevreyeAlmalar
                .Where(x => !x.SilindiMi && x.SeriAnahtari == null
                    && x.TesistatNo == kaynak.TesisatNo && x.SeriNo != null
                    && x.Firma != null && x.Firma.SirketId == dagitimSirketiId)
                .Select(x => x.SeriNo)
                .ToListAsync();
            mukerrer = oncekiSeriler.Any(x => YsDevreyeAlmaKaynak.SeriAnahtari(
                dagitimSirketiId, kaynak.TesisatNo, x) == seriAnahtari);
        }
        if (mukerrer)
            return KayitHatasi("Bu kaynak cihaz veya seri numarası için devreye alma kaydı zaten var.");

        var islem = new Ys_DevreyeAlma
        {
            FirmaId = kullanici.FirmaId.Value,
            MarkaId = marka.Id,
            TesistatNo = kaynak.TesisatNo,
            AboneNo = kaynak.AboneNo,
            UygunlukBelgeNo = null,
            UygunlukTarihi = null,
            MusteriAdi = kaynak.MusteriAdi,
            // TC bilgisi mevcut online servis sozlesmesinde bulunmuyor.
            // Istemciden gelen gizli alan guvenilir kaynak kabul edilmez.
            MusteriTcNo = null,
            MusteriTelefon = kaynak.MusteriTelefon,
            Adres = kaynak.Adres,
            CihazTipi = kaynak.CihazTipi,
            CihazMarka = marka.MarkaAdi ?? kaynak.CihazMarka,
            CihazModeli = dto.CihazModeli?.Trim(),
            CihazKapasite = kaynak.CihazKapasite,
            SeriNo = seriNo,
            KaynakCihazAnahtari = sorguKaydi.KaynakCihazAnahtari,
            SeriAnahtari = seriAnahtari,
            TeknisyenAdi = dto.TeknisyenAdi?.Trim(),
            TeknisyenYetkiBelgesiNo = dto.TeknisyenYetkiBelgesiNo?.Trim(),
            DevreyeAlmaTarihi = DateTime.Now,
            Notlar = dto.Notlar,
            Durum = DevreyeAlmaDurumDegerleri.Tamamlandi,
            OlusturmaTarihi = DateTime.Now,
            OlusturanKullanici = kullanici.UserName ?? "",
            SilindiMi = false
        };

        try
        {
            _context.Ys_DevreyeAlmalar.Add(islem);
            await _context.SaveChangesAsync();
            sorguKaydi.DevreyeAlmaId = islem.Id;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql
            && sql.Number is 2601 or 2627)
        {
            return KayitHatasi("Bu kaynak cihaz veya seri numarası için devreye alma kaydı zaten var.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number == 1205)
        {
            return KayitHatasi("Cihaz kaydı eşzamanlı bir işlem nedeniyle tamamlanamadı. Lütfen yeniden deneyin.");
        }

        return new YsDevreyeAlmaIslemSonucDto
        {
            Basarili = true,
            Mesaj = "Cihaz devreye alma islemi tamamlandi!",
            Id = islem.Id,
            RedirectUrl = "/ys-devreyeal/gecmis"
        };
    }

    private static YsDevreyeAlmaIslemSonucDto KayitHatasi(string mesaj) => new()
    {
        Basarili = false,
        Mesaj = mesaj,
        RedirectUrl = "/ys-devreyeal"
    };

    private static string? ZorunluAlanlariKontrolEt(YsDevreyeAlmaKaydetDto dto, YsDevreyeAlmaKaynak kaynak)
    {
        var eksikler = new List<string>();

        if (string.IsNullOrWhiteSpace(kaynak.TesisatNo))
            eksikler.Add("tesisat no");
        if (string.IsNullOrWhiteSpace(kaynak.AboneNo) && string.IsNullOrWhiteSpace(kaynak.SozlesmeNo))
            eksikler.Add("abone veya sozlesme no");
        if (string.IsNullOrWhiteSpace(kaynak.MusteriAdi))
            eksikler.Add("musteri adi");
        if (string.IsNullOrWhiteSpace(kaynak.Adres))
            eksikler.Add("adres");
        if (string.IsNullOrWhiteSpace(kaynak.CihazTipi))
            eksikler.Add("cihaz tipi");
        if (string.IsNullOrWhiteSpace(kaynak.CihazMarka))
            eksikler.Add("cihaz markasi");
        if (string.IsNullOrWhiteSpace(dto.CihazModeli))
            eksikler.Add("cihaz modeli");
        if (string.IsNullOrWhiteSpace(dto.SeriNo))
            eksikler.Add("seri no");
        if (string.IsNullOrWhiteSpace(dto.TeknisyenAdi))
            eksikler.Add("teknisyen adi");
        if (string.IsNullOrWhiteSpace(dto.TeknisyenYetkiBelgesiNo))
            eksikler.Add("teknisyen yetki belgesi no");

        return eksikler.Count == 0
            ? null
            : "Devreye alma kaydi icin zorunlu alanlar eksik: " + string.Join(", ", eksikler) + ".";
    }
}
