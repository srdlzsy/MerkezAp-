# Firma Mal Kabul Senaryo

Bu dokuman firma mal kabul akisini tek islem olarak anlatir. Akis siparisli ve
siparissiz mal kabulu ayri ekran/endpoint yapmak yerine, satir bazinda opsiyonel
siparis baglantisi ile calisir.

## Temel Kural

Firma mal kabulde yeni stok giris hareketi olusturulur.

Depo mal kabulden farki sudur:

- Depo mal kabulde gonderen deponun daha once olusturdugu mevcut hareket
  guncellenir.
- Firma mal kabulde tedarikciden gelen urun icin yeni `STOK_HAREKETLERI`
  satirlari yazilir.

Bu yuzden firma mal kabulde stok miktarini belirleyen ana alan:

```text
STOK_HAREKETLERI.sth_miktar
```

Kabul edilen gercek miktar neyse `sth_miktar` alanina o yazilir.

## UI Akisi

Liste ekraninda daha once yapilmis firma mal kabul fisleri gosterilir:

```text
GET /api/mal-kabul-islemleri/firma-mal-kabulleri?WarehouseNo=110&StartDate=2026-04-01&EndDate=2026-04-30
```

Kullanici mevcut bir fisin detayina girerse:

```text
GET /api/mal-kabul-islemleri/firma-mal-kabulleri/{seri}/{sira}?warehouseNo=110
```

Kullanici yeni bir fis acacaksa `Yeni Mal Kabul` aksiyonuyla create ekranina gecer.

Kullanici create ekraninda once cariyi secer.

Cari secildikten sonra mal kabul fisi bos olarak acilir. Kullanici iki sekilde
devam edebilir:

1. Siparis baglamadan urun ekler.
2. `Siparis Bagla` aksiyonu ile secili cariye ait acik verilen firma
   siparislerini cagirir.

Siparis baglamak zorunlu degildir. Ayni fis icinde hem siparisli hem de
siparissiz satir olabilir.

## Siparis Bagla Akisi

Kullanici `Siparis Bagla` dediginde UI secili cariye ait acik verilen firma
siparislerini ister.

Onerilen liste istegi:

```http
GET /api/siparis-islemleri/verilen-firma-siparisleri?WarehouseNo=110&StartDate=2026-04-01&EndDate=2026-04-30&CustomerCode=32004621&OnlyOpen=true
```

Backend bu filtreleri destekler:

- `CustomerCode`: sadece secili carinin siparislerini getirir.
- `OnlyOpen=true`: teslim miktari siparis miktarindan az olan siparisleri getirir.

Liste satirinda toplam, teslim edilen, kalan ve kapali durumu doner:

```json
{
  "documentSerie": "F110",
  "documentOrderNo": 2841,
  "customerCode": "32004621",
  "totalQuantity": 50,
  "totalDeliveredQuantity": 32,
  "totalRemainingQuantity": 18,
  "isClosed": false
}
```

Kullanici bir siparis sectiginde UI siparis detayini cagirir:

```http
GET /api/siparis-islemleri/verilen-firma-siparisleri/F110/2841?warehouseNo=110
```

Detay satirlarinda `orderGuid` doner. Mal kabul satirini siparise baglamak icin
stok kodu yerine bu deger kullanilmalidir.

```json
{
  "lineNo": 0,
  "stockCode": "015792",
  "quantity": 10,
  "deliveredQuantity": 4,
  "remainingQuantity": 6,
  "orderGuid": "1bb2b4fe-b722-4e67-9d4b-050b6d87e800"
}
```

## Mal Kabul Kaydetme

Tek endpoint kullanilir:

```http
POST /api/mal-kabul-islemleri/firma-mal-kabulleri
```

`warehouseNo` body icinden alinmaz. Kullanici JWT bilgisindeki depo kullanilir.

Request ornegi:

```json
{
  "customerCode": "32004621",
  "movementDate": "2026-04-20",
  "documentDate": "2026-04-20",
  "documentNo": "IRS-000123",
  "deliverer": "Teslim Eden",
  "receiver": "Teslim Alan",
  "description": "",
  "allowOrderOverReceiving": false,
  "lines": [
    {
      "stockCode": "015792",
      "quantity": 6,
      "unitPrice": 0,
      "unitPointer": 1,
      "lastConsumingDate": "2026-12-31",
      "orderGuid": "1bb2b4fe-b722-4e67-9d4b-050b6d87e800"
    },
    {
      "stockCode": "018888",
      "quantity": 3,
      "unitPrice": 0,
      "unitPointer": 1,
      "lastConsumingDate": "2026-11-30",
      "orderGuid": null
    }
  ]
}
```

## Mikro Hareket Yazimi

Her kabul satiri icin `STOK_HAREKETLERI` tablosuna yeni satir yazilir.

Temel kolonlar:

```text
sth_evraktip       = 13
sth_tip            = 0
sth_cins           = 0
sth_normal_iade    = 0
sth_evrakno_seri   = F{kullaniciDeposu}
sth_evrakno_sira   = backend tarafinda uretilen siradaki numara
sth_cari_kodu      = secili cari
sth_stok_kod       = satir stok kodu
sth_miktar         = kabul edilen miktar
sth_giris_depo_no  = kullanici deposu
sth_cikis_depo_no  = kullanici deposu
sth_belge_no       = tedarikci irsaliye no
sth_belge_tarih    = irsaliye tarihi
```

