/*
    MUHASEBE_FISLERI duzeltme denetim tablolarini raporlar ve istege bagli siler.

    Bu script duzeltme yapmaz. Varsayilan @DropAuditTables = 0 yalniz rapor verir.
    Tablolari ancak pilot ve toplu kontroller tamamlandiktan, gerekli yedek
    alindiktan ve geri alma ihtiyaci kalmadiktan sonra silin.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DropAuditTables bit = 0;
DECLARE @ConfirmDrop nvarchar(50) = N'';

/* 1. Once tablo ve kayit envanterini gor. */
SELECT
    t.name AS TableName,
    SUM(p.rows) AS [RowCount],
    t.create_date AS CreatedAt,
    t.modify_date AS LastModifiedAt
FROM sys.tables AS t
LEFT JOIN sys.partitions AS p
    ON p.object_id = t.object_id
   AND p.index_id IN (0, 1)
WHERE t.schema_id = SCHEMA_ID('dbo')
  AND t.name IN
  (
      'FURPA_MUH_REPAIR_BATCH_20261028',
      'FURPA_MUH_REPAIR_JOURNAL_20261028',
      'FURPA_MUH_REPAIR_INSERT_20261028',
      'FURPA_MUH_REPAIR_STOCK_20261028'
  )
GROUP BY t.name, t.create_date, t.modify_date
ORDER BY t.name;

IF OBJECT_ID('dbo.FURPA_MUH_REPAIR_BATCH_20261028', 'U') IS NOT NULL
BEGIN
    SELECT *
    FROM dbo.FURPA_MUH_REPAIR_BATCH_20261028
    ORDER BY StartedAt DESC;
END;

IF @DropAuditTables = 0
BEGIN
    PRINT 'SADECE RAPOR: Hicbir denetim tablosu silinmedi.';
    RETURN;
END;

/* Yanlislikla calistirmayi onleyen ikinci onay. */
IF @ConfirmDrop <> N'FURPA_MUH_REPAIR_AUDIT_DROP'
    THROW 51100, 'Silme onayi gecersiz. ConfirmDrop degerini bilincli olarak girin.', 1;

IF OBJECT_ID('dbo.FURPA_MUH_REPAIR_BATCH_20261028', 'U') IS NOT NULL
   AND EXISTS
   (
       SELECT 1
       FROM dbo.FURPA_MUH_REPAIR_BATCH_20261028
       WHERE Status = N'Running'
   )
    THROW 51101, 'Running durumunda duzeltme batch kaydi var. Denetim tablolari silinmedi.', 1;

BEGIN TRANSACTION;

DROP TABLE IF EXISTS dbo.FURPA_MUH_REPAIR_STOCK_20261028;
DROP TABLE IF EXISTS dbo.FURPA_MUH_REPAIR_INSERT_20261028;
DROP TABLE IF EXISTS dbo.FURPA_MUH_REPAIR_JOURNAL_20261028;
DROP TABLE IF EXISTS dbo.FURPA_MUH_REPAIR_BATCH_20261028;

COMMIT TRANSACTION;

PRINT 'FURPA muhasebe duzeltme denetim tablolari silindi.';
