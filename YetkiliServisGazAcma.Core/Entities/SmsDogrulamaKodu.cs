namespace YetkiliServisGazAcma.Entities
{
    public class SmsDogrulamaKodu : BaseEntity
    {
        public string KullaniciId { get; set; } = string.Empty;
        public string Telefon { get; set; } = string.Empty;
        public string KodHash { get; set; } = string.Empty;
        public string Amac { get; set; } = "GIRIS";
        public DateTime GecerlilikTarihi { get; set; }
        public DateTime? KullanildiTarihi { get; set; }
        public int DenemeSayisi { get; set; }
        public bool KullanildiMi { get; set; }

        public AppKullanici? Kullanici { get; set; }
    }
}
