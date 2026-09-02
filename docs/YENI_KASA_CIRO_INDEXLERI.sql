/*
    Yeni kasa / Shopigo ciro sorgulari icin onerilen index scripti.

    Amac:
    - GET /api/kasa-islemleri/kasa-cirolari yeni/toplam/ozet sorgularini hizlandirmak.
    - GET /api/kasa-islemleri/yeni-kasa-analizleri ekranlarinda satis, urun satiri ve odeme okumalarini daraltmak.
    - Duplicate odeme temizleme icin sale_uuid bazli odeme okumasini hizlandirmak.

    Notlar:
    - Bu script ShopigoCiroConnection ile baglanilan SHOPIGO DB uzerinde manuel/kontrollu calistirilmalidir.
    - Canlida mesai disi uygulanmasi onerilir.
    - Indexler unique degildir; duplicate key riski olusturmaz.
    - CREATE INDEX sirasinda tablo buyukse gecici IO/CPU yuku olusturur.
*/

IF OBJECT_ID(N'dbo.received_sales', N'U') IS NULL
BEGIN
    THROW 51000, 'dbo.received_sales tablosu bulunamadi. Script SHOPIGO DB uzerinde calistirilmali.', 1;
END;
GO

IF OBJECT_ID(N'dbo.payments', N'U') IS NULL
BEGIN
    THROW 51000, 'dbo.payments tablosu bulunamadi. Script SHOPIGO DB uzerinde calistirilmali.', 1;
END;
GO

IF OBJECT_ID(N'dbo.sale_items', N'U') IS NULL
BEGIN
    THROW 51000, 'dbo.sale_items tablosu bulunamadi. Script SHOPIGO DB uzerinde calistirilmali.', 1;
END;
GO

IF OBJECT_ID(N'dbo.payment_methods', N'U') IS NULL
BEGIN
    THROW 51000, 'dbo.payment_methods tablosu bulunamadi. Script SHOPIGO DB uzerinde calistirilmali.', 1;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.received_sales')
      AND name = N'IX_FR_RS_Ciro_TarihSubeKasa'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_RS_Ciro_TarihSubeKasa
    ON dbo.received_sales
    (
        received_at,
        subeno,
        kasano,
        initiated_by
    )
    INCLUDE
    (
        id,
        uuid,
        receipt_number,
        total_price,
        remaining_amount,
        market_id
    )
    WHERE
        deleted_at IS NULL
        AND status = N'4'
        AND received_at IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.received_sales')
      AND name = N'IX_FR_RS_Ciro_SubeTarih'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_RS_Ciro_SubeTarih
    ON dbo.received_sales
    (
        subeno,
        received_at,
        kasano,
        initiated_by
    )
    INCLUDE
    (
        id,
        uuid,
        receipt_number,
        total_price,
        remaining_amount,
        market_id
    )
    WHERE
        deleted_at IS NULL
        AND status = N'4'
        AND received_at IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.payments')
      AND name = N'IX_FR_Payments_Ciro_SaleUuid'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_Payments_Ciro_SaleUuid
    ON dbo.payments
    (
        sale_uuid,
        payment_method,
        id
    )
    INCLUDE
    (
        amount
    )
    WHERE
        deleted_at IS NULL
        AND refunded = 0
        AND sale_uuid IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.sale_items')
      AND name = N'IX_FR_SaleItems_Ciro_SaleUuid'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_SaleItems_Ciro_SaleUuid
    ON dbo.sale_items
    (
        sale_uuid,
        id
    )
    INCLUDE
    (
        quantity,
        total_price
    )
    WHERE
        deleted_at IS NULL
        AND refunded = 0
        AND sale_uuid IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.payment_methods')
      AND name = N'IX_FR_PaymentMethods_Ciro_Lookup'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_FR_PaymentMethods_Ciro_Lookup
    ON dbo.payment_methods
    (
        status,
        pavo_mediator,
        id
    )
    INCLUDE
    (
        name,
        pavo_type
    );
END;
GO
