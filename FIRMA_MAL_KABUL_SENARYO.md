# Firma Mal Kabul Senaryo

Bu dokuman firma mal kabul akisini, Mikro yazim kuralini, UI davranisini ve
AXATA manuel aktarim sinirlarini tek yerde anlatir.

Firma mal kabul tek endpoint ile calisir. Siparisli ve siparissiz mal kabul
ayri ekran veya ayri endpoint degildir; fark satir bazinda `orderGuid` dolu
olup olmamasina gore belirlenir.

## Kisa Ozet

- Firma mal kabul, tedarikciden gelen urun icin yeni stok giris hareketi yazar.
- Depo mal kabulden farklidir; depo mal kabul mevcut sevk hareketini kabul
  eder, firma mal kabul yeni `STOK_HAREKETLERI` satiri olusturur.
- Irsaliye/resmi gelen miktar `dispatchQuantity` alanidir.
- Fiili sayilan ve depoda kalacak net miktar `acceptedQuantity` alanidir.
- `quantity` eski uyumluluk alanidir. Yeni UI `dispatchQuantity` ve
  `acceptedQuantity` alanlarini ayri gondermelidir.
- `acceptedQuantity < dispatchQuantity` ise fark firma iadesidir.
- Varsayilan hedef davranis: fark icin otomatik firma iade evragi olusturulur,
  ama e-irsaliye otomatik gonderilmez.

Mikro uyumlu net stok kurali:

```text
irsaliye miktari = firma mal kabul girisi
fiili kabul      = depoda kalacak net miktar
iade farki       = irsaliye miktari - fiili kabul
net stok         = firma mal kabul - firma iade
```

Ornek:

```text
dispatchQuantity = 10
acceptedQuantity = 8
returnQuantity   = 2

Mikro yazimi:
  +10 firma mal kabul
  -2 firma iade

Net stok = 8
```

## Ana Endpointler

Liste:

```http
GET /api/mal-kabul-islemleri/firma-mal-kabulleri?WarehouseNo=110&StartDate=2026-04-01&EndDate=2026-04-30
```

Detay:

```http
GET /api/mal-kabul-islemleri/firma-mal-kabulleri/{seri}/{sira}?warehouseNo=110
```

E-irsaliye ETTN cozumleme:

```http
GET /api/mal-kabul-islemleri/firma-mal-kabulleri/e-irsaliye/ettn/{ettn}?warehouseNo=110
```

Mal kabul olusturma:

```http
POST /api/mal-kabul-islemleri/firma-mal-kabulleri
```

Alias:

```http
POST /api/mal-kabul-islemleri/mal-kabuller/firma
```

Offline status:

```http
GET /api/mal-kabul-islemleri/firma-mal-kabulleri/offline-sync/{clientRequestId}
```

Otomatik olusan firma iadesi icin manuel e-irsaliye gonderimi:

```http
POST /api/iade-islemleri/firma-iadeleri/{seri}/{sira}/e-irsaliye
```

## Yetkiler

```text
Liste  -> mal-kabul-islemleri.firma-mal-kabulleri.list
Detay  -> mal-kabul-islemleri.firma-mal-kabulleri.detail
Create -> mal-kabul-islemleri.firma-mal-kabulleri.create
Update -> mal-kabul-islemleri.firma-mal-kabulleri.update
```

Firma iadesi e-irsaliye gonderiminde mevcut iade detay yetkisi kullanilir:

```text
iade-islemleri.firma-iadeleri.detail
```

## Evrak No Kurali

`documentNo` opsiyoneldir. E-belge/e-irsaliye no varsa body icinden gelen
`documentNo` hem belge no hem de Mikro evrak seri/sira kaynagidir.

Tam format:

```text
seri + 9 haneli sayisal sira
```

Tam format kurallari:

- Bosluk iceremez.
- Toplam uzunluk 10-29 karakter araliginda olmalidir.
- Son 9 karakter sadece rakam olmalidir.
- Son 9 karakterden once en az 1 karakterlik seri prefix olmalidir.
- Seri prefix en fazla 20 karakter olabilir.
- `documentSerie` backend tarafinda son 9 hane atilarak uretilir.
- `documentOrderNo` backend tarafinda son 9 hane sayi olarak okunarak uretilir.

Ornek:

```text
documentNo      = ST12026000002395
documentSerie   = ST12026
documentOrderNo = 2395
```

Gecerli ornekler:

```text
ST12026000002395
C682026000003472
FRM2026600059281
OY32026000000162
```

Tam format sayilmayan ornekler:

```text
IRS-000123      -> prefix moduna duser, seri IRS000123 olur
IRS 000000123   -> prefix moduna duser, seri IRS000000123 olur
000000123       -> sadece sayisal oldugu icin prefix kabul edilmez, cari unvanina duser
```

