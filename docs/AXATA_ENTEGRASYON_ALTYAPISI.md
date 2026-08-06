# AXATA Entegrasyon Altyapisi

Bu dokuman, `FurpaMerkezApi` icindeki AXATA senkronizasyon modulunun 2026-08-05 itibariyla kodda dogrulanan durumunu anlatir. Ek olarak, paylasilan `Furpa.WorkerService` teknik dokumanindaki eski worker davranislariyla mevcut API davranisini karsilastirir.

Guvenlik notu:

- AXATA kullanici adi, sifre ve ic endpoint degerleri bu dokumanda acik yazilmaz.
- Dokuman yalnizca config anahtarlarini ve hangi akista kullanildiklarini anlatir.
- Gercek degerler ortam config'inde kalmalidir.

## Hizli Ozet

Mevcut API modulu uc isi birlikte yapar:

- `Mikro -> AXATA` yonunde Mikro verisinden preview/outbox payload uretir.
- Secili task'larda AXATA ana servisine WCF client ile canli dispatch yapar.
- `AXATA -> Mikro` yonunde C01/C02/C03/C4 outbound delivery, G01 inbound ATF, G02 inbound delivery ve DynamicCensus icin canli fetch/import/ack saglar; ayrica manuel body tabanli kurtarma endpointleri korunur.

Operasyon ekrani icin ana giris:

```text
GET /api/integrations/axata-sync/workbench?startDate=2026-08-05&endDate=2026-08-06&warehouseNo=50&take=50
GET /api/integrations/axata-sync/is-merkezi?startDate=2026-08-05&endDate=2026-08-06&warehouseNo=50&take=50
```

Bu endpoint teknik audit response'unu sade ve islevsel bir is merkezine indirger:

- `panel`: kullanicinin ilk bakista gorecegi ozet, akis, aksiyon ve oncelikli belgeler.
- `screenSections`: UI'nin sayfayi hangi bolumlerle kuracagini anlatan sade yerlesim sozlugu.
- `operationGroups`: kontrol, master veri, Mikro -> AXATA, AXATA -> Mikro ve manuel kurtarma operasyonlari.
- `endpointGroups`: her endpointin ne ise yaradigini, okuma/yazma durumunu ve write scope bilgisini anlatan sozluk.
- `glossary`: preview/dispatch/import/ack/rescue/C01/G02 gibi terimlerin UI karsiligi.
- `rules`: ekranda kaybolmamak icin uygulanacak ana operasyon kurallari.

UI ana ekranda once `workbench` veya Turkce alias olan `is-merkezi` sonucunu gostermelidir. `panel` daha kucuk ozet response'tur; `live/audit/overview` teknik detay ve derin inceleme icindir.

Sade route aileleri:

| Route ailesi | Amac | Not |
|---|---|---|
| `workbench`, `is-merkezi` | Tum ekran sozlugu ve canli durum | UI'nin ana girisidir |
| `panel` | Hafif ozet response | Workbench icindeki `panel` alaninin tekil halidir |
| `operations/...` | Normal operasyon akislari | Preview, dispatch, import ve belge rescue burada gosterilir |
| `recovery/...` | Manuel kurtarma araclari | Body'den yazim, serbest mal kabul/sayim, bekleyen kabul |
| `advanced/...` | Teknik job/outbox/task detaylari | Normal kullaniciya baskin gosterilmemelidir |
| `live/...`, `manual/...` | Eski teknik route'lar | Geriye uyumluluk icin kalir; yeni UI bunlari ana route olarak kullanmaz |

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
| `warehouse-inbound-order-sync` canli dispatch | Var, `addInboundOrder*`, hareket kodu `G02` |
| `company-receiving-sync` canli dispatch | Var, `addInboundOrder*`, hareket kodu `G01` |
| `inventory-count-sync` canli import | Var, AXATA EXT `vw_stok_duzeltme` -> Mikro dynamic census |
| Firma master canli dispatch | Var, `addFirmMaster` + `addFirmAddress` |
| Urun master canli dispatch | Var, `addSKUMaster`; toplu ve urun koduyla tekli route var |
| C01 AXATA pending delivery live fetch | Var |
| C01 AXATA -> Mikro sevk import | Var |
| C01 import sonrasi AXATA ack | Var, opsiyonel ama default true |
| C02/C03/C4 AXATA pending delivery live preview | Var |
| C02/C03/C4 AXATA -> Mikro live import | Var, basarili Mikro yazimdan sonra opsiyonel ack; C02 `MikroWriteRouting:CompanyMovement` ayarina uyar |
| G01 AXATA -> Mikro live fetch/import | Var, `getInboundATFListAsync`, `ENT016_IRS.S16STAT=1` ack; `MikroWriteRouting:CompanyReceiving` ayarina uyar |
| G02 AXATA -> Mikro live fetch/import | Var, `getInboundDeliveryListAsync`, hareket kodu `G02` |
| Kalici job/audit/retry tablosu | Yok |

