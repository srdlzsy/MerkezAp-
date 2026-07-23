# Firma Onerilen Siparis Sorgusu

Bu dokuman, Mikro canli veri kontrolunden sonra firma/tedarikci icin onerilen
siparis sorgusu mantigini anlatir.

## Canli Veri Kontrol Ozeti

Kontrol edilen depo: `110 - KESTEL 1 YENI MAHALLE`

Canli kontrolde gorulen onemli noktalar:

- `STOK_DEPO_DETAYLARI` icinde 110 depo icin `33006` stok/depo detay satiri var.
- Bu satirlarin `1418` tanesinde depo bazli `sdp_sat_cari_kod` dolu.
- `STOKLAR` icinde aktif stoklarda `1749` urunde global `sto_sat_cari_kod` dolu.
- `SATINALMA_SARTLARI` tablosu var ve `424075` satir iceriyor.
- 110 depo icin acik verilen firma siparislerinde yaklasik `165523` acik miktar var.
- Statik min/sip/max stok seviyeleri depo ve stok kartlarinda dolu olmadigi icin
  firma onerisi de son satis/tuketim ortalamasina gore hesaplanmalidir.

## Temel Mantik

- `@WarehouseNo`: Siparis isteyen depo/magaza.
- `@SupplierCode`: Zorunlu firma/tedarikci kodu.
- Firma siparisleri firma bazli oldugu icin firma secilmeden onerilen siparis listesi uretilmez.
- Secilen firmaya ait urunler listelenir.
- Firma urun eslesmesi once depo bazli `STOK_DEPO_DETAYLARI.sdp_sat_cari_kod`,
  sonra global `STOKLAR.sto_sat_cari_kod` uzerinden okunur.
- Secili firma varsa `SATINALMA_SARTLARI.sas_stok_kod + sas_cari_kod` eslesmesi
  de kabul edilir.
- Acik verilen firma siparisleri, teslim miktari takibi guvenilir tedarikciler icin
  ihtiyactan dusulur.
- Firma dis kaynak oldugu icin depo sorgusundaki gibi kaynak stok limiti yoktur.

Kisa formul:

```text
onerilen stok seviyesi = gunluk ortalama satis * onerilen gun

ihtiyac = onerilen stok seviyesi
        - depodaki mevcut stok
        - ayar izin veriyorsa firmaya acik verilen siparis miktari

onerilen firma siparisi = ihtiyac
```

Satinalma sartinda `sas_asgari_miktar` doluysa ve ihtiyac bu miktardan dusukse,
onerilen miktar asgari miktara tamamlanir.

## Optimize Ana Sorgu

