# Kasa, POS, Banka ve Icmal Veri Akisi

Bu dokuman; sube ayarlarindaki fiziksel kasa tanimlarinin, POS/PAV terminal satirlarinin, Furpa odeme katalogunun ve Icmal Kaydi Girisi ekraninin birbiriyle nasil baglandigini anlatir.

## Kisa Cevap

| Bilgi | Kesin kaynak | Yazilan yer |
|---|---|---|
| Sube icindeki fiziksel kasa | Furpa `CashRegistryDetails` | Kasa/POS ayar ekrani |
| Kasanin PAV/finans grup numarasi | Furpa `CashRegisterDetails.CashRegisterNo` | Kasa/POS ayar ekrani |
| Banka, terminal ID ve merchant no | Furpa `CashRegisterDetails` | Kasa/POS ayar ekrani |
| Banka odeme tipi no ve muhasebe hesap kodu | Furpa `PaymentTypes` | Bu ekrandan yazilmaz; yalniz katalog olarak okunur |
| Icmalde gosterilecek kasa listesi | Furpa `CashRegistryDetails` | Ayar ekranindaki fiziksel kasa kayitlari |
| Icmalde gosterilecek banka slip satirlari | Furpa `CashRegisterDetails.PaymentTypeNo` + Furpa `PaymentTypes` | Tek Furpa katalog iliskisinden response uretilir |

Eski Mikro `CashRegisterDetails` ve `CashRegisterBranches` tablolari bu akislar icin terminal kaynagi veya fallback degildir. Eski kayitlar yeni ayar ve icmal ekranini etkilemez.

## Tam Kaynak Matrisi

### Veritabani ve Dosya Sinirlari

| Katman | Context / Kaynak | Ne Icin Kullaniliyor | Yetki |
|---|---|---|---|
| Furpa ayar veritabani | `FurpaDbContext` | Sube, fiziksel kasa, POS terminali, banka etiketi ve kasiyer tanimlari | Okuma / yazma |
| Mikro veritabani | `MikroDbContext` / `MikroWriteDbContext` | Banknot/cek tanimlari, icmal belgeleri ve muhasebe hareketleri | Okuma / yazma |
| POS klasoru | `BranchDetails.BranchIpAddress` + `PoskonFolderPath` | Kasanin `ZRAPxxxx.xxx` dosyasindan NET CIRO okumak | Salt okunur dosya |
| Eski POS muhasebe entegrasyonu | Mikro `ZReport*`, `CashRegisterBranches` ve fatura tablolari | POS muhasebe aktarimi; kasa/POS ayar ekraninin veya icmal dropdown'larinin kaynagi degildir | Ayri modul |

### Furpa Ayar Tablolari

| Tablo | Anahtar | Kullanilan Alanlar | Icmaldeki Anlami | Islem |
|---|---|---|---|---|
| `CashRegistryDetails` | `DetailId` | `BranchNo`, `CashRegisterNo`, `CashRegisterType` | Subeye bagli fiziksel kasa numarasi ve kasa tipi | Okuma / yazma |
| `CashRegisterDetails` | `Id` | `CashRegisterNo`, `Bank`, `TerminalId`, `MerchantNo`, `CashNo`, `PaymentTypeNo` | Finans/PAV numarasi, banka etiketi, terminal ve uye isyeri numarasi; `CashNo` ile fiziksel kasaya, `PaymentTypeNo` ile kataloga baglanir | Okuma / yazma |
| `PaymentTypes` | `PaymentTypeNo` | `PaymentName`, `PaymentGenus`, `AccountCode` | Banka, yemek ceki, online odeme ve gider katalogu | Okuma |
| `BranchDetails` | `BranchNo` | `BranchIpAddress`, `PosGenelFolderPath`, `PoskonFolderPath`, `BranchScalesFolderPath`, `ScalesType` | Sube ve POS dosya yolu bilgileri | Okuma / yazma |
| `Cashiers` | `CashierId` | `CashierCode`, `CashierName`, `CashierPassword`, `CashierAuthorization`, `CashierState` | Icmalde secilen kasiyer/mudur katalog bilgisi | Okuma / yazma |
| `KASIYER_TANIMLARI` | Kasiyer kaydi | Kasiyer kodu ve tanim alanlari | Kasiyer olusturma/guncellemede uyumluluk icin senkronlanan tablo; icmal lookup'i dogrudan `Cashiers` okur | Yazma esitlemesi |

