# FurpaMerkezApi Proje Genel Isleyisi

Bu dokuman FurpaMerkezApi projesine yeni giren birinin sistemi hizli ve saglam anlamasi icin hazirlandi.

Kisa indeks:

```text
Bu API ne yapar?
Katmanlar nasil ayrilir?
Hangi DB ne icin kullanilir?
Auth, rol, permission ve menu nasil calisir?
Bir istek hangi adimlardan gecer?
Yeni endpoint veya ekran eklenirken nereye dokunulur?
Migration ne zaman gerekir?
GreenGrocer, e-irsaliye, timeout ve terminal IP gibi ozel isler nerede durur?
```

Ek dokumanlar:

- Kisa repo girisi: [../README.md](../README.md)
- Dokuman haritasi: [README.md](README.md)
- UI endpoint dokumani: [UI_API_DOKUMANI.md](UI_API_DOKUMANI.md)
- Yeni menu/yetki/migration rehberi: [YENI_MENU_YETKI_MIGRATION_REHBERI.md](YENI_MENU_YETKI_MIGRATION_REHBERI.md)

## Projenin Kisa Ozeti

`FurpaMerkezApi`, Furpa merkez operasyonlari icin yazilmis `.NET 9` Web API projesidir.

Ana gorevler:

```text
kullanici girisi ve JWT uretimi
kullanici, rol ve permission yonetimi
frontend menu ve buton yetkilerini beslemek
Mikro/Furpa kaynaklarindan liste, detay ve rapor verisi okumak
Mikro veya Furpa tarafina kontrollu yazma yapmak
fatura, sevk, iade, siparis, mal kabul, stok, kasa ve rapor ekranlarini beslemek
Uyumsoft e-fatura/e-irsaliye entegrasyonlarini calistirmak
Axata, POS muhasebe, Shopigo ciro gibi dis kaynaklari okumak
uzun surebilecek isleri background queue/worker ile yonetmek
terminal/mobil offline ve operasyon destek akislarini takip etmek
```

En kisa istek akisi:

```text
Frontend
  -> WebApi Controller
  -> Application contract/interface
  -> Infrastructure service/use case/query executor
  -> DbContext / WriteService / Integration Client
  -> DTO
  -> HTTP Response
```

## Katmanlar

Projede 4 ana katman vardir:

```text
Domain
Application
Infrastructure
WebApi
```

Bu yapi Clean Architecture cizgisine yakindir. Pratikte use case implementasyonlarinin buyuk kismi `Infrastructure` icindedir. `Application` daha cok kontrat, DTO ve sistem dili katmanidir.

## Domain Katmani

Domain cekirdek entity'leri ve temel kurallari tasir.

Ornek entity'ler:

```text
AppUser
AppRole
AppPermission
AppUserRole
AppRolePermission
Announcement / AnnouncementTarget / AnnouncementRead
FeedbackItem
DocumentFlow / DocumentFlowEvent
StockAnomaly / StockAnomalyEvent
MikroApiWriteAudit
MobileOfflineSyncRequest
UyumsoftInboxInvoice
GreenGrocerProductCaseProfile
GreenGrocerOrderLineSnapshot
DespatchDriver
```

Domain'in gorevi:

```text
entity alanlarini normalize etmek
zorunlu degerleri korumak
temel audit alanlarini tasimak
DB iliski modelini ifade etmek
```

Domain katmaninda HTTP, controller, EF sorgu detayi veya dis servis payload'u tutulmaz.

## Application Katmani

Application projenin kontrat ve dil katmanidir.

Burada sunlar bulunur:

```text
request modelleri
response / DTO modelleri
use case interface'leri
servis interface'leri
permission catalog
permission tree builder
ortak enum/constant tanimlari
```

Ornekler:

```text
src/FurpaMerkezApi.Application/Security/PermissionCatalog.cs
src/FurpaMerkezApi.Application/Security/PermissionCodes.cs
src/FurpaMerkezApi.Application/Security/PermissionTreeBuilder.cs
src/FurpaMerkezApi.Application/Modules/AyarIslemleri/Soforler/
src/FurpaMerkezApi.Application/Modules/GreenGrocer/ProductCases/
```

