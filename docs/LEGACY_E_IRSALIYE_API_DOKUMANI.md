# Eski Arayuz E-Irsaliye API Dokumani

Bu dokuman, sevk ve iade evraklarini kendi sistemi veya Mikro API uzerinden olusturan eski arayuzun FurpaMerkezApi e-irsaliye koprusunu nasil kullanacagini anlatir.

## Temel Bilgiler

- Ornek API adresi: `http://10.0.0.100:7508`
- Route koku: `/api/legacy/e-irsaliye`
- JWT veya FurpaMerkezApi login token'i gerekmez.
- Browser `Origin` veya `Referer` adresi backend konfigurasyonundaki `AllowedOrigins` listesinde bulunmalidir.
- `warehouseNo` her istekte query parametresi olarak gonderilmelidir.
- Gonderim POST isteginde `expectedLineCount` zorunludur.
- Bu endpointler yalniz eski arayuz icindir. Yeni arayuz JWT korumali normal e-irsaliye endpointlerini kullanir.

## Desteklenen Belge Turleri

| `documentKind` | Aciklama | Response `documentType` |
|---|---|---:|
| `depolar-arasi-sevkler` | Depolar arasi giden sevk | 3 |
| `depo-iadeleri` | Giden depo iadesi | 4 |
| `firma-sevkleri` | Giden firma sevki | 1 |
| `firma-iadeleri` | Firma iadesi | 2 |

Backend Mikro hareketlerini kontrol eder. Eski arayuz belge turunu yanlis gondermisse sevk/iade ayrimi Mikro satirlarindaki hareket tipine gore duzeltilebilir.

## Endpointler

### E-Irsaliye Gonder

```text
POST /api/legacy/e-irsaliye/{documentKind}/{documentSerie}/{documentOrderNo}/gonder?warehouseNo={warehouseNo}
POST /api/legacy/e-irsaliye/{documentKind}/giden/{documentSerie}/{documentOrderNo}/gonder?warehouseNo={warehouseNo}
```

Iki route ayni islemi yapar. Eski arayuz mevcut route bicimini koruyabilir.

Ornek:

```text
POST http://10.0.0.100:7508/api/legacy/e-irsaliye/depolar-arasi-sevkler/giden/F53/119668/gonder?warehouseNo=53
```

Body:

```json
{
  "driverId": "25a9f3ea-a55a-4558-bb82-8109c3f14cd4",
  "expectedLineCount": 15,
  "plaque": "16BZU759",
  "driverNameSurname": "SINAN BERKER",
  "driverTckn": "11111111111",
  "deliverer": "ZEHRA SAMUK",
  "receiver": "SINAN BERKER"
}
```

Alanlar:

| Alan | Zorunlu | Kural |
|---|---|---|
| `expectedLineCount` | Evet | Eski arayuzun kaydettigi orijinal aktif satir sayisi; en az 1 |
| `driverId` | Hayir | Aktif sofor kaydinin GUID degeri |
| `plaque` | Kosullu | `driverId` yoksa zorunlu; en fazla 25 karakter |
| `driverNameSurname` | Kosullu | `driverId` yoksa zorunlu; ad ve soyad icermeli |
| `driverTckn` | Kosullu | `driverId` yoksa zorunlu; 11 rakam |
| `deliverer` | Hayir | Teslim eden; en fazla 25 karakter |
| `receiver` | Hayir | Teslim alan; en fazla 25 karakter |

`driverId` gonderilirse plaka, ad soyad ve TCKN aktif sofor kaydindan doldurulur. Body'deki manuel sofor alanlari fallback olarak kalabilir.

### `expectedLineCount` Kurali

`expectedLineCount`, parcali e-irsaliye gonderimini engelleyen ana kontroldur.

Dogru kaynak:

```text
Kullanicinin Kaydet tusuna bastigi anda eski arayuzde bulunan
orijinal aktif sevk/iade satirlarinin sayisi
```

Yanlis kaynak:

```text
E-irsaliye tusuna basildiginda Mikro'dan yeniden okunan satir sayisi
```

