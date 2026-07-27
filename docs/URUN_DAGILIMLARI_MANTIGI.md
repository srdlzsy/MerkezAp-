# Urun Dagilimlari Mantigi

Bu dokuman `OperasyonIslemleri > UrunDagilimlari` modulunun is mantigini anlatir. Modul rapor ekrani degildir; merkez depodaki urunu subelere satis performansina gore paylastirmak, dagilim kaydini tutmak, bolge bilgilendirmesini isaretlemek ve kesinlestirme sonunda Mikro depo siparisi olusturmak icin kullanilir.

## Kisa Ozet

1. Kullanici stok, dagitim merkezi ve toplam koli girer.
2. API son satis hareketlerine bakarak sube bazli koli onerisi uretir.
3. UI koli dagilimini gerekirse duzenler.
4. Koli toplami kullanicinin girdigi toplam koliye esitse dagilim `STOK_DAGILIM` tablosuna kaydedilir.
5. Bolge bilgilendirme adiminda durum `Bilgilendirildi` olur ve istenirse stok kartinda siparis durdurma bayragi set edilir.
6. Kesinlestirme adiminda pozitif adetli her sube icin Mikro `DEPOLAR_ARASI_SIPARISLER` kaydi olusur.

## Temel Route ve Yetkiler

Backend route kok:

```text
/api/operasyon-islemleri/urun-dagilimlari
```

Yetki kodlari:

```text
operasyon-islemleri.urun-dagilimlari.list
operasyon-islemleri.urun-dagilimlari.detail
operasyon-islemleri.urun-dagilimlari.create
operasyon-islemleri.urun-dagilimlari.update
operasyon-islemleri.urun-dagilimlari.delete
```

Endpointler:

| Endpoint | Islev |
|---|---|
| `GET /dagitim-merkezleri` | Dagitim merkezi olarak secilebilecek depolari getirir. |
| `POST /oneri` | Satisa gore dagilim onerisi uretir. |
| `GET /` | Kayitli dagilim evraklarini listeler. |
| `GET /{documentNo}` | Dagilim evraki detayini getirir. |
| `POST /` | Yeni dagilim kaydi acar. |
| `PUT /{documentNo}` | Kaydedilmis ama bilgilendirilmemis dagilimi gunceller. |
| `POST /{documentNo}/bilgilendir` | Bolge bilgilendirme durumuna alir. |
| `POST /{documentNo}/kesinlestir` | Mikro depo siparislerini olusturur. |
| `DELETE /{documentNo}` | Sadece bilgilendirilmemis dagilimi siler. |

## Kullanilan Veri Kaynaklari

| Kaynak | Kullanim |
|---|---|
| Mikro `DEPOLAR` | Dagitim merkezi, sube, bolge ve depo adi bilgisi. |
| Mikro `STOKLAR` | Stok adi, birim, koli katsayisi ve siparis durdurma bayragi. |
| Mikro `BARKOD_TANIMLARI` | Oneri response'unda stok barkodu. |
| Mikro `STOK_HAREKETLERI` | Son donem satis miktari. |
| Furpa `STOK_DAGILIM` | Dagilim workflow ana kaydi. |
| Furpa `Bolge_Yoneticileri` | Bilgilendirme alicilari. |
| Mikro `DEPOLAR_ARASI_SIPARISLER` | Kesinlestirme sonucunda olusan depo siparisi. |

## Dagitim Merkezi Mantigi

`GET /dagitim-merkezleri` endpoint'i Mikro `DEPOLAR` tablosundan aktif depolari okur.

Secilebilir dagitim merkezleri:

- `dep_no > 0`
- `dep_iptal != true`
- `dep_envanter_harici_fl != true`
- `dep_no < 100` veya bilinen dagitim merkezleri: `50`, `53`, `56`

Sube satirlari ise oneri hesaplamasinda `dep_no > 100` olan aktif ve envanter harici olmayan depolardan gelir.