Bu katman "ne yapilabilir?" sorusuna cevap verir. "Nasil yapilir?" sorusu genelde Infrastructure katmanindadir.

## Infrastructure Katmani

Infrastructure sistemin calisan motorudur.

Burada sunlar vardir:

```text
DbContext'ler
EF configuration'lar
AuthDbContext migration'lari
Auth/JWT/password servisleri
use case implementasyonlari
query executor'lar
write service'ler
Mikro REST API client
Uyumsoft servisleri
Axata servisleri
background queue/worker siniflari
offline sync servisleri
```

Genel fikir:

```text
Application interface tanimlar.
Infrastructure interface'i gercekler.
WebApi controller interface'i cagirir.
```

## WebApi Katmani

WebApi HTTP giris kapisidir.

Burada sunlar bulunur:

```text
Program.cs
controller'lar
authentication / authorization konfigurasyonu
Swagger
CORS
health checks
exception middleware
request logging
correlation id middleware
startup database initialization
```

Controller hafif kalmalidir:

```text
HTTP request'i al
route/query/body/claim bilgisini coz
yetkili depo bilgisini belirle
use case veya servisi cagir
sonucu HTTP response olarak don
```

Agir is kurali controller icinde tutulmamalidir.

## Veri Kaynaklari

Projede tek DB yoktur. Her context'in ayri amaci vardir.

| Bilesen | Config | Kullanim |
|---|---|---|
| `AuthDbContext` | `AuthConnection` | kullanici, rol, permission, duyuru, sikayet, belge akis, stock anomaly, GreenGrocer profil/snapshot, sofor tanimi |
| `MikroDbContext` | `MikroConnection` veya profile | Mikro okuma, liste, detay, rapor |
| `MikroWriteDbContext` | `MikroWriteConnection` / `testMikroConnection` / profile | Mikro yazma operasyonlari |
| `FurpaDbContext` | `FurpaConnection` | Furpa ozel tablolar, sube ayarlari, etiket, cihaz, manav etiket ortalamalari |
| `AxataDbContext` | `AxataConnection` | Axata senkronizasyonu, config varsa aktif |
| `ShopigoCiroDbContext` | `ShopigoCiroConnection` | yeni kasa/Shopigo ciro verileri, config varsa aktif |
| `MikroApiClient` | `MikroApi:BaseUrl` | Mikro REST API cagrilari |

## SQL Timeout Mantigi

SQL command timeout degerleri `DatabaseCommandTimeouts` konfigurasyonundan okunur.

Ornek:

```json
{
  "DatabaseCommandTimeouts": {
    "DefaultSeconds": 300,
    "AuthSeconds": 300,
    "MikroReadSeconds": 300,
    "MikroWriteSeconds": 300,
    "FurpaSeconds": 300,
    "AxataSeconds": 300,
    "ShopigoCiroSeconds": 300
  }
}
```

Anlamlari:

```text
MikroReadSeconds       -> liste, detay, rapor, arama
MikroWriteSeconds      -> create/update/delete yazma islemleri
AuthSeconds            -> Auth DB islemleri
FurpaSeconds           -> Furpa DB islemleri
AxataSeconds           -> Axata DB islemleri
ShopigoCiroSeconds     -> Shopigo ciro islemleri
```

Degerler environment variable ile override edilebilir:

```text
DatabaseCommandTimeouts__MikroWriteSeconds=300
```

Terminal/mobil/web istemciler de HTTP client timeout degerini buna uygun tutmalidir. Interneti zayif subelerde API DB yazimini bitirirken istemci erken timeout olursa kullanici tekrar basabilir ve duplicate evrak riski dogar.

Pratik UI kural:

```text
create istegi timeout olduysa hemen tekrar yeni evrak basma
once liste/detaydan evrak olustu mu kontrol et
mumkunse clientRequestId/idempotency destekli akis kullan
```

## AuthDbContext

Auth DB uygulamanin kendi verisidir.

Guncel ana tablolar:

```text
app_users
app_roles
app_permissions
app_user_roles
app_role_permissions
mobile_offline_sync_requests
uyumsoft_inbox_invoices
feedback_items
announcements
announcement_targets
announcement_reads
document_flows
document_flow_events
mikro_api_write_audits
stock_anomalies
stock_anomaly_events
green_grocer_product_case_profiles
green_grocer_order_line_snapshots
despatch_drivers
```

