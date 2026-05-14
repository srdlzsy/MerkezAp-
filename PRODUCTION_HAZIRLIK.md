# Production Hazirlik

## Zorunlu ayarlar

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__AuthConnection`
- `ConnectionStrings__FurpaConnection`
- `ConnectionStrings__MikroConnection`
- `Jwt__SecretKey`
- Gerekliyse `ConnectionStrings__MikroWriteConnection`
- Gerekliyse `ConnectionStrings__ShopigoCiroConnection`
- Gerekliyse `EDespatch__Username`, `EDespatch__Password`
- Gerekliyse `EInvoice__Username`, `EInvoice__Password`
- Gerekliyse `AxataSynchronization__Username`, `AxataSynchronization__Password`
- Gerekliyse `AxataSynchronization__MainEndpointUrl`, `AxataSynchronization__ExtendedEndpointUrl`

## Production davranisi

- Swagger production'da varsayilan olarak kapali gelir: `Hosting__EnableSwagger=false`
- Root endpoint production'da detay gizler: `Hosting__ExposeDiagnosticsOnRoot=false`
- HTTPS redirect production'da varsayilan olarak acik gelir: `Hosting__EnforceHttps=true`
- Startup veritabani yazma isleri production'da varsayilan olarak kapali gelir:
- `StartupTasks__ApplyAuthMigrations=false`
- `StartupTasks__SynchronizePermissionCatalog=false`
- `StartupTasks__SynchronizeWarehouseUsers=false`
- Readiness endpoint artik temel veritabani bagimliliklarini gercekten test eder

## Acilista acmak istersen

- Auth migration icin: `StartupTasks__ApplyAuthMigrations=true`
- Yetki katalok senkronu icin: `StartupTasks__SynchronizePermissionCatalog=true`
- Depo kullanici otomatik olusturma icin: `StartupTasks__SynchronizeWarehouseUsers=true`

## CORS

- UI domainlerini `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1` ... ile ver
- Ornek:
- `Cors__AllowedOrigins__0=https://panel.firma.com`
- `Cors__AllowedOrigins__1=https://mobile.firma.com`

## Data protection

- Kalici key klasoru tanimla:
- `DataProtection__KeysPath=D:\\Furpa\\DataProtectionKeys`
- Birden fazla instance varsa ortak ve kalici klasor kullan

## Reverse proxy

- IIS, Nginx, YARP ya da load balancer arkasinda calisacaksa forwarded headers destegi acik gelir
- Varsayilan production ayari:
- `ReverseProxy__Enabled=true`
- `ReverseProxy__TrustAllNetworks=true`
- Daha siki guvenlik icin bu ayari kapatip sadece proxy IP'lerini ver:
- `ReverseProxy__TrustAllNetworks=false`
- `ReverseProxy__KnownProxies__0=10.0.0.10`

## HTTPS

- Uygulama reverse proxy arkasinda calisacaksa TLS'i proxy'de sonlandir
- Uygulama kendi icinde redirect yapacaksa `Hosting__EnforceHttps=true`
- HSTS production'da varsayilan acik: `Hosting__UseHsts=true`

## Saglik endpointleri

- `GET /health/live`
- `GET /health/ready`
- `ready` auth, furpa, mikro read/write ve varsa shopigo baglantisini test eder

## Loglama

- Uygulama console log yaninda dosya log da yazar
- Varsayilan klasor: `logs`
- Genel log dosyasi:
- `logs\\YYYY-MM\\application-YYYY-MM-DD.log`
- Hata log dosyasi:
- `logs\\YYYY-MM\\errors-YYYY-MM-DD.log`
- Her response icinde `X-Correlation-Id` header'i doner
- ProblemDetails cevaplarinda `correlationId` alani da olur

## Log ayarlari

- `Logging__File__Enabled=true`
- `Logging__File__BasePath=D:\\Furpa\\Logs`
- `Logging__File__MinimumLevel=Information`
- `Logging__File__ErrorFileMinimumLevel=Error`

## Local override

- Repo icine commit etmeden yerel override kullanmak icin:
- `src/FurpaMerkezApi.WebApi/appsettings.Local.json`
- Bu dosya `.gitignore` icinde
- Publish output'una kopyalanmaz, yani local secret dosyasi deploy paketine girmez
