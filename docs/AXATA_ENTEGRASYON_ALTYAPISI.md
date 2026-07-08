# AXATA Entegrasyon Altyapisi

Bu dokuman, `FurpaMerkezApi` icindeki AXATA senkronizasyon modulunun 2026-06-12 itibariyla kodda dogrulanan durumunu anlatir. Ek olarak, paylasilan `Furpa.WorkerService` teknik dokumanindaki eski worker davranislariyla mevcut API davranisini karsilastirir.

Guvenlik notu:

- AXATA kullanici adi, sifre ve ic endpoint degerleri bu dokumanda acik yazilmaz.
- Dokuman yalnizca config anahtarlarini ve hangi akista kullanildiklarini anlatir.
- Gercek degerler ortam config'inde kalmalidir.

## Hizli Ozet

Mevcut API modulu uc isi birlikte yapar:

- `Mikro -> AXATA` yonunde Mikro verisinden preview/outbox payload uretir.
- Secili task'larda AXATA ana servisine WCF client ile canli dispatch yapar.
- `AXATA -> Mikro` yonunde C01 outbound delivery icin canli fetch/import/ack, diger akislarda manuel body tabanli kurtarma saglar.

Bugunku net durum:

| Alan | Durum |
|---|---|
| Task katalogu | Var |
| Queue/worker altyapisi | Var, in-memory queue |
| Scheduler altyapisi | Var, config ile acilir |
| Preview | Var |
| DryRun | Var |
| Outbox JSON artifact | Var |
| `issued-warehouse-order-sync` canli dispatch | Var, `addOutboundOrder*`, hareket kodu `C01` |
| `company-receiving-sync` canli dispatch | Var, `addInboundOrder*`, hareket kodu `G01` |
| `inventory-count-sync` canli dispatch | Yok |
| Firma master canli dispatch | Yok, preview/outbox var |
| Urun master canli dispatch | Var, `addSKUMaster`; toplu ve urun koduyla tekli route var |
| C01 AXATA pending delivery live fetch | Var |
| C01 AXATA -> Mikro sevk import | Var |
| C01 import sonrasi AXATA ack | Var, opsiyonel ama default true |
| C02/C03/C4 AXATA pending delivery live preview | Var, kuyruk seviyesinde okur |
| C02/C03/C4 AXATA -> Mikro live import | Yok |
| G01/G02 AXATA -> Mikro live fetch/import | Yok, manuel body endpointleri var |
| Kalici job/audit/retry tablosu | Yok |

En kritik kural:

- AXATA C01 depo siparisi akisi icin kaynak depo, Mikro `ssip_cikdepo` alanidir.
- `warehouseNo=50` verildiginde merkez/kaynak depo 50'den cikan siparisler listelenir ve denetlenir.
- Bu, genel siparis ekranlarindaki "Verilen/Alinan" isimlendirmesinden farkli bir AXATA perspektifidir.

## WorkerService ile Iliski

Paylasilan eski `Furpa.WorkerService` dokumanina gore aktif worker servisinde su ana akislari vardir:

| Eski worker | Yon | Eski secim veya hareket tipi | API durumu |
|---|---|---|---|
| `FirmWorker` | Furpa/Mikro -> AXATA | firma master/adres | Preview/outbox var, canli dispatch yok |
| `ProductWorker` | Furpa/Mikro -> AXATA | SKU barkod/master/palet | Preview/outbox var, canli dispatch yok |
| `C_01_OutboundOrderWorker` | Mikro -> AXATA | `OutWarehouseNo == 50`, `C01`, `addOutboundOrderV2Async` | `issued-warehouse-order-sync` ile preview/outbox/live dispatch var |
| `C_01_OutBoundDeliveryWorker` | AXATA -> Mikro | `MovementType=C01`, `Status=0` | live preview/import/ack var |
| `C_02_OutboundOrderWorker` | Mikro -> AXATA | `OrderType=0`, `C02` | API'de ayri C02 order task'i yok |
| `C_02_OutBoundDeliveryWorker` | AXATA -> Mikro | `MovementType=C02`, `Status=0` | live queue preview var, import yok |
| `C_03_OutBoundDeliveryWorker` | AXATA -> Mikro | `MovementType=C03`, `Status=0` | live queue preview var, import yok |
| `C_04_OutBoundDeliveryWorker` | AXATA -> Mikro | `MovementType=C4`, `Status=0` | live queue preview var, import yok |
| `G_01_InboundOrderWorker` | Mikro -> AXATA | `WarehouseNo == 50`, `G01`, `addInboundOrderV2Async` | `company-receiving-sync` ile preview/outbox/live dispatch var |
| `G_01_InboundDeliveryWorker` | AXATA -> Mikro | `MovementType=G01`, ATF | manuel/native inbound ATF body endpoint var, live fetch yok |
| `G_02_InboundOrderWorker` | Mikro -> AXATA | `InWarehouseNo == 50`, `G02` | API'de ayri G02 order task'i yok |
| `G_02_InboundDeliveryWorker` | AXATA -> Mikro | `MovementType=G02` | planli profil var, import yok |
| `DynamicCensusWorker` | AXATA EXT -> Mikro | `vw_stok_duzeltme` | manuel incoming inventory count body endpoint var, live EXT polling yok |

