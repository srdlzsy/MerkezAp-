# Proje Genel Kod Incelemesi

Tarih: 2026-10-02

## Duzeltme durumu

Kullanicinin sonraki duzeltme talebiyle 1, 2, 3 ve 4 numarali bulgular icin kod/test/migration degisiklikleri uygulandi. Asagidaki bulgu metinleri ilk incelemenin tarihsel tespitleridir; acik kalanlarla karistirilmamalidir.

- Tam satir recovery, firma e-irsaliye create kontrolu, kalici manuel inceleme ve revision tabanli atomik retry eklendi.
- Yeni Auth migration'i `20261002111146_HardenOfflineCreateRecovery`; 2026-10-02'de `FurpaMerkezDb` Auth DB'ye uygulandi. Once checksum'li COPY_ONLY yedek/RESTORE VERIFYONLY, sonra migration history/kolon/durum sayisi dogrulamasi yapildi. Uygulama kodu yayini ve canli smoke bu adimda yapilmadi.
- Son test: 479 basarili, 0 basarisiz, 0 atlanan. Yerel SQL Server migration/eszamanlilik testi dahil.
- 5-12 numarali bulgular bu degisiklik kapsaminda kapatilmadi; genel proje icin kosulsuz canli onayi verilmedi.
- UI/legacy dokumanlari guncellendi. Yayin sirasi ve acik riskler: [Canliya Gecis Kontrol Listesi](../docs/CANLIYA_GECIS_KONTROL_LISTESI.md).

## Kapsam ve dogrulama

Mikro create/readback/idempotency, e-irsaliye, kimlik ve istemci rolleri, legacy kopru, proxy ayarlari, arka plan isleri ve ilgili testler incelendi. UI API dokumani ayrica test hostunda ApiExplorer/Swagger ile tum kayitli 714 method/route ve 643 JSON model uzerinden contract referansina baglandi. Bu rapor canli sistemdeki her business davranisin ucundan uca dogrulandigi anlamina gelmez.

- `dotnet test FurpaMerkezApi.sln --no-restore --verbosity quiet`: basarili.
- Infrastructure: 281; WebApi: 141; toplam 422 basarili, 0 basarisiz, 0 atlanan.
- Bulgular kaynak koddan cikarilan hata senaryolaridir. Canli veride bu senaryolarin gerceklestigi ayrica kanitlanmadi.
- Canli DB'ye yazma, gercek Mikro/Uyumsoft gonderimi veya guvenlik acigini kullanma denemesi yapilmadi.
- Terminal projesine ve uygulama koduna dokunulmadi. Yalnizca bu rapor eklendi.
- Yerel `appsettings.Production.json` Git tarafindan ignore ediliyor; repoya commit edilmis secret bulgusu olarak degerlendirilmedi. Canli ortam degiskenleri, IIS/proxy/firewall ayarlari ayrica incelenmedi.

P1: Veri butunlugu veya yetkilendirme nedeniyle oncelikli duzeltme. P2: Planli olarak ele alinmasi gereken guvenilirlik/operasyon sorunu.

## Bulgular

### 1. [P1] Bazi create akislarinda eksik veya farkli evrak basarili kurtarma sayiliyor

Kaynaklar:

- [CompanyMovementWriteService.cs](../src/FurpaMerkezApi.Infrastructure/Modules/Common/CompanyMovements/CompanyMovementWriteService.cs): 391, 430, 468, 504.
- [StockReceiptWriteService.cs](../src/FurpaMerkezApi.Infrastructure/Modules/StokIslemleri/Common/StockReceiptWriteService.cs): 266, 368, 445.
- [VirmanWriteService.cs](../src/FurpaMerkezApi.Infrastructure/Modules/StokIslemleri/Virmanlar/VirmanWriteService.cs): 382, 416, 461.

Firma sevki/iadesi, zayiat/masraf ve virman kurtarma sorgulari satir bulunmasini ve tek evrak basligini kontrol ediyor; orijinal istekteki stok, miktar, birim ve tum satirlarin tamamlanmis olmasini dogrulamiyor. Offline trace ile bulunan satirlarda da ayni eksiklik var. Bazi normal recovery sorgularinda trace eslesmesi de aranmiyor.

