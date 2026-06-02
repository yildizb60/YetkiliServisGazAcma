using System.Collections.Generic;

namespace YetkiliServisGazAcma.Entities
{
    public class Dag_Sirket : BaseEntity
    {
        public string? SirketAdi { get; set; }
        public string? Il { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public bool AktifMi { get; set; } = true;

        public ICollection<Ys_Firma>? Firmalar { get; set; }
        public ICollection<AppKullanici>? Personeller { get; set; }
    }
}