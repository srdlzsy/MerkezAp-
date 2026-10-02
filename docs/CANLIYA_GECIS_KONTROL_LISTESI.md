# Canliya Gecis Kontrol Listesi

Son kontrol: 2026-10-02.

## Karar

Genel proje icin kosulsuz "canliya hazir" onayi verilmedi. Bu surumde istenen uc alanin duzeltmeleri ve regresyon testleri tamamlandi; ancak genel incelemedeki diger P1 riskler acik. Bu dosya yayin oncesi kontrol kapisidir, canli smoke testi yapildigi anlamina gelmez.

## Bu surumde duzeltilenler

- Firma sevki/iadesi, zayiat/masraf ve virman kurtarmasi tam satir setini orijinal istekle karsilastirir. Eksik set basari sayilmaz; farkli icerik manuel inceleme gerektirir.
- Ham Mikro API response'u tek basina basarili create cevabi uretmez. Readback basarisizsa ayni id korunur; belirsiz sonuc sonrasi ikinci create yapilmaz.
- Bizim API'de trace ile olusturulan firma e-irsaliyesi, Completed create ve orijinal satir eslesmesi olmadan gonderilmez. Tracesiz eski evraklar local create kaydi aranmadan mevcut legacy kontrollerinden gecer.
- Manuel inceleme karari error_code/retryable ile kalici tutulur. Ayni id ile yeniden create/otomatik recovery engellenir. Farkli payload ile yapilan yanlis retry, dogru orijinal istegi manuel inceleme durumuna cevirmez.
- Failed kaydi yeniden sahiplenme revision tabanli optimistic concurrency ile tek kazananli hale getirildi. Eski bir istek yeni sahipligi veya Completed kaydi hata ile ezemez.

## Zorunlu migration

Auth DB migration: `20261002111146_HardenOfflineCreateRecovery`.

`mobile_offline_sync_requests` tablosuna su alanlar eklenir:

| Alan | Amac |
|---|---|
| `error_code` | Sabit hata sinifi |
| `retryable` | Manuel inceleme kararinin kalici korunmasi |
| `revision` | Eszamanli guncellemelerde tek kazanan kontrolu |

Migration ayrica eski Failed kayitlarinda bilinen `The existing Mikro document does not match the requested document content.` mesaji bulunanlari manuel inceleme olarak isaretler. Tum eski hata metinlerini anlamsal olarak siniflandirmaz. Mevcut Completed kayitlari ve Mikro evraklari topluca degistirilmez; gecmiste yanlis basarili sayilanlar ayri incelenmelidir.

Yerel `appsettings.Production.json` dosyasinda `StartupTasks:ApplyAuthMigrations=false`. Canlida override yoksa uygulama acilirken migration otomatik uygulanmaz. Sadece DLL yayinlamak yeterli degildir.

2026-10-02'de migration, `MIKRO-SUNUCU` uzerindeki `FurpaMerkezDb` Auth DB'ye tek transaction olarak uygulandi. Once `COPY_ONLY`, checksum'li yedek alindi ve `RESTORE VERIFYONLY` basarili oldu. Sonraki kontrolde migration history kaydi ile `error_code`, `retryable`, `revision` kolonlari dogrulandi. `mobile_offline_sync_requests` durum sayilari degismedi: Failed 137, Processing 25, Completed 2137. Eski hata metniyle eslesen backfill adayi 0 idi. Bu yalniz schema migration'idir; yeni kodun IIS'e yayini ve canli smoke testi ayri adimdir.

Yayin sirasi:

1. Auth DB yedegini ve geri donus prosedurunu dogrula; mevcut migration seviyesini kontrol et.
2. Yazma trafigini durdur, devam eden istekleri bitir ve eski uygulamanin tum instance/worker'larini kapat. Eski ve yeni binary'yi ayni kuyruga birlikte yazdirma; eski binary revision kontrolunu bilmez.
3. Yetkili ortamda gozden gecirilmis migration'i Auth DB'ye uygula. Bu adim 2026-10-02'de tamamlandi.
4. Tum instance'lari yeni binary ile yayinla. Yeni kolonlar ve migration history kaydi dogrulandi; yeni kodun IIS yayini ve canli smoke testi ise bu calismada yapilmadi.
5. Kontrollu test ortaminda asagidaki smoke testlerini gecir; ardindan onayli canli pilotu ac.
6. 409 errorCode, readback bekleme yasi, NeedsReview ve kuyruk hatalarini izle.

Geri donuste kolonlari korumak veri kaybini azaltir; ancak eski binary'ye donmek guvenlik duzeltmelerini de geri alir. Eski binary ile yazma trafigi acilmamali. Migration Down hata kararlarini siler; rutin rollback olarak kullanilmamali.

## Acik yayin riskleri

Genel rapor: [Proje Genel Incelemesi](../outputs/proje_genel_inceleme_2026-10-02.md).

| Risk | Bu surumde durum | Yayin oncesi gereken |
|---|---|---|
| API rotasinda MAX+1 seri/sira yarisi | Acik | Kalici rezervasyon/koordinasyon ve paralel cihaz testi |
| Istemci rolu pasifken genel role fallback | Acik | Fail-closed duzeltmesi ve yetki regresyon testi |
| Anonim legacy kopru | Acik | Guvenilir servis kimligi/ag siniri; Origin/CORS tek basina yeterli degil |
| TrustAllNetworks ile terminal IP kontrolu | Acik | Gercek proxy listesi, hop limiti ve dogrudan erisim kisiti |
| Eski POS fiyat gorevinin yeniyi ezmesi | Acik | Surum/tarih siralamasi; etkilenebilecek akis duzelmeden acilmamali |
| Kesin Uyumsoft reddinin Unknown kalmasi | Acik | Ret/belirsiz ayrimi ve operasyonel inceleme |
| Paralel refresh ve bellek ici operasyon kuyrugu | Acik | Ayrica planlanan guvenilirlik duzeltmeleri |

