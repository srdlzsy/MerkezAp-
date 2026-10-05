# Mikro API Create Payload Haritasi

Tarih: 2026-10-05

Bu dokuman, FurpaMerkezApi icinde yeni Mikro evragi olusturan akislarin hangi
Mikro API metoduna gittigini ve payload'in nerede uretildigini kod uzerinden
gosterir. Duzeltme, silme ve yalnizca "gonderildi" isareti atan Mikro yazmalari
ana create listesinden ayrilmistir.

## Canli Yonlendirme Durumu

`appsettings.Production.json` icinde asagidaki create yollarinin tamami
`MikroApi` modundadir:

- `InventoryCount`
- `IssuedWarehouseOrder`
- `IssuedCompanyOrder`
- `StockReceipt`
- `Virman`
- `InterWarehouseShipment`
- `WarehouseReturn`
- `CompanyMovement`
- `CompanyReceiving`
- `GreenGrocerOperations`
- `ProductDistribution`
- `GreenGrocerGoodsReceipt`
- `PosAccountingSlip`
- `AxataDynamicCensus`

Bu nedenle canlida bu islemler dogrudan Mikro DB insert yoluna degil, asagidaki
Mikro API metotlarina gider.

## Ortak Dis Zarf

Tum create payload'lari `MikroApiClient.PostWithMikroPayloadAsync` ile gonderilir.
Is payload'i, auth bilgileriyle birlikte `Mikro` nesnesinin icine birlestirilir:

```json
{
  "Mikro": {
    "FirmaKodu": "***",
    "CalismaYili": 2026,
    "KullaniciKodu": "***",
    "Sifre": "gunluk-md5-hash",
    "FirmaNo": 0,
    "SubeNo": 0,
    "ApiKey": "***",
    "evraklar": []
  }
}
```

Auth blogu `MikroApiAuthBlockFactory.CreateMikroPayload` tarafindan eklenir.
Parola, `yyyy-MM-dd + bosluk + SifreAnahtari` degerinin gunluk MD5 hash'idir.
Audit tablosuna auth blogu degil, yalniz business payload yazilir.

## Ana Create Matrisi

