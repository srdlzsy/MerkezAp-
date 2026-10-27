# AXATA Worker Gorevleri

Bu dokuman, production ortaminda `AxataSynchronization:Tasks` altinda bulunan gorevlerin kisa kullanim ozetidir. Ayrintili teknik akis icin [AXATA_ENTEGRASYON_ALTYAPISI.md](AXATA_ENTEGRASYON_ALTYAPISI.md) dokumanina bakilabilir.

## Calisma Kosullari

Bir gorevin otomatik calismasi icin su ayarlarin birlikte acik olmasi gerekir:

- `AxataSynchronization.Enabled=true`
- `AxataSynchronization.WorkerEnabled=true`
- `AxataSynchronization.SchedulerEnabled=true`
- `Tasks.{taskCode}.Enabled=true`
- `Tasks.{taskCode}.ScheduleEnabled=true`

`IntervalMinutes=5`, gorevin yaklasik bes dakikada bir planlanmasi anlamina gelir. Ayni gorev halen calisiyorsa ikinci bir kopyasi kuyruga alinmaz.

## Mikro'dan AXATA'ya Giden Gorevler

| Gorev kodu | Hareket | Ne ise yarar? |
| --- | --- | --- |
| `firm-master-sync` | Firma master | Mikro cari ve adres degisikliklerini AXATA firma kartlarina aktarir. |
| `product-master-sync` | Urun master | Mikro stok, barkod, birim ve temel urun bilgilerini AXATA urun kartlarina aktarir. |
| `issued-warehouse-order-sync` | C01 | Merkez depo `50`den subelere verilen depo siparislerini AXATA cikis siparisi olarak gonderir. |
| `received-company-order-sync` | C02 | Mikro'daki alinan firma/musteri siparislerini AXATA cikis siparisi olarak gonderir. Tum depolar taranir. |
| `warehouse-inbound-order-sync` | G02 | Subeden merkez depo `50`ye gelecek depo siparislerini AXATA giris siparisi olarak gonderir. |
| `company-receiving-sync` | G01 | Merkez depo `50` icin verilen firma/satinalma siparislerini AXATA giris siparisi olarak gonderir. |

Siparis AXATA'ya basariyla ulastiginda Mikro kaynak satirlari gonderildi olarak isaretlenir. Ayni belge AXATA'da zaten varsa ikinci siparis olusturulmaz; Mikro bayragi mevcut AXATA kaydiyla uzlastirilir.

## AXATA'dan Mikro'ya Gelen Gorevler

| Gorev kodu | Hareket | Ne ise yarar? |
| --- | --- | --- |
| `c01-outbound-delivery-import` | C01 | AXATA'da hazirlanan merkez depo cikis sevkini Mikro'da depolar arasi sevk hareketine cevirir. |
| `c02-outbound-delivery-import` | C02 | AXATA firma sevkini Mikro firma satis/sevk hareketine aktarir ve siparis teslim miktarini gunceller. |
| `c03-outbound-delivery-import` | C03 | AXATA depo iadesini Mikro depo iade hareketine aktarir. |
| `c04-outbound-delivery-import` | C4 | AXATA firma iadesini Mikro firma iade hareketine aktarir. |
| `g01-inbound-atf-import` | G01 | AXATA firma mal kabul/ATF satirlarini Mikro firma mal kabul hareketine aktarir. |
| `g02-inbound-delivery-import` | G02 | AXATA depo mal kabulunu Mikro'daki ilgili bekleyen sevk ve siparisle eslestirerek kabul eder. |

Mikro yazimi tamamlanmadan AXATA belgesi tamamlandi olarak isaretlenmez. AXATA'da sadece basligi bulunan, henuz satiri veya miktari olusmamis belgeler hata sayilmaz ve batch kotasini tuketmez; satirlari hazir olunca sonraki turda otomatik islenir.

## Sayim Gorevi

| Gorev kodu | Hareket | Ne ise yarar? | Production durumu |
| --- | --- | --- | --- |
| `inventory-count-sync` | Dynamic Census | AXATA `vw_stok_duzeltme` sayim/duzeltme kayitlarini Mikro stok hareketine aktarir ve `ENT011` durumunu gunceller. | Kapali |

Bu gorev production ayarinda `Enabled=false` ve `ScheduleEnabled=false` oldugu icin otomatik calismaz. Acilmasi ayri bir operasyon karari olmalidir.

## WarehouseOrderAutomation

`WarehouseOrderAutomation` bir scheduler gorevi degildir; kullanicinin sevk veya iade kaydi sirasinda calisan otomasyondur.

Production davranisi:

- Hedef depo `50` olan siparissiz depolar arasi sevkte otomatik `F50.{sira}` giris siparisi olusturur.
- Hedef depo `50` olan depo iadesinde otomatik `F50.{sira}` giris siparisi olusturur.
- Hareket ile siparis ayni veritabani transaction'i icinde yazilir.
- Olusan siparisi `warehouse-inbound-order-sync` daha sonra AXATA'ya G02 olarak gonderir.

## Kuyruk ve Hata Davranisi

- C01, C02, G01 ve G02 siparis gonderimleri ayri ve oncelikli worker hattinda calisir.
- Sevk/mal kabul donusleri genel hatta, firma ve urun master gorevlerinden once calisir.
- Bir belgenin veri hatasi ayni gorevdeki diger belgeleri durdurmaz.
- Gecici altyapi veya baglanti hatasi sonraki zamanlanmis turda yeniden denenir.
- Belgeye ozel veri sorunu kalici retry tablosunda tutulur: ilk hata sonrasi 15 dakika, ikinci hata sonrasi 1 saat, devaminda 6 saat arayla yeniden denenir.
- Basarili belge retry kaydindan kaldirilir.
- Tek gorevin calisma ust siniri production ortaminda 10 dakikadir.

## Production Ozeti

- Firma ve urun master: acik.
- C01/C02/G01/G02 siparis gonderimleri: acik.
- C01/C02/C03/C4/G01/G02 Mikro donusleri: acik.
- Dinamik sayim: kapali.
- Eski Windows worker: kapali kalmalidir. Yalniz API icindeki worker calismalidir.
