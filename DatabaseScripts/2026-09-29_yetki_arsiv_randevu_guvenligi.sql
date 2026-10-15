-- Run with sqlcmd -b before starting the updated API. No records are deleted.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
GO
IF OBJECT_ID(N'dbo.Ys_AspNetUsers', N'U') IS NULL
    OR OBJECT_ID(N'dbo.Ys_Dag_PersonelYetkiler', N'U') IS NULL
    OR OBJECT_ID(N'dbo.Ykc_Atamalar', N'U') IS NULL
    OR OBJECT_ID(N'dbo.Ykc_Fr265Kontroller', N'U') IS NULL
    THROW 51000, 'Required tables not found.', 1;
BEGIN TRANSACTION;
SELECT (SELECT COUNT_BIG(*) FROM dbo.Ys_AspNetUsers) AS Users,
       (SELECT COUNT_BIG(*) FROM dbo.Ys_Dag_PersonelYetkiler) AS Grants,
       (SELECT COUNT_BIG(*) FROM dbo.Ykc_Fr265Kontroller WHERE Sonuc <> N'BEKLIYOR') AS Results
INTO #Before;
GO
IF COL_LENGTH(N'dbo.Ys_AspNetUsers', N'ArsivlemeTarihi') IS NULL
    ALTER TABLE dbo.Ys_AspNetUsers ADD ArsivlemeTarihi datetime2 NULL;
IF COL_LENGTH(N'dbo.Ys_AspNetUsers', N'ArsivleyenKullaniciId') IS NULL
    ALTER TABLE dbo.Ys_AspNetUsers ADD ArsivleyenKullaniciId nvarchar(450) NULL;
IF COL_LENGTH(N'dbo.Ykc_Fr265Kontroller', N'AtamaId') IS NULL
    ALTER TABLE dbo.Ykc_Fr265Kontroller ADD AtamaId int NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Ys_AspNetUsers_ArsivPasif')
    ALTER TABLE dbo.Ys_AspNetUsers WITH CHECK ADD CONSTRAINT CK_Ys_AspNetUsers_ArsivPasif
        CHECK (ArsivlemeTarihi IS NULL OR AktifMi = 0);

DECLARE @drop nvarchar(max) = N'';
SELECT @drop += N'ALTER TABLE dbo.Ys_Dag_PersonelYetkiler DROP CONSTRAINT ' + QUOTENAME(f.name) + N';'
FROM sys.foreign_keys f
JOIN sys.foreign_key_columns c ON c.constraint_object_id = f.object_id
WHERE f.parent_object_id = OBJECT_ID(N'dbo.Ys_Dag_PersonelYetkiler')
  AND f.referenced_object_id = OBJECT_ID(N'dbo.Ys_AspNetUsers')
  AND COL_NAME(c.parent_object_id, c.parent_column_id) = N'KullaniciId'
  AND f.delete_referential_action <> 0;
IF @drop <> N'' EXEC sys.sp_executesql @drop;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.Ys_Dag_PersonelYetkiler')
      AND referenced_object_id = OBJECT_ID(N'dbo.Ys_AspNetUsers'))
    ALTER TABLE dbo.Ys_Dag_PersonelYetkiler WITH CHECK
        ADD CONSTRAINT FK_Ys_Dag_PersonelYetkiler_Ys_AspNetUsers_KullaniciId
        FOREIGN KEY (KullaniciId) REFERENCES dbo.Ys_AspNetUsers(Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.Ykc_Fr265Kontroller')
      AND referenced_object_id = OBJECT_ID(N'dbo.Ykc_Atamalar'))
    ALTER TABLE dbo.Ykc_Fr265Kontroller WITH CHECK
        ADD CONSTRAINT FK_Ykc_Fr265Kontroller_Ykc_Atamalar_AtamaId
        FOREIGN KEY (AtamaId) REFERENCES dbo.Ykc_Atamalar(Id) ON DELETE NO ACTION;
IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Ykc_Fr265Kontroller') AND name = N'IX_Ykc_Fr265Kontroller_AtamaId')
    CREATE INDEX IX_Ykc_Fr265Kontroller_AtamaId ON dbo.Ykc_Fr265Kontroller(AtamaId);

-- Only associate a historical result with the latest appointment existing at that time.
-- An appointment scheduled for after the result cannot validate it retrospectively.
UPDATE k SET AtamaId = a.Id
FROM dbo.Ykc_Fr265Kontroller k
CROSS APPLY (SELECT TOP (1) x.Id, x.RandevuTarihi, x.RandevuSaati
    FROM dbo.Ykc_Atamalar x WHERE x.TalepId = k.TalepId AND x.SilindiMi = 0
      AND x.OlusturmaTarihi <= k.KontrolTarihi ORDER BY x.Id DESC) a
WHERE k.AtamaId IS NULL AND k.KontrolTarihi IS NOT NULL AND k.Sonuc IN (N'UYGUN', N'UYGUN_DEGIL')
  AND DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00' AS time), TRY_CONVERT(time, a.RandevuSaati)),
      CAST(CAST(a.RandevuTarihi AS date) AS datetime2)) <= k.KontrolTarihi;

