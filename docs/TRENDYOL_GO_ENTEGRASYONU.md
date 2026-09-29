# Trendyol Go Market Entegrasyonu

Son kod/dokuman karsilastirmasi: 28.09.2026. Kapsam Uber Eats Trendyol Go **Market/Grocery** API'sidir; Yemek API'si degildir. Bu belge mevcut kodun yaptigini anlatir, canli sistemde basarili islem yapildigina dair kanit yerine gecmez. TGO stage/production uzerinde uctan uca dogrulama yapilmadi.

## Kisa cevap

Bizim API, TGO Market'in secilmis urun, siparis, fatura ve iade servislerine JWT korumali bir kopru sunuyor. Barkodlari elle yazmadan **tek sube ve tek katalog sayfasi** icin Mikro fiyati/stokunu onizleyip secili urunleri gonderebiliyor. TGO'nun tum ozellikleri desteklenmiyor: kategori agaci, bazi filtreler, claim olusturma, webhook, otomatik senkronizasyon ve Mikro satis/fatura kaydi eksik. `Enabled=false` varsayilan; bu haliyle canli gonderim kapali.

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
- `Stores` listesi TGO `storeId` -> Mikro `warehouseNo` baglantisidir. Mevcut config 59 magaza tanimlar. Isim benzerliginden depo secilmez. Magaza/depo eslesmesi canliya almadan once tek tek dogrulanmalidir.
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
| Sube bazli fiyat/stok, satis acma/kapama, batch kontrolu | Var | Raw toplu istekler; batch otomatik takip edilmez. |
| Mikro'dan barkod yazmadan fiyat/stok gonderme | Kismi | Yalniz TGO'da mevcut urunler, secili sube, secili sayfa ve `Ready` satirlar. |
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
| Mikro fiyat/stok | `GET /price-stock/preview?storeId=&page=&size=` | `POST /price-stock/dispatch` |
| Fatura linki | - | `POST /invoice-links` |
| Iade | `GET /claims`, `/claims/{claimId}/items/objectionable` | `PUT /claims/{claimId}/accept`, `/reject`, `POST /claims/{claimId}/items/objections` |

GET islemleri icin `list` veya `detail`, yazmalar icin `update` permission gerekir. Cogu TGO JSON cevabi API tarafinda domain DTO'ya donusturulmez; UI `orderNumber`, `packageId`, fiyat, promosyon ve agirlikli urun alanlarini resmi Market modeline gore okumalidir. TGO sipariste `orderNumber` kalir fakat paket parcala/iptal sonrasi `id` (packageId) degisebilir; `lines[].product.saleUnitValue`/`saleUnitType` gercek satilan agirlik icin dikkate alinmalidir ([siparis dokumani](https://developers.tgoapps.com/en/docs/trendyol-go-grocery/order-integration/get-orders)).

## Fiyat/stok: barkod elle girilmeden

1. `GET /stores` ile magaza secilir; `storeId` -> `warehouseNo` eslesmesi gorulur.
2. `GET /price-stock/preview?storeId={id}&page=0&size=100` cagrilir. Backend TGO katalog sayfasini okur ve barkodlari Mikro'da **tam eslesme** ile bulur.
3. Mikro fiyat, Trendyol Go icin ayrilan `TrendyolGo:PriceListNo` listesinden (varsayilan `3`) ve `PaymentPlanNo` degerinden (varsayilan `0`) depo ve barkod birimi esleserek okunur. Liste 1 veya genel satis fiyati fallback olarak kullanilmaz. Stok `dbo.fn_DepodakiMiktar(stockCode,warehouseNo,today)` ile gelir. Negatif stok sifira cekilir, kesirli stok tam sayiya asagi yuvarlanir.
4. `Ready` fiyat/stok farki olan ve gonderilebilir satirdir. `Unchanged` fark yoktur. `Skipped` Mikro barkodu/fiyati eksik, stok karti pasif/satisa kapali veya sayfada duplicate barkod gibi bir neden tasir. UI sebebi gostermelidir.
5. Kullanici satirlari tiklar; UI barkod listesini otomatik kurar. `previewHash`, `storeId`, `page`, `size`, secilen 1-100 benzersiz barkod `POST /price-stock/dispatch` body'ye konur. Hash degismisse backend `409` dondurur; UI yeni onizleme ister.