Bu tablo su anlama gelir:

- API, eski worker'in tamamini birebir iceri tasimis degildir.
- C01 cikis siparisi ve G01 firma mal kabul siparisi icin en kritik canli dispatch yollarini destekler.
- C01 teslimat tarafinda eski worker'a gore daha guvenli bir import sirasi vardir: once Mikro yazilir, sonra istenirse AXATA ack atilir.
- Diger hareket tipleri icin UI "planli profil" veya "manuel body kurtarma" olarak davranmalidir.

## Yonalimlar

### Mikro -> AXATA

Bu yonde API canli Mikro verisini okur ve AXATA payload'i uretir.

Desteklenen task'lar:

| Task | Depo gerekir mi? | Preview | DryRun/Outbox | Live dispatch | Hareket kodu |
|---|---:|---|---|---|---|
| `firm-master-sync` | Hayir | Var | Var | Yok | - |
| `product-master-sync` | Hayir | Var | Var | Var | `addSKUMaster` |
| `issued-warehouse-order-sync` | Evet | Var | Var | Var | `C01` |
| `company-receiving-sync` | Evet | Var | Var | Var | `G01` |
| `inventory-count-sync` | Evet | Var | Var | Yok | - |

`issued-warehouse-order-sync` icin ozel not:

- AXATA C01 eski worker mantiginda kaynak depo `OutWarehouseNo` / Mikro `ssip_cikdepo` alanidir.
- API icinde bu task, shared depo siparisi executor'ini AXATA perspektifiyle kullanir.
- Bu nedenle manuel aday listesi, genel preview, queue execute ve dispatch ayni `ssip_cikdepo = warehouseNo` evrenine bakar.
- `warehouseNo=50` icin hedef depo 150 olan `O150.5219` gibi evraklar beklenen adaylardir.

### AXATA -> Mikro

Bu yonde bugun iki farkli seviye vardir:

1. Canli C01 fetch/import:
   - AXATA ana servisten `getOutBoundDeliveryListAsync` ile `MovementType=C01`, `Status=0` okunur.
   - Mikro siparis satirlari `S06TESL` -> `DocumentSerie.DocumentOrderNo` ile bulunur.
   - Satir eslesmesi once `S07KALN + S07SKOD` -> `ssip_satirno + ssip_stok_kod`, sonra 1-bazli satir no farki, son olarak tekil stok + kalan miktar ile guvenli eslesme seklinde yapilir.
   - Mikro depolar arasi sevk fisi yazilir.
   - `STOK_HAREKETLERI_EK.sth_subesip_uid` linki ve teslim miktari kontrol edilir.
   - Basarili Mikro yazimdan sonra istenirse AXATA EXT `updIntegrationTableAsync` ile `ENT006.S06STAT=1` yapilir.

2. Manuel body tabanli kurtarma:
   - AXATA outbound delivery body eldeyse Mikro depolar arasi sevk yazilir.
   - AXATA inbound ATF body eldeyse Mikro firma mal kabul yazilir.
   - Serbest body ile firma mal kabul ve sayim sonucu yazilabilir.
   - Mikro'ya dusmus ama kabulde bekleyen depo mal kabulleri accept endpoint'leriyle tamamlanabilir.

Bugun olmayanlar:

- C02/C03/C4 icin Mikro'ya yazan live import/ack.
- G01/G02 icin live fetch/import.
- AXATA EXT `getViewDataAsync` polling ile otomatik sayim import.
- C01 disindaki hareket tipleri icin belge numarasi verip AXATA'dan tek belge fetch/import eden endpoint.
- Kalici retry/ack monitor.

## Mimari Bilesenler

### Application

Ana contract'lar:

- `IAxataSynchronizationService`
- `IAxataOutboundDeliveryImportService`
- `IAxataIntegrationAuditService`

DTO dosyalari:

- `AxataSynchronizationOverviewDto.cs`
- `AxataSynchronizationPreviewDto.cs`
- `AxataSynchronizationJobDtos.cs`
- `AxataSynchronizationManualDocumentDtos.cs`
- `AxataSynchronizationFetchProfileDtos.cs`
- `AxataSynchronizationConnectionTestDto.cs`
- `AxataOutboundDeliveryImportDtos.cs`
- `AxataIntegrationAuditDtos.cs`

### Infrastructure

Ana servisler:

- `AxataSynchronizationCatalog`
  - Task kodlari, adlari, kaynak/hedef sistem bilgileri.
- `AxataSynchronizationFetchProfileCatalog`
  - Eski worker parity icin C01/C02/C03/C4/G01/G02/EXT view profil sozlugu.
- `AxataSynchronizationService`
  - Overview, preview, job queue ve manual document operasyonlarini koordine eder.
- `AxataSynchronizationQueue`
  - In-memory job queue.
- `AxataSynchronizationWorker`
  - Queue'daki job'lari calistiran hosted service.
- `AxataSynchronizationScheduler`
  - Config ile acilan periyodik task tetikleyici.
- `AxataSynchronizationExecutionCoordinator`
  - Task code -> handler eslestirmesi.
- `AxataSynchronizationManualDocumentService`
  - Aday liste, tekil/toplu preview, tekil/toplu execute, tekil/toplu live dispatch.
- `AxataSynchronizationOutboxWriter`
  - Outbox JSON artifact yazar.
- `AxataSynchronizationLiveTransportService`
  - `addOutboundOrder*` ve `addInboundOrder*` WCF client ile typed request gonderir.
- `AxataOutboundDeliveryImportService`
  - C01 live fetch/import/ack ve live audit overview.
- `AxataSynchronizationConnectionProbeService`
  - Mikro/Furpa SQL ve AXATA endpoint probe.

Task handler'lari:

- `FirmMasterSyncTaskHandler`
- `ProductMasterSyncTaskHandler`
- `IssuedWarehouseOrderSyncTaskHandler`
- `CompanyReceivingSyncTaskHandler`
- `InventoryCountSyncTaskHandler`

### WebApi

Controller:

- `AxataSenkronizasyonuController`

Temel route:

```text
/api/integrations/axata-sync
```

Yetki kodlari:

- `entegrasyon-islemleri.axata-senkronizasyonu.list`
- `entegrasyon-islemleri.axata-senkronizasyonu.detail`
- `entegrasyon-islemleri.axata-senkronizasyonu.create`
- `entegrasyon-islemleri.axata-senkronizasyonu.update`

## Endpoint Gruplari

### Genel Durum

```text
GET /api/integrations/axata-sync
GET /api/integrations/axata-sync/health
GET /api/integrations/axata-sync/fetch-profiles
GET /api/integrations/axata-sync/jobs/{jobId}
```

`GET /api/integrations/axata-sync` response icinde task capability alanlari bulunur:

- `supportsManualDocuments`
- `supportsLiveDispatch`
- `liveOperationName`

### Worker Queue

```text
POST /api/integrations/axata-sync/jobs
POST /api/integrations/axata-sync/tasks/{taskCode}/execute
GET  /api/integrations/axata-sync/jobs/{jobId}
```

Davranis:

- `DryRun`: payload uretilir, dosya yazilmaz.
- `Outbox`: payload uretilir, JSON artifact yazilir.
- Bu endpointler canli AXATA dispatch yapmaz.

### Task Preview

```text
GET /api/integrations/axata-sync/tasks/{taskCode}/preview?warehouseNo=50&take=10
```

Not:

- `issued-warehouse-order-sync`, `company-receiving-sync`, `inventory-count-sync` icin `warehouseNo` gerekir.
- `firm-master-sync` ve `product-master-sync` depo bagimsizdir.

### Manuel Mikro -> AXATA Evrak Islemleri

```text
GET  /api/integrations/axata-sync/manual/tasks/{taskCode}/documents/candidates
POST /api/integrations/axata-sync/manual/tasks/{taskCode}/documents/preview
POST /api/integrations/axata-sync/manual/tasks/{taskCode}/documents/execute
POST /api/integrations/axata-sync/manual/tasks/{taskCode}/documents/preview-batch
POST /api/integrations/axata-sync/manual/tasks/{taskCode}/documents/execute-batch
```

Destekleyen task'lar:

- `issued-warehouse-order-sync`
- `company-receiving-sync`
- `inventory-count-sync`

