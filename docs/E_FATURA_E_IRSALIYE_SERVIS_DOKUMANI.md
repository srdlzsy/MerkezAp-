# E-Fatura Ve E-Irsaliye Servis Dokumani

Bu dokuman, dis paylasim icin yalnizca e-fatura ve e-irsaliye ile ilgili
servisleri kapsar. Stok, siparis, kasa, rapor, genel mal kabul veya sevk
olusturma endpointleri bu dokumana dahil edilmemistir.

## Ortak Bilgiler

Base URL:

```text
{{baseUrl}}
```

Tum JSON endpointlerinde beklenen temel header'lar:

```http
Accept: application/json
Authorization: Bearer {{token}}
```

Body alan endpointlerde ayrica:

```http
Content-Type: application/json
```

PDF endpointleri `application/pdf` doner. Hata durumlarinda genel olarak
`ProblemDetails` formati kullanilir.

Tarih alanlari ISO formatta gonderilmelidir:

```text
2026-07-21
2026-07-21T10:30:00
```

Enum notu: JSON body ve response icinde enum degerleri sayisal kullanilir.
Query parametrelerinde enum adi veya sayisal deger verilebilir.

| Enum | Degerler |
| --- | --- |
| `InvoiceSendingScenario` | `0`: `EFatura`, `1`: `EArsiv` |
| `InvoiceDocumentProfile` | `0`: `Auto`, `1`: `EFatura`, `2`: `EArsiv` |
| `EDespatchDocumentType` | `1`: `OutgoingCompanyShipment`, `2`: `CompanyReturn`, `3`: `InterWarehouseShipment`, `4`: `WarehouseReturn` |

## Yetkiler

| Alan | Yetki |
| --- | --- |
| Uyumsoft e-fatura liste/operasyon listesi | `entegrasyon-islemleri.uyumsoft-e-fatura.list` |
| Uyumsoft e-fatura detay/operasyon cagrisi | `entegrasyon-islemleri.uyumsoft-e-fatura.detail` |
| Uyumsoft e-irsaliye liste/operasyon listesi | `entegrasyon-islemleri.uyumsoft-e-irsaliye.list` |
| Uyumsoft e-irsaliye detay/operasyon cagrisi | `entegrasyon-islemleri.uyumsoft-e-irsaliye.detail` |
| Fatura gonderimi liste | `fatura-islemleri.fatura-gonderimi.list` |
| Fatura gonderimi detay/PDF/render | `fatura-islemleri.fatura-gonderimi.detail` |
| Fatura gonderimi validate/send/retry/preview | `fatura-islemleri.fatura-gonderimi.create` |
| Fatura goruntuleme liste/senkronizasyon | `fatura-islemleri.fatura-goruntuleme.list` |
| Fatura goruntuleme detay/PDF/render | `fatura-islemleri.fatura-goruntuleme.detail` |
| Fatura goruntuleme yazdirildi durumu | `fatura-islemleri.fatura-goruntuleme.update` |

E-irsaliye gonderim/PDF endpointlerinde belge modulunun kendi detay yetkisi
kullanilir.

## Uyumsoft E-Fatura Entegrasyon Servisi

Route kok adresi:

```text
/api/entegrasyon-islemleri/uyumsoft/e-fatura
```

Bu grup Uyumsoft `BasicIntegration` servisinin get operasyonlarini backend
uzerinden calistirir.

