/*
    Furpa API tarafindan ana birim miktariyla yazildigi kesin olan gecmis
    kayitlarda birim pointer alanini toplu olarak 1 yapar.

    Guvenlik:
      - Varsayilan @Apply = 0: yalniz on izleme yapar.
      - Canli uygulama icin @Apply ve @AllowBulkApply birlikte 1 olmalidir.
      - Yalniz Furpa izli (FR...) veya AXATA bayragi nedeniyle izi kapanmis,
        API kullanicisi 39 tarafindan yazilan depo siparisleri kapsanir.
      - Miktar, fiyat, tutar, stok kodu ve evrak baglantisi degismez.
      - Degisen her satirin onceki degeri kalici denetim tablosuna yazilir.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET LOCK_TIMEOUT 120000;

DECLARE @Apply bit = 0;
DECLARE @AllowBulkApply bit = 0;
DECLARE @StartDate date = '2026-10-01';
DECLARE @EndDateExclusive date = '2026-11-03';
DECLARE @UpdateUser smallint = 39;
DECLARE @BatchId uniqueidentifier = NEWID();
DECLARE @Now datetime = GETDATE();

IF @StartDate IS NULL OR @EndDateExclusive IS NULL OR @StartDate >= @EndDateExclusive
    THROW 51300, 'Gecersiz tarih araligi.', 1;

IF @Apply = 1 AND @AllowBulkApply = 0
    THROW 51301, 'Toplu uygulama kilitli. Apply ve AllowBulkApply birlikte 1 olmalidir.', 1;

IF OBJECT_ID('tempdb..#Candidate') IS NOT NULL DROP TABLE #Candidate;
CREATE TABLE #Candidate
(
    SourceTable sysname NOT NULL,
    SourceReason nvarchar(50) NOT NULL,
    RowGuid uniqueidentifier NOT NULL,
    DocumentDate datetime NOT NULL,
    DocumentSeries nvarchar(20) NULL,
    DocumentNo int NOT NULL,
    DocumentType int NULL,
    WarehouseNo int NULL,
    RowNo int NOT NULL,
    StockCode nvarchar(50) NOT NULL,
    Quantity float NOT NULL,
    OldPointer int NULL,
    OldLastUpdateUser smallint NULL,
    OldLastUpdateDate datetime NULL,
    OldChanged bit NULL,
    CONSTRAINT PK_UnitPointerBulkCandidate PRIMARY KEY (SourceTable, RowGuid)
);

INSERT #Candidate
SELECT
    N'STOK_HAREKETLERI', N'FR_TRACE', m.sth_Guid, m.sth_tarih,
    m.sth_evrakno_seri, m.sth_evrakno_sira, m.sth_evraktip,
    m.sth_cikis_depo_no, m.sth_satirno, m.sth_stok_kod,
    ISNULL(m.sth_miktar, 0), m.sth_birim_pntr,
    m.sth_lastup_user, m.sth_lastup_date, m.sth_degisti
FROM dbo.STOK_HAREKETLERI AS m WITH (READUNCOMMITTED, INDEX(NDX_STOK_HAREKETLERI_02))
WHERE m.sth_tarih >= @StartDate AND m.sth_tarih < @EndDateExclusive
  AND ISNULL(m.sth_birim_pntr, 0) <> 1
  AND UPPER(LTRIM(RTRIM(ISNULL(m.sth_eticaret_kanal_kodu, N'')))) LIKE N'FR%'
OPTION (MAXDOP 1, RECOMPILE);

INSERT #Candidate
SELECT
    N'DEPOLAR_ARASI_SIPARISLER',
    CASE
        WHEN UPPER(LTRIM(RTRIM(ISNULL(o.ssip_special1, N'')))) LIKE N'FR%'
            THEN N'FR_TRACE'
        WHEN NULLIF(LTRIM(RTRIM(ISNULL(o.ssip_special2, N''))), N'') IS NOT NULL
         AND NULLIF(LTRIM(RTRIM(ISNULL(o.ssip_special3, N''))), N'') IS NOT NULL
            THEN N'AXATA_SENT_TRACE_TAIL'
        ELSE N'API_AUTOMATION_NO_REQUEST' END,
    o.ssip_Guid, o.ssip_tarih, o.ssip_evrakno_seri, o.ssip_evrakno_sira,
    NULL, o.ssip_cikdepo, o.ssip_satirno, o.ssip_stok_kod,
    ISNULL(o.ssip_miktar, 0), o.ssip_birim_pntr,
    o.ssip_lastup_user, o.ssip_lastup_date, o.ssip_degisti
FROM dbo.DEPOLAR_ARASI_SIPARISLER AS o WITH (READUNCOMMITTED, INDEX(NDX_DEPOLAR_ARASI_SIPARISLER_03))
WHERE o.ssip_tarih >= @StartDate AND o.ssip_tarih < @EndDateExclusive
  AND ISNULL(o.ssip_birim_pntr, 0) <> 1
  AND
  (
      UPPER(LTRIM(RTRIM(ISNULL(o.ssip_special1, N'')))) LIKE N'FR%'
      OR
      (
          o.ssip_create_user = 39
          AND LTRIM(RTRIM(ISNULL(o.ssip_special1, N''))) = N'1'
      )
  )
OPTION (MAXDOP 1, RECOMPILE);

INSERT #Candidate
SELECT
    N'SIPARISLER', N'FR_TRACE', o.sip_Guid, o.sip_tarih,
    o.sip_evrakno_seri, o.sip_evrakno_sira, o.sip_tip,
    o.sip_depono, o.sip_satirno, o.sip_stok_kod,
    ISNULL(o.sip_miktar, 0), o.sip_birim_pntr,
    o.sip_lastup_user, o.sip_lastup_date, o.sip_degisti
FROM dbo.SIPARISLER AS o WITH (READUNCOMMITTED, INDEX(NDX_SIPARISLER_03))
WHERE o.sip_tarih >= @StartDate AND o.sip_tarih < @EndDateExclusive
  AND ISNULL(o.sip_birim_pntr, 0) <> 1
  AND UPPER(LTRIM(RTRIM(ISNULL(o.sip_eticaret_kanal_kodu, N'')))) LIKE N'FR%'
OPTION (MAXDOP 1, RECOMPILE);

INSERT #Candidate
SELECT
    N'SAYIM_SONUCLARI', N'FR_TRACE', c.sym_Guid, c.sym_tarihi,
    NULL, c.sym_evrakno, NULL, c.sym_depono, c.sym_satirno,
    c.sym_Stokkodu, ISNULL(c.sym_miktar1, 0), c.sym_birim_pntr,
    c.sym_lastup_user, c.sym_lastup_date, c.sym_degisti
FROM dbo.SAYIM_SONUCLARI AS c WITH (READUNCOMMITTED, INDEX(NDX_SAYIM_SONUCLARI_02))
WHERE c.sym_tarihi >= @StartDate AND c.sym_tarihi < @EndDateExclusive
  AND ISNULL(c.sym_birim_pntr, 0) <> 1
  AND UPPER(LTRIM(RTRIM(ISNULL(c.sym_serino, N'')))) LIKE N'FR%'
OPTION (MAXDOP 1, RECOMPILE);

SELECT
    SourceTable, SourceReason,
    COUNT_BIG(*) AS CandidateRows,
    COUNT(DISTINCT CONCAT(
        CONVERT(char(10), DocumentDate, 23), N'|', ISNULL(DocumentSeries, N''),
        N'|', DocumentNo, N'|', ISNULL(DocumentType, -1), N'|', ISNULL(WarehouseNo, -1))) AS CandidateDocuments,
    MIN(DocumentDate) AS FirstDocumentDate,
    MAX(DocumentDate) AS LastDocumentDate
FROM #Candidate
GROUP BY SourceTable, SourceReason
ORDER BY SourceTable, SourceReason;

SELECT OldPointer, COUNT_BIG(*) AS CandidateRows
FROM #Candidate
GROUP BY OldPointer
ORDER BY OldPointer;

DECLARE @Expected int = (SELECT COUNT(*) FROM #Candidate);
SELECT @BatchId AS BatchId, @Apply AS ApplyMode, @Expected AS TotalCandidateRows;

IF @Apply = 0
BEGIN
    PRINT 'TOPLU ON IZLEME TAMAMLANDI. CANLI VERI DEGISTIRILMEDI.';
    RETURN;
END;

IF @Expected = 0
    THROW 51302, 'Duzeltilecek satir bulunamadi.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    IF OBJECT_ID('dbo.FURPA_UNIT_POINTER_BULK_BATCH_20261102', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FURPA_UNIT_POINTER_BULK_BATCH_20261102
        (
            BatchId uniqueidentifier NOT NULL PRIMARY KEY,
            StartDate date NOT NULL,
            EndDateExclusive date NOT NULL,
            StartedAt datetime NOT NULL,
            CompletedAt datetime NULL,
            UpdateUser smallint NOT NULL,
            CandidateRows int NOT NULL,
            UpdatedRows int NULL,
            Status nvarchar(30) NOT NULL
        );
    END;

    IF OBJECT_ID('dbo.FURPA_UNIT_POINTER_BULK_ROW_20261102', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FURPA_UNIT_POINTER_BULK_ROW_20261102
        (
            BatchId uniqueidentifier NOT NULL,
            SourceTable sysname NOT NULL,
            SourceReason nvarchar(50) NOT NULL,
            RowGuid uniqueidentifier NOT NULL,
            DocumentDate datetime NOT NULL,
            DocumentSeries nvarchar(20) NULL,
            DocumentNo int NOT NULL,
            DocumentType int NULL,
            WarehouseNo int NULL,
            RowNo int NOT NULL,
            StockCode nvarchar(50) NOT NULL,
            Quantity float NOT NULL,
            OldPointer int NULL,
            NewPointer int NOT NULL,
            OldLastUpdateUser smallint NULL,
            OldLastUpdateDate datetime NULL,
            OldChanged bit NULL,
            ChangedAt datetime NOT NULL,
            CONSTRAINT PK_FURPA_UNIT_POINTER_BULK_ROW_20261102
                PRIMARY KEY (BatchId, SourceTable, RowGuid)
        );
    END;

    INSERT dbo.FURPA_UNIT_POINTER_BULK_BATCH_20261102
    (
        BatchId, StartDate, EndDateExclusive, StartedAt, UpdateUser,
        CandidateRows, Status
    )
    VALUES
    (
        @BatchId, @StartDate, @EndDateExclusive, @Now, @UpdateUser,
        @Expected, N'Running'
    );

    INSERT dbo.FURPA_UNIT_POINTER_BULK_ROW_20261102
    (
        BatchId, SourceTable, SourceReason, RowGuid, DocumentDate,
        DocumentSeries, DocumentNo, DocumentType, WarehouseNo, RowNo,
        StockCode, Quantity, OldPointer, NewPointer, OldLastUpdateUser,
        OldLastUpdateDate, OldChanged, ChangedAt
    )
    SELECT
        @BatchId, SourceTable, SourceReason, RowGuid, DocumentDate,
        DocumentSeries, DocumentNo, DocumentType, WarehouseNo, RowNo,
        StockCode, Quantity, OldPointer, 1, OldLastUpdateUser,
        OldLastUpdateDate, OldChanged, @Now
    FROM #Candidate;

    DECLARE @Updated int = 0;

    UPDATE m
    SET m.sth_birim_pntr = 1,
        m.sth_lastup_user = @UpdateUser,
        m.sth_lastup_date = @Now,
        m.sth_degisti = 1
    FROM dbo.STOK_HAREKETLERI AS m WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN #Candidate AS c
        ON c.SourceTable = N'STOK_HAREKETLERI' AND c.RowGuid = m.sth_Guid
    WHERE ISNULL(m.sth_birim_pntr, 0) = ISNULL(c.OldPointer, 0)
      AND ISNULL(m.sth_miktar, 0) = c.Quantity;
    SET @Updated = @Updated + @@ROWCOUNT;

    UPDATE o
    SET o.ssip_birim_pntr = 1,
        o.ssip_lastup_user = @UpdateUser,
        o.ssip_lastup_date = @Now,
        o.ssip_degisti = 1
    FROM dbo.DEPOLAR_ARASI_SIPARISLER AS o WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN #Candidate AS c
        ON c.SourceTable = N'DEPOLAR_ARASI_SIPARISLER' AND c.RowGuid = o.ssip_Guid
    WHERE ISNULL(o.ssip_birim_pntr, 0) = ISNULL(c.OldPointer, 0)
      AND ISNULL(o.ssip_miktar, 0) = c.Quantity;
    SET @Updated = @Updated + @@ROWCOUNT;

    UPDATE o
    SET o.sip_birim_pntr = 1,
        o.sip_lastup_user = @UpdateUser,
        o.sip_lastup_date = @Now,
        o.sip_degisti = 1
    FROM dbo.SIPARISLER AS o WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN #Candidate AS c
        ON c.SourceTable = N'SIPARISLER' AND c.RowGuid = o.sip_Guid
    WHERE ISNULL(o.sip_birim_pntr, 0) = ISNULL(c.OldPointer, 0)
      AND ISNULL(o.sip_miktar, 0) = c.Quantity;
    SET @Updated = @Updated + @@ROWCOUNT;

    UPDATE s
    SET s.sym_birim_pntr = 1,
        s.sym_lastup_user = @UpdateUser,
        s.sym_lastup_date = @Now,
        s.sym_degisti = 1
    FROM dbo.SAYIM_SONUCLARI AS s WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN #Candidate AS c
        ON c.SourceTable = N'SAYIM_SONUCLARI' AND c.RowGuid = s.sym_Guid
    WHERE ISNULL(s.sym_birim_pntr, 0) = ISNULL(c.OldPointer, 0)
      AND ISNULL(s.sym_miktar1, 0) = c.Quantity;
    SET @Updated = @Updated + @@ROWCOUNT;

    IF @Updated <> @Expected
        THROW 51303, 'Beklenen satirlarin tamami guncellenemedi; transaction geri alindi.', 1;

    UPDATE dbo.FURPA_UNIT_POINTER_BULK_BATCH_20261102
    SET CompletedAt = GETDATE(), UpdatedRows = @Updated, Status = N'Completed'
    WHERE BatchId = @BatchId;

    COMMIT TRANSACTION;

    SELECT @BatchId AS BatchId, @Expected AS CandidateRows,
           @Updated AS UpdatedRows, N'Completed' AS Status;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
