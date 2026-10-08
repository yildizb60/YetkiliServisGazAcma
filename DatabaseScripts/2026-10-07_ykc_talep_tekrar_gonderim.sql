SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Ykc_Talepler', N'SorguReferansi') IS NULL
    ALTER TABLE dbo.Ykc_Talepler ADD SorguReferansi varchar(64) NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Ykc_Talepler')
      AND name = N'IX_Ykc_Talepler_SorguReferansi')
    EXEC(N'CREATE UNIQUE INDEX IX_Ykc_Talepler_SorguReferansi
        ON dbo.Ykc_Talepler (SorguReferansi)
        WHERE SorguReferansi IS NOT NULL');

COMMIT TRANSACTION;
