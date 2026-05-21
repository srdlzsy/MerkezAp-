# Yeni Menu, Yetki ve Migration Rehberi

Bu rehber projede yeni bir ekran/menu eklerken API, permission ve DB migration tarafinda izlenecek temel yolu anlatir.

Ornek olarak:

```text
Module: mal-kabul-islemleri
Menu:   mal-kabul-farklari
Action: list
Policy: mal-kabul-islemleri.mal-kabul-farklari.list
```

## 1. Permission Catalog'a Menuyu Ekle

Yetkiler kod tarafinda `PermissionCatalog` uzerinden tanimlanir.

Dosya:

```text
src/FurpaMerkezApi.Application/Security/PermissionCatalog.cs
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

CRUD ekran olacaksa `ListActions` verme:

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

## 2. Controller'da Menu Kodunu ve Policy'yi Kullan

Controller `ModuleMenuControllerBase`'ten turetilirse endpoint kendi module/menu bilgisini de tasir.

Ornek:

```csharp
[ApiController]
[Route("api/mal-kabul-islemleri/mal-kabul-farklari")]
public sealed class MalKabulFarklariController(...)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "mal-kabul-islemleri";
    private const string ModuleName = "MalKabulIslemleri";
    private const string MenuCode = "mal-kabul-farklari";
    private const string MenuName = "MalKabulFarklari";
    private const string ListPolicy = "mal-kabul-islemleri.mal-kabul-farklari.list";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    public async Task<IActionResult> List(...)
    {
        ...
    }
}
```

Onemli:

- Route kebab-case olsun: `mal-kabul-farklari`
- Policy tam permission code ile ayni olsun.
- UI menusu `GET /api/auth/me` cevabindaki permission/module agacindan uretilir.

## 3. Application Katmanina Contract Ekle

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

## 4. Infrastructure Katmanina Implementasyon Ekle

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
    public async Task<IReadOnlyCollection<WarehouseReceivingDifferenceDto>> ExecuteAsync(...)
    {
        ...
    }
}
```

Okuma sorgularinda:

```csharp
mikroDbContext.STOK_HAREKETLERIs.AsNoTracking()
```

kullanmak iyi olur.

## 5. DI Kaydini Ekle

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

## 6. Permission DB'ye Nasil Duser?

Permission'in kodda tanimli olmasi tek basina canli DB'ye yetkiyi eklemez.

Bu projede iki yol var:

### Development Ortami

Development'ta `SynchronizePermissionCatalog` genelde aciktir. Uygulama acilisinda eksik permission'lari DB'ye ekleyebilir.

Ama buna guvenmek yerine migration yazmak daha temizdir.

### Production Ortami

Production ayarinda su deger kapali olabilir:

```json
"StartupTasks": {
  "SynchronizePermissionCatalog": false
}
```

Bu durumda yeni permission canli DB'ye ancak migration ile gider.

## 7. Permission Migration Yaz

Sadece yeni permission ekliyorsan en temiz migration sunlari yapar:

- `app_permissions` tablosuna permission ekler
- `app_role_permissions` tablosunda Administrator role'une baglar
- Down metodunda ikisini geri siler

Ornek migration:

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

Not:

- Permission ID deterministic olursa daha iyi olur.
- Bu projede `AuthSeedData` bilinmeyen permission kodlari icin `permission:{code}` metninden MD5 ile GUID uretir.
- Elle migration yaziyorsan bu GUID'in katalogdaki GUID ile ayni olmasina dikkat et.

## 8. Snapshot'i Guncelle

Migration elle yazildiysa `AuthDbContextModelSnapshot.cs` icine de seed kaydi eklenmelidir.

Iki yere eklenir:

```text
app_permissions HasData
app_role_permissions HasData
```

Bunu yapmazsan EF sonraki migration'da ayni permission'i tekrar pending change gibi gormeye calisabilir.

## 9. Migration'i DB'ye Uygula

Normal komut:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet ef database update --project src/FurpaMerkezApi.Infrastructure --startup-project src/FurpaMerkezApi.WebApi --context AuthDbContext
```

Eger build zaten alinmis ve NuGet/restore problemi varsa:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet ef database update --no-build --project src/FurpaMerkezApi.Infrastructure --startup-project src/FurpaMerkezApi.WebApi --context AuthDbContext
```

Eger EF `PendingModelChangesWarning` ile bloklarsa, repo'da alakasiz pending model degisiklikleri olabilir. Bu durumda:

1. Once alakasiz migration/snapshot farklarini kontrol et.
2. Mumkunse pending farklari temizle.
3. Acil durumda idempotent SQL ile permission'i ve `__EFMigrationsHistory` kaydini elle isleyebilirsin.

## 10. Rollere Yetki Ver

Migration sadece Administrator role'une yetki baglar.

Baska rollerin menuyu gormesi gerekiyorsa:

- Admin panelden ilgili role `mal-kabul-islemleri.mal-kabul-farklari.list` yetkisini ver.
- Ya da role-permission seed/migration'i ayrica yaz.

Kullanici token'i eskiyse cikis-giris yaptirmak gerekebilir. Cunku token icindeki permission claim'leri login aninda olusur.

## 11. Kontrol Listesi

Yeni menu eklerken hizli kontrol:

```text
[ ] PermissionCatalog'a menu/action eklendi
[ ] Controller route ve policy eklendi
[ ] Application request/response/interface eklendi
[ ] Infrastructure use case/query eklendi
[ ] DI kaydi eklendi
[ ] Permission migration yazildi
[ ] AuthDbContextModelSnapshot guncellendi
[ ] dotnet build alindi
[ ] dotnet ef database update calistirildi
[ ] Admin disi roller gerekiyorsa yetki verildi
[ ] Kullanici yeniden login oldu
```

## 12. Bu Isin Mantigi

Kisa ozet:

```text
PermissionCatalog  -> uygulamanin bildigi yetki kodlari
Migration          -> yetki kodlarini DB'ye tasir
RolePermissions    -> hangi rol hangi yetkiye sahip
JWT token          -> kullanicinin login anindaki yetkileri
[Authorize] policy -> endpoint'e giris kontrolu
GET /api/auth/me   -> UI menu agacini ve gorunurlugu besler
```

