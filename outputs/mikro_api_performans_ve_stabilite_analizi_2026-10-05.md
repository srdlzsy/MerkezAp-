# Mikro API Performans ve Stabilite Analizi

Tarih: 2026-10-05

## Yonetici Ozeti

Canli sistemde gorulen ana problem kilit bekleme degil, cok buyuk `STOK_HAREKETLERI` tablosu uzerinde uygun indeks veya tarih siniri olmadan yapilan taramalardir.

En kritik iki sorgu grubu sunlardir:

1. FurpaMerkezApi'nin guvenli retry/readback sorgulari `sth_eticaret_kanal_kodu` alanindan iz ariyor. Bu alan icin indeks yok ve yedi farkli kod yolu sorguyu tarih siniri olmadan calistiriyor.
2. Mikro API urununun kendi icinde calistirdigi `SELECT COUNT(1) FROM STOK_HAREKETLERI WITH (NOLOCK)` sorgusu tum tabloyu sayiyor. Bu sorgu FurpaMerkezApi kaynak kodunda bulunmuyor; `program_name=Mikro API - Main` oturumundan geliyor.

Timeout degerini artirmak bu iki kok nedeni cozmez. Once sorgular daraltilmali, sonra gerekli indeks DBA/Mikro destegiyle planli bakimda degerlendirilmelidir.

## Uygulama Durumu

2026-10-05 tarihinde dusuk riskli kod duzeltmeleri uygulandi:

- Depolar arasi sevk, depo iadesi, firma sevki/iadesi, zayiat/masraf, virman ve firma mal kabul recovery sorgulari request hareket tarihinden uretilen yarim-acik gun araligiyla sinirlandi.
- E-irsaliye create recovery trace kontrolu tum tabloda trace aramak yerine `sth_evraktip + sth_evrakno_seri + sth_evrakno_sira` anahtariyla belgeyi okuyup belge satirlarindaki trace degerlerini dogrulayacak sekilde degistirildi.
- Axata G01, dynamic census, C02 ve legacy C03/C4 duplicate/readback sorgulari belge tipi, hareket tipi, iade tipi, depo, seri ve uygun oldugu yerde tarih araligiyla daraltildi. Siparis GUID'i bulunan C02 akisinda once indeksli `sth_sip_uid` yolu kullaniliyor; belge no ve aciklama kontrolleri ayri fallback sorgulari olarak calisiyor.
- Login, refresh, `/api/auth/me` ve session permission profili yuklemeleri `AsSplitQuery()` ile role/permission koleksiyonlarinin tek buyuk JOIN uzerinden kartesyen cogalmasini engelleyecek sekilde duzenlendi.
- Kritik Mikro sorgularina `Furpa:<QueryName>` SQL etiketi ve sure/sonuc sayisi olcumu eklendi. Bir saniyeyi gecen sorgular Warning, hata alan sorgular exception ayrintisiyla Error seviyesinde loglaniyor.
- API request/response sozlesmesi degismedi; `UI_API_DOKUMANI.md` guncellemesi gerekmiyor.
- Solution build basarili; 483 test basarili, SQL Server entegrasyon ortami isteyen 1 test atlandi.

## Canli Veritabani Bulgulari

Salt-okunur katalog sorgulariyla canli Mikro DB'de asagidaki durum dogrulandi:

| Bulgu | Deger |
|---|---:|
| `STOK_HAREKETLERI` yaklasik satir sayisi | 216.396.588 |
| Clustered veri boyutu | 51.427,8 MB |
| Tablo ve tum indekslerin toplam boyutu | 154.390,9 MB |
| `sth_eticaret_kanal_kodu` veri tipi | `nvarchar(25)`, nullable |
| Bu alani iceren indeks | Yok |
| SQL `MAXDOP` | 8 |
| Parallelism cost threshold | 50 |
| SQL maksimum bellek | 110.000 MB |
| Query Store | Acik |

`MAXDOP=8` ve cost threshold `50` tek basina yanlis gorunmuyor. Pahali taramalar sekiz cekirdegi birlikte kullandigi icin `CXPACKET` goruluyor. Ilk mudahale global `MAXDOP` degisikligi olmamalidir.

## Ikinci Tur Genis Tarama

Ilk duzeltmelerden sonra kaynak kod, canli Query Store ve canli indeks katalogu tekrar tarandi. Bu tarama, trace sorgulari disinda da agirlik yaratabilecek alanlar oldugunu gostermistir. Ancak bulgularin kaynagi ayni degildir:

| Bulgu | Durum | Sahiplik | Oncelik |
|---|---|---|---|
| `SELECT COUNT(1) FROM STOK_HAREKETLERI` | Canlida kanitlandi | Mikro API urunu | Kritik |
| Tarih siniri olmayan eski trace sorgulari | Query Store gecmisinde kanitlandi; kaynak kodda duzeltildi | FurpaMerkezApi | Duzeltmenin canliya alinmasi kritik |
| `fn_DepodakiMiktar` fonksiyonunun satir basina cok kez cagrilmasi | Canlida ve kaynakta kanitlandi | FurpaMerkezApi + Mikro fonksiyonu | Yuksek |
| Eski firma/depo liste SQL'leri | Query Store'da kanitlandi; guncel kaynakta esdeger filtreler sargable | Eski canli surum veya baska istemci | Yuksek, deployment sonrasi yeniden olculmeli |
| Axata `sth_HareketGrupKodu1` / `sth_aciklama` duplicate aramalari | Kaynakta ve indeks katalogunda kanitlandi; canli toplam etkisi henuz olculmedi | FurpaMerkezApi | Orta-yuksek |
| Stok anomali dormant taramasi | Kaynakta kanitlandi; kullanici aksiyonuyla calisan agir analiz | FurpaMerkezApi | Orta |
| Mikro ic fiyat farki/ciro/rapor rutinleri | Query Store'da kanitlandi | Mikro/harici raporlar | DBA ve Mikro destegi |

### Query Store Toplam Yuk Kaniti

Son yedi gunluk Query Store ozetinde su sorgular one cikmistir:

- Mikro API tam tablo sayimi 4.786 kez calismis, yaklasik 3,61 milyar logical read ve 124,8 milyon ms toplam CPU tuketmistir.
- Eski firma mal kabul trace sorgusu 1.688 kez calismis, yaklasik 2,57 milyar logical read tuketmis ve tek calismada 153 saniyeye kadar cikmistir. Bu kod yolu mevcut calisma agacinda tarih araligiyla daraltilmistir; Query Store verisi duzeltme oncesi/canli binary davranisini gostermektedir.
- Eski depolar arasi sevk trace sorgusu 98 kez calismis, yaklasik 554 milyon logical read tuketmis ve tek calismada 126 saniyeye kadar cikmistir. Bu kod yolu da mevcut calisma agacinda daraltilmistir.

Bu nedenle kod degisikliginin varligi tek basina yeterli degildir. Canli deployment sonrasi yeni query planlarinda tarih parametreleri gorulmeli ve eski query id'lerinin calisma sayisi artmamalidir.

### Satir Basina Stok Fonksiyonu

`dbo.fn_DepodakiMiktar` canli Query Store'da milyonlarca kez calisan sorgular arasindadir. Kaynak kodda urun/depo satirlarinin her biri icin cagrilabildigi 13 nokta vardir:

- stok raporlari
- var/yok
- manav operasyon paneli
- urun dagilimlari
- Trendyol Go fiyat/stok calisma ekrani

Fonksiyon tek bir stok ve depo icin kabul edilebilir olsa bile, yuzlerce veya binlerce sonuc satirinda tekrarlandiginda N+1 benzeri CPU ve logical read carpani olusturur. Ilk iyilestirme adimi fonksiyonu degistirmek degil, endpoint bazinda kac satir icin cagrildigini ve P95 suresini olcmektir. Yuksek hacimli raporlarda aday cozum, gerekli stok/depo ciftlerini once sinirlayip toplu hareket ozetiyle birlestirmektir.

### Eski Liste SQL'leri ve Deployment Kontrolu

Query Store'da `CONVERT(date, sth_create_date) = @dateToGet` kullanan genis firma hareketi listeleri ve yalniz cari+seri+sira ile hareket arayan eski sorgular bulunmustur. Kolonun uzerinde `CONVERT` kullanmak tarih indeksini verimsizlestirir.

Guncel `CompanyMovementListQueryExecutor` bunun yerine yarim-acik tarih araligi kullanir:

```text
date >= startDate AND date < endDateExclusive
```

Dolayisiyla burada ilk is yeni kod yazmak degil, IIS'teki yayinin guncel assembly oldugunu dogrulamak ve deployment sonrasinda eski query id'lerinin yeniden calisip calismadigini izlemektir.

