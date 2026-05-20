using System.Collections.Generic;

namespace YetkiliServisGazAcma.Entities
{
    public class Ys_Marka : BaseEntity
    {
        public string? MarkaAdi { get; set; }
        public string? Aciklama { get; set; }
        public bool AktifMi { get; set; } = true;

        public ICollection<Ys_FirmaMarka>? FirmaMarkalar { get; set; }
    }
}