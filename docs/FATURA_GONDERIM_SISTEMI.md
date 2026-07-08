# Fatura Gonderim Sistemi

Bu dokuman, `fatura-gonderimi` modulunun 2026-07-08 itibariyla kodda gorulen
durumunu, hangi katmanlardan gectigini ve ileride sorun cikarmamasi icin dikkat
edilmesi gereken noktalarini anlatir.

## Kisa Ozet

Sistemde resmi fatura verisi **UBL Invoice XML** olarak uretilir. UI tarafindan secilen Mikro belgeleri backend'e gonderilir; backend bu belgelerden UBL-TR uyumlu XML olusturur.

Onemli ayrim:

- `/validate` akisi XML'i uretir, is kurallarini ve UBL-TR XSD kontrolunu calistirir, Uyumsoft'a gondermez.
- `/send` akisi hiz icin business/XSD validator'larini tekrar calistirmaz; UBL XML'i uretir ve Uyumsoft'a gonderir. Bu nedenle UI toplu gonderimde once `/validate`, sadece basarili belgeler icin `/send` cagirmalidir.

XSLT ise faturanin resmi verisi degildir. XSLT, faturanin HTML/PDF gibi goruntulendiginde nasil gorunecegini belirleyen sablondur. Karsi taraf ve entegrator asil olarak XML icindeki UBL alanlarini isler.

## Ana Dosyalar

- `src/FurpaMerkezApi.WebApi/Controllers/Modules/FaturaIslemleri/FaturaGonderimi/FaturaGonderimiController.cs`
  - API endpoint'lerini acar ve her endpoint'i ilgili use-case interface'ine yonlendirir.
- `src/FurpaMerkezApi.Application/Modules/FaturaIslemleri/FaturaGonderimi/InvoiceSendingModels.cs`
  - Request/response modellerini tutar.
- `src/FurpaMerkezApi.Application/Modules/FaturaIslemleri/FaturaGonderimi/I*UseCase.cs`
  - Controller'in bagli oldugu liste, detay, render, validate, send, retry, PDF ve iade referansi kontratlari.
- `src/FurpaMerkezApi.Infrastructure/Modules/FaturaIslemleri/FaturaGonderimi/*UseCase.cs`
  - Use-case implementasyonlari. Su an ince wrapper olarak `InvoiceSendingService` metodlarini cagirir.
- `src/FurpaMerkezApi.Infrastructure/Modules/FaturaIslemleri/FaturaGonderimi/InvoiceSendingService.cs`
  - Ortak is mantiginin bulundugu ana servis. Listeleme, render, validate, send,
    retry, PDF, iade referansi, UBL XML olusturma ve Uyumsoft cagrilarini yonetir.
- `src/FurpaMerkezApi.Infrastructure/Services/UblTrInvoiceBusinessRuleValidator.cs`
  - GIB/XSD disinda kalan is kurali kontrollerini yapar.
- `src/FurpaMerkezApi.Infrastructure/Services/UblTrInvoiceXmlValidator.cs`
  - Uretilen XML'i UBL-TR XSD dosyalari ile dogrular.
- `src/FurpaMerkezApi.WebApi/Assets/Xslt/efatura.xslt`
  - e-Fatura gorunum sablonu.
- `src/FurpaMerkezApi.WebApi/Assets/Xslt/earsiv.xslt`
  - e-Arsiv gorunum sablonu.
- `src/FurpaMerkezApi.WebApi/Assets/UblTr/xsdrt`
  - UBL-TR XSD sema dosyalari.

## Endpoint Matrisi

| Islem | Endpoint | Yetki | Ne yapar | Not |
| --- | --- | --- | --- | --- |
| Liste | `GET /api/fatura-islemleri/fatura-gonderimi` | `fatura-islemleri.fatura-gonderimi.list` | Mikro'dan gonderilecek/gonderilmis belgeleri listeler. | `isSent` tercih edilir, `SentState` legacy alias'tir. |
| Detay | `GET /api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}` | `fatura-islemleri.fatura-gonderimi.detail` | Belgeden UBL XML uretir ve HTML onizleme dondurur. | Uyumsoft'a gondermez. |
| PDF | `GET /api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}/pdf` | `fatura-islemleri.fatura-gonderimi.detail` | Gonderilmis giden faturanin Uyumsoft outbox PDF dosyasini dondurur. | `application/pdf` doner. |
| Render | `POST /api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}/render` | `fatura-islemleri.fatura-gonderimi.detail` | XSLT tercihiyle HTML onizleme uretir. | `fallbackToGeneral` default `true`. |
| Iade adaylari | `GET /api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}/return-reference-candidates` | `fatura-islemleri.fatura-gonderimi.detail` | Iade faturasi icin referans olabilecek faturalari listeler. | Mikro'ya yazmaz. |
| Iade referansi | `PUT /api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}/return-reference` | `fatura-islemleri.fatura-gonderimi.create` | Secilen iade referansini `EBELGE_EVRAK_HAREKETLERI` tablosuna yazar. | Send oncesi manuel duzeltme icindir. |
| Validate | `POST /api/fatura-islemleri/fatura-gonderimi/validate` | `fatura-islemleri.fatura-gonderimi.create` | Gonderim oncesi UBL/is kurali/XSD kontrolu yapar. | Uyumsoft'a gondermez, Mikro'yu guncellemez. |
| Send | `POST /api/fatura-islemleri/fatura-gonderimi/send` | `fatura-islemleri.fatura-gonderimi.create` | Belgeyi Uyumsoft'a gonderir ve basariliysa Mikro'ya belge no/UUID yazar. | Hiz icin validate'i tekrar calistirmaz. |
| Retry | `POST /api/fatura-islemleri/fatura-gonderimi/retry` | `fatura-islemleri.fatura-gonderimi.create` | Daha once gonderilmis faturayi Uyumsoft'ta yeniden kuyruya alir. | Ilk gonderim degildir, UBL yeniden uretilmez. |
| XML preview | `POST /api/fatura-islemleri/fatura-gonderimi/preview` | `fatura-islemleri.fatura-gonderimi.create` | Disaridan verilen XML'i HTML olarak render eder. | Mikro veya Uyumsoft guncellemesi yapmaz. |

