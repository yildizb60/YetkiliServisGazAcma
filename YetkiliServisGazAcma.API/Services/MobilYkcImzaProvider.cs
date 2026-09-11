using System.Text.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

// Pull integration: the document is queued locally; no remote signature is simulated.
public sealed class MobilYkcImzaProvider(IOptions<MobilImzaOptions> settings, IWebHostEnvironment environment) : IYkcImzaProvider
{
    public const string Prefix = "MOBIL-YKC-";
    public string ProviderAdi => "Mobil İmza Entegrasyonu";
    public bool KullanilabilirMi => settings.Value.Enabled;
    public bool DemoModuMu => false;

    public async Task<YkcImzaGonderSonuc> GonderAsync(YkcImzaGonderIstek istek, CancellationToken cancellationToken = default)
    {
        if (!KullanilabilirMi || !settings.Value.SablonGecerli(istek.KontrolNo))
            return YkcImzaGonderSonuc.Basarisiz("IMZA_ALANLARI_EKSIK", "PDF şablonuna ait onaylanmış imza koordinatları yapılandırılmadı.");
        var key = Prefix + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(istek.TekrarsizIstekAnahtari)));
        var manifest = new MobilImzaManifest(settings.Value.SablonSurumu, istek.BelgeHash, settings.Value.Alanlar(istek.KontrolNo));
        var directory = Path.Combine(environment.ContentRootPath, "App_Data", "imza-paketleri");
        Directory.CreateDirectory(directory);
        // Freeze the approved coordinates alongside this exact PDF version.
        var path = Path.Combine(directory, key + ".json");
        if (!File.Exists(path))
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(manifest), cancellationToken);
                try { File.Move(temporary, path, overwrite: false); }
                catch (IOException) when (File.Exists(path)) { }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        return new YkcImzaGonderSonuc { Basarili = true, ProviderDocumentId = key };
    }

    public Task<YkcImzaDurumSonuc> DurumSorgulaAsync(string providerDocumentId, CancellationToken cancellationToken = default)
        => Task.FromResult(new YkcImzaDurumSonuc { Basarili = true, Durum = YkcImzaDurumDegerleri.ImzaBekliyor });
}

public sealed record MobilImzaManifest(string SablonSurumu, string BelgeHash, List<MobilImzaAlani> ImzaAlanlari);
