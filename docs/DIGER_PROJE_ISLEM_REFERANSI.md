# Diger Proje Islem Referansi

Son guncelleme: `2026-09-11`

Bu dokuman sadece asagidaki entegrasyon kapsamini anlatir:

- cari, urun ve fiyat arama
- fiyat etiketi ve kunye etiketi
- depo/firma siparisleri
- depo/firma sevkleri
- depo/firma iadeleri
- depo/firma mal kabul
- kasa icmali, banknot ve kasa sayimi
- sayim sonucu, virman, zayiat ve masraf fisleri

Bu dosya, baska projenin hangi endpointi ne sirayla cagiracagini, verinin hangi sistemde yasadigini ve kullanacagi tam request modellerini tek yerde toplar. Daha genis API katalogu icin [UI_API_DOKUMANI.md](UI_API_DOKUMANI.md) kullanilabilir.

## Ortak Kurallar

- Tum endpointler JSON (`camelCase`) doner ve normalde `Authorization: Bearer {token}` ister.
- Liste/detay ekraninda depo kapsaminda `warehouseNo` bos birakilabilir; backend JWT deposunu uygular. Baska depo secimi ilgili `*.all-warehouses` yetkisini gerektirir.
- `warehouseNo`, islemi yapan kullanicinin/islem ekraninin deposudur. Kaynak veya karsi depo numarasi degildir.
- `documentSerie + documentOrderNo` belge anahtaridir. Liste cevabindaki depo bilgisi detay istegine de tasinmalidir.
- Route parametrelerinde canonical adlar `documentSerie` ve `documentOrderNo`'dur. Eski dokumanlardaki `{series}/{no}` ve `{seri}/{sira}` ayni degerleri anlatir.
- Create endpointlerinde timeout veya baglanti kopmasi ihtimali olan akislarda `clientRequestId` ayni mantiksal kayit boyunca korunmalidir. Yeni id ile tekrar POST etmek duplicate belge uretebilir.
- Tarih aralikli listelerde `StartDate` ve `EndDate` zorunludur. Depo yetkisi yoksa `WarehouseNo` gonderilmemeli; backend JWT deposunu kullanmalidir.
- Normal ekran route'u `{module}.{menu}.page`, tanim/yonetim ekrani `{module}.{menu}.manage` ile acilir. `list/detail/create/update/delete/print` izinleri endpoint ve buton yetkileridir.
- API ve istemci timeout'u uzun Mikro islemleri icin en az `300` saniye olmalidir. Timeout, islemin Mikro'da kesinlikle olusmadigi anlamina gelmez.
- Hatalar `application/problem+json` doner. UI kullaniciya once `detail` alanini gostermelidir.
- Mevcut `appsettings.json` ve production konfigurasyonunda routing degerleri `Database`tir. Kodda Mikro API destegi olan bir akis ancak ilgili `MikroWriteRouting` anahtari `MikroApi` yapilirsa Desktop API'yi kullanir.

## Veri Sahipligi

| Katman | Gorev | Temel nesneler |
|---|---|---|
| Mikro DB | Ticari/operasyonel kaynak veri ve ana belge kaydi | `STOKLAR`, `BARKOD_TANIMLARI`, `CARI_HESAPLAR`, `SIPARISLER`, `DEPOLAR_ARASI_SIPARISLER`, `STOK_HAREKETLERI`, `SAYIM_SONUCLARI` |
| Furpa DB | Uygulamaya ozel tanim, etiket belgesi, terminal/sube yardimci verisi | `LabelDocuments`, `LabelDocumentDetails`, `BranchDetails`, `CashRegistryDetails`, `CashRegisterDetails` |
| Auth DB | Kullanici/yetki ve operasyon izleme | kullanici/rol/yetki tablolar, `document_flows`, `mikro_api_write_audits`, offline retry kayitlari |

`document_flows` ve `mikro_api_write_audits` is belgesinin yerine gecmez. Belgenin gercek ticari kaynagi Mikro'daki siparis veya stok hareketidir.

Mikro API yazma audit durumlari:

- `Pending`: istek acildi, kesin sonuc henuz yok.
- `Succeeded`: Mikro API kesin basari cevabi verdi.
- `Failed`: kesin servis/is kurali hatasi alindi.
- `Unknown`: timeout, baglanti kopmasi veya iptal nedeniyle commit sonucu kanitlanamadi.
- `Recovered`: belge Mikro DB readback ile bulundu.

`Unknown` kayitta yeni body veya yeni `clientRequestId` ile kontrolsuz tekrar create yapilmaz.

## 1. Cari, Urun ve Fiyat Arama

