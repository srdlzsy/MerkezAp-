# FurpaMerkezApi Gelistirici Devam Rehberi

Bu dokuman projeyi alip gelistirmeye devam edecek kisinin gunluk olarak neye
bakacagini, hangi komutu ne zaman calistiracagini ve sifirdan yeni modul
eklerken hangi sirayi izleyecegini anlatir.

Ana hedef:

```text
dogru DB'ye baktigindan emin ol
dogru katmanda kod yaz
permission ve route adlarini tutarli ver
migration'i kontrollu uret
UI dokumanini guncel tut
build/test/pending migration kontrolunu bitirmeden isi kapatma
```

## Ilk Bakilacak Dosyalar

Projeye baslarken su dosyalari bu sirayla oku:

```text
docs/README.md
docs/PROJE_GENEL_ISLEYISI.md
docs/YENI_MENU_YETKI_MIGRATION_REHBERI.md
docs/UI_API_DOKUMANI.md
```

Modul ozelinde calisiyorsan ilgili controller, Application DTO/interface,
Infrastructure service ve varsa test dosyasini birlikte oku.

Ornek:

```text
src/FurpaMerkezApi.WebApi/Controllers/Modules/KasaIslemleri/ManavMalKabulVeEtiket
src/FurpaMerkezApi.Application/Modules/KasaIslemleri/ManavMalKabulVeEtiket
src/FurpaMerkezApi.Infrastructure/Modules/KasaIslemleri/ManavMalKabulVeEtiket
tests/FurpaMerkezApi.Infrastructure.Tests/Modules/KasaIslemleri/ManavMalKabulVeEtiket
```

## Proje Klasor Mantigi

Ana katmanlar:

```text
src/FurpaMerkezApi.Domain
src/FurpaMerkezApi.Application
src/FurpaMerkezApi.Infrastructure
src/FurpaMerkezApi.WebApi
tests
docs
```

Pratik anlamlari:

```text
Domain         -> entity ve temel model
Application    -> DTO, request/response, interface, permission katalogu
Infrastructure -> DB sorgusu, Mikro/Furpa/Auth yazma-okuma, servis implementasyonu
WebApi         -> controller, route, auth policy, HTTP modeli
tests          -> davranis ve regression testi
docs           -> UI, operasyon, migration ve modul rehberleri
```

Bir endpoint istegi genelde su yoldan gecer:

```text
Frontend
  -> WebApi Controller
  -> Application interface/request/DTO
  -> Infrastructure service/use case
  -> DbContext / raw SQL / external client
  -> DTO response
  -> HTTP response
```

## Gunluk Baslangic Komutlari

Repo kokune gec:

```powershell
cd "D:\PROJECTS\FURPA(Serdal OZSOY)\FurpaMerkezApı"
```

Bulundugun yeri kontrol et:

```powershell
Get-Location
```

Git durumunu kontrol et:

```powershell
git status --short
```

Neden?

```text
Calismaya baslamadan once hangi dosyalar degismis gormek gerekir.
Kirli worktree varsa bunlar kullanici degisikligi olabilir.
Ilgisiz degisiklikleri geri alma; sadece kendi isine dokun.
```

Hizli dosya arama:

```powershell
rg "KasaSayimlari"
rg "manav-mal-kabul-etiket"
rg "Authorize\(Policy"
```

Neden?

```text
rg repo icinde en hizli arama yoludur.
Yeni is yapmadan once mevcut isimlendirme ve pattern'i bulmak icin kullanilir.
```

Dosya listesi:

```powershell
rg --files
rg --files src/FurpaMerkezApi.WebApi
rg --files docs
```

## Build, Test ve Run

Restore gerekiyorsa:

```powershell
dotnet restore .\FurpaMerkezApi.sln
```

Ne zaman?

```text
Paketler ilk kez indirilecekse.
csproj veya NuGet paketleri degistiyse.
Build "package not found" gibi hata verdiyse.
```

Build:

```powershell
dotnet build .\FurpaMerkezApi.sln --no-restore
```

Ne zaman?

```text
Kod degistirdikten sonra.
Migration dosyasini elle duzelttikten sonra.
dotnet ef komutunu --no-build ile calistirmadan once.
```

