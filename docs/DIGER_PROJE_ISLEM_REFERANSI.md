# Diger Proje Islem Referansi

Bu dokuman sadece asagidaki entegrasyon kapsamini anlatir:

- cari, urun ve fiyat arama
- fiyat etiketi ve kunye etiketi
- depo/firma siparisleri
- depo/firma sevkleri
- depo/firma iadeleri
- depo/firma mal kabul
- kasa icmali, banknot ve kasa sayimi
- sayim sonucu, virman, zayiat ve masraf fisleri

Tam request modelleri ve tum alanlar icin [UI_API_DOKUMANI.md](UI_API_DOKUMANI.md) kaynak dokumandir. Bu dosya, baska projenin hangi endpointi ne sirayla cagiracagini ve verinin hangi sistemde yasadigini netlestirir.

## Ortak Kurallar

- Tum endpointler JSON (`camelCase`) doner ve normalde `Authorization: Bearer {token}` ister.
- Liste/detay ekraninda depo kapsaminda `warehouseNo` bos birakilabilir; backend JWT deposunu uygular. Baska depo secimi ilgili `*.all-warehouses` yetkisini gerektirir.
- `warehouseNo`, islemi yapan kullanicinin/islem ekraninin deposudur. Kaynak veya karsi depo numarasi degildir.
- `documentSerie + documentOrderNo` belge anahtaridir. Liste cevabindaki depo bilgisi detay istegine de tasinmalidir.
- Create endpointlerinde timeout veya baglanti kopmasi ihtimali olan akislarda `clientRequestId` ayni mantiksal kayit boyunca korunmalidir. Yeni id ile tekrar POST etmek duplicate belge uretebilir.
- Mevcut ayarlarda bu akislardaki yazmalar `MikroWriteRouting` ile `Database` rotasindadir; yani is kaydi `MikroWriteConnection` uzerinden dogrudan Mikro DB'ye yazilir. Ortam konfigurasyonu bunu degistirebilir.

## Veri Sahipligi

| Katman | Gorev | Temel nesneler |
|---|---|---|
| Mikro DB | Ticari/operasyonel kaynak veri ve ana belge kaydi | `STOKLAR`, `BARKOD_TANIMLARI`, `CARI_HESAPLAR`, `SIPARISLER`, `DEPOLAR_ARASI_SIPARISLER`, `STOK_HAREKETLERI`, `SAYIM_SONUCLARI` |
| Furpa DB | Uygulamaya ozel tanim, etiket belgesi, terminal/sube yardimci verisi | `LabelDocuments`, `LabelDocumentDetails`, `BranchDetails`, `CashRegistryDetails`, `CashRegisterDetails` |
| Auth DB | Kullanici/yetki ve operasyon izleme | kullanici/rol/yetki tablolar, `document_flows`, `mikro_api_write_audits`, offline retry kayitlari |

`document_flows` ve `mikro_api_write_audits` is belgesinin yerine gecmez. Belgenin gercek ticari kaynagi Mikro'daki siparis veya stok hareketidir.

## 1. Cari, Urun ve Fiyat Arama

