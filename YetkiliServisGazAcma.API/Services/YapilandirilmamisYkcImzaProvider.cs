using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.API.Services
{
    public sealed class YapilandirilmamisYkcImzaProvider : IYkcImzaProvider
    {
        private const string Mesaj = "Dijital imza sağlayıcısı henüz yapılandırılmadı.";

        public string ProviderAdi => "Yapılandırılmadı";
        public bool KullanilabilirMi => false;

        public Task<YkcImzaGonderSonuc> GonderAsync(
            YkcImzaGonderIstek istek,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(YkcImzaGonderSonuc.Basarisiz("PROVIDER_YOK", Mesaj));
        }

        public Task<YkcImzaDurumSonuc> DurumSorgulaAsync(
            string providerDocumentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(YkcImzaDurumSonuc.Basarisiz("PROVIDER_YOK", Mesaj));
        }
    }
}