| Method | Endpoint | Aciklama | Yetki | Response |
| --- | --- | --- | --- | --- |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura` | Servis ozeti, endpoint ve desteklenen operasyonlar | `list` | `UyumsoftConnectedServiceOverviewDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/operations` | Desteklenen operasyon listesi | `list` | `UyumsoftOperationDefinitionDto[]` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/get/{operationName}` | Dinamik Uyumsoft operasyon cagrisi | `detail` | `UyumsoftOperationResponseDto` |
| POST | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/get/{operationName}` | Dinamik Uyumsoft operasyon cagrisi, parametreler body ile | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/system/date` | Uyumsoft sistem tarihi | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/system/date/formatted?format={format}` | Formatli sistem tarihi | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/inbox/invoices/{invoiceUuid}` | Gelen fatura XML/veri cevabi | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/inbox/invoices/{invoiceUuid}/data` | Gelen fatura data cevabi | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/inbox/invoices/{invoiceUuid}/view` | Gelen fatura gorunumu | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/inbox/invoices/{invoiceUuid}/pdf` | Gelen fatura PDF datasini Uyumsoft response icinde doner | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/inbox/invoices/{invoiceUuid}/pdf-file` | Gelen fatura PDF dosyasi | `detail` | `application/pdf` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/inbox/invoices/{invoiceUuid}/status-with-logs` | Gelen fatura durum/log bilgisi | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/outbox/invoices/{invoiceUuid}` | Giden fatura XML/veri cevabi | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/outbox/invoices/{invoiceUuid}/data` | Giden fatura data cevabi | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/outbox/invoices/{invoiceUuid}/view` | Giden fatura gorunumu | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/outbox/invoices/{invoiceUuid}/pdf` | Giden fatura PDF datasini Uyumsoft response icinde doner | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/outbox/invoices/{invoiceUuid}/pdf-file` | Giden fatura PDF dosyasi | `detail` | `application/pdf` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/outbox/invoices/{invoiceUuid}/status-with-logs` | Giden fatura durum/log bilgisi | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/outbox/invoices/{invoiceUuid}/response-view` | Giden fatura yanit gorunumu | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-fatura/invoices/{invoiceUuid}/envelope` | Fatura zarfi | `detail` | `UyumsoftOperationResponseDto` |

Dinamik GET parametre formati:

```http
GET {{baseUrl}}/api/entegrasyon-islemleri/uyumsoft/e-fatura/get/GetInboxInvoice?parameter=invoiceId=11111111-1111-1111-1111-111111111111
Authorization: Bearer {{token}}
```

Dinamik POST body formati:

```json
{
  "parameters": [
    {
      "name": "invoiceId",
      "value": "11111111-1111-1111-1111-111111111111"
    }
  ]
}
```

Desteklenen ana Uyumsoft e-fatura operasyonlari:

| Grup | Operasyonlar |
| --- | --- |
| Sistem | `GetSystemDate`, `GetSystemDateWithFormat`, `GetAccessToken` |
| Kullanicilar | `GetEInvoiceUsers`, `GetUserAliasses`, `GetSystemUsersCompressedList`, `GetSystemUsersCompressedListOld` |
| Gelen Fatura | `GetInboxInvoices`, `GetInboxInvoiceList`, `GetInboxInvoice`, `GetInboxInvoicesData`, `GetInboxInvoiceData`, `GetInboxInvoiceView`, `GetInboxInvoicePdf`, `GetInboxInvoiceStatusWithLogs` |
| Giden Fatura | `GetOutboxInvoices`, `GetOutboxInvoiceList`, `GetOutboxInvoice`, `GetOutboxInvoicesData`, `GetOutboxInvoiceData`, `GetOutboxInvoiceView`, `GetOutboxInvoicePdf`, `GetOutboxInvoiceStatusWithLogs`, `GetOutboxInvoiceResponseView` |
| Dokuman/Rapor | `GetInvoiceEnvelope`, `GetSummaryReport`, `GetCustomerCreditInfo` |

## Fatura Gonderimi

Route kok adresi:

```text
/api/fatura-islemleri/fatura-gonderimi
```

Bu grup giden e-fatura/e-arsiv belgelerini listeler, render eder, validate eder,
Uyumsoft'a gonderir ve gonderilmis belgelerin PDF'ini getirir.

| Method | Endpoint | Aciklama | Yetki | Response |
| --- | --- | --- | --- | --- |
| GET | `/api/fatura-islemleri/fatura-gonderimi` | Gonderilecek/gonderilmis faturalari listeler | `list` | `InvoiceSendingListResponse` |
| GET | `/api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}` | Belge detayini ve HTML render sonucunu doner | `detail` | `InvoiceSendingDetailDto` |
| GET | `/api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}/pdf` | Gonderilmis giden faturanin PDF dosyasi | `detail` | `application/pdf` |
| POST | `/api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}/render` | XSLT tercihleriyle HTML render | `detail` | `InvoiceSendingDetailDto` |
| GET | `/api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}/return-reference-candidates` | Iade faturasi referans adaylari | `detail` | `InvoiceReturnReferenceCandidatesResponse` |
| PUT | `/api/fatura-islemleri/fatura-gonderimi/{documentSerie}/{documentOrderNo}/return-reference` | Iade fatura referansini gunceller | `create` | `UpdateInvoiceReturnReferenceResponse` |
| POST | `/api/fatura-islemleri/fatura-gonderimi/validate` | Secilen belgeleri UBL/is kurali/XSD olarak kontrol eder, gondermez | `create` | `ValidateInvoiceDocumentsResponse` |
| POST | `/api/fatura-islemleri/fatura-gonderimi/send` | Secilen belgeleri Uyumsoft'a gonderir | `create` | `SendInvoiceDocumentsResponse` |
| POST | `/api/fatura-islemleri/fatura-gonderimi/retry` | Daha once gonderilmis belgeyi yeniden kuyruga alir | `create` | `RetryInvoiceDocumentsResponse` |
| POST | `/api/fatura-islemleri/fatura-gonderimi/preview` | Disaridan verilen XML'i HTML olarak render eder | `create` | `InvoiceRenderedDocumentDto` |

Liste query parametreleri:

| Parametre | Zorunlu | Aciklama |
| --- | --- | --- |
| `StartDate` | Evet | Baslangic tarihi |
| `EndDate` | Evet | Bitis tarihi |
| `Scenario` | Hayir | `EFatura`/`EArsiv` veya `0`/`1`. Varsayilan `EFatura` |
| `isSent` | Hayir | `-1`: hepsi, `0`: gonderilmemis, `1`: gonderilmis |
| `SentState` | Hayir | `isSent` alias'i. Varsayilan `0` |

Liste ornegi:

```http
GET {{baseUrl}}/api/fatura-islemleri/fatura-gonderimi?StartDate=2026-07-01&EndDate=2026-07-21&Scenario=EFatura&isSent=0
Accept: application/json
Authorization: Bearer {{token}}
```

Toplu validate/send/retry body formati:

```json
{
  "scenario": 0,
  "documents": [
    {
      "documentSerie": "FRP26",
      "documentOrderNo": 21791
    }
  ]
}
```

Render body formati:

```json
{
  "scenario": 0,
  "profile": 0,
  "preferEmbeddedXslt": true,
  "fallbackToGeneral": true
}
```

Preview body formati:

```json
{
  "invoiceId": "preview",
  "xmlContent": "<Invoice>...</Invoice>",
  "profile": 0,
  "preferEmbeddedXslt": true
}
```

Fatura gonderimi ana response alanlari:

| Model | Alanlar |
| --- | --- |
| `InvoiceSendingListResponse` | `totalCount`, `items` |
| `InvoiceSendingListItemDto` | `documentSerie`, `documentOrderNo`, `invoiceId`, `documentDate`, `sentDocumentNo`, `isSent`, `customerCode`, `customerTitle`, `customerTcknVkn`, `targetAlias`, `invoiceProfileId`, `invoiceTypeCode`, `scenario`, `lineExtensionTotal`, `taxTotal`, `chargeTotal`, `payableTotal`, `shipmentDocumentNo`, `shipmentDocumentDate`, `returnInvoiceNo`, `returnInvoiceDate`, `warehouseName`, `description`, `sourceLineCount`, `sourceLineSummary`, `taxRateSummary` |
| `SendInvoiceDocumentsResponse` | `scenario`, `requestedCount`, `succeededCount`, `failedCount`, `items` |
| `SendInvoiceDocumentResultDto` | `documentSerie`, `documentOrderNo`, `invoiceId`, `customerCode`, `customerTitle`, `isSucceeded`, `serviceDocumentId`, `serviceDocumentNumber`, `message` |
| `ValidateInvoiceDocumentsResponse` | `scenario`, `requestedCount`, `validCount`, `invalidCount`, `items` |
| `RetryInvoiceDocumentsResponse` | `scenario`, `requestedCount`, `succeededCount`, `failedCount`, `items` |

## Fatura Goruntuleme

Route kok adresi:

```text
/api/fatura-islemleri/fatura-goruntuleme
```

Bu grup gelen/inbox faturalarin Uyumsoft'tan cache'e alinmasi, listelenmesi,
PDF/HTML olarak goruntulenmesi ve yazdirildi bilgisinin tutulmasi icindir.

| Method | Endpoint | Aciklama | Yetki | Response |
| --- | --- | --- | --- | --- |
| GET | `/api/fatura-islemleri/fatura-goruntuleme` | Gelen fatura cache listesini doner | `list` | `InvoiceViewingListResponse` |
| POST | `/api/fatura-islemleri/fatura-goruntuleme/senkronize` | Tarih araligini Uyumsoft'tan cache'e alma isini baslatir | `list` | `202 Accepted`, `InvoiceViewingSynchronizationProgressResponse` |
| GET | `/api/fatura-islemleri/fatura-goruntuleme/senkronize/progress` | Aktif/son senkronizasyon durumunu doner | `list` | `InvoiceViewingSynchronizationProgressResponse` |
| GET | `/api/fatura-islemleri/fatura-goruntuleme/{documentId}` | Gelen fatura PDF dosyasi | `detail` | `application/pdf` |
| GET | `/api/fatura-islemleri/fatura-goruntuleme/{documentId}/pdf` | PDF alias'i | `detail` | `application/pdf` |
| GET | `/api/fatura-islemleri/fatura-goruntuleme/{documentId}/detail` | Gelen fatura detay ve HTML render | `detail` | `InvoiceViewingDetailDto` |
| POST | `/api/fatura-islemleri/fatura-goruntuleme/{documentId}/render` | XSLT tercihleriyle HTML render | `detail` | `InvoiceViewingDetailDto` |
| PATCH | `/api/fatura-islemleri/fatura-goruntuleme/{documentId}/printed` | Yazdirildi/yazdirilmadi durumunu isaretler | `update` | `InvoiceViewingPrintedStateResponse` |

Liste query parametreleri:

| Parametre | Zorunlu | Aciklama |
| --- | --- | --- |
| `StartDate` | Evet | Baslangic tarihi |
| `EndDate` | Evet | Bitis tarihi |
| `isProcessed` veya `ProcessedState` | Hayir | `-1`: hepsi, `0`: islenmemis, `1`: islenmis |
| `isPrinted` veya `PrintedState` | Hayir | `-1`: hepsi, `0`: yazdirilmamis, `1`: yazdirilmis |
| `InvoiceId` veya `invoiceNo` | Hayir | Fatura numarasi filtresi |
| `DespatchId` veya `despatchNo` | Hayir | Irsaliye numarasi filtresi |
| `CustomerTitle` | Hayir | Cari unvan filtresi |
| `CustomerTcknVkn` veya `tcknVkn` | Hayir | TCKN/VKN filtresi |
| `DocumentId` veya `ettn` | Hayir | ETTN/UUID filtresi |
| `OrderDocumentId` | Hayir | Siparis belge numarasi filtresi |
| `Status` | Hayir | Durum filtresi |
| `InvoiceType` | Hayir | Fatura tipi filtresi |
| `MinInvoiceTotal` | Hayir | Minimum tutar |
| `MaxInvoiceTotal` | Hayir | Maksimum tutar |
| `HasDespatchId` | Hayir | Irsaliye bilgisi var/yok filtresi |
| `SearchField` | Hayir | `InvoiceDate`, `InvoiceId`, `CustomerTitle`, `CustomerTcknVkn`, `InvoiceTotal`, `DespatchId`, `Any`, `Status`, `InvoiceType`, `EnvelopeIdentifier`, `OrderDocumentId`, `Message`, `DocumentId` |
| `SearchText` | Hayir | Secilen alanda aranacak metin |
| `page` veya `PageNumber` | Hayir | Sayfa numarasi, varsayilan `1` |
| `PageSize` | Hayir | Sayfa boyutu, varsayilan `50` |

Liste ornegi:

```http
GET {{baseUrl}}/api/fatura-islemleri/fatura-goruntuleme?StartDate=2026-07-01&EndDate=2026-07-21&isProcessed=-1&isPrinted=-1&page=1&PageSize=50
Accept: application/json
Authorization: Bearer {{token}}
```

Senkronizasyon body formati:

```json
{
  "startDate": "2026-07-01",
  "endDate": "2026-07-21",
  "includeStatuses": false
}
```

Render body formati:

```json
{
  "profile": 0,
  "preferEmbeddedXslt": true,
  "fallbackToGeneral": true
}
```

Yazdirildi durumu body formati:

```json
{
  "isPrinted": true,
  "source": "manual-update"
}
```

Fatura goruntuleme ana response alanlari:

| Model | Alanlar |
| --- | --- |
| `InvoiceViewingListResponse` | `totalCount`, `pageNumber`, `pageSize`, `items` |
| `InvoiceViewingListItemDto` | `documentId`, `invoiceId`, `customerTitle`, `customerTcknVkn`, `createDate`, `invoiceDate`, `invoiceType`, `invoiceTotal`, `despatchId`, `isProcessed`, `isPrinted`, `isStandard`, `statusCode`, `status`, `envelopeIdentifier`, `envelopeStatusCode`, `message`, `taxTotal`, `taxExclusiveAmount`, `documentCurrencyCode`, `exchangeRate`, `orderDocumentId`, `isArchived`, `invoiceTipType`, `invoiceTipTypeCode`, `isSeen` |
| `InvoiceRenderedDocumentDto` | `source`, `invoiceId`, `profile`, `appliedXsltName`, `xsltSource`, `usedEmbeddedXslt`, `xmlContent`, `htmlContent` |
| `InvoiceViewingSynchronizationProgressResponse` | `isRunning`, `status`, `startDate`, `endDate`, `includeStatuses`, `pageIndex`, `pageNumber`, `pageSize`, `totalCount`, `totalPage`, `fetchedCount`, `matchedCount`, `insertedCount`, `updatedCount`, `progressPercent`, `startedAtUtc`, `lastUpdatedAtUtc`, `finishedAtUtc`, `elapsedMs`, `message` |

## Uyumsoft E-Irsaliye Entegrasyon Servisi

Route kok adresi:

```text
/api/entegrasyon-islemleri/uyumsoft/e-irsaliye
```

Bu grup Uyumsoft `BasicDespatchIntegration` servisinin get operasyonlarini
backend uzerinden calistirir.

| Method | Endpoint | Aciklama | Yetki | Response |
| --- | --- | --- | --- | --- |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye` | Servis ozeti, endpoint ve desteklenen operasyonlar | `list` | `UyumsoftConnectedServiceOverviewDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/operations` | Desteklenen operasyon listesi | `list` | `UyumsoftOperationDefinitionDto[]` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/get/{operationName}` | Dinamik Uyumsoft operasyon cagrisi | `detail` | `UyumsoftOperationResponseDto` |
| POST | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/get/{operationName}` | Dinamik Uyumsoft operasyon cagrisi, parametreler body ile | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/system/date` | Uyumsoft sistem tarihi | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/system/date/formatted?format={format}` | Formatli sistem tarihi | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/inbox/despatches/{despatchId}` | Gelen e-irsaliye XML/veri cevabi | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/inbox/despatches/{despatchId}/view` | Gelen e-irsaliye gorunumu | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/inbox/despatches/{despatchId}/pdf` | Gelen e-irsaliye PDF datasini Uyumsoft response icinde doner | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/inbox/despatches/{despatchId}/status-with-logs` | Gelen e-irsaliye durum/log bilgisi | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/outbox/despatches/{despatchId}` | Giden e-irsaliye XML/veri cevabi | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/outbox/despatches/{despatchId}/view` | Giden e-irsaliye gorunumu | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/outbox/despatches/{despatchId}/pdf` | Giden e-irsaliye PDF datasini Uyumsoft response icinde doner | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/outbox/despatches/{despatchId}/status-with-logs` | Giden e-irsaliye durum/log bilgisi | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/receipt-advices/{despatchId}/view` | Teslim alma yaniti gorunumu | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/receipt-advices/{despatchId}/pdf` | Teslim alma yaniti PDF datasini Uyumsoft response icinde doner | `detail` | `UyumsoftOperationResponseDto` |
| GET | `/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/despatches/{despatchId}/envelope?isInbox={trueOrFalse}` | E-irsaliye zarfi | `detail` | `UyumsoftOperationResponseDto` |

