/*
    Furpa Merkez API tarafindan kullanilan ozel SQL Server indexlerinin
    tek ve yeniden calistirilabilir kurulum kaynagi.

    Kapsam:
    - MikroDesktop_FURPA_2024: liste, safe-create/read-back ve AXATA G01
    - Furpa: kasa icmal ve yardimci hareket sorgulari
    - FurpaMerkezDb: e-irsaliye kuyruk sorgulari

    Bu dosya veri veya prosedur degistirmez. Yalniz eksik indexleri olusturur
    ve iki eski firma liste indexini gerekirse guncel INCLUDE yapisina tasir.
    SQL Server 10.0.0.207 uzerinde SSMS/sqlcmd ile dusuk trafik saatinde
    calistirilmalidir. SHOPIGO ayri sunucudadir ve olculmus ihtiyac olmadigi
    icin bu katalogda yeni SHOPIGO indexi yoktur.

    STOK_HAREKETLERI liste ekranlari icin filtered indexler:

    Amac:
    - Firma mal kabul / firma sevk / firma iade listelerini hizlandirmak.
    - Firma mal kabul guvenli retry/recovery trace sorgusunu dar bir seek ile calistirmak.
    - Depolar arasi sevk, depo mal kabul bekleyen/gelen ve depo iade listelerini hizlandirmak.
    - Zayiat, masraf fisi ve virman listelerine dar kapsamli destek vermek.
    - Zayiat/masraf guvenli retry readback sorgusunu trace ile seek ettirmek.

    Notlar:
    - Bu script Mikro DB uzerinde manuel/kontrollu calistirilmalidir.
    - Canlida mesai disi uygulanmasi onerilir.
    - Indexler unique degildir; duplicate key riski olusturmaz.
    - CREATE INDEX sirasinda tablo buyukse gecici IO/CPU yuku olusturur.
    - Tum indexler ONLINE ve dusuk paralellikle olusturulur. SQL Server
      gerekli kilidi 5 saniyede alamazsa canli yazmalari uzun bekletmemek icin komut
      iptal olur; sakin bir bakim penceresinde yeniden calistirilmalidir.
*/

USE [MikroDesktop_FURPA_2024];
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET DEADLOCK_PRIORITY LOW;
SET LOCK_TIMEOUT 5000;
GO

IF OBJECT_ID(N'dbo.STOK_HAREKETLERI', N'U') IS NULL
BEGIN
    THROW 51000, 'dbo.STOK_HAREKETLERI tablosu bulunamadi. Script Mikro DB uzerinde calistirilmali.', 1;
END;
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes AS i
    WHERE i.object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND i.name = N'IX_FR_STH_FirmaMalKabul_Liste'
      AND NOT EXISTS (
          SELECT 1
          FROM sys.index_columns AS ic
          INNER JOIN sys.columns AS c
              ON c.object_id = ic.object_id
             AND c.column_id = ic.column_id
          WHERE ic.object_id = i.object_id
            AND ic.index_id = i.index_id
            AND ic.key_ordinal = 2
            AND c.name = N'sth_tarih'
      )
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_FirmaMalKabul_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_giris_depo_no,
        sth_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_satirno,
        sth_belge_no,
        sth_belge_tarih,
        sth_create_date,
        sth_cari_kodu,
        sth_cikis_depo_no,
        sth_evraktip,
        sth_tip,
        sth_normal_iade,
        sth_miktar,
        sth_tutar
    )
    WHERE
        sth_evraktip = 13
        AND sth_tip = 0
        AND sth_normal_iade = 0
        AND sth_tarih IS NOT NULL
    WITH
    (
        DROP_EXISTING = ON,
        ONLINE = ON,
        MAXDOP = 1,
        SORT_IN_TEMPDB = OFF
    );
END;
GO

