# Stok Anomali Merkezi

Bu dokuman, `FurpaMerkezApi` icindeki Stok Anomali Merkezi modulunun
2026-07-08 itibariyla kodda gorulen davranisini anlatir.

Modulun amaci Mikro stok hareketleri uzerinden operasyonel riskleri taramak,
bulunan kayitlari merkez veritabaninda saklamak ve kullanicinin bu kayitlari
listeleyip takip edebilmesini saglamaktir.

## Kisa Ozet

Stok Anomali Merkezi su isleri yapar:

- Mikro verisinde belirli kurallara gore stok anomalisi arar.
- Bulunan anomalileri `stock_anomalies` tablosuna tekil `source_key` ile kaydeder.
- Her tespit ve durum degisikligini `stock_anomaly_events` tablosunda izler.
- Anomalileri depo, tip, durum, onem, satin almaci ve arama metnine gore listeler.
- Kullanici tarafindan durum degisikligi yapilmasina izin verir.
- Urun sorumlusu/satin almaci bilgisini Mikro stok karti ve depo detayi uzerinden zenginlestirir.

## Ana Akis

1. Kullanici `POST /api/stok-islemleri/stok-anomali-merkezi/tara` endpoint'ini cagirir.
2. Servis Mikro DB uzerinde anomali kurallarini calistirir.
3. Her kural kendi sonuc sayisini uretir; bir kural hata alsa bile diger kurallar calismaya devam eder.
4. Bulunan kayitlar `source_key` alanina gore tekillestirilir.
5. Urun kodu varsa urun sorumlusu bilgisi Mikro'dan okunur.
6. Kayit daha once yoksa yeni anomali acilir.
7. Kayit daha once varsa son bilgiyle guncellenir.
8. Daha once `Resolved` olan bir anomali tekrar yakalanirsa durum tekrar `Open` olur.
9. Her tespit icin `Detected`, her manuel durum degisikligi icin `StatusChanged` hareketi yazilir.

## Anomali Tipleri

| Tip | Kod degeri | Ne arar? | Varsayilan onem |
| --- | --- | --- | --- |
| `NegativeStock` | `1` | Depo/stok bakiyesi eksiye dusmus urunleri bulur. | `High`, miktar `-10` altindaysa `Critical` |
| `DuplicateDocument` | `2` | Ayni evrak/stok/miktar kombinasyonunun birden fazla gorundugu hareketleri bulur. | `High` |
| `ReceivingDifference` | `3` | Depolar arasi sevkte sevk miktari ile kabul/formul miktari farkli olanlari bulur. | Fark `10` ve uzeriyse `High`, degilse `Medium` |
| `HighQuantity` | `4` | Son hareket ortalamasina gore asiri yuksek miktarli hareketleri bulur. | `High`, ortalamanin `10` katini gecerse `Critical` |
| `DormantStock` | `5` | Depoda stok oldugu halde uzun suredir hareket gormeyen urunleri bulur. | `Medium` |
| `PendingInterWarehouseTransfer` | `6` | Belirlenen saatten eski, teslim alinmamis depolar arasi sevkleri bulur. | `High` |

## Durumlar

| Durum | Kod degeri | Anlam |
| --- | --- | --- |
| `Open` | `1` | Yeni veya tekrar yakalanmis acik anomali. |
| `Acknowledged` | `2` | Kullanici tarafindan goruldu/isleme alindi. |
| `Resolved` | `3` | Cozuldu. Ayni anomali sonraki taramada tekrar yakalanirsa yeniden `Open` olur. |
| `Ignored` | `4` | Bilerek dikkate alinmayan kayit. |

## API Endpointleri

Base route:

```text
/api/stok-islemleri/stok-anomali-merkezi
```

### Listeleme

```http
GET /api/stok-islemleri/stok-anomali-merkezi
```

Yetki:

```text
stok-islemleri.stok-anomali-merkezi.list
```

Query parametreleri:

| Parametre | Tip | Aciklama |
| --- | --- | --- |
| `warehouseNo` | `int?` | Depo filtresi. Admin olmayan kullanicilarda kullanicinin deposuna zorlanir. |
| `type` | `StockAnomalyType?` | Anomali tipi. |
| `status` | `StockAnomalyStatus?` | Durum filtresi. |
| `severity` | `StockAnomalySeverity?` | Onem filtresi. |
| `productManagerCode` | `string?` | Urun sorumlusu/satin almaci kodu. |
| `hasProductManager` | `bool?` | Atanmis/atanmamis urun sorumlusu filtresi. |
| `startDate` | `DateTime?` | `LastDetectedAtUtc` baslangic tarihi. |
| `endDate` | `DateTime?` | `LastDetectedAtUtc` bitis tarihi. |
| `search` | `string?` | Urun kodu, urun adi, evrak seri/no veya mesaj icinde arar. |
| `take` | `int` | Donulecek maksimum satir. Varsayilan `100`, maksimum `500`. |

Donus modeli `StockAnomalyListResponse`:

- `totalCount`: Filtreye uyan toplam kayit.
- `summary`: Acik, isleme alinmis, cozulmus, yok sayilmis, kritik ve yuksek adetleri.
- `items`: Liste satirlari.

Ornek:

```http
GET /api/stok-islemleri/stok-anomali-merkezi?status=Open&type=NegativeStock&warehouseNo=50&take=100
```

### Satin Almaci Listesi

```http
GET /api/stok-islemleri/stok-anomali-merkezi/satin-almacilar
```

Yetki:

```text
stok-islemleri.stok-anomali-merkezi.list
```

Query parametreleri:

| Parametre | Tip | Aciklama |
| --- | --- | --- |
| `warehouseNo` | `int?` | Depo filtresi. |
| `status` | `StockAnomalyStatus?` | Varsayilan `Open`. |

Bu endpoint anomalileri `ProductManagerCode` alanina gore gruplayip her satin almaci
icin anomali sayisini dondurur. Kod bos ise isim `ATANMAMIS`, `isAssigned=false`
olarak gelir.

### Detay

```http
GET /api/stok-islemleri/stok-anomali-merkezi/{id}
```

Yetki:

```text
stok-islemleri.stok-anomali-merkezi.detail
```

Detay sonucunda anomali alanlariyla birlikte `events` listesi de doner. Bu liste
anomali ilk/son tespitleri ve kullanici durum degisikliklerini kronolojik olarak
gosterir.

### Tarama

```http
POST /api/stok-islemleri/stok-anomali-merkezi/tara
```

Yetki:

```text
stok-islemleri.stok-anomali-merkezi.scan
```

Body:

```json
{
  "warehouseNo": 50,
  "startDate": "2026-07-01",
  "endDate": "2026-07-08",
  "dormantDays": 90,
  "pendingTransferHours": 24,
  "highQuantityLookbackDays": 30,
  "highQuantityMultiplier": 3,
  "highQuantityMinimum": 100,
  "takePerRule": 250
}
```

Varsayilanlar:

| Parametre | Varsayilan | Sinir |
| --- | --- | --- |
| `startDate` | `endDate - 7 gun` | `endDate` tarihinden buyuk olamaz |
| `endDate` | Bugun | `startDate` tarihinden kucuk olamaz |
| `dormantDays` | `90` | `1-3650` |
| `pendingTransferHours` | `24` | `1-720` |
| `highQuantityLookbackDays` | `30` | `1-365` |
| `highQuantityMultiplier` | `3` | `1.01-100` |
| `highQuantityMinimum` | `100` | `0+` |
| `takePerRule` | `250` | `1-1000` |

Donus modeli `StockAnomalyScanResponse`:

- `startedAtUtc`
- `finishedAtUtc`
- `detectedCount`
- `rules`: Her kural icin `type`, `detectedCount` ve varsa `error`.

### Durum Degistirme

```http
POST /api/stok-islemleri/stok-anomali-merkezi/{id}/durum
```

Yetki:

```text
stok-islemleri.stok-anomali-merkezi.update
```

Body:

```json
{
  "status": 2,
  "note": "Magaza ile gorusuldu, sayim bekleniyor."
}
```

`status` alaninda `Open`, `Acknowledged`, `Resolved`, `Ignored` enum degerleri
veya sayisal karsiliklari kullanilabilir. Islem sonrasi detay modeli doner.

## Yetki Ve Depo Kapsami

Yetkiler:

| Islem | Policy |
| --- | --- |
| Listeleme | `stok-islemleri.stok-anomali-merkezi.list` |
| Detay | `stok-islemleri.stok-anomali-merkezi.detail` |
| Durum guncelleme | `stok-islemleri.stok-anomali-merkezi.update` |
| Tarama | `stok-islemleri.stok-anomali-merkezi.scan` |

Depo kapsami:

- `Administrator` veya `Admin` rolundeki kullanici tum depolari gorebilir.
- Diger kullanicilarda istek icindeki `warehouseNo` dikkate alinmaz; kullanicinin
  token/claim uzerindeki depo numarasi kullanilir.
- Detay ve durum degistirme islerinde kullanici sadece kendi deposu veya iliskili
  depo olarak gecen kayitlara erisebilir.

## Veri Modeli

Ana tablo:

```text
stock_anomalies
```

Onemli alanlar:

| Alan | Aciklama |
| --- | --- |
| `source_key` | Ayni anomalinin tekrar acilmasini engelleyen tekil anahtar. |
| `type` | Anomali tipi. |
| `severity` | Onem seviyesi. |
| `status` | Kullanici takip durumu. |
| `warehouse_no` | Ana depo. |
| `related_warehouse_no` | Iliskili depo. Sevk/kabul gibi iki depolu durumlarda kullanilir. |
| `product_code`, `product_name` | Urun bilgisi. |
| `product_manager_code`, `product_manager_name` | Satin almaci/urun sorumlusu bilgisi. |
| `document_serie`, `document_order_no`, `document_no` | Evrak bilgisi. |
| `movement_guid` | Mikro hareket GUID'i. |
| `quantity`, `expected_quantity`, `actual_quantity`, `average_quantity` | Kuralin anlamina gore miktar alanlari. |
| `occurred_at_utc` | Anomalinin Mikro tarafindaki hareket tarihi. |
| `message` | Kullaniciya gosterilecek kisa aciklama. |
| `evidence` | Teknik kanit/detay metni. |
| `first_detected_at_utc`, `last_detected_at_utc` | Ilk ve son yakalanma zamani. |
| `resolved_at_utc` | Cozum zamani. Sadece `Resolved` durumunda dolar. |