### Katalog ve Referans Tablolari

| Tablo | Anahtar | Kullanilan Alanlar | Filtre / Kullanim |
|---|---|---|---|
| Furpa `PaymentTypes` | `PaymentTypeNo` | `PaymentName`, `PaymentGenus`, `AccountCode` | Banka: `PaymentGenus=1`; yemek ceki: `2`; online: `5` veya adinda `online`; magaza gideri: `110-113` |
| `BanknoteTypes` | `BanknoteType` | `Value` | Banknot adet ve tutar satirlarinin kupur katalogu |
| `GiftCheckTypes` | `GiftCheckType` | `Value` | Hediye ceki adet ve tutar satirlarinin katalogu |
| `DEPOLAR` | `dep_no` | `dep_adi` | Icmal liste/raporunda `BranchNo` icin sube/depo adi |

`PaymentTypes` katalogu POS cihazinin hangi kasaya bagli oldugunu tutmaz. Kasa-terminal iliskisi Furpa `CashRegisterDetails` tablosundan gelir; `CashRegisterDetails.PaymentTypeNo` katalogdaki odeme tipi, banka adi ve muhasebe hesap koduna baglanir. `Bank` alani okunabilir etiket ve eski kayit uyumlulugu icin korunur.

Icmal rapor sorgusundaki kolonlar su sabit odeme tipi numaralarina gore hesaplanir:

| Rapor Kolonu | `PaymentTypeID` |
|---|---:|
| Akbank / Halkbank / Is Bankasi / TEB / Yapi Kredi / Ziraat | `1 / 2 / 3 / 4 / 5 / 6` |
| Sodexo POS / Sodexo Kupon / Ticket POS / Ticket Kupon | `50 / 51 / 52 / 53` |
| Multinet / Setcard / Metropol | `54 / 55 / 56` |
| Masraf pusulasi | `100` |
| Magaza masrafi | `110-113` |
| Nakit | `500` |

Bu nedenle `PaymentTypes` tablosuna yeni bir banka veya yemek ceki eklemek lookup listesine kaydi getirebilir; ancak sabit kolonlu icmal raporuna yeni kolon/kategori eklemek icin rapor sorgusunun da guncellenmesi gerekir.

### Icmalin Kalici Yazildigi Mikro Tablolari

| Tablo | Belge Baglantisi | Fiziksel Olarak Yazilan Temel Alanlar | Rol |
|---|---|---|---|
| `Summaries` | `BranchNo + DocumentSerie + DocumentOrderNo` | `Id`, `CreateUser`, `DocumentSerie`, `DocumentOrderNo`, `PaymentTypeID`, `SummaryDate`, `BranchNo`, `CashNo`, `ZReportNo`, `CashierNo`, `ManagerNo`, `SlipNumber`, `Amount`, `TerminalId`, `Description`, `CreateDate`, `UpdateUser`, `UpdateDate` | Icmal odeme kalemlerini satir bazinda tutan ana tablo |
| `BanknoteMovements` | Ayni seri/sira ve sube | `DocumentSerie`, `DocumentOrderNo`, `BanknoteTypeID`, `BranchNo`, `CashNo`, `Quantity`, `Total` ve audit alanlari | Banknot kupur adetleri ve toplamlar |
| `GiftCheckMovements` | Ayni seri/sira ve sube | `DocumentSerie`, `DocumentOrderNo`, `GiftCheckTypeID`, `BranchNo`, `CashNo`, `Quantity`, `Total`, `CreateDate` | Hediye ceki adetleri ve toplamlar |
| `CARI_HESAP_HAREKETLERI` | `cha_aciklama = DocumentSerie.DocumentOrderNo` | `cha_evrak_tip`, `cha_tip`, `cha_cinsi`, `cha_kod`, `cha_meblag`, `cha_srmrkkodu`, `cha_special1`, tarih ve belge alanlari | Icmalin banka, nakit, Z toplam ve Z fark muhasebe hareketleri |