Ornek: 10 satirlik istegin sadece 3 satiri gorunurse bu 3 satirdan basarili response uretilip offline kaydi Completed yapilabilir. Ayni seri/sira ile mevcut baska icerik bulunmasi da gercek kurtarma ile karistirilabilir. Ham Mikro response satir sayisi kontrolu, DB icerigi dogrulamasinin yerine gecmez.

Cozum: Isleme ozel tam eslesme denetimi; trace, evrak kapsami, stok, miktar, birim ve satir coklugu dogrulanmali. Virmanda beklenen giris/cikis ciftleri de kontrol edilmeli. Eksik gorunum belirsiz kalmali, kanitlanmis farkli icerik manuel incelemeye alinmali.

Test: Kismi gorunen satirlar, ayni sayida farkli stok, degisen miktar/birim ve eksik virman karsi hareketi basari donmemeli.

Onemli duzeltme: Dokumandaki butun genisletilmis akislarin tam icerik eslesmesi yaptigi ifadesi mevcut kodla uyusmuyor. Depolar arasi sevkteki matcher korumasi bu servislerin tamaminda yok.

### 2. [P1] Firma e-irsaliyesinde orijinal create iceriginin tamamligi kontrol edilmiyor

Kaynak: [EDespatchService.cs](../src/FurpaMerkezApi.Infrastructure/Services/EDespatchService.cs): 266, 277, 976, 1249, 1360.

Depolar arasi gonderimde `EnsureInterWarehouseDocumentCompleteAsync` var. Firma hareketlerinde gonderim mevcut Mikro satirlarindan kuruluyor; orijinal create isteginin tamamligina iliskin esdeger kontrol yok. JWT akisi `expectedLineCount` gondermediginde sayi kontrolu engel olusturmuyor.

Iki okumada gorunen snapshot'in ayni kalmasi, evrakin tamam oldugunu kanitlamaz. Ilk bulgudaki erken create basarisi ardindan firma e-irsaliyesi gonderilirse sabit kalan eksik satir seti Uyumsoft'a gidebilir.

Cozum: Bizim API'de olusturulan firma sevki/iadesinde saklanan create istegiyle gonderilecek satirlar karsilastirilmali. Legacy'deki beklenen sayi kontrolu korunmali; ancak satir sayisinin tek basina icerik eslesmesi olmadigi unutulmamali.

Test: Create tamamlanmadan JWT gonderimi Uyumsoft cagrisina ulasmamali.

### 3. [P1] Manuel inceleme kararinin sonraki POST icin kalici engeli yok

Kaynaklar:

- [MikroApiCreateConflictFactory.cs](../src/FurpaMerkezApi.Infrastructure/Services/MikroApi/MikroApiCreateConflictFactory.cs): 11.
- [OfflineCreateGuard.cs](../src/FurpaMerkezApi.Infrastructure/OfflineSync/OfflineCreateGuard.cs): 69.
- [MobileOfflineSyncService.cs](../src/FurpaMerkezApi.Infrastructure/OfflineSync/MobileOfflineSyncService.cs): 197, 203, 210, 220.

HTTP cevabinda `MIKRO_DOCUMENT_CONTENT_MISMATCH` ve `retryable=false` uretiliyor. Ancak offline failure kaydina hata kodu/karari yerine yalniz exception mesaji aktariliyor. Sonraki acquire, recovery sonuc bulamazsa ve mesaj timeout/belirsiz sonuc sinifina girmiyorsa Failed kaydi yeniden Processing yapip execute izni veriyor.

Dolayisiyla arayuzun tekrar butonunu kapatmasi sunucu garantisi degil. Ayni id ile tekrar gelen istek, ozellikle eslesen trace bulunmayan duplicate evrak uyusmazliginda, yeniden yazma yoluna girebilir.

