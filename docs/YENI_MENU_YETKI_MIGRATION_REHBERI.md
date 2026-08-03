# Yeni Menu, Yetki ve Migration Rehberi

Bu dokuman FurpaMerkezApi projesinde yeni ekran, menu, endpoint veya Auth DB tablosu eklerken izlenecek yolu anlatir.

Ana fikir:

```text
PermissionCatalog.cs = uygulamanin bildigi yetki sozlugu
app_permissions       = bu sozlugun DB'deki kaydi
app_roles             = rol tanimlari
app_role_permissions  = rolun sahip oldugu yetkiler
app_user_roles        = kullanicinin rolleri
JWT permission claim  = login anindaki aktif yetkiler
[Authorize(Policy)]   = endpoint seviyesinde gercek guvenlik
Frontend permission   = menu, route, buton ve depo secici gorunurlugu
```

DB'ye elle permission eklemek tek basina yeni modul veya endpoint olusturmaz. Bu projede yeni yetki kodunun ana kaynagi kod tarafindaki `PermissionCatalog.cs` dosyasidir.

## Temel Kural

Her permission kodu su formdadir:

```text
{moduleCode}.{menuCode}.{actionCode}
```

Ornek:

```text
sevk-islemleri.giden-depolar-arasi-sevkler.list
ayar-islemleri.soforler.manage
green-grocer.product-case-profiles.update
```

Controller tarafinda ayni kod policy olarak kullanilir:

```csharp
[Authorize(Policy = "ayar-islemleri.soforler.list")]
```

Kullanici istegi geldiginde JWT icinde su claim aranir:

```text
permission = ayar-islemleri.soforler.list
```

Claim yoksa API `403 Forbidden` doner.

## Page, Manage ve Action Ayrimi

Projede son karar su sekildedir:

```text
menu/route normal ekran     -> *.page
menu/route yonetim ekrani   -> *.manage
liste/veri cekme            -> *.list
detay/veri inceleme         -> *.detail
butonlar                    -> *.create / *.update / *.delete / *.archive / *.transfer
depo secici                 -> *.all-warehouses
```

Bu ayrim onemlidir.

`list`, `detail`, `create`, `update`, `delete` yetkileri endpoint ve buton aksiyonlari icindir. UI sol menu veya route guard icin bu aksiyonlari tek basina kullanmamalidir.

Neden?

```text
Bir kullaniciya lookup veya modal icin list yetkisi verilebilir.
Bu, kullanicinin yonetim ekranini sol menude gormesi gerektigi anlamina gelmez.
```

Ornek:

```text
ayar-islemleri.soforler.manage -> Soforler yonetim ekrani gorunur
ayar-islemleri.soforler.list   -> e-irsaliye modalinda sofor arama API'si calisir
ayar-islemleri.soforler.create -> yeni sofor ekle butonu calisir
ayar-islemleri.soforler.delete -> sil/pasife al butonu calisir
```

GreenGrocer kasa profil ekrani icin de ayni mantik vardir:

```text
green-grocer.product-case-profiles.manage -> profil yonetim sayfasi
green-grocer.product-case-profiles.list   -> profil liste/cozumleme API'si
green-grocer.product-case-profiles.update -> profil kaydetme
```

## PermissionCatalog Nerede?

Dosya:

```text
src/FurpaMerkezApi.Application/Security/PermissionCatalog.cs
```

Bu dosya:

```text
Authorization policy'lerinin uretilmesine kaynak olur.
Startup permission senkronizasyonuna kaynak olur.
AuthDbContext seed/snapshot verisine kaynak olur.
Frontend menu agacinin anlamlandirilmasina kaynak olur.
```

Uygulama acilisinda policy'ler katalogdan uretilir:

```csharp
foreach (var permissionCode in PermissionCatalog.Codes)
{
    options.AddPolicy(permissionCode, policy => policy.RequireClaim("permission", permissionCode));
}
```

Katalogda olmayan bir permission kodu standart akista policy olarak taninmaz.

## Mevcut Action Setleri

`PermissionCatalog.cs` icinde action setleri merkezi tutulur. Yeni menu eklerken en yakin set secilmelidir.

### CrudActions

Normal CRUD ekranlar icin varsayilandir:

```text
page
list
detail
create
update
all-warehouses
```

Ornek:

```csharp
..CreateMenuPermissions("stok-islemleri", "StokIslemleri", "zayiat-fisleri", "ZayiatFisleri")
```

### ManageCrudActions

Yonetim/tanim ekranlari icindir:

```text
manage
list
detail
create
update
all-warehouses
```

Ornek ayar ekranlari:

```text
ayar-islemleri.cihazlar.*
ayar-islemleri.sube-ayarlari.*
ayar-islemleri.kasa-pos-terminalleri.*
ayar-islemleri.kasiyerler.*
```

### ManageCrudDeleteActions

Yonetim/tanim ekrani olup silme/pasife alma aksiyonu olan ekranlar icindir:

```text
manage
list
detail
create
update
delete
all-warehouses
```

Guncel ornek:

```text
ayar-islemleri.soforler.*
```

### ReadActions

Liste + detay olan, yazma olmayan ekranlar:

```text
page
list
detail
all-warehouses
```

### ListActions

Sadece liste/arama/rapor gibi ekranlar:

```text
page
list
all-warehouses
```

### ReadCreateActions

Liste, detay ve ekleme vardir; update yoktur:

```text
page
list
detail
create
all-warehouses
```

### ReadUpdateActions

Liste, detay ve update vardir; create yoktur:

```text
page
list
detail
update
all-warehouses
```

### ReadUpdateDeleteActions

Liste, detay, update ve delete vardir:

```text
page
list
detail
update
delete
all-warehouses
```

### Ozel Action Setleri

Projede bazi ekranlar kendi setini kullanir:

```text
FeedbackActions             -> page/list/detail/update/list-all
AnnouncementActions         -> page/list/detail/create/update/archive/all-warehouses
StockAnomalyActions         -> page/list/detail/update/scan/all-warehouses
ProductCaseProfileActions   -> manage/list/detail/create/update/delete/all-warehouses
EtiketBasimActions          -> page/list/detail/create/update/delete/transfer/all-warehouses
ProductDistributionActions  -> page/list/detail/create/update/delete/all-warehouses
```

Ozel action gerekiyorsa isim kisa, kebab-case ve is anlamina uygun olmalidir.

## Yeni Menu Ekleme

Once su kararlar verilir:

```text
moduleCode  -> kebab-case, URL/policy uyumlu
moduleName  -> PascalCase, okunabilir
menuCode    -> kebab-case, URL/policy uyumlu
menuName    -> PascalCase, okunabilir
actionSet   -> page mi manage mi, hangi API aksiyonlari var?
```

Normal ekran ornegi:

```csharp
..CreateMenuPermissions(
    "mal-kabul-islemleri",
    "MalKabulIslemleri",
    "mal-kabul-farklari",
    "MalKabulFarklari",
    ListActions),
```

Yonetim ekrani ornegi:

```csharp
..CreateMenuPermissions(
    "ayar-islemleri",
    "AyarIslemleri",
    "soforler",
    "Soforler",
    ManageCrudDeleteActions),
```

Uretilen yetkiler:

```text
ayar-islemleri.soforler.manage
ayar-islemleri.soforler.list
ayar-islemleri.soforler.detail
ayar-islemleri.soforler.create
ayar-islemleri.soforler.update
ayar-islemleri.soforler.delete
ayar-islemleri.soforler.all-warehouses
```

## Controller Kurallari

Controller route'u kebab-case olmalidir:

```csharp
[ApiController]
[Route("api/ayar-islemleri/soforler")]
public sealed class SoforlerController(...)
```

Policy const'lari permission koduyla birebir ayni olmalidir:

```csharp
private const string ListPolicy = "ayar-islemleri.soforler.list";
private const string DetailPolicy = "ayar-islemleri.soforler.detail";
private const string CreatePolicy = "ayar-islemleri.soforler.create";
private const string UpdatePolicy = "ayar-islemleri.soforler.update";
private const string DeletePolicy = "ayar-islemleri.soforler.delete";
```

Endpoint dogru policy ile korunmalidir:

```csharp
[HttpGet]
[Authorize(Policy = ListPolicy)]
public async Task<ActionResult<IReadOnlyCollection<DespatchDriverDto>>> List(...)

[HttpPost]
[Authorize(Policy = CreatePolicy)]
public async Task<ActionResult<DespatchDriverDto>> Create(...)
```

Yanlis ornek:

```csharp
[HttpGet]
[Authorize(Policy = "ayar-islemleri.soforler.update")]
public async Task<IActionResult> List(...)
```

Liste endpoint'i update yetkisi istememelidir.

## ModuleMenuControllerBase

Modul controller'lari genellikle `ModuleMenuControllerBase` miras alir.

Amac:

```text
moduleCode/moduleName/menuCode/menuName bilgisini standart tutmak
scaffold/metadata cevaplarini ayni yapida donmek
frontend tarafinda menu agaci ile controller mantigini uyumlu tutmak
```

Ornek:

```csharp
public sealed class SoforlerController(...)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
```

## Application Katmani

Yeni endpoint veya modul icin request/response/interface Application katmaninda tutulur.

Ornek klasor:

```text
src/FurpaMerkezApi.Application/Modules/AyarIslemleri/Soforler/
```

Tipik icerik:

```text
DespatchDriverListRequest
SaveDespatchDriverRequest
DespatchDriverDto
IDespatchDriverService
```

Controller HTTP modelini Application modeline map eder. Application modelleri HTTP attribute'lariyla kirletilmemelidir.

## Infrastructure Katmani

DB sorgusu, transaction, dis servis ve is kurali implementasyonu Infrastructure icindedir.

Ornek klasor:

```text
src/FurpaMerkezApi.Infrastructure/Modules/AyarIslemleri/Soforler/
```

Okuma sorgularinda genel kural:

```text
AsNoTracking kullan.
DTO projection yap.
Take/limit koy.
Depo yetkisi gerekiyorsa controller/claim helper ile coz.
```

Yazma sorgularinda genel kural:

```text
validasyon yap
duplicate riskini kontrol et
gerekirse transaction ac
audit alanlarini clock ile set et
anlamli exception firlat
```

Merkezi hata karsiliklari:

```text
ArgumentException         -> 400
UnauthorizedAccessException -> 401
ForbiddenAccessException  -> 403
KeyNotFoundException      -> 404
InvalidOperationException -> 409
TimeoutException          -> 504
```

## DI Kaydi

Yeni servis veya use case eklenirse DI kaydi zorunludur.

Dosya:

```text
src/FurpaMerkezApi.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs
```

Ornek:

```csharp
services.AddScoped<IDespatchDriverService, DespatchDriverService>();
```

DI kaydi yoksa uygulama runtime'da dependency resolution hatasi verir.

## AuthDbContext ve Migration

Auth DB su verileri tutar:

```text
kullanicilar
roller
permission kayitlari
role-permission iliskileri
duyurular
sikayet/oneriler
belge akis izleme
stock anomaly kayitlari
GreenGrocer kasa profilleri
GreenGrocer siparis snapshot'lari
e-irsaliye sofor tanimlari
```

Yeni Auth entity/tablo eklenirse migration gerekir.

Guncel ornek:

```text
despatch_drivers tablosu -> AddDespatchDrivers migration'i
ayar-islemleri.soforler.* yetkileri -> ayni migration icinde seed
```

## Permission DB'ye Nasil Duser?

Iki yol vardir.

### Startup Senkronizasyonu

Ayar:

```json
{
  "StartupTasks": {
    "SynchronizePermissionCatalog": true
  }
}
```

Bu aciksa uygulama acilisinda:

```text
PermissionCatalog.Definitions okunur.
Eksik app_permissions kayitlari eklenir.
Name/description farklari guncellenir.
Administrator role'e eksik katalog yetkileri eklenir.
```

Development icin pratiktir. Production'da kontrollu migration daha guvenlidir.

### Migration

Kontrollu deployment icin tavsiye edilen yol:

```text
PermissionCatalog'a ekle
dotnet ef migrations add ...
Migration'i incele
AuthDbContextModelSnapshot uyumunu kontrol et
dotnet ef database update ile uygula
```

EF, katalogdaki seed degisikligini migration'a `InsertData` olarak ekler.

## Deterministic Permission ID

Permission ID'leri ortamdan ortama degismemelidir.

Bu projede seed mantigi permission code'dan deterministic GUID uretir:

```text
permission:{code}
```

Bu sayede ayni permission code her ortamda ayni GUID'i alir.

Dikkat:

```text
code degisirse ID de degisir
rename islemi yeni permission eklemekten farklidir
role-permission baglantilari migrate edilmelidir
```

## AuthDbContextModelSnapshot Kontrolu

