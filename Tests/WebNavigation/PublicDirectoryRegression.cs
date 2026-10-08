using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Controllers;

internal static class PublicDirectoryRegression
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
            passed++;
        }

        using var handler = new PublicHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://fixture.invalid/") };
        var client = new YetkiliServisApiClient(http,
            Options.Create(new ApiIntegrationOptions { Enabled = true }),
            NullLogger<YetkiliServisApiClient>.Instance);
        var directory = Prepare(new YetkiliServislerController(client));
        var result = (ViewResult)await directory.Index("Request city", "Request district", 42, 999, "query", -4, 500);
        var model = (YetkiliServisRehberEkranDto)result.Model!;
        Check(handler.Paths.SequenceEqual(["/api/yetkili-servisler/rehber-ekrani"]),
            "Directory obtains filters, normalized query and rows in one anonymous request");
        Check(handler.LastBody.GetProperty("page").GetInt32() == -4
            && handler.LastBody.GetProperty("pageSize").GetInt32() == 500
            && handler.LastBody.GetProperty("kategoriId").GetInt32() == 999,
            "MVC forwards category and pagination without applying API rules");
        Check(model.Sonuc.Page == 1 && model.Sonuc.PageSize == 100 && model.Sonuc.TotalCount == 1
            && model.Sorgu.KategoriId == null && model.Sorgu.Il == "API city"
            && model.Sorgu.Ilce == "API district" && model.Sorgu.MarkaId == 42 && model.Sorgu.Q == "API query",
            "Directory uses the normalized query returned by the API");
        Check(model.Sonuc.Items.Single() is { FirmaAdi: "Service", Ilce: "District", SirketAdi: "Company" }
            && model.Sonuc.Items.Single().Markalar.SequenceEqual(["Brand"])
            && model.Sonuc.Items.Single().Kategoriler.Single().IconUrl == "/category.png",
            "Shared directory DTO retains display data without entity reconstruction");
        Check(model.Filtreler.Kategoriler.Select(x => x.Id).SequenceEqual([8, 2]),
            "MVC preserves API category ordering");

        handler.Paths.Clear();
        var legacy = await client.ListeSayfaliAsync(new() { Page = 2, PageSize = 10 });
        Check(legacy is { Page: 2, TotalCount: 42, TotalPages: 5, Items.Count: 1 }
            && handler.Paths.Single() == "/api/yetkili-servisler/liste",
            "Legacy public listing remains available with a shared typed response");
        var filters = await client.FiltreSecenekleriAsync("City");
        Check(filters?.Kategoriler.Count == 2
            && handler.Paths.Last() == "/api/yetkili-servisler/filtre-secenekleri",
            "Legacy filter options remain available");

        handler.Paths.Clear();
        var registration = Prepare(new KayitController(client));
        await registration.YetkiliServis();
        Check(handler.Paths.SequenceEqual(["/api/yetkili-servisler/basvuru-secenekleri"]),
            "Registration loads brands, categories, cities and codes in one request");
        Check(((List<string>)registration.ViewBag.Sehirler).Single() == "API city"
            && ((Dictionary<string, string>)registration.ViewBag.SehirFirmaKodlari)["API city"] == "API_CODE"
            && ((List<YetkiliServisMarkaSecenekDto>)registration.ViewBag.Markalar).Single().Id == 42,
            "Registration renders the API options instead of local city configuration");

        handler.Paths.Clear();
        registration = Prepare(new KayitController(client));
        var application = Application();
        var mismatch = (ViewResult)await registration.YetkiliServis(application, "different");
        Check(ReferenceEquals(mismatch.Model, application)
            && handler.Paths.SequenceEqual(["/api/yetkili-servisler/basvuru-secenekleri"]),
            "Password confirmation mismatch never submits registration");
        Check((string)registration.ViewBag.SeciliIlce == " District "
            && ((IEnumerable<int>)registration.ViewBag.SeciliMarkaIdleri).SequenceEqual([42])
            && ((IEnumerable<int>)registration.ViewBag.SeciliKategoriIdleri).SequenceEqual([8]),
            "Password mismatch retains district and selected options");

        handler.Paths.Clear();
        handler.RegistrationStatus = HttpStatusCode.BadRequest;
        registration = Prepare(new KayitController(client));
        application = Application();
        var rejected = (ViewResult)await registration.YetkiliServis(application, application.Sifre);
        Check(ReferenceEquals(rejected.Model, application)
            && (string)registration.ViewBag.Hata == "Canonical validation"
            && handler.Paths.SequenceEqual(["/api/yetkili-servisler/kayit", "/api/yetkili-servisler/basvuru-secenekleri"]),
            "Canonical API validation redisplays the same form with refreshed options");
        Check(handler.RegistrationBody.GetProperty("firmaAdi").GetString() == "Applicant"
            && handler.RegistrationBody.GetProperty("ilce").GetString() == "District"
            && handler.RegistrationBody.GetProperty("markaIdleri")[0].GetInt32() == 42
            && !handler.RegistrationBody.TryGetProperty("sifreTekrar", out _),
            "Registration sends the shared contract and keeps password confirmation in MVC");

        handler.Paths.Clear();
        handler.RegistrationStatus = HttpStatusCode.ServiceUnavailable;
        registration = Prepare(new KayitController(client));
        application = Application();
        var unavailable = (ViewResult)await registration.YetkiliServis(application, application.Sifre);
        Check(ReferenceEquals(unavailable.Model, application) && registration.ViewBag.Hata != null
            && ((IEnumerable<int>)registration.ViewBag.SeciliKategoriIdleri).SequenceEqual([8]),
            "Transport failures retain registration form state");

        handler.Paths.Clear();
        handler.RegistrationStatus = HttpStatusCode.OK;
        registration = Prepare(new KayitController(client));
        application = Application();
        var success = (RedirectResult)await registration.YetkiliServis(application, application.Sifre);
        Check(success.Url == "/giris" && registration.TempData["KayitMesaj"] != null
            && handler.Paths.SequenceEqual(["/api/yetkili-servisler/kayit"]),
            "Successful registration redirects without unnecessary options requests");

        handler.OptionsStatus = HttpStatusCode.ServiceUnavailable;
        registration = Prepare(new KayitController(client));
        Check(await registration.YetkiliServis() is ViewResult
            && registration.ViewBag.ApiUyari != null
            && ((List<string>)registration.ViewBag.Sehirler).Count == 0,
            "Options outages render a warning and never fall back to local city data");

        handler.DirectoryStatus = HttpStatusCode.ServiceUnavailable;
        directory = Prepare(new YetkiliServislerController(client));
        var failedDirectory = (ViewResult)await directory.Index(null, null, null, null, null, -4, 0);
        Check(failedDirectory.Model is YetkiliServisRehberEkranDto
            { Sonuc: { Page: 1, PageSize: 20, Items.Count: 0 } }
            && directory.TempData["Hata"] != null,
            "Directory outage renders an empty safe view with an API warning");
        Check(!handler.SentAuthorization, "Every public request remains anonymous");
        Console.WriteLine($"{passed} public directory and registration checks passed.");
    }

    private static YetkiliServisBasvuruDto Application() => new()
    {
        FirmaAdi = "Applicant", YetkiliKisi = "Contact", Telefon = "05550000000",
        Email = "applicant@example.invalid", VergiNo = "1000000001", TcKimlikNo = "10000000000",
        FaaliyetIli = "API city", Ilce = " District ", Adres = "Address",
        Sifre = "FixtureOnly123!", MarkaIdleri = [42], KategoriIdleri = [8]
    };

    private static T Prepare<T>(T controller) where T : Controller
    {
        var context = new DefaultHttpContext();
        controller.ControllerContext = new() { HttpContext = context };
        controller.TempData = new TempDataDictionary(context, new EmptyTempData());
        return controller;
    }

    private sealed class EmptyTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class PublicHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public JsonElement LastBody { get; private set; }
        public JsonElement RegistrationBody { get; private set; }
        public bool SentAuthorization { get; private set; }
        public HttpStatusCode RegistrationStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode OptionsStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode DirectoryStatus { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            SentAuthorization |= request.Headers.Authorization != null;
            LastBody = await request.Content!.ReadFromJsonAsync<JsonElement>(token);
            var row = new YetkiliServisDto
            {
                Id = 12, FirmaAdi = "Service", Ilce = "District", SirketAdi = "Company",
                Markalar = ["Brand"], Kategoriler = [new() { Id = 8, Ad = "Category", IconUrl = "/category.png" }]
            };
            var filters = new YetkiliServisFiltreSecenekleriDto
            {
                Markalar = [new() { Id = 42, MarkaAdi = "Brand" }],
                Kategoriler = [new() { Id = 8, Ad = "Z", AktifMi = true }, new() { Id = 2, Ad = "A", AktifMi = true }]
            };
            switch (path)
            {
                case "/api/yetkili-servisler/rehber-ekrani":
                    return Response(DirectoryStatus, new YetkiliServisRehberEkranDto
                    {
                        Sorgu = new() { Il = "API city", Ilce = "API district", MarkaId = 42, Q = "API query", Page = 1, PageSize = 100 },
                        Filtreler = filters,
                        Sonuc = new() { Page = 1, PageSize = 100, TotalCount = 1, Items = [row] }
                    });
                case "/api/yetkili-servisler/liste":
                    return Response(HttpStatusCode.OK, new YetkiliServisSayfaliDto { Page = 2, PageSize = 10, TotalCount = 42, Items = [row] });
                case "/api/yetkili-servisler/filtre-secenekleri":
                    return Response(HttpStatusCode.OK, filters);
                case "/api/yetkili-servisler/basvuru-secenekleri":
                    return Response(OptionsStatus, new YetkiliServisBasvuruSecenekleriDto
                    {
                        Markalar = filters.Markalar, Kategoriler = filters.Kategoriler,
                        Sehirler = ["API city"], SehirFirmaKodlari = new() { ["API city"] = "API_CODE" }
                    });
                case "/api/yetkili-servisler/kayit":
                    RegistrationBody = LastBody;
                    return Response(RegistrationStatus, new YetkiliServisKayitSonuc
                    {
                        Basarili = RegistrationStatus == HttpStatusCode.OK,
                        Mesaj = "Canonical validation", FirmaId = RegistrationStatus == HttpStatusCode.OK ? 12 : null
                    });
                default:
                    throw new InvalidOperationException("Unexpected public endpoint: " + path);
            }
        }

        private static HttpResponseMessage Response<T>(HttpStatusCode status, T body)
            => new(status) { Content = JsonContent.Create(body) };
    }
}
