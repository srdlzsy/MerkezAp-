# Yeni Menu, Yetki ve Migration Rehberi

Bu dokuman, FurpaMerkezApi projesinde yeni bir ekran/menu/API endpoint eklerken yetki sisteminin nasil calistigini ve migration tarafinda hangi adimlarin izlenmesi gerektigini anlatir.

Projede dogru mantik sudur:

```text
PermissionCatalog.cs = sistemde hangi yetkiler var?
DB app_permissions = katalogdaki yetkilerin kayitli hali
DB app_roles = roller
DB app_role_permissions = hangi rol hangi yetkiye sahip?
DB app_user_roles = hangi kullanici hangi role sahip?
JWT permission claim'leri = kullanicinin login anindaki yetkileri
[Authorize(Policy = "...")] = endpoint seviyesinde gercek guvenlik kontrolu
```

Yani DB'ye elle permission eklemek tek basina yeni modul/menu olusturmaz. Bu projede yetki tanimlarinin ana kaynagi kod tarafindaki `PermissionCatalog.cs` dosyasidir.

## 1. Temel Mantik

Her endpoint bir permission code ile korunur.

Ornek:

```text
Module: mal-kabul-islemleri
Menu:   mal-kabul-farklari
Action: list
Policy: mal-kabul-islemleri.mal-kabul-farklari.list
```

Controller tarafinda bu policy kullanilir:

```csharp
[Authorize(Policy = "mal-kabul-islemleri.mal-kabul-farklari.list")]
```

Kullanici bu endpoint'e istek attiginda ASP.NET authorization sistemi JWT icinde su claim var mi diye bakar:

```text
permission = mal-kabul-islemleri.mal-kabul-farklari.list
```

Claim varsa istek gecer. Yoksa API `403 Forbidden` doner.

## 2. DB'ye Permission Ekleyince Neden Modul Olusmaz?

`app_permissions` tablosunda sadece permission kaydi vardir:

```text
id
code
name
description
created_at_utc
updated_at_utc
```

Bu tabloda ayri `module_code`, `menu_code`, `action_code` kolonlari yoktur. Modul/menu/action bilgisi iki yoldan elde edilir:

1. Permission code `PermissionCatalog` icinde varsa katalogdaki tanim kullanilir.
2. Permission code katalogda yoksa kod noktalardan parcalanarak tahmini bilgi uretilir.

Ornek code:

```text
kasa-islemleri.kasa-sayimlari.list
```

Buradan su anlam cikarilir:

```text
module = kasa-islemleri
menu   = kasa-sayimlari
action = list
```

Ama bu sadece gorunum/dto tarafinda yardimci bir yorumlamadir. DB'ye su kaydi elle eklemek:

```text
stok-islemleri.yeni-menu.list
```

sunlari otomatik olusturmaz:

```text
Controller endpoint
Frontend menu
ASP.NET authorization policy
Business service/use case
Migration seed uyumu
```

Bu nedenle yeni menu icin ilk kaynak `PermissionCatalog.cs` olmalidir.

## 3. PermissionCatalog Ne Ise Yarar?

Dosya:

```text
src/FurpaMerkezApi.Application/Security/PermissionCatalog.cs
```

Bu dosya uygulamanin bildigi butun permission kodlarini tanimlar. Uygulama acilirken authorization policy'leri bu katalog uzerinden olusturulur.

WebApi tarafindaki mantik:

```csharp
foreach (var permissionCode in PermissionCatalog.Codes)
{
    options.AddPolicy(permissionCode, policy => policy.RequireClaim("permission", permissionCode));
}
```

Yani bir permission code `PermissionCatalog.Codes` icinde yoksa, standart yapi icinde o permission icin policy de olusmaz.

Bu yuzden:

```text
DB'ye elle permission eklemek = DB kaydi olusturur
PermissionCatalog'a eklemek = uygulamanin o yetkiyi gercekten tanimasini saglar
```

## 4. Action Tipleri

`PermissionCatalog.cs` icinde hazir action gruplari vardir.

### CrudActions

Default davranistir. `CreateMenuPermissions` cagrilirken action verilmezse kullanilir.

```text
list
detail
create
update
```

Ornek:

```csharp
..CreateMenuPermissions("stok-islemleri", "StokIslemleri", "zayiat-fisleri", "ZayiatFisleri")
```

Uretilen permission'lar:

```text
stok-islemleri.zayiat-fisleri.list
stok-islemleri.zayiat-fisleri.detail
stok-islemleri.zayiat-fisleri.create
stok-islemleri.zayiat-fisleri.update
```

### ReadActions

Sadece liste ve detay ekranlari icin kullanilir.