Bu context migration ile yonetilir. Uygulama acilisinda `StartupTasks:ApplyAuthMigrations=true` ise migration'lar otomatik uygulanir.

Not:

```text
AuthConnection SQL Server gibi gorunuyorsa UseSqlServer kullanilir.
Aksi halde Npgsql kullanilir.
```

## Mikro Okuma ve Yazma Ayrimi

Mikro okuma ve yazma baglantilari ayrilabilir.

`MikroDatabase:Profile`:

```text
Split -> okuma MikroConnection, yazma MikroWriteConnection varsa o; yoksa testMikroConnection
Test  -> okuma ve yazma testMikroConnection
Live  -> okuma ve yazma MikroConnection
```

Genel karar:

```text
liste/detay/rapor     -> MikroDbContext
create/update/delete  -> MikroWriteDbContext veya MikroApiClient
API'ye ozel yardimci  -> AuthDbContext veya FurpaDbContext
```

Mikro harici sistemdir. Mikro tablo semasi bu API migration'lariyla yonetilmez.

## FurpaDbContext

Furpa DB API'nin is destek tablolarini ve eski sistemden gelen bazi kaynaklari okur.

Ornek kullanim:

```text
DeviceDetails
DeviceTypes
BranchDetails
CashRegistryDetails
Cashiers
LabelDocuments
LabelDocumentDetails
Manav_Depo_Mal_Kabul_Etiket
VwKunyeNet gibi gorunumler
```

GreenGrocer kasa ortalamasi Furpa tarafindaki manav etiket gecmisinden hesaplanabilir.

## Uygulama Acilis Akisi

`Program.cs` temel akisi:

```text
configuration okunur
appsettings.Local.json varsa override edilir
logging ayarlanir
CORS, DataProtection, forwarded headers ayarlanir
AddCleanArchitecture ile WebApi + Infrastructure servisleri kaydedilir
JWT ve permission policy'leri kurulur
WebApplication build edilir
InitializeDatabaseAsync calisir
middleware pipeline kurulur
controller endpoint'leri map edilir
```

`appsettings.Local.json` local secret/config icindir; repoya gonderilmemelidir.

## StartupTasks

Startup DB isleri config ile kontrol edilir:

```text
ApplyAuthMigrations
SynchronizePermissionCatalog
SynchronizeWarehouseUsers
```

Anlamlari:

```text
ApplyAuthMigrations
  AuthDbContext migration'larini uygular.

SynchronizePermissionCatalog
  PermissionCatalog.Definitions ile app_permissions tablosunu senkronlar.
  Eksik permission'lari ekler.
  Name/description farklarini gunceller.
  Administrator role'e eksik katalog permission'larini ekler.

SynchronizeWarehouseUsers
  Mikro/Furpa kaynakli depo kullanici senkronizasyonunu calistirir.
```

Production'da bu ayarlar bilincli acik/kapali tutulmalidir. Kontrollu deployment icin migration repo icinde bulunmalidir.

## Middleware Pipeline

Pipeline ozet:

```text
Forwarded headers
HSTS / HTTPS redirection
Swagger
CorrelationIdMiddleware
RequestLoggingMiddleware
CORS
ExceptionHandlingMiddleware
Authentication
Authorization
Root endpoint
Health checks
Controllers
```

Health endpointleri:

```text
/health/live
/health/ready
```

## Authentication

Login endpoint'i:

```text
POST /api/auth/login
```

Akis:

```text
username/email normalize edilir
kullanici, rolleri ve permission'lari okunur
kullanici aktif mi kontrol edilir
sifre hash'i dogrulanir
terminal kullanicisi ise IP kontrolu yapilir
JWT token uretilir
AuthResponse icinde token ve UserDto doner
```

## Terminal IP Kontrolu

Terminal role kullanicilarda IP kontrolu vardir.

Temel mantik:

```text
kullanicinin deposu 100 altindaysa terminal IP kontrolu atlanir
terminal/sube kullanicisinda gelen IPv4 ile BranchDetails.BranchIpAddress karsilastirilir
ilk 3 IP blogu ayniysa giris serbesttir
```