| Amac | Endpoint | Ana response alanlari | Mikro kaynagi / mantik |
|---|---|---|---|
| Urun ara | `GET /api/arama-islemleri/urunler?stockName=&stockCode=&barcode=&companyCode=` | `stockCode`, `stockName`, `barcode`, `price`, `unitName`, `unitMultiplier`, blok/pasif alanlari, `modelCode`, `procurementType`, `sourceWarehouses` | `STOKLAR` ana karttir. Barkod `BARKOD_TANIMLARI`, depo blok/fiyat bilgisi stok-depo/fiyat kaynaklarindan, firma filtresi `SATINALMA_SARTLARI` iliskisinden gelir. |
| Fiyat gor | `GET /api/arama-islemleri/fiyat-gor?...` veya `GET /api/arama-islemleri/barkodlar/{barcode}/fiyat` | Urun ara ile ayni model; fiyat icin `price`, `priceTypeCode` | Secili islem deposunun satis fiyatini doner. `price` satis fiyatidir; mal kabul maliyeti olarak kullanilmaz. |
| Barkod cozumle | `GET /api/arama-islemleri/barkodlar/{barcode}/cozumle?operationType=&supplierCode=` | `isFound`, `stockCode`, `embeddedQuantity`, `unitMultiplier`, bloklar, `isUsableInOperation`, `operationDecision`, `errors` | Terazi/koli/alternatif barkodu normalize eder; satira ekleme karari bu response ile verilir. |
| Cari ara | `GET /api/arama-islemleri/cariler?searchText=&take=` | `customerCode`, `customerDisplayName`, `taxNumber`, adres numaralari, e-belge alanlari, `isLocked`, `isClosed`, `selectionLabel` | `CARI_HESAPLAR` ve cari adres/temsilci verisi. Iptal cari sonuc listesine alinmaz. Kilitli veya kapali cari gorunur fakat UI `isLocked` ya da `isClosed` true ise secimi engellemelidir. UI secimde sadece unvani degil `customerCode + selectionLabel` gosterir. |
| Urunden cari oner | `GET /api/arama-islemleri/urunler/{stockCode}/cari-onerileri` | `defaultSupplierCode`, `defaultSupplierName`, `suggestions[]` | Stok karti varsayilan tedarikcisi, aktif satin alma sartlari ve yakin stok hareketi gecmisi birlesir. |
| Barkoddan cari bul | `GET /api/arama-islemleri/cari-bul?barcode=` veya `GET /api/arama-islemleri/barkodlar/{barcode}/cariler` | urun eslesmesi, varsayilan tedarikci ve `suggestions[]` | Barkodu stoga cozer, ardindan cari onerilerini getirir. |
| Var/yok | `GET /api/arama-islemleri/var-yok?...` | urun, depo, `currentStockQuantity`, `hasStock`, fiyat ve blok alanlari | Secili depodaki anlik Mikro stok miktarini doner. |
| Depo ara | `GET /api/arama-islemleri/depolar?...` | depo no, ad, adres ve depo karti alanlari | Genel depo aramasidir. |
| Kaynak depo ara | `GET /api/arama-islemleri/depolar/kaynaklar?...` | `sourceWarehouseNo`, `displayName`, `modelCodes`, `modelNames` | Siparis verilebilir kaynak depolari doner. |

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
| Fiyat degisen urunleri getir | `GET /api/kasa-islemleri/etiket-belgeleri/fiyati-degisen-urunler?dateTimeFilter=dd.MM.yyyy%20HH:mm:ss` | stok, barkod, eski/yeni fiyat, birim ve etiket bilgileri | Urun ve fiyat Mikro'dan okunur. |
| Etiket belgesi olustur | `POST /api/kasa-islemleri/etiket-belgeleri` | `id/documentId`, belge tarihi, satirlar, etiket adetleri | Belge ve secilen satirlar Furpa DB `LabelDocuments` ve `LabelDocumentDetails` tablolarina yazilir. |
| Son/tum belgeler | `GET /api/kasa-islemleri/etiket-belgeleri/son`, `GET .../tumu`, `GET .../{documentId}` | belge id, tarih, olusturan, satir/etiket adetleri | Furpa DB etiket belge kaydi okunur. |
| Yazdirilacak etiketler | `GET /api/kasa-islemleri/etiket-belgeleri/etiketler?...` | stok adi, barkod, fiyat, birim, etiket adedi ve baski satiri | Furpa'daki belge satirlari Mikro urun/fiyat bilgisiyle birlesir. |
| Kunye etiketi | `GET /api/kasa-islemleri/kunye-etiket-yazdirma?dateToGet=...` | `stockCode`, `stockName`, `salesPrice`, uretim yeri/tarihi, alici, miktar, `takenTag` | Kunye gecmisi Furpa tarafindaki fatura/urun izleme kayitlarindan; stok ve fiyat Mikro'dan gelir. |
| Manav detayli kunye etiketi | `GET /api/kasa-islemleri/manav-kunye-etiket-yazdirma/detayli-etiketler?warehouseNo=&dateToGet=` | detayli manav kunye satirlari | Anonim legacy kullanimina aciktir; `warehouseNo` zorunludur. |
| Tek urunun son kunyesi | `GET /api/arama-islemleri/urunler/{stockCode}/son-kunye?warehouseNo=` | `productionCity`, `productionDistrict`, `shippingDate`, `manufacturer`, `salesPrice` | Secili stok ve depo icin en yeni sevk/kunye kaydi. |

Temel kural: fiyat etiketi belgesi uygulama kaydidir; satis fiyati ve stok karti Mikro'nun kaynagidir. Kunye bilgisi bir satis fiyati sorgusu degil, urun izlenebilirlik bilgisidir.

## 3. Depo ve Firma Siparisleri

| Islem | Endpointler | Ana response | Mikro kaynagi / mantik |
|---|---|---|---|
| Verilen depo siparisi | `GET/POST /api/siparis-islemleri/verilen-depo-siparisleri`; detay `/{documentSerie}/{documentOrderNo}` veya `/key/{documentKey}` | Create: `documentSerie`, `documentOrderNo`, `inWarehouseNo`, `outWarehouseNo`, `lineCount`, `totalQuantity`. Detay: header + `lines[]`. | `DEPOLAR_ARASI_SIPARISLER`. Subenin kaynak depoya talebidir; `outWarehouseNo` kaynak depo, `inWarehouseNo` teslim alacak depodur. |
| Alinan depo siparisi | `GET /api/siparis-islemleri/alinan-depo-siparisleri`; detay ayni anahtarla | Liste/detayda karsi depo, tarih, satir, miktar ve teslim bilgileri | Ayni `DEPOLAR_ARASI_SIPARISLER` kayitlarinin kaynak depo bakisidir. |
| Kaynak depo urunleri | `GET /api/siparis-islemleri/onerilen-depo-siparisleri/kaynak-depo-urunleri?sourceWarehouseNo=` | `stockCode`, `stockName`, `modelCode`, `barcode`, birim ve kaynak uygunlugu | Kaynak deponun model kodu tanimina uyan Mikro stok kartlarini getirir. Kaynak depo urun aramadaki `warehouseNo` alanina yazilmaz. |
| Verilen firma siparisi | `GET/POST /api/siparis-islemleri/verilen-firma-siparisleri`; detay `/.../{documentSerie}/{documentOrderNo}`; firma urunleri `/firma-urunleri` | Create/detayda belge anahtari, cari, teslim tarihi, satir miktar/fiyat/toplam | `SIPARISLER`; firma/cari bazli siparis satirlaridir. |
| Alinan firma siparisi | `GET /api/siparis-islemleri/alinan-firma-siparisleri`; detay `/.../{documentSerie}/{documentOrderNo}` | Belge, cari, satir, teslim/kalem durumu | Ayni `SIPARISLER` verisinin karsi taraf/alinan gorunumudur. |

