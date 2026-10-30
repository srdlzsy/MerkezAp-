/*
    Eksik muhasebe karsi hesap satirlarini ve eski stok fis baglantilarini duzeltir.

    Guvenli kullanim:
      1. Pilot on izleme:
         @Apply = 0, @TargetYevmiyeNo = test edilecek yevmiye no.
      2. Pilot uygulama:
         Ayni hedefle yalniz @Apply = 1 yapin. Script tam bir muhasebe fisini
         tek transaction icinde uygular. Sonuclari Mikro'dan kontrol edin.
      3. Toplu on izleme:
         @Apply = 0, hedef alanlari NULL, @AllowBulkApply = 0.
      4. Toplu uygulama:
         Hedef alanlari NULL, @Apply = 1 ve @AllowBulkApply = 1.

    Hedef secimi:
      - @TargetJournalNo: MUHASEBE_FISLERI.fis_sira_no
      - @TargetYevmiyeNo: MUHASEBE_FISLERI.fis_yevmiye_no (onerilen pilot secimi)
      - @TargetAccountingGuid: Herhangi bir MUHASEBE_FISLERI.fis_Guid satiri
      - @TargetJournalDate: Ayni numara birden fazla tarihte varsa zorunlu ayirici
      - @TargetDocumentGuid: CARI_HESAP_HAREKETLERI.cha_Guid ile alternatif secim

    Onemli:
      Belge GUID'i secilse bile muhasebe dengesi korunmak zorunda oldugundan
      o belgenin bagli oldugu muhasebe fisi butun olarak duzeltilir.

    Geri alma verileri:
      dbo.FURPA_MUH_REPAIR_BATCH_20261028
      dbo.FURPA_MUH_REPAIR_JOURNAL_20261028
      dbo.FURPA_MUH_REPAIR_INSERT_20261028
      dbo.FURPA_MUH_REPAIR_STOCK_20261028
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
DECLARE @TargetJournalNo int = NULL;                    -- fis_sira_no
DECLARE @TargetYevmiyeNo int = 25225028;                -- Pilot hedefi
DECLARE @TargetAccountingGuid uniqueidentifier = NULL;  -- Alternatif: fis_Guid
DECLARE @TargetJournalDate date = NULL;                  -- Gerekirse: '2026-10-14'
DECLARE @TargetDocumentGuid uniqueidentifier = NULL;     -- Alternatif: cha_Guid
DECLARE @AllowBulkApply bit = 0;                         -- Toplu APPLY icin ayrica 1 olmali
DECLARE @BatchId uniqueidentifier = NEWID();
DECLARE @Now datetime = GETDATE();
DECLARE @IsPilot bit = CASE
    WHEN @TargetJournalNo IS NOT NULL
      OR @TargetYevmiyeNo IS NOT NULL
      OR @TargetAccountingGuid IS NOT NULL
      OR @TargetDocumentGuid IS NOT NULL THEN 1
    ELSE 0
END;

IF @TargetJournalDate IS NOT NULL
   AND @TargetJournalNo IS NULL
   AND @TargetYevmiyeNo IS NULL
   AND @TargetAccountingGuid IS NULL
   AND @TargetDocumentGuid IS NULL
    THROW 50990, 'TargetJournalDate tek basina kullanilamaz. Yevmiye no veya belge GUID girin.', 1;

IF @Apply = 1 AND @IsPilot = 0 AND @AllowBulkApply = 0
    THROW 50991, 'Toplu uygulama kilitli. Once toplu on izleme yapin; sonra AllowBulkApply degerini 1 yapin.', 1;

IF OBJECT_ID('tempdb..#Journal') IS NOT NULL DROP TABLE #Journal;
IF OBJECT_ID('tempdb..#Document') IS NOT NULL DROP TABLE #Document;
IF OBJECT_ID('tempdb..#Line') IS NOT NULL DROP TABLE #Line;
IF OBJECT_ID('tempdb..#Tax') IS NOT NULL DROP TABLE #Tax;
IF OBJECT_ID('tempdb..#Resolved') IS NOT NULL DROP TABLE #Resolved;
IF OBJECT_ID('tempdb..#Plan') IS NOT NULL DROP TABLE #Plan;
IF OBJECT_ID('tempdb..#CandidateJournal') IS NOT NULL DROP TABLE #CandidateJournal;

/* 1. Bozuk surecin olusturdugu dengesiz muhasebe fisleri. */
SELECT
    m.fis_tarih,
    m.fis_sira_no,
    SUM(ISNULL(m.fis_meblag0, 0.0)) AS Balance0,
    SUM(ISNULL(m.fis_meblag1, 0.0)) AS Balance1,
    SUM(ISNULL(m.fis_meblag2, 0.0)) AS Balance2,
    MIN(m.fis_create_date) AS FirstCreateDate,
    MAX(m.fis_create_date) AS LastCreateDate
