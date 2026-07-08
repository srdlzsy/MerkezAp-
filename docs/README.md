# FurpaMerkezApi Dokumantasyon

Bu klasor, `FurpaMerkezApi` projesindeki teknik ve operasyonel dokumanlarin
toplandigi ana yerdir. Koddaki ana uygulama `.NET 9` Web API olarak calisir ve
iki temel veritabani kullanir:

- `FurpaMerkezDb` (`PostgreSQL`): login, register, rol, yetki ve merkez uygulama verileri.
- `MikroDB_V16_FURPA_2024` (`SQL Server`): stok, cari, fatura, sevk, siparis ve operasyonel Mikro verileri.

## Hizli Baslangic

Repo kokunden:

```powershell
dotnet build FurpaMerkezApi.sln
dotnet run --project src/FurpaMerkezApi.WebApi --launch-profile http
```

Swagger:

```text
http://localhost:5228/swagger
```

## Nereden Baslamali?

Projeyi genel anlamak icin once su sirayla okunmasi onerilir:

1. [PROJE_GENEL_ISLEYISI.md](PROJE_GENEL_ISLEYISI.md)
2. [UI_API_DOKUMANI.md](UI_API_DOKUMANI.md)
3. [YENI_MENU_YETKI_MIGRATION_REHBERI.md](YENI_MENU_YETKI_MIGRATION_REHBERI.md)
4. [OPERASYON_HIZLI_MUDAHALE.md](OPERASYON_HIZLI_MUDAHALE.md)

## Dokuman Haritasi

### Genel Proje Ve Yetki

| Dokuman | Icerik |
| --- | --- |
| [PROJE_GENEL_ISLEYISI.md](PROJE_GENEL_ISLEYISI.md) | Projenin genel mimarisi, katmanlari, modul mantigi ve gelistirme standartlari. |
| [UI_API_DOKUMANI.md](UI_API_DOKUMANI.md) | Frontend/UI entegrasyonu icin API endpointleri ve response modelleri. |
| [YENI_MENU_YETKI_MIGRATION_REHBERI.md](YENI_MENU_YETKI_MIGRATION_REHBERI.md) | Yeni menu/API eklerken yetki katalogu, migration ve policy adimlari. |
| [PRODUCTION_HAZIRLIK.md](PRODUCTION_HAZIRLIK.md) | Production hazirlik ve canliya alma notlari. |

### Operasyon

| Dokuman | Icerik |
| --- | --- |
| [OPERASYON_HIZLI_MUDAHALE.md](OPERASYON_HIZLI_MUDAHALE.md) | Sistem yavasligi, ekran acilmama ve acil mudahale adimlari. |
| [OPERASYON_ISLEMLERI_DETAYLI_API_DOKUMANI.md](OPERASYON_ISLEMLERI_DETAYLI_API_DOKUMANI.md) | Operasyon modulu API davranislari. |
| [STOK_ANOMALI_MERKEZI.md](STOK_ANOMALI_MERKEZI.md) | Stok Anomali Merkezi tarama kurallari, API'leri, veri modeli ve kullanim akisi. |

### Fatura Ve UBL

| Dokuman | Icerik |
| --- | --- |
| [FATURA_GONDERIM_SISTEMI.md](FATURA_GONDERIM_SISTEMI.md) | Fatura gonderim, validate/send/retry, Uyumsoft, XSLT ve Mikro yazim akisi. |
| [UBL_FATURA_MANTIGI.md](UBL_FATURA_MANTIGI.md) | UBL fatura XML mantigi ve Mikro verilerinden beslenme sekli. |
| [MIKRO_MUHASEBE_AKIS_REHBERI.md](MIKRO_MUHASEBE_AKIS_REHBERI.md) | Mikro stok/cari/fatura muhasebe akis rehberi. |

### Mal Kabul, Siparis Ve Stok

| Dokuman | Icerik |
| --- | --- |
| [DEPO_MAL_KABUL_ISLEYIS.md](DEPO_MAL_KABUL_ISLEYIS.md) | Depo mal kabul isleyisi. |
| [FIRMA_MAL_KABUL_SENARYO.md](FIRMA_MAL_KABUL_SENARYO.md) | Firma mal kabul senaryolari ve Mikro yazim kurallari. |
| [DEPO_ONERILEN_SIPARIS.md](DEPO_ONERILEN_SIPARIS.md) | Depo onerilen siparis sorgu ve hesap mantigi. |
| [FIRMA_ONERILEN_SIPARIS.md](FIRMA_ONERILEN_SIPARIS.md) | Firma/tedarikci onerilen siparis mantigi. |

### Mikro Ve Entegrasyon

| Dokuman | Icerik |
| --- | --- |
| [MIKRO_PROSEDUR_ANALIZI.md](MIKRO_PROSEDUR_ANALIZI.md) | Mikro stored procedure analizleri. |
| [MIKRO_REST_API_GECIS_ANALIZI.md](MIKRO_REST_API_GECIS_ANALIZI.md) | Dogrudan Mikro DB davranislarindan Mikro REST API'ye gecis analizi. |
| [MIKRO_API_POSTMAN_DOKUMANI.md](MIKRO_API_POSTMAN_DOKUMANI.md) | Mikro API Postman collection dokumani. |
| [AXATA_ENTEGRASYON_ALTYAPISI.md](AXATA_ENTEGRASYON_ALTYAPISI.md) | AXATA entegrasyon altyapisi ve mevcut API davranisi. |

## Gelistirme Standardi

Yeni gelistirmeler genel olarak `module > menu > action` standardi ile ilerler.

Ornek:

```text
Application/
  Modules/
    FaturaIslemleri/
      FaturaGonderimi/
        ISendInvoiceSendingDocumentsUseCase.cs

Infrastructure/
  Modules/
    FaturaIslemleri/
      FaturaGonderimi/
        SendInvoiceSendingDocumentsUseCase.cs

WebApi/
  Controllers/
    Modules/
      FaturaIslemleri/
        FaturaGonderimi/
          FaturaGonderimiController.cs
```

Temel kurallar:

- Her menu kendi controller dosyasina sahiptir.
- Controller sadece HTTP, auth ve request/response baglantisini tasir.
- Is akisi `Application` kontratlari ve `Infrastructure` implementasyonlari uzerinden ilerler.
- Mikro sorgulari ve dis servis cagrilari `Infrastructure` tarafinda kalir.
- Route, permission code ve menu yapisi ayni isimlendirme mantigini izler.

## Secret Ve Config Kurali

- Repo icindeki `appsettings.json` ve `appsettings.Production.json` dosyalari secret template olarak kalmalidir.
- Gercek sifre, connection string, JWT secret ve entegrasyon kullanici bilgileri GitHub'a push edilmemelidir.
- Lokal makinede secret gerekiyorsa `src/FurpaMerkezApi.WebApi/appsettings.Local.json` kullanilir.
- `appsettings.Local.json` `.gitignore` icindedir.
- Canli sunucuda secret'lar publish sonrasi server tarafinda veya environment variable olarak tutulmalidir.

## GitHub'a Gondermeden Once

- `appsettings.Production.json` icinde bos veya placeholder secret oldugunu kontrol et.
- `appsettings.Local.json` dosyasinin staged olmadigini kontrol et.
- `git status` ile beklenmeyen dosya veya secret olmadigini dogrula.
- Dokuman eklediysen bu klasordeki linklerin calistigini kontrol et.