En kritik kural:

- AXATA C01 depo siparisi akisi icin kaynak depo, Mikro `ssip_cikdepo` alanidir.
- `warehouseNo=50` verildiginde merkez/kaynak depo 50'den cikan siparisler listelenir ve denetlenir.
- Bu, genel siparis ekranlarindaki "Verilen/Alinan" isimlendirmesinden farkli bir AXATA perspektifidir.

## WorkerService ile Iliski

Paylasilan eski `Furpa.WorkerService` dokumanina gore aktif worker servisinde su ana akislari vardir:

| Eski worker | Yon | Eski secim veya hareket tipi | API durumu |
|---|---|---|---|
| `FirmWorker` | Furpa/Mikro -> AXATA | firma master/adres | `firm-master-sync` preview/outbox/live dispatch var |
| `ProductWorker` | Furpa/Mikro -> AXATA | SKU barkod/master/palet | `product-master-sync` preview/outbox/live dispatch var |
| `C_01_OutboundOrderWorker` | Mikro -> AXATA | `OutWarehouseNo == 50`, `C01`, `addOutboundOrderV2Async` | `issued-warehouse-order-sync` ile preview/outbox/live dispatch var |
| `C_01_OutBoundDeliveryWorker` | AXATA -> Mikro | `MovementType=C01`, `Status=0` | live preview/import/ack var |
| `C_02_OutboundOrderWorker` | Mikro -> AXATA | `OrderType=0`, `C02` | `received-company-order-sync` ile preview/outbox/live dispatch var |
| `C_02_OutBoundDeliveryWorker` | AXATA -> Mikro | `MovementType=C02`, `Status=0` | live preview/import/ack var |
| `C_03_OutBoundDeliveryWorker` | AXATA -> Mikro | `MovementType=C03`, `Status=0` | live preview/import/ack var |
| `C_04_OutBoundDeliveryWorker` | AXATA -> Mikro | `MovementType=C4`, `Status=0` | live preview/import/ack var |
| `G_01_InboundOrderWorker` | Mikro -> AXATA | `WarehouseNo == 50`, `G01`, `addInboundOrderV2Async` | `company-receiving-sync` ile preview/outbox/live dispatch var |
| `G_01_InboundDeliveryWorker` | AXATA -> Mikro | `MovementType=G01`, ATF | live preview/import/ack var; manuel/native inbound ATF body endpoint de korunur |
| `G_02_InboundOrderWorker` | Mikro -> AXATA | `InWarehouseNo == 50`, `G02` | `warehouse-inbound-order-sync` ile preview/outbox/live dispatch var |
| `G_02_InboundDeliveryWorker` | AXATA -> Mikro | `MovementType=G02` | live preview/import/ack var; mevcut Mikro bekleyen sevk fisi kabul edilir |
| `DynamicCensusWorker` | AXATA EXT -> Mikro | `vw_stok_duzeltme` | `inventory-count-sync` Live ve `live/axata/dynamic-census/*` ile var |

Bu tablo su anlama gelir:

- API, eski worker'in ana canli akislari icin API icinden preview/import/dispatch saglar.
- C01/C02/C03/C4/G01/G02/DynamicCensus import tarafinda eski worker'a gore daha guvenli sira vardir: once Mikro yazilir, sonra istenirse AXATA ack atilir.
- Manuel body endpointleri kaldirilmamistir; operasyonel kurtarma ve elle gelen AXATA body verisi icin kullanilmaya devam eder.

## Yonalimlar

### Mikro -> AXATA