`Summaries` entity'sinde gorulen bazi alanlar fiziksel kolon degildir ve EF tarafinda `Ignore` edilmistir:

| Hesaplanan Alan | Gercek Kaynak |
|---|---|
| `TypeName` | `Summaries.PaymentTypeID -> PaymentTypes.PaymentTypeNo -> PaymentName` |
| `AccountCode` | `PaymentTypes.AccountCode` |
| `Total` | `Summaries.Amount` veya ilgili hareket tablosundaki `Total` |
| `StoreExpenseType` | `PaymentTypeID` ve kategori kurallari |
| Banknot `Value` | `BanknoteMovements.BanknoteTypeID -> BanknoteTypes.BanknoteType -> Value` |
| Hediye ceki `Value` | `GiftCheckMovements.GiftCheckTypeID -> GiftCheckTypes.GiftCheckType -> Value` |

### Create Istegi Alanlarinin Hedefi

| API Alani | Kontrol Edilen Kaynak | Yazildigi Yer |
|---|---|---|
| `warehouseNo` | Furpa `CashRegistryDetails.BranchNo`; Mikro `DEPOLAR.dep_no` rapor adi icin | `Summaries.BranchNo`, banknot/cek `BranchNo`, muhasebe sorumluluk merkezi |
| `cashNo` | Furpa `CashRegistryDetails.CashRegisterNo` | `Summaries.CashNo`, banknot/cek `CashNo` |
| `cashRegisterNo` | Furpa `CashRegisterDetails.CashRegisterNo`; ayni satirin `CashNo` baglantisi dogrulanir | Yeni/eski kasa kararinda kullanilir; `PAV` ise yeni kasa kabul edilir |
| `terminalId` | Furpa `CashRegisterDetails.TerminalId` | Ilgili `Summaries.TerminalId` |
| `paymentTypeId` | Furpa `PaymentTypes.PaymentTypeNo` | `Summaries.PaymentTypeID`, muhasebe hesap secimi |
| `amount` | Istek govdesi | `Summaries.Amount`, muhasebe `cha_meblag` |
| `zReportNo` | Istek ve POS dosyasi | `Summaries.ZReportNo`; `ZRAP{zNo:0000}.{cashNo:000}` dosyasi okunur |
| `cashierNo`, `managerNo` | Furpa kasiyer katalogu | `Summaries.CashierNo`, `Summaries.ManagerNo` |
| Banknot/cek adetleri | Mikro kupur katalogu ile dogrulanir | `BanknoteMovements`, `GiftCheckMovements` |

Belge serisi API tarafinda `F{warehouseNo}.{cashNo}` olarak uretilir. Sira numarasi bu seri icin `Summaries.DocumentOrderNo` azamisinin bir fazlasidir ve ilk belge `1` ile baslar.

### Muhasebe Hareketi Kurallari

| Hareket | Dahili Odeme Tipi | Hesap / Kod | Borc-Alacak Kurali |
|---|---:|---|---|
| Banka / yemek ceki satiri | `PaymentTypes.PaymentTypeNo` | `PaymentTypes.AccountCode` | Normal odeme hareketi |
| Nakit toplam | `200` | `0002` | Nakit muhasebe hareketi |
| Z farki | `300` | `0002` | Z fark hareketi |
| Z toplam | `400` | Sube/depo numarasi | Karsi Z toplam hareketi |
| `Summaries` nakit gorunum satiri | `500` | Fiziksel odeme katalogu degil | Liste/rapor icin ozel nakit satiri |

Muhasebe satirlari `cha_evrak_tip=60`, `cha_fileid=51`, `cha_cinsi=5`, seri `X` ve aciklama olarak icmalin `seri.sira` degeriyle olusur. Iliskili finans numarasi `PAV` ile basliyorsa `cha_special1='1'` yazilir.

### Endpointten Kaynaga Izleme

