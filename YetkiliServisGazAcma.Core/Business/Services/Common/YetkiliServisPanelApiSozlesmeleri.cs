using YetkiliServisGazAcma.Entities;

namespace YetkiliServisGazAcma.Business.Services
{
    public class YsPanelDashboardFiltreDto
    {
        public DateTime? TakvimTarih { get; set; }
        public string? TakvimGorunum { get; set; }
    }

    public class YsPanelDashboardDto
    {
        public YsPanelFirmaDto? Firma { get; set; }
        public int BuAy { get; set; }
        public int GecerliBelgeSayisi { get; set; }
        public int BekleyenBelgeSayisi { get; set; }
        public int AktifMarkaSayisi { get; set; }
        public int AktifSubeSayisi { get; set; }
        public int Toplam { get; set; }
        public int Bekleyen { get; set; }
        public int Tamamlanan { get; set; }
        public int Iptal { get; set; }
        public List<YsPanelDevreyeAlmaDto> SonIslemler { get; set; } = new();
        public DateTime TakvimTarih { get; set; } = DateTime.Today;
        public string TakvimGorunum { get; set; } = "ay";
        public List<YsPanelDevreyeAlmaDto> TakvimIslemleri { get; set; } = new();
        public bool TakvimVerisiTam { get; set; }
        public bool IlkKurulumZorunlu { get; set; }
        public bool IlkKurulumTamamlandi { get; set; }
        public List<string> IlkKurulumEksikler { get; set; } = new();
        public int? YetkiBelgesiUyariGun { get; set; }
        public List<string> Bildirimler { get; set; } = new();
        public int BildirimSayisi { get; set; }
    }

    public class YsPanelBildirimDto
    {
        public List<string> Bildirimler { get; set; } = new();
        public int BildirimSayisi { get; set; }
    }

    public class YsPanelMarkalarDto
    {
        public YsPanelFirmaDto? Firma { get; set; }
        public List<YsPanelMarkaDto> TumMarkalar { get; set; } = new();
        public List<YsPanelFirmaMarkaDto> FirmaMarkalar { get; set; } = new();
        public List<int> SeciliMarkaIds { get; set; } = new();
    }

    public class YsPanelIlkKurulumDto
    {
        public YsPanelFirmaDto? Firma { get; set; }
        public List<YsPanelMarkaDto> TumMarkalar { get; set; } = new();
        public List<YsPanelUrunKategoriDto> TumKategoriler { get; set; } = new();
        public List<int> SeciliMarkaIds { get; set; } = new();
        public List<int> SeciliKategoriIds { get; set; } = new();
        public int AktifSubeSayisi { get; set; }
        public bool YetkiBelgesiVar { get; set; }
        public bool OnayliYetkiBelgesiVar { get; set; }
        public bool ZorunluMu { get; set; }
        public bool TamamlandiMi { get; set; }
        public List<string> Eksikler { get; set; } = new();
        public string? HataMesaji { get; set; }
    }

    public class YsPanelRaporFiltreDto
    {
        public DateTime? Bas { get; set; }
        public DateTime? Bit { get; set; }
        public List<int>? Ids { get; set; }
        public int? Limit { get; set; }
    }

    public class YsPanelRaporSonucDto
    {
        public YsPanelFirmaDto? Firma { get; set; }
        public DateTime BasTarih { get; set; }
        public DateTime BitTarih { get; set; }
        public int DevreyeSayisi { get; set; }
        public int Tamamlanan { get; set; }
        public int Bekleyen { get; set; }
        public int YetkiBelgesiOnayli { get; set; }
        public int YetkiBelgesiBekleyen { get; set; }
        public int YetkiBelgesiReddedilen { get; set; }
        public List<YsPanelDevreyeAlmaDto> SonIslemler { get; set; } = new();
        public List<string> ChartAylikLabels { get; set; } = new();
        public List<int> ChartAylikData { get; set; } = new();
        public List<int> ChartDurumData { get; set; } = new();
        public List<string> ChartMarkaLabels { get; set; } = new();
        public List<int> ChartMarkaData { get; set; } = new();
    }

    public class YsPanelIdDto
    {
        public int Id { get; set; }
    }

    public class YsPanelSubeKaydetDto
    {
        public int Id { get; set; }
        public string? SubeAdi { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? Telefon { get; set; }
        public string? Adres { get; set; }
        public bool AktifMi { get; set; }
    }

    public class YsPanelProfilGuncelleDto
    {
        public string? AdSoyad { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
    }