Dinamik GET ornegi:

```http
GET {{baseUrl}}/api/entegrasyon-islemleri/uyumsoft/e-irsaliye/get/GetOutboxDespatch?parameter=despatchId=11111111-1111-1111-1111-111111111111
Authorization: Bearer {{token}}
```

Desteklenen ana Uyumsoft e-irsaliye operasyonlari:

| Grup | Operasyonlar |
| --- | --- |
| Sistem | `GetSystemDate`, `GetSystemDateWithFormat`, `GetAccessToken` |
| Kullanicilar | `GetEDespatchUsers`, `GetUserAliasses`, `GetCustomerCreditInfo` |
| Gelen Irsaliye | `GetInboxDespatch`, `GetInboxDespatches`, `GetInboxDespatchList`, `GetInboxDespatchesData`, `GetInboxDespatchView`, `GetInboxDespatchPdf`, `GetInboxDespatchStatusWithLogs` |
| Giden Irsaliye | `GetOutboxDespatch`, `GetOutboxDespatches`, `GetOutboxDespatchList`, `GetOutboxDespatchesData`, `GetOutboxDespatchView`, `GetOutboxDespatchPdf`, `GetOutboxDespatchStatusWithLogs` |
| Makbuz/Dokuman | `GetReceiptAdviceView`, `GetReceiptAdvicePdf`, `GetInboxReceiptAdvicesList`, `GetInboxReceiptAdvices`, `GetInboxReceiptAdvicesData`, `GetDespatchEnvelope` |

