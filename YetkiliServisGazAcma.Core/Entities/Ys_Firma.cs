using System.Collections.Generic;

namespace YetkiliServisGazAcma.Entities
{
    public class Ys_Firma : BaseEntity
    {
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? TcKimlikNo { get; set; }
        public string? FaaliyetIli { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? VergiNo { get; set; }
        public string? VergiDairesi { get; set; }
        public bool AktifMi { get; set; } = true;

        public int SirketId { get; set; }
        public Dag_Sirket? Sirket { get; set; }

        public ICollection<Ys_Sertifika>? Sertifikalar { get; set; }
        public ICollection<Ys_FirmaMarka>? FirmaMarkalar { get; set; }
        public ICollection<Ys_FirmaKategori>? FirmaKategoriler { get; set; }
        public ICollection<Ys_Sube>? Subeler { get; set; }
    }
}
