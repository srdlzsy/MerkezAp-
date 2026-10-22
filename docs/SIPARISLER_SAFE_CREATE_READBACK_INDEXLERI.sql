/*
    Verilen depo/firma siparisi safe-create trace readback sorgulari icin
    hedefli nonclustered indexler.

    Canli envanterde hedef key'leri karsilayan index bulunmadigi icin
    hazirlanmistir. ONLINE = ON yazimlari build boyunca acik tutar; yine de
    disk/tempdb/log I/O tuketir ve baslangic/bitis asamalarinda kisa metadata
    kilitleri alir. Dusuk trafik saatinde ve depolama alani dogrulanarak calistirin.
*/

SET DEADLOCK_PRIORITY LOW;
SET LOCK_TIMEOUT 15000;
SET NOCOUNT ON;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.DEPOLAR_ARASI_SIPARISLER')
      AND name = N'IX_Furpa_SafeCreateTrace_WarehouseOrder'
)
BEGIN
    RAISERROR(N'Creating warehouse safe-create trace index...', 10, 1) WITH NOWAIT;
    CREATE NONCLUSTERED INDEX IX_Furpa_SafeCreateTrace_WarehouseOrder
        ON dbo.DEPOLAR_ARASI_SIPARISLER
        (
            ssip_special1,
            ssip_special2,
            ssip_special3,
            ssip_girdepo,
            ssip_cikdepo
        )
        INCLUDE
        (
            ssip_evrakno_seri,
            ssip_evrakno_sira,
            ssip_tarih,
            ssip_teslim_tarih
        )
        WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = ON);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SIPARISLER')
      AND name = N'IX_Furpa_SafeCreateTrace_CompanyOrder'
)
BEGIN
    RAISERROR(N'Creating company safe-create trace index...', 10, 1) WITH NOWAIT;
    CREATE NONCLUSTERED INDEX IX_Furpa_SafeCreateTrace_CompanyOrder
        ON dbo.SIPARISLER
        (
            sip_tip,
            sip_cins,
            sip_eticaret_kanal_kodu,
            sip_depono,
            sip_musteri_kod
        )
        INCLUDE
        (
            sip_evrakno_seri,
            sip_evrakno_sira,
            sip_tarih
        )
        WITH (ONLINE = ON, MAXDOP = 2, SORT_IN_TEMPDB = ON);
END;
