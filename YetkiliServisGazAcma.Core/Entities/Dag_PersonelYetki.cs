namespace YetkiliServisGazAcma.Entities
{
    public static class YetkiTipleri
    {
        public const string CERTIFIKA_ONAY = "CERTIFIKA_ONAY";
        public const string RAPOR_GOR = "RAPOR_GOR";
        public const string KULLANICI_YONET = "KULLANICI_YONET";
        public const string DAGITIM_SIRKET_YONET = "DAGITIM_SIRKET_YONET";
        public const string MARKA_YONET = "MARKA_YONET";
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
