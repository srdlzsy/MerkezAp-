# AXATA Entegrasyon Altyapisi

Bu dokuman, AXATA senkronizasyon modulunun mevcut teknik durumunu, calisma mantigini, operasyonel kullanim sinirlarini ve build/migration notlarini aciklayici sekilde toplar.

## Ozet

Mevcut modul iki amaci birlikte karsilar:

- `Mikro -> AXATA` yonunde canli Mikro verisinden payload uretmek ve operasyonel canli dispatch acmak
- Worker'dan bagimsiz manuel kurtarma endpoint'leri saglamak

Bugunku durum net olarak sudur:

- AXATA endpoint erisimi `health/probe` seviyesinde test edilebiliyor
- Mikro kaynakli belgeler tekil veya toplu secilip AXATA payload'ina donusturulebiliyor
- `DryRun` ve `Outbox` modlari destekleniyor
- `issued-warehouse-order-sync` ve `company-receiving-sync` icin canli `dispatch` endpoint'leri task bazli operation config ile legacy worker SOAP operasyonlarina gore calisir
- AXATA ham verisi operasyon tarafinda toparlanirsa `AXATA -> Mikro` yonunde hem normal manuel create/accept hem de AXATA-native body endpoint'leri kullanilabiliyor
- Ancak su an repo icinde "AXATA belge numarasi ver, canli SOAP'tan cek, sonra Mikro'ya yaz" yapan bir adapter yok

Kisacasi: payload uretim, manuel kurtarma ve secili task'larda canli dispatch hazir; canli AXATA fetch/ack transport kati halen sonraki fazin isi.

## Tasarim Niyeti

Bu modulun amaci eski `Furpa.WorkerService` kodunu birebir kopyalamak degildir.

Asil hedef sunlardir:

- eski worker'in kullandigi AXATA yonlerini task/handler mantigiyla normalize etmek
- ileride yeni worker yazilirken ayni queue, scheduler, preview ve dispatch omurgasini tekrar kullanmak
- operasyon ekibine worker'dan bagimsiz manuel kurtarma ve manuel aktarim ekranlari vermek
- canli SOAP dispatch, native import ve bekleyen kabul senaryolarini tek modulde toplamak

Bu nedenle burada "legacy ile birebir ayni operasyon sayisi" degil, "legacy mantigi icin uygun altyapi ve operasyonel acik kapilar" onceliklidir.

## Hedef

Eski `Furpa.WorkerService` mantigini API icine tasirken su hedefler izlendi:

- zamanli worker akisiyla manuel operasyon akislarini birbirinden ayirmak
- UI tarafina task overview, preview, health ve job izleme yuzeyi vermek
- gercek SOAP dispatch yazilmadan once payload/outbox merkezli saglam bir ara katman kurmak
- eski worker'daki dispatch kontratini API icine tasiyip tek evrak/toplu evrak canli gonderimini acmak
- ileride canli AXATA fetch ve ack katini minimum refactor ile bu altyapiya takabilmek

## Yonalimlar

### Mikro -> AXATA

Bu yonde sistem sunlari yapar:

- canli Mikro verisini okur
- task bazli normalize payload uretir
- preview doner
- ister worker kuyruguyla, ister manuel endpoint ile `DryRun` veya `Outbox` calistirir
- desteklenen task'larda canli `dispatch` endpoint'i ile SOAP gonderimi yapar

Bu yone ait evrak bazli manuel kurtarma task'lari:

- `issued-warehouse-order-sync`
- `company-receiving-sync`
- `inventory-count-sync`

Master task'lar:

- `firm-master-sync`
- `product-master-sync`

### AXATA -> Mikro

Bu yonde sistem su an iki seviyede calisir:

- AXATA-native belge bilgisi operasyon tarafinda hazirsa o veri dogrudan ilgili Mikro write use-case'ine map edilir
- AXATA verisi operasyon tarafinda manuel toparlanmis ise body ile create endpoint'lerine post edilir
- depo mal kabul gibi Mikro'ya dusmus ama kabulde takilmis belgeler manuel accept endpoint'leriyle tamamlanir

Bu yone ait kritik sinir:

- canli AXATA fetch yok
- belge no verip AXATA'dan veri cekme yok
- AXATA fetch sonrasi otomatik ack/commit akisi yok

## Legacy Worker Uyum Matrisi