Test:

```powershell
dotnet test .\FurpaMerkezApi.sln --no-build
```

Ne zaman?

```text
Build basarili olduktan sonra.
Ortak servis, permission, controller veya DB mapping degistirdikten sonra.
Docs-only degisiklikte zorunlu degil ama kod degisikligi varsa calistir.
```

API'yi calistirma:

```powershell
dotnet run --project .\src\FurpaMerkezApi.WebApi
```

WebApi klasorundeysen:

```powershell
cd .\src\FurpaMerkezApi.WebApi
dotnet run
```

Swagger adresi launch profile'a gore degisebilir. Varsayilan lokal adres icin:

```text
http://localhost:5228/swagger
```

Port doluysa:

```powershell
netstat -ano | findstr :5228
Get-Process -Id <PID>
```

Gerekirse ilgili sureci kapat:

```powershell
Stop-Process -Id <PID>
```

## Config ve DB Kontrolu

Onemli config dosyalari:

```text
src/FurpaMerkezApi.WebApi/appsettings.json
src/FurpaMerkezApi.WebApi/appsettings.Production.json
src/FurpaMerkezApi.WebApi/appsettings.Local.json
```

Kurallar:

```text
appsettings.Local.json local secret icindir ve commit edilmemelidir.
appsettings.Production.json gercek secret tasimamali; template veya deploy sonrasi server config olarak kalmalidir.
Canlida hangi DB'ye baglandigini appsettings ve process command line ile mutlaka kontrol et.
```

Connection string arama:

```powershell
rg -n "ConnectionStrings|AuthConnection|MikroConnection|FurpaConnection" .\src\FurpaMerkezApi.WebApi
```

Production dosyasini kontrol et:

```powershell
Get-Content .\src\FurpaMerkezApi.WebApi\appsettings.Production.json
```

Neden?

```text
API baska DB'ye bakiyorsa UI'da gordugun veri ile senin sorguladigin veri farkli cikar.
Silindi sandigin kayit baska server/database uzerinden gelmeye devam edebilir.
```

Canli servis hangi klasorden calisiyor kontrolu:

```powershell
Get-CimInstance Win32_Process |
  Where-Object { $_.CommandLine -like "*FurpaMerkezApi*" } |
  Format-List ProcessId,ExecutablePath,CommandLine
```

Neden?

```text
Deploy ettigini sandigin kod ile calisan servis farkli klasorden kalkmis olabilir.
CommandLine icindeki path ve environment appsettings secimini netlestirir.
```

## Permission Sistemi

Ana dosya:

```text
src/FurpaMerkezApi.Application/Security/PermissionCatalog.cs
```

Permission format:

```text
{moduleCode}.{menuCode}.{actionCode}
```

Ornek:

```text
kasa-islemleri.manav-mal-kabul-etiket.page
kasa-islemleri.manav-mal-kabul-etiket.list
kasa-islemleri.manav-mal-kabul-etiket.transfer
kasa-islemleri.manav-mal-kabul-etiket.all-warehouses
```

Action anlamlari:

```text
page           -> UI route/menu acma
manage         -> yonetim ekranini acma
list           -> liste/veri cekme
detail         -> detay/veri inceleme
create         -> yeni kayit
update         -> guncelleme
delete         -> silme
transfer       -> aktarim, Mikro yazma, ozel operasyon
all-warehouses -> tum depo/sube kapsaminda islem
```

Controller policy ornegi:

```csharp
[Authorize(Policy = "kasa-islemleri.manav-mal-kabul-etiket.list")]
```

UI icin kural:

```text
Sol menu ve route guard icin page/manage kullan.
Buton ve endpoint icin list/detail/create/update/delete/transfer kullan.
Depo secici icin all-warehouses kullan.
```

## Yeni Modul Ekleme Sirasi

Ornek yeni modul:

```text
moduleCode = kasa-islemleri
menuCode   = ornek-modul
menuName   = OrnekModul
route      = /api/kasa-islemleri/ornek-modul
```

1. Application klasoru olustur:

```text
src/FurpaMerkezApi.Application/Modules/KasaIslemleri/OrnekModul
```

