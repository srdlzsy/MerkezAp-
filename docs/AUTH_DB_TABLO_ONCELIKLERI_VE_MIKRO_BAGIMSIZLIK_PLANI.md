# Auth DB Tablo Oncelikleri ve Mikro Bagimsizlik Plani

## Amac

Bu belge `AuthDbContext` icindeki tablolarin uygulamadaki rolunu, bir tabloya
erisim olmadiginda beklenen davranisi ve Mikro okuma/yazma islemlerinin bu
tablolardan ne olcude bagimsiz olmasi gerektigini tanimlar.

Temel kararlar:

- Ticari evrak, stok, siparis, cari hareket ve depo hareketlerinde ana gercek
  Mikro'dur.
- Auth DB tablolari kimlik, yetki, tekrar engelleme, entegrasyon kuyrugu,
  denetim ve kullanici deneyimi saglar.
- Yardimci tablo arizasi Mikro'daki dogru veriyi yanlis gostermemeli ve ilgisiz
  modulleri durdurmamalidir.
- Sonucu belirsiz bir yazma islemi destek tablosu yok diye korlemesine tekrar
  gonderilmemelidir.
- Dusurulmus calisma modu `dogru fakat daha az dayanikli` olabilir; mukerrer
  veya dogrulanmamis ticari belge uretemez.

## Veri Otoritesi Sirasi

1. **Mikro ERP:** Ticari evrak ve hareketlerin son durumu.
2. **Dis servis:** Uyumsoft, Trendyol Go veya AXATA tarafinda gerceklesen dis
   islemin son durumu.
3. **Auth DB:** Kimlik/yetki, idempotency, kuyruk, denetim ve ekran destegi.
4. **Angular ve terminal yerel durumu:** Taslak ve kullanici deneyimi.

Auth DB'deki bir kayit ile Mikro celisirse ticari sonuc Mikro'dan tam geri
okuma ile dogrulanir. Dis servise gonderilen resmi bir belge icin dis servis
durumu da ayrica kontrol edilir.

## Oncelik Seviyeleri

### P0 - Uygulama kimligi icin zorunlu

Bu tablolar olmadan giris, oturum veya yetkilendirme guvenilir bicimde
calisamaz:

| Tablo | Rol | Yoklugunda davranis |
| --- | --- | --- |
| `app_users` | Kullanici kimligi | Giris ve kullanici islemleri durur |
| `app_roles` | Rol tanimlari | Yetkilendirme guvenilir degildir |
| `app_permissions` | Yetki katalogu | Yetki denetimi yapilamaz |
| `app_user_roles` | Kullanici-rol baglantisi | Rol tabanli erisim durur |
| `app_user_client_roles` | Istemci bazli roller | Istemci yetkisi dogrulanamaz |
| `app_role_permissions` | Rol-yetki baglantisi | Yetki sonucu uretilemez |
| `app_refresh_tokens` | Oturum yenileme | Mevcut erisim belirteci bitince yeniden giris gerekir |

P0 tablolari opsiyonel yapilmayacaktir. Ariza durumunda sistem guvensiz bir
varsayimla yetki vermek yerine kapali davranmalidir.

### P1 - Yazma dogrulugu icin kritik koordinasyon

| Tablo | Bugunku rol | Mevcut bagimlilik | Hedef davranis |
| --- | --- | --- | --- |
| `mobile_offline_sync_requests` | Ayni mobil/web istegin iki kez Mikro'ya yazilmasini onler, sonucu saklar ve yeniden oynatir | `clientRequestId` bulunan bircok create akisinda yazim oncesi zorunlu | Tablo yoksa yalniz tam Mikro on-kontrol ve son-kontrol stratejisi bulunan akislar kontrollu devam eder; belirsiz sonuc tekrar yazilmaz |
| `edespatch_submissions` | Uyumsoft gonderimini rezerve eder, sonucunu ve Mikro metadata tamamlama kuyrugunu saklar | E-irsaliye dis servis cagrisi oncesi zorunlu | Uyumsoft ve Mikro read-back ile mevcut belge kesin dogrulanabiliyorsa devam edilir; sonuc kanitlanamiyorsa yalniz e-irsaliye islemi bekletilir |

