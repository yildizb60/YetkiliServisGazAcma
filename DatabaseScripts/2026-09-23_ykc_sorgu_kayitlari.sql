-- API'yi Production ortaminda baslatmadan once ilgili veritabaninda calistirin.
-- Referanslar 20 dakika gecerlidir; eski kayitlar yeni sorgularda kademeli silinir.
IF OBJECT_ID(N'dbo.Ykc_SorguKayitlari', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ykc_SorguKayitlari
    (
        Referans varchar(64) NOT NULL CONSTRAINT PK_Ykc_SorguKayitlari PRIMARY KEY,
        KullaniciId nvarchar(450) NOT NULL,
        KaynakJson nvarchar(max) NOT NULL,
        GecerlilikTarihi datetime2 NOT NULL
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Ykc_SorguKayitlari')
      AND name = N'IX_Ykc_SorguKayitlari_GecerlilikTarihi'
)
BEGIN
    CREATE INDEX IX_Ykc_SorguKayitlari_GecerlilikTarihi
        ON dbo.Ykc_SorguKayitlari (GecerlilikTarihi);
END;
GO
