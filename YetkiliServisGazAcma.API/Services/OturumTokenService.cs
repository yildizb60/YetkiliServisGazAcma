using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Services;

public sealed class OturumTokenService(UserManager<AppKullanici> users, IConfiguration configuration)
{
    public async Task<OturumKullaniciDto> KullaniciAsync(AppKullanici user) => new(user.Id,
        user.UserName, user.Email, user.AdSoyad, user.PhoneNumber, user.KullaniciTipi,
        user.FirmaId, user.SirketId, (await users.GetRolesAsync(user)).ToArray());

    public async Task<OturumSonucu> OlusturAsync(AppKullanici user)
    {
        var dto = await KullaniciAsync(user);
        var expires = DateTimeOffset.UtcNow.AddHours(8);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Name, user.AdSoyad ?? ""),
            new(ClaimTypes.Email, user.Email ?? ""), new("KullaniciTipi", user.KullaniciTipi.ToString()),
            new("stamp", await users.GetSecurityStampAsync(user)), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        claims.AddRange(dto.Roller.Select(role => new Claim(ClaimTypes.Role, role)));
        var jwt = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims,
            expires: expires.UtcDateTime, signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
        return new OturumSonucu { Basarili = true, Token = new JwtSecurityTokenHandler().WriteToken(jwt), Bitis = expires, Kullanici = dto };
    }
}
