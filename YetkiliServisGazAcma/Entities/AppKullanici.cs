using Microsoft.AspNetCore.Identity;

namespace YetkiliServisGazAcma.Entities
{
    public class AppKullanici : IdentityUser
    {
        public string? AdSoyad { get; set; }

        // 1 = Yetkili Servis
        // 2 = Dağıtım Şirketi Personeli
        // 3 = Şirket Admini
        // 4 = Genel Sistem Admini
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
