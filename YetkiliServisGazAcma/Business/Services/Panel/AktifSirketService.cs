using Microsoft.AspNetCore.Identity;
using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AktifSirketService
    {
        private const string SessionPrefix = "AktifSirketId:";

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<AppKullanici> _userManager;
        private readonly PanelKapsamApiClient _panelKapsamApiClient;

        public AktifSirketService(
            IHttpContextAccessor httpContextAccessor,
            UserManager<AppKullanici> userManager,
            PanelKapsamApiClient panelKapsamApiClient)
        {
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
            _panelKapsamApiClient = panelKapsamApiClient;
        }

        public static bool GenelSistemAdminTipi(AppKullanici? kullanici)
        {
            return kullanici != null
                && (kullanici.KullaniciTipi == KullaniciTipiDegerleri.GenelSistemAdmin || (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && !kullanici.SirketId.HasValue));
        }

        public static bool SirketAdminTipi(AppKullanici? kullanici)
        {
            return kullanici != null && kullanici.KullaniciTipi == KullaniciTipiDegerleri.SirketAdmin && kullanici.SirketId.HasValue;
        }

        public async Task<bool> GenelSistemAdminMi(AppKullanici? kullanici)
        {
            if (kullanici == null) return false;

            if (GenelSistemAdminTipi(kullanici))
                return true;

            return await _userManager.IsInRoleAsync(kullanici, KullaniciRolAdlari.GenelSistemAdmin);
        }

        public async Task<bool> SirketAdminMi(AppKullanici? kullanici)
        {
            if (kullanici == null) return false;

            if (SirketAdminTipi(kullanici))
                return true;

            return await _userManager.IsInRoleAsync(kullanici, KullaniciRolAdlari.SirketAdmin);
        }

        public async Task<List<Dag_Sirket>> KullaniciSirketleriAsync(AppKullanici? kullanici)
        {
            if (kullanici == null)
                return new List<Dag_Sirket>();

            return await _panelKapsamApiClient.KullaniciSirketleriAsync(kullanici)
                ?? new List<Dag_Sirket>();
        }

        public async Task<int?> AktifSirketIdAsync(AppKullanici? kullanici)
        {
            if (kullanici == null)
                return null;

            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null)
                return kullanici.SirketId;

            var key = SessionKey(kullanici);
            var sirketler = await KullaniciSirketleriAsync(kullanici);
            var secilebilirIds = sirketler.Select(x => x.Id).ToHashSet();

            if (session.TryGetValue(key, out _))
            {
                var secili = session.GetInt32(key);
                if (secili.HasValue && secilebilirIds.Contains(secili.Value))
                    return secili.Value;

                session.Remove(key);
            }

            if (await GenelSistemAdminMi(kullanici))
                return null;

            var ilkSirketId = secilebilirIds.FirstOrDefault();
            if (ilkSirketId > 0)
            {
                session.SetInt32(key, ilkSirketId);
                return ilkSirketId;
            }

            return kullanici.SirketId;
        }

        public async Task<bool> SirketSecAsync(AppKullanici? kullanici, int sirketId)
        {
            if (kullanici == null)
                return false;

            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null)
                return false;

            var key = SessionKey(kullanici);

            if (sirketId <= 0)
            {
                if (await GenelSistemAdminMi(kullanici))
                {
                    session.Remove(key);
                    return true;
                }

                return false;
            }

            var sirketler = await KullaniciSirketleriAsync(kullanici);
            if (!sirketler.Any(x => x.Id == sirketId))
                return false;

            session.SetInt32(key, sirketId);
            return true;
        }

        private static string SessionKey(AppKullanici kullanici)
        {
            return $"{SessionPrefix}{kullanici.Id}";
        }
    }
}