### Axata Duplicate Kontrolleri

Canli indeks katalogunda:

- `sth_sip_uid` icin `NDX_STOK_HAREKETLERI_08` vardir; siparis GUID kontrolleri indeks kullanabilir.
- `sth_HareketGrupKodu1` icin indeks yoktur.
- `sth_aciklama` icin indeks yoktur.

Axata G01 ve dynamic census akislarinda `sth_HareketGrupKodu1 IN (...)`; outbound delivery akisinda `sth_aciklama IN (...)` veya aciklama esitligiyle duplicate kontrolu vardir. Bu sorgular tarih, belge veya depo ile yeterince daraltilmazsa 216 milyon satirda tarama uretebilir. Kod tarafinda belge tipi, hareket tipi, iade tipi, depo, seri ve uygun akislerde tarih kosullari eklenmistir. C02 kontrolu once mevcut `sth_sip_uid` indeksini kullanir; metin kontrolleri yalniz fallback olarak calisir. Sirf bu kullanim icin genis metin indeksi eklemek ilk tercih olmamalidir.

### Stok Anomali Taramasi

Dormant stok kuralinda pozitif bakiyeler icin son hareket `OUTER APPLY TOP (1)` ile tum hareket gecmisinden aranir. Kosul giris/cikis/nakliye depolarini `OR` ile birlestirdigi icin tek indeks yoluna iyi oturmaz. Bu endpoint surekli liste sorgusu degil, acik tarama aksiyonudur; yine de tum depolar ve `take=1000` ile mesai saatinde calistirilmasi agirlik yaratabilir.

Bu tarama icin depo zorunlulugu, daha dusuk batch boyutu ve kuyruk/background calisma degerlendirilmelidir.

### Mikro Icindeki Nadir Ama Cok Uzun Raporlar

Query Store'da FurpaMerkezApi kaynak kodunda bulunmayan Mikro fiyat farki, ciro primi ve satis raporu sorgulari tek calismada yaklasik 20-80 dakika araligina cikmistir. Bu sorgular API endpointlerinden gelmese bile ayni SQL sunucusunun CPU ve I/O kaynaklarini tuketerek API isteklerini `runnable` veya `CXPACKET` beklemesine itebilir.

Bu nedenle API optimizasyonundan sonra da yavaslama surerse ayni zaman araliginda Mikro kullanicilarinin actigi raporlar Query Store query id ve baslangic zamaniyla eslestirilmelidir.

## Audit Verisi

Incelenen 45 Mikro API yazma audit kaydi:

| Durum | Adet |
|---|---:|
| `Recovered` | 30 |
| `Unknown` | 14 |
| `Failed` | 1 |

Sureler:

| Metrik | Deger |
|---|---:|
| Ortalama | 123,6 saniye |
| Medyan | 144,8 saniye |
| P90 | 168,6 saniye |
| Maksimum | 193,3 saniye |

Ek bulgular:

- 30 `Recovered` kaydin 24'u normal Mikro cevabindan sonra, 6'si `MikroAPI - TimeOut` cevabina ragmen DB readback ile dogrulanmistir.
- 14 `Unknown` kaydin 12'si `DahiliStokHareketKaydetV2`, 2'si `IrsaliyeKaydetV2` cagrilaridir.
- Bir kayit SQL deadlock nedeniyle kesin `Failed` olmustur. Ayni payload daha sonra yeniden denenmis ve ikinci sonuc `Unknown` kalmistir.
- Tum yazmalarda `attempt_count=1` olmasi olumludur. Unsafe POST cagrilari korlemesine otomatik tekrar edilmemistir.

Bu veri, Mikro timeout cevabinin "evrak olusmadi" anlamina gelmedigini kanitlar. Readback kaldirilmamali ve timeout sonrasinda yeni `clientRequestId` ile yeniden yazma yapilmamalidir.

## Kritik Bulgu 1: Indekssiz Trace Readback

Canlida gorulen sorgu:

```sql
SELECT ...
FROM STOK_HAREKETLERI
WHERE sth_evraktip = 17
  AND sth_tip = 2
  AND sth_cins = 6
  AND sth_normal_iade = 0
  AND sth_cikis_depo_no = @sourceWarehouseNo
  AND sth_eticaret_kanal_kodu = @traceKey;
```

Ornek anlik maliyetler:

- 6,1 saniye duvar suresinde 42,1 saniye toplam CPU.
- 2,1 saniye duvar suresinde 11,8 saniye toplam CPU.
- 0,66 saniye duvar suresinde 5,1 saniye toplam CPU.
- `blocking_session_id=0`: kilit yok.
- `wait_type=CXPACKET`: paralel tarama var.

Analiz sirasinda ayni desende tarih siniri olmayan yedi kod noktasi bulunuyordu:

1. Depolar arasi sevk recovery
2. Depo iadesi recovery
3. Firma sevki/iadesi recovery
4. Zayiat/masraf fisi recovery
5. Virman recovery
6. Firma mal kabul recovery
7. E-irsaliye create trace dogrulamasi

Bu sorgular dogru bir guvenlik amacina hizmet ediyor, ancak 216 milyon satir uzerinde indekssiz calismalari stabil degil.

### Kod Tarafinda Ilk Duzeltme (Uygulandi)

Her readback sorgusuna request'in belge/hareket tarihinden uretilen sargable yarim-acik tarih araligi eklenmelidir:

```csharp
movement.sth_tarih >= movementDate &&
movement.sth_tarih < movementDate.AddDays(1)
```

Kolon uzerinde `.Date`, `CAST` veya fonksiyon kullanilmamalidir. Boylece mevcut `sth_tarih` ve depo+tarih indeksleri kullanilabilir.

E-irsaliye akisi belge seri/sira bilgisini zaten biliyor. Burada once:

```text
sth_evraktip + sth_evrakno_seri + sth_evrakno_sira
```

ile mevcut `NDX_STOK_HAREKETLERI_05` indeksinden belge okunmali, trace degeri bulunan belge satirlari uzerinde dogrulanmalidir. Tum tabloda trace aranmamalidir.

## Kritik Bulgu 2: Mikro API Icindeki Tum Tablo COUNT

SQL oturumunda gorulen sorgu:

```sql
SELECT COUNT(1)
FROM STOK_HAREKETLERI WITH (NOLOCK);
```

Kaynak:

```text
host_name    = MIKRO-SUNUCU
program_name = MIKRO API - Main
```

Bu sorgu FurpaMerkezApi kodundan gelmiyor. Mikro API'nin dahili yazma akisinda calisiyor ve ayni SQL session'i daha sonra belge satir numarasi sorgularinda da kullaniliyor.

`NOLOCK` sorguyu ucuzlatmaz; yalniz paylasimli kilit davranisini degistirir. 216 milyon kaydi saymak yine CPU ve memory bandwidth tuketir. Mikro API saglayicisindan su degisiklikler istenmelidir:

- Her satir veya her evrak icin tam tablo sayimi kaldirilmali.
- Yaklasik satir sayisi gerekiyorsa metadata/partition istatistigi kullanilmali.
- Cok satirli evrak tek transaction ve toplu isleme ile yazilmali.
- Satir basi tekrar eden tablo kontrol sorgulari kaldirilmali.
- `sth_evrakno_seri` uzerindeki ifade/concat yerine dogrudan esitlik kullanilmali.
- Mikro'nun kendi 130-170 saniye civarinda urettigi `MikroAPI - TimeOut` kok nedeni incelenmeli.

## Kritik Bulgu 3: Paralel Yazma ve Deadlock

FurpaMerkezApi POST isteklerini otomatik retry etmiyor ve belge seri bazli application lock kullaniyor. Bunlar dogru korumalardir.

Ancak farkli endpoint veya seri yazmalari Mikro API tarafinda ayni anda calisabilir. Audit penceresinde ayni anda en fazla iki Mikro yazmasi gorulmesine ragmen bir deadlock olusmustur. Bu, Mikro API/SQL yazma yolunun dusuk paralellikte bile hassas oldugunu gosteriyor.

Oneri:

- Yalniz Mikro API write POST'lari icin konfigurasyonlu global concurrency gate eklenmeli.
- Baslangic degeri `MaxConcurrentWrites=1` olmali; olcumden sonra `2` denenebilir.
- GET/read cagrilari bu gate'e alinmamali.
- Kuyrukta bekleyen request yeni Mikro POST uretmemeli ve istemci iptalinde temizlenmeli.
- Birden fazla IIS instance varsa process-ici semaphore yeterli degildir; dagitik lock/queue gerekir.

Bu yaklasim throughput'u azaltabilir, ancak mevcut durumda her cagrinin 2-3 dakika surmesi ve deadlock/timeout uretmesi daha buyuk kayiptir.