### Sube POS fiyat senkronu

`price-stock/dispatch` Trendyol Go tarafinda basarili olduktan sonra secilen satirlar, sube kasalarinin kullandigi yerel PostgreSQL `market.stoksatisfiyat` tablosu icin kalici kuyruga yazilir. Bu islem HTTP cevabini bekletmez; worker, `BranchDetails.BranchIpAddress` ile sube bilgisayarina baglanir ve `sdp_depo_no + sfiyat_stokkod + sfiyat_listesirano + fiyat_tip_kodu` eslesmesiyle upsert uygular.

- `TrendyolGo:BranchPosPriceSync:Enabled` varsayilan olarak `false` gelir.
- Etkinlestirmek icin merkez sunucunun sube PostgreSQL `5432` portuna erisimi ve PostgreSQL `pg_hba.conf` icinde merkez IP'si icin kullanici/DB izni gerekir.
- Sube kapaliysa is kaybolmaz; kuyruk kaydi `RetryDelaySeconds` sonunda yeniden denenir. Trendyol gonderimi ve kullanici ekrani hata almaz veya bu ag beklemesini yasamaz.
- `Password` ayari kod deposunda tutulmamalidir; canli ortamda `TrendyolGo__BranchPosPriceSync__Password` ortam degiskeni kullanilmalidir.
6. Basarili gonderimde `upstreamResponse.batchRequestId` alinir; `GET /products/batch-requests/{id}` ile satir bazli sonuc kontrol edilir. Sonraki TGO katalog sayfasina gecilir.

```json
{
  "storeId": 402535,
  "page": 0,
  "size": 100,
  "previewHash": "onizlemeden-gelen-deger",
  "barcodes": ["8690000000000"]
}
```

Bu manuel is merkezi TGO'da olmayan Mikro urununu **olusturmaz**, butun subeleri veya tum sayfalari tek seferde islemez. `POST /products/price-and-inventory` ham endpointiyle ayrica 1-1000 satir gonderilebilir, fakat barkod ve fiyat/stok JSON'unu istemci hazirlar. Ilk aktarimda fiyat ve stok birlikte gonderilmelidir. `storeId` atlanirsa TGO islemi tum subelere uygulayabilir; UI bunu rutin sube akisinda hic kullanmamalidir. TGO ayni body'nin 15 dakika icinde tekrarini reddedebilir; timeout sonrasi batch sonucu ve guncel urun durumu kontrol edilmeden yeni istek acilmamalidir ([resmi fiyat/stok kurallari](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/urun-entegrasyonu/hm-sube-bazli-urun-fiyat-stok-guncelleme)).

## Diger operasyonlar

**Urun katalogu:** `GET /brands` marka ID'si verir; kategori agaci yoktur. Urun olusturma icin TGO'nun `barcode`, `title`, `brandId`, `categoryId`, `vatRate` gibi zorunlu alanlari saglanir. Urun guncelleme TGO'daki panel bilgisini gunceller; musteriye gorunen icerik icin ek TGO onay/talep sureci gerekebilir. Fiyat/stok ayri endpoint ile guncellenir ([urun olusturma](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/urun-entegrasyonu/hm-urun-aktarimi), [urun guncelleme](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/urun-entegrasyonu/hm-urun-bilgisi-guncelleme)). Satis kapatma isteginde `items[].saleOffReason` gerekir; `UNSUPPLIED`, `SEASONAL_PRODUCT`, `SELLER_CLOSED`, `ARCHIVED` degerleri resmi dokumandadir ([satis ac/kapat](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/urun-entegrasyonu/hm-urun-satisa-acma-kapama)).

