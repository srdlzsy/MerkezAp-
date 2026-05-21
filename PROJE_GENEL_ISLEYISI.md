# FurpaMerkezApi Proje Genel Isleyisi

Bu dokuman, projeye yeni giren birinin sistemi hizli ama saglam sekilde anlamasi icin hazirlandi.

Amacimiz su sorulara net cevap vermek:

- Bu API ne is yapiyor?
- Hangi katman ne sorumluluk tasiyor?
- Bir istek sisteme girdiginde hangi adimlardan geciyor?
- Yetki ve menu yapisi nasil calisiyor?
- Yeni bir ekran veya endpoint eklemek istersek nereye dokunuyoruz?

## 1. Projenin Kisa Ozeti

`FurpaMerkezApi`, Furpa merkez operasyonlari icin yazilmis bir `.NET 9` Web API projesidir.

Proje tek bir veritabaniyla degil, birden fazla kaynakla calisir:

- `Auth` verisi icin `PostgreSQL`
- Operasyonel is verisi icin `SQL Server / Mikro`
- API'ye ozel bazi yardimci tablolar ve gorunumler icin `SQL Server / Furpa`

Bu API'nin temel gorevi sunlardir:

- kullanici girisi yapmak
- kullanici, rol ve yetki yonetmek
- merkez operasyon ekranlari icin veri sunmak
- bazi belgeleri veya hareketleri Mikro/Furpa uzerinden okumak ya da yazmak
- bazi entegrasyon ve dosya uretim islemlerini kuyruk uzerinden arka planda calistirmak

Kisaca:

`Frontend -> WebApi -> Use Case -> Query/Write Service -> Veritabani/Entegrasyon`

## 1.1 Secret ve Repo Kurali

Bu proje icinde secret yonetimi icin net kural sunlardir:

- `appsettings.json` ve `appsettings.Production.json` repoda kalir ama secret tasimaz
- gercek sifreler, connection string'ler ve JWT secret'lari GitHub'a gonderilmez
- lokal calisma icin `src/FurpaMerkezApi.WebApi/appsettings.Local.json` kullanilir
- bu dosya `.gitignore` icinde oldugu icin repoya gitmez
- production secret'lari server ortaminda tutulur

Cok onemli:

- Bir secret track edilen dosyaya yazilip push edilirse artik "gizli" sayilmaz
- Sonradan dosyadan silmek tek basina yeterli degildir
- Bu durumda secret rotate edilmelidir

## 2. Katmanlar ve Sorumluluklari

Projede 4 ana katman var.

### 2.1 Domain

`Domain` katmani sistemin cekirdek auth varliklarini tutar.

Burada ozellikle su entity'ler bulunur:

- `AppUser`
- `AppRole`
- `AppPermission`
- `AppUserRole`
- `AppRolePermission`

Bu katmanin gorevi:

- veri dogrulama ve normalize etme
- temel entity kurallarini koruma
- auth tarafindaki iliski modelini tasima

Burada controller, db context veya HTTP bilgisi yoktur.

### 2.2 Application

`Application` katmani bu projede daha cok "kontrat ve dil" katmani gibi kullaniliyor.

Burada genelde sunlar bulunur:

- request modelleri
- response / dto modelleri
- use case interface'leri
- servis interface'leri
- permission katalogu

Onemli bir not:

Bu repo "clean architecture" cizgisine yakin dursa da, use case implementasyonlarinin buyuk kismi `Application` katmaninda degil, `Infrastructure` katmanindadir.

Yani `Application` daha cok su isi yapar:

- sistemin ne bekledigini tanimlar
- isimlendirme standardini korur
- katmanlar arasi baglayici kontrat saglar

### 2.3 Infrastructure

`Infrastructure` katmani sistemin calisan motorudur.

Burada sunlar bulunur:

- `DbContext`'ler
- auth servisleri
- JWT token uretimi
- query executor'lar
- write service'ler
- use case implementasyonlari
- entegrasyon servisleri
- background worker'lar

Kisaca:

- `Application` ne yapilacagini soyler
- `Infrastructure` bunu nasil yapacagini gercekler