## Kritik Bulgu 4: Unknown Kayitlarin Sonradan Toparlanmasi

Mevcut reconciliation worker eski `Pending` kayitlari `Unknown` yapar ve hatali `Succeeded` timeout kayitlarini duzeltir. Fakat endpoint'e ozel Mikro DB readback yapmaz.

Sonuc:

- Ilk request sirasinda gorunmeyen evrak daha sonra tamamlanirsa audit `Unknown` kalabilir.
- Kullanici ayni `clientRequestId` ile tekrar geldigi zaman readback yeniden calisabilir, ancak kullanici gelmezse otomatik toparlama olmaz.

Trace sorgulari optimize edildikten sonra endpoint'e ozel, sinirli ve artan beklemeli reconciliation eklenebilir:

```text
1 dakika -> 5 dakika -> 15 dakika -> son kontrol
```

Her kontrolde ayni payload snapshot'i ve ayni `clientRequestId` kullanilmali; Mikro'ya yeni write POST atilmamalidir.

## Yuksek Oncelik: Auth `/me` Sorgusu

Bu Mikro DB sorgusu degil, fakat ayni SQL altyapisinda CPU baskisindan etkileniyor. Kullanici, genel roller, client roller ve iki permission koleksiyonu tek buyuk JOIN ile yukleniyor. Canli snapshot'ta sorgu 26 saniye `runnable` durumda CPU sirasi beklemistir.

`AuthService.LoadUserAsync`, login, refresh ve session permission yuklemelerinde `AsSplitQuery()` uygulanmistir. Bu, role-permission koleksiyonlarinin kartesyen satir cogalmasini azaltir. Sorgular `Furpa:Auth:*` etiketiyle izlenir; login, refresh ve `/api/auth/me` profil yuklemelerinde sure olcumu de loglanir.

## Timeout Ayarlari

Canli ayarlar:

```text
Mikro API HTTP timeout : 300 saniye
Mikro DB read timeout  : 300 saniye
Mikro DB write timeout : 300 saniye
Unsafe POST retry      : kapali
```

Bu degerler mevcut semptomun kok nedeni degildir. Mikro API kendi response'u icinde 130-170 saniyede `MikroAPI - TimeOut` dondurmektedir. Furpa HTTP timeout'unu 300'den daha yukari almak bunu duzeltmez.

300 saniyelik SQL timeout genis raporlar icin gerekli olabilir; ancak yavas sorguyu normal kabul etme araci olmamalidir. Endpoint bazli sure, logical read ve sonuc satir sayisi olculmelidir.

## Indeks Secenegi

Kod tarafindaki tarih daraltmasi ilk ve en risksiz adimdir. Bundan sonra Mikro/DBA onayiyla trace icin filtreli indeks degerlendirilebilir.

Ornek aday, dogrudan uygulanacak migration degildir:

```sql
CREATE NONCLUSTERED INDEX IX_FR_STH_Trace
ON dbo.STOK_HAREKETLERI (sth_eticaret_kanal_kodu)
INCLUDE (
    sth_evraktip,
    sth_tip,
    sth_cins,
    sth_normal_iade,
    sth_cikis_depo_no,
    sth_giris_depo_no,
    sth_nakliyedeposu,
    sth_evrakno_seri,
    sth_evrakno_sira,
    sth_satirno
)
WHERE sth_eticaret_kanal_kodu IS NOT NULL
  AND sth_eticaret_kanal_kodu <> N''
WITH (MAXDOP = 2, SORT_IN_TEMPDB = ON);
```

Uyarilar:

- Indeks olusturmak 216 milyon satiri okuyacagi icin bakim penceresinde yapilmalidir.
- Disk ve transaction log boslugu onceden kontrol edilmelidir.
- Mikro surum guncellemelerinin ozel indeksleri etkileyip etkilemedigi saglayiciyla teyit edilmelidir.
- Canlida dogrudan denenmemeli; once staging veya production restore uzerinde sure/boyut olculmelidir.

## Mevcut Olumlu Noktalar

- Unsafe POST otomatik retry kapali.
- `clientRequestId` ve trace mekanizmasi mevcut.
- Timeout cevabi basarisizlik sayilip korlemesine ikinci evrak yazilmiyor.
- Readback satir butunlugunu kontrol ediyor.
- Belge seri tahsisi icin process-ici ve SQL application lock birlikte kullaniliyor.
- Liste ekranlari icin depo+tarih odakli ozel indeksler mevcut.
- `Application Name=FurpaMerkezApi` sayesinde SQL oturumlari ayirt edilebiliyor.