Operasyonel alanlar:

```text
sth_HareketGrupKodu1 = SKT, dd.MM.yyyy formatinda
sth_HareketGrupKodu2 = teslim eden
sth_HareketGrupKodu3 = teslim alan
```

Siparis baglantisi:

```text
Siparisli satir   -> sth_sip_uid = SIPARISLER.sip_Guid
Siparissiz satir -> sth_sip_uid = Guid.Empty
```

## Siparisli Satir Kurali

`orderGuid` doluysa backend ilgili `SIPARISLER` satirini bulur ve kontrol eder:

```text
sip_tip        = 1
sip_cins       = 0
sip_depono     = kullanici deposu
sip_musteri_kod = secili cari
sip_stok_kod   = kabul satirindaki stok kodu
sip_iptal      != true
sip_kapat_fl   != true
```

Kalan miktar:

```text
kalan = sip_miktar - sip_teslim_miktar
```

Kabul kaydedilince:

```text
SIPARISLER.sip_teslim_miktar += siparise baglanan kabul miktari
```

## Senaryo 1: Siparissiz Mal Kabul

Kullanici cari secer, urunleri elle ekler, siparis baglamaz.

Satir:

```text
stockCode = 018888
quantity  = 3
orderGuid = null
```

Sonuc:

```text
sth_miktar  = 3
sth_sip_uid = Guid.Empty
```

Siparis tablosuna dokunulmaz.

## Senaryo 2: Siparisli Tam Kabul

Siparis:

```text
sip_miktar         = 10
sip_teslim_miktar = 0
kalan              = 10
```

Kabul:

```text
quantity  = 10
orderGuid = sip_Guid
```

Sonuc:

```text
sth_miktar              = 10
sth_sip_uid             = sip_Guid
sip_teslim_miktar       = 10
```

## Senaryo 3: Siparisli Kismi Kabul

Siparis:

```text
sip_miktar         = 10
sip_teslim_miktar = 0
kalan              = 10
```

Kabul:

```text
quantity = 6
```

Sonuc:

```text
sth_miktar        = 6
sth_sip_uid       = sip_Guid
sip_teslim_miktar = 6
kalan             = 4
```

Siparis kapanmaz, kalan miktar sonraki kabulde tekrar gorunur.

## Senaryo 4: Siparis Kalanindan Fazla Kabul

Siparis:

```text
sip_miktar         = 10
sip_teslim_miktar = 4
kalan              = 6
```

Kabul:

```text
quantity = 8
```

Varsayilan kural:

```text
allowOrderOverReceiving = false
```

Bu durumda API `409 Conflict` doner. Cunku kabul miktari siparis kalanindan
fazladir.

Esnek kural:

```text
allowOrderOverReceiving = true
```

Bu durumda backend satiri iki harekete boler:

```text
6 adet siparisli hareket
  sth_miktar  = 6
  sth_sip_uid = sip_Guid

2 adet siparissiz fazla hareket
  sth_miktar  = 2
  sth_sip_uid = Guid.Empty
```

Siparis teslim miktari sadece kalan kadar artar:

```text
sip_teslim_miktar = 10
```

Fazla gelen 2 adet stok girisine dahil olur ama siparisi tasirmaz.

## Response Ornegi

```json
{
  "documentSerie": "F110",
  "documentOrderNo": 1250,
  "movementDate": "2026-04-20T00:00:00",
  "documentDate": "2026-04-20T00:00:00",
  "documentNo": "IRS-000123",
  "warehouseNo": 110,
  "customerCode": "32004621",
  "lineCount": 2,
  "totalReceivedQuantity": 9,
  "totalOrderLinkedQuantity": 6,
  "totalOrderlessQuantity": 3,
  "totalOrderOverReceivedQuantity": 0,
  "totalAmount": 0,
  "writeConnectionName": "testMikroConnection",
  "lines": [
    {
      "movementGuid": "9c2d1f41-6f91-4e70-8e50-53d1e4bc88b0",
      "sourceLineNo": 0,
      "movementLineNo": 0,
      "stockCode": "015792",
      "orderGuid": "1bb2b4fe-b722-4e67-9d4b-050b6d87e800",
      "isOrderLinked": true,
      "receivingMode": "order-linked",
      "requestedQuantity": 6,
      "acceptedQuantity": 6,
      "orderLinkedQuantity": 6,
      "orderlessQuantity": 0,
      "orderRemainingBefore": 6,
      "orderRemainingAfter": 0
    }
  ]
}
```

## UI Davranisi

- Cari secilmeden `Siparis Bagla` pasif olmalidir.
- Cari secildikten sonra `CustomerCode` ile acik verilen siparisler cagrilir.
- UI siparis detayindan gelen `orderGuid` alanini mal kabul satirina tasir.
- Kullanici isterse siparis baglamadan manuel satir ekleyebilir.
- Kalan miktardan fazla kabul girilirse UI once uyarmalidir.
- Fazla kabul isteniyorsa `allowOrderOverReceiving = true` ile backend fazla
  kismi otomatik siparissiz satira boler.