## Guncel Katman Akisi

Controller artik dogrudan tek bir servis interface'ine bagli degildir. Her endpoint
ayri bir use-case kontrati uzerinden calisir:

| Endpoint/Islem | Application kontrati | Infrastructure implementasyonu |
| --- | --- | --- |
| Liste | `IListInvoiceSendingDocumentsUseCase` | `ListInvoiceSendingDocumentsUseCase` |
| Detay | `IGetInvoiceSendingDocumentUseCase` | `GetInvoiceSendingDocumentUseCase` |
| PDF | `IGetInvoiceSendingPdfUseCase` | `GetInvoiceSendingPdfUseCase` |
| Render | `IRenderInvoiceSendingDocumentUseCase` | `RenderInvoiceSendingDocumentUseCase` |
| Validate | `IValidateInvoiceSendingDocumentsUseCase` | `ValidateInvoiceSendingDocumentsUseCase` |
| Send | `ISendInvoiceSendingDocumentsUseCase` | `SendInvoiceSendingDocumentsUseCase` |
| Retry | `IRetryInvoiceSendingDocumentsUseCase` | `RetryInvoiceSendingDocumentsUseCase` |
| Iade adaylari | `IListInvoiceReturnReferenceCandidatesUseCase` | `ListInvoiceReturnReferenceCandidatesUseCase` |
| Iade referansi | `IUpdateInvoiceReturnReferenceUseCase` | `UpdateInvoiceReturnReferenceUseCase` |

Bu use-case siniflari DI tarafinda `ServiceCollectionExtensions` icinde register edilir.
Mevcut implementasyonlar ortak `InvoiceSendingService` servisini kullanir; yani davranisin
ana kaynagi hala `InvoiceSendingService.cs` dosyasidir.

## Uctan Uca Kullanim Akisi

Normal toplu gonderim:

1. UI tarih, senaryo ve `isSent=0` ile listeyi ceker.
2. Kullanici gonderilecek belgeleri secer.
3. UI `/validate` cagirir.
4. Backend her belge icin UBL XML uretir, is kurali ve XSD kontrolu yapar.
5. UI validate sonucunu satir bazinda gosterir.
6. Hatali belge varsa kullanici Mikro verisini veya iade referansini duzeltir.
7. Tum secili belgeler basariliysa UI `/send` cagirir.
8. Backend belge bazinda SQL application lock alir.
9. Backend UBL XML uretir ve Uyumsoft `SendInvoiceAsync` cagirir.
10. Uyumsoft numara donerse Mikro `cha_belge_no` ve `cha_uuid` guncellenir.
11. UI send sonucunu satir bazinda gosterir.
12. UI listeyi yeniler; gonderilen belge artik `isSent=1` tarafinda gorunur.

Gonderilmis fatura goruntuleme:

1. UI `isSent=1` ile gonderilmisleri listeler.
2. Kullanici satiri acar.
3. PDF gerekiyorsa `/pdf` endpoint'i cagrilir.
4. Backend Uyumsoft outbox'tan PDF alir ve `application/pdf` doner.

Iade faturasi:

1. UI iade belgeyi listeler.
2. Kullanici veya sistem referans fatura adaylarini `/return-reference-candidates` ile kontrol eder.
3. Gerekirse `/return-reference` ile referans kaydedilir.
4. Validate/send akisi normal fatura gibi devam eder.
5. Send sirasinda referans halen yoksa backend fallback aday arar; bulursa kaydeder, bulamazsa belgeyi durdurur.

## API Akisi

### 1. Listeleme

Endpoint:

```http
GET /api/fatura-islemleri/fatura-gonderimi
```

Parametreler:

- `startDate`
- `endDate`
- `scenario`: `0` e-Fatura, `1` e-Arsiv
- `sentState` veya `isSent`
  - `0`: gonderilmemisler
  - `1`: gonderilmisler
  - `-1`: hepsi

Controller tarafinda `startDate` ve `endDate` zorunludur. `scenario` verilmezse
varsayilan `EFatura` kabul edilir. `isSent` verilirse `sentState` degerinin onune
gecer; bu nedenle yeni UI kodunda `isSent` kullanilmasi daha nettir.

Bu endpoint Mikro'dan gonderime uygun belgeleri listeler. Senaryoya gore e-Fatura/e-Arsiv ayrimi yapilir.

Onemli nokta: seri eslesmesi `FaturaSeries` uzerinden yapilir. Seri cakismalarinda toplamlar sismesin diye en uzun ve en spesifik seri secilir. Ornek: `FR` ve `FRP` varsa `FRP001` icin `FRP` tercih edilir.

