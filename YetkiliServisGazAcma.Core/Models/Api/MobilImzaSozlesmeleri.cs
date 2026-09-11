using System.ComponentModel.DataAnnotations;

namespace YetkiliServisGazAcma.Models;

public sealed record MobilImzaAlani(int ImzaciSiraNo, int Sayfa, double X, double Y, double Genislik, double Yukseklik, int KontrolNo = 0);
public sealed record MobilImzaBelgesi(int BelgeId, int TalepId, int SirketId, int BelgeVersiyonu, string BelgeHash, string DosyaAdi);
public sealed record MobilImzaPaketi(MobilImzaBelgesi Belge, string PdfBase64, string SablonSurumu,
    string KoordinatBirimi, string KoordinatBaslangici, IReadOnlyList<MobilImzaAlani> ImzaAlanlari,
    IReadOnlyList<MobilImzaci> Imzacilar);
public sealed record MobilImzaci(int SiraNo, string Rol, string? AdSoyad);

public sealed class MobilImzaBildirimi
{
    [Range(1, int.MaxValue)] public int BelgeVersiyonu { get; set; }
    [Required, RegularExpression("^[A-Fa-f0-9]{64}$")] public string KaynakBelgeHash { get; set; } = "";
    [Required, MaxLength(180)] public string DosyaAdi { get; set; } = "";
    [MaxLength(2048)] public string? DosyaUrl { get; set; }
    [Required, MaxLength(14_000_000)] public string PdfBase64 { get; set; } = "";
    [Required, MinLength(1), MaxLength(10)] public List<MobilImzaTamamlananImzaci> Imzalar { get; set; } = [];
}
public sealed record MobilImzaTamamlananImzaci(int SiraNo, DateTimeOffset ImzaTarihi);