```sql
DECLARE @WarehouseNo int = 110;              -- siparis isteyen magaza/depo
DECLARE @SupplierCode nvarchar(25) = N'32000999'; -- zorunlu firma/tedarikci
DECLARE @LookbackDays int = 43;              -- Mikro prosedurleriyle uyumlu donem
DECLARE @FallbackRecommendedDay int = 7;     -- kartta onerilen gun yoksa varsayilan
DECLARE @DeductOpenCompanyOrders bit = 0;    -- ayardan gelir

;WITH StockBase AS (
    SELECT
        stock.sto_kod,
        stock.sto_isim,
        stock.sto_model_kodu,
        stock.sto_birim2_katsayi,
        stock.sto_min_stok_belirleme_gun,
        stock.sto_sip_stok_belirleme_gun,
        stock.sto_max_stok_belirleme_gun,
        COALESCE(
            NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''),
            NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), '')
        ) AS DefaultSupplierCode,
        NULLIF(@SupplierCode, '') AS EffectiveSupplierCode
    FROM dbo.STOKLAR AS stock
    LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail
        ON detail.sdp_depo_no = @WarehouseNo
       AND detail.sdp_depo_kod = stock.sto_kod
    WHERE ISNULL(stock.sto_iptal, 0) = 0
      AND ISNULL(COALESCE(detail.sdp_Pasif_fl, stock.sto_pasif_fl), 0) = 0
      AND ISNULL(COALESCE(detail.sdp_sipdursun, stock.sto_siparis_dursun), 0) = 0
      AND ISNULL(COALESCE(detail.sdp_malkabuldursun, stock.sto_malkabul_dursun), 0) = 0
      AND stock.sto_isim NOT LIKE 'DLS%'
      AND stock.sto_isim NOT LIKE 'SRF%'
      AND stock.sto_kod NOT IN ('011141','013199','000154','000754','000051','089020','000219')
      AND (
          COALESCE(
              NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''),
              NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), '')
          ) = @SupplierCode
          OR EXISTS (
              SELECT 1
              FROM dbo.SATINALMA_SARTLARI AS term
              WHERE term.sas_stok_kod = stock.sto_kod
                AND term.sas_cari_kod = @SupplierCode
                AND ISNULL(term.sas_iptal, 0) = 0
                AND (term.sas_depo_no IN (0, @WarehouseNo) OR term.sas_depo_no IS NULL)
                AND (term.sas_basla_tarih IS NULL OR term.sas_basla_tarih <= GETDATE())
                AND (term.sas_bitis_tarih IS NULL OR term.sas_bitis_tarih >= CONVERT(date, GETDATE()))
          )
      )
),
Consumption AS (
    SELECT
        movement.sth_stok_kod AS StockCode,
        SUM(ISNULL(movement.sth_miktar, 0)) AS SalesQuantity
    FROM dbo.STOK_HAREKETLERI AS movement
    INNER JOIN StockBase AS stock
        ON stock.sto_kod = movement.sth_stok_kod
    WHERE movement.sth_tarih >= DATEADD(DAY, -@LookbackDays, CONVERT(date, GETDATE()))
      AND movement.sth_tarih < DATEADD(DAY, 1, CONVERT(date, GETDATE()))
      AND movement.sth_cikis_depo_no = @WarehouseNo
      AND ISNULL(movement.sth_iptal, 0) = 0
      AND movement.sth_tip = 1
      AND movement.sth_cins = 1
      AND movement.sth_normal_iade = 0
      AND movement.sth_evraktip IN (4, 1)
    GROUP BY movement.sth_stok_kod
),
OpenCompanyOrders AS (
    SELECT
        orders.sip_stok_kod AS StockCode,
        orders.sip_musteri_kod AS SupplierCode,
        SUM(ISNULL(orders.sip_miktar, 0) - ISNULL(orders.sip_teslim_miktar, 0)) AS OpenOrderQuantity
    FROM dbo.SIPARISLER AS orders
    INNER JOIN StockBase AS stock
        ON stock.sto_kod = orders.sip_stok_kod
       AND stock.EffectiveSupplierCode = orders.sip_musteri_kod
    WHERE @DeductOpenCompanyOrders = 1
      AND orders.sip_tip = 1
      AND orders.sip_cins = 0
      AND orders.sip_depono = @WarehouseNo
      AND ISNULL(orders.sip_iptal, 0) = 0
      AND ISNULL(orders.sip_kapat_fl, 0) = 0
      AND ISNULL(orders.sip_miktar, 0) > ISNULL(orders.sip_teslim_miktar, 0)
    GROUP BY orders.sip_stok_kod, orders.sip_musteri_kod
),
TargetStock AS (
    SELECT
        summary.sho_StokKodu AS StockCode,
        SUM(ISNULL(summary.sho_GirisNormal, 0) + ISNULL(summary.sho_CikisIade, 0)
          - ISNULL(summary.sho_CikisNormal, 0) - ISNULL(summary.sho_GirisIade, 0)) AS TargetOnHand
    FROM dbo.STOK_HAREKETLERI_OZET AS summary
    INNER JOIN StockBase AS stock
        ON stock.sto_kod = summary.sho_StokKodu
    WHERE summary.sho_Depo = @WarehouseNo
    GROUP BY summary.sho_StokKodu
),
Calculated AS (
    SELECT
        stock.sto_kod,
        stock.sto_isim,
        stock.sto_model_kodu,
        stock.DefaultSupplierCode,
        stock.EffectiveSupplierCode,
        supplier.cari_unvan1 AS SupplierName,
        barcode.bar_kodu,
        ISNULL(targetStock.TargetOnHand, 0) AS TargetOnHand,
        ISNULL(consumption.SalesQuantity, 0) AS SalesQuantity,
        ISNULL(openOrders.OpenOrderQuantity, 0) AS OpenOrderQuantity,
        stock.sto_birim2_katsayi,
        stock.sto_min_stok_belirleme_gun,
        ISNULL(NULLIF(stock.sto_sip_stok_belirleme_gun, 0), @FallbackRecommendedDay) AS RecommendedDay,
        stock.sto_max_stok_belirleme_gun,
        purchaseTerm.sas_brut_fiyat,
        purchaseTerm.sas_asgari_miktar,
        purchaseTerm.sas_teslim_sure
    FROM StockBase AS stock
    LEFT JOIN dbo.CARI_HESAPLAR AS supplier
        ON supplier.cari_kod = stock.EffectiveSupplierCode
    LEFT JOIN Consumption AS consumption
        ON consumption.StockCode = stock.sto_kod
    LEFT JOIN OpenCompanyOrders AS openOrders
        ON openOrders.StockCode = stock.sto_kod
       AND openOrders.SupplierCode = stock.EffectiveSupplierCode
    LEFT JOIN TargetStock AS targetStock
        ON targetStock.StockCode = stock.sto_kod
    OUTER APPLY (
        SELECT TOP 1 barcode.bar_kodu
        FROM dbo.BARKOD_TANIMLARI AS barcode
        WHERE barcode.bar_stokkodu = stock.sto_kod
          AND barcode.bar_birimpntr = 1
        ORDER BY ISNULL(barcode.bar_master, 0) DESC, barcode.bar_create_date DESC
    ) AS barcode
    OUTER APPLY (
        SELECT TOP 1
            term.sas_brut_fiyat,
            term.sas_asgari_miktar,
            term.sas_teslim_sure
        FROM dbo.SATINALMA_SARTLARI AS term
        WHERE term.sas_stok_kod = stock.sto_kod
          AND term.sas_cari_kod = stock.EffectiveSupplierCode
          AND ISNULL(term.sas_iptal, 0) = 0
          AND (term.sas_depo_no IN (0, @WarehouseNo) OR term.sas_depo_no IS NULL)
          AND (term.sas_basla_tarih IS NULL OR term.sas_basla_tarih <= GETDATE())
          AND (term.sas_bitis_tarih IS NULL OR term.sas_bitis_tarih >= CONVERT(date, GETDATE()))
        ORDER BY
            CASE WHEN term.sas_depo_no = @WarehouseNo THEN 0 ELSE 1 END,
            term.sas_belge_tarih DESC,
            term.sas_create_date DESC
    ) AS purchaseTerm
)
SELECT
    calc.EffectiveSupplierCode AS supplierCode,
    calc.SupplierName AS supplierName,
    calc.sto_kod AS stockCode,
    calc.sto_isim AS stockName,
    calc.sto_model_kodu AS modelCode,
    ISNULL(calc.bar_kodu, '') AS barcode,
    calc.TargetOnHand AS targetOnHand,
    calc.SalesQuantity AS salesQuantity,
    calc.OpenOrderQuantity AS openCompanyOrderQuantity,
    calc.sto_birim2_katsayi AS packageFactor,
    calc.sto_min_stok_belirleme_gun AS minDay,
    calc.RecommendedDay AS recommendedDay,
    calc.sto_max_stok_belirleme_gun AS maxDay,
    recommended.RecommendedStockQuantity AS recommendedStockQuantity,
    recommended.RecommendedStockQuantity - calc.TargetOnHand - calc.OpenOrderQuantity AS needQuantity,
    recommended.SuggestedOrderQuantity AS suggestedOrderQuantity,
    calc.sas_brut_fiyat AS purchasePrice,
    calc.sas_asgari_miktar AS minimumPurchaseQuantity,
    calc.sas_teslim_sure AS deliveryDay
FROM Calculated AS calc
CROSS APPLY (
    SELECT CEILING((calc.SalesQuantity / NULLIF(@LookbackDays, 0)) * calc.RecommendedDay) AS RecommendedStockQuantity
) AS targetQuantity
CROSS APPLY (
    SELECT
        CASE
            WHEN targetQuantity.RecommendedStockQuantity - calc.TargetOnHand - calc.OpenOrderQuantity <= 0 THEN 0
            WHEN ISNULL(calc.sas_asgari_miktar, 0) > targetQuantity.RecommendedStockQuantity - calc.TargetOnHand - calc.OpenOrderQuantity
                THEN calc.sas_asgari_miktar
            ELSE targetQuantity.RecommendedStockQuantity - calc.TargetOnHand - calc.OpenOrderQuantity
        END AS SuggestedOrderQuantity,
        targetQuantity.RecommendedStockQuantity
) AS recommended
WHERE calc.SalesQuantity > 0
  AND ISNULL(calc.bar_kodu, '') <> ''
  AND recommended.SuggestedOrderQuantity > 0
ORDER BY recommended.SuggestedOrderQuantity DESC, calc.SupplierName, calc.sto_isim;
```

