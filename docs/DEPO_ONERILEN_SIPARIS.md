# Depo Onerilen Siparis Sorgusu

Bu dokuman, Mikro canli veri kontrolunden sonra depo onerilen siparis icin
kullanilmasi onerilen sorgu mantigini anlatir.

## Temel Mantik

- `@TargetWarehouseNo`: Siparis isteyen depo/magaza.
- `@SourceWarehouseNo`: Urunu gonderecek kaynak depo.
- Kaynak depo hangi urun ailesini besliyorsa sadece o urunler listelenir.
- Urun ailesi `DEPOLAR.dep_barkod_yazici_yolu` icindeki model kodlarindan okunur.
- Statik min/sip/max stok seviyeleri canli veride bos oldugu icin onerilen miktar
  son satis/tuketim ortalamasindan hesaplanir.
- Acik gelen depo siparisleri, teslim miktari takibi guvenilir kaynak depolar icin
  ihtiyactan dusulur.
- Kaynak depoda elde olmayan miktar onerilmez.

Kisa formul:

```text
minimum stok seviyesi = gunluk ortalama satis * min stok gunu
                     (min stok gunu yoksa onerilen gun kullanilir)

onerilen/hedef stok seviyesi = gunluk ortalama satis * onerilen gun

maksimum stok seviyesi = gunluk ortalama satis * max stok gunu
                       (max stok gunu doluysa)

minimum ihtiyac = minimum stok seviyesi
                - hedef depodaki mevcut stok
                - ayar izin veriyorsa hedef depoya acik gelen siparis miktari

onerilen depo siparisi = minimum ihtiyac
                        + koli katina yukari yuvarlama
                        + varsa max stok siniri
                        + kaynak depo stok siniri
```

Siparis sadece stok minimum esigin altina dustugunde onerilir. `sto_birim2_katsayi`
1'den buyukse miktar koli katina yukari yuvarlanir. `sto_max_stok_belirleme_gun`
doluysa son miktar maksimum stok seviyesini asmamalidir; limit devreye girerse
miktar asagi dogru en yakin koli katina iner. Kaynak depoda elde stok daha dusukse
son miktar yine koli kati korunarak kaynak stokla sinirlanir.

## Kaynak Depo Model Kodlari

Canli Mikro kontrolunde gorulen kaynak depo eslesmeleri:

| Kaynak Depo | Depo Adi | Model Kodlari | Anlam |
| --- | --- | --- | --- |
| `50` | `MERKEZ DEPO` | `01,02,03,04,20` | Merkez depo urunleri |
| `53` | `ET-SARKUTERI DEPO` | `15,21` | Et/sarkuteri |
| `55` | `UNLU URETIM` | `30,31,32,33,22` | Unlu uretim |
| `56` | `MANAV DEPO` | `10,11,12,23` | Manav |
| `58` | `UNLU URETIM - OZLUCE` | `40` | Unlu uretim |

Bu yuzden `@SourceWarehouseNo = 50` ise manav, et ve unlu urunleri gelmez.
`@SourceWarehouseNo = 56` ise sadece manav model kodlari gelir.

## Neden Mikro Proseduru Direkt Kullanilmiyor?

Mevcut prosedurler:

- `DepoOnerilenSiparisler`
- `DepoOnerilenSiparislerFurpa`
- `DepoOnerilenSiparislerGokhan`

Canli kontrolde bu prosedurlerde su riskler goruldu:

- Parametre isimleri yon olarak kafa karistiriyor.
- Bazi versiyonlarda model kodu hedef depodan okunuyor; magaza deposunda model kodu bos ise sonuc kirpiliyor.
- `STOKLAR.sto_min_stok`, `sto_siparis_stok`, `sto_max_stok` canli veride bos.
- `STOK_DEPO_DETAYLARI.sdp_min_stok`, `sdp_sip_stok`, `sdp_max_stok` canli veride bos.
- Acik gelen siparisler prosedurlerde net ihtiyac hesabina saglikli dahil edilmiyor.

