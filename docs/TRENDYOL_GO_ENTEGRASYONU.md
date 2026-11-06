# Trendyol Go Market Entegrasyonu

Son kod/dokuman karsilastirmasi: 04.11.2026. Kapsam Uber Eats Trendyol Go **Market/Grocery** API'sidir; Yemek API'si degildir. Bu belge mevcut kodun yaptigini anlatir, canli sistemde basarili islem yapildigina dair kanit yerine gecmez.

## Kisa cevap

Bizim API, TGO Market'in urun, siparis, fatura ve iade servislerine JWT korumali bir kopru sunar. Mikro'daki degisen fiyat/stok satirlari tek veya tum magazalar icin secilebilir. TGO katalogunda olmayan uygun urun once katalog batch'ine, kabul edilince otomatik fiyat/stok batch'ine alinir. Fiyat/stok sonucu urun bazinda kesinlesmeden sube POS veritabanina yazilmaz. TGO siparisleri Mikro'da otomatik satis/fatura evragi olusturmaz; bu kapsam bilerek kapali tutulmustur.

## Mimari ve veri akisi

```text
UI (JWT + permission)
  -> TrendyolGoController
  -> TrendyolGoIntegrationService / TrendyolGoPriceStockWorkbench
  -> TrendyolGoApiClient (Basic Auth, x-agentname, x-executor-user)
  -> api.tgoapis.com veya stageapi.tgoapis.com

Fiyat/stok onizlemede ayrica:
  TGO magazasi katalog sayfasi -> barkod
  barkod -> Mikro BARKOD_TANIMLARI / STOKLAR / STOK_DEPO_DETAYLARI
  Mikro satis fiyati + sube stok miktari -> karsilastirma -> secili satirlar
```

