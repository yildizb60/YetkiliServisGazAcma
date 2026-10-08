using System.Net;
using System.Net.Http.Headers;
using System.Text;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Models;

internal static class AuthTransportRegression
{
    public static async Task RunAsync()
    {
        const string failure = "Kimlik servisine ula\u015f\u0131lamad\u0131. L\u00fctfen tekrar deneyin.";
        const string rateLimit = "\u00c7ok fazla deneme yap\u0131ld\u0131. L\u00fctfen bir dakika sonra tekrar deneyin.";
        const string success = """{"basarili":true,"token":"issued-token","bitis":"2027-01-01T00:00:00Z","kullanici":{"id":"user","userName":"fixture","roller":["Personel"]}}""";
        var passed = 0;
        void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException("FAIL: " + label);
            passed++;
            Console.WriteLine("PASS: " + label);
        }

        using var handler = new AuthHandler { Body = success };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://fixture.invalid/") };
        var defaultAuthorization = new AuthenticationHeaderValue("Bearer", "stale-default-token");
        http.DefaultRequestHeaders.Authorization = defaultAuthorization;
        var auth = new AuthApiClient(http);
        var calls = new (string Path, HttpMethod Method, string? Token, string? Payload, Func<Task<OturumSonucu>> Call)[]
        {
            ("token", HttpMethod.Post, null, """{"email":"fixture-login","sifre":"fixture-password"}""",
                () => auth.GirisAsync("fixture-login", "fixture-password")),
            ("sms-dogrula", HttpMethod.Post, null, """{"dogrulama":"challenge","kod":"123456"}""",
                () => auth.SmsAsync("challenge", "123456")),
            ("sifre-unuttum", HttpMethod.Post, null, """{"kullaniciAdi":"fixture-login"}""",
                () => auth.SifreUnuttumAsync("fixture-login")),
            ("sifre-yenile", HttpMethod.Post, null, """{"dogrulama":"challenge","kod":"123456","yeniSifre":"new-password"}""",
                () => auth.SifreYenileAsync("challenge", "123456", "new-password")),
            ("me", HttpMethod.Get, "me-token", null, () => auth.KullaniciAsync("me-token")),
            ("profil", HttpMethod.Put, "profile-token", """{"adSoyad":"Fixture","email":"fixture@example.invalid","phoneNumber":"05551234567"}""",
                () => auth.ProfilAsync(new("Fixture", "fixture@example.invalid", "05551234567"), "profile-token")),
            ("sifre-degistir", HttpMethod.Post, "password-token", """{"mevcutSifre":"old-password","yeniSifre":"new-password"}""",
                () => auth.SifreAsync(new("old-password", "new-password"), "password-token"))
        };
        foreach (var call in calls)
        {
            var before = handler.Calls;
            var result = await call.Call();
            Check(result is { Basarili: true, Token: "issued-token", Kullanici.Id: "user" }
                && result.Bitis == DateTimeOffset.Parse("2027-01-01T00:00:00Z")
                && result.Kullanici.Roller.SequenceEqual(["Personel"]),
                call.Path + ": success retains the session contract");
            Check(handler.Calls == before + 1 && handler.Path == "/api/auth/" + call.Path
                && handler.Method == call.Method && handler.Token == call.Token
                && (call.Token == null ? handler.AuthorizationValues.All(string.IsNullOrEmpty) : handler.Scheme == "Bearer")
                && handler.RequestBody == call.Payload && handler.Disposed,
                call.Path + ": method, JSON payload, per-request authorization and disposal are preserved");
            Check(call.Payload == null ? handler.RequestContent == null : handler.RequestContent?.Headers.ContentType?.MediaType == "application/json",
                call.Path + ": GET has no body and writes have JSON content type");
            if (handler.RequestContent != null)
            {
                try
                {
                    await handler.RequestContent.ReadAsStringAsync();
                    throw new InvalidOperationException("Expected request content disposal");
                }
                catch (ObjectDisposedException) { Check(true, call.Path + ": request content is disposed"); }
            }
        }
        await auth.GirisAsync("fixture-login", "fixture-password");
        Check(handler.AuthorizationValues.All(string.IsNullOrEmpty) && http.DefaultRequestHeaders.Authorization == defaultAuthorization,
            "Anonymous login never inherits an earlier bearer and never mutates shared default headers");

