# Evrak Tarih Toleransi

Bu kural, terminal veya sube cihazinin tarihi bozuldugunda 2019 gibi hatali bir tarihin Mikro'ya yazilmasini API katmaninda engeller.

## Kural

- Belge, hareket, siparis, sayim, mal kabul, icmal ve banknot takip tarihi bugunden en fazla 10 gun geride olabilir.
- Bu tarihler en fazla yarin olabilir.
- Teslim tarihi ileri planlanabildigi icin ust sinira tabi degildir; ancak 10 gunden daha eski olamaz.
- Administrator veya herhangi bir `.all-warehouses` / `.list-all` yetkisine sahip merkez kullanicisi bu kontrolden muaftir.

## Kapsam

Kontrol yalniz `[ValidateDocumentDateTolerance]` ile isaretlenmis evrak olusturma isteklerinde calisir. Global `POST` veya `PUT` istegi olmasi tek basina yeterli degildir.

Kapsanan ana akislar:

- Firma ve depo siparisi olusturma veya oneriden siparise cevirme
- Firma/depo sevk ve iade evraki olusturma
- Firma mal kabul ve stok girisi
- Virman, sayim, zayiat ve masraf fisi
- Kasa icmali ve banknot takip kaydi
- Manav Mikro mal kabul evraki ve manav stok duzeltmesi

Rapor, arama, toplu yazdirma ve gecmis kasa hareket aktarimi gibi yeni evrak olusturmayan `POST` istekleri bu kontrolden etkilenmez. Mikro evrak duzenleme ve AXATA entegrasyon controller'larindaki acik muafiyetler de korunur.

## Hata Davranisi

Tolerans disi tarih Mikro yazimi baslamadan `400 Bad Request` ile reddedilir. Hata mesaji alan adini, girilen tarihi ve izin verilen araligi acikca bildirir. Terminal bu mesaji kullaniciya gostermeli ve taslagi silmemelidir.

## Neden API Katmaninda

Terminal saati guvenilir bir kaynak degildir. Terminal tarafindaki saat uyarisi kullanici deneyimini iyilestirir; veri butunlugunun kesin korumasi API tarafindaki bu kontroldur.
