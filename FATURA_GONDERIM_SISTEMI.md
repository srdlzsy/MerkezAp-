# Fatura Gonderim Sistemi

Bu dokuman, `fatura-gonderimi` modulunun nasil calistigini, hangi katmanlardan gectigini ve ileride sorun cikarmamasi icin dikkat edilmesi gereken noktalarini anlatir.

## Kisa Ozet

Sistemde resmi fatura verisi **UBL Invoice XML** olarak uretilir. UI tarafindan secilen Mikro belgeleri backend'e gonderilir; backend bu belgelerden UBL-TR uyumlu XML olusturur, kurallari kontrol eder, XSD ile dogrular ve ancak bundan sonra Uyumsoft'a gonderir.

XSLT ise faturanin resmi verisi degildir. XSLT, faturanin HTML/PDF gibi goruntulendiginde nasil gorunecegini belirleyen sablondur. Karsi taraf ve entegrator asil olarak XML icindeki UBL alanlarini isler.

## Ana Dosyalar

- `src/FurpaMerkezApi.WebApi/Controllers/Modules/FaturaIslemleri/FaturaGonderimi/FaturaGonderimiController.cs`
  - API endpoint'lerini acar.
- `src/FurpaMerkezApi.Application/Modules/FaturaIslemleri/FaturaGonderimi/InvoiceSendingModels.cs`
  - Request/response modellerini tutar.
- `src/FurpaMerkezApi.Infrastructure/Modules/FaturaIslemleri/FaturaGonderimi/InvoiceSendingService.cs`
  - Listeleme, render, validate, send, UBL XML olusturma ve Uyumsoft gonderimini yonetir.
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

Bu endpoint Mikro'dan gonderime uygun belgeleri listeler. Senaryoya gore e-Fatura/e-Arsiv ayrimi yapilir.

Onemli nokta: seri eslesmesi `FaturaSeries` uzerinden yapilir. Seri cakismalarinda toplamlar sismesin diye en uzun ve en spesifik seri secilir. Ornek: `FR` ve `FRP` varsa `FRP001` icin `FRP` tercih edilir.

### 2. Detay ve Render

Endpoint:

```http
GET /api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}
POST /api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}/render
```

Render akisi UBL XML'i olusturur ve XSLT ile HTML gorunum uretir. Bu islem Uyumsoft'a gonderim yapmaz ve Mikro'da belgeyi gonderilmis isaretlemez.

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
6. Iade faturasiysa iade referansi zorunlu hale gelir.
7. UBL Invoice XML olusturulur.
8. Is kurali validasyonu yapilir.
9. UBL-TR XSD validasyonu yapilir.
10. Uyumsoft `SendInvoiceAsync` servisine gonderilir.
11. Uyumsoft belge numarasi donerse Mikro guncellenir.

Gonderim basarili olunca Mikro'da guncellenen alanlar:

- `cha_belge_no`: Uyumsoft'un verdigi fatura numarasi
- `cha_kilitli`: `true`
- `cha_degisti`: `true`
- `cha_lastup_user`: sistemde sabit Mikro kullanicisi
- `cha_lastup_date`: guncelleme zamani

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

Toplu gonderim icin onerilen akış:

1. Kullanici tarih ve senaryo ile fatura listesini ceker.
2. Kullanici belgeleri secer.
3. UI `/validate` endpoint'ini cagirir.
4. Her belge icin sonucu gosterir.
5. Hatali belgeler varsa kullaniciya gonderim yaptirmadan duzeltme ister.
6. Tum belgeler basariliysa `/send` cagirir.
7. Send sonucunda her belge icin basarili/basarisiz mesajini gosterir.

Bu akış Uyumsoft'a hatali belge gonderme riskini azaltir.

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

## Degisiklik Yaparken Dikkat

- UBL XML alanlarina dokunurken mutlaka `/validate` ve build calistirilmali.
- XSLT degisiklikleri gorunum odaklidir; XML is kurallarini duzeltmez.
- KDV/istisna/iade kurallarinda degisiklik yapiliyorsa hem business validator hem XML builder birlikte dusunulmeli.
- KDV pointer eslesmesi hard-coded yapilmamali; canli Mikro `dbo.fn_hs_vergi_oran_listesi()` fonksiyonu esas alinmali.
- Hizmet faturalarinda KDV hesabi degisirse `cha_vergi1..cha_vergi10` secimi mutlaka tekrar test edilmeli.
- `FaturaSeries` eslesmesi degistirilirse toplam tutarlar tekrar kontrol edilmeli.
- Uyumsoft gonderiminden once validate katmanini atlayan bir UI akisina izin verilmemeli.
- Sunucuya deploy/restart yapilmadan `10.0.0.100:7508` uzerindeki `/validate` yeni kodu yansitmaz.

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
