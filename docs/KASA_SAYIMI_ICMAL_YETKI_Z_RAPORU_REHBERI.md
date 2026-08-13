# Kasa Sayimi, Icmal Girisi, Yetki ve Z Raporu Rehberi

Bu dokuman `Kasa Sayimlari` ve `Icmal Kaydi Girisi` ekranlarinin son
tasarimini, backend yetki mantigini, UI entegrasyon kurallarini, Mikro DB
yazim davranisini ve canli kontrol adimlarini anlatir.

Ana hedef:

- Icmal kaydi olusturma ile mevcut kasa sayimi kayitlarini listeleme/duzenleme/silme ayrilsin.
- Yetkisi olmayan kullanici kendi subesi disindaki kaydi gormesin, duzenlemesin, silmesin.
- `all-warehouses` yetkisi olan kullanici listeledigi baska sube kaydinin detayina girip islem yapabilsin.
- UI genelde oturum deposunu veya bos depo gonderse bile backend belge serisinden hedef subeyi cozebilsin.
- Z rapor tutari manuel girildiginde eski sistemdeki gibi Mikro CARI hareketlerine yansisin.

## Kisa Ozet

Iki ekran farkli sorumluluk tasir:

```text
Icmal Kaydi Girisi:
  Yeni kasa sayimi/icmal kaydi olusturur.
  Duzenle ve sil burada beklenmez.

Kasa Sayimlari:
  Mevcut kayitlari listeler.
  Detay acar.
  Yetkiye gore secili kaydi duzenler veya siler.
```

Guncel yetkiler:

```text
kasa-islemleri.icmal-kaydi-girisi.page
kasa-islemleri.icmal-kaydi-girisi.list
kasa-islemleri.icmal-kaydi-girisi.create
kasa-islemleri.icmal-kaydi-girisi.all-warehouses

kasa-islemleri.kasa-sayimlari.page
kasa-islemleri.kasa-sayimlari.list
kasa-islemleri.kasa-sayimlari.detail
kasa-islemleri.kasa-sayimlari.update
kasa-islemleri.kasa-sayimlari.delete
kasa-islemleri.kasa-sayimlari.all-warehouses
```

En kritik davranis:

```text
GET/PUT/DELETE .../kasa-sayimlari/F116.57/1456?warehouseNo=1

Kullanicida kasa-sayimlari.all-warehouses varsa:
  backend hedef subeyi documentSerie icinden cozer: F116.57 -> 116
  query'deki warehouseNo=1 oturum/islem deposu gibi gelse bile hedef belge subesi sayilmaz

Kullanicida kasa-sayimlari.all-warehouses yoksa:
  backend eski guvenli davranisi korur
  istek kendi JWT deposu disina cikamaz
```

## Kaynak Dosyalar

Backend:

```text
src/FurpaMerkezApi.Application/Security/PermissionCatalog.cs
src/FurpaMerkezApi.WebApi/Controllers/Modules/KasaIslemleri/KasaSayimlari/KasaSayimlariController.cs
src/FurpaMerkezApi.Infrastructure/Modules/KasaIslemleri/KasaSayimlari/Commands/CashSummaryCommandsUseCase.cs
src/FurpaMerkezApi.Infrastructure/Modules/KasaIslemleri/KasaSayimlari/Commands/CashSummaryCustomerMovementFactory.cs
```

Migration:

```text
src/FurpaMerkezApi.Infrastructure/Migrations/20260813071013_MoveCashSummaryEditDeletePermissionsToList.cs
src/FurpaMerkezApi.Infrastructure/Migrations/20260813071013_MoveCashSummaryEditDeletePermissionsToList.Designer.cs
src/FurpaMerkezApi.Infrastructure/Migrations/AuthDbContextModelSnapshot.cs
```

Testler:

```text
tests/FurpaMerkezApi.WebApi.Tests/Modules/KasaIslemleri/KasaSayimlari/KasaSayimlariPermissionTests.cs
tests/FurpaMerkezApi.Infrastructure.Tests/Modules/KasaIslemleri/KasaSayimlari/CashSummaryCommandsUseCaseCustomerMovementTests.cs
```

Ilgili genel dokumanlar:

```text
docs/UI_API_DOKUMANI.md
docs/PROJE_GENEL_ISLEYISI.md
docs/YENI_MENU_YETKI_MIGRATION_REHBERI.md
```

## Menu ve Yetki Ayrimi

