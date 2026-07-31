# Manav Kasa Siparis ve Sevk Akisi

Bu dokuman 56 MANAV DEPO icin tasarlanan kasa/koli siparis mantigini,
yeni eklenen ayarlari ve siparis-sevk baglantisinin nasil calisacagini anlatir.

## Kisa Ozet

Amacimiz subenin manav urunlerinde yine bildigi gibi `1 kasa`, `2 kasa`,
`3 koli` gibi giris yapmasi; sistemin bunu Mikro icin anlamli KG/ADET
miktarina cevirebilmesidir.

Bu is iki parcaya ayrildi:

1. Kasa/koli cozumleme:
   Kullanici kasa/koli girer, API bunu tahmini KG/ADET miktarina cevirir.

2. Siparise bagli sevk:
   Sevk sirasinda UI gercek siparis satiri GUID'ini gonderirse, sevk satiri
   Mikro depo siparisi satirina baglanabilir.

Bu iki parca ayri ayri acilip kapatilabilir.

## Neden Boyle Ayrildi?

Canli Mikro gecmisinde manav siparisleri net bir "teslim kapatma" akisi gibi
calismiyordu. Subelerin girdigi miktar pratikte kasa talebi gibi kullaniliyordu.
Gercek sevk ise manav depoda etiket/terazi barkodu ile olusan gercek KG/ADET
miktariyla yapiliyordu.

Bu yuzden yeni mantik tek seferde zorunlu hale getirilmedi. Once kasa/koli
cozumleme eklendi. Siparise bagli sevk ise ayri bir flag ile kontrollu hale
getirildi.

## Yeni Config Ayarlari

Ana config bolumu:

```json
{
  "GreenGrocerProductCases": {
    "Enabled": true,
    "OrderLinkingEnabled": false
  }
}
```

Ortam degiskeni karsiliklari:

```text
GreenGrocerProductCases__Enabled=true|false
GreenGrocerProductCases__OrderLinkingEnabled=true|false
```

### Enabled

`Enabled`, yeni manav kasa/koli cozumleme modulu acik mi sorusunun cevabidir.

`Enabled=true`:

- `product-case-profiles` endpointleri aktif olur.
- `resolution-preview` calisir.
- UI barkod/stok seciminden sonra kasa/koli miktarini KG/ADET'e cozdurebilir.

`Enabled=false`:

- Kasa profil/cozumleme endpointleri `409 Conflict` doner.
- UI bu endpointleri cagirmamali.
- Eski manav siparis/sevk akisi devam eder.
- `OrderLinkingEnabled=true` verilmis olsa bile baglama aktif sayilmaz.

### OrderLinkingEnabled

`OrderLinkingEnabled`, manav sevk satirinin Mikro depo siparisi satirina
baglanip baglanmayacagini kontrol eder.

`OrderLinkingEnabled=false`:

- Varsayilan ve guvenli moddur.
- 56 kaynak depo ve model kodu `10`, `11`, `12` olan manav urunlerinde
  `warehouseOrderLineGuid` gelse bile backend bu GUID'i temizler.
- Sevk siparis satirina baglanmaz.
- Kalan siparis miktari kontrolu uygulanmaz.
- `ssip_teslim_miktar` ve `ssip_kapat_fl` guncellenmez.
- Eski canli davranis korunur.

`OrderLinkingEnabled=true`:

- UI gercek siparis satiri GUID'ini sevk satirinda `warehouseOrderLineGuid`
  olarak gonderirse backend bunu korur.
- Sevk satiri Mikro siparis satirina baglanir.
- Database modunda `STOK_HAREKETLERI_EK.sth_subesip_uid` yazilir.
- Mikro API modunda `DahiliStokHareketKaydetV2` satirina `sth_subesip_uid`
  gonderilir.
- Database modunda backend kalan miktar kontrolu yapar ve
  `ssip_teslim_miktar` / `ssip_kapat_fl` gunceller.
- Bu ayar otomatik depo siparisi uretmez. Sadece UI'nin gonderdigi gercek
  siparis satiri GUID'ini kullanir.