```text
list
detail
```

Ornek:

```csharp
..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "kasa-cirolari", "KasaCirolari", ReadActions)
```

### ListActions

Sadece listeleme yetkisi gereken ekranlar icin kullanilir.

```text
list
```

Ornek:

```csharp
..CreateMenuPermissions("rapor-islemleri", "RaporIslemleri", "satis-analizleri", "SatisAnalizleri", ListActions)
```

### ReadCreateActions

Liste, detay ve ekleme vardir; guncelleme yoktur.

```text
list
detail
create
```

### ReadUpdateActions

Liste, detay ve guncelleme vardir; ekleme yoktur.

```text
list
detail
update
```

### Ozel Action

Gerektiginde ozel action tanimlanabilir. Ornek:

```text
list-all
```

Bu projede `sikayet-oneri` icin `FeedbackActions` kullaniliyor.

## 5. Yeni Menuyu PermissionCatalog'a Ekle

Yeni menu eklerken once module ve menu kararini netlestir.

Karar verilmesi gereken bilgiler:

```text
moduleCode = kebab-case ve URL/policy uyumlu olmalidir
moduleName = PascalCase okunabilir ad
menuCode   = kebab-case ve URL/policy uyumlu olmalidir
menuName   = PascalCase okunabilir ad
actions    = ekranin gercek operasyonlarina gore secilmelidir
```

Sadece liste ekrani olacaksa:

```csharp
..CreateMenuPermissions(
    "mal-kabul-islemleri",
    "MalKabulIslemleri",
    "mal-kabul-farklari",
    "MalKabulFarklari",
    ListActions),
```

CRUD ekran olacaksa action parametresi verme:

```csharp
..CreateMenuPermissions(
    "mal-kabul-islemleri",
    "MalKabulIslemleri",
    "ornek-menu",
    "OrnekMenu"),
```

Bu otomatik olarak su yetkileri uretir:

```text
mal-kabul-islemleri.ornek-menu.list
mal-kabul-islemleri.ornek-menu.detail
mal-kabul-islemleri.ornek-menu.create
mal-kabul-islemleri.ornek-menu.update
```

## 6. Policy Kodunu Controller'da Kullan

Controller'da policy string'i permission code ile birebir ayni olmalidir.

Ornek:

```csharp
[ApiController]
[Route("api/mal-kabul-islemleri/mal-kabul-farklari")]
public sealed class MalKabulFarklariController(...) : ControllerBase
{
    private const string ModuleCode = "mal-kabul-islemleri";
    private const string MenuCode = "mal-kabul-farklari";

    private const string ListPolicy = ModuleCode + "." + MenuCode + ".list";
    private const string DetailPolicy = ModuleCode + "." + MenuCode + ".detail";
    private const string CreatePolicy = ModuleCode + "." + MenuCode + ".create";
    private const string UpdatePolicy = ModuleCode + "." + MenuCode + ".update";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        ...
    }

    [HttpGet("{id}")]
    [Authorize(Policy = DetailPolicy)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        ...
    }

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    public async Task<IActionResult> Create(..., CancellationToken cancellationToken)
    {
        ...
    }

    [HttpPut("{id}")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<IActionResult> Update(int id, ..., CancellationToken cancellationToken)
    {
        ...
    }
}
```

Dikkat edilmesi gerekenler:

```text
Route kebab-case olsun.
Policy tam permission code ile ayni olsun.
List endpoint list policy kullansin.
Detail endpoint detail policy kullansin.
Create endpoint create policy kullansin.
Update endpoint update policy kullansin.
Endpoint'e gereksiz buyuk yetki verme.
```

Yanlis ornek:

```csharp
[Authorize(Policy = "stok-islemleri.zayiat-fisleri.update")]
public async Task<IActionResult> List(...)
```

Liste endpoint'i update yetkisi istememelidir.

## 7. Application Katmanina Contract Ekle

Yeni ekranin request/response/interface dosyalari Application tarafinda durur.

Ornek klasor:

```text
src/FurpaMerkezApi.Application/Modules/MalKabulIslemleri/MalKabulFarklari/
```

Tipik dosyalar:

```text
IListWarehouseReceivingDifferencesUseCase.cs
WarehouseReceivingDifferenceListRequest.cs
WarehouseReceivingDifferenceDto.cs
```

Interface ornegi:

```csharp
public interface IListWarehouseReceivingDifferencesUseCase
{
    Task<IReadOnlyCollection<WarehouseReceivingDifferenceDto>> ExecuteAsync(
        WarehouseReceivingDifferenceListRequest request,
        CancellationToken cancellationToken);
}
```