Bu yonde API canli Mikro verisini okur ve AXATA payload'i uretir.

Desteklenen task'lar:

| Task | Depo gerekir mi? | Preview | DryRun/Outbox | Live dispatch | Hareket kodu |
|---|---:|---|---|---|---|
| `firm-master-sync` | Hayir | Var | Var | Var | `addFirmMaster` + `addFirmAddress` |
| `product-master-sync` | Hayir | Var | Var | Var | `addSKUMaster` |
| `issued-warehouse-order-sync` | Evet | Var | Var | Var | `C01` |
| `received-company-order-sync` | Evet | Var | Var | Var | `C02` |
| `warehouse-inbound-order-sync` | Evet | Var | Var | Var | `G02` |
| `company-receiving-sync` | Evet | Var | Var | Var | `G01` |
| `inventory-count-sync` | Evet | Var | Var | Var | Live modda `vw_stok_duzeltme` -> Mikro |

`issued-warehouse-order-sync` icin ozel not:

- AXATA C01 eski worker mantiginda kaynak depo `OutWarehouseNo` / Mikro `ssip_cikdepo` alanidir.
- API icinde bu task, shared depo siparisi executor'ini AXATA perspektifiyle kullanir.
- Bu nedenle manuel aday listesi, genel preview, queue execute ve dispatch ayni `ssip_cikdepo = warehouseNo` evrenine bakar.
- `warehouseNo=50` icin hedef depo 150 olan `O150.5219` gibi evraklar beklenen adaylardir.

`warehouse-inbound-order-sync` icin ozel not:

- AXATA G02 eski worker mantiginda hedef/giris depo Mikro `ssip_girdepo` alanidir.
- API icinde bu task, shared depo siparisi executor'ini `WarehouseOrderListDirection.Issued` ile kullanir.
- Bu nedenle manuel aday listesi, preview, outbox execute ve dispatch ayni `ssip_girdepo = warehouseNo` evrenine bakar.
- `warehouseNo=50` icin kaynak/cikis deposu sube olan, merkez depoya gelen depolar arasi siparisler beklenen adaylardir.

### AXATA -> Mikro

Bu yonde bugun alti farkli canli import seviyesi vardir:

1. Canli C01 fetch/import:
   - AXATA ana servisten `getOutBoundDeliveryListAsync` ile `MovementType=C01`, `Status=0` okunur.
   - Mikro siparis satirlari `S06TESL` -> `DocumentSerie.DocumentOrderNo` ile bulunur.
   - Satir eslesmesi once `S07KALN + S07SKOD` -> `ssip_satirno + ssip_stok_kod`, sonra 1-bazli satir no farki, son olarak tekil stok + kalan miktar ile guvenli eslesme seklinde yapilir.
   - Mikro depolar arasi sevk fisi yazilir.
   - `STOK_HAREKETLERI_EK.sth_subesip_uid` linki ve teslim miktari kontrol edilir.
   - Basarili Mikro yazimdan sonra istenirse AXATA EXT `updIntegrationTableAsync` ile `ENT006.S06STAT=1` yapilir.

2. Canli G02 fetch/import:
   - AXATA ana servisten `getInboundDeliveryListAsync` ile `MovementType=G02`, `Status=0` okunur.
   - Mikro siparis satirlari `S16BNUM` -> `DocumentSerie.DocumentOrderNo` ile bulunur.
   - Siparise bagli mevcut sevk fisi `STOK_HAREKETLERI_EK.sth_subesip_uid` linkiyle bulunur.
   - Backend yeni sevk fisi olusturmaz; mevcut bekleyen sevk fisini `AcceptWarehouseReceivingUseCase` ile kabul eder.
   - `ssip_teslim_miktar` AXATA kabul miktarina gore guncellenir, `ssip_kapat_fl` yeniden hesaplanir.
   - Basarili Mikro yazimdan sonra istenirse AXATA EXT `updIntegrationTableAsync` ile `ENT016_MST.S16STAT=1`, `IDField=S16ID` yapilir.