Depo siparisi satirinda teslim takibi `ssip_miktar`, `ssip_teslim_miktar`, `ssip_kapat_fl` gibi alanlarla; firma siparisinde karsilik gelen `sip_*` teslim alanlariyla ilerler. Sevk satiri siparise baglaniyorsa `STOK_HAREKETLERI_EK` baglantisi teslim bilgisini etkiler.

## 4. Depolar Arasi Gelen/Giden Sevk

| Islem | Endpointler | Ana response | Mikro kaynagi / mantik |
|---|---|---|---|
| Giden liste | `GET /api/sevk-islemleri/depolar-arasi-sevkler/giden` | belge anahtari, kaynak/hedef depo, tarih, satir/toplam, e-irsaliye durumu | `STOK_HAREKETLERI` depolar arasi nakliye hareketleri. |
| Gelen liste | `GET /api/sevk-islemleri/depolar-arasi-sevkler/gelen` | Ayni belge modeli, kullanicinin hedef depo bakisi | Ayni stok hareketlerinin hedef depo bakisi. |
| Detay | `GET /api/sevk-islemleri/depolar-arasi-sevkler/giden/{documentSerie}/{documentOrderNo}` veya `/gelen/...` | `header` ve `lines[]`; satirda stok, miktar, depo, birim, fiyat, GUID ve siparis baglantisi | Ana belge anahtari stok hareketinin evrak seri/sira bilgisidir. |
| Olustur | `POST /api/sevk-islemleri/depolar-arasi-sevkler/giden` | belge seri/sira, hareket GUID'leri, kaynak/hedef depo, satir ve toplamlar | Mikro `STOK_HAREKETLERI` (+ gerekiyorsa `STOK_HAREKETLERI_EK`). Normal depolar arasi sevk nakliye evrak tipinde tutulur. |
| Guncelle | `PUT /api/sevk-islemleri/depolar-arasi-sevkler/giden/{documentSerie}/{documentOrderNo}` | Guncel belge/detay | Satir hedeflemede Mikro GUID'i ve guncel `sth_lastup_date` korunmalidir; eski concurrency degeriyle yazmak `409` uretebilir. |
| E-irsaliye | `POST .../giden/{documentSerie}/{documentOrderNo}/e-irsaliye`; PDF `GET .../e-irsaliye/pdf` | zarf/ETTN, durum, hata mesaji ve PDF | Stok hareketi Mikro'da kalir; e-belge gonderim ve takip bilgisi uygulama/Axata/Uyumsoft entegrasyon katmaninda izlenir. |

## 5. Firmadan Gelen/Giden Sevk

| Islem | Endpointler | Ana response | Mikro kaynagi / mantik |
|---|---|---|---|
| Giden firma sevki | `GET/POST /api/sevk-islemleri/firma-sevkleri/giden`; detay `/giden/{documentSerie}/{documentOrderNo}` | belge, cari, sevk satirlari, toplam, e-irsaliye durumu | `STOK_HAREKETLERI` ve cari baglantili ticari sevk verisi. |
| Gelen firma sevki | `GET /api/sevk-islemleri/firma-sevkleri/gelen`; detay `/gelen/{documentSerie}/{documentOrderNo}` | karsi cari, belge, satirlar ve durum | Firma sevkinin gelen bakisi. |
| E-irsaliye/PDF | `POST .../giden/{documentSerie}/{documentOrderNo}/e-irsaliye`, `GET .../giden/{documentSerie}/{documentOrderNo}/e-irsaliye/pdf` | e-irsaliye sonucu, ETTN/zarf ve hata bilgisi | Gonderim basarili olmadan ayni irsaliye no tekrar yollanmamalidir; once entegrasyon takip kaydi ve Uyumsoft belgesi sorgulanmalidir. |

Firma sevkinde cari kodu, adres numaralari ve e-belge uygunlugu `CARI_HESAPLAR`/cari adres verisinden gelir. Stok miktar hareketinin asil kaydi yine Mikro'daki `STOK_HAREKETLERI`dir.

## 6. Depo ve Firma Iadeleri

