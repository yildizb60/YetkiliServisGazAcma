using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services;

// Forwards the API-issued token; the web application never signs API tokens.
public sealed class ApiJwtTokenService(IHttpContextAccessor accessor)
{
    public Task<string?> OlusturAsync(AppKullanici kullanici)
    {
        var context = accessor.HttpContext ?? throw new ApiOturumSuresiDolduException();
        return Task.FromResult<string?>(ApiKullaniciOturumu.Token(context, kullanici.Id));
    }
}