Cozum: Siniflandirilmis hata kodu ve terminal manuel inceleme durumu kalici saklanmali. Ayni id, yetkili ve denetlenebilir bir cozum olmadan create yoluna geri donmemeli. Kesin reddedilen ve duzeltildikten sonra tekrar denenebilen is kurallari ayri ele alinmali.

Test: Manuel inceleme response'undan sonra ayni POST tekrarlansa bile Mikro create cagri sayisi artmamali.

### 4. [P1] Basarisiz istegi yeniden sahiplenme atomik degil

Kaynaklar:

- [MobileOfflineSyncService.cs](../src/FurpaMerkezApi.Infrastructure/OfflineSync/MobileOfflineSyncService.cs): 210.
- [MobileOfflineSyncRequestConfiguration.cs](../src/FurpaMerkezApi.Infrastructure/Persistence/Configurations/MobileOfflineSyncRequestConfiguration.cs).

Iki paralel retry ayni Failed kaydi okuyabilir, recovery'de sonuc bulamayabilir ve ikisi de RestartProcessing/SaveChanges sonrasi Proceed alabilir. Kaydin ilk olusturulmasindaki unique index, mevcut satirin yeniden sahiplenilmesini tekil hale getirmiyor. Bu geciste rowversion veya kosullu durum guncellemesi yok.

Cozum: Surum/durum ve sahiplik kosullu atomik acquire; yalniz bir istek calistirmayi kazanir. Digeri Processing cevabi alir. Gerekiyorsa lease sahibi/fencing bilgisi de tutulur.

Test: Gercek SQL Server uzerinde iki ayri context/baglanti ile ayni Failed istege paralel retry; yalniz bir execute.

### 5. [P1] Mikro API rotasinda seri/sira uretimi eszamanli isteklerde cakismaya acik

Kaynaklar:

- [StockReceiptWriteService.cs](../src/FurpaMerkezApi.Infrastructure/Modules/StokIslemleri/Common/StockReceiptWriteService.cs): 171, 521.
- [CompanyMovementWriteService.cs](../src/FurpaMerkezApi.Infrastructure/Modules/Common/CompanyMovements/CompanyMovementWriteService.cs): 198, 560.
- [VirmanWriteService.cs](../src/FurpaMerkezApi.Infrastructure/Modules/StokIslemleri/Virmanlar/VirmanWriteService.cs): 195, 540.

API rotalari `MAX(sth_evrakno_sira) + 1` ile numara alip dis servise yaziyor. Database rotasindaki Serializable transaction benzeri bir numara rezervasyonu bu API yolunda yok. Farkli clientRequestId kullanan iki cihaz ayni seri/sirayi secebilir.

Sonuc Mikro'nun davranisina gore duplicate reddi veya yanlis evrakla recovery riski; 1. bulgu bunun etkisini buyutuyor. Mevcut Mikro davranisi incelenmeden kesin satir birlesmesi oldugu iddia edilmiyor.

Cozum: Seri/evrak turu kapsaminda kalici numara rezervasyonu veya koordineli kilit. Diger uygulamalarin ayni seriyi kullanmasi da tasarima dahil edilmeli. Belirsiz yazma sonrasi korlemesine yeni sira tahsis edilmemeli.

Test: Ayni seride farkli id'li paralel create; her istegin kendi tam evrakina baglanmasi.

### 6. [P1] Pasif istemci rolu genel rollere geri dusup yetkiyi genisletebilir

Kaynak: [SessionAccessProfileResolver.cs](../src/FurpaMerkezApi.Infrastructure/Authentication/SessionAccessProfileResolver.cs): 40, 46, 55, 60.

Etkin istemci rolleri once `IsActive` ile filtreleniyor. Sonuc bos ise yalniz aktif `SubeKullanicisi` teknik rolu bulunan hesap fail-closed davraniyor; diger hesaplar genel rollerine donuyor.

Ornek: Genis genel rol tasiyan klasik kullaniciya dar web profili atanir; web rolu pasif edilirse genel genis yetkiler yeniden acilir. Teknik SubeKullanicisi rolunun pasif edilmesi de fallback kararini degistirebilir.