| Islem | Endpointler | Ana response | Mikro kaynagi / mantik |
|---|---|---|---|
| Iade edilebilir urunleri getir | `GET /api/iade-islemleri/depo-iadeleri/iade-edilebilir-urunler?targetWarehouseNo=51&search=` | `stockCode`, `stockName`, `barcode`, birim/katsayi, `modelCode`, kaynak/hedef depo | Hedef iade deposunun model kodu tanimina uyan stok kartlari. Sonuc, iade icin urun secim katalogudur; hareket yaratmaz. |
| Giden depo iadesi | `GET/POST /api/iade-islemleri/depo-iadeleri/giden`; detay `/giden/{documentSerie}/{documentOrderNo}` | belge, karsi depo, satirlar, toplam ve e-irsaliye durumu | `STOK_HAREKETLERI`; depolar arasi iade hareketi olarak tutulur. |
| Gelen depo iadesi | `GET /api/iade-islemleri/depo-iadeleri/gelen`; detay `/gelen/{documentSerie}/{documentOrderNo}` | iadenin hedef depo bakisi | Ayni stok hareketlerinin gelen taraf gorunumu. |
| Firma iadesi | `GET/POST /api/iade-islemleri/firma-iadeleri`; detay `/{documentSerie}/{documentOrderNo}` | cari, belge, satir miktar/fiyat, toplam ve e-irsaliye | `STOK_HAREKETLERI` ve cari baglantisi. Iade niteligini stok hareketinin normal/iade bilgisi belirler. |
| E-irsaliye/PDF | Depo ve firma iade rootunde `POST /{documentSerie}/{documentOrderNo}/e-irsaliye`, `GET /{documentSerie}/{documentOrderNo}/e-irsaliye/pdf` | e-belge sonuc ve PDF | Iade belgesi Mikro stok hareketinden uretilir; e-belge entegrasyon sonucu ayrica izlenir. |

## 7. Mal Kabul ve Mal Kabul Farklari

| Islem | Endpointler | Ana response | Mikro/Furpa mantigi |
|---|---|---|---|
| Firma mal kabul liste/detay | `GET /api/mal-kabul-islemleri/firma-mal-kabulleri`; detay `/{documentSerie}/{documentOrderNo}` | cari, irsaliye/fatura no, tarih, satirlar, kabul miktar/fiyatlari | Stok girisi Mikro `STOK_HAREKETLERI`; cari/muhasebe baglantisi ilgili Mikro cari hareketleriyle kurulur. |
| Firma mal kabul olustur | `POST /api/mal-kabul-islemleri/firma-mal-kabulleri` | zorunlu `documentSerie` + `documentOrderNo`, stok hareket GUID'leri, cari, satir/toplam ve `clientRequestId` | Firma irsaliyesini stok/cari kaydina cevirir. Manuel kayitta Mikro belge no bos kalir; yalniz QR/e-belgeden gelen `officialDocumentNo`, `sth_belge_no` alanina yazilir. Fiyat yoksa satis fiyatina dusulmez; e-belge satir net fiyati veya satin alma fiyati kullanilir, yoksa `0`. |
| ETTN ile e-belge bul | `GET .../firma-mal-kabulleri/resmi-belge/ettn/{ettn}` | resmi belge header ve satirlari | Uyumsoft/e-belge kaynagini okunur, mal kabul olusturma oncesi esleme icin kullanilir. |
| Depo mal kabul detay | `GET /api/mal-kabul-islemleri/depo-mal-kabulleri/{documentSerie}/{documentOrderNo}` | sevk header/satirlari, beklenen-kabul edilen miktar ve fark | Kaynak depo sevki Mikro stok hareketidir. |
| Depo mal kabul onay | `POST .../depo-mal-kabulleri/{documentSerie}/{documentOrderNo}/kabul` | kabul sonucu, belge/satir ve fark bilgisi | Gelen depo, sevk belgesini kabul eder; takip durumu Auth `document_flows` kaydina da yansir. |
| Mal kabul farklari | `GET /api/mal-kabul-islemleri/mal-kabul-farklari/olusturdugum` ve `/kabul-ettigim` | belge, kaynak/hedef depo, urun, sevk-kabul farki, durum | Fark, yeni bir ticari kaynak degil; sevk ve kabul miktarlarinin karsilastirilmis gorunumudur. |

## 8. Kasa Icmali, Banknot ve Kasa Sayimi

| Islem | Endpointler | Ana response | Veri yeri ve mantik |
|---|---|---|---|
| Kasa icmali girisi | `POST /api/kasa-islemleri/kasa-sayimlari` | `documentSerie`, `documentOrderNo`, kasa, tarih, toplam ve hareket bilgileri | Mikro `Summaries`, `BanknoteMovements`, `GiftCheckMovements` ve `CARI_HESAP_HAREKETLERI` tarafina yazar. Kasa/terminal tanimlari Furpa `CashRegistryDetails`, `CashRegisterDetails`, `BranchDetails` kaynaklarindan gelir. |
| Kasa sayimi liste/rapor | `GET /api/kasa-islemleri/kasa-sayimlari`; `/rapor` | belge seri/sira, kasa, kasiyer, sayilan/toplam/fark tutarlari | Kasa sayim belgesi ve hareketleri Mikro tarafinda; terminal/sube yardimci verisi Furpa'da. |
| Kasa sayimi detay | `GET .../kasa-sayimlari/{documentSerie}/{documentOrderNo}` ve `/detaylar` | header, odeme/sayim satirlari, kasa-kasiyer ve farklar | Detay guncellemesi ayni belge anahtariyla yapilir. |
| Banknot satirlari | `GET .../{documentSerie}/{documentOrderNo}/banknot-hareketleri`; `PUT` ayni rota | kupur, adet, toplam | Mikro DB `BanknoteMovements`. |
| Hediye ceki satirlari | `GET/PUT .../{documentSerie}/{documentOrderNo}/hediye-ceki-hareketleri` | cek tipi, adet/tutar | Kasa sayim belgesine bagli hareketler. |
| Online odeme tipleri | `GET /api/kasa-islemleri/kasa-sayimlari/odeme-tipleri/online` | `paymentName`, `paymentTypeNo`, `paymentGenus`, `accountCode`, `terminalId`, `slipNumber`, `amountValue` | Mikro `PaymentTypes`; `PaymentGenus=5` veya adinda `online` gecen kayitlar gelir. Canli temel kayitlar: Online Odeme `10/1/0021`, Trendyol `600/5/0013`, Yemek Sepeti `601/5/0014`. |
| Bagimsiz banknot takip | `GET/POST /api/kasa-islemleri/banknot-takipleri`; detay `/{banknoteTrackId}`; ozet `/sayim-toplami` | takip id, depo, kupur satirlari, beklenen/sayilan toplam, fark | Mikro DB `BanknoteTracks` ve `BanknoteMovements`. |