Liste belge bazinda doner. Ayni `cha_evrakno_seri` + `cha_evrakno_sira` altinda birden fazla hizmet/cari hareket satiri varsa tek fatura satirinda toplanir. Hafif liste modunda agir satir ozetleri hesaplanmaz; detay/render/validate/send gibi tam mod akislarinda hizmet satirlari yine ayri kalem olarak kalir ve farkli KDV oranlari tek satira ezilmez.

Liste performansi icin dikkat:

- Liste endpoint'i hiz icin hafif modda calisir. Stok satiri istisna aramasi, iade referansi lookup'i, hizmet/demirbas satir ozeti ve KDV oran ozeti liste sirasinda hesaplanmaz.
- Bu agir alanlar detay/render/validate/send gibi belge odakli akislar sirasinda tam modda hesaplanir.
- UI liste ekraninda `sourceLineSummary`, `taxRateSummary` ve iade referansi alanlarini kesin kaynak gibi kullanmamalidir; kesin kontrol icin detay, iade adaylari veya validate akisi kullanilmalidir.
- UI gunluk veya kisa tarih araligi ile sorgulamalidir.
- UI `isSent` ve `SentState` parametrelerinden sadece birini gondermelidir; tercih edilen parametre `isSent`tir.
- `isSent=-1` en pahali moddur; ekran varsayilani mumkunse `isSent=0`, gonderilenler sekmesi icin `isSent=1` olmalidir.
- Sorgu mevcut Mikro indeksinden yararlanmak icin once `CARI_HESAP_HAREKETLERI.cha_tarihi` ile daraltir, sonra dogruluk icin `cha_belge_tarih` filtresini de uygular.
- Bu optimizasyon `cha_tarihi` ve `cha_belge_tarih` ayni gun oldugu fatura akisinda guvenlidir; canli kontrolde 2026-07-07 gonderilmis e-fatura setinde farkli tarihli satir bulunmamistir.
- DBA tarafinda kontrol edilecek mevcut indeks: `NDX_CARI_HESAP_HAREKETLERI_02 (cha_tarihi)`. Bu indeks kullanilmiyorsa istatistikler ve execution plan incelenmelidir.
- `STOK_HAREKETLERI.sth_fat_uid` indeksi sevkiyat/istisna apply'lari icin kritiktir; modelde var gorunuyor, canli planda kullanildigi kontrol edilmelidir.

### 2. Detay ve Render

Endpoint:

```http
GET /api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}
POST /api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}/render
```

Render akisi UBL XML'i olusturur ve XSLT ile HTML gorunum uretir. Bu islem Uyumsoft'a gonderim yapmaz ve Mikro'da belgeyi gonderilmis isaretlemez.

Detay endpoint'inde `scenario` query parametresi vardir ve verilmezse `EFatura`
kabul edilir.

Render request ornegi:

```json
{
  "scenario": 0,
  "profile": 0,
  "preferEmbeddedXslt": true,
  "fallbackToGeneral": true
}
```

Controller'da JSON alan adi `fallbackToGeneral` olarak gelir; application modelinde
bu deger `FallbackToDefaultXslt` olarak tasinir.

Render tarafinda XSLT siralamasi:

1. `preferEmbeddedXslt = true` ise XML icindeki gomulu XSLT aranir.
2. Gomulu XSLT yoksa ve `fallbackToDefaultXslt = true` ise asset dosyasi kullanilir.
3. Profil e-Arsiv ise `earsiv.xslt`, aksi durumda `efatura.xslt` kullanilir.

### 3. Gonderim Oncesi Kontrol

Endpoint:

```http
POST /api/fatura-islemleri/fatura-gonderimi/validate
```

Request ornegi:

```json
{
  "scenario": 0,
  "documents": [
    {
      "documentSerie": "ABC",
      "documentOrderNo": 12345
    }
  ]
}
```

`scenario` ve `documents` zorunludur. Her belge icin `documentSerie` bos olamaz,
`documentOrderNo` ise `1` veya daha buyuk olmalidir. Servis secimleri trim edip
tekillestirir; ayni belge iki kez gonderilirse tek islenir.

Bu endpoint asil gonderimden once prova yapar.

Yaptiklari:

- Secimleri tekillestirir.
- Belgeyi Mikro'dan bulur.
- Belge daha once gonderildiyse hata verir.
- Iade faturasiysa referans faturayi cozmeye calisir.
- UBL Invoice XML olusturur.
- Is kurali validator'ini calistirir.
- UBL-TR XSD validator'ini calistirir.

Yapmadiklari:

- Uyumsoft'a gondermez.
- Mikro'da `cha_belge_no` yazmaz.
- Mikro'da belgeyi kilitlemez.
- Iade referansi fallback olarak bulunursa validate sirasinda Mikro'ya kaydetmez.

Bu nedenle UI tarafinda en dogru kullanim:

1. Kullanici faturalari secer.
2. Once `/validate` calisir.
3. Tum belgeler gecerliyse `/send` calisir.

### 4. Gercek Gonderim

Endpoint:

```http
POST /api/fatura-islemleri/fatura-gonderimi/send
```

Gercek gonderim akisi:

1. Request dogrulanir.
2. Uyumsoft ve firma ayarlari kontrol edilir.
3. Secilen belgeler tekillestirilir.
4. Belge Mikro'dan yuklenir.
5. Daha once gonderildiyse tekrar gonderilmez.
6. Ayni belge icin SQL application lock alinir; baska bir istek ayni belgeyi gonderiyorsa ikinci istek Uyumsoft'a gitmeden durdurulur.
7. Iade faturasiysa iade referansi zorunlu hale gelir.
8. UBL Invoice XML olusturulur.
9. Uyumsoft `SendInvoiceAsync` servisine gonderilir.
10. Uyumsoft belge numarasi donerse Mikro guncellenir.

Performans nedeniyle gercek `/send` akisi business/XSD validator'larini yeniden calistirmaz. Bu kontroller `/validate` endpoint'inde yapilir; UI toplu gonderimde once `/validate`, sadece basarili belgeler icin `/send` cagirmalidir.

Gonderim basarili olunca Mikro'da guncellenen alanlar:

- `cha_belge_no`: Uyumsoft'un verdigi fatura numarasi
- `cha_uuid`: Uyumsoft teknik invoice id/ETTN bilgisi; servis id bos donerse lokal fatura UUID degeri saklanir
- `cha_kilitli`: `true`
- `cha_degisti`: `true`
- `cha_lastup_user`: sistemde sabit Mikro kullanicisi
- `cha_lastup_date`: guncelleme zamani

### 5. Gonderilmis Fatura PDF

Endpoint:

```http
GET /api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}/pdf?scenario=EFatura
```

PDF akisi:

1. Belge Mikro'dan yuklenir.
2. Lookup icin once `cha_uuid`/ETTN, sonra lokal `cha_Guid` denenir.
3. Uyumsoft outbox PDF servisi cagrilir.
4. Ilk basarili PDF cevabi `application/pdf` olarak doner.
5. Tum lookup denemeleri basarisiz olursa hangi id'lerin denendigi hata mesajina eklenir.

Bu endpoint sadece gonderilmis giden fatura PDF'i icindir. Bekleyen fatura onizlemesi icin detay/render endpointleri kullanilir.

### 6. Tekrar Gonderim

Endpoint:

```http
POST /api/fatura-islemleri/fatura-gonderimi/retry
```

Retry akisi:

1. Request dogrulanir.
2. Tek istekte en fazla 20 belge kabul edilir.
3. Belgeler Mikro'dan yuklenir.
4. Belge daha once gonderilmemisse retry reddedilir.
5. Belgenin `cha_uuid`/Uyumsoft invoice id bilgisi yoksa retry reddedilir.
6. Uyumsoft `RetrySendInvoicesAsync` servisine invoice id listesi gonderilir.
7. Mikro'da yeniden UBL uretilmez, `cha_belge_no` veya `cha_uuid` tekrar yazilmaz.

Retry ilk gonderim yerine kullanilmaz. Sadece Uyumsoft'ta daha once olusmus/gonderilmis faturanin tekrar kuyruya alinmasi icindir.

### 7. XML Preview

Endpoint:

```http
POST /api/fatura-islemleri/fatura-gonderimi/preview
```

Request ornegi:

```json
{
  "invoiceId": "preview",
  "xmlContent": "<Invoice>...</Invoice>",
  "profile": 0,
  "preferEmbeddedXslt": true
}
```

Bu endpoint eldeki herhangi bir UBL XML'i HTML olarak gormek icindir. `xmlContent`
zorunludur. Mikro'dan belge yuklemez, Uyumsoft'a gitmez, Mikro'da alan guncellemez.
UI veya test araci tarafindan uretilecek XML'in gorunumunu kontrol etmek icin kullanilir.

## Mikro Kaynaklari ve Kolon Haritasi

Listeleme ve UBL uretimi agirlikli olarak su kaynaklari kullanir:

| Kaynak | Amac | Kritik kolonlar |
| --- | --- | --- |
| `CARI_HESAP_HAREKETLERI` | Fatura basligi, hizmet satirlari, tutarlar, belge no, UUID | `cha_Guid`, `cha_evrakno_seri`, `cha_evrakno_sira`, `cha_tarihi`, `cha_belge_tarih`, `cha_belge_no`, `cha_uuid`, `cha_ciro_cari_kodu`, `cha_vergipntr`, `cha_vergi1..10` |
| `CARI_HESAPLAR` | Alici/satici cari bilgisi | `cari_kod`, `cari_unvan1`, `cari_unvan2`, `cari_vdaire_no`, `cari_VergiKimlikNo`, `cari_efatura_fl`, `cari_EMail` |
| `CARI_HESAP_ADRESLERI` | Adres, alias/e-posta, il/ilce | `adr_cari_kod`, `adr_adres_no`, `adr_efatura_alias`, `adr_cadde`, `adr_sokak`, `adr_ilce`, `adr_il`, `adr_posta_kodu` |
| `CARI_HESAP_HAREKETLERI_EK` | Istisna, rusum, ozel matrah bilgileri | `chaek_related_uid`, `cha_Istisna1`, `cha_HalRusum`, `cha_ozel_matrah_kodu` |
| `STOK_HAREKETLERI` | Stok satirlari, sevkiyat/irsaliye, stok KDV bilgileri | `sth_fat_uid`, `sth_stok_kod`, `sth_miktar`, `sth_tutar`, `sth_vergi`, `sth_vergi_pntr`, `sth_belge_no`, `sth_belge_tarih` |
| `STOK_HAREKETLERI_EK` | Stok satiri istisna kodu | `sthek_related_uid`, `sth_istisna` |
| `STOKLAR` | Stok adi ve birim | `sto_kod`, `sto_isim`, `sto_birim1_ad` |
| `HIZMET_HESAPLARI` / `DEMIRBASLAR` | Hizmet/demirbas satir adi | `hiz_kod`, `hiz_isim`, `dem_kod`, `dem_isim` |
| `DEPOLAR` | Sevkiyat deposu adi | `dep_no`, `dep_adi` |
| `EBELGE_EVRAK_HAREKETLERI` | Iade fatura referansi | `ebh_related_uid`, `ebh_iade_fat_no1`, `ebh_iade_fat_tarihi1` |
| `Furpa.dbo.FaturaSeries` | Seri e-fatura/e-arsiv ayrimi | `seri`, `efatura` |

