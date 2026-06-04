/*
  Sertifika adlandirmasini Yetki Belgesi adlandirmasina tasir.
  Veri silmez; mevcut tablo, kolon, index ve yetki tipi degerlerini yeniden adlandirir.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[Ys_Sertifikalar]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[Ys_YetkiBelgeleri]', N'U') IS NULL
BEGIN
    EXEC sp_rename N'[dbo].[Ys_Sertifikalar]', N'Ys_YetkiBelgeleri';
END;

IF OBJECT_ID(N'[dbo].[Ys_YetkiBelgeleri]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Ys_YetkiBelgeleri', N'SertifikaBaslangicTarihi') IS NOT NULL
       AND COL_LENGTH(N'dbo.Ys_YetkiBelgeleri', N'YetkiBelgesiBaslangicTarihi') IS NULL
    BEGIN
        EXEC sp_rename N'[dbo].[Ys_YetkiBelgeleri].[SertifikaBaslangicTarihi]', N'YetkiBelgesiBaslangicTarihi', N'COLUMN';
    END;

    IF COL_LENGTH(N'dbo.Ys_YetkiBelgeleri', N'SertifikaBitisTarihi') IS NOT NULL
       AND COL_LENGTH(N'dbo.Ys_YetkiBelgeleri', N'YetkiBelgesiBitisTarihi') IS NULL
    BEGIN
        EXEC sp_rename N'[dbo].[Ys_YetkiBelgeleri].[SertifikaBitisTarihi]', N'YetkiBelgesiBitisTarihi', N'COLUMN';
    END;

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Ys_Sertifikalar_FirmaId_Durum_SilindiMi'
          AND object_id = OBJECT_ID(N'[dbo].[Ys_YetkiBelgeleri]')
    )
    BEGIN
        EXEC sp_rename
            N'[dbo].[Ys_YetkiBelgeleri].[IX_Ys_Sertifikalar_FirmaId_Durum_SilindiMi]',
            N'IX_Ys_YetkiBelgeleri_FirmaId_Durum_SilindiMi',
            N'INDEX';
    END;

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Ys_Sertifikalar_SertifikaBitisTarihi_SilindiMi'
          AND object_id = OBJECT_ID(N'[dbo].[Ys_YetkiBelgeleri]')
    )
    BEGIN
        EXEC sp_rename
            N'[dbo].[Ys_YetkiBelgeleri].[IX_Ys_Sertifikalar_SertifikaBitisTarihi_SilindiMi]',
            N'IX_Ys_YetkiBelgeleri_YetkiBelgesiBitisTarihi_SilindiMi',
            N'INDEX';
    END;

    UPDATE dbo.Ys_YetkiBelgeleri
    SET DosyaYolu = REPLACE(DosyaYolu, N'/sertifikalar/', N'/yetki-belgeleri/')
    WHERE DosyaYolu LIKE N'%/sertifikalar/%';
END;

IF COL_LENGTH(N'dbo.Ys_DevreyeAlmalar', N'TeknisyenSertifikaNo') IS NOT NULL
   AND COL_LENGTH(N'dbo.Ys_DevreyeAlmalar', N'TeknisyenYetkiBelgesiNo') IS NULL
BEGIN
    EXEC sp_rename N'[dbo].[Ys_DevreyeAlmalar].[TeknisyenSertifikaNo]', N'TeknisyenYetkiBelgesiNo', N'COLUMN';
END;

IF OBJECT_ID(N'[dbo].[Ys_Dag_PersonelYetkiler]', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.Ys_Dag_PersonelYetkiler
    SET YetkiTipi = N'YETKI_BELGESI_ONAY'
    WHERE YetkiTipi = N'CERTIFIKA_ONAY';
END;

COMMIT TRANSACTION;
