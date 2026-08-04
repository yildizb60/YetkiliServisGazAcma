using System;

namespace YetkiliServisGazAcma.Entities
{
    public class Ykc_Atama : BaseEntity
    {
        public int TalepId { get; set; }
        public string? AtananKullaniciId { get; set; }
        public string? AtananKullaniciTipi { get; set; }
        public string? AtananEkip { get; set; }
        public string? Bolge { get; set; }
        public string? HedefUygulama { get; set; }
        public DateTime? RandevuTarihi { get; set; }
        public string? RandevuSaati { get; set; }
        public string? Aciklama { get; set; }

        public Ykc_Talep? Talep { get; set; }
        public AppKullanici? AtananKullanici { get; set; }
    }
}
