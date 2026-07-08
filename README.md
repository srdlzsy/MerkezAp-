# FurpaMerkezApi

Furpa merkez operasyonlari icin gelistirilen `.NET 9` Web API projesidir. Sistem
kimlik/yetki ve merkez uygulama verileri icin `FurpaMerkezDb` veritabanini,
operasyonel stok, cari, fatura, sevk ve siparis verileri icin Mikro SQL Server
veritabanini kullanir.

## Hizli Baslangic

```powershell
dotnet build FurpaMerkezApi.sln
dotnet run --project src/FurpaMerkezApi.WebApi --launch-profile http
```

Swagger:

```text
http://localhost:5228/swagger
```

## Dokumantasyon

Tum teknik ve operasyonel dokumanlar [docs](docs/README.md) klasorundedir.

Ilk bakilacak dosyalar:

- [docs/PROJE_GENEL_ISLEYISI.md](docs/PROJE_GENEL_ISLEYISI.md)
- [docs/UI_API_DOKUMANI.md](docs/UI_API_DOKUMANI.md)
- [docs/YENI_MENU_YETKI_MIGRATION_REHBERI.md](docs/YENI_MENU_YETKI_MIGRATION_REHBERI.md)
- [docs/OPERASYON_HIZLI_MUDAHALE.md](docs/OPERASYON_HIZLI_MUDAHALE.md)

## Katmanlar

- `Domain`: entity ve domain modelleri.
- `Application`: use-case kontratlari, DTO'lar, servis abstraction'lari ve permission katalogu.
- `Infrastructure`: EF DbContext'leri, Mikro sorgulari, dis servis entegrasyonlari ve use-case implementasyonlari.
- `WebApi`: controller'lar, auth, Swagger ve HTTP middleware.

## Gelistirme Notlari

- Yeni moduller `module > menu > action` standardina gore eklenir.
- Route, permission code ve menu yapisi ayni isimlendirme mantigini izler.
- Mikro sorgulari ve dis servis cagrilari `Infrastructure` tarafinda kalir.
- Secret, connection string, JWT key ve entegrasyon sifreleri repoya yazilmaz.
- Lokal secret ihtiyaci icin `src/FurpaMerkezApi.WebApi/appsettings.Local.json` kullanilir.

## GitHub'a Gondermeden Once

```powershell
git status
dotnet build FurpaMerkezApi.sln
```

`appsettings.Local.json` veya gercek secret iceren herhangi bir dosyanin staged
olmadigini kontrol et.