## Config Matrisi

| Enabled | OrderLinkingEnabled | Kasa cozumleme | Sevk-siparis bagi | Davranis |
| --- | --- | --- | --- | --- |
| false | false | Kapali | Kapali | Tam eski akis |
| false | true | Kapali | Kapali | `Enabled=false` oldugu icin baglama da kapali sayilir |
| true | false | Acik | Kapali | Kasa -> KG/ADET hesaplanir, sevk eski mantikta kalir |
| true | true | Acik | Acik | Kasa -> KG/ADET hesaplanir, UI GUID gonderirse sevk siparise baglanir |

## Yeni API Modulu

Route:

```text
api/green-grocer/product-case-profiles
```

Endpointler:

```text
GET    /api/green-grocer/product-case-profiles
GET    /api/green-grocer/product-case-profiles/{stockCode}
PUT    /api/green-grocer/product-case-profiles/{stockCode}
DELETE /api/green-grocer/product-case-profiles/{stockCode}
POST   /api/green-grocer/product-case-profiles/resolution-preview
POST   /api/green-grocer/product-case-profiles/cozumleme-onizleme
```

Yetkiler:

```text
green-grocer.product-case-profiles.list
green-grocer.product-case-profiles.detail
green-grocer.product-case-profiles.update
green-grocer.product-case-profiles.delete
```

## Yeni Tablolar

Uygulama/Auth DB tarafina iki tablo eklendi:

```text
green_grocer_product_case_profiles
green_grocer_order_line_snapshots
```

### green_grocer_product_case_profiles

Stok bazli kasa/koli cozumleme kuralidir.

Bu tablo su sorulari cevaplar:

- Bu urun kasa mi, koli mi, adet mi, kg mi girilecek?
- KG urunde ortalama Furpa etiket gecmisinden mi alinacak?
- Manuel kg/kasa degeri mi kullanilacak?
- ADET urunde koli ici adet nereden gelecek?
- Bu urun siparise baglanabilir mi?
- Manuel onay gerekir mi?
- Ortalama icin minimum veri guveni kac olmali?

### green_grocer_order_line_snapshots

Siparis aninda kullanilan kasa/koli girisi, ortalama/katsayi ve Mikro'ya yazilan
tahmini KG/ADET miktarini sabitler.

UI `resolution-preview` cevabini depo siparisi create satirindaki
`greenGrocerCase` nesnesine tasirsa backend Mikro siparis satiri olustuktan sonra
satir GUID'i ile snapshot yazar. Boylece sonradan ortalama degisse bile manav
depo gelen sipariste ve GreenGrocer raporlarinda "3 kasa ~= 11.25 KG" bilgisi
aynen gorulebilir.

## Profil Alanlari

### inputMode

Kullanicinin UI'da hangi tip miktar girdigini anlatir.

```text
Case      kasa girisi
Pack      koli/paket girisi
Piece     direkt adet girisi
KgDirect  direkt kg girisi
Sarf      kasa/ambalaj/sarf malzemesi
```

### conversionMode

Girilen miktarin Mikro ana miktarina nasil cevrilecegini anlatir.

```text
LabelAverageKgPerCase  Furpa etiket gecmisinden kg/kasa ortalamasi
ManualKgPerCase        profil uzerindeki manuel kg/kasa
FixedUnitsPerCase      Mikro birim2 katsayisi veya manuel adet/koli katsayisi
DirectQuantity         girilen miktari direkt Mikro ana birimine yaz
ManualOnly             otomatik hesaplama yok, manuel karar gerekli
Blocked                urun manav kasa siparisinde engelli
```

### manualKgPerCase

`ManualKgPerCase` modunda kullanilir.

Ornek:

```text
3 kasa * 12.5 kg/kasa = 37.5 KG
```

### manualUnitsPerCase

`FixedUnitsPerCase` modunda kullanilir.

Ornek:

```text
3 koli * 25 adet/koli = 75 ADET
```