Request/response tipleri mumkun oldugunca sade tutulmali, controller icindeki body veya query modelleri Application contract'larina temizce map edilmelidir.

## 8. Infrastructure Katmanina Implementasyon Ekle

Is kurali ve DB sorgusu Infrastructure tarafinda yazilir.

Ornek klasor:

```text
src/FurpaMerkezApi.Infrastructure/Modules/MalKabulIslemleri/MalKabulFarklari/
```

Ornek:

```csharp
public sealed class ListWarehouseReceivingDifferencesUseCase(MikroDbContext mikroDbContext)
    : IListWarehouseReceivingDifferencesUseCase
{
    public async Task<IReadOnlyCollection<WarehouseReceivingDifferenceDto>> ExecuteAsync(
        WarehouseReceivingDifferenceListRequest request,
        CancellationToken cancellationToken)
    {
        return await mikroDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(...)
            .Select(...)
            .ToArrayAsync(cancellationToken);
    }
}
```

Okuma sorgularinda `AsNoTracking()` kullanmak iyi olur. Yazma islemlerinde transaction, concurrency, validasyon ve hata mesajlari ekranin riskine gore ayrica dusunulmelidir.

## 9. DI Kaydini Ekle

Dosya:

```text
src/FurpaMerkezApi.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs
```

Using ekle:

```csharp
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabulFarklari;
using FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabulFarklari;
```

Service kaydi:

```csharp
services.AddScoped<
    IListWarehouseReceivingDifferencesUseCase,
    ListWarehouseReceivingDifferencesUseCase>();
```

Kayit eklenmezse controller calisirken dependency resolution hatasi alinir.

## 10. Permission DB'ye Nasil Duser?

Permission'in katalogda tanimli olmasi uygulamanin yetkiyi bilmesini saglar. DB'ye dusmesi icin iki yol vardir.

### 10.1 Startup Synchronization

Uygulama acilisinda su ayar aktifse katalog DB ile senkronlanir:

```json
{
  "StartupTasks": {
    "SynchronizePermissionCatalog": true
  }
}
```

Bu akista:

```text
PermissionCatalog.Definitions okunur.
Eksik app_permissions kayitlari DB'ye eklenir.
Mevcut kayitlarin name/description alanlari guncellenir.
Administrator role varsa eksik yetkiler admin role'e eklenir.
```

Development ortaminda bu pratik olabilir. Ancak production icin migration daha kontrolludur.

### 10.2 Migration

Production veya kontrollu deployment icin yeni permission'lar migration ile DB'ye tasinmalidir.

Migration sunlari yapar:

```text
app_permissions tablosuna yeni permission kayitlarini ekler
app_role_permissions tablosunda Administrator role'e baglar
Down metodunda once role-permission, sonra permission kaydini siler
```

Bu projede tavsiye edilen yontem:

```text
PermissionCatalog'a ekle
Migration yaz
Snapshot uyumunu kontrol et
DB update uygula
```

## 11. Permission Migration Yaz

Sadece yeni permission ekliyorsan en temiz migration su sekilde olur.

Ornek:

```csharp
public partial class AddWarehouseReceivingDifferencePermissions : Migration
{
    private static readonly Guid PermissionId = new("31b9c4fd-80bd-7967-11b0-3fccd5adf5e5");
    private static readonly Guid AdministratorRoleId = new("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a");
    private static readonly DateTime SeededAtUtc = new(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc);

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            table: "app_permissions",
            columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
            values: new object[]
            {
                PermissionId,
                "mal-kabul-islemleri.mal-kabul-farklari.list",
                SeededAtUtc,
                "MalKabulIslemleri > MalKabulFarklari > Listele yetkisi.",
                "MalKabulFarklari Listele",
                null
            });

        migrationBuilder.InsertData(
            table: "app_role_permissions",
            columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
            values: new object[] { PermissionId, AdministratorRoleId, SeededAtUtc });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            table: "app_role_permissions",
            keyColumns: new[] { "permission_id", "role_id" },
            keyValues: new object[] { PermissionId, AdministratorRoleId });

        migrationBuilder.DeleteData(
            table: "app_permissions",
            keyColumn: "id",
            keyValue: PermissionId);
    }
}
```

Birden fazla action varsa her permission icin ayri ID kullanilir:

```text
ornek-menu.list
ornek-menu.detail
ornek-menu.create
ornek-menu.update
```

Her biri `app_permissions` kaydi, her biri admin role icin `app_role_permissions` kaydi ister.

## 12. Permission ID Nasil Secilmeli?

Permission ID sabit ve tekrar uretilebilir olmalidir.