**Siparis:** `GET /orders?storeId=...` ile yeni paketler alinir. Sirayla `picked`, gerekiyorsa tedarik edilememe/alternatif, `invoice-amount`, `invoiced` uygulanir. `invoiced` body'de `invoiceAmount`, `bagCount` (en fazla 10), `receiptLink`, `invoiceTaxAmount` vardir. TGO kendi kuryesi/oz kurye ve provizyon tipine gore tutar kurallari uygular; UI tutari otomatik tahmin etmemelidir ([paket statuleri](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/siparis-entegrasyonu/hm-paket-statu-bildirimi)). Alternatif icin `alternativeItems`, `collectedItemIdList`, `alternativeItemIdList` resmi semasiyla gonderilir ve yeni packageId yeniden okunur ([alternatif urun](https://developers.tgoapps.com/en/docs/trendyol-go-grocery/order-integration/alternative-product-shipment)). `items/unsupplied` upstream endpointi teyitsiz oldugu icin stage testi olmadan kullanilmamalidir.

**Fatura ve iade:** `/invoice-links` JSON'u `invoiceLink` ve sayisal `shipmentPackageId` alir. TGO, fatura URL'sinin uzun sure erisilebilir olmasini ister ([fatura linki](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/siparis-entegrasyonu/hm-fatura-gonderimi)). `GET /claims` ve claim accept/reject JSON aksiyonlari vardir; saticinin yeni claim olusturmasi yoktur. Itiraz JSON'u gonderilir, resmi gorselli multipart secenegi yoktur ([iade listeleme](https://developers.tgoapps.com/en/docs/trendyol-go-grocery/returned-order-integration/get-returned-orders), [kabul/red](https://developers.tgoapps.com/en/docs/trendyol-go-grocery/returned-order-integration/claim-accept-reject), [itiraz](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/iade-entegrasyonu/hm-iadeye-itiraz-etme)).

## Hata, tekrar deneme ve canliya alma

- UI `401/403` yetki/kimlik, `400` eksik veya hatali body, `409` onizleme degisti, `429/5xx/timeout` gecici veya belirsiz sonuc olarak ayirmalidir. TGO yazma istegi timeout gorduyse islemin uygulanip uygulanmadigi bilinmez; kontrolsuz ayni veya farkli body ile tekrarlamak risklidir.
- Batch response'taki `batchRequestId` saklanip resmi batch servisiyle durum, `failedItemCount` ve hata nedenleri kontrol edilmelidir. TGO batch sorgusu sinirli sure acik olabilir; resmi dokuman 4 saatlik pencere belirtir ([batch kontrolu](https://developers.tgoapps.com/docs/trendyol-go-hizli-market-entegrasyonu/urun-entegrasyonu/hm-toplu-islem-kontrolu)). Bizde kalici batch/audit tablosu veya otomatik uzlastirma yoktur.
- Webhook kullanilacaksa ayri HTTPS Basic Auth alicisi, `id + packageStatus` uzerinden idempotency ve kalici isleme gerekir. TGO webhook'u Created/Cancelled/Delivered/UnSupplied olaylari icin sunar; mevcut API **hicbirini almaz** ([resmi webhook](https://developers.tgoapps.com/en/docs/trendyol-go-grocery/order-integration/webhook-integration)).
- Canliya almadan once: Auth migration ve yetkiler; secret rotasyonu; 59 magaza/depo eslesmesi; stage'de urun GET, fiyat/stok batch ve paket aksiyonlari; `items/unsupplied` resmi yol teyidi; TGO dokumaniyla body alani kontrolu; kullanici onayli UI; batch ve timeout proseduru tamamlanmalidir.
- Mevcut testler servis icin birim testidir. Stage veya production'a gercek istek, fiyat/stok yazma ve siparis/islem sonucunun uctan uca teyidi bu belge hazirlanirken yapilmadi.