`issued-warehouse-order-sync` aday listesi:

- Query'deki `warehouseNo`, AXATA kaynak/cikis depodur.
- Mikro filtresi `ssip_cikdepo = warehouseNo` olur.
- Aday listesi `skip/take` ile sayfalanir; `take` en fazla 100'dur.
- 150 aday varsa once `skip=0&take=100`, sonra `skip=100&take=100` cagrilir.
- Response item icinde `documentSerie`, `documentOrderNo`, `lineCount`, `totalQuantity` dogrudan preview/execute body'lerine tasinabilir.

### Canli Mikro -> AXATA Dispatch

```text
POST /api/integrations/axata-sync/manual/tasks/{taskCode}/documents/dispatch
POST /api/integrations/axata-sync/manual/tasks/{taskCode}/documents/dispatch-batch
```

Destekleyen task'lar:

- `issued-warehouse-order-sync`
  - Varsayilan WCF operation fallback: `addOutboundOrder`
  - Config ile genelde `addOutboundOrderV2`
  - Hareket tipi: `C01`
  - Master alanlari worker parity:
    - `S00TESN = DocumentSerie.DocumentOrderNo`
    - `S00TMUS = InWarehouseNo`
    - `S00SMUS = OutWarehouseNo`
    - `S00HTP1 = C01`
    - `S00HTP2 = C01`
    - `S00FBLK = OutWarehouseNo`
- `company-receiving-sync`
  - Varsayilan WCF operation fallback: `addInboundOrder`
  - Config ile genelde `addInboundOrderV2`
  - Hareket tipi: `G01`
  - Master alanlari worker parity:
    - `S13HKOD = G01`
    - `S13BNUM = DocumentSerie.DocumentOrderNo`
    - `S13FIRM = CustomerCode`

Canli dispatch response'u sunlari tasir:

- `operationName`
- `endpointUrl`
- `isSuccess`
- `serviceState`
- `serviceMessage`
- `payloadJson`
- `requestPayloadJson`
- `responsePayloadJson`

### Live Audit

```text
GET /api/integrations/axata-sync/live/audit/overview?startDate=2026-06-11&endDate=2026-06-11&warehouseNo=50&take=50
```

Bu endpoint veri yazmaz.

Kontroller:

- Mikro depolar arasi siparisleri `ssip_cikdepo` uzerinden okur.
- `ssip_special1` tum satirlarda worker basari bayragi olarak `1` mi kontrol eder.
- `ssip_special1=1` olan belgede hic `STOK_HAREKETLERI_EK.sth_subesip_uid` sevk linki yoksa `sentWarehouseOrdersMissingMikroShipments`, en az bir link olup eksik link veya miktar farki varsa `sentWarehouseOrdersWithShipmentDifferences` olarak ayirir.
- Sevk donus problemi once belge bazinda tek havuzda hesaplanir; `linkedMovementLineCount == 0` kritik liste, `linkedMovementLineCount > 0` ve eksik link/miktar farki uyari listesidir.
- Tarih filtresi `ssip_tarih` uzerinden calisir; `ssip_lastup_date` sadece problem listelerinde en yeni guncellenen belgeyi one almak icin kullanilir.
- AXATA pending outbound delivery kuyrugunu `C01`, `C02`, `C03`, `C4` icin `Status=0` olarak okur.
- C01 icin Mikro siparis satiri, depo uyumu, kalan miktar ve sevk fisi linkini kontrol eder.
- C02/C03/C4 icin kuyruk seviyesinde rapor verir.

Response'taki kritik alanlar:

- `isInSync`
- `summary`
- `outboundDeliverySummaries`
- `unsyncedWarehouseOrders`
- `sentWarehouseOrdersMissingMikroShipments`
- `sentWarehouseOrdersWithShipmentDifferences`
- `pendingOutboundDeliveries`
- `interventionCandidates`
- `operations`
- `notes`

`unsyncedWarehouseOrders` icindeki bir evrak, `manual/tasks/issued-warehouse-order-sync/documents/candidates` endpoint'inde ayni `warehouseNo/startDate/endDate` ile gorulebilmelidir. Bu eslesme AXATA C01 kaynak depo filtresinin dogrulama kuralidir.

`operations` alani UI'nin kontrol kulesi ekraninda kullanacagi operasyon kartlarini verir:

- `warehouse-orders-not-sent-to-axata`: Mikro siparis AXATA'ya gitmemis/eksik gitmis; manuel dispatch route'u vardir.
- `axata-pending-outbound-deliveries`: AXATA `Status=0` bekleyen sevk kuyrugu; C01 icin import route'u vardir.
- `sent-to-axata-missing-mikro-shipment`: AXATA'ya gonderildi isaretli ama belge genelinde Mikro sevk linki yok; liste overview icindedir, C01 belge bazli rescue route'u vardir.
- `sent-to-axata-shipment-differences`: Belgede en az bir Mikro sevk linki var ama eksik link veya miktar farki bulunur; kismi sevk/satir farki olarak aksiyonsuz incelenir.

### Outbound Delivery By Date

```text
GET /api/integrations/axata-sync/live/axata/outbound-deliveries/by-date?date=2026-06-19
```

Bu endpoint:

- AXATA `ENT006` tablosunu secilen tarihe gore okur.
- `date` query parametresi zorunludur.
- Tarih filtresi `ENT006.S06ITAR = yyyyMMdd` olarak uygulanir.
- `ENT007` satirlari `S07TESL` teslimat numarasi ile gruplanip satir sayisi ve toplam miktar uretilir.
- Mikro'ya veri yazmaz.
- AXATA status/ack guncellemez.
- Pending filtrelemez; secilen tarihteki AXATA sevk basliklarini raporlar.

Response `AxataOutboundDeliveriesByDateDto` doner. Her kayit icin:

- AXATA sira no
- teslimat/belge no
- parse edilebildiyse Mikro seri/sira
- status
- hareket tipi
- kaynak/hedef depo kodu
- AXATA sevk tarihi
- transfer tarihi
- satir sayisi
- toplam miktar
- plaka
- surucu adi

### Outbound Delivery Live Queue Preview

```text
GET /api/integrations/axata-sync/live/axata/outbound-deliveries/preview?movementType=C02&take=20
```

Desteklenen `movementType` degerleri:

- `C01`
- `C02`
- `C03`
- `C4`
- `C04` alias olarak kabul edilir ve `C4` sorgusuna donusur.

Bu endpoint:

- AXATA ana servisten `getOutBoundDeliveryListAsync` cagirir.
- `CompanyCode=01`, `WarehouseCode=01`, secili `MovementType`, `Status=0` ile okur.
- Mikro'ya yazmaz.
- AXATA status guncellemez.
- UI'nin C02/C03/C4 kuyrugunu audit ekranindan bagimsiz incelemesini saglar.

Response `AxataOutboundDeliveryQueuePreviewDto` doner. Her belge icin:

- AXATA sira no
- teslimat/belge no
- parse edilebildiyse Mikro seri/sira
- kaynak/hedef depo
- tarih
- satir sayisi
- toplam miktar
- `hasLiveImport`
- `currentHandling`
- `warning`

`C01` icin `hasLiveImport=true` gelir, ancak detayli Mikro eslesme ve import uygunlugu icin asagidaki C01 endpoint'i kullanilmalidir. `C02/C03/C4` icin bu endpoint sadece kuyruk preview'dur.

### C01 Live AXATA -> Mikro Import

```text
GET  /api/integrations/axata-sync/live/axata/outbound-deliveries/c01/preview?take=20
POST /api/integrations/axata-sync/live/axata/outbound-deliveries/c01/import
GET  /api/integrations/axata-sync/live/axata/outbound-deliveries/c01/documents/F50/15035/preview?status=1
POST /api/integrations/axata-sync/live/axata/outbound-deliveries/c01/documents/F50/15035/import
```

Preview:

- AXATA ana servisten `getOutBoundDeliveryListAsync` cagirir.
- `CompanyCode=01`, `WarehouseCode=01`, `MovementType=C01`, `Status=0`.
- Mikro'ya yazmaz.
- AXATA status guncellemez.

Import:

- `take`, `continueOnError`, `acknowledge` alir.
- Uygun C01 teslimatlarini Mikro depolar arasi sevk fisine cevirir.
- `acknowledge=true` ise Mikro yazim basarili olduktan sonra `updIntegrationTableAsync` ile `ENT006.S06STAT=1` yapar.
- Mikro sevk linki zaten varsa duplicate fis acmaz; uygun durumda sadece ack atabilir.

Belge bazli rescue:

- `sentWarehouseOrdersMissingMikroShipments` listesindeki `documentSerie/documentOrderNo` ile calisir.
- AXATA ana servisten `OrderNumber=seri.sira`, `MovementType=C01` ile teslimat detayini arar.
- `status` verilmezse once `0`, sonra `1` denenir.
- AXATA teslimat satirlari Mikro siparis satirlariyla guvenli eslesirse ve teslim miktari Mikro kalan siparis miktarini asmazsa Mikro sevk fisi olusturur.
- `sentWarehouseOrdersWithShipmentDifferences` listesindeki belgeler kismi sevk/satir farki uyarisi olarak incelenir; otomatik rescue/import butonu burada acilmaz.
- POST body: `{ "status": "1", "acknowledge": false }`; `acknowledge` default olarak kapali tutulmalidir.

