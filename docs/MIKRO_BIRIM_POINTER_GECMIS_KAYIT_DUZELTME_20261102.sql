/*
    Gecmiste koli katsayisinin Mikro birim pointer alanina yazildigi satirlari
    evrak bazinda inceler ve onay verilirse yalniz pointer alanini 1 yapar.

    Varsayilan ayarlar A01/3529 evrakini SADECE ON IZLER.
    @Apply = 0 iken canli veri ve kalici tablo degismez.

    Guvenli kullanim:
      1. Hedef tablo/evrak bilgilerini girin, @Apply = 0 ile calistirin.
      2. AUTO_INVALID_POINTER ve AUTO_UNDEFINED_UNIT satirlari kesin hatalidir.
      3. REVIEW_PACKAGE_FACTOR satirlari pointer ile koli katsayisi cakismasidir.
      4. Pilot evraki Mikro ekranindan kontrol ettikten sonra:
           @Apply = 1
           @AllowApply = 1
           @UpdateUser = islemi yapan Mikro kullanici no
         yapin.
      5. REVIEW_PACKAGE_FACTOR satirlarini da duzeltmek icin ayrica
         @IncludePackageFactorCollision = 1 gerekir.

    Desteklenen tablolar:
      STOK_HAREKETLERI
      DEPOLAR_ARASI_SIPARISLER
      SIPARISLER
      SAYIM_SONUCLARI

    Bu script miktar, fiyat, tutar, stok kodu ve evrak baglantilarini degistirmez.
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
SET LOCK_TIMEOUT 30000;

DECLARE @Apply bit = 0;
DECLARE @AllowApply bit = 0;
DECLARE @IncludePackageFactorCollision bit = 0;
DECLARE @UpdateUser smallint = NULL;

DECLARE @SourceTable sysname = N'STOK_HAREKETLERI';
DECLARE @DocumentSeries nvarchar(20) = N'A01';
DECLARE @DocumentNo int = 3529;
DECLARE @DocumentDate date = '2026-10-22';
DECLARE @StockMovementDocumentType tinyint = 13;
DECLARE @InventoryWarehouseNo int = NULL;

DECLARE @BatchId uniqueidentifier = NEWID();
DECLARE @Now datetime = GETDATE();

IF @SourceTable NOT IN
   (N'STOK_HAREKETLERI', N'DEPOLAR_ARASI_SIPARISLER', N'SIPARISLER', N'SAYIM_SONUCLARI')
    THROW 51100, 'Desteklenmeyen SourceTable.', 1;

IF @DocumentNo IS NULL OR @DocumentDate IS NULL
    THROW 51101, 'Evrak bazli guvenlik icin DocumentNo ve DocumentDate zorunludur.', 1;

IF @SourceTable <> N'SAYIM_SONUCLARI' AND NULLIF(LTRIM(RTRIM(@DocumentSeries)), N'') IS NULL
    THROW 51102, 'Bu tablo icin DocumentSeries zorunludur.', 1;

IF @SourceTable = N'STOK_HAREKETLERI' AND @StockMovementDocumentType IS NULL
    THROW 51103, 'Stok hareketinde DocumentType zorunludur.', 1;

IF @SourceTable = N'SAYIM_SONUCLARI' AND @InventoryWarehouseNo IS NULL
    THROW 51104, 'Sayim sonucu icin InventoryWarehouseNo zorunludur.', 1;

IF @Apply = 1 AND (@AllowApply = 0 OR @UpdateUser IS NULL)
    THROW 51105, 'Uygulama kilitli. AllowApply=1 ve UpdateUser zorunludur.', 1;

IF OBJECT_ID('tempdb..#Candidate') IS NOT NULL DROP TABLE #Candidate;
CREATE TABLE #Candidate
(
    SourceTable sysname NOT NULL,
    RowGuid uniqueidentifier NOT NULL,
    DocumentDate datetime NOT NULL,
    DocumentSeries nvarchar(20) NULL,
    DocumentNo int NOT NULL,
    DocumentType int NULL,
    WarehouseNo int NULL,
    RowNo int NOT NULL,
    StockCode nvarchar(50) NOT NULL,
    Quantity float NOT NULL,
    CurrentPointer int NULL,
    Unit1Name nvarchar(50) NULL,
    Unit1Factor float NULL,
    Unit2Name nvarchar(50) NULL,
    Unit2Factor float NULL,
    Unit3Name nvarchar(50) NULL,
    Unit3Factor float NULL,
    Unit4Name nvarchar(50) NULL,
    Unit4Factor float NULL,
    CurrentUnitName nvarchar(50) NULL,
    PackageFactor float NULL,
    Classification nvarchar(50) NULL,
    ShouldFix bit NOT NULL DEFAULT (0),
    OriginalLastUpdateUser smallint NULL,
    OriginalLastUpdateDate datetime NULL,
    OriginalChanged bit NULL,
    CONSTRAINT PK_Candidate PRIMARY KEY (RowGuid)
);

IF @SourceTable = N'STOK_HAREKETLERI'
BEGIN
    INSERT #Candidate
    (
        SourceTable, RowGuid, DocumentDate, DocumentSeries, DocumentNo,
        DocumentType, WarehouseNo, RowNo, StockCode, Quantity, CurrentPointer,
        Unit1Name, Unit1Factor, Unit2Name, Unit2Factor, Unit3Name, Unit3Factor,
        Unit4Name, Unit4Factor, OriginalLastUpdateUser, OriginalLastUpdateDate,
        OriginalChanged
    )
    SELECT
        @SourceTable, m.sth_Guid, m.sth_tarih, m.sth_evrakno_seri,
        m.sth_evrakno_sira, m.sth_evraktip, NULL, m.sth_satirno,
        m.sth_stok_kod, ISNULL(m.sth_miktar, 0), m.sth_birim_pntr,
        s.sto_birim1_ad, s.sto_birim1_katsayi,
        s.sto_birim2_ad, s.sto_birim2_katsayi,
        s.sto_birim3_ad, s.sto_birim3_katsayi,
        s.sto_birim4_ad, s.sto_birim4_katsayi,
        m.sth_lastup_user, m.sth_lastup_date, m.sth_degisti
    FROM dbo.STOK_HAREKETLERI AS m WITH (READUNCOMMITTED, INDEX(NDX_STOK_HAREKETLERI_05))
    LEFT JOIN dbo.STOKLAR AS s WITH (READUNCOMMITTED) ON s.sto_kod = m.sth_stok_kod
    WHERE m.sth_evraktip = @StockMovementDocumentType
      AND m.sth_evrakno_seri = @DocumentSeries
      AND m.sth_evrakno_sira = @DocumentNo
      AND m.sth_tarih >= @DocumentDate
      AND m.sth_tarih < DATEADD(day, 1, @DocumentDate);
END;

IF @SourceTable = N'DEPOLAR_ARASI_SIPARISLER'
BEGIN
    INSERT #Candidate
    (
        SourceTable, RowGuid, DocumentDate, DocumentSeries, DocumentNo,
        DocumentType, WarehouseNo, RowNo, StockCode, Quantity, CurrentPointer,
        Unit1Name, Unit1Factor, Unit2Name, Unit2Factor, Unit3Name, Unit3Factor,
        Unit4Name, Unit4Factor, OriginalLastUpdateUser, OriginalLastUpdateDate,
        OriginalChanged
    )
    SELECT
        @SourceTable, o.ssip_Guid, o.ssip_tarih, o.ssip_evrakno_seri,
        o.ssip_evrakno_sira, NULL, o.ssip_cikdepo, o.ssip_satirno,
        o.ssip_stok_kod, ISNULL(o.ssip_miktar, 0), o.ssip_birim_pntr,
        s.sto_birim1_ad, s.sto_birim1_katsayi,
        s.sto_birim2_ad, s.sto_birim2_katsayi,
        s.sto_birim3_ad, s.sto_birim3_katsayi,
        s.sto_birim4_ad, s.sto_birim4_katsayi,
        o.ssip_lastup_user, o.ssip_lastup_date, o.ssip_degisti
    FROM dbo.DEPOLAR_ARASI_SIPARISLER AS o WITH (READUNCOMMITTED, INDEX(NDX_DEPOLAR_ARASI_SIPARISLER_05))
    LEFT JOIN dbo.STOKLAR AS s WITH (READUNCOMMITTED) ON s.sto_kod = o.ssip_stok_kod
    WHERE o.ssip_evrakno_seri = @DocumentSeries
      AND o.ssip_evrakno_sira = @DocumentNo
      AND o.ssip_tarih >= @DocumentDate
      AND o.ssip_tarih < DATEADD(day, 1, @DocumentDate);
END;

IF @SourceTable = N'SIPARISLER'
BEGIN
    INSERT #Candidate
    (
        SourceTable, RowGuid, DocumentDate, DocumentSeries, DocumentNo,
        DocumentType, WarehouseNo, RowNo, StockCode, Quantity, CurrentPointer,
        Unit1Name, Unit1Factor, Unit2Name, Unit2Factor, Unit3Name, Unit3Factor,
        Unit4Name, Unit4Factor, OriginalLastUpdateUser, OriginalLastUpdateDate,
        OriginalChanged
    )
    SELECT
        @SourceTable, o.sip_Guid, o.sip_tarih, o.sip_evrakno_seri,
        o.sip_evrakno_sira, o.sip_tip, o.sip_depono, o.sip_satirno,
        o.sip_stok_kod, ISNULL(o.sip_miktar, 0), o.sip_birim_pntr,
        s.sto_birim1_ad, s.sto_birim1_katsayi,
        s.sto_birim2_ad, s.sto_birim2_katsayi,
        s.sto_birim3_ad, s.sto_birim3_katsayi,
        s.sto_birim4_ad, s.sto_birim4_katsayi,
        o.sip_lastup_user, o.sip_lastup_date, o.sip_degisti
    FROM dbo.SIPARISLER AS o WITH (READUNCOMMITTED, INDEX(NDX_SIPARISLER_06))
    LEFT JOIN dbo.STOKLAR AS s WITH (READUNCOMMITTED) ON s.sto_kod = o.sip_stok_kod
    WHERE o.sip_evrakno_seri = @DocumentSeries
      AND o.sip_evrakno_sira = @DocumentNo
      AND o.sip_tarih >= @DocumentDate
      AND o.sip_tarih < DATEADD(day, 1, @DocumentDate);
END;

IF @SourceTable = N'SAYIM_SONUCLARI'
BEGIN
    INSERT #Candidate
    (
        SourceTable, RowGuid, DocumentDate, DocumentSeries, DocumentNo,
        DocumentType, WarehouseNo, RowNo, StockCode, Quantity, CurrentPointer,
        Unit1Name, Unit1Factor, Unit2Name, Unit2Factor, Unit3Name, Unit3Factor,
        Unit4Name, Unit4Factor, OriginalLastUpdateUser, OriginalLastUpdateDate,
        OriginalChanged
    )
    SELECT
        @SourceTable, c.sym_Guid, c.sym_tarihi, NULL, c.sym_evrakno,
        NULL, c.sym_depono, c.sym_satirno, c.sym_Stokkodu,
        ISNULL(c.sym_miktar1, 0), c.sym_birim_pntr,
        s.sto_birim1_ad, s.sto_birim1_katsayi,
        s.sto_birim2_ad, s.sto_birim2_katsayi,
        s.sto_birim3_ad, s.sto_birim3_katsayi,
        s.sto_birim4_ad, s.sto_birim4_katsayi,
        c.sym_lastup_user, c.sym_lastup_date, c.sym_degisti
    FROM dbo.SAYIM_SONUCLARI AS c WITH (READUNCOMMITTED, INDEX(NDX_SAYIM_SONUCLARI_02))
    LEFT JOIN dbo.STOKLAR AS s WITH (READUNCOMMITTED) ON s.sto_kod = c.sym_Stokkodu
    WHERE c.sym_depono = @InventoryWarehouseNo
      AND c.sym_evrakno = @DocumentNo
      AND c.sym_tarihi >= @DocumentDate
      AND c.sym_tarihi < DATEADD(day, 1, @DocumentDate);
END;

IF NOT EXISTS (SELECT 1 FROM #Candidate)
    THROW 51106, 'Hedef evrak bulunamadi.', 1;

UPDATE #Candidate
SET CurrentUnitName = CASE CurrentPointer
        WHEN 1 THEN Unit1Name WHEN 2 THEN Unit2Name
        WHEN 3 THEN Unit3Name WHEN 4 THEN Unit4Name END,
    PackageFactor = CASE
        WHEN NULLIF(LTRIM(RTRIM(Unit2Name)), N'') IS NOT NULL
          AND ABS(ISNULL(Unit2Factor, 0)) > 1
        THEN ABS(Unit2Factor)
        WHEN ABS(ISNULL(Unit1Factor, 0)) > 0 THEN ABS(Unit1Factor)
        ELSE 1 END;

UPDATE #Candidate
SET Classification = CASE
        WHEN CurrentPointer = 1 THEN N'OK_PRIMARY_UNIT'
        WHEN CurrentPointer IS NULL OR CurrentPointer < 1 OR CurrentPointer > 4
            THEN N'AUTO_INVALID_POINTER'
        WHEN NULLIF(LTRIM(RTRIM(CurrentUnitName)), N'') IS NULL
            THEN N'AUTO_UNDEFINED_UNIT'
        WHEN PackageFactor > 1 AND ABS(CurrentPointer - PackageFactor) < 0.000001
            THEN N'REVIEW_PACKAGE_FACTOR'
        ELSE N'KEEP_VALID_ALTERNATE_UNIT' END;

UPDATE #Candidate
SET ShouldFix = CASE
        WHEN Classification IN (N'AUTO_INVALID_POINTER', N'AUTO_UNDEFINED_UNIT') THEN 1
        WHEN Classification = N'REVIEW_PACKAGE_FACTOR'
         AND @IncludePackageFactorCollision = 1 THEN 1
        ELSE 0 END;

SELECT
    SourceTable, DocumentDate, DocumentSeries, DocumentNo, DocumentType,
    WarehouseNo, RowNo, RowGuid, StockCode, Quantity, CurrentPointer,
    CurrentUnitName, Unit1Name, Unit1Factor, Unit2Name, Unit2Factor,
    Unit3Name, Unit3Factor, Unit4Name, Unit4Factor, PackageFactor,
    Classification, ShouldFix
FROM #Candidate
ORDER BY RowNo;

SELECT Classification, COUNT(*) AS [RowCount],
       SUM(CASE WHEN ShouldFix = 1 THEN 1 ELSE 0 END) AS SelectedForFix
FROM #Candidate
GROUP BY Classification
ORDER BY Classification;

IF @Apply = 0
BEGIN
    PRINT 'ON IZLEME TAMAMLANDI. CANLI VERI DEGISTIRILMEDI.';
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM #Candidate WHERE ShouldFix = 1)
    THROW 51107, 'Duzeltme icin secilen satir yok.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    IF OBJECT_ID('dbo.FURPA_UNIT_POINTER_REPAIR_BATCH_20261102', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FURPA_UNIT_POINTER_REPAIR_BATCH_20261102
        (
            BatchId uniqueidentifier NOT NULL PRIMARY KEY,
            SourceTable sysname NOT NULL,
            DocumentDate datetime NOT NULL,
            DocumentSeries nvarchar(20) NULL,
            DocumentNo int NOT NULL,
            StartedAt datetime NOT NULL,
            CompletedAt datetime NULL,
            UpdateUser smallint NOT NULL,
            Status nvarchar(30) NOT NULL
        );
    END;

    IF OBJECT_ID('dbo.FURPA_UNIT_POINTER_REPAIR_ROW_20261102', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FURPA_UNIT_POINTER_REPAIR_ROW_20261102
        (
            BatchId uniqueidentifier NOT NULL,
            SourceTable sysname NOT NULL,
            RowGuid uniqueidentifier NOT NULL,
            StockCode nvarchar(50) NOT NULL,
            Quantity float NOT NULL,
            OldPointer int NULL,
            NewPointer int NOT NULL,
            Classification nvarchar(50) NOT NULL,
            OldLastUpdateUser smallint NULL,
            OldLastUpdateDate datetime NULL,
            OldChanged bit NULL,
            ChangedAt datetime NOT NULL,
            CONSTRAINT PK_FURPA_UNIT_POINTER_REPAIR_ROW_20261102
                PRIMARY KEY (BatchId, SourceTable, RowGuid)
        );
    END;

    INSERT dbo.FURPA_UNIT_POINTER_REPAIR_BATCH_20261102
    (
        BatchId, SourceTable, DocumentDate, DocumentSeries, DocumentNo,
        StartedAt, UpdateUser, Status
    )
    VALUES
    (
        @BatchId, @SourceTable, @DocumentDate, @DocumentSeries, @DocumentNo,
        @Now, @UpdateUser, N'Running'
    );

    INSERT dbo.FURPA_UNIT_POINTER_REPAIR_ROW_20261102
    (
        BatchId, SourceTable, RowGuid, StockCode, Quantity, OldPointer,
        NewPointer, Classification, OldLastUpdateUser, OldLastUpdateDate,
        OldChanged, ChangedAt
    )
    SELECT
        @BatchId, SourceTable, RowGuid, StockCode, Quantity, CurrentPointer,
        1, Classification, OriginalLastUpdateUser, OriginalLastUpdateDate,
        OriginalChanged, @Now
    FROM #Candidate
    WHERE ShouldFix = 1;

    DECLARE @Expected int = (SELECT COUNT(*) FROM #Candidate WHERE ShouldFix = 1);
    DECLARE @Updated int = 0;

    IF @SourceTable = N'STOK_HAREKETLERI'
    BEGIN
        UPDATE m
        SET m.sth_birim_pntr = 1,
            m.sth_lastup_user = @UpdateUser,
            m.sth_lastup_date = @Now,
            m.sth_degisti = 1
        FROM dbo.STOK_HAREKETLERI AS m WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN #Candidate AS c ON c.RowGuid = m.sth_Guid AND c.ShouldFix = 1
        WHERE ISNULL(m.sth_birim_pntr, 0) = ISNULL(c.CurrentPointer, 0)
          AND ISNULL(m.sth_miktar, 0) = c.Quantity;

        SET @Updated = @@ROWCOUNT;
    END;

    IF @SourceTable = N'DEPOLAR_ARASI_SIPARISLER'
    BEGIN
        UPDATE o
        SET o.ssip_birim_pntr = 1,
            o.ssip_lastup_user = @UpdateUser,
            o.ssip_lastup_date = @Now,
            o.ssip_degisti = 1
        FROM dbo.DEPOLAR_ARASI_SIPARISLER AS o WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN #Candidate AS c ON c.RowGuid = o.ssip_Guid AND c.ShouldFix = 1
        WHERE ISNULL(o.ssip_birim_pntr, 0) = ISNULL(c.CurrentPointer, 0)
          AND ISNULL(o.ssip_miktar, 0) = c.Quantity;

        SET @Updated = @@ROWCOUNT;
    END;

    IF @SourceTable = N'SIPARISLER'
    BEGIN
        UPDATE o
        SET o.sip_birim_pntr = 1,
            o.sip_lastup_user = @UpdateUser,
            o.sip_lastup_date = @Now,
            o.sip_degisti = 1
        FROM dbo.SIPARISLER AS o WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN #Candidate AS c ON c.RowGuid = o.sip_Guid AND c.ShouldFix = 1
        WHERE ISNULL(o.sip_birim_pntr, 0) = ISNULL(c.CurrentPointer, 0)
          AND ISNULL(o.sip_miktar, 0) = c.Quantity;

        SET @Updated = @@ROWCOUNT;
    END;

    IF @SourceTable = N'SAYIM_SONUCLARI'
    BEGIN
        UPDATE c0
        SET c0.sym_birim_pntr = 1,
            c0.sym_lastup_user = @UpdateUser,
            c0.sym_lastup_date = @Now,
            c0.sym_degisti = 1
        FROM dbo.SAYIM_SONUCLARI AS c0 WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN #Candidate AS c ON c.RowGuid = c0.sym_Guid AND c.ShouldFix = 1
        WHERE ISNULL(c0.sym_birim_pntr, 0) = ISNULL(c.CurrentPointer, 0)
          AND ISNULL(c0.sym_miktar1, 0) = c.Quantity;

        SET @Updated = @@ROWCOUNT;
    END;

    IF @Updated <> @Expected
        THROW 51108, 'Beklenen satirlarin tamami guncellenemedi; transaction geri alindi.', 1;

    UPDATE dbo.FURPA_UNIT_POINTER_REPAIR_BATCH_20261102
    SET CompletedAt = GETDATE(), Status = N'Completed'
    WHERE BatchId = @BatchId;

    COMMIT TRANSACTION;

    SELECT @BatchId AS BatchId, @SourceTable AS SourceTable,
           @Expected AS UpdatedRows, N'Completed' AS Status;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
