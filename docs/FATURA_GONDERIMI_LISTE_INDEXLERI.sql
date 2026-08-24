/*
    Fatura Gonderimi liste ekranlari icin onerilen index scripti.

    Amac:
    - GET /api/fatura-islemleri/fatura-gonderimi liste sorgusunu hizlandirmak.
    - CARI_HESAP_HAREKETLERI tarih/seri filtrelerini daraltmak.
    - STOK_HAREKETLERI.sth_fat_uid uzerinden irsaliye/depo ve detay kalemi okumalarini hizlandirmak.
    - CARI_HESAP_ADRESLERI adres no 1 join maliyetini azaltmak.

    Notlar:
    - Bu script Mikro DB uzerinde manuel/kontrollu calistirilmalidir.
    - Canlida mesai disi uygulanmasi onerilir.
    - Indexler unique degildir; duplicate key riski olusturmaz.
    - CREATE INDEX sirasinda tablo buyukse gecici IO/CPU yuku olusturur.
*/

IF OBJECT_ID(N'dbo.CARI_HESAP_HAREKETLERI', N'U') IS NULL
BEGIN
    THROW 51000, 'dbo.CARI_HESAP_HAREKETLERI tablosu bulunamadi. Script Mikro DB uzerinde calistirilmali.', 1;
END;
GO

IF OBJECT_ID(N'dbo.STOK_HAREKETLERI', N'U') IS NULL
BEGIN
    THROW 51000, 'dbo.STOK_HAREKETLERI tablosu bulunamadi. Script Mikro DB uzerinde calistirilmali.', 1;
END;
GO

IF OBJECT_ID(N'dbo.CARI_HESAP_ADRESLERI', N'U') IS NULL
BEGIN
    THROW 51000, 'dbo.CARI_HESAP_ADRESLERI tablosu bulunamadi. Script Mikro DB uzerinde calistirilmali.', 1;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CARI_HESAP_HAREKETLERI')
      AND name = N'IX_FR_CHH_FaturaGonderimi_Liste'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_CHH_FaturaGonderimi_Liste
    ON dbo.CARI_HESAP_HAREKETLERI
    (
        cha_tip,
        cha_belge_tarih,
        cha_tarihi,
        cha_evrakno_seri,
        cha_evrakno_sira
    )
    INCLUDE
    (
        cha_Guid,
        cha_iptal,
        cha_uuid,
        cha_evrak_tip,
        cha_normal_Iade,
        cha_aciklama,
        cha_belge_no,
        cha_aratoplam,
        cha_vergi1,
        cha_vergi2,
        cha_vergi3,
        cha_vergi4,
        cha_vergi5,
        cha_vergi6,
        cha_vergi7,
        cha_vergi8,
        cha_vergi9,
        cha_vergi10,
        cha_ebelge_turu,
        cha_ciro_cari_kodu,
        cha_cinsi,
        cha_miktari,
        cha_kasa_hizkod,
        cha_vergipntr
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
      AND name = N'IX_FR_STH_FaturaGonderimi_FatUid'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_STH_FaturaGonderimi_FatUid
    ON dbo.STOK_HAREKETLERI
    (
        sth_fat_uid,
        sth_satirno
    )
    INCLUDE
    (
        sth_Guid,
        sth_iptal,
        sth_create_date,
        sth_belge_no,
        sth_belge_tarih,
        sth_cikis_depo_no,
        sth_stok_kod,
        sth_miktar,
        sth_tutar,
        sth_iskonto1,
        sth_iskonto2,
        sth_iskonto3,
        sth_iskonto4,
        sth_iskonto5,
        sth_iskonto6,
        sth_vergi,
        sth_vergi_pntr
    )
    WHERE
        sth_fat_uid IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CARI_HESAP_ADRESLERI')
      AND name = N'IX_FR_CHA_FaturaGonderimi_Adres1'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_CHA_FaturaGonderimi_Adres1
    ON dbo.CARI_HESAP_ADRESLERI
    (
        adr_cari_kod,
        adr_adres_no
    )
    INCLUDE
    (
        adr_cadde,
        adr_sokak,
        adr_ilce,
        adr_il,
        adr_efatura_alias,
        adr_posta_kodu
    )
    WHERE
        adr_adres_no = 1;
END;
GO
