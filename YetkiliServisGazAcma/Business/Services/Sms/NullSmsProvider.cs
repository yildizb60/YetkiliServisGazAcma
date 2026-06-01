using Microsoft.Extensions.Options;

namespace YetkiliServisGazAcma.Business.Services
{
    public class NullSmsProvider : ISmsProvider
    {
        private readonly SmsOptions _options;

        public NullSmsProvider(IOptions<SmsOptions> options)
        {
            _options = options.Value;
        }

        public string ProviderName => string.IsNullOrWhiteSpace(_options.Provider)
            ? "Pending"
            : _options.Provider;

        public Task<SmsGonderimSonucu> GonderAsync(string telefon, string mesaj, string? firmaKodu = null)
        {
            if (string.Equals(_options.Provider, "Development", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new SmsGonderimSonucu(
                    true,
                    MesajId: $"DEV-{DateTime.Now:yyyyMMddHHmmss}"));
            }

            return Task.FromResult(new SmsGonderimSonucu(
                false,
                Hata: "SMS sağlayıcı API bilgisi henüz yapılandırılmadı."));
        }
    }
}
