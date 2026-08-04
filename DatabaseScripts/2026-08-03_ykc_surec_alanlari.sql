IF COL_LENGTH('dbo.Ykc_Talepler', 'IptalTarihi') IS NULL
BEGIN
    ALTER TABLE dbo.Ykc_Talepler ADD IptalTarihi DATETIME2 NULL;
END;

IF COL_LENGTH('dbo.Ykc_Talepler', 'IptalEdenKullaniciId') IS NULL
BEGIN
    ALTER TABLE dbo.Ykc_Talepler ADD IptalEdenKullaniciId NVARCHAR(450) NULL;
END;

IF COL_LENGTH('dbo.Ykc_Talepler', 'IptalAciklama') IS NULL
BEGIN
    ALTER TABLE dbo.Ykc_Talepler ADD IptalAciklama NVARCHAR(1000) NULL;
END;

IF COL_LENGTH('dbo.Ykc_Talepler', 'Aufnr') IS NULL
BEGIN
    ALTER TABLE dbo.Ykc_Talepler ADD Aufnr NVARCHAR(64) NULL;
END;