    public class YsPanelMarkaGuncelleDto
    {
        public List<int> MarkaIds { get; set; } = new();
    }

    public class YsPanelMarkaKaydetDto
    {
        public int Id { get; set; }
        public string? MarkaAdi { get; set; }
        public string? Aciklama { get; set; }
    }

    public class YsPanelFirmaDto
    {
        public int? GecerliYetkiBelgesiId { get; set; }
        public bool GecerliYetkiBelgesiVar { get; set; }
        public int? GosterilenYetkiBelgesiId { get; set; }
        public bool BekleyenYetkiBelgesiVar { get; set; }
        public string? YetkiBelgesiDurumu { get; set; }
        public int Id { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public string? FirmaAdi { get; set; }
        public string? YetkiliKisi { get; set; }
        public string? Telefon { get; set; }
        public string? Email { get; set; }
        public string? Adres { get; set; }
        public string? VergiNo { get; set; }
        public string? FaaliyetIli { get; set; }
        public int SirketId { get; set; }
        public YsPanelSirketDto? Sirket { get; set; }
        public List<YsPanelYetkiBelgesiDto> YetkiBelgeleri { get; set; } = new();
        public List<YsPanelFirmaMarkaDto> FirmaMarkalar { get; set; } = new();
        public List<YsPanelFirmaKategoriDto> FirmaKategoriler { get; set; } = new();
        public List<YsPanelSubeDto> Subeler { get; set; } = new();

        public static YsPanelFirmaDto FromEntity(Ys_Firma firma)
        {
            return new YsPanelFirmaDto
            {
                Id = firma.Id,
                OlusturmaTarihi = firma.OlusturmaTarihi,
                FirmaAdi = firma.FirmaAdi,
                YetkiliKisi = firma.YetkiliKisi,
                Telefon = firma.Telefon,
                Email = firma.Email,
                Adres = firma.Adres,
                VergiNo = firma.VergiNo,
                FaaliyetIli = firma.FaaliyetIli,
                SirketId = firma.SirketId,
                Sirket = firma.Sirket == null ? null : YsPanelSirketDto.FromEntity(firma.Sirket),
                YetkiBelgeleri = firma.YetkiBelgeleri?.Select(YsPanelYetkiBelgesiDto.FromEntity).ToList() ?? new(),
                FirmaMarkalar = firma.FirmaMarkalar?.Where(x => !x.SilindiMi).Select(YsPanelFirmaMarkaDto.FromEntity).ToList() ?? new(),
                FirmaKategoriler = firma.FirmaKategoriler?
                    .Where(x => !x.SilindiMi && x.Kategori != null && !x.Kategori.SilindiMi && x.Kategori.AktifMi)
                    .Select(YsPanelFirmaKategoriDto.FromEntity).ToList() ?? new(),
                Subeler = firma.Subeler?.Select(YsPanelSubeDto.FromEntity).ToList() ?? new()
            };
        }
    }

    public class YsPanelSirketDto
    {
        public int Id { get; set; }
        public string? SirketAdi { get; set; }
        public string? Il { get; set; }

        public static YsPanelSirketDto FromEntity(Dag_Sirket sirket)
        {
            return new YsPanelSirketDto
            {
                Id = sirket.Id,
                SirketAdi = sirket.SirketAdi,
                Il = sirket.Il
            };
        }
    }

    public class YsPanelYetkiBelgesiDto
    {
        public int Id { get; set; }
        public int Durum { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public DateTime? YetkiBelgesiBaslangicTarihi { get; set; }
        public DateTime YetkiBelgesiBitisTarihi { get; set; }
        public bool SilindiMi { get; set; }

        public static YsPanelYetkiBelgesiDto FromEntity(Ys_YetkiBelgesi belge)
        {
            return new YsPanelYetkiBelgesiDto
            {
                Id = belge.Id,
                Durum = belge.Durum,
                OlusturmaTarihi = belge.OlusturmaTarihi,
                YetkiBelgesiBaslangicTarihi = belge.YetkiBelgesiBaslangicTarihi,
                YetkiBelgesiBitisTarihi = belge.YetkiBelgesiBitisTarihi,
                SilindiMi = belge.SilindiMi
            };
        }
    }

    public class YsPanelFirmaMarkaDto
    {
        public int Id { get; set; }
        public int MarkaId { get; set; }
        public bool SilindiMi { get; set; }
        public YsPanelMarkaDto? Marka { get; set; }