Ek olarak paylasimli internet kullanan depolar config ile ayni gruba alinabilir:

```text
Auth:TerminalLogin:SharedNetworkWarehouseGroups
```

Ornek:

```text
50 ve 56 ayni interneti kullaniyorsa [50, 56] grubu tanimlanir.
56 kullanicisi 50 deposunun tanimli agindan da girebilir.
```

## JWT Icindeki Bilgiler

JWT claim'leri:

```text
sub
unique_name
email
nameidentifier
name
first_name
last_name
warehouse_no
warehouse_name
jti
role
permission
```

Permission claim ornegi:

```text
permission = kasa-islemleri.kasa-sayimlari.list
permission = ayar-islemleri.soforler.manage
```

Role veya permission degisirse mevcut token kendiliginden degismez. Kullanici yeniden login olmali veya token refresh akisi varsa yeni token almalidir.

## Authorization ve Permission

Yetki tanimi koddan gelir, kime verildigi DB'den gelir.

```text
PermissionCatalog.cs -> bilinen yetkiler
app_permissions      -> DB kaydi
app_role_permissions -> rol atamasi
JWT permission claim -> login anindaki yetki
[Authorize] policy   -> endpoint kontrolu
```

UI menu/buton kararlari permission listesine gore verilmelidir. Role name'e gore ekran acilmamalidir.

Detayli rehber:

[YENI_MENU_YETKI_MIGRATION_REHBERI.md](YENI_MENU_YETKI_MIGRATION_REHBERI.md)

## Page / Manage / Action Modeli

Guncel karar:

```text
normal menu/route          -> *.page
yonetim/tanim menu/route   -> *.manage
liste/veri cekme           -> *.list
detay                      -> *.detail
ekle                       -> *.create
duzenle                    -> *.update
sil/pasife al              -> *.delete
arsivle                    -> *.archive
aktar                      -> *.transfer
depo secici                -> *.all-warehouses
```

`list/detail/create/update/delete` yetkileri tek basina menu/route acma sebebi olmamalidir.

Ornek:

```text
ayar-islemleri.soforler.manage -> Soforler ekrani gorunur
ayar-islemleri.soforler.list   -> sofor listesi/arama API'si calisir
```

## Kasa Sayimlari ve Icmal Kaydi Girisi

Kasa sayimi modulunde create ekrani ile liste/edit ekrani ayridir.

```text
Icmal Kaydi Girisi -> yeni kasa sayimi/icmal kaydi olusturur
Kasa Sayimlari     -> mevcut kayitlari listeler, detay acar, secili kaydi duzenler veya siler
```

Guncel yetki modeli:

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

Akis:

```text
Yeni kayit:
  POST /api/kasa-islemleri/kasa-sayimlari
  -> kasa-islemleri.icmal-kaydi-girisi.create
  -> all-warehouses yoksa JWT deposuna yazar
  -> all-warehouses varsa UI sube secmeli ve body'de warehouseNo gondermelidir

Liste ve detay:
  GET /api/kasa-islemleri/kasa-sayimlari
  GET /api/kasa-islemleri/kasa-sayimlari/{seri}/{sira}
  -> kasa-islemleri.kasa-sayimlari.list/detail

Secili kaydi duzenleme:
  PUT /api/kasa-islemleri/kasa-sayimlari/{seri}/{sira}/detaylar
  PUT /api/kasa-islemleri/kasa-sayimlari/{seri}/{sira}/banknot-hareketleri
  -> kasa-islemleri.kasa-sayimlari.update

Secili kaydi silme:
  DELETE /api/kasa-islemleri/kasa-sayimlari/{seri}/{sira}
  -> kasa-islemleri.kasa-sayimlari.delete
```

Legacy uyumluluk route'lari ayni mantiktadir:

```text
POST /api/kasa-islemleri/kasa-sayimlari/UpdateSummaryDetails      -> kasa-sayimlari.update
POST /api/kasa-islemleri/kasa-sayimlari/UpdateBanknoteMovements   -> kasa-sayimlari.update
POST /api/kasa-islemleri/kasa-sayimlari/DeleteSummary             -> kasa-sayimlari.delete
```