Cozum: Profil/eslesme varligi ile rolun aktifligi ayri degerlendirilmeli. Tanimli profilin aktif rolu kalmadiginda bos yetki donmeli. Genel rol fallback'i yalniz gercekten profilsiz klasik kullanicilar icin olmali; eksik istemci profili kurali acik tanimlanmali.

Test: Klasik ve birlesik hesapta istemci rolunu pasife alma, eksik istemci eslesmesi, teknik rol pasife alma.

### 7. [P1] Legacy koprude Origin/Referer kimlik dogrulama yerine kullaniliyor

Kaynak: [LegacyEDespatchBridgeController.cs](../src/FurpaMerkezApi.WebApi/Controllers/Legacy/LegacyEDespatchBridgeController.cs): 13, 64, 83, 378.

Controller anonim. Origin/Referer istemcinin gonderdigi header'lardan okunuyor; browser disindaki istemci bunlari kendisi belirleyebilir. CORS ve origin listesi kullaniciyi veya guvenilir sunucuyu dogrulamaz. Yerel production dosyasinda kopru acik, depo listesi bos; bos liste tum depolari dislamadan kabul ediyor.

API'ye ag erisimi olan yetkisiz bir istemci, ag katmaninda ek koruma yoksa, bu endpointlerden gonderim/PDF islemi deneyebilir. Canli ag erisimi ve firewall kurallari dogrulanmadi.

Cozum: Eski arayuzun sunucusundan sunucuya kimligi dogrulanmis kopru; donusturulebilir servis anahtari veya mTLS ve ag kisitlamasi. Sirri browser JavaScript'ine koymak cozum degil. Depo allowlist'i daraltilmali; normal JWT akisina dokunulmamalidir.

Test: Sadece dogru Origin gonderen kimliksiz istemci reddedilmeli; izinli servis ve depo kabul edilmeli.

### 8. [P1] Proxy guveni terminal IP/depo kontrolunu zayiflatiyor

Kaynaklar:

- [Program.cs](../src/FurpaMerkezApi.WebApi/Program.cs): 102, 104.
- Yerel `src/FurpaMerkezApi.WebApi/appsettings.Production.json`: 383.
- [AuthService.cs](../src/FurpaMerkezApi.Infrastructure/Services/AuthService.cs): 105.

`TrustAllNetworks=true` iken KnownNetworks/KnownProxies temizleniyor; ForwardLimit sinirsiz. Terminal kontrolunde kullanilan RemoteIpAddress forwarded header zincirinden etkileniyor.

Uygulamaya dogrudan erisilebiliyorsa veya on proxy istemcinin header'ini guvenli sekilde yenilemiyorsa sahte X-Forwarded-For terminalin sube-ag kontrolunu atlatabilir. Canli proxy topolojisi incelenmedigi icin acigin disaridan kullanilabildigi kesinlestirilmedi.

Cozum: Yalniz gercek proxy adreslerine guven, hop limitini tanimla ve backend'e sadece proxy erisimini sagla. Ayar degisikligi gercek IIS/proxy topolojisiyle test edilmeli.

### 9. [P1] Eski POS fiyat retry'i yeni fiyati geri alabilir

Kaynak: [TrendyolGoBranchPosPriceSyncService.cs](../src/FurpaMerkezApi.Infrastructure/Modules/EntegrasyonIslemleri/TrendyolGo/TrendyolGoBranchPosPriceSyncService.cs): 49, 136, 154.

Basarisiz gorev ileri tarihe ertelenirken yeni gorev islenebiliyor. Upsert, fiyat/guncelleme tarihinin hedefteki kayittan yeni oldugunu kontrol etmeden yaziyor. PostgreSQL advisory lock paralel cakismayi siralar, ancak eskimis veriyi engellemez.

Ornek: 100 TL'lik A gorevi hata alir, 120 TL'lik B gorevi basarili olur, sonra A retry edilince fiyat tekrar 100 TL olur. Bu dis Trendyol HTTP istek sayisi degil, sube POS fiyat senkronizasyonunun siralama sorunudur.