Hareket tablosu:

```text
stock_anomaly_events
```

Onemli alanlar:

| Alan | Aciklama |
| --- | --- |
| `stock_anomaly_id` | Ana anomali kaydi. |
| `event_type` | `Detected` veya `StatusChanged`. |
| `status` | O olay anindaki durum. |
| `message` | Olay mesaji veya kullanici notu. |
| `changed_by_user_id` | Durum degisikligini yapan kullanici. Tespit olaylarinda bostur. |
| `occurred_at_utc` | Olay zamani. |

## Kullanilan Mikro Kaynaklari

Tarama kurallari ve zenginlestirme icin su Mikro tablolarina bakilir:

| Tablo | Kullanim |
| --- | --- |
| `STOK_HAREKETLERI_OZET` | Eksi stok ve hareketsiz stok bakiyesi. |
| `STOK_HAREKETLERI` | Evrak tekrari, kabul farki, yuksek miktar ve bekleyen transfer. |
| `STOKLAR` | Urun adi ve genel urun sorumlusu kodu. |
| `DEPOLAR` | Depo adi. |
| `STOK_DEPO_DETAYLARI` | Depo bazli urun sorumlusu kodu. |
| `CARI_PERSONEL_TANIMLARI` | Urun sorumlusu/satin almaci ad soyad bilgisi. |

## Teknik Notlar

- Taramada her kural kendi SQL sorgusunu calistirir; `commandTimeout` 180 saniyedir.
- Kural bazli hata olursa scan tamamen dusmez; response icindeki ilgili rule satirinda
  `error` alanina hata yazilir.
- Upsert islemi `source_key` uzerinden yapilir.
- EF concurrency hatasi olursa servis 3 deneme yapar.
- Denemelerden sonra yine concurrency hatasi olursa SQL Server icin `UPDLOCK, HOLDLOCK`
  kullanan atomik upsert yoluna duser.
- `source_key` icin unique index vardir: `ux_stock_anomalies_source_key`.
- Liste performansi icin depo/durum/tarih, tip/durum/tarih ve satin almaci/durum/tarih
  indexleri vardir.

## Operasyonel Kullanim Onerisi

Gunluk operasyon icin pratik sira:

1. Sabah once genel tarama calistirilir.
2. `Open` ve `Critical/High` kayitlar liste ekranindan incelenir.
3. Eksi stok ve bekleyen transferler operasyon ekibine oncelikli aktarilir.
4. Kabul farklari ilgili depo/mal kabul ekibiyle kontrol edilir.
5. Asiri miktar ve tekrarli evrak kayitlari evrak/mikro hareket kontrolune gider.
6. Gecici incelenen kayitlar `Acknowledged`, cozulmus kayitlar `Resolved`, bilerek
   dikkate alinmayan kayitlar `Ignored` yapilir.
7. `Resolved` kayit tekrar taramada yakalanirsa otomatik `Open` olur; bu durum sorunun
   geri geldigini gosterir.

## Kod Konumlari

| Katman | Dosya |
| --- | --- |
| Controller | `src/FurpaMerkezApi.WebApi/Controllers/Modules/StokIslemleri/StokAnomaliMerkezi/StokAnomaliMerkeziController.cs` |
| Application contract | `src/FurpaMerkezApi.Application/Modules/StokIslemleri/StokAnomaliMerkezi/IStockAnomalyCenterService.cs` |
| Servis | `src/FurpaMerkezApi.Infrastructure/Modules/StokIslemleri/StokAnomaliMerkezi/StockAnomalyCenterService.cs` |
| Domain entity | `src/FurpaMerkezApi.Domain/Entities/StockAnomaly.cs` |
| Event entity | `src/FurpaMerkezApi.Domain/Entities/StockAnomalyEvent.cs` |
| EF config | `src/FurpaMerkezApi.Infrastructure/Persistence/Configurations/StockAnomalyConfiguration.cs` |
| Event EF config | `src/FurpaMerkezApi.Infrastructure/Persistence/Configurations/StockAnomalyEventConfiguration.cs` |
| Ilk migration | `src/FurpaMerkezApi.Infrastructure/Migrations/20260702112832_AddStockAnomalyCenter.cs` |
| Satin almaci migration | `src/FurpaMerkezApi.Infrastructure/Migrations/20260707130000_AddStockAnomalyProductManagers.cs` |
