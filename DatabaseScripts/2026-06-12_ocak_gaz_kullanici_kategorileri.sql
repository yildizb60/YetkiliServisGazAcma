IF NOT EXISTS (
    SELECT 1
    FROM dbo.Ys_UrunKategoriler
    WHERE SilindiMi = 0 AND Ad = N'Ocak'
)
BEGIN
    INSERT INTO dbo.Ys_UrunKategoriler
        (Ad, IconUrl, SiraNo, AktifMi, OlusturmaTarihi, OlusturanKullanici, SilindiMi)
    VALUES
        (N'Ocak', N'/images/icons/category-ocak.svg', 9, 1, GETDATE(), N'kategori-script', 0);
END
ELSE
BEGIN
    UPDATE dbo.Ys_UrunKategoriler
    SET IconUrl = N'/images/icons/category-ocak.svg',
        SiraNo = 9,
        AktifMi = 1,
        GuncellemeTarihi = GETDATE(),
        GuncelleyenKullanici = N'kategori-script'
    WHERE SilindiMi = 0 AND Ad = N'Ocak';
END

IF NOT EXISTS (
    SELECT 1
    FROM dbo.Ys_UrunKategoriler
    WHERE SilindiMi = 0 AND Ad = N'Gaz Kullanıcı Cihazlar'
)
BEGIN
    INSERT INTO dbo.Ys_UrunKategoriler
        (Ad, IconUrl, SiraNo, AktifMi, OlusturmaTarihi, OlusturanKullanici, SilindiMi)
    VALUES
        (N'Gaz Kullanıcı Cihazlar', N'/images/icons/category-gaz-kullanici-cihazlar.svg', 10, 1, GETDATE(), N'kategori-script', 0);
END
ELSE
BEGIN
    UPDATE dbo.Ys_UrunKategoriler
    SET IconUrl = N'/images/icons/category-gaz-kullanici-cihazlar.svg',
        SiraNo = 10,
        AktifMi = 1,
        GuncellemeTarihi = GETDATE(),
        GuncelleyenKullanici = N'kategori-script'
    WHERE SilindiMi = 0 AND Ad = N'Gaz Kullanıcı Cihazlar';
END
