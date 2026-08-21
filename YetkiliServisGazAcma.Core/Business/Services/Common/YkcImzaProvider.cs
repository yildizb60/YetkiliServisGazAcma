namespace YetkiliServisGazAcma.Business.Services
{
    public interface IYkcImzaProvider
    {
        string ProviderAdi { get; }
        bool KullanilabilirMi { get; }
        bool DemoModuMu { get; }

        Task<YkcImzaGonderSonuc> GonderAsync(
            YkcImzaGonderIstek istek,
            CancellationToken cancellationToken = default);

        Task<YkcImzaDurumSonuc> DurumSorgulaAsync(
            string providerDocumentId,
            CancellationToken cancellationToken = default);
    }

    public class YkcImzaGonderIstek
    {
        public int TalepId { get; set; }
        public int BelgeVersiyonu { get; set; }
        public string BelgeAdi { get; set; } = "FR265.docx";
        public string IcerikTipi { get; set; } = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        public byte[] BelgeBytes { get; set; } = Array.Empty<byte>();
        public string BelgeHash { get; set; } = "";
        public string TekrarsizIstekAnahtari { get; set; } = "";
        public List<YkcImzaProviderImzaci> Imzacilar { get; set; } = new();
    }

    public class YkcImzaProviderImzaci
    {
        public int SiraNo { get; set; }
        public string Rol { get; set; } = "";
        public string? AdSoyad { get; set; }
        public string? KullaniciId { get; set; }
    }

    public class YkcImzaGonderSonuc
    {
        public bool Basarili { get; set; }
        public string? ProviderDocumentId { get; set; }
        public string? HataKodu { get; set; }
        public string? HataMesaji { get; set; }

        public static YkcImzaGonderSonuc Basarisiz(string hataKodu, string hataMesaji)
        {
            return new YkcImzaGonderSonuc
            {
                Basarili = false,
                HataKodu = hataKodu,
                HataMesaji = hataMesaji
            };
        }
    }

    public class YkcImzaDurumSonuc
    {
        public bool Basarili { get; set; }
        public string Durum { get; set; } = YkcImzaDurumDegerleri.ImzaBekliyor;
        public string? HataKodu { get; set; }
        public string? HataMesaji { get; set; }
        public List<YkcImzaProviderImzaciDurumu> Imzacilar { get; set; } = new();
        public byte[]? NihaiBelgeBytes { get; set; }
        public string? NihaiBelgeAdi { get; set; }
        public string? NihaiBelgeIcerikTipi { get; set; }

        public static YkcImzaDurumSonuc Basarisiz(string hataKodu, string hataMesaji)
        {
            return new YkcImzaDurumSonuc
            {
                Basarili = false,
                HataKodu = hataKodu,
                HataMesaji = hataMesaji
            };
        }
    }

    public class YkcImzaProviderImzaciDurumu
    {
        public int SiraNo { get; set; }
        public string? Durum { get; set; }
        public DateTime? ImzaTarihi { get; set; }
    }

    public class YkcImzaEntegrasyonDto
    {
        public bool KullanilabilirMi { get; set; }
        public bool DemoModuMu { get; set; }
        public string? ProviderAdi { get; set; }
    }
}
