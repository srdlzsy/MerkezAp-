/*
    STOK_HAREKETLERI liste ekranlari icin onerilen filtered index scripti.

    Amac:
    - Firma mal kabul / firma sevk / firma iade listelerini hizlandirmak.
    - Depolar arasi sevk, depo mal kabul bekleyen/gelen ve depo iade listelerini hizlandirmak.
    - Zayiat, masraf fisi ve virman listelerine dar kapsamli destek vermek.

    Notlar:
    - Bu script Mikro DB uzerinde manuel/kontrollu calistirilmalidir.
    - Canlida mesai disi uygulanmasi onerilir.
    - Indexler unique degildir; duplicate key riski olusturmaz.
    - CREATE INDEX sirasinda tablo buyukse gecici IO/CPU yuku olusturur.
*/

IF OBJECT_ID(N'dbo.STOK_HAREKETLERI', N'U') IS NULL
BEGIN
    THROW 51000, 'dbo.STOK_HAREKETLERI tablosu bulunamadi. Script Mikro DB uzerinde calistirilmali.', 1;
END;
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes AS i
    WHERE i.object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND i.name = N'IX_FR_STH_FirmaMalKabul_Liste'
      AND NOT EXISTS (
          SELECT 1
          FROM sys.index_columns AS ic
          INNER JOIN sys.columns AS c
              ON c.object_id = ic.object_id
             AND c.column_id = ic.column_id
          WHERE ic.object_id = i.object_id
            AND ic.index_id = i.index_id
            AND ic.key_ordinal = 2
            AND c.name = N'sth_tarih'
      )
)
BEGIN
    DROP INDEX IX_FR_STH_FirmaMalKabul_Liste
    ON dbo.STOK_HAREKETLERI;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_FirmaMalKabul_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_FirmaMalKabul_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_giris_depo_no,
        sth_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_belge_no,
        sth_belge_tarih,
        sth_create_date,
        sth_cari_kodu,
        sth_cikis_depo_no,
        sth_evraktip,
        sth_tip,
        sth_normal_iade,
        sth_miktar,
        sth_tutar
    )
    WHERE
        sth_evraktip = 13
        AND sth_tip = 0
        AND sth_normal_iade = 0
        AND sth_tarih IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_FirmaCikis_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_FirmaCikis_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_cikis_depo_no,
        sth_normal_iade,
        sth_belge_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_belge_no,
        sth_tarih,
        sth_create_date,
        sth_cari_kodu,
        sth_giris_depo_no,
        sth_evraktip,
        sth_tip,
        sth_miktar,
        sth_tutar
    )
    WHERE
        sth_evraktip = 1
        AND sth_tip = 1
        AND sth_belge_tarih IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_DepoSevkCikis_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_DepoSevkCikis_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_cikis_depo_no,
        sth_normal_iade,
        sth_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_belge_no,
        sth_belge_tarih,
        sth_giris_depo_no,
        sth_nakliyedeposu,
        sth_nakliyedurumu,
        sth_HareketGrupKodu1,
        sth_HareketGrupKodu3,
        sth_ismerkezi_kodu,
        sth_aciklama,
        sth_miktar
    )
    WHERE
        sth_evraktip = 17
        AND sth_tarih IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_DepoSevkNakliye_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_DepoSevkNakliye_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_nakliyedeposu,
        sth_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_belge_no,
        sth_belge_tarih,
        sth_cikis_depo_no,
        sth_giris_depo_no,
        sth_nakliyedurumu,
        sth_normal_iade,
        sth_HareketGrupKodu1,
        sth_HareketGrupKodu3,
        sth_ismerkezi_kodu,
        sth_aciklama,
        sth_miktar
    )
    WHERE
        sth_evraktip = 17
        AND sth_tarih IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_DepoSevkGiris_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_DepoSevkGiris_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_giris_depo_no,
        sth_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_belge_no,
        sth_belge_tarih,
        sth_cikis_depo_no,
        sth_nakliyedeposu,
        sth_nakliyedurumu,
        sth_normal_iade,
        sth_HareketGrupKodu1,
        sth_HareketGrupKodu3,
        sth_ismerkezi_kodu,
        sth_aciklama,
        sth_miktar
    )
    WHERE
        sth_evraktip = 17
        AND sth_tarih IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_StokFisi_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_StokFisi_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_cikis_depo_no,
        sth_cins,
        sth_belge_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_belge_no,
        sth_tarih,
        sth_create_date,
        sth_HareketGrupKodu1,
        sth_HareketGrupKodu2,
        sth_isemri_gider_kodu,
        sth_evraktip,
        sth_tip,
        sth_aciklama,
        sth_miktar,
        sth_tutar
    )
    WHERE
        sth_evraktip = 0
        AND sth_tip = 1
        AND sth_normal_iade = 0
        AND sth_belge_tarih IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_Virman_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_Virman_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_cikis_depo_no,
        sth_belge_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_belge_no,
        sth_tarih,
        sth_create_date,
        sth_evraktip,
        sth_cins,
        sth_tip,
        sth_aciklama,
        sth_miktar,
        sth_tutar
    )
    WHERE
        sth_evraktip = 6
        AND sth_cins = 3
        AND sth_normal_iade = 0
        AND sth_belge_tarih IS NOT NULL;
END;
GO