Kritik Mikro yazimlari:

- Send basarili olursa `CARI_HESAP_HAREKETLERI` satirlarina `cha_belge_no`, `cha_uuid`, `cha_kilitli`, `cha_degisti`, `cha_lastup_user`, `cha_lastup_date` yazilir.
- Iade referansi manuel veya send fallback ile secilirse `EBELGE_EVRAK_HAREKETLERI` update edilir; kayit yoksa insert edilir.
- Validate, render, detail, PDF ve preview akislari Mikro fatura alanlarini guncellemez.

## UBL XML Nasil Olusuyor?

Backend, Mikro verilerinden `n1:Invoice` kok elemanli UBL XML uretir.

Temel alanlar:

- `UBLVersionID`: `2.1`
- `CustomizationID`: `TR1.2`
- `ProfileID`
  - e-Fatura: `TICARIFATURA` veya `TEMELFATURA`
  - e-Arsiv: `EARSIVFATURA`
- `ID`: fatura numarasi
- `UUID`: Mikro fatura hareketinin `cha_Guid` degeri
- `IssueDate`
- `IssueTime`
- `InvoiceTypeCode`
  - `SATIS`
  - `IADE`
  - `ISTISNA`
  - `OZELMATRAH`
- `DocumentCurrencyCode`: `TRY`
- `LineCountNumeric`
- `AccountingSupplierParty`
- `AccountingCustomerParty`
- `TaxTotal`
- `LegalMonetaryTotal`
- `InvoiceLine`

Ek alanlar:

- `UBLExtensions`
- `Signature`
- Iade faturalarinda `BillingReference`
- Irsaliye varsa `DespatchDocumentReference`
- XSLT sablonu varsa `AdditionalDocumentReference`

UBL-TR XSD sira konusunda katidir. Kok `Invoice` icinde `UBLExtensions` basta gelmelidir; aksi halde `UBLVersionID beklenmiyor, once UBLExtensions bekleniyor` hatasi alinir. Adres icinde de eleman sirasi onemlidir. Biz `adr_cadde` ve `adr_sokak` bilgisini tek `StreetName` altinda birlestiriyoruz; `AdditionalStreetName` kullanilmiyor. `CitySubdivisionName`, `CityName`, `Country` sirasi korunmalidir.

## KDV, Istisna ve Ozel Matrah Mantigi

KDV orani Mikro'daki pointer degerinden sabit tabloyla tahmin edilmemelidir. Eski sistemde gorulen `1=%18, 2=%8, 3=%1, 4=%0` eslesmesi artik guncel olmayabilir. Mevcut kod oran bilgisini canli Mikro fonksiyonundan okur:

```sql
SELECT DepartmentNo, Rate, Name
FROM dbo.fn_hs_vergi_oran_listesi()
ORDER BY DepartmentNo
```

25.06.2026 kontrolunde canli sistemde tipik oranlar su sekilde goruldu:

```text
1 = 20
2 = 10
3 = 1
4 = 0
8 = 18
9 = 8
```

Bu liste Mikro ayarina bagli oldugu icin kesin kaynak her zaman `dbo.fn_hs_vergi_oran_listesi()` sonucudur.

Stok satirlarinda:

- KDV pointer: `STOK_HAREKETLERI.sth_vergi_pntr`
- KDV tutari: `STOK_HAREKETLERI.sth_vergi`
- Oran: `dbo.fn_hs_vergi_oran_listesi()` ile pointer eslesmesi

Hizmet/cari hareket satirlarinda:

- KDV pointer: `CARI_HESAP_HAREKETLERI.cha_vergipntr`
- KDV tutari: pointer'a gore `cha_vergi1..cha_vergi10` alanlarindan secilir
- Toplam KDV: `cha_vergi1 + ... + cha_vergi10`
- Oran: `dbo.fn_hs_vergi_oran_listesi()` ile pointer eslesmesi

Bu ayrim onemlidir. Ornek: `FEF26/4792` belgesinde `cha_vergipntr = 3` ve canli Mikro'da pointer `3 = %1` idi. Eski kod sadece `cha_vergi1` okusaydi KDV `0` gorunur, sistem hatali sekilde `TaxExemptionReasonCode` isterdi. Dogru davranis: pointer 3 icin `cha_vergi3` okunur ve belge normal `%1 KDV` ile gider.

`InvoiceTypeCode` secimi:

- Iade ise `IADE`
- `IstisnaKodu` doluysa `ISTISNA`
- `OzelMatrahKodu` doluysa `OZELMATRAH`
- Diger durumlarda `SATIS`