| Eski worker akisi | Yeni altyapi durumu | Operasyon notu |
|---|---|---|
| Firma master push | Payload/preview/outbox var, canli SOAP dispatch yok | worker kolay eklenir ama canli transport henuz yazilmadi |
| Urun master push | Payload/preview/outbox var, canli SOAP dispatch yok | `addSKUMaster/addSKUBarcode/addSKUPalet` fazi eksik |
| Verilen depo siparisi -> AXATA | Payload/preview/outbox + canli dispatch var | tekil/toplu manuel kurtarma hazir |
| Depolar arasi sevk belgesi -> AXATA | Yok | kesilmis sevk belgesini direkt AXATA'ya basan task/endpoint henuz yok |
| Firma mal kabul -> AXATA | Payload/preview/outbox + canli dispatch var | tekil/toplu manuel kurtarma hazir |
| AXATA outbound delivery -> Mikro depolar arasi sevk | Manuel AXATA-native import var | belge body'si elde olmali, canli fetch yok |
| AXATA inbound ATF -> Mikro firma mal kabul | Manuel AXATA-native import var | belge body'si elde olmali, canli fetch yok |
| AXATA inventory count / dynamic census -> Mikro | Serbest body ile manuel incoming var | AXATA EXT polling ve status update yok |
| AXATA status list / ack / commit | Yok | sonraki faz gerektirir |

Bu matrisin anlami su:

- worker yazmak icin task katalogu, queue, scheduler, handler ve manual operation katmani hazir
- ama eski worker'in tum SOAP operasyonlari henuz API icine canli transport olarak tasinmis degil
- ozellikle master sync ve AXATA'dan canli okuma taraflari halen genisleme noktasi

## Eski Worker Ozetine Gore Eklenebilecekler

Kullanici tarafindan paylasilan eski worker ozeti, bugunku `AxataSenkronizasyonu` modulune hangi genislemelerin en anlamli oldugunu net gosteriyor.

### 1. Yeni task aileleri

Bugunku katalog daha cok `Mikro -> AXATA` payload/dispatch tarafina odakli. Eski worker akislarina daha cok yaklasmak icin su task aileleri eklenebilir:

| Onerilen task | Yon | Eski worker karsiligi | Kazanim |
|---|---|---|---|
| `outbound-delivery-import-c01` | `AXATA -> Mikro` | `C_01_OutBoundDeliveryWorker` | AXATA C01 teslimatlarini dogrudan cekip Mikro sevke/movement'a cevirmek |
| `outbound-delivery-import-c02` | `AXATA -> Mikro` | `C_02_OutBoundDeliveryWorker` | Musteri cikis teslimatlarini siparis teslim miktari ile birlikte islemek |
| `outbound-delivery-import-c03` | `AXATA -> Mikro` | `C_03_OutBoundDeliveryWorker` | Iade/ozel cikis tiplerini ayri profile baglamak |
| `outbound-delivery-import-c04` | `AXATA -> Mikro` | `C_04_OutBoundDeliveryWorker` | Diger cikis hareket kodlarini profile baglamak |
| `inbound-delivery-import-g01` | `AXATA -> Mikro` | `G_01_InboundDeliveryWorker` | AXATA mal kabul/ATF kaydini Firma Mal Kabul use-case'ine aktarmak |
| `inbound-delivery-import-g02` | `AXATA -> Mikro` | `G_02_InboundDeliveryWorker` | Depolar arasi giris teslimatlarini Mikro movement + extra ile yazmak |

Bu task'lar eklenirse modul sadece "manual body ile import" degil, "AXATA'dan kontrollu fetch + map + import" yetenegine de kavusur.

### 2. Query profile mantigi

Eski worker kodlarinda fetch davranisi sabit query profilleriyle ilerliyor:

- `CompanyCode`
- `WarehouseCode`
- `MovementType`
- `Status`

Yeni modulde buna uygun bir `FetchProfile` veya `TaskProfile` katmani eklenebilir.

Ornek profiller:

- `C01`: `CompanyCode=01`, `WarehouseCode=01`, `MovementType=C01`, `Status=0`
- `C02`: `MovementType=C02`
- `C04`: profil ailesi `C04` diye adlandirilsa da legacy worker sorgu degeri `MovementType=C4` kullanir
- `G01`: inbound ATF / firma mal kabul profili
- `G02`: depolar arasi giris teslimat profili

Bu yapiyla ayni fetch motoru farkli task'lar icin tekrar kullanilabilir.

### 3. Ack sirasini guvenli hale getirme

Paylasilan `C_01_OutBoundDeliveryWorker` icinde AXATA kaydi `S06STAT = 1` yapildiktan sonra lokal DB yazimi yapiliyor.

Bu tasarimda su risk vardir:

- AXATA "islendi" der
- ama Mikro/Furpa DB yazimi patlarsa veri kaybi veya eksik isleme olusur

Yeni `AxataSenkronizasyonu` icin daha dogru sira su olmalidir:

1. AXATA kaydini fetch et
2. lokal map ve validation yap
3. Mikro/Furpa DB transaction commit et
4. commit basariliysa `updIntegrationTable` ile ack/status guncelle
5. ack hatasi varsa retry edilebilir hale getir

Bu nokta eski worker parity'den bile daha degerli bir iyilestirmedir.

### 4. V1 / V2 operasyon secimi

Eski worker ozetinde siparis gonderimlerinde `addOutboundOrderV2Async` ve `addInboundOrderV2Async` notlari geciyor.
Bu destek bugun kodda vardir:

- task bazli `LiveOperationName` config'i kullanilabilir
- `V1` ve `V2` transport secimi ayni payload icin config uzerinden yapilabilir
- `issued-warehouse-order-sync` varsayilan uygulama ayarinda `addOutboundOrderV2`
- `company-receiving-sync` varsayilan uygulama ayarinda `addInboundOrderV2`
- config verilmezse geriye uyumluluk icin `addOutboundOrder` / `addInboundOrder` fallback'i calisir

Boylece sahadaki AXATA servis kontrati farkliysa kod degistirmeden task bazli transport secilebilir.

### 5. Mapping katmanini task bazli zenginlestirme

Eski delivery worker'lar yalnizca `ProductMovement` yazmiyor; bazi akislarda sunlari da yapiyor:

- `Order.DeliveredQuantity` guncelleme
- `InterWarehouseOrder.DeliveredQuantity` guncelleme
- `ProductMovementExtra` yazma
- `WarehouseOrderGuid` / `OrderGuid` baglama

Bu yuzden yeni modulde `AXATA -> Mikro` task'lari icin salt generic import yerine task bazli `post-processing` kural seti olmasi gerekir.

### 6. Manuel operasyon icin yeni endpoint ailesi

Bugunku manuel incoming endpoint'ler body-based calisiyor. Eski worker mantigina daha yakin olmak icin su aile eklenebilir:

- `POST /api/integrations/axata-sync/manual/fetch/outbound-deliveries/{profileCode}/preview`
- `POST /api/integrations/axata-sync/manual/fetch/outbound-deliveries/{profileCode}/execute`
- `POST /api/integrations/axata-sync/manual/fetch/inbound-deliveries/{profileCode}/preview`
- `POST /api/integrations/axata-sync/manual/fetch/inbound-deliveries/{profileCode}/execute`

Bu sayede operasyon ekibi AXATA body toplamak zorunda kalmadan, tanimli profil uzerinden sistemin kendisi fetch yapabilir.

## Mimari Bilesenler

### Application Katmani

Ana contract:

- `IAxataSynchronizationService`

Tasiyan modeller:

- overview DTO'lari
- preview DTO'lari
- health/probe DTO'lari
- job DTO'lari
- tekil manuel belge DTO'lari
- aday liste DTO'lari
- toplu manuel belge DTO'lari

Bu katmanin sorumlulugu transport degil, API ile altyapi arasindaki sozlesmeyi sabitlemektir.

### Infrastructure Katmani

Runtime bilesenleri:

- `AxataSynchronizationCatalog`
  Task tanimlari, kodlari, akislari ve sistem yonlerini tutar.
- `AxataSynchronizationQueue`
  Manuel veya scheduler kaynakli job'lari kuyruklar.
- `AxataSynchronizationWorker`
  Kuyruktaki isi arka planda calistirir.
- `AxataSynchronizationScheduler`
  Konfigurasyona gore task tetikler.
- `AxataSynchronizationExecutionCoordinator`
  Task code ile dogru handler'i eslestirir.
- `AxataSynchronizationOutboxWriter`
  `Outbox` modunda payload'lari klasore artifact olarak yazar.
- `AxataSynchronizationLiveTransportService`
  Eski worker SOAP operasyon isimlerine gore canli AXATA dispatch envelope'i uretir ve gonderir.
- `AxataSynchronizationConnectionProbeService`
  Mikro SQL, Furpa SQL ve AXATA endpoint probe sonuclarini toplar.
- `AxataSynchronizationManualDocumentService`
  tekil belge preview/execute
  aday belge listeleme
  toplu belge preview/execute
  tekil/toplu canli dispatch
  akislarinin merkezidir.

Task handler'lari:

- `FirmMasterSyncTaskHandler`
- `ProductMasterSyncTaskHandler`
- `IssuedWarehouseOrderSyncTaskHandler`
- `CompanyReceivingSyncTaskHandler`
- `InventoryCountSyncTaskHandler`

### WebApi Katmani

Ana controller:

- `api/integrations/axata-sync`

Bu controller uzerinden su yuzeyler acilir:

- overview
- health
- fetch profile katalogu
- task preview
- worker queue job olusturma
- tekil manuel belge preview/execute
- toplu manuel belge preview/execute
- manuel incoming create/accept
- toplu incoming create/accept
- bekleyen depo mal kabul liste/detail

## Desteklenen Task Katalogu

### 1. `firm-master-sync`

- Akis: `Mikro -> AXATA`
- Depo bagimsizdir
- Cari hesap verilerini AXATA master payload'ina cevirir

### 2. `product-master-sync`

- Akis: `Mikro -> AXATA`
- Depo bagimsizdir
- Stok ve barkod verilerini AXATA master payload'ina cevirir

### 3. `issued-warehouse-order-sync`

- Akis: `Mikro -> AXATA`
- `warehouseNo` gerektirir
- Verilen depo siparislerini belge bazli payload'a donusturur
- Tekil ve toplu manuel belge kurtarma destekler

### 4. `company-receiving-sync`

- Akis: `Mikro -> AXATA`
- `warehouseNo` gerektirir
- Firma mal kabul belgelerini payload'a donusturur
- Tekil ve toplu manuel belge kurtarma destekler

### 5. `inventory-count-sync`

- Akis: `Mikro -> AXATA`
- `warehouseNo` gerektirir
- Sayim sonucu belgelerini payload'a donusturur
- Tekil ve toplu manuel belge kurtarma destekler

## Calisma Modlari

### `DryRun`

- payload uretilir
- artifact dosyasi yazilmaz
- operasyon ekibi JSON'u inceleyebilir
- test ve dogrulama icin idealdir

### `Outbox`

- payload uretilir
- `App_Data/AxataSynchronizationOutbox` altina JSON artifact yazilir
- su an gercek SOAP dispatch yerine operasyonel cikti gorevi gorur

### `Live Dispatch`

- secilen belge aninda AXATA main endpoint'ine SOAP olarak gonderilir
- response icindeki `state` ve `message` alanlari yakalanir
- request XML ve response XML operasyonel inceleme icin response body'de dondurulur
- su an yalnizca `issued-warehouse-order-sync` ve `company-receiving-sync` icin tanimlidir

Not:

- Bu surumde `Outbox`, AXATA'ya gercek gonderim degildir
- Yani `Outbox` = "hazir payload dosyalandi", "AXATA teslim aldi" degildir

## Endpoint Gruplari

### 1. Genel Izleme

- `GET /api/integrations/axata-sync`
- `GET /api/integrations/axata-sync/health`
- `GET /api/integrations/axata-sync/fetch-profiles`
- `GET /api/integrations/axata-sync/jobs/{jobId}`

Not:

- overview icindeki task kayitlari artik `SupportsManualDocuments` ve `SupportsLiveDispatch` capability alanlarini da tasir
- `fetch-profiles` endpoint'i eski worker parity icin planlanan AXATA fetch/import profillerini ve bugunku fallback route'larini listeler

### 2. Worker Tetikleme

- `POST /api/integrations/axata-sync/jobs`
- `POST /api/integrations/axata-sync/tasks/{taskCode}/execute`

Bu grup queue tabanlidir ve arka planda calisir.

### 3. Task Preview

- `GET /api/integrations/axata-sync/tasks/{taskCode}/preview?warehouseNo=1&take=10`

Bu endpoint task genelinden preview alir; secili belge mantigi yoktur.

### 4. Tekil Manuel Belge Kurtarma

- `GET /api/integrations/axata-sync/manual/tasks/{taskCode}/documents/candidates`
- `POST /api/integrations/axata-sync/manual/tasks/{taskCode}/documents/preview`
- `POST /api/integrations/axata-sync/manual/tasks/{taskCode}/documents/execute`

Amaç:

- tek belgeyi secmek
- payload'i incelemek
- gerekirse aninda `DryRun` veya `Outbox` almak

### 5. Canli Manuel Dispatch

- `POST /api/integrations/axata-sync/manual/tasks/{taskCode}/documents/dispatch`
- `POST /api/integrations/axata-sync/manual/tasks/{taskCode}/documents/dispatch-batch`

Amac:

- secilen evraki worker kuyruguna girmeden AXATA'ya gercekten basmak
- eski worker'daki `addOutboundOrder*` ve `addInboundOrder*` kontratini task bazli config ile API icine tasimak
- request XML, response XML ve AXATA `state/message` sonucunu ayni response'ta almak

### 6. Toplu Manuel Belge Kurtarma

- `POST /api/integrations/axata-sync/manual/tasks/{taskCode}/documents/preview-batch`
- `POST /api/integrations/axata-sync/manual/tasks/{taskCode}/documents/execute-batch`

Amaç:

- secili evrak listesini tek cagrida ele almak
- toplu retry/kurtarma operasyonunu queue beklemeden yapmak

Batch davranisi:

- `ContinueOnError = true` ise response `Failures` listesi doner
- bir evrak patlasa bile digerleri devam eder
- `ContinueOnError = false` ise ilk hatada request fail olur

### 7. AXATA-Native Manuel Import

- `POST /api/integrations/axata-sync/manual/axata/outbound-deliveries/inter-warehouse-shipments`
- `POST /api/integrations/axata-sync/manual/axata/outbound-deliveries/inter-warehouse-shipments/batch`
- `POST /api/integrations/axata-sync/manual/axata/inbound-atf/company-receivings`
- `POST /api/integrations/axata-sync/manual/axata/inbound-atf/company-receivings/batch`

Bu grup AXATA tarafindan bilinen belge alanlarini minimum donusumle Mikro write use-case'lerine baglar.

### 8. AXATA -> Mikro Manuel Incoming

- `POST /api/integrations/axata-sync/manual/incoming/company-receivings`
- `POST /api/integrations/axata-sync/manual/incoming/company-receivings/batch`
- `POST /api/integrations/axata-sync/manual/incoming/inventory-counts`
- `POST /api/integrations/axata-sync/manual/incoming/inventory-counts/batch`

Bu endpoint'ler AXATA ham verisi operasyonda daha serbest bir body ile hazirlandiginda kullanilir.

### 9. Bekleyen Depo Mal Kabul Operasyonlari

- `GET /api/integrations/axata-sync/manual/incoming/warehouse-receivings`
- `GET /api/integrations/axata-sync/manual/incoming/warehouse-receivings/{documentSerie}/{documentOrderNo}`
- `POST /api/integrations/axata-sync/manual/incoming/warehouse-receivings/{documentSerie}/{documentOrderNo}/accept`
- `POST /api/integrations/axata-sync/manual/incoming/warehouse-receivings/accept-batch`

Bu grup, belge Mikro'ya gelmis ama kabulde takilmis senaryolarda kullanilir.

## Manuel Aktarim Cevap Matrisi

Kullanici tarafinda en kritik soru su oldugu icin bunu acik yazmak gerekir:

"Depo sevki kesildi, bu evragi manuel olarak Mikro'dan AXATA'ya veya AXATA'dan Mikro'ya alabilir miyiz?"

| Senaryo | Mumkun mu? | Kullanilacak yol | Kritik sinir |
|---|---|---|---|
| Mikro'da verilen depo siparisi var, AXATA'ya yeniden gonderilecek | Evet | `manual/tasks/issued-warehouse-order-sync/documents/*` ve gerekirse `dispatch*` | belge Mikro'da okunuyor, AXATA'dan fetch yok |
| Mikro'da kesilmis depolar arasi sevk belgesi var, bunu direkt AXATA'ya gondermek isteniyor | Hayir | su an endpoint yok | yeni task + handler + live transport map'i gerekir |
| Mikro'da firma mal kabul belgesi var, AXATA'ya yeniden gonderilecek | Evet | `manual/tasks/company-receiving-sync/documents/*` ve gerekirse `dispatch*` | belge Mikro'da okunuyor, task config'ine gore `addInboundOrder*` kontrati kullaniliyor |
| AXATA outbound delivery bilgisi elde var, Mikro'da depolar arasi sevk yaratmak isteniyor | Evet | `manual/axata/outbound-deliveries/inter-warehouse-shipments*` | belge body'si operasyonda hazir olmali |
| AXATA inbound ATF bilgisi elde var, Mikro'da firma mal kabul yaratmak isteniyor | Evet | `manual/axata/inbound-atf/company-receivings*` | belge body'si operasyonda hazir olmali |
| AXATA'daki belge sadece belge numarasi ile biliniyor, sistem gidip AXATA'dan cekip Mikro'ya yazsin isteniyor | Hayir | su an endpoint yok | canli fetch adapter'i eklenmeli |
| Sevk zaten Mikro'ya dusmus ama kabulde kalmis | Evet | `manual/incoming/warehouse-receivings*` | burada yeni belge cekilmez, mevcut belge tamamlanir |

