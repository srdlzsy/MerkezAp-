# Mikro API Timeout ve Write Audit Incelemesi

Tarih: 7 Ekim 2026

## Yonetici Ozeti

Sorun Furpa API, istemci agi veya IIS yoluyla sinirli degildir. Ayni yazma istegi Mikro
sunucusunda dogrudan `http://localhost:8084` adresine gonderildiginde de cok satirli
evrakta istemci timeout'a dusmektedir.

Kontrollu testlerin sonucu:

- Servis yeniden baslatilmadan once tek satirli localhost istegi 75,6 saniyede timeout
  oldu ve test evraki olusmadi.
- `MikroAPIContainer` servisi yeniden baslatildiktan sonra tek satirli istek 10,47
  saniyede HTTP 200 ile basarili oldu.
- Ayni servis durumunda 50 satirli istek 75,15 saniyede istemci timeout'una dustu.
- Timeout, sunucudaki islemi durdurmadi. Mikro SQL yazimi devam etti ve evrak 50/50
  satira yaklasik 273,48 saniyelik SQL yazim araliginda tamamladi.
- Timeout anindan sonra yazma yaklasik 200 saniye daha devam etti.
- Son kontrolde satir numaralari `0-49`, farkli GUID sayisi `50`, toplam miktar `0,05`
  ve aktif Mikro SQL istegi `0` idi.

Bu davranis nedeniyle timeout sonrasi ayni belgeyi kontrol etmeden tekrar POST etmek
mukerrer veya cakisan evrak riski olusturur. Mevcut Database yazma rotasinin korunmasi
kisa vadede dogru karardir.

## Kontrollu Testler

### Port ve servis

- Windows servisi: `MikroAPIContainer` / `Mikro API`
- Calisan proses: test oncesi PID `9572`
- `localhost:8084`: TCP acik
- `localhost:8094`: TCP kapali
- Mikro destek mesajinda belirtilen `8094`, bu kurulumda dinlenen port degildir.

### Servis yeniden baslatilmadan once

Test evraki: `TAPI56/213524137`

- Adres: `http://localhost:8084/Api/apiMethods/DahiliStokHareketKaydetV2`
- Satir sayisi: 1
- Istemci timeout siniri: 75 saniye
- Olculen sure: 75.598 saniye
- HTTP cevabi: yok
- Sonuc: timeout
- SQL readback: evrak bulunamadi

Bu test, istemci bilgisayari ile Mikro sunucusu arasindaki ag yolunu devre disi
biraktigi icin problemin yalnizca dis ag/guvenlik duvari kaynakli olmadigini gosterir.

### Servis yeniden baslatildiktan sonra tek satir

Test evraki: `TAPI56/213525054`

- Satir sayisi: 1
- Olculen sure: 10.474 saniye
- HTTP durum kodu: 200
- Mikro cevabi: basarili
- SQL readback: 1 satir

Servis yeniden baslatma tek satirli istegi gecici olarak duzeltmistir. Bununla birlikte
10,47 saniye tek satir icin yine yuksek bir suredir.

### Servis yeniden baslatildiktan sonra 50 satir

Test evraki: `TAPI56/213525276`

- Trace: `FT261007113436713`
- Satir sayisi: 50
- Her satirin miktari: `0,001`
- Toplam miktar: `0,05`
- Istemci timeout siniri: 75 saniye
- Istemcinin timeout suresi: 75.145 saniye
- HTTP cevabi: yok

SQL gozlemi:

- Ilk satir zamani: `11:35:40`
- Son satir zamani: `11:40:13`
- SQL'de gorulen yazim araligi: `273,48 saniye`
- Ara kontrollerde satir sayisi 23, 26, 28, 29, 31 ... 50 olarak ilerledi.
- Ara okumalar `NOLOCK` ile yapildigi icin bu ilerleme tek basina her satirin ayri
  transaction ile commit edildigini kanitlamaz. Ancak istemci koptuktan sonra sunucu
  isleminin devam ettigini kesin olarak gosterir.
