-- Idempotent Migration: Add PaymentTypeNo to Furpa.dbo.CashRegisterDetails and backfill from Furpa.dbo.PaymentTypes
-- Target Database: Furpa (Server: 10.0.0.207)

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 1. Add Column if not exists
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'CashRegisterDetails'
      AND COLUMN_NAME = 'PaymentTypeNo'
)
BEGIN
    PRINT 'Adding column PaymentTypeNo to Furpa.dbo.CashRegisterDetails...';
    ALTER TABLE dbo.CashRegisterDetails
    ADD PaymentTypeNo INT NULL;
    PRINT 'Column PaymentTypeNo added.';
END
ELSE
BEGIN
    PRINT 'Column PaymentTypeNo already exists on Furpa.dbo.CashRegisterDetails.';
END
GO

-- 2. Backfill PaymentTypeNo from Furpa.dbo.PaymentTypes (Genus = 1 Bank catalog)
PRINT 'Backfilling PaymentTypeNo from Furpa.dbo.PaymentTypes...';

UPDATE d
SET d.PaymentTypeNo = p.PaymentTypeNo
FROM dbo.CashRegisterDetails d
INNER JOIN dbo.PaymentTypes p
    ON LTRIM(RTRIM(p.PaymentName)) = LTRIM(RTRIM(d.Bank))
   AND p.PaymentGenus = 1
WHERE d.PaymentTypeNo IS NULL
   OR d.PaymentTypeNo <> p.PaymentTypeNo;

PRINT CONCAT('Backfill complete. Rows affected: ', @@ROWCOUNT);
GO

-- 3. Create index on (CashNo, PaymentTypeNo) if not exists
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_CashRegisterDetails_CashNo_PaymentTypeNo'
      AND object_id = OBJECT_ID('dbo.CashRegisterDetails')
)
BEGIN
    PRINT 'Creating index IX_CashRegisterDetails_CashNo_PaymentTypeNo...';
    CREATE NONCLUSTERED INDEX IX_CashRegisterDetails_CashNo_PaymentTypeNo
    ON dbo.CashRegisterDetails (CashNo, PaymentTypeNo);
    PRINT 'Index created.';
END
ELSE
BEGIN
    PRINT 'Index IX_CashRegisterDetails_CashNo_PaymentTypeNo already exists.';
END
GO

-- 4. PaymentTypeNo must be unique before it can be a referenced catalog key.
IF EXISTS
(
    SELECT 1
    FROM dbo.PaymentTypes
    GROUP BY PaymentTypeNo
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51000, 'PaymentTypes contains duplicate PaymentTypeNo values. Unique key and FK were not created.', 1;
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE [type] = 'UQ'
      AND [name] = 'UQ_PaymentTypes_PaymentTypeNo'
      AND parent_object_id = OBJECT_ID('dbo.PaymentTypes')
)
BEGIN
    PRINT 'Creating UQ_PaymentTypes_PaymentTypeNo...';
    ALTER TABLE dbo.PaymentTypes
        ADD CONSTRAINT UQ_PaymentTypes_PaymentTypeNo UNIQUE (PaymentTypeNo);
    PRINT 'Unique catalog key created.';
END
ELSE
BEGIN
    PRINT 'UQ_PaymentTypes_PaymentTypeNo already exists.';
END
GO

-- 5. Reject orphaned non-null links, then enforce the terminal-to-catalog relation.
IF EXISTS
(
    SELECT 1
    FROM dbo.CashRegisterDetails AS d
    LEFT JOIN dbo.PaymentTypes AS p
        ON p.PaymentTypeNo = d.PaymentTypeNo
    WHERE d.PaymentTypeNo IS NOT NULL
      AND p.PaymentTypeNo IS NULL
)
BEGIN
    THROW 51001, 'CashRegisterDetails contains PaymentTypeNo values missing from PaymentTypes. FK was not created.', 1;
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = 'FK_CashRegisterDetails_PaymentTypes_PaymentTypeNo'
      AND parent_object_id = OBJECT_ID('dbo.CashRegisterDetails')
)
BEGIN
    PRINT 'Creating FK_CashRegisterDetails_PaymentTypes_PaymentTypeNo...';
    ALTER TABLE dbo.CashRegisterDetails WITH CHECK
        ADD CONSTRAINT FK_CashRegisterDetails_PaymentTypes_PaymentTypeNo
        FOREIGN KEY (PaymentTypeNo)
        REFERENCES dbo.PaymentTypes (PaymentTypeNo);

    ALTER TABLE dbo.CashRegisterDetails
        CHECK CONSTRAINT FK_CashRegisterDetails_PaymentTypes_PaymentTypeNo;
    PRINT 'Foreign key created.';
END
ELSE
BEGIN
    PRINT 'FK_CashRegisterDetails_PaymentTypes_PaymentTypeNo already exists.';
END
GO

-- 6. Verification summary
SELECT
    COUNT(*) AS TotalRows,
    COUNT(CASE WHEN PaymentTypeNo IS NOT NULL THEN 1 END) AS RowsWithPaymentTypeNo,
    COUNT(CASE WHEN PaymentTypeNo IS NULL THEN 1 END) AS RowsWithoutPaymentTypeNo
FROM dbo.CashRegisterDetails;

SELECT
    (SELECT COUNT(*) FROM dbo.PaymentTypes) AS PaymentTypeCount,
    (SELECT COUNT(*) FROM sys.key_constraints
     WHERE [name] = 'UQ_PaymentTypes_PaymentTypeNo'
       AND parent_object_id = OBJECT_ID('dbo.PaymentTypes')) AS UniqueCatalogKeyCount,
    (SELECT COUNT(*) FROM sys.foreign_keys
     WHERE [name] = 'FK_CashRegisterDetails_PaymentTypes_PaymentTypeNo'
       AND parent_object_id = OBJECT_ID('dbo.CashRegisterDetails')) AS ForeignKeyCount;
GO