## Operasyonel Senaryolar

### Senaryo A: Mikro belgesi AXATA'ya dusmedi

Onerilen akis:

1. `manual/tasks/{taskCode}/documents/candidates` ile adaylari listele
2. hedef belgeyi sec
3. `.../documents/preview` veya `.../documents/preview-batch` ile payload'i incele
4. sadece payload gerekiyorsa `.../documents/execute` veya `.../documents/execute-batch` calistir
5. gercek gonderim gerekiyorsa `.../documents/dispatch` veya `.../documents/dispatch-batch` kullan
6. AXATA response icindeki `state/message` bilgisini kaydet

### Senaryo B: Cok sayida belge toplu yeniden gonderilecek

Onerilen akis:

1. aday listeyi al
2. secilen belgeleri batch body'ye koy
3. once `preview-batch`
4. payload only gerekiyorsa `execute-batch`
5. gercek sevk gerekiyorsa `dispatch-batch`
6. `Failures` listesini ayrica raporla

### Senaryo C: AXATA verisi elde var ama sistem AXATA'dan cekemiyor

Onerilen akis:

1. operasyon ekibi AXATA verisini disaridan toplar
2. veri eski worker alan adlariyla hazirsa `manual/axata/outbound-deliveries/inter-warehouse-shipments` veya `manual/axata/inbound-atf/company-receivings` kullanilir
3. daha serbest body gerekiyorsa `manual/incoming/company-receivings` veya `manual/incoming/inventory-counts` endpoint'ine post edilir
4. coklu belge varsa ilgili batch endpoint kullanilir

### Senaryo D: Depo sevki Mikro'ya geldi ama kabulde takildi

Onerilen akis:

1. `manual/incoming/warehouse-receivings` ile bekleyenleri listele
2. gerekiyorsa detail endpoint ile satirlari incele
3. tek belge ise `.../accept`
4. coklu belge ise `.../accept-batch`

### Senaryo E: Hedef eski worker'i kopyalamak degil, onun rahat yazilacagi zemini kurmak

Bu hedef acisindan mevcut altyapi sunlari saglar:

1. task kodlari ve yonleri kataloglanmis durumda
2. her task icin ayrik handler modeli var
3. worker queue ve scheduler hosted service olarak hazir
4. preview/dry-run/outbox ile gercek gonderim oncesi dogrulama katmani var
5. operasyon ekibi icin worker'dan bagimsiz manuel endpoint yuzeyi var
6. canli dispatch gereken iki kritik task icin legacy SOAP envelope uretimi mevcut

Bu da yeni worker yazilirken ayni handler'larin tekrar kullanilabilecegi, eksik SOAP operasyonlarinin ise ayri ayri eklenebilecegi anlamina gelir.

## AXATA Okuma/Yazma Yetkinligi

Bu baslik kritik cunku operasyon tarafinda en cok bu soru sorulur.

### Elimizdeki AXATA servisinin sagladigi yetenek siniflari

Bugunku bilgiye gore elimizde iki farkli servis ailesi var:

- `AxataServicePool.svc`  -> ana operasyon servisi
- `AxataServicePoolEXT.svc` -> integration table / view / status update servisi

Bu iki servis birlikte bize teorik olarak su capability setini veriyor:

| Yetkinlik | Tipik operasyonlar | Ne ise yarar |
|---|---|---|
| `Push/Dispatch` | `addOutboundOrder*`, `addInboundOrder*`, `addFirmMaster`, `addFirmAddress`, `addSKUMaster`, `addSKUBarcode`, `addSKUPalet` | Mikro/Furpa verisini AXATA'ya gondermek |
| `Fetch/List` | `getOutBoundDeliveryListAsync`, `getInboundATFListAsync`, `getInboundDeliveryListAsync` | AXATA'da olusmus teslimat veya kabul verisini cekmek |
| `Status/Ack` | `updIntegrationTableAsync` | AXATA entegrasyon satirini "islendi" durumuna almak |
| `Generic View Read` | `getViewDataAsync` | AXATA EXT tarafindaki view tabanli verileri okuyup Mikro'ya islemek |

Bu tablo bize sunu soyler:

- bugunku API modulunde `Push/Dispatch` tarafinin bir kismi kullaniliyor
- `Fetch/List` ve `Status/Ack` taraflari ise halen eksik parity alani
- yani servis yetenegi var, uygulama adaptoru henuz tam degil

### Su an var olan