Kasa sayiminda banka odeme tipi sorgularinda kasa numarasi yerine mumkunse `cashFinanceNumber`/terminal `cashRegisterNo` kullanilmalidir.

## 9. Sayim Sonucu, Virman, Zayiat ve Masraf

| Islem | Endpointler | Ana response | Mikro kaynagi / mantik |
|---|---|---|---|
| Sayim sonucu | `GET/POST /api/stok-islemleri/sayim-sonuclari`; detay `/{documentNo}?documentDate=&warehouseNo=` | belge no/tarih/depo, satirlar, sistem miktari, sayilan miktar, fark | Ana tablo `SAYIM_SONUCLARI`; sayim sonucu stok farkini resmilestirir. Offline retry icin `GET .../offline-sync/{clientRequestId}` kullanilir. |
| Virman | `GET/POST /api/stok-islemleri/virmanlar`; detay `/{documentSerie}/{documentOrderNo}` | belge seri/sira, cikis/giris depo, satirlar, toplam ve hareket GUID'leri | Mikro `STOK_HAREKETLERI`; ayni evrakta cikis ve giris hareketleriyle depo/hesap aktarimi yapar. |
| Zayiat fisi | `GET/POST /api/stok-islemleri/zayiat-fisleri`; detay `/{documentSerie}/{documentOrderNo}` | belge, depo, stok, miktar, birim fiyat/tutar, aciklama | Mikro `STOK_HAREKETLERI`; zayiat stoktan dusen ayri bir hareket turudur. |
| Masraf fisi | `GET/POST /api/stok-islemleri/masraf-fisleri`; detay `/{documentSerie}/{documentOrderNo}` | belge, depo, stok, miktar, tutar, masraf/aciklama bilgisi | Mikro `STOK_HAREKETLERI`; stok/maliyet odakli masraf hareketidir. |

Bu dort akista belge basarili yazildiginda response'taki belge anahtari ve hareket GUID'leri saklanmali; ekran sonrasi listeyi veya detay endpointini yeniden okuyarak kesin durum gosterilmelidir.

## Tam Request Modelleri

Asagidaki modeller JSON tarafindaki `camelCase` alan adlariyla verilmistir. `?` opsiyonel alani, `[]` dizi alanini gosterir. Tarihler ISO 8601, `Guid` alanlari standart GUID metnidir. `lines` icin `min 1` yaziyorsa bos dizi kabul edilmez.

### Ortak Path ve Liste Query Modelleri

```ts
type DocumentPath = {
  documentSerie: string;
  documentOrderNo: number; // int
};

type DocumentDetailQuery = {
  warehouseNo?: number; // > 0
};

type WarehouseDateRangeQuery = {
  warehouseNo?: number; // > 0
  startDate: string;     // ISO 8601, zorunlu
  endDate: string;       // ISO 8601, zorunlu
};
```

`WarehouseDateRangeQuery`; depo/firma siparisi, depo/firma sevki, depo/firma iadesi, virman, zayiat ve masraf liste endpointlerinin ortak modelidir. Verilen firma siparisi listesinde buna ek olarak `customerCode?: string (max 25)` ve `onlyOpen?: boolean` vardir.

### Arama Query Modelleri

```ts
type ProductSearchQuery = {
  warehouseNo?: number;       // > 0
  barcode?: string;
  stockCode?: string;
  stockName?: string;
  supplierCode?: string;
  companyCode?: string;       // supplierCode alias'i
  includeDelisted?: boolean;  // default true
  take?: number;              // 1..150, default 150
};

type ProductAvailabilityQuery = {
  warehouseNo?: number;
  barcode?: string;
  stockCode?: string;
  stockName?: string;
  includeDelisted?: boolean;  // default true
  take?: number;              // 1..100, default 20
};

type BarcodeResolutionQuery = {
  warehouseNo?: number;
  operationType?: string;     // max 64
  targetWarehouseNo?: number;
  supplierCode?: string;      // max 25
  companyCode?: string;       // max 25, alias
  isRefund?: boolean;
  screenCode?: string;        // max 64
};

type CustomerSearchQuery = {
  searchText: string;         // zorunlu, min 2
  take?: number;              // 1..100, default 20
};

type BarcodeCustomerLookupQuery = {
  barcode: string;            // zorunlu, max 128; path alias'ta path'ten gelir
  warehouseNo?: number;
  take?: number;              // 1..25, default 10
};

type ProductCustomerSuggestionQuery = {
  warehouseNo?: number;
  take?: number;              // 1..25, default 10
};

type WarehouseSearchQuery = {
  searchText?: string;
  warehouseNo?: number;       // > 0
  take?: number;              // 1..200, default 100
};

type SourceWarehouseSearchQuery = {
  searchText?: string;
  take?: number;              // 1..200, default 100
};
```

`fiyat-gor` normal arama route'u `ProductSearchQuery` kullanir. Barkod path alias'i yalniz `warehouseNo?`, `includeDelisted?` ve `take? (1..100, default 20)` alir. `son-kunye` yalniz `warehouseNo?` alir.

### Etiket ve Kunye Modelleri