Icine genelde sunlar gelir:

```text
OrnekModulDtos.cs
IOrnekModulService.cs
```

2. Infrastructure implementasyonu olustur:

```text
src/FurpaMerkezApi.Infrastructure/Modules/KasaIslemleri/OrnekModul/OrnekModulService.cs
```

3. Controller olustur:

```text
src/FurpaMerkezApi.WebApi/Controllers/Modules/KasaIslemleri/OrnekModul/OrnekModulController.cs
```

4. DI kaydi ekle:

```text
src/FurpaMerkezApi.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs
```

Ornek:

```csharp
services.AddScoped<IOrnekModulService, OrnekModulService>();
```

5. PermissionCatalog'a ekle:

```csharp
..CreateMenuPermissions("kasa-islemleri", "KasaIslemleri", "ornek-modul", "OrnekModul", ReadUpdateDeleteActions),
```

6. Migration uret.

7. UI dokumanini guncelle:

```text
docs/UI_API_DOKUMANI.md
```

8. Gerekiyorsa ozel rehber ekle:

```text
docs/ORNEK_MODUL_REHBERI.md
```

9. Test ekle veya mevcut testleri guncelle.

## Auth Migration Komutlari

PermissionCatalog veya Auth entity/model degistiyse migration gerekir.

Migration ekle:

```powershell
dotnet ef migrations add AddOrRenamePermission `
  --project .\src\FurpaMerkezApi.Infrastructure `
  --startup-project .\src\FurpaMerkezApi.WebApi `
  --context AuthDbContext
```

Neden?

```text
PermissionCatalog seed verisi AuthDbContext snapshot'ina yansir.
Migration olmadan dotnet run sirasinda PendingModelChangesWarning hatasi alinabilir.
```

Migration'i geri al:

```powershell
dotnet ef migrations remove `
  --project .\src\FurpaMerkezApi.Infrastructure `
  --startup-project .\src\FurpaMerkezApi.WebApi `
  --context AuthDbContext
```

Ne zaman?

```text
Migration yanlis uretildiyse.
Snapshot istemedigin degisiklikler aldiysa.
Migration dosyasi henuz DB'ye uygulanmadiysa.
```

Zorla geri al:

```powershell
dotnet ef migrations remove --force `
  --project .\src\FurpaMerkezApi.Infrastructure `
  --startup-project .\src\FurpaMerkezApi.WebApi `
  --context AuthDbContext
```

Pending model kontrolu:

```powershell
dotnet ef migrations has-pending-model-changes `
  --project .\src\FurpaMerkezApi.Infrastructure `
  --startup-project .\src\FurpaMerkezApi.WebApi `
  --context AuthDbContext
```

Beklenen sonuc:

```text
No changes have been made to the model since the last migration.
```

DB'ye migration uygula:

```powershell
dotnet ef database update `
  --project .\src\FurpaMerkezApi.Infrastructure `
  --startup-project .\src\FurpaMerkezApi.WebApi `
  --context AuthDbContext