3. Canli C02 fetch/import:
   - AXATA ana servisten `getOutBoundDeliveryListAsync` ile `MovementType=C02`, `Status=0` okunur.
   - `S06TESL` seri.sira formatinda Mikro alinan firma siparisine baglanir.
   - Satirlar `S07KALN + S07SKOD` ile Mikro `SIPARISLER.sip_satirno + sip_stok_kod` satirina eslestirilir.
   - Mikro firma sevki standart firma sevk akisiyle olusturulur ve satir siparis linki `sth_sip_uid` ile korunur.
   - `MikroWriteRouting:CompanyMovement=Database` ise eski worker parity icin `STOK_HAREKETLERI` dogrudan transaction icinde yazilir, `sip_teslim_miktar` ayni save akisi icinde guncellenir.
   - `MikroWriteRouting:CompanyMovement=MikroApi` ise `POST /Api/apiMethods/IrsaliyeKaydetV2` kullanilir; API sonrasi `sth_sip_uid` linkleri geri okunup dogrulanmadan `sip_teslim_miktar` guncellenmez ve AXATA ack atilmaz.
   - Duplicate riski icin siparis bagli mevcut firma sevki veya ayni AXATA belge no/aciklamasiyla olusmus hareket varsa tekrar fis olusturulmaz.
   - Basarili Mikro yazim ve gerekli link dogrulamasi tamamlandiktan sonra istenirse `ENT006.S06STAT=1` ack atilir.

4. Canli C03/C4 legacy fetch/import:
   - C03 `MovementType=C03`, C4 `MovementType=C4` olarak AXATA pending kuyrugundan okunur.
   - C03 eski worker davranisina uygun `F50`, type/cins/iade kombinasyonu ile firma iade/ozel cikis hareketi yazar; cari kodu AXATA `S06FIRM` alanindan alinir.
   - C4 eski worker davranisina uygun `F50`, type 2/cins 6 ve 50 -> 51 depo hareketi yazar.
   - AXATA `S06SIRA` degeri duplicate kontrolu icin hareket grup kodunda saklanir.
   - Basarili Mikro yazimdan sonra istenirse `ENT006.S06STAT=1` ack atilir.

5. Canli G01 inbound ATF fetch/import:
   - AXATA ana servisten `getInboundATFListAsync` ile `MovementType=G01`, `Status=0` okunur.
   - `S16SIPN` seri.sira formatinda Mikro firma siparisi, `S16KALN` siparis satir no kabul edilir.
   - Satirlar Mikro `SIPARISLER` ile eslesirse `DocumentType=13` firma mal kabul hareketine cevrilir.
   - `MikroWriteRouting:CompanyReceiving=Database` ise eski worker parity icin dogrudan DB transaction yolu kullanilir; `S16SIRA -> sth_HareketGrupKodu1` izi korunur ve `sip_teslim_miktar` ayni transaction icinde artirilir.
   - `MikroWriteRouting:CompanyReceiving=MikroApi` ise mevcut firma mal kabul use case yolu calisir, `POST /Api/apiMethods/IrsaliyeKaydetV2` kullanilir, siparis linkleri `sth_sip_uid` ile geri okunup dogrulanmadan AXATA ack atilmaz.
   - MikroApi yolunda ayni siparis satiri AXATA tarafinda parcalanmis gelirse miktarlar siparis GUID bazinda toplanarak tek mal kabul satirina indirilir; duplicate kontrolu mevcut `sth_sip_uid` linkleriyle yapilir.
   - Basarili Mikro yazimdan sonra istenirse AXATA EXT `ENT016_IRS.S16STAT=1`, `IDField=S16SIRA` ack atilir.

6. Canli DynamicCensus fetch/import:
   - AXATA EXT `getViewDataAsync` ile `vw_stok_duzeltme` okunur.
   - `S11STIP=1` giris duzeltmesi, diger tipler cikis duzeltmesi olarak Mikro `STOK_HAREKETLERI` kaydina cevrilir.
   - Eski worker'dan farkli olarak `S11SIRA` Mikro hareket grup koduna `AXATA-S11:{rowNo}` seklinde yazilir ve duplicate engeli olarak kullanilir.
   - Basarili Mikro yazimdan sonra istenirse AXATA EXT `ENT011.S11STAT=1`, `IDField=S11SIRA` ack atilir.

