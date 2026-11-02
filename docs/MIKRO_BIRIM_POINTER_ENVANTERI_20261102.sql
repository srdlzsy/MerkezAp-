/*
    Mikro evrak tablolarindaki birim pointer kayitlarini salt okunur tarar.
    Kalici tablo olusturmaz ve canli kayit degistirmez.

    Siniflar:
      AUTO_INVALID_POINTER       : Pointer NULL, 0 veya 4'ten buyuk.
      AUTO_UNDEFINED_UNIT        : Pointer 2-4 fakat stokta o birim tanimsiz.
      REVIEW_PACKAGE_FACTOR      : Pointer, stokun koli katsayisina esit.
      KEEP_VALID_ALTERNATE_UNIT  : Tanimli gercek alternatif birim olabilir.
*/

SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SET LOCK_TIMEOUT 30000;

DECLARE @StartDate date = '2026-10-01';
DECLARE @EndDateExclusive date = '2026-11-03';
DECLARE @MaxDocuments int = 1000;

IF @StartDate IS NULL OR @EndDateExclusive IS NULL OR @StartDate >= @EndDateExclusive
    THROW 51200, 'Gecersiz tarih araligi.', 1;

IF OBJECT_ID('tempdb..#Raw') IS NOT NULL DROP TABLE #Raw;
IF OBJECT_ID('tempdb..#Audit') IS NOT NULL DROP TABLE #Audit;

CREATE TABLE #Raw
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
    CreateDate datetime NULL,
    CreateUser smallint NULL,
    TraceKey nvarchar(100) NULL,
    PRIMARY KEY (SourceTable, RowGuid)
);

INSERT #Raw
SELECT N'STOK_HAREKETLERI', m.sth_Guid, m.sth_tarih,
       m.sth_evrakno_seri, m.sth_evrakno_sira, m.sth_evraktip,
       m.sth_cikis_depo_no, m.sth_satirno, m.sth_stok_kod,
       ISNULL(m.sth_miktar, 0), m.sth_birim_pntr,
       m.sth_create_date, m.sth_create_user,
       NULLIF(LTRIM(RTRIM(m.sth_eticaret_kanal_kodu)), N'')
FROM dbo.STOK_HAREKETLERI AS m WITH (READUNCOMMITTED, INDEX(NDX_STOK_HAREKETLERI_02))
WHERE m.sth_tarih >= @StartDate AND m.sth_tarih < @EndDateExclusive
  AND ISNULL(m.sth_birim_pntr, 0) <> 1
OPTION (MAXDOP 1, RECOMPILE);

INSERT #Raw
SELECT N'DEPOLAR_ARASI_SIPARISLER', o.ssip_Guid, o.ssip_tarih,
       o.ssip_evrakno_seri, o.ssip_evrakno_sira, NULL,
       o.ssip_cikdepo, o.ssip_satirno, o.ssip_stok_kod,
       ISNULL(o.ssip_miktar, 0), o.ssip_birim_pntr,
       o.ssip_create_date, o.ssip_create_user,
       NULLIF(CONCAT(
           LTRIM(RTRIM(ISNULL(o.ssip_special1, N''))),
           LTRIM(RTRIM(ISNULL(o.ssip_special2, N''))),
           LTRIM(RTRIM(ISNULL(o.ssip_special3, N'')))), N'')
FROM dbo.DEPOLAR_ARASI_SIPARISLER AS o WITH (READUNCOMMITTED, INDEX(NDX_DEPOLAR_ARASI_SIPARISLER_03))
WHERE o.ssip_tarih >= @StartDate AND o.ssip_tarih < @EndDateExclusive
  AND ISNULL(o.ssip_birim_pntr, 0) <> 1
OPTION (MAXDOP 1, RECOMPILE);

