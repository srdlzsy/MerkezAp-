/*
    MUHASEBE_FISLERI kurus dengeleme duzeltmesi.

    Amac:
      Ham toplami sifir oldugu halde Mikro'nun satir bazli 2 hane
      yuvarlamasinda +/- 0,01 bakiye veren fisleri tam 0,00 yapar.

    Kapsam ve guvenlik:
      - Yalniz Ekim 2026 fislerini inceler.
      - fis_meblag0/1/2 ham toplamlarinin ucu de zaten sifir olmalidir.
      - Yerel para yuvarlama farki tam bir kurus olmalidir.
      - Diger para alanlarindaki yuvarlama farki de bir kurusu gecemez.
      - Gercek bakiyesi olan 207,90 / 2.305,24 / 70,42 gibi fislere dokunmaz.
      - Her satirin eski ve yeni tutarini kalici geri alma tablosuna yazar.

    Kullanim:
      1. Pilot on izleme:
         @Apply = 0, @TargetYevmiyeNo = 2516109
      2. Pilot uygulama:
         @Apply = 1, @TargetYevmiyeNo = 2516109
      3. Toplu on izleme:
         @Apply = 0, @TargetYevmiyeNo = NULL, @AllowBulkApply = 0
      4. Toplu uygulama:
         @Apply = 1, @TargetYevmiyeNo = NULL, @AllowBulkApply = 1
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 120000;

DECLARE @Apply bit = 0;
DECLARE @TargetYevmiyeNo int = NULL;                    -- Pilot icin ornek: 2516109
DECLARE @AllowBulkApply bit = 0;
DECLARE @StartDate date = '2026-10-01';
DECLARE @EndDateExclusive date = '2026-11-01';
DECLARE @BatchId uniqueidentifier = NEWID();
DECLARE @Now datetime = GETDATE();

IF @Apply = 1 AND @TargetYevmiyeNo IS NULL AND @AllowBulkApply = 0
    THROW 52000, 'Toplu uygulama kilitli. AllowBulkApply degerini 1 yapin.', 1;

IF OBJECT_ID('tempdb..#Candidate') IS NOT NULL DROP TABLE #Candidate;
IF OBJECT_ID('tempdb..#LinePlan') IS NOT NULL DROP TABLE #LinePlan;

/* Mikro'nun ekranda kullandigi satir bazli iki hane toplamini da hesapla. */
SELECT
    m.fis_tarih AS JournalDate,
    m.fis_sira_no AS JournalNo,
    MAX(m.fis_yevmiye_no) AS YevmiyeNo,
    COUNT(*) AS LineCount,
    SUM(ISNULL(m.fis_meblag0, 0.0)) AS Raw0,
    SUM(ISNULL(m.fis_meblag1, 0.0)) AS Raw1,
    SUM(ISNULL(m.fis_meblag2, 0.0)) AS Raw2,
    SUM(ROUND(ISNULL(m.fis_meblag0, 0.0), 2)) AS Rounded0,
    SUM(ROUND(ISNULL(m.fis_meblag1, 0.0), 2)) AS Rounded1,
    SUM(ROUND(ISNULL(m.fis_meblag2, 0.0), 2)) AS Rounded2
INTO #Candidate
FROM dbo.MUHASEBE_FISLERI AS m WITH (READCOMMITTEDLOCK)
WHERE m.fis_tarih >= @StartDate
  AND m.fis_tarih < @EndDateExclusive
GROUP BY m.fis_tarih, m.fis_sira_no
HAVING ABS(SUM(ISNULL(m.fis_meblag0, 0.0))) <= 0.005
   AND ABS(SUM(ISNULL(m.fis_meblag1, 0.0))) <= 0.005
   AND ABS(SUM(ISNULL(m.fis_meblag2, 0.0))) <= 0.005
   AND ABS(SUM(ROUND(ISNULL(m.fis_meblag0, 0.0), 2))) >= 0.005
   AND ABS(SUM(ROUND(ISNULL(m.fis_meblag0, 0.0), 2))) <= 0.011
   AND ABS(SUM(ROUND(ISNULL(m.fis_meblag1, 0.0), 2))) <= 0.011
   AND ABS(SUM(ROUND(ISNULL(m.fis_meblag2, 0.0), 2))) <= 0.011
   AND (@TargetYevmiyeNo IS NULL OR MAX(m.fis_yevmiye_no) = @TargetYevmiyeNo);

CREATE UNIQUE CLUSTERED INDEX IX_Candidate
    ON #Candidate(JournalDate, JournalNo);