Guvenli sira:

1. AXATA pending delivery okunur.
2. Mikro siparis satiri ve depo uyumu dogrulanir.
3. Mikro sevk fisi yazilir.
4. Link/kalan miktar kontrolleri tamamlanir.
5. Istenirse AXATA ack atilir.

Bu sira eski worker'daki "once AXATA stat update, sonra lokal DB" riskini azaltir.

### Manuel AXATA-Native Body Import

```text
POST /api/integrations/axata-sync/manual/axata/outbound-deliveries/inter-warehouse-shipments
POST /api/integrations/axata-sync/manual/axata/outbound-deliveries/inter-warehouse-shipments/batch
POST /api/integrations/axata-sync/manual/axata/inbound-atf/company-receivings
POST /api/integrations/axata-sync/manual/axata/inbound-atf/company-receivings/batch
```

Bu endpointler AXATA'dan canli fetch yapmaz. Operasyon ekibi veya baska sistem AXATA body bilgisini hazirlar.

### Serbest Manuel Incoming

```text
POST /api/integrations/axata-sync/manual/incoming/company-receivings
POST /api/integrations/axata-sync/manual/incoming/company-receivings/batch
POST /api/integrations/axata-sync/manual/incoming/inventory-counts
POST /api/integrations/axata-sync/manual/incoming/inventory-counts/batch
```

Kullanim:

- AXATA verisi operasyon tarafinda serbest body olarak toparlanmissa kullanilir.
- Firma mal kabul tarafinda `dispatchQuantity`, `acceptedQuantity`, `autoCreateReturnForPartialAcceptance` desteklenir.
- Native ATF endpointinden farkli olarak kismi kabul/iade senaryolari burada daha dogru temsil edilir.

### Bekleyen Depo Mal Kabul

```text
GET  /api/integrations/axata-sync/manual/incoming/warehouse-receivings
GET  /api/integrations/axata-sync/manual/incoming/warehouse-receivings/{documentSerie}/{documentOrderNo}
POST /api/integrations/axata-sync/manual/incoming/warehouse-receivings/{documentSerie}/{documentOrderNo}/accept
POST /api/integrations/axata-sync/manual/incoming/warehouse-receivings/accept-batch
```

Bu grup yeni AXATA belgesi cekmez. Mikro'ya zaten dusmus ama kabulde bekleyen depo mal kabul belgelerini tamamlar.

## Operasyonel Senaryolar

### Senaryo 1: Audit `unsyncedWarehouseOrders` evrak gosteriyor

Ornek:

```json
{
  "documentSerie": "O150",
  "documentOrderNo": 5219,
  "inWarehouseNo": 150,
  "outWarehouseNo": 50,
  "state": "NotSent"
}
```

Beklenen manuel kontrol:

```text
GET /api/integrations/axata-sync/manual/tasks/issued-warehouse-order-sync/documents/candidates?warehouseNo=50&startDate=2026-06-11&endDate=2026-06-11&take=100
```

Bu cagri ayni evraki aday listede gostermelidir. Gostermezse C01 kaynak depo filtresi veya tarih/evrak flag mantigi tekrar kontrol edilmelidir.

Sonraki adimlar:

1. `documents/preview` ile payload kontrol edilir.
2. Sadece dosyalama gerekiyorsa `documents/execute` + `Outbox`.
3. AXATA'ya gercek gonderim gerekiyorsa `documents/dispatch`.

### Senaryo 2: AXATA C01 pending queue dolu

1. `live/audit/overview` ile pending durum gorulur.
2. `live/axata/outbound-deliveries/c01/preview` ile import edilebilir kayitlar kontrol edilir.
3. `CanImport=true` olanlar icin `live/axata/outbound-deliveries/c01/import` calistirilir.
4. `acknowledge=true` ise Mikro yazimdan sonra AXATA ack atilir.

### Senaryo 3: AXATA inbound ATF verisi elde var

1. Body tam AXATA-native sekilde hazirsa `manual/axata/inbound-atf/company-receivings`.
2. Kismi kabul/iade ayrimi gerekiyorsa `manual/incoming/company-receivings`.
3. Coklu evrak icin batch endpoint kullanilir.

