# UBL Fatura Mantigi

Bu dokuman, UBL fatura XML'inin genel mantigini anlatir. Amac belirli bir faturayi degil, UBL icindeki temel bolumlerin ne ise yaradigini ve bizim sistemde hangi Mikro verilerinden beslendigini aciklamaktir.

UBL'yi kabaca su sekilde dusunebiliriz:

```text
Fatura kimligi ve ust bilgiler
Satici bilgisi
Alici bilgisi
Satirlar
Vergi ozetleri
Parasal toplamlar
Ek belgeler ve notlar
```

En kritik kural sudur:

```text
Fatura ekranda tek belge olabilir.
Ama UBL icinde birden fazla satir, birden fazla KDV orani ve birden fazla vergi ozeti olabilir.
```

## Temel UBL Bolumleri

### 1. Fatura Ust Bilgileri

Bu alanlar faturanin kimligini ve turunu anlatir.

```xml
<cbc:UBLVersionID>2.1</cbc:UBLVersionID>
<cbc:CustomizationID>TR1.2</cbc:CustomizationID>
<cbc:ProfileID>TICARIFATURA</cbc:ProfileID>
<cbc:ID>FEF2026000004709</cbc:ID>
<cbc:UUID>...</cbc:UUID>
<cbc:IssueDate>2026-06-17</cbc:IssueDate>
<cbc:InvoiceTypeCode>SATIS</cbc:InvoiceTypeCode>
<cbc:DocumentCurrencyCode>TRY</cbc:DocumentCurrencyCode>
<cbc:LineCountNumeric>2</cbc:LineCountNumeric>
```

| UBL alani | Anlami |
| --- | --- |
| `UBLVersionID` | UBL standardi versiyonu |
| `CustomizationID` | Turkiye UBL-TR uyarlama versiyonu |
| `ProfileID` | Fatura senaryosu/profili: `TICARIFATURA`, `TEMELFATURA`, `EARSIVFATURA` |
| `ID` | Resmi fatura numarasi |
| `UUID` | Faturanin benzersiz teknik kimligi |
| `IssueDate` | Fatura tarihi |
| `InvoiceTypeCode` | Fatura tipi: satis, iade, istisna vb. |
| `DocumentCurrencyCode` | Para birimi |
| `LineCountNumeric` | UBL icindeki fatura satiri sayisi |

Bizim sistemde:

| UBL alani | Kaynak/mantik |
| --- | --- |
| `ID` | `documentSerie + yil + sira` mantigiyla uretilir veya Mikro belge numarasindan beslenir |
| `UUID` | Mikro fatura hareketinin `cha_Guid` degeri |
| `IssueDate` | Mikro belge tarihi |
| `ProfileID` | e-Fatura/e-Arsiv senaryosu ve cari durumuna gore secilir |
| `LineCountNumeric` | Uretilen `InvoiceLine` sayisi |

## 2. Taraf Bilgileri

UBL'de satici ve alici ayri bolumlerde durur.

```xml
<cac:AccountingSupplierParty>
  ...
</cac:AccountingSupplierParty>

<cac:AccountingCustomerParty>
  ...
</cac:AccountingCustomerParty>
```

| Bolum | Anlami |
| --- | --- |
| `AccountingSupplierParty` | Faturayi kesen taraf |
| `AccountingCustomerParty` | Faturanin kesildigi taraf |

Bu bolumlerde genelde sunlar olur:

```text
Unvan
VKN/TCKN
Vergi dairesi
Adres
Il/ilce
E-posta/telefon
```

Bizim sistemde alici bilgileri genelde Mikro `CARI_HESAPLAR` ve `CARI_HESAP_ADRESLERI` tablolarindan gelir.

## 3. InvoiceLine

`InvoiceLine`, faturanin satiridir.

Bir faturada tek satir olabilir:

```text
1 adet hizmet
```

Birden fazla satir da olabilir:

```text
3 stok kalemi
2 hizmet kalemi
1 masraf kalemi
```

UBL'de her satir ayri `cac:InvoiceLine` olarak yazilir.

```xml
<cac:InvoiceLine>
  <cbc:ID>1</cbc:ID>
  <cbc:InvoicedQuantity unitCode="NIU">1</cbc:InvoicedQuantity>
  <cbc:LineExtensionAmount currencyID="TRY">1000.00</cbc:LineExtensionAmount>
  ...
</cac:InvoiceLine>
```

| UBL alani | Anlami |
| --- | --- |
| `InvoiceLine` | Fatura satiri |
| `ID` | Satir numarasi |
| `InvoicedQuantity` | Miktar |
| `LineExtensionAmount` | Satirin KDV haric net tutari |

Bizim sistemde satirlar iki ana kaynaktan gelebilir:

| Fatura tipi | Mikro kaynagi |
| --- | --- |
| Stoklu fatura | `STOK_HAREKETLERI` |
| Hizmet/cari fatura | `CARI_HESAP_HAREKETLERI` + `HIZMET_HESAPLARI` |