### Icmal Kaydi Girisi

Bu ekran sadece yeni kayit girisi icindir.

| Yetki | Anlam |
| --- | --- |
| `kasa-islemleri.icmal-kaydi-girisi.page` | UI route/menu gorunurlugu |
| `kasa-islemleri.icmal-kaydi-girisi.list` | Create form lookup verileri, kasalar, kasiyerler, odeme tipleri, Z rapor toplam lookup |
| `kasa-islemleri.icmal-kaydi-girisi.create` | Yeni icmal/kasa sayimi kaydi olusturma |
| `kasa-islemleri.icmal-kaydi-girisi.all-warehouses` | Create sirasinda kullanicinin hedef sube secmesine izin verir |

Bu ekranda `update` ve `delete` yoktur.

### Kasa Sayimlari

Bu ekran mevcut kayitlarin operasyon ekranidir.

| Yetki | Anlam |
| --- | --- |
| `kasa-islemleri.kasa-sayimlari.page` | UI route/menu gorunurlugu |
| `kasa-islemleri.kasa-sayimlari.list` | Kasa sayimi listesi ve raporu |
| `kasa-islemleri.kasa-sayimlari.detail` | Secili belge detaylari, banknot ve hediye ceki detaylari |
| `kasa-islemleri.kasa-sayimlari.update` | Secili kaydin detay veya banknot hareketini guncelleme |
| `kasa-islemleri.kasa-sayimlari.delete` | Secili kaydi silme |
| `kasa-islemleri.kasa-sayimlari.all-warehouses` | Liste/detay/update/delete tarafinda baska sube kayitlarina yetkili erisim |

## Endpoint Matrisi

Base route:

```text
/api/kasa-islemleri/kasa-sayimlari
```

### Kasa Sayimlari Liste/Rapor

| Method | Endpoint | Policy | Depo davranisi |
| --- | --- | --- | --- |
| `GET` | `/api/kasa-islemleri/kasa-sayimlari?dateToGet=2026-08-08&warehouseNo=1` | `kasa-sayimlari.list` | Liste icin ilgili list policy uzerinden depo kapsami uygulanir |
| `GET` | `/api/kasa-islemleri/kasa-sayimlari/rapor?dateToGet=2026-08-08&warehouseNo=1` | `kasa-sayimlari.list` | Rapor icin ilgili list policy uzerinden depo kapsami uygulanir |

UI notu:

- Kullanicida `kasa-islemleri.kasa-sayimlari.all-warehouses` varsa depo filtresi acilabilir.
- Kullanicida bu yetki yoksa UI depo filtresini gizlemeli veya kilitlemelidir.
- Yetki yokken farkli depo gonderilirse backend `403 Forbidden` doner.

### Kasa Sayimlari Detay

| Method | Endpoint | Policy | Depo davranisi |
| --- | --- | --- | --- |
| `GET` | `/api/kasa-islemleri/kasa-sayimlari/{documentSerie}/{documentOrderNo}` | `kasa-sayimlari.detail` | `documentSerie` icinden sube cozulur; all yetkisi varsa o subeye erisir |
| `GET` | `/api/kasa-islemleri/kasa-sayimlari/{documentSerie}/{documentOrderNo}/detaylar` | `kasa-sayimlari.detail` | Aynidir |
| `GET` | `/api/kasa-islemleri/kasa-sayimlari/{documentSerie}/{documentOrderNo}/banknot-hareketleri` | `kasa-sayimlari.detail` | Aynidir |
| `GET` | `/api/kasa-islemleri/kasa-sayimlari/{documentSerie}/{documentOrderNo}/hediye-ceki-hareketleri` | `kasa-sayimlari.detail` | Aynidir |

Ornek:

```text
GET /api/kasa-islemleri/kasa-sayimlari/F116.57/1456/detaylar
GET /api/kasa-islemleri/kasa-sayimlari/F116.57/1456/detaylar?warehouseNo=1
```

Kullanicida `kasa-islemleri.kasa-sayimlari.all-warehouses` varsa iki ornekte de hedef belge subesi `116` kabul edilir.

### Icmal Kaydi Girisi Lookup Endpointleri

Bu endpointler create formunu besler.

