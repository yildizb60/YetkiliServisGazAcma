using System;

namespace YetkiliServisGazAcma.Entities
{
    public class Ys_YetkiBelgesi : BaseEntity
    {
        public int FirmaId { get; set; }
        public string? DosyaYolu { get; set; }
        public DateTime? YetkiBelgesiBaslangicTarihi { get; set; }
        public DateTime YetkiBelgesiBitisTarihi { get; set; }
        public int Durum { get; set; } = 0;
        // 0 = Onayda Bekliyor
        // 1 = Onaylandı
        // 2 = Reddedildi

        public string? RedGerekce { get; set; }
        public DateTime? OnayTarihi { get; set; }
        public string? OnaylayanKullanici { get; set; }

        public Ys_Firma? Firma { get; set; }
    }
}