Bu yuzden asagidaki SELECT daha okunabilir ve daha kontrol edilebilir ana sorgu olarak onerilir.

## Optimize Ana Sorgu

```sql
DECLARE @TargetWarehouseNo int = 110; -- siparis isteyen magaza/depo
DECLARE @SourceWarehouseNo int = 50;  -- kaynak depo: 50, 53, 55, 56, 58...
DECLARE @LookbackDays int = 43;       -- Mikro prosedurlerindeki son 43 gun mantigi
DECLARE @FallbackRecommendedDay int = 7;

DECLARE @SourceModelCodes nvarchar(100);

SELECT @SourceModelCodes = dep_barkod_yazici_yolu
FROM dbo.DEPOLAR
WHERE dep_no = @SourceWarehouseNo;

IF NULLIF(LTRIM(RTRIM(ISNULL(@SourceModelCodes, N''))), N'') IS NULL
BEGIN
    THROW 50001, 'Secilen kaynak depo icin dep_barkod_yazici_yolu/model kodlari bos.', 1;
END;

;WITH SourceModels AS (
    SELECT LTRIM(RTRIM(value)) AS ModelCode
    FROM STRING_SPLIT(@SourceModelCodes, ',')
    WHERE LTRIM(RTRIM(value)) <> ''
),
StockBase AS (
    SELECT
        stock.sto_kod,
        stock.sto_isim,
        stock.sto_model_kodu,
        stock.sto_birim2_katsayi,
        stock.sto_min_stok_belirleme_gun,
        stock.sto_sip_stok_belirleme_gun,
        stock.sto_max_stok_belirleme_gun
    FROM dbo.STOKLAR AS stock
    INNER JOIN SourceModels AS model
        ON model.ModelCode = stock.sto_model_kodu
    WHERE ISNULL(stock.sto_iptal, 0) = 0
      AND ISNULL(stock.sto_siparis_dursun, 0) = 0
      AND stock.sto_isim NOT LIKE 'DLS%'
      AND stock.sto_isim NOT LIKE 'SRF%'
      AND stock.sto_kod NOT IN ('011141','013199','000154','000754','000051','089020','000219')
      AND EXISTS (
          SELECT 1
          FROM dbo.STOK_DEPO_DETAYLARI AS targetDetail
          WHERE targetDetail.sdp_depo_no = @TargetWarehouseNo
            AND targetDetail.sdp_depo_kod = stock.sto_kod
            AND ISNULL(targetDetail.sdp_sipdursun, 0) = 0
      )
      AND EXISTS (
          SELECT 1
          FROM dbo.STOK_DEPO_DETAYLARI AS sourceDetail
          WHERE sourceDetail.sdp_depo_no = @SourceWarehouseNo
            AND sourceDetail.sdp_depo_kod = stock.sto_kod
            AND ISNULL(sourceDetail.sdp_sipdursun, 0) = 0
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
      AND movement.sth_cikis_depo_no = @TargetWarehouseNo
      AND ISNULL(movement.sth_iptal, 0) = 0
      AND movement.sth_tip = 1
      AND movement.sth_cins = 1
      AND movement.sth_normal_iade = 0
      AND movement.sth_evraktip IN (4, 1)
    GROUP BY movement.sth_stok_kod
),
OpenIncoming AS (
    SELECT
        warehouseOrder.ssip_stok_kod AS StockCode,
        SUM(ISNULL(warehouseOrder.ssip_miktar, 0) - ISNULL(warehouseOrder.ssip_teslim_miktar, 0)) AS OpenOrderQuantity
    FROM dbo.DEPOLAR_ARASI_SIPARISLER AS warehouseOrder
    INNER JOIN Consumption AS consumption
        ON consumption.StockCode = warehouseOrder.ssip_stok_kod
    WHERE warehouseOrder.ssip_girdepo = @TargetWarehouseNo
      AND warehouseOrder.ssip_cikdepo = @SourceWarehouseNo
      AND ISNULL(warehouseOrder.ssip_iptal, 0) = 0
      AND ISNULL(warehouseOrder.ssip_kapat_fl, 0) = 0
      AND ISNULL(warehouseOrder.ssip_miktar, 0) > ISNULL(warehouseOrder.ssip_teslim_miktar, 0)
    GROUP BY warehouseOrder.ssip_stok_kod
),
StockBalance AS (
    SELECT
        movement.sth_stok_kod AS StockCode,
        ROUND(SUM(CASE
            WHEN movement.sth_tip = 0 AND (movement.sth_giris_depo_no = @TargetWarehouseNo OR @TargetWarehouseNo = 0)
                THEN ISNULL(movement.sth_miktar, 0)
            WHEN movement.sth_tip = 1 AND (movement.sth_cikis_depo_no = @TargetWarehouseNo OR @TargetWarehouseNo = 0)
                THEN -1 * ISNULL(movement.sth_miktar, 0)
            WHEN movement.sth_tip = 2 AND movement.sth_giris_depo_no = @TargetWarehouseNo
                THEN ISNULL(movement.sth_miktar, 0)
            WHEN movement.sth_tip = 2 AND movement.sth_cikis_depo_no = @TargetWarehouseNo
                THEN -1 * ISNULL(movement.sth_miktar, 0)
            ELSE 0
        END), 8) AS TargetOnHand,
        ROUND(SUM(CASE
            WHEN movement.sth_tip = 0 AND (movement.sth_giris_depo_no = @SourceWarehouseNo OR @SourceWarehouseNo = 0)
                THEN ISNULL(movement.sth_miktar, 0)
            WHEN movement.sth_tip = 1 AND (movement.sth_cikis_depo_no = @SourceWarehouseNo OR @SourceWarehouseNo = 0)
                THEN -1 * ISNULL(movement.sth_miktar, 0)
            WHEN movement.sth_tip = 2 AND movement.sth_giris_depo_no = @SourceWarehouseNo
                THEN ISNULL(movement.sth_miktar, 0)
            WHEN movement.sth_tip = 2 AND movement.sth_cikis_depo_no = @SourceWarehouseNo
                THEN -1 * ISNULL(movement.sth_miktar, 0)
            ELSE 0
        END), 8) AS SourceOnHand
    FROM dbo.STOK_HAREKETLERI AS movement
    INNER JOIN Consumption AS consumption
        ON consumption.StockCode = movement.sth_stok_kod
    WHERE movement.sth_tarih <= GETDATE()
      AND NOT (movement.sth_cins IN (9, 15))
      AND (
          (movement.sth_tip = 0 AND (movement.sth_giris_depo_no IN (@TargetWarehouseNo, @SourceWarehouseNo) OR @TargetWarehouseNo = 0 OR @SourceWarehouseNo = 0))
          OR (movement.sth_tip = 1 AND (movement.sth_cikis_depo_no IN (@TargetWarehouseNo, @SourceWarehouseNo) OR @TargetWarehouseNo = 0 OR @SourceWarehouseNo = 0))
          OR (
              movement.sth_tip = 2
              AND movement.sth_giris_depo_no <> movement.sth_cikis_depo_no
              AND (
                  movement.sth_giris_depo_no IN (@TargetWarehouseNo, @SourceWarehouseNo)
                  OR movement.sth_cikis_depo_no IN (@TargetWarehouseNo, @SourceWarehouseNo)
              )
          )
      )
    GROUP BY movement.sth_stok_kod
),
Calculated AS (
    SELECT
        stock.sto_kod,
        stock.sto_isim,
        stock.sto_model_kodu,
        barcode.bar_kodu,
        ISNULL(stockBalance.TargetOnHand, 0) AS TargetOnHand,
        ISNULL(stockBalance.SourceOnHand, 0) AS SourceOnHand,
        ISNULL(consumption.SalesQuantity, 0) AS SalesQuantity,
        ISNULL(openIncoming.OpenOrderQuantity, 0) AS OpenOrderQuantity,
        stock.sto_birim2_katsayi,
        stock.sto_min_stok_belirleme_gun,
        ISNULL(NULLIF(stock.sto_sip_stok_belirleme_gun, 0), @FallbackRecommendedDay) AS RecommendedDay,
        stock.sto_max_stok_belirleme_gun
    FROM StockBase AS stock
    INNER JOIN Consumption AS consumption
        ON consumption.StockCode = stock.sto_kod
    LEFT JOIN OpenIncoming AS openIncoming
        ON openIncoming.StockCode = stock.sto_kod
    LEFT JOIN StockBalance AS stockBalance
        ON stockBalance.StockCode = stock.sto_kod
    OUTER APPLY (
        SELECT TOP 1 barcode.bar_kodu
        FROM dbo.BARKOD_TANIMLARI AS barcode
        WHERE barcode.bar_stokkodu = stock.sto_kod
          AND barcode.bar_birimpntr = 1
        ORDER BY ISNULL(barcode.bar_master, 0) DESC, barcode.bar_create_date DESC
    ) AS barcode
)
SELECT
    calc.sto_kod AS stockCode,
    calc.sto_isim AS stockName,
    calc.sto_model_kodu AS modelCode,
    ISNULL(calc.bar_kodu, '') AS barcode,
    calc.TargetOnHand AS targetOnHand,
    calc.SourceOnHand AS sourceOnHand,
    calc.SalesQuantity AS salesQuantity,
    calc.OpenOrderQuantity AS openIncomingOrderQuantity,
    calc.sto_birim2_katsayi AS packageFactor,
    calc.sto_min_stok_belirleme_gun AS minDay,
    calc.RecommendedDay AS recommendedDay,
    calc.sto_max_stok_belirleme_gun AS maxDay,
    recommended.RecommendedStockQuantity AS recommendedStockQuantity,
    threshold.MinimumNeedQuantity AS needQuantity,
    recommended.SuggestedOrderQuantity AS suggestedOrderQuantity
FROM Calculated AS calc
CROSS APPLY (
    SELECT
        CEILING((calc.SalesQuantity / NULLIF(@LookbackDays, 0)) *
            ISNULL(NULLIF(calc.sto_min_stok_belirleme_gun, 0), calc.RecommendedDay)) AS MinimumStockQuantity,
        CEILING((calc.SalesQuantity / NULLIF(@LookbackDays, 0)) * calc.RecommendedDay) AS RecommendedStockQuantity,
        CASE
            WHEN ISNULL(calc.sto_max_stok_belirleme_gun, 0) > 0
                THEN CEILING((calc.SalesQuantity / NULLIF(@LookbackDays, 0)) * calc.sto_max_stok_belirleme_gun)
            ELSE NULL
        END AS MaximumStockQuantity,
        CASE
            WHEN ABS(ISNULL(calc.sto_birim2_katsayi, 0)) > 1 THEN ABS(calc.sto_birim2_katsayi)
            ELSE 0
        END AS PackageQuantity
) AS targetQuantity
CROSS APPLY (
    SELECT
        targetQuantity.MinimumStockQuantity - calc.TargetOnHand - calc.OpenOrderQuantity AS MinimumNeedQuantity,
        CASE
            WHEN targetQuantity.MaximumStockQuantity IS NULL THEN NULL
            ELSE targetQuantity.MaximumStockQuantity - calc.TargetOnHand - calc.OpenOrderQuantity
        END AS MaximumAllowedQuantity
) AS threshold
CROSS APPLY (
    SELECT
        CASE
            WHEN threshold.MinimumNeedQuantity <= 0 THEN 0
            WHEN targetQuantity.PackageQuantity > 0
                THEN CEILING(threshold.MinimumNeedQuantity / targetQuantity.PackageQuantity) * targetQuantity.PackageQuantity
            ELSE threshold.MinimumNeedQuantity
        END AS RoundedOrderQuantity
) AS roundedOrder
CROSS APPLY (
    SELECT
        CASE
            WHEN roundedOrder.RoundedOrderQuantity <= 0 THEN 0
            WHEN threshold.MaximumAllowedQuantity IS NOT NULL AND threshold.MaximumAllowedQuantity <= 0 THEN 0
            WHEN threshold.MaximumAllowedQuantity IS NOT NULL AND roundedOrder.RoundedOrderQuantity > threshold.MaximumAllowedQuantity
                THEN
                    CASE
                        WHEN targetQuantity.PackageQuantity > 0
                            THEN FLOOR(threshold.MaximumAllowedQuantity / targetQuantity.PackageQuantity) * targetQuantity.PackageQuantity
                        ELSE threshold.MaximumAllowedQuantity
                    END
            ELSE roundedOrder.RoundedOrderQuantity
        END AS MaxLimitedOrderQuantity
) AS maxLimited
CROSS APPLY (
    SELECT
        CASE
            WHEN maxLimited.MaxLimitedOrderQuantity <= 0 THEN 0
            WHEN calc.SourceOnHand <= 0 THEN 0
            WHEN maxLimited.MaxLimitedOrderQuantity > calc.SourceOnHand
                THEN
                    CASE
                        WHEN targetQuantity.PackageQuantity > 0
                            THEN FLOOR(calc.SourceOnHand / targetQuantity.PackageQuantity) * targetQuantity.PackageQuantity
                        ELSE calc.SourceOnHand
                    END
            ELSE maxLimited.MaxLimitedOrderQuantity
        END AS SuggestedOrderQuantity,
        targetQuantity.RecommendedStockQuantity
) AS recommended
WHERE calc.SalesQuantity > 0
  AND ISNULL(calc.bar_kodu, '') <> ''
  AND recommended.SuggestedOrderQuantity > 0
ORDER BY suggestedOrderQuantity DESC, calc.sto_isim;
```

