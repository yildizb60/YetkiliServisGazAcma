using System;
using System.Collections.Generic;

namespace YetkiliServisGazAcma.Entities
{
    public class Ykc_Talep : BaseEntity
    {
        public int? FirmaId { get; set; }
        public int? SirketId { get; set; }

        public string? Vkn { get; set; }
        public string? FirmaKodu { get; set; }
        public string? KaynakTipi { get; set; } = "Manuel";

        public string? TesisatNo { get; set; }
        public string? SozlesmeNo { get; set; }
        public string? AboneNo { get; set; }
        public string? ProjeNo { get; set; }
        public string? SayacNo { get; set; }
        public string? MusteriAdi { get; set; }
        public string? MusteriTelefon { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? Bolge { get; set; }
        public string? Adres { get; set; }

        public string? EskiCihazTipiKodu { get; set; }
        public string? EskiCihazTipi { get; set; }
        public string? EskiMarkaKodu { get; set; }
        public string? EskiMarka { get; set; }
        public string? EskiBacaTipiKodu { get; set; }
        public string? EskiBacaTipi { get; set; }
        public string? EskiKapasite { get; set; }

        public string? YeniCihazTipiKodu { get; set; }
        public string? YeniCihazTipi { get; set; }
        public string? YeniMarkaKodu { get; set; }
        public string? YeniMarka { get; set; }
        public string? YeniBacaTipiKodu { get; set; }
        public string? YeniBacaTipi { get; set; }
        public string? YeniKapasite { get; set; }
        public string? YeniModel { get; set; }
        public string? YeniSeriNo { get; set; }
        public bool? IkinciElCihazMi { get; set; }

        public DateTime? Fr265BelgeOlusturmaTarihi { get; set; }
        public int Fr265BelgeVersiyonNo { get; set; } = 1;
        public string? Fr265BelgeHash { get; set; }

        public int Durum { get; set; } = Business.Services.YkcDurumDegerleri.TalepAlindi;
        public DateTime TalepTarihi { get; set; } = DateTime.Now;
        public string? RedAciklama { get; set; }
        public DateTime? IptalTarihi { get; set; }
        public string? IptalEdenKullaniciId { get; set; }
        public string? IptalAciklama { get; set; }

        public string? AtananKullaniciId { get; set; }
        public string? AtananKullaniciTipi { get; set; }
        public string? AtananEkip { get; set; }
        public string? HedefUygulama { get; set; }
        public DateTime? RandevuTarihi { get; set; }
        public string? RandevuSaati { get; set; }
        public string? RandevuId { get; set; }
        public string? IsEmriNo { get; set; }
        public string? Aufnr { get; set; }
        public bool CallCenterTetiklenecekMi { get; set; }
        public bool CallCenterTetiklendiMi { get; set; }

        public Ys_Firma? Firma { get; set; }
        public Dag_Sirket? Sirket { get; set; }
        public AppKullanici? AtananKullanici { get; set; }
        public ICollection<Ykc_FormDosya> FormDosyalari { get; set; } = new List<Ykc_FormDosya>();
        public ICollection<Ykc_Atama> Atamalar { get; set; } = new List<Ykc_Atama>();
        public ICollection<Ykc_IslemGecmisi> IslemGecmisi { get; set; } = new List<Ykc_IslemGecmisi>();
        public ICollection<Ykc_Fr265Kontrol> Kontroller { get; set; } = new List<Ykc_Fr265Kontrol>();
        public ICollection<Ykc_ImzaSureci> ImzaSurecleri { get; set; } = new List<Ykc_ImzaSureci>();
    }
}