IF @TargetYevmiyeNo IS NOT NULL AND NOT EXISTS (SELECT 1 FROM #Candidate)
    THROW 52001, 'Pilot yevmiye kurus dengeleme kosullarina uymuyor.', 1;

/*
   Her satiri iki haneye yuvarla. Yuvarlama bakiyesini mutlak tutari
   en buyuk satira uygula; boylece goreli degisim en dusuk kalir.
*/
;WITH Ranked AS
(
    SELECT
        c.JournalDate,
        c.JournalNo,
        c.YevmiyeNo,
        m.fis_Guid,
        m.fis_satir_no,
        m.fis_hesap_kod,
        m.fis_meblag0 AS Old0,
        m.fis_meblag1 AS Old1,
        m.fis_meblag2 AS Old2,
        m.fis_lastup_date AS OldLastUpdate,
        m.fis_degisti AS OldChanged,
        CONVERT(float, ROUND(ISNULL(m.fis_meblag0, 0.0), 2)) AS RoundedLine0,
        CONVERT(float, ROUND(ISNULL(m.fis_meblag1, 0.0), 2)) AS RoundedLine1,
        CONVERT(float, ROUND(ISNULL(m.fis_meblag2, 0.0), 2)) AS RoundedLine2,
        ROW_NUMBER() OVER
        (
            PARTITION BY c.JournalDate, c.JournalNo
            ORDER BY
                ABS(ISNULL(m.fis_meblag0, 0.0)) DESC,
                m.fis_satir_no DESC,
                m.fis_Guid
        ) AS BalanceRank
    FROM #Candidate AS c
    INNER JOIN dbo.MUHASEBE_FISLERI AS m WITH (READCOMMITTEDLOCK)
        ON m.fis_tarih = c.JournalDate
       AND m.fis_sira_no = c.JournalNo
), Planned AS
(
    SELECT
        r.*,
        SUM(r.RoundedLine0) OVER (PARTITION BY r.JournalDate, r.JournalNo) AS RoundedTotal0,
        SUM(r.RoundedLine1) OVER (PARTITION BY r.JournalDate, r.JournalNo) AS RoundedTotal1,
        SUM(r.RoundedLine2) OVER (PARTITION BY r.JournalDate, r.JournalNo) AS RoundedTotal2
    FROM Ranked AS r
)
SELECT
    p.JournalDate,
    p.JournalNo,
    p.YevmiyeNo,
    p.fis_Guid,
    p.fis_satir_no,
    p.fis_hesap_kod,
    p.Old0,
    p.Old1,
    p.Old2,
    p.OldLastUpdate,
    p.OldChanged,
    CONVERT(float, p.RoundedLine0 - CASE WHEN p.BalanceRank = 1 THEN p.RoundedTotal0 ELSE 0.0 END) AS New0,
    CONVERT(float, p.RoundedLine1 - CASE WHEN p.BalanceRank = 1 THEN p.RoundedTotal1 ELSE 0.0 END) AS New1,
    CONVERT(float, p.RoundedLine2 - CASE WHEN p.BalanceRank = 1 THEN p.RoundedTotal2 ELSE 0.0 END) AS New2,
    p.BalanceRank
INTO #LinePlan
FROM Planned AS p;

CREATE UNIQUE CLUSTERED INDEX IX_LinePlan ON #LinePlan(fis_Guid);
CREATE INDEX IX_LinePlanJournal ON #LinePlan(JournalDate, JournalNo);

DECLARE @CandidateCount int = (SELECT COUNT(*) FROM #Candidate);
DECLARE @LineCount int = (SELECT COUNT(*) FROM #LinePlan);
DECLARE @MaxAdjustment float =
(
    SELECT MAX(v.Adjustment)
    FROM #LinePlan AS p
    CROSS APPLY
    (
        VALUES
            (ABS(ISNULL(p.New0, 0.0) - ISNULL(p.Old0, 0.0))),
            (ABS(ISNULL(p.New1, 0.0) - ISNULL(p.Old1, 0.0))),
            (ABS(ISNULL(p.New2, 0.0) - ISNULL(p.Old2, 0.0)))
    ) AS v(Adjustment)
);

DECLARE @InvalidPlanCount int =
(
    SELECT COUNT(*)
    FROM
    (
        SELECT
            JournalDate,
            JournalNo,
            SUM(New0) AS NewRaw0,
            SUM(New1) AS NewRaw1,
            SUM(New2) AS NewRaw2,
            SUM(ROUND(New0, 2)) AS NewRounded0,
            SUM(ROUND(New1, 2)) AS NewRounded1,
            SUM(ROUND(New2, 2)) AS NewRounded2
        FROM #LinePlan
        GROUP BY JournalDate, JournalNo
    ) AS x
    WHERE ABS(x.NewRaw0) > 0.000001
       OR ABS(x.NewRaw1) > 0.000001
       OR ABS(x.NewRaw2) > 0.000001
       OR ABS(x.NewRounded0) > 0.000001
       OR ABS(x.NewRounded1) > 0.000001
       OR ABS(x.NewRounded2) > 0.000001
);

SELECT
    @Apply AS ApplyMode,
    @TargetYevmiyeNo AS TargetYevmiyeNo,
    @AllowBulkApply AS AllowBulkApply,
    @BatchId AS BatchId,
    @CandidateCount AS CandidateJournals,
    @LineCount AS CandidateLines,
    @MaxAdjustment AS MaxLineAdjustment,
    @InvalidPlanCount AS InvalidPlanCount;

SELECT TOP (20)
    c.JournalDate,
    c.JournalNo,
    c.YevmiyeNo,
    c.LineCount,
    c.Raw0,
    c.Rounded0 AS OldMikroBalance0,
    c.Rounded1 AS OldMikroBalance1,
    c.Rounded2 AS OldMikroBalance2
FROM #Candidate AS c
ORDER BY c.YevmiyeNo;

IF @MaxAdjustment > 0.021
    THROW 52002, 'Bir satirdaki degisim iki kurusu geciyor. Uygulama durduruldu.', 1;

IF @InvalidPlanCount <> 0
    THROW 52003, 'Yeni tutar plani tam sifir vermiyor. Uygulama durduruldu.', 1;

IF @Apply = 0
BEGIN
    PRINT 'ON IZLEME TAMAMLANDI. Canli kayit degistirilmedi.';
    RETURN;
END;

/* Kalici denetim ve geri alma tablolari. */
IF OBJECT_ID('dbo.FURPA_MUH_ROUNDING_BATCH_20261030', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FURPA_MUH_ROUNDING_BATCH_20261030
    (
        BatchId uniqueidentifier NOT NULL PRIMARY KEY,
        StartedAt datetime NOT NULL,
        CompletedAt datetime NULL,
        Status nvarchar(30) NOT NULL,
        TargetYevmiyeNo int NULL,
        CandidateJournals int NOT NULL,
        CandidateLines int NOT NULL,
        MaxLineAdjustment float NOT NULL,
        ErrorMessage nvarchar(2048) NULL
    );
END;

IF OBJECT_ID('dbo.FURPA_MUH_ROUNDING_LINE_20261030', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FURPA_MUH_ROUNDING_LINE_20261030
    (
        BatchId uniqueidentifier NOT NULL,
        JournalDate datetime NOT NULL,
        JournalNo int NOT NULL,
        YevmiyeNo int NOT NULL,
        AccountingGuid uniqueidentifier NOT NULL,
        AccountCode nvarchar(50) NOT NULL,
        Old0 float NOT NULL,
        Old1 float NOT NULL,
        Old2 float NOT NULL,
        New0 float NOT NULL,
        New1 float NOT NULL,
        New2 float NOT NULL,
        OldLastUpdate datetime NULL,
        OldChanged bit NOT NULL,
        ChangedAt datetime NOT NULL,
        CONSTRAINT PK_FURPA_MUH_ROUNDING_LINE_20261030
            PRIMARY KEY (BatchId, AccountingGuid)
    );
END;

INSERT dbo.FURPA_MUH_ROUNDING_BATCH_20261030
(
    BatchId, StartedAt, Status, TargetYevmiyeNo,
    CandidateJournals, CandidateLines, MaxLineAdjustment
)
VALUES
(
    @BatchId, @Now, N'Running', @TargetYevmiyeNo,
    @CandidateCount, @LineCount, @MaxAdjustment
);

BEGIN TRY
    BEGIN TRANSACTION;

    /* Plan sonrasinda satir degistiyse sessizce uzerine yazma. */
    IF EXISTS
    (
        SELECT 1
        FROM #LinePlan AS p
        INNER JOIN dbo.MUHASEBE_FISLERI AS m WITH (UPDLOCK, HOLDLOCK)
            ON m.fis_Guid = p.fis_Guid
        WHERE ISNULL(m.fis_meblag0, 0.0) <> ISNULL(p.Old0, 0.0)
           OR ISNULL(m.fis_meblag1, 0.0) <> ISNULL(p.Old1, 0.0)
           OR ISNULL(m.fis_meblag2, 0.0) <> ISNULL(p.Old2, 0.0)
           OR ISNULL(m.fis_lastup_date, '19000101') <> ISNULL(p.OldLastUpdate, '19000101')
    )
        THROW 52004, 'Bir muhasebe satiri on izlemeden sonra degisti.', 1;

    INSERT dbo.FURPA_MUH_ROUNDING_LINE_20261030
    (
        BatchId, JournalDate, JournalNo, YevmiyeNo, AccountingGuid,
        AccountCode, Old0, Old1, Old2, New0, New1, New2,
        OldLastUpdate, OldChanged, ChangedAt
    )
    SELECT
        @BatchId, p.JournalDate, p.JournalNo, p.YevmiyeNo, p.fis_Guid,
        p.fis_hesap_kod, p.Old0, p.Old1, p.Old2, p.New0, p.New1, p.New2,
        p.OldLastUpdate, p.OldChanged, @Now
    FROM #LinePlan AS p;

    UPDATE m
    SET m.fis_meblag0 = p.New0,
        m.fis_meblag1 = p.New1,
        m.fis_meblag2 = p.New2,
        m.fis_lastup_date = @Now,
        m.fis_degisti = 1
    FROM dbo.MUHASEBE_FISLERI AS m
    INNER JOIN #LinePlan AS p ON p.fis_Guid = m.fis_Guid;

    IF @@ROWCOUNT <> @LineCount
        THROW 52005, 'Planlanan muhasebe satirlarinin tamami guncellenemedi.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM #Candidate AS c
        CROSS APPLY
        (
            SELECT
                SUM(ISNULL(m.fis_meblag0, 0.0)) AS Raw0,
                SUM(ISNULL(m.fis_meblag1, 0.0)) AS Raw1,
                SUM(ISNULL(m.fis_meblag2, 0.0)) AS Raw2,
                SUM(ROUND(ISNULL(m.fis_meblag0, 0.0), 2)) AS Rounded0,
                SUM(ROUND(ISNULL(m.fis_meblag1, 0.0), 2)) AS Rounded1,
                SUM(ROUND(ISNULL(m.fis_meblag2, 0.0), 2)) AS Rounded2
            FROM dbo.MUHASEBE_FISLERI AS m
            WHERE m.fis_tarih = c.JournalDate
              AND m.fis_sira_no = c.JournalNo
        ) AS v
        WHERE ABS(v.Raw0) > 0.000001
           OR ABS(v.Raw1) > 0.000001
           OR ABS(v.Raw2) > 0.000001
           OR ABS(v.Rounded0) > 0.000001
           OR ABS(v.Rounded1) > 0.000001
           OR ABS(v.Rounded2) > 0.000001
    )
        THROW 52006, 'Uygulama sonrasinda tam sifir olmayan fis bulundu.', 1;

    COMMIT TRANSACTION;

    UPDATE dbo.FURPA_MUH_ROUNDING_BATCH_20261030
    SET CompletedAt = GETDATE(), Status = N'Completed'
    WHERE BatchId = @BatchId;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    UPDATE dbo.FURPA_MUH_ROUNDING_BATCH_20261030
    SET CompletedAt = GETDATE(), Status = N'Failed', ErrorMessage = ERROR_MESSAGE()
    WHERE BatchId = @BatchId;

    THROW;
END CATCH;

SELECT
    @BatchId AS BatchId,
    @CandidateCount AS CompletedJournals,
    @LineCount AS UpdatedLines,
    @MaxAdjustment AS MaxLineAdjustment,
    (SELECT COUNT(*)
     FROM #Candidate AS c
     CROSS APPLY
     (
         SELECT SUM(ROUND(ISNULL(m.fis_meblag0, 0.0), 2)) AS Balance0
         FROM dbo.MUHASEBE_FISLERI AS m
         WHERE m.fis_tarih = c.JournalDate AND m.fis_sira_no = c.JournalNo
     ) AS v
     WHERE ABS(v.Balance0) > 0.000001) AS RemainingMikroBalanceCount;

/*
    Geri alma ornegi (yalniz ilgili BatchId ile ve kontrollu calistirin):

    UPDATE m
    SET m.fis_meblag0 = b.Old0,
        m.fis_meblag1 = b.Old1,
        m.fis_meblag2 = b.Old2,
        m.fis_lastup_date = b.OldLastUpdate,
        m.fis_degisti = b.OldChanged
    FROM dbo.MUHASEBE_FISLERI AS m
    INNER JOIN dbo.FURPA_MUH_ROUNDING_LINE_20261030 AS b
        ON b.AccountingGuid = m.fis_Guid
    WHERE b.BatchId = 'BURAYA_BATCH_ID';
*/
