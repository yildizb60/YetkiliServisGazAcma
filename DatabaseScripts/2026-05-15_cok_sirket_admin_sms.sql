IF NOT EXISTS (SELECT 1 FROM dbo.Ys_AspNetRoles WHERE NormalizedName = 'GENELSISTEMADMIN')
BEGIN
    INSERT INTO dbo.Ys_AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'GenelSistemAdmin', 'GENELSISTEMADMIN', NEWID());
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Ys_AspNetRoles WHERE NormalizedName = 'SIRKETADMIN')
BEGIN
    INSERT INTO dbo.Ys_AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'SirketAdmin', 'SIRKETADMIN', NEWID());
END;

UPDATE dbo.Ys_AspNetUsers
SET KullaniciTipi = 4
WHERE Email = 'admin@corumgaz.com'
  AND KullaniciTipi = 3
  AND SirketId IS NULL;

IF OBJECT_ID('dbo.Ys_SmsDogrulamaKodlari', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ys_SmsDogrulamaKodlari
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Ys_SmsDogrulamaKodlari PRIMARY KEY,
        KullaniciId nvarchar(450) NOT NULL,
        Telefon nvarchar(32) NOT NULL,
        KodHash nvarchar(128) NOT NULL,
        Amac nvarchar(32) NOT NULL,
        GecerlilikTarihi datetime2 NOT NULL,
        KullanildiTarihi datetime2 NULL,
        DenemeSayisi int NOT NULL CONSTRAINT DF_Ys_SmsDogrulamaKodlari_DenemeSayisi DEFAULT 0,
        KullanildiMi bit NOT NULL CONSTRAINT DF_Ys_SmsDogrulamaKodlari_KullanildiMi DEFAULT 0,
        OlusturmaTarihi datetime2 NOT NULL CONSTRAINT DF_Ys_SmsDogrulamaKodlari_OlusturmaTarihi DEFAULT GETDATE(),
        OlusturanKullanici nvarchar(max) NULL,
        GuncellemeTarihi datetime2 NULL,
        GuncelleyenKullanici nvarchar(max) NULL,
        SilindiMi bit NOT NULL CONSTRAINT DF_Ys_SmsDogrulamaKodlari_SilindiMi DEFAULT 0,
        SilinmeTarihi datetime2 NULL,
        SilenKullanici nvarchar(max) NULL,
        CONSTRAINT FK_Ys_SmsDogrulamaKodlari_Ys_AspNetUsers_KullaniciId
            FOREIGN KEY (KullaniciId) REFERENCES dbo.Ys_AspNetUsers(Id)
    );

    CREATE INDEX IX_Ys_SmsDogrulamaKodlari_KullaniciId
        ON dbo.Ys_SmsDogrulamaKodlari (KullaniciId, KullanildiMi, GecerlilikTarihi);
END;

IF OBJECT_ID('dbo.Ys_SmsGonderimLoglari', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ys_SmsGonderimLoglari
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Ys_SmsGonderimLoglari PRIMARY KEY,
        KullaniciId nvarchar(450) NULL,
        Telefon nvarchar(32) NOT NULL,
        Mesaj nvarchar(max) NOT NULL,
        Saglayici nvarchar(64) NOT NULL,
        BasariliMi bit NOT NULL,
        SaglayiciMesajId nvarchar(128) NULL,
        HataMesaji nvarchar(max) NULL,
        OlusturmaTarihi datetime2 NOT NULL CONSTRAINT DF_Ys_SmsGonderimLoglari_OlusturmaTarihi DEFAULT GETDATE(),
        OlusturanKullanici nvarchar(max) NULL,
        GuncellemeTarihi datetime2 NULL,
        GuncelleyenKullanici nvarchar(max) NULL,
        SilindiMi bit NOT NULL CONSTRAINT DF_Ys_SmsGonderimLoglari_SilindiMi DEFAULT 0,
        SilinmeTarihi datetime2 NULL,
        SilenKullanici nvarchar(max) NULL,
        CONSTRAINT FK_Ys_SmsGonderimLoglari_Ys_AspNetUsers_KullaniciId
            FOREIGN KEY (KullaniciId) REFERENCES dbo.Ys_AspNetUsers(Id)
    );

    CREATE INDEX IX_Ys_SmsGonderimLoglari_KullaniciId
        ON dbo.Ys_SmsGonderimLoglari (KullaniciId, OlusturmaTarihi);
END;
