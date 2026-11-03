-- Idempotent Migration: Add PaymentTypeNo to Furpa.dbo.CashRegisterDetails and backfill from Furpa.dbo.PaymentTypes
-- Target Database: Furpa (Server: 10.0.0.207)

SET NOCOUNT ON;

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

-- 4. Verification summary
SELECT 
    COUNT(*) AS TotalRows,
    COUNT(CASE WHEN PaymentTypeNo IS NOT NULL THEN 1 END) AS RowsWithPaymentTypeNo,
    COUNT(CASE WHEN PaymentTypeNo IS NULL THEN 1 END) AS RowsWithoutPaymentTypeNo
FROM dbo.CashRegisterDetails;
GO
