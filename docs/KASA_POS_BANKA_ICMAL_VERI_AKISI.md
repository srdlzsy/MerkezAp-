# Kasa, POS, Banka ve Icmal Veri Akisi

Bu dokuman; sube ayarlarindaki fiziksel kasa tanimlarinin, POS/PAV terminal satirlarinin, Mikro banka odeme katalogunun ve Icmal Kaydi Girisi ekraninin birbiriyle nasil baglandigini anlatir.

## Kisa Cevap

| Bilgi | Kesin kaynak | Yazilan yer |
|---|---|---|
| Sube icindeki fiziksel kasa | Furpa `CashRegistryDetails` | Kasa/POS ayar ekrani |
| Kasanin PAV/finans grup numarasi | Furpa `CashRegisterDetails.CashRegisterNo` | Kasa/POS ayar ekrani |
| Banka, terminal ID ve merchant no | Furpa `CashRegisterDetails` | Kasa/POS ayar ekrani |
| Banka odeme tipi no ve muhasebe hesap kodu | Mikro `PaymentTypes` | Bu ekrandan yazilmaz; yalniz katalog olarak okunur |
| Icmalde gosterilecek kasa listesi | Furpa `CashRegistryDetails` | Ayar ekranindaki fiziksel kasa kayitlari |
| Icmalde gosterilecek banka slip satirlari | Furpa terminal satiri + Mikro `PaymentTypes` eslesmesi | Iki kaynaktan okunarak response uretilir |

Eski Mikro `CashRegisterDetails` ve `CashRegisterBranches` tablolari bu akislar icin terminal kaynagi veya fallback degildir. Eski kayitlar yeni ayar ve icmal ekranini etkilemez.

## Alanlarin Anlami

### `branchNo`

Sube/depo numarasidir. Ornek: `129`.

### `cashNo`

Sube icindeki fiziksel kasa numarasidir. Furpa `CashRegistryDetails.CashRegisterNo` ve Furpa `CashRegisterDetails.CashNo` alanlarinin ortak degeridir.

Ornek: `103`.

### `cashRegisterNo` / `cashFinanceNumber` / `terminalNo`

POS veya finans terminal grubunun numarasidir. Furpa `CashRegisterDetails.CashRegisterNo` alaninda tutulur.

Ornek: `PAV210009468` veya `UB0016027768`.

- Ayar endpointlerinde ayni deger `terminalNo` adiyla donebilir.
- Sube kasa listesinde ayni deger `cashFinanceNumber` adi ile gosterilir.
- Icmal request ve bazi geriye uyumlu lookup endpointlerinde `cashRegisterNo` adi kullanilir.

Bu deger fiziksel kasa numarasi degildir.

### `terminalId`

Bankanin POS cihaz/terminal kimligidir. Her banka satirinda farkli olabilir.

Ornek: `PSB75693`.

### `merchantNo`

Bankanin uye isyeri numarasidir.

### `bank`

Furpa terminal satirinda saklanan banka adidir. Bu ad Mikro `PaymentTypes.PaymentName` katalogundaki `PaymentGenus = 1` kaydiyla eslesmelidir.

API buyuk/kucuk harf farkini kabul eder, ancak Furpa'ya Mikro katalogundaki kesin yazimi kaydeder. Katalogda olmayan banka yeni kayit veya guncellemede `400 Bad Request` ile reddedilir.

## Tablo Iliskisi

Ornek kayit:

```text
Sube: 145
Fiziksel kasa: 163
PAV grubu: PAV210009468
Banka: TEB
Terminal ID: PSB75693
Merchant: 000000000495366
```

Furpa fiziksel kasa kaydi:

```text
CashRegistryDetails
BranchNo = 145
CashRegisterNo = 163
CashRegisterType = ...
```

Furpa POS/PAV satiri:

```text
CashRegisterDetails
CashNo = 163
CashRegisterNo = PAV210009468
Bank = TEB
TerminalId = PSB75693
MerchantNo = 000000000495366
```

Mikro banka katalog kaydi:

```text
PaymentTypes
PaymentName = TEB
PaymentTypeNo = ...
PaymentGenus = 1
AccountCode = ...
```

Iliski su sekildedir:

```text
branchNo + cashNo
        |
        v
Furpa CashRegistryDetails
        |
        | CashRegistryDetails.CashRegisterNo = CashRegisterDetails.CashNo
        v
Furpa CashRegisterDetails
        |
        | CashRegisterDetails.Bank = PaymentTypes.PaymentName
        v
Mikro PaymentTypes (PaymentGenus = 1)
```

## Ayar Ekrani Yazma Akisi

### 1. Secenekleri yukleme

```text
GET /api/ayar-islemleri/kasa-pos-terminalleri/secenekler
```

- Kasa tipi seceneklerini dondurur.
- Banka dropdown'unu Mikro `PaymentTypes` tablosundaki `PaymentGenus = 1` kayitlarindan doldurur.
- UI banka adini serbest metin olarak yazdirmamalidir.

### 2. Yeni kasa ve terminal grubunu birlikte olusturma

```text
POST /api/ayar-islemleri/kasa-pos-terminalleri
```

API:

1. Subenin Furpa `BranchDetails` kaydini kontrol eder.
2. Banka adlarini Mikro `PaymentTypes` kataloguyla dogrular.
3. PAV/finans grubunun baska kasada kullanilip kullanilmadigini kontrol eder.
4. Furpa `CashRegistryDetails` fiziksel kasa kaydini olusturur.
5. Furpa `CashRegisterDetails` banka terminal satirlarini olusturur.
6. Furpa yazilarini tek transaction icinde tamamlar.

Bu islem Mikro `CashRegisterDetails`, Mikro `CashRegisterBranches` veya Mikro `PaymentTypes` tablolarina yazmaz.

### 3. Mevcut kasaya banka terminal satiri ekleme

```text
POST /api/ayar-islemleri/kasa-pos-terminalleri/subeler/{branchNo}/kasalar/{cashNo}/terminaller
```

Yalniz Furpa `CashRegisterDetails` tablosuna yazar. Banka adi once Mikro katalogundan dogrulanir.

### 4. Terminal satirini guncelleme

```text
PUT /api/ayar-islemleri/kasa-pos-terminalleri/subeler/{branchNo}/kasalar/{cashNo}/terminaller/{terminalId}
```

- Banka, terminal ID ve merchant bilgisi guncellenebilir.
- Birden fazla banka satiri bulunan PAV grubunun grup numarasi tek satirdan degistirilemez.
- Tum grubu baska kasaya almak icin `Dogru Kasaya Tasi` islemi kullanilir.

### 5. PAV grubunu dogru kasaya tasima

```text
PUT /api/ayar-islemleri/kasa-pos-terminalleri/subeler/{branchNo}/kasalar/{cashNo}/terminal-gruplari/{terminalNo}/tasi
```

Ayni PAV numarasina ait tum banka satirlarinin Furpa `CashRegisterDetails.CashNo` degerini hedef fiziksel kasa ile degistirir.

### 6. Silme

```text
DELETE /api/ayar-islemleri/kasa-pos-terminalleri/subeler/{branchNo}/kasalar/{cashNo}
DELETE /api/ayar-islemleri/kasa-pos-terminalleri/subeler/{branchNo}/kasalar/{cashNo}/terminaller/{terminalId}
```

- Terminal silme yalniz secilen Furpa banka terminal satirini siler.
- Kasa silme fiziksel kasa kaydini siler. Ayni fiziksel kasa baska sube kaydinda kullanilmiyorsa bagli Furpa terminal satirlari da silinir.

## Ayar Ekrani Okuma Akisi

### Sube kasalari

```text
GET /api/ayar-islemleri/sube-ayarlari/{branchNo}/kasalar
```

Fiziksel kasa satirlarini Furpa `CashRegistryDetails` tablosundan okur. `cashFinanceNumber`, ayni fiziksel kasaya bagli Furpa `CashRegisterDetails.CashRegisterNo` degerinden cozulur.

Ayni fiziksel kasada birden fazla farkli PAV/finans numarasi varsa API rastgele ilk satiri secmez; `409 Conflict` dondurur.

### Kasanin terminal satirlari

```text
GET /api/ayar-islemleri/kasa-pos-terminalleri/kasalar/{cashNo}/terminaller?branchNo={branchNo}
```

