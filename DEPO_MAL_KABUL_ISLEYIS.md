# Depo Mal Kabul Isleyis

Bu dokuman depolar arasi sevklerden gelen mallarin alici depo tarafinda nasil kabul edilecegini, eksik/fazla durumlarinin nasil yorumlanacagini ve mevcut Mikro stok hareketi alanlarinin hangi anlamda kullanilacagini anlatir.

## Temel Kural

Depolar arasi sevkte ana hareketi gonderen depo olusturur. Alici depo mal kabul yaparken yeni ana sevk hareketi olusturmaz; var olan `STOK_HAREKETLERI` satirlarini kabul durumuna getirir.

En onemli ayrim:

```text
sth_miktar        = gonderen deponun sevk ettigi / e-irsaliyeye konu olan resmi miktar
sth_FormulMiktar  = alici deponun mal kabulde saydigi fiili miktar
```

`sth_miktar` mal kabul sirasinda degistirilmez. Cunku bu miktar gonderen deponun sevk ettigi ve e-irsaliye ile bildirdigi resmi miktardir.

Depo stoklari `sth_miktar` uzerinden hesaplandigi icin `sth_FormulMiktar` tek basina stok miktarini degistirmez. Bu alan sayim/kabul sonucunu izlemek icin kullanilir.

## Sevk Olustugunda

Ornek:

```text
Gonderen depo: 50
Alici depo:    110
Transit depo:  60
Sevk miktari:  10
```

Gonderen depo depolar arasi sevk kestiginde hareket yaklasik su durumda olur:

```text
sth_evraktip        = 17
sth_normal_iade     = 0
sth_cikis_depo_no   = 50
sth_giris_depo_no   = 60
sth_nakliyedeposu   = 110
sth_nakliyedurumu   = 0
sth_miktar          = 10
sth_FormulMiktar    = 0 veya null
```

Bu hareketin anlami:

```text
50 numarali depodan 10 adet cikti.
Mal 110 numarali depoya gidiyor.
Mal henuz 110 numarali depo tarafindan kabul edilmedi.
```

E-irsaliye gonderimi de `sth_miktar` uzerinden yapilir.

## Bekleyen Mal Kabul Listesi

Alici depo bekleyen mal kabul belgelerini su mantikla gorur:

```text
sth_evraktip = 17
sth_normal_iade = 0
sth_nakliyedeposu = kullanici deposu
sth_nakliyedurumu != 1
```

UI tarafinda gelen sevk detayina girildiginde satirlar `movementGuid` ile izlenmelidir.

Stok kodu ile eslestirme tek basina guvenli degildir. Ayni stok kodu ayni evrakta birden fazla satir olarak gelebilir; parti, lot, aciklama veya siparis baglantisi farkli olabilir.

## Sayim Asamasi

Alici depo satirlari sayar ve her satir icin kabul edilen miktari girer.

Her satir icin:

```text
gonderilenMiktar = sth_miktar
sayilanMiktar    = UI'dan gelen receivedQuantity
fark             = sayilanMiktar - gonderilenMiktar
```

Durumlar:

```text
fark = 0   -> tam kabul
fark < 0   -> eksik kabul
fark > 0   -> fazla tespit
```

## Tam Kabul Senaryosu

Ornek:

```text
Gonderilen miktar: 10
Sayilan miktar:    10
Fark:              0
```

Bu durumda mevcut hareket kabul edilir:

```text
sth_FormulMiktar  = 10
sth_giris_depo_no = 110
sth_nakliyedeposu = 60
sth_nakliyedurumu = 1
```

Sonuc:

```text
50 depodan 10 adet cikar.
110 depoya 10 adet girer.
Fark yoktur.
Evrak kabul edilmis olur.
```

## Eksik Kabul Senaryosu

Ornek:

```text
Gonderilen miktar: 10
Sayilan miktar:    8
Eksik miktar:      2
```

Ana hareket uzerinde:

```text
sth_miktar        = 10
sth_FormulMiktar  = 8
sth_giris_depo_no = 110
sth_nakliyedeposu = 60
sth_nakliyedurumu = 1
```

Burada `sth_miktar` degismedigi icin stok hesabi acisindan ana hareket 110 depoya 10 adet giris yaptirir. Fiziksel gercekte ise 110 depoda 8 adet vardir.

Bu nedenle eksik durumda sadece ana hareketi kabul etmek stok gercegini tam yansitmaz. Eksik 2 adet icin ayri bir fark kapatma aksiyonu gerekir.

Mantikli isleyis:

```text
1. Ana sevk hareketi kabul edilir.
2. sth_FormulMiktar alicinin saydigi miktar olarak yazilir.
3. Eksik miktar fark olarak raporlanir.
4. Eksik fark manuel karar veya ayrica gelistirilecek fark kapatma islemiyle kapatilir.
```

Eksik farkin olasi kapanis aksiyonlari:

```text
Kaynak depo eksik gonderdi:
  Kaynak depo ile mutabakat yapilir.

Urun yolda kayboldu veya fire oldu:
  Noksan/fire hareketi ile stoktan dusulur.

Sayim hatasi yapildi:
  Mal kabul duzeltmesi veya yeniden sayim yapilir.

Eksik urun sonra geldi:
  Ek sevk/e-irsaliye veya ayrica kabul sureci ile tamamlanir.
```

Ilk uygulama davranisi:

```text
allowDiscrepancy = false:
  Eksik varsa kabul bloke edilir ve 409 Conflict doner.

allowDiscrepancy = true:
  Evrak kabul edilir, fark response'ta doner.
  differenceResolutionStatus = "requires-manual-resolution"
```

Bu ilk uygulamada eksik fark icin otomatik stok duzeltme hareketi acilmaz.

## Fazla Kabul Senaryosu

Ornek:

```text
Gonderilen miktar: 10
Sayilan miktar:    12
Fazla miktar:      2
```

Ana hareket uzerinde:

```text
sth_miktar        = 10
sth_FormulMiktar  = 12
sth_giris_depo_no = 110
sth_nakliyedeposu = 60
sth_nakliyedurumu = 1
```

Burada ana hareketin `sth_miktar` degeri yine 10 olarak kalir. Cunku resmi sevk ve e-irsaliye 10 adet uzerindendir.

Fazla 2 adet mevcut sevk hareketine eklenmez. Aksi halde e-irsaliye/sevk evragi 10 iken stok hareketi 12 gibi davranir ve resmi belge ile stok hareketi birbirinden kopar.

Mantikli isleyis:

```text
1. Ana sevk hareketi kendi resmi miktariyla kabul edilir.
2. sth_FormulMiktar alicinin saydigi miktar olarak yazilir.
3. Fazla miktar fark olarak raporlanir.
4. Fazla icin ek karar beklenir.
```

Fazla farkin olasi kapanis aksiyonlari:

```text
Ek sevk/e-irsaliye:
  Gonderen depo fazla 2 adet icin ek sevk olusturur.
  Alici depo yeni belgeyi ayrica kabul eder.

Yetkili sayim fazlasi girisi:
  Yetkili onayi ile alici depoya ayri stok giris hareketi acilir.

Yanlis gelen urun:
  Fiziksel urun geri gonderilir veya iade/fark surecine alinir.

Bekletme:
  Fazla fiziksel olarak ayrilir, sistemde fark kaydi acik kalir.
```

Ilk uygulama davranisi:

```text
allowDiscrepancy = false:
  Fazla varsa kabul bloke edilir ve 409 Conflict doner.

allowDiscrepancy = true:
  Evrak kabul edilir, fark response'ta doner.
  differenceResolutionStatus = "requires-manual-resolution"
```

Bu ilk uygulamada fazla fark icin otomatik stok giris hareketi acilmaz.

## API Akisi

Bekleyen mal kabul evraklari listelenir:

```text
GET /api/mal-kabul-islemleri/depo-mal-kabulleri?WarehouseNo=110&StartDate=2026-04-01&EndDate=2026-04-30
```

Kullanici listeden bir evrak secerse veya ustten seri/sira girerek devam etmek isterse ayni detay endpointi kullanilir:

```text
GET /api/mal-kabul-islemleri/depo-mal-kabulleri/{seri}/{sira}?warehouseNo=110
```

UI detaydaki `items[].movementGuid` alanini saklar ve kabul ekranina tasir.

Mal kabul istegi:

```text
POST /api/mal-kabul-islemleri/depo-mal-kabulleri/{seri}/{sira}/kabul
```

Request:

```json
{
  "allowDiscrepancy": false,
  "lines": [
    {
      "movementGuid": "8d4a5a77-1b3f-4f2a-93a1-b90a1b7d3c11",
      "receivedQuantity": 10
    }
  ]
}
```

Response:

```json
{
  "documentSerie": "F110",
  "documentOrderNo": 3694,
  "warehouseNo": 110,
  "sourceWarehouseNo": 50,
  "transitWarehouseNo": 60,
  "shippingState": 1,
  "lineCount": 1,
  "totalShippedQuantity": 10,
  "totalReceivedQuantity": 8,
  "totalMissingQuantity": 2,
  "totalExcessQuantity": 0,
  "hasDiscrepancy": true,
  "differenceResolutionStatus": "requires-manual-resolution",
  "writeConnectionName": "testMikroConnection",
  "lines": [
    {
      "movementGuid": "8d4a5a77-1b3f-4f2a-93a1-b90a1b7d3c11",
      "lineNo": 0,
      "stockCode": "015792",
      "shippedQuantity": 10,
      "receivedQuantity": 8,
      "differenceQuantity": -2,
      "differenceType": "missing"
    }
  ]
}
```

## UI Davranisi

UI sayim sonrasi kullaniciya fark tablosu gostermelidir.

Ornek:

```text
Stok       Sevk   Sayim   Fark   Durum
015792     10     8       -2     Eksik
016100     5      5        0     Tam
017300     3      4       +1     Fazla
```

Fark yoksa:

```text
Kabul Et
```

Fark varsa:

```text
Kabulu Bloke Et
Farkla Kabul Et
```

`Farkla Kabul Et` secilirse UI `allowDiscrepancy = true` gonderir.

## Sonraki Gelistirme Noktalari

Bu ilk akista farklar sadece tespit edilir ve response ile UI'a doner.

Sonraki adimda ayrica tasarlanmasi gereken aksiyonlar:

```text
Eksik fark kapatma:
  Noksan/fire hareketi mi acilacak?
  Kaynak depoya mutabakat kaydi mi acilacak?
  Transit/fark deposunda mi bekletilecek?

Fazla fark kapatma:
  Ek sevk/e-irsaliye mi istenecek?
  Yetkili sayim fazlasi girisi mi acilacak?
  Fiziksel geri gonderim sureci mi baslatilacak?
```

Bu kararlar netlestikten sonra eksik/fazla icin ayri endpointler eklenmelidir.
