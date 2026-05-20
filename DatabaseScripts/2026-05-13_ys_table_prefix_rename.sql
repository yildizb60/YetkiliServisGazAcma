/*
    Yetkili Servis Gaz Acma - Ys_ tablo adi standardizasyonu

    Bu script migration degildir. Mudurun istedigi tablo adlarini manuel SQL ile
    duzeltmek icin hazirlandi. Uygulamayi yeni kodla calistirmadan once veritabani
    uzerinde bir kez calistirilmalidir.

    Onemli:
    - Script sadece tablo adlarini degistirir, veri silmez.
    - __EFMigrationsHistory tablosu kullanilmadigi icin en sonda kaldirilir.
    - Calistirmadan once veritabani yedegi alinmasi onerilir.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Renames TABLE
(
    OldName sysname NOT NULL,
    NewName sysname NOT NULL
);

INSERT INTO @Renames (OldName, NewName)
VALUES
    (N'AspNetUsers', N'Ys_AspNetUsers'),
    (N'AspNetRoles', N'Ys_AspNetRoles'),
    (N'AspNetUserRoles', N'Ys_AspNetUserRoles'),
    (N'AspNetUserClaims', N'Ys_AspNetUserClaims'),
    (N'AspNetUserLogins', N'Ys_AspNetUserLogins'),
    (N'AspNetRoleClaims', N'Ys_AspNetRoleClaims'),
    (N'AspNetUserTokens', N'Ys_AspNetUserTokens'),
    (N'Dag_Sirketler', N'Ys_Dag_Sirketler'),
    (N'Dag_PersonelYetkiler', N'Ys_Dag_PersonelYetkiler'),
    (N'UrunKategoriler', N'Ys_UrunKategoriler');

DECLARE @OldName sysname;
DECLARE @NewName sysname;
DECLARE @ObjectName nvarchar(300);
DECLARE @Message nvarchar(500);

DECLARE RenameCursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT OldName, NewName
    FROM @Renames;

OPEN RenameCursor;
FETCH NEXT FROM RenameCursor INTO @OldName, @NewName;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(QUOTENAME(N'dbo') + N'.' + QUOTENAME(@OldName), N'U') IS NOT NULL
       AND OBJECT_ID(QUOTENAME(N'dbo') + N'.' + QUOTENAME(@NewName), N'U') IS NOT NULL
    BEGIN
        SET @Message =
            N'Eski ve yeni tablo adi ayni anda var: ' + @OldName + N' / ' + @NewName
            + N'. Veri karismamasi icin script durduruldu.';
        THROW 51000, @Message, 1;
    END

    IF OBJECT_ID(QUOTENAME(N'dbo') + N'.' + QUOTENAME(@OldName), N'U') IS NOT NULL
       AND OBJECT_ID(QUOTENAME(N'dbo') + N'.' + QUOTENAME(@NewName), N'U') IS NULL
    BEGIN
        SET @ObjectName = QUOTENAME(N'dbo') + N'.' + QUOTENAME(@OldName);
        EXEC sys.sp_rename @objname = @ObjectName, @newname = @NewName, @objtype = N'OBJECT';
    END

    FETCH NEXT FROM RenameCursor INTO @OldName, @NewName;
END

CLOSE RenameCursor;
DEALLOCATE RenameCursor;

IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[__EFMigrationsHistory];
END

COMMIT TRANSACTION;