E-belge/e-irsaliye yoksa:

- UI `documentNo` alanini bos gonderebilir.
- Backend cari unvanindan seri/prefix uretir.
- Ayni depo ve seri icin siradaki `documentOrderNo` degerini verir.
- Response'taki `documentNo`, uretilen nihai evrak no olur.

Kullanici veya UI prefix gondermek isterse:

```text
documentNo = ULK
```

Backend bunu tam evrak no degil seri/prefix kabul eder. Prefix olarak
kullanilmasi icin degerin harf icermesi gerekir. Sadece harf-rakam
karakterleri kullanilir, Turkce karakterler ASCII karsiligina cevrilir,
bosluk/noktalama atilir ve seri en fazla 20 karaktere kisaltilir.

Ornek:

```text
request documentNo = ULK
uretilen seri      = ULK
siradaki sira      = 42
response documentNo = ULK000000042
```

Tekrar kontrolu:

- Ayni depoda ayni `documentNo` tekrar kullanilamaz.
- Ayni depoda ayni `documentSerie + documentOrderNo` tekrar kullanilamaz.

## Firma Mal Kabul Icin E-Irsaliye ETTN Cozumleme

Tedarikcinin e-irsaliyesi varsa UI once QR/ETTN bilgisini cozumler. Bu adim
Mikro'ya mal kabul yazmaz; sadece create ekranini resmi e-irsaliye bilgileriyle
on doldurur.

Endpoint:

```http
GET /api/mal-kabul-islemleri/firma-mal-kabulleri/e-irsaliye/ettn/{ettn}?warehouseNo=110
```

Ornek:

```http
GET /api/mal-kabul-islemleri/firma-mal-kabulleri/e-irsaliye/ettn/3fd0e4f4-87a2-43f2-b5ca-f2a4fd778111?warehouseNo=110
```

Bu endpoint'in amaci:

- Uyumsoft gelen e-irsaliye kutusunda ETTN/UUID ile resmi irsaliyeyi bulmak.
- Gonderici firma bilgisine gore Mikro cari adaylarini dondurmek.
- E-irsaliye ust bilgilerini create header alanlarina aday yapmak.
- E-irsaliye kalemlerini ic stok kodlariyla eslestirip create satiri adaylari
  uretmek.

On dolum map'i:

```text
primaryCustomerSuggestion.customerCode -> request.customerCode
despatchNumber                         -> request.documentNo
actualDespatchDate ?? issueDate        -> request.movementDate
issueDate                              -> request.documentDate
ettn                                   -> UI referans bilgisi
notes                                  -> UI not paneli veya kisa description adayi
```

`despatchNumber` tam `seri + 9 haneli sira` formatindaysa backend seri/sirayi
bundan cozer. Tam formatta degilse backend bunu harf iceren prefix olarak
degerlendirebilir veya cari unvanindan seri uretebilir. Bu yuzden kayit sonrasi
UI yine response'taki `documentNo`, `documentSerie` ve `documentOrderNo`
alanlarini esas almalidir.

Cari secimi:

- `primaryCustomerSuggestion` doluysa UI bunu varsayilan cari olarak secebilir.
- `primaryCustomerSuggestion` bos ama `suggestedCustomers` doluysa kullaniciya
  cari secim listesi acilir.
- Hic cari adayi yoksa kullanici cariyi manuel secmelidir.
- Cari secilmeden create kaydi gonderilmemelidir.

Satir on dolumu:

```text
lines[].internalStockCode -> request.lines[].stockCode
lines[].quantity          -> request.lines[].dispatchQuantity
lines[].quantity          -> request.lines[].acceptedQuantity varsayilani
lines[].description       -> request.lines[].description
```

Kurallar:

- `isMatched = true` ve `canUseForGoodsAcceptance = true` olan satirlar create
  satirina tek tikla aktarilabilir.
- `isMatched = false` olan satirlar manuel stok eslestirme listesine dusmelidir.
- UI resmi e-irsaliye miktarini `dispatchQuantity` olarak korur.
- UI fiili sayimi `acceptedQuantity` alaninda kullaniciya duzelttirir.
- Normal durumda `acceptedQuantity = dispatchQuantity` onerilir.
- Kullanici eksik kabul girerse backend fark icin otomatik firma iade evragi
  olusturabilir.

Ornek ETTN cozumleme cevabindan create taslagi:

```json
{
  "clientRequestId": "d8d0f3d6-5c62-4c67-b6b7-0f5d76b81b6f",
  "customerCode": "120.01.03106",
  "movementDate": "2026-05-06",
  "documentDate": "2026-05-06",
  "documentNo": "IRS2026000001234",
  "deliverer": "",
  "receiver": "",
  "description": "EIRS IRS2026000001234",
  "allowOrderOverReceiving": false,
  "autoCreateReturnForPartialAcceptance": true,
  "lines": [
    {
      "stockCode": "015792",
      "dispatchQuantity": 12,
      "acceptedQuantity": 12,
      "unitPrice": 0,
      "unitPointer": 1,
      "orderGuid": null,
      "description": "Kolili urun"
    }
  ]
}
```

Bu taslak kullanici tarafindan kontrol edilir. Kullanici fiili sayimda 12 yerine
10 kabul ederse UI sadece `acceptedQuantity` alanini 10 yapar; backend 12 mal
kabul ve 2 firma iade yazar.

E-irsaliye yoksa bu adim atlanir. UI cari secimiyle manuel create ekranini acar
ve `documentNo` bos veya harf iceren prefix olarak gonderilebilir.

## Request Modeli

```json
{
  "clientRequestId": "d8d0f3d6-5c62-4c67-b6b7-0f5d76b81b6f",
  "customerCode": "32004621",
  "movementDate": "2026-04-20",
  "documentDate": "2026-04-20",
  "documentNo": "ST12026000002395",
  "deliverer": "Teslim Eden",
  "receiver": "Teslim Alan",
  "description": "",
  "allowOrderOverReceiving": false,
  "autoCreateReturnForPartialAcceptance": true,
  "lines": [
    {
      "stockCode": "015792",
      "dispatchQuantity": 10,
      "acceptedQuantity": 8,
      "unitPrice": 0,
      "unitPointer": 1,
      "lastConsumingDate": "2026-12-31",
      "orderGuid": "1bb2b4fe-b722-4e67-9d4b-050b6d87e800",
      "description": "",
      "partyCode": "",
      "lotNo": 0,
      "projectCode": "",
      "customerResponsibilityCenter": "",
      "productResponsibilityCenter": ""
    },
    {
      "stockCode": "018888",
      "dispatchQuantity": 3,
      "acceptedQuantity": 3,
      "unitPrice": 0,
      "unitPointer": 1,
      "lastConsumingDate": "2026-11-30",
      "orderGuid": null
    }
  ]
}
```

Alan notlari:

- `warehouseNo` body icinden alinmaz; JWT icindeki kullanici deposu kullanilir.
- `clientRequestId` teknik olarak opsiyoneldir, mobil offline guvenli retry icin
  pratikte zorunludur.
- `movementDate` bos ise bugun kullanilir.
- `documentDate` bos ise `movementDate` kullanilir.
- `documentDate`, `movementDate` degerinden once olamaz.
- `customerCode` zorunludur.
- `lines` bos olamaz.
- `stockCode` zorunludur.
- `dispatchQuantity` sifirdan buyuk olmalidir.
- `acceptedQuantity` sifir olabilir ama negatif olamaz.
- `acceptedQuantity`, `dispatchQuantity` degerinden buyuk olamaz.
- `unitPrice` negatif olamaz.
- `unitPointer` 1-255 araliginda olmalidir.
- `lotNo` negatif olamaz.

Eski uyumluluk:

```text
quantity
```

alani hala desteklenir. Yeni UI bu alani ana alan gibi kullanmamalidir.
Backend map kurali:

```text
dispatchQuantity = DispatchQuantity ?? Quantity ?? AcceptedQuantity ?? 0
acceptedQuantity = AcceptedQuantity ?? Quantity ?? dispatchQuantity
```

Bu yuzden yalnizca `quantity = 10` gonderilirse sistem bunu:

```text
dispatchQuantity = 10
acceptedQuantity = 10
```

olarak yorumlar ve iade farki olusmaz.

## Kismi Kabul ve Otomatik Iade

Mikro tarafinda kismi kabul farkindan otomatik iade irsaliyesi olusturma ayari
vardir:

```text
E_FATURA_TANIMLARI.efp_KismiKabulde_IadeIrs_Olussun
```

Backend tarafinda bu davranis request alanina modellenmistir:

```text
autoCreateReturnForPartialAcceptance
```

Varsayilan hedef deger:

```text
true
```

Kural:

```text
returnQuantity = dispatchQuantity - acceptedQuantity
```

Durumlar:

```text
returnQuantity <= 0
  -> returnStatus = Yok
  -> iade evragi olusmaz

returnQuantity > 0 ve autoCreateReturnForPartialAcceptance = true
  -> firma iade evragi otomatik olusur
  -> returnStatus = IadeOlusturuldu
  -> returnEDespatchStatus = GonderimBekliyor

returnQuantity > 0 ve autoCreateReturnForPartialAcceptance = false
  -> iade evragi otomatik olusmaz
  -> returnStatus = IadeBekliyor
  -> returnEDespatchStatus = Yok
```

