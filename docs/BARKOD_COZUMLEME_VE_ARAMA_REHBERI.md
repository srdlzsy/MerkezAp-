# Barkod Cozumleme, Okutma Ve Urun Arama Rehberi

Bu dokuman mobil terminal ve UI ekranlarinda barkod okutma, urun arama, fiyat
gorme, cari bulma ve barkod tanimlama ihtiyaci dogdugunda hangi mantigin
kullanildigini tek yerde anlatir.

Kisa karar:

- Kamera veya fiziksel okuyucu ile barkod okutulan satir ekleme ekranlarinda
  ana endpoint `GET /api/arama-islemleri/barkodlar/{barcode}/cozumle` olmalidir.
- Klavyeden urun adi, stok kodu veya barkod aranan liste ekranlarinda
  `GET /api/arama-islemleri/urunler` kullanilir.
- Fiyat gorme menusu icin `GET /api/arama-islemleri/fiyat-gor` veya barkod
  kisayolu `GET /api/arama-islemleri/barkodlar/{barcode}/fiyat` kullanilir.
- Barkoddan cari/tedarikci onerisi icin `GET /api/arama-islemleri/cari-bul`
  veya `GET /api/arama-islemleri/barkodlar/{barcode}/cariler` kullanilir.
- Barkod tanimi ekleme/silme/guncelleme icin bu projede su an acik bir mobil
  yazma endpoint'i yoktur. Mevcut barkod akislari Mikro tarafindan okuma yapar.

## Ana Parcalar

| Parca | Gorev |
| --- | --- |
| `AramaIslemleriController` | HTTP route, auth, query parametre baglantisi. |
| `ResolveBarcodeUseCase` | Tek barkodu urune cevirir ve islem kararini verir. |
| `SearchProductsUseCase` | Urun/fiyat liste aramalarini Mikro proseduruyle yapar. |
| `BarcodeLookupNormalizer` | 27/29 terazi barkodunu okutulan deger ve arama degeri olarak ayirir. |
| `BarcodeResolutionDto` | UI'in satira ekleme kararinda okuyacagi tek cevap modeli. |
| `BARKOD_TANIMLARI` | Barkoddan stok koduna ana eslesme kaynagi. |
| `STOKLAR` | Stok adi, model kodu, birimler ve stok karti bloklari. |
| `STOK_DEPO_DETAYLARI` | Depo ozel blok ve sorumlu/tedarikci bilgileri. |
| `DEPOLAR.dep_barkod_yazici_yolu` | Depo model kod listesi olarak kullanilan alan. |
| `SATINALMA_SARTLARI` | Tedarikci/satinalma sarti kontrol kaynagi. |

Ilgili kodlar:

- `src/FurpaMerkezApi.WebApi/Controllers/Modules/AramaIslemleri/AramaIslemleriController.cs`
- `src/FurpaMerkezApi.Infrastructure/Modules/AramaIslemleri/ResolveBarcode/ResolveBarcodeUseCase.cs`
- `src/FurpaMerkezApi.Infrastructure/Modules/AramaIslemleri/SearchProducts/SearchProductsUseCase.cs`
- `src/FurpaMerkezApi.Application/Modules/AramaIslemleri/Common/BarcodeLookupNormalizer.cs`

## Endpoint Haritasi

| Senaryo | Endpoint | Not |
| --- | --- | --- |
| Tek barkod okutma ve satira ekleme karari | `GET /api/arama-islemleri/barkodlar/{barcode}/cozumle` | En kritik endpoint. UI barkodu tahmin etmez, aynen gonderir. |
| Genel urun arama | `GET /api/arama-islemleri/urunler` | Barkod, stok kodu, stok adi, firma/tedarikci filtresi alir. |
| Fiyat gorme | `GET /api/arama-islemleri/fiyat-gor` | `arama-islemleri.fiyat-gor.list` yetkisi ister. |
| Barkodla fiyat gorme | `GET /api/arama-islemleri/barkodlar/{barcode}/fiyat` | Fiyat gorme icin path alias. |
| Barkodla cari bulma | `GET /api/arama-islemleri/cari-bul` | `arama-islemleri.cari-bul.list` yetkisi ister. |
| Barkodla cari bulma alias | `GET /api/arama-islemleri/barkodlar/{barcode}/cariler` | Ayni cari bulma akisini path ile calistirir. |
| Stoktan cari onerileri | `GET /api/arama-islemleri/urunler/{stockCode}/cari-onerileri` | Mal kabul/siparis ekranlarinda yardimci olabilir. |
| Offline katalog | `GET /api/mobile-sync/urun-fiyat-katalogu` | Offline okutma icin depo bazli local katalog besler. |

