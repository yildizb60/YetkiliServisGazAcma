namespace YetkiliServisGazAcma.Entities
{
    public class Ykc_IslemGecmisi : BaseEntity
    {
        public int TalepId { get; set; }
        public string? IslemTipi { get; set; }
        public int? EskiDurum { get; set; }
        public int? YeniDurum { get; set; }
        public string? Aciklama { get; set; }
        public string? KullaniciId { get; set; }
        public string? KullaniciAdi { get; set; }

        public Ykc_Talep? Talep { get; set; }
    }
}