Onemli:

```text
Otomatik firma iade evragi olusur.
E-irsaliye otomatik gonderilmez.
```

Kullanici iade evragini kontrol eder ve firma iade ekranindan kendisi
gonderir:

```http
POST /api/iade-islemleri/firma-iadeleri/{seri}/{sira}/e-irsaliye
```

Gonderim body modeli:

```json
{
  "plaque": "16ABC123",
  "driverNameSurname": "Ali Veli",
  "driverTckn": "12345678901"
}
```

Bu bilgiler mal kabul create ekraninda istenmez; kullanici e-irsaliye gonder
aksiyonuna bastiginda firma iade ekraninda alinmalidir.

## Mikro Hareket Yazimi

### Firma Mal Kabul Hareketi

Her kabul satiri icin `STOK_HAREKETLERI` tablosuna yeni giris hareketi yazilir.

Temel kolonlar:

```text
sth_evraktip       = 13
sth_tip            = 0
sth_cins           = 0
sth_normal_iade    = 0
sth_evrakno_seri   = documentNo icinden turetilen seri
sth_evrakno_sira   = documentNo icinden turetilen sira
sth_satirno        = hareket satir no
sth_belge_no       = documentNo
sth_belge_tarih    = documentDate
sth_tarih          = movementDate
sth_cari_kodu      = customerCode
sth_stok_kod       = stockCode
sth_miktar         = dispatchQuantity
sth_birim_pntr     = unitPointer
sth_tutar          = dispatchQuantity * unitPrice
sth_giris_depo_no  = kullanici deposu
sth_cikis_depo_no  = kullanici deposu
sth_sip_uid        = orderGuid varsa orderGuid, yoksa Guid.Empty
```

Operasyonel alanlar:

```text
sth_HareketGrupKodu1 = SKT, dd.MM.yyyy formatinda
sth_HareketGrupKodu2 = deliverer
sth_HareketGrupKodu3 = receiver
sth_aciklama         = satir description, yoksa fis description
sth_parti_kodu       = partyCode
sth_lot_no           = lotNo
sth_proje_kodu       = projectCode
sth_cari_srm_merkezi = customerResponsibilityCenter
sth_stok_srm_merkezi = productResponsibilityCenter
```

### Otomatik Firma Iade Hareketi

`acceptedQuantity < dispatchQuantity` ise ve otomatik iade aciksa ayni database
transaction icinde firma iade hareketi yazilir.

Temel kolonlar:

```text
sth_evraktip       = 1
sth_tip            = 1
sth_cins           = 0
sth_normal_iade    = 1
sth_evrakno_seri   = F{kullaniciDeposu}
sth_evrakno_sira   = siradaki firma iade evrak no
sth_satirno        = iade satir no
sth_belge_no       = bos
sth_belge_tarih    = documentDate
sth_tarih          = movementDate
sth_cari_kodu      = customerCode
sth_stok_kod       = stockCode
sth_miktar         = dispatchQuantity - acceptedQuantity
sth_birim_pntr     = unitPointer
sth_tutar          = returnQuantity * unitPrice
sth_giris_depo_no  = 0
sth_cikis_depo_no  = kullanici deposu
sth_sip_uid        = Guid.Empty
sth_aciklama       = AUTO IADE {malKabulSeri}/{malKabulSira} S{sourceLineNo}
```

Iade evragi sadece Mikro stok/cari hareketi olarak olusur. E-irsaliye
gonderimi otomatik tetiklenmez.

## Siparis Baglantisi

Satirda `orderGuid` doluysa backend ilgili `SIPARISLER` satirini bulur.

Kontroller:

```text
sip_tip         = 1
sip_cins        = 0
sip_depono      = kullanici deposu
sip_musteri_kod = customerCode
sip_stok_kod    = stockCode
sip_iptal       != true
sip_kapat_fl    != true
```

Kalan miktar:

```text
kalan = sip_miktar - sip_teslim_miktar
```

Siparise bagli normal kabulde:

```text
SIPARISLER.sip_teslim_miktar += dispatchQuantity
```

Yani 10 irsaliye, 8 fiili kabul senaryosunda siparis teslim miktari 10 artar;
net stok 8'e otomatik firma iadesiyle duser.

Ayni request icinde ayni `orderGuid` birden fazla satirda kullanilamaz.

## Siparis Kalanindan Fazla Kabul

`dispatchQuantity`, siparis kalanindan buyukse varsayilan olarak islem
engellenir:

```text
allowOrderOverReceiving = false
```

