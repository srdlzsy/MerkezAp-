# Manav Mal Kabul ve Etiket API Rehberi

Bu dokuman eski WinForms `Manav Mal Kabul ve Etiket` akisinin yeni API'deki son halini
anlatir. Modul artik sadece etiket basim degil; manav tartim/etiket kaydi,
canli Mikro manav mal kabul belgelerini okuma ve etiket-Mikro karsilastirma
islerini de kapsar. UI bu modulu `Gelen Fatura -> Manav Mal Kabul -> Tartim/Etiket -> Mikro Kontrol -> Rapor`
seklinde uctan uca operasyon ekrani olarak tasarlamalidir.

Ana route:

```text
api/kasa-islemleri/manav-mal-kabul-etiket
```

Yeni UI ve backend icin tek route budur.

Modul, manav/depo mal kabul etiketi icin su isleri kapsar:

- Tedarikci arama
- MNV stok arama
- Gelen fatura veya manuel belge bilgisinden fiyatli mal kabul satiri hazirlama
- Net kilo, kasa darasi, ortalama kilo ve etiket barkodu hesaplama
- `Furpa.dbo.Manav_Depo_Mal_Kabul_Etiket` kabul kayitlarini listeleme/ekleme/guncelleme/silme
- Etiket yazdirma icin label datasini dondurme
- Alinan urunler, 56 Manav Depo stok raporu ve canli Mikro manav mal kabul belgelerini okuma
- Etiket tartimi ile Mikro mal kabul hareketlerini tarih/cari/stok bazinda karsilastirma
- Fiyatli/onayli satirlarla Mikro manav mal kabul belgesi olusturmak

## UI Is Akisi

Bu modul tek ekranda tabli veya bolmeli tasarlanabilir:

| Bolum | Amac | Endpointler |
|---|---|---|
| `Fatura/Mal Kabul` | Gelen fatura secimi, tedarikci, tarih, belge no, fiyat, KDV ve Mikro aktarim onayi | `/incoming-invoices`, `/suppliers`, `/stocks`, `POST /micro/goods-receipts`, `GET /micro/goods-receipts` |
| `Tartim ve Etiket` | Brut kg, kasa darasi, kasa sayisi, net kg, ortalama kg ve etiket basimi | `POST /acceptance-records/calculate`, `/acceptance-records`, `/labels/preview`, `/acceptance-records/{id}/label` |
| `Kontrol` | Furpa etiket kaydi ile Mikro mal kabul miktarlarini karsilastirma | `GET /micro/goods-receipts/comparison` |
| `Raporlar` | Alinan urun/fatura farki ve 56 Manav Depo stok durumu | `/reports/received-products`, `/reports/depot-stock` |

Gunluk onerilen akis:

1. Tarih ve tedarikci secilir.
2. Gelen fatura cache'inden fatura secilir veya manuel belge bilgisi girilir; satirlar stok, miktar, fiyat ve KDV ile hazirlanir.
3. Tartim yapildikca `calculate` ile net kg ve ortalama kg hesaplanir.
4. Onaylanan tartim satiri `POST /acceptance-records` ile Furpa etiket kaydina donusur.
5. Etiket icin `GET /acceptance-records/{id}/label` veya kaydetmeden once `POST /labels/preview` kullanilir.
6. Fiyatli ve kesinlesmis satirlar `POST /micro/goods-receipts` ile Mikro'ya aktarilir.
7. Aktarimdan sonra `GET /micro/goods-receipts` ve `GET /micro/goods-receipts/comparison` yenilenir.
8. Gun sonunda `reports/received-products` ve `reports/depot-stock` ile fark/stok kontrolu yapilir.

Durum modeli onerisi:

- `Taslak`: satir UI'da hazirlaniyor.
- `Etiket Kaydi`: Furpa kabul kaydi var, `microTransferred=false`.
- `Etiket Basildi`: baski UI/terminal tarafinda tamamlandi; API su an ayri baski log'u tutmaz.
- `Mikro Aktarildi`: `microTransferred=true` veya Mikro belge listesinde satir gorunuyor.
- `Fark Var`: comparison status `FARKLI`, `SADECE_ETIKET` veya `SADECE_MIKRO`.
- `Tamam`: comparison status `ESLESTI` veya operasyonel olarak kabul edilen `YAKIN`.

Kritik tasarim notlari:

- Etiket kaydi ile Mikro fatura/mal kabul kaydi ayridir; biri digerinin otomatik sonucu gibi varsayilmamalidir.
- Mikro aktarimi fiyat ve KDV netlesmeden acilmamalidir.
- Ayni stoktan birden fazla tartim olabilir; UI satirlari sadece stok koduna gore ezmemelidir.
- `acceptanceRecordId` Mikro aktarim satirina tasinirse basarili aktarimdan sonra ilgili Furpa kaydi `Mikro_Aktarildi=1` olur.
- Timeout veya belirsiz aktarim durumunda UI yeni seri/sira ile tekrar denemeden once Mikro belge listesini ve karsilastirmayi yenilemelidir.

## Yetkiler

```text
kasa-islemleri.manav-mal-kabul-etiket.list
kasa-islemleri.manav-mal-kabul-etiket.detail
kasa-islemleri.manav-mal-kabul-etiket.create
kasa-islemleri.manav-mal-kabul-etiket.update
kasa-islemleri.manav-mal-kabul-etiket.delete
kasa-islemleri.manav-mal-kabul-etiket.transfer
kasa-islemleri.manav-mal-kabul-etiket.all-warehouses
```

Permission katalogu startup senkronizasyonu aciksa bu yetkileri otomatik
ekler. Ayrica migration istenirse auth katalog migration'i uretilebilir.

## Referans Arama

Tedarikci arama:

```http
GET /api/kasa-islemleri/manav-mal-kabul-etiket/suppliers?query=ABC&take=20
```

Kurallar:

- `query` en az 2 karakter olmalidir.
- Eski uygulamadaki cari kod prefix haricleri korunur:
  `8888`, `1999`, `2012`, `4690`, `1998`, `2022`, `120.MY`.
- Response `supplierCode`, `supplierName`, `supplierTitle2` ve `supplierTaxNo` alanlarini dondurur.

Stok arama:

```http
GET /api/kasa-islemleri/manav-mal-kabul-etiket/stocks?query=DOMATES&prefix=MNV&take=20
GET /api/kasa-islemleri/manav-mal-kabul-etiket/stocks/{stockCode}
GET /api/kasa-islemleri/manav-mal-kabul-etiket/stocks/by-name?name=MNV DOMATES
```

Kurallar:

- Varsayilan stok prefix'i `MNV`.
- `query` stok adi, stok kodu veya barkod icinde aranir.
- `*` karakteri SQL wildcard gibi `%` davranisina cevrilir.
- Barkod secimi `BARKOD_TANIMLARI` icinden master/birim onceligiyle yapilir.
- Response `stockCode`, `stockName`, `barcode`, `unitName`, `modelCode` ve `wholesaleTaxPointer` alanlarini dondurur.

## Gelen Fatura Cache

Manav ekrani gelen fatura baslik/ozet bilgisini Auth DB'deki Uyumsoft inbox cache'inden okuyabilir.

```http
GET /api/kasa-islemleri/manav-mal-kabul-etiket/incoming-invoices?startDate=2026-08-13&endDate=2026-08-13&supplierCode=32000297&take=100
```

Query:

```text
startDate        opsiyonel; bos ise endDate - 7 gun
endDate          opsiyonel; bos ise bugun
supplierCode     opsiyonel; verilirse Mikro cari kartinin VKN/TCKN veya unvaniyla gelen faturalar daraltilir
searchText       opsiyonel; fatura no, documentId, tedarikci unvani, VKN/TCKN, irsaliye no veya siparis belge no icinde arar
includeArchived  opsiyonel; default false
take             opsiyonel; default 100, max 500
```

Response item:

```json
{
  "documentId": "9f4c0c1a-...",
  "invoiceId": "GIB2026000012345",
  "supplierTitle": "HAL TEDARIKCI A",
  "supplierTaxNo": "1234567890",
  "createDate": "2026-08-13T08:10:00",
  "invoiceDate": "2026-08-13T00:00:00",
  "invoiceType": "SATIS",
  "invoiceTotal": 25004.0,
  "taxExclusiveAmount": 24753.96,
  "taxTotal": 250.04,
  "despatchId": "IRS2026000099",
  "isProcessed": false,
  "isPrinted": false,
  "isStandard": true,
  "statusCode": "ACCEPTED",
  "status": "Kabul edildi",
  "message": "",
  "documentCurrencyCode": "TRY",
  "exchangeRate": 1.0,
  "orderDocumentId": "",
  "isArchived": false,
  "invoiceTipType": "Temel Fatura",
  "invoiceTipTypeCode": 0,
  "isSeen": true,
  "lastSynchronizedAtUtc": "2026-08-13T05:15:00Z",
  "matchedSupplierCode": "32000297",
  "matchedSupplierName": "HAL TEDARIKCI A",
  "canStartAcceptance": true
}
```