### 2.4 WebApi

`WebApi` katmani HTTP giris kapisidir.

Burada sunlar yer alir:

- `Program.cs`
- controller'lar
- authentication/authorization konfigurasyonu
- swagger
- exception middleware

Bu katmanin gorevi:

- HTTP istegini almak
- request'i parse etmek
- ilgili use case'i cagirmak
- sonucu HTTP cevabina cevirmek

Burada agir is kurali tutulmamasi hedeflenmis.

## 3. Veritabani Yapisi

Projede birden fazla veri kaynagi var. Bu ayrim projeyi anlamak icin cok onemli.

### 3.1 AuthConnection

Bu veritabani `PostgreSQL` tarafidir.

Burada tutulur:

- kullanicilar
- roller
- yetkiler
- user-role iliskileri
- role-permission iliskileri

Bu alan sistemin "kim giris yapabilir, neyi gorebilir?" kismini yonetir.

### 3.2 MikroConnection

Bu veritabani operasyonel is verilerinin ana kaynagidir.

Ornek:

- siparisler
- stoklar
- sevk verileri
- mal kabul hareketleri
- iade verileri

Bu veritabani agirlikli olarak okuma amacli kullanilir.

### 3.3 MikroWriteConnection / testMikroConnection

Projede okuma ve yazma icin farkli Mikro baglantisi kullanilabilecek bir yapi var.

`MikroDatabase:Profile` ayari buna karar verir:

- `Live`: okuma ve yazma ayni canli veritabani
- `Test`: okuma ve yazma test veritabani
- `Split`: okuma canli, yazma ayri baglanti

Bu yapi sayesinde kritik yazma operasyonlari kontrollu yonetilebilir.

### 3.4 FurpaConnection

Bu veritabani API'ye ozel yardimci tablolari ve gorunumleri barindirir.

Ornek:

- `LabelDocuments`
- `LabelDocumentDetails`
- `AuthorizationFiles`
- `Cashiers`
- `BranchDetails`
- `VwKunyeNet` gibi gorunumler

Yani bu kisim, "Mikro'nun ham tablolarindan ayri olarak API'nin kendi ihtiyaclari icin kullandigi alan" gibi dusunulebilir.

## 4. Uygulama Acildiginda Ne Olur?

Uygulama calistiginda genel akis su sekildedir:

1. `Program.cs` icinde uygulama ayaga kalkar.
2. `Controllers`, `Routing`, `CORS`, `DataProtection` ve diger servisler kaydedilir.
3. `AddCleanArchitecture(...)` cagrisi ile hem `WebApi` hem `Infrastructure` servisleri DI container'a eklenir.
4. Uygulama build edildikten sonra `InitializeDatabaseAsync()` cagrilir.
5. Bu adimda auth veritabani migration'lari uygulanir.
6. Ardindan permission katalogu veritabaniyla senkronize edilir.
7. Swagger, CORS, exception middleware, authentication ve authorization pipeline'a baglanir.
8. Son olarak controller endpoint'leri map edilir.

Bu tasarim sayesinde uygulama ayaga kalkarken:

- auth semasi guncel hale gelir
- permission katalogu eksikse veritabanina islenir
- API hemen kullanima hazir olur

Production notu:

- `StartupTasks` ayarlari production'da kontrollu acilmalidir
- `appsettings.Production.json` repo icinde template olarak kalmali, gercek degerler server tarafinda doldurulmalidir

## 5. Authentication ve Authorization Mantigi

Bu proje sadece login yapan kullaniciyi tanimaz. Ayni zamanda kullanicinin:

- hangi depoya bagli oldugunu
- hangi rollere sahip oldugunu
- hangi permission'lara sahip oldugunu

JWT token icine yazar.

### 5.1 Login akisi

`POST /api/auth/login` cagrildiginda:

1. kullanici username veya email ile aranir
2. rol ve permission iliskileri birlikte yuklenir
3. sifre dogrulanir
4. kullanici aktif degilse giris engellenir
5. JWT token uretilir
6. response icinde hem token hem de `UserDto` doner

