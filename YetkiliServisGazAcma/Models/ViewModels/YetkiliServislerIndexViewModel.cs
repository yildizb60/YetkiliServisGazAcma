using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Models.ViewModels
{
    public class YetkiliServislerIndexViewModel
    {
        public List<Ys_Firma> Firmalar { get; set; } = new();
        public List<Ys_Marka> Markalar { get; set; } = new();
        public List<UrunKategori> Kategoriler { get; set; } = new();
        public List<string> Iller { get; set; } = new();
        public List<string> Ilceler { get; set; } = new();

        public string SeciliIl { get; set; } = string.Empty;
        public string SeciliIlce { get; set; } = string.Empty;
        public int? SeciliMarkaId { get; set; }
        public int? SeciliKategoriId { get; set; }
        public string Q { get; set; } = string.Empty;

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        public string VeriKaynagi { get; set; } = "API";
    }
}