TGO'dan okunan siparis ve iade JSON'u buyuk olcude oldugu gibi istemciye doner; siparis Mikro'da satis/fatura evragina cevrilmez. `status` ve `stores` konfigurasyon verisidir; `connection-test` gercek TGO siparis GET istegi yapar. TGO yazma cevaplari cogunlukla asenkron `batchRequestId` icerir. HTTP 200/204, tek tek satirlarin tamamlandigi anlamina gelmez. Resmi gelistirici portali Uber Eats gecisinde siparis modeli degisiklikleri oldugunu duyuruyor; ham JSON dondurmamiz UI'nin bu degisikliklere otomatik uyum sagladigi anlamina gelmez ([TGO gelistirici portali](https://developers.tgoapps.com/)).

## Kurulum ve erisim

- Config bolumu `TrendyolGo`; `Enabled=false` varsayilandir. Production base URL `https://api.tgoapis.com`, stage URL `https://stageapi.tgoapis.com`.
- Satici kimligi `SupplierId` ile belirlenir. Basic Auth icin `AuthorizationToken` veya `ApiKey`/`ApiSecret` sunucu secret/env ayarinda tutulur; UI'ya veya repo'ya yazilmaz. Bu projede `AuthorizationToken` env adi `TrendyolGo__AuthorizationToken`, acma anahtari `TrendyolGo__Enabled`.
- `AgentName` ve `ExecutorUser`, TGO'nun yazma isteklerinde istedigi `x-agentname` ve `x-executor-user` header degerlerine kaynak olur. `ExecutorUser` islem yapanin e-posta bilgisidir; mevcut tasarimda sabit config degeridir, oturumdaki kullanicidan uretilmez.
- `Stores` listesi TGO `storeId` -> Mikro `warehouseNo` baglantisidir. Mevcut production config 60 magaza tanimlar. Isim benzerliginden depo secilmez. Magaza/depo eslesmesi canliya almadan once tek tek dogrulanmalidir.
- UI menu yetkisi `entegrasyon-islemleri.trendyol-go.page`; okuma `list`/`detail`, yazma `update`. Auth DB permission migration'i `20260928101758_AddTrendyolGoIntegrationPermissions` hedef ortamda uygulanmis ve kullanici rolune atanmis olmalidir.
- TGO API key/secret/token bu dokumanda yer almaz. Daha once sohbet veya dosyalarda paylasilan token/secret aciga cikmis sayilmali ve TGO tarafinda yenilenmelidir.

## Kapsam matrisi

`Var`: controller route'u ve upstream cagri kodu var, canli dogrulama yok. `Kismi`: fonksiyon var ama TGO sozlesmesinin parcasi eksik. `Yok`: bu API'de route/akis yok.

| TGO Market yetenegi | Bizde | Sinir / not |
|---|---|---|
| Magaza listesi/eslestirme ve baglanti testi | Var | Magazalar local config'den; test siparis GET ile yapilir. |
| Siparis paketlerini listeleme, siparis no ve paket ID ile okuma | Var | Manuel GET; surekli polling/webhook yok. |
| Siparis kabul (picked), provizyon araligi, hazir (invoiced) | Var | Kullanici/istemci dogru sirayi ve guncel packageId'yi yonetir. |
| Tedarik edilememe bildirimi | Kismi | `items/unsupplied` route'u kodda var; upstream path/body resmi dokuman ve stage ile henuz dogrulanmadi. |
| Alternatif urun, manuel sevk/teslim | Var | TGO islem durumu yeniden okunmali. |
| Fatura linki gonderme | Var | Linkin gecerliligi/arsiv suresi istemcinin sorumlulugunda. |
| Marka listeleme | Var | Kategori agaci ayni kapsamda degil. |
| Kategori agaci, guncel kategori/attribute ID alma | Yok | Urun olusturmada `categoryId` disaridan saglanmali. |
| Urun listeleme | Kismi | `categoryIds`, `title`, `orderBy`, `order` filtreleri yok. |
| Urun olusturma/guncelleme, attribute gonderme | Var | Raw JSON; tam urun kurali/semantik dogrulama TGO'da. Otomatik Mikro -> TGO urun master aktarimi yok. |
| Sube bazli fiyat/stok, satis acma/kapama, batch kontrolu | Var | Fiyat/stok ve otomatik katalog batch'leri kalici kuyrukta urun bazinda takip edilir. |
| Mikro'dan barkod yazmadan fiyat/stok gonderme | Var | Secili/tum magazalar, degisen/tarih/tum modlari; katalog eksigi otomatik devam eder. |
| Iade listeleme | Kismi | `claimIds`, `orderNumber`, `acceptedStartDate`, `acceptedEndDate` filtreleri yok. |
| Iade kabul/red ve JSON itiraz | Kismi | Itiraz gorseli/multipart yok. |
| Satici tarafindan claim olusturma | Yok | Resmi `createClaim` servisi API'ye eklenmedi. |
| Siparis webhook alicisi, otomatik siparis senkronu | Yok | Siparisler UI GET ile cekilir, kalici olay/idempotency kaydi yok. |
| Stage test siparisi olusturma | Yok | Resmi stage-only test servisi proxy edilmez. |
| TGO siparisinden Mikro satis/fatura evragi | Yok | Entegrasyon okuma ve TGO'ya aksiyon gonderme seviyesinde. |
| Uber Eats gecisiyle gelen siparis modeli uyumu | Dogrulanmadi | API ham JSON dondurur; UI alanlari ve operasyon akisi guncel Market ornekleriyle stage'de test edilmeli. |

Kaynaklar: [TGO urun filtreleme V2](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/urun-entegrasyonu/hm-urun-filtreleme-v2), [urun aktarimi](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/urun-entegrasyonu/hm-urun-aktarimi), [kategori kontrolu onerisi](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/urun-entegrasyonu/hm-urun-bilgisi-guncelleme), [siparis webhook'u](https://developers.tgoapps.com/en/docs/trendyol-go-grocery/order-integration/webhook-integration), [iade olusturma](https://developers.tgoapps.com/en/docs/trendyol-go-grocery/returned-order-integration/create-claim).

## Bizim API route'lari

Tum route'lar `/api/entegrasyon-islemleri/trendyol-go` kokunun altindadir. Tam query/default ve ornek response icin [UI API Dokumani](UI_API_DOKUMANI.md#trendyol-go-market-entegrasyonu) kullanilmalidir.

| Grup | Okuma | Yazma |
|---|---|---|
| Durum/magaza | `GET /` veya `/status`, `/stores`, `/connection-test?storeId=` | - |
| Siparis | `GET /orders`, `/orders/by-number/{orderNumber}`, `/orders/{orderId}/invoice-amount`, `/packages/by-ids?id=...` | `PUT /packages/{packageId}/picked`, `/invoiced`, `/items/unsupplied`, `/mark-alternative`, `/manual-shipped`, `/manual-delivered` |
| Urun | `GET /brands`, `/products`, `/products/batch-requests/{batchRequestId}` | `POST /products`, `PUT /products`, `POST /products/price-and-inventory`, `PUT /products/sale-on`, `/sale-off`, `POST /products/seller-attributes` |
| Mikro fiyat/stok (Snapshot) | `GET /price-stock/preview?storeId=&view=actionable` | `POST /price-stock/preview/refresh?storeId=`, `POST /price-stock/dispatch` |
| Mikro fiyat/stok (Dogrudan / Hizli) | `GET /price-stock/direct-preview?storeId=&mode=changed` | `POST /price-stock/direct-dispatch` |
| Kalici batch akisi | `GET /batch-workflows?take=100` | - |
| Mikro urun katalogu | `GET /catalog/preview?storeId=&view=actionable` | `POST /catalog/dispatch` |
| Fatura linki | - | `POST /invoice-links` |
| Iade | `GET /claims`, `/claims/{claimId}/items/objectionable` | `PUT /claims/{claimId}/accept`, `/reject`, `POST /claims/{claimId}/items/objections` |

GET islemleri icin `list` veya `detail`, yazmalar icin `update` permission gerekir. Cogu TGO JSON cevabi API tarafinda domain DTO'ya donusturulmez; UI `orderNumber`, `packageId`, fiyat, promosyon ve agirlikli urun alanlarini resmi Market modeline gore okumalidir. TGO sipariste `orderNumber` kalir fakat paket parcala/iptal sonrasi `id` (packageId) degisebilir; `lines[].product.saleUnitValue`/`saleUnitType` gercek satilan agirlik icin dikkate alinmalidir ([siparis dokumani](https://developers.tgoapps.com/en/docs/trendyol-go-grocery/order-integration/get-orders)).

## Fiyat/stok: barkod elle girilmeden

1. `GET /stores` ile magaza secilir; `storeId` -> `warehouseNo` eslesmesi gorulur.
2. `GET /price-stock/preview?storeId={id}` hazir snapshot'i hemen doner. Ilk snapshot yoksa uzun taramayi request icinde bekletmez; `snapshotStatus=Preparing` doner ve hosted worker yenilemeyi kuyruktan yapar. UI 2-5 saniyede bir ayni GET'i tekrar cagirir. `PreviewWarmupStoreIds` bos oldugunda uygulama acilisinda katalog taramasi yapilmaz; ilk ekran istegi yenilemeyi kuyruga alir.
3. Worker TGO katalogunun tum sayfalarini `PreviewPageParallelism` siniriyla tarar; canli varsayilan `1` istektir. Sayfalar toplandiktan sonra benzersiz barkodlari Mikro'da en fazla 1000 barkodluk toplu sorgularla **tam eslesme** uzerinden bulur; her TGO sayfasi icin ayri Mikro sorgusu calistirmaz. Hazir snapshot varsayilan 1800 saniyelik `PreviewRefreshIntervalSeconds` araliginda yenilenir. Yenileme boyunca eski snapshot ekranda kalir ve durum `Refreshing` olur.
4. Mikro fiyat, Trendyol Go icin ayrilan `TrendyolGo:PriceListNo` listesinden (varsayilan `3`) ve `PaymentPlanNo` degerinden (varsayilan `0`) depo ve barkod birimi esleserek okunur. Liste 1 veya genel satis fiyati fallback olarak kullanilmaz. Stok `dbo.fn_DepodakiMiktar(stockCode,warehouseNo,today)` ile gelir. Negatif stok sifira cekilir, kesirli stok tam sayiya asagi yuvarlanir. Sonra `TrendyolGo:StockReserveQuantity` (canli varsayilan `2`) dusulur ve TGO'ya `max(0, depo stok - rezerv)` gonderilir. Magaza bazinda farkli rezerv gerekiyorsa ilgili `Stores` kaydina `StockReserveQuantity` yazilir; bu deger genel ayari ezer.
5. `Ready` fiyat/stok farki olan ve gonderilebilir satirdir. `Unchanged` fark yoktur. `Skipped` Mikro barkodu/fiyati eksik, stok karti pasif/satisa kapali veya sayfada duplicate barkod gibi bir neden tasir. UI sebebi gostermelidir.
6. Manuel `Yenile` butonu `POST /price-stock/preview/refresh?storeId={id}` cagirir. Cevap `202 Accepted` olur; ayni magazanin isi zaten kuyrukta/calisiyorsa ikinci yenileme acilmaz.
7. Kullanici gonderim modunu secer ve `previewHash` ile `storeId` degerlerini `POST /price-stock/dispatch` endpointine gonderir. `mode=selected` yalniz `barcodes` listesindeki `Ready` satirlari, `mode=changed` tum `Ready` farklari, `mode=all-existing` ise TGO'da mevcut olup Mikro'da gecerli bulunan `Ready + Unchanged` satirlarin tamamini yeniden gonderir. Gonderimde tum katalog tekrar okunmaz, yalniz gonderilecek barkodlar Mikro'dan yeniden dogrulanir. Fiyat/stok veya snapshot hash'i degismisse backend `409` dondurur; UI yeni onizleme ister.

### Sube POS fiyat senkronu

`price-stock/dispatch` isteginin HTTP 200 almasi POS yazimi icin yeterli degildir. `batchRequestId`, `trendyol_go_batch_follow_up_tasks` tablosuna kaydedilir ve hosted worker TGO batch sonucunu kontrol eder. Yalniz TGO'nun urun bazinda basarili saydigi satirlar, kasalarin kullandigi PostgreSQL `market.stoksatisfiyat` tablosu icin kalici POS kuyruguna yazilir. Hatali urunler subeye yazilmaz; kismi sonuc ekranda `Kismi hata` olarak gorulur.

Uygulama batch kontrolu sirasinda kapanirsa `Processing` kaydinin iki dakikalik kirasi dolar ve yeni instance isi devralir. TGO'nun sonuc penceresi dolmadan takip tamamlanamazsa is kalici hata olur. `20261104112210_AddTrendyolGoBatchFollowUpTasks` migration'i hedef Auth/Furpa veritabaninda uygulanmis olmalidir.

POS worker varsayilan olarak depo `BranchDetails.BranchIpAddress` adresini kullanir. Merkez veya farkli PostgreSQL sunucusu kullanan istisna depolar `WarehouseHosts` ile override edilir; tum depolarda ortak bir hedef gerekiyorsa `BranchPosPriceSync:Host` kullanilabilir. Aktarim `sdp_depo_no + sfiyat_stokkod + sfiyat_listesirano + fiyat_tip_kodu` eslesmesiyle upsert uygular.

- `TrendyolGo:BranchPosPriceSync:Enabled` varsayilan olarak `false` gelir.
- Etkinlestirmek icin merkez sunucunun sube PostgreSQL `5432` portuna erisimi ve PostgreSQL `pg_hba.conf` icinde merkez IP'si icin kullanici/DB izni gerekir.
- Sube kapaliysa is kaybolmaz; kuyruk kaydi `RetryDelaySeconds` sonunda yeniden denenir. Trendyol gonderimi ve kullanici ekrani hata almaz veya bu ag beklemesini yasamaz.
- `Password` ayari kod deposunda tutulmamalidir; canli ortamda `TrendyolGo__BranchPosPriceSync__Password` ortam degiskeni kullanilmalidir.
- Kuyruk ayni depo/stok/fiyat-listesi/birim anahtarini transaction advisory lock ile serilestirir. Yazma once tarih kontrollu `UPDATE`, kayit yoksa `INSERT` uygular; uzak POS veritabaninda benzersiz indeks bulunmasina bagimli degildir. Fiyat ve depo blok tarihleri birbirinden bagimsiz karsilastirilir.
- Basarili kalemler `public.stoksatisfiyat` tablosuna yazildiktan sonra ayni PostgreSQL transaction'i icinde `public.sync` tablosundaki `online_satis_fiyatlari_sync` anahtari ilerletilir. Kasa yeni anahtari ancak fiyat ve satis-durdurma satirlari eksiksiz kaydedildiginde gorur.
8. Kabul edilen istegin `batchRequestId` degeri kalici takip tablosuna yazilir. Worker `GET /products/batch-requests/{id}` sonucunu periyodik kontrol eder. Toplu modlarda backend Trendyol limitine uygun en fazla 1000 satirlik paketlemeyi kendisi yapar; UI katalog sayfalama veya paket yonetimi yapmaz.

### Dogrudan degisiklik gonderimi

`direct-preview` Mikro'daki uygun fiyat/depo satirlarini `changed`, tarih veya `all` modunda listeler. Kullanici secimini `direct-dispatch` ile gonderdiginde her magaza icin su akis uygulanir:

Onizleme ham depo stokunu, magaza rezervini ve TGO'ya gidecek net stogu ayri alanlarda gosterir. Ornegin depo stoku `10`, rezerv `2` ise TGO miktari `8`; depo stoku `2` veya daha azsa TGO miktari `0` olur. Ayni hesap snapshot, dogrudan gonderim ve katalog eksigi tamamlandiktan sonraki otomatik ilk fiyat/stok gonderiminde kullanilir.

1. Mikro'da aktif olan barkodlar en fazla 1000 satirlik fiyat/stok batch'lerine gonderilir. Satisi durdurulan, pasife alinan, web gonderimi kapatilan veya fiyati iptal edilen barkodlar `SELLER_CLOSED` sebebiyle magazaya ozel `sale-off` batch'ine ayrilir.
2. Fiyat, stok ve satis kapatma batch'leri kalici takibe alinir. Worker TGO sonucunu urun bazinda kontrol eder; yalniz basarili barkodlari sube POS kuyruguna yazar.
3. TGO'nun katalogda bulunamadigi icin reddettigi barkodlarda katalog snapshot'i yalniz bu hata olustugunda yenilenir.
4. Eksik barkodlar Mikro marka, kategori, KDV ve barkod kurallarini geciyorsa katalog create batch'ine gonderilir.
5. Katalog batch'i kabul edilince worker katalogu yeniden okur ve ayni urunlerin ilk fiyat/stok batch'ini otomatik acar. Kullanici yeniden gondermez.
6. Diger fiyat/stok dogrulama hatalari katalog hatasi sayilmaz; gerekcesiyle birlikte takip ekraninda kalir.

Katalog kurallarini gecemeyen urunler sessizce atlanmaz; ilgili batch akisi hata veya kismi hata olarak gorunur. Uygun diger urunler islenmeye devam eder.

```json
{
  "storeId": 402535,


  "previewHash": "onizlemeden-gelen-deger",
  "mode": "all-existing",
  "barcodes": []
}
```

Gonderim modlari:

```text
selected      Yalniz barcodes listesindeki Ready farklari gonderir.
changed       Snapshot'taki tum Ready fiyat/stok farklarini gonderir.
all-existing  TGO'da mevcut, Mikro'da gecerli Ready ve Unchanged urunlerin tamamini yeniden gonderir.
```

`all-existing`, `Skipped` urunleri gondermez. Barkod eslesmesi olmayan, Mikro'da pasif/satisa kapali, fiyati sifir veya stoku okunamayan urunler sorun listesinde kalir. Bu mod TGO'da olmayan Mikro urunlerini olusturmaz.

Dogrudan akis TGO'da olmayan uygun Mikro urununu otomatik olusturur. Snapshot tabanli eski akis yalniz TGO'da mevcut urunleri gonderir; manuel katalog ekrani da ayrica kullanilabilir. `POST /products/price-and-inventory` ham endpointiyle 1-1000 satir gonderilebilir, fakat barkod ve fiyat/stok JSON'unu istemci hazirlar. Ilk aktarimda fiyat ve stok birlikte gonderilmelidir. `storeId` atlanirsa TGO islemi tum subelere uygulayabilir; UI bunu rutin sube akisinda hic kullanmamalidir. TGO ayni body'nin 15 dakika icinde tekrarini reddedebilir; timeout sonrasi batch sonucu ve guncel urun durumu kontrol edilmeden yeni istek acilmamalidir ([resmi fiyat/stok kurallari](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/urun-entegrasyonu/hm-sube-bazli-urun-fiyat-stok-guncelleme)).

### Mikro katalog aktarimi

`GET /api/entegrasyon-islemleri/trendyol-go/catalog/preview?storeId=402535&view=actionable&take=500`

Bu endpoint Mikro stok kartlarini ana/master barkodlariyla tarar ve secili magazanin hazir TGO snapshot'i ile karsilastirir. Urun olusturma supplier genelinde calisir; `storeId`, karsilastirma yapilacak TGO katalogu ve sonraki ilk fiyat/stok aktariminda kullanilacak depo baglami icindir.

Durumlar:

```text
Existing         Barkod TGO katalogunda zaten var; create gonderilmez.
Ready            Yeni urun icin barkod, KDV, marka ve kategori hazir.
MappingRequired  Mikro marka veya kategori kodunun TGO ID eslemesi eksik/celiskili.
Skipped          Urun pasif/satisa kapali, barkodsuz, gecersiz barkodlu, fiyatsiz, desteklenmeyen KDV oranli veya duplicate barkodlu.
```

Backend marka ve kategori eslemelerini once secili magazada zaten kabul edilmis barkodlardan ogrenmeye calisir. Ayni Mikro kodu yalniz tek bir TGO ID degerine gidiyorsa otomatik eslesir. Bilinen istisnalar konfigurasyonla acikca verilebilir ve otomatik eslemeyi ezer:

Katalog onizlemesi ve otomatik eksik urun olusturma, magazanin tam TGO katalog snapshot'i hazir olmadan calismaz. Mevcut barkodlar yeniden `create` edilmez. `DefaultBrandId` ve `DefaultCategoryId` varsayilan olarak `0` yani kapali gelir; dogrulanmis ortak bir ID yoksa acik esleme yapilmali, rastgele `1` kullanilmamalidir. Desteklenmeyen KDV orani da otomatik olarak baska bir orana cevrilmez.

```json
{
  "TrendyolGo": {
    "Catalog": {
      "BrandMappings": {
        "ULKER": 10050
      },
      "CategoryMappings": {
        "BISKUVI": 2045
      }
    }
  }
}
```

Toplu gonderim:

```http
POST /api/entegrasyon-islemleri/trendyol-go/catalog/dispatch
```

```json
{
  "storeId": 402535,
  "previewHash": "KATALOG-ONIZLEMESINDEN-GELEN-HASH",
  "sendAll": true,
  "barcodes": []
}
```

`sendAll=true` yalniz `Ready` urunleri en fazla 1000 satirlik TGO create batch'lerine ayirir. Secili gonderimde `sendAll=false` ve onizlemeden gelen barkodlar kullanilir. Hash degismisse istek reddedilir. Response'taki tum `batchRequestId` degerleri kontrol edilmeden fiyat/stok gonderilmez. Urunler TGO tarafinda kabul edilip katalog snapshot'inda `Existing` gorundukten sonra ilgili subede `price-stock/dispatch` ve `mode=all-existing` ile ilk fiyat/stok yuklemesi yapilir.

Katalog create akisi yalniz TGO'nun destekledigi `0`, `1`, `10` ve `20` KDV oranlarini kabul eder. Bosluk veya TGO'nun urun barkodunda kabul etmedigi `?/&%+^'*_` karakterlerini iceren barkodlar gonderilmez; onizlemede gerekcesiyle `Skipped` kalir.

## Diger operasyonlar

**Urun katalogu:** `GET /brands` marka ID'si verir; kategori agaci yoktur. Urun olusturma icin TGO'nun `barcode`, `title`, `brandId`, `categoryId`, `vatRate` gibi zorunlu alanlari saglanir. Urun guncelleme TGO'daki panel bilgisini gunceller; musteriye gorunen icerik icin ek TGO onay/talep sureci gerekebilir. Fiyat/stok ayri endpoint ile guncellenir ([urun olusturma](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/urun-entegrasyonu/hm-urun-aktarimi), [urun guncelleme](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/urun-entegrasyonu/hm-urun-bilgisi-guncelleme)). Satis kapatma isteginde `items[].saleOffReason` gerekir; `UNSUPPLIED`, `SEASONAL_PRODUCT`, `SELLER_CLOSED`, `ARCHIVED` degerleri resmi dokumandadir ([satis ac/kapat](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/urun-entegrasyonu/hm-urun-satisa-acma-kapama)).

**Siparis:** `GET /orders?storeId=...` ile yeni paketler alinir. Sirayla `picked`, gerekiyorsa tedarik edilememe/alternatif, `invoice-amount`, `invoiced` uygulanir. `invoiced` body'de `invoiceAmount`, `bagCount` (en fazla 10), `receiptLink`, `invoiceTaxAmount` vardir. TGO kendi kuryesi/oz kurye ve provizyon tipine gore tutar kurallari uygular; UI tutari otomatik tahmin etmemelidir ([paket statuleri](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/siparis-entegrasyonu/hm-paket-statu-bildirimi)). Alternatif icin `alternativeItems`, `collectedItemIdList`, `alternativeItemIdList` resmi semasiyla gonderilir ve yeni packageId yeniden okunur ([alternatif urun](https://developers.tgoapps.com/en/docs/trendyol-go-grocery/order-integration/alternative-product-shipment)). `items/unsupplied` upstream endpointi teyitsiz oldugu icin stage testi olmadan kullanilmamalidir.

**Fatura ve iade:** `/invoice-links` JSON'u `invoiceLink` ve sayisal `shipmentPackageId` alir. TGO, fatura URL'sinin uzun sure erisilebilir olmasini ister ([fatura linki](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/siparis-entegrasyonu/hm-fatura-gonderimi)). `GET /claims` ve claim accept/reject JSON aksiyonlari vardir; saticinin yeni claim olusturmasi yoktur. Itiraz JSON'u gonderilir, resmi gorselli multipart secenegi yoktur ([iade listeleme](https://developers.tgoapps.com/en/docs/trendyol-go-grocery/returned-order-integration/get-returned-orders), [kabul/red](https://developers.tgoapps.com/en/docs/trendyol-go-grocery/returned-order-integration/claim-accept-reject), [itiraz](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/iade-entegrasyonu/hm-iadeye-itiraz-etme)).

## Hata, tekrar deneme ve canliya alma

- UI `401/403` yetki/kimlik, `400` eksik veya hatali body, `409` onizleme degisti, `429/5xx/timeout` gecici veya belirsiz sonuc olarak ayirmalidir. TGO yazma istegi timeout gorduyse islemin uygulanip uygulanmadigi bilinmez; kontrolsuz ayni veya farkli body ile tekrarlamak risklidir.
- Batch response'taki `batchRequestId`, durum, `failedItemCount`, urun durumlari ve hata nedenleri worker tarafindan kalici olarak takip edilir. TGO batch sorgusu sinirli sure acik olabilir; resmi dokuman 4 saatlik pencere belirtir ([batch kontrolu](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/urun-entegrasyonu/hm-toplu-islem-kontrolu)).
- Webhook kullanilacaksa ayri HTTPS Basic Auth alicisi, `id + packageStatus` uzerinden idempotency ve kalici isleme gerekir. TGO webhook'u Created/Cancelled/Delivered/UnSupplied olaylari icin sunar; mevcut API **hicbirini almaz** ([resmi webhook](https://developers.tgoapps.com/en/docs/trendyol-go-grocery/order-integration/webhook-integration)).
- Canliya almadan once: Auth migration ve yetkiler; secret rotasyonu; 60 magaza/depo eslesmesi; stage'de urun GET, fiyat/stok batch ve paket aksiyonlari; `items/unsupplied` resmi yol teyidi; TGO dokumaniyla body alani kontrolu; kullanici onayli UI; batch ve timeout proseduru tamamlanmalidir.
- Mevcut testler servis icin birim testidir. Stage veya production'a gercek istek, fiyat/stok yazma ve siparis/islem sonucunun uctan uca teyidi bu belge hazirlanirken yapilmadi.
