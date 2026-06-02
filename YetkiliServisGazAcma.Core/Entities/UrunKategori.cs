using System.Collections.Generic;

namespace YetkiliServisGazAcma.Entities
{
    public class UrunKategori : BaseEntity
    {
        public string? Ad { get; set; }
        public string? IconUrl { get; set; }
        public int SiraNo { get; set; } = 0;
        public bool AktifMi { get; set; } = true;

        public ICollection<Ys_FirmaKategori>? FirmaKategoriler { get; set; }
    }
}