Yerel production dosyasinin gercek sunucu ayarlariyla ayni oldugu varsayilmamali. Ortam degiskenleri, IIS/proxy, dis servis yetkileri ve DB izinleri canli sorumlusu tarafindan dogrulanmali. Bu calismada ayarlar otomatik degistirilmedi.

## UI API dokumani

[UI_API_DOKUMANI.md](UI_API_DOKUMANI.md) ve [LEGACY_E_IRSALIYE_API_DOKUMANI.md](LEGACY_E_IRSALIYE_API_DOKUMANI.md) bu degisiklikler icin guncellendi.

- Yeni zorunlu JWT request alani yok; terminal kodu degistirilmedi.
- Guvenli retry icin `clientRequestId` ve payload snapshot'i ayni kalir.
- `MIKRO_DOCUMENT_CONTENT_MISMATCH` + `retryable=false`: tekrar gonderme yok, yeni id ile ayni fisi yeniden olusturma yok; yetkili incelemesi.
- `MIKRO_WRITE_OUTCOME_UNCONFIRMED` / `MIKRO_WRITE_IN_PROGRESS`: ayni id/body ile kontrollu retry; ikinci Mikro create degil readback.
- Ekrani kapatmak/vazgecmek backend islemini iptal etmez. Pending kayit silinmez.
- Legacy `expectedLineCount` orijinal kaydedilen satir listesinden gelir; gecici Mikro sonucundan uretilmez.
- Create conflict karari ile e-irsaliye `status=NeedsReview` ayni state degildir. Ilki create/recovery 409 sozlesmesi, ikincisi gonderilmis e-belgenin isaretleme/icerik incelemesidir.
- Manuel incelemeyi kaldiran yeni yonetim endpointi bu surumde yoktur. UI boyle bir route varsaymamali.

Degisen akislar icin dokuman yeterli entegrasyon bilgisini icerir. Tum kayitli API sozlesmesi 2026-10-02'de test hostunda ApiExplorer/Swagger ile uretilen 714 method/route ve 643 JSON model uzerinden dogrulandi; tam makine referansi `API_SOZLESME_REFERANSI.md` ve `API_SOZLESMESI.json` dosyalarindadir. Bu kontrol canli endpointlere business istek atmaz; canli smoke/onay ayri kalir. Bilinen rol fallback siniri ve legacy guvenlik siniri dokumana acikca eklendi.

## Testler ve smoke matrisi

Otomatik test komutu:

```powershell
$env:FURPA_RUN_LOCALDB_TESTS = 'true'
dotnet test FurpaMerkezApi.sln --no-restore --verbosity quiet
```

LocalDB testi yalniz Windows'ta bu bayrakla calisir; uygulama connection string'lerini kullanmaz. Kendine ozel gecici DB olusturur, yeni migration'i uygular, iki baglantida paralel retry'i dogrular ve kendi DB'sini temizler.

2026-10-02 sonucu: 479 basarili (338 Infrastructure + 141 WebApi), 0 basarisiz, 0 atlanan. Onceki 422 teste 57 regresyon/integrasyon senaryosu eklendi. Yerel SQL testi migration/backfill ve atomik acquire kontrolunu kapsar; gercek Mikro/Uyumsoft servisine yazma yapmaz.

Ek dogrulamalar: WebApi `dotnet publish --configuration Release --no-restore` basarili. EF `has-pending-model-changes` kontrolunde migration disinda bekleyen model degisikligi yok. Bu kontroller uygulamayi canli sunucuda baslatmaz.

Yayin oncesi entegrasyon ortaminda dogrulanacaklar:

| Senaryo | Beklenen |
|---|---|
| Firma sevki/iadesi, zayiat/masraf, virman tam readback | Tek evrak, tam satirlar, basarili response |
| Mikro satirlari parca parca gorunur | Erken basari yok; tamamlanana kadar belirsiz sonuc |
| Ayni sayida farkli stok/miktar/birim veya fazla satir | 409, sabit mismatch kodu, tekrar yazma yok |
| Timeout sonrasi ayni id/body tekrar edilir | Mevcut evrak toparlanir; yeni evrak yazilmaz |
| Manuel inceleme sonrasi uygulama restart + ayni POST | Ayni non-retryable karar; create yok |
| Ayni Failed istege iki cihazdan eszamanli retry | Yalniz bir execute |
| Firma e-irsaliyesi create tamamlanmadan istenir | Uyumsoft'a gonderilmeden engellenir |
| Legacy trace'siz belge | Dogru expectedLineCount ile mevcut akis korunur |
| Web/terminal ayni hesap, farkli clientType | Oturumun dogru yetki seti; acik fallback riski ayrica kapatilmali |
| Gonderimden sonra Mikro metadata gecikir | Tekrar e-belge gonderilmez; durum/PDF dogru gosterilir |

Bu smoke matrisi canlida henuz calistirilmadi. Otomatik test basarisi tek basina canliya cikis onayi degildir.
