using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services;

internal static class YkcTalepKapsami
{
    internal static IQueryable<Ykc_Talep> Uygula(
        IQueryable<Ykc_Talep> query,
        AppKullanici kullanici,
        bool genelYetkili,
        int? dogrulanmisSirketId = null)
    {
        // A firm account is always restricted to its own records, even if a stale
        // or incorrectly assigned privileged role reaches this layer.
        if (kullanici.KullaniciTipi == KullaniciTipiDegerleri.SertifikaliFirma
            && !kullanici.FirmaId.HasValue)
            return query.Where(x => false);

        if (kullanici.FirmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == kullanici.FirmaId.Value);
            return dogrulanmisSirketId.HasValue
                ? query.Where(x => x.SirketId == dogrulanmisSirketId.Value)
                : query;
        }

        // Explicit company selection is authorized by the API before querying.
        if (dogrulanmisSirketId.HasValue)
            return query.Where(x => x.SirketId == dogrulanmisSirketId.Value);

        if (genelYetkili)
            return query;

        if (kullanici.SirketId.HasValue)
            return query.Where(x => x.SirketId == kullanici.SirketId.Value);

        return query.Where(x => false);
    }
}
