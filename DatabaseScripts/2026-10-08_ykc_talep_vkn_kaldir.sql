-- Deploy the API/MVC version without Ykc_Talep.Vkn before running this script.
-- The firm's tax number remains in Ys_Firmalar.VergiNo.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET LOCK_TIMEOUT 5000;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.Ykc_Talepler', N'Vkn') IS NOT NULL
    BEGIN
        -- Dynamic SQL also permits rerunning after the column has been removed.
        EXEC sys.sp_executesql N'
            IF EXISTS (SELECT 1 FROM dbo.Ykc_Talepler WITH (TABLOCKX,HOLDLOCK)
                WHERE NULLIF(LTRIM(RTRIM(Vkn)),N'''') IS NOT NULL)
                THROW 51011, ''Vkn contains data. No column was removed; review the data first.'', 1;
            ALTER TABLE dbo.Ykc_Talepler DROP COLUMN Vkn;';
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