## Kullanim Ornekleri

```sql
-- Merkez depo urunleri
SET @SourceWarehouseNo = 50;

-- Et/sarkuteri
SET @SourceWarehouseNo = 53;

-- Unlu uretim
SET @SourceWarehouseNo = 55;

-- Manav
SET @SourceWarehouseNo = 56;

-- Unlu uretim - Ozluce
SET @SourceWarehouseNo = 58;
```

## Dikkat Edilecek Noktalar

- Kaynak deponun `dep_barkod_yazici_yolu` alani bos ise sorgu bilerek hata verir.
- Bu hata, kaynak deponun urun ailesi tanimli degil demektir.
- `openIncomingOrderQuantity` sadece ayardaki guvenilir kaynak depolar icin hesaplanir.
  Kaynak depo guvenilir listede degilse veya ayar kapaliysa bu miktar `0` kabul edilir.
- Oneri minimum stok esigine gore tetiklenir; miktar hedef stoga kadar degil,
  minimum stok acigina gore baslar.
- `sto_birim2_katsayi` 1'den buyukse miktar koli katina yukari yuvarlanir.
- `sto_max_stok_belirleme_gun` doluysa onerilen miktar maksimum stok seviyesini asmayacak sekilde sinirlanir; koli kati korunur.
- Kaynak depodaki stok daha azsa son miktar kaynak stokla sinirlanir; koli kati korunur.
- Sorgu read-only'dir; Mikro'ya veri yazmaz.
- Acik siparis dusumu ayari:

```json
"SuggestedWarehouseOrders": {
  "OpenIncomingOrderDeduction": {
    "Enabled": true,
    "TrustedSourceWarehouseNos": [50]
  }
}
```

## Temel Mantik Ozeti

Bu sorgunun amaci, bir magaza/deponun hangi urunden ne kadar siparis etmesi
gerektigini hesaplamaktir. Hesaplama su sirayla ilerler:

1. Once kaynak depo belirlenir.

   Ornek: `50` merkez depo, `56` manav depo, `53` et/sarkuteri depo.

2. Kaynak deponun hangi urun ailesini besledigi okunur.

   Bu bilgi `DEPOLAR.dep_barkod_yazici_yolu` alanindan gelir.
   Ornek: `56` icin `10,11,12,23` model kodlari manav urunlerini ifade eder.

3. Urun listesi sadece bu kaynak deponun model kodlariyla sinirlanir.

   Bu sayede `50` secildiginde manav, et veya unlu urunleri listeye karismaz.
   `56` secildiginde de sadece manav urunleri gelir.

4. Hedef deponun son satis/tuketim miktari hesaplanir.

   Canli veride min/sip/max stok seviyeleri dolu olmadigi icin statik stok
   seviyesine gore degil, son `@LookbackDays` gunluk satis/tuketim ortalamasina
   gore minimum, onerilen ve varsa maksimum stok seviyeleri hesaplanir.

