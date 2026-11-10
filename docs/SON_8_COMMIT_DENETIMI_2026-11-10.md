# Son 8 Commit Denetimi - 2026-11-10

## Incelenen Alanlar

- Yeni raporlar ve verilen firma siparisi onayi
- Depo siparisi kaynak/hedef stok kurallari
- Belge akis takibi, depo oncelikleri ve operasyon paneli
- Kasa hareket aktarimi ve satis karsilastirma
- Depo yetki kapsami
- Evrak tarih toleransi
- Kasa sayimi banka odeme tipi katalogu

## Dogrulanan Kararlar

- POS belge akis takibinde `PSF` e-arsiv serileri dislanir.
- Gunluk PS cari bacagi olusan stok tipleri `sth_evraktip IN (4,16)` ile takip edilir. Bilgi fisi/irsaliye niteligindeki tip `1`, ay sonu `FUR26 + POSTHS` sureci nedeniyle sahte eksik cari bacagi uretmez.
- Satis karsilastirmasinda cari-stok farki toplam stok yerine `CurrentComparableStockAmount` ile hesaplanir.
- Bekleyen mal kabul yalniz basarili e-irsaliye adimina ulasmis depo sevk/iade evraklarindan hesaplanir.
- Ana sayfadaki oncelik rotalari 30 gunluk operasyon penceresini, hazir filtreyi ve gelen/giden yonunu belge akis ekranina tasir.
- Depo kapsaminda endpoint'e ait `.all-warehouses` yetkisi esas alinir; genel arama endpointlerinde yetkili merkez kullanicisinin secili deposu korunur.
- Halkbank banka secim listesinden adiyla degil, Furpa `PaymentTypes.PaymentTypeNo=2` kimligiyle cikarilir.

## Bu Denetimde Duzeltilen Nokta

Tarih toleransi ilk halinde tum `POST/PUT` isteklerindeki genel `Date` alanlarini taradigi icin toplu yazdirma veya gecmis aktarim gibi create olmayan komutlari engelleyebiliyordu. Filtre yalniz acikca isaretlenen evrak olusturma isteklerinde calisacak sekilde daraltildi.

## Bilerek Degistirilmeyenler

- `StokHareketYaz` prosedurune dokunulmadi.
- `BelgeTuru=3` icin muhasebenin ay sonu `FUR26 + POSTHS` kapatma sureci otomatiklestirilmedi.
- Gecmis POS kayitlari topluca degistirilmedi.
- Angular'da API'nin Halkbank satirini dondurmemesi yeterli oldugu icin ek istemci filtresi eklenmedi.

Kasa aktariminin PS, FUR26 ve POSTHS muhasebe sinirlari ayrica `KASA_HAREKET_AKTARIMI_MUHASEBE_AKISI.md` dosyasinda belgelenmistir.