```ts
type LabelTagListQuery = {
  warehouseNo?: number;
  dateToGet: string;          // zorunlu
};

type LabelPriceChangedProductListQuery = {
  warehouseNo?: number;
  dateTimeFilter: string;     // zorunlu, dd.MM.yyyy HH:mm:ss
};

type CreateLabelDocumentRequest = {
  warehouseNo?: number;
  lines: Array<{
    productCode: string;      // zorunlu, max 25
  }>;                         // min 1
};

type KunyeLabelTagListQuery = {
  dateToGet: string;          // zorunlu; depo JWT'den gelir
};

type ManavDetailedKunyeQuery = {
  warehouseNo: number;       // zorunlu, > 0
  dateToGet?: string;
};
```

### Depo Siparisi Modelleri

```ts
type CreateIssuedWarehouseOrderRequest = {
  inWarehouseNo?: number;     // all-warehouses yoksa bos birak
  outWarehouseNo: number;     // zorunlu kaynak depo, > 0
  orderDate?: string;
  deliveryDate?: string;
  description?: string;       // max 50
  lines: IssuedWarehouseOrderLine[]; // min 1
};

type IssuedWarehouseOrderLine = {
  stockCode: string;          // zorunlu, max 25
  quantity: number;           // > 0
  recommendedQuantity?: number; // >= 0
  unitPrice: number;          // >= 0
  unitPointer?: number;       // 1..255, default 1
  description?: string;       // max 50
  packageCode?: string;       // max 25
  projectCode?: string;       // max 25
  responsibilityCenter?: string; // max 25
  greenGrocerCase?: GreenGrocerCase;
};

type GreenGrocerCase = {
  inputQuantity: number;      // > 0
  inputMode: string;          // zorunlu, max 40
  conversionMode: string;     // zorunlu, max 60
  microUnit: string;          // zorunlu, max 20
  estimatedQuantity: number;  // > 0; line.quantity ile ayni olmali
  averageKgPerCase?: number;  // > 0
  unitsPerCase?: number;      // > 0
  averageSource: string;      // zorunlu, max 60
  averageRecordCount?: number;// >= 0
  averageCaseCount?: number;  // >= 0
  coefficientOfVariation?: number; // >= 0
  confidence: string;         // zorunlu, max 30
};

type SuggestedWarehouseOrderQuery = {
  targetWarehouseNo?: number;
  sourceWarehouseNo: number;  // zorunlu, > 0
  lookbackDays?: number;      // 1..365, default 43
  fallbackRecommendedDay?: number; // 1..365, default 7
};

type ConvertSuggestedWarehouseOrderRequest = {
  targetWarehouseNo?: number;
  sourceWarehouseNo: number;
  orderDate?: string;
  deliveryDate?: string;
  description?: string;       // max 50
  lines: Array<{
    stockCode: string;        // max 25
    quantity: number;         // > 0
    recommendedQuantity?: number; // >= 0
    unitPrice: number;        // >= 0
    unitPointer?: number;     // 1..255, default 1
    description?: string;     // max 50
    packageCode?: string;     // max 25
    projectCode?: string;     // max 25
    responsibilityCenter?: string; // max 25
  }>;                         // min 1
};

```

### Firma Siparisi Modelleri

```ts
type CreateIssuedCompanyOrderRequest = {
  warehouseNo?: number;
  customerCode: string;       // zorunlu, max 25
  orderDate?: string;
  deliveryDate: string;       // zorunlu
  description1?: string;      // max 50
  description2?: string;      // max 50
  deliverer?: string;         // max 25
  receiver?: string;          // max 25
  lines: CompanyOrderLine[];  // min 1
};

type CompanyOrderLine = {
  stockCode: string;          // zorunlu, max 25
  quantity: number;           // > 0
  recommendedQuantity?: number; // >= 0
  unitPrice: number;          // >= 0
  unitPointer?: number;       // 1..255, default 1
  description1?: string;      // max 50
  description2?: string;      // max 50
  packageCode?: string;       // max 25
  projectCode?: string;       // max 25
  customerResponsibilityCenter?: string; // max 25
  productResponsibilityCenter?: string;  // max 25
};

type SupplierProductsQuery = {
  warehouseNo?: number;
  customerCode: string;       // zorunlu, max 25
  search?: string;            // max 100
  take?: number;              // 1..2000, default 500
};

type SuggestedCompanyOrderQuery = {
  warehouseNo?: number;
  supplierCode: string;       // zorunlu, max 25
  lookbackDays?: number;      // 1..365, default 43
  fallbackRecommendedDay?: number; // 1..365, default 7
};
```

`POST /onerilen-firma-siparisleri/convert-to-order`, `CreateIssuedCompanyOrderRequest` ile ayni alanlari kullanir; `customerCode` yerine `supplierCode` gonderilir.

### Depolar Arasi Sevk ve Depo Iadesi Modelleri

```ts
type CreateInterWarehouseShipmentRequest = {
  clientRequestId?: string;   // Guid; retry boyunca degismez
  sourceWarehouseNo?: number; // all-warehouses yoksa bos birak
  targetWarehouseNo: number;  // > 0
  transitWarehouseNo?: number;
  movementDate?: string;
  documentDate?: string;
  documentNo?: string;        // max 50
  description?: string;       // max 50
  lines: InterWarehouseShipmentLine[]; // min 1
};

type InterWarehouseShipmentLine = {
  stockCode: string;          // zorunlu, max 25
  quantity: number;           // > 0
  warehouseOrderLineGuid?: string; // Guid, yalniz gercek siparis satiri
  unitPrice: number;          // >= 0
  unitPointer?: number;       // 1..255, default 1
  description?: string;       // max 50
  partyCode?: string;         // max 25
  lotNo?: number;             // >= 0
  projectCode?: string;       // max 25
  customerResponsibilityCenter?: string; // max 25
  productResponsibilityCenter?: string;  // max 25
};

type CreateWarehouseReturnRequest = {
  clientRequestId?: string;
  sourceWarehouseNo?: number;
  targetWarehouseNo: number;
  transitWarehouseNo?: number;
  movementDate?: string;
  documentDate?: string;
  documentNo?: string;        // max 50
  description?: string;       // max 50
  lines: WarehouseReturnLine[]; // min 1
};

type WarehouseReturnLine = Omit<InterWarehouseShipmentLine, "warehouseOrderLineGuid">;

type WarehouseReturnEligibleProductsQuery = {
  warehouseNo?: number;
  targetWarehouseNo?: number;
  search?: string;            // max 100
};
```