Not:

- Bu endpoint fatura baslik/ozet bilgisini dondurur; Uyumsoft cache manav tartim/urun satirlarini tasimaz.
- `matchedSupplierCode` VKN/TCKN eslesmesiyle dolarsa UI tedarikci alanini otomatik onerebilir.
- `canStartAcceptance=false` ise UI manuel tedarikci secimine izin verebilir.

## Hesaplama

```http
POST /api/kasa-islemleri/manav-mal-kabul-etiket/acceptance-records/calculate
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
GET /api/kasa-islemleri/manav-mal-kabul-etiket/acceptance-records?date=2026-07-30
```

Tek kayit:

```http
GET /api/kasa-islemleri/manav-mal-kabul-etiket/acceptance-records/{id}
```

Yeni kayit:

```http
POST /api/kasa-islemleri/manav-mal-kabul-etiket/acceptance-records
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
PUT /api/kasa-islemleri/manav-mal-kabul-etiket/acceptance-records/{id}
```

Silme:

```http
DELETE /api/kasa-islemleri/manav-mal-kabul-etiket/acceptance-records/{id}
```

## Etiket Data

Kayittan etiket:

```http
GET /api/kasa-islemleri/manav-mal-kabul-etiket/acceptance-records/{id}/label
```

Kaydetmeden etiket preview:

```http
POST /api/kasa-islemleri/manav-mal-kabul-etiket/labels/preview
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
GET /api/kasa-islemleri/manav-mal-kabul-etiket/reports/received-products?date=2026-07-30
```

Depo son stok:

```http
GET /api/kasa-islemleri/manav-mal-kabul-etiket/reports/depot-stock?warehouseNo=56&date=2026-07-30
```

Depo stok raporunda varsayilan depo `56` olur. Kullanici farkli depo isterse
standart depo yetkisi/all-warehouses kontrolu uygulanir.

Alinan urunler raporu response item:

```json
{
  "supplierCode": "32000297",
  "supplierName": "TEDARIKCI A",
  "stockCode": "MNV001",
  "barcode": "1234567",
  "stockName": "MNV DOMATES",
  "labelRowCount": 2,
  "documentSeries": "MNV26",
  "documentNo": "10, 11",
  "seriesAndNumber": "MNV26 - 10, 11",
  "grossWeight": 100.0,
  "caseTotalTare": 12.0,
  "palletTare": 5.0,
  "caseCount": 10,
  "netReceivedWeight": 83.0,
  "invoiceQuantity": 80.0,
  "invoiceDifference": -3.0,
  "microRowCount": 1,
  "microAmount": 2800.0,
  "microDocument": "EFT261/2014",
  "status": "FARKLI",
  "unitName": "KG"
}
```

Depo stok raporu response item:

```json
{
  "stockCode": "MNV001",
  "stockName": "MNV DOMATES",
  "responsible": "SATINALMA SORUMLUSU",
  "currentStock": 125.5,
  "purchasePriceWithVat": 18.75,
  "salesPrice": 24.9,
  "barcode": "1234567",
  "unitName": "KG",
  "modelCode": "10"
}
```

Rapor notlari:

- `reports/received-products` Furpa tartim/etiket kayitlarini canli Mikro manav mal kabul miktarlariyla ayni satirda karsilastirir.
- `status` degeri `ESLESTI`, `YAKIN`, `FARKLI`, `SADECE_ETIKET` veya `SADECE_MIKRO` olabilir.
- Liste farki buyuk satirlari once dondurur; UI gun sonu kontrolunde ilk bakilacak satirlari ustte gosterebilir.
- `reports/depot-stock` barkod, birim ve model kodunu dondurur; UI stok secimi, etiket veya kabul ekranina geciste bu alanlari kullanabilir.

## Canli Mikro Manav Mal Kabul Belgeleri

Canli 2026 akisinda manav mal kabul belgeleri Mikro'da normal depo mal kabul
belgesi gibi degil, su `STOK_HAREKETLERI` formatiyla gorunur:

```text
sth_tip = 0
sth_cins = 16
sth_evraktip = 3
sth_normal_iade = 0
sth_giris_depo_no = 56
sth_cikis_depo_no = 1
STOKLAR.sto_isim LIKE 'MNV%'
```