Sonuc:

```text
409 Conflict
```

Yetkili kullanici fazla kabule izin verirse:

```text
allowOrderOverReceiving = true
```

Backend satiri iki normal mal kabul hareketine boler:

```text
kalan kadar siparisli hareket
fazla kisim siparissiz order-overflow hareket
```

Siparis teslim miktari sadece kalan kadar artar. Fazla gelen kisim stok girisine
dahil olur ama siparisi tasirmaz.

## Response Modeli

Ornek response:

```json
{
  "documentSerie": "ST12026",
  "documentOrderNo": 2395,
  "movementDate": "2026-04-20T00:00:00",
  "documentDate": "2026-04-20T00:00:00",
  "documentNo": "ST12026000002395",
  "warehouseNo": 110,
  "customerCode": "32004621",
  "lineCount": 2,
  "totalReceivedQuantity": 13,
  "totalOrderLinkedQuantity": 10,
  "totalOrderlessQuantity": 3,
  "totalOrderOverReceivedQuantity": 0,
  "totalAmount": 0,
  "writeConnectionName": "MikroConnection",
  "totalDispatchQuantity": 13,
  "totalNetAcceptedQuantity": 11,
  "totalReturnedQuantity": 2,
  "autoCreatedReturnLineCount": 1,
  "autoCreatedReturnDocumentSerie": "F110",
  "autoCreatedReturnDocumentOrderNo": 4301,
  "returnEDespatchStatus": "GonderimBekliyor",
  "lines": [
    {
      "movementGuid": "9c2d1f41-6f91-4e70-8e50-53d1e4bc88b0",
      "sourceLineNo": 0,
      "movementLineNo": 0,
      "stockCode": "015792",
      "orderGuid": "1bb2b4fe-b722-4e67-9d4b-050b6d87e800",
      "isOrderLinked": true,
      "receivingMode": "order-linked",
      "requestedQuantity": 10,
      "acceptedQuantity": 10,
      "orderLinkedQuantity": 10,
      "orderlessQuantity": 0,
      "orderRemainingBefore": 10,
      "orderRemainingAfter": 0,
      "dispatchQuantity": 10,
      "physicalAcceptedQuantity": 8,
      "returnQuantity": 2,
      "returnStatus": "IadeOlusturuldu",
      "returnMovementGuid": "1d2c3f41-6f91-4e70-8e50-53d1e4bc88b0",
      "returnDocumentSerie": "F110",
      "returnDocumentOrderNo": 4301,
      "returnEDespatchStatus": "GonderimBekliyor"
    },
    {
      "movementGuid": "1fa42cb0-23e9-41a2-9b56-9a3fb9e2f111",
      "sourceLineNo": 1,
      "movementLineNo": 1,
      "stockCode": "018888",
      "orderGuid": null,
      "isOrderLinked": false,
      "receivingMode": "orderless",
      "requestedQuantity": 3,
      "acceptedQuantity": 3,
      "orderLinkedQuantity": 0,
      "orderlessQuantity": 3,
      "orderRemainingBefore": 0,
      "orderRemainingAfter": 0,
      "dispatchQuantity": 3,
      "physicalAcceptedQuantity": 3,
      "returnQuantity": 0,
      "returnStatus": "Yok",
      "returnMovementGuid": null,
      "returnDocumentSerie": null,
      "returnDocumentOrderNo": null,
      "returnEDespatchStatus": "Yok"
    }
  ]
}
```

Alan anlamlari:

- `lineCount`: olusan normal firma mal kabul hareket sayisi.
- `totalReceivedQuantity`: Mikro'ya yazilan mal kabul giris miktari toplamidir.
- `totalDispatchQuantity`: `totalReceivedQuantity` ile ayni anlamdadir; UI icin
  resmi/irsaliye toplamidir.
- `totalNetAcceptedQuantity`: fiili kabul toplamidir; net kabul/stokta kalacak
  miktar icin kullanilir.
- `totalReturnedQuantity`: iade farki toplamidir.
- `autoCreatedReturnLineCount`: otomatik olusan firma iade hareket satir sayisi.
- `autoCreatedReturnDocumentSerie` ve `autoCreatedReturnDocumentOrderNo`:
  otomatik olusan iade evragi linki icin kullanilir.
- `returnEDespatchStatus = GonderimBekliyor`: e-irsaliye gonderildi anlamina
  gelmez; kullanici gonderimi bekliyor demektir.

Satir alanlarinda dikkat:

- `requestedQuantity`: kaynak satirdaki resmi/irsaliye miktari.
- `acceptedQuantity`: olusan mal kabul hareket miktari. Kismi kabulde bu alan
  fiili kabul degil, Mikro'ya yazilan mal kabul miktaridir.