## E-Irsaliye Gonderimi Ve PDF

Bu endpointler Mikro'da olusmus sevk/iade belgelerini Uyumsoft'a e-irsaliye
olarak gonderir veya gonderilmis belgenin PDF'ini getirir.

Gonderim body formati tum belge tiplerinde aynidir:

```json
{
  "plaque": "34ABC123",
  "driverNameSurname": "Ad Soyad",
  "driverTckn": "11111111111"
}
```

Alan kurallari:

| Alan | Zorunlu | Kural |
| --- | --- | --- |
| `plaque` | Evet | Maksimum 25 karakter |
| `driverNameSurname` | Evet | Maksimum 25 karakter |
| `driverTckn` | Evet | Maksimum 25 karakter |

Gonderim response modeli:

| Alan | Aciklama |
| --- | --- |
| `documentType` | Belge tipi: `1`: `OutgoingCompanyShipment`, `2`: `CompanyReturn`, `3`: `InterWarehouseShipment`, `4`: `WarehouseReturn` |
| `documentSerie` | Mikro belge seri |
| `documentOrderNo` | Mikro belge sira no |
| `eDespatchDocumentNo` | Uretilen e-irsaliye belge no |
| `eDespatchUuid` | Uretilen e-irsaliye UUID/ETTN |
| `serviceDocumentId` | Uyumsoft servis belge id |
| `serviceDocumentNumber` | Uyumsoft servis belge numarasi |
| `sentAt` | Gonderim zamani |
| `endpointUrl` | Kullanilan Uyumsoft endpoint adresi |

