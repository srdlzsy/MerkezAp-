# Mikro REST API Gecis Analizi

Bu dokuman, FurpaMerkezApi icindeki dogrudan Mikro DB okuma/yazma davranisini Mikro REST API ile hangi noktalarda degistirebilecegimizi analiz eder.

Odak:

- Mevcut `create`, `update`, `delete` yazma noktalarini cikarmak.
- Her islem icin olasi Mikro REST endpoint karsiligini yazmak.
- Hangi islerin hizli tasinabilecegini, hangilerinin riskli oldugunu ayirmak.
- Gecis icin onerilen teknik mimariyi ve is sirasini netlestirmek.

Referans dokumanlar:

- `MIKRO_API_POSTMAN_DOKUMANI.md`
- `src/FurpaMerkezApi.WebApi/appsettings.Local.json`
- `src/FurpaMerkezApi.WebApi/appsettings.Production.json`

## Calisan Mikro REST Baglantisi

Uygulama config'ine `MikroApi` section'i eklendi.

```json
{
  "MikroApi": {
    "BaseUrl": "http://10.0.0.207:8084",
    "FirmaKodu": "SOPHIGET",
    "CalismaYili": 2026,
    "KullaniciKodu": "API",
    "SifreAnahtari": "<secret>",
    "FirmaNo": 0,
    "SubeNo": 0,
    "ApiKey": "<secret>",
    "TimeoutSeconds": 300
  }
}
```

`Sifre` alani sabit gonderilmiyor. Her istek icin gunluk hash uretilmeli:

```text
Sifre = MD5("yyyy-MM-dd <SifreAnahtari>")
```

Ornek:

```text
MD5("2026-06-11 <SifreAnahtari>") = "<gunluk-md5-hash>"
```

Canli test edilen endpointler:

| Endpoint | Sonuc |
|---|---|
| `GET /Api/APIMethods/HealthCheck2` | `ApiStatus=Up`, `StatusCode=200` |
| `POST /Api/APIMethods/APILogin` | `StatusCode=200`, `IsError=false` |
| `POST /Api/APIMethods/StokListesiV2` | `StatusCode=200`, `Data.StokListesi` dolu |

## Mevcut Mimari Ozeti

Sistem su anda Mikro ile iki farkli yoldan calisiyor:

1. **Read path dogrudan DB**: listeleme, arama, detay, rapor ve belge goruntuleme ekranlari `MikroDbContext` veya raw SQL ile Mikro tablolarindan okuyor.
2. **Write path dogrudan DB**: operasyonel create/update/delete islemleri `MikroWriteDbContext` ile Mikro tablolarina yaziyor.

Okuma tarafi cok fazla join, filtre, custom tablo ve rapor mantigi kullandigi icin REST API'ye tasinmasi ilk etapta verimli degil. Yazma tarafi ise Mikro REST API icin daha iyi aday.

Onerilen strateji:

```text
Read islemleri: DB okumaya devam
Write islemleri: modul modul Mikro REST API'ye tasinabilir
```

## Onerilen Teknik Mimari

### 1. MikroApiClient

Yeni bir typed client/service yazilmali.

Onerilen siniflar:

```text
Infrastructure/Services/MikroApi/MikroApiOptions.cs
Infrastructure/Services/MikroApi/MikroApiClient.cs
Infrastructure/Services/MikroApi/MikroApiAuthBlockFactory.cs
Infrastructure/Services/MikroApi/MikroApiResult.cs
Infrastructure/Services/MikroApi/MikroApiException.cs
```

Sorumluluklar:

- `MikroApi` config section'ini okumak.
- Gunluk MD5 hash uretmek.
- Ortak `Mikro` auth blogunu her request'e eklemek.
- Path casing'i collection'a uygun gondermek.
- HTTP status + response body loglamak.
- `IsError`, `StatusCode`, `ErrorMessage` gibi alanlari normalize etmek.
- Timeout, retry ve raw response yakalamak.

### 2. Write Mode / Feature Flag

Her kritik modul icin gecis tek seferde yapilmamali. Config ile secilebilir olmali.

Onerilen ayar:

```json
{
  "MikroWriteRouting": {
    "InventoryCount": "MikroApi",
    "IssuedWarehouseOrder": "MikroApi",
    "IssuedCompanyOrder": "MikroApi",
    "StockReceipt": "MikroApi",
    "Virman": "MikroApi",
    "InterWarehouseShipment": "MikroApi",
    "WarehouseReturn": "MikroApi",
    "CompanyMovement": "MikroApi",
    "CompanyReceiving": "MikroApi",
    "WarehouseReceivingAcceptance": "MikroApi"
  }
}
```

Modlar:

| Mod | Anlam |
|---|---|
| `Database` | Mevcut davranis, dogrudan DB write |
| `MikroApi` | Sadece Mikro REST API ile yaz |
| `DualShadow` | DB write yap, ayni payload'i API'ye dry-run/test olarak gonder veya logla |

Not: Mikro API'de gercek dry-run gorunmuyor. Bu yuzden `DualShadow` ancak test firma/yil veya log-only ile uygulanmali.

### 3. Idempotency ve Geri Donus

DB write tarafinda belge seri/sira ve GUID bizim tarafimizda uretiliyor. Mikro REST API'ye gecince response icinden donen GUID/seri/sira net degilse mutlaka DB'den geri okuma gerekir.

Her create icin:

- Client request id veya belge referansi tutulmali.
- Request payload loglanmali.
- Response body loglanmali.
- Basarili create sonrasi belge seri/sira/GUID DB'den dogrulanmali.
- Ayni request tekrar geldiginde duplicate belge olusturmamali.

## Genel Gecis Onceligi

| Oncelik | Islem ailesi | Gerekce |
|---|---|---|
| P0 | `MikroApiClient`, auth, logging, config | Tum islemlerin temeli |
| P1 | Sayim sonucu create | En izole, en az yan etki |
| P1 | Depolar arasi siparis create | Tek tabloya yakin, REST endpoint net |
| P2 | Verilen firma siparisi create | Endpoint var, mapping biraz daha genis |
| P2 | Zayiat/masraf/virman stok hareketleri | Tek tablo ama hareket tipi/cins dogrulanmali |
| P2 | Firma sevk/firma iade | Irsaliye endpointleriyle test gerekir |
| P3 | Depolar arasi sevk/depo iade | Hareket + hareket ek + siparis baglantisi var |
| P3 | Firma mal kabul | Siparis teslim miktari ve iade hareketleri var |
| P3 | Kasa sayimi / POS muhasebe | Custom tablolar + cari hareket + harici surecler var |

## Create / Update / Delete Eslesme Tablosu

### Stok Sayim Sonuclari

Mevcut kod:

- `InventoryCountWriteService`
- Mikro tablo: `SAYIM_SONUCLARI`
- Islem: sayim sonucu satirlarini create eder.
- Offline sync icin `ClientRequestId` iz mekanizmasi var.

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| Create | `POST /Api/apiMethods/SayimSonuclariKaydetV2` | En iyi ilk pilot aday |
| Update | `POST /Api/apiMethods/SayimSonuclariDuzeltV2` | Mevcut kodda update yok, gelecekte kullanilabilir |
| Satir sil | `POST /Api/apiMethods/SayimSonuclariSatirSilV2` | GUID saklama gerekir |
| Belge sil | `POST /Api/apiMethods/SayimSonuclariSilV2` | Silme senaryosu tasarlanmali |

Gecis notu:

- Mevcut create response `documentNo`, `warehouseNo`, `lineCount`, `totalQuantity` donuyor.
- Mikro API response belge no/GUID donmezse create sonrasi `SAYIM_SONUCLARI` DB'den geri okuma gerekir.
- Bu modulde API gecisi digerlerine gore dusuk riskli.

### Depolar Arasi Siparis

Mevcut kod:

- `CreateIssuedWarehouseOrderUseCase`
- Mikro tablo: `DEPOLAR_ARASI_SIPARISLER`
- Islem: depo giris/cikis siparis satirlari create eder.

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| Create | `POST /Api/apiMethods/DepolarArasiSiparisKaydetV2` | Net endpoint var |
| Update | `POST /Api/apiMethods/DepolarArasiSiparisDuzeltV2` | AXATA canli dispatch sonrasi `ssip_special1=1` isaretlemesi MikroApi modunda bu endpoint ile yapilir |
| GUID satir sil | `POST /Api/apiMethods/DepolarArasiSiparisGuidSilV2` | GUID gerektirir |
| Belge sil | `POST /Api/apiMethods/DepolarArasiSiparisSilV2` | Belge seri/sira veya GUID davranisi test edilmeli |

Gecis notu:

- Mevcut kod `documentSerie = F{InWarehouseNo}` ve sirayi DB max ile uretiyor.
- REST API kendi sirasini uretebilir veya verilen seri/sirayi kabul edebilir; bu davranis test edilmeli.
- `ssip_rezervasyon_miktari`, `ssip_paket_kod`, `ssip_sormerkezi` gibi alanlar mapping'e eklenmeli.
- `issued-warehouse-order-sync` AXATA'ya basarili gonderimden sonra `MikroWriteRouting:IssuedWarehouseOrder=MikroApi` ise `DepolarArasiSiparisDuzeltV2` ile satir `ssip_Guid` + `ssip_special1=1` gonderir; DB fallback yoktur, sadece read-only geri okuma ile tum satirlarin isaretlendigi dogrulanir.

### Verilen Firma Siparisi

Mevcut kod:

- `CreateIssuedCompanyOrderUseCase`
- Mikro tablo: `SIPARISLER`
- Islem: firma/musteri siparis satirlari create eder.

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| Create | `POST /api/APIMethods/SiparisKaydetV2` | Collection'da birden fazla ornek var |
| Update | `POST /Api/apiMethods/SiparisDuzeltV2` | GUID veya seri/sira gerekir |
| GUID satir sil | `POST /Api/apiMethods/SiparisGuidSilV2` | Satir GUID saklanmali |
| Belge sil | `POST /Api/apiMethods/SiparisSilV2` | Silme kurali test edilmeli |

Gecis notu:

- Mevcut kod cari odeme plani ve `cari_pasaport_no == "1"` bilgisini DB'den okuyup siparis defaultlarina yaziyor.
- REST payload olusturulmadan once cari bilgisi DB'den okunmaya devam edebilir.
- Siparis tipi/cinsi net: mevcut kod `sip_tip=1`, `sip_cins=0`.

Uygulama durumu:

- `CreateIssuedCompanyOrderUseCase` icine `MikroWriteRouting:IssuedCompanyOrder` baglandi.
- `Database`, `MikroApi` ve `DualShadow` modlari desteklenir.
- `MikroApi` modu `POST /api/APIMethods/SiparisKaydetV2` endpoint'ini kullanir.
- Payload mapper mevcut sistem davranisini korur: `sip_tip=1`, `sip_cins=0`, `sip_evrakno_seri=F{WarehouseNo}`, `sip_evrakno_sira` DB max + 1.
- Cari defaultlari (`cari_odemeplan_no`, `cari_pasaport_no == "1"`) REST payload olusmadan once Mikro DB'den okunmaya devam eder.
- REST create sonrasi belge `SIPARISLER` tablosundan geri okunup mevcut `CreateIssuedCompanyOrderResponse` formatina cevrilir.
- AXATA canli dispatch sonrasi firma siparisi gonderildi bayragi `MikroWriteRouting:CompanyOrderSentFlag=MikroApi` ise `POST /Api/apiMethods/SiparisDuzeltV2` ile `sip_Guid + sip_special1=1` olarak isaretlenir; DB fallback yoktur, read-only geri okuma ile dogrulanir.

### Zayiat Fisi / Masraf Fisi

Mevcut kod:

- `StockReceiptWriteService`
- Mikro tablo: `STOK_HAREKETLERI`
- Islem: zayiat ve stok masraf hareketlerini create eder.
- Belge tipi/cinsleri:
  - `sth_evraktip=0`
  - zayiat icin `sth_cins=4`
  - masraf icin `sth_cins=5`
  - cikis hareketi `sth_tip=1`

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| Create | `POST /Api/apiMethods/DahiliStokHareketKaydetV2` | En olasi endpoint |
| Update | `POST /Api/apiMethods/DahiliStokHareketDuzeltV2` | Mevcut kodda update yok |
| GUID sil | `POST /Api/apiMethods/DahiliStokHareketGuidSilV2` | Satir GUID gerekir |
| Belge sil | `POST /Api/apiMethods/DahiliStokHareketSilV2` | Belge silme test edilmeli |

Gecis notu:

- Endpoint'in `sth_cins=4/5` degerlerini kabul ettigi test edilmeli.
- Mevcut alanlarin cogu default; API minimum payload ile calisabilir.
- Basarili geciste belge sira uretimini Mikro API'ye birakmak daha saglikli olabilir.

Uygulama durumu:

- `StockReceiptWriteService` icine `MikroWriteRouting:StockReceipt` baglandi.
- `Database`, `MikroApi` ve `DualShadow` modlari desteklenir.
- `MikroApi` modu `POST /Api/apiMethods/DahiliStokHareketKaydetV2` endpoint'ini kullanir.
- Payload mapper mevcut sistem davranisini korur:
  - zayiat/fire: `sth_evraktip=0`, `sth_cins=4`, `sth_tip=1`
  - masraf/sarf: `sth_evraktip=0`, `sth_cins=5`, `sth_tip=1`