Hizmet satirinda:

```text
cha_kasa_hizkod  -> hizmet kodu
hiz_isim         -> hizmet adi
cha_miktari      -> miktar
cha_aratoplam    -> KDV haric satir tutari
```

Stok satirinda:

```text
sth_stok_kod     -> stok kodu
sto_isim         -> stok adi
sth_miktar       -> miktar
sth_tutar        -> KDV haric tutar
```

## 4. Satir Vergisi

Her `InvoiceLine` kendi vergi bilgisini tasir.

```xml
<cac:TaxTotal>
  <cbc:TaxAmount currencyID="TRY">200.00</cbc:TaxAmount>
  <cac:TaxSubtotal>
    <cbc:TaxableAmount currencyID="TRY">1000.00</cbc:TaxableAmount>
    <cbc:TaxAmount currencyID="TRY">200.00</cbc:TaxAmount>
    <cbc:Percent>20.00</cbc:Percent>
  </cac:TaxSubtotal>
</cac:TaxTotal>
```

| UBL alani | Anlami |
| --- | --- |
| `TaxTotal` | Bu satirin toplam vergisi |
| `TaxAmount` | Bu satirin KDV tutari |
| `TaxSubtotal` | Vergi detay grubu |
| `TaxableAmount` | Verginin hesaplandigi matrah |
| `Percent` | KDV orani |

Mikro mantigi:

```text
Vergi pointer = hangi KDV oraninin kullanildigini soyler
Vergi tutari  = pointer hangi kolonu gosteriyorsa oradan okunur
```

Hizmet/cari satirinda:

```text
cha_vergipntr = 1  => cha_vergi1
cha_vergipntr = 2  => cha_vergi2
...
cha_vergipntr = 10 => cha_vergi10
```

Stok satirinda:

```text
sth_vergi_pntr -> KDV pointer
sth_vergi      -> KDV tutari
```

KDV orani ise Mikro vergi oran listesinden cozulur:

```text
fn_hs_vergi_oran_listesi()
```

Ornek:

```text
pointer 1 => %20
pointer 2 => %10
```

Burada onemli nokta:

```text
KDV orani hizmet adindan okunmaz.
KDV orani Mikro satirindaki vergi pointer'indan cozulur.
```

## 5. Fatura Ust Vergi Ozeti

Satirlarin icinde vergi bilgisi vardir; ama UBL faturanin ustunde ayrica genel vergi ozeti de ister.

```xml
<cac:TaxTotal>
  <cbc:TaxAmount currencyID="TRY">250.00</cbc:TaxAmount>

  <cac:TaxSubtotal>
    <cbc:TaxableAmount currencyID="TRY">500.00</cbc:TaxableAmount>
    <cbc:TaxAmount currencyID="TRY">50.00</cbc:TaxAmount>
    <cbc:Percent>10.00</cbc:Percent>
  </cac:TaxSubtotal>

  <cac:TaxSubtotal>
    <cbc:TaxableAmount currencyID="TRY">1000.00</cbc:TaxableAmount>
    <cbc:TaxAmount currencyID="TRY">200.00</cbc:TaxAmount>
    <cbc:Percent>20.00</cbc:Percent>
  </cac:TaxSubtotal>
</cac:TaxTotal>
```

Bu bolumun mantigi:

```text
Faturadaki tum satirlari KDV oranina gore grupla.
Her KDV oraninin matrahini topla.
Her KDV oraninin KDV tutarini topla.
En ustte tum KDV'lerin toplam tutarini yaz.
```

Ornek:

```text
%10 satirlari matrahi: 500,00
%10 KDV: 50,00

%20 satirlari matrahi: 1.000,00
%20 KDV: 200,00

Toplam KDV: 250,00
```

Farkli KDV oranlari varsa UBL'de ayri `TaxSubtotal` olarak durmalidir.

Yanlis:

```text
%10 ve %20 satirlari tek vergi grubuna ezmek
```

Dogru:

```text
%10 ayri TaxSubtotal
%20 ayri TaxSubtotal
```

## 6. LegalMonetaryTotal

`LegalMonetaryTotal`, faturanin genel parasal toplamlaridir.

```xml
<cac:LegalMonetaryTotal>
  <cbc:LineExtensionAmount currencyID="TRY">1500.00</cbc:LineExtensionAmount>
  <cbc:TaxExclusiveAmount currencyID="TRY">1500.00</cbc:TaxExclusiveAmount>
  <cbc:TaxInclusiveAmount currencyID="TRY">1750.00</cbc:TaxInclusiveAmount>
  <cbc:AllowanceTotalAmount currencyID="TRY">0.00</cbc:AllowanceTotalAmount>
  <cbc:ChargeTotalAmount currencyID="TRY">0.00</cbc:ChargeTotalAmount>
  <cbc:PayableAmount currencyID="TRY">1750.00</cbc:PayableAmount>
</cac:LegalMonetaryTotal>
```