Once `branchNo + cashNo` fiziksel kasa eslesmesini Furpa `CashRegistryDetails` ile sinirlar, sonra Furpa `CashRegisterDetails` banka satirlarini dondurur.

### Tanim sorunlari

```text
GET /api/ayar-islemleri/kasa-pos-terminalleri/tanim-sorunlari?branchNo={branchNo}
```

Asagidaki sorunlari raporlar:

- `MISSING_TERMINAL_GROUP`: Fiziksel kasa var, POS/PAV satiri yok.
- `MULTIPLE_TERMINAL_GROUPS`: Bir fiziksel kasaya birden fazla PAV grubu bagli.
- `INCOMPLETE_TERMINAL_ROW`: PAV, banka, terminal ID veya merchant bilgisi eksik.
- `UNKNOWN_BANK`: Furpa banka adi Mikro `PaymentTypes` katalogunda yok.
- `TERMINAL_GROUP_SHARED`: Ayni PAV grubu birden fazla fiziksel kasaya bagli.

Angular ayar ekrani bu sorunlari listeler ve `Kasayi Ac` aksiyonuyla ilgili kasayi duzeltmeye acar.

## Icmal Ekrani Okuma Sirasi

Yeni UI asagidaki sirayi kullanmalidir.

### 1. Fiziksel kasa listesini getir

```text
GET /api/kasa-islemleri/kasa-sayimlari/kasalar?branchNo=145
```

Kaynak: Furpa `CashRegistryDetails`.

UI kullaniciya `cashNo` degerini fiziksel kasa olarak sectirir.

### 2. Secilen kasanin PAV/finans detayini getir

```text
GET /api/kasa-islemleri/kasa-sayimlari/kasa-detayi?branchNo=145&cashNo=163
```

Kaynak: Furpa `CashRegisterDetails`.

- Secilen `cashNo` degeri gercekten `branchNo` subesine ait olmalidir.
- Tek PAV/finans grubu varsa detay doner.
- Birden fazla PAV grubu varsa `409 Conflict` doner ve ayar ekraniyla eslesme duzeltilmelidir.
- Kayit yoksa `404 Not Found` doner.

### 3. Banka slip satirlarini getir

```text
GET /api/kasa-islemleri/kasa-sayimlari/odeme-tipleri/banka?branchNo=145&cashNo=163
```

Backend:

1. Fiziksel kasanin subeye ait oldugunu Furpa `CashRegistryDetails` ile dogrular.
2. Bu kasanin Furpa `CashRegisterDetails` banka terminal satirlarini okur.
3. Her satirin `Bank` degerini Mikro `PaymentTypes.PaymentName` ile eslestirir.
4. Yalniz `PaymentGenus = 1` odeme tiplerini dondurur.
5. Response'ta Furpa `TerminalId` ile Mikro `PaymentTypeNo` ve `AccountCode` degerlerini birlestirir.

Ornek response satiri:

```json
{
  "paymentName": "TEB",
  "paymentTypeNo": 7,
  "terminalId": "PSB75693",
  "accountCode": "0007",
  "slipNumber": 0,
  "amountValue": 0,
  "paymentGenus": 1
}
```

Yeni UI banka endpointine `branchNo + cashNo` gondermelidir. `cashRegisterNo` query alani yalniz eski istemciler icin geriye uyumluluk yoludur.

## Icmal Kaydi Sirasinda Kullanilan Bilgiler

Icmal create body icinde:

- `warehouseNo`: hedef sube/depo.
- `cashNo`: fiziksel kasa.
- `cashRegisterNo`: secilen kasanin Furpa PAV/finans grup numarasi.
- Banka satirlarinda `paymentTypeNo`: Mikro `PaymentTypes.PaymentTypeNo`.
- Banka satirlarinda `terminalId`: Furpa `CashRegisterDetails.TerminalId`.
- `accountCode`: Mikro `PaymentTypes.AccountCode`.

API, body'deki `cashRegisterNo` degerinin secilen `cashNo` ile Furpa tarafinda eslestigini yeniden kontrol eder. Eslesmiyorsa kaydi reddeder.

PAV/finans numarasi `PAV` ile basliyorsa olusan cari hesap hareketlerinde yeni kasa ayrimi icin `cha_special1 = "1"` yazilir. Diger kasa gruplarinda bu alan bos kalir.

