-- Adds a supporting index only; does not modify appointment records.
IF OBJECT_ID(N'dbo.Ykc_Talepler', N'U') IS NULL
    THROW 50001, 'dbo.Ykc_Talepler tablosu bulunamadi.', 1;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Ykc_Talepler')
      AND name = N'IX_Ykc_Talepler_RandevuTarihi_SilindiMi'
)
BEGIN
    CREATE INDEX IX_Ykc_Talepler_RandevuTarihi_SilindiMi
        ON dbo.Ykc_Talepler (RandevuTarihi, SilindiMi)
        INCLUDE (SirketId, AtananKullaniciId, Bolge, AtananEkip, Durum, RandevuSaati);
END;