Bu projede `AuthSeedData` icinde su mantik vardir:

```text
permission:{code}
```

metninden MD5 hash alinir ve GUID uretilir.

Bunun nedeni sudur:

```text
Ayni permission code her ortamda ayni GUID'i alsin.
Migration ve seed data birbirine ters dusmesin.
Admin role permission baglantilari bozulmasin.
```

Elle migration yazarken dikkat:

```text
PermissionCatalog'daki code ile migration'daki code birebir ayni olmali.
Migration'daki GUID, AuthSeedData'nin urettigi GUID ile uyumlu olmali.
Code degisirse GUID de degisir.
```

Eger code rename yapiliyorsa sadece yeni permission eklemek yetmez. Eski role-permission baglantilarini yeni permission'a tasiyan migration yazmak gerekir.

## 13. AuthDbContextModelSnapshot Kontrolu

Migration elle yazildiysa `AuthDbContextModelSnapshot.cs` dosyasinda seed snapshot uyumu kontrol edilmelidir.

Dosya:

```text
src/FurpaMerkezApi.Infrastructure/Migrations/AuthDbContextModelSnapshot.cs
```

Seed veriler iki yerde gorulebilir:

```text
app_permissions HasData
app_role_permissions HasData
```

Snapshot uyumsuz kalirsa EF sonraki migration'da ayni permission'i tekrar eklemeye veya silmeye calisabilir.

Pratik kontrol:

```text
dotnet ef migrations add TestCheck ...
```

bos migration uretmeye calisiyorsa snapshot uyumu genelde iyidir. Alakasiz seed farklari cikiyorsa once onlar incelenmelidir. Gereksiz test migration commit edilmemelidir.

## 14. Migration'i DB'ye Uygula

Normal komut:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet ef database update --project src/FurpaMerkezApi.Infrastructure --startup-project src/FurpaMerkezApi.WebApi --context AuthDbContext
```

Build zaten alinmis ve restore/build problemi varsa:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet ef database update --no-build --project src/FurpaMerkezApi.Infrastructure --startup-project src/FurpaMerkezApi.WebApi --context AuthDbContext
```

Eger EF `PendingModelChangesWarning` ile bloklarsa:

```text
1. PermissionCatalog ve AuthSeedData farklarini kontrol et.
2. Snapshot ile model seed verileri uyumlu mu bak.
3. Alakasiz pending migration farklari varsa once onlari temizle.
4. Acil durumda idempotent SQL ile permission ve __EFMigrationsHistory kaydi elle islenebilir.
```

Acil SQL yontemi son care olmalidir. Normal kosulda migration repo icinde tutulmalidir.

## 15. Rollere Yetki Ver

Migration genelde sadece Administrator role'e yeni yetkiyi baglar.

Diger roller icin:

```text
Admin panelden ilgili role yeni permission verilir.
Ya da role-permission migration'i ayrica yazilir.
```

Role permission atama akisi:

```text
POST /api/roles/{roleId}/permissions
```

Bu servis mevcut role permission'larini silip gonderilen listeyi bastan yazar. Bu yuzden frontend veya client tum permission listesini gondermelidir; sadece yeni permission'i gonderirse rolun eski yetkileri kaybolabilir.

## 16. Kullanici Token'i Ne Zaman Guncellenir?

Kullanici login oldugunda:

```text
kullanici rolleri okunur
rollerin permission'lari okunur
her permission JWT icine permission claim olarak yazilir
```

Bu yuzden role yeni yetki verdikten sonra kullanicinin mevcut token'i hemen degismez.

Gerekli aksiyon:

```text
Kullanici cikis-giris yapmali.
Ya da frontend yeni token alacak bir refresh akisi kullaniyorsa token yenilenmeli.
```

Token yenilenmeden endpoint hala `403 Forbidden` donebilir. Bu normaldir.

## 17. Frontend Menu Gorunurlugu

Frontend tarafinda menu gorunurlugu kullanicinin permission listesine gore yapilmalidir.

Backend login/me cevaplarinda kullanicinin yetkileri ve permission tree bilgisi donebilir:

```text
permissions
permissionTree
```

Dogru frontend mantigi:

```text
Menuyu gostermek icin ilgili list permission kontrol edilir.
Detay butonu icin detail permission kontrol edilir.
Ekle butonu icin create permission kontrol edilir.
Duzenle butonu icin update permission kontrol edilir.
```

Ornek:

```text
kasa-islemleri.kasa-sayimlari.list   -> menu/list gorunur
kasa-islemleri.kasa-sayimlari.create -> yeni kayit butonu gorunur
kasa-islemleri.kasa-sayimlari.update -> duzenle butonu gorunur
```

