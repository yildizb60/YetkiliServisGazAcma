using Microsoft.AspNetCore.Mvc;

namespace YetkiliServisGazAcma.API.Controllers
{
    public partial class AdminPanelApiController
    {
        [HttpPost("dashboard")]
        public async Task<IActionResult> Dashboard([FromBody] AdminDashboardFiltreDto? dto)
        {
            var kapsam = await KapsamSirketIdAsync(dto?.SirketId);
            if (kapsam.gecersiz)
                return Forbid();

            var ozet = await _dashboardService.GetirAsync(kapsam.sirketId);

            return Ok(new AdminDashboardApiDto
            {
                ToplamDevreyeAlma = ozet.ToplamDevreyeAlma,
                ToplamFirma = ozet.ToplamFirma,
                OnayBekleyen = ozet.OnayBekleyen,
                SuresiBitecek = ozet.SuresiBitecek,
                ToplamSirket = ozet.ToplamSirket,
                BuAyDevreyeAlma = ozet.BuAyDevreyeAlma,
                SonYetkiBelgeleri = ozet.SonYetkiBelgeleri.Select(x => new AdminYetkiBelgesiOzetDto
                {
                    Id = x.Id,
                    FirmaId = x.FirmaId,
                    FirmaAdi = x.Firma?.FirmaAdi,
                    SirketAdi = x.Firma?.Sirket?.SirketAdi,
                    Durum = x.Durum,
                    OlusturmaTarihi = x.OlusturmaTarihi,
                    YetkiBelgesiBitisTarihi = x.YetkiBelgesiBitisTarihi
                }).ToList(),
                SonDevreyeAlmalar = ozet.SonDevreyeAlmalar.Select(x => new AdminDevreyeAlmaOzetDto
                {
                    Id = x.Id,
                    FirmaId = x.FirmaId,
                    FirmaAdi = x.Firma?.FirmaAdi,
                    MarkaAdi = x.Marka?.MarkaAdi,
                    TesistatNo = x.TesistatNo,
                    Durum = x.Durum,
                    OlusturmaTarihi = x.OlusturmaTarihi
                }).ToList()
            });
        }
    }
}
