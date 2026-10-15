-- Deploy the API/MVC version without Ys_DevreyeAlma.PdfYolu first.
-- PDFs are generated from records; nonempty legacy paths require manual review.
-- SeriAnahtari and its unique index are intentionally preserved.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.Ys_DevreyeAlmalar', N'PdfYolu') IS NOT NULL
        EXEC(N'
            IF EXISTS (SELECT 1 FROM dbo.Ys_DevreyeAlmalar WITH (TABLOCKX, HOLDLOCK)
                WHERE NULLIF(LTRIM(RTRIM(PdfYolu)), N'''') IS NOT NULL)
                THROW 51010, ''Nonempty PdfYolu values exist. Review them before removing the column.'', 1;
            ALTER TABLE dbo.Ys_DevreyeAlmalar DROP COLUMN PdfYolu;
        ');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
