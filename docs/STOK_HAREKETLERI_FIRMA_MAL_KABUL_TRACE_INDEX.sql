/*
    Firma mal kabul guvenli retry/readback sorgulari icin kritik filtered index.

    Uygulama:
    1. FurpaMerkez IIS app pool'unu durdurun.
    2. MikroDesktop_FURPA_2024 veritabaninda bu scripti calistirin.
    3. Sonuc sorgusunda indexin is_disabled=0 oldugunu dogrulayin.
    4. App pool'u yeniden baslatin ve /health/ready kontrolunu yapin.

    Script yeniden calistirilabilir. Index mevcutsa veri degisikligi yapmaz.
    SQL Server 2017 CREATE INDEX icin WAIT_AT_LOW_PRIORITY desteklemediginden,
    aktif yazma trafigi varken calistirilmamalidir.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
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

IF DB_NAME() <> N'MikroDesktop_FURPA_2024'
BEGIN
    THROW 51000, 'Bu script MikroDesktop_FURPA_2024 veritabaninda calistirilmalidir.', 1;
END;
GO

IF OBJECT_ID(N'dbo.STOK_HAREKETLERI', N'U') IS NULL
BEGIN
    THROW 51001, 'dbo.STOK_HAREKETLERI tablosu bulunamadi.', 1;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_FirmaMalKabul_Trace'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_FirmaMalKabul_Trace
    ON dbo.STOK_HAREKETLERI
    (
        sth_eticaret_kanal_kodu,
        sth_satirno
    )
    INCLUDE
    (
        sth_tarih,
        sth_belge_tarih,
        sth_belge_no,
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_cari_kodu,
        sth_giris_depo_no,
        sth_stok_kod,
        sth_miktar,
        sth_tutar,
        sth_sip_uid
    )
    WHERE
        sth_evraktip = 13
        AND sth_tip = 0
        AND sth_normal_iade = 0
        AND sth_eticaret_kanal_kodu IS NOT NULL
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
    i.filter_definition,
    CAST(SUM(ps.reserved_page_count) * 8.0 / 1024.0 AS decimal(18, 2)) AS reserved_mb
FROM sys.indexes AS i
INNER JOIN sys.dm_db_partition_stats AS ps
    ON ps.object_id = i.object_id
   AND ps.index_id = i.index_id
WHERE i.object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
  AND i.name = N'IX_FR_STH_FirmaMalKabul_Trace'
GROUP BY
    i.name,
    i.is_disabled,
    i.has_filter,
    i.filter_definition;
GO