Belge listeleme:

```http
GET /api/kasa-islemleri/manav-mal-kabul-etiket/micro/goods-receipts?date=2026-08-13
GET /api/kasa-islemleri/manav-mal-kabul-etiket/micro/goods-receipts?date=2026-08-13&supplierCode=32000297
```

Yetki: `kasa-islemleri.manav-mal-kabul-etiket.list`

Response belge bazlidir; her belgenin satirlari ayrica gelir:

```json
[
  {
    "date": "2026-08-13T00:00:00",
    "documentSeries": "EFT261",
    "documentOrderNo": 2014,
    "seriesAndNumber": "EFT261/2014",
    "supplierCode": "32000297",
    "supplierName": "TEDARIKCI A",
    "createUserNo": 15,
    "lineCount": 2,
    "totalQuantity": 1427.0,
    "totalAmount": 49945.0,
    "totalTax": 0.0,
    "firstCreatedAt": "2026-08-13T11:52:56.557",
    "lastCreatedAt": "2026-08-13T11:52:56.557",
    "lines": [
      {
        "lineNo": 0,
        "stockCode": "001082",
        "stockName": "MNV URUN",
        "quantity": 714.0,
        "unitPrice": 35.0,
        "amount": 24990.0,
        "taxAmount": 0.0,
        "taxPointer": 0,
        "inWarehouseNo": 56,
        "outWarehouseNo": 1
      }
    ]
  }
]
```

## Etiket - Mikro Mal Kabul Karsilastirma

Etiket kayitlari ile canli Mikro manav mal kabul hareketleri ayni gun, cari ve
stok bazinda karsilastirilir:

```http
GET /api/kasa-islemleri/manav-mal-kabul-etiket/micro/goods-receipts/comparison?date=2026-08-13
GET /api/kasa-islemleri/manav-mal-kabul-etiket/micro/goods-receipts/comparison?date=2026-08-13&supplierCode=32000297
```

Yetki: `kasa-islemleri.manav-mal-kabul-etiket.list`

Response:

```json
[
  {
    "date": "2026-08-13T00:00:00",
    "supplierCode": "32000297",
    "supplierName": "TEDARIKCI A",
    "stockCode": "001082",
    "stockName": "MNV URUN",
    "labelRowCount": 1,
    "labelNetWeight": 714.4,
    "microRowCount": 2,
    "microQuantity": 1427.0,
    "difference": 712.6,
    "microAmount": 49945.0,
    "microDocument": "EFT261/2014, EFT261/2014",
    "status": "FARKLI"
  }
]
```

Status degerleri:

- `ESLESTI`: fark 0.01 veya altinda
- `YAKIN`: fark 2 birim veya altinda
- `FARKLI`: iki tarafta da kayit var ama fark buyuk
- `SADECE_ETIKET`: tartim/etiket var, Mikro mal kabul yok
- `SADECE_MIKRO`: Mikro mal kabul var, etiket kaydi yok

`reports/received-products` endpointindeki `invoiceQuantity` alani da artik
bu canli manav mal kabul formatindan okunur; genel stok hareketleriyle
karismasinin onune gecilir.

## Mikro Mal Kabul Olusturma

Canli Mikro'ya manav mal kabul belgesi yazar:

```http
POST /api/kasa-islemleri/manav-mal-kabul-etiket/micro/goods-receipts
```

Body:

```json
{
  "date": "2026-08-13",
  "supplierCode": "32000297",
  "documentSeries": "MNV26",
  "documentOrderNo": null,
  "documentNo": null,
  "mikroUserNo": 15,
  "description": "Manav mal kabul",
  "markAcceptanceRecordsTransferred": true,
  "lines": [
    {
      "acceptanceRecordId": 12345,
      "stockCode": "001082",
      "quantity": 714.4,
      "unitPrice": 35.0,
      "unitPointer": 1,
      "taxPointer": 3,
      "taxRatePercent": 1,
      "taxAmount": null,
      "description": "Domates"
    }
  ]
}
```

Alan kurallari:

- `date` ve `supplierCode` zorunludur.
- `documentSeries` bos gelirse `MNV` kullanilir.
- `documentOrderNo` bos gelirse ayni seri icin canli Mikro'daki son siranin
  bir fazlasi uretilir.
- `documentNo` bos gelirse `documentOrderNo` metni kullanilir.
- `mikroUserNo` bos gelirse proje default'u `39` kullanilir; UI canli eski
  kullanici numarasini biliyorsa gonderebilir.