| Method | Endpoint | Policy |
| --- | --- | --- |
| `GET` | `/kasiyerler/ikili` | `icmal-kaydi-girisi.list` |
| `GET` | `/kasalar` | `icmal-kaydi-girisi.list` |
| `GET` | `/kasa-detayi` | `icmal-kaydi-girisi.list` |
| `GET` | `/kasiyerler` | `icmal-kaydi-girisi.list` |
| `GET` | `/banknot-tipleri` | `icmal-kaydi-girisi.list` |
| `GET` | `/hediye-ceki-tipleri` | `icmal-kaydi-girisi.list` |
| `GET` | `/odeme-tipleri/banka` | `icmal-kaydi-girisi.list` |
| `GET` | `/odeme-tipleri/yemek-ceki` | `icmal-kaydi-girisi.list` |
| `GET` | `/odeme-tipleri/online` | `icmal-kaydi-girisi.list` |
| `GET` | `/odeme-tipleri/masraf-pusulasi` | `icmal-kaydi-girisi.list` |
| `GET` | `/odeme-tipleri/magaza-masrafi` | `icmal-kaydi-girisi.list` |
| `GET` | `/online-kasa-detaylari` | `icmal-kaydi-girisi.list` |
| `GET` | `/z-rapor-toplam` | `icmal-kaydi-girisi.list` |

### Icmal Kaydi Olusturma

| Method | Endpoint | Policy | Depo davranisi |
| --- | --- | --- | --- |
| `POST` | `/api/kasa-islemleri/kasa-sayimlari` | `icmal-kaydi-girisi.create` | Yetkiye gore hedef sube cozulur |

Davranis:

| Kullanici durumu | Body `warehouseNo` | Sonuc |
| --- | --- | --- |
| `icmal-kaydi-girisi.all-warehouses` yok | Bos/null | JWT deposuna kaydeder |
| `icmal-kaydi-girisi.all-warehouses` yok | JWT deposu | Kaydeder |
| `icmal-kaydi-girisi.all-warehouses` yok | Baska depo | `403 Forbidden` |
| `icmal-kaydi-girisi.all-warehouses` var | Bos/null | `400 Bad Request` |
| `icmal-kaydi-girisi.all-warehouses` var | Secilen depo | Secilen depoya kaydeder |

Neden all yetkili kullanicida `warehouseNo` zorunlu?

- Bu kullanici birden fazla sube icin kayit acabilir.
- Hedef sube belirsiz kalirsa yanlis subeye Mikro kaydi yazilabilir.
- Bu nedenle UI sube sectirmeli ve body'de `warehouseNo` gondermelidir.

### Kasa Sayimi Guncelleme

| Method | Endpoint | Policy | Aciklama |
| --- | --- | --- | --- |
| `PUT` | `/{documentSerie}/{documentOrderNo}/detaylar` | `kasa-sayimlari.update` | Odeme/detay satirlarini gunceller |
| `PUT` | `/{documentSerie}/{documentOrderNo}/banknot-hareketleri` | `kasa-sayimlari.update` | Banknot hareketlerini gunceller |

Legacy uyum endpointleri:

| Method | Endpoint | Policy |
| --- | --- | --- |
| `POST` | `/UpdateSummaryDetails` | `kasa-sayimlari.update` |
| `POST` | `/UpdateBanknoteMovements` | `kasa-sayimlari.update` |

Not:

- Guncellemede hedef belge subesi `documentSerie` icinden cozulur.
- `all-warehouses` yetkisi olan kullanici baska sube kaydini listeleyip secmis ise update yapabilir.
- Yetki yoksa kullanici kendi JWT deposu disina cikamaz.

### Kasa Sayimi Silme

| Method | Endpoint | Policy |
| --- | --- | --- |
| `DELETE` | `/{documentSerie}/{documentOrderNo}` | `kasa-sayimlari.delete` |
| `POST` | `/DeleteSummary` | `kasa-sayimlari.delete` |

Ornek:

```text
DELETE /api/kasa-islemleri/kasa-sayimlari/F116.57/1456?warehouseNo=1
```

Eger kullanicida `kasa-islemleri.kasa-sayimlari.all-warehouses` ve `kasa-islemleri.kasa-sayimlari.delete` varsa:

```text
documentSerie = F116.57
cozulen hedef sube = 116
warehouseNo=1 yok sayilmaz ama hedef belge subesinin onune gecmez
silme F116.57 / 1456 icin calisir
```

Silme response'u:

```json
{
  "documentSerie": "F116.57",
  "documentOrderNo": 1456,
  "deletedSummaryLineCount": 3,
  "deletedBanknoteLineCount": 10,
  "deletedGiftCheckLineCount": 0,
  "deletedCustomerMovementCount": 3
}
```

