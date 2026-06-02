using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YetkiliServisApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiIntegrationOptions _options;
        private readonly ILogger<YetkiliServisApiClient> _logger;

        public YetkiliServisApiClient(
            HttpClient httpClient,
            IOptions<ApiIntegrationOptions> options,
            ILogger<YetkiliServisApiClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<List<Ys_Firma>?> ListeAsync(YetkiliServisListeIstek istek)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Yetkili servis liste");
                return null;
            }

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/yetkili-servisler/liste", istek);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Yetkili servis API liste cagrisinda basarisiz yanit dondu. StatusCode: {StatusCode}", response.StatusCode);
                    ApiClientFallback.EnsureAllowed(_options, "Yetkili servis liste");
                    return null;
                }

                var servisler = await response.Content.ReadFromJsonAsync<List<YetkiliServisApiDto>>();
                return servisler?
                    .Select(MapToFirma)
                    .OrderBy(x => x.FirmaAdi)
                    .ToList();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Yetkili servis API liste cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Yetkili servis liste");
                return null;
            }
        }

        public async Task<YetkiliServisKayitSonuc?> KayitAsync(YetkiliServisKayitIstek istek)
        {
            if (!_options.Enabled)
            {
                ApiClientFallback.EnsureAllowed(_options, "Yetkili servis kayit");
                return null;
            }

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/yetkili-servisler/kayit", istek);
                var sonuc = await response.Content.ReadFromJsonAsync<YetkiliServisKayitSonuc>();

                if (sonuc != null)
                    return sonuc;

                return new YetkiliServisKayitSonuc
                {
                    Basarili = false,
                    Mesaj = response.IsSuccessStatusCode
                        ? "API kayit cevabi okunamadi."
                        : "API kayit islemi basarisiz oldu."
                };
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Yetkili servis API kayit cagrisina ulasilamadi.");
                ApiClientFallback.EnsureAllowed(_options, "Yetkili servis kayit");
                return null;
            }
        }

        private static Ys_Firma MapToFirma(YetkiliServisApiDto dto)
        {
            return new Ys_Firma
            {
                Id = dto.Id,
                FirmaAdi = dto.FirmaAdi,
                YetkiliKisi = dto.YetkiliKisi,
                Telefon = dto.Telefon,
                Email = dto.Email,
                Adres = dto.Adres,
                FaaliyetIli = dto.FaaliyetIli,
                SirketId = dto.SirketId,
                Sirket = new Dag_Sirket
                {
                    Id = dto.SirketId,
                    SirketAdi = dto.SirketAdi
                },
                AktifMi = true,
                FirmaMarkalar = dto.Markalar
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select((marka, index) => new Ys_FirmaMarka
                    {
                        FirmaId = dto.Id,
                        MarkaId = index + 1,
                        Marka = new Ys_Marka { Id = index + 1, MarkaAdi = marka },
                        YetkiBitisTarihi = DateTime.Now.AddYears(1),
                        SilindiMi = false
                    })
                    .ToList(),
                FirmaKategoriler = dto.Kategoriler
                    .Select(kategori => new Ys_FirmaKategori
                    {
                        FirmaId = dto.Id,
                        KategoriId = kategori.Id,
                        Kategori = new UrunKategori
                        {
                            Id = kategori.Id,
                            Ad = kategori.Ad,
                            IconUrl = kategori.IconUrl,
                            AktifMi = true
                        },
                        YetkiBitisTarihi = DateTime.Now.AddYears(1),
                        SilindiMi = false
                    })
                    .ToList(),
                Subeler = string.IsNullOrWhiteSpace(dto.Ilce)
                    ? new List<Ys_Sube>()
                    : new List<Ys_Sube>
                    {
                        new()
                        {
                            FirmaId = dto.Id,
                            Il = dto.FaaliyetIli,
                            Ilce = dto.Ilce,
                            AktifMi = true,
                            SilindiMi = false
                        }
                    }
            };
        }

        public class YetkiliServisListeIstek
        {
            public string? Il { get; set; }
            public string? Ilce { get; set; }
            public int? MarkaId { get; set; }
            public int? KategoriId { get; set; }
            public string? Q { get; set; }
        }

        public class YetkiliServisKayitIstek
        {
            public string? FirmaAdi { get; set; }
            public string? YetkiliKisi { get; set; }
            public string? Telefon { get; set; }
            public string? Email { get; set; }
            public string? Adres { get; set; }
            public string? FaaliyetIli { get; set; }
            public string? VergiNo { get; set; }
            public string? VergiDairesi { get; set; }
            public string Sifre { get; set; } = string.Empty;
            public List<int> MarkaIdleri { get; set; } = new();
            public List<int> KategoriIdleri { get; set; } = new();
        }

        public class YetkiliServisKayitSonuc
        {
            public bool Basarili { get; set; }
            public string? Mesaj { get; set; }
            public int? FirmaId { get; set; }
        }

        private class YetkiliServisApiDto
        {
            public int Id { get; set; }
            public string? FirmaAdi { get; set; }
            public string? YetkiliKisi { get; set; }
            public string? Telefon { get; set; }
            public string? Email { get; set; }
            public string? Adres { get; set; }
            public string? FaaliyetIli { get; set; }
            public string? Ilce { get; set; }
            public int SirketId { get; set; }
            public string? SirketAdi { get; set; }
            public List<string> Markalar { get; set; } = new();
            public List<KategoriApiDto> Kategoriler { get; set; } = new();
        }

        private class KategoriApiDto
        {
            public int Id { get; set; }
            public string? Ad { get; set; }
            public string? IconUrl { get; set; }
        }
    }
}