- `lines[]` zorunludur; fiyat bilinmeden Mikro belgesi olusturulmaz.
- `acceptanceRecordId` verilirse basarili yazmadan sonra ilgili Furpa etiket
  kaydi `Mikro_Aktarildi = 1` yapilir.
- `taxAmount` verilirse aynen kullanilir; verilmezse `taxRatePercent` varsa
  `quantity * unitPrice * taxRatePercent / 100` hesaplanir; ikisi de yoksa
  KDV tutari `0` olur.
- `taxPointer` verilmezse stok kartindaki `sto_toptan_vergi` kullanilir.

Mikro'ya yazilan canli format:

```text
Once CARI_HESAP_HAREKETLERI fatura/cari hareket basligi acilir.
STOK_HAREKETLERI satirlari bu basliga sth_fat_uid = cha_Guid ile baglanir.

CARI_HESAP_HAREKETLERI
cha_fileid = 51
cha_tip = 1
cha_cinsi = 35
cha_evrak_tip = 0
cha_normal_Iade = 0
cha_cari_cins = 0
cha_kod = supplierCode
cha_ciro_cari_kodu = supplierCode
cha_srmrkkodu = 56
cha_ebelge_turu = 7
cha_fatura_belge_turu = 0
cha_meblag = satir toplam + KDV
cha_aratoplam = satir toplam
cha_vergi1..20 = satirlardaki vergi pointer toplamlarina gore

STOK_HAREKETLERI
sth_tip = 0
sth_cins = 16
sth_normal_iade = 0
sth_evraktip = 3
sth_giris_depo_no = 56
sth_cikis_depo_no = 1
sth_fiyat_liste_no = -1
sth_fileid = 16
sth_fat_uid = CARI_HESAP_HAREKETLERI.cha_Guid
sth_eticaret_kanal_kodu = FRMNV{yyMMdd}{hash}
```

Notlar:

- Alternatif doviz kuru Mikro `fn_KurBul(date, fn_FirmaAlternatifDovizCinsi(), 1)` fonksiyonundan okunur.
- `offlineTraceKey` Mikro kolon uzunluguna uygun kisa izdir; `FRMNV{yyMMdd}{hash}` formatinda uretilir ve hem cari baslikta hem stok satirlarinda saklanir.
- UI bu endpointi sadece kullanici mal kabul/fatura kontrolunu bitirdikten sonra cagirir; etiket/tartim kaydi kendi basina Mikro mal kabul anlamina gelmez.

Duplicate korumasi:

- Ayni `date + documentSeries + documentOrderNo + tip/cins/evraktip + depo`
  kombinasyonunda daha once hareket varsa API `409 Conflict` doner.
- Evrak sira uretimi ve insert ayni Mikro transaction'i icinde
  `Serializable` izolasyonla yapilir.

Response:

```json
{
  "date": "2026-08-13T00:00:00",
  "documentSeries": "MNV26",
  "documentOrderNo": 10,
  "seriesAndNumber": "MNV26/10",
  "supplierCode": "32000297",
  "createUserNo": 15,
  "lineCount": 1,
  "totalQuantity": 714.4,
  "totalAmount": 25004.0,
  "totalTax": 250.04,
  "updatedAcceptanceRecordCount": 1,
  "offlineTraceKey": "FRMNV260813A1B2C3D4",
  "lines": [
    {
      "lineNo": 0,
      "stockCode": "001082",
      "stockName": "MNV URUN",
      "quantity": 714.4,
      "unitPrice": 35.0,
      "amount": 25004.0,
      "taxAmount": 250.04,
      "taxPointer": 3,
      "inWarehouseNo": 56,
      "outWarehouseNo": 1,
      "movementGuid": "49f26b26-9f37-4d64-98e7-1e2f7a5e2d41",
      "barcode": null,
      "unitName": null,
      "description": "Domates"
    }
  ]
}
```

`GET /micro/goods-receipts` ile okunan mevcut Mikro belgelerinde document seviyesinde ayrica
`documentNo`, `invoiceGuid`, `offlineTraceKey`; satir seviyesinde `movementGuid`, `barcode`,
`unitName` ve `description` alanlari dolu gelir. `POST /micro/goods-receipts` response'unda
yeni yazilan hareketin `movementGuid` degeri hemen doner; barkod/birim gerekiyorsa UI belgeyi
yeniden okuyarak dolu Mikro detayini alabilir.
