# FurpaMerkezApi Proje Genel Isleyisi

Bu dokuman, FurpaMerkezApi projesine yeni giren birinin sistemi hizli ama saglam sekilde anlamasi icin hazirlandi.

Temel sorular:

```text
Bu API ne is yapiyor?
Hangi katman ne sorumluluk tasiyor?
Bir istek sisteme girdiginde hangi adimlardan geciyor?
Auth, rol, permission ve menu yapisi nasil calisiyor?
Yeni bir ekran veya endpoint eklemek istersek nereye dokunuyoruz?
Migration ne zaman gerekir?
Mikro DB, Furpa DB, Auth DB ve Mikro REST API arasindaki fark nedir?
```

## 1. Projenin Kisa Ozeti

`FurpaMerkezApi`, Furpa merkez operasyonlari icin yazilmis bir `.NET 9` Web API projesidir.

API'nin ana gorevleri:

```text
Kullanici girisi yapmak
Kullanici, rol ve yetki yonetmek
Merkez operasyon ekranlari icin veri sunmak
Mikro/Furpa kaynaklarindan veri okumak
Bazi operasyonlarda Mikro veya Furpa tarafina yazmak
Fatura, sevk, siparis, stok, kasa, mal kabul gibi modulleri beslemek
Axata, Uyumsoft, POS muhasebe gibi entegrasyonlari calistirmak
Uzun isleri background worker/queue ile islemek
```

En kisa akis:

```text
Frontend
  -> WebApi Controller
  -> Application contract / use case interface
  -> Infrastructure implementation
  -> DbContext / QueryExecutor / WriteService / Integration Client
  -> DTO
  -> HTTP Response
```

## 2. Ana Mimari

Projede 4 ana katman var:

```text
Domain
Application
Infrastructure
WebApi
```

Bu repo Clean Architecture cizgisine yakindir, ama pratikte use case implementasyonlarinin buyuk kismi `Infrastructure` katmanindadir. `Application` daha cok kontrat, DTO ve sistem dili katmanidir.

## 3. Domain Katmani

Domain katmani cekirdek entity'leri ve temel kurallari tasir.

Onemli auth entity'leri:

```text
AppUser
AppRole
AppPermission
AppUserRole
AppRolePermission
MobileOfflineSyncRequest
UyumsoftInboxInvoice
FeedbackItem
```

Domain katmaninin gorevi:

```text
Entity alanlarini normalize etmek
Bos/hatali degerleri engellemek
Temel iliski modelini tasimak
Auth tarafindaki user-role-permission zincirini temsil etmek
```

Domain katmaninda HTTP, controller, EF query veya dis entegrasyon detayi tutulmaz.

## 4. Application Katmani

Application katmani projenin kontrat ve dil katmanidir.

Burada genelde sunlar bulunur:

```text
Request modelleri
Response / DTO modelleri
Use case interface'leri
Servis interface'leri
Permission catalog
Permission tree builder
Policy/permission code tanimlari
```

Ornek:

```text
src/FurpaMerkezApi.Application/Security/PermissionCatalog.cs
src/FurpaMerkezApi.Application/Security/PermissionCodes.cs
src/FurpaMerkezApi.Application/Security/PermissionTreeBuilder.cs
src/FurpaMerkezApi.Application/Modules/...
```

Bu katman genelde "ne yapilabilir?" sorusunu cevaplar. "Nasil yapilir?" sorusunun cevabi cogu zaman `Infrastructure` icindedir.

## 5. Infrastructure Katmani

Infrastructure katmani sistemin calisan motorudur.

Burada sunlar bulunur:

```text
DbContext'ler
EF configuration'lar
Migration'lar
Auth servisleri
JWT token uretimi
Password hashing
Use case implementasyonlari
Query executor'lar
Write service'ler
Mikro REST API client
Uyumsoft servisleri
Axata senkronizasyon servisleri
Background worker'lar
Offline sync servisleri
```

Temel fikir:

```text
Application interface tanimlar.
Infrastructure interface'i gercekler.
WebApi controller interface'i cagirir.
```