### Gonderim Endpointleri

| Belge | Method | Endpoint | Belge tipi | Yetki |
| --- | --- | --- | --- | --- |
| Firma sevki | POST | `/api/sevk-islemleri/firma-sevkleri/{documentSerie}/{documentOrderNo}/e-irsaliye?warehouseNo={warehouseNo}` | `OutgoingCompanyShipment` | `sevk-islemleri.giden-firma-sevkleri.detail` |
| Firma sevki alias | POST | `/api/sevk-islemleri/firma-sevkleri/giden/{documentSerie}/{documentOrderNo}/e-irsaliye?warehouseNo={warehouseNo}` | `OutgoingCompanyShipment` | `sevk-islemleri.giden-firma-sevkleri.detail` |
| Depolar arasi sevk | POST | `/api/sevk-islemleri/depolar-arasi-sevkler/{documentSerie}/{documentOrderNo}/e-irsaliye?warehouseNo={warehouseNo}` | `InterWarehouseShipment` | `sevk-islemleri.giden-depolar-arasi-sevkler.detail` |
| Depolar arasi sevk alias | POST | `/api/sevk-islemleri/depolar-arasi-sevkler/giden/{documentSerie}/{documentOrderNo}/e-irsaliye?warehouseNo={warehouseNo}` | `InterWarehouseShipment` | `sevk-islemleri.giden-depolar-arasi-sevkler.detail` |
| Firma iadesi | POST | `/api/iade-islemleri/firma-iadeleri/{documentSerie}/{documentOrderNo}/e-irsaliye?warehouseNo={warehouseNo}` | `CompanyReturn` | `iade-islemleri.firma-iadeleri.detail` |
| Depo iadesi | POST | `/api/iade-islemleri/depo-iadeleri/{documentSerie}/{documentOrderNo}/e-irsaliye?warehouseNo={warehouseNo}` | `WarehouseReturn` | `iade-islemleri.giden-depo-iadeleri.detail` |
| Depo iadesi alias | POST | `/api/iade-islemleri/depo-iadeleri/giden/{documentSerie}/{documentOrderNo}/e-irsaliye?warehouseNo={warehouseNo}` | `WarehouseReturn` | `iade-islemleri.giden-depo-iadeleri.detail` |

