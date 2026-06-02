using System;

namespace YetkiliServisGazAcma.Entities
{
    public class Ys_FirmaKategori : BaseEntity
    {
        public int FirmaId { get; set; }
        public int KategoriId { get; set; }
        public DateTime YetkiBitisTarihi { get; set; }

        public Ys_Firma? Firma { get; set; }
        public UrunKategori? Kategori { get; set; }
    }
}