Cozum: Stok/depo/liste/birim bazinda monoton surum veya guvenilir kaynak tarihi; eskimis gorevi atla ya da son istenen durumu yaz. Kosullu UPDATE eklenirse INSERT fallback'inin mevcut ama daha yeni kaydi duplicate etmemesi de saglanmali.

Test: Eski gorev hata -> yeni gorev basari -> eski gorev retry; hedef yeni fiyatta kalmali.

### 10. [P2] Uyumsoft kesin reddi kalici Unknown dongusune girebilir

Kaynaklar:

- [EDespatchService.cs](../src/FurpaMerkezApi.Infrastructure/Services/EDespatchService.cs): 85, 141, 271, 290, 350, 2753.
- [EDespatchSubmission.cs](../src/FurpaMerkezApi.Domain/Entities/EDespatchSubmission.cs): 16, 77.

Gonderimden once Unknown submission kaydi aciliyor. Uyumsoft kesin basarisiz response dondugunde EnsureSucceeded exception firlatiyor; rezervasyon ayri bir kesin ret durumuna gecmiyor. Mevcut Unknown yeni gonderimi engelliyor, worker outbox'ta belge aramayi surduruyor.

Bu nedenle duzeltilebilir adres/icerik reddi de timeout gibi belirsiz sonuc olarak kilitlenebilir. Sonsuz sorgu ve operasyonel bekleme olusur.

Cozum: Kanitlanmis servis reddini Rejected benzeri ayri duruma al; duzeltme sonrasi kontrollu tekrar politikasi tanimla. Timeout/baglanti kopmasinda Unknown korunsun, otomatik yeniden gonderim yapilmasin. Uzun bekleyen Unknown kayitlari icin alarm ve manuel inceleme mekanizmasi ekle.

Test: Kesin ret ile timeout farkli durumlara dusmeli; timeout yeni belge gondermemeli.

### 11. [P2] Refresh token ayni anda iki kez tuketilebilir

Kaynaklar:

- [AuthService.cs](../src/FurpaMerkezApi.Infrastructure/Services/AuthService.cs): 125, 157.
- [AppRefreshTokenConfiguration.cs](../src/FurpaMerkezApi.Infrastructure/Persistence/Configurations/AppRefreshTokenConfiguration.cs).

Iki paralel refresh ayni aktif token'i okuyup ikisi de revoke ve yeni token ekleme yapabilir. Unique TokenHash yeni uretilen farkli tokenlari engellemez; eski token'i tuketme kosullu/atomik degil.

Sonuc: Tek refresh token iki aktif ardil oturuma dallanabilir. Birinin logout edilmesi digerini kapatmaz.

Cozum: Aktif eski token'in atomik tuketimi ve tek ardil garantisi. Kaybeden cagri icin belirli bir hata/yenileme politikasi; yalniz frontend'de istek siralama yeterli degil.

Test: Ayni refresh token ile iki eszamanli cagri; en fazla bir yeni aktif ardil.

### 12. [P2] Operasyon dosya kuyrugu restart'ta kayboluyor ve sinirsiz buyuyor

Kaynak: [OperationsJobQueue.cs](../src/FurpaMerkezApi.Infrastructure/Modules/OperasyonIslemleri/Operations/OperationsJobQueue.cs): 10, 11, 13, 25.

Dosya uretim isleri unbounded Channel ve bellek ici ConcurrentDictionary'de tutuluyor. Tamamlanan kayitlar temizlenmiyor. IIS recycle/restart bekleyen isleri ve durum sorgusunu kaybettirir; uzun calisma bellek buyumesine neden olur.

Cozum: Dayaniklilik beklenen isler icin kalici durum/kuyruk; bounded kapasite ve yogunlukta kontrollu ret/bekletme. Tamamlanan isler icin saklama suresi/temizlik.

Bu, e-irsaliyenin kalici submission/metadata kuyrugundan farkli bir kuyruktur; e-irsaliye kuyrugu gereksiz diye kaldirilmamalidir.

## Dokuman ve test aciklari

