using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
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
        private const string GenelGirisHatasi = "Kullanici adi veya sifre hatali.";

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
        public async Task<IActionResult> Token([FromBody] LoginDto? dto)
        {
            if (dto == null || !ModelState.IsValid)
                return Unauthorized(new { mesaj = GenelGirisHatasi });

            var kullanici = await _userManager.FindByEmailAsync(dto.Email)
                         ?? await _userManager.FindByNameAsync(dto.Email);

            if (kullanici == null)
            {
                _logger.LogWarning("API token istegi basarisiz. Kullanici bulunamadi: {Email}", dto.Email);
                return Unauthorized(new { mesaj = GenelGirisHatasi });
            }

            if (!kullanici.AktifMi)
            {
                _logger.LogWarning("API token istegi pasif hesap nedeniyle reddedildi. KullaniciId: {KullaniciId}", kullanici.Id);
                return Unauthorized(new { mesaj = "Hesabiniz aktif degil." });
            }

            var sonuc = await _signInManager.CheckPasswordSignInAsync(kullanici, dto.Sifre, true);

            if (sonuc.IsLockedOut)
            {
                _logger.LogWarning("API token istegi kilitli hesap nedeniyle reddedildi. KullaniciId: {KullaniciId}", kullanici.Id);
                return Unauthorized(new { mesaj = "Cok fazla hatali giris denemesi yapildi. Lutfen 15 dakika sonra tekrar deneyin." });
            }

            if (!sonuc.Succeeded)
            {
                _logger.LogWarning("API token istegi hatali sifre nedeniyle reddedildi. KullaniciId: {KullaniciId}", kullanici.Id);
                return Unauthorized(new { mesaj = GenelGirisHatasi });
            }

            var roller = await _userManager.GetRolesAsync(kullanici);
            var token = TokenOlustur(kullanici, roller);

            _logger.LogInformation("API token olusturuldu. KullaniciId: {KullaniciId}, Roller: {Roller}", kullanici.Id, string.Join(",", roller));

            return Ok(new
            {
                token,
                email = kullanici.Email,
                adSoyad = kullanici.AdSoyad,
                tip = kullanici.KullaniciTipi,
                roller
            });
        }

        private string TokenOlustur(AppKullanici kullanici, IList<string> roller)
        {
            var expireDays = int.TryParse(_config["Jwt:ExpireDays"], out var parsedExpireDays) && parsedExpireDays > 0
                ? parsedExpireDays
                : 1;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var krediler = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var talepler = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, kullanici.Id),
                new(ClaimTypes.Email, kullanici.Email!),
                new(ClaimTypes.Name, kullanici.AdSoyad ?? ""),
                new("KullaniciTipi", kullanici.KullaniciTipi.ToString())
            };

            foreach (var rol in roller)
                talepler.Add(new Claim(ClaimTypes.Role, rol));

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: talepler,
                expires: DateTime.UtcNow.AddDays(expireDays),
                signingCredentials: krediler);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class LoginDto
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Sifre { get; set; } = string.Empty;
    }
}