## Kullanim Ornekleri

Tek firmaya gore onerilen siparis:

```sql
SET @SupplierCode = N'32000999';
```

Depo degistirmek icin:

```sql
SET @WarehouseNo = 110;
```

## Dikkat Edilecek Noktalar

- Sorgu read-only'dir; Mikro'ya veri yazmaz.
- `@SupplierCode` zorunludur; firma secilmeden liste uretilmez.
- `SATINALMA_SARTLARI` eslesmesi secili firmaya ait urunu listeye alabilir.
- Acik verilen firma siparisi, sadece ayardaki guvenilir tedarikciler icin
  ihtiyactan dusulur.
- Varsayilan ayarda acik firma siparisi dusumu kapalidir; cunku firma mal kabul
  ve diger hareketler cogu zaman siparis GUID'ine baglanmadiginda
  `sip_teslim_miktar` ilerlemez.
- Acik firma siparisi dusumu ayari:

```json
"SuggestedCompanyOrders": {
  "OpenIssuedOrderDeduction": {
    "Enabled": false,
    "TrustedSupplierCodes": []
  }
}
```
- `sas_asgari_miktar` ihtiyactan buyukse onerilen miktar asgari miktara tamamlanir.
- Firma dis kaynak oldugu icin kaynak depo stogu gibi bir limit uygulanmaz.

