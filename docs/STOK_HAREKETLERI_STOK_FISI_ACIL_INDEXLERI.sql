/*
    Zayiat/masraf liste ve guvenli readback sorgulari icin acil filtered indexler.

    Canli bulgu (2026-10-19):
    - dbo.STOK_HAREKETLERI yaklasik 218 milyon satirdi.
    - Depo + tarih zayiat liste sorgusu uzun tablo taramasi yaparak BULK INSERT
      create islemlerini bloke ediyordu.
    - Bu script veri degistirmez ve unique index olusturmaz.

    Uygulama:
    - Mikro DB uzerinde, dusuk trafik/bakim penceresinde calistirin.
    - Once devam eden create isteklerinin bitmesini bekleyin.
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
      AND name = N'IX_FR_STH_StokFisi_Trace'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_StokFisi_Trace
    ON dbo.STOK_HAREKETLERI
    (
        sth_eticaret_kanal_kodu,
        sth_cikis_depo_no,
        sth_cins,
        sth_tarih,
        sth_satirno
    )
    INCLUDE
    (
        sth_belge_tarih,
        sth_belge_no,
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_HareketGrupKodu1,
        sth_HareketGrupKodu2,
        sth_miktar,
        sth_tutar
    )
    WHERE
        sth_evraktip = 0
        AND sth_tip = 1
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
        AND sth_belge_tarih IS NOT NULL
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
      N'IX_FR_STH_StokFisi_Trace',
      N'IX_FR_STH_StokFisi_Liste'
  )
ORDER BY i.name;
GO