- `physicalAcceptedQuantity`: fiili/net kabul miktari.
- `dispatchQuantity`: resmi/irsaliye miktari.
- `returnQuantity`: iade edilmesi gereken fark.
- `returnStatus`: `Yok`, `IadeOlusturuldu` veya `IadeBekliyor`.

## Offline Retry Kurali

Mobil offline akista her taslak icin tek bir `clientRequestId` uretilmelidir.

Kurallar:

- Ayni taslak tekrar gonderiliyorsa ayni `clientRequestId` kullanilir.
- Taslak icerigi degistiyse yeni `clientRequestId` uretilir.
- Ayni `clientRequestId` ve ayni payload tekrar gelirse backend ayni sonucu
  dondurmeye calisir.
- Ayni `clientRequestId` ile farkli payload gelirse `409 Conflict` doner.
- Ayni `clientRequestId` halen isleniyorsa `409 Conflict` doner.
- POST cevabi cihaza ulasmadiysa UI once ayni request'i tekrar dener, sonra
  gerekirse offline status endpoint'ini cagirir.

Status endpoint:

```http
GET /api/mal-kabul-islemleri/firma-mal-kabulleri/offline-sync/{clientRequestId}
```

## UI Akisi

Ana ekran:

- Tarih araligi ve depo filtresiyle yapilmis firma mal kabul fisleri listelenir.
- Liste create kaynagi degildir; gecmis fisleri gosterir.
- Kullanici mevcut fisi acarsa detay endpoint'i salt okunur detay icin
  kullanilir.

Yeni mal kabul:

- Kullanici `Yeni Mal Kabul` aksiyonuna basar.
- Kullanici tedarikci e-irsaliyesinin QR/ETTN bilgisini okutursa once
  `Firma Mal Kabul Icin E-Irsaliye ETTN Cozumleme` akisi calisir.
- ETTN cozumleme basarili olursa UI create ekraninin ust bilgilerini
  `primaryCustomerSuggestion`, `despatchNumber`, `issueDate` ve
  `actualDespatchDate` alanlarindan on doldurur.
- ETTN cozumlemede stok eslesmesi bulunan satirlar create satirina aktarilabilir.
- Stok eslesmesi bulunmayan e-irsaliye satirlari manuel eslestirme listesine
  dusmelidir.
- E-irsaliye yoksa veya kullanici ETTN okutmayacaksa UI manuel akisa devam eder.
- Manuel akista cari secimi zorunludur.
- Cari secilmeden satir kaydetme ve `Siparis Bagla` pasif olmalidir.
- E-belge/e-irsaliye no varsa UI `documentNo` alanini bosluksuz `seri + 9 haneli
  sayisal sira` formatinda gonderebilir.
- E-belge/e-irsaliye yoksa UI `documentNo` alanini bos birakabilir veya
  kullanicidan `ULK`, `ABC` gibi kisa bir seri/prefix alabilir.
- UI kayit sonrasi request'teki bos/prefix degeri degil, response'taki
  `documentNo`, `documentSerie` ve `documentOrderNo` alanlarini esas almalidir.
- UI yeni taslakta `clientRequestId` uretip saklamalidir.

Satir girisi:

- UI resmi/irsaliye miktarini `dispatchQuantity` alaninda tutar.
- UI fiili sayilan miktari `acceptedQuantity` alaninda tutar.
- Normal durumda iki alan esit onerilir.
- Eksik kabul varsa UI farki anlik gosterir:

```text
returnQuantity = dispatchQuantity - acceptedQuantity
```

- `acceptedQuantity > dispatchQuantity` UI tarafinda engellenmelidir.
- Otomatik iade varsayilan acik gonderilir:

```json
{
  "autoCreateReturnForPartialAcceptance": true
}
```

Kayit sonrasi:

- UI olusan mal kabul evragi icin `documentSerie` + `documentOrderNo` ile detay
  ekranina gecebilir veya listeyi yenileyebilir.
- `autoCreatedReturnLineCount > 0` ise UI firma iade linkini gostermelidir.
- Iade linki:

```text
autoCreatedReturnDocumentSerie + autoCreatedReturnDocumentOrderNo
```

- Durum `GonderimBekliyor` olarak gosterilir.
- UI e-irsaliye gonderimini otomatik tetiklemez.
- Kullanici iade evragindan manuel e-irsaliye gonderir.

## Siparis Bagla Akisi

Kullanici `Siparis Bagla` dediginde UI secili cariye ait acik verilen firma
siparislerini ister.

Onerilen liste istegi:

