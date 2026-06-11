using System;
using YetkiliServisGazAcma.Business.Services;

namespace YetkiliServisGazAcma.Entities
{
    public class Ys_DevreyeAlma : BaseEntity
    {
        public int FirmaId { get; set; }
        public int? MarkaId { get; set; }

        // Tesisat Bilgileri
        public string? TesistatNo { get; set; }
        public string? AboneNo { get; set; }
        public string? UygunlukBelgeNo { get; set; }
        public DateTime? UygunlukTarihi { get; set; }

        // Müşteri Bilgileri
        public string? MusteriAdi { get; set; }
        public string? MusteriTcNo { get; set; }
        public string? MusteriTelefon { get; set; }
        public string? Adres { get; set; }

        // Cihaz Bilgileri
        public string? CihazTipi { get; set; }
        public string? CihazMarka { get; set; }
        public string? CihazModeli { get; set; }
        public string? CihazKapasite { get; set; }
        public string? SeriNo { get; set; }

        // Teknisyen Bilgileri
        public string? TeknisyenAdi { get; set; }
        public string? TeknisyenYetkiBelgesiNo { get; set; }

        // Diğer
        public DateTime DevreyeAlmaTarihi { get; set; }
        public string? Notlar { get; set; }
        public int Durum { get; set; } = DevreyeAlmaDurumDegerleri.Bekliyor;
        // 0=Bekliyor, 1=Tamamlandı, 2=İptal
        public string? PdfYolu { get; set; }

        // Navigation
        public Ys_Firma? Firma { get; set; }
        public Ys_Marka? Marka { get; set; }
    }
}