| Furpa modulu / islem | Furpa API veya tetikleyici | Routing anahtari | Mikro API metodu | Payload ureten kod |
|---|---|---|---|---|
| Depolar arasi sevk | `POST /api/sevk-islemleri/depolar-arasi-sevkler/giden` | `InterWarehouseShipment` | `DahiliStokHareketKaydetV2` | `InterWarehouseShipmentMikroApiPayloadFactory.Create` |
| Depo iadesi | `POST /api/iade-islemleri/depo-iadeleri/giden` | `WarehouseReturn` | `DahiliStokHareketKaydetV2` | `WarehouseReturnMikroApiPayloadFactory.Create` |
| Firma sevki | `POST /api/sevk-islemleri/firma-sevkleri/giden` | `CompanyMovement` | `IrsaliyeKaydetV2` | `CompanyMovementIrsaliyeMikroApiPayloadFactory.Create` |
| Firma iadesi | `POST /api/iade-islemleri/firma-iadeleri` | `CompanyMovement` | `IrsaliyeKaydetV2` | `CompanyMovementIrsaliyeMikroApiPayloadFactory.Create` |
| Firma mal kabul | `POST /api/mal-kabul-islemleri/firma-mal-kabulleri` | `CompanyReceiving` | `IrsaliyeKaydetV2` | `CompanyReceivingIrsaliyeMikroApiPayloadFactory.Create` |
| Firma mal kabul otomatik iade | Firma mal kabul icinde eksik/fazla senaryosu | `CompanyReceiving` | `IrsaliyeKaydetV2` | `CompanyReceivingIrsaliyeMikroApiPayloadFactory.Create` |
| Zayiat fisi | `POST /api/stok-islemleri/zayiat-fisleri` | `StockReceipt` | `DahiliStokHareketKaydetV2` | `StockMovementMikroApiPayloadFactory.CreateStockReceipt` |
| Masraf fisi | `POST /api/stok-islemleri/masraf-fisleri` | `StockReceipt` | `DahiliStokHareketKaydetV2` | `StockMovementMikroApiPayloadFactory.CreateStockReceipt` |
| Virman | `POST /api/stok-islemleri/virmanlar` | `Virman` | `DahiliStokHareketKaydetV2` | `StockMovementMikroApiPayloadFactory.CreateVirman` |
| Sayim sonucu | `POST /api/stok-islemleri/sayim-sonuclari` | `InventoryCount` | `SayimSonuclariKaydetV2` | `InventoryCountMikroApiPayloadFactory.Create` |
| Verilen depo siparisi | `POST /api/siparis-islemleri/verilen-depo-siparisleri` | `IssuedWarehouseOrder` | `DepolarArasiSiparisKaydetV2` | `IssuedWarehouseOrderMikroApiPayloadFactory.Create` |
| Verilen firma siparisi | `POST /api/siparis-islemleri/verilen-firma-siparisleri` | `IssuedCompanyOrder` | `SiparisKaydetV2` | `IssuedCompanyOrderMikroApiPayloadFactory.Create` |
| Manav ic tartim/stok duzeltmesi | `POST /api/green-grocer/operations/adjustments` | `GreenGrocerOperations` | `DahiliStokHareketKaydetV2` | `GreenGrocerOperationsUseCase.ExecuteAdjustmentMikroApiAsync` |
| Manav mal kabul ERP aktarimi | `POST /api/kasa-islemleri/manav-mal-kabul-etiket/micro/goods-receipts` | `GreenGrocerGoodsReceipt` | `AlimSatimEvragiKaydetV2` | `CreateMicroGoodsReceiptMikroApiPayload` |
| POS muhasebe fisi | `.../z-raporlari/erpye-gonder`, `.../pos-faturalar/erpye-gonder`, `.../gider-pusulalari/erpye-gonder` | `PosAccountingSlip` | `MuhasebeFisKaydetV2` | `CreateAccountingSlipMikroApiPayload` |
| Urun dagilimi depo siparisleri | `POST /api/operasyon-islemleri/urun-dagilimlari/{documentNo}/kesinlestir` | `ProductDistribution` | `DepolarArasiSiparisKaydetV2` | `ProductDistributionService.CreateWarehouseOrdersMikroApiAsync` |
| AXATA dinamik sayim/hareket | `POST /api/integrations/axata-sync/live/axata/dynamic-census/import` | `AxataDynamicCensus` | `DahiliStokHareketKaydetV2` | `AxataDynamicCensusImportService.ExecuteGroupWithMikroApiAsync` |

Root create alias'lari bulunan ekranlarda `/giden` olmayan eski route da ayni use-case'e
gidebilir. Tabloda yeni UI icin tercih edilen canonical route yazilmistir.

## Ikincil Create Cagrilari

Depolar arasi sevk veya depo iadesinde satir siparise bagli degilse ve otomatik
depo siparisi kurali devredeyse asil hareketten once ikinci bir create yapilir:

```text
DepolarArasiSiparisKaydetV2
  -> olusan ssip_Guid geri okunur
  -> asil sevk/iade payload'inda sth_subesip_uid alanina yazilir
  -> DahiliStokHareketKaydetV2 cagrilir
```

Bu alt akis da `IssuedWarehouseOrder=MikroApi` olmasini gerektirir.

## AXATA Akislarinin Gittigi Create Servisleri

AXATA her hareket icin ayri Mikro payload standardi uretmez. Bazi akislar ana
create servislerini yeniden kullanir:

| AXATA hareketi | Kullanilan Furpa create servisi | Son Mikro metodu |
|---|---|---|
| `C01` depo sevki | `CreateInterWarehouseShipmentUseCase.ExecuteAsync` | `DahiliStokHareketKaydetV2` |
| `C02` firma sevki | `CreateCompanyShipmentUseCase` -> `CompanyMovementWriteService` | `IrsaliyeKaydetV2` |
| `C03` legacy firma hareketi | `CreateCompanyShipmentUseCase` -> `CompanyMovementWriteService` | `IrsaliyeKaydetV2` |
| `G01` firma mal kabul | `CreateCompanyReceivingUseCase.ExecuteAsync` | `IrsaliyeKaydetV2` |
| Dynamic census | `AxataDynamicCensusImportService` kendi payload'ini kurar | `DahiliStokHareketKaydetV2` |
| `G02` depo mal kabul | Yeni evrak create etmez; mevcut sevki kabul eder | `DahiliStokHareketDuzeltV2` |

