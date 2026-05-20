using System;

namespace YetkiliServisGazAcma.Entities
{
    public class Ys_Sertifika : BaseEntity
    {
        public int FirmaId { get; set; }
        public string? DosyaYolu { get; set; }
        public DateTime? SertifikaBaslangicTarihi { get; set; }
        public DateTime SertifikaBitisTarihi { get; set; }
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
