IF OBJECT_ID(N'dbo.Ykc_Talepler', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ykc_Talepler
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Ykc_Talepler PRIMARY KEY,
        FirmaId INT NULL,
        SirketId INT NULL,
        Vkn NVARCHAR(32) NULL,
        FirmaKodu NVARCHAR(64) NULL,
        KaynakTipi NVARCHAR(32) NULL,
        TesisatNo NVARCHAR(64) NULL,
        SozlesmeNo NVARCHAR(64) NULL,
        AboneNo NVARCHAR(64) NULL,
        ProjeNo NVARCHAR(64) NULL,
        SayacNo NVARCHAR(64) NULL,
        MusteriAdi NVARCHAR(256) NULL,
        MusteriTelefon NVARCHAR(32) NULL,
        Il NVARCHAR(100) NULL,
        Ilce NVARCHAR(100) NULL,
        Bolge NVARCHAR(100) NULL,
        Adres NVARCHAR(1000) NULL,
        EskiCihazTipiKodu NVARCHAR(64) NULL,
        EskiCihazTipi NVARCHAR(128) NULL,
        EskiMarkaKodu NVARCHAR(64) NULL,
        EskiMarka NVARCHAR(128) NULL,
        EskiBacaTipiKodu NVARCHAR(64) NULL,
        EskiBacaTipi NVARCHAR(128) NULL,
        EskiKapasite NVARCHAR(64) NULL,
        YeniCihazTipiKodu NVARCHAR(64) NULL,
        YeniCihazTipi NVARCHAR(128) NULL,
        YeniMarkaKodu NVARCHAR(64) NULL,
        YeniMarka NVARCHAR(128) NULL,
        YeniBacaTipiKodu NVARCHAR(64) NULL,
        YeniBacaTipi NVARCHAR(128) NULL,
        YeniKapasite NVARCHAR(64) NULL,
        YeniModel NVARCHAR(128) NULL,
        YeniSeriNo NVARCHAR(128) NULL,
        Durum INT NOT NULL CONSTRAINT DF_Ykc_Talepler_Durum DEFAULT (1),
        TalepTarihi DATETIME2 NOT NULL CONSTRAINT DF_Ykc_Talepler_TalepTarihi DEFAULT (SYSDATETIME()),
        RedAciklama NVARCHAR(1000) NULL,
        IptalTarihi DATETIME2 NULL,
        IptalEdenKullaniciId NVARCHAR(450) NULL,
        IptalAciklama NVARCHAR(1000) NULL,
        AtananKullaniciId NVARCHAR(450) NULL,
        AtananKullaniciTipi NVARCHAR(100) NULL,
        AtananEkip NVARCHAR(150) NULL,
        HedefUygulama NVARCHAR(64) NULL,
        RandevuTarihi DATETIME2 NULL,
        RandevuSaati NVARCHAR(16) NULL,
        RandevuId NVARCHAR(64) NULL,
        IsEmriNo NVARCHAR(64) NULL,
        Aufnr NVARCHAR(64) NULL,
        CallCenterTetiklenecekMi BIT NOT NULL CONSTRAINT DF_Ykc_Talepler_CallCenterTetiklenecekMi DEFAULT (0),
        CallCenterTetiklendiMi BIT NOT NULL CONSTRAINT DF_Ykc_Talepler_CallCenterTetiklendiMi DEFAULT (0),
        OlusturmaTarihi DATETIME2 NOT NULL CONSTRAINT DF_Ykc_Talepler_OlusturmaTarihi DEFAULT (SYSDATETIME()),
        OlusturanKullanici NVARCHAR(256) NULL,
        GuncellemeTarihi DATETIME2 NULL,
        GuncelleyenKullanici NVARCHAR(256) NULL,
        SilindiMi BIT NOT NULL CONSTRAINT DF_Ykc_Talepler_SilindiMi DEFAULT (0),
        SilinmeTarihi DATETIME2 NULL,
        SilenKullanici NVARCHAR(256) NULL
    );

    CREATE INDEX IX_Ykc_Talepler_Firma_Tarih ON dbo.Ykc_Talepler(FirmaId, TalepTarihi, SilindiMi);
    CREATE INDEX IX_Ykc_Talepler_Sirket_Durum ON dbo.Ykc_Talepler(SirketId, Durum, SilindiMi);
    CREATE INDEX IX_Ykc_Talepler_Tesisat ON dbo.Ykc_Talepler(TesisatNo, SilindiMi);
END;