## Tek Barkod Cozumleme

Kamera veya el terminali barkod okuttugunda temel cagri su sekildedir:

```http
GET /api/arama-islemleri/barkodlar/2700174041103/cozumle?warehouseNo=110&operationType=shipment&targetWarehouseNo=120
Authorization: Bearer {token}
```

Query alanlari:

| Alan | Anlam |
| --- | --- |
| `warehouseNo` | Kaynak depo. Verilmezse JWT icindeki depo kullanilir. |
| `operationType` | Islem tipi. Satira ekleme kararinda kullanilir. |
| `screenCode` | Eski UI uyumu. `operationType` bos ise ekran baglami olarak okunur. |
| `targetWarehouseNo` | Hedef depo. Model kod uygunlugu hesaplanabilir. |
| `supplierCode` | Secili tedarikci. Satinalma sarti kontrolunde kullanilir. |
| `companyCode` | `supplierCode` ile ayni anlamda geriye uyum alias'i. |
| `isRefund` | `false` ise iade disi DLS/99 filtresi uygulanir. |

Desteklenen tipik islem tipleri:

| Gelen degerler | Normalize edilen tip |
| --- | --- |
| `firma-mal-kabulleri`, `depo-mal-kabulleri`, `receiving` | `receiving` |
| `verilen-firma-siparisleri`, `verilen-depo-siparisleri`, `order` | `order` |
| `giden-firma-sevkleri`, `giden-depolar-arasi-sevkler`, `shipment`, `dispatch` | `shipment` |
| `firma-iadeleri`, `giden-depo-iadeleri`, `return` | `return` |
| `zayiat-fisleri`, `masraf-fisleri`, `fire`, `waste` | `waste` |
| `sayim-sonuclari`, `count` | `count` |

## Cozumleme Sirasi

`ResolveBarcodeUseCase` tek barkodu su sirayla cozer:

1. Barkod trim edilir.
2. Barkod 13 haneli EAN-13 ise check digit kontrol edilir.
3. Barkod `27` veya `29` ile baslayan 13 haneli terazi barkoduysa:
   - `lookupBarcode` ilk 7 hane olur.
   - 8-12. haneler `embeddedQuantity` olarak KG miktarina cevrilir.
   - Ornek: `2700174041103` icin `lookupBarcode = 2700174`,
     `embeddedQuantity = 4.11`, `embeddedQuantityUnit = KG`.
4. Once `BARKOD_TANIMLARI.bar_kodu = lookupBarcode` exact aranir.
5. Terazi barkodunda bulunamazsa orijinal barkodla tekrar denenir.
6. Barkod tablosunda bulunamazsa `STOKLAR.sto_kod` veya
   `STOKLAR.sto_kuresel_urun_numarasi` ile eslesme denenir.
7. Stok bulunursa `STOKLAR` ve varsa `STOK_DEPO_DETAYLARI` bilgileri okunur.
8. Stokun tum aktif barkodlari okunur; primary, koli/master ve alternatif
   barkod bilgileri hesaplanir.
9. Hedef depo verilirse `DEPOLAR.dep_barkod_yazici_yolu` model kod listesi
   okunur ve `STOKLAR.sto_model_kodu` ile karsilastirilir.
10. Tedarikci veya ilgili islem tipi varsa `SATINALMA_SARTLARI` kontrol edilir.
11. Depo fiyat bilgisi bulunur.
12. `isUsableInOperation`, `operationDecision`, `warnings` ve `errors`
    alanlari uretilir.

## Response Alanlari Nasil Okunur

UI icin en onemli alanlar:

| Alan | UI'daki anlami |
| --- | --- |
| `isFound` | Urun/stok eslesmesi bulundu mu? |
| `stockCode`, `stockName` | Satira yazilacak ana urun bilgisi. |
| `matchedBarcode` | Sistemde eslesen barkod. |
| `lookupBarcode` | API'nin arama icin kullandigi barkod. Terazide ilk 7 hane olabilir. |
| `barcodeKind` | `product`, `case`, `alternative`, `variable-weight`, `stock-code`, `gtin` gibi pratik tip. |
| `isVariableWeightBarcode` | Terazi barkodu mu? |
| `embeddedQuantity` | Terazi barkodundaki KG miktari. |
| `isCaseBarcode` | Koli/master barkod mu? |
| `matchedUnitsPerCase` | Okutulan barkod koli ise koli ici miktar. |
| `isPassive` | Urun pasif mi? Pasif urun islemde bloklanir. |
| `isSalesBlocked` | Satis/sevk cikisi icin blok bilgisi. Sevkte tek basina engel degildir. |
| `isOrderBlocked` | Siparis icin blok bilgisi. |
| `isGoodsAcceptanceBlocked` | Mal kabul icin blok bilgisi. |
| `isAllowedForTargetWarehouse` | Hedef depo model kod uygunlugu. Her zaman blok anlamina gelmez. |
| `hasPurchaseRequirement` | Satinalma sarti sonucu. Her zaman blok anlamina gelmez. |
| `salesPrice`, `priceTypeCode` | Secili depodaki fiyat bilgisi. |
| `isUsableInOperation` | Satira ekleme icin ana karar alani. |
| `operationDecision` | Kararin kullaniciya gosterilebilir aciklamasi. |
| `warnings` | Kullaniciya engel olmayan uyarilar. |
| `errors` | Engelleyici hatalar. |

UI hata onceligi:

1. `isFound = false` ise satir ekleme yapilmaz, "urun bulunamadi" gosterilir.
2. `isUsableInOperation = false` ise satir ekleme yapilmaz.
3. Mesaj icin once `errors.first`, yoksa `operationDecision` kullanilir.
4. Islem basarili ama `warnings` doluysa ilk uyari bilgi olarak gosterilebilir.

## Islem Bazli Karar Kurallari

Bugunku API karar mantigi:

| Islem | Blok kurali |
| --- | --- |
| Tum islemler | `isPassive = true` ise urun kullanilamaz. |
| Tum islemler | `isRefund = false` ve urun `DLS/99` ise urun kullanilamaz. |
| `receiving` | Mal kabul bloklu ise kullanilamaz. |
| `order` | Siparis bloklu ise kullanilamaz. |
| `shipment` | Satis/sevk blok bilgisi bilgi olarak doner; pasif/DLS disinda satira eklemeyi bloklamaz. |
| `return`, `waste` | Satis/sevk cikis bloklu ise kullanilamaz. |
| `count` | Pasif/DLS disinda ozel blok uygulanmaz. |
| Bilinmeyen islem tipi | Ozel kural yoksa genel bilgiler doner. |

Hedef depo/model kodu:

- Hedef depo verilirse `isAllowedForTargetWarehouse`,
  `targetWarehouseReason`, `productModelCode` ve `targetWarehouseModelCodes`
  alanlari hesaplanabilir.
- `shipment` yani depolar/subeler arasi sevk akisi icin hedef depo model kodu
  bloklayici degildir. Eski terminal davranisina uyum icin sevkte hedef depo
  barkod aramayi durdurmaz.
- Diger operasyonlarda hedef depo sonucu karar motoruna dahil edilir.

Satinalma sarti:

- `receiving` ve `order` icin satinalma sarti kontrol edilir ve gerekiyorsa
  bloklayici olur.
- `supplierCode` verilirse diger operasyonlarda da bilgi amacli kontrol
  yapilabilir.
- `shipment` icin sirf `targetWarehouseNo` geldi diye satinalma sarti kontrolu
  calismaz ve blok uretmez.

## Ekran Bazli Onerilen Kullanim

### Depolar Arasi Sevk

Frontend barkodu aynen cozumleme endpoint'ine gonderir:

```http
GET /api/arama-islemleri/barkodlar/{barcode}/cozumle?operationType=shipment&warehouseNo={kaynakDepo}&targetWarehouseNo={hedefDepo}
```

Kural:

- Hedef depo model kodu hesaplanabilir ama sevkte bloklayici degildir.
- `isSalesBlocked = true` sevkte bilgi/uyari olarak okunur; tek basina satira
  ekleme engeli degildir.
- Satira ekleme karari `isUsableInOperation` ile verilir.
- Terazi barkoduysa miktar icin `embeddedQuantity` onerilebilir.
- Koli barkoduysa miktar icin `matchedUnitsPerCase` onerilebilir.

### Depo Siparisi

```http
GET /api/arama-islemleri/barkodlar/{barcode}/cozumle?operationType=order&warehouseNo={kaynakDepo}&targetWarehouseNo={hedefDepo}
```

Kural:

- Siparis bloklari dikkate alinir.
- Hedef depo/model kod uygunlugu karar motoruna dahil edilir.
- Satinalma sarti `order` icin karar motoruna dahil edilir.

### Firma Siparisi

```http
GET /api/arama-islemleri/barkodlar/{barcode}/cozumle?operationType=order&warehouseNo={depo}&supplierCode={firma}
```

Kural:

- Tedarikciye gore `SATINALMA_SARTLARI` kontrol edilir.
- `hasPurchaseRequirement = false` ise order icin blok uretilebilir.
- Siparis bloklari dikkate alinir.

### Firma Mal Kabul

```http
GET /api/arama-islemleri/barkodlar/{barcode}/cozumle?operationType=receiving&warehouseNo={depo}&supplierCode={firma}
```

Kural:

- Mal kabul bloklari dikkate alinir.
- Tedarikci satinalma sarti karar motoruna dahil edilir.
- Terazi/koli bilgisi satir miktari icin kullanilabilir.

### Iade, Fire, Sayim

Iade:

```http
GET /api/arama-islemleri/barkodlar/{barcode}/cozumle?operationType=return&warehouseNo={depo}&isRefund=true
```

Fire:

```http
GET /api/arama-islemleri/barkodlar/{barcode}/cozumle?operationType=waste&warehouseNo={depo}
```

Sayim:

```http
GET /api/arama-islemleri/barkodlar/{barcode}/cozumle?operationType=count&warehouseNo={depo}
```

Kural:

- Iade/fire cikis mantiginda satis/sevk bloklari dikkate alinir.
- Sayimda pasif/DLS disinda ozel operasyon blogu yoktur.

## Urun Arama Ne Zaman Kullanilir

`GET /api/arama-islemleri/urunler` liste/arama deneyimi icindir.

Ornekler:

```http
GET /api/arama-islemleri/urunler?warehouseNo=110&stockName=sut&take=20
GET /api/arama-islemleri/urunler?warehouseNo=110&stockCode=015550
GET /api/arama-islemleri/urunler?warehouseNo=110&barcode=8690000000000
GET /api/arama-islemleri/urunler?warehouseNo=110&companyCode=120.01.03106&stockName=sut
```

Kurallar:

- `barcode`, `stockCode`, `stockName`, `companyCode` veya `supplierCode`
  alanlarindan en az biri verilmelidir.
- `stockName` en az 2 karakter olmalidir.
- `take` default 20, max 100 olur.
- Barkod 27/29 terazi barkoduysa burada da `lookupBarcode` ilk 7 haneye
  normalize edilir.
- Bu endpoint `dbo.__StokveFiyatArama_Gokhan` prosedurunu kullanir.
- Liste icin uygundur, ama satira ekleme karari icin son kontrol yine
  `barkodlar/{barcode}/cozumle` olmalidir.

## Fiyat Gorme

Fiyat gorme menusu arama islemleri altinda ayri yetkili bir hizli ekrandir.

```http
GET /api/arama-islemleri/fiyat-gor?warehouseNo=110&barcode=8690000000000
GET /api/arama-islemleri/barkodlar/8690000000000/fiyat?warehouseNo=110
```

Yetki:

```text
arama-islemleri.fiyat-gor.list
```

Notlar:

- Response modeli `ProductLookupItemDto[]` ile urun arama response'u aynidir.
- UI fiyat ekraninda `price`, `priceTypeCode`, `unitName`, `barcode`,
  `stockCode` ve `stockName` one cikarilabilir.
- Offline terminal fiyat okutma icin online endpoint yerine
  `GET /api/mobile-sync/urun-fiyat-katalogu` katalogu kullanilmalidir.

## Cari Bulma

Barkod okutulup urunun varsayilan veya gecmiste kullanilmis carileri
onerilecekse:

```http
GET /api/arama-islemleri/cari-bul?barcode=8690000000000&warehouseNo=110
GET /api/arama-islemleri/barkodlar/8690000000000/cariler?warehouseNo=110
```

Yetki:

```text
arama-islemleri.cari-bul.list
```

Akis:

1. Backend once barkodu `ResolveBarcodeUseCase` ile urune cozer.
2. Urun bulunursa `urunler/{stockCode}/cari-onerileri` mantigi calisir.
3. Varsayilan tedarikci ve yakin gecmis stok hareketlerinden cari onerileri
   doner.

## Offline Okutma

Offline terminalde anlik barkod okutma API'ye gitmeden local katalogdan
cozulmelidir.

Katalog kaynagi:

```http
GET /api/mobile-sync/urun-fiyat-katalogu?warehouseNo=110
```

Mobil uygulama local DB'de en az su anahtarlarla arama yapmalidir:

- `barcode + warehouseNo`
- Terazi barkodu icin `lookupBarcode + warehouseNo`
- Gerekirse `stockCode + warehouseNo`

Offline davranista da online endpoint ile ayni UI prensibi korunmalidir:

