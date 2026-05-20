namespace YetkiliServisGazAcma.Entities
{
    public class SmsGonderimLog : BaseEntity
    {
        public string? KullaniciId { get; set; }
        public string Telefon { get; set; } = string.Empty;
        public string Mesaj { get; set; } = string.Empty;
        public string Saglayici { get; set; } = string.Empty;
        public bool BasariliMi { get; set; }
        public string? SaglayiciMesajId { get; set; }
        public string? HataMesaji { get; set; }

        public AppKullanici? Kullanici { get; set; }
    }
}
