SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @HizmetTurleri TABLE (
        Ad NVARCHAR(100) NOT NULL PRIMARY KEY,
        SiraNo INT NOT NULL,
        IconUrl NVARCHAR(500) NULL
    );

    INSERT INTO @HizmetTurleri (Ad, SiraNo, IconUrl)
    VALUES
        (N'Kombi', 1, NULL),
        (N'Ocak', 2, N'/images/icons/category-ocak.svg'),
        (N'Şofben', 3, NULL),
        (N'Gaz Kullanıcı Cihazlar', 4, N'/images/icons/category-gaz-kullanici-cihazlar.svg');

    DECLARE @SofbenId INT;
    DECLARE @EskiSofbenId INT;

    SELECT @SofbenId = MIN(Id)
    FROM dbo.Ys_UrunKategoriler
    WHERE Ad = N'Şofben';

    SELECT @EskiSofbenId = MIN(Id)
    FROM dbo.Ys_UrunKategoriler
    WHERE Ad = N'Şofbenler';

    IF @SofbenId IS NULL AND @EskiSofbenId IS NOT NULL
    BEGIN
        UPDATE dbo.Ys_UrunKategoriler
        SET Ad = N'Şofben'
        WHERE Id = @EskiSofbenId;

        SET @SofbenId = @EskiSofbenId;
    END;

    IF @SofbenId IS NOT NULL
    BEGIN
        UPDATE bag
        SET KategoriId = @SofbenId,
            GuncellemeTarihi = GETDATE(),
            GuncelleyenKullanici = N'hizmet-turu-script'
        FROM dbo.Ys_FirmaKategoriler AS bag
        INNER JOIN dbo.Ys_UrunKategoriler AS kategori ON kategori.Id = bag.KategoriId
        WHERE kategori.Ad = N'Şofbenler';
    END;

    IF EXISTS (
        SELECT 1
        FROM dbo.Ys_UrunKategoriler AS kategori
        INNER JOIN @HizmetTurleri AS tur ON tur.Ad = kategori.Ad
        GROUP BY kategori.Ad
        HAVING COUNT(*) > 1
    )
        THROW 51001, N'Aynı hizmet türüne ait birden fazla kategori var; önce kayıtlar incelenmeli.', 1;

    INSERT INTO dbo.Ys_UrunKategoriler
        (Ad, IconUrl, SiraNo, AktifMi, OlusturmaTarihi, OlusturanKullanici, SilindiMi)
    SELECT tur.Ad, tur.IconUrl, tur.SiraNo, 1, GETDATE(), N'hizmet-turu-script', 0
    FROM @HizmetTurleri AS tur
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.Ys_UrunKategoriler AS kategori WHERE kategori.Ad = tur.Ad
    );

    UPDATE kategori
    SET SiraNo = tur.SiraNo,
        IconUrl = COALESCE(tur.IconUrl, kategori.IconUrl),
        AktifMi = 1,
        SilindiMi = 0,
        SilinmeTarihi = NULL,
        SilenKullanici = NULL,
        GuncellemeTarihi = GETDATE(),
        GuncelleyenKullanici = N'hizmet-turu-script'
    FROM dbo.Ys_UrunKategoriler AS kategori
    INNER JOIN @HizmetTurleri AS tur ON tur.Ad = kategori.Ad
    WHERE kategori.SiraNo <> tur.SiraNo
       OR kategori.AktifMi = 0
       OR kategori.SilindiMi = 1
       OR kategori.SilinmeTarihi IS NOT NULL
       OR kategori.SilenKullanici IS NOT NULL
       OR (tur.IconUrl IS NOT NULL AND (kategori.IconUrl IS NULL OR kategori.IconUrl <> tur.IconUrl));

    IF EXISTS (
        SELECT 1
        FROM dbo.Ys_Firmalar AS firma
        WHERE firma.SilindiMi = 0
          AND EXISTS (
              SELECT 1
              FROM dbo.Ys_FirmaKategoriler AS bag
              INNER JOIN dbo.Ys_UrunKategoriler AS kategori ON kategori.Id = bag.KategoriId
              WHERE bag.FirmaId = firma.Id AND bag.SilindiMi = 0
                AND NOT EXISTS (SELECT 1 FROM @HizmetTurleri AS tur WHERE tur.Ad = kategori.Ad)
          )
          AND NOT EXISTS (
              SELECT 1
              FROM dbo.Ys_FirmaKategoriler AS bag
              INNER JOIN dbo.Ys_UrunKategoriler AS kategori ON kategori.Id = bag.KategoriId
              INNER JOIN @HizmetTurleri AS tur ON tur.Ad = kategori.Ad
              WHERE bag.FirmaId = firma.Id AND bag.SilindiMi = 0
          )
    )
        THROW 51002, N'Yalnız kaldırılacak hizmet türlerine bağlı firma var; önce hizmet kapsamını güncelleyin.', 1;

    UPDATE bag
    SET SilindiMi = 1,
        SilinmeTarihi = GETDATE(),
        SilenKullanici = N'hizmet-turu-script',
        GuncellemeTarihi = GETDATE(),
        GuncelleyenKullanici = N'hizmet-turu-script'
    FROM dbo.Ys_FirmaKategoriler AS bag
    INNER JOIN dbo.Ys_UrunKategoriler AS kategori ON kategori.Id = bag.KategoriId
    WHERE bag.SilindiMi = 0
      AND NOT EXISTS (SELECT 1 FROM @HizmetTurleri AS tur WHERE tur.Ad = kategori.Ad);

    UPDATE kategori
    SET AktifMi = 0,
        SilindiMi = 1,
        SilinmeTarihi = GETDATE(),
        SilenKullanici = N'hizmet-turu-script',
        GuncellemeTarihi = GETDATE(),
        GuncelleyenKullanici = N'hizmet-turu-script'
    FROM dbo.Ys_UrunKategoriler AS kategori
    WHERE NOT EXISTS (SELECT 1 FROM @HizmetTurleri AS tur WHERE tur.Ad = kategori.Ad)
      AND (kategori.AktifMi = 1 OR kategori.SilindiMi = 0);

    IF (SELECT COUNT(*) FROM dbo.Ys_UrunKategoriler WHERE AktifMi = 1 AND SilindiMi = 0) <> 4
        THROW 51003, N'Aktif hizmet türü sayısı dört değil; işlem geri alındı.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