Migration sonrasinda snapshot kontrol edilmelidir:

```text
src/FurpaMerkezApi.Infrastructure/Migrations/AuthDbContextModelSnapshot.cs
```

Pratik kontrol komutu:

```powershell
dotnet ef migrations has-pending-model-changes --project src\FurpaMerkezApi.Infrastructure --startup-project src\FurpaMerkezApi.WebApi --context AuthDbContext
```

Beklenen sonuc:

```text
No changes have been made to the model since the last migration.
```

Bu kontrol gecmezse `dotnet run` sirasinda `PendingModelChangesWarning` ile API acilmayabilir.

## Migration Komutlari

Yeni migration:

```powershell
dotnet ef migrations add AddMyFeature --project src\FurpaMerkezApi.Infrastructure --startup-project src\FurpaMerkezApi.WebApi --context AuthDbContext
```

DB'ye uygulama:

```powershell
dotnet ef database update --project src\FurpaMerkezApi.Infrastructure --startup-project src\FurpaMerkezApi.WebApi --context AuthDbContext
```

Pending model kontrolu:

```powershell
dotnet ef migrations has-pending-model-changes --project src\FurpaMerkezApi.Infrastructure --startup-project src\FurpaMerkezApi.WebApi --context AuthDbContext
```

Build/test:

```powershell
dotnet build FurpaMerkezApi.sln
dotnet test FurpaMerkezApi.sln --no-build
```

## Rollere Yetki Verme

Migration genelde Administrator role'e yeni yetkiyi baglar.

Admin olmayan roller icin:

```text
Admin panelden role permission atanir.
Ya da ayri role-permission migration'i yazilir.
```

Role permission endpoint'i mevcut role permission'larini silip gelen listeyi bastan yazar:

```text
POST /api/roles/{roleId}/permissions
```

Bu yuzden sadece yeni permission ID'si gonderilmez. Rolun sahip olmasi gereken tum permission ID listesi gonderilir.

## Token Ne Zaman Guncellenir?

Kullanici login oldugunda:

```text
roller okunur
rollerin permission'lari okunur
permission claim'leri JWT icine yazilir
```

Role veya permission degistiyse mevcut token kendiliginden degismez.

Gerekli aksiyon:

```text
kullanici cikis-giris yapar
veya token refresh akisi varsa yeni token alir
```

Eski token ile endpoint hala `403 Forbidden` donebilir. Bu normaldir.

## all-warehouses Mantigi

Depo kapsamli endpointlerde ek yetki:

```text
{module}.{menu}.all-warehouses
```

Bu yetki varsa UI depo secici gosterebilir. Yoksa kullanici sadece JWT icindeki kendi deposu ile calisir.

Backend genelde aksiyon policy'sinden all-warehouses kodunu turetir:

```text
sevk-islemleri.giden-depolar-arasi-sevkler.list
-> sevk-islemleri.giden-depolar-arasi-sevkler.all-warehouses
```

UI role adına bakmamalidir. Depo secici icin ilgili permission koduna bakmalidir.

## Frontend Kurali

Frontend `login.user.permissions` veya `GET /api/auth/me` cevabindaki permission listesine bakar.

Dogru karar tablosu:

```text
normal menu/route          -> *.page
yonetim/tanim menu/route   -> *.manage
liste tablosu refresh      -> *.list
detay butonu               -> *.detail
ekle butonu                -> *.create
duzenle butonu             -> *.update
sil/pasife al butonu       -> *.delete
arsivle                    -> *.archive
aktar                      -> *.transfer
depo secici                -> *.all-warehouses
```

Role name ile UI acilmamalidir. Admin/Administrator backend tarafinda tam yetkili kabul edilse bile UI karari permission listesine gore verilmelidir.

## Yeni Auth DB Tanimi Eklemek

Soforler modulu bu desenin guncel ornegidir.

Eklenen parcalar:

```text
Domain entity:
  src/FurpaMerkezApi.Domain/Entities/DespatchDriver.cs

Application contract:
  src/FurpaMerkezApi.Application/Modules/AyarIslemleri/Soforler/

Infrastructure service:
  src/FurpaMerkezApi.Infrastructure/Modules/AyarIslemleri/Soforler/

EF configuration:
  src/FurpaMerkezApi.Infrastructure/Persistence/Configurations/DespatchDriverConfiguration.cs

DbContext:
  AuthDbContext.DespatchDrivers

Controller:
  src/FurpaMerkezApi.WebApi/Controllers/Modules/AyarIslemleri/Soforler/

Permission:
  ayar-islemleri.soforler.manage/list/detail/create/update/delete/all-warehouses

Migration:
  AddDespatchDrivers
```

