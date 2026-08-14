namespace YetkiliServisGazAcma.Entities
{
    public class Ykc_Imzaci : BaseEntity
    {
        public int ImzaSureciId { get; set; }
        public string Rol { get; set; } = "";
        public string? AdSoyad { get; set; }
        public string? KullaniciId { get; set; }
        public int SiraNo { get; set; }
        public string Durum { get; set; } = Business.Services.YkcImzaciDurumDegerleri.Bekliyor;
        public DateTime? ImzaTarihi { get; set; }

        public Ykc_ImzaSureci? ImzaSureci { get; set; }
        public AppKullanici? Kullanici { get; set; }
    }
}
