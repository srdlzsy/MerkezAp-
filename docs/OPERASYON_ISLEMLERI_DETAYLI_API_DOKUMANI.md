# Operasyon Islemleri Dosya Akisi

Bu dokuman operasyon modulunde kullanicinin "dosya olustur/gonder" dediginde API tarafinda ne oldugunu anlatir.

Normal kullanicida depo numarasi request body veya query'den istenmez; login olan kullanicinin JWT token'indaki `warehouse_no` claim'i kullanilir. `Admin`/`Administrator` baska depo icin dosya isi baslatacaksa dosya olusturma endpointlerinde opsiyonel `warehouseNo` query parametresi gonderebilir.

## Ortak Isleyis

Operasyon dosya aksiyonlari `api/operations` route'u altindadir.

Endpointler:

- `GET /api/operations/scalesfile?warehouseNo=135`
- `GET /api/operations/productbarcodeplunofile?warehouseNo=135`
- `GET /api/operations/productbarcodeplonofile?warehouseNo=135`
- `GET /api/operations/cashierfile?warehouseNo=135`
- `GET /api/operations/promofile?warehouseNo=135`
- `GET /api/operations/jobs/{jobId}`

Dosya olusturma endpointleri `operasyon-islemleri.operations.create` yetkisi ister. Job detayini okumak icin `operasyon-islemleri.operations.detail` yetkisi gerekir.

Akis:

1. UI ilgili endpointi cagirir.
2. API islem deposunu cozer: normal kullanicida token deposu, admin query'de `warehouseNo` gonderdiyse secili depo kullanilir.
3. Yapilacak is `OperationsJobQueue` icine atilir.
4. API hemen `202 Accepted` doner.
5. Response icinde `jobId`, `operation`, `status`, `warehouseNo`, `createdAtUtc` vardir.
6. Arka plandaki `OperationsJobWorker` isi kuyruktan alir.
7. Job once `Running`, sonra basariliysa `Succeeded`, hata alirsa `Failed` olur.
8. UI `GET /api/operations/jobs/{jobId}` ile periyodik sorgulama yapar.

Job detayinda su alanlar doner:

- `jobId`
- `operation`
- `status`
- `warehouseNo`
- `requestedByUserId`
- `createdAtUtc`
- `startedAtUtc`
- `completedAtUtc`
- `message`
- `errorMessage`
- `files`

`files` icindeki her kayit:

- `fileName`: olusan dosya adi
- `localPath`: API sunucusunda olusan lokal dosya yolu
- `networkPath`: sube/ag paylasimina kopyalandiysa hedef yol, kopyalanmadiysa `null`

## Export Klasoru

Dosyalar once lokal export klasorune yazilir.

`OperationsExport:BasePath` doluysa temel klasor olarak bu deger kullanilir. Bos ise varsayilan klasor:

```text
{uygulama_klasoru}/App_Data/OperationsExports
```

Her job kendi klasorune yazilir:

```text
{BasePath}/{warehouseNo}/{operationFolder}/{jobId}
```

Ornek:

```text
D:\Furpa\OperationsExports\135\scalesfile\{jobId}
```

Bu klasore IIS Application Pool kimliginin yazma izni olmalidir. Yazma izni yoksa job `Failed` olur.

## Network Kopyalama

Lokal dosya olustuktan sonra sistem, sube konfigrasyonu varsa dosyayi ag paylasimina da kopyalar.

Sube bilgisi `FurpaDbContext.BranchDetails` tablosundan okunur:

- `BranchNo`: cozulmus islem deposu ile eslesir
- `BranchIpAddress`: sube IP adresi
- `BranchScalesFolderPath`: terazi dosyasi hedef klasoru
- `PosGenelFolderPath`: POS genel dosyalari hedef klasoru
- `PoskonFolderPath`: kasa mesaj dosyalari hedef klasoru
- `ScalesType`: terazi tipi

Hedef UNC yol su sekilde kurulur:

```text
\\{BranchIpAddress}\{folderPath}
```

Ornek:

```text
\\192.168.1.10\Terazi
```

Sube kaydi, IP veya hedef klasor bilgisi yoksa ilgili dosya lokal olusur ama network'e kopyalanmaz. Bu durumda `networkPath = null` donebilir.

## Terazi Dosyasi

Endpoint:

```http
GET /api/operations/scalesfile
```

Job tipi:

```text
ScalesFile
```

Ne yapar:

1. Cozulmus islem deposuna gore `BranchDetails` kaydi aranir.
2. Terazi dosyasi icin branch kaydi zorunludur.
3. Mikro'dan teraziye gidecek urunler cekilir.
4. `ScalesType` degerine gore dosya formati secilir.
5. Dosya lokal export klasorune yazilir.
6. `BranchScalesFolderPath` doluysa sube paylasimina kopyalanir.

Mikro urun filtresi:

- Barkod bos olmayacak.
- Barkod `27` veya `29` ile baslayacak.
- Barkod uzunlugu 7 olacak.
- Barkod birim pointer degeri 1 olacak.
- Fiyat deposu cozulmus islem deposu olacak.
- Fiyat liste sirasi 1 olacak.
- Fiyat 0'dan buyuk olacak.
- Stok pasif olmayacak.
- Stok satis dursun olmayacak.

Kullanilan Mikro alanlari:

- `STOKLAR.sto_kod`
- `STOKLAR.sto_isim`
- `STOKLAR.sto_plu_no`
- `STOKLAR.sto_RafOmru`
- `STOKLAR.sto_toplam_rafomru`
- `BARKOD_TANIMLARI.bar_kodu`
- `STOK_SATIS_FIYAT_LISTELERI.sfiyat_fiyati`
- `STOK_SATIS_FIYAT_LISTELERI.sfiyat_deposirano`
- `STOK_SATIS_FIYAT_LISTELERI.sfiyat_listesirano`

`ScalesType = 0` ise olusan dosya:

```text
Terazi.plu
```

Bu format CAS 16 tipi icindir. PLU degeri mumkunse barkodun son 4 hanesinden uretilir, olmazsa Mikro'daki `sto_plu_no` kullanilir. Urun adi Turkce karakterlerden arindirilir ve fiyat kurus formatina cevrilir.

`ScalesType = 1` ise olusan dosya:

```text
ART_STM.txt
```

Bu format CAS 500 tipi icindir. Dosyada PLU no, barkod, urun adi, fiyat ve raf omru bilgisi kullanilir.

Basarili mesaj:

```text
Terazi dosyasi olusturuldu.
```

Hata durumlari:

- Branch kaydi yoksa job `Failed` olur.
- Terazi urunu bulunamazsa job `Failed` olur.
- `ScalesType` 0 veya 1 degilse job `Failed` olur.
- Lokal klasore yazilamazsa job `Failed` olur.
- Network kopyalama sirasinda hata olursa job `Failed` olur.

## Urun, Barkod ve PLU Dosyalari

Endpointler:

```http
GET /api/operations/productbarcodeplunofile
GET /api/operations/productbarcodeplonofile
```

Ikinci endpoint eski uyumluluk alias'idir. Ikisi de ayni isi yapar.

Job tipi:

```text
ProductBarcodePluNoFile
```

Ne yapar:

1. Cozulmus islem deposuna gore branch kaydi opsiyonel okunur.
2. Depoya ait kasalar `CashRegistryDetails` tablosundan okunur.
3. Mikro'dan urun, barkod, PLU ve fiyat bilgileri cekilir.
4. `URUN.DAT`, `BARKOD.IDX`, `PLUNO.IDX` dosyalari olusturulur.
5. Her kasa icin `MESAJ.xxx` dosyasi olusturulur.
6. Branch network path bilgileri varsa dosyalar sube paylasimlarina kopyalanir.

Mikro urun filtresi:

- Barkod bos olmayacak.
- Barkod birim pointer degeri 1 olacak.
- Fiyat deposu cozulmus islem deposu olacak.
- Fiyat liste sirasi 1 olacak.
- Fiyat 0'dan buyuk olacak.
- Stok pasif olmayacak.
- Stok satis dursun olmayacak.

Terazi dosyasindan farki:

- Barkodun `27` veya `29` ile baslamasi zorunlu degildir.
- Barkodun 7 haneli olmasi zorunlu degildir.
- Daha genel POS urun dosyasi uretir.

Olusan dosyalar:

```text
URUN.DAT
BARKOD.IDX
PLUNO.IDX
MESAJ.001
MESAJ.002
...
```

`URUN.DAT` icindeki ana bilgiler:

- PLU no
- Barkod
- Urun adi
- Fiyat
- Perakende vergi orani
- Birim adi
- Barkod icerigi bilgisi

`BARKOD.IDX` barkoda gore index dosyasidir.

`PLUNO.IDX` PLU numarasina gore index dosyasidir.

`MESAJ.xxx` dosyalari kasa bazlidir. `xxx` kasa numarasinin 3 haneli halidir. Ornek: kasa no 1 icin `MESAJ.001`.

Dosyalarin hedefleri:

- `URUN.DAT`, `BARKOD.IDX`, `PLUNO.IDX` -> `PosGenelFolderPath`
- `MESAJ.xxx` -> `PoskonFolderPath`

Basarili mesaj:

```text
URUN.DAT, BARKOD.IDX ve PLUNO.IDX dosyalari olusturuldu.
```

Hata durumlari:

- Urun bulunamazsa job `Failed` olur.
- Lokal klasore yazilamazsa job `Failed` olur.
- Network kopyalama sirasinda hata olursa job `Failed` olur.

Not:

Branch kaydi bu is icin zorunlu degildir. Branch yoksa lokal dosya uretilebilir, network kopyasi yapilmaz.

## Kasiyer ve Yetki Dosyalari

Endpoint:

```http
GET /api/operations/cashierfile
```

Job tipi:

```text
CashierFile
```

Ne yapar:

1. Cozulmus islem deposuna gore branch kaydi opsiyonel okunur.
2. Depoya ait kasalar `CashRegistryDetails` tablosundan okunur.
3. Aktif kasiyerler `Cashiers` tablosundan okunur.
4. Yetki satirlari `AuthorizationFiles` tablosundan okunur.
5. `KASIYER.DAT` ve `YETKI.DAT` dosyalari olusturulur.
6. Her kasa icin `MESAJ.xxx` dosyasi olusturulur.
7. Branch network path bilgileri varsa dosyalar sube paylasimlarina kopyalanir.

Kasiyer filtresi:

- `CashierState = true`

`KASIYER.DAT` icindeki ana bilgiler:

- Kasiyer kodu
- Kasiyer adi
- Kasiyer sifresi
- Kasiyer yetkisi

Kasiyer adi formatlanirken:

- Ad soyad gibi boslukluysa ilk ad ve ikinci kelimenin ilk harfi kullanilir.
- 20 karakterden uzunsa kisaltilir.
- Turkce karakterler ASCII karsiliklarina cevrilir.

`YETKI.DAT` icinde 3 satir uretilir:

```text
Z,...
R,...
X,...
```

Her satir `AuthorizationFiles` tablosundaki boolean degerlerden `*` veya `-` karakterleriyle olusur.

Olusan dosyalar:

```text
KASIYER.DAT
YETKI.DAT
MESAJ.001
MESAJ.002
...
```

Dosyalarin hedefleri:

- `KASIYER.DAT`, `YETKI.DAT` -> `PosGenelFolderPath`
- `MESAJ.xxx` -> `PoskonFolderPath`

Basarili mesaj:

```text
KASIYER.DAT ve YETKI.DAT dosyalari olusturuldu.
```

Hata durumlari:

- Aktif kasiyer yoksa job `Failed` olur.
- Lokal klasore yazilamazsa job `Failed` olur.
- Network kopyalama sirasinda hata olursa job `Failed` olur.

Not:

Kasiyer dosyasi tum aktif kasiyerleri okur. Depo bazli kasiyer filtresi kodda yoktur; depo bilgisi dosyanin nereye yazilacagini ve kasa mesajlarini belirler.

## Promosyon Dosyalari

Endpoint:

```http
GET /api/operations/promofile
```

Job tipi:

```text
PromoFile
```

Ne yapar:

1. Cozulmus islem deposuna gore branch kaydi opsiyonel okunur.
2. Depoya ait kasalar `CashRegistryDetails` tablosundan okunur.
3. Mikro'dan promosyon disi PLU listeleri okunur.
4. Mikro'dan grup/ozel kod urun listesi okunur.
5. Mayday veritabanindan aktif promosyonlar okunur.
6. Uyum veritabanindan GIB vergi numaralari okunmaya calisilir.
7. Promosyon ve yardimci dosyalar olusturulur.
8. Her kasa icin `MESAJ.xxx` dosyasi olusturulur.
9. Branch network path bilgileri varsa dosyalar sube paylasimlarina kopyalanir.

Olusan dosyalar:

```text
PROMO.DAT
NOPROMO.DAT
NOCEK.DAT
NOYEMEK.DAT
GRUP.DAT
OZELKOD.DAT
EFATVNO.DAT
MESAJ.001
MESAJ.002
...
```

Dosyalarin hedefleri:

- `PROMO.DAT`, `NOPROMO.DAT`, `NOCEK.DAT`, `NOYEMEK.DAT`, `GRUP.DAT`, `OZELKOD.DAT`, `EFATVNO.DAT` -> `PosGenelFolderPath`
- `MESAJ.xxx` -> `PoskonFolderPath`

### PROMO.DAT

Kaynak:

- Connection string: `MaydayConnection`, `MaydayMarketConnection` veya `MaydaYMarketConnection`
- Tablo: `PROMOSYON_TANIMLARI`
- Sube eslesmesi varsa tablo: `PROMOSYON_SUBELER`

Sube filtresi:

- `PROMOSYON_SUBELER` tablosunda kayit varsa sadece cozulmus islem deposuyla eslesen promosyon kodlari alinir.
- `PROMOSYON_SUBELER` bos ise promosyonlar sube filtresi olmadan degerlendirilir.

Promosyon filtresi:

- Promosyon kodu bos olmayacak.
- Bitis tarihi gecmis olmayacak.
- Pasif alan okunabiliyorsa pasif degeri 0 olacak.

Desteklenen promosyon tipleri:

- `P1`
- `P2`
- `P3`
- `P8`
- `P9`
- `PM`

Kod, Mayday kolon isimlerinde farkli varyasyonlari okuyabilecek sekilde yazilmistir. Ornegin promosyon kodu icin `PromotionCode`, `PromosyonKodu`, `PROMOSYON_KODU`, `ProKod` gibi kolon adlari desteklenir.

### NOPROMO.DAT, NOCEK.DAT, NOYEMEK.DAT

Kaynak:

- Mikro `STOKLAR`

Filtre:

- `sto_perakende_vergi = 4`

Bu filtreye giren urunlerin PLU numaralari 6 haneli formatta dosyalara yazilir.

Ayni PLU listesi uc dosyaya da yazilir:

- `NOPROMO.DAT`
- `NOCEK.DAT`
- `NOYEMEK.DAT`

### GRUP.DAT ve OZELKOD.DAT

Kaynak:

- Mikro `STOKLAR`

Filtre:

- `sto_mkod_artik` bos olmayacak.
- `sto_mkod_artik` `0` olmayacak.

Dosya satirinda:

- PLU no
- Kalite/ozel kod degeri

Ayni veri iki dosyaya da yazilir:

- `GRUP.DAT`
- `OZELKOD.DAT`

### EFATVNO.DAT

Kaynak:

- Connection string: `UyumConnection`, `UYUMConnection` veya `UyumDbConnection`
- Tablo: `dbo.CarilerGib`

Okunan kolon varyasyonlari:

- `VergiNumarasi`
- `TaxNumber`
- `TaxNo`
- `VergiNo`
- `vkn`

UYUM connection string yoksa veya okuma sirasinda SQL hatasi olursa job fail edilmez. Sistem warning log yazar ve bos `EFATVNO.DAT` uretmeye devam eder.

Basarili mesaj:

```text
PROMO.DAT ve yardimci promosyon dosyalari olusturuldu.
```

Hata durumlari:

- Mayday connection string yoksa job `Failed` olur.
- Mayday tablolarina erisilemezse job `Failed` olur.
- `PROMOSYON_SUBELER` kayitlari var ama sube/promosyon kodu kolonlari map edilemezse job `Failed` olur.
- Lokal klasore yazilamazsa job `Failed` olur.
- Network kopyalama sirasinda hata olursa job `Failed` olur.

Not:

Promosyon bulunmasa bile `PROMO.DAT` bos olusabilir. Kodda promosyon sayisi sifir diye job fail edilmiyor.

## Authorization File Ekrani

Bu kisim dosya olusturma job'i degildir. Yetki tablosunu okumak ve guncellemek icindir.

Endpointler:

```http
GET /api/operations/getauthorizationfile
GET /api/operations/authorization-files
POST /api/operations/saveauthorizationfile
POST /api/operations/authorization-files
```

Listeleme yetkisi:

```text
operasyon-islemleri.operations.list
```

Guncelleme yetkisi:

```text
operasyon-islemleri.operations.update
```

GET response alanlari:

- `id`
- `updateDate`
- `name`
- `z`
- `r`
- `x`

POST body dizi olarak gonderilir:

```json
[
  {
    "id": 1,
    "name": "Ornek Yetki",
    "z": true,
    "r": false,
    "x": true
  }
]
```

POST akis:

1. Body bos ise hata doner.
2. Ayni `id` birden fazla gonderildiyse hata doner.
3. Gonderilen id'ler `AuthorizationFiles` tablosunda aranir.
4. Eksik id varsa hata doner.
5. `name`, `z`, `r`, `x` alanlari guncellenir.
6. `updateDate` server saatine gore set edilir.
7. Basarili olursa `201 Created` doner, response body yoktur.

## Urun Dagilimlari Workflow

Bu kisim dosya olusturma job'i degildir. `FrmDagilim` ekraninin yeni API karsiligidir ve kalici veri degistirir:

- Furpa `STOK_DAGILIM` kaydi olusturur/gunceller/siler.
- Bilgilendirme adiminda dagilim durumunu `1` yapar.
- Opsiyonel olarak Mikro `STOKLAR.sto_siparis_dursun = 1` isaretler.
- Kesinlestirmede Mikro `DEPOLAR_ARASI_SIPARISLER` satirlari olusturur.

Route:

```http
GET    /api/operasyon-islemleri/urun-dagilimlari/dagitim-merkezleri
POST   /api/operasyon-islemleri/urun-dagilimlari/oneri
GET    /api/operasyon-islemleri/urun-dagilimlari
GET    /api/operasyon-islemleri/urun-dagilimlari/{documentNo}
POST   /api/operasyon-islemleri/urun-dagilimlari
PUT    /api/operasyon-islemleri/urun-dagilimlari/{documentNo}
POST   /api/operasyon-islemleri/urun-dagilimlari/{documentNo}/bilgilendir
POST   /api/operasyon-islemleri/urun-dagilimlari/{documentNo}/kesinlestir
DELETE /api/operasyon-islemleri/urun-dagilimlari/{documentNo}
```

Yetkiler:

```text
operasyon-islemleri.urun-dagilimlari.list
operasyon-islemleri.urun-dagilimlari.detail
operasyon-islemleri.urun-dagilimlari.create
operasyon-islemleri.urun-dagilimlari.update
operasyon-islemleri.urun-dagilimlari.delete
```

Durumlar:

```text
0 Kaydedildi       Guncelle/sil/bilgilendir acik
1 Bilgilendirildi  Kesinlestir acik
2 Dagilim Yapildi  Yazma aksiyonlari kapali
```

Oneri hesaplama:

- Varsayilan satis periyodu 42 gundur.
- Aktif subeler Mikro `DEPOLAR` tablosundan okunur.
- Satislar `STOK_HAREKETLERI` uzerinden, `sth_tip = 1`, `sth_cins = 1`, `sth_normal_iade = 0` filtresiyle hesaplanir.
- Mevcut stok `dbo.fn_DepodakiMiktar(stok, depo, tarih)` ile doner.
- Toplam koli satis payina gore satirlara dagitilir; kalan kusurat en yuksek payli subelere verilir.
- Donen `summary.caseDifference` sifir degilse UI kaydetmeden once dagilimi duzeltmelidir.

Kesinlestirme:

- Sadece `Dagilim_Adet_Miktar > 0` satirlar Mikro siparisine donusur.
- Siparis serisi `D{subeDepoNo}` olarak uretilir.
- Cikis depo `Dagitim_Merkezi`, giris depo satirdaki `Sube_Kodu` olur.
- `ssip_aciklama = "Dagilim {documentNo}"` yazilir.
- Ayni evrak tekrar kesinlestirilirse bu aciklama, stok, cikis depo ve giris depo uzerinden mevcut siparisler bulunur; cift siparis uretilmez.

Bilgilendirme:

- API senkron SMTP gondermez.
- `Bolge_Yoneticileri` tablosundan bolge muduru/e-posta bilgilerini ve dagilim ozetini doner.
- UI veya dis entegrasyon bu response ile mail/outbox adimini baglamalidir.

## Sik Karsilasilan Sonuclar

`202 Accepted`:

- Is kuyruga alinmistir.
- Dosya henuz olusmus olmak zorunda degildir.
- UI `jobId` ile durum sorgulamaya devam etmelidir.

`Queued`:

- Is kuyrukta bekliyor.

`Running`:

- Worker isi isliyor.

`Succeeded`:

- Dosyalar lokal olarak olustu.
- `networkPath` dolu olan dosyalar network paylasimina da kopyalandi.

`Failed`:

- `errorMessage` alanindaki mesaj UI'da kullaniciya gosterilebilir.

## Onemli Teknik Notlar

- Queue uygulama icindedir ve memory uzerinde tutulur.
- Uygulama restart olursa kuyruktaki ve gecmis job kayitlari kaybolabilir.
- Dosyalar Windows-1254 encoding ile yazilir.
- Dosya uretme islemi Mikro stok/fiyat verisini degistirmez.
- Dosya uretme islemi Mayday veya Uyum verisini degistirmez.
- Sadece authorization file kaydetme endpointi `AuthorizationFiles` tablosunu gunceller.
- "Gonder" ifadesi harici bir terazi/POS API'sine canli istek atmak degildir. Sistem dosya uretir ve konfigure edilmisse ag paylasimina kopyalar.