| Amac | Endpoint | Ana response alanlari | Mikro kaynagi / mantik |
|---|---|---|---|
| Urun ara | `GET /api/arama-islemleri/urunler?stockName=&stockCode=&barcode=&companyCode=` | `stockCode`, `stockName`, `barcode`, `price`, `unitName`, `unitMultiplier`, blok/pasif alanlari, `modelCode`, `procurementType`, `sourceWarehouses` | `STOKLAR` ana karttir. Barkod `BARKOD_TANIMLARI`, depo blok/fiyat bilgisi stok-depo/fiyat kaynaklarindan, firma filtresi `SATINALMA_SARTLARI` iliskisinden gelir. |
| Fiyat gor | `GET /api/arama-islemleri/fiyat-gor?...` veya `GET /api/arama-islemleri/barkodlar/{barcode}/fiyat` | Urun ara ile ayni model; fiyat icin `price`, `priceTypeCode` | Secili islem deposunun satis fiyatini doner. `price` satis fiyatidir; mal kabul maliyeti olarak kullanilmaz. |
| Barkod cozumle | `GET /api/arama-islemleri/barkodlar/{barcode}/cozumle?operationType=&supplierCode=` | `isFound`, `stockCode`, `embeddedQuantity`, `unitMultiplier`, bloklar, `isUsableInOperation`, `operationDecision`, `errors` | Terazi/koli/alternatif barkodu normalize eder; satira ekleme karari bu response ile verilir. |
| Cari ara | `GET /api/arama-islemleri/cariler?searchText=&take=` | `customerCode`, `customerDisplayName`, `taxNumber`, adres numaralari, e-belge alanlari, `isLocked`, `isClosed`, `selectionLabel` | `CARI_HESAPLAR` ve cari adres/temsilci verisi. Iptal cari sonuc listesine alinmaz. Kilitli veya kapali cari gorunur fakat UI `isLocked` ya da `isClosed` true ise secimi engellemelidir. UI secimde sadece unvani degil `customerCode + selectionLabel` gosterir. |
| Urunden cari oner | `GET /api/arama-islemleri/urunler/{stockCode}/cari-onerileri` | `defaultSupplierCode`, `defaultSupplierName`, `suggestions[]` | Stok karti varsayilan tedarikcisi, aktif satin alma sartlari ve yakin stok hareketi gecmisi birlesir. |

Ornek urun response cekirdegi:

```json
{
  "warehouseNo": 120,
  "barcode": "8690526366654",
  "stockCode": "019700",
  "stockName": "URUN ADI",
  "price": 60.0,
  "unitName": "ADET",
  "unitMultiplier": 12,
  "modelCode": "01",
  "procurementType": "Warehouse",
  "sourceWarehouses": [{ "warehouseNo": 50, "warehouseName": "MERKEZ DEPO" }],
  "hasPurchaseRequirement": true,
  "isOrderBlocked": false,
  "isPassive": false,
  "isDelisted": false
}
```

## 2. Fiyat Etiketi ve Kunye Etiketi

| Islem | Endpoint | Response'ta kullanilacak alanlar | Veri yeri ve mantik |
|---|---|---|---|
| Fiyat degisen urunleri getir | `GET /api/kasa-islemleri/etiket-belgeleri/fiyati-degisen-urunler?date=` | stok, barkod, eski/yeni fiyat, birim ve etiket bilgileri | Urun ve fiyat Mikro'dan okunur. |
| Etiket belgesi olustur | `POST /api/kasa-islemleri/etiket-belgeleri` | `id/documentId`, belge tarihi, satirlar, etiket adetleri | Belge ve secilen satirlar Furpa DB `LabelDocuments` ve `LabelDocumentDetails` tablolarina yazilir. |
| Son/tum belgeler | `GET /api/kasa-islemleri/etiket-belgeleri/son`, `GET .../tumu`, `GET .../{documentId}` | belge id, tarih, olusturan, satir/etiket adetleri | Furpa DB etiket belge kaydi okunur. |
| Yazdirilacak etiketler | `GET /api/kasa-islemleri/etiket-belgeleri/etiketler?...` | stok adi, barkod, fiyat, birim, etiket adedi ve baski satiri | Furpa'daki belge satirlari Mikro urun/fiyat bilgisiyle birlesir. |
| Kunye etiketi | `GET /api/kasa-islemleri/kunye-etiket-yazdirma?dateToGet=...` | `stockCode`, `stockName`, `salesPrice`, uretim yeri/tarihi, alici, miktar, `takenTag` | Kunye gecmisi Furpa tarafindaki fatura/urun izleme kayitlarindan; stok ve fiyat Mikro'dan gelir. |
| Tek urunun son kunyesi | `GET /api/arama-islemleri/urunler/{stockCode}/son-kunye?warehouseNo=` | `productionCity`, `productionDistrict`, `shippingDate`, `manufacturer`, `salesPrice` | Secili stok ve depo icin en yeni sevk/kunye kaydi. |

