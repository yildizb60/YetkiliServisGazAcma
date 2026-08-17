SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.Ykc_FormDosyalari', 'U') IS NOT NULL
BEGIN
    UPDATE dbo.Ykc_FormDosyalari
    SET DepolamaTuru = 'LEGACY_WWWROOT'
    WHERE (DosyaYolu LIKE '/uploads/ykc/%' OR DosyaYolu LIKE 'uploads/ykc/%')
      AND (DepolamaTuru IS NULL OR DepolamaTuru <> 'LEGACY_WWWROOT');
END;

IF OBJECT_ID('dbo.Ykc_ImzaSurecleri', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.Ykc_FormDosyalari', 'U') IS NOT NULL
BEGIN
    UPDATE dosya
    SET DosyaTuru = 'FR265_IMZALI_ADAY',
        GuncellemeTarihi = GETDATE(),
        GuncelleyenKullanici = 'YKC_IMZA_AKIS_DUZELTMESI'
    FROM dbo.Ykc_FormDosyalari dosya
    WHERE dosya.DosyaTuru = 'FR265_IMZALI_NIHAI'
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.Ykc_ImzaSurecleri surec
          WHERE surec.NihaiDosyaId = dosya.Id
            AND surec.Durum = 'TAMAMLANDI'
            AND NULLIF(LTRIM(RTRIM(surec.ProviderDocumentId)), '') IS NOT NULL
            AND surec.SilindiMi = 0
      );

    UPDATE surec
    SET Durum = 'HAZIR',
        TamamlanmaTarihi = NULL,
        SonKontrolTarihi = NULL,
        NihaiDosyaId = NULL,
        BelgeHash = NULL,
        BelgeOlusturmaTarihi = NULL,
        HataKodu = NULL,
        HataMesaji = NULL,
        GuncellemeTarihi = GETDATE(),
        GuncelleyenKullanici = 'YKC_IMZA_AKIS_DUZELTMESI'
    FROM dbo.Ykc_ImzaSurecleri surec
    WHERE NULLIF(LTRIM(RTRIM(surec.ProviderDocumentId)), '') IS NULL
      AND surec.Durum = 'TAMAMLANDI';

    UPDATE surec
    SET BelgeHash = NULL,
        BelgeOlusturmaTarihi = NULL,
        GuncellemeTarihi = GETDATE(),
        GuncelleyenKullanici = 'YKC_IMZA_AKIS_DUZELTMESI'
    FROM dbo.Ykc_ImzaSurecleri surec
    WHERE surec.Durum IN ('HAZIR', 'HATA')
      AND NULLIF(LTRIM(RTRIM(surec.ProviderDocumentId)), '') IS NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.Ykc_FormDosyalari dosya
          WHERE dosya.TalepId = surec.TalepId
            AND dosya.DosyaTuru IN ('FR265_TASLAK', 'FR265_IMZAYA_GONDERILEN')
            AND dosya.SilindiMi = 0
            AND dosya.BelgeHash = surec.BelgeHash
      );
END;

IF OBJECT_ID('dbo.Ykc_Talepler', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.Ykc_FormDosyalari', 'U') IS NOT NULL
BEGIN
    UPDATE talep
    SET Fr265BelgeHash = NULL,
        Fr265BelgeOlusturmaTarihi = NULL,
        GuncellemeTarihi = GETDATE(),
        GuncelleyenKullanici = 'YKC_IMZA_AKIS_DUZELTMESI'
    FROM dbo.Ykc_Talepler talep
    WHERE talep.Fr265BelgeHash IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.Ykc_FormDosyalari dosya
          WHERE dosya.TalepId = talep.Id
            AND dosya.DosyaTuru IN ('FR265_TASLAK', 'FR265_IMZAYA_GONDERILEN')
            AND dosya.SilindiMi = 0
            AND dosya.BelgeHash = talep.Fr265BelgeHash
      );
END;

COMMIT TRANSACTION;