## 6. WebApi Katmani

WebApi katmani HTTP giris kapisidir.

Burada sunlar vardir:

```text
Program.cs
Controller'lar
Authentication/Authorization konfigurasyonu
Swagger konfigurasyonu
CORS
Health checks
Exception middleware
Request logging
Correlation id middleware
Startup database initialization
```

Controller'in hedefi hafif kalmaktir:

```text
HTTP request'i al
Claim veya route/query/body bilgilerini oku
Use case/servis cagir
Sonucu HTTP response olarak don
```

Agir is kurali controller icinde tutulmamalidir.

## 7. Veritabani ve Dis Kaynaklar

Projede tek veritabani yoktur. Birden fazla veri kaynagi vardir.

| Bilesen | Kaynak | Kullanim |
|---|---|---|
| `AuthDbContext` | `AuthConnection` | kullanici, rol, permission, auth migration |
| `MikroDbContext` | `MikroConnection` veya profile'a gore test | Mikro okuma, liste, detay, rapor |
| `MikroWriteDbContext` | `MikroWriteConnection`, `testMikroConnection` veya profile'a gore live | Mikro DB yazma operasyonlari |
| `FurpaDbContext` | `FurpaConnection` | API'ye ozel tablo/gorunumler, branch, label, operasyon destek verileri |
| `AxataDbContext` | `AxataConnection` | Axata senkronizasyonu, config varsa kaydedilir |
| `ShopigoCiroDbContext` | `ShopigoCiroConnection` | Shopigo ciro verileri, config varsa kaydedilir |
| `MikroApiClient` | `MikroApi:BaseUrl` | Mikro REST API cagrilari |

## 8. AuthDbContext

`AuthDbContext`, auth ve yetki sisteminin ana DB context'idir.

Tablolar:

```text
app_users
app_roles
app_permissions
app_user_roles
app_role_permissions
mobile_offline_sync_requests
uyumsoft_inbox_invoices
feedback_items
```

Bu context migration ile yonetilir. Uygulama acilisinda `StartupTasks:ApplyAuthMigrations` aktifse migration'lar otomatik uygulanir.

Not:

```text
AuthConnection SQL Server veya PostgreSQL olabilir.
Connection string SQL Server gibi gorunuyorsa UseSqlServer kullanilir.
Aksi halde Npgsql kullanilir.
```

## 9. Mikro Okuma ve Yazma Ayrimi

Mikro tarafinda okuma ve yazma baglantilari ayrilabilir.

`MikroDatabase:Profile` ayari:

```text
Split -> okuma MikroConnection, yazma MikroWriteConnection varsa o; yoksa testMikroConnection
Test  -> okuma ve yazma testMikroConnection
Live  -> okuma ve yazma MikroConnection
```

Bu ayrim ozellikle kritik yazma operasyonlarinda onemlidir.

Genel karar:

```text
Liste/detay/rapor -> MikroDbContext
Create/update/delete -> mevcut modullerde cogunlukla MikroWriteDbContext
API'ye ozel yardimci veri -> FurpaDbContext
```

Mikro DB harici bir sistemdir. Mikro tablo semasini bu projenin migration'lariyla yonetmek standart senaryo degildir.

## 10. FurpaDbContext

`FurpaDbContext`, API'nin kendi ihtiyaclari icin kullandigi SQL Server tarafidir.

Ornek kullanimlar:

```text
LabelDocuments
LabelDocumentDetails
AuthorizationFiles
Cashiers
BranchDetails
VwKunyeNet gibi gorunumler
```

Furpa tarafinda API'nin sahip oldugu tablo/gorunum ihtiyaci varsa bu context kullanilir.

## 11. Mikro REST API Client

Projede Mikro REST API icin `MikroApiClient` altyapisi vardir.

Ilgili klasor:

```text
src/FurpaMerkezApi.Infrastructure/Services/MikroApi
```

Ana siniflar:

```text
MikroApiOptions
MikroApiClient
MikroApiAuthBlockFactory
MikroApiResult
MikroApiException
```