IF NOT EXISTS (
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

/* Depo sevk/iade safe-create ve duplicate kontrolleri. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI') AND name = N'IX_FR_STH_DepoIade_Trace')
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_DepoIade_Trace
    ON dbo.STOK_HAREKETLERI
    (
        sth_eticaret_kanal_kodu,
        sth_cikis_depo_no,
        sth_tarih,
        sth_satirno
    )
    INCLUDE
    (
        sth_belge_tarih,
        sth_belge_no,
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_nakliyedeposu,
        sth_giris_depo_no,
        sth_miktar,
        sth_tutar
    )
    WHERE sth_evraktip = 17
      AND sth_tip = 2
      AND sth_cins = 6
      AND sth_normal_iade = 1
      AND sth_eticaret_kanal_kodu IS NOT NULL
      AND sth_tarih IS NOT NULL
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI') AND name = N'IX_FR_STH_DepoSevk_Trace')
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_DepoSevk_Trace
    ON dbo.STOK_HAREKETLERI
    (
        sth_eticaret_kanal_kodu,
        sth_cikis_depo_no,
        sth_tarih,
        sth_satirno
    )
    INCLUDE
    (
        sth_belge_tarih,
        sth_belge_no,
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_nakliyedeposu,
        sth_giris_depo_no,
        sth_miktar,
        sth_tutar,
        sth_stok_kod,
        sth_birim_pntr,
        sth_aciklama,
        sth_parti_kodu,
        sth_lot_no,
        sth_proje_kodu,
        sth_cari_srm_merkezi,
        sth_stok_srm_merkezi
    )
    WHERE sth_evraktip = 17
      AND sth_tip = 2
      AND sth_cins = 6
      AND sth_normal_iade = 0
      AND sth_eticaret_kanal_kodu IS NOT NULL
      AND sth_tarih IS NOT NULL
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI') AND name = N'IX_FR_STH_DepoSevk_Duplicate')
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_DepoSevk_Duplicate
    ON dbo.STOK_HAREKETLERI
    (
        sth_cikis_depo_no,
        sth_nakliyedeposu,
        sth_giris_depo_no,
        sth_tarih,
        sth_belge_tarih,
        sth_evrakno_seri,
        sth_create_date,
        sth_evrakno_sira,
        sth_satirno
    )
    INCLUDE
    (
        sth_belge_no,
        sth_miktar,
        sth_tutar,
        sth_stok_kod,
        sth_birim_pntr,
        sth_aciklama,
        sth_parti_kodu,
        sth_lot_no,
        sth_proje_kodu,
        sth_cari_srm_merkezi,
        sth_stok_srm_merkezi,
        sth_eticaret_kanal_kodu
    )
    WHERE sth_evraktip = 17
      AND sth_tip = 2
      AND sth_cins = 6
      AND sth_normal_iade = 0
      AND sth_create_date IS NOT NULL
      AND sth_evrakno_sira IS NOT NULL
      AND sth_satirno IS NOT NULL
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

/* Depolar arasi siparis liste ve safe-create read-back sorgulari. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.DEPOLAR_ARASI_SIPARISLER') AND name = N'IX_FR_DAS_GirisTarih_Liste')
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_DAS_GirisTarih_Liste
    ON dbo.DEPOLAR_ARASI_SIPARISLER (ssip_girdepo, ssip_tarih)
    INCLUDE
    (
        ssip_iptal,
        ssip_special1,
        ssip_create_date,
        ssip_lastup_date,
        ssip_teslim_tarih,
        ssip_evrakno_seri,
        ssip_evrakno_sira,
        ssip_belgeno,
        ssip_miktar,
        ssip_tutar,
        ssip_cikdepo
    )
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.DEPOLAR_ARASI_SIPARISLER') AND name = N'IX_FR_DAS_CikisTarih_Liste')
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_DAS_CikisTarih_Liste
    ON dbo.DEPOLAR_ARASI_SIPARISLER (ssip_cikdepo, ssip_tarih)
    INCLUDE
    (
        ssip_iptal,
        ssip_special1,
        ssip_create_date,
        ssip_lastup_date,
        ssip_teslim_tarih,
        ssip_evrakno_seri,
        ssip_evrakno_sira,
        ssip_belgeno,
        ssip_miktar,
        ssip_tutar,
        ssip_girdepo
    )
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.DEPOLAR_ARASI_SIPARISLER') AND name = N'IX_Furpa_SafeCreateTrace_WarehouseOrder')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Furpa_SafeCreateTrace_WarehouseOrder
    ON dbo.DEPOLAR_ARASI_SIPARISLER
    (
        ssip_special1,
        ssip_special2,
        ssip_special3,
        ssip_girdepo,
        ssip_cikdepo
    )
    INCLUDE (ssip_evrakno_seri, ssip_evrakno_sira, ssip_tarih, ssip_teslim_tarih)
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SIPARISLER') AND name = N'IX_Furpa_SafeCreateTrace_CompanyOrder')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Furpa_SafeCreateTrace_CompanyOrder
    ON dbo.SIPARISLER
    (
        sip_tip,
        sip_cins,
        sip_eticaret_kanal_kodu,
        sip_depono,
        sip_musteri_kod
    )
    INCLUDE (sip_evrakno_seri, sip_evrakno_sira, sip_tarih)
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

/* AXATA G01 siparis GUID read-back sorgusu. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI') AND name = N'IX_FR_STH_AXATA_G01_SipUid')
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_AXATA_G01_SipUid
    ON dbo.STOK_HAREKETLERI (sth_sip_uid)
    INCLUDE (sth_iptal)
    WHERE sth_evraktip = 13
      AND sth_tip = 0
      AND sth_cins = 0
      AND sth_sip_uid IS NOT NULL
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

/* AXATA siparis baglantisi ve etiket fiyat degisikligi sorgulari. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI_EK') AND name = N'IX_FR_STHEK_SubeSipUid')
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STHEK_SubeSipUid
    ON dbo.STOK_HAREKETLERI_EK (sth_subesip_uid)
    INCLUDE (sthek_related_uid)
    WHERE sth_subesip_uid IS NOT NULL
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.STOK_FIYAT_DEGISIKLIKLERI') AND name = N'IX_FR_FID_EtiketFiyatDegisikligi')
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_FID_EtiketFiyatDegisikligi
    ON dbo.STOK_FIYAT_DEGISIKLIKLERI (fid_depo_no, fid_lastup_date DESC, fid_stok_kod)
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

/* Furpa kasa icmal ve yardimci hareket sorgulari. */
USE [Furpa];
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Summaries') AND name = N'IX_FR_Summaries_DateBranch')
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Summaries') AND name = N'IX_FR_Summaries_Document')
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BanknoteMovements') AND name = N'IX_FR_BanknoteMovements_Document')
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_BanknoteMovements_Document
    ON dbo.BanknoteMovements (BranchNo, DocumentSerie, DocumentOrderNo)
    INCLUDE (BanknoteTypeID, Quantity, Total)
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.GiftCheckMovements') AND name = N'IX_FR_GiftCheckMovements_Document')
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_GiftCheckMovements_Document
    ON dbo.GiftCheckMovements (BranchNo, DocumentSerie, DocumentOrderNo)
    INCLUDE (GiftCheckTypeID, Quantity, Total)
    WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = OFF);
