# Etiket Basim API Rehberi

Bu dokuman `Etiket Basim` eski WinForms uygulamasinin API'ye tasinan ilk
surumunu anlatir. Modul route kokudur:

```text
api/kasa-islemleri/etiket-basim
```

Modul, manav/depo mal kabul etiketi icin su isleri kapsar:

- Tedarikci arama
- MNV stok arama
- Net kilo, kasa darasi, ortalama kilo ve etiket barkodu hesaplama
- `Furpa.dbo.Manav_Depo_Mal_Kabul_Etiket` kabul kayitlarini listeleme/ekleme/guncelleme/silme
- Etiket yazdirma icin label datasini dondurme
- Alinan urunler ve depo stok raporlarini okuma
- Mikro mal kabul aktarim route'unu sozlesme olarak gostermek, fakat guvenli aktarim tamamlanana kadar kapali tutmak

## Yetkiler

```text
kasa-islemleri.etiket-basim.list
kasa-islemleri.etiket-basim.detail
kasa-islemleri.etiket-basim.create
kasa-islemleri.etiket-basim.update
kasa-islemleri.etiket-basim.delete
kasa-islemleri.etiket-basim.transfer
kasa-islemleri.etiket-basim.all-warehouses
```

Permission katalogu startup senkronizasyonu aciksa bu yetkileri otomatik
ekler. Ayrica migration istenirse auth katalog migration'i uretilebilir.

## Referans Arama

Tedarikci arama:

```http
GET /api/kasa-islemleri/etiket-basim/suppliers?query=ABC&take=20
```

Kurallar:

- `query` en az 2 karakter olmalidir.
- Eski uygulamadaki cari kod prefix haricleri korunur:
  `8888`, `1999`, `2012`, `4690`, `1998`, `2022`, `120.MY`.

Stok arama:

```http
GET /api/kasa-islemleri/etiket-basim/stocks?query=DOMATES&prefix=MNV&take=20
GET /api/kasa-islemleri/etiket-basim/stocks/{stockCode}
GET /api/kasa-islemleri/etiket-basim/stocks/by-name?name=MNV DOMATES
```

Kurallar:

- Varsayilan stok prefix'i `MNV`.
- `query` stok adi, stok kodu veya barkod icinde aranir.
- `*` karakteri SQL wildcard gibi `%` davranisina cevrilir.
- Barkod secimi `BARKOD_TANIMLARI` icinden master/birim onceligiyle yapilir.

## Hesaplama

```http
POST /api/kasa-islemleri/etiket-basim/acceptance-records/calculate
```

Body:

```json
{
  "grossWeight": 100.0,
  "caseTare": 1.2,
  "caseCount": 10,
  "palletTare": 5.0,
  "stockBarcode": "1234567"
}
```

Kural:

```text
caseTotalTare = caseTare * caseCount
netReceivedWeight = grossWeight - caseTotalTare - palletTare
averageCaseWeight = netReceivedWeight / caseCount
```

Ek kurallar:

- `caseCount` bos gelirse `1` kabul edilir.
- `palletTare` bos gelirse `0` kabul edilir.
- `averageCaseWeight > 99` ise API hata doner.
- `labelBarcodeRaw` eski uygulamadaki ortalama kilo metni kuralina gore uretilir. `labelBarcode` ise EAN13 icin check-digit eklenmis yazdirilacak nihai degerdir.

Ornek response:

```json
{
  "caseTotalTare": 12.0,
  "netReceivedWeight": 83.0,
  "averageCaseWeight": 8.3,
  "labelBarcodeRaw": "123456708300",
  "labelBarcode": "1234567083001",
  "barcodeSymbology": "EAN13"
}
```

## Kabul Kayitlari

Gunluk liste:

```http
GET /api/kasa-islemleri/etiket-basim/acceptance-records?date=2026-07-30
```

Tek kayit:

```http
GET /api/kasa-islemleri/etiket-basim/acceptance-records/{id}
```

Yeni kayit:

```http
POST /api/kasa-islemleri/etiket-basim/acceptance-records
```

Body:

```json
{
  "supplierCode": "120.001",
  "supplierName": "TEDARIKCI A",
  "documentSeries": "MNV",
  "documentNo": "12345",
  "stockCode": "MNV001",
  "stockName": "MNV DOMATES",
  "stockBarcode": "1234567",
  "grossWeight": 100.0,
  "caseTare": 1.2,
  "caseCount": 10,
  "palletTare": 5.0,
  "receivedBy": "Ali",
  "caseType": "REHINLI"
}
```

Notlar:

- `caseType` olarak `REHINLI`, `REHINSIZ` ve Turkce karakterli varyasyonlar kabul edilir; response tablo degeriyle uyumlu normalize edilmis kasa tipini doner.
- Yeni kayit `Mikro_Aktarildi = 0` olarak acilir.
- `documentSeries` bos gelirse `MNV` kabul edilir.
- Aktarilmis kayit guncellenemez ve silinemez.

Guncelleme:

```http
PUT /api/kasa-islemleri/etiket-basim/acceptance-records/{id}
```

Silme:

```http
DELETE /api/kasa-islemleri/etiket-basim/acceptance-records/{id}
```

## Etiket Data

Kayittan etiket:

```http
GET /api/kasa-islemleri/etiket-basim/acceptance-records/{id}/label
```

Kaydetmeden etiket preview:

```http
POST /api/kasa-islemleri/etiket-basim/labels/preview
```

API su bilgileri dondurur:

- stok kodu
- stok adi
- tedarikci
- ortalama kasa kilosu
- etiket tarihi
- etiket sayisi
- ham label barkodu: `labelBarcodeRaw`
- yazdirilacak nihai label barkodu: `labelBarcode`
- barkod sembolojisi
- kasa darasi
- kasa tipi

Yazdirma isini UI veya terminal tarafinda bu data ile yapabilir. Eski sistem ZPL/ESC/POS uretmez; Windows printer driver uzerinden DevExpress XtraReport yazdirir.

## Raporlar

Alinan urunler / fatura farki:

```http
GET /api/kasa-islemleri/etiket-basim/reports/received-products?date=2026-07-30
```

Depo son stok:

```http
GET /api/kasa-islemleri/etiket-basim/reports/depot-stock?warehouseNo=56&date=2026-07-30
```

Depo stok raporunda varsayilan depo `56` olur. Kullanici farkli depo isterse
standart depo yetkisi/all-warehouses kontrolu uygulanir.

## Mikro Aktarim

Route sozlesme olarak eklendi:

```http
POST /api/kasa-islemleri/etiket-basim/micro/goods-receipts
```

Body:

```json
{
  "date": "2026-07-30",
  "supplierCode": "120.001"
}
```

Bu endpoint su an `501 Not Implemented` doner. Sebep:

- Eski uygulama `STOK_HAREKETLERI` tablosuna dogrudan insert yapiyordu.
- Eski sorguda `Mikro_Aktarildi = 0` filtresi yoktu.
- Transaction ve duplicate/idempotency korumasi yoktu.
- Evrak sira uretimi `MAX + 1` mantigi ile race condition riski tasiyor.

Aktarim acilmadan once kural:

- Sadece bekleyen kayitlar alinmali.
- Tarih + cari kod bazinda toplu aktarilmali.
- Insert ve `Mikro_Aktarildi = 1` update ayni transaction icinde olmali.
- Tekrar cagrida duplicate hareket olusmamali.
- Evrak no stratejisi Mikro tarafinda onaylanmali.
