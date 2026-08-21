namespace YetkiliServisGazAcma.Entities
{
    public static class YetkiTipleri
    {
        public const string YETKI_BELGESI_ONAY = "YETKI_BELGESI_ONAY";
        public const string RAPOR_GOR = "RAPOR_GOR";
        public const string KULLANICI_YONET = "KULLANICI_YONET";
        public const string DAGITIM_SIRKET_YONET = "DAGITIM_SIRKET_YONET";
        public const string MARKA_YONET = "MARKA_YONET";
        public const string YKC_TALEP_GOR = "YKC_TALEP_GOR";
        public const string YKC_ATAMA_YAP = "YKC_ATAMA_YAP";
        public const string YKC_FR265_IMZA_ISLEM = "YKC_FR265_IMZA_ISLEM";
        public const string YKC_RAPOR_GOR = "YKC_RAPOR_GOR";
        public const string TAM_YETKI = "TAM_YETKI";
    }

    public class Dag_PersonelYetki : BaseEntity
    {
        public string KullaniciId { get; set; } = string.Empty;
        public int SirketId { get; set; }
        public string YetkiTipi { get; set; } = string.Empty;

        public AppKullanici? Kullanici { get; set; }
        public Dag_Sirket? Sirket { get; set; }
    }
}
