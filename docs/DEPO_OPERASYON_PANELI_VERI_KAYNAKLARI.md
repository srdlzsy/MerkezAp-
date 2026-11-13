# Depo Operasyon Panelleri Veri Kaynaklari

Bu belge `depo-oncelikleri`, `depo-operasyon-paneli` ve `belge-akis-takibi`
ekranlarindaki sayilarin hangi kaynaktan ve hangi tarih anlami ile uretildigini
aciklar.

## Kesin veri kaynagi

Depolar arasi hareketlerde asagidaki bilgiler canli Mikro
`STOK_HAREKETLERI` kaydindan okunur:

- bugunku sevk ve depo iadesi: `sth_tarih` belge tarihi,
- bugunku depo kabulu: `sth_nakliyedurumu = 1` ve `sth_lastup_date`,
- bekleyen kabul: teslim edilmemis ve e-irsaliye isareti tamamlanmis hareket,
- kabul bekleyen cikis: ayni belgenin kaynak depo gorunumu,
- e-irsaliye bekleyen depo hareketi: teslim edilmemis ve e-irsaliye isareti
  bulunmayan hareket.

Bekleyen operasyonlar en erken `01.11.2026` belge tarihinden baslar. Ayni seri,
sira ve kaynak depoya ait stok satirlari tek evrak olarak sayilir. Kabul edilen
evrakta hedef depo `sth_giris_depo_no`, bekleyen evrakta hedef depo
`sth_nakliyedeposu` alanindan cozulur.

## Belge akis kaynagi

`document_flows` teknik ve operasyonel surec kaydidir. Asagidaki bilgiler bu
kaynaktan gelmeye devam eder:

- UYUM/e-irsaliye basarisizligi,
- AXATA gonderimi bekleyen siparis,
- diger basarisiz API islemleri,
- dis belge numarasi, ETTN, hata ve olay gecmisi.

`openOrderCount` tum Mikro acik siparisleri degil, `OrderCreated` asamasinda
kalan ve AXATA gonderimi tamamlanmayan siparisleri ifade eder. Kullanici
ekraninda bu nedenle `AXATA Bekleyen Siparis` olarak adlandirilir.

Belge Akis Takibi tarih filtresi bir audit filtresidir. Normal listede son
hareket zamani, bekleyen presetlerinde akis olusma zamani kullanilir. Buna
karsilik paneldeki `Bugunku Sevk` Mikro belge tarihini esas alir. Bu iki tarih
bilincli olarak farkli anlamdadir.

## Dayaniklilik ve performans

Mikro hareket ozeti 30 saniye onbellekte tutulur; ana sayfa ve operasyon
panelinin arka arkaya acilmasi ayni agir sorguyu tekrar calistirmaz. Mikro
okumasi gecici olarak basarisiz olursa endpoint tamamen dusmez ve belge akis
verisine geri doner. Bu durumda loga uyari yazilir; Mikro verisi tekrar
erisilebilir oldugunda sonraki istek kesin kaynaga geri doner.

## 13.11.2026 canli kontrolu

Kontrol aninda Mikro'da 13.11.2026 tarihli 106 depolar arasi sevk bulundu.
Belge akisinda ayni gun olusturulmus 99 sevk vardi. Fark, 12.11.2026 tarihinde
olusturulup belge tarihi 13.11.2026 olan yedi `UN` serili evraktan kaynaklandi.
Yeni panel hesabi bu yedi evraki Mikro belge tarihine gore dogru gune dahil
eder. Ortak bulunan 4.050 depo hareketinde Mikro kabul/e-irsaliye durumu ile
belge akis durumu birebir eslesmistir.