- REST create sonrasi belge `STOK_HAREKETLERI` tablosundan geri okunup mevcut `CreateStockReceiptResponse` formatina cevrilir.

### Virman

Mevcut kod:

- `VirmanWriteService`
- Mikro tablo: `STOK_HAREKETLERI`
- Islem: ayni depo icinde giris/cikis tipli virman hareketleri create eder.
- Belge tipi/cinsleri:
  - `sth_evraktip=6`
  - `sth_cins=3`
  - `sth_tip` satirdan geliyor.

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| Create | `POST /Api/apiMethods/DahiliStokHareketKaydetV2` | Hareket tipi/cins test edilmeli |
| Update | `POST /Api/apiMethods/DahiliStokHareketDuzeltV2` | Mevcut kodda update yok |
| GUID sil | `POST /Api/apiMethods/DahiliStokHareketGuidSilV2` | Satir GUID gerekir |
| Belge sil | `POST /Api/apiMethods/DahiliStokHareketSilV2` | Belge silme test edilmeli |

Gecis notu:

- Virman icin `sth_giris_depo_no` ve `sth_cikis_depo_no` ayni depo.
- API payload'inda hem giris hem cikis satir modeli nasil bekleniyor test edilmeli.

Uygulama durumu:

- `VirmanWriteService` icine `MikroWriteRouting:Virman` baglandi.
- `Database`, `MikroApi` ve `DualShadow` modlari desteklenir.
- `MikroApi` modu `POST /Api/apiMethods/DahiliStokHareketKaydetV2` endpoint'ini kullanir.
- Payload mapper mevcut sistem davranisini korur: `sth_evraktip=6`, `sth_cins=3`, `sth_tip` satirdan gelir, giris/cikis depo ayni depodur.
- REST create sonrasi belge `STOK_HAREKETLERI` tablosundan geri okunup mevcut `CreateVirmanResponse` formatina cevrilir.

Canli stok hareket frekansi notu:

- Stok hareket REST fazinda `sth_cins`, `sth_tip`, `sth_evraktip` kombinasyonlari canli veriye gore dogrulanmali.
- Zayiat/fire icin en net kombinasyon: `sth_cins=4`, `sth_tip=1`, `sth_evraktip=0`.
- Sarf icin canli veri kombinasyonu: `sth_cins=5`, `sth_tip=1`, `sth_evraktip=0`.
- Stok virman icin iki satirli akis beklenir: `sth_cins=3`, `sth_evraktip=6`, `sth_tip=1` cikis ve `sth_tip=0` giris.
- Depolar arasi nakliye/sevk tarafinda transfer kombinasyonlari `sth_cins=6`, `sth_tip=2`, `sth_evraktip=17` veya `2` olarak gorunur; create icin `DahiliStokHareketKaydetV2` secildi, canli tek satirli payload ile teyit edilmelidir.

### Firma Sevk / Firma Iade

Mevcut kod:

- `CompanyMovementWriteService`
- Kullanan use-case'ler:
  - `CreateCompanyShipmentUseCase`
  - `CreateCompanyReturnUseCase`
- Mikro tablo: `STOK_HAREKETLERI`
- Islem: cari bagli stok cikis/iade hareketleri create eder.
- Belge tipi/cinsleri:
  - `sth_evraktip=1`
  - `sth_tip=1`
  - normal sevk icin `sth_normal_iade=0`
  - iade icin `sth_normal_iade=1`

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| Create | `POST /Api/apiMethods/IrsaliyeKaydetV2` | Firma sevk/iade icin en olasi endpoint |
| Alternatif create | `POST /Api/apiMethods/AlimSatimEvragiKaydetV2` | Belge cinsi ihtiyacina gore test edilmeli |
| Update | `POST /Api/apiMethods/IrsaliyeDuzeltV2` | GUID veya belge seri/sira gerekir |
| Satir sil | `POST /Api/apiMethods/IrsaliyeSatirSilV2` | Satir GUID gerekir |
| Belge sil | `POST /Api/apiMethods/IrsaliyeSilV2` | Silme kurali test edilmeli |

Gecis notu:

- `CompanyMovementWriteService` icine `MikroWriteRouting:CompanyMovement` baglandi.
- `Database`, `MikroApi` ve `DualShadow` modlari desteklenir.
- `MikroApi` modu `POST /Api/apiMethods/IrsaliyeKaydetV2` endpoint'ini kullanir.
- Payload mapper mevcut sistem davranisini korur: `sth_evraktip=1`, `sth_tip=1`, `sth_cins=0`, normal sevk icin `sth_normal_iade=0`, firma iade icin `sth_normal_iade=1`.
- Cari kodu, depo, seri/sira, stok satirlari, fiyat, cari adres no, parti/lot, proje ve sorumluluk merkezi alanlari REST payload'ina maplenir.
- REST create sonrasi belge `STOK_HAREKETLERI` tablosundan geri okunup mevcut `CreateCompanyMovementResponse` formatina cevrilir.
- Canli ortamda tek satirli firma sevk ve firma iade payload'i ile Mikro tarafindaki e-irsaliye/irsaliye detay kurallari ayrica dogrulanmali.

### Depolar Arasi Sevk

Mevcut kod:

- `CreateInterWarehouseShipmentUseCase`
- Mikro tablolar:
  - `STOK_HAREKETLERI`
  - `STOK_HAREKETLERI_EK`
  - gerekirse `DEPOLAR_ARASI_SIPARISLER`
- Islem:
  - depolar arasi sevk hareketi create eder.
  - bagli depo siparisi varsa teslim miktarini gunceller.
  - bagli siparis yoksa otomatik depo siparisi olusturabilir.
  - hareket-ek tablosuyla hareketi siparis GUID'ine baglar.

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| Sevk create | `POST /Api/apiMethods/DahiliStokHareketKaydetV2` | Canli bagli siparis ornegi `sth_subesip_uid` ile dogrulandi |
| Otomatik siparis create | `POST /Api/apiMethods/DepolarArasiSiparisKaydetV2` | Mevcut otomasyon korunacaksa gerekir |
| Update | `POST /Api/apiMethods/DahiliStokHareketDuzeltV2` veya `POST /Api/apiMethods/IrsaliyeDuzeltV2` | Belge tipine gore secilir |
| Satir sil | `GuidSilV2` veya `SatirSilV2` ailesi | GUID saklama zorunlu |
| Belge sil | ilgili `...SilV2` endpoint'i | Etki analizi gerekir |

Gecis notu:

- `CreateInterWarehouseShipmentUseCase` icine `MikroWriteRouting:InterWarehouseShipment` baglandi.
- `Database`, `MikroApi` ve `DualShadow` modlari desteklenir.
- `MikroApi` modu stok hareketini `POST /Api/apiMethods/DahiliStokHareketKaydetV2` endpoint'i ile olusturur.
- Payload mapper mevcut sistem davranisini korur: `sth_evraktip=17`, `sth_tip=2`, `sth_cins=6`, `sth_normal_iade=0`, kaynak depo `sth_cikis_depo_no`, transit depo `sth_giris_depo_no`, hedef depo `sth_nakliyedeposu`, durum `sth_nakliyedurumu=0`.
- Mikro destek cevabina gore bagli depo siparisi satirlari payload satirinda `sth_subesip_uid = warehouseOrderLineGuid` olarak gonderilir; Mikro hareketi ilgili depo siparisine bu alanla baglar ve response icinde olusan hareket `guid` bilgisini dondurur.
- REST create sonrasi hareket satirlari `STOK_HAREKETLERI` tablosundan `sth_Guid` ile geri okunur.
- `warehouseOrderLineGuid` ile gelen bagli satirlarda `STOK_HAREKETLERI_EK` linki ve siparis teslim/kapanma etkisi Mikro tarafina birakilir; ayni satir icin DB'den tekrar hareket-ek insert veya teslim miktari update yapilmaz.
- `MikroApi` modunda otomatik depo siparisi olusturma aciksa once `POST /Api/apiMethods/DepolarArasiSiparisKaydetV2` ile depo siparisi olusturulur, olusan `DEPOLAR_ARASI_SIPARISLER.ssip_Guid` degerleri geri okunur ve sevk payload'ina `sth_subesip_uid` olarak yazilir.
- API-only davranisi korumak icin otomatik depo siparisli sevkte `MikroWriteRouting:IssuedWarehouseOrder` de `MikroApi` olmalidir; degilse sevk create baslamadan hata verilir.
- Canli ortamda tek satirli, bagli siparisli ve otomatik siparisli senaryolar ayri ayri dogrulanmali.

### Depo Iade

Mevcut kod:

- `CreateWarehouseReturnUseCase`
- Mikro tablolar:
  - `STOK_HAREKETLERI`
  - `STOK_HAREKETLERI_EK`
  - gerekirse `DEPOLAR_ARASI_SIPARISLER`
- Islem: depolar arasi iade hareketi create eder ve otomatik siparis baglantisi kurabilir.

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| Create | `POST /Api/apiMethods/DahiliStokHareketKaydetV2` | `sth_normal_iade=1` depo iadesi payload mapper ile baglandi |
| Otomatik siparis create | `POST /Api/apiMethods/DepolarArasiSiparisKaydetV2` | Otomasyon aciksa gerekir |
| Update | ilgili `...DuzeltV2` | Mevcut kodda update yok |
| Sil | ilgili `...SilV2` / `...GuidSilV2` | GUID ve belge kimligi gerekir |

Gecis notu:

- `CreateWarehouseReturnUseCase` icine `MikroWriteRouting:WarehouseReturn` baglandi.
- `Database`, `MikroApi` ve `DualShadow` modlari desteklenir.
- `MikroApi` modu stok hareketini `POST /Api/apiMethods/DahiliStokHareketKaydetV2` endpoint'i ile olusturur.
- Payload mapper mevcut sistem davranisini korur: `sth_evraktip=17`, `sth_tip=2`, `sth_cins=6`, `sth_normal_iade=1`, kaynak depo `sth_cikis_depo_no`, transit depo `sth_giris_depo_no`, hedef depo `sth_nakliyedeposu`, durum `sth_nakliyedurumu=0`.
- Mikro destek cevabina gore bagli depo siparisi satirlari payload satirinda `sth_subesip_uid` olarak gonderilir; depo iadesinde UI'dan bagli satir GUID'i alinmaz, otomasyonla olusan `DEPOLAR_ARASI_SIPARISLER.ssip_Guid` degeri kullanilir.
- REST create sonrasi hareket satirlari `STOK_HAREKETLERI` tablosundan `sth_Guid` ile geri okunur.
- `MikroApi` modunda otomatik depo siparisi olusturma aciksa once `POST /Api/apiMethods/DepolarArasiSiparisKaydetV2` ile depo siparisi olusturulur, olusan `ssip_Guid` degerleri geri okunur ve iade payload'ina `sth_subesip_uid` olarak yazilir.
- API-only davranisi korumak icin otomatik depo siparisli iadede `MikroWriteRouting:IssuedWarehouseOrder` de `MikroApi` olmalidir; degilse depo iade create baslamadan hata verilir.
- `MikroApi` modunda `DEPOLAR_ARASI_SIPARISLER`, `STOK_HAREKETLERI_EK` veya siparis teslim miktari icin DB tamamlayici insert/update yapilmaz; link/teslim etkisi Mikro tarafina birakilir.
- Canli ortamda otomatik siparis kapali ve acik depo iade senaryolari ayri ayri dogrulanmali.

### Firma Mal Kabul

Mevcut kod:

- `CreateCompanyReceivingUseCase`
- Mikro tablolar:
  - `STOK_HAREKETLERI`
  - gerekirse iade hareketi olarak yine `STOK_HAREKETLERI`
  - bagli sipariste `Database` modunda `SIPARISLER` teslim miktari update; `MikroApi` modunda `sth_sip_uid` ile Mikro API teslim etkisi
- Islem:
  - firma mal kabul hareketi create eder.
  - eksik/fazla ve iade senaryolarini yonetir.
  - siparis teslim miktarini Database modunda uygular, MikroApi modunda bu etkiyi Mikro API'ye birakir.

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| Mal kabul create | `POST /Api/apiMethods/IrsaliyeKaydetV2` | Toptan alis / perakende alis tipi test edilmeli |
| Alternatif create | `POST /Api/apiMethods/AlimSatimEvragiKaydetV2` | Mikro belge akisina gore secilebilir |
| Update | `POST /Api/apiMethods/IrsaliyeDuzeltV2` | GUID gerekir |
| Satir sil | `POST /Api/apiMethods/IrsaliyeSatirSilV2` | GUID gerekir |
| Belge sil | `POST /Api/apiMethods/IrsaliyeSilV2` | Etki analizi gerekir |

Gecis notu:

- `CreateCompanyReceivingUseCase` icine `MikroWriteRouting:CompanyReceiving` baglandi.
- `Database`, `MikroApi` ve `DualShadow` modlari desteklenir.
- `MikroApi` modu ana mal kabul hareketlerini `POST /Api/apiMethods/IrsaliyeKaydetV2` endpoint'i ile olusturur.
- Payload mapper mevcut sistem davranisini korur: `sth_evraktip=13`, `sth_tip=0`, `sth_cins=0`, `sth_normal_iade=0`, cari kodu, depo, seri/sira, siparis GUID, SKT, teslim eden/alan, parti/lot, proje ve sorumluluk merkezi alanlari maplenir.
- REST create sonrasi ana mal kabul hareketleri `STOK_HAREKETLERI` tablosundan `sth_Guid` ile geri okunur ve mevcut `CreateCompanyReceivingResponse` formatina cevrilir.
- Bagli siparis satirinda `sth_sip_uid = orderGuid` payload icinde gonderilir; `MikroApi` modunda `SIPARISLER.sip_teslim_miktar` icin DB tamamlayici update yapilmaz, teslim etkisi Mikro API'ye birakilir.
- Kismi kabulde otomatik firma iade hareketi aciksa iade hareketleri de `POST /Api/apiMethods/IrsaliyeKaydetV2` ile olusturulur; olusan iade satir GUID'leri geri okunup response'taki `returnMovementGuid` alanlarina yazilir.
- `MikroApi` modunda firma mal kabul icin Mikro is tablolarina manuel `STOK_HAREKETLERI` insert'i veya `SIPARISLER` update'i yapilmaz; DB sadece varlik kontrolu ve recovery icin okunur.
- Offline `clientRequestId` idempotency akisi korunur; trace degeri `sth_eticaret_kanal_kodu` alanina payload ile tasinir.
- Canli ortamda siparissiz, siparis bagli, fazla kabul ve kismi kabul/otomatik iade senaryolari ayri ayri dogrulanmali.

### Depo Mal Kabul Kabul Islemi

Mevcut kod:

- `AcceptWarehouseReceivingUseCase`
- Mikro tablo: `STOK_HAREKETLERI`
- Islem: bekleyen depolar arasi sevk satirlarini kabul eder.
- Guncellenen alanlar:
  - `sth_FormulMiktar`
  - `sth_giris_depo_no`
  - `sth_nakliyedeposu`
  - `sth_nakliyedurumu`
  - `sth_lastup_user`
  - `sth_lastup_date`
  - `sth_degisti`

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| Update | Direkt net endpoint yok | `IrsaliyeDuzeltV2`, `DahiliStokHareketDuzeltV2` veya `KayitKaydetV2` test edilmeli |

Gecis notu:

- `AcceptWarehouseReceivingUseCase` icine `MikroWriteRouting:WarehouseReceivingAcceptance` baglandi.
- `Database`, `MikroApi` ve `DualShadow` modlari desteklenir.
- `MikroApi` modu mevcut hareket satirlarini `POST /Api/apiMethods/DahiliStokHareketDuzeltV2` endpoint'i ile GUID bazli gunceller.
- Payload sadece kabul icin degisen alanlari tasir: `sth_Guid`, `sth_FormulMiktar`, `sth_giris_depo_no`, `sth_nakliyedeposu`, `sth_nakliyedurumu`, update user/date ve `sth_degisti`.
- REST update sonrasi hareketler `STOK_HAREKETLERI` tablosundan geri okunur; kabul miktari, hedef depo, transit depo ve teslim durumu dogrulanir.
- Canli ortamda eksiksiz kabul, eksik kabul ve fazla kabul senaryolari ayri ayri dogrulanmali.

### Kasa Sayimi

Mevcut kod:

- `CashSummaryCommandsUseCase`
- Mikro/custom tablolar:
  - `Summaries`
  - `BanknoteMovements`
  - `GiftCheckMovements`
  - `CARI_HESAP_HAREKETLERI`
- Islemler:
  - create kasa sayimi
  - update detaylar
  - update banknotlar
  - delete kasa sayimi

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| Cari hareket create | `POST /Api/apiMethods/TahsilatTediyeKaydetV2` veya `POST /Api/apiMethods/DekontKaydetV2` | Sadece cari hareket kismina denk gelebilir |
| Cari hareket update | `TahsilatTediyeDuzeltV2` | Custom tablolar kapsanmaz |
| Cari hareket sil | `TahsilatTediyeSilV2` veya `DekontSilV2` | Custom tablolar kapsanmaz |

Gecis notu:

- Kasa sayimi Mikro API'ye tamamen tasinacak iyi bir aday degil.
- `Summaries`, `BanknoteMovements`, `GiftCheckMovements` custom/yardimci tablolar REST collection'da yok.
- Sadece `CARI_HESAP_HAREKETLERI` kismi API'ye alinabilir ama bu da veri tutarliligini ikiye boler.
- Su an DB'de kalmasi onerilir.

### POS Muhasebe Aktarimi

Mevcut kod:

- `PosMuhasebeAktarimiService`
- Mikro/custom tablolar:
  - `ZReportTotals`
  - `ZReportDetails`
  - `ZReportBankDetails`
  - `Invoices`
  - `InvoiceLines`
  - `ExpenseNotes`
  - `ExpenseNoteLines`
  - `CashRegisterBranches`
- Islemler:
  - import POS faturasi
  - update POS faturasi
  - delete POS faturasi
  - import gider pusulasi
  - update gider pusulasi
  - delete gider pusulasi
  - kasa-subeler mapping create/update
  - ERP'ye gonderim tarafinda yazar henuz eksik gorunuyor.

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| POS fatura create | `POST /Api/apiMethods/FaturaKaydetV2` veya `POST /api/APIMethods/FaturaKaydetV3` | Ayrica e-belge akisina bagli |
| POS fatura update | Alim-satim / fatura duzeltme endpointleri | Collection'da net fatura update sinirli |
| POS fatura sil | Alim-satim evragi sil veya fatura sil davranisi test edilmeli | Riskli |
| Gider pusulasi / masraf | `KasaMasrafFisiKaydetV2`, `DekontKaydetV2`, `MuhasebeFisKaydetV2` | Muhasebe kuralina gore secilmeli |

Gecis notu:

- Bu modul once staging/import mantigini korumali.
- "ERP'ye gonder" asamasinda Mikro REST writer eklenebilir.
- Fatura/masraf muhasebe etkisi yuksek oldugu icin P3.

### Fatura Gonderimi

Mevcut kod:

- `InvoiceSendingService`
- Okuma: `CARI_HESAP_HAREKETLERI`, `STOK_HAREKETLERI`, cari/adres tablolarindan fatura XML'i hazirlar.
- Update: Uyumsoft gonderim basarili olunca `CARI_HESAP_HAREKETLERI` satirlarinda:
  - `cha_belge_no`
  - `cha_kilitli`
  - `cha_degisti`
  - `cha_lastup_user`
  - `cha_lastup_date`

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| Fatura PDF | `POST /API/APIMethods/FaturaPdfV2` | Sadece PDF/okuma tarafi olabilir |
| Fatura create | `POST /Api/apiMethods/FaturaKaydetV2` veya V3 | Mevcut kod fatura create etmiyor, gonderiyor |
| Fatura hareket update | Net degil | Belge no/kilit update icin API endpoint net degil |

