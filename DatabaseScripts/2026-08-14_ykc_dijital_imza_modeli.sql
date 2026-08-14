IF COL_LENGTH('dbo.Ykc_Talepler', 'IkinciElCihazMi') IS NULL
BEGIN
    ALTER TABLE dbo.Ykc_Talepler ADD IkinciElCihazMi BIT NULL;
END;

IF COL_LENGTH('dbo.Ykc_Talepler', 'Fr265BelgeOlusturmaTarihi') IS NULL
BEGIN
    ALTER TABLE dbo.Ykc_Talepler ADD Fr265BelgeOlusturmaTarihi DATETIME2 NULL;
END;

IF COL_LENGTH('dbo.Ykc_Talepler', 'Fr265BelgeVersiyonNo') IS NULL
BEGIN
    ALTER TABLE dbo.Ykc_Talepler ADD Fr265BelgeVersiyonNo INT NOT NULL CONSTRAINT DF_Ykc_Talepler_Fr265BelgeVersiyonNo DEFAULT(1);
END;

IF COL_LENGTH('dbo.Ykc_Talepler', 'Fr265BelgeHash') IS NULL
BEGIN
    ALTER TABLE dbo.Ykc_Talepler ADD Fr265BelgeHash NVARCHAR(128) NULL;
END;

IF COL_LENGTH('dbo.Ykc_FormDosyalari', 'DepolamaTuru') IS NULL
BEGIN
    ALTER TABLE dbo.Ykc_FormDosyalari ADD DepolamaTuru NVARCHAR(40) NOT NULL CONSTRAINT DF_Ykc_FormDosyalari_DepolamaTuru DEFAULT('PRIVATE');
END;

IF COL_LENGTH('dbo.Ykc_FormDosyalari', 'BelgeHash') IS NULL
BEGIN
    ALTER TABLE dbo.Ykc_FormDosyalari ADD BelgeHash NVARCHAR(128) NULL;
END;