Eger response su sekilde donerse:

```json
{
  "documentSerie": "F116.57",
  "documentOrderNo": 1456,
  "deletedSummaryLineCount": 0,
  "deletedBanknoteLineCount": 0,
  "deletedGiftCheckLineCount": 0,
  "deletedCustomerMovementCount": 0
}
```

Bu genelde su anlama gelir:

- Belge o seri/sira ile DB'de yoktur.
- Yanlis DB veya yanlis Mikro connection'a bakiliyordur.
- Servis eski deploy ile calisiyordur.
- `documentSerie` veya `documentOrderNo` yanlistir.
- Kayit daha once silinmistir.

## Depo Cozumleme Kurali

### Temel Claim

Kullanici JWT icinde kendi deposunu tasir:

```text
warehouse_no = 1
```

Backend bu claim'i zorunlu kabul eder.

### all-warehouses Policy Turetme

Backend action permission kodundan all depo yetkisini turetir.

Ornek:

```text
kasa-islemleri.kasa-sayimlari.detail
-> kasa-islemleri.kasa-sayimlari.all-warehouses

kasa-islemleri.icmal-kaydi-girisi.create
-> kasa-islemleri.icmal-kaydi-girisi.all-warehouses
```

### Belge Serisinden Sube Cozumleme

Kasa sayimi belge serisi su formdadir:

```text
F{warehouseNo}.{cashNo}
```

Ornekler:

```text
F1.57    -> warehouseNo = 1, cashNo = 57
F116.57  -> warehouseNo = 116, cashNo = 57
F169.240 -> warehouseNo = 169, cashNo = 240
```

Backend `TryResolveWarehouseNoFromDocumentSerie` ile `F` harfini atar, noktanin solundaki parcayi sube olarak okur.

### Detail/Update/Delete Icin Cozumleme Sirasi

Detail/update/delete gibi var olan belgeye bagli islemlerde:

```text
1. documentSerie icinden warehouseNo cozulur.
2. Kullanici ilgili action icin all-warehouses yetkisine sahipse bu warehouseNo hedef kabul edilir.
3. Seri cozulmezse veya all yetki yoksa request/query warehouseNo denenir.
4. warehouseNo yoksa JWT deposu kullanilir.
5. Yetkisiz farkli depo istenirse 403 doner.
```

Bu davranis sadece `Kasa Sayimlari` belge islemleri icin ozel olarak eklendi.

## UI Davranis Rehberi

### Menu/Route Acma

UI role ismine bakmamali, `permissions` listesine bakmalidir.

```text
Icmal Kaydi Girisi route:
  kasa-islemleri.icmal-kaydi-girisi.page

Kasa Sayimlari route:
  kasa-islemleri.kasa-sayimlari.page
```

### Icmal Kaydi Girisi UI

Form davranisi:

| Durum | UI davranisi |
| --- | --- |
| Kullanici `icmal-kaydi-girisi.all-warehouses` yetkisine sahip degil | Sube secici gizli/kilitli; body `warehouseNo` gonderilmeyebilir |
| Kullanici `icmal-kaydi-girisi.all-warehouses` yetkisine sahip | Sube secici zorunlu; body `warehouseNo` secili sube olmalidir |
| Kullanici `icmal-kaydi-girisi.create` yetkisine sahip degil | Kaydet butonu gorunmemelidir |

Bu ekranda duzenle/sil butonu olmamalidir.

### Kasa Sayimlari UI

Liste davranisi:

| Durum | UI davranisi |
| --- | --- |
| `kasa-sayimlari.all-warehouses` yok | Sadece kendi sube listesi; depo filtresi gizli/kilitli |
| `kasa-sayimlari.all-warehouses` var | Tum sube veya secili sube listeleme acilabilir |

Detay davranisi:

```text
Satir documentSerie = F116.57 ise UI bunu aynen path'e koymalidir.
Backend all yetki varsa subeyi F116.57 icinden cozer.
```

Buton gorunurlugu:

| Buton | Gerekli permission |
| --- | --- |
| Detay | `kasa-islemleri.kasa-sayimlari.detail` |
| Duzenle | `kasa-islemleri.kasa-sayimlari.update` |
| Sil | `kasa-islemleri.kasa-sayimlari.delete` |
| Depo filtresi/secici | `kasa-islemleri.kasa-sayimlari.all-warehouses` |