Gecis notu:

- Bu modul Mikro REST'ten cok Uyumsoft SOAP entegrasyonudur.
- Fatura gonderildikten sonra Mikro hareketini isaretleme icin DB update su an daha kontrollu.

### Ayarlar / Kasa Terminal / Kasiyer

Mevcut kod:

- `AyarlarService`
- Furpa DB ve Mikro custom tablolarina yazar.
- Kasa terminal mapping ve cihaz ayarlari Mikro REST collection'da yok.

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| Create/update/delete | Yok | DB'de kalmali |

### GreenGrocer Depo Siparisi Silme

Mevcut kod:

- `DeleteGreenGrocerOrderUseCase`
- Mikro tablo: `DEPOLAR_ARASI_SIPARISLER`
- Islem: belirli depo siparisi satirlarini siler.

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| GUID sil | `POST /Api/apiMethods/DepolarArasiSiparisGuidSilV2` | Satir GUID ile daha guvenli |
| Belge sil | `POST /Api/apiMethods/DepolarArasiSiparisSilV2` | Tum belgeyi silebilir, dikkat |

Gecis notu:

- Depolar arasi siparis create API'ye tasininca bu silme de ayni ailede tasinabilir.

## Okuma Islemleri Icin Durum

REST API'de su liste endpointleri var ve calisabilir:

| Okuma | REST endpoint | Bizdeki kullanim |
|---|---|---|
| Stok listesi | `POST /Api/APIMethods/StokListesiV2` | Arama, etiket, barkod, mobil katalog icin kismi |
| Cari listesi | `POST /Api/APIMethods/CariListesiV3` | Cari arama/katalog icin kismi |
| Kullanici listesi | `POST /Api/APIMethods/KullaniciListesiV2` | Lookup icin kismi |
| Vergi listesi | `POST /Api/APIMethods/VergiListesiV2` | Yardimci lookup |

Ancak okuma tarafini hemen REST'e tasimamak daha dogru:

- Bizde cok fazla custom join var.
- Barkod, fiyat, etiket, rapor ve satis analizleri cok tablo kullaniyor.
- Mikro REST liste endpointleri sayfali ve genel amacli.
- Performans, alan eksikligi ve filtre farklari sorun olabilir.

Oneri:

```text
Read path = DB
Write path = Mikro REST pilotlari
```

## Uygulama Plani

### Faz 0 - Teknik altyapi

- `MikroApiOptions` ekle.
- `MikroApiClient` ekle.
- Gunluk MD5 hash ureten auth builder ekle.
- Ortak `Mikro` blok builder ekle.
- `MikroApiResult` response modeli ekle.
- Raw request/response structured logging ekle.
- Timeout degeri config'e al.
- Health check'e Mikro REST probe ekle.

### Faz 1 - Dusuk risk create pilotlari

1. `SayimSonuclariKaydetV2`
2. `DepolarArasiSiparisKaydetV2`

Her pilot icin:

- Bir mapper yaz.
- Mevcut DB writer'i koru.
- Config ile `Database` / `MikroApi` sec.
- Test ortaminda ayni request'i DB write ve REST write olarak karsilastir.
- REST create sonrasi DB'den belgeyi okuyup response'u mevcut API response'una cevir.

### Faz 2 - Orta risk create/update aileleri

1. `SiparisKaydetV2` - verilen firma siparisi create icin baglandi
2. `DahiliStokHareketKaydetV2` - zayiat/masraf/virman/depolar arasi sevk/depo iade create icin baglandi
3. `IrsaliyeKaydetV2` - firma sevk/iade ve firma mal kabul create icin baglandi

Bu fazda update/sil endpointleri de contract olarak hazirlanabilir:

- `SiparisDuzeltV2`
- `SiparisGuidSilV2`
- `DahiliStokHareketDuzeltV2`
- `DahiliStokHareketGuidSilV2`
- `IrsaliyeDuzeltV2`
- `IrsaliyeSatirSilV2`

2026-09-04 itibariyla eklenen opsiyonel routing kapilari:

| Routing key | Varsayilan | Durum | Mikro API hedefi |
|---|---|---|---|
| `WarehouseShippingUpdate` | `Database` | Guard eklendi; API mapper bekliyor | `DahiliStokHareketDuzeltV2`, `DahiliStokHareketGuidSilV2`, `DepolarArasiSiparisDuzeltV2` |
| `GreenGrocerOrderDelete` | `Database` | `MikroApi` modu baglandi | `DepolarArasiSiparisGuidSilV2` |
| `MicroDocumentEditing` | `Database` | Depo siparisi/firma siparisi/stok hareketi update-sil ailelerinde guard eklendi | `DepolarArasiSiparisDuzeltV2`, `DepolarArasiSiparisGuidSilV2`, `SiparisDuzeltV2`, `SiparisGuidSilV2`, `DahiliStokHareketDuzeltV2`, `DahiliStokHareketGuidSilV2` |
| `GreenGrocerOperations` | `Database` | Guard eklendi; MNV payload mapper bekliyor | `DahiliStokHareketKaydetV2` |
| `ProductDistribution` | `Database` | Guard eklendi; `D{depo}` seri ve rezervasyon semantigi icin ozel mapper bekliyor | `DepolarArasiSiparisKaydetV2` |
| `InvoiceSendingMarkAsSent` | `Database` | `MikroApi` modu deneysel olarak baglandi; response sonrasi DB readback ile dogrular | `AlimSatimEvragiDuzeltV2` (`cha_Guid`, `cha_belge_no`, `cha_uuid`, `cha_kilitli`) |
| `CompanyOrderSentFlag` | `Database` | AXATA worker/manual dispatch firma siparisi gonderildi bayragi icin `MikroApi` modu baglandi | `SiparisDuzeltV2` (`sip_Guid`, `sip_special1`) |

### Faz 3 - Yuksek risk ve karma is akislari

- Depolar arasi sevk
- Depo iade
- Firma mal kabul
- Depo mal kabul kabul islemi
- Kasa sayimi
- POS muhasebe
- Fatura gonderimi sonrasi Mikro isaretleme

Bu fazda bazi islemler DB'de kalabilir. Amac her seyi REST'e tasimak degil, Mikro'nun resmi API'sinin guvenli oldugu noktalari kullanmak olmali.

## Endpoint Dogrulama Checklist'i

Her Mikro REST endpoint'i icin canli testte su cevaplar kaydedilmeli:

- Basarili response semasi.
- Hata response semasi.
- Belge seri/sira Mikro tarafinda uretiliyor mu?
- Verilen seri/sira kabul ediliyor mu?
- GUID response'ta donuyor mu?
- Ayni request tekrar atilirsa duplicate olusuyor mu?
- Silme endpoint'i belge mi satir mi siliyor?
- Duzeltme endpoint'i tam belge mi partial update mi bekliyor?
- Trigger/muhasebe etkisi DB write ile ayni mi?
- E-belge / AXATA / Uyumsoft gibi yan sistemlere etkisi var mi?

## Riskler

| Risk | Aciklama | Onlem |
|---|---|---|
| Response semasi belirsiz | Postman collection'da response ornekleri yok | Runtime test dokumani tutulmali |
| Belge no/GUID kaybi | API create sonrasi gerekli kimlik donmeyebilir | DB'den geri okuma ve audit log |
| Duplicate create | Retry ayni belgeyi iki kez acabilir | ClientRequestId, belge no, idempotency kontrolu |
| Mapping farki | DB'deki kolon defaultlari API tarafinda farkli olabilir | DB write vs API write karsilastirma |
| Performans | Liste endpointleri DB kadar hizli olmayabilir | Read path DB kalmali |
| Custom tablolar | Kasa/POS/terminal gibi tablolar REST'te yok | Bu moduller DB'de kalmali |
| Transaction kaybi | DB'de tek transaction olan is API'de birden fazla call olabilir | Saga/compensation veya DB kalma karari |

## Ilk Is Paketi Onerisi

En temiz baslangic:

1. `MikroApiClient` altyapisini ekle.
2. `SayimSonuclariKaydetV2` icin mapper yaz.
3. `InventoryCountWriteService` icinde route flag ekle.
4. REST create sonrasi DB'den sayim satirlarini geri okuyup mevcut response'u uret.
5. Test sonucu olumluysa `DepolarArasiSiparisKaydetV2` ile devam et.

Bu sira sistemin canli operasyon riskini dusuk tutar ve Mikro REST API davranisini kontrollu sekilde ogrenmemizi saglar.

## MikroApi Gecisi Pilot / Platform Testleri

Bu bolum canliya gecmeden once test veya pilot ortamda calistirilacak zorunlu test setidir. Her testte request body, response body, MikroApi audit kaydi, geri okunan Mikro belge/satir bilgisi ve varsa yan sistem etkisi kaydedilmelidir.

### Genel Platform Testleri

- `MikroApi` config dogrulama: `BaseUrl`, `FirmaKodu`, `CalismaYili`, `KullaniciKodu`, `SifreAnahtari`, `ApiKey`, timeout ve retry ayarlari dogru ortam degerleriyle calismali.
- Auth testi: gunluk MD5 sifre uretimi ile Mikro API login veya basit health/probe cagrisi basarili donmeli.
- Audit testi: her POST icin `MikroApiWriteAudit` kaydinda path, request, response, HTTP status, Mikro `StatusCode`, hata mesaji ve recovery bilgisi gorulmeli.
- Gizli veri testi: log ve audit icinde sifre/API key gibi alanlar maskelenmis olmali.
- Timeout/retry testi: timeout durumunda kontrollu hata donmeli; retry duplicate belge olusturmamali.
- Idempotency testi: `clientRequestId` destekleyen akislarda ayni request tekrar geldiginde ayni sonuc toparlanmali; farkli payload ile ayni id gelirse hata donmeli.
- Yetki testi: depo bazli create/list/detail akislari normal depo yetkisi ve `all-warehouses` yetkisi ile ayri ayri denenmeli.
- Config routing testi: ayni islem `Database` ve `MikroApi` modunda ayri calistirilmali; `DualShadow` icin dry-run olmadigi bilindigi icin canli yazim gibi kullanilmamali.
- Recovery testi: MikroApi basarili dondukten sonra DB readback ile belge/satir bulunmali; bulunamazsa backend basarili gibi davranmamali.
- Hata response testi: eksik zorunlu alan, hatali stok/cari/depo, kapali siparis ve fazla miktar gibi durumlarda backend anlamli hata dondurmeli.
- Performans testi: listeleme read path DB'de kalirken create sonrasi readback suresi kabul edilebilir olmali.

### Sayim Sonuclari

- `MikroWriteRouting:InventoryCount=MikroApi` ile tek satirli sayim olustur.
- Cok satirli sayim olustur; farkli stok, barkod, birim pointer ve miktar kombinasyonlarini dene.
- Ayni `clientRequestId` ile tekrar gonder; duplicate sayim olusmadigini ve response'un toparlandigini dogrula.
- Mikro `SAYIM_SONUCLARI` geri okumasinda belge no, depo, tarih, satir sayisi ve toplam miktar UI response'u ile eslesmeli.

### Verilen Depo Siparisi

- `MikroWriteRouting:IssuedWarehouseOrder=MikroApi` ile tek satirli depo siparisi olustur.
- Cok satirli depo siparisi olustur; `ssip_Guid`, seri, sira, giris depo, cikis depo, miktar ve birim bilgilerini geri oku.
- Ayni seri/sira icin duplicate riskini test et; tekrar gonderim yeni evrak mi aciyor, hata mi donuyor kaydet.
- `DepolarArasiSiparisDuzeltV2` ile `ssip_Guid + ssip_special1=1` isaretleme testini yap; tum satirlar read-only geri okumada `1` olmali.

### Verilen Firma Siparisi

- `MikroWriteRouting:IssuedCompanyOrder=MikroApi` ile tek satirli firma siparisi olustur.
- Cok satirli sipariste cari, stok, miktar, fiyat, proje ve sorumluluk merkezi alanlarini geri oku.
- Hatali cari/stok ve sifir miktar senaryolarinda backend hata response'unu kaydet.
- Siparis create sonrasi sonraki mal kabul akisi icin `sip_Guid` degerinin dogru okunabildigini dogrula.

### Stok Fisi / Sarf / Zayiat

- `MikroWriteRouting:StockReceipt=MikroApi` ile desteklenen stok hareket tiplerini tek tek dene.
- `sth_tip`, `sth_cins`, `sth_evraktip`, depo, stok, miktar, fiyat ve belge no alanlarini DB write ornegiyle karsilastir.
- Iptal/silme kullanilacaksa ilgili `DahiliStokHareket...SilV2` endpoint davranisini ayri test et; belge mi satir mi sildigini kaydet.

### Virman

- `MikroWriteRouting:Virman=MikroApi` ile giris/cikis depo virmanini olustur.
- Stok hareketinde giris depo, cikis depo, miktar, birim ve hareket cinsi DB mode ornegiyle ayni olmali.
- Negatif miktar, ayni depo, hatali depo ve hatali stok senaryolarini hata testi olarak calistir.

### Firma Sevk / Firma Iade

