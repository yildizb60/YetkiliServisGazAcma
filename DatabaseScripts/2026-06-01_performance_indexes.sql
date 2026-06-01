IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ys_AspNetUsers_KullaniciTipi_AktifMi_FirmaId' AND object_id = OBJECT_ID(N'[dbo].[Ys_AspNetUsers]'))
    CREATE INDEX IX_Ys_AspNetUsers_KullaniciTipi_AktifMi_FirmaId ON dbo.Ys_AspNetUsers (KullaniciTipi, AktifMi, FirmaId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ys_AspNetUsers_KullaniciTipi_AktifMi_SirketId' AND object_id = OBJECT_ID(N'[dbo].[Ys_AspNetUsers]'))
    CREATE INDEX IX_Ys_AspNetUsers_KullaniciTipi_AktifMi_SirketId ON dbo.Ys_AspNetUsers (KullaniciTipi, AktifMi, SirketId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ys_Firmalar_SirketId_SilindiMi_AktifMi' AND object_id = OBJECT_ID(N'[dbo].[Ys_Firmalar]'))
    CREATE INDEX IX_Ys_Firmalar_SirketId_SilindiMi_AktifMi ON dbo.Ys_Firmalar (SirketId, SilindiMi, AktifMi);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ys_Firmalar_FaaliyetIli_SilindiMi' AND object_id = OBJECT_ID(N'[dbo].[Ys_Firmalar]'))
    CREATE INDEX IX_Ys_Firmalar_FaaliyetIli_SilindiMi ON dbo.Ys_Firmalar (FaaliyetIli, SilindiMi);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ys_Subeler_FirmaId_SilindiMi_AktifMi' AND object_id = OBJECT_ID(N'[dbo].[Ys_Subeler]'))
    CREATE INDEX IX_Ys_Subeler_FirmaId_SilindiMi_AktifMi ON dbo.Ys_Subeler (FirmaId, SilindiMi, AktifMi);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ys_Sertifikalar_FirmaId_Durum_SilindiMi' AND object_id = OBJECT_ID(N'[dbo].[Ys_Sertifikalar]'))
    CREATE INDEX IX_Ys_Sertifikalar_FirmaId_Durum_SilindiMi ON dbo.Ys_Sertifikalar (FirmaId, Durum, SilindiMi);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ys_Sertifikalar_SertifikaBitisTarihi_SilindiMi' AND object_id = OBJECT_ID(N'[dbo].[Ys_Sertifikalar]'))
    CREATE INDEX IX_Ys_Sertifikalar_SertifikaBitisTarihi_SilindiMi ON dbo.Ys_Sertifikalar (SertifikaBitisTarihi, SilindiMi);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ys_FirmaMarkalar_FirmaId_MarkaId_SilindiMi' AND object_id = OBJECT_ID(N'[dbo].[Ys_FirmaMarkalar]'))
    CREATE INDEX IX_Ys_FirmaMarkalar_FirmaId_MarkaId_SilindiMi ON dbo.Ys_FirmaMarkalar (FirmaId, MarkaId, SilindiMi);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ys_FirmaKategoriler_FirmaId_KategoriId_SilindiMi' AND object_id = OBJECT_ID(N'[dbo].[Ys_FirmaKategoriler]'))
    CREATE INDEX IX_Ys_FirmaKategoriler_FirmaId_KategoriId_SilindiMi ON dbo.Ys_FirmaKategoriler (FirmaId, KategoriId, SilindiMi);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ys_DevreyeAlmalar_FirmaId_DevreyeAlmaTarihi_SilindiMi' AND object_id = OBJECT_ID(N'[dbo].[Ys_DevreyeAlmalar]'))
    CREATE INDEX IX_Ys_DevreyeAlmalar_FirmaId_DevreyeAlmaTarihi_SilindiMi ON dbo.Ys_DevreyeAlmalar (FirmaId, DevreyeAlmaTarihi, SilindiMi);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ys_DevreyeAlmalar_MarkaId_Durum_SilindiMi' AND object_id = OBJECT_ID(N'[dbo].[Ys_DevreyeAlmalar]'))
    CREATE INDEX IX_Ys_DevreyeAlmalar_MarkaId_Durum_SilindiMi ON dbo.Ys_DevreyeAlmalar (MarkaId, Durum, SilindiMi);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ys_SmsDogrulamaKodlari_KullaniciId_Amac_KullanildiMi_SilindiMi_GecerlilikTarihi' AND object_id = OBJECT_ID(N'[dbo].[Ys_SmsDogrulamaKodlari]'))
    CREATE INDEX IX_Ys_SmsDogrulamaKodlari_KullaniciId_Amac_KullanildiMi_SilindiMi_GecerlilikTarihi ON dbo.Ys_SmsDogrulamaKodlari (KullaniciId, Amac, KullanildiMi, SilindiMi, GecerlilikTarihi);