| Endpoint / Islem | Birinci Kaynak | Tamamlayici Kaynak | Sonuc |
|---|---|---|---|
| `GET /api/ayarlar/kasa-pos-terminalleri` | Furpa `CashRegistryDetails` | Furpa `CashRegisterDetails` | Fiziksel kasa ve bagli POS satirlari |
| `GET /api/ayarlar/kasa-pos-terminalleri/banka-secenekleri` | Furpa `PaymentTypes` | `PaymentGenus=1` | Banka secenekleri |
| `GET /api/kasa-islemleri/kasa-sayimlari/kasalar` | Furpa `CashRegistryDetails` | Sube filtresi | Fiziksel kasa listesi |
| `GET /api/kasa-islemleri/kasa-sayimlari/odeme-tipleri/banka` | Furpa `CashRegisterDetails` | Furpa `PaymentTypes` | Kasaya bagli banka/POS terminal secenekleri; `PaymentTypeNo=2` Halkbank icmal satiri bu secim listesinden haric tutulur |
| `GET /api/kasa-islemleri/kasa-sayimlari/odeme-tipleri/yemek-ceki` | Furpa `PaymentTypes` | `PaymentGenus=2` | Yemek ceki secenekleri |
| `GET /api/kasa-islemleri/kasa-sayimlari/odeme-tipleri/online` | Furpa `PaymentTypes` | `PaymentGenus=5` veya adinda `online` | Online odeme tipleri |
| `GET /api/kasa-islemleri/kasa-sayimlari/online-kasa-detaylari` | Furpa `CashRegisterDetails` | Banka adinda `online` | Online POS/kasa detaylari |
| `GET /api/kasa-islemleri/kasa-sayimlari/banknot-tipleri` | Mikro `BanknoteTypes` | - | Banknot kupurleri |
| `GET /api/kasa-islemleri/kasa-sayimlari/hediye-ceki-tipleri` | Mikro `GiftCheckTypes` | - | Hediye ceki kupurleri |
| `GET /api/kasa-islemleri/kasa-sayimlari/z-rapor-toplam` | Furpa `BranchDetails` | POS `ZRAP` dosyasi | NET CIRO / Z toplam |
| Icmal create/update | Furpa kasa-POS baglantisi ve odeme katalogu | Mikro hareket tablolari | Dort Mikro hareket tablosuna transaction ile kayit |
| Icmal liste/rapor/detay | Mikro `Summaries` | Mikro `DEPOLAR`, Furpa `PaymentTypes`, Mikro banknot/cek kataloglari | Ekran ve rapor DTO'lari |

### Bu Modulden Ayri Olan POS Muhasebe Kaynaklari

| Tablo Grubu | Kullanan Modul | Icmal Ayarlarinda Kullaniliyor mu? |
|---|---|---|
| `ZReportTotals`, `ZReportDetails`, `ZReportBankDetails` | `PosMuhasebeAktarimiService` | Hayir |
| `CashRegisterBranches` | POS muhasebe aktariminda kasa-sube eslestirme | Hayir |
| `Invoices`, `InvoiceLines` | POS muhasebe fatura aktarimi | Hayir |
| `ExpenseNotes`, `ExpenseNoteLines` | POS muhasebe gider pusulasi aktarimi | Hayir |
| Eski Mikro `CashRegisterDetails` | Eski/ayri kasa tanimi | Hayir; guncel ayar kaynagi Furpa `CashRegisterDetails` |

### Kod Kaynaklari

