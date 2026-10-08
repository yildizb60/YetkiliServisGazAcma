using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;

internal static class SharedApiTransportRegression
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException("FAIL: " + label);
            passed++;
            Console.WriteLine("PASS: " + label);
        }
        var handler = new TransportHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://fixture.invalid/") };
        var context = new DefaultHttpContext
        {
            Session = new MemorySession(),
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user")], "Fixture"))
        };
        context.Session.SetString("API.UserId", "user");
        context.Session.SetString("API.AccessToken", "test-token");
        context.Session.SetString("API.Expires", DateTimeOffset.UtcNow.AddHours(1).ToString("O"));
        var tokens = new ApiJwtTokenService(new HttpContextAccessor { HttpContext = context });
        var options = Options.Create(new ApiIntegrationOptions { Enabled = true });
        var user = new AppKullanici { Id = "user" };
        T Client<T>() where T : class
        {
            var constructor = typeof(T).GetConstructors().Single();
            var args = constructor.GetParameters().Select(p =>
            {
                if (p.ParameterType == typeof(HttpClient)) return (object)http;
                if (p.ParameterType == typeof(IOptions<ApiIntegrationOptions>)) return options;
                if (p.ParameterType == typeof(ApiJwtTokenService)) return tokens;
                if (p.ParameterType == typeof(AktifSirketService)) return null;
                if (p.ParameterType.IsGenericType && p.ParameterType.GetGenericTypeDefinition() == typeof(ILogger<>))
                    return Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(typeof(T)));
                throw new InvalidOperationException("Unexpected client dependency: " + p.ParameterType);
            }).ToArray();
            return (T)constructor.Invoke(args);
        }
        var reads = new (string Name, Func<Task> Call, bool Auth)[]
        {
            ("Dashboard", () => Client<AdminDashboardApiClient>().GetirAsync(user, 7), true),
            ("Notification counts", () => Client<AdminDashboardApiClient>().BildirimOzetiAsync(user, 7), true),
            ("Personnel", () => Client<AdminKullaniciApiClient>().YetkiListeAsync(user, 7), true),
            ("Reports", () => Client<AdminRaporApiClient>().DevreyeAlmaDetayAsync(user, 1, 7), true),
            ("Branches", () => Client<AdminSubeApiClient>().DetayAsync(user, 1, 7), true),
            ("Approvals", () => Client<AdminYetkiBelgesiOnayApiClient>().ListeleAsync(user, 7), true),
            ("Service editor", () => Client<AdminYetkiliServisApiClient>().EditorAsync(user, 1, 7), true),
            ("Service branch list", () => Client<YetkiliServisPanelApiClient>().SubelerAsync(user), true),
            ("Services", () => Client<AdminYetkiliServisApiClient>().DetayAsync(user, 1, 7), true),
            ("Companies", () => Client<DagitimSirketApiClient>().GetirAsync(user, 7), true),
            ("Panel scope", () => Client<PanelKapsamApiClient>().PanelKimlikAsync(user, 7), true),
            ("Personnel panel", () => Client<PersonelPanelApiClient>().YetkilerimAsync(user, 7), true),
            ("Personnel dashboard", () => Client<PersonelPanelApiClient>().DashboardAsync(user, 7), true),
            ("Certificates", () => Client<YetkiBelgesiApiClient>().FirmaEkraniAsync(user, 1), true),
            ("Commissioning", () => Client<YetkiliServisDevreyeAlmaApiClient>().DetayAsync(user, 1), true),
            ("Commissioning screen", () => Client<YetkiliServisDevreyeAlmaApiClient>().EkranAsync(user), true),
            ("Service panel", () => Client<YetkiliServisPanelApiClient>().ProfilAsync(user), true),
            ("Scoped service branch", () => Client<YetkiliServisPanelApiClient>().SubeGetirAsync(user, 12), true),
            ("YKC", () => Client<YkcApiClient>().DetayAsync(user, 1), true),
            ("Brands", () => Client<MarkaApiClient>().GetirAsync(user, 1), true),
            ("Public summary", () => Client<HomeOzetApiClient>().GetirAsync(), false),
            ("Public categories", () => Client<UrunKategoriApiClient>().ListeAsync(), false),
            ("Public directory", () => Client<YetkiliServisApiClient>().ListeSayfaliAsync(new()), false)
        };
        handler.Status = HttpStatusCode.Forbidden;
        handler.Body = "{\"basarili\":true,\"id\":123}";
        foreach (var read in reads)
        {
            var count = handler.Calls;
            try { await read.Call(); throw new InvalidOperationException("Expected access rejection: " + read.Name); }
            catch (ApiIntegrationException ex)
            {
                Check(ex.StatusCode == 403 && handler.Calls == count + 1 && handler.Disposed
                    && handler.LastToken == (read.Auth ? "test-token" : null),
                    read.Name + ": access errors cannot be read as success; request auth and response disposal are correct");
            }
        }
        handler.Status = HttpStatusCode.OK;
        foreach (var (name, body, verify) in new (string, string, Func<Task<bool>>)[]
        {

            ("Service editor", """{"servis":{"id":7,"firmaAdi":"Firm"},"markalar":[{"id":3,"aktifMi":false}],"seciliMarkaIds":[3],"seciliKategoriIds":[4]}""",
                async () => await Client<AdminYetkiliServisApiClient>().EditorAsync(user, 7, 7) is
                    { Servis.Id: 7, Markalar.Count: 1, SeciliMarkaIds.Count: 1, SeciliKategoriIds.Count: 1 }),
            ("Service branches", """[{"id":8,"firmaId":7,"subeAdi":"Branch"}]""",
                async () => (await Client<YetkiliServisPanelApiClient>().SubelerAsync(user))?.Single() is { Id: 8, FirmaId: 7 }),
            ("Certificate null expiry", """{"belgeler":[{"id":8,"yetkiBelgesiBitisTarihi":null}]}""",
                async () => (await Client<YetkiBelgesiApiClient>().FirmaEkraniAsync(user, 7))?.Belgeler.Single() is { Id: 8, YetkiBelgesiBitisTarihi: null }),
            ("Approval flags", """{"bekleyenler":[{"id":8,"firmaAdi":"Firm","firmaAdres":"Address","onaylanabilir":true}]}""",
                async () => (await Client<AdminYetkiBelgesiOnayApiClient>().ListeleAsync(user, 7))?.Bekleyenler.Single()
                    is { Id: 8, FirmaAdi: "Firm", FirmaAdres: "Address", Onaylanabilir: true }),
            ("Notification counts", "{\"onayBekleyen\":3,\"suresiBitecek\":2}",
                async () => await Client<AdminDashboardApiClient>().BildirimOzetiAsync(user, 7) is { OnayBekleyen: 3, SuresiBitecek: 2 }),
            ("Personnel dashboard", "{\"onayBekleyen\":3,\"belgeYetkisi\":true,\"ykcYetkileri\":{\"raporlariGorebilir\":true}}",
                async () => await Client<PersonelPanelApiClient>().DashboardAsync(user, 7) is
                    { OnayBekleyen: 3, BelgeYetkisi: true, Ykc: null, YkcYetkileri.RaporlariGorebilir: true, YkcYetkileri.TalepleriGorebilir: false }),
            ("Dashboard", "{\"toplamFirma\":42,\"sonYetkiBelgeleri\":[{\"id\":12,\"firmaAdi\":\"Firma\"}]}",
                async () => await Client<AdminDashboardApiClient>().GetirAsync(user, 7) is { ToplamFirma: 42, SonYetkiBelgeleri.Count: 1 }),
            ("Personnel", "{\"personeller\":[{\"id\":\"person-1\",\"adSoyad\":\"Personel\"}],\"ozet\":{\"toplamPersonel\":50}}",
                async () => await Client<AdminKullaniciApiClient>().YetkiListeAsync(user, 7) is { Personeller.Count: 1, Ozet.ToplamPersonel: 50 }),
            ("Reports", "{\"id\":12,\"firmaId\":7,\"firmaAdi\":\"Firma\"}",
                async () => await Client<AdminRaporApiClient>().DevreyeAlmaDetayAsync(user, 12, 7) is { Id: 12, FirmaId: 7, FirmaAdi: "Firma" }),
            ("Branches", "{\"basarili\":true,\"sube\":{\"id\":12,\"subeAdi\":\"Merkez\"},\"firmalar\":[{\"id\":7}]}",
                async () => await Client<AdminSubeApiClient>().DetayAsync(user, 12, 7) is { Basarili: true, Sube.SubeAdi: "Merkez", Firmalar.Count: 1 }),
            ("Approvals", "{\"bekleyenler\":[{\"id\":12}],\"reddedilenler\":[{\"id\":13}]}",
                async () => await Client<AdminYetkiBelgesiOnayApiClient>().ListeleAsync(user, 7) is { Bekleyenler.Count: 1, Reddedilenler.Count: 1 }),
            ("Services", "{\"servis\":{\"id\":7,\"firmaAdi\":\"Firma\"},\"subeler\":[{\"id\":12}]}",
                async () => await Client<AdminYetkiliServisApiClient>().DetayAsync(user, 7, 7) is { Servis.FirmaAdi: "Firma", Subeler.Count: 1 }),
            ("Companies", "{\"id\":7,\"sirketAdi\":\"Company\"}",
                async () => await Client<DagitimSirketApiClient>().GetirAsync(user, 7) is { Id: 7, SirketAdi: "Company" }),
            ("Panel scope", "{\"sirketAdi\":\"Company\",\"firmaKodu\":\"FIXTURE\"}",
                async () => await Client<PanelKapsamApiClient>().PanelKimlikAsync(user, 7) is { SirketAdi: "Company", FirmaKodu: "FIXTURE" }),
            ("Personnel panel", "{\"yetkiler\":[\"REPORT_ONLY\"]}",
                async () => (await Client<PersonelPanelApiClient>().YetkilerimAsync(user, 7))?.SequenceEqual(["REPORT_ONLY"]) == true),
            ("Certificates", "{\"firma\":{\"id\":7},\"belgeler\":[{\"id\":12}],\"bildirimler\":[\"Notice\"]}",
                async () => await Client<YetkiBelgesiApiClient>().FirmaEkraniAsync(user, 7) is { Firma.Id: 7, Belgeler.Count: 1, Bildirimler.Count: 1 }),
            ("Commissioning", "{\"id\":12,\"musteriAdi\":\"Customer\"}",
                async () => await Client<YetkiliServisDevreyeAlmaApiClient>().DetayAsync(user, 12) is { Id: 12, MusteriAdi: "Customer" }),
            ("Commissioning screen", """{"erisilebilir":true,"firma":{"id":7,"sirketAdi":"Company"},"markalar":[{"id":3,"markaAdi":"Brand"}]}""",
                async () => await Client<YetkiliServisDevreyeAlmaApiClient>().EkranAsync(user)
                    is { Erisilebilir: true, Firma.SirketAdi: "Company", Markalar: [{ Id: 3, MarkaAdi: "Brand" }] }),
            ("Commissioning blocked screen", """{"erisilebilir":false,"hata":"Certificate required","redirectUrl":"/ys-yetki-belgesi/index"}""",
                async () => await Client<YetkiliServisDevreyeAlmaApiClient>().EkranAsync(user)
                    is { Erisilebilir: false, Hata: "Certificate required", RedirectUrl: "/ys-yetki-belgesi/index" }),
            ("Commissioning source reference", """{"basarili":true,"sozlesmeNo":"000943","cihazlar":[{"sorguReferansi":"device-reference","cihazKapasite":"20000"}]}""",
                async () => await Client<YetkiliServisDevreyeAlmaApiClient>().TesisatSorgulaAsync(user, "100", "000943")
                    is { Basarili: true, SozlesmeNo: "000943", Cihazlar: [{ SorguReferansi: "device-reference", CihazKapasite: "20000" }] }),
            ("Commissioning save", """{"basarili":true,"id":12,"mesaj":"Saved","redirectUrl":"/ys-devreyeal/gecmis#devreye-alma-detay-12"}""",
                async () => await Client<YetkiliServisDevreyeAlmaApiClient>().KaydetAsync(user, new())
                    is { Basarili: true, Id: 12, Mesaj: "Saved", RedirectUrl: "/ys-devreyeal/gecmis#devreye-alma-detay-12" }),
            ("Service panel", "{\"id\":7,\"firmaAdi\":\"Firma\",\"subeler\":[{\"id\":12}]}",
                async () => await Client<YetkiliServisPanelApiClient>().ProfilAsync(user) is { Id: 7, FirmaAdi: "Firma", Subeler.Count: 1 }),
            ("YKC", "{\"id\":12,\"aboneAdiSoyadi\":\"Customer\"}",
                async () => await Client<YkcApiClient>().DetayAsync(user, 12) is { Id: 12 }),
            ("Scoped service branch", "{\"id\":12,\"firmaId\":7,\"subeAdi\":\"Merkez\"}",
                async () => await Client<YetkiliServisPanelApiClient>().SubeGetirAsync(user, 12) is { Id: 12, FirmaId: 7, SubeAdi: "Merkez" }),
            ("User list DTO", "[{\"id\":\"user-12\",\"firmaAdi\":\"Firma\",\"sirketAdi\":\"Company\"}]",
                async () => (await Client<AdminKullaniciApiClient>().ListeleAsync(user, 7, null, null, null, null))?.Single()
                    is { Id: "user-12", FirmaAdi: "Firma", SirketAdi: "Company" }),
            ("User edit DTO", "{\"id\":\"user-12\",\"email\":\"edit@fixture.test\",\"firmaId\":7}",
                async () => await Client<AdminKullaniciApiClient>().GetirAsync(user, "user-12", 7)
                    is { Id: "user-12", Email: "edit@fixture.test", FirmaId: 7 }),
            ("Company options DTO", "[{\"id\":7,\"sirketAdi\":\"Company\"}]",
                async () => (await Client<AdminKullaniciApiClient>().SirketSecenekleriAsync(user, 7))?.Single()
                    is { Id: 7, SirketAdi: "Company" }),
            ("Report aggregate DTO", "{\"devreyeSayisi\":1,\"sonIslemler\":[{\"id\":12,\"sozlesmeNo\":\"000943\",\"firmaAdi\":\"Firma\",\"cihazKapasite\":\"20000\"}]}",
                async () => (await Client<AdminRaporApiClient>().RaporlarOzetAsync(user, 7, null, null, null))?.SonIslemler.Single()
                    is { Id: 12, SozlesmeNo: "000943", FirmaAdi: "Firma", CihazKapasite: "20000" }),
            ("Brands", "{\"id\":12,\"markaAdi\":\"Brand\"}",
                async () => await Client<MarkaApiClient>().GetirAsync(user, 12) is { Id: 12, MarkaAdi: "Brand" }),
            ("Public summary", "{\"servisCount\":42}",
                async () => await Client<HomeOzetApiClient>().GetirAsync() is { ServisCount: 42 }),
            ("Public categories", "[{\"id\":12,\"ad\":\"Category\"}]",
                async () => (await Client<UrunKategoriApiClient>().ListeAsync())?.Single().Ad == "Category"),
            ("Public directory", "{\"page\":2,\"pageSize\":10,\"totalCount\":42,\"totalPages\":5,\"items\":[{\"id\":12,\"firmaAdi\":\"Firma\",\"markalar\":[\"Brand\"],\"kategoriler\":[{\"id\":3,\"ad\":\"Category\"}]}]}",
                async () => await Client<YetkiliServisApiClient>().ListeSayfaliAsync(new() { Page = 2 })
                    is { Page: 2, TotalCount: 42, TotalPages: 5, Items.Count: 1 })
        })
        {
            handler.Body = body;
            Check(await verify(), name + ": extracted contracts deserialize and preserve view data");
        }
        var brands = Client<MarkaApiClient>();
        foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity })
        {
            handler.Status = status;
            handler.Body = "{\"basarili\":true,\"mesaj\":\"Validation message\",\"id\":1}";
            Check(await brands.EkleAsync(user, new()) is { Basarili: false, Mesaj: "Validation message" },
                "HTTP failure overrides a contradictory success body: " + status);
            Check(await Client<YetkiliServisDevreyeAlmaApiClient>().KaydetAsync(user, new())
                    is { Basarili: false, Mesaj: "Validation message" },
                "Commissioning shared result preserves business validation: " + status);
        }
        handler.Status = HttpStatusCode.OK;
        foreach (var body in new[] { "", "null", "<html>private diagnostic</html>" })
        {
            handler.Body = body;
            try { await brands.GetirAsync(user, 1); throw new InvalidOperationException("Expected malformed JSON rejection"); }
            catch (ApiIntegrationException ex)
            {
                Check(ex.StatusCode == 502 && !ex.Message.Contains("private diagnostic") && handler.Disposed,
                    "Empty/malformed API responses fail safely without exposing the body");
            }
        }
        handler.Body = "{\"basarili\":true,\"id\":15}";
        handler.TransientFailures = 1;
        var before = handler.Calls;
        Check(await Client<YetkiliServisPanelApiClient>().ProfilAsync(user) is { Id: 15 }
            && handler.Calls == before + 2, "Explicit read operation retries a temporary network failure");
        handler.TransientFailures = 10;
        before = handler.Calls;
        try { await Client<YetkiliServisPanelApiClient>().ProfilAsync(user); throw new InvalidOperationException("Expected exhausted retries"); }
        catch (ApiIntegrationException ex)
        {
            Check(ex.StatusCode == 503 && handler.Calls == before + 3,
                "Persistent read network failures stop after three attempts");
        }
        handler.TransientFailures = 0;
        foreach (var status in new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.RequestTimeout, HttpStatusCode.TooManyRequests })
        {
            handler.Status = status;
            before = handler.Calls;
            try { await Client<YetkiliServisPanelApiClient>().ProfilAsync(user); throw new InvalidOperationException("Expected read rejection"); }
            catch (ApiIntegrationException ex)
            {
                Check(ex.StatusCode == (int)status && handler.Disposed
                    && handler.Calls == before + (status == HttpStatusCode.TooManyRequests ? 1 : 3),
                    "Read retries are bounded and never replay a rate-limited request: " + status);
            }
        }
        handler.Status = HttpStatusCode.OK;
        foreach (var call in new Func<Task>[]
        {
            () => brands.EkleAsync(user, new()),
            () => Client<YetkiliServisPanelApiClient>().SubeSilAsync(user, 1),
            () => Client<YkcApiClient>().OlusturAsync(user, new()),
            () => Client<YetkiliServisDevreyeAlmaApiClient>().KaydetAsync(user, new()),
            () => Client<YetkiliServisApiClient>().KayitAsync(new())
        })
        {
            handler.TransientFailures = 1;
            before = handler.Calls;
            try { await call(); throw new InvalidOperationException("Expected network error"); }
            catch (ApiIntegrationException ex)
            {
                Check(ex.StatusCode == 503 && handler.Calls == before + 1,
                    "A write is never automatically replayed after a lost response");
            }
        }
        handler.TransientFailures = 0;
        using var stream = new MemoryStream([37, 80, 68, 70]);
        var file = new FormFile(stream, 0, stream.Length, "Dosya", "test.pdf")
        {
            Headers = new HeaderDictionary(), ContentType = "application/pdf"
        };
        Check(await Client<YetkiBelgesiApiClient>().YukleAsync(user, 7, file, new DateTime(2027, 10, 7), null)
            is { Basarili: true } && handler.UploadType == "application/pdf"
            && handler.UploadBody.Contains("FirmaId") && handler.UploadBody.Contains("2027-10-07"),
            "Certificate upload preserves multipart field names, dates and content type");
        Check(await Client<YkcApiClient>().FormYukleAsync(user, 72, file, "FR265") is { Basarili: true }
            && handler.UploadBody.Contains("TalepId") && handler.UploadBody.Contains("DosyaTuru")
            && handler.UploadType == "application/pdf", "YKC upload preserves its original file and document metadata");
        handler.Status = HttpStatusCode.BadRequest;
        handler.Body = "{\"basarili\":false,\"mesaj\":\"Dosya uygun değil.\"}";
        Check(await Client<YetkiBelgesiApiClient>().YukleAsync(user, 7, file, DateTime.Today, null)
            is { Basarili: false, Mesaj: "Dosya uygun değil." }, "Upload validation stays a business error, not an outage");
        handler.Status = HttpStatusCode.OK;
        handler.Body = "%PDF-test";
        handler.FileResponse = true;
        var download = await Client<AdminYetkiBelgesiOnayApiClient>().RaporAsync(user, new() { SirketId = 7, Tip = "bekleyen" }, false);
        Check(download?.DosyaAdi == "rapor.pdf" && download.ContentType == "application/pdf"
            && Encoding.UTF8.GetString(download.Bytes) == "%PDF-test" && handler.Disposed,
            "Shared file transport preserves bytes, content type, filename and disposal");
        var serviceFiles = Client<AdminYetkiliServisApiClient>();
        foreach (var asPdf in new[] { true, false })
        {
            var serviceFile = await serviceFiles.DosyaAsync(user, 12, 7, asPdf);
            Check(serviceFile?.DosyaAdi == "rapor.pdf" && Encoding.UTF8.GetString(serviceFile.Bytes) == "%PDF-test"
                && handler.LastToken == "test-token" && handler.Disposed,
                "Service-record file client preserves API bytes, authentication and metadata: " + asPdf);
        }
        handler.FileResponse = false;
        handler.Body = "{\"mesaj\":\"Private server detail\"}";
        foreach (var status in new[] { HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.ServiceUnavailable, HttpStatusCode.TooManyRequests })
        {
            handler.Status = status;
            before = handler.Calls;
            try { await serviceFiles.DosyaAsync(user, 12, 7, true); throw new InvalidOperationException("Expected file rejection"); }
            catch (ApiIntegrationException ex)
            {
                Check(ex.StatusCode == (int)status && handler.Calls == before + 1 && !ex.Message.Contains("Private server detail")
                    && (status != HttpStatusCode.NotFound || ex.Message.Contains("bulunamadı")),
                    "Service-record export retains the actual file failure status: " + status);
            }
        }
        handler.Status = HttpStatusCode.OK;
        context.Session.Remove("API.AccessToken");
        before = handler.Calls;
        try { await brands.GetirAsync(user, 1); throw new InvalidOperationException("Expected missing session rejection"); }
        catch (ApiOturumSuresiDolduException)
        {
            Check(handler.Calls == before, "Missing session retains the sign-in flow and cannot send an anonymous authenticated request");
        }
        Console.WriteLine($"{passed} shared API transport checks passed.");
    }

    private sealed class TransportHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public int TransientFailures { get; set; }
        public HttpStatusCode Status { get; set; }
        public string Body { get; set; } = "{}";
        public string? LastToken { get; private set; }
        public bool Disposed { get; private set; }
        public bool FileResponse { get; set; }
        public string? UploadType { get; private set; }
        public string UploadBody { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastToken = request.Headers.Authorization?.Parameter;
            Disposed = false;
            if (TransientFailures > 0) { TransientFailures--; throw new HttpRequestException("Fixture lost response"); }
            if (request.Content is MultipartFormDataContent form)
            {
                UploadType = form.Single(x => x.Headers.ContentDisposition?.FileName != null).Headers.ContentType?.MediaType;
                UploadBody = await form.ReadAsStringAsync(cancellationToken);
            }
            var response = new HttpResponseMessage(Status) { Content = new TrackedContent(Body, () => Disposed = true) };
            if (FileResponse)
            {
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileNameStar = "rapor.pdf" };
            }
            return response;
        }
    }

    private sealed class TrackedContent(string value, Action disposed) : StringContent(value, Encoding.UTF8, "application/json")
    {
        protected override void Dispose(bool disposing) { if (disposing) disposed(); base.Dispose(disposing); }
    }
}