Mikro API yazimi devam ederken Mikro sorgusu gecici olarak 1, 15 veya 21 satir dondurebilir. Bu gecici sayi `expectedLineCount` olarak gonderilirse koruma etkisiz kalir.

Backend su kontrolleri yapar:

1. Mikro'dan belge ve aktif satirlari okur.
2. Aktif satir sayisini `expectedLineCount` ile karsilastirir.
3. Belge, adres, sofor ve UBL bilgilerini hazirlar.
4. Uyumsoft cagrisindan hemen once belge satirlarini tekrar okur.
5. Satir sayisi ve satir icerigi degismemisse Uyumsoft'a gonderir.
6. Eksik, fazla veya degisen satir varsa `409 Conflict` doner; Uyumsoft'a gonderim yapilmaz.

Bizim API'de trace ile olusturulmus firma sevki/iadesinde ek olarak kayitli create istegi kontrol edilir. Create tamamlanmadiysa veya stok/miktar/birim/satirlar orijinal istekle eslesmiyorsa gonderim engellenir. Bu kontrol `expectedLineCount` zorunlulugunu kaldirmaz. `MIKRO_DOCUMENT_CONTENT_MISMATCH` ve `retryable=false` gelirse tekrar gonderim yapilmaz; yetkili incelemesi gerekir. Eski sistemde olusturulan ve local create trace'i olmayan evraklarda local create kaydi aranmaz.

Basarili response, `200 OK`:

```json
{
  "documentType": 3,
  "documentSerie": "F53",
  "documentOrderNo": 119668,
  "eDespatchDocumentNo": "FRM2026600133000",
  "eDespatchUuid": "0d594419-f940-4f7f-acaf-36ee7735bc21",
  "serviceDocumentId": "...",
  "serviceDocumentNumber": "FRM2026600133000",
  "sentAt": "2026-10-02T10:30:00+03:00",
  "endpointUrl": "...",
  "localMikroMetadataUpdated": false,
  "warning": "E-irsaliye Uyumsoft'a gonderildi; Mikro metadata update was queued and will continue in the background; do not resend.",
  "localMikroMetadataUpdateQueued": true
}
```

`200 OK` geldiyse e-irsaliye Uyumsoft'a gonderilmistir. `localMikroMetadataUpdated=false` veya `localMikroMetadataUpdateQueued=true` yeniden POST atilmasi gerektigi anlamina gelmez.

### Gonderim Durumunu Sorgula

```text
GET /api/legacy/e-irsaliye/{documentKind}/{documentSerie}/{documentOrderNo}/durum?warehouseNo={warehouseNo}
GET /api/legacy/e-irsaliye/{documentKind}/giden/{documentSerie}/{documentOrderNo}/durum?warehouseNo={warehouseNo}
```

Ornek:

```text
GET http://10.0.0.100:7508/api/legacy/e-irsaliye/depolar-arasi-sevkler/giden/F53/119668/durum?warehouseNo=53
```

Response:

```json
{
  "documentType": 3,
  "documentSerie": "F53",
  "documentOrderNo": 119668,
  "isSentToUyumsoft": true,
  "status": "PendingMetadata",
  "eDespatchDocumentNo": "FRM2026600133000",
  "eDespatchUuid": "0d594419-f940-4f7f-acaf-36ee7735bc21",
  "sentAtUtc": "2026-10-02T07:30:00Z",
  "localMikroMetadataUpdated": false,
  "localMikroMetadataUpdateQueued": true,
  "warning": "E-irsaliye Uyumsoft'a gonderildi; Mikro isaretlemesi arka planda devam ediyor."
}
```

Durumlar:

| `status` | Anlami | Eski arayuz davranisi |
|---|---|---|
| `NotSent` | Auth DB'de onayli gonderim yok | Gonder butonu kullanici aksiyonuyla acilabilir |
| `Unknown` | Uyumsoft sonucu dogrulaniyor | POST atma; 5 saniye sonra sadece durum sorgula |
| `PendingMetadata` | Uyumsoft basarili, Mikro isaretleme bekliyor | Gonderildi goster; PDF ac; POST'u kapat |
| `Completed` | Uyumsoft ve Mikro isaretleme tamam | Gonderildi goster; PDF ac |
| `NeedsReview` | Uyumsoft basarili, Mikro icerik/isaret uyusmazligi var | Yeniden gonderme; PDF ac; inceleme uyarisi goster |
| `Sent` | Eski kayitta Uyumsoft gonderim izi bulundu | Gonderildi goster; yeniden POST atma |

`isSentToUyumsoft=true` ise `status` ne olursa olsun tekrar gonderim POST'u atilmamalidir.

### PDF Getir

```text
GET /api/legacy/e-irsaliye/{documentKind}/{documentSerie}/{documentOrderNo}/pdf?warehouseNo={warehouseNo}
GET /api/legacy/e-irsaliye/{documentKind}/giden/{documentSerie}/{documentOrderNo}/pdf?warehouseNo={warehouseNo}
```

Basarili response:

```text
HTTP 200
Content-Type: application/pdf
Content-Disposition: inline; filename="FRM2026600133000.pdf"
```

Browser yeni sekmede acabilir veya response `blob` olarak alinabilir. PDF hatasi gonderim POST'unu tekrarlama nedeni degildir.

## Eski Arayuz Is Akisi

### Liste Acilisi

1. Belgenin tum aktif satirlarinda ayni FRM ve UUID varsa `Gonderildi` goster.
2. Satirlarin bir bolumunde FRM/UUID eksik veya farkliysa belgeyi belirsiz kabul et.
3. Yalniz belirsiz belgeler icin bir kez `GET .../durum` cagir.
4. `isSentToUyumsoft=true` ise FRM ve PDF butonunu goster, gonder butonunu kapat.
5. `NotSent` ise gonder butonunu acik tut.

### Kaydetme ve Gonderme

1. Kullanici sevk/iade formunu kaydederken orijinal aktif satir sayisini sakla.
2. Mikro kaydetme isleminin basarili cevabini bekle.
3. E-irsaliye butonuna basildiginda saklanan sayiyi `expectedLineCount` olarak gonder.
4. POST devam ederken butonu kilitle; cift tik veya paralel POST uretme.
5. `200 OK` gelirse response FRM/UUID'sini ekranda sakla ve PDF butonunu ac.
6. `409 Conflict` gelirse otomatik POST tekrarlama. Mesaji goster, `GET .../durum` ile kesin durumu kontrol et.
7. Timeout veya `502/503/504` gelirse sonucu belirsiz say. Yeni POST atmadan once `GET .../durum` cagir.

## Ornek JavaScript

```js
const API_BASE = "http://10.0.0.100:7508";

async function sendLegacyEDespatch(document, driver) {
  const url =
    `${API_BASE}/api/legacy/e-irsaliye/` +
    `${document.kind}/giden/` +
    `${encodeURIComponent(document.serie)}/${document.orderNo}/gonder` +
    `?warehouseNo=${document.warehouseNo}`;

  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      driverId: driver.id,
      expectedLineCount: document.originalLines.length,
      plaque: driver.plaque,
      driverNameSurname: driver.fullName,
      driverTckn: driver.tckn,
      deliverer: document.deliverer || null,
      receiver: document.receiver || null
    })
  });

  const contentType = response.headers.get("content-type") || "";
  const body = contentType.includes("json") ? await response.json() : null;

  if (response.ok) return body;

  const error = new Error(body?.detail || `HTTP ${response.status}`);
  error.status = response.status;
  throw error;
}
```

Durum sorgusu:

```js
async function getLegacyEDespatchStatus(document) {
  const url =
    `${API_BASE}/api/legacy/e-irsaliye/` +
    `${document.kind}/giden/` +
    `${encodeURIComponent(document.serie)}/${document.orderNo}/durum` +
    `?warehouseNo=${document.warehouseNo}`;

  const response = await fetch(url);
  const body = await response.json();
  if (!response.ok) throw new Error(body?.detail || `HTTP ${response.status}`);
  return body;
}
```