Bu desen yeni Auth DB tanim ekranlari icin tekrar kullanilabilir.

## E-Irsaliye Sofor Secimi Ozel Notu

E-irsaliye gonderme endpointleri artik iki sekli destekler:

```text
Elle giris:
  driverId yok
  plaque + driverNameSurname + driverTckn zorunlu

Kayitli sofor:
  driverId var
  backend aktif sofor kaydindan plaka/ad soyad/TCKN cozer
```

`driverId` ile birlikte manuel alanlar dolu gelirse manuel alanlar secili kaydin ustune yazilir. Bu, UI'da "sofor sec, gerekirse plaka veya ad soyad duzelt" akisini destekler.

## Rename Kurali

Permission code rename etmek yeni permission eklemek degildir.

Dogru migration:

```text
yeni permission'i ekle
eski role-permission baglantilarini yeni permission'a tasi
eski permission'i sil veya devre disi birak
Down metodunda tersini yap
```

Sadece `PermissionCatalog.cs` icinde code degistirmek production DB'de yetki kopmasina sebep olabilir.

## Checklist

Yeni menu/endpoint icin:

```text
[ ] Module code ve menu code net
[ ] Ekran tipi belli: page mi manage mi?
[ ] Action set dogru secildi
[ ] PermissionCatalog.cs guncellendi
[ ] Controller route kebab-case
[ ] Controller policy const'lari permission ile birebir ayni
[ ] Her endpoint en dar dogru policy ile korundu
[ ] Application request/response/interface eklendi
[ ] Infrastructure implementation eklendi
[ ] DI kaydi eklendi
[ ] Auth entity/tablo varsa EF configuration ve DbSet eklendi
[ ] Migration olusturuldu
[ ] Migration diff'i incelendi
[ ] AuthDbContextModelSnapshot kontrol edildi
[ ] UI_API_DOKUMANI.md guncellendi
[ ] dotnet build alindi
[ ] dotnet test calistirildi
[ ] has-pending-model-changes temiz
[ ] Admin disi roller gerekiyorsa permission atandi
[ ] Kullanici token'i yenilendi
[ ] Frontend menu/route/buton permission listesine baglandi
```

## Sik Yapilan Hatalar

### Sadece DB'ye Permission Eklemek

Yanlis:

```text
app_permissions'a kayit attim, menu olusmali.
```

Dogru:

```text
PermissionCatalog'a eklenir.
Controller endpoint kodda olur.
Frontend route/menu tanimi yapilir.
DB sadece yetki kaydi ve rol baglantisini tutar.
```

### List Yetkisiyle Menu Acmak

Yanlis:

```text
*.list varsa sayfa ac.
```

Dogru:

```text
normal sayfa icin *.page
yonetim sayfasi icin *.manage
```

### Role Permission'a Sadece Yeni Yetki Gondermek

Yanlis:

```text
POST /api/roles/{id}/permissions
body: [sadece yeni permissionId]
```

Dogru:

```text
rolun sahip olmasi gereken tum permission ID listesi gonderilir
```

### Yetki Verildi Ama Hala 403

Muhtemel neden:

```text
kullanici eski JWT ile devam ediyor
```

Cozum:

```text
cikis-giris
veya token refresh
```

### PendingModelChangesWarning

Muhtemel neden:

```text
entity/seed degisti ama migration yok
snapshot ile model uyumsuz
```

Cozum:

```text
dotnet ef migrations has-pending-model-changes ...
gerekirse yeni migration olustur
migration diff'ini kontrol et
```

## Kisa Ozet

Bu projenin yetki yasam dongusu:

```text
PermissionCatalog'a ekle
Migration veya startup sync ile DB'ye tasi
Role'e ata
Kullanicinin token'ini yenile
Controller'da policy olarak kullan
Frontend'de menu/buton/depo secici kararini permission'a bagla
```

En onemli cumle:

```text
Yeni bir menu veya endpoint icin yetki DB'den baslamaz; PermissionCatalog.cs icinden baslar.
```
