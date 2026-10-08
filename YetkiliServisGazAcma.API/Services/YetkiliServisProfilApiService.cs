using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Business.Services;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;
using Microsoft.AspNetCore.Identity;

namespace YetkiliServisGazAcma.API.Services;

public sealed class YetkiliServisProfilApiService(AppDbContext context, UserManager<AppKullanici> userManager)
{
    private readonly AppDbContext _context = context;
    private readonly UserManager<AppKullanici> _userManager = userManager;

    public async Task<ApiIslemSonuc> GuncelleAsync(YsPanelProfilGuncelleDto? dto, AppKullanici kullanici)
    {
        kullanici.AdSoyad = dto?.AdSoyad ?? kullanici.AdSoyad;
        kullanici.PhoneNumber = dto?.Telefon ?? kullanici.PhoneNumber;

        if (!string.IsNullOrWhiteSpace(dto?.Email) && dto.Email != kullanici.Email)
        {
            kullanici.Email = dto.Email;
            kullanici.UserName = dto.Email;
        }

        var sonuc = await _userManager.UpdateAsync(kullanici);
        if (!sonuc.Succeeded)
        {
            var hata = string.Join(" ", sonuc.Errors.Select(x => x.Description));
            return ApiIslemSonuc.Basarisiz(string.IsNullOrWhiteSpace(hata)
                ? "Guncelleme sirasinda hata olustu."
                : hata);
        }

        if (!string.IsNullOrWhiteSpace(dto?.AdSoyad))
        {
            var firma = await _context.Ys_Firmalar
                .FirstOrDefaultAsync(x => x.Id == kullanici.FirmaId!.Value && !x.SilindiMi);
            if (firma != null)
            {
                firma.YetkiliKisi = dto.AdSoyad;
                firma.GuncellemeTarihi = DateTime.Now;
                firma.GuncelleyenKullanici = kullanici.UserName ?? "sistem";
                await _context.SaveChangesAsync();
            }
        }

        return ApiIslemSonuc.BasariliSonuc("Profil bilgileri guncellendi.");
    }
}