### UI'nin Yapmamasi Gerekenler

```text
Role name == Admin ise buton ac
Role name == Merkez ise tum depo ac
warehouseNo=1 ise her belgeye erisir
Icmal Kaydi Girisi altinda duzenle/sil koy
All yetkili create'te warehouseNo bos gonder
```

Dogru karar kaynagi her zaman aktif kullanicinin permission listesidir.

## Create Request Ornegi

Tum depo yetkisi olan kullanici sube secerek create yapar.

```http
POST /api/kasa-islemleri/kasa-sayimlari
Content-Type: application/json
```

```json
{
  "cashNo": 57,
  "zReportNo": 31113905,
  "cashierNo": 5140,
  "managerNo": 3343,
  "zTotalValue": 94895.15,
  "total": 94981.35,
  "summaryDate": "2026-08-08",
  "warehouseNo": 1,
  "giftCheckMovements": [],
  "banknoteMovements": [
    { "banknoteType": 1, "quantity": 30, "total": 6000, "value": 200 },
    { "banknoteType": 2, "quantity": 6, "total": 600, "value": 100 }
  ],
  "paymentTypes": [
    {
      "paymentName": "Is Bankasi",
      "paymentTypeNo": 3,
      "accountCode": "0004",
      "terminalId": "S0ML5E03",
      "slipNumber": 8,
      "amountValue": 3126.93
    },
    {
      "paymentName": "Multinet",
      "paymentTypeNo": 54,
      "accountCode": "K.0002",
      "terminalId": "",
      "slipNumber": 15,
      "amountValue": 25955.43
    }
  ],
  "storeExpenses": []
}
```

Response ornegi:

```json
{
  "documentSerie": "F1.57",
  "documentOrderNo": 1,
  "summaryDate": "2026-08-08T00:00:00",
  "warehouseNo": 1,
  "lineCount": 3,
  "total": 94981.35,
  "writeConnectionName": "MikroConnection"
}
```

Response alanlari:

| Alan | Anlam |
| --- | --- |
| `documentSerie` | Mikro belge serisi; `F{warehouseNo}.{cashNo}` |
| `documentOrderNo` | Seri icindeki yeni sira numarasi |
| `summaryDate` | Kasa sayimi tarihi |
| `warehouseNo` | Kaydin yazildigi sube |
| `lineCount` | `Summaries` tablosuna yazilan satir sayisi |
| `total` | Belge toplam tutari |
| `writeConnectionName` | Kullanilan Mikro write connection adi |

## Z Rapor No ve Z Rapor Tutari

Iki farkli alan vardir:

| Alan | Anlam |
| --- | --- |
| `zReportNo` | Z rapor numarasi |
| `zTotalValue` | Z rapor toplam tutari |

`zReportNo` bazen otomatik gelmeyebilir. Manuel kayit icin:

```text
Z rapor no yoksa:
  zReportNo = 0 gonderilebilir

Z rapor tutari biliniyorsa:
  zTotalValue mutlaka gercek tutar olarak gonderilmelidir
```

`zTotalValue = 0` ise backend Z toplam/fark CARI hareketlerini yazmaz. Bu durum genelde Z tutari bilinmiyor veya gercekten 0 kabul ediliyor anlamina gelir.

## Eski Sistemle Karsilastirma

Eski sistem `SummariesController.AddSummary` icinde su mantigi kullaniyordu:

```text
PaymentType 200 -> Banknot/Nakit toplam
PaymentType 300 -> Fark = Total - ZTotalValue, AccountCode = 0002
PaymentType 400 -> Z rapor toplami = ZTotalValue, AccountCode = warehouseNo
```

Sonra `PaymentTypeID` 200/300/400 olan satirlari `Summaries` listesinden siliyordu.

Yani eski sistemde:

```text
Z toplam/fark Summaries tablosunda kalmiyordu.
Z toplam/fark CARI_HESAP_HAREKETLERI tarafina yaziliyordu.
```

Yeni sistemde de ana fikir budur:

```text
Summaries:
  Normal odeme/detay satirlari ve nakit toplam

CARI_HESAP_HAREKETLERI:
  Ana hareket
  Z fark hareketi
  Z toplam hareketi
```

Farkli tutulan kisim:

```text
Eski sistem CARI'de DocumentSerie = X, DocumentOrderNo = 0 yaziyordu.
Yeni sistem CARI'de gercek documentSerie/documentOrderNo yazar.
```

