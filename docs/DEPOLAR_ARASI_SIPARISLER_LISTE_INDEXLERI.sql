/*
    DEPOLAR_ARASI_SIPARISLER liste sorgulari icin iki yonlu indexler.

    Desteklenen sorgular:
    - Verilen depo siparisleri: ssip_girdepo + ssip_tarih
    - Alinan depo siparisleri: ssip_cikdepo + ssip_tarih

    Canli DMV bulgusu (2026-10-27):
    - ssip_girdepo + ssip_tarih icin 3.004 seek ve yaklasik yuzde 85
      iyilestirme sinyali bulunuyor.
    - Mevcut safe-create trace indexi ssip_special alanlariyla basladigi icin
      liste sorgularinin depo + tarih erisimini karsilamiyor.

    Uygulama:
    - Mikro DB uzerinde dusuk trafik/bakim penceresinde calistirin.
    - ONLINE build yine de CPU, disk ve transaction log kullanir.
    - Canli veri/log dosyalarinin autogrowth adimi 64 MB oldugu icin build
      oncesinde dosyalari yeterli miktarda onceden buyutmek daha sagliklidir.
*/

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

IF OBJECT_ID(N'dbo.DEPOLAR_ARASI_SIPARISLER', N'U') IS NULL
BEGIN
    THROW 51000, 'dbo.DEPOLAR_ARASI_SIPARISLER tablosu bulunamadi. Script Mikro DB uzerinde calistirilmali.', 1;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.DEPOLAR_ARASI_SIPARISLER')
      AND name = N'IX_FR_DAS_GirisTarih_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_DAS_GirisTarih_Liste
    ON dbo.DEPOLAR_ARASI_SIPARISLER
    (
        ssip_girdepo,
        ssip_tarih
    )
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
    WHERE object_id = OBJECT_ID(N'dbo.DEPOLAR_ARASI_SIPARISLER')
      AND name = N'IX_FR_DAS_CikisTarih_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_DAS_CikisTarih_Liste
    ON dbo.DEPOLAR_ARASI_SIPARISLER
    (
        ssip_cikdepo,
        ssip_tarih
    )
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
    WITH
    (
        ONLINE = ON,
        MAXDOP = 2,
        SORT_IN_TEMPDB = OFF
    );
END;
GO

SELECT
    i.name,
    i.is_disabled,
    i.has_filter,
    i.filter_definition
FROM sys.indexes AS i
WHERE i.object_id = OBJECT_ID(N'dbo.DEPOLAR_ARASI_SIPARISLER')
  AND i.name IN
  (
      N'IX_FR_DAS_GirisTarih_Liste',
      N'IX_FR_DAS_CikisTarih_Liste'
  )
ORDER BY i.name;
GO
