using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class AdminYetkiliServisListeFiltreDto
    {
        public int? SirketId { get; set; }
        public string? Q { get; set; }
        public string? Il { get; set; }
        public int? Durum { get; set; }
        public string? DevreyeSiralama { get; set; }
    }

    public class AdminYetkiliServisGetirFiltreDto
    {
        public int Id { get; set; }
        public int? SirketId { get; set; }
    }

    public class AdminYetkiliServisKaydetDto
    {
        public int Id { get; set; }
        public int? SirketId { get; set; }
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? FaaliyetIli { get; set; }
        public string? VergiNo { get; set; }
        public string? VergiDairesi { get; set; }
        public bool AktifMi { get; set; } = true;
        public List<int>? MarkaIds { get; set; }
        public List<int>? KategoriIds { get; set; }
    }

    public class AdminYetkiliServisDurumDto
    {
        public int Id { get; set; }
        public int? SirketId { get; set; }
    }

    public class AdminYetkiliServisListeDto
    {
        public List<AdminYetkiliServisDto> Servisler { get; set; } = new();
        public List<string> Sehirler { get; set; } = new();
        public Dictionary<int, int> DevreyeSayilari { get; set; } = new();
        public Dictionary<int, string> Ilceler { get; set; } = new();
    }

    public class AdminYetkiliServisDetayDto
    {
        public AdminYetkiliServisDto? Servis { get; set; }
        public List<AdminYetkiliServisYetkiBelgesiDto> YetkiBelgeleri { get; set; } = new();
        public List<AdminYetkiliServisSubeDto> Subeler { get; set; } = new();
        public List<AdminYetkiliServisDevreyeDto> Devreye { get; set; } = new();
    }

    public class AdminYetkiliServisDto
    {
        public int Id { get; set; }
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? VergiNo { get; set; }
        public string? VergiDairesi { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? FaaliyetIli { get; set; }
        public bool AktifMi { get; set; }
        public int SirketId { get; set; }
        public string? SirketAdi { get; set; }
        public List<AdminYetkiliServisKategoriDto> Kategoriler { get; set; } = new();
        public List<AdminYetkiliServisMarkaDto> Markalar { get; set; } = new();
            public static AdminYetkiliServisDto FromEntity(Ys_Firma servis)
        {
            return new AdminYetkiliServisDto
            {
                Id = servis.Id,
                FirmaAdi = servis.FirmaAdi,
                YetkiliKisi = servis.YetkiliKisi,
                VergiNo = servis.VergiNo,
                VergiDairesi = servis.VergiDairesi,
                Telefon = servis.Telefon,
                Email = servis.Email,
                Adres = servis.Adres,
                FaaliyetIli = servis.FaaliyetIli,
                AktifMi = servis.AktifMi,
                SirketId = servis.SirketId,
                SirketAdi = servis.Sirket?.SirketAdi,
                Kategoriler = servis.FirmaKategoriler?
                    .Where(x => !x.SilindiMi && x.Kategori != null && !x.Kategori.SilindiMi && x.Kategori.AktifMi)
                    .Select(x => new AdminYetkiliServisKategoriDto
                    {
                        Id = x.Kategori!.Id,
                        Ad = x.Kategori.Ad,
                        IconUrl = x.Kategori.IconUrl
                    })
                    .GroupBy(x => x.Id)
                    .Select(x => x.First())
                    .ToList() ?? new List<AdminYetkiliServisKategoriDto>(),
                Markalar = servis.FirmaMarkalar?
                    .Where(x => !x.SilindiMi && x.Marka != null)
                    .Select(x => new AdminYetkiliServisMarkaDto
                    {
                        Id = x.Marka!.Id,
                        MarkaAdi = x.Marka.MarkaAdi
                    })
                    .GroupBy(x => x.Id)
                    .Select(x => x.First())
                    .ToList() ?? new List<AdminYetkiliServisMarkaDto>()
            };
        }
    }

    public class AdminYetkiliServisKategoriDto
    {
        public int Id { get; set; }
        public string? Ad { get; set; }
        public string? IconUrl { get; set; }
    }

    public class AdminYetkiliServisMarkaDto
    {
        public int Id { get; set; }
        public string? MarkaAdi { get; set; }
    }

    public class AdminYetkiliServisYetkiBelgesiDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public int Durum { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public DateTime? YetkiBelgesiBaslangicTarihi { get; set; }
        public DateTime YetkiBelgesiBitisTarihi { get; set; }
    }

    public class AdminYetkiliServisSubeDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string? SubeAdi { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? Telefon { get; set; }
    }

    public class AdminYetkiliServisDevreyeDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string? TesistatNo { get; set; }
        public int Durum { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public string? MarkaAdi { get; set; }
    }

    public class AdminYetkiliServisEditorDto
    {
        public AdminYetkiliServisDto Servis { get; set; } = new() { AktifMi = true };
        public List<string> Sehirler { get; set; } = new();
        public Dictionary<string, string> SehirFirmaKodlari { get; set; } = new();
        public List<MarkaApiDto> Markalar { get; set; } = new();
        public List<AdminYetkiliServisKategoriDto> Kategoriler { get; set; } = new();
        public List<int> SeciliMarkaIds { get; set; } = new();
        public List<int> SeciliKategoriIds { get; set; } = new();
    }
}
