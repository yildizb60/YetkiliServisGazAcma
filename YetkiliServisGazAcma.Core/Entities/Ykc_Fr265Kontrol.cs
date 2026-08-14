namespace YetkiliServisGazAcma.Entities
{
    public class Ykc_Fr265Kontrol : BaseEntity
    {
        public int TalepId { get; set; }
        public int KontrolNo { get; set; }
        public string Sonuc { get; set; } = Business.Services.YkcFr265KontrolSonucDegerleri.Bekliyor;
        public string? Aciklama { get; set; }
        public string? KontrolEdenKullaniciId { get; set; }
        public DateTime? KontrolTarihi { get; set; }

        public Ykc_Talep? Talep { get; set; }
        public AppKullanici? KontrolEdenKullanici { get; set; }
    }
}