```http
GET /api/siparis-islemleri/verilen-firma-siparisleri?WarehouseNo=110&StartDate=2026-04-01&EndDate=2026-04-30&CustomerCode=32004621&OnlyOpen=true
```

Filtreler:

- `CustomerCode`: secili carinin siparislerini getirir.
- `OnlyOpen=true`: teslim miktari siparis miktarindan az olan siparisleri getirir.

Liste ornegi:

```json
{
  "documentSerie": "F110",
  "documentOrderNo": 2841,
  "customerCode": "32004621",
  "totalQuantity": 50,
  "totalDeliveredQuantity": 32,
  "totalRemainingQuantity": 18,
  "isClosed": false
}
```

Kullanici siparis sectiginde detay:

```http
GET /api/siparis-islemleri/verilen-firma-siparisleri/F110/2841?warehouseNo=110
```

Detay satirlarinda `orderGuid` doner. Mal kabul satirini siparise baglamak icin
stok kodu degil, bu `orderGuid` kullanilir.

```json
{
  "lineNo": 0,
  "stockCode": "015792",
  "quantity": 10,
  "deliveredQuantity": 4,
  "remainingQuantity": 6,
  "orderGuid": "1bb2b4fe-b722-4e67-9d4b-050b6d87e800"
}
```

Siparis baglamak zorunlu degildir. Ayni fis icinde siparisli ve siparissiz
satirlar birlikte gidebilir.

## Senaryolar

### Senaryo 1: Siparissiz Tam Kabul

Input:

```text
stockCode = 018888
dispatchQuantity = 3
acceptedQuantity = 3
orderGuid = null
```

Sonuc:

```text
Firma mal kabul:
  sth_miktar = 3
  sth_sip_uid = Guid.Empty

Firma iade:
  olusmaz

Siparis:
  dokunulmaz
```

### Senaryo 2: Siparisli Tam Kabul

Siparis:

```text
sip_miktar = 10
sip_teslim_miktar = 0
kalan = 10
```

Input:

```text
dispatchQuantity = 10
acceptedQuantity = 10
orderGuid = sip_Guid
```

Sonuc:

```text
Firma mal kabul:
  sth_miktar = 10
  sth_sip_uid = sip_Guid

Firma iade:
  olusmaz

Siparis:
  sip_teslim_miktar = 10
```

### Senaryo 3: Siparisli Kismi Teslim, Fark Yok

Siparis:

```text
sip_miktar = 10
sip_teslim_miktar = 0
kalan = 10
```

Input:

```text
dispatchQuantity = 6
acceptedQuantity = 6
orderGuid = sip_Guid
```

Sonuc:

```text
Firma mal kabul:
  sth_miktar = 6
  sth_sip_uid = sip_Guid

Firma iade:
  olusmaz

Siparis:
  sip_teslim_miktar = 6
  kalan = 4
```

### Senaryo 4: Irsaliye 10, Fiili Kabul 8

Input:

```text
dispatchQuantity = 10
acceptedQuantity = 8
autoCreateReturnForPartialAcceptance = true
```

Hesap:

```text
returnQuantity = 10 - 8 = 2
```

Sonuc:

```text
Firma mal kabul:
  sth_evraktip = 13
  sth_tip = 0
  sth_normal_iade = 0
  sth_miktar = 10

Firma iade:
  sth_evraktip = 1
  sth_tip = 1
  sth_normal_iade = 1
  sth_miktar = 2

Net stok = 8
returnStatus = IadeOlusturuldu
returnEDespatchStatus = GonderimBekliyor
```

Kullanici daha sonra firma iade evragindan e-irsaliye gonderir.

### Senaryo 5: Irsaliye 10, Fiili Kabul 8, Otomatik Iade Kapali

Input:

```text
dispatchQuantity = 10
acceptedQuantity = 8
autoCreateReturnForPartialAcceptance = false
```

Sonuc:

```text
Firma mal kabul:
  sth_miktar = 10

Firma iade:
  olusmaz

Net stok = 10
returnQuantity = 2
returnStatus = IadeBekliyor
returnEDespatchStatus = Yok
```

Bu durumda UI farki kaybettirmemeli; kullaniciyi manuel firma iade cozumune
yonlendirmelidir.

### Senaryo 6: Siparis Kalanindan Fazla Kabul, Izin Yok

Siparis:

```text
sip_miktar = 10
sip_teslim_miktar = 4
kalan = 6
```

Input:

```text
dispatchQuantity = 8
acceptedQuantity = 8
allowOrderOverReceiving = false
```

Sonuc:

```text
409 Conflict
```

### Senaryo 7: Siparis Kalanindan Fazla Kabul, Izin Var

Siparis:

```text
sip_miktar = 10
sip_teslim_miktar = 4
kalan = 6
```