| Sorumluluk | Dosya |
|---|---|
| Kasa/POS ayarlari ve banka secenekleri | `src/FurpaMerkezApi.Infrastructure/Modules/AyarIslemleri/Ayarlar/AyarlarService.cs` |
| Icmal lookup'lari | `src/FurpaMerkezApi.Infrastructure/Modules/KasaIslemleri/KasaSayimlari/Lookups/CashSummaryLookupsUseCase.cs` |
| Kategori kurallari | `src/FurpaMerkezApi.Infrastructure/Modules/KasaIslemleri/KasaSayimlari/CashSummaryCategoryMatcher.cs` |
| Create/update/delete ve belge numarasi | `src/FurpaMerkezApi.Infrastructure/Modules/KasaIslemleri/KasaSayimlari/Commands/CashSummaryCommandsUseCase.cs` |
| Liste, detay ve rapor sorgulari | `src/FurpaMerkezApi.Infrastructure/Modules/KasaIslemleri/KasaSayimlari/Queries/CashSummaryQueriesUseCase.cs` |
| Mikro muhasebe hareketi uretimi | `src/FurpaMerkezApi.Infrastructure/Modules/KasaIslemleri/KasaSayimlari/Commands/CashSummaryCustomerMovementFactory.cs` |
| Z raporu dosya okuma | `src/FurpaMerkezApi.Infrastructure/Modules/KasaIslemleri/KasaSayimlari/Files/GetCashSummaryZReportTotalUseCase.cs` |
| Furpa entity ve kolon eslemeleri | `src/FurpaMerkezApi.Infrastructure/Persistence/Furpa/FurpaDbContext.cs` |
| Mikro icmal entity'leri | `src/FurpaMerkezApi.Infrastructure/Persistence/Mikro/Models/CashSummaryModuleModels.cs` |
| Mikro tablo eslemeleri | `src/FurpaMerkezApi.Infrastructure/Persistence/Mikro/MikroDbContext.AdditionalModels.cs` |
| Ayri POS muhasebe akisi | `src/FurpaMerkezApi.Infrastructure/Modules/EntegrasyonIslemleri/PosMuhasebeAktarimi/PosMuhasebeAktarimiService.cs` |

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

Furpa terminal satirinda saklanan banka adidir. Asil bag `CashRegisterDetails.PaymentTypeNo` ile Furpa `PaymentTypes.PaymentTypeNo` arasindadir. Banka adi katalogdaki `PaymentName` ile kanonik tutulur.

API buyuk/kucuk harf farkini kabul eder, ancak Furpa'ya Furpa katalogundaki kesin yazimi ve `PaymentTypeNo` degerini kaydeder. Katalogda olmayan banka yeni kayit veya guncellemede `400 Bad Request` ile reddedilir.

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
PaymentTypeNo = 4
TerminalId = PSB75693
MerchantNo = 000000000495366
```

Furpa banka katalog kaydi:

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
        | CashRegisterDetails.PaymentTypeNo = PaymentTypes.PaymentTypeNo
        v
Furpa PaymentTypes (PaymentGenus = 1)
```

## Ayar Ekrani Yazma Akisi

### 1. Secenekleri yukleme

```text
GET /api/ayar-islemleri/kasa-pos-terminalleri/secenekler
```

- Kasa tipi seceneklerini dondurur.
- Banka dropdown'unu Furpa `PaymentTypes` tablosundaki `PaymentGenus = 1` kayitlarindan doldurur.
- UI banka adini serbest metin olarak yazdirmamalidir.

### 2. Yeni kasa ve terminal grubunu birlikte olusturma

```text
POST /api/ayar-islemleri/kasa-pos-terminalleri
```

API:

1. Subenin Furpa `BranchDetails` kaydini kontrol eder.
2. Banka adlarini Furpa `PaymentTypes` kataloguyla dogrular ve `PaymentTypeNo` bagini kurar.
3. PAV/finans grubunun baska kasada kullanilip kullanilmadigini kontrol eder.
4. Furpa `CashRegistryDetails` fiziksel kasa kaydini olusturur.
5. Furpa `CashRegisterDetails` banka terminal satirlarini olusturur.
6. Furpa yazilarini tek transaction icinde tamamlar.

Bu islem Mikro `CashRegisterDetails`, Mikro `CashRegisterBranches` veya Mikro `PaymentTypes` tablolarina yazmaz.

### 3. Mevcut kasaya banka terminal satiri ekleme

```text
POST /api/ayar-islemleri/kasa-pos-terminalleri/subeler/{branchNo}/kasalar/{cashNo}/terminaller
```

