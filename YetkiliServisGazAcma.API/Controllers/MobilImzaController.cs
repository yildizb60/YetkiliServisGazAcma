using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YetkiliServisGazAcma.API.Services;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.API.Controllers;

[ApiController]
[Route("api/entegrasyon/imza")]
[Authorize(Policy = MobilImzaAuthenticationHandler.SchemeName)]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("MobilImza")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MobilImzaController(YkcImzaAkisService service) : ControllerBase
{
    private int[] Sirketler => User.FindAll(MobilImzaAuthenticationHandler.CompanyClaim).Select(x => int.Parse(x.Value)).ToArray();

    [HttpGet("bekleyenler")]
    public async Task<IActionResult> Bekleyenler(int sonId = 0, int adet = 25, CancellationToken cancellationToken = default)
    {
        if (sonId < 0 || adet is < 1 or > 100) return BadRequest(new { mesaj = "sonId en az 0, adet 1–100 olmalı." });
        var belgeler = await service.MobilBekleyenlerAsync(Sirketler, sonId, adet, cancellationToken);
        return Ok(new { belgeler, sonrakiId = belgeler.LastOrDefault()?.BelgeId, devamOlabilir = belgeler.Count == adet });
    }

    [HttpGet("belgeler/{belgeId:int}/paket")]
    [ProducesResponseType(typeof(MobilImzaPaketi), StatusCodes.Status200OK)]
    public async Task<IActionResult> Paket(int belgeId, CancellationToken cancellationToken)
    {
        var paket = await service.MobilPaketAsync(Sirketler, belgeId, cancellationToken);
        return paket == null ? NotFound(new { mesaj = "İmzaya açık PDF paketi bulunamadı." }) : Ok(paket);
    }

    [HttpPost("belgeler/{belgeId:int}/imzalandi")]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> Imzalandi(int belgeId, [FromBody] MobilImzaBildirimi bildirim, CancellationToken cancellationToken)
    {
        var result = await service.MobilImzalandiAsync(Sirketler, belgeId, bildirim, cancellationToken);
        return StatusCode(result.StatusCode, new { basarili = result.StatusCode == 200, result.Tekrar, result.Mesaj });
    }
}
