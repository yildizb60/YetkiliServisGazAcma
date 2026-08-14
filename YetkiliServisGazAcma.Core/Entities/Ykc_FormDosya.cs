namespace YetkiliServisGazAcma.Entities
{
    public class Ykc_FormDosya : BaseEntity
    {
        public int TalepId { get; set; }
        public string DosyaTuru { get; set; } = Business.Services.YkcFormDosyaTuruDegerleri.TeknikEk;
        public string? DosyaAdi { get; set; }
        public string? DosyaYolu { get; set; }
        public string? IcerikTipi { get; set; }
        public long? DosyaBoyutu { get; set; }
        public string DepolamaTuru { get; set; } = Business.Services.YkcDepolamaTuruDegerleri.Private;
        public string? BelgeHash { get; set; }

        public Ykc_Talep? Talep { get; set; }
    }
}