```

Onemli `--no-build` notu:

```text
Migration dosyasini elle duzelttiysen once dotnet build calistir.
Sonra dotnet ef ... --no-build kullan.
Aksi halde eski derlenmis DLL calisir ve duzelttigin migration degil eski hali uygulanir.
```

Guvenli sira:

```powershell
dotnet build .\FurpaMerkezApi.sln --no-restore
dotnet ef migrations has-pending-model-changes --project .\src\FurpaMerkezApi.Infrastructure --startup-project .\src\FurpaMerkezApi.WebApi --context AuthDbContext --no-build
dotnet ef database update --project .\src\FurpaMerkezApi.Infrastructure --startup-project .\src\FurpaMerkezApi.WebApi --context AuthDbContext --no-build
```

## Migration Yazarken Dikkat

EF migration'i otomatik urettikten sonra mutlaka dosyayi incele:

```powershell
Get-ChildItem .\src\FurpaMerkezApi.Infrastructure\Migrations |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 5 Name,LastWriteTime
```

Yeni migration'i ac:

```powershell
Get-Content .\src\FurpaMerkezApi.Infrastructure\Migrations\<MigrationDosyasi>.cs
```

Kontrol et:

```text
Gereksiz DeleteData var mi?
Role permission baglantilari kopuyor mu?
Eski yetki yeni yetkiye tasinacaksa app_role_permissions korunuyor mu?
Raw SQL aktif DB provider'a uygun mu?
```

Provider notu:

```text
Auth DB hangi provider ile calisiyorsa SQL syntax'i ona gore yazilmalidir.
SQL Server icin uniqueidentifier: CAST('guid' AS uniqueidentifier)
PostgreSQL icin uuid cast: 'guid'::uuid
Bu iki syntax birbirinin yerine kullanilmaz.
```

SQL Server ornegi:

```sql
CAST('119f2412-b309-b962-fbec-6733704c5818' AS uniqueidentifier)
```

PostgreSQL ornegi:

```sql
'119f2412-b309-b962-fbec-6733704c5818'::uuid
```

Bu projede migration logu SQL Server komutlari uretiyorsa raw SQL de SQL Server
uyumlu yazilmalidir.

## Mikro Yazma Gelistirmesi

Mikro tarafina yazan islerde daha yavas ama daha emin ilerle.

Kontrol listesi:

```text
Canli eski sistem ayni isi hangi tablolara yaziyor?
Belge tip/cins/evraktip alanlari nedir?
Giris depo ve cikis depo alanlari dogru mu?
Evrak seri/sira nasil uretiliyor?
Duplicate kontrolu var mi?
Transaction var mi?
Hata olursa kismi kayit kaliyor mu?
Yetki ve depo kapsami kontrol edildi mi?
UI ayni istegi tekrar basarsa ne olur?
```

Mikro sorgusu aramak:

```powershell
rg -n "STOK_HAREKETLERI|CARI_HESAP_HAREKETLERI|EVRAK_ACIKLAMALARI" .\src .\docs
```

Canli kayit formati ararken:

```sql
SELECT TOP 20 *
FROM STOK_HAREKETLERI
WHERE sth_tarih >= '2026-01-01'
ORDER BY sth_create_date DESC;
```

Not:

```text
Bu sorguyu dogrudan prod DB'de calistirirken filtreyi dar tut.
TOP, tarih, seri, depo veya cari filtresi olmadan buyuk Mikro tablolarina yuklenme.
```

## UI Dokumani Guncelleme

Her yeni endpoint veya response alaninda ana UI dokumani guncellenir:

```text
docs/UI_API_DOKUMANI.md
```

Ek olarak modul buyukse ozel dokuman ac:

```text
docs/MANAV_MAL_KABUL_VE_ETIKET_API.md
docs/KASA_SAYIMI_ICMAL_YETKI_Z_RAPORU_REHBERI.md
```

UI dokumaninda bulunmasi gerekenler:

```text
route
permission
request modeli
response modeli
status kodlari
depo yetkisi davranisi
ornek request/response
UI'nin hangi alani ne icin kullanacagi
eski route/alias varsa notu
```

Route degistiğinde arama:

```powershell
rg -n "eski-route|EskiModulAdi|oldPermission" .\src .\docs .\tests
```

## Test Yazma Mantigi

Dar kapsamli degisiklik:

```text
Tek servis veya tek helper degisti -> ilgili unit test yeterli olabilir.
```

Orta riskli degisiklik:

```text
Controller/policy/request response degisti -> WebApi test ekle veya guncelle.
```

Yuksek riskli degisiklik:

```text
Mikro yazma, kasa sayimi, permission, migration veya depo yetkisi degisti -> daha genis test ve manuel DB kontrol gerekir.
```

Belirli test projesini calistirma:

```powershell
dotnet test .\tests\FurpaMerkezApi.Infrastructure.Tests\FurpaMerkezApi.Infrastructure.Tests.csproj --no-build
dotnet test .\tests\FurpaMerkezApi.WebApi.Tests\FurpaMerkezApi.WebApi.Tests.csproj --no-build
```

Belirli test adini filtreleme:

```powershell
dotnet test .\FurpaMerkezApi.sln --no-build --filter "FullyQualifiedName~KasaSayimlari"
```

## Sik Hatalar ve Cozumleri

### PendingModelChangesWarning

Hata:

```text
The model for context 'AuthDbContext' has pending changes.
```

Sebep:

```text
PermissionCatalog veya Auth model degisti ama migration eklenmedi.
```

Cozum:

```powershell
dotnet ef migrations add MigrationAdi --project .\src\FurpaMerkezApi.Infrastructure --startup-project .\src\FurpaMerkezApi.WebApi --context AuthDbContext
dotnet build .\FurpaMerkezApi.sln --no-restore
dotnet ef migrations has-pending-model-changes --project .\src\FurpaMerkezApi.Infrastructure --startup-project .\src\FurpaMerkezApi.WebApi --context AuthDbContext --no-build
```

### Incorrect syntax near '::'

Hata:

```text
Incorrect syntax near '::'
```

Sebep:

```text
SQL Server'a PostgreSQL cast syntax'i gonderildi.
```

Cozum:

```text
'guid'::uuid yerine CAST('guid' AS uniqueidentifier) kullan.
Migration dosyasini duzelttikten sonra mutlaka dotnet build calistir.
```

### API baska veri donduruyor

Sebep adaylari:

```text
Yanlis DB'ye bakiyorsun.
Servis baska klasorden calisiyor.
Production config farkli.
Eski deploy restart olmadi.
UI cache veya eski endpoint kullaniyor.
```

Kontrol:

```powershell
Get-CimInstance Win32_Process |
  Where-Object { $_.CommandLine -like "*FurpaMerkezApi*" } |
  Format-List ProcessId,ExecutablePath,CommandLine