INTO #Journal
FROM dbo.MUHASEBE_FISLERI AS m WITH (READCOMMITTEDLOCK)
WHERE m.fis_tarih >= '2026-09-02'
  AND m.fis_tarih <  '2026-10-16'
  AND (@TargetJournalNo IS NULL OR m.fis_sira_no = @TargetJournalNo)
  AND
  (
      @TargetYevmiyeNo IS NULL
      OR EXISTS
      (
          SELECT 1
          FROM dbo.MUHASEBE_FISLERI AS targetYevmiye WITH (READCOMMITTEDLOCK)
          WHERE targetYevmiye.fis_tarih = m.fis_tarih
            AND targetYevmiye.fis_sira_no = m.fis_sira_no
            AND targetYevmiye.fis_yevmiye_no = @TargetYevmiyeNo
      )
  )
  AND
  (
      @TargetAccountingGuid IS NULL
      OR EXISTS
      (
          SELECT 1
          FROM dbo.MUHASEBE_FISLERI AS targetAccounting WITH (READCOMMITTEDLOCK)
          WHERE targetAccounting.fis_Guid = @TargetAccountingGuid
            AND targetAccounting.fis_tarih = m.fis_tarih
            AND targetAccounting.fis_sira_no = m.fis_sira_no
      )
  )
  AND (@TargetJournalDate IS NULL OR
       (m.fis_tarih >= @TargetJournalDate AND m.fis_tarih < DATEADD(day, 1, @TargetJournalDate)))
  AND
  (
      @TargetDocumentGuid IS NULL
      OR EXISTS
      (
          SELECT 1
          FROM dbo.CARI_HESAP_HAREKETLERI AS targetDocument WITH (READCOMMITTEDLOCK)
          WHERE targetDocument.cha_Guid = @TargetDocumentGuid
            AND targetDocument.cha_fis_tarih = m.fis_tarih
            AND targetDocument.cha_fis_sirano = m.fis_sira_no
      )
  )
GROUP BY m.fis_tarih, m.fis_sira_no
HAVING ABS(SUM(ISNULL(m.fis_meblag0, 0.0))) > 0.005
   AND MIN(m.fis_create_date) >= '2026-09-09T17:00:00'
   AND MIN(m.fis_create_date) <  '2026-10-16T00:00:00';

CREATE UNIQUE CLUSTERED INDEX IX_Journal ON #Journal(fis_tarih, fis_sira_no);

/* 2. Eski fis sira numarasini tasiyan stok satirlarina bagli cari belgeler. */
SELECT
    c.cha_Guid,
    c.cha_fis_tarih,
    c.cha_fis_sirano,
    c.cha_evrak_tip,
    c.cha_tip,
    c.cha_cinsi,
    c.cha_normal_Iade,
    c.cha_ticaret_turu,
    c.cha_d_cins,
    c.cha_evrakno_seri,
    c.cha_evrakno_sira,
    j.Balance0,
    j.Balance1,
    j.Balance2
INTO #Document
FROM dbo.CARI_HESAP_HAREKETLERI AS c WITH (READCOMMITTEDLOCK)
INNER JOIN #Journal AS j
    ON j.fis_tarih = c.cha_fis_tarih
   AND j.fis_sira_no = c.cha_fis_sirano
WHERE EXISTS
(
    SELECT 1
    FROM dbo.STOK_HAREKETLERI AS s WITH (READCOMMITTEDLOCK)
    WHERE s.sth_fat_uid = c.cha_Guid
      AND ISNULL(s.sth_fis_sirano, -2147483648) <> c.cha_fis_sirano
);

CREATE UNIQUE CLUSTERED INDEX IX_Document ON #Document(cha_Guid);
CREATE INDEX IX_DocumentJournal ON #Document(cha_fis_tarih, cha_fis_sirano);

SELECT
    d.*,
    s.sth_Guid,
    s.sth_stok_kod,
    s.sth_vergi_pntr,
    s.sth_fis_sirano AS OldStockJournalNo,
    CONVERT(float,
        ISNULL(s.sth_tutar, 0.0)
      - ISNULL(s.sth_iskonto1, 0.0)
      - ISNULL(s.sth_iskonto2, 0.0)
      - ISNULL(s.sth_iskonto3, 0.0)
      - ISNULL(s.sth_iskonto4, 0.0)
      - ISNULL(s.sth_iskonto5, 0.0)
      - ISNULL(s.sth_iskonto6, 0.0)
      + ISNULL(s.sth_masraf1, 0.0)
      + ISNULL(s.sth_masraf2, 0.0)
      + ISNULL(s.sth_masraf3, 0.0)
      + ISNULL(s.sth_masraf4, 0.0)) AS NetAmount