- Terazi barkodunda miktar barkoddan okunur.
- Koli barkodunda koli ici adet onerilebilir.
- Urun bulunamazsa satira ekleme yapilmaz.

## Barkod Ekletme Veya Tanimlama

Su an bu projede barkod tanimi eklemek, silmek veya guncellemek icin acik bir
mobil/API endpoint'i yoktur.

Mevcut durum:

- `AramaIslemleri` barkodlari sadece okur.
- Ana kaynak Mikro `BARKOD_TANIMLARI` tablosudur.
- `BARKOD_TANIMLARI.bar_kodu` Mikro modelinde unique index'e sahiptir.
- Barkod ekleme dogrudan arama endpoint'lerinin sorumlulugu degildir.

Ileride barkod ekletme istenirse onerilen guvenli akis:

1. Kullanici bilinmeyen barkodu okutur.
2. UI `cozumle` endpoint'inden `isFound = false` alir.
3. UI "barkod tanimlama talebi" baslatir.
4. Kullanici urunu arama ekranindan secerek barkodu hangi stoga baglayacagini
   belirler.
5. API once `BARKOD_TANIMLARI.bar_kodu` icin duplicate kontrolu yapar.
6. Stok kodu ve birim pointer dogrulanir.
7. Yetki kontrolu yapilir.
8. Talep audit bilgisiyle kaydedilir veya yetkili kullanici direkt Mikro
   yazma akisini calistirir.

Onerilen yeni endpoint taslagi:

```http
POST /api/stok-islemleri/barkod-tanimlari/talepler
```

Ornek body:

```json
{
  "barcode": "8690000000000",
  "stockCode": "015550",
  "unitPointer": 1,
  "isCaseBarcode": false,
  "warehouseNo": 110,
  "note": "Terminalden okutuldu, sistemde tanimsiz geldi."
}
```

Bu akis bugun uygulanmis degildir; sadece sonraki gelistirme icin oneridir.
Barkod tanimlama yazma isi yapilacaksa arama endpoint'lerine eklenmemeli,
ayri permission, audit ve duplicate kontroluyle tasarlanmalidir.

## Yeni Ekran Eklerken Checklist

- Barkod okutma varsa ilk is `barkodlar/{barcode}/cozumle` cagrisi olsun.
- Frontend barkodun urun barkodu, koli barkodu, alternatif barkod veya terazi
  barkodu oldugunu kendi tahmin etmesin.
- `operationType` mutlaka gonderilsin.
- Kaynak depo icin `warehouseNo` dogru gonderilsin.
- Hedef depo varsa `targetWarehouseNo` gonderilebilir, fakat sevkte blok
  beklenmemelidir.
- Firma/tedarikci baglami varsa `supplierCode` veya `companyCode` gonderilsin.
- Satira ekleme karari icin `isUsableInOperation` tek ana karar olsun.
- Miktar onerisi icin sirayla `embeddedQuantity`, `matchedUnitsPerCase`, sonra
  default `1` kullanilabilir.
- Hata mesaji icin once `errors.first`, yoksa `operationDecision`, yoksa kisa
  standart mesaj kullanilsin.
- Liste arama icin `urunler`, fiyat ekrani icin `fiyat-gor`, cari onerisi icin
  `cari-bul` kullanilsin.

## Sorun Analizinde Bakilacak Alanlar

Bir barkod beklenenden farkli davraniyorsa API cevabinda su alanlara bak:

```text
isFound
barcode
lookupBarcode
isVariableWeightBarcode
embeddedQuantity
isBarcodeCheckDigitValid
resolutionSource
barcodeKind
stockCode
matchedBarcode
isCaseBarcode
matchedUnitsPerCase
operationType
warehouseNo
targetWarehouseNo
isAllowedForTargetWarehouse
targetWarehouseReason
productModelCode
targetWarehouseModelCodes
supplierCode
hasPurchaseRequirement
purchaseRequirementReason
isPassive
isSalesBlocked
isOrderBlocked
isGoodsAcceptanceBlocked
isUsableInOperation
operationDecision
warnings
errors
```

En sik yanlislar:

- Sevk ekraninda hedef depo/model kod uyarisi hata gibi yorumlanmasi.
- Terazi barkodunda okutulan 13 hane ile aranan 7 hanenin karistirilmasi.
- Urun arama sonucunun satira dogrudan eklenmesi ve `cozumle` kararinin
  atlanmasi.
- `operationType` gonderilmedigi icin backend'in sadece genel bilgi dondurmesi.
- `supplierCode` gonderilmedigi icin firma/satinalma sarti kontrolunun eksik
  kalmasi.