`TaxExemptionReasonCode` sadece gercekten KDV orani `0` olan satir veya vergi grubu icin yazilir. Header'da istisna kodu var diye tum satirlar KDV'siz kabul edilmez. KDV orani `0`, matrah `0`dan buyuk ve istisna kodu bos ise validate hata verir; bu veri Mikro tarafinda `cha_Istisna1`, ozel matrah veya ilgili dogru kaynak alanla tamamlanmalidir.

Tevkifat/ozel oran gibi pointer'lar dikkat ister. Canli listede `5`, `7`, `10`, `11` gibi oranlar gorulebilir; bunlar normal KDV gibi degilse UBL'de ayrica tevkifat/withholding yapisi gerekebilir. Gercek fatura akisi bu pointer'lari kullanmaya baslarsa ayrica test edilmelidir.

## XSLT Ne Ise Yarar?

XSLT, XML faturanin gorunum sablonudur.

Bizim sistemde:

- e-Fatura icin `efatura.xslt`
- e-Arsiv icin `earsiv.xslt`

XML uretilirken ilgili XSLT base64 olarak `AdditionalDocumentReference` icine eklenir. Bu sayede fatura goruntulenirken bizim sablonumuz kullanilabilir.

Kritik ayrim:

- XML yanlissa fatura resmi olarak hatali olabilir.
- XSLT yanlissa fatura verisi dogru olsa bile gorunum bozuk olabilir.
- XSLT genelde alici tarafin muhasebe verisini degil, faturayi nasil gordugunu etkiler.

## Is Kurali Kontrolleri

`UblTrInvoiceBusinessRuleValidator` XSD'nin yakalamadigi veya bizim akis icin kritik olan kontrolleri yapar.

Kontrol edilen basliklar:

- XML kok elemani UBL `Invoice` mi?
- `UBLVersionID = 2.1` mi?
- `CustomizationID = TR1.2` mi?
- Para birimi `TRY` mi?
- Senaryoya gore `ProfileID` dogru mu?
- Uyumsoft hedef alias/e-posta dolu mu?
- e-Arsiv icin hedef e-posta formatli mi?
- Alici/satici VKN/TCKN 10 veya 11 haneli mi?
- Alici/satici unvan, adres, sehir, ulke ve vergi dairesi dolu mu?
- En az bir fatura satiri var mi?
- `LineCountNumeric` satir sayisi ile uyumlu mu?
- Satir miktari sifirdan buyuk mu?
- Satir mal/hizmet adi dolu mu?
- KDV `TaxTypeCode = 0015` mi?
- KDV orani izinli oranlardan biri mi?
  - `0`
  - `1`
  - `8`
  - `10`
  - `18`
  - `20`
- KDV tutari matrah ve oranla uyumlu mu?
- Toplamlar satir toplamlarindan hesaplanan degerlerle uyumlu mu?
- Iade faturasinda `BillingReference` var mi?
- KDV orani `0` olan matrahli satirlarda `TaxExemptionReasonCode` var mi?

## UBL-TR XSD Kontrolu

`UblTrInvoiceXmlValidator`, uretilen XML'i resmi UBL-TR XSD semalari ile dogrular.

Ana sema:

```text
Assets/UblTr/xsdrt/maindoc/UBL-Invoice-2.1.xsd
```

Imza semasi:

```text
Assets/UblTr/xsdrt/common/UBL-xmldsig-core-schema-2.1.xsd
```

Buradaki amac, Uyumsoft'a gitmeden once XML'in UBL-TR semasina yapisal olarak uygun oldugunu yakalamaktir.

## e-Fatura ve e-Arsiv Ayrimi

Senaryo `InvoiceSendingScenario` ile belirlenir:

```text
EFatura = 0
EArsiv = 1
```

Bu deger su alanlari etkiler:

- Mikro liste filtresi
- `FaturaSeries.efatura` filtresi
- `cari_efatura_fl` filtresi
- `ProfileID`
- Uyumsoft `Scenario`
- Hedef musteri bilgisi
- Kullanilacak XSLT

e-Fatura tarafinda hedef alan genelde alias mantigindadir. e-Arsiv tarafinda elektronik teslim icin hedef e-posta kritik hale gelir.

## Iade Faturasi Puf Noktalari

Iade faturalarinda `BillingReference` zorunludur. Yani iadenin hangi faturaya istinaden kesildigi XML icinde bulunmalidir.

Sistem davranisi:

- Referans zaten varsa kullanir.
- Referans yoksa aday fatura arar.
- Validate sirasinda aday bulunursa sadece XML icin kullanir, Mikro'ya yazmaz.
- Send sirasinda aday bulunursa Mikro'ya kaydeder ve sonra gonderir.
- Aday bulunamazsa hem validate hem send hata verir.

Bu yuzden iade faturalarinda gonderimden once referans adaylari UI'da kontrol edilmelidir.

Ilgili endpoint'ler:

```http
GET /api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}/return-reference-candidates
PUT /api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}/return-reference
```

## En Cok Sorun Cikarabilecek Noktalar

### 1. Alici VKN/TCKN

VKN/TCKN bos, kisa, uzun veya harfli olursa fatura Uyumsoft'a gitmeden bizim validator'da kalir.

### 2. e-Arsiv E-Posta

e-Arsivde alici e-posta bilgisi bos veya gecersizse gonderim sorunlu olur. Sistem bunu validate asamasinda yakalar.

### 3. Adres ve Sehir Bilgisi