Temel kural: fiyat etiketi belgesi uygulama kaydidir; satis fiyati ve stok karti Mikro'nun kaynagidir. Kunye bilgisi bir satis fiyati sorgusu degil, urun izlenebilirlik bilgisidir.

## 3. Depo ve Firma Siparisleri

| Islem | Endpointler | Ana response | Mikro kaynagi / mantik |
|---|---|---|---|
| Verilen depo siparisi | `GET/POST /api/siparis-islemleri/verilen-depo-siparisleri`; detay `/{documentSerie}/{documentOrderNo}` veya `/key/{documentKey}` | Create: `documentSerie`, `documentOrderNo`, `inWarehouseNo`, `outWarehouseNo`, `lineCount`, `totalQuantity`. Detay: header + `lines[]`. | `DEPOLAR_ARASI_SIPARISLER`. Subenin kaynak depoya talebidir; `outWarehouseNo` kaynak depo, `inWarehouseNo` teslim alacak depodur. |
| Alinan depo siparisi | `GET /api/siparis-islemleri/alinan-depo-siparisleri`; detay ayni anahtarla | Liste/detayda karsi depo, tarih, satir, miktar ve teslim bilgileri | Ayni `DEPOLAR_ARASI_SIPARISLER` kayitlarinin kaynak depo bakisidir. |
| Kaynak depo urunleri | `GET /api/siparis-islemleri/onerilen-depo-siparisleri/kaynak-depo-urunleri?sourceWarehouseNo=` | `stockCode`, `stockName`, `modelCode`, `barcode`, birim ve kaynak uygunlugu | Kaynak deponun model kodu tanimina uyan Mikro stok kartlarini getirir. Kaynak depo urun aramadaki `warehouseNo` alanina yazilmaz. |
| Verilen firma siparisi | `GET/POST /api/siparis-islemleri/verilen-firma-siparisleri`; detay `/.../{series}/{no}`; firma urunleri `/firma-urunleri` | Create/detayda belge anahtari, cari, teslim tarihi, satir miktar/fiyat/toplam | `SIPARISLER`; firma/cari bazli siparis satirlaridir. |
| Alinan firma siparisi | `GET /api/siparis-islemleri/alinan-firma-siparisleri`; detay `/.../{series}/{no}` | Belge, cari, satir, teslim/kalem durumu | Ayni `SIPARISLER` verisinin karsi taraf/alinan gorunumudur. |

Depo siparisi satirinda teslim takibi `ssip_miktar`, `ssip_teslim_miktar`, `ssip_kapat_fl` gibi alanlarla; firma siparisinde karsilik gelen `sip_*` teslim alanlariyla ilerler. Sevk satiri siparise baglaniyorsa `STOK_HAREKETLERI_EK` baglantisi teslim bilgisini etkiler.

## 4. Depolar Arasi Gelen/Giden Sevk