- [UI_API_DOKUMANI.md](../docs/UI_API_DOKUMANI.md), Guvenli Retry bolumu: Tum genisletilmis create akislarinin tam satir/icerik kontrolu yaptigi garanti, 1. bulgu duzeltilmeden verilmemeli.
- Ayni bolumde belirsiz sonucta yeni id uretme yasagi ile pending formu yeni id ile duzenleme yonlendirmesi birlikte bulunuyor. Bagimsiz yeni islem ile sonucu belirsiz onceki islemin yerine gecme ayrilmali; eski pending kaydi kaybedilmemeli.
- 422 testin gecmesi degerli, ancak InMemory EF testleri SQL Server eszamanlilik/kilit/sahiplenme davranisini kanitlamaz.
- Oncelikli ek testler: gec gorunen parcali satirlar, duplicate farkli icerik, eszamanli retry ve numara tahsisi, kalici manuel inceleme, pasif istemci rolu, kesin Uyumsoft reddi, eski fiyat gorevi ve paralel refresh.
- Repoda GitHub Actions workflow gorulmedi; harici CI kullaniliyor olabilir. Build/test ve bu regresyon testleri merge oncesi otomatik kapida calismali.

## Gelistirme ve sadelestirme

1. `EDespatchService` ve `MikroDocumentEditingService` cok buyuk sorumluluklar tasiyor. Once davranis testleri, sonra UBL olusturma, submission/reconciliation ve Mikro metadata yazimini ayirmak bakimi kolaylastirir. Genis mimari yeniden yazim ilk is olmamali.
2. Create/recovery servislerinde kopyalanan kod farkli guvenlik seviyelerine kaymis. Ortak sonuc/sahiplenme sozlesmesi kullanilip evrak turune ozel eslesme kurallari korunmali; her belgeyi tek genel matcher'a zorlamak dogru degil.
3. Legacy koprunun send ve durum/PDF erisim kontrollerindeki tekrar ortak bir filtre veya yardimciya alinabilir. Kimlik dogrulama kurali tek yerde uygulanmali.
4. Metadata worker'da sirali isleme, uzun suren bir belgenin digerlerini bekletmesine neden olabilir. Belge kilitleri korunarak sinirli paralellik; kuyruk yasi, deneme sayisi ve NeedsReview metrikleri eklenmeli.
5. API seviyesinde login hiz siniri/hesap korumasi belirgin degil. IIS veya gateway'de mevcut olup olmadigi dogrulanmali; yoksa eklenmeli.
6. Genel `InvalidOperationException -> 409` eslemesi teknik hatalari is kurali gibi gosterebilir. Yeni gelistirmelerde siniflandirilmis hata tipleri tercih edilmeli.
7. Gereksiz oldugu kanitlanmadan modul/endpoint silinmemeli. Once olculebilir adaylar: tekrar eden validation, tamamlanan bellek ici job birikimi, eskimis POS gorevleri ve kalici hatalarin sinirsiz sorgulanmasi.

## Onerilen uygulama sirasi

1. Mikro tam icerik recovery ve firma e-irsaliye tamamlik kontrolu: Bulgular 1-2.
2. Kalici manuel inceleme, atomik retry ve guvenli numara tahsisi: Bulgular 3-5.
3. Istemci rol fallback'i, legacy kimlik dogrulama ve proxy guveni: Bulgular 6-8. Ag erisimi aciksa legacy/proxy onlemleri ilk grupla paralel acil ele alinmali.
4. POS fiyat siralama ve kesin Uyumsoft ret durumu: Bulgular 9-10.
5. Refresh tek kullanim, dosya kuyrugu ve test/dokuman tamamlama: Bulgular 11-12.
6. Isleyis sabitlendikten sonra buyuk servisleri sadelestirme.

Mevcut katmanli yapiyi veya tek kullanici/istemci profili modelini bastan degistirmek gerekmiyor. Oncelik, mevcut korumalarin tum yazma yollarinda ayni kesinlikle uygulanmasi ve kararlarin veritabaninda atomik/kalici hale getirilmesi.
