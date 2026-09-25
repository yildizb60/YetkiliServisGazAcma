using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed partial class YkcImzaAkisService
{
    private IQueryable<Ykc_ImzaSureci> MobilSurecler(int[] sirketler) => _context.Ykc_ImzaSurecleri
        .Where(s => !s.SilindiMi && s.ProviderDocumentId != null && s.ProviderDocumentId.StartsWith(MobilYkcImzaProvider.Prefix)
            && s.Talep != null && !s.Talep.SilindiMi && s.Talep.SirketId.HasValue && sirketler.Contains(s.Talep.SirketId.Value)
            && s.BelgeVersiyonu == s.Talep.Fr265BelgeVersiyonNo
            && !s.Talep.ImzaSurecleri.Any(other => !other.SilindiMi && (other.BelgeVersiyonu > s.BelgeVersiyonu
                || other.BelgeVersiyonu == s.BelgeVersiyonu && other.Id > s.Id)));

    public async Task<List<MobilImzaBelgesi>> MobilBekleyenlerAsync(int[] sirketler, int sonId, int adet, CancellationToken ct)
    {
        var records = await MobilSurecler(sirketler).AsNoTracking()
            .Where(s => s.Id > sonId && s.Talep!.Durum == YkcDurumDegerleri.SahaIsleminde
                && s.NihaiDosyaId == null && (s.Durum == YkcImzaDurumDegerleri.ImzaBekliyor || s.Durum == YkcImzaDurumDegerleri.KismiImzali))
            .OrderBy(s => s.Id).Take(adet).Select(s => new { s.Id, s.TalepId, SirketId = s.Talep!.SirketId!.Value, s.BelgeVersiyonu, s.BelgeHash }).ToListAsync(ct);
        return records.Select(s => new MobilImzaBelgesi(s.Id, s.TalepId, s.SirketId, s.BelgeVersiyonu,
            s.BelgeHash ?? "", $"Cihaz_Degisim_Formu_{s.TalepId}.pdf")).ToList();
    }

    public async Task<MobilImzaPaketi?> MobilPaketAsync(int[] sirketler, int belgeId, CancellationToken ct)
    {
        var surec = await MobilSurecler(sirketler).AsNoTracking().Include(s => s.Talep).ThenInclude(t => t!.FormDosyalari)
            .Include(s => s.Imzacilar).FirstOrDefaultAsync(s => s.Id == belgeId && s.Talep!.Durum == YkcDurumDegerleri.SahaIsleminde
                && s.NihaiDosyaId == null && (s.Durum == YkcImzaDurumDegerleri.ImzaBekliyor || s.Durum == YkcImzaDurumDegerleri.KismiImzali), ct);
        if (surec?.Talep == null || !GuvenliMobilAnahtar(surec.ProviderDocumentId)) return null;
        var draft = surec.Talep.FormDosyalari.Where(x => !x.SilindiMi && x.DosyaTuru == YkcFormDosyaTuruDegerleri.Fr265ImzayaGonderilen
            && x.BelgeHash == surec.BelgeHash).OrderByDescending(x => x.Id).FirstOrDefault();
        if (draft == null) return null;
        var pdf = await PrivateBelgeOkuAsync(draft, ct);
        if (!PdfDosyasiMi(pdf, draft) || !string.Equals(HashOlustur(pdf!), surec.BelgeHash, StringComparison.OrdinalIgnoreCase)) return null;
        var manifestPath = PrivateDocumentStorage.ExistingFile(
            _environment, _configuration, "imza-paketleri", surec.ProviderDocumentId + ".json");
        if (manifestPath is null) return null;
        var manifest = JsonSerializer.Deserialize<MobilImzaManifest>(await File.ReadAllTextAsync(manifestPath, ct));
        if (manifest == null || !string.Equals(manifest.BelgeHash, surec.BelgeHash, StringComparison.OrdinalIgnoreCase)) return null;
        return new MobilImzaPaketi(new MobilImzaBelgesi(surec.Id, surec.TalepId, surec.Talep.SirketId!.Value,
            surec.BelgeVersiyonu, surec.BelgeHash!, draft.DosyaAdi!), Convert.ToBase64String(pdf!), manifest.SablonSurumu,
            "pt", "top-left", manifest.ImzaAlanlari,
            surec.Imzacilar.Where(x => !x.SilindiMi).OrderBy(x => x.SiraNo).Select(x => new MobilImzaci(x.SiraNo, x.Rol, x.AdSoyad)).ToList());
    }

    public async Task<MobilImzaKabulSonucu> MobilImzalandiAsync(int[] sirketler, int belgeId, MobilImzaBildirimi bildirim, CancellationToken ct)
    {
        var validation = MobilImzaBildirimKontrolu.Dogrula(bildirim);
        if (validation.Hata != null) return new(400, false, validation.Hata);
        var pdf = validation.Pdf!;
        var hash = HashOlustur(pdf);
        // Serialize callbacks for one document; an accepted replay must not add another file/history entry.
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await _context.Database.SqlQuery<int>($"SELECT [Id] AS [Value] FROM [dbo].[Ykc_ImzaSurecleri] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {belgeId}").ToListAsync(ct);
        var surec = await MobilSurecler(sirketler).Include(s => s.Talep).ThenInclude(t => t!.FormDosyalari)
            .Include(s => s.Imzacilar).FirstOrDefaultAsync(s => s.Id == belgeId, ct);
        if (surec?.Talep == null) return new(404, false, "Belge bulunamadı.");
        if (surec.BelgeVersiyonu != bildirim.BelgeVersiyonu || !string.Equals(surec.BelgeHash, bildirim.KaynakBelgeHash, StringComparison.OrdinalIgnoreCase))
            return new(409, false, "Belge sürümü veya kaynak PDF özeti eşleşmiyor.");
        if (surec.Durum == YkcImzaDurumDegerleri.Tamamlandi && surec.NihaiDosyaId.HasValue)
        {
            var previous = surec.Talep.FormDosyalari.FirstOrDefault(x => x.Id == surec.NihaiDosyaId && !x.SilindiMi);
            return previous?.BelgeHash == hash ? new(200, true, "Bildirim daha önce işlendi.") : new(409, false, "Bu belge için farklı bir imzalı PDF zaten kaydedilmiş.");
        }
        if (surec.Talep.Durum != YkcDurumDegerleri.SahaIsleminde || surec.Durum is not (YkcImzaDurumDegerleri.ImzaBekliyor or YkcImzaDurumDegerleri.KismiImzali))
            return new(409, false, "Belge imza kabulüne açık değil.");
        var signers = surec.Imzacilar.Where(x => !x.SilindiMi).OrderBy(x => x.SiraNo).ToList();
        if (!signers.Select(x => x.SiraNo).SequenceEqual(bildirim.Imzalar.Select(x => x.SiraNo).Order()))
            return new(400, false, "İmzacı listesi belgenin gerekli imzacılarıyla eşleşmiyor.");
        var saved = await PrivateBelgeKaydetAsync(surec.TalepId, bildirim.DosyaAdi, "application/pdf", pdf, ct);
        var commitStarted = false;
        try
        {
            var file = new Ykc_FormDosya
            {
                TalepId = surec.TalepId, DosyaTuru = YkcFormDosyaTuruDegerleri.Fr265ImzaliNihai,
                DosyaAdi = bildirim.DosyaAdi, DosyaYolu = saved.DepolamaAnahtari, IcerikTipi = "application/pdf",
                DosyaBoyutu = pdf.LongLength, DepolamaTuru = YkcDepolamaTuruDegerleri.Private,
                BelgeHash = hash, OlusturmaTarihi = saved.KayitTarihi, OlusturanKullanici = "MobilImza"
            };
            _context.Ykc_FormDosyalari.Add(file);
            surec.NihaiDosya = file;
            surec.Durum = YkcImzaDurumDegerleri.Tamamlandi;
            surec.TamamlanmaTarihi = DateTime.Now;
            surec.SonKontrolTarihi = DateTime.Now;
            surec.HataKodu = null;
            surec.HataMesaji = null;
            foreach (var signer in signers)
            {
                signer.Durum = YkcImzaciDurumDegerleri.Imzaladi;
                signer.ImzaTarihi = bildirim.Imzalar.Single(x => x.SiraNo == signer.SiraNo).ImzaTarihi.LocalDateTime;
                signer.GuncellemeTarihi = DateTime.Now;
                signer.GuncelleyenKullanici = "MobilImza";
            }
            surec.GuncellemeTarihi = DateTime.Now;
            surec.GuncelleyenKullanici = "MobilImza";
            surec.Talep.IslemGecmisi.Add(new Ykc_IslemGecmisi
            {
                TalepId = surec.TalepId, IslemTipi = "FR265ImzaliNihaiBelgeAlindi", YeniDurum = surec.Talep.Durum,
                Aciklama = "İmzalı PDF mobil imza uygulamasından alındı.", KullaniciAdi = "MobilImza",
                OlusturmaTarihi = DateTime.Now, OlusturanKullanici = "MobilImza"
            });
            await _context.SaveChangesAsync(ct);
            commitStarted = true;
            await transaction.CommitAsync(ct);
        }
        catch
        {
            // Preserve the PDF if the commit outcome is unknown; a retry resolves its status.
            await transaction.RollbackAsync(CancellationToken.None);
            var newPath = Path.GetFullPath(Path.Combine(PrivateBelgeKoku(), saved.DepolamaAnahtari[4..].Replace('/', Path.DirectorySeparatorChar)));
            if (!commitStarted && newPath.StartsWith(Path.GetFullPath(PrivateBelgeKoku()) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                File.Delete(newPath);
            throw;
        }
        return new(200, false, "İmzalı PDF kaydedildi; belge bekleyen imza listesinden çıkarıldı.");
    }

    private static bool GuvenliMobilAnahtar(string? key) => key != null && key.StartsWith(MobilYkcImzaProvider.Prefix, StringComparison.Ordinal)
        && key.Length == MobilYkcImzaProvider.Prefix.Length + 64 && key[MobilYkcImzaProvider.Prefix.Length..].All(Uri.IsHexDigit);
}

public sealed record MobilImzaKabulSonucu(int StatusCode, bool Tekrar, string Mesaj);