| Islem | Endpointler | Ana response | Mikro kaynagi / mantik |
|---|---|---|---|
| Giden liste | `GET /api/sevk-islemleri/depolar-arasi-sevkler/giden` | belge anahtari, kaynak/hedef depo, tarih, satir/toplam, e-irsaliye durumu | `STOK_HAREKETLERI` depolar arasi nakliye hareketleri. |
| Gelen liste | `GET /api/sevk-islemleri/depolar-arasi-sevkler/gelen` | Ayni belge modeli, kullanicinin hedef depo bakisi | Ayni stok hareketlerinin hedef depo bakisi. |
| Detay | `GET /api/sevk-islemleri/depolar-arasi-sevkler/giden/{series}/{no}` veya `/gelen/...` | `header` ve `lines[]`; satirda stok, miktar, depo, birim, fiyat, GUID ve siparis baglantisi | Ana belge anahtari stok hareketinin evrak seri/sira bilgisidir. |
| Olustur | `POST /api/sevk-islemleri/depolar-arasi-sevkler/giden` | belge seri/sira, hareket GUID'leri, kaynak/hedef depo, satir ve toplamlar | Mikro `STOK_HAREKETLERI` (+ gerekiyorsa `STOK_HAREKETLERI_EK`). Normal depolar arasi sevk nakliye evrak tipinde tutulur. |
| Guncelle | `PUT /api/sevk-islemleri/depolar-arasi-sevkler/giden/{series}/{no}` | Guncel belge/detay | Satir hedeflemede Mikro GUID'i ve guncel `sth_lastup_date` korunmalidir; eski concurrency degeriyle yazmak `409` uretebilir. |
| E-irsaliye | `POST .../giden/{series}/{no}/e-irsaliye`; PDF `GET .../e-irsaliye/pdf` | zarf/ETTN, durum, hata mesaji ve PDF | Stok hareketi Mikro'da kalir; e-belge gonderim ve takip bilgisi uygulama/Axata/Uyumsoft entegrasyon katmaninda izlenir. |

## 5. Firmadan Gelen/Giden Sevk

| Islem | Endpointler | Ana response | Mikro kaynagi / mantik |
|---|---|---|---|
| Giden firma sevki | `GET/POST /api/sevk-islemleri/firma-sevkleri/giden`; detay `/giden/{series}/{no}` | belge, cari, sevk satirlari, toplam, e-irsaliye durumu | `STOK_HAREKETLERI` ve cari baglantili ticari sevk verisi. |
| Gelen firma sevki | `GET /api/sevk-islemleri/firma-sevkleri/gelen`; detay `/gelen/{series}/{no}` | karsi cari, belge, satirlar ve durum | Firma sevkinin gelen bakisi. |
| E-irsaliye/PDF | `POST .../giden/{series}/{no}/e-irsaliye`, `GET .../giden/{series}/{no}/e-irsaliye/pdf` | e-irsaliye sonucu, ETTN/zarf ve hata bilgisi | Gonderim basarili olmadan ayni irsaliye no tekrar yollanmamalidir; once entegrasyon takip kaydi ve Uyumsoft belgesi sorgulanmalidir. |

Firma sevkinde cari kodu, adres numaralari ve e-belge uygunlugu `CARI_HESAPLAR`/cari adres verisinden gelir. Stok miktar hareketinin asil kaydi yine Mikro'daki `STOK_HAREKETLERI`dir.

## 6. Depo ve Firma Iadeleri

| Islem | Endpointler | Ana response | Mikro kaynagi / mantik |
|---|---|---|---|
| Iade edilebilir urunleri getir | `GET /api/iade-islemleri/depo-iadeleri/iade-edilebilir-urunler?targetWarehouseNo=51&search=` | `stockCode`, `stockName`, `barcode`, birim/katsayi, `modelCode`, kaynak/hedef depo | Hedef iade deposunun model kodu tanimina uyan stok kartlari. Sonuc, iade icin urun secim kataloğudur; hareket yaratmaz. |
| Giden depo iadesi | `GET/POST /api/iade-islemleri/depo-iadeleri/giden`; detay `/giden/{series}/{no}` | belge, karsi depo, satirlar, toplam ve e-irsaliye durumu | `STOK_HAREKETLERI`; depolar arasi iade hareketi olarak tutulur. |
| Gelen depo iadesi | `GET /api/iade-islemleri/depo-iadeleri/gelen`; detay `/gelen/{series}/{no}` | iadenin hedef depo bakisi | Ayni stok hareketlerinin gelen taraf gorunumu. |
| Firma iadesi | `GET/POST /api/iade-islemleri/firma-iadeleri`; detay `/{series}/{no}` | cari, belge, satir miktar/fiyat, toplam ve e-irsaliye | `STOK_HAREKETLERI` ve cari baglantisi. Iade niteligini stok hareketinin normal/iade bilgisi belirler. |
| E-irsaliye/PDF | Depo ve firma iade rootunde `POST /{series}/{no}/e-irsaliye`, `GET /{series}/{no}/e-irsaliye/pdf` | e-belge sonuc ve PDF | Iade belgesi Mikro stok hareketinden uretilir; e-belge entegrasyon sonucu ayrica izlenir. |