Bu iki tablo destek tablolari icinde en kritik olanlardir. Bunlari sorgudan
cikarmak veya hatayi genel bir `catch` ile yutmak dogru cozum degildir.

P1 icin hedef yedek yazma sirasi:

1. Istek ve evrak icin deterministik kimlik uretilir.
2. Mikro'da ayni evrak veya islem izi aranir.
3. Mevcut evrak tam ve dogruysa yeniden yazilmaz.
4. Evrak yoksa yalniz ilgili islem kilitlenerek yazilir.
5. Yazimdan sonra baslik, satir, miktar, depo ve ticari baglantilar Mikro'dan
   tam geri okunur.
6. Sonuc kanitlanamiyorsa otomatik tekrar yapilmaz; manuel kontrole alinir.
7. E-irsaliyede Mikro kontrolune ek olarak Uyumsoft sonucu da dogrulanir.

### P2 - Modul icin gerekli, uygulamanin tamami icin zorunlu degil

Bu tablolar ilgili entegrasyonun ayarini, kuyrugunu veya is durumunu tutar.
Tablo yoksa ilgili modul acik bir hata/uyari vermeli; Mikro'nun diger okuma ve
yazma islemleri calismaya devam etmelidir.

| Tablo grubu | Etkilenen alan |
| --- | --- |
| `uyumsoft_inbox_invoices` | Uyumsoft gelen e-belge onbellegi ve isleme durumu |
| `trendyol_go_branch_pos_price_sync_tasks` | Basarili Trendyol sonucunun sube POS'a yazilma kuyrugu |
| `trendyol_go_batch_follow_up_tasks` | Trendyol batch sonucunun takip ve tekrar durumu |
| `trendyol_go_categories` | Trendyol kategori katalogu |
| `trendyol_go_category_mappings` | Mikro-Trendyol kategori eslemeleri |
| `trendyol_go_product_category_overrides` | Urun bazli kategori istisnalari |
| `trendyol_go_runtime_settings` | Trendyol calisma zamani ayarlari |
| `trendyol_go_store_settings` | Magaza, depo ve stok rezerv ayarlari |
| `terminal_installations` | Terminal kurulum ve surum takibi |
| `despatch_drivers` | E-irsaliye surucu tanimlari |
| `green_grocer_product_case_profiles` | Manav urun koli profilleri |
| `green_grocer_order_line_snapshots` | Manav siparis satir anlik goruntuleri |
| `axata_synchronization_document_states` | AXATA senkron belge durumu |

P2 tablosu arizasi baska bir modulun Mikro okumasini veya yazmasini engellemez.
Ancak ilgili modul, kendi kuyrugu veya zorunlu ayari yoksa basariliymis gibi
cevap vermez.

### P3 - Denetim, izleme ve operasyon gorunurlugu

| Tablo | Rol | Yoklugunda davranis |
| --- | --- | --- |
| `mikro_api_write_audits` | Mikro API yazma denetimi | Mikro islemi devam eder, warning loglanir |
| `safe_write_review_audits` | Manuel guvenli-yazim inceleme gecmisi | Normal Mikro islemleri devam eder; inceleme gecmisi eksik bildirilir |
| `document_flows` | API belge akisinin guncel ozeti | Mikro tabanli paneller calisir; akis takibi kullanilamaz olarak isaretlenir |
| `document_flow_events` | Belge akis olay gecmisi | Mikro sonucu etkilenmez; olay gecmisi eksik kalir |
| `database_monitoring_incidents` | Veritabani izleme olaylari | Izleme gecmisi eksilir, ticari islem etkilenmez |
| `database_session_termination_audits` | Sonlandirilan SQL oturumu denetimi | Yonetim denetimi eksilir, ticari islem etkilenmez |
| `stock_anomalies` | Stok anomali ozetleri | Mikro stok verisi gosterilir, anomali karti sifir/bilinmiyor olur |
| `stock_anomaly_events` | Anomali olay gecmisi | Mikro stok verisi etkilenmez |