Bu alan bos birakilirsa backend Mikro `STOKLAR.sto_birim2_katsayi` degerini
kullanabilir.

### averageWindowDays

Furpa etiket gecmisinden ortalama alirken kac gun geriye bakilacagini belirtir.
Varsayilan deger `30` gundur.

### minAverageRecordCount

Ortalamanin guvenilir sayilmasi icin minimum etiket/kayit sayisidir.

### minAverageCaseCount

Ortalamanin guvenilir sayilmasi icin minimum toplam kasa sayisidir.

### maxCoefficientOfVariation

Kasa agirliklari cok daginiksa sistem `confidence=Medium` uretir.
Varsayilan deger `0.25` olarak ayarlandi.

### requiresManualApproval

UI'nin satiri eklerken kullaniciya manuel onay gostermesi gerekip gerekmedigini
anlatir.

### allowOrderLinking

Profil bazinda bu urun siparise baglanabilir mi sorusunun cevabidir.

Tek basina yeterli degildir. `resolution-preview` response'unda
`isOrderLinkable=true` gelebilmesi icin hem profil `allowOrderLinking=true`
olmali hem de global `GreenGrocerProductCases:OrderLinkingEnabled=true`
olmalidir.

### overDeliveryTolerancePercent

Ileride siparis-sevk toleransini yonetmek icin profil uzerinde tutulur.
Mevcut sevk validasyonu simdilik Mikro siparis satirindaki kalan miktar
kontrolunu kullanir.

## Resolution Preview Nasil Calisir?

UI stok/barkod secip kullanicinin girdigi kasa/koli miktarini backend'e yollar.

Request:

```json
{
  "stockCode": "001082",
  "inputQuantity": 3,
  "sourceWarehouseNo": 56,
  "targetWarehouseNo": 110,
  "orderDate": "2026-07-31T00:00:00"
}
```

Backend su islemleri yapar:

1. `Enabled=true` mi kontrol eder.
2. Kaynak depo `56` mi kontrol eder.
3. Mikro `STOKLAR` tablosundan stok kartini okur.
4. Stok model kodu `10`, `11`, `12`, `23` icinde mi kontrol eder.
5. Aktif profil varsa profil kuralini kullanir.
6. Profil yoksa otomatik karar verir:
   - Model `23`: sarf/kasa malzemesi gibi davranir.
   - Ana birim `ADET` ve birim2 katsayisi varsa koli/adet cevrimi yapar.
   - Ana birim `ADET` ama katsayi yoksa direkt adet kabul eder.
   - Ana birim `KG` ise Furpa etiket gecmisinden kg/kasa ortalamasi hesaplar.
7. Sonucta UI'ye `estimatedQuantity`, `confidence`, `warnings`, `errors`,
   `isUsable`, `isOrderLinkable` alanlarini dondurur.

Ornek response:

```json
{
  "stockCode": "001082",
  "stockName": "MNV SEFTALI KG",
  "modelCode": "10",
  "modelName": "Meyve",
  "unit1": "KG",
  "inputQuantity": 3,
  "inputMode": "Case",
  "conversionMode": "LabelAverageKgPerCase",
  "microUnit": "KG",
  "estimatedQuantity": 11.25,
  "averageKgPerCase": 3.75,
  "averageSource": "LabelHistory",
  "confidence": "High",
  "requiresManualApproval": false,
  "isOrderLinkable": true,
  "isUsable": true,
  "warnings": [],
  "errors": []
}
```

Not: `isOrderLinkable=true` ancak `OrderLinkingEnabled=true` ise gelebilir.
Default config'te bu alan false gelir.

## Furpa Etiket Gecmisi Nasil Kullaniliyor?

KG urunlerde ortalama kasa agirligi Furpa DB uzerinden hesaplanir.

Kaynak tablo:

```text
Furpa.dbo.Manav_Depo_Mal_Kabul_Etiket
```

Kullanilan ana alanlar:

```text
Stok_Kod
Kasa_Sayisi
[Alınan_Net_Miktar]
Olusturma_Tarihi
```

