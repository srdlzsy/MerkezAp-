/*
    Depolar arasi sevk guvenli trace/readback ve son create duplicate kontrolu
    icin acil filtered indexler.

    Canli bulgu (2026-10-19):
    - TryFindRecentDuplicateAsync sorgusu genel sevk/list indeksleriyle 240 saniye bekledi.
    - Trace readback, trace anahtari indeksin ilk anahtari olmadigi icin gereksiz satir okuyabiliyordu.
    - Bu script veri degistirmez ve unique index olusturmaz.

    Uygulama:
    - Mikro DB uzerinde dusuk trafik/bakim penceresinde calistirin.
    - ONLINE yine de index baslangic/bitisinde kisa schema lock isteyebilir.
    - 60 saniyede gerekli lock alinamazsa komut iptal olur; yogun saatte zorlamayin.
*/

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET DEADLOCK_PRIORITY LOW;
SET LOCK_TIMEOUT 60000;
GO

IF OBJECT_ID(N'dbo.STOK_HAREKETLERI', N'U') IS NULL
BEGIN
    THROW 51000, 'dbo.STOK_HAREKETLERI tablosu bulunamadi. Script Mikro DB uzerinde calistirilmali.', 1;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_DepoSevk_Trace'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_DepoSevk_Trace
    ON dbo.STOK_HAREKETLERI
    (
        sth_eticaret_kanal_kodu,
        sth_cikis_depo_no,
        sth_tarih,
        sth_satirno
    )
    INCLUDE
    (
        sth_belge_tarih,
        sth_belge_no,
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_nakliyedeposu,
        sth_giris_depo_no,
        sth_miktar,
        sth_tutar,
        sth_stok_kod,
        sth_birim_pntr,
        sth_aciklama,
        sth_parti_kodu,
        sth_lot_no,
        sth_proje_kodu,
        sth_cari_srm_merkezi,
        sth_stok_srm_merkezi
    )
    WHERE
        sth_evraktip = 17
        AND sth_tip = 2
        AND sth_cins = 6
        AND sth_normal_iade = 0
        AND sth_eticaret_kanal_kodu IS NOT NULL
        AND sth_tarih IS NOT NULL
    WITH
    (
        ONLINE = ON,
        MAXDOP = 2,
        SORT_IN_TEMPDB = OFF
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_DepoSevk_Duplicate'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_DepoSevk_Duplicate
    ON dbo.STOK_HAREKETLERI
    (
        sth_cikis_depo_no,
        sth_nakliyedeposu,
        sth_giris_depo_no,
        sth_tarih,
        sth_belge_tarih,
        sth_evrakno_seri,
        sth_create_date,
        sth_evrakno_sira,
        sth_satirno
    )
    INCLUDE
    (
        sth_belge_no,
        sth_miktar,
        sth_tutar,
        sth_stok_kod,
        sth_birim_pntr,
        sth_aciklama,
        sth_parti_kodu,
        sth_lot_no,
        sth_proje_kodu,
        sth_cari_srm_merkezi,
        sth_stok_srm_merkezi,
        sth_eticaret_kanal_kodu
    )
    WHERE
        sth_evraktip = 17
        AND sth_tip = 2
        AND sth_cins = 6
        AND sth_normal_iade = 0
        AND sth_create_date IS NOT NULL
        AND sth_evrakno_sira IS NOT NULL
        AND sth_satirno IS NOT NULL
    WITH
    (
        ONLINE = ON,
        MAXDOP = 2,
        SORT_IN_TEMPDB = OFF
    );
END;
GO

SELECT
    i.name,
    i.is_disabled,
    i.has_filter,
    i.filter_definition
FROM sys.indexes AS i
WHERE i.object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
  AND i.name IN
  (
      N'IX_FR_STH_DepoSevk_Trace',
      N'IX_FR_STH_DepoSevk_Duplicate'
  )
ORDER BY i.name;
GO