### Senaryo 4: Depo sevki Mikro'ya dustu ama kabulde kaldi

1. `manual/incoming/warehouse-receivings` ile listele.
2. Gerekirse detail endpoint ile satirlari incele.
3. Tek belge icin `accept`.
4. Coklu belge icin `accept-batch`.

## Fetch Profile Katalogu

`GET /api/integrations/axata-sync/fetch-profiles` UI icin mevcut ve planli profilleri dondurur.

Bugunku katalog:

| Kod | Fetch operation | Movement/Profile | Durum |
|---|---|---|---|
| `c01-outbound-delivery` | `getOutBoundDeliveryListAsync` | `C01` | Implemented |
| `c02-outbound-delivery` | `getOutBoundDeliveryListAsync` | `C02` | Live queue preview var, import yok |
| `c03-outbound-delivery` | `getOutBoundDeliveryListAsync` | `C03` | Live queue preview var, import yok |
| `c04-outbound-delivery` | `getOutBoundDeliveryListAsync` | `C4` | Live queue preview var, import yok |
| `g01-inbound-atf` | `getInboundATFListAsync` | `G01` | Manual body route var, live fetch yok |
| `g02-inbound-delivery` | `getInboundDeliveryListAsync` | `G02` | Planned |
| `inventory-count-ext-view` | `getViewDataAsync` | `vw_stok_duzeltme` | Manual incoming route var, live polling yok |

## Konfigurasyon

Config bolumu:

```text
AxataSynchronization
```

Temel alanlar:

- `Enabled`
- `WorkerEnabled`
- `SchedulerEnabled`
- `MainEndpointUrl`
- `ExtendedEndpointUrl`
- `Username`
- `Password`
- `DefaultLookbackDays`
- `PreviewDefaultTake`
- `EndpointProbeTimeoutSeconds`
- `OutboxBasePath`
- `WarehouseOrderAutomation.Enabled`
- `WarehouseOrderAutomation.WarehouseNos`
- `WarehouseOrderAutomation.CreateForInterWarehouseShipments`
- `WarehouseOrderAutomation.CreateForWarehouseReturns`
- `Tasks.{taskCode}.Enabled`
- `Tasks.{taskCode}.ScheduleEnabled`
- `Tasks.{taskCode}.IntervalMinutes`
- `Tasks.{taskCode}.DefaultWarehouseNo`
- `Tasks.{taskCode}.LiveOperationName`

Davranis:

- `Enabled=false` ise `execute`, `dispatch`, live import ve worker kuyrugu gibi yazma/operasyon endpointleri 409 doner.
- Manuel operasyon kullanilip otomatik worker istenmiyorsa production icin onerilen kombinasyon `Enabled=true`, `WorkerEnabled=false`, `SchedulerEnabled=false` olur.
- `WorkerEnabled=false` sadece arka plan kuyruk isleyicisini kapatir; manuel `execute` ve `dispatch` icin ana `Enabled=true` olmalidir.
- `Tasks.{taskCode}.Enabled=false` ise sadece ilgili task kapali sayilir.

Canli dispatch icin zorunlular:

- `MainEndpointUrl`
- `Username`
- `Password`

C01 import + ack icin ek zorunlu:

- `ExtendedEndpointUrl`, eger `acknowledge=true` kullanilacaksa.

Operation secimi:

- `issued-warehouse-order-sync` icin `LiveOperationName` genelde `addOutboundOrderV2` olmalidir.
- `company-receiving-sync` icin `LiveOperationName` genelde `addInboundOrderV2` olmalidir.
- Config bos ise fallback olarak `addOutboundOrder` / `addInboundOrder` kullanilir.

## Auth ve Migration

AXATA menu permission migration'i vardir:

- `20260429143000_AddAxataSynchronizationPermissions.cs`
- `20260429143000_AddAxataSynchronizationPermissions.Designer.cs`

Eklenen yetkiler:

- `entegrasyon-islemleri.axata-senkronizasyonu.list`
- `entegrasyon-islemleri.axata-senkronizasyonu.detail`
- `entegrasyon-islemleri.axata-senkronizasyonu.create`
- `entegrasyon-islemleri.axata-senkronizasyonu.update`

Mevcut AXATA endpoint genisletmeleri icin ek EF migration gerekmez:

- Queue in-memory.
- Outbox filesystem tabanli.
- Dispatch/import audit log kalici tabloya yazilmiyor.

Ileride onerilen tablolar:

- `AxataIntegrationJobs`
- `AxataIntegrationJobArtifacts`
- `AxataDispatchAuditLogs`
- `AxataIncomingDocumentInbox`
- `AxataReconciliationLogs`
- `AxataIncomingDeliveryImports`
- `AxataIncomingDeliveryImportLines`

Bu tablolar zorunlu degil; ancak kalici retry, ack monitor ve servis restart sonrasi izlenebilirlik icin gereklidir.

## UI Icin Kritik Kurallar

UI su ayrimi net yapmalidir:

- `execute` endpointleri `DryRun/Outbox` isidir, AXATA'ya canli gonderim degildir.
- `dispatch` endpointleri AXATA'ya WCF client ile canli yazim yapar.
- `live/axata/outbound-deliveries/preview` C01/C02/C03/C4 kuyrugunu canli okur ama veri yazmaz.
- `live/axata/outbound-deliveries/by-date` AXATA `ENT006.S06ITAR` tarihine gore sevkleri listeler; veri yazmaz ve pending filtrelemez.
- `live/axata/outbound-deliveries/c01/import` AXATA'dan canli okur ve Mikro'ya yazar.
- `manual/axata/*` endpointleri AXATA'dan canli okumaz; body UI veya operasyon tarafindan saglanir.
- `inventory-count-sync` icin live dispatch butonu gosterilmemelidir.
- `firm-master-sync` icin live dispatch butonu gosterilmemelidir.
- `product-master-sync` icin toplu live dispatch ve urun koduyla tekli dispatch butonu gosterilebilir.
- `issued-warehouse-order-sync` aday listesinde `warehouseNo`, hedef depo degil AXATA kaynak/cikis depodur.
- `live/audit/overview` veri yazmaz; kontrol ve karar ekranidir.
- C02/C03/C4 icin UI preview butonu acabilir, import/ack butonu acmamalidir.
- G01/G02 fetch-import icin UI henuz aktif execute butonu acmamalidir.

## Bilinen Sinirlar

- Job listesi ve sonuc detaylari kalici DB'de tutulmaz.
- Outbox basarisi "AXATA kabul etti" anlamina gelmez.
- Firma master task'i canli WCF dispatch yapmaz.
- Urun master task'i `Live` modunda `ENT004`, `ENT003_List` ve `ENT004_UNIT_List` iceren `addSKUMaster` paketlerini canli gonderir.
- C02/C03/C4 live queue preview vardir ama live import/ack henuz yoktur.
- G01/G02 live fetch-import henuz yoktur.
- C01 belge bazli rescue vardir; C02/C03/C4/G01/G02 icin AXATA belge numarasi ile tek belge fetch/import endpoint'i yoktur.
- EXT `getViewDataAsync` tabanli otomatik sayim polling yoktur.
- Dispatch request/response XML'i response body'de doner; hassas veri icerebilecegi icin UI bunu dikkatli gostermelidir.

## Build ve Dogrulama

Repo `global.json` ile .NET SDK `9.0.200` bekler.

Onerilen build:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
$env:MSBUILDUSESERVER='0'
dotnet build FurpaMerkezApi.sln --no-restore -maxcpucount:1
```

Kilitli DLL veya local runtime output problemi varsa ayri output klasoru:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
$env:MSBUILDUSESERVER='0'
dotnet build FurpaMerkezApi.sln --no-restore -maxcpucount:1 -p:OutDir="artifacts\\axata-verify\\"
```

Canli AXATA dogrulamasi icin sahada kontrol edilmesi gerekenler:

- `health` endpoint'inde Main ve EXT probe sonucu.
- `issued-warehouse-order-sync` dispatch XML'inde `S00HTP1/S00HTP2 = C01`.
- `company-receiving-sync` dispatch XML'inde `S13HKOD = G01`.
- C01 audit ile manual candidates'in ayni `ssip_cikdepo` evrenine bakmasi.
- C01 importta ack'in Mikro yazim basarisindan sonra atilmasi.

## Sonraki Faz Onerisi

1. C02/C03/C4 live import/ack handler'lari.
2. G01/G02 live fetch/import handler'lari.
3. EXT `getViewDataAsync` dynamic census polling.
4. Master data icin canli dispatch:
   - `addFirmMaster`
   - `addFirmAddress`
   - `addSKUMaster`
   - `addSKUBarcode`
   - `addSKUPalet`
5. Kalici job/audit/retry tablolari.
6. C02/C03/C4/G01/G02 icin belge numarasi ile AXATA'dan tek belge fetch endpointleri.
7. Ack/retry monitor ekrani.
8. Dispatch sonucunu DB'de saklayan reconcile katmani.