Input:

```text
dispatchQuantity = 8
acceptedQuantity = 8
allowOrderOverReceiving = true
```

Sonuc:

```text
1. hareket:
  receivingMode = order-linked
  sth_miktar = 6
  sth_sip_uid = sip_Guid

2. hareket:
  receivingMode = order-overflow
  sth_miktar = 2
  sth_sip_uid = Guid.Empty

Siparis:
  sip_teslim_miktar = 10
```

Fazla gelen 2 adet stok girisine dahil olur ama siparisi tasirmaz.

## AXATA Notlari

AXATA manuel aktariminda iki farkli firma mal kabul yolu vardir.

### AXATA Native Inbound ATF

Endpoint:

```http
POST /api/integrations/axata-sync/manual/axata/inbound-atf/company-receivings
```

Batch:

```http
POST /api/integrations/axata-sync/manual/axata/inbound-atf/company-receivings/batch
```

Bu endpoint AXATA-native body kullanir. Satir modelinde sadece `quantity` vardir;
satir bazli fiili kabul miktari yoktur.

Backend map:

```text
dispatchQuantity = quantity
acceptedQuantity = quantity
autoCreateReturnForPartialAcceptance = true
```

Sonuc:

```text
quantity tek basina fark/iade olusturmaz.
```

Kismi kabul gerekiyorsa bu endpoint yerine manuel incoming endpoint'i
kullanilmalidir.

`DocumentNo` bos ise backend sirasiyla su alanlari dener:

```text
DocumentNo
InvoiceNo
AxataOrderNo
```

Son secilen deger tam firma mal kabul `documentNo` formatindaysa aynen
kullanilir:

```text
seri + 9 haneli sayisal sira
```

Tam formatta degilse seri/prefix kabul edilir ve siradaki sira backend
tarafindan uretilir. Bu alanlarin hepsi bos ise backend cari unvanindan
seri/sira uretir.

### Manuel Incoming Company Receiving

Endpoint:

```http
POST /api/integrations/axata-sync/manual/incoming/company-receivings
```

Batch:

```http
POST /api/integrations/axata-sync/manual/incoming/company-receivings/batch
```

Bu endpoint `CreateCompanyReceivingHttpRequest` ile ayni body modelini kullanir.
Bu yuzden kismi kabul icin dogru endpoint budur:

```json
{
  "customerCode": "32004621",
  "movementDate": "2026-04-20",
  "documentDate": "2026-04-20",
  "documentNo": "ST12026000002395",
  "allowOrderOverReceiving": false,
  "autoCreateReturnForPartialAcceptance": true,
  "lines": [
    {
      "stockCode": "015792",
      "dispatchQuantity": 10,
      "acceptedQuantity": 8,
      "unitPrice": 0,
      "unitPointer": 1,
      "orderGuid": null
    }
  ]
}
```

Bu cagrida 10 mal kabul, 2 firma iade olusur. E-irsaliye gonderimi yine
kullanici aksiyonudur.

## Hata Durumlari

Yaygin hata durumlari:

- `400 Bad Request`: validation hatasi.
- `401 Unauthorized`: token yok veya gecersiz.
- `403 Forbidden`: yetki yok.
- `404 Not Found`: cari veya siparis satiri bulunamadi.
- `409 Conflict`: belge zaten var, siparis kalanindan fazla kabul engellendi,
  offline `clientRequestId` cakisiyor veya request halen isleniyor.

Onemli validation mesajlari:

```text
Line dispatch quantity must be greater than zero.
Line accepted quantity can not be negative.
Line accepted quantity can not be greater than dispatch quantity.
Receiving quantity is greater than remaining order quantity.
```

## UI Kontrol Listesi

- Cari secilmeden `Siparis Bagla` pasif.
- E-belge/e-irsaliye varsa `documentNo` bosluksuz `seri + 9 haneli sayisal sira`
  formatinda gonderilebilir.
- E-belge/e-irsaliye yoksa `documentNo` bos veya harf iceren kisa prefix
  olabilir.
- Yeni UI `quantity` yerine `dispatchQuantity` ve `acceptedQuantity` gonderir.
- `acceptedQuantity > dispatchQuantity` engellenir.
- Eksik kabulde fark gosterilir.
- `autoCreateReturnForPartialAcceptance` varsayilan `true`.
- Otomatik iade olusursa iade evragi linki/statusu gosterilir.
- `GonderimBekliyor`, e-irsaliye gonderildi anlamina gelmez.
- UI e-irsaliye gonderimini otomatik yapmaz.
- Kullanici firma iade evragindan manuel e-irsaliye gonderir.
- Offline taslakta ayni islem icin ayni `clientRequestId` korunur.