## Oneri Hesaplama

Oneri endpoint'i:

```text
POST /api/operasyon-islemleri/urun-dagilimlari/oneri
```

Giris:

```json
{
  "stockCode": "153.01.0001",
  "distributionCenterWarehouseNo": 50,
  "totalCaseQuantity": 120,
  "salesDayCount": 42,
  "referenceDate": "2026-07-24",
  "includeBranchesWithoutSales": false
}
```

Kurallar:

- `stockCode` ve `distributionCenterWarehouseNo` zorunludur.
- Hedef toplam koli icin onerilen request alani `targetCaseQuantity`dir.
- Uyumluluk icin `allocatedCaseQuantity` da hedef aliasi olarak kabul edilir; ikisi de bos ise `totalCaseQuantity` kullanilir.
- `salesDayCount` bos gelirse `42` kullanilir.
- `salesDayCount` 1 ile 365 arasina cekilir.
- `referenceDate` bos gelirse bugunun tarihi kullanilir.
- Donem baslangici: `referenceDate - salesDayCount + 1`
- Donem bitisi: `referenceDate + 1`

Satis verisi Mikro `STOK_HAREKETLERI` uzerinden okunur:

```text
sth_cikis_depo_no = sube dep_no
sth_stok_kod = stockCode
sth_tarih >= periodStart
sth_tarih < periodEndExclusive
sth_tip = 1
sth_cins = 1
sth_normal_iade = 0
```

Mevcut stok bilgisi:

```sql
dbo.fn_DepodakiMiktar(stockCode, warehouseNo, referenceDate)
```

Bu bilgi oneri response'unda `currentStockQuantity` olarak gelir; kayitli `STOK_DAGILIM` detayinda ana hesap kalemi satis/koli/adet uzerinden yurur.

## Koli Dagitim Algoritmasi

API toplam koliyi subelere satis payina gore dagitir.

1. Her subenin agirligi `lastSalesQuantity` degeridir.
2. Toplam satis sifirdan buyukse:
   - `rawCase = totalCaseQuantity * subeSatis / toplamSatis`
3. Toplam satis yoksa:
   - Dagilim esit pay mantigina doner.
4. Her satir icin once `floor(rawCase)` alinir.
5. Kalan koli varsa en buyuk ondalik farktan baslayarak satirlara 1'er koli eklenir.
6. Esitlik durumunda once daha yuksek agirlik, sonra daha kucuk depo kodu onceliklidir.

Response satirlarinda miktar ve yuzde ayrimi aciktir:

| Alan | Anlam |
|---|---|
| `regionCode` | Subenin `DEPOLAR.dep_bolge_kodu` degeri. |
| `regionName` | UI etiketi; ornek `Bolge 1`. |
| `lastSalesQuantity` | Secili donemdeki satis miktari; birimi `quantityUnitName`. |
| `currentStockQuantity` | Referans tarihteki stok miktari; birimi `quantityUnitName`. |
| `companyAverageDailySales` | Sube basina gunluk ortalama satis miktari; yuzde degildir. |
| `branchAverageDailySales` | Bu subenin gunluk ortalama satis miktari; yuzde degildir. |
| `salesSharePercent` | Subenin toplam satis icindeki payi, `0..100`. |
| `caseSharePercent` | Subeye ayrilan kolinin toplam koli icindeki payi, `0..100`. |
| `caseQuantity` | Koli miktari; birimi `caseUnitName`. |
| `unitQuantity` | Mikro siparisine gidecek miktar; birimi `quantityUnitName`. |
| `quantityUnitName` | Satis/stok/adet miktarlarinin birim etiketi. |
| `caseUnitName` | Koli alanlarinin birim etiketi. |

Response'taki `reason` alanlari:

| Reason | Anlam |
|---|---|
| `sales-share` | Koli satis payina gore verildi. |
| `equal-share` | Satis yoktu, esit pay kullanildi. |
| `rounded-to-zero` | Satis vardi ama yuvarlama sonucu koli sifir kaldi. |
| `no-period-sales` | Secili donemde satis yok. |
| `saved` | Kayitli detaydan gelen satir. |
| `no-allocation` | Kayitli detayda adet/koli yok. |


## Akilli Dengeleme

Endpoint:

```text
POST /api/operasyon-islemleri/urun-dagilimlari/dengele
```

Bu endpoint kayit yapmaz; UI gridindeki mevcut satirlari alip hedef toplam koliye gore yeni bir dagilim onerisi doner.

Temel kavramlar:

| Alan | Anlam |
|---|---|
| `targetCaseQuantity` | Kullanicinin hedefledigi toplam koli. UI'daki `Hedef` alanidir. |
| `summary.allocatedCaseQuantity` | Satirlara dagitilmis toplam koli. UI'daki `Dagitilan` alanidir. |
| `summary.caseDifference` | `targetCaseQuantity - allocatedCaseQuantity`. Negatifse fazla dagitim vardir. |
| `lines[].isLocked` | Satir kilitliyse dengeleme o satirin kolisini degistirmez. |

Ornek:

```text
Hedef: 2000
Dagitilan: 2100
Fark: -100
```

Bu durumda API fazla 100 koliyi kilitli olmayan satirlardan dusmeye calisir. Dusme sirasi dusuk satisli subelerden baslar. Hedef kullanici tarafindan 2100'e cekilecekse UI `targetCaseQuantity = 2100` gonderir; boylece fark kapanir.

Dengeleme davranisi:

- Eksik koli varsa kilitli olmayan satirlara satis payina gore eklenir.
- Fazla koli varsa kilitli olmayan ve kolisi olan satirlardan dusuk satis oncelikli dusulur.
- Kilitli satirlar degismez.
- Kilitli satirlar tek basina hedefi asarsa endpoint yine response doner ama `warnings` icinde hedefe tam inilemedigini belirtir.
- `unitQuantity` her satir icin `caseQuantity * stock.packageFactor` olarak yeniden hesaplanir.

Response `reason` degerleri:

| Reason | Anlam |
|---|---|
| `locked` | Satir kilitli oldugu icin degismedi. |
| `balanced-up` | Eksik koli bu satira eklendi. |
| `balanced-down` | Fazla koli bu satirdan dusuldu. |
| `unchanged` | Satir kilitli degil ama degismedi. |

## Kaydetme Kurallari

Kaydetme endpoint'i:

```text
POST /api/operasyon-islemleri/urun-dagilimlari
```

Guncelleme endpoint'i:

```text
PUT /api/operasyon-islemleri/urun-dagilimlari/{documentNo}
```

Kayit oncesi kontroller:

- En az bir satir zorunludur.
- Ayni sube/depo iki kez gonderilemez.
- Dagitim merkezi, dagitim satiri olarak gonderilemez.
- Satir deposu Mikro `DEPOLAR` tablosunda bulunmalidir.
- `caseQuantity` negatif olamaz.
- `unitQuantity` negatif olamaz.
- `unitQuantity` bos gelirse `caseQuantity * stock.packageFactor` hesaplanir.
- `targetCaseQuantity` doluysa satir koli toplami bu degerle birebir esit olmalidir; bos ise `allocatedCaseQuantity`, o da bos ise `totalCaseQuantity` esas alinir.

Bu nedenle UI tarafinda:

```text
summary.caseDifference == 0
```

olmadan kaydetme butonu aktif olmamalidir.

## STOK_DAGILIM Kaydi

Yeni dagilim kaydi Furpa veritabanindaki `dbo.STOK_DAGILIM` tablosuna satir satir yazilir.

Yazilan ana alanlar:

| Alan | Kaynak |
|---|---|
| `Evrak_No` | Transaction icinde uretilen dagilim evrak no. |
| `Kayit_Tarihi` | Bugunun tarihi. |
| `Stok_Kodu` | Secilen stok. |
| `Bolge` | Subenin `dep_bolge_kodu` degeri sayiya cevrilebiliyorsa. |
| `Sube_Kodu` | Dagilim satiri depo no. |
| `Toplam_Satis_42_Gun` | Oneri/kayit satirindaki satis miktari. |
| `Sirket_Ortalama_Satisi` | Sirket ortalama gunluk satis degeri. |
| `Sube_Ortalama_Satisi` | Sube ortalama gunluk satis degeri. |
| `Dagilim_Koli_Miktar` | Satir hedef/dagilim koli miktari; API request tarafinda `caseQuantity` olarak gelir. |
| `Dagilim_Adet_Miktar` | Satir adet miktari. |
| `Dagilimi_Yapan` | Kullanici veya request `distributedBy`. |
| `Durum` | Ilk kayitta `0`. |
| `Kesinlestirme_Tarihi` | Ilk kayitta bos. |
| `Dagitim_Merkezi` | Cikis/dagitim merkezi depo no. |

`Evrak_No` uretimi:

```sql
SELECT COALESCE(MAX(TRY_CONVERT(int, Evrak_No)), 0) + 1
FROM dbo.STOK_DAGILIM WITH (UPDLOCK, HOLDLOCK)
WHERE TRY_CONVERT(int, Evrak_No) IS NOT NULL;
```

Bu islem `Serializable` transaction icinde yapilir.

## Durum Akisi

| Kod | Ad | Anlam |
|---|---|---|
| `0` | `Kaydedildi` | Dagilim kaydedildi, henuz bolge bilgilendirmesi yok. |
| `1` | `Bilgilendirildi` | Bolge bilgilendirme adimi yapildi. |
| `2` | `Dagilim Yapildi` | Dagilim kesinlesti ve depo siparisleri olustu. |

UI aksiyonlari:

| Durum | Guncelle | Sil | Bilgilendir | Kesinlestir |
|---|---|---|---|---|
| `0` | Acik | Acik | Acik | Kapali |
| `1` | Kapali | Kapali | Acik | Acik |
| `2` | Kapali | Kapali | Kapali | Kapali |

Backend kurallari:

- Sadece `Durum = 0` kayitlar guncellenebilir.
- Sadece `Durum = 0` kayitlar silinebilir.
- `Durum = 2` kayit tekrar bilgilendirilemez.
- `Durum = 0` kayit kesinlestirilmek istenirse `allowFinalizeWithoutNotification = true` gerekir.

## Bilgilendirme Mantigi

Endpoint:

```text
POST /api/operasyon-islemleri/urun-dagilimlari/{documentNo}/bilgilendir
```

Body:

```json
{
  "notifyBy": "MERKEZ",
  "markStockOrderingStopped": true
}
```

Yaptiklari:

1. Dagilim detayini okur.
2. Kayit kesinlesmis degilse `STOK_DAGILIM.Durum = 1` yapar.
3. `Bolge_Yoneticileri` tablosundan bolge bazli alici bilgilerini hazirlar.
4. `markStockOrderingStopped = true` ise Mikro `STOKLAR.sto_siparis_dursun = 1` yapar.
5. Response'ta konu, mesaj ve bolge bazli recipient ozeti doner.

Onemli not:

- API su an direkt mail gondermez.
- UI veya entegrasyon katmani response'taki `recipients`, `subject`, `message` bilgisini mail/outbox icin kullanmalidir.

## Kesinlestirme Mantigi

Endpoint:

```text
POST /api/operasyon-islemleri/urun-dagilimlari/{documentNo}/kesinlestir
```

Body:

```json
{
  "finalizeBy": "MERKEZ",
  "orderDate": "2026-07-24",
  "deliveryDate": "2026-07-24",
  "allowFinalizeWithoutNotification": false
}
```

