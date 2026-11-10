# Kasa Hareket Aktarimi ve Muhasebe Akisi

Bu dokuman, kasa hareketlerinin Furpa ara tablolarindan Mikro'ya aktarilmasini, belge akis takibinde hangi POS evraklarinin izlenecegini ve satis karsilastirma raporundaki tutarlarin nasil yorumlanacagini aciklar.

## Kaynak ve Yazim Siniri

- Ham kasa fisleri Furpa `PosFaturas`, `PosFaturaSatirs` ve ilgili odeme tablolarinda tutulur.
- Mikro stok/cari yazimini mevcut `StokHareketYaz` ve bagli prosedurler yapar.
- API prosedurun muhasebe kurallarini yeniden uygulamaz; aktarimi baslatir, sonucu okur, raporlar ve belge akisina kaydeder.
- Bu denetimde `StokHareketYaz` proseduru degistirilmemistir.

## Belge Turlerine Gore Akis

### Normal perakende satisi

Normal kasa satisi PS serisiyle stok hareketi ve ayni gunluk PS cari bacagini olusturur. Belge akis takibinde stok tarafi `sth_evraktip=4`, cari tarafi ise karsilik gelen perakende satis hareketi olarak izlenir.

### Gider pusulasi

Gider pusulasi PS serisiyle `sth_evraktip=16` stok hareketi ve kendi cari bacagini olusturur. Bu nedenle belge akis takibine dahil edilir.

### Vergi/TCKN bilgili fis veya irsaliye (`BelgeTuru=3`)

Bu kayitlarin muhasebe sureci uc asamalidir:

1. Kasa aktarimi PS serisiyle yalniz stok dusumunu yazar (`sth_evraktip=1`).
2. Muhasebe donem sonunda FUR26 serisiyle musteri carisine borc yazar.
3. Kartla alinmis tutar POSTHS tahsilat evrakiyla cariye alacak yazilarak kapatilir.

FUR26 ve POSTHS evraklari PS evrakinin seri/sira numarasini tasimaz. Bu nedenle `BelgeTuru=3` PS stok satirinda ayni kimlikle bir PS cari hareketi aramak yanlistir.

## Belge Akis Takibi

Kasa POS evraklari okunurken:

- seri `PS%` olmali,
- e-arsiv serileri olan `PSF%` dislanmali,
- gunluk ayni kimlikte cari bacagi beklenen stok tipleri yalniz `sth_evraktip IN (4,16)` olmali.

Bu kurallar, e-arsiv faturalarinin veya `BelgeTuru=3` bilgi fislerinin sahte "eksik cari hareketi" olarak gorunmesini engeller.

## Satis Karsilastirma

- **Stok Hareket Tutari:** Rapor kapsamindaki toplam Mikro stok hareketidir; `BelgeTuru=3` stoklari bu toplama dahil olabilir.
- **Cari Hesap Tutari:** Gunluk PS cari hareketlerinin toplamidir.
- **Cari / Stok Farki:** Toplam stoktan degil, cariyle ayni gun karsilastirilabilir stok tutarindan hesaplanir: `CurrentComparableStockAmount - CurrentAccountAmount`.
- **Aktarilan Tutar:** Furpa POS satirlarinin net + KDV toplamidir.
- **Gider Pusulasi:** `BelgeTuru=4` satirlaridir.
- **Gider Haric Tutar:** Aktarilan tutar eksi gider pusulasidir.
- **Z Raporu ve Aktarim Farki:** Kasa gun sonu tutariyla gider haric aktarim tutarinin mutabakatini gosterir.

`BelgeTuru=3` stoku gunluk PS cariye yazilmadigi icin cari/stok farkina dahil edilmez; aksi halde gercekte olmayan bir fark uretilir.

## Operasyonel Riskler

- Gecmis bir gunun kasa aktarimi donem kapandiktan sonra calistirilirsa stok Mikro'ya sonradan gelebilir; FUR26/POSTHS kapatma islemi muhasebe tarafinda ayrica kontrol edilmelidir.
- `BelgeTuru=3` kaydinda musteri cari kodu yoksa donem sonu faturasi otomatik olarak eslestirilemez. Bu kayit veri kalitesi uyarisi olarak ele alinmalidir.
- Eski donem evrak numarasi cakismalari prosedurun numara uretim kuraliyla ilgilidir. API raporu bu durumu gizlememeli; prosedur degisikligi ayri bir canli veri ve Mikro uyumluluk calismasi olarak yapilmalidir.

## Bilerek Otomatiklestirilmeyenler

- FUR26 faturasi veya POSTHS tahsilati API tarafinda otomatik uretilmez.
- Gecmis POS evraklari topluca degistirilmez.
- Prosedur numara uretimi bu rapor/akis duzeltmesinin parcasi degildir.