Mikro REST auth mantigi:

```text
Her istekte ortak Mikro auth blogu uretilir.
Sifre icin gunluk MD5 hash kullanilir.
Formul: MD5("yyyy-MM-dd <SifreAnahtari>")
Varsayilan saat offset'i Turkiye saatine uygun olacak sekilde ayarlanabilir.
```

Client metotlari:

```text
GetAsync<TResponse>
PostAsync<TResponse>
PostWithMikroEnvelopeAsync<TResponse>
PostWithMikroPayloadAsync<TResponse>
PostLoginAsync<TResponse>
```

Onemli karar:

```text
MikroApiClient controller'a dogrudan baglanmamalidir.
Use case veya write service icinde kullanilmalidir.
Mikro API payload modeli frontend request modeline sizdirilmemelidir.
```

Bu altyapi olsa bile her modul otomatik Mikro REST API kullanmaz. Bir modul acikca `MikroApiClient` enjekte edip kullanmadikca mevcut DB okuma/yazma davranisi devam eder.

## 12. Uygulama Acilis Akisi

Uygulama `Program.cs` ile ayaga kalkar.

Acilis sirasinda temel akis:

```text
Encoding provider kaydedilir.
WebApplicationBuilder olusturulur.
appsettings.Local.json varsa configuration'a eklenir.
Hosting, reverse proxy, CORS, DataProtection ayarlari okunur.
Production config validasyonu yapilir.
Logging provider'lari ayarlanir.
Controllers, health checks, routing, http context accessor kaydedilir.
Forwarded headers, CORS, DataProtection ayarlanir.
AddCleanArchitecture ile WebApi + Infrastructure servisleri kaydedilir.
Application build edilir.
InitializeDatabaseAsync calisir.
Middleware pipeline kurulur.
Controller endpoint'leri map edilir.
```

`appsettings.Local.json` opsiyoneldir ve sonradan eklendigi icin ayni key'leri override edebilir. Lokal secret'lar icin uygundur; repoya gonderilmemelidir.

## 13. StartupTasks

Uygulama acilisinda DB ile ilgili bazi isler `StartupTasks` ayarlarina baglidir.

Varsayilanlar:

```text
ApplyAuthMigrations = true
SynchronizePermissionCatalog = true
SynchronizeWarehouseUsers = true
```

Anlamlari:

```text
ApplyAuthMigrations
  AuthDbContext migration'larini uygular.

SynchronizePermissionCatalog
  PermissionCatalog.Definitions listesini app_permissions ile senkronlar.
  Eksik permission'lari ekler.
  Name/description degistiyse gunceller.
  Administrator role'e eksik katalog permission'larini ekler.

SynchronizeWarehouseUsers
  Mikro kaynakli depo/kullanici senkronizasyonunu calistirir.
```

Production notu:

```text
Production'da bu ayarlar bilincli acik/kapali tutulmalidir.
Migration ve permission ekleme islemleri kontrollu deployment akisi ile yapilmalidir.
```

## 14. Middleware Pipeline

Pipeline sirasi ozetle:

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

CORS, exception middleware'den once konumlandirilmis. Bu sayede hata cevaplarinda da CORS davranisi daha saglam olur.

Health endpoint'leri:

```text
/health/live
/health/ready
```

`/health/ready` core dependency ve operasyon export path gibi kontrolleri calistirir.

## 15. Authentication Akisi

Login endpoint'i:

```text
POST /api/auth/login
```

Akis:

```text
Username veya email normalize edilir.
Kullanici bulunur.
Kullanici rolleri ve role permission'lari Include ile yuklenir.
Kullanici aktif mi kontrol edilir.
Sifre hash'i dogrulanir.
Terminal role ozel IP/sube kontrolu varsa uygulanir.
JWT token uretilir.
AuthResponse icinde token, expire bilgisi ve UserDto doner.
```

Register akisi ilk kullaniciyi administrator role'e baglayabilir.

## 16. JWT Icindeki Bilgiler

