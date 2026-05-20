using System;

namespace YetkiliServisGazAcma.Entities
{
    public class Ys_FirmaMarka : BaseEntity
    {
        public int FirmaId { get; set; }
        public int MarkaId { get; set; }
        public DateTime YetkiBitisTarihi { get; set; }

        public Ys_Firma? Firma { get; set; }
        public Ys_Marka? Marka { get; set; }
    }
}