        handler.Body = """{"basarili":true,"dogrulama":"sms-challenge","mesaj":"SMS sent"}""";
        Check(await auth.GirisAsync("fixture-login", "fixture-password") is { Basarili: true, Dogrulama: "sms-challenge", Mesaj: "SMS sent", Token: null },
            "SMS challenge login does not require or invent an access token");
        handler.Status = HttpStatusCode.BadRequest;
        handler.Body = """{"basarili":true,"mesaj":"Invalid SMS code"}""";
        Check(await auth.SmsAsync("sms-challenge", "000000") is { Basarili: false, Mesaj: "Invalid SMS code" },
            "SMS validation overrides a contradictory success flag and retains the message");
        handler.Status = HttpStatusCode.OK;
        handler.Body = success;
        Check(await auth.SmsAsync("sms-challenge", "123456") is { Basarili: true, Token: "issued-token" },
            "Successful SMS verification retains the issued token");

        handler.Status = HttpStatusCode.Unauthorized;
        handler.Body = """{"basarili":true,"mesaj":"Invalid credentials"}""";
        Check(await auth.GirisAsync("fixture-login", "wrong-password") is { Basarili: false, Mesaj: "Invalid credentials" },
            "Anonymous 401 remains an invalid-credentials response instead of expiring a session");
        foreach (var call in calls.Where(x => x.Token != null))
        {
            handler.Body = "<html>private diagnostic</html>";
            var before = handler.Calls;
            try { await call.Call(); throw new InvalidOperationException("Expected expired session"); }
            catch (ApiOturumSuresiDolduException)
            {
                Check(handler.Calls == before + 1 && handler.Disposed && handler.Token == call.Token,
                    call.Path + ": bearer 401 expires the session before parsing its body, without retry");
            }
        }
        foreach (var token in new[] { null, "", " " })
        {
            foreach (var call in new Func<Task<OturumSonucu>>[]
            {
                () => auth.KullaniciAsync(token!),
                () => auth.ProfilAsync(new("Fixture", "fixture@example.invalid", null), token!),
                () => auth.SifreAsync(new("old", "new"), token!)
            })
            {
                var before = handler.Calls;
                try { await call(); throw new InvalidOperationException("Expected missing bearer rejection"); }
                catch (ApiOturumSuresiDolduException)
                {
                    Check(handler.Calls == before, "A missing bearer cannot turn an authenticated auth endpoint into an anonymous request");
                }
            }
        }

        foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.Forbidden, HttpStatusCode.NotFound,
            HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity, HttpStatusCode.ServiceUnavailable })
        {
            handler.Status = status;
            handler.Body = """{"basarili":true,"mesaj":"Actionable auth message"}""";
            Check(await auth.ProfilAsync(new("Fixture", "fixture@example.invalid", null), "profile-token")
                is { Basarili: false, Mesaj: "Actionable auth message" },
                "Auth response retains its business message and rejects false success: " + status);
        }
        handler.Status = HttpStatusCode.BadRequest;
        handler.Body = """{"mesaj":" "}""";
        Check(await auth.GirisAsync("fixture-login", "fixture-password") is { Basarili: false, Mesaj: "Bilgileri kontrol edip tekrar deneyin." },
            "Blank validation messages retain the auth-specific fallback");
        handler.Status = HttpStatusCode.OK;
        handler.Body = """{"basarili":false}""";
        Check(await auth.GirisAsync("fixture-login", "fixture-password") is { Basarili: false, Mesaj: "Bilgileri kontrol edip tekrar deneyin." },
            "A business failure on HTTP 200 also gets a usable validation message");

        handler.Status = HttpStatusCode.TooManyRequests;
        foreach (var body in new[] { "{}", "", "null", "<html>private diagnostic</html>", "{\"basarili\":true,\"mesaj\":\" \"}" })
        {
            handler.Body = body;
            var before = handler.Calls;
            var result = await auth.GirisAsync("fixture-login", "fixture-password");
            Check(!result.Basarili && result.Mesaj == rateLimit && handler.Calls == before + 1 && handler.Disposed,
                "429 retains its auth retry message even without valid JSON, with no automatic retry");
        }
        handler.Body = """{"basarili":true,"mesaj":"Wait fifteen minutes"}""";
        Check(await auth.SmsAsync("challenge", "123456") is { Basarili: false, Mesaj: "Wait fifteen minutes" },
            "429 preserves the API's more specific lockout message");

        foreach (var status in new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized })
        {
            handler.Status = status;
            foreach (var body in new[] { "", "null", "[]", "<html>private diagnostic</html>", "{\"basarili\":\"invalid\"}" })
            {
                handler.Body = body;
                var before = handler.Calls;
                var result = await auth.GirisAsync("fixture-login", "fixture-password");
                Check(!result.Basarili && result.Mesaj == failure && handler.Calls == before + 1 && handler.Disposed,
                    "Malformed auth JSON fails safely without exposing diagnostics: " + status);
            }
        }

        foreach (var call in calls)
        {
            handler.Status = HttpStatusCode.ServiceUnavailable;
            handler.Body = "{}";
            var before = handler.Calls;
            Check(!(await call.Call()).Basarili && handler.Calls == before + 1,
                call.Path + ": server errors are never automatically replayed");
            foreach (var exception in new Exception[]
            {
                new HttpRequestException("private fixture-password issued-token"),
                new TaskCanceledException("private fixture-password issued-token"),
                new OperationCanceledException("private fixture-password issued-token")
            })
            {
                handler.Failure = exception;
                before = handler.Calls;
                var result = await call.Call();
                Check(!result.Basarili && result.Mesaj == failure && handler.Calls == before + 1,
                    call.Path + ": network/timeout failure is sanitized and never retried: " + exception.GetType().Name);
            }
            handler.Failure = null;
        }

        using var timeoutHandler = new AuthHandler { WaitForCancellation = true };
        using var timeoutHttp = new HttpClient(timeoutHandler)
        {
            BaseAddress = new Uri("https://fixture.invalid/"), Timeout = TimeSpan.FromMilliseconds(50)
        };
        var timeout = await new AuthApiClient(timeoutHttp).GirisAsync("fixture-login", "fixture-password");
        Check(!timeout.Basarili && timeout.Mesaj == failure && timeoutHandler.Calls == 1,
            "Actual HttpClient timeout produces the auth outage message without retrying credentials");
        Console.WriteLine($"{passed} auth transport regression checks passed.");
    }

    private sealed class AuthHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string Body { get; set; } = "{}";
        public Exception? Failure { get; set; }
        public bool WaitForCancellation { get; set; }
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Token { get; private set; }
        public string? Scheme { get; private set; }
        public string[] AuthorizationValues { get; private set; } = [];
        public string? RequestBody { get; private set; }
        public HttpContent? RequestContent { get; private set; }
        public bool Disposed { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath;
            Token = request.Headers.Authorization?.Parameter;
            Scheme = request.Headers.Authorization?.Scheme;
            AuthorizationValues = request.Headers.TryGetValues("Authorization", out var values) ? values.ToArray() : [];
            RequestContent = request.Content;
            RequestBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Disposed = false;
            if (WaitForCancellation) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            if (Failure != null) throw Failure;
            return new HttpResponseMessage(Status) { Content = new TrackedContent(Body, () => Disposed = true) };
        }
    }

    private sealed class TrackedContent(string body, Action disposed) : StringContent(body, Encoding.UTF8, "application/json")
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing) disposed();
            base.Dispose(disposing);
        }
    }
}
