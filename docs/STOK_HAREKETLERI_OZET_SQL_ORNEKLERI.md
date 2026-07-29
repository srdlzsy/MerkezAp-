# STOK_HAREKETLERI_OZET SQL Ornekleri

Bu dokuman `STOK_HAREKETLERI_OZET` tablosundan stok bakiyesi kontrol etmek
icin kullanilabilecek SQL orneklerini icerir.

## Temel Bilgi

`STOK_HAREKETLERI_OZET`, Mikro tarafinda `STOK_HAREKETLERI` kayitlarinin
stok, depo, mali yil, donem, hareket cinsi, sorumluluk merkezi ve proje
kiriminda ozetlenmis halidir.

Tek depo bakiyesi icin dogru net stok formulu:

```sql
SUM(
    ISNULL(sho_GirisNormal, 0)
  + ISNULL(sho_GirisIade, 0)
  - ISNULL(sho_CikisNormal, 0)
  - ISNULL(sho_CikisIade, 0)
)
```

Notlar:

- Tek depo bakiyesinde `sho_HareketCins NOT IN (9, 15)` kullanilir.
- `9` ve `15` deger farki hareketleridir, miktar bakiyesine dahil edilmez.
- Tek depo bakiyesinde `6 Transfer` dahil edilmelidir; giris depoya arti, cikis depoya eksi etki eder.
- Tum depolar toplaminda transfer ic hareket oldugu icin genelde `sho_HareketCins NOT IN (6, 9, 15)` kullanilir.
- Bu tablo gun bazli tarih tutmaz; yil/donem bazli ozet tutar. Gun hassasiyetli stok icin `dbo.fn_DepodakiMiktar(stok, depo, tarih)` kullanilmalidir.

## 1. Tek Stok ve Tek Depo Guncel Stok

```sql
DECLARE @stockCode nvarchar(25) = N'015771';
DECLARE @warehouseNo int = 135;

SELECT
    summary.sho_StokKodu AS StockCode,
    summary.sho_Depo AS WarehouseNo,
    ROUND(SUM(
        ISNULL(summary.sho_GirisNormal, 0)
      + ISNULL(summary.sho_GirisIade, 0)
      - ISNULL(summary.sho_CikisNormal, 0)
      - ISNULL(summary.sho_CikisIade, 0)
    ), 8) AS Quantity
FROM dbo.STOK_HAREKETLERI_OZET AS summary WITH (NOLOCK)
WHERE summary.sho_StokKodu = @stockCode
  AND summary.sho_Depo = @warehouseNo
  AND summary.sho_HareketCins NOT IN (9, 15)
GROUP BY summary.sho_StokKodu, summary.sho_Depo;
```

## 2. Mikro Fonksiyonlariyla Karsilastirma

```sql
DECLARE @stockCode nvarchar(25) = N'015771';
DECLARE @warehouseNo int = 135;
DECLARE @reportDate datetime = GETDATE();

SELECT
    dbo.fn_DepodakiMiktarSonDurum(@stockCode, @warehouseNo) AS OzetFonksiyon,
    dbo.fn_DepodakiMiktar(@stockCode, @warehouseNo, @reportDate) AS HareketFonksiyon;
```

## 3. Ozet Tablo ve Fonksiyon Karsilastirma

```sql
DECLARE @stockCode nvarchar(25) = N'015771';
DECLARE @warehouseNo int = 135;
DECLARE @reportDate datetime = GETDATE();

SELECT
    summary.sho_StokKodu AS StockCode,
    summary.sho_Depo AS WarehouseNo,
    ROUND(SUM(
        ISNULL(summary.sho_GirisNormal, 0)
      + ISNULL(summary.sho_GirisIade, 0)
      - ISNULL(summary.sho_CikisNormal, 0)
      - ISNULL(summary.sho_CikisIade, 0)
    ), 8) AS OzetQuantity,
    dbo.fn_DepodakiMiktarSonDurum(@stockCode, @warehouseNo) AS OzetFonksiyon,
    dbo.fn_DepodakiMiktar(@stockCode, @warehouseNo, @reportDate) AS HareketFonksiyon
FROM dbo.STOK_HAREKETLERI_OZET AS summary WITH (NOLOCK)
WHERE summary.sho_StokKodu = @stockCode
  AND summary.sho_Depo = @warehouseNo
  AND summary.sho_HareketCins NOT IN (9, 15)
GROUP BY summary.sho_StokKodu, summary.sho_Depo;
```

## 4. Depodaki Stokta Olan Ilk 100 Urun

```sql
DECLARE @warehouseNo int = 135;

SELECT TOP (100)
    summary.sho_StokKodu AS StockCode,
    COALESCE(stock.sto_isim, '') AS StockName,
    summary.sho_Depo AS WarehouseNo,
    COALESCE(warehouse.dep_adi, '') AS WarehouseName,
    ROUND(SUM(
        ISNULL(summary.sho_GirisNormal, 0)
      + ISNULL(summary.sho_GirisIade, 0)
      - ISNULL(summary.sho_CikisNormal, 0)
      - ISNULL(summary.sho_CikisIade, 0)
    ), 8) AS Quantity
FROM dbo.STOK_HAREKETLERI_OZET AS summary WITH (NOLOCK)
LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
    ON stock.sto_kod = summary.sho_StokKodu
LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK)
    ON warehouse.dep_no = summary.sho_Depo
WHERE summary.sho_Depo = @warehouseNo
  AND summary.sho_HareketCins NOT IN (9, 15)
GROUP BY
    summary.sho_StokKodu,
    stock.sto_isim,
    summary.sho_Depo,
    warehouse.dep_adi
HAVING ABS(SUM(
        ISNULL(summary.sho_GirisNormal, 0)
      + ISNULL(summary.sho_GirisIade, 0)
      - ISNULL(summary.sho_CikisNormal, 0)
      - ISNULL(summary.sho_CikisIade, 0)
)) > 0.000001
ORDER BY ABS(Quantity) DESC;
```

