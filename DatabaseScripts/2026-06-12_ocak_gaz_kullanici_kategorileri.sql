SET NOCOUNT ON;

DECLARE @Ocak NVARCHAR(100) = N'Ocak';
DECLARE @GazKullaniciCihazlar NVARCHAR(100) = N'Gaz Kullan' + NCHAR(305) + N'c' + NCHAR(305) + N' Cihazlar';

IF NOT EXISTS (
    SELECT 1
    FROM dbo.Ys_UrunKategoriler
    WHERE SilindiMi = 0 AND Ad = @Ocak
)
BEGIN
    INSERT INTO dbo.Ys_UrunKategoriler
        (Ad, IconUrl, SiraNo, AktifMi, OlusturmaTarihi, OlusturanKullanici, SilindiMi)
    VALUES
        (@Ocak, N'/images/icons/category-ocak.svg', 9, 1, GETDATE(), N'kategori-script', 0);
END
ELSE
BEGIN
    UPDATE dbo.Ys_UrunKategoriler
    SET IconUrl = N'/images/icons/category-ocak.svg',
        SiraNo = 9,
        AktifMi = 1,
        GuncellemeTarihi = GETDATE(),
        GuncelleyenKullanici = N'kategori-script'
    WHERE SilindiMi = 0 AND Ad = @Ocak;
END

IF NOT EXISTS (
    SELECT 1
    FROM dbo.Ys_UrunKategoriler
    WHERE SilindiMi = 0 AND Ad = @GazKullaniciCihazlar
)
BEGIN
    INSERT INTO dbo.Ys_UrunKategoriler
        (Ad, IconUrl, SiraNo, AktifMi, OlusturmaTarihi, OlusturanKullanici, SilindiMi)
    VALUES
        (@GazKullaniciCihazlar, N'/images/icons/category-gaz-kullanici-cihazlar.svg', 10, 1, GETDATE(), N'kategori-script', 0);
END
ELSE
BEGIN
    UPDATE dbo.Ys_UrunKategoriler
    SET IconUrl = N'/images/icons/category-gaz-kullanici-cihazlar.svg',
        SiraNo = 10,
        AktifMi = 1,
        GuncellemeTarihi = GETDATE(),
        GuncelleyenKullanici = N'kategori-script'
    WHERE SilindiMi = 0 AND Ad = @GazKullaniciCihazlar;
END