- AXATA Main endpoint probe
- AXATA EXT endpoint probe
- Mikro verisinden AXATA formatina payload donusumu
- `issued-warehouse-order-sync` icin canli `addOutboundOrder*` dispatch
- `company-receiving-sync` icin canli `addInboundOrder*` dispatch
- AXATA-native outbound delivery -> Mikro depolar arasi sevk mapleme
- AXATA-native inbound ATF -> Mikro firma mal kabul mapleme
- manuel incoming body kabul katmani
- bekleyen depo mal kabul belge accept katmani

### Su an olmayan

- AXATA belge numarasina gore canli fetch
- AXATA SOAP response parse edip Mikro create/accept baslatma
- AXATA fetch sonrasi otomatik ack/commit
- inventory count icin eski worker benzeri Mikro -> AXATA canli push kontrati
- retry/ack store mekanizmasi
- `firm-master-sync` icin canli `addFirmMaster/addFirmAddress` dispatch
- `product-master-sync` icin canli `addSKUMaster/addSKUBarcode/addSKUPalet` dispatch
- AXATA EXT tarafinda `getViewData/updIntegrationTable` is akislarini kullanan canli adapter
- job ve dispatch sonucunun kalici DB persistence'i
- Mikro'daki `depolar-arasi-sevk` belgesini AXATA'ya direkt tasiyan canli task/endpoint
- `getOutBoundDeliveryListAsync`, `getInboundATFListAsync`, `getInboundDeliveryListAsync` bazli canli fetch task'lari
- task bazli `V1/V2` SOAP operasyon secimi

Yani sistem su an "AXATA'yi hic bilmiyor" degil; belirli task'larda canli yazabiliyor. Ama belge no ile canli okuyan tam adapter seviyesine henuz ulasmis degil.

## Endpoint Bosluk Analizi

Kod tarafinda mevcut route'lara bakildiginda bu modul operasyon icin yeterli bir ilk katman veriyor; ancak eski worker kapsami icin halen eksik endpoint aileleri var:

### Bugun olan route aileleri

- task overview / preview / queue / job detail
- tekil ve toplu manuel `preview`
- tekil ve toplu manuel `execute`
- tekil ve toplu manuel `dispatch`
- AXATA-native outbound delivery import
- AXATA-native inbound ATF import
- serbest body ile manuel incoming company receiving
- serbest body ile manuel incoming inventory count
- bekleyen depo mal kabul liste/detail/accept

### Henuz olmayan route aileleri

- `GET /api/integrations/axata-sync/manual/axata/...` tipinde belge no ile canli fetch route'lari
- `POST /api/integrations/axata-sync/manual/axata/.../ack` tipinde teslim/onay route'lari
- firm/product master icin canli dispatch route'lari
- depolar arasi sevk belgesi icin Mikro -> AXATA canli dispatch route'lari
- AXATA fetch profile'lari ile `preview/execute` route'lari
- AXATA EXT polling veya status update route'lari
- dispatch sonucu tekrar deneme, reconcile veya audit route'lari

Sonuc:

- "manuel operasyon modulu" hedefi icin mevcut endpoint seti anlamli ve kullanilabilir
- "eski worker birebir feature parity" hedefi icin route kapsami halen tamam degil

## Yetki ve Migration Durumu

Auth tarafinda AXATA senkronizasyon menusu icin permission migration'i alinmistir:

- `20260429143000_AddAxataSynchronizationPermissions.cs`
- `20260429143000_AddAxataSynchronizationPermissions.Designer.cs`
- `AuthDbContextModelSnapshot.cs`

Uygulama acilisinda `AuthDbContext` icin `Database.MigrateAsync()` calistigi icin bu permission migration'lari startup'ta otomatik uygulanir.

Bu migration su kazanimlari getirir:

- `entegrasyon-islemleri.axata-senkronizasyonu.list`
- `entegrasyon-islemleri.axata-senkronizasyonu.detail`
- `entegrasyon-islemleri.axata-senkronizasyonu.create`
- `entegrasyon-islemleri.axata-senkronizasyonu.update`

Ayrica admin role mapping'leri de eklenmistir.

Onemli not:

- Bu dokumanda anlatilan toplu manuel endpoint genisletmesi icin yeni EF migration gerekmedi
- Cunku yapilan degisiklikler controller/service/DTO seviyesinde kaldi
- veri modeli veya DbContext yapisi degismedi

Bugunku kod gercegine gore zorunlu migration sonucu:

- mevcut manuel endpointler icin ek migration gerekmiyor
- mevcut queue/job sistemi in-memory oldugu icin job tablosu gerekmiyor
- `Outbox` filesystem tabanli oldugu icin artifact tablosu gerekmiyor
- Auth tarafinda yalnizca permission migration'i gerekliydi ve alinmis durumda

Ancak su hedefler istenirse yeni migration acilmasi mantiklidir:

- dispatch sonucunu kalici saklamak
- AXATA'dan gelen ham belgeyi inbox mantigiyla saklamak
- retry/ack/reconcile kayitlari tutmak
- evrak esleme ve tekrar gonderim logu saklamak
- fetch edilmis delivery kayitlarinda idempotency/islenme izi tutmak

Worker-ready ikinci faz icin onerilen tablolar:

- `AxataIntegrationJobs`
- `AxataIntegrationJobArtifacts`
- `AxataDispatchAuditLogs`
- `AxataIncomingDocumentInbox`
- `AxataReconciliationLogs`
- `AxataIncomingDeliveryImports`
- `AxataIncomingDeliveryImportLines`

Bu tablolar bugun zorunlu degil; ama ileride "servis restart olsa bile job/ack izlenebilir olsun" ihtiyaci icin en dogru migration adimidir.

Ozet migration karari:

- sadece yeni task/handler/controller eklenirse migration zorunlu degil
- ama canli fetch + idempotent import + ack retry guvenligi istenirse yeni tablo acmak kuvvetle tavsiye edilir

## Konfigurasyon

`src/FurpaMerkezApi.WebApi/appsettings.json` icinde `AxataSynchronization` bolumu kullanilir.

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
- `OutboxBasePath`
- `Tasks.{taskCode}.Enabled`
- `Tasks.{taskCode}.ScheduleEnabled`
- `Tasks.{taskCode}.IntervalMinutes`
- `Tasks.{taskCode}.DefaultWarehouseNo`

Canli dispatch icin ek not:

- `Username` ve ozellikle `Password` dolu olmadan `dispatch` endpoint'leri calismaz
- `MainEndpointUrl` eski worker'daki `AxataServicePool.svc` ana servisine bakmalidir

## Build ve Dogrulama Notu

Repo `global.json` ile `9.0.200` SDK'sina sabitlenmistir.

Bu ortamda paralel build cagrilari bazi durumlarda su semptomla dusuyordu:

- `0 Uyari`
- `0 Hata`
- buna ragmen `Build Failed`

Guvenilir calisan komut deseni su oldu:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
$env:MSBUILDUSESERVER='0'
dotnet build FurpaMerkezApi.sln --no-restore -maxcpucount:1
```

Eger `WebApi/bin/Debug` altinda kilitli DLL varsa ayni dogrulama ayri output klasorune yonlendirilerek yapilabilir:

```powershell
$env:MSBuildEnableWorkloadResolver='false'
$env:MSBUILDUSESERVER='0'
dotnet build FurpaMerkezApi.sln --no-restore -maxcpucount:1 -p:OutDir="artifacts\\axata-verify\\"
```

Bu komutla solution build basarili sekilde tamamlandi.

Katman bazli dogrulanan ciktilar:

- `FurpaMerkezApi.Domain.dll`
- `FurpaMerkezApi.Application.dll`
- `FurpaMerkezApi.Infrastructure.dll`
- `FurpaMerkezApi.WebApi.dll`

Canli AXATA dispatch notu:

- kod derleme seviyesinde dogrulandi
- AXATA WSDL ve canli endpoint bu ortamdan erisilemedigi icin SOAP envelope'lerin sahada endpoint/credential ile test edilmesi gerekir

## Sonraki Faz Onerisi

Bir sonraki teknik adimlar:

- belge numarasina gore canli AXATA fetch endpoint'i eklemek
- dispatch sonrasi ack/retry/store mekanizmasi eklemek
- `inventory-count-sync` icin eski worker benzeri canli push kontratini netlestirmek
- `firm-master-sync` icin `addFirmMaster/addFirmAddress` transport katini eklemek
- `product-master-sync` icin `addSKUMaster/addSKUBarcode/addSKUPalet` transport katini eklemek
- AXATA EXT icin canli incoming polling + status update adapter'i yazmak

Bu katman gelince su yetenekler ayni altyapiya eklenebilir:

- `LiveDispatch`
- belge no ile canli AXATA fetch
- AXATA response/ack parse etme
- retry + status persistence
- outbox'tan gercek transport'a terfi

Bu sebeple mevcut modul "nihai entegrasyon" degil, ama canli transport baglamaya hazir operasyonel omurgadir.