UI'da `Icmal Kaydi Girisi` altinda duzenle/sil aksiyonu beklenmez. Kullanici once `Kasa Sayimlari` listesinden kaydi secer; yetkisi varsa duzenle/sil butonlari orada gorunur.

## Rol ve Yetki Zinciri

Erisim zinciri:

```text
User -> UserRoles -> Role -> RolePermissions -> Permission -> JWT Claim -> Policy -> Endpoint
```

Role permission atama endpoint'i:

```text
POST /api/roles/{roleId}/permissions
```

Bu endpoint mevcut role permission'larini silip gelen listeyi bastan yazar. Bu yuzden sadece yeni permission ID'si degil, rolun sahip olmasi gereken tum permission ID listesi gonderilmelidir.

## Depo Yetkisi ve all-warehouses

JWT icinde kullanicinin ana deposu vardir:

```text
warehouse_no
warehouse_name
```

Kullanici kendi deposu disina cikacaksa ilgili ekran icin `*.all-warehouses` yetkisi gerekir.

UI depo secici gostermek icin role bakmamalidir:

```text
ilgili menu icin *.all-warehouses varsa depo secici ac
yoksa depo alanini gizle veya kilitle
```

Backend farkli depo gonderilip yetki yoksa `403 Forbidden` doner.

## Hata Yonetimi

Merkezi exception middleware hata cevaplarini `ProblemDetails` olarak doner.

Genel eslesmeler:

```text
ArgumentException           -> 400 Bad Request
UnauthorizedAccessException -> 401 Unauthorized
ForbiddenAccessException    -> 403 Forbidden
KeyNotFoundException        -> 404 Not Found
InvalidOperationException   -> 409 Conflict
TimeoutException            -> 504 Gateway Timeout
Diger hatalar               -> 500 Internal Server Error
```

Controller icinde gereksiz `try/catch` yazmak yerine anlamli exception firlatmak tercih edilir.

## E-Irsaliye Akisi

E-irsaliye gonderimi mevcut Mikro evraklarindan uretilir. Yeni sevk/iade kaydi olusturmaz.

Desteklenen akisler:

```text
firma sevki
firma iadesi
depolar arasi sevk
depo iadesi
```

Gonderim request'i ortak model kullanir:

```text
SendEDespatchHttpRequest
```

Sofor bilgisi iki sekilde gelir:

```text
driverId yoksa:
  plaque, driverNameSurname, driverTckn manuel zorunlu

driverId varsa:
  backend Auth DB despatch_drivers tablosundan aktif soforu okur
  bos gelen alanlari kayittan doldurur
  dolu manuel alanlar kaydin ustune yazilir
```

Bu sayede UI ister eski gibi elle giris yapar, ister kayitli sofor secip otomatik doldurur.

UBL tarafinda `DriverPerson` sirasi:

```text
FirstName
FamilyName
NationalityID
```

Bu sira Uyumsoft XML schema hatalarini onlemek icin korunmalidir.

## Soforler Modulu

Sofor tanimlari Auth DB'dedir:

```text
despatch_drivers
```

Endpointler:

```text
GET    /api/ayar-islemleri/soforler
GET    /api/ayar-islemleri/soforler/{id}
POST   /api/ayar-islemleri/soforler
PUT    /api/ayar-islemleri/soforler/{id}
DELETE /api/ayar-islemleri/soforler/{id}
```

Yetki modeli:

```text
ayar-islemleri.soforler.manage -> ekran/route
ayar-islemleri.soforler.list   -> liste/arama
ayar-islemleri.soforler.detail -> detay
ayar-islemleri.soforler.create -> ekle
ayar-islemleri.soforler.update -> guncelle
ayar-islemleri.soforler.delete -> pasife al
```

Delete fiziksel silmez; kaydi pasife alir. E-irsaliye gonderiminde sadece aktif soforler cozulur.

## GreenGrocer / Manav Kasa Mantigi

GreenGrocer manav depo siparis/sevk isini daha dogru anlamak icin eklenen ozel bolumdur.

Konfig:

```json
{
  "GreenGrocerProductCases": {
    "Enabled": true,
    "OrderLinkingEnabled": false
  }
}
```

Anlam:

```text
Enabled
  kasa profil/cozumleme modulu tamamen acik mi?

OrderLinkingEnabled
  manav sevkinde siparis satiri GUID linki aktif mi?
```