7. Manuel body tabanli kurtarma:
   - AXATA outbound delivery body eldeyse Mikro depolar arasi sevk yazilir.
   - AXATA inbound ATF body eldeyse Mikro firma mal kabul yazilir.
   - Serbest body ile firma mal kabul ve sayim sonucu yazilabilir.
   - Mikro'ya dusmus ama kabulde bekleyen depo mal kabulleri accept endpoint'leriyle tamamlanabilir.

Bugun olmayanlar:

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

- `issued-warehouse-order-sync`, `warehouse-inbound-order-sync`, `company-receiving-sync`, `inventory-count-sync` icin `warehouseNo` gerekir.
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
- `warehouse-inbound-order-sync`
- `company-receiving-sync`
- `inventory-count-sync`

`issued-warehouse-order-sync` aday listesi:

- Query'deki `warehouseNo`, AXATA kaynak/cikis depodur.
- Mikro filtresi `ssip_cikdepo = warehouseNo` olur.
- Aday listesi `skip/take` ile sayfalanir; `take` en fazla 100'dur.
- 150 aday varsa once `skip=0&take=100`, sonra `skip=100&take=100` cagrilir.
- Response item icinde `documentSerie`, `documentOrderNo`, `lineCount`, `totalQuantity` dogrudan preview/execute body'lerine tasinabilir.

`warehouse-inbound-order-sync` aday listesi:

- Query'deki `warehouseNo`, AXATA hedef/giris depodur.
- Mikro filtresi `ssip_girdepo = warehouseNo` olur.
- Response item icindeki `documentSerie`, `documentOrderNo`, `lineCount`, `totalQuantity` dogrudan preview/execute body'lerine tasinabilir.

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
  - AXATA basarili donerse worker basari bayragi `ssip_special1=1` olarak isaretlenir; `MikroWriteRouting:IssuedWarehouseOrder=Database` iken mevcut DB update yolu, `MikroApi` iken `DepolarArasiSiparisDuzeltV2` yolu kullanilir. MikroApi modunda DB fallback yapilmaz, yazim satir GUID'leri uzerinden read-only geri okuma ile dogrulanir.
  - Master alanlari worker parity:
    - `S00TESN = DocumentSerie.DocumentOrderNo`
    - `S00TMUS = InWarehouseNo`
    - `S00SMUS = OutWarehouseNo`
    - `S00HTP1 = C01`
    - `S00HTP2 = C01`
    - `S00FBLK = OutWarehouseNo`
- `warehouse-inbound-order-sync`
  - Varsayilan WCF operation fallback: `addInboundOrder`
  - Config ile genelde `addInboundOrderV2`
  - Hareket tipi: `G02`
  - AXATA basarili donerse worker basari bayragi `ssip_special1=1` olarak isaretlenir; filtre `ssip_girdepo = warehouseNo` evrenindedir.
  - Master alanlari worker parity:
    - `S13HKOD = G02`
    - `S13BNUM = DocumentSerie.DocumentOrderNo`
    - `S13FIRM = OutWarehouseNo`
  - Satir alanlari worker parity:
    - `S13KALN = LineNo`
    - `S13SKU = StockCode`
    - `S13MIKT = RemainingQuantity > 0 ? RemainingQuantity : Quantity`
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
- C02/C03/C4 icin pending kuyruk ve import edilebilirlik endpointleri vardir.

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
- `axata-pending-outbound-deliveries`: AXATA `Status=0` bekleyen sevk kuyrugu; C01/C02/C03/C4 icin ilgili live import route'u vardir.
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
- UI'nin C01/C02/C03/C4 kuyrugunu audit ekranindan bagimsiz incelemesini saglar.

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

`hasLiveImport=true` gelen kayitlarda detayli Mikro eslesme ve import uygunlugu icin ilgili hareket tipinin ozel preview endpoint'i kullanilmalidir: `c01`, `c02`, `c03` veya `c04`.

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
- Mikro fis tarihi import gunudur. AXATA `ENT006.S06ITAR` dun/onceki gun olsa bile Mikro `STOK_HAREKETLERI.sth_tarih` ve `sth_belge_tarih` backendin Mikro'ya yazdigi gun olarak set edilir.
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

### G02 Live AXATA -> Mikro Import