### Firma Sevki ve Firma Iadesi Ortak Modeli

```ts
type CreateCompanyMovementRequest = {
  clientRequestId?: string;   // Guid
  warehouseNo?: number;
  customerCode: string;       // zorunlu, max 25
  movementDate?: string;
  documentDate?: string;
  documentNo?: string;        // max 50
  description?: string;       // max 50
  deliverer?: string;         // max 25
  receiver?: string;          // max 25
  lines: CompanyMovementLine[]; // min 1
};

type CompanyMovementLine = {
  stockCode: string;          // zorunlu, max 25
  quantity: number;           // > 0
  unitPrice: number;          // >= 0
  unitPointer?: number;       // 1..255, default 1
  description?: string;       // max 50
  partyCode?: string;         // max 25
  lotNo?: number;             // >= 0
  projectCode?: string;       // max 25
  customerResponsibilityCenter?: string; // max 25
  productResponsibilityCenter?: string;  // max 25
  orderLineGuid?: string;     // Guid
};
```

### Sevk Guncelleme Modeli

```ts
type UpdateWarehouseShippingDocumentRequest = {
  movementDate?: string;
  documentDate?: string;
  documentNo?: string;        // max 50
  targetWarehouseNo?: number;
  transitWarehouseNo?: number;
  description?: string;       // max 50
  lines?: Array<{
    movementGuid?: string;    // mevcut satir Guid'i
    action?: string;          // max 20
    rowNo?: number;           // >= 0
    stockCode?: string;       // max 25
    quantity?: number;        // > 0
    unitPrice?: number;       // >= 0
    amount?: number;          // >= 0
    unitPointer?: number;     // 1..4
    description?: string;     // max 50
    partyCode?: string;       // max 25
    lotNo?: number;           // >= 0
    projectCode?: string;     // max 25
    customerResponsibilityCenter?: string; // max 25
    productResponsibilityCenter?: string;  // max 25
  }>;
};
```

### Firma Mal Kabul Modeli

```ts
type CreateCompanyReceivingRequest = {
  warehouseNo?: number;
  clientRequestId?: string;   // Guid
  customerCode: string;       // zorunlu, max 25
  movementDate?: string;
  documentDate?: string;
  documentSerie: string;      // zorunlu, max 20, yalniz ASCII harf/rakam
  documentOrderNo: number;    // zorunlu, > 0
  officialDocumentKind?: string; // max 30
  officialDocumentNo?: string;   // max 50
  officialDocumentDate?: string;
  officialDocumentEttn?: string; // max 50
  sourceDocumentKind?: string;   // max 30
  sourceDocumentNumber?: string; // max 50
  sourceDocumentDate?: string;
  despatchNumber?: string;    // max 50
  issueDate?: string;
  invoiceNumber?: string;     // max 50
  invoiceDate?: string;
  ettn?: string;              // max 50
  deliverer?: string;         // max 25
  receiver?: string;          // max 25
  description?: string;       // max 50
  allowOrderOverReceiving?: boolean; // default false
  autoCreateReturnForPartialAcceptance?: boolean; // default true
  lines: CompanyReceivingLine[]; // min 1
};

type CompanyReceivingLine = {
  stockCode: string;          // zorunlu, max 25
  quantity?: number;          // > 0
  dispatchQuantity?: number;  // > 0
  acceptedQuantity?: number;  // >= 0
  unitPrice: number;          // >= 0
  unitPointer?: number;       // 1..255, default 1
  lastConsumingDate?: string;
  orderGuid?: string;         // Guid
  description?: string;       // max 50
  partyCode?: string;         // max 25
  lotNo?: number;             // >= 0
  projectCode?: string;       // max 25
  customerResponsibilityCenter?: string; // max 25
  productResponsibilityCenter?: string;  // max 25
};
```

Firma mal kabulde `documentSerie + documentOrderNo` tum depolar genelinde tekildir. Ayni seri/sira daha once baska depoda bile kullanildiysa create `409 Conflict` doner.

### Depo Mal Kabul Modeli

```ts
type AcceptWarehouseReceivingRequest = {
  warehouseNo?: number;
  allowDiscrepancy?: boolean; // default false
  lines: Array<{
    movementGuid: string;     // sevk satiri Guid'i
    receivedQuantity: number; // >= 0
  }>;                         // min 1
};
```

### Kasa Icmali ve Kasa Sayimi Modelleri

