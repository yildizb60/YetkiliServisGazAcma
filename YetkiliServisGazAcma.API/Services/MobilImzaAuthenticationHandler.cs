using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace YetkiliServisGazAcma.API.Services;

public sealed class MobilImzaAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> schemes,
    ILoggerFactory logger, UrlEncoder encoder, IOptions<MobilImzaOptions> settings, IWebHostEnvironment environment)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemes, logger, encoder)
{
    public const string SchemeName = "MobilImza";
    public const string CompanyClaim = "imza:sirket";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!settings.Value.Enabled) return Task.FromResult(AuthenticateResult.NoResult());
        if (!Request.IsHttps && !(environment.IsDevelopment() && System.Net.IPAddress.IsLoopback(Context.Connection.RemoteIpAddress ?? System.Net.IPAddress.None)))
            return Fail();
        var keys = Request.Headers["X-Imza-Key"];
        if (keys.Count != 1 || keys[0] is not { Length: >= 32 and <= 256 } key) return Fail();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        foreach (var client in settings.Value.Istemciler)
        {
            if (client.SirketIdleri.Length == 0 || client.SirketIdleri.Any(x => x <= 0)) continue;
            byte[] expected;
            try { expected = Convert.FromHexString(client.ApiKeySha256); }
            catch (FormatException) { continue; }
            if (expected.Length != 32 || !CryptographicOperations.FixedTimeEquals(hash, expected)) continue;
            var claims = client.SirketIdleri.Distinct().Select(id => new Claim(CompanyClaim, id.ToString())).ToList();
            claims.Add(new Claim(ClaimTypes.NameIdentifier, client.Ad));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
        return Fail();
    }

    private static Task<AuthenticateResult> Fail() => Task.FromResult(AuthenticateResult.Fail("Entegrasyon kimliği geçersiz."));
}
