# Mikro Stok Hareketleri Kilit Analizi

Bu belge, 2026-10-19 tarihinde canli `MikroDesktop_FURPA_2024` veritabaninda
gorulen create gecikmelerinin salt-okuma incelemesini ve uygulanacak duzeltmeleri
ozetler.

## Canli Bulgular

- `STOK_HAREKETLERI` yaklasik 218 milyon satirdir.
- `STOK_HAREKETLERI_OZET` yaklasik 22 milyon satirdir.
- Veritabaninda `READ_COMMITTED_SNAPSHOT` kapaliydi.
- Zayiat/masraf liste sorgusu ayni depo ve gun icin iki kez paralel calisiyor,
  uygun filtered index bulunmadigi icin uzun tarama yapiyordu.
- Bu `SELECT` istekleri `STOK_HAREKETLERI` create yazilarini bloke ediyordu.
- Liste istekleri bittikten sonra ikinci darbogaz goruldu: Mikro
  `mye_STOK_HAREKETLERI_Trigger`, cok satirli bulk insertlerde
  `STOK_HAREKETLERI_OZET` sayfalarinda `LCK_M_U` zinciri olusturuyordu.
- Bir root blocker arkasinda firma mal kabul ve diger stok hareketi create
  istekleri dahil cok sayida API transaction'i siraya giriyordu.
- `Guvenli Yazmalar` tablosu bu SQL kilidini olusturmuyordu. Buradaki
  `Processing`, `MIKRO_WRITE_QUEUE_BUSY` ve
  `MIKRO_WRITE_OUTCOME_UNCONFIRMED` kayitlari cogunlukla kilit/gecikmenin
  sonucudur.

## Kod Duzeltmeleri

1. Zayiat/masraf liste sorgusu SQL Server'da `ReadUncommitted` transaction ile
   calisir. Liste okumasi create yazilarinin onune tablo/page read lock koymaz.
2. Sorguya `Furpa:List:StockReceipt` etiketi eklenmistir. Canli SQL ekraninda
   kaynak daha kolay ayirt edilir.
3. `SqlBulkCopy` belge atomikligini korur, fakat `BatchSize=1` kullanir. Mikro
   trigger'i her satirda hizli tek-satir koluna girer; 22 milyon satirli ozet
   tablosunda cok satirli update taramasi olusmaz.
4. Izleme ekraninda trigger kilidi
   `stock-summary-trigger-contention` recommendation koduyla ayri gosterilir.
5. Guvenli yazma stale esigi, gercek create timeout/lease profiliyle uyumlu
   olarak 12 dakikadir.

## Gerekli Indexler

Acil iki filtered index:

```text
IX_FR_STH_StokFisi_Liste
IX_FR_STH_StokFisi_Trace
```

Kurulum scripti:

```text
docs/STOK_HAREKETLERI_STOK_FISI_ACIL_INDEXLERI.sql
```

Bu script 218 milyon satirli tabloda yogun saatte calistirilmaz. Dusuk trafik
veya bakim penceresinde, devam eden create istekleri tamamlandiktan sonra
uygulanir. `ONLINE=ON` tabloyu tamamen kapatmaz; yine de baslangic/bitiste schema
lock ve kurulum boyunca ciddi IO/CPU kullanimi olabilir.

## Yayin Sirasi

1. Yeni API surumunu yayinla. Bu adim liste read lock ve bulk-trigger batch
   sorununu kod tarafinda azaltir.
2. `/health/ready` ve `Veritabani Izleme > Canli SQL` ile worker/DB durumunu
   kontrol et.
3. Dusuk trafikte acil iki index scriptini Mikro DB'de calistir.
4. Ayni depo/gun zayiat ve masraf listelerini ac; sureyi once/sonra olc.
5. Es zamanli firma mal kabul ve sevk create testinde `LCK_M_U`, `LCK_M_IX`,
   blocked request ve acik transaction sayilarini izle.
6. `Guvenli Yazmalar` kayitlarini otomatik silme. Ilgili
   `offline-sync/{clientRequestId}` veya inceleme akisi ile uzlastir.

## Basari Kriteri

- Zayiat/masraf liste sorgusu saniyeler yerine kisa surede tamamlanir.
- Liste `SELECT`i create `BULK INSERT` isteklerini bloke etmez.
- `mye_STOK_HAREKETLERI_Trigger` kaynakli uzun `LCK_M_U` zinciri olusmaz.
- Firma mal kabul create istekleri 300 saniyelik kuyruk timeout'una ulasmaz.
- Yeni `MIKRO_WRITE_QUEUE_BUSY` ve belirsiz sonuc kayitlari belirgin bicimde
  azalir.

## Mudahale Kurali

- Bir kilit zincirinde once root blocker'in SQL'i, program adi, baslangic zamani
  ve transaction yasi kontrol edilir.
- Tum bekleyen API SPID'leri topluca `KILL` edilmez. Bu, rollback yukunu artirir
  ve kullanici sonuclarini daha belirsiz yapar.
- `KILL` yalniz etkisi dogrulanmis tek root blocker icin, yetkili kullanicinin
  gerekceli aksiyonuyla son secenek olarak kullanilir.
- Mikro vendor trigger'i canlida dogrudan degistirilmez veya kapatilmaz. Ozet
  stok tutarliligi bu trigger'a baglidir.