```text
GET  /api/integrations/axata-sync/live/axata/inbound-deliveries/g02/preview?take=20
POST /api/integrations/axata-sync/live/axata/inbound-deliveries/g02/import
GET  /api/integrations/axata-sync/live/axata/inbound-deliveries/g02/documents/F50/15035/preview?status=1
POST /api/integrations/axata-sync/live/axata/inbound-deliveries/g02/documents/F50/15035/import
```

Preview:

- AXATA ana servisten `getInboundDeliveryListAsync` cagirir.
- `CompanyCode=01`, `WarehouseCode=01`, `MovementType=G02`, `Status=0`.
- Mikro'ya yazmaz.
- AXATA status guncellemez.
- `S16BNUM=seri.sira` ile Mikro depolar arasi siparis bulunur.
- `STOK_HAREKETLERI_EK.sth_subesip_uid` ile siparise bagli bekleyen sevk fisi bulunur.

Import:

- `take`, `continueOnError`, `acknowledge` alir.
- Uygun G02 teslimatlarini yeni fis yaratmadan mevcut Mikro bekleyen sevk fisine kabul olarak uygular.
- Mal kabul yazimi mevcut `AcceptWarehouseReceivingUseCase` uzerinden yapilir; routing `MikroWriteRouting:WarehouseReceivingAcceptance` ayarina uyar.
- Kabulden sonra siparis `ssip_teslim_miktar` alanlari AXATA kabul miktarina gore guncellenir ve `ssip_kapat_fl` yeniden hesaplanir.
- `acknowledge=true` ise Mikro yazim basarili olduktan sonra `updIntegrationTableAsync` ile `ENT016_MST.S16STAT=1`, `IDField=S16ID` yapar.
- Mikro kabul zaten varsa duplicate kabul yapmaz; uygun durumda sadece ack atabilir.

Belge bazli rescue:

- AXATA ana servisten `OrderNumber=seri.sira`, `MovementType=G02` ile teslimat detayini arar.
- `status` verilmezse once `0`, sonra `1` denenir.
- AXATA satirlari Mikro siparis ve mevcut sevk satirlariyla guvenli eslesirse kabul uygular.
- POST body: `{ "status": "1", "acknowledge": false }`; kontrollu rescue icin once `acknowledge=false` ile yazim sonucu izlenebilir.

Guvenli sira:

1. AXATA G02 delivery okunur.
2. Mikro siparis satiri ve bekleyen sevk fisi linki dogrulanir.
3. Mikro bekleyen sevk fisi kabul edilir.
4. Siparis teslim miktari AXATA kabul miktarina gore guncellenir.
5. Istenirse AXATA `ENT016_MST` ack atilir.

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
| `c02-outbound-delivery` | `getOutBoundDeliveryListAsync` | `C02` | Implemented, live preview/import/ack var |
| `c03-outbound-delivery` | `getOutBoundDeliveryListAsync` | `C03` | Implemented, live preview/import/ack var |
| `c04-outbound-delivery` | `getOutBoundDeliveryListAsync` | `C4` | Implemented, live preview/import/ack var |
| `g01-inbound-atf` | `getInboundATFListAsync` | `G01` | Implemented, live preview/import/ack var |
| `g02-inbound-delivery` | `getInboundDeliveryListAsync` | `G02` | Implemented, live preview/import/ack var |
| `inventory-count-ext-view` | `getViewDataAsync` | `vw_stok_duzeltme` | Implemented, live preview/import/ack var |

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
- `received-company-order-sync` icin `LiveOperationName` genelde `addOutboundOrderV2` olmalidir.
- `warehouse-inbound-order-sync` icin `LiveOperationName` genelde `addInboundOrderV2` olmalidir.
- `company-receiving-sync` icin `LiveOperationName` genelde `addInboundOrderV2` olmalidir.
- `firm-master-sync` icin `LiveOperationName` `addFirmMaster+addFirmAddress` olarak tutulur.
- `inventory-count-sync` Live modda `getViewData(vw_stok_duzeltme)+updIntegrationTable(ENT011)` davranisiyla AXATA EXT -> Mikro calisir.
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
- `live/axata/outbound-deliveries/c01|c02|c03|c04/import` AXATA'dan canli okur ve Mikro'ya yazar.
- `live/axata/inbound-atf/g01/import` AXATA G01 ATF satirlarini firma mal kabul hareketine cevirir.
- `live/axata/inbound-deliveries/g02/import` AXATA'dan canli okur ve mevcut Mikro bekleyen sevk fisini kabul eder.
- `live/axata/dynamic-census/import` AXATA EXT `vw_stok_duzeltme` satirlarini Mikro dynamic census hareketine cevirir.
- `manual/axata/*` endpointleri AXATA'dan canli okumaz; body UI veya operasyon tarafindan saglanir.
- `inventory-count-sync` Live modda Mikro sayim payload'i degil, AXATA EXT dynamic census importu calistirir; UI bunu acik etiketlemelidir.
- `firm-master-sync` icin live dispatch butonu gosterilebilir.
- `product-master-sync` icin toplu live dispatch ve urun koduyla tekli dispatch butonu gosterilebilir.
- `issued-warehouse-order-sync` aday listesinde `warehouseNo`, hedef depo degil AXATA kaynak/cikis depodur.
- `live/audit/overview` veri yazmaz; kontrol ve karar ekranidir.
- C02/C03/C4 icin UI preview ve import/ack butonu acabilir; yazmadan once ozel preview sonucunda `canImport=true` aranmalidir.
- C02 import `CompanyMovement` yazma rotasina uyar: `Database` modunda DB transaction, `MikroApi` modunda `IrsaliyeKaydetV2` calisir. UI ayni endpointi kullanir; mod secimi backend config isidir.
- C03/C4 legacy importlar su an DB yazimidir; Mikro API kontrati netlesmeden `MikroWriteRouting` ile API yoluna alinmamalidir.
- G01 icin `live/axata/inbound-atf/g01/*`, G02 icin `live/axata/inbound-deliveries/g02/*` route'lari kullanilmalidir.

