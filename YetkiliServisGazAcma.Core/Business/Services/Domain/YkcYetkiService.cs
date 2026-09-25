using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using YetkiliServisGazAcma.Entities;
using YetkiliServisGazAcma.Models;

namespace YetkiliServisGazAcma.Business.Services
{
    public sealed class YkcYetkiOzeti
    {
        public bool TalepleriGorebilir { get; init; }
        public bool TalepOlusturabilir { get; init; }
        public bool AtamaYapabilir { get; init; }
        public bool Fr265ImzaIslemiYapabilir { get; init; }
        public bool RaporlariGorebilir { get; init; }

        public bool YetkiliMi(string yetkiTipi)
        {
            return yetkiTipi switch
            {
                YetkiTipleri.YKC_TALEP_GOR => TalepleriGorebilir,
                YetkiTipleri.YKC_ATAMA_YAP => AtamaYapabilir,
                YetkiTipleri.YKC_FR265_IMZA_ISLEM => Fr265ImzaIslemiYapabilir,
                YetkiTipleri.YKC_RAPOR_GOR => RaporlariGorebilir,
                _ => false
            };
        }
    }

    public sealed class YkcYetkiService
    {
        public static bool FirmaDosyasinaErisimVarMi(AppKullanici kullanici, Ykc_Talep talep)
        {
            return kullanici.FirmaId.HasValue && talep.FirmaId == kullanici.FirmaId.Value;
        }

        private readonly AppDbContext _context;
        private readonly UserManager<AppKullanici> _userManager;

        public YkcYetkiService(AppDbContext context, UserManager<AppKullanici> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<YkcYetkiOzeti> OzetAsync(
            AppKullanici kullanici,
            int? sirketId = null,
            CancellationToken cancellationToken = default)
        {
            var roller = await _userManager.GetRolesAsync(kullanici);
            var sertifikaliFirma = roller.Contains(KullaniciRolAdlari.SertifikaliFirma)
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma;
            if (sertifikaliFirma)
            {
                return kullanici.FirmaId.HasValue
                    ? new YkcYetkiOzeti { TalepleriGorebilir = true, TalepOlusturabilir = true }
                    : new YkcYetkiOzeti();
            }

            var icYonetici = roller.Any(x => x is KullaniciRolAdlari.GenelSistemAdmin
                or KullaniciRolAdlari.EskiSuperAdmin
                or KullaniciRolAdlari.SirketAdmin)
                || kullanici.KullaniciTipi is KullaniciTipiDegerleri.GenelSistemAdmin or KullaniciTipiDegerleri.SirketAdmin;

            if (icYonetici)
                return TumYonetimYetkileri();

            var personel = roller.Contains(KullaniciRolAdlari.Personel)
                || kullanici.KullaniciTipi == KullaniciTipiDegerleri.Personel;
            if (!personel)
                return new YkcYetkiOzeti();

            var kapsamSirketId = sirketId ?? kullanici.SirketId;
            var query = _context.Dag_PersonelYetkiler
                .AsNoTracking()
                .Where(x => x.KullaniciId == kullanici.Id && !x.SilindiMi);

            if (kapsamSirketId.HasValue)
                query = query.Where(x => x.SirketId == kapsamSirketId.Value);

            var yetkiler = await query
                .Select(x => x.YetkiTipi)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToListAsync(cancellationToken);

            if (yetkiler.Contains(YetkiTipleri.TAM_YETKI))
                return TumYonetimYetkileri();

            return new YkcYetkiOzeti
            {
                TalepleriGorebilir = yetkiler.Contains(YetkiTipleri.YKC_TALEP_GOR),
                TalepOlusturabilir = false,
                AtamaYapabilir = yetkiler.Contains(YetkiTipleri.YKC_ATAMA_YAP),
                Fr265ImzaIslemiYapabilir = yetkiler.Contains(YetkiTipleri.YKC_FR265_IMZA_ISLEM),
                RaporlariGorebilir = yetkiler.Contains(YetkiTipleri.YKC_RAPOR_GOR)
            };
        }

        public async Task<bool> YetkiliMiAsync(
            AppKullanici kullanici,
            string yetkiTipi,
            int? sirketId = null,
            CancellationToken cancellationToken = default)
        {
            var ozet = await OzetAsync(kullanici, sirketId, cancellationToken);
            return ozet.YetkiliMi(yetkiTipi);
        }

        private static YkcYetkiOzeti TumYonetimYetkileri()
        {
            return new YkcYetkiOzeti
            {
                TalepleriGorebilir = true,
                TalepOlusturabilir = false,
                AtamaYapabilir = true,
                Fr265ImzaIslemiYapabilir = true,
                RaporlariGorebilir = true
            };
        }
    }
}