### 5.2 Token icinde neler var?

JWT token icine su bilgiler yazilir:

- kullanici id
- username
- email
- ad soyad
- `warehouse_no`
- `warehouse_name`
- roller
- permission claim'leri

Bu cok onemli cunku bircok endpoint depo numarasini query'den degil, dogrudan token'dan okur.

### 5.3 Yetki nasil kontrol ediliyor?

Projedeki authorization mantigi policy tabanlidir.

Her permission kodu icin startup asamasinda otomatik policy uretilir.

Ornek permission kodlari:

- `kasa-islemleri.etiket-belgeleri.list`
- `siparis-islemleri.verilen-depo-siparisleri.detail`
- `kullanici-islemleri.users.manage`

Bir action ustunde su sekilde kullanilir:

```csharp
[Authorize(Policy = "kasa-islemleri.etiket-belgeleri.list")]
```

Bu durumda token icinde ilgili `permission` claim'i yoksa istek reddedilir.

## 6. Permission ve Menu Agaci Mantigi

Bu projede yetki yapisi ayni zamanda frontend menu yapisini da besler.

Temel hiyerarsi soyledir:

`Module -> Menu -> Action`

Ornek:

- Module: `kasa-islemleri`
- Menu: `etiket-belgeleri`
- Action: `list`

Tam permission kodu:

`kasa-islemleri.etiket-belgeleri.list`

### 6.1 Permission katalogu ne yapiyor?

`PermissionCatalog`, sistemdeki standart permission listesini tanimlar.

Bu liste:

- startup'ta veritabanina senkronize edilir
- authorization policy'lerinin olusmasini saglar
- frontend'in menu agaci kurmasina kaynak olur

### 6.2 Frontend menuyu nereden uretebilir?

Iki ana kaynak var:

- `GET /api/auth/me`
- `GET /api/permissions/catalog`

`auth/me` cevabinda kullanicinin gorebilecegi module-menu-action agaci doner.

Bu sayede frontend:

- kullanicinin hangi menuleri gorecegini
- hangi aksiyonlari yapabilecegini

dinamik sekilde uretebilir.

### 6.3 Ayni controller icinde birden fazla menu olabilir mi?

Evet, olabilir.

Proje bunu destekliyor.

Yani "ayri menu" demek her zaman "ayri controller" demek degildir.

Asil belirleyici sey su:

- route
- permission kodu
- menu code
- frontend'in bu menuye nasil baktigi

## 7. Bir Istek Sistemde Nasil Ilerliyor?

En sade sekilde bir endpoint'in yolculugu su sekildedir:

1. frontend HTTP istegi gonderir
2. controller istegi alir
3. request modeli cikarilir
4. kullanicidan gerekli claim'ler okunur
5. ilgili use case interface'i cagrilir
6. `Infrastructure` icindeki implementasyon devreye girer
7. query executor veya write service veritabanina gider
8. sonuc dto olarak doner
9. controller bunu `200`, `201` gibi uygun HTTP response'a cevirir

## 8. Gercek Ornek: Kunye / Etiket Akisi

`EtiketBelgeleriController` icindeki `etiketler` endpoint'i bu akisin guzel bir ornegidir.

Mantik su sekilde ilerler:

1. endpoint request'ten tarihi alir
2. depo numarasini token'dan alir
3. `IListLabelTagsUseCase` cagrilir
4. use case implementasyonu `LabelTagQueryExecutor`'a delegasyon yapar
5. query executor `FurpaDbContext` uzerinden veritabani baglantisini acar
6. `VwKunyeNet` gorunumune SQL sorgusu atar
7. satirlari `LabelTagDto` listesine map eder
8. controller `200 OK` ile sonucu doner

Buradaki kritik gozlem su:

- controller veri sorgulamaz
- use case icinde de agir mantik yok
- asil sorgu `query executor` icindedir

Bu desen proje genelinde cok tekrar eder.

## 9. Okuma ve Yazma Desenleri

Projede okuma ve yazma akislarinda farkli sinif tipleri gorulur.