`warehouseNo` query parametresi verilmezse kullanicinin warehouse bilgisi
kullanilir.

Gonderim ornegi:

```http
POST {{baseUrl}}/api/sevk-islemleri/firma-sevkleri/F50/1/e-irsaliye?warehouseNo=1
Accept: application/json
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "plaque": "34ABC123",
  "driverNameSurname": "Ad Soyad",
  "driverTckn": "11111111111"
}
```

### PDF Endpointleri

| Belge | Method | Endpoint | Response |
| --- | --- | --- | --- |
| Firma sevki | GET | `/api/sevk-islemleri/firma-sevkleri/{documentSerie}/{documentOrderNo}/e-irsaliye/pdf?warehouseNo={warehouseNo}` | `application/pdf` |
| Firma sevki alias | GET | `/api/sevk-islemleri/firma-sevkleri/giden/{documentSerie}/{documentOrderNo}/e-irsaliye/pdf?warehouseNo={warehouseNo}` | `application/pdf` |
| Depolar arasi sevk | GET | `/api/sevk-islemleri/depolar-arasi-sevkler/{documentSerie}/{documentOrderNo}/e-irsaliye/pdf?warehouseNo={warehouseNo}` | `application/pdf` |
| Depolar arasi sevk alias | GET | `/api/sevk-islemleri/depolar-arasi-sevkler/giden/{documentSerie}/{documentOrderNo}/e-irsaliye/pdf?warehouseNo={warehouseNo}` | `application/pdf` |
| Firma iadesi | GET | `/api/iade-islemleri/firma-iadeleri/{documentSerie}/{documentOrderNo}/e-irsaliye/pdf?warehouseNo={warehouseNo}` | `application/pdf` |
| Depo iadesi | GET | `/api/iade-islemleri/depo-iadeleri/{documentSerie}/{documentOrderNo}/e-irsaliye/pdf?warehouseNo={warehouseNo}` | `application/pdf` |
| Depo iadesi alias | GET | `/api/iade-islemleri/depo-iadeleri/giden/{documentSerie}/{documentOrderNo}/e-irsaliye/pdf?warehouseNo={warehouseNo}` | `application/pdf` |