Adres, sehir, ulke kodu ve vergi dairesi bos olmamali. Bos degerler XML icin teknik olarak uretilebilse bile ticari/entegrator tarafinda sorun cikarabilir.

### 4. KDV Kodlari

KDV icin `TaxTypeCode` degeri `0015` olmali. Oran Mikro'nun guncel KDV fonksiyonundan okunur; eski sabit pointer eslesmelerine guvenilmemelidir.

### 5. Sifir KDV ve Istisna Kodu

KDV orani `0` ve matrah varsa `TaxExemptionReasonCode` gereklidir. Bos giderse belge validate asamasinda kalmalidir; Uyumsoft'a gonderilirse entegrator veya GIB tarafinda hata verme riski yuksektir.

Bu hata her zaman "fatura gercekten KDV'siz" anlamina gelmez. Once KDV pointer ve tutar dogru okunuyor mu kontrol edilmelidir. Hizmet faturalarinda `cha_vergipntr = 3` ise KDV tutari `cha_vergi3` alanindan gelmelidir; sadece `cha_vergi1` okumak belgeyi hatali sekilde KDV'siz gosterir.

### 6. Toplam Tutarlar

Satir toplamlarindan hesaplanan tutarlar ile `LegalMonetaryTotal` ve `TaxTotal` uyumlu olmali. Yuvarlama toleransi kucuktur; fark buyurse validate hata verir.

### 7. Seri Eslesmesi

`FaturaSeries` tarafinda benzer prefix'li seriler olabilir. Sistem en uzun prefix'i secerek `FR` / `FRP` gibi cakismalarda cift eslesmeyi engeller.

### 8. XSLT Bozulmasi

XSLT faturanin resmi muhasebe verisini degistirmez ama goruntuyu etkiler. XSLT degisikliginden sonra en az su kontroller yapilmali:

- XSLT XML olarak parse oluyor mu?
- `.NET XslCompiledTransform` yukleyebiliyor mu?
- Ornek fatura render ediliyor mu?
- e-Fatura ve e-Arsiv ayri ayri deneniyor mu?

## UI Icin Onerilen Kullanim

Toplu gonderim icin onerilen akis:

1. Kullanici tarih ve senaryo ile fatura listesini ceker.
2. Kullanici belgeleri secer.
3. UI `/validate` endpoint'ini cagirir.
4. Her belge icin sonucu gosterir.
5. Hatali belgeler varsa kullaniciya gonderim yaptirmadan duzeltme ister.
6. Tum belgeler basariliysa `/send` cagirir.
7. Send sonucunda her belge icin basarili/basarisiz mesajini gosterir.

Bu akis Uyumsoft'a hatali belge gonderme riskini azaltir.

## Hata Ayiklama Rehberi

### Validate hata veriyor ama render calisiyor

Render sadece goruntu uretir. Validate ise UBL is kurali ve XSD kontrolu yapar. Bu durumda XML goruntulenebilir ama resmi gonderim icin eksik/hatalidir.

### Uyumsoft hata veriyor

Once `/validate` sonucu kontrol edilmeli. Validate geciyor ama Uyumsoft hata veriyorsa:

- Uyumsoft kullanici/endpoint ayarlari
- Target alias/e-posta
- Musterinin e-Fatura/e-Arsiv durumu
- Entegrator tarafindaki kabul kurallari
- Daha once gonderilmis belge numarasi

kontrol edilmeli.

### Fatura gonderildi ama listede hala gonderilmemis gorunuyor

Mikro'da `cha_belge_no` yazildi mi kontrol edilmeli. Send basarili olunca sistem `cha_belge_no` alanina Uyumsoft belge numarasini yazar ve satirlari kilitler.

### Iade faturasi hata veriyor

`BillingReference` yoktur veya referans fatura bulunamamistir. Once return reference candidates endpoint'i ile adaylar kontrol edilmeli, gerekirse manuel referans secilmelidir.

### XSD "UBLVersionID beklenmiyor, UBLExtensions bekleniyor" hatasi

UBL kok eleman sirasi yanlistir. `UBLExtensions` en basta uretilmelidir. Bu hata XML daha is kurallarina gelmeden XSD tarafinda durur.

### XSD "PostalAddress AdditionalStreetName beklenmiyor" hatasi

Adres eleman sirasi veya kullanilan adres elemani UBL-TR semasina uymuyordur. `adr_cadde` ve `adr_sokak` ayri `AdditionalStreetName` olarak yazilmamali; tek `StreetName` icinde birlestirilmelidir.

### "zero-rated taxable line requires TaxExemptionReasonCode" hatasi

Sistem bir satiri matrahli ama `%0 KDV` olarak gormustur. Iki ihtimal vardir:

- Gercekten KDV'siz/istisnali satis vardir; Mikro'da istisna kodu tamamlanmalidir.
- KDV tutari yanlis kaynaktan okunuyordur; ozellikle hizmet faturalarinda `cha_vergipntr` hangi vergi kolonunu gosteriyorsa `cha_vergi1..10` icinden o kolon okunmalidir.

Bu hata geldiginde once canli oran listesi kontrol edilmelidir:

```sql
SELECT DepartmentNo, Rate, Name
FROM dbo.fn_hs_vergi_oran_listesi()
ORDER BY DepartmentNo
```

## Operasyon Ayarlari

Fatura gonderimi icin temel ayarlar:

| Ayar | Kullanildigi yer | Aciklama |
| --- | --- | --- |
| `ConnectionStrings:MikroConnection` | Okuma akislari | Liste, detay, validate, render, PDF belge lookup icin Mikro okuma baglantisi. |
| `ConnectionStrings:MikroWriteConnection` | Yazma akislari | Send sonrasi `cha_belge_no`/`cha_uuid` yazimi ve iade referansi kaydi. |
| `EInvoice:EndpointUrl` | Uyumsoft e-fatura servisi | `SendInvoiceAsync`, `RetrySendInvoicesAsync`, outbox PDF sorgulari. |
| `EInvoice:Username` | Uyumsoft user info | E-fatura WCF cagrilarinda kullanilir. |
| `EInvoice:Password` | Uyumsoft user info | E-fatura WCF cagrilarinda kullanilir. |
| `EDespatch:SupplierCustomerCode` | Satici firma bilgisi | Fatura XML'indeki supplier party Mikro'da bu cari koddan yuklenir. |
| `Assets/Xslt/efatura.xslt` | e-Fatura gorunum | XML icine base64 XSLT olarak eklenir veya render fallback olur. |
| `Assets/Xslt/earsiv.xslt` | e-Arsiv gorunum | XML icine base64 XSLT olarak eklenir veya render fallback olur. |
| `Assets/UblTr/xsdrt` | XSD validate | `/validate` sirasinda UBL-TR sema kontrolu. |

Notlar:

- Aktif production servis yeni kodu ancak deploy/restart sonrasi kullanir.
- `Axata` ayarlarindaki `MainEndpointUrl` fatura gonderimi icin kullanilmaz; Axata entegrasyon modulune aittir.
- Uyumsoft endpoint/kullanici/sifre eksikse `/send` ve `/retry` durur.
- `SupplierCustomerCode` eksikse `/validate` ve `/send` supplier bilgisi uretilemedigi icin durur.

## Canli Kontrol ve Test Checklist

Degisiklikten veya deploydan sonra onerilen hizli kontrol:

1. Build:
   ```bash
   dotnet build FurpaMerkezApi.sln --no-restore
   ```
2. Liste:
   ```http
   GET /api/fatura-islemleri/fatura-gonderimi?StartDate=2026-07-07&EndDate=2026-07-07&Scenario=EFatura&isSent=1
   ```
3. Liste performansi:
   - Tek gunluk sorgu kullan.
   - `isSent` ve `SentState` parametrelerini birlikte gonderme.
   - 2026-07-07 kontrolunde `cha_tarihi` ve `cha_belge_tarih` ayni gun cikti; bu yuzden mevcut `cha_tarihi` indeksiyle daraltma guvenli kabul edildi.
4. Detay/render:
   - Gonderilmemis bir belge icin detay HTML aciliyor mu kontrol et.
5. Validate:
   - Secili bir belge icin `/validate` basarili mi kontrol et.
6. Send:
   - Sadece validate basarili belge ile dene.
   - Basariliysa Mikro'da `cha_belge_no`, `cha_uuid`, `cha_kilitli` yazildi mi kontrol et.
7. PDF:
   - Gonderilmis belge icin `/pdf` PDF blob donduruyor mu kontrol et.
8. Iade:
   - Iade belge varsa return-reference candidates ve validate akisi denenmeli.
9. Retry:
   - Sadece `isSent=true` ve `cha_uuid` dolu belgeyle denenmeli.

## Degisiklik Yaparken Dikkat

- UBL XML alanlarina dokunurken mutlaka `/validate` ve build calistirilmali.
- XSLT degisiklikleri gorunum odaklidir; XML is kurallarini duzeltmez.
- KDV/istisna/iade kurallarinda degisiklik yapiliyorsa hem business validator hem XML builder birlikte dusunulmeli.
- KDV pointer eslesmesi hard-coded yapilmamali; canli Mikro `dbo.fn_hs_vergi_oran_listesi()` fonksiyonu esas alinmali.
- Hizmet faturalarinda KDV hesabi degisirse `cha_vergi1..cha_vergi10` secimi mutlaka tekrar test edilmeli.
- `FaturaSeries` eslesmesi degistirilirse toplam tutarlar tekrar kontrol edilmeli.
- Uyumsoft gonderiminden once validate katmanini atlayan bir UI akisina izin verilmemeli.
- Sunucuya deploy/restart yapilmadan `10.0.0.100:7508` uzerindeki endpointler yeni kodu yansitmaz.
- `/send` performans nedeniyle validator calistirmadigi icin bu davranis degistirilecekse UI akisi ve operasyon suresi birlikte degerlendirilmelidir.

## Komutlar

Build:

```bash
dotnet build FurpaMerkezApi.sln --no-restore
```

XSLT hizli parse/yukleme kontrolu icin PowerShell:

```powershell
$paths = @(
  'src/FurpaMerkezApi.WebApi/Assets/Xslt/efatura.xslt',
  'src/FurpaMerkezApi.WebApi/Assets/Xslt/earsiv.xslt'
)

foreach ($path in $paths) {
  [xml]$xml = Get-Content -Path $path -Raw
  $settings = [System.Xml.XmlReaderSettings]::new()
  $settings.DtdProcessing = [System.Xml.DtdProcessing]::Parse
  $reader = [System.Xml.XmlReader]::Create((Resolve-Path $path), $settings)
  $xslt = [System.Xml.Xsl.XslCompiledTransform]::new()
  $xslt.Load($reader)
  $reader.Close()
  Write-Output "$path OK"
}
```