## 7. Mal Kabul ve Mal Kabul Farklari

| Islem | Endpointler | Ana response | Mikro/Furpa mantigi |
|---|---|---|---|
| Firma mal kabul liste/detay | `GET /api/mal-kabul-islemleri/firma-mal-kabulleri`; detay `/{series}/{no}` | cari, irsaliye/fatura no, tarih, satirlar, kabul miktar/fiyatlari | Stok girisi Mikro `STOK_HAREKETLERI`; cari/muhasebe baglantisi ilgili Mikro cari hareketleriyle kurulur. |
| Firma mal kabul olustur | `POST /api/mal-kabul-islemleri/firma-mal-kabulleri` | zorunlu `documentSerie` + `documentOrderNo`, stok hareket GUID'leri, cari, satir/toplam ve `clientRequestId` | Firma irsaliyesini stok/cari kaydina cevirir. Manuel kayitta Mikro belge no bos kalir; yalniz QR/e-belgeden gelen `officialDocumentNo`, `sth_belge_no` alanina yazilir. Fiyat yoksa satis fiyatina dusulmez; e-belge satir net fiyati veya satin alma fiyati kullanilir, yoksa `0`. |
| ETTN ile e-belge bul | `GET .../firma-mal-kabulleri/resmi-belge/ettn/{ettn}` | resmi belge header ve satirlari | Uyumsoft/e-belge kaynagini okunur, mal kabul olusturma oncesi esleme icin kullanilir. |
| Depo mal kabul detay | `GET /api/mal-kabul-islemleri/depo-mal-kabulleri/{series}/{no}` | sevk header/satirlari, beklenen-kabul edilen miktar ve fark | Kaynak depo sevki Mikro stok hareketidir. |
| Depo mal kabul onay | `POST .../depo-mal-kabulleri/{series}/{no}/kabul` | kabul sonucu, belge/satir ve fark bilgisi | Gelen depo, sevk belgesini kabul eder; takip durumu Auth `document_flows` kaydina da yansir. |
| Mal kabul farklari | `GET /api/mal-kabul-islemleri/mal-kabul-farklari/olusturdugum` ve `/kabul-ettigim` | belge, kaynak/hedef depo, urun, sevk-kabul farki, durum | Fark, yeni bir ticari kaynak degil; sevk ve kabul miktarlarinin karsilastirilmis gorunumudur. |

## 8. Kasa Icmali, Banknot ve Kasa Sayimi

| Islem | Endpointler | Ana response | Veri yeri ve mantik |
|---|---|---|---|
| Kasa icmali girisi | `POST /api/kasa-islemleri/kasa-sayimlari` | `documentSerie`, `documentOrderNo`, kasa, tarih, toplam ve hareket bilgileri | Mikro `Summaries`, `BanknoteMovements`, `GiftCheckMovements` ve `CARI_HESAP_HAREKETLERI` tarafina yazar. Kasa/terminal tanimlari Furpa `CashRegistryDetails`, `CashRegisterDetails`, `BranchDetails` kaynaklarindan gelir. |
| Kasa sayimi liste/rapor | `GET /api/kasa-islemleri/kasa-sayimlari`; `/rapor` | belge seri/sira, kasa, kasiyer, sayilan/toplam/fark tutarlari | Kasa sayim belgesi ve hareketleri Mikro tarafinda; terminal/sube yardimci verisi Furpa'da. |
| Kasa sayimi detay | `GET .../kasa-sayimlari/{series}/{no}` ve `/detaylar` | header, odeme/sayim satirlari, kasa-kasiyer ve farklar | Detay guncellemesi ayni belge anahtariyla yapilir. |
| Banknot satirlari | `GET .../{series}/{no}/banknot-hareketleri`; `PUT` ayni rota | kupur, adet, toplam | Mikro DB `BanknoteMovements`. |
| Hediye ceki satirlari | `GET/PUT .../{series}/{no}/hediye-ceki-hareketleri` | cek tipi, adet/tutar | Kasa sayim belgesine bagli hareketler. |
| Bagimsiz banknot takip | `GET/POST /api/kasa-islemleri/banknot-takipleri`; detay `/{banknoteTrackId}`; ozet `/sayim-toplami` | takip id, depo, kupur satirlari, beklenen/sayilan toplam, fark | Mikro DB `BanknoteTracks` ve `BanknoteMovements`. |