## API ve UI Akisi

Backend endpointleri:

```text
GET  /api/siparis-islemleri/onerilen-firma-siparisleri
POST /api/siparis-islemleri/onerilen-firma-siparisleri/convert-to-order
```

Listeleme parametreleri:

```text
supplierCode: zorunlu, UI'da secilen firma kodu
warehouseNo: opsiyonel, bos gelirse login kullanicisinin deposu kullanilir
lookbackDays: opsiyonel, varsayilan 43
fallbackRecommendedDay: opsiyonel, varsayilan 7
```

UI tek sayfa akisi:

1. Kullanici firma/tedarikci secer.
2. Sayfa `GET /onerilen-firma-siparisleri` ile secilen firmaya ait onerilen kalemleri getirir.
3. Grid firma, stok kodu, stok adi, barkod, mevcut stok, son satis, acik firma siparisi,
   ihtiyac, asgari alim miktari, alis fiyati ve onerilen siparis miktarini gosterir.
4. Kullanici satirlari secer ve gerekirse miktari duzenler.
5. Secilen satirlar `POST /convert-to-order` ile verilen firma siparisine cevrilir.

`convert-to-order` body ornegi:

```json
{
  "supplierCode": "32000999",
  "deliveryDate": "2026-07-01",
  "description1": "Onerilen siparisten olustu",
  "lines": [
    {
      "stockCode": "010001",
      "quantity": 24,
      "recommendedQuantity": 24,
      "unitPrice": 15.75,
      "unitPointer": 1
    }
  ]
}
```

Olusan belge mevcut verilen firma siparisi yazma altyapisini kullanir. Bu nedenle
Mikro yazma modu, evrak numarasi uretimi ve Mikro API/database routing davranisi
`VerilenFirmaSiparisleri` ile aynidir.