### 9.1 Query Executor

Okuma tarafinda genelde:

- `...QueryExecutor`
- `List...UseCase`
- `Get...UseCase`

deseni vardir.

Bu siniflar:

- sorgu kurar
- filtreleri uygular
- dto map eder
- sonucu doner

### 9.2 Write Service

Yazma tarafinda genelde:

- `...WriteService`
- `Create...UseCase`

deseni gorulur.

Bu siniflar:

- request'i validate eder
- transaction acar
- ilgili tabloya insert/update yapar
- sonucu response dto olarak doner

### 9.3 Neden bu ayrim var?

Bu ayrim kodu daha okunur yapar:

- controller HTTP bilir
- use case is adini bilir
- executor/service ise veritabani detayini bilir

## 10. Exception ve Hata Yonetimi

Projede merkezi bir exception middleware bulunur.

Bu middleware throw edilen exception'lari `ProblemDetails` formatinda HTTP cevabina cevirir.

Eslesmeler genel olarak su sekildedir:

- `ArgumentException` -> `400 Bad Request`
- `UnauthorizedAccessException` -> `401 Unauthorized`
- `InvalidOperationException` -> `409 Conflict`
- `KeyNotFoundException` -> `404 Not Found`
- diger tum exception'lar -> `500 Internal Server Error`

Bu sayede controller'larda surekli `try/catch` yazmaya gerek kalmaz.

## 11. Claims ve Warehouse Mantigi

Projede bircok endpoint depo bilgisini query'den almak zorunda kalmaz.

Cunku JWT token icinde:

- `warehouse_no`
- `warehouse_name`

claim'leri vardir.

Controller tarafinda sik gorulen kullanim:

```csharp
var warehouseNo = User.GetRequiredWarehouseNo();
```

Bu yaklasim su avantajlari saglar:

- kullanici kendi deposu disina kolayca tasamaz
- frontend daha az parametre gonderir
- endpoint'ler daha standart olur

Tabii bazi endpoint'ler opsiyonel olarak query'den de `warehouseNo` alabilir.

## 12. Background Job ve Entegrasyon Mantigi

Bu proje sadece anlik HTTP cevaplari ureten bir API degil.

Bazi isler kuyruga atilir ve arkada calisir.

### 12.1 Axata senkronizasyonu

Axata ile ilgili kisimda:

- bir queue vardir
- bir worker vardir
- bir scheduler vardir

Calisma mantigi:

1. is kuyruga eklenir
2. worker kuyruktan isi alir
3. ilgili task handler ile entegrasyon isi yapilir
4. sonuc basarili / basarisiz olarak isaretlenir

Scheduler aktifse belirli araliklarla otomatik is de uretebilir.

### 12.2 Operations dosya uretimi

`Operations` modulu da benzer mantikla calisir.

Amac:

- dosya uretim taleplerini kuyruga almak
- uzun surebilecek dosya uretimini request-response akisinin disina tasimak

Bu sayede kullanici:

- job id alir
- sonra job durumunu sorgular

## 13. Iskelet Endpoint Mantigi

Projede bazi menu ve action'lar henuz tam baglanmamis olabilir.

Bu durumlarda `ModuleMenuControllerBase` uzerinden `501 Not Implemented` donen iskelet endpoint'ler bulunur.

Bunun anlami su:

- menu
- route
- permission yapisi

simdiden tanimlanmistir ama is kurali veya veritabani baglantisi henuz bitmemistir.

Bu yaklasim frontend ile backend'in ayni menu agacinda paralel ilerlemesini kolaylastirir.

## 14. Yeni Bir Modul veya Ekran Eklerken Genel Yol

Projede yeni bir is gelistirirken genelde su adimlar izlenir:

1. once bunun hangi `module/menu/action` yapisina ait oldugu belirlenir
2. gerekiyorsa `PermissionCatalog`'a yeni permission tanimi eklenir
3. `Application` katmaninda request/response ve use case interface'i tanimlanir
4. `Infrastructure` katmaninda implementasyon yazilir
5. gerekiyorsa query executor veya write service olusturulur
6. `WebApi` katmaninda controller action eklenir
7. ilgili servis DI'a kaydedilir
8. frontend bu yeni route ve permission yapisini kullanir

