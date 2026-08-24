using System.Security.Cryptography;
using System.Text.RegularExpressions;
using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.API.Services
{
    public sealed partial class DemoYkcImzaProvider : IYkcImzaProvider
    {
        public string ProviderAdi => "Demo İmza Sağlayıcısı";
        public bool KullanilabilirMi => true;
        public bool DemoModuMu => true;

        public Task<YkcImzaGonderSonuc> GonderAsync(
            YkcImzaGonderIstek istek,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (istek.TalepId <= 0 || istek.BelgeBytes.Length == 0)
                return Task.FromResult(YkcImzaGonderSonuc.Basarisiz("DEMO_BELGE_GECERSIZ", "Demo imza için geçerli bir FR265 belgesi gerekir."));

            var gercekHash = Convert.ToHexString(SHA256.HashData(istek.BelgeBytes));
            if (!string.IsNullOrWhiteSpace(istek.BelgeHash)
                && !string.Equals(gercekHash, istek.BelgeHash.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(YkcImzaGonderSonuc.Basarisiz("DEMO_HASH_UYUSMAZLIGI", "FR265 belge özeti doğrulanamadı."));
            }

            var belgeNo = $"DEMO-YKC-{istek.TalepId}-V{Math.Max(istek.BelgeVersiyonu, 1)}-{gercekHash[..12]}";
            return Task.FromResult(new YkcImzaGonderSonuc
            {
                Basarili = true,
                ProviderDocumentId = belgeNo
            });
        }

        public Task<YkcImzaDurumSonuc> DurumSorgulaAsync(
            string providerDocumentId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var temizBelgeNo = providerDocumentId?.Trim() ?? string.Empty;
            var eslesme = DemoBelgeNoRegex().Match(temizBelgeNo);
            if (!eslesme.Success
                || !int.TryParse(eslesme.Groups["talepId"].Value, out var talepId)
                || !int.TryParse(eslesme.Groups["versiyon"].Value, out var versiyon))
            {
                return Task.FromResult(YkcImzaDurumSonuc.Basarisiz("DEMO_BELGE_BULUNAMADI", "Demo imza belge numarası geçersiz."));
            }

            var tamamlanmaTarihi = DateTime.Now;

            return Task.FromResult(new YkcImzaDurumSonuc
            {
                Basarili = true,
                Durum = YkcImzaDurumDegerleri.Tamamlandi,
                Imzacilar = Enumerable.Range(1, 3)
                    .Select(siraNo => new YkcImzaProviderImzaciDurumu
                    {
                        SiraNo = siraNo,
                        Durum = YkcImzaciDurumDegerleri.Imzaladi,
                        ImzaTarihi = tamamlanmaTarihi
                    })
                    .ToList()
            });
        }

        [GeneratedRegex("^DEMO-YKC-(?<talepId>\\d+)-V(?<versiyon>\\d+)-(?<hash>[A-F0-9]{12})$", RegexOptions.CultureInvariant)]
        private static partial Regex DemoBelgeNoRegex();
    }
}