- `MikroWriteRouting:CompanyMovement=MikroApi` ile firma sevk olustur.
- Firma iade veya iade normal tipi gerekiyorsa ayri evrakla test et.
- Cari, depo, `sth_evraktip`, `sth_tip`, `sth_cins`, `sth_normal_iade`, stok, miktar, fiyat ve vergi alanlarini geri oku.
- E-irsaliye veya Uyumsoft'a etki edecek evraklarda e-belge akisi ayrica test edilmeli.

### Depolar Arasi Sevk

- `MikroWriteRouting:InterWarehouseShipment=MikroApi` ile siparissiz tek satirli sevk olustur.
- Var olan depo siparis satiri ile `warehouseOrderLineGuid` gonder; Mikro payload'inda `sth_subesip_uid` gitmeli.
- Sevk sonrasi `STOK_HAREKETLERI_EK.sth_subesip_uid` linki Mikro tarafinda olusmali.
- Bagli sipariste `ssip_teslim_miktar` ve kapanma etkisi Mikro tarafinda beklenen sekilde olusmali.
- Otomatik depo siparisi gerekiyorsa `MikroWriteRouting:IssuedWarehouseOrder=MikroApi` ile once siparisin API'den olustugunu, sonra sevkin bu `ssip_Guid` ile baglandigini dogrula.
- Kismi sevk, tam sevk, kalan miktari asan sevk, hatali hedef depo ve hatali transit depo senaryolarini ayri calistir.

### Depo Iade

- `MikroWriteRouting:WarehouseReturn=MikroApi` ile otomatik siparis kapaliyken depo iadesi olustur.
- Otomatik siparis acikken once `DepolarArasiSiparisKaydetV2`, sonra `DahiliStokHareketKaydetV2` akisi calismali.
- Iade hareketinde `sth_normal_iade=1`, `sth_evraktip=17`, depo ve miktar alanlarini geri oku.
- `sth_subesip_uid` linki, `STOK_HAREKETLERI_EK` kaydi ve siparis teslim etkisi Mikro tarafinda dogrulanmali.
- `IssuedWarehouseOrder` MikroApi degilken otomatik siparisli iade baslamadan hata vermeli; DB tamamlayici insert yapmamali.

### Firma Mal Kabul

- `MikroWriteRouting:CompanyReceiving=MikroApi` ile siparissiz tam kabul olustur.
- `orderGuid` dolu siparis bagli tam kabul olustur; `sth_sip_uid` ve siparis teslim etkisi dogrulanmali.
- Kismi kabul test et: `dispatchQuantity > acceptedQuantity` iken ana mal kabul ve otomatik firma iade hareketi MikroApi ile olusmali.
- `autoCreateReturnForPartialAcceptance=false` test et; iade hareketi olusmamali, response manuel cozum bekleyen durum dondurmeli.
- Fazla kabul senaryosu: `allowOrderOverReceiving=false` hata vermeli, `true` ise kalan siparisli ve fazla kisim siparissiz bolunmeli.
- Offline/idempotency senaryosu: ayni `clientRequestId` ile tekrar request sonucu toparlanmali.

### Depo Mal Kabul Kabul Islemi

- `MikroWriteRouting:WarehouseReceivingAcceptance=MikroApi` ile bekleyen depolar arasi mal kabul satirlarini kabul et.
- `DahiliStokHareketDuzeltV2` sonrasi hareket satirlarinda kabul miktari ve nakliye durumu beklenen hale gelmeli.
- Tam kabul, kismi kabul, sifir kabul ve fazla kabul hatasi ayri test edilmeli.
- Kabul sonrasi depo mal kabul farklari ve detay endpointleri yeni durumu dogru gostermeli.

### AXATA Entegrasyonu

- Worker/scheduler acikken normal task'larin Mikro'ya belge yazmadigini, sadece Outbox/payload urettigini dogrula.
- `issued-warehouse-order-sync` manuel live dispatch ile AXATA `addOutboundOrder*` basarili donmeli.
- `IssuedWarehouseOrder=MikroApi` iken dispatch sonrasi `ssip_special1=1` isaretleme `DepolarArasiSiparisDuzeltV2` ile yapilmali; DB update fallback olmamali.
- `IssuedWarehouseOrder=Database` iken eski DB update davranisi korunmali.
- Live audit `unsyncedWarehouseOrders`, `sentWarehouseOrdersMissingMikroShipments`, `sentWarehouseOrdersWithShipmentDifferences` listelerini dogru uretmeli.
- C01 import testinde AXATA teslimati Mikro depolar arasi sevk fisine cevrilmeli; `sth_subesip_uid` linki olusmali.
- C01 belge bazli rescue testinde `status=0` ve `status=1` arama davranisi, guvenli satir eslesmesi ve duplicate fis engeli dogrulanmali.
- ACK testinde Mikro yazim basarili olmadan AXATA `ENT006.S06STAT=1` yapilmamali.

### Duzeltme ve Silme Aileleri

- Depo siparisi update: `DepolarArasiSiparisDuzeltV2` ile miktar ve `ssip_special1` gibi alanlar GUID bazli guncellenmeli.
- Depo siparisi GUID silme: `DepolarArasiSiparisGuidSilV2` satir silme davranisi test edilmeli.
- Firma siparisi update/sil: `SiparisDuzeltV2`, `SiparisGuidSilV2`, `SiparisSilV2` davranisi belge ve satir bazinda ayrilmali.
- Dahili stok hareket update/sil: `DahiliStokHareketDuzeltV2`, `DahiliStokHareketGuidSilV2`, `DahiliStokHareketSilV2` icin GUID ve belge kimligi davranisi kaydedilmeli.
- Irsaliye update/sil: `IrsaliyeDuzeltV2`, `IrsaliyeSatirSilV2`, `IrsaliyeSilV2` e-belgeye etkisiyle birlikte test edilmeli.
- Duzeltme/silme testleri canli pilotta once test firma/yil veya geri alinabilir evraklarla yapilmali.

### Kapanis Kabul Kriterleri

- Her MikroApi create/update icin API response basarili, audit tamamlanmis ve DB readback dogrulanmis olmali.
- MikroApi modunda is tablolarina manuel DB insert/update kalmamali; DB sadece readback, dogrulama ve rapor icin okunmali.
- Database moduna geri donus ayni config degisikligiyle calismali.
- UI response alanlari Database ve MikroApi modunda ayni sozlesmeyi korumali.
- Yetki, depo scope, duplicate, timeout, recovery ve yan sistem etkileri test kanitiyla kapatilmali.
- Pilot evraklar icin Mikro ekraninda belge, satir, link, teslim miktari ve e-belge/AXATA durumlari operasyon ekibiyle birlikte onaylanmali.