## Banka Listesi Bos Gelirse

Asagidaki sirayla kontrol edilmelidir:

1. `branchNo + cashNo` Furpa `CashRegistryDetails` tablosunda var mi?
2. Ayni `cashNo` icin Furpa `CashRegisterDetails` satiri var mi?
3. Terminal satirinda `Bank`, `CashRegisterNo`, `TerminalId` ve `MerchantNo` dolu mu?
4. `Bank`, Mikro `PaymentTypes.PaymentName` alaninda var mi?
5. Eslesen Mikro kaydinda `PaymentGenus = 1` mi?
6. Ayar ekranindaki `Tanim Sorunlari` bolumu hata gosteriyor mu?

API, eski Mikro terminal tablolarina fallback yapmaz. Furpa tanimi eksikse eski kaydi getirerek sorunu gizlemek yerine bos liste veya acik hata dondurur.

## Kontrol SQL Ornekleri

Veritabani adlari ortama gore connection string'den alinmalidir. Asagidaki adlar mantiksal ornektir.

Fiziksel kasa:

```sql
SELECT DetailId, BranchNo, CashRegisterNo AS CashNo, CashRegisterType
FROM Furpa.dbo.CashRegistryDetails
WHERE BranchNo = 145
  AND CashRegisterNo = 163;
```

PAV ve banka terminal satirlari:

```sql
SELECT Id, CashNo, CashRegisterNo AS FinanceNo, Bank, TerminalId, MerchantNo
FROM Furpa.dbo.CashRegisterDetails
WHERE CashNo = 163
ORDER BY CashRegisterNo, Bank, Id;
```

Banka odeme katalogu:

```sql
SELECT PaymentTypeNo, PaymentName, PaymentGenus, AccountCode
FROM MikroDesktop_FURPA_2024.dbo.PaymentTypes
WHERE PaymentGenus = 1
ORDER BY PaymentName, PaymentTypeNo;
```

Tek sorguda mantiksal eslesme:

```sql
SELECT
    r.BranchNo,
    r.CashRegisterNo AS CashNo,
    d.CashRegisterNo AS FinanceNo,
    d.Bank,
    d.TerminalId,
    d.MerchantNo,
    p.PaymentTypeNo,
    p.AccountCode
FROM Furpa.dbo.CashRegistryDetails r
LEFT JOIN Furpa.dbo.CashRegisterDetails d
    ON d.CashNo = r.CashRegisterNo
LEFT JOIN MikroDesktop_FURPA_2024.dbo.PaymentTypes p
    ON p.PaymentName = d.Bank
   AND p.PaymentGenus = 1
WHERE r.BranchNo = 145
  AND r.CashRegisterNo = 163
ORDER BY d.CashRegisterNo, d.Bank, d.Id;
```

## Degistirme Yetkisi ve Sinirlar

Kasa/POS ayar ekranindan:

- Fiziksel kasa olusturulabilir ve silinebilir.
- Kasa tipi degistirilebilir.
- PAV/finans grubu ve banka terminal satirlari eklenebilir, guncellenebilir, silinebilir.
- PAV grubu dogru kasaya tasinabilir.

Bu ekrandan Mikro `PaymentTypes` katalog kaydi olusturulmaz veya degistirilmez. Yeni bir banka secenegi gerekiyorsa once Mikro odeme tipi katalogunda `PaymentGenus = 1` kaydi yetkili surecle tanimlanmalidir. Daha sonra banka ayar ekranindaki dropdown'da gorunur ve Furpa terminal satirinda kullanilabilir.

## Sahada Beklenen Sonuc

- Ayar ekraninda tanimlanan fiziksel kasa icmal kasa listesinde gorunur.
- Ayar ekraninda ayni kasaya eklenen banka terminal satirlari icmal banka listesinde gorunur.
- Yanlis subenin veya yanlis kasanin bankalari kullaniciya gelmez.
- Mikro katalogunda bulunmayan banka yeni kaydedilemez.
- Bir fiziksel kasaya birden fazla PAV baglanirsa rastgele secim yapilmaz; duzeltilebilir hata uretilir.
- Eski Mikro terminal kayitlari yeni Furpa tanimlarinin onune gecmez.