Yalniz Furpa `CashRegisterDetails` tablosuna yazar. Banka adi once Furpa katalogundan dogrulanir.

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
- `UNKNOWN_BANK`: Furpa banka adi Furpa `PaymentTypes` katalogunda yok.
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
3. Her satirin `PaymentTypeNo` degerini Furpa `PaymentTypes.PaymentTypeNo` ile eslestirir; eski null kayitlarda gecici olarak banka adi eslesmesini kullanir.
4. Yalniz `PaymentGenus = 1` odeme tiplerini dondurur.
5. Icmalde ayri sabit kategori olarak hesaplanan Halkbank'i adindan degil, resmi `PaymentTypeNo = 2` kimliginden taniyarak listeden cikarir.
6. Response'ta Furpa `TerminalId`, `PaymentTypeNo` ve `AccountCode` degerlerini birlestirir.

Halkbank filtresi metin aramasi yapmaz. Banka etiketi sonradan degisse bile `PaymentTypeNo=2` haric tutulur; adinda `Halkbank` gecen baska bir odeme tipi ise kimligi `2` degilse otomatik olarak elenmez.

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
- Banka satirlarinda `paymentTypeNo`: Furpa `PaymentTypes.PaymentTypeNo`.
- Banka satirlarinda `terminalId`: Furpa `CashRegisterDetails.TerminalId`.
- `accountCode`: Furpa `PaymentTypes.AccountCode`.

API, body'deki `cashRegisterNo` degerinin secilen `cashNo` ile Furpa tarafinda eslestigini yeniden kontrol eder. Eslesmiyorsa kaydi reddeder.

PAV/finans numarasi `PAV` ile basliyorsa olusan cari hesap hareketlerinde yeni kasa ayrimi icin `cha_special1 = "1"` yazilir. Diger kasa gruplarinda bu alan bos kalir.

## Banka Listesi Bos Gelirse

Asagidaki sirayla kontrol edilmelidir:

1. `branchNo + cashNo` Furpa `CashRegistryDetails` tablosunda var mi?
2. Ayni `cashNo` icin Furpa `CashRegisterDetails` satiri var mi?
3. Terminal satirinda `Bank`, `CashRegisterNo`, `TerminalId` ve `MerchantNo` dolu mu?
4. `PaymentTypeNo`, Furpa `PaymentTypes.PaymentTypeNo` alaninda var mi?
5. Eslesen Furpa kaydinda `PaymentGenus = 1` mi?
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
SELECT Id, CashNo, CashRegisterNo AS FinanceNo, Bank, PaymentTypeNo, TerminalId, MerchantNo
FROM Furpa.dbo.CashRegisterDetails
WHERE CashNo = 163
ORDER BY CashRegisterNo, Bank, Id;
```

Banka odeme katalogu:

```sql
SELECT PaymentTypeNo, PaymentName, PaymentGenus, AccountCode
FROM Furpa.dbo.PaymentTypes
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
LEFT JOIN Furpa.dbo.PaymentTypes p
    ON p.PaymentTypeNo = d.PaymentTypeNo
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

Bu ekrandan Furpa `PaymentTypes` katalog kaydi olusturulmaz veya degistirilmez. Yeni bir banka secenegi gerekiyorsa once Furpa odeme tipi katalogunda `PaymentGenus = 1` kaydi yetkili surecle tanimlanmalidir. Daha sonra banka ayar ekranindaki dropdown'da gorunur ve Furpa terminal satirinda kullanilabilir.

## Sahada Beklenen Sonuc

- Ayar ekraninda tanimlanan fiziksel kasa icmal kasa listesinde gorunur.
- Ayar ekraninda ayni kasaya eklenen banka terminal satirlari icmal banka listesinde gorunur.
- Yanlis subenin veya yanlis kasanin bankalari kullaniciya gelmez.
- Furpa katalogunda bulunmayan banka yeni kaydedilemez.
- Bir fiziksel kasaya birden fazla PAV baglanirsa rastgele secim yapilmaz; duzeltilebilir hata uretilir.
- Eski Mikro terminal kayitlari yeni Furpa tanimlarinin onune gecmez.