- Nihai satir sayisi: 50
- Nihai farkli satir no sayisi: 50
- Nihai farkli hareket GUID sayisi: 50
- Min/max satir no: 0/49
- Nihai toplam miktar: `0,05`

Ayni zaman araliginda `MİKRO APİ - Main` SQL oturumu aktif gorulmustur.
Gozlenen istekte `blocking_session_id=0` ve `CXPACKET` beklemesi vardi. Bu gozlem
kilit blokajindan cok paralel sorgu/CPU calismasina isaret eder; ancak tek basina tam
kok nedeni kanitlamaz.

## mikro_api_write_audits Analizi

### Tablo ne ise yarar?

`mikro_api_write_audits`, Furpa API uzerinden Mikro'ya yapilan yazma cagrilarini
izler. Akis su sekildedir:

1. Mikro isteginden once `Pending` kaydi acilir.
2. Endpoint sonucu kesin basariysa `Succeeded`, kesin is kurali hatasiysa `Failed`,
   timeout/baglanti kopmasi gibi sonuc kanitlanamiyorsa `Unknown` yazilir.
3. Ilgili is servisi Mikro DB readback ile evraki ve satir icerigini dogrularsa kayit
   `Recovered` olur.
4. Arka plan siniflandiricisi 5 dakikada bir calisir. 15 dakikadan eski `Pending`
   kayitlari `Unknown` yapar; kendisi tekrar Mikro yazimi veya business readback yapmaz.

Tablo request ID, correlation ID, endpoint, payload SHA-256 hash, HTTP/Mikro durumlari,
sinirlandirilmis/redakte response, hata, deneme sayisi ve sureyi saklar. Ham payload
ve Mikro sifresi saklanmaz.

### Tum endpointler

Canli tabloda 11 Agustos-6 Ekim 2026 araliginda toplam 1.411 kayit bulundu:

| Durum | Adet | Anlam |
|---|---:|---|
| Succeeded | 920 | Dogrudan basarili tamamlanan audit |
| Recovered | 283 | Is servisi tarafindan SQL/readback ile dogrulanan sonuc |
| Unknown | 185 | Yazma sonucu kesinlestirilememis kayit |
| Failed | 23 | Kesin hata |

### DahiliStokHareketKaydetV2

Bu endpoint icin toplam 254 audit kaydi bulundu:

| Durum | Adet | Oran | Ortalama sure | Maksimum sure |
|---|---:|---:|---:|---:|
| Recovered | 135 | %53,15 | 95,46 sn | 193,29 sn |
| Unknown | 105 | %41,34 | 132,21 sn | 170,02 sn |
| Failed | 10 | %3,94 | 35,18 sn | 106,23 sn |
| Succeeded | 4 | %1,57 | 0,81 sn | 2,25 sn |

Onemli yorum:

- `Recovered` her zaman once timeout oldugu anlamina gelmez. Depolar arasi sevk kodu,
  basarili Mikro cevabindan sonra da SQL readback yapar ve audit'i `Recovered` yapar.
- 135 Recovered kaydin 128'inde hata bos, 7'sinde `MikroAPI - TimeOut` vardir.
- 105 Unknown kaydin 95'inde acikca `MikroAPI - TimeOut`, 5'inde istek iptali ve
  5'inde eski timeout kaydinin sonradan duzeltilmesi vardir.
- Boylece 254 kaydin en az 107'sinde timeout gecmisi vardir: yaklasik `%42,13`.
- SQL/readback ile kanitlanmis basari `Recovered + Succeeded = 139` kayittir: `%54,72`.
- Unknown response'larda HTTP 200 ve Mikro status 200 gorulebilmesine ragmen response
  govdesi `success:false` ve `errorText:"MikroAPI - TimeOut"` olabilmektedir. Bu nedenle
  yalniz HTTP 200 basari kaniti degildir.