INSERT #Raw
SELECT N'SIPARISLER', o.sip_Guid, o.sip_tarih,
       o.sip_evrakno_seri, o.sip_evrakno_sira, o.sip_tip,
       o.sip_depono, o.sip_satirno, o.sip_stok_kod,
       ISNULL(o.sip_miktar, 0), o.sip_birim_pntr,
       o.sip_create_date, o.sip_create_user,
       NULLIF(LTRIM(RTRIM(o.sip_eticaret_kanal_kodu)), N'')
FROM dbo.SIPARISLER AS o WITH (READUNCOMMITTED, INDEX(NDX_SIPARISLER_03))
WHERE o.sip_tarih >= @StartDate AND o.sip_tarih < @EndDateExclusive
  AND ISNULL(o.sip_birim_pntr, 0) <> 1
OPTION (MAXDOP 1, RECOMPILE);

INSERT #Raw
SELECT N'SAYIM_SONUCLARI', c.sym_Guid, c.sym_tarihi,
       NULL, c.sym_evrakno, NULL, c.sym_depono,
       c.sym_satirno, c.sym_Stokkodu, ISNULL(c.sym_miktar1, 0),
       c.sym_birim_pntr, c.sym_create_date, c.sym_create_user,
       NULLIF(LTRIM(RTRIM(c.sym_serino)), N'')
FROM dbo.SAYIM_SONUCLARI AS c WITH (READUNCOMMITTED, INDEX(NDX_SAYIM_SONUCLARI_02))
WHERE c.sym_tarihi >= @StartDate AND c.sym_tarihi < @EndDateExclusive
  AND ISNULL(c.sym_birim_pntr, 0) <> 1
OPTION (MAXDOP 1, RECOMPILE);

SELECT
    r.*,
    s.sto_birim1_ad AS Unit1Name,
    s.sto_birim1_katsayi AS Unit1Factor,
    s.sto_birim2_ad AS Unit2Name,
    s.sto_birim2_katsayi AS Unit2Factor,
    s.sto_birim3_ad AS Unit3Name,
    s.sto_birim3_katsayi AS Unit3Factor,
    s.sto_birim4_ad AS Unit4Name,
    s.sto_birim4_katsayi AS Unit4Factor,
    CASE r.CurrentPointer
        WHEN 1 THEN s.sto_birim1_ad WHEN 2 THEN s.sto_birim2_ad
        WHEN 3 THEN s.sto_birim3_ad WHEN 4 THEN s.sto_birim4_ad END AS CurrentUnitName,
    CASE
        WHEN NULLIF(LTRIM(RTRIM(s.sto_birim2_ad)), N'') IS NOT NULL
         AND ABS(ISNULL(s.sto_birim2_katsayi, 0)) > 1
        THEN ABS(s.sto_birim2_katsayi)
        WHEN ABS(ISNULL(s.sto_birim1_katsayi, 0)) > 0
        THEN ABS(s.sto_birim1_katsayi)
        ELSE 1 END AS PackageFactor,
    CASE
        WHEN UPPER(ISNULL(r.TraceKey, N'')) LIKE N'FR%'
            THEN N'CONFIRMED_API'
        WHEN r.SourceTable = N'DEPOLAR_ARASI_SIPARISLER'
         AND r.CreateUser = 39
         AND LEFT(ISNULL(r.TraceKey, N''), 1) = N'1'
         AND LEN(ISNULL(r.TraceKey, N'')) > 1
            THEN N'CONFIRMED_API_SENT_FLAG'
        WHEN r.SourceTable = N'DEPOLAR_ARASI_SIPARISLER'
         AND r.CreateUser = 39
         AND ISNULL(r.TraceKey, N'') = N'1'
            THEN N'CONFIRMED_API_AUTOMATION'
        WHEN r.CreateUser = 39
            THEN N'API_USER_WITHOUT_TRACE'
        ELSE N'OTHER_SOURCE' END AS SourceConfidence,
    CAST(NULL AS nvarchar(50)) AS Classification
INTO #Audit
FROM #Raw AS r
LEFT JOIN dbo.STOKLAR AS s WITH (READUNCOMMITTED) ON s.sto_kod = r.StockCode;