## Uygulama Sirasi

### Asama 1 - Kod, dusuk risk

1. [Tamamlandi] Trace readback sorgularini belge/hareket tarihi veya belge anahtariyla daralt.
2. [Tamamlandi] E-irsaliye trace dogrulamasini seri/sira indeksinden baslat.
3. Yeni binary canliya alindiktan sonra eski tarih filtresiz Query Store query id'lerinin tekrar calismadigini dogrula.
4. [Tamamlandi] Axata duplicate sorgularini tarih+belge tipi+depo ve varsa seri/sira ile daralt.
5. [Tamamlandi] Auth user-role-permission sorgularini split query/projection yap.
6. Audit'e `operationCode`, `clientRequestId`, belge seri/sira, depo ve beklenen satir sayisini ekle.
7. [Kismen tamamlandi] Kritik Axata ve recovery/readback sorgularina SQL etiketi, sure ve sonuc sayisi logu eklendi. Mikro write ve `fn_DepodakiMiktar` kullanan endpointlerin correlation id/P95 kapsami genisletilmeli.

### Asama 2 - Kontrollu yuk yonetimi

1. Mikro write concurrency gate'i `1` ile devreye al.
2. Kuyruk suresi, Mikro cagrisi suresi ve readback suresini ayri metriklerle izle.
3. Bir hafta veri sonrasi concurrency `2` testi yap.
4. Stok anomali taramasini depo bazli, dusuk batch ile ve mumkunse background kuyrukta calistir.

### Asama 3 - DB/Mikro saglayici

1. Trace indeksini restore/staging ortaminda test et.
2. Planli bakimda, DBA ve Mikro onayiyla uygula.
3. Mikro API'deki tam tablo `COUNT(1)` sorgusunun kaldirilmasini saglayicidan iste.
4. `fn_DepodakiMiktar` kullanan yuksek hacimli raporlari toplu ozet sorgulariyla karsilastir.
5. Mikro fiyat farki/ciro raporlarinin uzun planlarini query id bazinda Mikro destegiyle incele.
6. Mikro API'nin ic timeout ve satir-basi sorgu davranisini duzelt.

### Asama 4 - Otomatik reconciliation

1. `Unknown` kayitlari endpoint bazli readback ile tekrar kontrol et.
2. Tam eslesme varsa `Recovered` yap.
3. Parcali/farkli icerik varsa manuel inceleme durumuna al.
4. Asla yeni `clientRequestId` veya yeni Mikro write POST uretme.

## Yapilmamasi Gerekenler

- Readback kontrolunu kaldirmak.
- Timeout sonrasinda yeni `clientRequestId` ile tekrar yazmak.
- Sadece HTTP/SQL timeout degerini buyutmek.
- `NOLOCK` ekleyerek CPU sorununun cozuldugunu varsaymak.
- Ilk adim olarak sunucu genelinde `MAXDOP` dusurmek.
- Mikro tablo indeksini plansiz ve bakim penceresi olmadan olusturmak.
- Test etmeden Mikro API rotasini topluca dogrudan DB yazimina cevirmek.

## Kabul Kriterleri

Duzeltme sonrasi hedefler:

- Trace readback P95 suresi 500 ms altinda.
- Trace readback sorgularinda tam tablo paralel taramasi olmamasi.
- Mikro write `Unknown` oraninin yuzde 1'in altina inmesi.
- Deadlock sayisinin sifirlanmasi.
- Ayni mantiksal islem icin ikinci evrak olusmamasi.
- `/api/auth/me` P95 suresinin 1 saniye altinda olmasi.
- Mikro API yazma kuyrugu ve aktif write sayisinin gozlemlenebilir olmasi.

## Net Sonuc

FurpaMerkezApi'nin guvenli retry tasarimi dogru yonde, ancak readback sorgulari canli tablonun boyutuna gore optimize edilmemis. Mikro API urununun kendi tam tablo `COUNT(1)` sorgusu da ayni tabloyu tekrar tekrar tarayarak sorunu buyutuyor.

En iyi ve stabil model: kontrollu Mikro write concurrency, tarih/belge ile daraltilmis readback, gerekli filtreli indeks, korunan idempotency ve sonradan endpoint-bazli reconciliation birlikte uygulanmasidir.