`C04` legacy transfer akisi ana Mikro API create fabrikalarindan gecmeyen ozel bir
akistir; create payload envanterine dahil edilmemistir.

## Payload Aileleri

### 1. DahiliStokHareketKaydetV2

Kullananlar: depolar arasi sevk, depo iadesi, zayiat, masraf, virman, manav
duzeltmesi ve AXATA dynamic census.

```json
{
  "evraklar": [
    {
      "satirlar": [
        {
          "sth_tarih": "05.10.2026",
          "sth_tip": 2,
          "sth_cins": 6,
          "sth_normal_iade": 0,
          "sth_evraktip": 17,
          "sth_evrakno_seri": "F56",
          "sth_evrakno_sira": 88242,
          "sth_satirno": 0,
          "sth_stok_kod": "015550",
          "sth_miktar": 10,
          "sth_birim_pntr": 1,
          "sth_tutar": 0,
          "sth_giris_depo_no": 60,
          "sth_cikis_depo_no": 56,
          "sth_nakliyedeposu": 110,
          "sth_subesip_uid": null,
          "sth_eticaret_kanal_kodu": "FR..."
        }
      ],
      "evrak_aciklamalari": [
        { "aciklama": "Aciklama" }
      ]
    }
  ]
}
```

Her kullanim tum alanlari gondermez. Sevk/iade fabrikalari nakliye ve siparis
baglantisi alanlarini; zayiat/masraf `sth_isemri_gider_kodu` alanini; virman ise
her is satiri icin giris ve cikis hareketlerini birlikte uretir.

### 2. IrsaliyeKaydetV2

Kullananlar: firma sevki, firma iadesi ve firma mal kabul.

```json
{
  "evraklar": [
    {
      "satirlar": [
        {
          "sth_tarih": "05.10.2026",
          "sth_tip": 1,
          "sth_cins": 1,
          "sth_normal_iade": 0,
          "sth_evraktip": 1,
          "sth_evrakno_seri": "F56",
          "sth_evrakno_sira": 123,
          "sth_satirno": 0,
          "sth_stok_kod": "015550",
          "sth_cari_kodu": "120.01.001",
          "sth_miktar": 10,
          "sth_birim_pntr": 1,
          "sth_tutar": 1000,
          "sth_sip_uid": "siparis-guid-veya-bos",
          "sth_giris_depo_no": 0,
          "sth_cikis_depo_no": 56,
          "sth_eticaret_kanal_kodu": "FR..."
        }
      ],
      "evrak_aciklamalari": [
        { "aciklama": "Aciklama" }
      ]
    }
  ]
}
```

Firma mal kabul ayni payload ailesini kullanir ancak hareket yonu, iade bayragi,
depo ve siparis baglantisi alanlari mal kabul senaryosuna gore uretilir.

### 3. DepolarArasiSiparisKaydetV2

Kullananlar: verilen depo siparisi, urun dagilimi kesinlestirme ve sevk/iade
otomatik siparis alt akisi.

```json
{
  "evraklar": [
    {
      "satirlar": [
        {
          "ssip_tarih": "05.10.2026",
          "ssip_teslim_tarih": "06.10.2026",
          "ssip_evrakno_seri": "F56",
          "ssip_evrakno_sira": 123,
          "ssip_satirno": 0,
          "ssip_stok_kod": "015550",
          "ssip_b_fiyat": 0,
          "ssip_miktar": 10,
          "ssip_tutar": 0,
          "ssip_teslim_miktar": 0,
          "ssip_girdepo": 110,
          "ssip_cikdepo": 56,
          "ssip_birim_pntr": 1
        }
      ]
    }
  ]
}
```

### 4. SiparisKaydetV2

Kullanan: verilen firma siparisi.