## Bilinen Sinirlar

- Job listesi ve sonuc detaylari kalici DB'de tutulmaz.
- Outbox basarisi "AXATA kabul etti" anlamina gelmez.
- Firma master task'i canli WCF dispatch yapar; `addFirmMaster` ve `addFirmAddress` birlikte cagrilir.
- Urun master task'i `Live` modunda `ENT004`, `ENT003_List` ve `ENT004_UNIT_List` iceren `addSKUMaster` paketlerini canli gonderir.
- C01 ve G02 belge bazli rescue vardir; C02/C03/C4/G01 icin AXATA belge numarasi ile tek belge fetch/import endpoint'i yoktur.
- C01 audit/panel iki ayri eksigi ayirir: gercekten Mikro sevk/link yoksa C01 import/rescue calistirilabilir; Mikro sipariste `ssip_teslim_miktar` doluysa veya mevcut sevk fisi bulunuyorsa otomatik import kapatilir ve belge manuel link/evrak izi incelemesine dusurulur.
- `AXATA teslim miktari Mikro siparis kalan miktarindan buyuk` hatasi genelde siparis teslim miktari zaten kapanmisken ayni AXATA sevkini tekrar Mikro'ya cevirmeye calismak anlamina gelir. Bu durumda ikinci sevk basilmamali; mevcut Mikro evragi ve `STOK_HAREKETLERI_EK.sth_subesip_uid` linki kontrol edilmelidir.
- Mevcut C01 Mikro sevk aramasi performans icin `STOK_HAREKETLERI` uzerinde tarih, depo, hareket tipi ve stok kodu ile daraltilir; bellek tarafinda once aciklama=siparis no, sonra ayni stok/miktar imzasi eslestirilir. Pencere siparis tarihinden 1 gun once baslar ve 7 gun sonrasina kadar bakar.
- EXT `getViewDataAsync` tabanli DynamicCensus import vardir; kalici inbox/retry tablosu yoktur.
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

1. Kalici job/audit/retry tablolari.
2. Ack/retry monitor ekrani.
3. Dispatch sonucunu DB'de saklayan reconcile katmani.
4. C02/C03/C4/G01 icin belge numarasi ile AXATA'dan tek belge fetch/import endpointleri.
5. C03/C4 legacy hareketleri ve DynamicCensus stok duzeltmeleri icin Mikro API kontrati netlesirse route bazli `MikroApi` yazma destegi.
6. Master data icin ek canli dispatch aileleri:
   - `addSKUBarcode`
   - `addSKUPalet`
