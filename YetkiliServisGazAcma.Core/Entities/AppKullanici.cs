using Microsoft.AspNetCore.Identity;

namespace YetkiliServisGazAcma.Entities
{
    public class AppKullanici : IdentityUser
    {
        public string? AdSoyad { get; set; }

        // Değerler: Business.Services.KullaniciTipi enum'u ile tutulur.
        public int KullaniciTipi { get; set; }

        public bool AktifMi { get; set; } = true;

        // Yetkili servis ise dolu, değilse null
        public int? FirmaId { get; set; }
        public Ys_Firma? Firma { get; set; }

        // Dağıtım Sirketi personeli ise dolu, değilse null
        public int? SirketId { get; set; }
        public Dag_Sirket? Sirket { get; set; }
    }
}
