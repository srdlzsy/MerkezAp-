# FurpaMerkezApi

`.NET 9` ile kurulan bu API, Furpa merkez operasyonlari icin iki veritabaniyla calisir:

- `FurpaMerkezDb` (`PostgreSQL`): login, register, rol ve yetki yonetimi
- `MikroDB_V16_FURPA_2024` (`SQL Server`): operasyonel is verileri

## Katmanlar

- `Domain`: auth ve yetki entity'leri
- `Application`: servis abstraction'lari, DTO'lar ve permission katalogu
- `Infrastructure`: `AuthDbContext`, `MikroDbContext`, auth servisleri ve Mikro sorgulari
- `WebApi`: controller'lar, JWT auth, Swagger ve middleware

## Yetki Yapisi

Yetkiler hiyerarsik olarak tutulur:

- `Module`
- `Menu`
- `Action`

Ornek:

- `siparis-islemleri.alinan-depo-siparisleri.list`
- `siparis-islemleri.verilen-depo-siparisleri.update`
- `kullanici-islemleri.users.manage`

Frontend, `GET /api/auth/me` ve `GET /api/permissions/catalog` cevaplarindan menu agacini uretebilir.

## Mevcut Moduller

- `SiparisIslemleri`
- `SevkIslemleri`
- `MalKabulIslemleri`
- `IadeIslemleri`
- `KullaniciIslemleri`
- `KasaIslemleri`

## Gelistirme Standardi

Yeni gelistirmeler `module > menu > action` standardi ile ilerler.

Ornek klasor yapisi:

```text
Application/
  Modules/
    SiparisIslemleri/
      AlinanDepoSiparisleri/
        List/
          IListReceivedWarehouseOrdersUseCase.cs
      Common/
        WarehouseOrderListRequest.cs
        WarehouseOrderListItemDto.cs

Infrastructure/
  Modules/
    SiparisIslemleri/
      AlinanDepoSiparisleri/
        List/
          ListReceivedWarehouseOrdersUseCase.cs
      Common/
        WarehouseOrderListQueryExecutor.cs

WebApi/
  Controllers/
    Modules/
      SiparisIslemleri/
        AlinanDepoSiparisleri/
          AlinanDepoSiparisleriController.cs
```

Kurallar:

- her menu kendi controller dosyasina sahiptir
- her action kendi use-case klasorunden gelistirilir
- `WebApi` sadece HTTP ve auth/permission katmanidir
- is kurali ve use-case akisi `Application` abstraction'lari uzerinden ilerler
- Mikro sorgulari `Infrastructure` tarafinda kalir
- route, permission code ve menu yapisi birebir ayni mantigi izler

## Calistirma

```powershell
dotnet build FurpaMerkezApi.sln
dotnet run --project src/FurpaMerkezApi.WebApi --launch-profile http
```

Swagger:

```text
http://localhost:5228/swagger
```

UI entegrasyon dokumani:

```text
UI_API_DOKUMANI.md
```

Auth/migration notu:

- `AuthDbContext` icin permission veya seed degisikligi yapilirsa yeni EF Core migration alinmalidir.
- Uygulama acilisinda auth migration'lari otomatik uygulanir; model ile snapshot uyusmazsa `PendingModelChangesWarning` hatasi alinabilir.

## Secret ve Config Kurali

- Repo icindeki `appsettings.json` ve `appsettings.Production.json` dosyalari secret template olarak kalmalidir.
- Gercek sifre, connection string, JWT secret ve entegrasyon kullanici bilgileri GitHub'a push edilmemelidir.
- Lokal makinede secret gerekiyorsa `src/FurpaMerkezApi.WebApi/appsettings.Local.json` kullanilir.
- `appsettings.Local.json` `.gitignore` icindedir; normal `git add .` ile repoya gitmez.
- Canli sunucuda secret'lar ya publish sonrasi server'daki `appsettings.Production.json` icine yazilmali ya da environment variable olarak verilmelidir.
- Bir secret yanlislikla commit edildiyse sadece dosyadan silmek yetmez; secret rotate edilmeli ve gerekirse git history temizlenmelidir.

## GitHub'a Gondermeden Once

- `appsettings.Production.json` icinde bos veya placeholder secret oldugunu kontrol et
- `appsettings.Local.json` dosyasinin staged olmadigini kontrol et
- Gerekirse `git status` ile son kez dogrula
- Server'a ozel degerleri repo icine degil, server tarafina yaz

## Ornek Endpoint'ler

- `POST /api/auth/login`
- `GET /api/auth/me`
- `GET /api/permissions/catalog`
- `GET /api/siparis-islemleri/alinan-depo-siparisleri?WarehouseNo=110&StartDate=2026-04-01&EndDate=2026-04-10`
- `GET /api/siparis-islemleri/verilen-depo-siparisleri?WarehouseNo=110&StartDate=2026-04-01&EndDate=2026-04-10`
- `GET /api/siparis-islemleri/verilen-depo-siparisleri/D110/1915?warehouseNo=110`
- `GET /api/siparis-islemleri/verilen-depo-siparisleri/key/MTEwfEQxMTB8MTkxNQ`