```json
{
  "evraklar": [
    {
      "satirlar": [
        {
          "sip_tarih": "05.10.2026",
          "sip_teslim_tarih": "06.10.2026",
          "sip_tip": 0,
          "sip_cins": 0,
          "sip_evrakno_seri": "F56",
          "sip_evrakno_sira": 123,
          "sip_satirno": 0,
          "sip_musteri_kod": "120.01.001",
          "sip_stok_kod": "015550",
          "sip_b_fiyat": 100,
          "sip_miktar": 10,
          "sip_birim_pntr": 1,
          "sip_teslim_miktar": 0,
          "sip_tutar": 1000,
          "sip_depono": 56
        }
      ]
    }
  ]
}
```

### 5. SayimSonuclariKaydetV2

Kullanan: stok sayim sonucu create.

```json
{
  "evraklar": [
    {
      "satirlar": [
        {
          "sym_tarihi": "05.10.2026",
          "sym_depono": 56,
          "sym_Stokkodu": "015550",
          "sym_reyonkodu": "",
          "sym_koridorkodu": "",
          "sym_rafkodu": "",
          "sym_miktar1": 10,
          "sym_miktar2": 0,
          "sym_miktar3": 0,
          "sym_miktar4": 0,
          "sym_miktar5": 0,
          "sym_birim_pntr": 1,
          "sym_barkod": "869...",
          "sym_parti_kodu": "",
          "sym_lot_no": 0,
          "sym_serino": "FR..."
        }
      ]
    }
  ]
}
```

### 6. AlimSatimEvragiKaydetV2

Kullanan: manav mal kabul ve etiket ekraninin Mikro ERP aktarimi.

```json
{
  "evraklar": [
    {
      "cha_tarihi": "05.10.2026",
      "cha_tip": 0,
      "cha_cinsi": 35,
      "cha_normal_Iade": 0,
      "cha_evrak_tip": 0,
      "cha_evrakno_seri": "...",
      "cha_evrakno_sira": 123,
      "cha_kod": "cari-kodu",
      "cha_aratoplam": 1000,
      "cha_aciklama": "...",
      "detay": [
        {
          "sth_stok_kod": "015550",
          "sth_miktar": 10,
          "sth_birim_pntr": 1,
          "sth_tutar": 1000,
          "sth_vergi": 100,
          "sth_giris_depo_no": 56,
          "sth_cikis_depo_no": 0,
          "sth_eticaret_kanal_kodu": "..."
        }
      ]
    }
  ]
}
```

Bu payload digerlerinden farkli olarak cari hareketi `cha_*` baslik alanlarinda,
stok hareketlerini ayni evraktaki `detay[]` altinda tasir.

### 7. MuhasebeFisKaydetV2

Kullanan: POS muhasebe aktarimi.

```json
{
  "evraklar": [
    {
      "satirlar": [
        {
          "fis_tarih": "05.10.2026",
          "fis_tur": 0,
          "fis_hesap_kod": "hesap-kodu",
          "fis_aciklama1": "aciklama",
          "fis_meblag0": 1000,
          "fis_sorumluluk_kodu": "sorumluluk-kodu",
          "fis_tic_evrak_seri": "...",
          "fis_tic_evrak_sira": 123,
          "fis_tic_belgeno": "..."
        }
      ],
      "fis_detay": [
        {
          "mfd_evrak_seri": "...",
          "mfd_evrak_sira": 123,
          "mfd_cariunvan": "...",
          "mfd_carivergidaireno": "...",
          "mfd_kdvtutar": 100,
          "mfd_caritutar": 1000
        }
      ]
    }
  ]
}
```

## Create Olmayan Mikro Yazmalari

Asagidaki metotlar Mikro API'ye POST edilir ancak yeni ana evrak create listesine
dahil degildir:

| Islem | Mikro metodu | Amac |
|---|---|---|
| Depo mal kabul | `DahiliStokHareketDuzeltV2` | Var olan sevk satirlarini kabul edildi olarak gunceller |
| E-irsaliye isaretleme | `DahiliStokHareketDuzeltV2` | FRM/UUID/plaka/teslim bilgilerini mevcut harekete yazar |
| Sevk/iade duzenleme | `DahiliStokHareketDuzeltV2` | Mevcut satirlari gunceller |
| Siparis guncelleme | `SiparisDuzeltV2`, `DepolarArasiSiparisDuzeltV2` | Mevcut siparis satirini gunceller |
| Evrak/satir silme | `*GuidSilV2` | Mevcut kaydi GUID ile siler |
| Fatura referans/marker | `KayitKaydetTopluV2` | Yardimci tabloya insert/update veya isaretleme yapar |
| Mikro evrak duzenleme | Cesitli `DuzeltV2`, `GuidSilV2`, `KayitKaydetTopluV2` | Yonetim ekranindaki duzeltme islemleri |