Bu fark bilincli olarak korunur. Nedeni:

- Update/delete islemleri belge anahtariyla daha guvenli calisir.
- `F116.57 / 1456` kaydinin CARI satirlari direkt bulunur.
- Aciklama string'ine bagli silme/guncelleme daha kirilgan oldugu icin tercih edilmez.

## Mikro DB Yazim Mantigi

Create sirasinda yazilan ana tablolar:

```text
Summaries
BanknoteMovements
GiftCheckMovements
CARI_HESAP_HAREKETLERI
```

### Summaries

Buraya yazilanlar:

- Nakit toplam satiri (`PaymentTypeID = 500`, `Description = Nakit Toplam`)
- Banka POS odeme satirlari
- Yemek ceki/online odeme satirlari
- Magaza masrafi satirlari

Buraya yazilmayanlar:

- Z rapor toplam satiri
- Z rapor fark satiri

### BanknoteMovements

Her banknot tipi icin:

```text
DocumentSerie
DocumentOrderNo
SummaryDate
WarehouseNo
CashNo
BanknoteType
Quantity
Total
Value
```

### GiftCheckMovements

Hediye ceki hareketleri varsa belge seri/sira ile yazilir.

### CARI_HESAP_HAREKETLERI

Z raporu ve toplam fark muhasebe hareketleri burada tutulur.

Yeni factory:

```text
CashSummaryCustomerMovementFactory
```

Satirlar:

| cha_satir_no | Tip | Tutar | Kod | Aciklama |
| --- | --- | --- | --- | --- |
| `0` | Borc (`cha_tip = 0`) | `documentTotal` | `KASA-{warehouseNo}` | `Kasa sayimi {seri}/{sira}` |
| `1` | Alacak (`cha_tip = 1`) | `documentTotal - zTotalValue` | `0002` | `Z Rapor Farki {seri}/{sira}` |
| `2` | Alacak (`cha_tip = 1`) | `zTotalValue` | `{warehouseNo}` | `Z Rapor Toplami {seri}/{sira}` |

Ortak CARI alanlari:

| Alan | Deger |
| --- | --- |
| `cha_fileid` | `51` |
| `cha_evrak_tip` | `60` |
| `cha_evrakno_seri` | Gercek `documentSerie` |
| `cha_evrakno_sira` | Gercek `documentOrderNo` |
| `cha_cinsi` | `5` |
| `cha_fatura_belge_turu` | `3` |
| `cha_diger_belge_adi` | `Z Raporu` |
| `cha_srmrkkodu` | `warehouseNo` |
| `cha_karsisrmrkkodu` | `warehouseNo` |

Ornek:

```text
total       = 94981.35
zTotalValue = 94895.15
fark        = 86.20
```

CARI satirlari:

```text
satir 0 -> KASA-1 -> 94981.35
satir 1 -> 0002   -> 86.20
satir 2 -> 1      -> 94895.15
```

## Guncelleme Mantigi

### Detay Guncelleme

`PUT /{documentSerie}/{documentOrderNo}/detaylar`

Guncellenenler:

- `Summaries` detay satirlari
- Belge toplam tutari
- Ana CARI hareket tutari (`cha_satir_no = 0`)
- Eger Z toplam ve Z fark satirlari varsa Z fark satiri yeniden hesaplanir

Yeniden hesap:

```text
zDifference = yeniDocumentTotal - mevcutZTotalMovement.cha_meblag
```

Z toplam satiri update sirasinda degistirilmez; cunku update detay request'inde yeni `zTotalValue` alani yoktur.

### Banknot Guncelleme

`PUT /{documentSerie}/{documentOrderNo}/banknot-hareketleri`

Guncellenenler:

- `BanknoteMovements`
- Nakit toplam satiri
- Belge toplam tutari
- Ana CARI hareket tutari
- Varsa Z fark satiri

## Silme Mantigi

Silme sirasinda belge seri/sira uzerinden su tablolar temizlenir:

```text
Summaries
BanknoteMovements
GiftCheckMovements
CARI_HESAP_HAREKETLERI
```

CARI satirlari:

```sql
WHERE cha_evrakno_seri = @documentSerie
  AND cha_evrakno_sira = @documentOrderNo
```

Bu nedenle yeni sistemin CARI tarafinda gercek belge seri/sira yazmasi onemlidir.

## Canli DB Kontrol SQL Ornekleri

Belge var mi?