| UBL alani | Anlami |
| --- | --- |
| `LineExtensionAmount` | Satirlarin KDV haric toplam tutari |
| `TaxExclusiveAmount` | Vergi haric fatura toplami |
| `TaxInclusiveAmount` | Vergi dahil fatura toplami |
| `AllowanceTotalAmount` | Toplam iskonto |
| `ChargeTotalAmount` | Toplam ek masraf/diger vergi |
| `PayableAmount` | Odenecek tutar |

Basit hesap:

```text
LineExtensionAmount = satir net tutarlari toplami
TaxExclusiveAmount  = KDV haric toplam
TaxInclusiveAmount  = KDV haric toplam + KDV + varsa diger vergiler
PayableAmount       = odenecek son tutar
```

Bu alanlar satirlarla uyumlu olmalidir.

Yanlis ornek:

```text
Satir 1: 1.000
Satir 2: 500
LegalMonetaryTotal.LineExtensionAmount: 1.000
```

Bu yanlistir. Dogrusu:

```text
LineExtensionAmount: 1.500
```

## 7. Iskonto ve Masraf Mantigi

UBL'de iskonto ve ek masraf bilgileri `AllowanceCharge` ile temsil edilir.

Satir iskontosu olabilir:

```xml
<cac:AllowanceCharge>
  <cbc:ChargeIndicator>false</cbc:ChargeIndicator>
  <cbc:Amount currencyID="TRY">100.00</cbc:Amount>
</cac:AllowanceCharge>
```

Anlami:

```text
ChargeIndicator false => iskonto
ChargeIndicator true  => masraf/artirim
```

Bizim sistemde stok satirlarinda iskonto alanlari Mikro stok hareketinden okunur:

```text
sth_iskonto1
sth_iskonto2
...
sth_iskonto6
```

Hizmet satirlarinda genelde iskonto yoktur; satir net tutari `cha_aratoplam` uzerinden gelir.

## 8. Iade Faturasi Mantigi

Iade faturalarinda UBL icinde iadeye konu olan fatura referansi bulunmalidir.

```xml
<cac:BillingReference>
  <cac:InvoiceDocumentReference>
    <cbc:ID>...</cbc:ID>
    <cbc:IssueDate>...</cbc:IssueDate>
    <cbc:DocumentTypeCode>IADE</cbc:DocumentTypeCode>
  </cac:InvoiceDocumentReference>
</cac:BillingReference>
```

Anlami:

```text
Bu iade faturasi hangi asil faturaya istinaden kesildi?
```

Bizim sistemde bu bilgi Mikro `EBELGE_EVRAK_HAREKETLERI` kaydindan veya fallback referans cozumleme mantigindan gelir.

## 9. XSLT ve UBL Ayrimi

UBL resmi fatura verisidir.

XSLT ise bu XML'in nasil gorunecegini belirleyen sablondur.

```text
UBL yanlissa fatura verisi yanlistir.
XSLT yanlissa fatura goruntusu bozuk olabilir ama veri dogru kalabilir.
```

Bu yuzden asil muhasebe ve GIB mantigi UBL alanlarindadir.

## 10. Sistemimizde Kontrol Mantigi

Gonderimden once UBL is kurali validator'u su uyumlari kontrol eder:

```text
LineCountNumeric = InvoiceLine sayisi mi?
LegalMonetaryTotal.LineExtensionAmount = satir matrahlari toplami mi?
TaxTotal.TaxAmount = satir KDV toplamlari mi?
TaxInclusiveAmount = matrah + KDV + diger vergiler mi?
PayableAmount = odenecek tutar dogru mu?
```

Bu kontrollerin amaci sudur:

```text
Satirlar dogru ama toplam yanlis olmasin.
Toplam dogru ama KDV ozeti yanlis olmasin.
Farkli KDV oranlari tek gruba ezilmesin.
Tek fatura icindeki birden fazla satir kaybolmasin.
```

## 11. Liste ve UBL Ayrimi

Liste ekraninda belge bazli gosterim dogrudur:

```text
Bir fatura numarasi = ekranda bir satir
```

Ama UBL icinde satir bazli temsil dogrudur:

```text
Bir fatura numarasi = birden fazla InvoiceLine olabilir
```

Ornek:

```text
Fatura No: FEF2026000004709

UBL satirlari:
1. CIRO PRIMI GELIRI %10
2. CIRO PRIMI GELIRI %20
```

Bu tek faturadir. Ekranda tek fatura gorunmelidir. Fakat UBL'de iki satir kalmalidir.

## 12. Kisa Ozet

```text
InvoiceLine
  Fatura satiri.

LineExtensionAmount
  Satirin KDV haric tutari.

TaxTotal
  Satirin veya faturanin vergi toplam bilgisi.

TaxSubtotal
  KDV oranina gore matrah/vergi detayi.

LegalMonetaryTotal
  Faturanin genel parasal toplam bilgisi.

PayableAmount
  Odenecek son tutar.
```

En temel kural:

```text
Satirlarin toplami, vergi ozetleri ve genel toplamlar birbirini tutmalidir.
```