`mikro_api_write_audits` bugun best-effort calisir. Bu commit ile depo
oncelikleri ve depo operasyon paneli de `document_flows`, `stock_anomalies`
ve `feedback_items` tablolarinin bulunmamasi halinde Mikro verisini gostermeye
devam eder.

Tolerans yalniz SQL Server `Invalid object name` (`208`) hatasi icindir.
Baglanti, yetki, zaman asimi ve hatali sorgu sorunlari gizlenmez.

### P4 - Kullanici deneyimi ve iletisim

| Tablo grubu | Rol | Yoklugunda davranis |
| --- | --- | --- |
| `feedback_items` | Geri bildirim ve destek talepleri | Geri bildirim karti kullanilamaz; Mikro islemleri devam eder |
| `announcements` | Duyuru basligi ve icerigi | Duyurular gosterilemez |
| `announcement_targets` | Duyuru hedefleri | Hedefli duyuru gosterilemez |
| `announcement_reads` | Duyuru okundu bilgisi | Okundu durumu saklanamaz |

## Okuma Islemleri Icin Kural

- Stok, siparis, sevk, mal kabul, iade ve cari hareket sayilari mumkun oldugu
  yerde dogrudan Mikro'dan okunur.
- `document_flows` API'nin yaptigi islemleri ve olay gecmisini gostermeye devam
  eder; Mikro'daki ticari gercegin yerine kullanilmaz.
- Yardimci kart verisi yoksa `0`, `bilinmiyor` veya `takip kullanilamiyor`
  bilgisi acikca donulur. Yardimci tablo eksigi tum endpointi `500` yapmaz.
- Mikro sorgusu basarisizsa Auth DB verisi Mikro gercegiymis gibi sunulmaz.

## Yazma Islemleri Icin Kural

- Auth DB denetim kaydi yazilamamasi, sonucu Mikro'dan kesin dogrulanmis bir
  islemi basarisiz gostermemelidir.
- Auth DB rezervasyonu yapilamadan devam etmek ancak o islem icin tam Mikro
  recovery/read-back stratejisi varsa mumkundur.
- Yalniz uygulama ici bellek kilidi yeterli kabul edilmez; uygulama yeniden
  basladiginda veya birden fazla instance calistiginda koruma kaybolur.
- Timeout veya baglanti kopmasinda otomatik ikinci yazimdan once Mikro'da ayni
  evrak mutlaka aranir.
- Uyumsoft gibi dis sistemlerde, dis servis sonucu dogrulanmadan resmi belge
  yeniden gonderilmez.

## Bugunku Durum

| Alan | Durum |
| --- | --- |
| Mikro okuma ve canli depo metrikleri | Mikro ana kaynak olarak kullaniliyor |
| Depo oncelikleri yardimci tablo toleransi | Uygulandi |
| Depo operasyon paneli `document_flows` toleransi | Uygulandi |
| Mikro API yazma audit toleransi | Zaten best-effort |
| `safe_write_review_audits` normal Mikro akisindan ayrimi | Mevcut |
| `mobile_offline_sync_requests` olmadan Mikro read-back yedek yazimi | Henuz uygulanmadi |
| `edespatch_submissions` olmadan Uyumsoft + Mikro yedek dogrulamasi | Henuz uygulanmadi |

## Sonraki Teknik Oncelik

1. Her Mikro create akisi icin `mevcut evragi bul`, `tamligini dogrula` ve
   `sonucu geri oku` stratejisini standartlastir.
2. `OfflineCreateGuard` icinde yalniz destek deposunun gercekten kullanilamaz
   oldugu durumlarda bu stratejiye gec.
3. Dusurulmus modu health endpointi, log ve API cevabinda gorunur yap.
4. E-irsaliye icin Uyumsoft belge sorgusu olmadan `edespatch_submissions`
   bypass etme.
5. Entegrasyon testlerinde her P1 tablosunu ayri ayri kullanilamaz yaparak
   mukerrer belge olusmadigini kanitla.

Bu plan uygulanana kadar P1 tablolarinin silinmesi veya bilincli olarak devre
disi birakilmasi desteklenmez. Tablolar korunur; Mikro bagimsizligi kontrollu
ve testli yedek akislarla kademeli olarak artirilir.
