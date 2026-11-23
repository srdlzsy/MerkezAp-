# FurpaMerkezApi Dokuman Haritasi

Bu klasorde yalniz guncel sozlesmeler, teknik referanslar, modul ozetleri,
operasyon rehberleri ve kontrollu veri duzeltme kayitlari tutulur. Ayni isi
yapan parcali index dosyalari kaldirilmistir.

## Ana Kaynaklar

| Kaynak | Amac |
|---|---|
| `API_SOZLESMESI.json` | Uygulamanin makine tarafindan okunabilir guncel API sozlesmesi |
| `API_SOZLESME_REFERANSI.md` | Endpoint ve model referansi |
| `UI_API_DOKUMANI.md` | Angular/terminal entegrasyon davranislari |
| `PROJE_GENEL_ISLEYISI.md` | Katmanlar, veri sahipligi ve temel isleyis |
| `GELISTIRICI_DEVAM_REHBERI.md` | Gelistirme, test, migration ve yayin adimlari |
| `CANLIYA_GECIS_KONTROL_LISTESI.md` | Yayin oncesi ve sonrasi kontroller |

## Index Ve SQL Performansi

| Kaynak | Amac |
|---|---|
| `UYGULAMA_INDEXLERI.sql` | Kullanilan tum ozel SQL Server indexlerinin tek idempotent kurulum dosyasi |
| `MIKRO_SORGU_VE_INDEX_ENVANTERI.md` | Sorgu erisim yollari, NOLOCK karari, aktif indexler ve kullanim aciklamasi |
| `SQL_PERFORMANS_TESPIT_SORGULARI.md` | Salt okunur performans tespit sorgulari |
| `MIKRO_STOK_HAREKETLERI_KILIT_ANALIZI.md` | STOK_HAREKETLERI kilit ve yazma maliyeti analizi |

`UYGULAMA_INDEXLERI.sql` disinda yeni kalici index SQL dosyasi acilmaz. Yeni bir
index gerekiyorsa once execution plan ve logical-read kaniti bu envantere
eklenir, sonra ana kataloga idempotent bolum olarak yazilir.

## Modul Ozetleri

- AXATA: `AXATA_ENTEGRASYON_ALTYAPISI.md`, `AXATA_WORKER_GOREVLERI.md`
- Firma/depo mal kabul: `FIRMA_MAL_KABUL_SENARYO.md`, `DEPO_MAL_KABUL_ISLEYIS.md`
- Siparis: `DEPO_ONERILEN_SIPARIS.md`, `FIRMA_ONERILEN_SIPARIS.md`
- Kasa: `KASA_HAREKET_AKTARIMI_MUHASEBE_AKISI.md`,
  `KASA_POS_BANKA_ICMAL_VERI_AKISI.md`,
  `KASA_SAYIMI_ICMAL_YETKI_Z_RAPORU_REHBERI.md`
- Manav: `MANAV_MAL_KABUL_VE_ETIKET_API.md`,
  `MANAV_OPERASYON_PANELI.md`, `MANAV_KASA_SIPARIS_SEVK_AKISI.md`
- Fatura/e-belge: `FATURA_GONDERIM_SISTEMI.md`,
  `E_FATURA_E_IRSALIYE_SERVIS_DOKUMANI.md`, `UBL_FATURA_MANTIGI.md`
- Trendyol Go: `TRENDYOL_GO_ENTEGRASYONU.md`
- Operasyon ve stok: `OPERASYON_ISLEMLERI_DETAYLI_API_DOKUMANI.md`,
  `DEPO_OPERASYON_PANELI_VERI_KAYNAKLARI.md`, `STOK_ANOMALI_MERKEZI.md`
- Raporlama: `rapor-modulu-envanter.md`

## Teknik Referanslar

- Mikro: `MIKRO_DESKTOP_GECIS_REHBERI.md`, `MIKRO_PROSEDUR_ANALIZI.md`,
  `MIKRO_REST_API_GECIS_ANALIZI.md`, `MIKRO_API_POSTMAN_DOKUMANI.md`
- Veri ve bagimsizlik: `AUTH_DB_TABLO_ONCELIKLERI_VE_MIKRO_BAGIMSIZLIK_PLANI.md`
- Barkod ve arama: `BARKOD_COZUMLEME_VE_ARAMA_REHBERI.md`
- Yetki: `YENI_MENU_YETKI_MIGRATION_REHBERI.md`
- Guvenli create: `TERMINAL_GUVENLI_CREATE_AKISI.md`
- Harici kaynak: `DIGER_PROJE_ISLEM_REFERANSI.md`

## Operasyon Ve Tarihsel SQL

`OPERASYON_HIZLI_MUDAHALE.md` canli sorunlarda ilk bakilacak rehberdir.

Tarih veya konu adi tasiyan `MIGRATE_*`, `DEPLOY_*`, `MIKRO_BIRIM_POINTER_*`
ve `MUHASEBE_FISLERI_*` SQL dosyalari kalici index katalogu degildir. Bunlar
uygulanmis veri/schema duzeltmelerinin denetlenebilir gecmisidir; otomatik veya
toplu bicimde yeniden calistirilmaz.

## Temel Komutlar

```powershell
dotnet build FurpaMerkezApi.sln
dotnet test --no-build
dotnet run --project src/FurpaMerkezApi.WebApi --launch-profile http
```

Swagger varsayilan olarak `http://localhost:5228/swagger` adresindedir.
