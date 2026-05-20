using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class ApiJwtTokenService
    {
        private readonly UserManager<AppKullanici> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiJwtTokenService> _logger;

        public ApiJwtTokenService(
            UserManager<AppKullanici> userManager,
            IConfiguration configuration,
            ILogger<ApiJwtTokenService> logger)
        {
            _userManager = userManager;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string?> OlusturAsync(AppKullanici kullanici)
        {
            var jwtKey = _configuration["Jwt:Key"];
            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(jwtKey) ||
                string.IsNullOrWhiteSpace(issuer) ||
                string.IsNullOrWhiteSpace(audience))
            {
                _logger.LogWarning("API token uretimi icin Jwt ayarlari eksik.");
                return null;
            }

            var roller = await _userManager.GetRolesAsync(kullanici);
            var talepler = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, kullanici.Id),
                new Claim(ClaimTypes.Email, kullanici.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, kullanici.AdSoyad ?? string.Empty),
                new Claim("KullaniciTipi", kullanici.KullaniciTipi.ToString())
            };

            foreach (var rol in roller)
                talepler.Add(new Claim(ClaimTypes.Role, rol));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var krediler = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expireDays = int.TryParse(_configuration["Jwt:ExpireDays"], out var gun)
                ? Math.Max(1, gun)
                : 1;

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: talepler,
                expires: DateTime.Now.AddDays(expireDays),
                signingCredentials: krediler);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
