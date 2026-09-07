IF OBJECT_ID(N'dbo.Ys_YetkiBelgeleri', N'U') IS NULL
    THROW 50001, 'dbo.Ys_YetkiBelgeleri tablosu bulunamadi.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Ys_YetkiBelgeleri_FirmaId_SilindiMi_OlusturmaTarihi'
      AND object_id = OBJECT_ID(N'dbo.Ys_YetkiBelgeleri')
)
BEGIN
    CREATE INDEX IX_Ys_YetkiBelgeleri_FirmaId_SilindiMi_OlusturmaTarihi
        ON dbo.Ys_YetkiBelgeleri (FirmaId, SilindiMi, OlusturmaTarihi DESC);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Ys_YetkiBelgeleri_Durum_SilindiMi_OlusturmaTarihi'
      AND object_id = OBJECT_ID(N'dbo.Ys_YetkiBelgeleri')
)
BEGIN
    CREATE INDEX IX_Ys_YetkiBelgeleri_Durum_SilindiMi_OlusturmaTarihi
        ON dbo.Ys_YetkiBelgeleri (Durum, SilindiMi, OlusturmaTarihi DESC)
        INCLUDE (FirmaId, YetkiBelgesiBaslangicTarihi, YetkiBelgesiBitisTarihi, DosyaYolu);
END;
