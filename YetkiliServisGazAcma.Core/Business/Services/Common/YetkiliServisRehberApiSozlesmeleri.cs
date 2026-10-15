namespace YetkiliServisGazAcma.Business.Services
{
    public class YetkiliServisDto
    {
        public int Id { get; set; }
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? FaaliyetIli { get; set; }
        public string? Ilce { get; set; }
        public int SirketId { get; set; }
        public string? SirketAdi { get; set; }
        public List<string> Markalar { get; set; } = new();
        public List<UrunKategoriApiDto> Kategoriler { get; set; } = new();
    }

    public class YetkiliServisSayfaliDto
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        public List<YetkiliServisDto> Items { get; set; } = new();
    }

    public class YetkiliServisFiltreSecenekleriIstek
    {
        public string? Il { get; set; }
    }

    public class YetkiliServisFiltreSecenekleriDto
    {
        public List<YetkiliServisMarkaSecenekDto> Markalar { get; set; } = new();
        public List<UrunKategoriApiDto> Kategoriler { get; set; } = new();
        public List<string> Iller { get; set; } = new();
        public List<string> Ilceler { get; set; } = new();
    }

    public class YetkiliServisMarkaSecenekDto
    {
        public int Id { get; set; }
        public string? MarkaAdi { get; set; }
    }

    public class YetkiliServisFiltreDto
    {
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public int? SirketId { get; set; }
        public int? MarkaId { get; set; }
        public int? KategoriId { get; set; }
        public string? Q { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class YetkiliServisBasvuruDto
    {
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? FaaliyetIli { get; set; }
        public string? Ilce { get; set; }
        public string? VergiNo { get; set; }
        public string? VergiDairesi { get; set; }
        public string? TcKimlikNo { get; set; }
        public string Sifre { get; set; } = string.Empty;
        public List<int>? MarkaIdleri { get; set; }
        public List<int>? KategoriIdleri { get; set; }
    }

    public sealed class YetkiliServisRehberEkranDto
    {
        public YetkiliServisFiltreDto Sorgu { get; set; } = new();
        public YetkiliServisFiltreSecenekleriDto Filtreler { get; set; } = new();
        public YetkiliServisSayfaliDto Sonuc { get; set; } = new() { Page = 1, PageSize = 20 };
    }

    public sealed class YetkiliServisBasvuruSecenekleriDto
    {
        public List<YetkiliServisMarkaSecenekDto> Markalar { get; set; } = new();
        public List<UrunKategoriApiDto> Kategoriler { get; set; } = new();
        public List<string> Sehirler { get; set; } = new();
        public Dictionary<string, string> SehirFirmaKodlari { get; set; } = new();
    }

    public sealed class YetkiliServisKayitSonuc : ApiIslemSonuc
    {
        public int? FirmaId { get; set; }
    }
}