Kesin hatalarin dagilimi:

- 6 adet U17/gider muhasebe kodu bos hatasi
- 2 adet ayni seri/sira ile evrak mevcut hatasi
- 1 adet SQL deadlock
- 1 adet genel stok hareketi kaydedilemedi hatasi

### 7 Ekim testleri neden audit tablosunda yok?

7 Ekim kontrollu testleri Furpa API endpointinden degil, dogrudan Mikro API
`localhost:8084` adresinden gonderildi. Bu nedenle bizim audit servisi devreye girmedi.
SQL kontrolunde 7 Ekim icin audit sayisi `0` bulundu. Test kanitlari Mikro
`STOK_HAREKETLERI`, PowerShell sonuc JSON'lari ve canli SQL izlemesinden gelmektedir.

### Audit tablosundaki eksikler

Kok neden ve performans analizi icin tabloya ileride su alanlar eklenebilir:

- Belge seri/sira ve satir sayisi
- Kaynak/hedef/transit depo
- Islem/modul kodu ve `clientRequestId`
- Mikro servis makinesi, proses ve servis versiyonu
- Timeout sonrasi ilk/son readback zamani
- `Recovered` alt nedeni: normal basari dogrulamasi, timeout recovery veya duplicate recovery

Mevcut `recovered_document_no` alani dahili sevklerde cogunlukla bos kalmaktadir;
135 Recovered kaydin 127'sinde GUID varken hicbirinde belge no dolu degildir. Seri/sira
ayri alanlar olarak saklanirsa operasyonel takip kolaylasir.

## Teknik Sonuc

1. Sorun dis agdan kaynaklanmiyor; localhost'ta tekrarlandi.
2. Servis restart'i takilmis tek satir davranisini gecici olarak duzeltti.
3. Cok satirli yazmada temel performans sorunu devam ediyor.
4. Istemci timeout'u sunucudaki yazmayi iptal etmiyor.
5. Timeout sonrasi kontrolsuz retry kesinlikle yapilmamali.
6. Mikro API'nin sabit timeout suresi, 50 satirin yaklasik 4,5 dakikalik islem
   suresiyle uyumlu degil.
7. Kisa vadede `MikroWriteRouting:InterWarehouseShipment=Database` korunmalidir.
8. Mikro API rotasi kullanilacaksa timeout sonrasi ayni trace/seri/sira ile readback
   tamamlanmadan yeni create gonderilmemelidir.

## Mikro Destek Icin Mail

Konu: DahiliStokHareketKaydetV2 - localhost testinde timeout sonrasi islemin devam etmesi ve 50 satirin 273 saniyede yazilmasi

Merhaba,

Daha once ilettigimiz `/Api/apiMethods/DahiliStokHareketKaydetV2` timeout problemi
icin, ag etkisini elemek amaciyla Mikro API'nin kurulu oldugu sunucuda dogrudan
`localhost` uzerinden kontrollu testler yaptik.

Sunucumuzdaki Mikro API Windows servisi `MikroAPIContainer` adiyla calismakta ve
gercek dinleme portu `8084` olarak gorunmektedir. `localhost:8084` TCP testi basarili,
`localhost:8094` ise kapali durumdadir.

Test sonuclarimiz asagidadir:

1. Servis yeniden baslatilmadan once tek satirli istek

- Adres: `http://localhost:8084/Api/apiMethods/DahiliStokHareketKaydetV2`
- Test evraki: `TAPI56/213524137`
- Satir sayisi: 1
- Sonuc: 75,6 saniye sonunda HTTP cevabi gelmeden timeout
- Veritabani kontrolu: test evraki olusmadi

2. `MikroAPIContainer` servisi yeniden baslatildiktan sonra tek satirli istek

