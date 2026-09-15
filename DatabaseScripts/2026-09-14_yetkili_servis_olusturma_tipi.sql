-- Apply before deploying code that reads Ys_Firmalar.OlusturmaTipi.
-- Historic self-registration sets OlusturanKullanici to the VKN; other records
-- remain admin-created. Review ambiguous legacy rows before production use.
IF OBJECT_ID(N'dbo.Ys_Firmalar', N'U') IS NULL
    THROW 50001, 'dbo.Ys_Firmalar tablosu bulunamadi.', 1;

IF COL_LENGTH(N'dbo.Ys_Firmalar', N'OlusturmaTipi') IS NULL
    ALTER TABLE dbo.Ys_Firmalar ADD OlusturmaTipi tinyint NULL;

EXEC sys.sp_executesql N'
    UPDATE dbo.Ys_Firmalar
    SET OlusturmaTipi = CASE
        WHEN VergiNo IS NOT NULL AND LTRIM(RTRIM(OlusturanKullanici)) = LTRIM(RTRIM(VergiNo)) THEN 1
        ELSE 0
    END
    WHERE OlusturmaTipi IS NULL';

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Ys_Firmalar')
    AND name = N'OlusturmaTipi' AND is_nullable = 1)
    EXEC sys.sp_executesql N'ALTER TABLE dbo.Ys_Firmalar ALTER COLUMN OlusturmaTipi tinyint NOT NULL';

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints AS dc
    INNER JOIN sys.columns AS c
        ON c.object_id = dc.parent_object_id
        AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Ys_Firmalar')
        AND c.name = N'OlusturmaTipi')
    EXEC sys.sp_executesql N'ALTER TABLE dbo.Ys_Firmalar ADD CONSTRAINT DF_Ys_Firmalar_OlusturmaTipi DEFAULT (0) FOR OlusturmaTipi';

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Ys_Firmalar') AND name = N'CK_Ys_Firmalar_OlusturmaTipi')
    EXEC sys.sp_executesql N'ALTER TABLE dbo.Ys_Firmalar ADD CONSTRAINT CK_Ys_Firmalar_OlusturmaTipi CHECK (OlusturmaTipi IN (0, 1))';