Kasa sayiminda banka odeme tipi sorgularinda kasa numarasi yerine mumkunse `cashFinanceNumber`/terminal `cashRegisterNo` kullanilmalidir.

## 9. Sayim Sonucu, Virman, Zayiat ve Masraf

| Islem | Endpointler | Ana response | Mikro kaynagi / mantik |
|---|---|---|---|
| Sayim sonucu | `GET/POST /api/stok-islemleri/sayim-sonuclari`; detay `/{documentNo}?documentDate=&warehouseNo=` | belge no/tarih/depo, satirlar, sistem miktari, sayilan miktar, fark | Ana tablo `SAYIM_SONUCLARI`; sayim sonucu stok farkini resmilestirir. Offline retry icin `GET .../offline-sync/{clientRequestId}` kullanilir. |
| Virman | `GET/POST /api/stok-islemleri/virmanlar`; detay `/{series}/{no}` | belge seri/sira, cikis/giris depo, satirlar, toplam ve hareket GUID'leri | Mikro `STOK_HAREKETLERI`; ayni evrakta cikis ve giris hareketleriyle depo/hesap aktarimi yapar. |
| Zayiat fisi | `GET/POST /api/stok-islemleri/zayiat-fisleri`; detay `/{series}/{no}` | belge, depo, stok, miktar, birim fiyat/tutar, aciklama | Mikro `STOK_HAREKETLERI`; zayiat stoktan dusen ayri bir hareket turudur. |
| Masraf fisi | `GET/POST /api/stok-islemleri/masraf-fisleri`; detay `/{series}/{no}` | belge, depo, stok, miktar, tutar, masraf/aciklama bilgisi | Mikro `STOK_HAREKETLERI`; stok/maliyet odakli masraf hareketidir. |

Bu dort akista belge basarili yazildiginda response'taki belge anahtari ve hareket GUID'leri saklanmali; ekran sonrasi listeyi veya detay endpointini yeniden okuyarak kesin durum gosterilmelidir.

## Diger Proje Icin Onerilen Akis

1. Kullanici urun arar veya barkod okutur; satira ekleme karari icin `barkodlar/{barcode}/cozumle` kullanilir.
2. Firma secimi gerekiyorsa `cariler` ile secim yapilir; urun secildiyse opsiyonel `cari-onerileri` ile aday tedarikci gosterilir.
3. Create isteginden donen belge anahtari ve `clientRequestId` saklanir.
4. Timeout/HTTP 0 durumunda yeni kayit acilmaz; ayni `clientRequestId` ile retry veya ilgili detay/liste readback yapilir.
5. E-irsaliye akisi belge yazildiktan sonra ayri calisir. Hata durumunda tekrar gondermeden once e-belge takip kaydi ve varsa ETTN/zarf sorgulanir.
6. UI, Auth takip tablosunu belge kaynagi gibi kullanmaz; kesin stok/siparis durumu icin ilgili Mikro tabanli detay endpointini yeniden cagirir.