IF OBJECT_ID('dbo.Ykc_Fr265Kontroller', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ykc_Fr265Kontroller
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Ykc_Fr265Kontroller PRIMARY KEY,
        TalepId INT NOT NULL,
        KontrolNo INT NOT NULL,
        Sonuc NVARCHAR(40) NOT NULL CONSTRAINT DF_Ykc_Fr265Kontroller_Sonuc DEFAULT('BEKLIYOR'),
        Aciklama NVARCHAR(1000) NULL,
        KontrolEdenKullaniciId NVARCHAR(450) NULL,
        KontrolTarihi DATETIME2 NULL,
        OlusturmaTarihi DATETIME2 NOT NULL CONSTRAINT DF_Ykc_Fr265Kontroller_OlusturmaTarihi DEFAULT(GETDATE()),
        OlusturanKullanici NVARCHAR(256) NULL,
        GuncellemeTarihi DATETIME2 NULL,
        GuncelleyenKullanici NVARCHAR(256) NULL,
        SilindiMi BIT NOT NULL CONSTRAINT DF_Ykc_Fr265Kontroller_SilindiMi DEFAULT(0),
        SilinmeTarihi DATETIME2 NULL,
        SilenKullanici NVARCHAR(256) NULL,
        CONSTRAINT FK_Ykc_Fr265Kontroller_Ykc_Talepler FOREIGN KEY (TalepId) REFERENCES dbo.Ykc_Talepler(Id),
        CONSTRAINT FK_Ykc_Fr265Kontroller_Ys_AspNetUsers FOREIGN KEY (KontrolEdenKullaniciId) REFERENCES dbo.Ys_AspNetUsers(Id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ykc_Fr265Kontroller_TalepId_KontrolNo_SilindiMi' AND object_id = OBJECT_ID('dbo.Ykc_Fr265Kontroller'))
BEGIN
    CREATE INDEX IX_Ykc_Fr265Kontroller_TalepId_KontrolNo_SilindiMi ON dbo.Ykc_Fr265Kontroller(TalepId, KontrolNo, SilindiMi);
END;

IF OBJECT_ID('dbo.Ykc_ImzaSurecleri', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ykc_ImzaSurecleri
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Ykc_ImzaSurecleri PRIMARY KEY,
        TalepId INT NOT NULL,
        ProviderDocumentId NVARCHAR(128) NULL,
        BelgeVersiyonu INT NOT NULL CONSTRAINT DF_Ykc_ImzaSurecleri_BelgeVersiyonu DEFAULT(1),
        Durum NVARCHAR(40) NOT NULL CONSTRAINT DF_Ykc_ImzaSurecleri_Durum DEFAULT('HAZIR'),
        GonderimTarihi DATETIME2 NULL,
        TamamlanmaTarihi DATETIME2 NULL,
        SonKontrolTarihi DATETIME2 NULL,
        HataKodu NVARCHAR(80) NULL,
        HataMesaji NVARCHAR(1000) NULL,
        NihaiDosyaId INT NULL,
        BelgeHash NVARCHAR(128) NULL,
        BelgeOlusturmaTarihi DATETIME2 NULL,
        OlusturmaTarihi DATETIME2 NOT NULL CONSTRAINT DF_Ykc_ImzaSurecleri_OlusturmaTarihi DEFAULT(GETDATE()),
        OlusturanKullanici NVARCHAR(256) NULL,
        GuncellemeTarihi DATETIME2 NULL,
        GuncelleyenKullanici NVARCHAR(256) NULL,
        SilindiMi BIT NOT NULL CONSTRAINT DF_Ykc_ImzaSurecleri_SilindiMi DEFAULT(0),
        SilinmeTarihi DATETIME2 NULL,
        SilenKullanici NVARCHAR(256) NULL,
        CONSTRAINT FK_Ykc_ImzaSurecleri_Ykc_Talepler FOREIGN KEY (TalepId) REFERENCES dbo.Ykc_Talepler(Id),
        CONSTRAINT FK_Ykc_ImzaSurecleri_Ykc_FormDosyalari FOREIGN KEY (NihaiDosyaId) REFERENCES dbo.Ykc_FormDosyalari(Id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ykc_ImzaSurecleri_TalepId_Durum_SilindiMi' AND object_id = OBJECT_ID('dbo.Ykc_ImzaSurecleri'))
BEGIN
    CREATE INDEX IX_Ykc_ImzaSurecleri_TalepId_Durum_SilindiMi ON dbo.Ykc_ImzaSurecleri(TalepId, Durum, SilindiMi);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ykc_ImzaSurecleri_ProviderDocumentId' AND object_id = OBJECT_ID('dbo.Ykc_ImzaSurecleri'))
BEGIN
    CREATE INDEX IX_Ykc_ImzaSurecleri_ProviderDocumentId ON dbo.Ykc_ImzaSurecleri(ProviderDocumentId);
END;

IF OBJECT_ID('dbo.Ykc_Imzacilar', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ykc_Imzacilar
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Ykc_Imzacilar PRIMARY KEY,
        ImzaSureciId INT NOT NULL,
        Rol NVARCHAR(120) NOT NULL,
        AdSoyad NVARCHAR(256) NULL,
        KullaniciId NVARCHAR(450) NULL,
        SiraNo INT NOT NULL,
        Durum NVARCHAR(40) NOT NULL CONSTRAINT DF_Ykc_Imzacilar_Durum DEFAULT('BEKLIYOR'),
        ImzaTarihi DATETIME2 NULL,
        OlusturmaTarihi DATETIME2 NOT NULL CONSTRAINT DF_Ykc_Imzacilar_OlusturmaTarihi DEFAULT(GETDATE()),
        OlusturanKullanici NVARCHAR(256) NULL,
        GuncellemeTarihi DATETIME2 NULL,
        GuncelleyenKullanici NVARCHAR(256) NULL,
        SilindiMi BIT NOT NULL CONSTRAINT DF_Ykc_Imzacilar_SilindiMi DEFAULT(0),
        SilinmeTarihi DATETIME2 NULL,
        SilenKullanici NVARCHAR(256) NULL,
        CONSTRAINT FK_Ykc_Imzacilar_Ykc_ImzaSurecleri FOREIGN KEY (ImzaSureciId) REFERENCES dbo.Ykc_ImzaSurecleri(Id),
        CONSTRAINT FK_Ykc_Imzacilar_Ys_AspNetUsers FOREIGN KEY (KullaniciId) REFERENCES dbo.Ys_AspNetUsers(Id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ykc_Imzacilar_ImzaSureciId_SiraNo_SilindiMi' AND object_id = OBJECT_ID('dbo.Ykc_Imzacilar'))
BEGIN
    CREATE INDEX IX_Ykc_Imzacilar_ImzaSureciId_SiraNo_SilindiMi ON dbo.Ykc_Imzacilar(ImzaSureciId, SiraNo, SilindiMi);
END;
