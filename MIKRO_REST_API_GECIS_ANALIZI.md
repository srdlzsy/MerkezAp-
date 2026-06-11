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
    "ApiKey": "<secret>"
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
    "InventoryCount": "Database",
    "IssuedWarehouseOrder": "Database",
    "IssuedCompanyOrder": "Database",
    "StockReceipt": "Database",
    "InterWarehouseShipment": "Database",
    "CompanyMovement": "Database"
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
| Update | `POST /Api/apiMethods/DepolarArasiSiparisDuzeltV2` | Mevcut kodda manuel update yok |
| GUID satir sil | `POST /Api/apiMethods/DepolarArasiSiparisGuidSilV2` | GUID gerektirir |
| Belge sil | `POST /Api/apiMethods/DepolarArasiSiparisSilV2` | Belge seri/sira veya GUID davranisi test edilmeli |

Gecis notu:

- Mevcut kod `documentSerie = F{InWarehouseNo}` ve sirayi DB max ile uretiyor.
- REST API kendi sirasini uretebilir veya verilen seri/sirayi kabul edebilir; bu davranis test edilmeli.
- `ssip_rezervasyon_miktari`, `ssip_paket_kod`, `ssip_sormerkezi` gibi alanlar mapping'e eklenmeli.

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

- Mevcut kod cari adres numarasini DB'den cozuyor.
- REST payload'ina cari kodu, depo, seri/sira, stok satirlari, fiyat, parti/lot, proje ve sorumluluk merkezi alanlari maplenmeli.
- Bu islem icin once test ortaminda tek satirli sevk ve iade payload'i dogrulanmali.

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
| Sevk create | `POST /Api/apiMethods/DahiliStokHareketKaydetV2` veya `POST /Api/apiMethods/IrsaliyeKaydetV2` | Hangisinin dogru oldugu canli payload ile test edilmeli |
| Otomatik siparis create | `POST /Api/apiMethods/DepolarArasiSiparisKaydetV2` | Mevcut otomasyon korunacaksa gerekir |
| Update | `POST /Api/apiMethods/DahiliStokHareketDuzeltV2` veya `POST /Api/apiMethods/IrsaliyeDuzeltV2` | Belge tipine gore secilir |
| Satir sil | `GuidSilV2` veya `SatirSilV2` ailesi | GUID saklama zorunlu |
| Belge sil | ilgili `...SilV2` endpoint'i | Etki analizi gerekir |

Gecis notu:

- Bu modul tek REST call ile tasinmayabilir.
- Hareket-ek (`STOK_HAREKETLERI_EK`) baglantisini Mikro API otomatik uretiyor mu bilinmiyor.
- Siparis teslim miktari update davranisi API tarafinda otomatik olmuyorsa DB update gerekecek.
- Bu yuzden P3, yani gecis icin gec asama adayi.

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
| Create | `POST /Api/apiMethods/DahiliStokHareketKaydetV2` veya `POST /Api/apiMethods/IrsaliyeKaydetV2` | Iade tipi/cinsi test edilmeli |
| Otomatik siparis create | `POST /Api/apiMethods/DepolarArasiSiparisKaydetV2` | Otomasyon aciksa gerekir |
| Update | ilgili `...DuzeltV2` | Mevcut kodda update yok |
| Sil | ilgili `...SilV2` / `...GuidSilV2` | GUID ve belge kimligi gerekir |

Gecis notu:

- Depolar arasi sevk ile ayni risk ailesinde.
- REST gecisi icin once otomatik siparis kapali senaryo test edilmeli.

### Firma Mal Kabul

Mevcut kod:

- `CreateCompanyReceivingUseCase`
- Mikro tablolar:
  - `STOK_HAREKETLERI`
  - gerekirse iade hareketi olarak yine `STOK_HAREKETLERI`
  - bagli siparis varsa `SIPARISLER` teslim miktari update
- Islem:
  - firma mal kabul hareketi create eder.
  - eksik/fazla ve iade senaryolarini yonetir.
  - siparis teslim miktarini uygular.

REST karsiliklari:

| Islem | Mikro REST endpoint | Not |
|---|---|---|
| Mal kabul create | `POST /Api/apiMethods/IrsaliyeKaydetV2` | Toptan alis / perakende alis tipi test edilmeli |
| Alternatif create | `POST /Api/apiMethods/AlimSatimEvragiKaydetV2` | Mikro belge akisina gore secilebilir |
| Update | `POST /Api/apiMethods/IrsaliyeDuzeltV2` | GUID gerekir |
| Satir sil | `POST /Api/apiMethods/IrsaliyeSatirSilV2` | GUID gerekir |
| Belge sil | `POST /Api/apiMethods/IrsaliyeSilV2` | Etki analizi gerekir |

Gecis notu:

- Siparis teslim miktari API tarafinda otomatik guncelleniyor mu bilinmiyor.
- Mevcut kodda idempotency, duplicate belge kontrolu ve linked order logic var.
- Bu modul P3 olmali; once daha basit stok/siparis endpointleri stabilize edilmeli.

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

- Bu islem standart belge create degil, mevcut hareket satirlarinda kabul durumunu degistiriyor.
- Mikro REST collection'da "mal kabul kabul et" gibi direkt endpoint gorunmuyor.
- DB'de kalmasi su an daha guvenli.

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

1. `SiparisKaydetV2`
2. `DahiliStokHareketKaydetV2`
3. `IrsaliyeKaydetV2`

Bu fazda update/sil endpointleri de contract olarak hazirlanabilir:

- `SiparisDuzeltV2`
- `SiparisGuidSilV2`
- `DahiliStokHareketDuzeltV2`
- `DahiliStokHareketGuidSilV2`
- `IrsaliyeDuzeltV2`
- `IrsaliyeSatirSilV2`

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