5. Hedef deponun mevcut stogu hesaba katilir.

   Depoda zaten yeterli stok varsa yeni siparis onerilmez.

6. Hedef depoya daha once acilmis ama henuz tam teslim edilmemis depo siparisleri,
   sadece kaynak depo ayardaki guvenilir listede ise ihtiyactan dusulur.

   Boylece ayni urun icin gereksiz ikinci siparis onerisi uretilmez.

7. Minimum stok acigi koli katina yuvarlanir ve varsa maksimum stok seviyesiyle sinirlanir.

   Boylece 5'lik koliyle satilan urunde 13 adet gibi boluk siparis yerine koli
   katina uygun miktar uretilir; ancak max stok seviyesi doluysa miktar max stogu
   asmayacak sekilde asagi dogru koli katina kirpilir.

8. Kaynak deponun eldeki stogu kontrol edilir.

   Ihtiyac daha fazla olsa bile kaynak depoda olmayan miktar onerilmez.

Kisa formul:

```text
minimum stok seviyesi = gunluk ortalama satis * min stok gunu
                     (min stok gunu yoksa onerilen gun kullanilir)

onerilen/hedef stok seviyesi = gunluk ortalama satis * onerilen gun

maksimum stok seviyesi = gunluk ortalama satis * max stok gunu
                       (max stok gunu doluysa)

minimum ihtiyac = minimum stok seviyesi
                - hedef depodaki mevcut stok
                - ayar izin veriyorsa hedef depoya acik gelen siparis miktari

onerilen siparis = minimum ihtiyac
                 + koli katina yukari yuvarlama
                 + varsa max stok siniri
                 + kaynak depo stok siniri
```

