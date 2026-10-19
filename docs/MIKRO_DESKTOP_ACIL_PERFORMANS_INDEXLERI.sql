/*
    MikroDesktop_FURPA_2024 icin canli kullanimda dogrulanan acil indeksler.

    Kapsam:
    - AXATA/depo sevk siparis satiri baglantisi
    - Kasa sayimi tarih, belge ve alt hareket sorgulari
    - Fiyati degisen urunler etiketi sorgusu

    Script yeniden calistirilabilir. Mevcut indekslere dokunmaz.
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

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI_EK')
      AND name = N'IX_FR_STHEK_SubeSipUid'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STHEK_SubeSipUid
    ON dbo.STOK_HAREKETLERI_EK (sth_subesip_uid)
    INCLUDE (sthek_related_uid)
    WHERE sth_subesip_uid IS NOT NULL
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Summaries')
      AND name = N'IX_FR_Summaries_DateBranch'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_Summaries_DateBranch
    ON dbo.Summaries (SummaryDate, BranchNo)
    INCLUDE
    (
        PaymentTypeID,
        DocumentSerie,
        DocumentOrderNo,
        CashNo,
        ZReportNo,
        CashierNo,
        ManagerNo,
        Amount
    )
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Summaries')
      AND name = N'IX_FR_Summaries_Document'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_Summaries_Document
    ON dbo.Summaries (BranchNo, DocumentSerie, DocumentOrderNo, PaymentTypeID)
    INCLUDE
    (
        Id,
        SummaryDate,
        CashNo,
        ZReportNo,
        CashierNo,
        ManagerNo,
        SlipNumber,
        Amount,
        TerminalId,
        Description
    )
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.BanknoteMovements')
      AND name = N'IX_FR_BanknoteMovements_Document'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_BanknoteMovements_Document
    ON dbo.BanknoteMovements (BranchNo, DocumentSerie, DocumentOrderNo)
    INCLUDE (BanknoteTypeID, Quantity, Total)
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.GiftCheckMovements')
      AND name = N'IX_FR_GiftCheckMovements_Document'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_GiftCheckMovements_Document
    ON dbo.GiftCheckMovements (BranchNo, DocumentSerie, DocumentOrderNo)
    INCLUDE (GiftCheckTypeID, Quantity, Total)
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_FIYAT_DEGISIKLIKLERI')
      AND name = N'IX_FR_FID_EtiketFiyatDegisikligi'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_FID_EtiketFiyatDegisikligi
    ON dbo.STOK_FIYAT_DEGISIKLIKLERI
    (
        fid_depo_no,
        fid_lastup_date DESC,
        fid_stok_kod
    )
    INCLUDE
    (
        fid_create_date,
        fid_belge_tarih,
        fid_tarih,
        fid_eskifiy_tutar,
        fid_yenifiy_tutar
    )
    WHERE fid_yapildi_fl = 1
      AND fid_lastup_date IS NOT NULL
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

SELECT
    OBJECT_NAME(i.object_id) AS table_name,
    i.name,
    i.is_disabled,
    i.has_filter,
    CAST(SUM(ps.reserved_page_count) * 8.0 / 1024.0 AS decimal(18, 2)) AS reserved_mb
FROM sys.indexes AS i
INNER JOIN sys.dm_db_partition_stats AS ps
    ON ps.object_id = i.object_id
   AND ps.index_id = i.index_id
WHERE i.name IN
(
    N'IX_FR_STHEK_SubeSipUid',
    N'IX_FR_Summaries_DateBranch',
    N'IX_FR_Summaries_Document',
    N'IX_FR_BanknoteMovements_Document',
    N'IX_FR_GiftCheckMovements_Document',
    N'IX_FR_FID_EtiketFiyatDegisikligi'
)
GROUP BY OBJECT_NAME(i.object_id), i.name, i.is_disabled, i.has_filter
ORDER BY table_name, i.name;
GO
