using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace YetkiliServisGazAcma.Business.Services
{
    public sealed class SertifikaliFirmaKimlikOptions
    {
        public bool Enabled { get; set; }
        public string Provider { get; set; } = "Yapilandirilmamis";
    }

    public interface ISertifikaliFirmaKimlikProvider
    {
        string ProviderAdi { get; }
        bool KullanilabilirMi { get; }

        Task<SertifikaliFirmaKimlikSonucu> KimlikDogrulaAsync(
            string kullaniciAdi,
            string sifre,
            CancellationToken cancellationToken = default);

        Task<SertifikaliFirmaKimlikTamamlamaSonucu> DogrulamayiTamamlaAsync(
            string dogrulamaReferansi,
            CancellationToken cancellationToken = default);
    }

    public sealed class SertifikaliFirmaKimlikSonucu
    {
        public bool Basarili { get; init; }
        public string Mesaj { get; init; } = string.Empty;
        public string? YerelKullaniciAdi { get; init; }
        public string? Telefon { get; init; }
        public string? SertifikaNo { get; init; }
        public string? DogrulamaReferansi { get; init; }
        public bool TelefonDogrulamasiGerekliMi { get; init; }

        public static SertifikaliFirmaKimlikSonucu Basarisiz(string mesaj)
        {
            return new SertifikaliFirmaKimlikSonucu { Mesaj = mesaj };
        }
    }

    public sealed record SertifikaliFirmaKimlikTamamlamaSonucu(bool Basarili, string Mesaj);

    public sealed class YapilandirilmamisSertifikaliFirmaKimlikProvider : ISertifikaliFirmaKimlikProvider
    {
        public string ProviderAdi => "Yapılandırılmamış sertifikalı firma kimlik servisi";
        public bool KullanilabilirMi => false;

        public Task<SertifikaliFirmaKimlikSonucu> KimlikDogrulaAsync(
            string kullaniciAdi,
            string sifre,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(SertifikaliFirmaKimlikSonucu.Basarisiz(
                "Sertifikalı firma kimlik servisi henüz yapılandırılmadı."));
        }

        public Task<SertifikaliFirmaKimlikTamamlamaSonucu> DogrulamayiTamamlaAsync(
            string dogrulamaReferansi,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new SertifikaliFirmaKimlikTamamlamaSonucu(
                false,
                "Sertifikalı firma kimlik servisi henüz yapılandırılmadı."));
        }
    }

    public static class SertifikaliFirmaKimlikServiceCollectionExtensions
    {
        public static IServiceCollection AddSertifikaliFirmaKimlikServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<SertifikaliFirmaKimlikOptions>(
                configuration.GetSection("SertifikaliFirmaKimlik"));
            services.AddSingleton<ISertifikaliFirmaKimlikProvider,
                YapilandirilmamisSertifikaliFirmaKimlikProvider>();
            return services;
        }
    }
}
