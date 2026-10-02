# Son Degisiklikler ve Guncel Isleyis - 2026-10-02

## Genel Durum

Son duzeltmelerden sonra solution derlemesi ve tum otomatik testler basarilidir.

- `dotnet build FurpaMerkezApi.sln --no-restore`: basarili, 0 hata.
- `dotnet test FurpaMerkezApi.sln --no-build`: 479 basarili, 1 atlanan, 0 basarisiz.
- Atlanan test, harici SQL Server gerektiren mevcut offline-sync entegrasyon testidir.
- WebApi derleme ciktisini kilitleyen Roslyn `VBCSCompiler` hostu durduruldu; ardindan tam derleme basarili tamamlandi.

Derlemede bir adet mevcut nullable uyari kalmistir: `ApiDocumentationContractTests.cs:116` icin `CS8602`. Bu uyari yayin engeli degildir, ancak ayri bir temizlik isinde null kontrolu eklenmelidir.

## Son Yapilan Duzeltmeler

### 1. Mikro evrak numarasi cakismasi onlendi

Birden fazla istek ayni seri icin ayni anda `MAX + 1` hesapladiginda ayni evrak numarasini alma riski vardi. Yeni `MikroDocumentSequenceLock` ile ilgili seri ve islem tipi icin hem uygulama ici hem SQL Server seviyesinde kilit aliniyor.

Bu koruma su create akislarinda kullaniliyor:

- Firma sevki ve firma iadesi
- Zayiat ve masraf fisi
- Virman
- Depo iadesi
- Verilen depo ve firma siparisleri
- Manav MNV duzeltmeleri
- Axata dinamik sayim aktarimi

Kilit, evrak numarasi belirlenmeden once alinir; Mikro yazma ve readback/kurtarma sonucu kesinlesene kadar korunur. Boylece ayni seri icin paralel yazmalar sira ile calisir.

### 2. Refresh token rotation atomik hale getirildi

Refresh token satirina `revision` concurrency alani eklendi. Ayni refresh token ile iki paralel istek gelirse yalniz biri tokeni iptal edip yeni token uretebilir; diger istek `401 Unauthorized` alir.

Bu, eski refresh tokenin iki kez kullanilip iki farkli aktif oturum zinciri olusturmasini engeller.

UI davranisi:

1. Bir istemci icinde refresh cagrisi tekil tutulur.
2. Devam eden refresh varken ikinci refresh istegi atilmaz.
3. Basarili response'taki yeni access ve refresh token birlikte saklanir.
4. Eski tokenla gelen gec kalmis `401` icin eski tokenla tekrar deneme yapilmaz; guncel token seti yoksa yeniden login istenir.

Bu kural `UI_API_DOKUMANI.md` icine eklendi.

### 3. Guvenli create ve recovery modeli korunuyor

Sevk, iade, firma hareketi, zayiat, masraf ve virman create akislarinda her mantiksal kayit denemesi ayni `clientRequestId` ve ayni payload snapshot'i ile yasamaya devam eder.

- Timeout veya baglanti kopmasinda yeni evrak numarasi ya da yeni `clientRequestId` uretilmez.
- Backend once Mikro readback yapar.
- Evrak tum beklenen satirlariyla bulunursa `Recovered` sonucu verilir.
- Ayni evrak anahtarinda icerik farkliysa `MIKRO_DOCUMENT_CONTENT_MISMATCH`, `retryable=false` doner; yeniden yazma yapilmaz, manuel inceleme gerekir.
- Islem devam ediyorsa `MIKRO_WRITE_IN_PROGRESS`, sonuc henuz kanitlanamadiysa `MIKRO_WRITE_OUTCOME_UNCONFIRMED` doner; ayni id ve snapshot ile kontrollu tekrar denenir.

Bu kurallar parca evrak, cift tiklama ve timeout sonrasi ikinci evrak riskini azaltir.

### 4. E-irsaliye akisi

Yeni JWT'li ekranlar normal e-irsaliye endpointlerini kullanir. Eski arayuz icin gecici legacy kopru ayridir.

- Legacy koprude `expectedLineCount` zorunludur.
- Backend, Uyumsoft'a gondermeden once Mikro'daki aktif satir sayisini iki asamada bu degerle karsilastirir.
- Satirlar eksik veya fazla ise `409 Conflict` doner; Uyumsoft'a belge gonderilmez.
- Uyumsoft gonderimi basarili ama Mikro FRM/UUID isaretlemesi gecikirse durum `PendingMetadata` olur. Bu yeniden POST nedeni degildir.
- Satir icerigi veya miktarlar uyusmazsa otomatik isaretleme yapilmaz; `NeedsReview` durumuna alinir.