```ts
type CreateCashSummaryRequest = {
  warehouseNo?: number;
  cashNo: number;             // > 0
  zReportNo: number;          // >= 0
  cashierNo: number;          // > 0
  managerNo: number;          // > 0
  zTotalValue: number;
  total: number;
  summaryDate: string;        // zorunlu
  giftCheckMovements?: GiftCheckMovement[];
  banknoteMovements?: BanknoteMovement[];
  paymentTypes?: PaymentTypeMovement[];
  storeExpenses?: StoreExpenseMovement[];
};

type GiftCheckMovement = {
  giftCheckType: number;      // > 0
  quantity: number;           // >= 0
  total: number;
  value: number;
};

type BanknoteMovement = {
  banknoteType: number;       // > 0
  quantity: number;           // >= 0
  total: number;
  value: number;
};

type PaymentTypeMovement = {
  paymentName: string;        // zorunlu, max 100
  paymentTypeNo: number;      // > 0
  accountCode?: string;       // max 40
  terminalId?: string;        // max 40
  slipNumber?: number;        // >= 0
  amountValue: number;
};

type StoreExpenseMovement = {
  storeExpensesType: number;  // > 0
  description?: string;       // max 250
  amountValue: number;
};

type UpdateCashSummaryDetailsRequest = {
  warehouseNo?: number;
  details: Array<{
    typeName?: string;        // max 50
    paymentTypeId: number;    // >= 0
    accountCode?: string;     // max 40
    slipNumber?: number;      // >= 0
    amount: number;
    terminalId?: string;      // max 40
    description?: string;     // max 250
  }>;                         // min 1
};

type UpdateCashSummaryBanknotesRequest = {
  warehouseNo?: number;
  banknoteMovements?: BanknoteMovement[];
};

type UpdateCashSummaryGiftChecksRequest = {
  warehouseNo?: number;
  giftCheckMovements?: GiftCheckMovement[];
};

type CashSummaryLookupQueries = {
  cashierPair: { cashierCode: number; managerCode: number }; // ikisi de > 0
  cashRegistries: { branchNo?: number };
  cashRegisterDetail: { cashNo?: number; cashRegisterNo?: string }; // max 40
  cashierSearch: { filterString: string }; // zorunlu, 1..100
  bankPaymentTypes: { cashRegisterNo: string }; // zorunlu, max 40
  zReportTotal: {
    warehouseNo?: number;
    documentSerie?: string;  // max 20
    zReportNo: number;       // >= 0
    cashNo: number;          // > 0
  };
};
```

Liste ve rapor query modeli `{ dateToGet: string, warehouseNo?: number }` seklindedir. Detay, guncelleme ve silme rotalari `documentSerie/documentOrderNo` path anahtarini; gerekiyorsa `warehouseNo` query alanini kullanir.

### Bagimsiz Banknot Takip Modeli

```ts
type CreateBanknoteTrackRequest = {
  warehouseNo?: number;
  banknoteTrackDate: string;  // zorunlu
  totalAmount: number;        // >= 0
  deliveryTotalAmount: number;// >= 0
  deliverer?: string;         // max 100
  receiver?: string;          // max 100
};
```

Liste ve sayim toplami query modeli `{ dateToGet: string, warehouseNo?: number }`; detay path'i `{banknoteTrackId: guid}` seklindedir.

### Sayim Sonucu Modeli

```ts
type CreateInventoryCountRequest = {
  warehouseNo?: number;
  clientRequestId?: string;   // Guid
  name?: string;              // max 25
  documentDate?: string;
  lines: Array<{
    stockCode: string;        // zorunlu, max 25
    quantity: number;         // >= 0
    barcode?: string;         // max 50
    unitPointer?: number;     // 1..255, default 1
  }>;                         // min 1
};
```

Detay: `GET /api/stok-islemleri/sayim-sonuclari/{documentNo}?documentDate={iso}&warehouseNo={no}`. Offline durum: `GET /api/stok-islemleri/sayim-sonuclari/offline-sync/{clientRequestId}`.

### Virman Modeli

```ts
type CreateVirmanRequest = {
  clientRequestId?: string;
  warehouseNo?: number;
  movementDate?: string;
  documentDate?: string;
  documentNo?: string;        // max 50
  description?: string;       // max 50
  lines: Array<{
    stockCode: string;        // zorunlu, max 25
    movementType: number;     // 0..255
    quantity: number;         // > 0
    unitPointer?: number;     // 1..255, default 1
    description?: string;     // max 50
    partyCode?: string;       // max 25
    lotNo?: number;           // >= 0
    projectCode?: string;     // max 25
  }>;                         // min 1
};
```

### Zayiat ve Masraf Fisi Ortak Modeli

```ts
type CreateStockReceiptRequest = {
  clientRequestId?: string;
  warehouseNo?: number;
  creator: string;            // zorunlu, max 25
  acceptor: string;           // zorunlu, max 25
  movementDate?: string;
  documentDate?: string;
  documentNo?: string;        // max 50
  description?: string;       // max 50
  lines: Array<{
    stockCode: string;        // zorunlu, max 25
    quantity: number;         // > 0
    unitPointer?: number;     // 1..255, default 1
    description?: string;     // max 50
    partyCode?: string;       // max 25
    lotNo?: number;           // >= 0
    projectCode?: string;     // max 25
  }>;                         // min 1
};
```

## Diger Proje Icin Onerilen Akis

1. Kullanici urun arar veya barkod okutur; satira ekleme karari icin `barkodlar/{barcode}/cozumle` kullanilir.
2. Firma secimi gerekiyorsa `cariler` ile secim yapilir; urun secildiyse opsiyonel `cari-onerileri` ile aday tedarikci gosterilir.
3. Create isteginden donen belge anahtari ve `clientRequestId` saklanir.
4. Timeout/HTTP 0 durumunda yeni kayit acilmaz; ayni `clientRequestId` ile retry veya ilgili detay/liste readback yapilir.
5. E-irsaliye akisi belge yazildiktan sonra ayri calisir. Hata durumunda tekrar gondermeden once e-belge takip kaydi ve varsa ETTN/zarf sorgulanir.
6. UI, Auth takip tablosunu belge kaynagi gibi kullanmaz; kesin stok/siparis durumu icin ilgili Mikro tabanli detay endpointini yeniden cagirir.