Frontend menuyu gizlese bile gercek guvenlik backend `[Authorize]` kontroludur. UI kontrolu sadece kullanici deneyimi icindir.

## 18. Mevcut Yapiya Gore En Dogru Pratik

Bu proje icin tavsiye edilen karar:

```text
Yetki tanimi koddan gelsin.
Yetki atamasi DB'den gelsin.
Endpoint guvenligi policy ile yapilsin.
Frontend gorunurlugu permission listesine gore yapilsin.
```

DB-first permission mantigi bu projede ana model degildir. Yani admin panelden DB'ye yeni permission ekleyerek uygulamaya yeni modul kazandirmak beklenmemelidir.

DB-first sistem istenirse daha farkli bir mimari gerekir:

```text
modules tablosu
menus tablosu
permissions tablosu
dynamic authorization policy provider
custom authorization handler
frontend dynamic route/menu builder
```

Bu daha esnek ama daha karmasik bir yapidir. Mevcut API endpoint'leri kodda tanimli oldugu icin, code-first permission catalog bu proje icin daha guvenli ve bakimi daha kolaydir.

## 19. Yeni Menu Icin Tam Kontrol Listesi

Yeni menu eklerken hizli kontrol:

```text
[ ] Module code belirlendi
[ ] Menu code belirlendi
[ ] Action seti belirlendi: ListActions, ReadActions, CRUD, vb.
[ ] PermissionCatalog.cs icine menu eklendi
[ ] Controller route eklendi
[ ] Controller policy const'lari eklendi
[ ] Her endpoint dogru policy ile korundu
[ ] Application request/response/interface eklendi
[ ] Infrastructure use case/query/service eklendi
[ ] DI kaydi eklendi
[ ] Permission migration yazildi
[ ] Permission GUID'leri katalog/seed mantigiyla uyumlu
[ ] Administrator role permission baglantisi eklendi
[ ] AuthDbContextModelSnapshot kontrol edildi
[ ] dotnet build alindi
[ ] dotnet ef database update calistirildi
[ ] Admin disi roller gerekiyorsa yetki verildi
[ ] Kullanici cikis-giris yapti
[ ] Frontend menu/buton gorunurlugu permission'a baglandi
[ ] 401/403 davranisi test edildi
```

## 20. Sik Yapilan Hatalar

### Sadece DB'ye Permission Eklemek

Yanlis beklenti:

```text
DB'ye permission ekledim, modul otomatik olusmali.
```

Dogru:

```text
PermissionCatalog'a eklenmeli.
Controller endpoint kodda olmali.
Frontend menu/route tanimi olmali.
DB sadece yetki kaydini ve rol baglantisini tutar.
```

### Policy Code ile Catalog Code Farkli

Yanlis:

```text
Catalog: stok-islemleri.zayiat-fisleri.list
Policy:  stok-islemleri.zayiat-fisi.list
```

Bu durumda kullanicida yetki olsa bile endpoint acilmaz.

### Role Yetki Verildi Ama Kullanici Hala 403 Aliyor

Muhtemel neden:

```text
Kullanici eski JWT ile istek atiyor.
```

Cozum:

```text
Cikis-giris yaptir.
Token yenile.
```

### AssignPermissions Endpoint'ine Sadece Yeni Yetki Gondermek

`RoleService.AssignPermissionsAsync` role ait mevcut tum permission'lari silip gelen listeyi tekrar ekler.

Bu yuzden sadece yeni permission ID'si gonderilirse rolun eski yetkileri gider.

Dogru:

```text
Rolun sahip olmasi gereken tum permission ID listesi gonderilmeli.
```

### Rename Islemini Yeni Permission Gibi Yapmak

Permission code degistirmek migration acisindan rename'dir.

Dogru migration:

```text
Yeni permission'i ekle
Eski role-permission baglantilarini yeni permission'a tasi
Eski permission'i sil veya pasife al
Down metodunda tersini yap
```

## 21. Kisa Ozet

Bu islerin en kisa akli:

```text
PermissionCatalog  -> uygulamanin bildigi yetki kodlari
Migration          -> yetki kodlarini DB'ye tasir
Roles              -> yetkileri paketler
UserRoles          -> kullaniciya rol verir
JWT token          -> kullanicinin login anindaki yetkilerini tasir
[Authorize] policy -> endpoint'e giris kontrolu yapar
Frontend           -> permission'a gore menu/buton gosterir
```

En onemli kural:

```text
Yeni bir menu veya endpoint icin permission DB'den baslamaz.
Yeni yetki PermissionCatalog.cs icinden baslar.
```