INTO #Line
FROM #Document AS d
INNER JOIN dbo.STOK_HAREKETLERI AS s WITH (READCOMMITTEDLOCK)
    ON s.sth_fat_uid = d.cha_Guid
WHERE ISNULL(s.sth_fis_sirano, -2147483648) <> d.cha_fis_sirano;

CREATE UNIQUE CLUSTERED INDEX IX_Line ON #Line(sth_Guid);
CREATE INDEX IX_LineJournal ON #Line(cha_fis_tarih, cha_fis_sirano);

/* Vergi fonksiyonunu her stok satiri icin degil, her pointer icin bir kez cagir. */
SELECT p.sth_vergi_pntr, dbo.fn_VergiYuzde(p.sth_vergi_pntr) AS TaxRate
INTO #Tax
FROM (SELECT DISTINCT sth_vergi_pntr FROM #Line) AS p;

CREATE UNIQUE CLUSTERED INDEX IX_Tax ON #Tax(sth_vergi_pntr);

/* 3. Mikro'nun resmi stok muhasebe hesap sablonunu islem turune gore sec. */
SELECT
    l.*,
    st.sto_kod AS FoundStockCode,
    CASE
        WHEN l.cha_evrak_tip = 0 AND l.cha_tip = 1 AND l.cha_cinsi = 6
             AND l.cha_normal_Iade = 0 AND l.cha_ticaret_turu = 0
            THEN st.sto_muh_kod
        WHEN l.cha_evrak_tip = 63 AND l.cha_tip = 0 AND l.cha_cinsi IN (6, 7)
             AND l.cha_normal_Iade = 0
            THEN st.sto_muh_sat_muh_kod
        WHEN l.cha_evrak_tip = 63 AND l.cha_tip = 0 AND l.cha_cinsi = 6
             AND l.cha_normal_Iade = 1 AND l.cha_ticaret_turu = 0
            THEN st.sto_muh_Iade_kod
        WHEN l.cha_evrak_tip = 63 AND l.cha_tip = 0 AND l.cha_cinsi = 15
             AND l.cha_normal_Iade = 1 AND l.cha_ticaret_turu = 0
            THEN st.sto_muh_aIiskmuhkod
    END AS AccountTemplate,
    t.TaxRate,
    CASE WHEN l.cha_evrak_tip = 0 THEN 1.0 ELSE -1.0 END AS DirectionSign
INTO #Resolved
FROM #Line AS l
LEFT JOIN dbo.STOKLAR AS st WITH (READCOMMITTEDLOCK)
    ON st.sto_kod = l.sth_stok_kod
LEFT JOIN #Tax AS t
    ON t.sth_vergi_pntr = l.sth_vergi_pntr;

ALTER TABLE #Resolved ADD AccountCode nvarchar(50) NULL;
UPDATE #Resolved
SET AccountCode = REPLACE(
        AccountTemplate,
        '#HVO#',
        RIGHT('000' + CONVERT(varchar(3), CONVERT(int, ROUND(TaxRate, 0))), 3));

/* 4. Bir belge/hesap icin tek muhasebe satiri planla. */
SELECT
    r.cha_fis_tarih,
    r.cha_fis_sirano,
    r.cha_Guid,
    r.AccountCode,
    SUM(r.DirectionSign * r.NetAmount) AS Amount0,
    COUNT(*) AS StockLineCount,
    CAST(NULL AS float) AS Amount1,
    CAST(NULL AS float) AS Amount2,
    CAST(NULL AS uniqueidentifier) AS BaseGuid,
    NEWID() AS NewGuid,
    CAST(NULL AS int) AS NewLineNo
INTO #Plan
FROM #Resolved AS r
GROUP BY r.cha_fis_tarih, r.cha_fis_sirano, r.cha_Guid, r.AccountCode;

CREATE CLUSTERED INDEX IX_PlanJournal ON #Plan(cha_fis_tarih, cha_fis_sirano);

UPDATE p
SET BaseGuid = b.fis_Guid,
    Amount1 = p.Amount0 * CASE WHEN ABS(ISNULL(b.fis_meblag0, 0.0)) > 0.0000001
                              THEN ISNULL(b.fis_meblag1, 0.0) / b.fis_meblag0 ELSE 0.0 END,
    Amount2 = p.Amount0 * CASE WHEN ABS(ISNULL(b.fis_meblag0, 0.0)) > 0.0000001
                              THEN ISNULL(b.fis_meblag2, 0.0) / b.fis_meblag0 ELSE 0.0 END
FROM #Plan AS p
OUTER APPLY
(
    SELECT TOP (1) m.*
    FROM dbo.MUHASEBE_FISLERI AS m WITH (READCOMMITTEDLOCK)
    WHERE m.fis_tarih = p.cha_fis_tarih
      AND m.fis_sira_no = p.cha_fis_sirano
    ORDER BY CASE WHEN m.fis_ticari_uid = p.cha_Guid THEN 0 ELSE 1 END,
             ABS(ISNULL(m.fis_meblag0, 0.0)) DESC,
             m.fis_satir_no
) AS b;

;WITH MaxLines AS
(
    SELECT fis_tarih, fis_sira_no, MAX(ISNULL(fis_satir_no, -1)) AS MaxLineNo
    FROM dbo.MUHASEBE_FISLERI WITH (READCOMMITTEDLOCK)
    WHERE fis_tarih >= '2026-09-02' AND fis_tarih < '2026-10-16'
    GROUP BY fis_tarih, fis_sira_no
), Numbered AS
(
    SELECT p.*,
           ml.MaxLineNo,
           ROW_NUMBER() OVER
             (PARTITION BY p.cha_fis_tarih, p.cha_fis_sirano
              ORDER BY p.cha_Guid, p.AccountCode) AS RowNo
    FROM #Plan AS p
    INNER JOIN MaxLines AS ml
        ON ml.fis_tarih = p.cha_fis_tarih
       AND ml.fis_sira_no = p.cha_fis_sirano
)
UPDATE Numbered SET NewLineNo = MaxLineNo + RowNo;

SELECT DISTINCT cha_fis_tarih, cha_fis_sirano
INTO #CandidateJournal
FROM #Line;
CREATE UNIQUE CLUSTERED INDEX IX_CandidateJournal
    ON #CandidateJournal(cha_fis_tarih, cha_fis_sirano);

DECLARE @CandidateJournalCount int = (SELECT COUNT(*) FROM #CandidateJournal);

IF @IsPilot = 1 AND @CandidateJournalCount = 0
    THROW 50992, 'Pilot hedefi duzeltme kosullarina uyan bir muhasebe fisi bulamadi.', 1;

IF @IsPilot = 1 AND @CandidateJournalCount > 1
    THROW 50993, 'Pilot hedefi birden fazla muhasebe fisi buldu. TargetJournalDate ile tek fisi secin.', 1;

/* 5. Apply oncesi zorunlu kontroller. */
DECLARE @UnsupportedOperations int =
(
    SELECT COUNT(*)
    FROM #Document AS d
    WHERE NOT
    (
           (d.cha_evrak_tip = 0 AND d.cha_tip = 1 AND d.cha_cinsi = 6
            AND d.cha_normal_Iade = 0 AND d.cha_ticaret_turu = 0)
        OR (d.cha_evrak_tip = 63 AND d.cha_tip = 0 AND d.cha_cinsi IN (6, 7)
            AND d.cha_normal_Iade = 0)
        OR (d.cha_evrak_tip = 63 AND d.cha_tip = 0 AND d.cha_cinsi = 6
            AND d.cha_normal_Iade = 1 AND d.cha_ticaret_turu = 0)
        OR (d.cha_evrak_tip = 63 AND d.cha_tip = 0 AND d.cha_cinsi = 15
            AND d.cha_normal_Iade = 1 AND d.cha_ticaret_turu = 0)
    )
);

DECLARE @MissingStocks int =
    (SELECT COUNT(*) FROM #Resolved WHERE FoundStockCode IS NULL);
DECLARE @EmptyTemplates int =
    (SELECT COUNT(*) FROM #Resolved WHERE NULLIF(LTRIM(RTRIM(AccountTemplate)), '') IS NULL);
DECLARE @FractionalTaxRates int =
    (SELECT COUNT(*) FROM #Tax WHERE TaxRate IS NULL OR ABS(TaxRate - ROUND(TaxRate, 0)) > 0.000001);
DECLARE @MissingAccounts int =
(
    SELECT COUNT(*)
    FROM #Plan AS p
    LEFT JOIN dbo.MUHASEBE_HESAP_PLANI AS h WITH (READCOMMITTEDLOCK)
        ON h.muh_hesap_kod = p.AccountCode
    WHERE h.muh_hesap_kod IS NULL
);
DECLARE @MissingBaseRows int =
    (SELECT COUNT(*) FROM #Plan WHERE BaseGuid IS NULL);

;WITH Expected AS
(
    SELECT
        cj.cha_fis_tarih,
        cj.cha_fis_sirano,
        MAX(j.Balance0) AS Balance0,
        MAX(j.Balance1) AS Balance1,
        MAX(j.Balance2) AS Balance2,
        SUM(p.Amount0) AS Missing0,
        SUM(p.Amount1) AS Missing1,
        SUM(p.Amount2) AS Missing2
    FROM #CandidateJournal AS cj
    INNER JOIN #Journal AS j
        ON j.fis_tarih = cj.cha_fis_tarih
       AND j.fis_sira_no = cj.cha_fis_sirano
    INNER JOIN #Plan AS p
        ON p.cha_fis_tarih = cj.cha_fis_tarih
       AND p.cha_fis_sirano = cj.cha_fis_sirano
    GROUP BY cj.cha_fis_tarih, cj.cha_fis_sirano
)
SELECT * INTO #Expected FROM Expected;

DECLARE @UnbalancedAfterPlan int =
(
    SELECT COUNT(*) FROM #Expected
    WHERE ABS(Balance0 + Missing0) > 0.01
       OR ABS(Balance1 + Missing1) > 0.01
       OR ABS(Balance2 + Missing2) > 0.01
);

SELECT
    @Apply AS ApplyMode,
    @IsPilot AS PilotMode,
    @TargetJournalNo AS TargetJournalNo,
    @TargetYevmiyeNo AS TargetYevmiyeNo,
    @TargetAccountingGuid AS TargetAccountingGuid,
    @TargetJournalDate AS TargetJournalDate,
    @TargetDocumentGuid AS TargetDocumentGuid,
    @AllowBulkApply AS AllowBulkApply,
    @BatchId AS BatchId,
    (SELECT COUNT(*) FROM #CandidateJournal) AS CandidateJournals,
    (SELECT COUNT(*) FROM #Document) AS CandidateDocuments,
    (SELECT COUNT(*) FROM #Line) AS BrokenStockRows,
    (SELECT COUNT(*) FROM #Plan) AS PlannedAccountingRows,
    @UnsupportedOperations AS UnsupportedOperations,
    @MissingStocks AS MissingStocks,
    @EmptyTemplates AS EmptyTemplates,
    @FractionalTaxRates AS FractionalTaxRates,
    @MissingAccounts AS MissingAccounts,
    @MissingBaseRows AS MissingBaseRows,
    @UnbalancedAfterPlan AS UnbalancedAfterPlan,
    (SELECT MAX(ABS(Balance0 + Missing0)) FROM #Expected) AS MaxResidual0,
    (SELECT MAX(ABS(Balance1 + Missing1)) FROM #Expected) AS MaxResidual1,
    (SELECT MAX(ABS(Balance2 + Missing2)) FROM #Expected) AS MaxResidual2;

SELECT AccountCode, COUNT(*) AS PlannedRows, SUM(StockLineCount) AS StockRows,
       SUM(Amount0) AS Amount0
FROM #Plan
GROUP BY AccountCode
ORDER BY PlannedRows DESC;

/* Pilot kontrolde kullaniciya duzeltilecek evrak ve satirlari ayrintili goster. */
IF @IsPilot = 1
BEGIN
    SELECT
        d.cha_fis_tarih AS JournalDate,
        d.cha_fis_sirano AS JournalNo,
        d.cha_Guid AS DocumentGuid,
        CONCAT(d.cha_evrakno_seri, '.', d.cha_evrakno_sira) AS DocumentNo,
        d.cha_evrak_tip,
        d.cha_tip,
        d.cha_cinsi,
        d.cha_normal_Iade,
        COUNT(l.sth_Guid) AS BrokenStockRows,
        SUM(l.NetAmount) AS NetStockAmount
    FROM #Document AS d
    INNER JOIN #Line AS l ON l.cha_Guid = d.cha_Guid
    GROUP BY d.cha_fis_tarih, d.cha_fis_sirano, d.cha_Guid,
             d.cha_evrakno_seri, d.cha_evrakno_sira, d.cha_evrak_tip,
             d.cha_tip, d.cha_cinsi, d.cha_normal_Iade
    ORDER BY d.cha_fis_tarih, d.cha_fis_sirano, DocumentNo;

    SELECT
        p.cha_fis_tarih AS JournalDate,
        p.cha_fis_sirano AS JournalNo,
        p.cha_Guid AS DocumentGuid,
        p.AccountCode,
        p.StockLineCount,
        p.Amount0,
        p.Amount1,
        p.Amount2,
        p.NewLineNo
    FROM #Plan AS p
    ORDER BY p.cha_fis_tarih, p.cha_fis_sirano, p.NewLineNo;
END;

IF @UnsupportedOperations <> 0 OR @MissingStocks <> 0 OR @EmptyTemplates <> 0
   OR @FractionalTaxRates <> 0 OR @MissingAccounts <> 0
   OR @MissingBaseRows <> 0 OR @UnbalancedAfterPlan <> 0
BEGIN
    THROW 51000, 'On izleme kontrolleri temiz degil. Canli uygulama durduruldu.', 1;
END;

IF @Apply = 0
BEGIN
    IF @IsPilot = 1
        PRINT 'PILOT ON IZLEME TAMAMLANDI. Hicbir canli kayit degistirilmedi.';
    ELSE
        PRINT 'TOPLU ON IZLEME TAMAMLANDI. Hicbir canli kayit degistirilmedi.';
    RETURN;
END;

/* 6. Kalici denetim tablolari. */
IF OBJECT_ID('dbo.FURPA_MUH_REPAIR_BATCH_20261028', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FURPA_MUH_REPAIR_BATCH_20261028
    (
        BatchId uniqueidentifier NOT NULL PRIMARY KEY,
        StartedAt datetime NOT NULL,
        CompletedAt datetime NULL,
        Status nvarchar(30) NOT NULL,
        CandidateJournals int NOT NULL,
        CandidateDocuments int NOT NULL,
        BrokenStockRows int NOT NULL,
        PlannedAccountingRows int NOT NULL,
        ErrorMessage nvarchar(2048) NULL
    );
END;

IF OBJECT_ID('dbo.FURPA_MUH_REPAIR_JOURNAL_20261028', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FURPA_MUH_REPAIR_JOURNAL_20261028
    (
        BatchId uniqueidentifier NOT NULL,
        JournalDate datetime NOT NULL,
        JournalNo int NOT NULL,
        StartedAt datetime NOT NULL,
        CompletedAt datetime NULL,
        Status nvarchar(30) NOT NULL,
        ErrorMessage nvarchar(2048) NULL,
        CONSTRAINT PK_FURPA_MUH_REPAIR_JOURNAL_20261028
            PRIMARY KEY (BatchId, JournalDate, JournalNo)
    );
END;

IF OBJECT_ID('dbo.FURPA_MUH_REPAIR_INSERT_20261028', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FURPA_MUH_REPAIR_INSERT_20261028
    (
        BatchId uniqueidentifier NOT NULL,
        JournalDate datetime NOT NULL,
        JournalNo int NOT NULL,
        DocumentGuid uniqueidentifier NOT NULL,
        InsertedGuid uniqueidentifier NOT NULL,
        AccountCode nvarchar(50) NOT NULL,
        Amount0 float NOT NULL,
        Amount1 float NOT NULL,
        Amount2 float NOT NULL,
        [LineNo] int NOT NULL,
        CreatedAt datetime NOT NULL,
        CONSTRAINT PK_FURPA_MUH_REPAIR_INSERT_20261028 PRIMARY KEY (BatchId, InsertedGuid)
    );
END;

IF OBJECT_ID('dbo.FURPA_MUH_REPAIR_STOCK_20261028', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FURPA_MUH_REPAIR_STOCK_20261028
    (
        BatchId uniqueidentifier NOT NULL,
        StockMovementGuid uniqueidentifier NOT NULL,
        DocumentGuid uniqueidentifier NOT NULL,
        JournalDate datetime NOT NULL,
        OldJournalNo int NULL,
        NewJournalNo int NOT NULL,
        ChangedAt datetime NOT NULL,
        CONSTRAINT PK_FURPA_MUH_REPAIR_STOCK_20261028 PRIMARY KEY (BatchId, StockMovementGuid)
    );
END;

INSERT dbo.FURPA_MUH_REPAIR_BATCH_20261028
(
    BatchId, StartedAt, Status, CandidateJournals, CandidateDocuments,
    BrokenStockRows, PlannedAccountingRows
)
SELECT @BatchId, @Now, N'Running',
       (SELECT COUNT(*) FROM #CandidateJournal),
       (SELECT COUNT(*) FROM #Document),
       (SELECT COUNT(*) FROM #Line),
       (SELECT COUNT(*) FROM #Plan);

/* MUHASEBE_FISLERI'nin tum mevcut kolonlarini kopyala; yalniz gerekli alanlari override et. */
DECLARE @ColumnList nvarchar(max);
DECLARE @SelectList nvarchar(max);
DECLARE @InsertSql nvarchar(max);

SELECT @ColumnList = STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(c.name)), N',')
       WITHIN GROUP (ORDER BY c.column_id)
FROM sys.columns AS c
WHERE c.object_id = OBJECT_ID('dbo.MUHASEBE_FISLERI')
  AND c.is_identity = 0 AND c.is_computed = 0;

SELECT @SelectList = STRING_AGG(CONVERT(nvarchar(max),
    CASE c.name
        WHEN 'fis_Guid' THEN N'p.NewGuid'
        WHEN 'fis_DBCno' THEN N'CONVERT(smallint, 0)'
        WHEN 'fis_SpecRECno' THEN N'CONVERT(int, 0)'
        WHEN 'fis_degisti' THEN N'CONVERT(bit, 0)'
        WHEN 'fis_checksum' THEN N'CONVERT(int, 0)'
        WHEN 'fis_create_date' THEN N'@InsertNow'
        WHEN 'fis_lastup_date' THEN N'@InsertNow'
        WHEN 'fis_hesap_kod' THEN N'p.AccountCode'
        WHEN 'fis_satir_no' THEN N'p.NewLineNo'
        WHEN 'fis_meblag0' THEN N'p.Amount0'
        WHEN 'fis_meblag1' THEN N'p.Amount1'
        WHEN 'fis_meblag2' THEN N'p.Amount2'
        WHEN 'fis_ticari_uid' THEN N'p.cha_Guid'
        ELSE N'b.' + QUOTENAME(c.name)
    END), N',') WITHIN GROUP (ORDER BY c.column_id)
FROM sys.columns AS c
WHERE c.object_id = OBJECT_ID('dbo.MUHASEBE_FISLERI')
  AND c.is_identity = 0 AND c.is_computed = 0;

SET @InsertSql = N'
INSERT dbo.MUHASEBE_FISLERI (' + @ColumnList + N')
SELECT ' + @SelectList + N'
FROM #CurrentPlan AS p
INNER JOIN dbo.MUHASEBE_FISLERI AS b ON b.fis_Guid = p.BaseGuid;';

CREATE TABLE #CurrentPlan
(
    cha_fis_tarih datetime NOT NULL,
    cha_fis_sirano int NOT NULL,
    cha_Guid uniqueidentifier NOT NULL,
    AccountCode nvarchar(50) NOT NULL,
    Amount0 float NOT NULL,
    Amount1 float NOT NULL,
    Amount2 float NOT NULL,
    BaseGuid uniqueidentifier NOT NULL,
    NewGuid uniqueidentifier NOT NULL,
    NewLineNo int NOT NULL
);

DECLARE @JournalDate datetime;
DECLARE @JournalNo int;

DECLARE journal_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT cha_fis_tarih, cha_fis_sirano
FROM #CandidateJournal
ORDER BY cha_fis_tarih, cha_fis_sirano;

OPEN journal_cursor;
FETCH NEXT FROM journal_cursor INTO @JournalDate, @JournalNo;

WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT dbo.FURPA_MUH_REPAIR_JOURNAL_20261028
            (BatchId, JournalDate, JournalNo, StartedAt, Status)
        VALUES (@BatchId, @JournalDate, @JournalNo, GETDATE(), N'Running');

        /* Fis o anda degismisse bu fis icin devam etme. */
        DECLARE @CurrentBalance0 float;
        SELECT @CurrentBalance0 = SUM(ISNULL(fis_meblag0, 0.0))
        FROM dbo.MUHASEBE_FISLERI WITH (UPDLOCK, HOLDLOCK)
        WHERE fis_tarih = @JournalDate AND fis_sira_no = @JournalNo;

        IF ABS(@CurrentBalance0 -
            (SELECT Balance0 FROM #Journal WHERE fis_tarih = @JournalDate AND fis_sira_no = @JournalNo)) > 0.005
            THROW 51001, 'Fis on izlemeden sonra degisti.', 1;

        IF EXISTS
        (
            SELECT 1
            FROM #Line AS l
            INNER JOIN dbo.STOK_HAREKETLERI AS s WITH (UPDLOCK, HOLDLOCK)
                ON s.sth_Guid = l.sth_Guid
            WHERE l.cha_fis_tarih = @JournalDate
              AND l.cha_fis_sirano = @JournalNo
              AND ISNULL(s.sth_fis_sirano, -2147483648) <> ISNULL(l.OldStockJournalNo, -2147483648)
        )
            THROW 51002, 'Stok fis baglantisi on izlemeden sonra degisti.', 1;

        TRUNCATE TABLE #CurrentPlan;
        INSERT #CurrentPlan
        SELECT cha_fis_tarih, cha_fis_sirano, cha_Guid, AccountCode,
               Amount0, Amount1, Amount2, BaseGuid, NewGuid, NewLineNo
        FROM #Plan
        WHERE cha_fis_tarih = @JournalDate AND cha_fis_sirano = @JournalNo;

        DECLARE @InsertNow datetime = GETDATE();
        EXEC sys.sp_executesql @InsertSql, N'@InsertNow datetime', @InsertNow = @InsertNow;

        INSERT dbo.FURPA_MUH_REPAIR_INSERT_20261028
        (
            BatchId, JournalDate, JournalNo, DocumentGuid, InsertedGuid,
            AccountCode, Amount0, Amount1, Amount2, [LineNo], CreatedAt
        )
        SELECT @BatchId, @JournalDate, @JournalNo, cha_Guid, NewGuid,
               AccountCode, Amount0, Amount1, Amount2, NewLineNo, @InsertNow
        FROM #CurrentPlan;

        INSERT dbo.FURPA_MUH_REPAIR_STOCK_20261028
        (
            BatchId, StockMovementGuid, DocumentGuid, JournalDate,
            OldJournalNo, NewJournalNo, ChangedAt
        )
        SELECT @BatchId, l.sth_Guid, l.cha_Guid, @JournalDate,
               l.OldStockJournalNo, @JournalNo, @InsertNow
        FROM #Line AS l
        WHERE l.cha_fis_tarih = @JournalDate AND l.cha_fis_sirano = @JournalNo;

        UPDATE s
        SET s.sth_fis_sirano = @JournalNo
        FROM dbo.STOK_HAREKETLERI AS s
        INNER JOIN #Line AS l ON l.sth_Guid = s.sth_Guid
        WHERE l.cha_fis_tarih = @JournalDate
          AND l.cha_fis_sirano = @JournalNo
          AND ISNULL(s.sth_fis_sirano, -2147483648) = ISNULL(l.OldStockJournalNo, -2147483648);

        IF EXISTS
        (
            SELECT 1
            FROM dbo.STOK_HAREKETLERI AS s
            INNER JOIN #Line AS l ON l.sth_Guid = s.sth_Guid
            WHERE l.cha_fis_tarih = @JournalDate
              AND l.cha_fis_sirano = @JournalNo
              AND ISNULL(s.sth_fis_sirano, -2147483648) <> @JournalNo
        )
            THROW 51003, 'Stok fis baglantilarinin tamami guncellenemedi.', 1;

        DECLARE @Final0 float, @Final1 float, @Final2 float;
        SELECT @Final0 = SUM(ISNULL(fis_meblag0, 0.0)),
               @Final1 = SUM(ISNULL(fis_meblag1, 0.0)),
               @Final2 = SUM(ISNULL(fis_meblag2, 0.0))
        FROM dbo.MUHASEBE_FISLERI
        WHERE fis_tarih = @JournalDate AND fis_sira_no = @JournalNo;

        IF ABS(@Final0) > 0.01 OR ABS(@Final1) > 0.01 OR ABS(@Final2) > 0.01
            THROW 51004, 'Fis uygulama sonrasinda dengede degil.', 1;

        UPDATE dbo.FURPA_MUH_REPAIR_JOURNAL_20261028
        SET CompletedAt = GETDATE(), Status = N'Completed'
        WHERE BatchId = @BatchId AND JournalDate = @JournalDate AND JournalNo = @JournalNo;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

        UPDATE dbo.FURPA_MUH_REPAIR_BATCH_20261028
        SET Status = N'Failed', ErrorMessage = ERROR_MESSAGE()
        WHERE BatchId = @BatchId;

        CLOSE journal_cursor;
        DEALLOCATE journal_cursor;
        THROW;
    END CATCH;

    FETCH NEXT FROM journal_cursor INTO @JournalDate, @JournalNo;
END;

CLOSE journal_cursor;
DEALLOCATE journal_cursor;

UPDATE dbo.FURPA_MUH_REPAIR_BATCH_20261028
SET CompletedAt = GETDATE(), Status = N'Completed'
WHERE BatchId = @BatchId;

/* 7. Canli uygulama sonu kesin dogrulama. */
SELECT
    @BatchId AS BatchId,
    b.Status,
    b.CandidateJournals,
    (SELECT COUNT(*) FROM dbo.FURPA_MUH_REPAIR_JOURNAL_20261028 j
     WHERE j.BatchId = @BatchId AND j.Status = N'Completed') AS CompletedJournals,
    b.PlannedAccountingRows,
    (SELECT COUNT(*) FROM dbo.FURPA_MUH_REPAIR_INSERT_20261028 i
     WHERE i.BatchId = @BatchId) AS InsertedAccountingRows,
    b.BrokenStockRows,
    (SELECT COUNT(*) FROM dbo.FURPA_MUH_REPAIR_STOCK_20261028 s
     WHERE s.BatchId = @BatchId) AS UpdatedStockRows,
    (SELECT COUNT(*)
     FROM #CandidateJournal cj
     CROSS APPLY
     (
         SELECT SUM(ISNULL(m.fis_meblag0, 0.0)) B0,
                SUM(ISNULL(m.fis_meblag1, 0.0)) B1,
                SUM(ISNULL(m.fis_meblag2, 0.0)) B2
         FROM dbo.MUHASEBE_FISLERI m
         WHERE m.fis_tarih = cj.cha_fis_tarih AND m.fis_sira_no = cj.cha_fis_sirano
     ) q
     WHERE ABS(q.B0) > 0.01 OR ABS(q.B1) > 0.01 OR ABS(q.B2) > 0.01) AS RemainingUnbalancedJournals,
    (SELECT COUNT(*)
     FROM dbo.STOK_HAREKETLERI s
     INNER JOIN #Line l ON l.sth_Guid = s.sth_Guid
     WHERE ISNULL(s.sth_fis_sirano, -2147483648) <> l.cha_fis_sirano) AS RemainingBrokenStockRows
FROM dbo.FURPA_MUH_REPAIR_BATCH_20261028 b
WHERE b.BatchId = @BatchId;