END;
GO

IF OBJECT_ID(N'dbo.CashRegisterDetails', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.CashRegisterDetails', N'PaymentTypeNo') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CashRegisterDetails') AND name = N'IX_CashRegisterDetails_CashNo_PaymentTypeNo')
BEGIN
    CREATE NONCLUSTERED INDEX IX_CashRegisterDetails_CashNo_PaymentTypeNo
    ON dbo.CashRegisterDetails (CashNo, PaymentTypeNo);
END;
GO

/* FurpaMerkezDb e-irsaliye worker kuyrugu. */
USE [FurpaMerkezDb];
GO

IF OBJECT_ID(N'dbo.edespatch_submissions', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.edespatch_submissions') AND name = N'IX_edespatch_submissions_document_key')
BEGIN
    CREATE UNIQUE INDEX IX_edespatch_submissions_document_key
    ON dbo.edespatch_submissions (document_key);
END;
GO

IF OBJECT_ID(N'dbo.edespatch_submissions', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.edespatch_submissions') AND name = N'IX_edespatch_submissions_document_no')
BEGIN
    CREATE UNIQUE INDEX IX_edespatch_submissions_document_no
    ON dbo.edespatch_submissions (document_no);
END;
GO

IF OBJECT_ID(N'dbo.edespatch_submissions', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.edespatch_submissions') AND name = N'IX_edespatch_submissions_status_next_attempt_at_utc')
BEGIN
    CREATE NONCLUSTERED INDEX IX_edespatch_submissions_status_next_attempt_at_utc
    ON dbo.edespatch_submissions (status, next_attempt_at_utc);
END;
GO

IF OBJECT_ID(N'dbo.edespatch_submissions', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.edespatch_submissions') AND name = N'IX_edespatch_submissions_uuid')
BEGIN
    CREATE UNIQUE INDEX IX_edespatch_submissions_uuid
    ON dbo.edespatch_submissions (uuid);
END;
GO

/* FurpaMerkezDb bolumunun kisa dogrulamasi. */
SELECT
    DB_NAME() AS database_name,
    OBJECT_SCHEMA_NAME(i.object_id) + N'.' + OBJECT_NAME(i.object_id) AS table_name,
    i.name,
    i.is_disabled,
    i.has_filter
FROM sys.indexes AS i
WHERE i.name LIKE N'IX[_]FR[_]%'
   OR i.name LIKE N'IX[_]Furpa[_]SafeCreateTrace[_]%'
   OR i.name LIKE N'IX[_]edespatch[_]%'
   OR i.name = N'IX_CashRegisterDetails_CashNo_PaymentTypeNo'
ORDER BY table_name, i.name;
GO

USE [MikroDesktop_FURPA_2024];
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_FirmaMalKabul_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_FirmaMalKabul_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_giris_depo_no,
        sth_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_satirno,
        sth_belge_no,
        sth_belge_tarih,
        sth_create_date,
        sth_cari_kodu,
        sth_cikis_depo_no,
        sth_evraktip,
        sth_tip,
        sth_normal_iade,
        sth_miktar,
        sth_tutar
    )
    WHERE
        sth_evraktip = 13
        AND sth_tip = 0
        AND sth_normal_iade = 0
        AND sth_tarih IS NOT NULL
    WITH
    (
        ONLINE = ON,
        MAXDOP = 2,
        SORT_IN_TEMPDB = OFF
    );
END;
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes AS i
    WHERE i.object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND i.name = N'IX_FR_STH_FirmaMalKabul_Liste'
      AND NOT EXISTS (
          SELECT 1
          FROM sys.index_columns AS ic
          INNER JOIN sys.columns AS c
              ON c.object_id = ic.object_id
             AND c.column_id = ic.column_id
          WHERE ic.object_id = i.object_id
            AND ic.index_id = i.index_id
            AND c.name = N'sth_satirno'
      )
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_FirmaMalKabul_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_giris_depo_no,
        sth_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_satirno,
        sth_belge_no,
        sth_belge_tarih,
        sth_create_date,
        sth_cari_kodu,
        sth_cikis_depo_no,
        sth_evraktip,
        sth_tip,
        sth_normal_iade,
        sth_miktar,
        sth_tutar
    )
    WHERE
        sth_evraktip = 13
        AND sth_tip = 0
        AND sth_normal_iade = 0
        AND sth_tarih IS NOT NULL
    WITH
    (
        DROP_EXISTING = ON,
        ONLINE = ON,
        MAXDOP = 1,
        SORT_IN_TEMPDB = OFF
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_FirmaCikis_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_FirmaCikis_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_cikis_depo_no,
        sth_normal_iade,
        sth_belge_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_satirno,
        sth_belge_no,
        sth_tarih,
        sth_create_date,
        sth_cari_kodu,
        sth_giris_depo_no,
        sth_evraktip,
        sth_tip,
        sth_miktar,
        sth_tutar
    )
    WHERE
        sth_evraktip = 1
        AND sth_tip = 1
        AND sth_belge_tarih IS NOT NULL
    WITH
    (
        ONLINE = ON,
        MAXDOP = 2,
        SORT_IN_TEMPDB = OFF
    );
END;
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes AS i
    WHERE i.object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND i.name = N'IX_FR_STH_FirmaCikis_Liste'
      AND NOT EXISTS (
          SELECT 1
          FROM sys.index_columns AS ic
          INNER JOIN sys.columns AS c
              ON c.object_id = ic.object_id
             AND c.column_id = ic.column_id
          WHERE ic.object_id = i.object_id
            AND ic.index_id = i.index_id
            AND c.name = N'sth_satirno'
      )
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_FirmaCikis_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_cikis_depo_no,
        sth_normal_iade,
        sth_belge_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_satirno,
        sth_belge_no,
        sth_tarih,
        sth_create_date,
        sth_cari_kodu,
        sth_giris_depo_no,
        sth_evraktip,
        sth_tip,
        sth_miktar,
        sth_tutar
    )
    WHERE
        sth_evraktip = 1
        AND sth_tip = 1
        AND sth_belge_tarih IS NOT NULL
    WITH
    (
        DROP_EXISTING = ON,
        ONLINE = ON,
        MAXDOP = 1,
        SORT_IN_TEMPDB = OFF
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_DepoSevkCikis_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_DepoSevkCikis_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_cikis_depo_no,
        sth_normal_iade,
        sth_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_belge_no,
        sth_belge_tarih,
        sth_giris_depo_no,
        sth_nakliyedeposu,
        sth_nakliyedurumu,
        sth_HareketGrupKodu1,
        sth_HareketGrupKodu3,
        sth_ismerkezi_kodu,
        sth_aciklama,
        sth_miktar
    )
    WHERE
        sth_evraktip = 17
        AND sth_tarih IS NOT NULL
    WITH
    (
        ONLINE = ON,
        MAXDOP = 2,
        SORT_IN_TEMPDB = OFF
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_DepoSevkNakliye_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_DepoSevkNakliye_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_nakliyedeposu,
        sth_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_belge_no,
        sth_belge_tarih,
        sth_cikis_depo_no,
        sth_giris_depo_no,
        sth_nakliyedurumu,
        sth_normal_iade,
        sth_HareketGrupKodu1,
        sth_HareketGrupKodu3,
        sth_ismerkezi_kodu,
        sth_aciklama,
        sth_miktar
    )
    WHERE
        sth_evraktip = 17
        AND sth_tarih IS NOT NULL
    WITH
    (
        ONLINE = ON,
        MAXDOP = 2,
        SORT_IN_TEMPDB = OFF
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_DepoSevkGiris_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_DepoSevkGiris_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_giris_depo_no,
        sth_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_belge_no,
        sth_belge_tarih,
        sth_cikis_depo_no,
        sth_nakliyedeposu,
        sth_nakliyedurumu,
        sth_normal_iade,
        sth_HareketGrupKodu1,
        sth_HareketGrupKodu3,
        sth_ismerkezi_kodu,
        sth_aciklama,
        sth_miktar
    )
    WHERE
        sth_evraktip = 17
        AND sth_tarih IS NOT NULL
    WITH
    (
        ONLINE = ON,
        MAXDOP = 2,
        SORT_IN_TEMPDB = OFF
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_StokFisi_Trace'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_StokFisi_Trace
    ON dbo.STOK_HAREKETLERI
    (
        sth_eticaret_kanal_kodu,
        sth_cikis_depo_no,
        sth_cins,
        sth_tarih,
        sth_satirno
    )
    INCLUDE
    (
        sth_belge_tarih,
        sth_belge_no,
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_HareketGrupKodu1,
        sth_HareketGrupKodu2,
        sth_miktar,
        sth_tutar
    )
    WHERE
        sth_evraktip = 0
        AND sth_tip = 1
        AND sth_normal_iade = 0
        AND sth_eticaret_kanal_kodu IS NOT NULL
        AND sth_tarih IS NOT NULL
    WITH
    (
        ONLINE = ON,
        MAXDOP = 2,
        SORT_IN_TEMPDB = OFF
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_StokFisi_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_StokFisi_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_cikis_depo_no,
        sth_cins,
        sth_belge_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_belge_no,
        sth_tarih,
        sth_create_date,
        sth_HareketGrupKodu1,
        sth_HareketGrupKodu2,
        sth_isemri_gider_kodu,
        sth_evraktip,
        sth_tip,
        sth_aciklama,
        sth_miktar,
        sth_tutar
    )
    WHERE
        sth_evraktip = 0
        AND sth_tip = 1
        AND sth_normal_iade = 0
        AND sth_belge_tarih IS NOT NULL
    WITH
    (
        ONLINE = ON,
        MAXDOP = 2,
        SORT_IN_TEMPDB = OFF
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_Virman_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_Virman_Liste
    ON dbo.STOK_HAREKETLERI
    (
        sth_cikis_depo_no,
        sth_belge_tarih
    )
    INCLUDE
    (
        sth_evrakno_seri,
        sth_evrakno_sira,
        sth_belge_no,
        sth_tarih,
        sth_create_date,
        sth_evraktip,
        sth_cins,
        sth_tip,
        sth_aciklama,
        sth_miktar,
        sth_tutar
    )
    WHERE
        sth_evraktip = 6
        AND sth_cins = 3
        AND sth_normal_iade = 0
        AND sth_belge_tarih IS NOT NULL
    WITH
    (
        ONLINE = ON,
        MAXDOP = 2,
        SORT_IN_TEMPDB = OFF
    );
END;
GO

/* Tum katalog icin tek sonuc listesi. */
SELECT
    q.database_name,
    q.table_name,
    q.index_name,
    q.is_disabled,
    q.has_filter
FROM
(
    SELECT
        N'MikroDesktop_FURPA_2024' AS database_name,
        OBJECT_SCHEMA_NAME(i.object_id, DB_ID(N'MikroDesktop_FURPA_2024'))
            + N'.'
            + OBJECT_NAME(i.object_id, DB_ID(N'MikroDesktop_FURPA_2024')) AS table_name,
        i.name AS index_name,
        i.is_disabled,
        i.has_filter
    FROM MikroDesktop_FURPA_2024.sys.indexes AS i
    WHERE i.name LIKE N'IX[_]FR[_]%'
       OR i.name LIKE N'IX[_]Furpa[_]SafeCreateTrace[_]%'

    UNION ALL

    SELECT
        N'Furpa',
        OBJECT_SCHEMA_NAME(i.object_id, DB_ID(N'Furpa'))
            + N'.'
            + OBJECT_NAME(i.object_id, DB_ID(N'Furpa')),
        i.name,
        i.is_disabled,
        i.has_filter
    FROM Furpa.sys.indexes AS i
    WHERE i.name LIKE N'IX[_]FR[_]%'
       OR i.name = N'IX_CashRegisterDetails_CashNo_PaymentTypeNo'

    UNION ALL

    SELECT
        N'FurpaMerkezDb',
        OBJECT_SCHEMA_NAME(i.object_id, DB_ID(N'FurpaMerkezDb'))
            + N'.'
            + OBJECT_NAME(i.object_id, DB_ID(N'FurpaMerkezDb')),
        i.name,
        i.is_disabled,
        i.has_filter
    FROM FurpaMerkezDb.sys.indexes AS i
    WHERE i.name LIKE N'IX[_]edespatch[_]%'
) AS q
ORDER BY q.database_name, q.table_name, q.index_name;
GO