PDF acma:

```js
function openLegacyEDespatchPdf(document) {
  const url =
    `${API_BASE}/api/legacy/e-irsaliye/` +
    `${document.kind}/giden/` +
    `${encodeURIComponent(document.serie)}/${document.orderNo}/pdf` +
    `?warehouseNo=${document.warehouseNo}`;

  window.open(url, "_blank", "noopener,noreferrer");
}
```

## Hata Kodlari

Hatalar `application/problem+json` veya JSON `ProblemDetails` olarak doner:

```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "Document line count does not match the legacy request. Expected 15, but Mikro currently contains 1 lines; no e-despatch was sent.",
  "instance": "/api/legacy/e-irsaliye/depolar-arasi-sevkler/giden/F53/119668/gonder",
  "correlationId": "..."
}
```

| HTTP | Anlami | UI davranisi |
|---:|---|---|
| 400 | Eksik/gecersiz body, `expectedLineCount` veya sofor bilgisi | Formu duzelt; otomatik retry yapma |
| 403 | Origin veya depo izinli degil | Sistem yoneticisine bildir |
| 404 | Kopru kapali, belge/FRM/PDF bulunamadi | Mesaji goster; gonderim durumunu kontrol et |
| 409 | Belge eksik, degismis, create tamamlanmamis veya zaten inceleniyor | Uyumsoft'a gonderilmedi varsayma; once durum sorgula |
| 502 | Uyumsoft/dis servis baglanti hatasi | Once durum sorgula; kontrolsuz POST atma |
| 503 | Veritabani gecici olarak erisilemiyor | 5, 10, 20, en fazla 30 saniye bekleyerek durum sorgula |
| 504 | Dis servis timeout | Sonuc belirsiz; once durum sorgula |

## Polling ve Yuk Kurali

- Ayni belge icin ayni anda tek istek calistir.
- `Completed`, `PendingMetadata`, `NeedsReview` ve `Sent` durumlarini surekli sorgulama.
- Yalniz `Unknown` veya gecici hata alan kayitlari tekrar sorgula.
- Gorunmeyen grid satirlari icin durum istegi atma.
- Sayfadan cikarken timer ve bekleyen istekleri iptal et.
- PDF hatasinda gonderim POST'unu tekrar etme.

## Canliya Alma Sirasi

`expectedLineCount` backend tarafinda zorunludur. Eski arayuz eski body ile devam ederse `400 Bad Request` alir.

Guvenli yayin sirasi:

1. Eski arayuzu `expectedLineCount` gonderecek sekilde yayinla.
2. Eski arayuzun body alanini dogru gonderdigini network kaydindan kontrol et.
3. Yeni FurpaMerkezApi backend surumunu yayinla.
4. Satiri eksik gorunen test belgesinde `409`, tamamlanmis belgede `200` alindigini dogrula.
5. `200` sonrasinda durum ve PDF endpointlerini kontrol et.

## Backend Konfigurasyonu

```json
{
  "LegacyEDespatchBridge": {
    "Enabled": true,
    "AllowedOrigins": [
      "http://10.0.0.100:5002"
    ],
    "AllowedWarehouseNos": [53, 56]
  }
}
```

Canlida `AllowedOrigins` ve `AllowedWarehouseNos` listeleri bos birakilmamalidir. Kopru anonimdir ve gercek e-irsaliye urettigi icin yalniz gereken eski arayuz originleri ile depolar acilmalidir.

Origin/Referer ve CORS kimlik dogrulama degildir; header'lar browser disindan taklit edilebilir. Kopruyu bu listelere guvenerek genel erisime acmayin. Ag/proxy seviyesinde yalniz guvenilir erisimi saglayin; servis kimlik dogrulamasi ayri bir acik gelistirmedir. Yayin kontrolu: [Canliya Gecis Kontrol Listesi](CANLIYA_GECIS_KONTROL_LISTESI.md).