PDF ornegi:

```http
GET {{baseUrl}}/api/sevk-islemleri/firma-sevkleri/F50/1/e-irsaliye/pdf?warehouseNo=1
Authorization: Bearer {{token}}
```

## Gelen E-Irsaliye ETTN Sorgulama

Mal kabul ekranlari icin gelen e-irsaliye ETTN/UUID bilgisiyle Uyumsoft'tan
irsaliye okunur, ic stok/cari eslestirme onerileri donulur.

| Method | Endpoint | Aciklama | Yetki | Response |
| --- | --- | --- | --- | --- |
| GET | `/api/mal-kabul-islemleri/firma-mal-kabulleri/e-irsaliye/ettn/{ettn}?warehouseNo={warehouseNo}` | Firma mal kabul icin gelen e-irsaliye sorgular | `mal-kabul-islemleri.firma-mal-kabulleri.create` | `InboundDespatchLookupResponse` |
| GET | `/api/mal-kabul-islemleri/depo-mal-kabulleri/e-irsaliye/ettn/{ettn}?warehouseNo={warehouseNo}` | Depo mal kabul icin gelen e-irsaliye sorgular | `mal-kabul-islemleri.depo-mal-kabulleri.update` | `InboundDespatchLookupResponse` |

Response ana alanlari:

| Alan | Aciklama |
| --- | --- |
| `isFound` | E-irsaliye bulundu mu |
| `warehouseNo` | Sorgulanan depo |
| `receivingContext` | `firma-mal-kabulleri` veya `depo-mal-kabulleri` |
| `ettn` | Sorgulanan UUID/ETTN |
| `despatchNumber` | E-irsaliye numarasi |
| `issueDate` | Duzenleme tarihi |
| `actualDespatchDate` | Fiili sevk tarihi |
| `sender` | Gonderici bilgileri |
| `receiver` | Alici bilgileri |
| `primaryCustomerSuggestion` | Birincil cari eslestirme onerisi |
| `suggestedCustomers` | Tum cari eslestirme onerileri |
| `lines` | Irsaliye satirlari ve stok eslestirme bilgileri |

Ornek:

```http
GET {{baseUrl}}/api/mal-kabul-islemleri/firma-mal-kabulleri/e-irsaliye/ettn/11111111-1111-1111-1111-111111111111?warehouseNo=1
Accept: application/json
Authorization: Bearer {{token}}
```

## Uyumsoft Ortak Response Modelleri

`UyumsoftOperationResponseDto` alanlari:

| Alan | Aciklama |
| --- | --- |
| `serviceKey` | `e-fatura` veya `e-irsaliye` |
| `serviceName` | Uyumsoft servis adi |
| `operationName` | Calistirilan operasyon |
| `resultElementName` | SOAP sonuc element adi |
| `isSucceeded` | Uyumsoft islem sonucu |
| `message` | Servis mesaji |
| `scalarValue` | Tekil metin sonuc varsa |
| `resultAttributes` | Sonuc attribute degerleri |
| `nodes` | Parse edilmis response agaci |
| `invoiceList` | Fatura liste operasyonlari icin normalize liste |
| `responsePayloadJson` | Ham response'un JSON hali |

`UyumsoftConnectedServiceOverviewDto` alanlari:

| Alan | Aciklama |
| --- | --- |
| `serviceKey` | Servis anahtari |
| `serviceName` | Servis adi |
| `endpointUrl` | Kullanilan Uyumsoft endpoint URL |
| `wsdlUrl` | WSDL URL |
| `contractName` | SOAP contract adi |
| `supportedGetOperations` | Desteklenen get operasyonlari |

## Kisa Akis Onerileri

Fatura gonderimi icin tipik akis:

1. `GET /api/fatura-islemleri/fatura-gonderimi?...&isSent=0`
2. `POST /api/fatura-islemleri/fatura-gonderimi/validate`
3. `POST /api/fatura-islemleri/fatura-gonderimi/send`
4. Gonderimden sonra `GET /api/fatura-islemleri/fatura-gonderimi/{seri}/{sira}/pdf?scenario=EFatura`

Gelen fatura goruntuleme icin tipik akis:

1. `POST /api/fatura-islemleri/fatura-goruntuleme/senkronize`
2. `GET /api/fatura-islemleri/fatura-goruntuleme/senkronize/progress`
3. `GET /api/fatura-islemleri/fatura-goruntuleme?...`
4. `GET /api/fatura-islemleri/fatura-goruntuleme/{documentId}/pdf`

E-irsaliye gonderimi icin tipik akis:

1. Ilgili Mikro belgesi olusturulmus olmalidir.
2. Belge tipine uygun `POST .../e-irsaliye` endpointi cagrilir.
3. Basarili response icindeki `eDespatchUuid` ve `eDespatchDocumentNo` saklanir.
4. Gerekirse ayni belge icin `GET .../e-irsaliye/pdf` ile PDF alinir.

Gelen e-irsaliye mal kabul icin tipik akis:

1. `GET /api/mal-kabul-islemleri/{firma-mal-kabulleri|depo-mal-kabulleri}/e-irsaliye/ettn/{ettn}?warehouseNo=...`
2. Response icindeki `primaryCustomerSuggestion`, `suggestedCustomers` ve `lines`
   eslestirme bilgileri UI veya entegrasyon tarafinda kullanilir.
