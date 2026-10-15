-- API'yi yeni surumle baslatmadan once ilgili veritabaninda calistirin.
-- Eski devreye alma kayitlari bilinmeyen kaynak anahtarlari nedeniyle geriye donuk doldurulmaz.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Ys_DevreyeAlmalar', N'U') IS NULL
    OR COL_LENGTH(N'dbo.Ys_DevreyeAlmalar', N'SilindiMi') IS NULL
    THROW 51000, 'Ys_DevreyeAlmalar tablosu veya SilindiMi kolonu bulunamadi.', 1;
GO

BEGIN TRANSACTION;
GO

IF OBJECT_ID(N'dbo.Ys_DevreyeAlmaSorguKayitlari', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ys_DevreyeAlmaSorguKayitlari
    (
        Referans varchar(64) NOT NULL CONSTRAINT PK_Ys_DevreyeAlmaSorguKayitlari PRIMARY KEY,
        KullaniciId nvarchar(450) NOT NULL,
        FirmaId int NOT NULL,
        DagitimSirketiId int NOT NULL,
        KaynakJson nvarchar(max) NOT NULL,
        KaynakCihazAnahtari varchar(64) NOT NULL,
        GecerlilikTarihi datetime2 NOT NULL,
        DevreyeAlmaId int NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Ys_DevreyeAlmaSorguKayitlari') AND name = N'IX_Ys_DevreyeAlmaSorguKayitlari_GecerlilikTarihi')
    CREATE INDEX IX_Ys_DevreyeAlmaSorguKayitlari_GecerlilikTarihi
        ON dbo.Ys_DevreyeAlmaSorguKayitlari (GecerlilikTarihi);
GO

IF COL_LENGTH(N'dbo.Ys_DevreyeAlmalar', N'KaynakCihazAnahtari') IS NULL
    ALTER TABLE dbo.Ys_DevreyeAlmalar ADD KaynakCihazAnahtari varchar(64) NULL;
GO

IF COL_LENGTH(N'dbo.Ys_DevreyeAlmalar', N'SeriAnahtari') IS NULL
    ALTER TABLE dbo.Ys_DevreyeAlmalar ADD SeriAnahtari varchar(64) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Ys_DevreyeAlmalar') AND name = N'IX_Ys_DevreyeAlmalar_KaynakCihazAnahtari')
    CREATE UNIQUE INDEX IX_Ys_DevreyeAlmalar_KaynakCihazAnahtari
        ON dbo.Ys_DevreyeAlmalar (KaynakCihazAnahtari)
        WHERE KaynakCihazAnahtari IS NOT NULL AND SilindiMi = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Ys_DevreyeAlmalar') AND name = N'IX_Ys_DevreyeAlmalar_SeriAnahtari')
    CREATE UNIQUE INDEX IX_Ys_DevreyeAlmalar_SeriAnahtari
        ON dbo.Ys_DevreyeAlmalar (SeriAnahtari)
        WHERE SeriAnahtari IS NOT NULL AND SilindiMi = 0;
GO

IF OBJECT_ID(N'dbo.Ys_DevreyeAlmaSorguKayitlari', N'U') IS NULL
    OR COL_LENGTH(N'dbo.Ys_DevreyeAlmalar', N'KaynakCihazAnahtari') IS NULL
    OR COL_LENGTH(N'dbo.Ys_DevreyeAlmalar', N'SeriAnahtari') IS NULL
    OR NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Ys_DevreyeAlmaSorguKayitlari') AND name = N'IX_Ys_DevreyeAlmaSorguKayitlari_GecerlilikTarihi')
    OR NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Ys_DevreyeAlmalar') AND name = N'IX_Ys_DevreyeAlmalar_KaynakCihazAnahtari' AND is_unique = 1 AND has_filter = 1)
    OR NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Ys_DevreyeAlmalar') AND name = N'IX_Ys_DevreyeAlmalar_SeriAnahtari' AND is_unique = 1 AND has_filter = 1)
    THROW 51001, 'Devreye alma kaynak semasi eksik olusturuldu.', 1;

COMMIT TRANSACTION;
GO
