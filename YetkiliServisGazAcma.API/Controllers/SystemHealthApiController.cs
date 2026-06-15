using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Business.Services.Online;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/health")]
    public class SystemHealthApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly SmsOptions _smsOptions;
        private readonly OnlineServiceOptions _onlineOptions;

        public SystemHealthApiController(
            AppDbContext context,
            IOptions<SmsOptions> smsOptions,
            IOptions<OnlineServiceOptions> onlineOptions)
        {
            _context = context;
            _smsOptions = smsOptions.Value;
            _onlineOptions = onlineOptions.Value;
        }

        [HttpGet]
        [HttpGet("/health")]
        public async Task<IActionResult> Get()
        {
            var db = await DatabaseDurumuAsync();
            var sms = SmsDurumu();
            var online = OnlineServisDurumu();

            var bilesenler = new Dictionary<string, SaglikBileseni>
            {
                ["api"] = new(true, "API ayakta."),
                ["database"] = db,
                ["sms"] = sms,
                ["onlineService"] = online
            };

            var saglikli = bilesenler.Values.All(x => x.Saglikli);
            var genelDurum = saglikli
                ? "Healthy"
                : "Unhealthy";

            var response = new SistemSaglikCevap
            {
                Durum = genelDurum,
                ZamanUtc = DateTime.UtcNow,
                Bilesenler = bilesenler
            };

            return saglikli
                ? Ok(response)
                : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }

        private async Task<SaglikBileseni> DatabaseDurumuAsync()
        {
            try
            {
                var canConnect = await _context.Database.CanConnectAsync(HttpContext.RequestAborted);
                return canConnect
                    ? new SaglikBileseni(true, "Veritabani baglantisi basarili.")
                    : new SaglikBileseni(false, "Veritabani baglantisi kurulamadi.");
            }
            catch (Exception ex)
            {
                return new SaglikBileseni(false, "Veritabani kontrolu sirasinda hata olustu.", ex.GetType().Name);
            }
        }

        private SaglikBileseni SmsDurumu()
        {
            if (!_smsOptions.Enabled)
                return new SaglikBileseni(true, "SMS kapali. Canli gonderim yapilmaz.");

            if (_smsOptions.TestMode)
                return new SaglikBileseni(true, "SMS test modunda. Kod ekranda gosterilir.");

            if (string.Equals(_smsOptions.Provider, "AhlatciSms", StringComparison.OrdinalIgnoreCase)
                && (!string.IsNullOrWhiteSpace(_smsOptions.BearerToken)
                    || (!string.IsNullOrWhiteSpace(_smsOptions.Username)
                        && !string.IsNullOrWhiteSpace(_smsOptions.Password))))
            {
                return new SaglikBileseni(true, "Ahlatci SMS ayarlari hazir.");
            }

            return new SaglikBileseni(false, "Canli SMS icin AhlatciSms kullanici bilgileri veya bearer token eksik.");
        }

        private SaglikBileseni OnlineServisDurumu()
        {
            if (!_onlineOptions.Enabled)
                return new SaglikBileseni(true, "Online cihaz servisi kapali.");

            return Uri.TryCreate(_onlineOptions.Endpoint, UriKind.Absolute, out _)
                ? new SaglikBileseni(true, "Online cihaz servisi endpoint ayari mevcut.")
                : new SaglikBileseni(false, "Online cihaz servisi endpoint ayari gecersiz.");
        }

        public class SistemSaglikCevap
        {
            public string Durum { get; set; } = "Healthy";
            public DateTime ZamanUtc { get; set; }
            public Dictionary<string, SaglikBileseni> Bilesenler { get; set; } = new();
        }

        public record SaglikBileseni(bool Saglikli, string Mesaj, string? Detay = null);
    }
}