```sql
DECLARE @seri nvarchar(20) = N'F116.57';
DECLARE @sira int = 1456;

SELECT
    DocumentSerie,
    DocumentOrderNo,
    BranchNo,
    CashNo,
    ZReportNo,
    PaymentTypeID,
    SlipNumber,
    Amount,
    Description
FROM Summaries
WHERE DocumentSerie = @seri
  AND DocumentOrderNo = @sira
ORDER BY PaymentTypeID, TerminalId;
```

Banknot hareketleri:

```sql
DECLARE @seri nvarchar(20) = N'F116.57';
DECLARE @sira int = 1456;

SELECT
    DocumentSerie,
    DocumentOrderNo,
    BranchNo,
    CashNo,
    BanknoteTypeID,
    Quantity,
    Total,
    Value
FROM BanknoteMovements
WHERE DocumentSerie = @seri
  AND DocumentOrderNo = @sira
ORDER BY BanknoteTypeID;
```

CARI hareketleri:

```sql
DECLARE @seri nvarchar(20) = N'F116.57';
DECLARE @sira int = 1456;

SELECT
    cha_fileid,
    cha_evrak_tip,
    cha_evrakno_seri,
    cha_evrakno_sira,
    cha_satir_no,
    cha_tip,
    cha_cinsi,
    cha_kod,
    cha_srmrkkodu,
    cha_meblag,
    cha_aratoplam,
    cha_aciklama,
    cha_fatura_belge_turu,
    cha_diger_belge_adi
FROM CARI_HESAP_HAREKETLERI
WHERE cha_evrakno_seri = @seri
  AND cha_evrakno_sira = @sira
ORDER BY cha_satir_no;
```

Silme sonrasi kontrol:

```sql
DECLARE @seri nvarchar(20) = N'F116.57';
DECLARE @sira int = 1456;

SELECT 'Summaries' AS TableName, COUNT(*) AS RowCount
FROM Summaries
WHERE DocumentSerie = @seri AND DocumentOrderNo = @sira
UNION ALL
SELECT 'BanknoteMovements', COUNT(*)
FROM BanknoteMovements
WHERE DocumentSerie = @seri AND DocumentOrderNo = @sira
UNION ALL
SELECT 'GiftCheckMovements', COUNT(*)
FROM GiftCheckMovements
WHERE DocumentSerie = @seri AND DocumentOrderNo = @sira
UNION ALL
SELECT 'CARI_HESAP_HAREKETLERI', COUNT(*)
FROM CARI_HESAP_HAREKETLERI
WHERE cha_evrakno_seri = @seri AND cha_evrakno_sira = @sira;
```

## Sik Karsilasilan Durumlar

### Yetkili kullanici baska sube detayini acamiyor

Kontrol et:

```text
1. Kullanici permissions icinde kasa-islemleri.kasa-sayimlari.detail var mi?
2. Kullanici permissions icinde kasa-islemleri.kasa-sayimlari.all-warehouses var mi?
3. Path'teki documentSerie dogru mu? Ornek F116.57
4. Servis son deploy ile mi calisiyor?
5. Auth DB migration uygulanmis mi?
```

### DELETE 0 satir sildi

Kontrol et:

```text
1. Belge zaten silinmis olabilir.
2. Yanlis seri/sira olabilir.
3. Yanlis Mikro DB connection olabilir.
4. Canli servis eski build ile calisiyor olabilir.
5. Kayit legacy sistemde farkli CARI anahtariyla yazilmis olabilir.
```

DB'de once `Summaries` var mi bak. Yoksa API'nin 0 silmesi dogaldir.

### All yetkili create 400 dondu

Sebep:

```text
Kullanicida icmal-kaydi-girisi.all-warehouses var ama body warehouseNo bos.
```

UI cozum:

```text
Sube secimini zorunlu yap.
Secili subeyi body.warehouseNo olarak gonder.
```

### Yetkisiz kullanici 403 aldi

Bu dogru davranistir.

Ornek:

```text
JWT warehouse_no = 1
Request warehouseNo = 116
Kullanicida ilgili *.all-warehouses yok
Sonuc: 403 Forbidden
```

### Z rapor no yok ama tutar var

Manuel kayitta:

```json
{
  "zReportNo": 0,
  "zTotalValue": 94895.15
}
```

Bu durumda Z rapor no bilinmese bile Z tutari CARI tarafina yazilir.

### Z rapor tutari 0 gonderildi