- Test evraki: `TAPI56/213525054`
- Satir sayisi: 1
- Sonuc: HTTP 200, basarili
- Toplam sure: 10,47 saniye
- Veritabani kontrolu: 1/1 satir olustu

3. Servis yeniden baslatildiktan sonra 50 satirli istek

- Test evraki: `TAPI56/213525276`
- Trace: `FT261007113436713`
- Satir sayisi: 50
- Satir basi miktar: `0,001`; toplam miktar: `0,05`
- Istemci sonucu: 75,15 saniye sonunda HTTP cevabi gelmeden timeout
- Buna ragmen sunucudaki islem durmadi ve SQL yazimi devam etti
- Ilk satir zamani: 11:35:40
- Son satir zamani: 11:40:13
- SQL'de olculen yazim araligi: 273,48 saniye
- Nihai kontrol: 50/50 satir, satir numaralari 0-49 ve 50 farkli hareket GUID'i
- Timeout sonrasinda islem yaklasik 200 saniye daha devam etti

Ayni zaman araliginda Mikro API SQL oturumunda bloklayan session olmadan
`CXPACKET` beklemesi gozlemledik. Bu nedenle testimizde klasik bir lock blocking
durumu tespit etmedik. Bununla birlikte endpointin ic sorgu ve islem adimlarini
goremedigimiz icin kok nedenin Mikro API servis loglariyla incelenmesi gerekiyor.

Ayrica kendi uygulama audit verimizde, 11 Agustos-5 Ekim 2026 arasinda bu endpoint
icin 254 cagri bulunuyor. Bunlarin 105'i sonucu belirsiz (`Unknown`), 10'u kesin hata,
139'u ise dogrudan veya Mikro DB readback ile dogrulanmis basaridir. En az 107 kayitta
timeout gecmisi bulunmaktadir. Unknown kayitlarin 95'inde response govdesi acikca
`MikroAPI - TimeOut` icermektedir.

Dikkat ceken diger konu, timeout olmasina ragmen sunucu isleminin devam etmesidir.
Istemci ayni istegi yeniden gonderirse ilk islem arka planda devam ettigi icin mukerrer
veya seri/sira cakismasi riski olusmaktadir.

Asagidaki konularda inceleme ve yonlendirme rica ediyoruz:

1. `DahiliStokHareketKaydetV2` neden satirlari bu kadar uzun surede isliyor?
2. 50 satirli istegin yaklasik 273 saniye surmesi beklenen bir davranis midir?
3. Istemci baglantisi timeout olduktan sonra islemin devam etmesi tasarlanan davranis
   midir? Bu durumda guvenli retry icin onerilen resmi yontem nedir?
4. Endpoint evrak bazinda atomik transaction garantisi veriyor mu? Timeout veya servis
   kesintisinde kismi evrak riski var midir?
5. Onerilen maksimum satir sayisi veya resmi batch/parcalama yontemi var midir?
6. Bu endpoint icin bilinen performans problemi, guncel surum veya hotfix bulunuyor mu?
7. `MikroAPIContainer` servisinde kuyruk/thread tikanmasi veya kaynak sizintisi olup
   olmadigini hangi log/performance counter ile kontrol edebiliriz?
8. Kurulumumuzda aktif port 8084 iken onceki yonlendirmenizde 8094 belirtilmisti.
   Bu port farki surum veya kurulum konfigurasyonundan mi kaynaklanmaktadir?

Inceleme icin 7 Ekim 2026 tarihinde 11:35:37-11:40:13 saat araligindaki Mikro API
servis loglarinin, endpoint islem adimlarinin ve SQL sorgularinin kontrol edilmesini
rica ederiz.

Guvenlik nedeniyle kimlik bilgisi iceren test scriptini paylasmayacagiz. Talep edilirse
maskelenmis payload, sonuc JSON'lari ve SQL satir zamanlari ayrica iletilebilir.

Saygilarimizla,

Necip Muzaffer
Furpa Marketler Zinciri