Auth DB tablolari:

```text
green_grocer_product_case_profiles
green_grocer_order_line_snapshots
```

Temel is kurali:

```text
Subeler manav siparisini kasa adedi gibi girer.
Mikro ana birim KG/ADET oldugu icin miktar Mikro'da farkli gorunebilir.
API kasa profilinden veya etiket gecmisinden ortalama kg/kasa hesaplar.
UI hem kasa adedini hem tahmini KG bilgisini gosterir.
Sevk tarafinda gercek okutulan KG/ADET Mikro hareket miktaridir.
```

Order linking kapaliyken:

```text
sourceWarehouseNo = 56
model kodu 10/11/12 olan manav urunlerinde
warehouseOrderLineGuid yok sayilir
eski canli mantik korunur
```

Order linking acikken:

```text
UI gercek siparis satiri GUID'ini gonderir.
Sevk siparise baglanir.
Kalan/teslim miktari kurallari calisir.
```

Detayli dokuman:

[MANAV_KASA_SIPARIS_SEVK_AKISI.md](MANAV_KASA_SIPARIS_SEVK_AKISI.md)

## Barkod Cozumleme ve Arama

Barkod arama/okutma tarafinda yeni merkezi cozumleme akisi bulunur.

Ana hedef:

```text
barkod bulundu mu?
urun barkodu mu, koli barkodu mu, alternatif barkod mu?
koli ici adet nedir?
islem icin uygun mu?
sevk/siparis/iade/fire icin blok/uyari var mi?
```

Detayli dokuman:

[BARKOD_COZUMLEME_VE_ARAMA_REHBERI.md](BARKOD_COZUMLEME_VE_ARAMA_REHBERI.md)

## Document Flow

Belge akis izleme Auth DB tarafindadir:

```text
document_flows
document_flow_events
```

Siparis, sevk, iade, mal kabul ve e-irsaliye adimlari burada izlenebilir.

Kullanim:

```text
home oncelik kartlari
operasyon paneli
basarisiz e-irsaliye takibi
bekleyen mal kabul takibi
```

Bu tablolar Mikro evraginin yerine gecmez; operasyonel takip/audit icindir.

## Background Jobs

Projede uzun surebilecek isler queue/worker ile calisir.

Ornekler:

```text
AxataSynchronizationQueue / Worker / Scheduler
OperationsJobQueue / Worker
InvoiceViewingAutomaticSynchronizationScheduler
InvoiceViewingSynchronizationJobQueue
```

AmaÃ§:

```text
HTTP request'i uzun is boyunca bloklamamak
progress/status bilgisini ayri endpoint ile gostermek
tekrar deneme ve audit davranisini merkezi yapmak
```

## Mikro REST API Client

Mikro REST API altyapisi:

```text
src/FurpaMerkezApi.Infrastructure/Services/MikroApi
```

Ana siniflar:

```text
MikroApiClient
MikroApiOptions
MikroApiAuthBlockFactory
MikroApiResult
MikroApiException
MikroApiWriteAuditService
```

Mikro REST API controller'a dogrudan baglanmamalidir. Use case veya write service icinden kullanilmalidir.

Detayli dokumanlar:

- [MIKRO_REST_API_GECIS_ANALIZI.md](MIKRO_REST_API_GECIS_ANALIZI.md)
- [MIKRO_API_POSTMAN_DOKUMANI.md](MIKRO_API_POSTMAN_DOKUMANI.md)

## Yeni Modul veya Endpoint Ekleme

Genel yol:

```text
1. Module/menu/action kararini ver.
2. PermissionCatalog.cs icine yetkileri ekle.
3. Controller route ve policy const'larini yaz.
4. Application request/response/interface ekle.
5. Infrastructure implementation yaz.
6. DbContext/QueryExecutor/WriteService gerekiyorsa ekle.
7. DI kaydini yap.
8. Auth entity veya permission seed varsa migration olustur.
9. UI_API_DOKUMANI.md guncelle.
10. Frontend menu/route/buton gorunurlugunu permission'a bagla.
11. Build/test ve pending migration kontrolu yap.
```

Detay:

[YENI_MENU_YETKI_MIGRATION_REHBERI.md](YENI_MENU_YETKI_MIGRATION_REHBERI.md)

## Migration Ne Zaman Gerekir?

AuthDbContext icin migration gerekir:

```text
yeni Auth entity/tablo
entity kolon/iliski degisikligi
permission seed degisikligi kontrollu deployment ile tasinacaksa
permission code/menu/action tasima veya rename
GreenGrocer profil/snapshot gibi Auth DB tablolari
despatch_drivers gibi tanim tablolari
```

FurpaDbContext icin migration sadece API'nin sahip oldugu Furpa tablo semasi degisiyorsa dusunulur.

MikroDbContext icin normalde migration yazilmaz; Mikro harici ERP semasidir.

Sadece su degisikliklerde genelde migration gerekmez:

```text
yeni controller action
yeni query/use case
yeni DTO
sadece endpoint davranisi
sadece dokuman guncellemesi
```

Kontrol komutu:

```powershell
dotnet ef migrations has-pending-model-changes --project src\FurpaMerkezApi.Infrastructure --startup-project src\FurpaMerkezApi.WebApi --context AuthDbContext
```

Permission seed degisikliginde migration EF ile scaffold edilmelidir. Sadece elle `.cs` migration dosyasi eklemek `.Designer.cs` ve `AuthDbContextModelSnapshot` guncellenmedigi icin `dotnet run` sirasinda `PendingModelChangesWarning` hatasina yol acabilir.

Permission tasima veya rename islerinde migration role atamalarini da korumalidir:

```text
eski permission -> yeni permission
app_role_permissions kopyalanir
eski permission baglantilari temizlenir
Down metodunda ters yonde tasinir
```

## Build ve Test

Genel dogrulama:

```powershell
dotnet build FurpaMerkezApi.sln
dotnet test FurpaMerkezApi.sln --no-build
dotnet ef migrations has-pending-model-changes --project src\FurpaMerkezApi.Infrastructure --startup-project src\FurpaMerkezApi.WebApi --context AuthDbContext
```

API acilisinda pending model hatasi gorulurse once migration/snapshot uyumu kontrol edilmelidir.

## Secret ve Config Kurallari

Gercek sifre, API key, JWT secret ve connection string track edilen dosyalara yazilmamalidir.

Local override:

```text
src/FurpaMerkezApi.WebApi/appsettings.Local.json
```

Production:

```text
environment variable
server-side config
secret manager
deployment pipeline secret store
```

Secret yanlislikla paylasildiysa sadece dosyadan silmek yetmez; secret rotate edilmelidir.

## Kod Okuma Sirasi

Projeyi ilk kez okuyacak biri icin:

1. [../README.md](../README.md)
2. [README.md](README.md)
3. [PROJE_GENEL_ISLEYISI.md](PROJE_GENEL_ISLEYISI.md)
4. `src/FurpaMerkezApi.WebApi/Program.cs`
5. `src/FurpaMerkezApi.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`
6. `src/FurpaMerkezApi.Application/Security/PermissionCatalog.cs`
7. `src/FurpaMerkezApi.Infrastructure/Services/AuthService.cs`
8. `src/FurpaMerkezApi.Infrastructure/Authentication/JwtTokenFactory.cs`
9. Ilgili controller
10. Controller'in kullandigi Application interface'i
11. Infrastructure implementation
12. QueryExecutor veya WriteService

Ozel konular:

```text
Yetki/migration:
  YENI_MENU_YETKI_MIGRATION_REHBERI.md

UI endpoint:
  UI_API_DOKUMANI.md

Manav kasa:
  MANAV_KASA_SIPARIS_SEVK_AKISI.md

Barkod:
  BARKOD_COZUMLEME_VE_ARAMA_REHBERI.md

Manav mal kabul ve etiket:
  MANAV_MAL_KABUL_VE_ETIKET_API.md

E-fatura / e-irsaliye:
  E_FATURA_E_IRSALIYE_SERVIS_DOKUMANI.md
```

## Sik Yapilan Hatalar

### DB'ye Permission Ekleyince Menu Olusacak Sanmak

Dogru model:

```text
PermissionCatalog'a eklenir.
Controller endpoint kodda olur.
Frontend menu/route tanimi yapilir.
DB sadece permission kaydi ve rol baglantisini tutar.
```