`zTotalValue = 0` ise Z fark ve Z toplam CARI satirlari yazilmaz.

Bu davranis bilincli:

- Z tutari yoksa yanlis muhasebe hareketi acilmasin.
- Gercek Z tutari sonra belli olacaksa yeni kayit/guncelleme stratejisi ayrica tasarlanmalidir.

## Migration Notlari

Permission katalogu degistigi icin Auth DB migration vardir.

Migration amaci:

```text
icmal-kaydi-girisi.update/delete yetkilerini kaldir
kasa-sayimlari.update/delete yetkilerini ekle
var olan role permission baglarini yeni yetkilere tasi
```

Migration dosyasi:

```text
20260813071013_MoveCashSummaryEditDeletePermissionsToList
```

Canlida dikkat:

```text
1. Kod deploy edilmeli.
2. AuthDbContext migration uygulanmali.
3. Servis restart edilmeli.
4. Kullanici tekrar login olmali veya token yenilenmeli.
```

Token yenilenmezse kullanicinin eski permission claim'leri devam edebilir.

## Test Komutlari

Yetki/controller testleri:

```powershell
dotnet test tests/FurpaMerkezApi.WebApi.Tests/FurpaMerkezApi.WebApi.Tests.csproj -v:minimal --filter KasaSayimlariPermissionTests
```

Z raporu CARI hareket factory testleri:

```powershell
dotnet test tests/FurpaMerkezApi.Infrastructure.Tests/FurpaMerkezApi.Infrastructure.Tests.csproj -v:minimal --filter CashSummaryCommandsUseCaseCustomerMovementTests
```

Genel build:

```powershell
dotnet build FurpaMerkezApi.sln --no-restore -v:minimal
```

Beklenen sonuc:

```text
0 hata
0 uyari
```

## Deploy Sonrasi Kontrol Listesi

1. API process son build ile mi calisiyor?

```powershell
Get-CimInstance Win32_Process |
  Where-Object { $_.CommandLine -like "*FurpaMerkezApi*" } |
  Format-List ProcessId,ExecutablePath,CommandLine
```

2. Dogru config/connection mi kullaniliyor?

```text
appsettings.Production.json
ConnectionStrings
MikroConnection
AuthConnection
```

3. Auth migration uygulanmis mi?

```text
Auth DB __EFMigrationsHistory icinde:
20260813071013_MoveCashSummaryEditDeletePermissionsToList
```

4. Kullanici permission claim'leri yenilenmis mi?

```text
Logout/login yap.
GET /api/auth/me permissions listesini kontrol et.
```

5. Kasa Sayimlari detay testi:

```text
GET /api/kasa-islemleri/kasa-sayimlari/F116.57/1456/detaylar
```

6. Silme testi:

```text
DELETE /api/kasa-islemleri/kasa-sayimlari/F116.57/1456?warehouseNo=1
```

7. DB kontrol:

```text
Summaries count = 0
BanknoteMovements count = 0
GiftCheckMovements count = 0
CARI_HESAP_HAREKETLERI count = 0
```

## Kabul Kriterleri

Bu is tamam kabul edilir, eger:

- Icmal Kaydi Girisi ekrani sadece yeni kayit olusturur.
- Icmal Kaydi Girisi icin update/delete permission yoktur.
- Kasa Sayimlari listesi kayitlari gosterir.
- Kasa Sayimlari detay endpointleri `kasa-sayimlari.detail` ile calisir.
- Kasa Sayimlari update endpointleri `kasa-sayimlari.update` ile calisir.
- Kasa Sayimlari delete endpointleri `kasa-sayimlari.delete` ile calisir.
- Tum depo yetkisi olan kullanici `F116.57` gibi seri icinden 116 subesini cozebilir.
- Tum depo yetkisi olmayan kullanici kendi subesi disina cikamaz.
- All yetkili create isteginde `warehouseNo` zorunludur.
- Z rapor tutari varsa CARI'de 3 satir olusur: ana hareket, fark, Z toplam.
- Z rapor tutari yoksa sadece ana CARI hareket olusur.
- Update islemi ana CARI tutarini ve varsa Z farkini gunceller.
- Delete islemi `Summaries`, `BanknoteMovements`, `GiftCheckMovements`, `CARI_HESAP_HAREKETLERI` satirlarini temizler.
- Testler ve solution build temiz gecer.

## Commit Onerisi

```text
Kasa sayimi yetki ve z raporu rehberini ekle
```
