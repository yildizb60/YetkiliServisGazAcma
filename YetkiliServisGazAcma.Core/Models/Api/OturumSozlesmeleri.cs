using System.ComponentModel.DataAnnotations;

namespace YetkiliServisGazAcma.Models;

public sealed record OturumKullaniciDto(string Id, string? UserName, string? Email,
    string? AdSoyad, string? PhoneNumber, int KullaniciTipi, int? FirmaId, int? SirketId,
    string[] Roller);

public sealed class OturumSonucu
{
    public bool Basarili { get; set; }
    public string? Mesaj { get; set; }
    public string? Token { get; set; }
    public DateTimeOffset? Bitis { get; set; }
    public OturumKullaniciDto? Kullanici { get; set; }
    public string? Dogrulama { get; set; }
}

public sealed record GirisIstegi([Required, StringLength(256)] string Email, [Required, StringLength(1024)] string Sifre);
public sealed record SmsDogrulamaIstegi([Required, StringLength(4096)] string Dogrulama, [Required, StringLength(8)] string Kod);
public sealed record SifreUnuttumIstegi([Required, StringLength(256)] string KullaniciAdi);
public sealed record SifreYenileIstegi([Required, StringLength(4096)] string Dogrulama,
    [Required, StringLength(8)] string Kod, [Required, StringLength(1024)] string YeniSifre);
public sealed record ProfilGuncelleIstegi([Required, StringLength(150)] string AdSoyad,
    [Required, EmailAddress, StringLength(256)] string Email, [Phone, StringLength(30)] string? PhoneNumber);
public sealed record SifreDegistirIstegi([Required, StringLength(1024)] string MevcutSifre,
    [Required, StringLength(1024)] string YeniSifre);
