namespace YetkiliServisGazAcma.Entities
{
    public class Ys_Sube : BaseEntity
    {
        public int FirmaId { get; set; }
        public Ys_Firma? Firma { get; set; }

        public string? SubeAdi { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? Telefon { get; set; }
        public string? Adres { get; set; }
        public bool AktifMi { get; set; } = true;
    }
}
