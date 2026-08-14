namespace YetkiliServisGazAcma.Entities
{
    public class Ykc_ImzaSureci : BaseEntity
    {
        public int TalepId { get; set; }
        public string? ProviderDocumentId { get; set; }
        public int BelgeVersiyonu { get; set; } = 1;
        public string Durum { get; set; } = Business.Services.YkcImzaDurumDegerleri.Hazir;
        public DateTime? GonderimTarihi { get; set; }
        public DateTime? TamamlanmaTarihi { get; set; }
        public DateTime? SonKontrolTarihi { get; set; }
        public string? HataKodu { get; set; }
        public string? HataMesaji { get; set; }
        public int? NihaiDosyaId { get; set; }
        public string? BelgeHash { get; set; }
        public DateTime? BelgeOlusturmaTarihi { get; set; }

        public Ykc_Talep? Talep { get; set; }
        public Ykc_FormDosya? NihaiDosya { get; set; }
        public ICollection<Ykc_Imzaci> Imzacilar { get; set; } = new List<Ykc_Imzaci>();
    }
}