## 5. Tum Depolarda Stok Toplami

```sql
DECLARE @stockCode nvarchar(25) = N'015771';

SELECT
    summary.sho_StokKodu AS StockCode,
    COALESCE(stock.sto_isim, '') AS StockName,
    ROUND(SUM(
        ISNULL(summary.sho_GirisNormal, 0)
      + ISNULL(summary.sho_GirisIade, 0)
      - ISNULL(summary.sho_CikisNormal, 0)
      - ISNULL(summary.sho_CikisIade, 0)
    ), 8) AS TotalQuantity
FROM dbo.STOK_HAREKETLERI_OZET AS summary WITH (NOLOCK)
LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
    ON stock.sto_kod = summary.sho_StokKodu
WHERE summary.sho_StokKodu = @stockCode
  AND summary.sho_HareketCins NOT IN (6, 9, 15)
GROUP BY summary.sho_StokKodu, stock.sto_isim;
```

## 6. Tek Depoda Eksi Stoklar

```sql
DECLARE @warehouseNo int = 135;

SELECT TOP (100)
    summary.sho_Depo AS WarehouseNo,
    COALESCE(warehouse.dep_adi, '') AS WarehouseName,
    summary.sho_StokKodu AS StockCode,
    COALESCE(stock.sto_isim, '') AS StockName,
    ROUND(SUM(
        ISNULL(summary.sho_GirisNormal, 0)
      + ISNULL(summary.sho_GirisIade, 0)
      - ISNULL(summary.sho_CikisNormal, 0)
      - ISNULL(summary.sho_CikisIade, 0)
    ), 8) AS Quantity
FROM dbo.STOK_HAREKETLERI_OZET AS summary WITH (NOLOCK)
LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK)
    ON warehouse.dep_no = summary.sho_Depo
LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
    ON stock.sto_kod = summary.sho_StokKodu
WHERE summary.sho_Depo = @warehouseNo
  AND summary.sho_HareketCins NOT IN (9, 15)
GROUP BY
    summary.sho_Depo,
    warehouse.dep_adi,
    summary.sho_StokKodu,
    stock.sto_isim
HAVING SUM(
        ISNULL(summary.sho_GirisNormal, 0)
      + ISNULL(summary.sho_GirisIade, 0)
      - ISNULL(summary.sho_CikisNormal, 0)
      - ISNULL(summary.sho_CikisIade, 0)
) < -0.000001
ORDER BY Quantity ASC;
```

## 7. Stokun Depo Dagilimi

```sql
DECLARE @stockCode nvarchar(25) = N'015771';

SELECT
    summary.sho_Depo AS WarehouseNo,
    COALESCE(warehouse.dep_adi, '') AS WarehouseName,
    ROUND(SUM(
        ISNULL(summary.sho_GirisNormal, 0)
      + ISNULL(summary.sho_GirisIade, 0)
      - ISNULL(summary.sho_CikisNormal, 0)
      - ISNULL(summary.sho_CikisIade, 0)
    ), 8) AS Quantity
FROM dbo.STOK_HAREKETLERI_OZET AS summary WITH (NOLOCK)
LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK)
    ON warehouse.dep_no = summary.sho_Depo
WHERE summary.sho_StokKodu = @stockCode
  AND summary.sho_HareketCins NOT IN (9, 15)
GROUP BY summary.sho_Depo, warehouse.dep_adi
HAVING ABS(SUM(
        ISNULL(summary.sho_GirisNormal, 0)
      + ISNULL(summary.sho_GirisIade, 0)
      - ISNULL(summary.sho_CikisNormal, 0)
      - ISNULL(summary.sho_CikisIade, 0)
)) > 0.000001
ORDER BY Quantity DESC;
```

## 8. Yil ve Donem Bazli Stok Hareket Ozeti

```sql
DECLARE @stockCode nvarchar(25) = N'015771';
DECLARE @warehouseNo int = 135;

SELECT
    summary.sho_MaliYil AS FiscalYear,
    summary.sho_Donem AS PeriodNo,
    summary.sho_HareketCins AS MovementKind,
    COALESCE(kind.SHCinsIsim, '') AS MovementKindName,
    ROUND(SUM(ISNULL(summary.sho_GirisNormal, 0)), 8) AS InNormal,
    ROUND(SUM(ISNULL(summary.sho_GirisIade, 0)), 8) AS InReturn,
    ROUND(SUM(ISNULL(summary.sho_CikisNormal, 0)), 8) AS OutNormal,
    ROUND(SUM(ISNULL(summary.sho_CikisIade, 0)), 8) AS OutReturn,
    ROUND(SUM(
        ISNULL(summary.sho_GirisNormal, 0)
      + ISNULL(summary.sho_GirisIade, 0)
      - ISNULL(summary.sho_CikisNormal, 0)
      - ISNULL(summary.sho_CikisIade, 0)
    ), 8) AS NetQuantity
FROM dbo.STOK_HAREKETLERI_OZET AS summary WITH (NOLOCK)
LEFT JOIN dbo.vw_Stok_Hareket_Cins_Isimleri AS kind WITH (NOLOCK)
    ON kind.SHCinsNo = summary.sho_HareketCins
WHERE summary.sho_StokKodu = @stockCode
  AND summary.sho_Depo = @warehouseNo
GROUP BY
    summary.sho_MaliYil,
    summary.sho_Donem,
    summary.sho_HareketCins,
    kind.SHCinsIsim
ORDER BY summary.sho_MaliYil DESC, summary.sho_Donem DESC, summary.sho_HareketCins;
```