-- Repair only open, unsigned work whose former success no longer belongs to its appointment.
SELECT t.Id, t.Durum AS EskiDurum, ((n.MaxNo - 1) / 5 + 1) * 5 + 1 AS YeniKontrolNo,
    CAST(CASE WHEN k.Id IS NOT NULL AND (k.AtamaId IS NULL OR k.AtamaId <> a.Id OR a.Id IS NULL)
        THEN 1 ELSE 0 END AS bit) AS YeniDonem,
    CAST(CASE WHEN r.Zaman > GETDATE() THEN 1 ELSE 0 END AS bit) AS GelecekRandevu
INTO #Repair
FROM dbo.Ykc_Talepler t
CROSS APPLY (SELECT ISNULL(MAX(KontrolNo), 1) AS MaxNo FROM dbo.Ykc_Fr265Kontroller
    WHERE TalepId = t.Id AND SilindiMi = 0) n
OUTER APPLY (SELECT TOP (1) Id FROM dbo.Ykc_Atamalar
    WHERE TalepId = t.Id AND SilindiMi = 0 ORDER BY Id DESC) a
OUTER APPLY (SELECT TOP (1) Id, AtamaId FROM dbo.Ykc_Fr265Kontroller
    WHERE TalepId = t.Id AND SilindiMi = 0 AND Sonuc = N'UYGUN'
      AND KontrolNo >= ((n.MaxNo - 1) / 5) * 5 + 1 ORDER BY KontrolNo DESC) k
CROSS APPLY (SELECT DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00' AS time), TRY_CONVERT(time, t.RandevuSaati)),
    CAST(CAST(t.RandevuTarihi AS date) AS datetime2)) AS Zaman) r
WHERE t.SilindiMi = 0 AND t.Durum IN (3, 4)
  AND NOT EXISTS (SELECT 1 FROM dbo.Ykc_ImzaSurecleri s WHERE s.TalepId = t.Id AND s.SilindiMi = 0
    AND (NULLIF(LTRIM(RTRIM(s.ProviderDocumentId)), N'') IS NOT NULL
      OR s.Durum IN (N'IMZAYA_GONDERILDI', N'IMZA_BEKLIYOR', N'KISMI_IMZALI', N'TAMAMLANDI')))
  AND ((t.Durum = 4 AND r.Zaman > GETDATE())
    OR (k.Id IS NOT NULL AND (k.AtamaId IS NULL OR k.AtamaId <> a.Id OR a.Id IS NULL)));

INSERT dbo.Ykc_Fr265Kontroller (TalepId, KontrolNo, Sonuc, OlusturmaTarihi, OlusturanKullanici, SilindiMi)
SELECT r.Id, r.YeniKontrolNo + n.No, N'BEKLIYOR', GETDATE(), N'migration:appointment-control', 0
FROM #Repair r CROSS JOIN (VALUES(0),(1),(2),(3),(4)) n(No) WHERE r.YeniDonem = 1;
UPDATE t SET Durum = CASE WHEN r.GelecekRandevu = 1 THEN 3 ELSE t.Durum END,
    Fr265BelgeVersiyonNo = CASE WHEN t.Fr265BelgeVersiyonNo < 1 THEN 2 ELSE t.Fr265BelgeVersiyonNo + 1 END,
    Fr265BelgeHash = NULL, Fr265BelgeOlusturmaTarihi = NULL,
    GuncellemeTarihi = GETDATE(), GuncelleyenKullanici = N'migration:appointment-control'
FROM dbo.Ykc_Talepler t JOIN #Repair r ON r.Id = t.Id;
INSERT dbo.Ykc_IslemGecmisi (TalepId, IslemTipi, EskiDurum, YeniDurum, Aciklama, OlusturmaTarihi, OlusturanKullanici, SilindiMi)
SELECT r.Id, N'RandevuKontrolBaglantisiDuzeltildi', r.EskiDurum, t.Durum,
    N'Önceki kontrol sonuçları korundu. Güncel randevu için yeni kontrol sonucu bekleniyor.',
    GETDATE(), N'migration:appointment-control', 0
FROM #Repair r JOIN dbo.Ykc_Talepler t ON t.Id = r.Id;

IF EXISTS (SELECT 1 FROM #Before WHERE Users <> (SELECT COUNT_BIG(*) FROM dbo.Ys_AspNetUsers)
    OR Grants <> (SELECT COUNT_BIG(*) FROM dbo.Ys_Dag_PersonelYetkiler)
    OR Results <> (SELECT COUNT_BIG(*) FROM dbo.Ykc_Fr265Kontroller WHERE Sonuc <> N'BEKLIYOR'))
    THROW 51000, 'History counts changed; migration aborted.', 1;
SELECT COUNT(*) AS RepairedOpenRequests FROM #Repair;
COMMIT;
DROP TABLE #Before;
DROP TABLE #Repair;
GO