Kurallar:

- `deliveryDate`, `orderDate` tarihinden once olamaz.
- `unitQuantity > 0` olan satirlar siparise cevrilir.
- Pozitif adetli satir yoksa hata doner.
- Dagilim `Durum = 0` ise normalde kesinlestirme yapilmaz; `allowFinalizeWithoutNotification = true` gonderilirse izin verilir.

Olusan Mikro depo siparisi:

| Alan | Deger |
|---|---|
| Tablo | `DEPOLAR_ARASI_SIPARISLER` |
| Seri | `D{subeDepoNo}` |
| Sira | Ayni seri icin max sira + 1 |
| Giris depo | Dagilim satirindaki sube depo no |
| Cikis depo | Dagitim merkezi depo no |
| Stok | Dagilim stogu |
| Miktar | `unitQuantity` |
| Birim pointer | `1` |
| Fiyat | `0` |
| Aciklama | `Dagilim {documentNo}` |
| Rezervasyon miktari | `caseQuantity` |

Tekrar calistirma korumasi:

- API once ayni stok, ayni cikis depo ve ayni aciklama (`Dagilim {documentNo}`) ile daha once olusmus siparislere bakar.
- Ayni sube icin siparis bulunursa yeni kayit acmaz.
- Response'ta bu satirlar `alreadyExisted = true` olarak doner.

Kesinlestirme sonunda:

- `STOK_DAGILIM.Durum = 2`
- `STOK_DAGILIM.Kesinlestirme_Tarihi = finalizedAt.Date`
- Response'ta `createdDocumentCount`, `existingDocumentCount`, `totalUnitQuantity` ve `orders[]` doner.

## Liste ve Detay Okuma

Liste endpoint'i:

```text
GET /api/operasyon-islemleri/urun-dagilimlari
```

Filtreler:

- `status`
- `documentNo`
- `stockCode`
- `distributionCenterWarehouseNo`
- `createdFrom`
- `createdTo`
- `take`

Detay endpoint'i:

```text
GET /api/operasyon-islemleri/urun-dagilimlari/{documentNo}
```

Detay response'u:

- `header`: evrak, durum, stok, dagitim merkezi, dagilimi yapan
- `summary`: toplam koli/adet ve denge bilgisi
- `lines`: sube bazli dagilim satirlari
- `availableActions`: UI aksiyon durumlari

## Teknik Dayaniklilik Notlari

- Eski `STOK_DAGILIM` kayitlarinda sayisal alanlar `nvarchar` olarak tutulmus olabilir.
- `1,6` gibi virgullu sayilar okunurken virgulu noktaya cevirip `TRY_CONVERT` ile normalize edilir.
- Detayda Mikro stok karti artik yoksa endpoint 500 vermez; `stockName` stok kodu fallback'iyle doner.
- Kesinlestirme `MikroWriteDbContext`, dagilim kaydi ise `FurpaDbContext` uzerinden yazilir.

## UI Icin Pratik Akis

1. `GET /dagitim-merkezleri` ile dagitim merkezi dropdown'i doldur.
2. Stok sec, toplam koli gir, `POST /oneri` cagir.
3. Gridde `caseQuantity` duzenlenebilir olsun.
4. Kullanici summary/hedef toplam koli alanini degistirirse bunu `targetCaseQuantity` olarak requeste ekle.
5. Geriye uyum icin UI `allocatedCaseQuantity` gonderirse backend bunu da hedef aliasi olarak kabul eder.
6. `lines[].caseQuantity` toplami hedef toplam koliye esit degilse kaydetme.
7. Kaydetten sonra gelen `documentNo` ile detay ekranina gec.
8. `status.code = 0` iken guncelle/sil/bilgilendir acik olsun.
9. Bilgilendirden sonra kesinlestir acik olsun.
10. Kesinlestirme response'undaki `orders[]` ile olusan depo siparislerini kullaniciya goster.