## 15. Migration Ne Zaman Gerekir?

Bu konu projede en cok karistirilan basliklardan biridir.

### 15.1 Gerekmez

Su durumlarda cogu zaman migration gerekmez:

- sadece yeni controller action eklemek
- sadece yeni use case yazmak
- sadece yeni permission/menu kodu eklemek

Cunku permission katalogu startup'ta veritabanina senkronize edilir.

### 15.2 Gerekir

Su durumlarda migration gerekir:

- `AuthDbContext` entity veya tablo semasi degisirse
- `FurpaDbContext` tarafinda EF ile yonetilen tablo semasi degisirse
- yeni EF entity eklenip fiziksel tablo bekleniyorsa

### 15.3 Dikkat edilmesi gereken nokta

`Mikro` veritabani bu projede genelde mevcut bir harici sistem gibi kullaniliyor.

Yani Mikro tarafinda tablo yapisini bu API'nin migration'i ile yonetmek standart senaryo degil.

## 16. Bu Projede Kod Okumaya Nereden Baslanmali?

Projeyi ilk kez okuyacak biri icin en iyi baslangic sirasi su olur:

1. `README.md`
2. `src/FurpaMerkezApi.WebApi/Program.cs`
3. `src/FurpaMerkezApi.WebApi/Configuration/ServiceCollectionExtensions.cs`
4. `src/FurpaMerkezApi.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`
5. `src/FurpaMerkezApi.Application/Security/PermissionCatalog.cs`
6. `src/FurpaMerkezApi.Infrastructure/Services/AuthService.cs`
7. ilgini ceken bir modul controller'i
8. o controller'in kullandigi use case implementasyonu
9. ilgili query executor veya write service

Bu sirayla gidersen:

- sistem nasil ayaga kalkiyor
- auth nasil calisiyor
- permission yapisi nasil olusuyor
- bir endpoint veriyi nasil cekiyor

cok daha hizli anlasilir.

## 17. Kisa Bir Zihinsel Model

Projeyi akilda tutmanin en kolay yolu su modeldir:

- `Domain`: kimlik ve yetki cekirdegi
- `Application`: kontratlar, dto'lar, permission dili
- `Infrastructure`: gercek is yapan katman
- `WebApi`: HTTP giris kapisi

Bir istegin hayati:

`Frontend -> Controller -> UseCase -> Query/Write Service -> Db/Integration -> DTO -> HTTP Response`

Bir kullanicinin hayati:

`User -> Role -> Permission -> JWT Claim -> Policy -> Endpoint Erisimi`

Bir menu'nun hayati:

`PermissionCatalog -> Permission Tree -> auth/me -> Frontend Menu`

## 18. Sonuc

Bu proje temelde su fikre dayanir:

- auth ve yetki merkezi yonetilir
- operasyonel veri farkli kaynaklardan okunur/yazilir
- her is alani module-menu-action mantigina oturur
- controller hafif tutulur
- asil uygulama davranisi `Infrastructure` tarafinda toplanir

Projeyi buyuturken en saglam yaklasim su olur:

- once menu ve permission'i netlestir
- sonra request/response dilini kur
- sonra db/entegrasyon implementasyonunu yaz
- en son controller ve frontend baglantisini tamamla

## 19. Ilgili Diger Dokumanlar

Repo icinde senaryo bazli ek dokumanlar da bulunuyor:

- `UI_API_DOKUMANI.md`
- `AXATA_ENTEGRASYON_ALTYAPISI.md`
- `UYUMSOFT_ENTEGRASYON_DOKUMANI.md`
- `DEPO_MAL_KABUL_ISLEYIS.md`
- `FIRMA_MAL_KABUL_SENARYO.md`

Bu dosya "buyuk resmi" anlatir.

Yukardaki dokumanlar ise belirli modulleri veya entegrasyonlari daha derin anlatir.