Mantik:

```text
kg/kasa = [Alınan_Net_Miktar] / Kasa_Sayisi
```

Sonra secilen gun araliginda ortalama, toplam kasa sayisi, kayit sayisi,
standart sapma ve varyasyon katsayisi hesaplanir.

Veri azsa veya agirliklar cok daginiksa response hata vermek yerine
`confidence=Medium` ve uyari dondurebilir. Hic ortalama yoksa `isUsable=false`
olur ve UI satir ekletmemelidir.

## Siparis Akisi Nasil Olmali?

Ideal UI akisi:

1. Kullanici manav siparis ekraninda stok/barkod secer.
2. Kullanici `3 kasa` gibi giris yapar.
3. UI `resolution-preview` endpointini cagirir.
4. Backend `3 kasa ~= 11.25 KG` gibi sonuc dondurur.
5. UI kullaniciya hem kasa girisini hem tahmini KG/ADET sonucunu gosterir.
6. `isUsable=false` ise satir ekletmez.
7. `confidence=Medium` veya `requiresManualApproval=true` ise uyari/onay gosterir.
8. Siparis satirina Mikro miktari olarak `estimatedQuantity` yazilir.
9. Manav siparisi `outWarehouseNo=56` ile kaydediliyorsa UI ayni cozumleme
   cevabini satirdaki `greenGrocerCase` nesnesine tasir.

Bu sayede sube yine kasa mantiginda calisir; Mikro tarafinda ise KG/ADET daha
anlamli durur.

Manav siparisi create satiri ornegi:

```json
{
  "stockCode": "001082",
  "quantity": 11.25,
  "unitPointer": 1,
  "description": "3 kasa",
  "greenGrocerCase": {
    "inputQuantity": 3,
    "inputMode": "Case",
    "conversionMode": "LabelAverageKgPerCase",
    "microUnit": "KG",
    "estimatedQuantity": 11.25,
    "averageKgPerCase": 3.75,
    "unitsPerCase": null,
    "averageSource": "LabelHistory",
    "averageRecordCount": 47,
    "averageCaseCount": 7526,
    "coefficientOfVariation": 0.08,
    "confidence": "High"
  }
}
```

Burada `quantity` Mikro'ya yazilacak degerdir. `greenGrocerCase.inputQuantity`
ise kullanicinin girdigi kasa/koli miktaridir.

## Sevk Akisi Nasil Olmali?

Manav depo sevkte gercek hareket miktari etiket/terazi barkodundan gelen
gercek KG/ADET miktaridir.

### OrderLinkingEnabled=false

Bu eski mantiktir.

1. UI sevk satiri gonderir.
2. Satirda `warehouseOrderLineGuid` olsa bile backend 56 depo + model `10/11/12`
   icin bu GUID'i temizler.
3. Sevk siparise baglanmaz.
4. Kalan siparis miktari kontrolu yoktur.
5. Teslim miktari guncellenmez.
6. UI manav sevkte siparis secme, kalan siparis kapatma ve
   `warehouseOrderLineGuid` tasima akisini gostermemelidir. Gelen siparis detayi
   ve rapordaki `greenGrocerCase` bilgisi sadece bilgilendirme icindir.

### OrderLinkingEnabled=true

Bu yeni kontrollu baglama modudur.

1. UI manav depo gelen siparis detayinda `items[].greenGrocerCase` dolu satirlari
   "3 kasa ~= 11.25 KG" gibi gosterir.
2. UI sevk satirinda gercek siparis satiri GUID'ini `warehouseOrderLineGuid`
   olarak gonderir.
3. Backend GUID'i temizlemez.
4. Backend siparis satirini bulur.
5. Kaynak depo, hedef depo ve stok kodu eslesmesini kontrol eder.
6. Siparis satiri kapali mi kontrol eder.
7. Sevk miktari kalan miktardan fazla mi kontrol eder.
8. Hareket olusurken siparis satiri GUID'i sevk hareketine baglanir.
9. Database modunda `ssip_teslim_miktar` ve `ssip_kapat_fl` guncellenir.

