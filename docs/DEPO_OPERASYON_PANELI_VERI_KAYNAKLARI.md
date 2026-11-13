# Depo Operasyon Panelleri Veri Kaynaklari

Bu belge `depo-oncelikleri`, `depo-operasyon-paneli` ve `belge-akis-takibi`
ekranlarindaki sayilarin hangi kaynaktan ve hangi tarih anlami ile uretildigini
aciklar.

## Kesin veri kaynagi

Depolar arasi ve firmaya giden hareketlerde asagidaki bilgiler canli Mikro
`STOK_HAREKETLERI` kaydindan okunur:

- bugunku sevk ve depo iadesi: `sth_tarih` belge tarihi,
- bugunku depo kabulu: `sth_nakliyedurumu = 1` ve `sth_lastup_date`,
- bekleyen kabul: teslim edilmemis ve e-irsaliye isareti tamamlanmis hareket,
- kabul bekleyen cikis: ayni belgenin kaynak depo gorunumu,
- e-irsaliye bekleyen depo hareketi: teslim edilmemis ve e-irsaliye isareti
  bulunmayan hareket.
- firmaya sevk ve firma iadesi: `sth_evraktip = 1`, `sth_tip = 1` ve
  `sth_normal_iade` degeri.

Bekleyen operasyonlar en erken `01.11.2026` belge tarihinden baslar. Ayni seri,
sira ve kaynak depoya ait stok satirlari tek evrak olarak sayilir. Kabul edilen
evrakta hedef depo `sth_giris_depo_no`, bekleyen evrakta hedef depo
`sth_nakliyedeposu` alanindan cozulur.

AXATA bekleyen siparis sayisi `document_flows` tablosundan uretilmez. Etkin
AXATA gorevlerinin kapsadigi Mikro `DEPOLAR_ARASI_SIPARISLER` ve `SIPARISLER`
evraklari okunur. Evrakin tum satirlarinda `special1 = '1'` ise AXATA'ya
gonderilmis, aksi halde bekleyen kabul edilir. Boylece AXATA gorev kapsaminda
olmayan depo siparisleri ve gecmisten acik kalmis audit kayitlari bekleyen
sayisini sisirmez.

## Belge akis kaynagi

`document_flows` yalniz bu API'nin yaptigi islemlerin teknik ve operasyonel
denetim kaydidir. Bir kayit Mikro'daki guncel evrak durumundan farkli gorunse
bile gecmis kaniti olarak korunur. Belge Akis Takibi bu kayittan su bilgileri
gostermeye devam eder:

- UYUM/e-irsaliye basarisizligi,
- API'nin siparis olusturma ve AXATA gonderme denemeleri,
- diger basarisiz API islemleri,
- dis belge numarasi, ETTN, hata ve olay gecmisi.

`document_flows`, `stock_anomalies` veya `feedback_items` gibi yardimci Auth
DB tablolari bulunamazsa Mikro kaynakli depo sayilari hesaplanmaya devam eder.
Yardimci kartlar sifir kabul edilir, eksiklik warning loguna yazilir ve depo
operasyon panelinde akis takibinin kullanilabilir olmadigi bildirilir. Bu
tolerans yalniz SQL Server'in `Invalid object name` (`208`) hatasi icindir;
baglanti, yetki ve gercek sorgu hatalari gizlenmez.

Paneldeki `openOrderCount`, tum Mikro acik siparisleri degil, etkin AXATA
gorevlerinin kapsadigi ve Mikro'da henuz gonderim isareti almamis siparisleri
ifade eder. Kullanici ekraninda bu nedenle `AXATA Bekleyen Siparis` olarak
adlandirilir.

Belge Akis Takibi tarih filtresi bir audit filtresidir. Normal listede son
hareket zamani, bekleyen presetlerinde akis olusma zamani kullanilir. Buna
karsilik paneldeki `Bugunku Sevk` Mikro belge tarihini esas alir. Bu iki tarih
bilincli olarak farkli anlamdadir.

## Dayaniklilik ve performans

Mikro hareket ve AXATA siparis ozetleri 30 saniye onbellekte tutulur; ana sayfa
ve operasyon panelinin arka arkaya acilmasi ayni agir sorgulari tekrar
calistirmaz. Mikro okumasi gecici olarak basarisiz olursa endpoint tamamen
dusmez ve o istek icin belge akis verisine geri doner. Bu durum loga uyari
olarak yazilir; Mikro tekrar erisilebilir oldugunda sonraki istek kesin kaynaga
geri doner.

## 13.11.2026 canli kontrolu

Kontrol aninda Mikro'da 13.11.2026 tarihli 106 depolar arasi sevk bulundu.
Belge akisinda ayni gun olusturulmus 99 sevk vardi. Fark, 12.11.2026 tarihinde
olusturulup belge tarihi 13.11.2026 olan yedi `UN` serili evraktan kaynaklandi.
Yeni panel hesabi bu yedi evraki Mikro belge tarihine gore dogru gune dahil
eder. Ortak bulunan 4.050 depo hareketinde Mikro kabul/e-irsaliye durumu ile
belge akis durumu birebir eslesmistir.

Ayni kontrolde Belge Akisi `2.424` siparisi `OrderCreated` asamasinda
gosterirken etkin AXATA gorev kapsamindaki Mikro evraklarinda gercek bekleyen
siparis sayisi `0` bulunmustur. C01 kapsamindaki 1.998 ve G02 kapsamindaki 119
depolar arasi siparisin tum satirlari gonderilmis durumdadir. Yeni panel
hesabi, audit tablosundaki eski kayitlari silmeden bu sahte bekleyen sayisini
ekrandan kaldirir.
