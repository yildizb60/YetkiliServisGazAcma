using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppKullanici> _userManager;
        private readonly SignInManager<AppKullanici> _signInManager;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<AppKullanici> userManager,
            SignInManager<AppKullanici> signInManager,
            IConfiguration config,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _config = config;
            _logger = logger;
        }

        [HttpPost("token")]
        public async Task<IActionResult> Token([FromBody] LoginDto dto)
        {
            // Kullaniciyı bul
            var kullanici = await _userManager.FindByEmailAsync(dto.Email)
                         ?? await _userManager.FindByNameAsync(dto.Email);

            if (kullanici == null)
            {
                _logger.LogWarning("API token istegi basarisiz. Kullanici bulunamadi: {Email}", dto.Email);
                return Unauthorized(new { mesaj = "Kullanici bulunamadı" });
            }

            if (!kullanici.AktifMi)
            {
                _logger.LogWarning("API token istegi pasif hesap nedeniyle reddedildi. KullaniciId: {KullaniciId}", kullanici.Id);
                return Unauthorized(new { mesaj = "Hesabınız aktif değil" });
            }

            // Şifre kontrolü
            var sonuc = await _signInManager
                .CheckPasswordSignInAsync(kullanici, dto.Sifre, true);

            if (sonuc.IsLockedOut)
            {
                _logger.LogWarning("API token istegi kilitli hesap nedeniyle reddedildi. KullaniciId: {KullaniciId}", kullanici.Id);
                return Unauthorized(new { mesaj = "Cok fazla hatali giris denemesi yapildi. Lutfen 15 dakika sonra tekrar deneyin." });
            }

            if (!sonuc.Succeeded)
            {
                _logger.LogWarning("API token istegi hatali sifre nedeniyle reddedildi. KullaniciId: {KullaniciId}", kullanici.Id);
                return Unauthorized(new { mesaj = "Şifre hatalı" });
            }

            // Rolleri al
            var roller = await _userManager.GetRolesAsync(kullanici);

            // Token oluştur
            var token = TokenOlustur(kullanici, roller);
            _logger.LogInformation("API token olusturuldu. KullaniciId: {KullaniciId}, Roller: {Roller}", kullanici.Id, string.Join(",", roller));

            return Ok(new
            {
                token = token,
                email = kullanici.Email,
                adSoyad = kullanici.AdSoyad,
                tip = kullanici.KullaniciTipi,
                roller = roller
            });
        }

        private string TokenOlustur(AppKullanici kullanici, IList<string> roller)
        {
            var key = new SymmetricSecurityKey(
                              Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var krediler = new SigningCredentials(
                              key, SecurityAlgorithms.HmacSha256);

            var talepler = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, kullanici.Id),
                new Claim(ClaimTypes.Email,          kullanici.Email!),
                new Claim(ClaimTypes.Name,           kullanici.AdSoyad ?? ""),
                new Claim("KullaniciTipi",           kullanici.KullaniciTipi.ToString())
            };

            // Rolleri ekle
            foreach (var rol in roller)
                talepler.Add(new Claim(ClaimTypes.Role, rol));

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: talepler,
                expires: DateTime.Now.AddDays(
                                        int.Parse(_config["Jwt:ExpireDays"]!)),
                signingCredentials: krediler
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class LoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Sifre { get; set; } = string.Empty;
    }
}
