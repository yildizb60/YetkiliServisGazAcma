using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace YetkiliServisGazAcma.Business.Services.Online
{
    public class OnlineCihazBilgileriClient
    {
        private const string SoapAction = "http://tempuri.org/IOnline/YS_CihazBilgileriGetir";

        private readonly HttpClient _httpClient;
        private readonly OnlineServiceOptions _options;
        private readonly ILogger<OnlineCihazBilgileriClient> _logger;

        public OnlineCihazBilgileriClient(
            HttpClient httpClient,
            IOptions<OnlineServiceOptions> options,
            ILogger<OnlineCihazBilgileriClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<OnlineCihazBilgileriSonuc> YSCihazBilgileriGetirAsync(
            string? firma,
            long tesisatNo,
            long sozlesmeNo,
            CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
                return OnlineCihazBilgileriSonuc.Basarisiz("Online cihaz servisi kapali.");

            var endpoint = string.IsNullOrWhiteSpace(_options.Endpoint)
                ? "http://onlinesvc.marmaragaz.com.tr/Test/Online.svc"
                : _options.Endpoint.Trim();

            var firmaKodu = string.IsNullOrWhiteSpace(firma) ? _options.Firma : firma;
            if (string.IsNullOrWhiteSpace(firmaKodu))
                return OnlineCihazBilgileriSonuc.Basarisiz("Firma kodu bulunamadi.");

            try
            {
                var envelope = BuildEnvelope(firmaKodu.Trim(), tesisatNo, sozlesmeNo);
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(envelope, Encoding.UTF8, "text/xml")
                };

                request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{SoapAction}\"");
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/xml; charset=utf-8");

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseXml = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Online cihaz servisi HTTP {StatusCode} dondu. Body: {Body}",
                        (int)response.StatusCode,
                        responseXml);

                    return OnlineCihazBilgileriSonuc.Basarisiz("Online cihaz servisi yanit vermedi.");
                }

                return ParseResponse(responseXml);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return OnlineCihazBilgileriSonuc.Basarisiz("Online cihaz servisi zaman asimina ugradi.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Online cihaz servisi cagrilirken hata olustu.");
                return OnlineCihazBilgileriSonuc.Basarisiz("Online cihaz servisine baglanilamadi.");
            }
        }

        private static string BuildEnvelope(string firma, long tesisatNo, long sozlesmeNo)
        {
            XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
            XNamespace temp = "http://tempuri.org/";

            var document = new XDocument(
                new XElement(soap + "Envelope",
                    new XAttribute(XNamespace.Xmlns + "s", soap),
                    new XElement(soap + "Body",
                        new XElement(temp + "YS_CihazBilgileriGetir",
                            new XElement(temp + "Firma", firma),
                            new XElement(temp + "tesisatNo", tesisatNo),
                            new XElement(temp + "sozlesmeNo", sozlesmeNo)))));

            return document.ToString(SaveOptions.DisableFormatting);
        }

        private static OnlineCihazBilgileriSonuc ParseResponse(string responseXml)
        {
            var document = XDocument.Parse(responseXml);
            var fault = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Fault");
            if (fault != null)
                return OnlineCihazBilgileriSonuc.Basarisiz(ReadDescendant(fault, "faultstring") ?? "SOAP servisi hata dondu.");

            var result = document.Descendants()
                .FirstOrDefault(x => x.Name.LocalName == "YS_CihazBilgileriGetirResult");

            if (result == null)
                return OnlineCihazBilgileriSonuc.Basarisiz("Online cihaz servisi beklenen formatta yanit dondurmedi.");

            var hataKodu = ReadInt(result, "HataKodu") ?? 0;
            var hataMesaji = ReadChild(result, "HataMesaji");
            var cihazlar = result.Descendants()
                .Where(x => x.Name.LocalName == "CihazDto")
                .Select(x => new OnlineCihazDto
                {
                    CihazKapasite = ReadDouble(x, "cihazkapasite"),
                    CihazMarka = ReadChild(x, "cihazmarka"),
                    CihazTipi = ReadChild(x, "cihaztipi"),
                    CihazTipKodu = ReadChild(x, "cihaztipkodu"),
                    ProjeNo = ReadChild(x, "projeno"),
                    TesisatNo = ReadLong(x, "tesisatno")
                })
                .ToList();

            return new OnlineCihazBilgileriSonuc
            {
                Basarili = hataKodu == 0,
                HataKodu = hataKodu,
                HataMesaji = hataMesaji,
                Adres = ReadChild(result, "adres"),
                CariAd = ReadChild(result, "cariad"),
                CariKod = ReadLong(result, "carikod"),
                SayacNo = ReadLong(result, "sayacno"),
                SozlesmeNo = ReadLong(result, "sozlesmeno"),
                TesisatNo = ReadLong(result, "tesisatno"),
                Cihazlar = cihazlar
            };
        }

        private static string? ReadChild(XElement parent, string localName)
        {
            var value = parent.Elements().FirstOrDefault(x => x.Name.LocalName == localName)?.Value;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string? ReadDescendant(XElement parent, string localName)
        {
            var value = parent.Descendants().FirstOrDefault(x => x.Name.LocalName == localName)?.Value;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static int? ReadInt(XElement parent, string localName)
        {
            return int.TryParse(ReadChild(parent, localName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        private static long? ReadLong(XElement parent, string localName)
        {
            return long.TryParse(ReadChild(parent, localName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        private static double? ReadDouble(XElement parent, string localName)
        {
            return double.TryParse(ReadChild(parent, localName), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }
    }

    public class OnlineCihazBilgileriSonuc
    {
        public bool Basarili { get; set; }
        public int HataKodu { get; set; }
        public string? HataMesaji { get; set; }
        public string? Adres { get; set; }
        public string? CariAd { get; set; }
        public long? CariKod { get; set; }
        public long? SayacNo { get; set; }
        public long? SozlesmeNo { get; set; }
        public long? TesisatNo { get; set; }
        public List<OnlineCihazDto> Cihazlar { get; set; } = new();

        public static OnlineCihazBilgileriSonuc Basarisiz(string mesaj)
        {
            return new OnlineCihazBilgileriSonuc
            {
                Basarili = false,
                HataMesaji = mesaj
            };
        }
    }

    public class OnlineCihazDto
    {
        public double? CihazKapasite { get; set; }
        public string? CihazMarka { get; set; }
        public string? CihazTipi { get; set; }
        public string? CihazTipKodu { get; set; }
        public string? ProjeNo { get; set; }
        public long? TesisatNo { get; set; }
    }
}