### List Yetkisini Route Yetkisi Gibi Kullanmak

Dogru model:

```text
normal ekran -> *.page
yonetim ekran -> *.manage
API liste -> *.list
```

### Role Permission Atarken Sadece Yeni Permission Gondermek

`POST /api/roles/{id}/permissions` mevcut listeyi bastan yazar. Tum permission ID listesi gonderilmelidir.

### Yetki Verip Eski Token ile Test Etmek

Kullanici yeniden login olmadan JWT icindeki permission claim'leri degismez.

### Controller'a DB/Integration Detayi Koymak

Controller use case/service cagirir. DB ve entegrasyon detayi Infrastructure icinde kalir.

### Timeout Sonrasi Tekrar Tekrar Create Basmak

Internet zayifsa istemci timeout olabilir ama API yazma islemi DB'de tamamlanmis olabilir. UI once evrak olustu mu kontrol etmelidir.

### Mikro REST API'ye Toplu ve Kontrolsuz Gecmek

Mikro REST gecisi modul bazli yapilmalidir. Payload, response, duplicate, seri/sira ve geri okuma dogrulamalari test edilmelidir.

## Kisa Zihinsel Model

Katman:

```text
Domain         -> entity ve temel kurallar
Application    -> kontrat, DTO, permission dili
Infrastructure -> DB, servis, use case, entegrasyon
WebApi         -> HTTP giris kapisi
```

Yetki:

```text
PermissionCatalog -> DB permission -> Role -> User -> JWT claim -> Policy -> Endpoint
```

Menu:

```text
*.page / *.manage -> route/menu
*.list/detail/... -> API ve buton
*.all-warehouses  -> depo secici
```

Endpoint:

```text
Controller -> UseCase/Service -> QueryExecutor/WriteService -> DB/Integration -> DTO
```

Migration:

```text
Model veya seed degisti -> migration
Migration eklendi -> snapshot kontrol
Snapshot temiz -> build/test
```

## Ilgili Dokumanlar

Genel:

- [../README.md](../README.md)
- [README.md](README.md)
- [UI_API_DOKUMANI.md](UI_API_DOKUMANI.md)
- [YENI_MENU_YETKI_MIGRATION_REHBERI.md](YENI_MENU_YETKI_MIGRATION_REHBERI.md)
- [PRODUCTION_HAZIRLIK.md](PRODUCTION_HAZIRLIK.md)

Operasyon:

- [OPERASYON_HIZLI_MUDAHALE.md](OPERASYON_HIZLI_MUDAHALE.md)
- [OPERASYON_ISLEMLERI_DETAYLI_API_DOKUMANI.md](OPERASYON_ISLEMLERI_DETAYLI_API_DOKUMANI.md)
- [STOK_ANOMALI_MERKEZI.md](STOK_ANOMALI_MERKEZI.md)

Fatura ve entegrasyon:

- [FATURA_GONDERIM_SISTEMI.md](FATURA_GONDERIM_SISTEMI.md)
- [E_FATURA_E_IRSALIYE_SERVIS_DOKUMANI.md](E_FATURA_E_IRSALIYE_SERVIS_DOKUMANI.md)
- [UBL_FATURA_MANTIGI.md](UBL_FATURA_MANTIGI.md)
- [MIKRO_MUHASEBE_AKIS_REHBERI.md](MIKRO_MUHASEBE_AKIS_REHBERI.md)

Mikro ve modul rehberleri:

- [MIKRO_REST_API_GECIS_ANALIZI.md](MIKRO_REST_API_GECIS_ANALIZI.md)
- [MIKRO_API_POSTMAN_DOKUMANI.md](MIKRO_API_POSTMAN_DOKUMANI.md)
- [BARKOD_COZUMLEME_VE_ARAMA_REHBERI.md](BARKOD_COZUMLEME_VE_ARAMA_REHBERI.md)
- [MANAV_KASA_SIPARIS_SEVK_AKISI.md](MANAV_KASA_SIPARIS_SEVK_AKISI.md)
- [MANAV_MAL_KABUL_VE_ETIKET_API.md](MANAV_MAL_KABUL_VE_ETIKET_API.md)
