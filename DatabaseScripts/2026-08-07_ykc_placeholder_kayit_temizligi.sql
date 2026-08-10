-- Swagger/test ekranindan "string" placeholder degerleriyle olusan YKC kayitlarini
-- operasyonel liste ve raporlardan kaldirmak icin soft-delete scripti.
-- Fiziksel silme yapmaz; denetim izi icin satirlari korur.

DECLARE @SilinenTalepler TABLE (Id INT PRIMARY KEY);

UPDATE t
SET
    t.SilindiMi = 1,
    t.SilinmeTarihi = COALESCE(t.SilinmeTarihi, GETDATE()),
    t.SilenKullanici = COALESCE(t.SilenKullanici, 'ykc-placeholder-cleanup')
OUTPUT inserted.Id INTO @SilinenTalepler(Id)
FROM dbo.Ykc_Talepler t
WHERE t.SilindiMi = 0
  AND (
        LTRIM(RTRIM(ISNULL(t.TesisatNo, ''))) = 'string'
        OR LTRIM(RTRIM(ISNULL(t.MusteriAdi, ''))) = 'string'
        OR (
            LTRIM(RTRIM(ISNULL(t.ProjeNo, ''))) = 'string'
            AND LTRIM(RTRIM(ISNULL(t.SayacNo, ''))) = 'string'
            AND LTRIM(RTRIM(ISNULL(t.YeniKapasite, ''))) = 'string'
        )
  );

UPDATE a
SET
    a.SilindiMi = 1,
    a.SilinmeTarihi = COALESCE(a.SilinmeTarihi, GETDATE()),
    a.SilenKullanici = COALESCE(a.SilenKullanici, 'ykc-placeholder-cleanup')
FROM dbo.Ykc_Atamalar a
INNER JOIN @SilinenTalepler s ON s.Id = a.TalepId
WHERE a.SilindiMi = 0;

UPDATE f
SET
    f.SilindiMi = 1,
    f.SilinmeTarihi = COALESCE(f.SilinmeTarihi, GETDATE()),
    f.SilenKullanici = COALESCE(f.SilenKullanici, 'ykc-placeholder-cleanup')
FROM dbo.Ykc_FormDosyalari f
INNER JOIN @SilinenTalepler s ON s.Id = f.TalepId
WHERE f.SilindiMi = 0;

UPDATE g
SET
    g.SilindiMi = 1,
    g.SilinmeTarihi = COALESCE(g.SilinmeTarihi, GETDATE()),
    g.SilenKullanici = COALESCE(g.SilenKullanici, 'ykc-placeholder-cleanup')
FROM dbo.Ykc_IslemGecmisi g
INNER JOIN @SilinenTalepler s ON s.Id = g.TalepId
WHERE g.SilindiMi = 0;

SELECT Id AS SoftDeletedYkcTalepId
FROM @SilinenTalepler
ORDER BY Id;