```

## Git ve Commit

Durum kontrolu:

```powershell
git status --short
git diff --stat
```

Detay diff:

```powershell
git diff -- src/FurpaMerkezApi.Application/Security/PermissionCatalog.cs
git diff -- docs/UI_API_DOKUMANI.md
```

Commit oncesi standart kontrol:

```powershell
dotnet build .\FurpaMerkezApi.sln --no-restore
dotnet test .\FurpaMerkezApi.sln --no-build
git status --short
```

Stage:

```powershell
git add -A
```

Commit:

```powershell
git commit -m "kisa ve net commit mesaji"
```

Commit mesaji ornekleri:

```text
add kasa sayimi update delete permissions
rename etiket basim to manav mal kabul ve etiket
add manav mikro goods receipt transfer
fix cash summary detail payment categories
```

Secret kontrolu:

```powershell
git diff --cached -- src/FurpaMerkezApi.WebApi/appsettings.Production.json
git diff --cached --name-only
```

Kural:

```text
Gercek sifre, connection string, token, API key ve musteriye ozel secret commit edilmez.
```

## Yeni Modul Mini Checklist

```text
[ ] Mevcut benzer modul incelendi
[ ] Module/menu/route/permission isimleri netlesti
[ ] Application DTO/interface eklendi
[ ] Infrastructure service eklendi
[ ] Controller eklendi
[ ] DI kaydi eklendi
[ ] PermissionCatalog eklendi
[ ] Auth migration uretildi
[ ] Migration diff'i incelendi
[ ] Pending model kontrolu temiz
[ ] UI_API_DOKUMANI.md guncellendi
[ ] Gerekirse ozel docs/*.md eklendi
[ ] Test eklendi veya mevcut test guncellendi
[ ] dotnet build basarili
[ ] dotnet test basarili
[ ] git diff/stat kontrol edildi
```

## En Kisa Usta Akisi

Bir isi bitirirken su akisi kullan:

```powershell
git status --short
dotnet build .\FurpaMerkezApi.sln --no-restore
dotnet test .\FurpaMerkezApi.sln --no-build
dotnet ef migrations has-pending-model-changes --project .\src\FurpaMerkezApi.Infrastructure --startup-project .\src\FurpaMerkezApi.WebApi --context AuthDbContext --no-build
git diff --stat
```

Eger hepsi temizse:

```text
Kod calisir.
Migration unutulmamistir.
Testler gecmistir.
Dokuman diff'i gorulmustur.
Commit icin hazirdir.
```