Legacy bridge gecici bir uyumluluk katmanidir. Eski arayuz kullanimi tamamen bittiginde `LegacyEDespatchBridge.Enabled=false` yapilarak kapatilmalidir.

### 5. Kullanici ve yetki modeli

Her sube tek hesapla calisir: ornek `160.sube`.

- Web login: `clientType=web`, eski Magazaci profilinin yetkileri.
- Terminal login: `clientType=terminal`, eski Terminal profilinin yetkileri.
- Ayni kullanici adi ve sifre kullanilir, ancak etkin rol ve permission seti oturum turune gore `app_user_client_roles` tablosundan okunur.
- `50.muhasebe`, `01.icmal` ve `Administrator` gibi klasik hesaplar eski genel rol modeliyle calismaya devam eder.
- Web ve terminal refresh tokenlari birbirinden bagimsizdir.

Bu sayede tek sube hesabi korunurken web ve terminal ekranlari birbirinin yetkilerini gormez.

### 6. Canli ag/proxy ayari

Sunucunun onunde Nginx, YARP, load balancer veya baska bir reverse proxy olmadigi teyit edildi. Bu nedenle production ayarinda:

```json
"ReverseProxy": {
  "Enabled": false,
  "TrustAllNetworks": false,
  "KnownProxies": []
}
```

olarak ayarlandi. API artik istemcinin gonderdigi sahte `X-Forwarded-For` gibi headerlari guvenilir kaynak kabul etmez. Terminal IP/depo kontrolu IIS'in gercek istemci IP'si uzerinden calisir.

## Uctan Uca Genel Isleyis

1. Kullanici `POST /api/auth/login` ile web veya terminal `clientType` bilgisini gonderir.
2. Backend o istemci turune ait etkin rolleri ve permissionlari belirler.
3. UI `login.user` veya `GET /api/auth/me` cevabindaki permissionlar ile menu, route ve butonlari olusturur.
4. Terminal oturumunda IP/depo kontrolu uygulanir; web oturumunda bu kontrol uygulanmaz.
5. Create ekraninda UI bir `clientRequestId` ve degismeyen payload snapshot'i olusturur.
6. Backend istekten once gerekli yetki, depo kapsami ve is kurallarini kontrol eder.
7. Mikro API yazmasi belirsiz kalirsa backend readback ile evraki ve tum satirlarini dogrular.
8. Evrak numarasi gereken akislar seri bazli kilit altinda calisir; paralel istekler ayni numarayi alamaz.
9. E-irsaliye gonderiminde belge satir butunlugu dogrulanir, Uyumsoft sonucu ve Mikro metadata durumu ayri takip edilir.
10. UI sadece backendin tipli sonucuna gore tekrar dener, manuel inceleme veya yeniden login kararini verir.

## Canliya Alma Sirasi

Refresh token degisikligi icin API yayini oncesinde Auth DB migration uygulanmalidir:

```text
20261002132927_HardenRefreshTokenRotation
```

Bu migration `app_refresh_tokens.revision` alanini ekler. Migration uygulanmadan yeni API binarysi yayinlanmamalidir; aksi halde refresh token isleminde eksik kolon hatasi alinir.

Onerilen sira:

1. Auth DB yedegini al.
2. Pending migrationi kontrollu sekilde uygula.
3. Yeni API binarysini yayinla.
4. Production `ReverseProxy.Enabled=false` ayarinin ilgili sunucudaki dosyada oldugunu kontrol et.
5. IIS App Pool'u recycle et.
6. Web ve terminalden yeni login, refresh, create retry ve terminal IP kontrolu smoke testlerini yap.

Not: Onceki idempotent migration scripti uretilirken, eski bir migrationdaki PostgreSQL tipinin mevcut SQL Server provider ile uyusmazligi goruldu. Bu yeni migrationin kod hatasi degildir; migration uygulamasi yayin penceresinde mevcut migration zinciri ve gercek Auth DB uzerinde kontrollu yapilmalidir.

## Dokumantasyon Durumu

- `docs/UI_API_DOKUMANI.md`: UI davranis kurallari, auth, client-role, safe retry ve legacy e-irsaliye akisi guncel.
- `docs/LEGACY_E_IRSALIYE_API_DOKUMANI.md`: eski arayuzun ayri entegrasyon dokumani.
- `docs/API_SOZLESME_REFERANSI.md` ve `docs/API_SOZLESMESI.json`: API kontrat referanslari.

Yeni refresh davranisi UI dokumanina eklendi. Endpoint veya request modeli degismedi; istemcinin refresh isteklerini paralel atmama kurali netlestirildi.
