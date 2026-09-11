using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using YetkiliServisGazAcma.Business.Services;

var passed = 0;
await CheckCookie("valid API session", _ => { }, accepted: true);
await CheckCookie("server session lost after restart", s => s.Clear());
await CheckCookie("different API user", s => s.SetString(ApiKullaniciOturumu.UserKey, "different-user"));
await CheckCookie("expired API token", s => s.SetString(ApiKullaniciOturumu.ExpiresKey, DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O")));
await CheckCookie("invalid expiry", s => s.SetString(ApiKullaniciOturumu.ExpiresKey, "invalid"));
await CheckCookie("missing expiry", s => s.Remove(ApiKullaniciOturumu.ExpiresKey));
await CheckCookie("missing token", s => s.Remove(ApiKullaniciOturumu.TokenKey));
await CheckCookie("empty token", s => s.SetString(ApiKullaniciOturumu.TokenKey, " "));
await CheckCookie("cookie without user ID", _ => { }, principal: new ClaimsPrincipal(new ClaimsIdentity([], "Cookies")));

await CheckMissingUser(authenticated: true);
await CheckMissingUser(authenticated: false);
Console.WriteLine($"PASS: {passed} web/API session checks.");

async Task CheckCookie(string name, Action<MemorySession> change, bool accepted = false, ClaimsPrincipal? principal = null)
{
    var session = new MemorySession();
    session.SetString(ApiKullaniciOturumu.UserKey, "test-user");
    session.SetString(ApiKullaniciOturumu.TokenKey, "test-token");
    session.SetString(ApiKullaniciOturumu.ExpiresKey, DateTimeOffset.UtcNow.AddHours(1).ToString("O"));
    session.SetString("active-company", "old-company");
    change(session);
    var auth = new RecordingAuthentication();
    using var services = new ServiceCollection().AddSingleton<IAuthenticationService>(auth).BuildServiceProvider();
    var http = new DefaultHttpContext { Session = session, RequestServices = services };
    var scheme = new AuthenticationScheme("Cookies", null, typeof(CookieAuthenticationHandler));
    var ticket = new AuthenticationTicket(principal ?? AuthenticatedUser(), scheme.Name);
    var validation = new CookieValidatePrincipalContext(http, scheme, new CookieAuthenticationOptions(), ticket);

    await ApiKullaniciOturumu.ValidatePrincipalAsync(validation);

    Assert(session.Loaded, name + ": session loaded");
    Assert((validation.Principal != null) == accepted, name + ": principal result");
    Assert(auth.SignOutCount == (accepted ? 0 : 1), name + ": sign-out count");
    if (accepted)
        Assert(session.GetString(ApiKullaniciOturumu.TokenKey) == "test-token", name + ": token preserved");
    else
        Assert(!session.Keys.Any(), name + ": API token and company selection cleared");
    Console.WriteLine("PASS: " + name);
    passed++;
}

async Task CheckMissingUser(bool authenticated)
{
    var auth = new RecordingAuthentication();
    using var services = new ServiceCollection().AddSingleton<IAuthenticationService>(auth).BuildServiceProvider();
    var http = new DefaultHttpContext
    {
        Session = new MemorySession(), RequestServices = services,
        User = authenticated ? AuthenticatedUser() : new ClaimsPrincipal(new ClaimsIdentity())
    };
    using var handler = new NoNetworkHandler();
    using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
    var service = new ApiKullaniciOturumu(new AuthApiClient(client), new HttpContextAccessor { HttpContext = http });

    var user = await service.GetUserAsync(http.User);

    Assert(user == null, "missing API session returns no user");
    Assert(http.User.Identity?.IsAuthenticated != true, "request principal is anonymous");
    Assert(handler.Calls == 0, "no API or database fallback");
    Assert(auth.SignOutCount == (authenticated ? 1 : 0), "only stale login signs out");
    Console.WriteLine("PASS: user lookup for " + (authenticated ? "stale cookie" : "anonymous request"));
    passed++;
}

static ClaimsPrincipal AuthenticatedUser() => new(new ClaimsIdentity(
    [new Claim(ClaimTypes.NameIdentifier, "test-user")], "Cookies"));

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + message);
}

sealed class MemorySession : ISession
{
    private readonly Dictionary<string, byte[]> data = [];
    public bool Loaded { get; private set; }
    public bool IsAvailable => true;
    public string Id => "test-session";
    public IEnumerable<string> Keys => data.Keys;
    public void Clear() => data.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) { Loaded = true; return Task.CompletedTask; }
    public void Remove(string key) => data.Remove(key);
    public void Set(string key, byte[] value) => data[key] = value;
    public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => data.TryGetValue(key, out value);
}

sealed class RecordingAuthentication : IAuthenticationService
{
    public int SignOutCount { get; private set; }
    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());
    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        SignOutCount++;
        return Task.CompletedTask;
    }
}

sealed class NoNetworkHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        throw new InvalidOperationException("API must not be called without a valid local session.");
    }
}
