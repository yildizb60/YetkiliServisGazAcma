-- Deploy the API/MVC version without these four properties before running this script.
-- Preserve requests, assignments, signatures, history and all other columns.
-- Real external identifiers or a recorded dispatch stop the entire migration.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET LOCK_TIMEOUT 5000;

DECLARE @columns TABLE (Position int PRIMARY KEY, Name sysname, HasData nvarchar(500));
INSERT @columns VALUES
    (1, N'RandevuId', N'NULLIF(LTRIM(RTRIM([RandevuId])),N'''') IS NOT NULL'),
    (2, N'IsEmriNo', N'NULLIF(LTRIM(RTRIM([IsEmriNo])),N'''') IS NOT NULL'),
    (3, N'CallCenterTetiklendiMi', N'[CallCenterTetiklendiMi] = 1'),
    -- The literal Swagger example "string" is not an external order number.
    (4, N'Aufnr', N'NULLIF(LTRIM(RTRIM([Aufnr])),N'''') IS NOT NULL AND [Aufnr] <> N''string''');

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @name sysname, @predicate nvarchar(500), @default sysname,
        @sql nvarchar(max), @message nvarchar(2048);
    DECLARE unused_columns CURSOR LOCAL FAST_FORWARD FOR
        SELECT Name,HasData FROM @columns ORDER BY Position;
    OPEN unused_columns;
    FETCH NEXT FROM unused_columns INTO @name,@predicate;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF COL_LENGTH(N'dbo.Ykc_Talepler', @name) IS NOT NULL
        BEGIN
            SET @message = N'Column '+@name+N' contains data. No columns were removed; review the data first.';
            SET @sql = N'IF EXISTS (SELECT 1 FROM dbo.Ykc_Talepler WITH (TABLOCKX,HOLDLOCK) WHERE '
                +@predicate+N') THROW 51020, @message, 1;';
            EXEC sys.sp_executesql @sql,N'@message nvarchar(2048)',@message;

            SET @default = NULL;
            SELECT @default=dc.name FROM sys.default_constraints dc
            JOIN sys.columns c ON c.object_id=dc.parent_object_id AND c.column_id=dc.parent_column_id
            WHERE dc.parent_object_id=OBJECT_ID(N'dbo.Ykc_Talepler') AND c.name=@name;
            IF @default IS NOT NULL
            BEGIN
                SET @sql=N'ALTER TABLE dbo.Ykc_Talepler DROP CONSTRAINT '+QUOTENAME(@default)+N';';
                EXEC sys.sp_executesql @sql;
            END;
            SET @sql=N'ALTER TABLE dbo.Ykc_Talepler DROP COLUMN '+QUOTENAME(@name)+N';';
            EXEC sys.sp_executesql @sql;
        END;
        FETCH NEXT FROM unused_columns INTO @name,@predicate;
    END;
    CLOSE unused_columns;
    DEALLOCATE unused_columns;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    IF CURSOR_STATUS('local','unused_columns') >= 0 CLOSE unused_columns;
    IF CURSOR_STATUS('local','unused_columns') > -3 DEALLOCATE unused_columns;
    THROW;
END CATCH;