Onemli not: Bu modda sevk miktari yine gercek KG/ADET olmalidir. Kasa sayisi
degil, okutulan/olculen gercek miktar gonderilmelidir.

## Otomatik Depo Siparisi Uretimi

Mevcut degisiklik manav icin otomatik depo siparisi uretmez.

Yani `OrderLinkingEnabled=true` su an sadece su anlama gelir:

```text
UI gercek siparis satiri GUID'ini gonderirse onu koru ve sevke bagla.
```

Su anlama gelmez:

```text
GUID yoksa otomatik manav siparisi olustur.
```

Bu ayrim bilerek yapildi. Otomatik siparis uretimi daha riskli oldugu icin
ayri bir karar ve ayri test ister.

## UI Icin Net Kurallar

- `Enabled=false` ise yeni kasa profil ekranini gizle.
- `Enabled=false` ise `resolution-preview` cagirma.
- `Enabled=true` ise siparis satiri eklemeden once `resolution-preview` cagir.
- `isUsable=false` ise satir ekletme.
- `errors.first` kullaniciya net hata olarak gosterilebilir.
- `warnings.first` uyari olarak gosterilebilir.
- `confidence=Medium` ise kullanicidan onay istenebilir.
- `estimatedQuantity` Mikro'ya yazilacak KG/ADET miktaridir.
- `inputQuantity` kullanicinin girdigi kasa/koli/adet degeridir.
- Siparis create satirinda `greenGrocerCase` gonderilirse gelen siparis detayi ve
  GreenGrocer raporlari ayni kasa/ortalama bilgisini geri dondurur.
- `OrderLinkingEnabled=false` ise manav sevkte `warehouseOrderLineGuid` gonderme.
- `OrderLinkingEnabled=true` ve `isOrderLinkable=true` ise sevkte ilgili
  siparis satirinin `lineGuid` degerini `warehouseOrderLineGuid` olarak gonder.

## Backend Icin Net Kurallar

- Kasa profil/cozumleme modulu kapaliysa endpointler 409 doner.
- `OrderLinkingEnabled=false` iken 56 depo manav urunlerinde siparis GUID'i
  temizlenir.
- `OrderLinkingEnabled=true` iken GUID korunur ama sadece UI gonderirse kullanilir.
- 56 depo icin otomatik depo siparisi uretimi kapali kalir.
- `outWarehouseNo=56` olan depo siparislerinde satir `greenGrocerCase` bilgisi
  gelirse backend `green_grocer_order_line_snapshots` kaydi yazar.
- Depo siparis detayi ve GreenGrocer raporlari snapshot varsa `greenGrocerCase`
  / `caseInfo` alanlarini dondurur.
- Siparis satiri baglama acildiginda mevcut standart kontroller calisir:
  kaynak depo, hedef depo, stok kodu, kapali satir ve kalan miktar.

## Kodda Degisen Ana Yerler

Application:

```text
src/FurpaMerkezApi.Application/Modules/GreenGrocer/ProductCases
src/FurpaMerkezApi.Application/Security/PermissionCatalog.cs
```

Domain:

```text
src/FurpaMerkezApi.Domain/Entities/GreenGrocerProductCaseProfile.cs
src/FurpaMerkezApi.Domain/Entities/GreenGrocerOrderLineSnapshot.cs
```

Infrastructure:

```text
src/FurpaMerkezApi.Infrastructure/Modules/GreenGrocer/ProductCases
src/FurpaMerkezApi.Infrastructure/Modules/GreenGrocer/Reports
src/FurpaMerkezApi.Infrastructure/Modules/SiparisIslemleri/Common/WarehouseOrderDetailQueryExecutor.cs
src/FurpaMerkezApi.Infrastructure/Modules/SiparisIslemleri/VerilenDepoSiparisleri/Create/CreateIssuedWarehouseOrderUseCase.cs
src/FurpaMerkezApi.Infrastructure/Modules/SevkIslemleri/DepolarArasiSevkler/Create/GreenGrocerShipmentLineNormalizer.cs
src/FurpaMerkezApi.Infrastructure/Modules/SevkIslemleri/DepolarArasiSevkler/Create/CreateInterWarehouseShipmentUseCase.cs
src/FurpaMerkezApi.Infrastructure/Persistence/Configurations/GreenGrocerProductCaseProfileConfiguration.cs
src/FurpaMerkezApi.Infrastructure/Persistence/Configurations/GreenGrocerOrderLineSnapshotConfiguration.cs
src/FurpaMerkezApi.Infrastructure/Migrations/20260731061750_AddGreenGrocerProductCaseProfiles.cs
```

WebApi:

```text
src/FurpaMerkezApi.WebApi/Controllers/Modules/GreenGrocer/ProductCases
src/FurpaMerkezApi.WebApi/appsettings.json
```

Tests:

```text
tests/FurpaMerkezApi.Infrastructure.Tests/Modules/SevkIslemleri/DepolarArasiSevkler/GreenGrocerShipmentLineNormalizerTests.cs
```

Docs:

```text
docs/UI_API_DOKUMANI.md
docs/MANAV_KASA_SIPARIS_SEVK_AKISI.md
```

## Riskler ve Dikkat Edilecekler

1. Ortalama yoksa otomatik KG hesaplamak dogru degildir.
   Bu durumda profil manuel tanimlanmali veya UI satir ekletmemelidir.

2. `OrderLinkingEnabled=true` acilirsa sevk miktari kalan siparis miktarindan
   fazla olamaz. Bu eski serbest sevk davranisindan farklidir.

3. Siparis miktari ortalama KG ile olusturulup sevk gercek KG ile yapildigi icin
   tolerans ihtiyaci dogabilir. Profilde `overDeliveryTolerancePercent` alanlari
   bunun icin hazir durur.

4. Snapshot yazimi Mikro siparisi olustuktan sonra Auth DB'ye yazilir. Mikro
   siparis basarili olup snapshot yazimi teknik bir sebeple basarisiz kalirsa
   siparis kaydi geri alinmaz; log uzerinden takip edilmelidir.

5. Mikro API modunda teslim miktari/kapatma etkisi Mikro API tarafinin
   davranisina baglidir. Database modunda backend kendisi gunceller.

## Onerilen Devreye Alma Plani

1. `Enabled=true`, `OrderLinkingEnabled=false` ile basla.
2. UI sipariste `resolution-preview` kullanmaya baslasin.
3. Kritik KG urunler icin profil tanimla.
4. Raporlarda "sube kasa talebi" ve "tahmini KG/ADET" ayrimini dogrula.
5. Bir veya iki sube ile `OrderLinkingEnabled=true` pilot yap.
6. Sevkte kalan miktar/kapatma davranisini canli veriyle kontrol et.
7. Tolerans, pilot sube kapsami ve sevk kapatma kurallarina gore sonraki fazi
   netlestir.

## Kapsamli Commit Onerisi

Baslik:

```text
feat(green-grocer): add configurable case resolution and order-linked shipment flow
```

Detayli commit mesaji:

```text
feat(green-grocer): add configurable case resolution and order-linked shipment flow

- add GreenGrocer product case profile domain model, DTOs, service and controller
- add Auth DB migration for product case profiles and order line snapshots
- calculate KG-per-case averages from Furpa manav label history
- support manual kg/case, fixed units/case, direct quantity, manual-only and blocked conversion modes
- add GreenGrocerProductCases.Enabled feature flag for disabling the new resolution API
- add GreenGrocerProductCases.OrderLinkingEnabled flag for controlled manav shipment order linking
- keep legacy manav shipment behavior by default by detaching order GUIDs when linking is disabled
- preserve warehouseOrderLineGuid and use existing shipment/order validation when linking is enabled
- document UI request/response contracts and operational rollout rules
- cover manav shipment link detach/keep behavior with tests
```