IF OBJECT_ID(N'dbo.Ykc_FormDosyalari', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ykc_FormDosyalari
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Ykc_FormDosyalari PRIMARY KEY,
        TalepId INT NOT NULL,
        DosyaTuru NVARCHAR(64) NOT NULL,
        DosyaAdi NVARCHAR(260) NULL,
        DosyaYolu NVARCHAR(1000) NULL,
        IcerikTipi NVARCHAR(128) NULL,
        DosyaBoyutu BIGINT NULL,
        OlusturmaTarihi DATETIME2 NOT NULL CONSTRAINT DF_Ykc_FormDosyalari_OlusturmaTarihi DEFAULT (SYSDATETIME()),
        OlusturanKullanici NVARCHAR(256) NULL,
        GuncellemeTarihi DATETIME2 NULL,
        GuncelleyenKullanici NVARCHAR(256) NULL,
        SilindiMi BIT NOT NULL CONSTRAINT DF_Ykc_FormDosyalari_SilindiMi DEFAULT (0),
        SilinmeTarihi DATETIME2 NULL,
        SilenKullanici NVARCHAR(256) NULL,
        CONSTRAINT FK_Ykc_FormDosyalari_Talepler FOREIGN KEY (TalepId) REFERENCES dbo.Ykc_Talepler(Id)
    );

    CREATE INDEX IX_Ykc_FormDosyalari_Talep_Tur ON dbo.Ykc_FormDosyalari(TalepId, DosyaTuru, SilindiMi);
END;

IF OBJECT_ID(N'dbo.Ykc_Atamalar', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ykc_Atamalar
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Ykc_Atamalar PRIMARY KEY,
        TalepId INT NOT NULL,
        AtananKullaniciId NVARCHAR(450) NULL,
        AtananKullaniciTipi NVARCHAR(100) NULL,
        AtananEkip NVARCHAR(150) NULL,
        Bolge NVARCHAR(100) NULL,
        HedefUygulama NVARCHAR(64) NULL,
        RandevuTarihi DATETIME2 NULL,
        RandevuSaati NVARCHAR(16) NULL,
        Aciklama NVARCHAR(1000) NULL,
        OlusturmaTarihi DATETIME2 NOT NULL CONSTRAINT DF_Ykc_Atamalar_OlusturmaTarihi DEFAULT (SYSDATETIME()),
        OlusturanKullanici NVARCHAR(256) NULL,
        GuncellemeTarihi DATETIME2 NULL,
        GuncelleyenKullanici NVARCHAR(256) NULL,
        SilindiMi BIT NOT NULL CONSTRAINT DF_Ykc_Atamalar_SilindiMi DEFAULT (0),
        SilinmeTarihi DATETIME2 NULL,
        SilenKullanici NVARCHAR(256) NULL,
        CONSTRAINT FK_Ykc_Atamalar_Talepler FOREIGN KEY (TalepId) REFERENCES dbo.Ykc_Talepler(Id)
    );

    CREATE INDEX IX_Ykc_Atamalar_Talep_Tarih ON dbo.Ykc_Atamalar(TalepId, OlusturmaTarihi, SilindiMi);
END;

IF OBJECT_ID(N'dbo.Ykc_IslemGecmisi', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ykc_IslemGecmisi
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Ykc_IslemGecmisi PRIMARY KEY,
        TalepId INT NOT NULL,
        IslemTipi NVARCHAR(100) NULL,
        EskiDurum INT NULL,
        YeniDurum INT NULL,
        Aciklama NVARCHAR(1000) NULL,
        KullaniciId NVARCHAR(450) NULL,
        KullaniciAdi NVARCHAR(256) NULL,
        OlusturmaTarihi DATETIME2 NOT NULL CONSTRAINT DF_Ykc_IslemGecmisi_OlusturmaTarihi DEFAULT (SYSDATETIME()),
        OlusturanKullanici NVARCHAR(256) NULL,
        GuncellemeTarihi DATETIME2 NULL,
        GuncelleyenKullanici NVARCHAR(256) NULL,
        SilindiMi BIT NOT NULL CONSTRAINT DF_Ykc_IslemGecmisi_SilindiMi DEFAULT (0),
        SilinmeTarihi DATETIME2 NULL,
        SilenKullanici NVARCHAR(256) NULL,
        CONSTRAINT FK_Ykc_IslemGecmisi_Talepler FOREIGN KEY (TalepId) REFERENCES dbo.Ykc_Talepler(Id)
    );

    CREATE INDEX IX_Ykc_IslemGecmisi_Talep_Tarih ON dbo.Ykc_IslemGecmisi(TalepId, OlusturmaTarihi, SilindiMi);
END;