JWT icine temel claim'ler yazilir:

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
role claim'leri
permission claim'leri
```

Permission claim'leri cok onemlidir:

```text
permission = kasa-islemleri.kasa-sayimlari.list
permission = kasa-islemleri.kasa-sayimlari.detail
```

Endpoint yetki kontrolu bu claim'ler uzerinden yapilir.

Role veya permission degistiginde mevcut JWT otomatik degismez. Kullanici yeniden login olmali veya token refresh mekanizmasi varsa yeni token almalidir.

## 17. Authorization ve Permission Mantigi

Projede authorization policy tabanlidir.

Startup'ta `PermissionCatalog.Codes` uzerinden her permission icin policy uretilir:

```csharp
options.AddPolicy(permissionCode, policy => policy.RequireClaim("permission", permissionCode));
```

Controller'da:

```csharp
[Authorize(Policy = "kasa-islemleri.kasa-sayimlari.list")]
```

Bu durumda token icinde ayni permission claim'i yoksa istek `403 Forbidden` olur.

En onemli kural:

```text
Yetkinin tanimi koddan gelir.
Yetkinin kime verildigi DB'den gelir.
```

## 18. PermissionCatalog ve Menu Agaci

Permission sistemi ayni zamanda frontend menu gorunurlugunu besler.

Hiyerarsi:

```text
Module -> Menu -> Action
```

Ornek:

```text
Module: kasa-islemleri
Menu:   kasa-sayimlari
Action: list
Code:   kasa-islemleri.kasa-sayimlari.list
```

`PermissionCatalog.cs` sistemde bilinen permission'lari tanimlar.

Bu katalog:

```text
Authorization policy'lerinin olusmasini saglar.
DB permission senkronizasyonuna kaynak olur.
Permission tree olusturmaya kaynak olur.
Frontend menu/buton gorunurlugunu besler.
```

DB'ye elle permission eklemek yeni modul/menu/API endpoint olusturmaz. Yeni menu veya endpoint icin dogru baslangic `PermissionCatalog.cs` dosyasidir.

Detayli rehber:

```text
YENI_MENU_YETKI_MIGRATION_REHBERI.md
```

## 19. Rol ve Yetki Zinciri

Sistemde erisim zinciri:

```text
User -> UserRoles -> Role -> RolePermissions -> Permission -> JWT Claim -> Policy -> Endpoint
```

Tablolar:

```text
app_users
app_roles
app_permissions
app_user_roles
app_role_permissions
```

Role permission atama:

```text
POST /api/roles/{roleId}/permissions
```

Dikkat:

```text
RoleService.AssignPermissionsAsync mevcut role permission'larini siler ve gelen listeyi yeniden ekler.
Bu endpoint'e sadece yeni permission ID'si degil, rolun sahip olmasi gereken tum permission ID listesi gonderilmelidir.
```

User role atama:

```text
POST /api/users/{userId}/roles
```

Sadece aktif roller kullaniciya atanabilir.

## 20. Frontend Menu ve Buton Mantigi

Frontend tarafinda menu gorunurlugu kullanicinin permission listesine gore yapilmalidir.

Dogru yaklasim:

```text
Menu/list gorunurlugu -> *.list permission
Detay butonu         -> *.detail permission
Ekle butonu          -> *.create permission
Duzenle butonu       -> *.update permission
Ozel aksiyon         -> ilgili ozel permission
```

Frontend menuyu gizlese bile gercek guvenlik backend `[Authorize]` kontroludur. UI kontrolu sadece kullanici deneyimi icindir.

## 21. Bir Istek Sistemde Nasil Ilerler?

Tipik okuma istegi:

```text
Frontend istek atar.
Authentication JWT'yi dogrular.
Authorization permission claim'i kontrol eder.
Controller route/query/body/claim bilgilerini okur.
Use case interface'i cagrilir.
Infrastructure implementation devreye girer.
QueryExecutor veya servis DB'ye gider.
DTO listesi veya response olusturulur.
Controller 200 OK doner.
```

Tipik yazma istegi:

```text
Frontend body gonderir.
Authorization create/update permission kontrol eder.
Controller request'i use case'e aktarir.
Use case / write service validasyon yapar.
MikroWriteDbContext, FurpaDbContext veya entegrasyon client kullanilir.
Gerekirse transaction acilir.
Sonuc DTO olarak doner.
```

Mikro REST API kullanilan bir istek:

```text
Use case veya write service MikroApiClient kullanir.
Request DTO Mikro API payload'una map edilir.
Mikro auth blogu MikroApiAuthBlockFactory ile uretilir.
HTTP request Mikro API'ye gider.
Response MikroApiResult<T> olarak normalize edilir.
Gerekirse DB'den geri okuma/dogrulama yapilir.
API kendi DTO'sunu doner.
```

## 22. Okuma ve Yazma Desenleri

Projede sik gorulen sinif tipleri:

```text
...Controller
IList...UseCase
IGet...DetailUseCase
ICreate...UseCase
...QueryExecutor
...WriteService
...Service
```

Okuma tarafinda:

```text
MikroDbContext
FurpaDbContext
AsNoTracking
Select ile DTO projection
Raw SQL veya EF query
```

Yazma tarafinda:

```text
MikroWriteDbContext
FurpaDbContext
WriteService
Transaction
Validasyon
Sonuc DTO
```

Controller DB detayini bilmemelidir. Query ve write detaylari Infrastructure icinde kalmalidir.

## 23. Claims ve Depo Mantigi

JWT icinde depo bilgileri vardir:

```text
warehouse_no
warehouse_name
```

Birçok endpoint depo bilgisini query'den almak yerine claim'den okuyabilir.

Ornek:

```csharp
var warehouseNo = User.GetRequiredWarehouseNo();
```

Avantaj:

```text
Kullanici kendi deposu disina kolayca tasamaz.
Frontend daha az parametre gonderir.
Endpoint davranisi standartlasir.
```

Bazi merkez/rapor endpoint'lerinde query ile depo secimi gerekebilir. Bu durumda endpoint'in permission seviyesi dikkatli belirlenmelidir.

## 24. Exception ve Hata Yonetimi

Projede merkezi exception middleware bulunur.

Genel eslesmeler:

```text
ArgumentException         -> 400 Bad Request
UnauthorizedAccessException -> 401 Unauthorized
InvalidOperationException -> 409 Conflict
KeyNotFoundException      -> 404 Not Found
Diger exception'lar       -> 500 Internal Server Error
```

Controller icinde gereksiz `try/catch` yazmak yerine anlamli exception firlatmak tercih edilir.

## 25. Background Job ve Entegrasyonlar

Projede sadece request-response endpoint'leri yoktur. Uzun surebilecek isler background worker ile islenebilir.

### Axata Senkronizasyonu

Ana bilesenler:

```text
AxataSynchronizationQueue
AxataSynchronizationWorker
AxataSynchronizationScheduler
AxataSynchronizationExecutionCoordinator
Task handler'lar
```

Akis:

```text
Is kuyruga eklenir.
Worker isi alir.
Ilgili task handler calisir.
Sonuc audit/log olarak islenir.
Scheduler aktifse belirli araliklarla otomatik is uretir.
```

### Operations Dosya Uretimi

Ana bilesenler:

```text
OperationsJobQueue
OperationsJobWorker
OperationsService
OperationsFileGenerationService
```

Amac:

```text
Uzun surebilecek dosya uretimini HTTP request disina tasimak.
Kullaniciya job id vermek.
Sonra job durumunu sorgulatmak.
```

## 26. Modul Gruplari

Projede ana module gruplari PermissionCatalog icinde gorulebilir.

Ornekler:

```text
kullanici-islemleri
arama-islemleri
green-grocer
ortak-islemler
ayar-islemleri
siparis-islemleri
sevk-islemleri
iade-islemleri
mal-kabul-islemleri
stok-islemleri
rapor-islemleri
operasyon-islemleri
duzeltme-islemleri
entegrasyon-islemleri
fatura-islemleri
kasa-islemleri
```

Yeni ekran eklenirken once hangi module altina girecegi netlestirilmelidir.

## 27. Yeni Modul veya Endpoint Eklerken Genel Yol

Genel adimlar:

```text
1. Module/menu/action karari ver.
2. PermissionCatalog.cs icine gerekli permission'lari ekle.
3. Controller route ve policy const'larini yaz.
4. Application katmaninda request/response/interface ekle.
5. Infrastructure katmaninda implementation yaz.
6. QueryExecutor veya WriteService gerekiyorsa ekle.
7. DI kaydini ekle.
8. Permission migration gerekip gerekmedigine karar ver.
9. Frontend route/menu/buton gorunurlugunu permission'a bagla.
10. Kullanici/role yetki atamasini yap.
11. Kullaniciya tekrar login yaptir veya token yenilet.
```

Detayli permission/migration adimlari icin:

```text
YENI_MENU_YETKI_MIGRATION_REHBERI.md
```

## 28. Migration Ne Zaman Gerekir?

Bu konu projede kritik.

### AuthDbContext Icin Gerekir

Su durumlarda Auth migration gerekir:

```text
Auth entity semasi degisirse
Yeni auth tablosu eklenirse
Auth seed verisi kontrollu deployment ile tasinacaksa
Permission'lar production'a migration ile gidecekse
```

PermissionCatalog startup senkronizasyonu permission'lari DB'ye ekleyebilir. Ancak production icin en kontrollu yontem yeni permission'lari migration ile tasimaktir.

### FurpaDbContext Icin Gerekebilir

Furpa tarafinda API'nin sahip oldugu tablo semasi degisiyorsa migration gerekebilir.

Ornek:

```text
LabelDocuments benzeri API tablolari
Feedback veya operasyon destek tablolari
Yeni EF entity ile fiziksel tablo beklenmesi
```

### MikroDbContext Icin Genelde Gerekmez

Mikro harici sistemdir. Mikro tablo semasi normalde bu API migration'lari ile yonetilmez.

### Sadece Kod Degisikliginde Gerekmez

Genelde migration gerekmez:

```text
Yeni controller action
Yeni query/use case
Yeni DTO
Sadece endpoint davranisi degisikligi
Sadece frontend menu gorunurlugu degisikligi
```

Ama yeni permission production'a garanti gitsin isteniyorsa permission migration yazilmalidir.

## 29. Secret ve Config Kurallari

Secret yonetiminde temel kural:

```text
Gercek sifre, API key, JWT secret ve connection string public repo veya paylasimli dokumanda tutulmaz.
```

Lokal calisma:

```text
src/FurpaMerkezApi.WebApi/appsettings.Local.json
```

Bu dosya opsiyonel olarak okunur ve local override icin kullanilir. Repoya gonderilmemelidir.

Production:

```text
Environment variable
Server-side config
Secret manager
Deployment pipeline secret store
```

Eger bir secret track edilen dosyaya yazilip push edilirse:

```text
Sonradan silmek tek basina yeterli degildir.
Secret rotate edilmelidir.
```

Production validasyonlari:

```text
AuthConnection zorunlu
FurpaConnection zorunlu
Mikro read/write connection zorunlu
Axata aciksa AxataConnection zorunlu
JWT SecretKey placeholder olamaz
HTTPS veya reverse proxy ayari zorunlu
```

## 30. Kod Okumaya Nereden Baslanmali?

Projeyi ilk kez okuyacak biri icin tavsiye edilen sira:

```text
README.md
PROJE_GENEL_ISLEYISI.md
src/FurpaMerkezApi.WebApi/Program.cs
src/FurpaMerkezApi.WebApi/Configuration/ServiceCollectionExtensions.cs
src/FurpaMerkezApi.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs
src/FurpaMerkezApi.Application/Security/PermissionCatalog.cs
src/FurpaMerkezApi.Infrastructure/Services/AuthService.cs
src/FurpaMerkezApi.Infrastructure/Authentication/JwtTokenFactory.cs
Ilgili module controller'i
Controller'in kullandigi Application interface'i
Infrastructure implementation
QueryExecutor veya WriteService
```

Mikro REST API icin:

```text
src/FurpaMerkezApi.Infrastructure/Services/MikroApi
MIKRO_REST_API_GECIS_ANALIZI.md
MIKRO_API_POSTMAN_DOKUMANI.md
```

Permission/migration icin:

```text
YENI_MENU_YETKI_MIGRATION_REHBERI.md
```

## 31. Sik Yapilan Hatalar

### DB'ye Permission Ekleyince Menu Olusacak Sanmak

Yanlis:

```text
app_permissions tablosuna kayit attim, modul/menu otomatik olusur.
```

Dogru:

```text
PermissionCatalog'a eklenir.
Controller endpoint kodda olur.
Frontend menu/route tanimi yapilir.
DB sadece permission kaydi ve rol baglantisini tutar.
```

### Role Permission Atarken Sadece Yeni Permission Gondermek

Yanlis:

```text
POST /api/roles/{id}/permissions -> sadece yeni permission ID
```

Dogru:

```text
Rolun sahip olmasi gereken tum permission ID listesi gonderilir.
```

### Kullaniciya Yetki Verilip Eski Token ile Test Etmek

Yanlis:

```text
Role yetki verdim ama kullanici hala 403 aliyor, sistem bozuk.
```

Dogru:

```text
Kullanici yeniden login olmali veya token yenilenmeli.
```

### Controller'a DB/Integration Detayi Koymak

Yanlis:

```text
Controller icinde kompleks EF query veya Mikro API payload hazirlamak.
```

Dogru:

```text
Controller use case/service cagirir.
DB ve entegrasyon detayi Infrastructure icinde kalir.
```

### Mikro REST API'ye Toplu ve Kontrolsuz Gecmek

Yanlis:

```text
Mevcut DB write akislarini tek seferde Mikro REST'e tasimak.
```

Dogru:

```text
Modul bazli pilot gecis.
Payload ve response semasini test etmek.
Gerekirse DB'den geri okuma ile dogrulamak.
Duplicate ve seri/sira/GUID riskini kontrol etmek.
```

## 32. Kisa Zihinsel Model

Projeyi akilda tutmanin en kolay hali:

```text
Domain         -> cekirdek entity ve temel kurallar
Application    -> kontratlar, DTO'lar, permission dili
Infrastructure -> DB, servis, use case implementation, entegrasyon
WebApi         -> HTTP giris kapisi
```

Bir kullanicinin erisim modeli:

```text
User -> Role -> Permission -> JWT Claim -> Policy -> Endpoint
```

Bir menu'nun modeli:

```text
PermissionCatalog -> Permission Tree -> auth/me -> Frontend Menu
```

Bir endpoint'in modeli:

```text
Controller -> UseCase -> QueryExecutor/WriteService -> Db/Integration -> DTO
```

Bir permission'in dogru yasam dongusu:

```text
PermissionCatalog'a ekle
DB'ye senkronla veya migration ile tasi
Role'e ata
Kullaniciyi yeniden login ettir
Endpoint'te policy olarak kullan
Frontend'de menu/buton gorunurlugune bagla
```

## 33. Ilgili Dokumanlar

Repo icindeki ek dokumanlar:

```text
YENI_MENU_YETKI_MIGRATION_REHBERI.md
UI_API_DOKUMANI.md
MIKRO_REST_API_GECIS_ANALIZI.md
MIKRO_API_POSTMAN_DOKUMANI.md
AXATA_ENTEGRASYON_ALTYAPISI.md
MIKRO_MUHASEBE_AKIS_REHBERI.md
DEPO_MAL_KABUL_ISLEYIS.md
FIRMA_MAL_KABUL_SENARYO.md
FATURA_GONDERIM_SISTEMI.md
UBL_FATURA_MANTIGI.md
PRODUCTION_HAZIRLIK.md
```

Bu dosya buyuk resmi anlatir. Modul veya entegrasyon bazli detaylar icin ilgili dokumana bakilmalidir.