        public static YsPanelFirmaMarkaDto FromEntity(Ys_FirmaMarka firmaMarka)
        {
            return new YsPanelFirmaMarkaDto
            {
                Id = firmaMarka.Id,
                MarkaId = firmaMarka.MarkaId,
                SilindiMi = firmaMarka.SilindiMi,
                Marka = firmaMarka.Marka == null ? null : YsPanelMarkaDto.FromEntity(firmaMarka.Marka)
            };
        }
    }

    public class YsPanelMarkaDto
    {
        public int Id { get; set; }
        public string? MarkaAdi { get; set; }
        public string? Aciklama { get; set; }
        public bool AktifMi { get; set; }

        public static YsPanelMarkaDto FromEntity(Ys_Marka marka)
        {
            return new YsPanelMarkaDto
            {
                Id = marka.Id,
                MarkaAdi = marka.MarkaAdi,
                Aciklama = marka.Aciklama,
                AktifMi = marka.AktifMi
            };
        }
    }

    public class YsPanelFirmaKategoriDto
    {
        public int Id { get; set; }
        public int KategoriId { get; set; }
        public bool SilindiMi { get; set; }
        public YsPanelUrunKategoriDto? Kategori { get; set; }

        public static YsPanelFirmaKategoriDto FromEntity(Ys_FirmaKategori kategori)
        {
            return new YsPanelFirmaKategoriDto
            {
                Id = kategori.Id,
                KategoriId = kategori.KategoriId,
                SilindiMi = kategori.SilindiMi,
                Kategori = kategori.Kategori == null ? null : YsPanelUrunKategoriDto.FromEntity(kategori.Kategori)
            };
        }
    }

    public class YsPanelUrunKategoriDto
    {
        public int Id { get; set; }
        public string? Ad { get; set; }
        public bool AktifMi { get; set; }

        public static YsPanelUrunKategoriDto FromEntity(UrunKategori kategori)
        {
            return new YsPanelUrunKategoriDto
            {
                Id = kategori.Id,
                Ad = kategori.Ad,
                AktifMi = kategori.AktifMi
            };
        }
    }

    public class YsPanelSubeDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string? SubeAdi { get; set; }
        public string? Il { get; set; }
        public string? Ilce { get; set; }
        public string? Telefon { get; set; }
        public string? Adres { get; set; }
        public bool AktifMi { get; set; }
        public bool SilindiMi { get; set; }
        public DateTime OlusturmaTarihi { get; set; }

        public static YsPanelSubeDto FromEntity(Ys_Sube sube)
        {
            return new YsPanelSubeDto
            {
                Id = sube.Id,
                FirmaId = sube.FirmaId,
                SubeAdi = sube.SubeAdi,
                Il = sube.Il,
                Ilce = sube.Ilce,
                Telefon = sube.Telefon,
                Adres = sube.Adres,
                AktifMi = sube.AktifMi,
                SilindiMi = sube.SilindiMi,
                OlusturmaTarihi = sube.OlusturmaTarihi
            };
        }
    }

    public class YsPanelDevreyeAlmaDto : DevreyeAlmaKayitDto
    {
        public YsPanelFirmaDto? Firma { get; set; }
        public YsPanelMarkaDto? Marka { get; set; }

        public static YsPanelDevreyeAlmaDto FromEntity(Ys_DevreyeAlma islem)
        {
            return new YsPanelDevreyeAlmaDto
            {
                Id = islem.Id,
                FirmaId = islem.FirmaId,
                MarkaId = islem.MarkaId,
                TesistatNo = islem.TesistatNo,
                MusteriAdi = islem.MusteriAdi,
                MusteriTelefon = islem.MusteriTelefon,
                MusteriTcNo = islem.MusteriTcNo,
                Adres = islem.Adres,
                CihazTipi = islem.CihazTipi,
                CihazMarka = islem.CihazMarka,
                CihazModeli = islem.CihazModeli,
                SeriNo = islem.SeriNo,
                CihazKapasite = islem.CihazKapasite,
                TeknisyenAdi = islem.TeknisyenAdi,
                TeknisyenYetkiBelgesiNo = islem.TeknisyenYetkiBelgesiNo,
                Durum = islem.Durum,
                DevreyeAlmaTarihi = islem.DevreyeAlmaTarihi,
                OlusturmaTarihi = islem.OlusturmaTarihi,
                Notlar = islem.Notlar,
                Firma = islem.Firma == null ? null : YsPanelFirmaDto.FromEntity(islem.Firma),
                Marka = islem.Marka == null ? null : YsPanelMarkaDto.FromEntity(islem.Marka)
            };
        }
    }
}