Sonuc olarak sorgu, hem urun ailesini dogru kaynak depoya gore filtreler hem de
gereksiz/fazla siparis olusmasini engeller.

## API ve UI Akisi

Backend endpointleri:

```text
GET  /api/siparis-islemleri/onerilen-depo-siparisleri
POST /api/siparis-islemleri/onerilen-depo-siparisleri/convert-to-order
```

Listeleme parametreleri:

```text
sourceWarehouseNo: zorunlu, urunu gonderecek kaynak depo
targetWarehouseNo: opsiyonel, bos gelirse login kullanicisinin deposu kullanilir
lookbackDays: opsiyonel, varsayilan 43
fallbackRecommendedDay: opsiyonel, varsayilan 7
```

UI tek sayfa akisi:

1. Kullanici kaynak depo secer.
2. Sayfa `GET /onerilen-depo-siparisleri` ile onerilen kalemleri getirir.
3. Grid stok kodu, stok adi, barkod, hedef stok, kaynak stok, son satis, acik siparis,
   ihtiyac, koli katsayisi ve onerilen siparis miktarini gosterir.
4. Kullanici satirlari secer ve gerekirse miktari duzenler.
5. Secilen satirlar `POST /convert-to-order` ile verilen depo siparisine cevrilir.

`convert-to-order` body ornegi:

```json
{
  "sourceWarehouseNo": 50,
  "deliveryDate": "2026-07-01",
  "description": "Onerilen siparisten olustu",
  "lines": [
    {
      "stockCode": "010001",
      "quantity": 12,
      "recommendedQuantity": 12,
      "unitPointer": 1
    }
  ]
}
```

Olusan belge mevcut verilen depo siparisi yazma altyapisini kullanir. Bu nedenle
Mikro yazma modu, evrak numarasi uretimi ve Mikro API/database routing davranisi
`VerilenDepoSiparisleri` ile aynidir.