UPDATE #Audit
SET Classification = CASE
    WHEN CurrentPointer IS NULL OR CurrentPointer < 1 OR CurrentPointer > 4
        THEN N'AUTO_INVALID_POINTER'
    WHEN NULLIF(LTRIM(RTRIM(CurrentUnitName)), N'') IS NULL
        THEN N'AUTO_UNDEFINED_UNIT'
    WHEN PackageFactor > 1 AND ABS(CurrentPointer - PackageFactor) < 0.000001
        THEN N'REVIEW_PACKAGE_FACTOR'
    ELSE N'KEEP_VALID_ALTERNATE_UNIT' END;

SELECT
    SourceTable, SourceConfidence, Classification, CurrentPointer,
    COUNT_BIG(*) AS [RowCount],
    COUNT(DISTINCT CONCAT(
        CONVERT(char(10), DocumentDate, 23), N'|', ISNULL(DocumentSeries, N''),
        N'|', DocumentNo, N'|', ISNULL(DocumentType, -1), N'|', ISNULL(WarehouseNo, -1))) AS DocumentCount,
    MIN(DocumentDate) AS FirstDocumentDate,
    MAX(DocumentDate) AS LastDocumentDate,
    MIN(CreateDate) AS FirstCreateDate,
    MAX(CreateDate) AS LastCreateDate
FROM #Audit
GROUP BY SourceTable, SourceConfidence, Classification, CurrentPointer
ORDER BY SourceTable, SourceConfidence, Classification, CurrentPointer;

SELECT TOP (@MaxDocuments)
    SourceTable, DocumentDate, DocumentSeries, DocumentNo,
    DocumentType, WarehouseNo, SourceConfidence, Classification,
    COUNT(*) AS [ProblemRowCount],
    STRING_AGG(CONVERT(nvarchar(max), CONCAT(
        StockCode, N':', Quantity, N'@P', CurrentPointer,
        N'/K', PackageFactor)), N'; ') WITHIN GROUP (ORDER BY RowNo) AS Lines
FROM #Audit
WHERE Classification IN
      (N'AUTO_INVALID_POINTER', N'AUTO_UNDEFINED_UNIT', N'REVIEW_PACKAGE_FACTOR')
GROUP BY SourceTable, DocumentDate, DocumentSeries, DocumentNo,
         DocumentType, WarehouseNo, SourceConfidence, Classification
ORDER BY DocumentDate DESC, SourceTable, DocumentSeries, DocumentNo;

/* Yalniz kesin olarak bu API tarafindan yazildigi kanitlanan sorunlu kayitlar. */
SELECT
    SourceTable,
    COUNT_BIG(*) AS ProblemRowCount,
    COUNT(DISTINCT CONCAT(
        CONVERT(char(10), DocumentDate, 23), N'|', ISNULL(DocumentSeries, N''),
        N'|', DocumentNo, N'|', ISNULL(DocumentType, -1), N'|', ISNULL(WarehouseNo, -1))) AS DocumentCount,
    MIN(DocumentDate) AS FirstDocumentDate,
    MAX(DocumentDate) AS LastDocumentDate
FROM #Audit
WHERE SourceConfidence LIKE N'CONFIRMED_API%'
GROUP BY SourceTable
ORDER BY SourceTable;

SELECT
    COUNT(*) AS TotalNonPrimaryRows,
    SUM(CASE WHEN Classification IN
        (N'AUTO_INVALID_POINTER', N'AUTO_UNDEFINED_UNIT') THEN 1 ELSE 0 END) AS DefiniteBrokenRows,
    SUM(CASE WHEN Classification = N'REVIEW_PACKAGE_FACTOR' THEN 1 ELSE 0 END) AS ReviewRows,
    SUM(CASE WHEN Classification = N'KEEP_VALID_ALTERNATE_UNIT' THEN 1 ELSE 0 END) AS ValidAlternateRows
FROM #Audit;

PRINT 'ENVANTER TAMAMLANDI. CANLI VERI DEGISTIRILMEDI.';