## Kod Referanslari

- Ortak Mikro zarf: `src/FurpaMerkezApi.Infrastructure/Services/MikroApi/MikroApiAuthBlockFactory.cs`
- HTTP istemcisi ve audit: `src/FurpaMerkezApi.Infrastructure/Services/MikroApi/MikroApiClient.cs`
- Depolar arasi sevk: `src/FurpaMerkezApi.Infrastructure/Modules/SevkIslemleri/DepolarArasiSevkler/Create/CreateInterWarehouseShipmentUseCase.cs`
- Depo iadesi: `src/FurpaMerkezApi.Infrastructure/Modules/IadeIslemleri/DepoIadeleri/Create/CreateWarehouseReturnUseCase.cs`
- Firma sevk/iade: `src/FurpaMerkezApi.Infrastructure/Modules/Common/CompanyMovements/CompanyMovementWriteService.cs`
- Firma mal kabul: `src/FurpaMerkezApi.Infrastructure/Modules/MalKabulIslemleri/MalKabuller/CompanyReceiving/CreateCompanyReceivingUseCase.cs`
- Zayiat/masraf: `src/FurpaMerkezApi.Infrastructure/Modules/StokIslemleri/Common/StockReceiptWriteService.cs`
- Virman: `src/FurpaMerkezApi.Infrastructure/Modules/StokIslemleri/Virmanlar/VirmanWriteService.cs`
- Sayim: `src/FurpaMerkezApi.Infrastructure/Modules/StokIslemleri/Common/InventoryCountWriteService.cs`
- Firma siparisi: `src/FurpaMerkezApi.Infrastructure/Modules/SiparisIslemleri/VerilenFirmaSiparisleri/Create/CreateIssuedCompanyOrderUseCase.cs`
- Depo siparisi: `src/FurpaMerkezApi.Infrastructure/Modules/SiparisIslemleri/VerilenDepoSiparisleri/Create/CreateIssuedWarehouseOrderUseCase.cs`
- Manav mal kabul: `src/FurpaMerkezApi.Infrastructure/Modules/KasaIslemleri/ManavMalKabulVeEtiket/ManavMalKabulVeEtiketService.MikroApi.cs`
- POS muhasebe: `src/FurpaMerkezApi.Infrastructure/Modules/EntegrasyonIslemleri/PosMuhasebeAktarimi/PosMuhasebeAktarimiService.MikroApi.cs`
- AXATA dynamic census: `src/FurpaMerkezApi.Infrastructure/Modules/EntegrasyonIslemleri/AxataSenkronizasyonu/AxataDynamicCensusImportService.cs`
- Urun dagilimi: `src/FurpaMerkezApi.Infrastructure/Modules/OperasyonIslemleri/UrunDagilimlari/ProductDistributionService.cs`

## Onemli Notlar

- Payload tarihleri Mikro'ya genel olarak `dd.MM.yyyy` formatinda gider.
- `clientRequestId` destekleyen stok hareketlerinde deger `FR...` trace anahtarina
  donusturulup genellikle `sth_eticaret_kanal_kodu` alanina yazilir.
- Mikro API HTTP 200 donse bile create tamamlanmis kabul edilmez. Ilgili servisler
  Mikro DB readback ile seri/sira, satir sayisi, stok, miktar, birim ve gerekli
  baglantilari dogrular.
- Timeout, iptal veya belirsiz cevapta yeni `clientRequestId` ile yeni payload
  uretilmemelidir. Ayni payload ve ayni kimlikle recovery/guvenli retry yapilir.
- Bu orneklerde auth degerleri bilerek maskelidir; gercek sifre ve API anahtari
  dokumana veya loga yazilmamalidir.
