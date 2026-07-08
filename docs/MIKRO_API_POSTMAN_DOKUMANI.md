# MikroAPI-33e Postman Endpoint Dokumani

Bu dokuman `D:\PROJECTS\FURPA(Serdal OZSOY)\MikroAPI20240614140402-34a\MikroAPI.postman_collection-33e\MikroAPI.postman_collection-33e.json` dosyasindaki Postman collection JSON iceriginden uretilmistir. Response ornekleri collection icinde bulunmadigi icin response semasi runtime testleri veya resmi Mikro API dokumani ile dogrulanmalidir.

## Kisa Ozet

- Collection adi: `MikroAPI-33d`
- Postman collection id: `718bdd44-f21f-4193-a1e1-c223266b6526`
- Schema: `https://schema.getpostman.com/json/collection/v2.1.0/collection.json`
- Kaynak dosya: `D:\PROJECTS\FURPA(Serdal OZSOY)\MikroAPI20240614140402-34a\MikroAPI.postman_collection-33e\MikroAPI.postman_collection-33e.json`
- Guncelleme tarihi: `2026-06-11`
- Toplam grup: `34`
- Toplam endpoint/request: `150`
- Base URL degerleri: `http://10.0.0.207:8084`, `http://10.0.0.207:8094`
- Request body formati genel olarak `raw JSON`.
- Cogu business endpoint `POST` ile calisiyor; silme/guncelleme islemlerinde de HTTP method olarak genelde `POST` kullanilmis.
- Path casing collection icinde karisik: `/API/APIMethods`, `/api/APIMethods`, `/Api/apiMethods`. Entegrasyonda collection pathini aynen kullanmak guvenlidir.
- Bu dokumanda `Sifre`, `ApiKey`, `Token`, `Password` benzeri alanlar repo guvenligi icin placeholder olarak tutuldu.

## Calisan Sunucu ve Auth Profili

Postman environment icin onerilen degiskenler:

| Degisken | Deger |
|---|---|
| `MikroBaseUrl` | `http://10.0.0.207:8084` |
| `MikroFirmaKodu` | `SOPHIGET` |
| `MikroCalismaYili` | `2026` |
| `MikroKullaniciKodu` | `API` |
| `MikroSifreAnahtari` | `<MIKRO_API_SIFRE_ANAHTARI>` |
| `MikroSifreHash` | Pre-request script ile uretilir |
| `MikroApiKey` | `<MIKRO_API_KEY>` |

Dogru calistigi daha once test edilen endpointler:

| Islem | Method | URL | Beklenen sonuc |
|---|---|---|---|
| HealthCheck2 | `GET` | `{{MikroBaseUrl}}/Api/APIMethods/HealthCheck2` | `ApiStatus = Up`, `StatusCode = 200` |
| APILogin | `POST` | `{{MikroBaseUrl}}/Api/APIMethods/APILogin` | `StatusCode = 200`, `IsError = false` |
| Stok Listesi V2 | `POST` | `{{MikroBaseUrl}}/Api/APIMethods/StokListesiV2` | `StatusCode = 200`, `Data.StokListesi` dolu |

## Kimlik Dogrulama

Mikro ERP REST API icin `Sifre` alani gunluk tarih ile uretilen MD5 hash degeridir:

```text
Sifre = MD5("YYYY-MM-DD <MikroSifreAnahtari>")
```

Postman collection veya environment seviyesinde su pre-request script kullanilabilir:

```javascript
const now = new Date();
const year = now.getFullYear();
const month = String(now.getMonth() + 1).padStart(2, "0");
const day = String(now.getDate()).padStart(2, "0");
const formattedDate = `${year}-${month}-${day}`;
const passwordSeed = pm.environment.get("MikroSifreAnahtari");
const md5Hash = CryptoJS.MD5(`${formattedDate} ${passwordSeed}`).toString();

pm.environment.set("MikroSifreHash", md5Hash);
```

Not: Tarih istemcinin calistigi gune gore uretilir. Gun donumu saatlerinde istemci ve Mikro API sunucusunun ayni tarih gununde oldugundan emin olunmalidir.

## Genel Kullanim Akisi

1. `POST /Api/APIMethods/APILogin` ile Mikro API oturumu acilir veya servis kullanicisi dogrulanir.
2. V2 endpointlerin cogunda body icinde `Mikro` nesnesi gonderilir. Bu nesnede firma, calisma yili, kullanici ve sifre bilgileri bulunur.
3. Liste endpointlerinde tarih, sayfalama ve filtre alanlari body icinde gonderilir.
4. Kaydet/guncelle/sil endpointleri genelde ilgili belge veya kart tipine ait array alanlariyla calisir: `cariler[]`, `stoklar[]`, `evraklar[]`, `satirlar[]`, `adresler[]` gibi.
5. GUID ile silme veya satir silme endpointlerinde ilgili kaydin Mikro GUID degeri zorunlu kabul edilmelidir.
6. Islem sonunda hata/sonuc modeli collection icinde yok; entegrasyon kodu HTTP status + response body icindeki basari/hata alanlarini loglamalidir.

### Ortak Mikro Blok

```json
{
  "Mikro": {
    "FirmaKodu": "{{MikroFirmaKodu}}",
    "CalismaYili": {{MikroCalismaYili}},
    "KullaniciKodu": "{{MikroKullaniciKodu}}",
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "ApiKey": "{{MikroApiKey}}"
  }
}
```

Not: Bazi endpointlerde `FirmaNo` ve `SubeNo` yok; bazi endpointlerde `Mikro` nesnesi yerine login alanlari top-level gonderiliyor.

## Grup Ozeti

| Grup | Endpoint sayisi | Ana islev |
|---|---:|---|
| Adres | 3 | Master data kart kaydetme/guncelleme/silme. |
| Alım Satım Evrağı - Fatura | 15 | Alim-satim evraklari, irsaliye ve fatura islemleri. |
| Alınan Teklif | 5 | Siparis, teklif ve sart belgeleri. |
| Cari | 2 | Master data kart kaydetme/guncelleme/silme. |
| Dahili Stok Hareket | 6 | Collection icindeki ilgili Mikro API islem grubu. |
| Dekont | 8 | Finans, tahsilat/tediye, dekont ve muhasebe fisleri. |
| Depolar Arası Sipariş | 5 | Depolar arasi siparis kaydetme/guncelleme/silme. |
| Etiket Basım Kaydet | 1 | Stok, sayim, uretim ve operasyon hareketleri. |
| Evrak Açıklamaları | 3 | Collection icindeki ilgili Mikro API islem grubu. |
| Evrak Belge Resim | 2 | Collection icindeki ilgili Mikro API islem grubu. |
| Fiyat Değişikliği | 1 | Collection icindeki ilgili Mikro API islem grubu. |
| Image Data | 3 | Collection icindeki ilgili Mikro API islem grubu. |
| İrsaliye | 17 | Collection icindeki ilgili Mikro API islem grubu. |
| Kasa Masraf Fişi | 1 | Finans, tahsilat/tediye, dekont ve muhasebe fisleri. |
| Kayıt Kaydet | 3 | Collection icindeki ilgili Mikro API islem grubu. |
| Listeler | 9 | Stok, cari, kullanici, vergi ve parametre listeleri. |
| Login-Logoff | 7 | Oturum, saglik kontrolu ve logoff. |
| Muhasebe | 4 | Finans, tahsilat/tediye, dekont ve muhasebe fisleri. |
| Operasyon Tamamlama Fişi | 2 | Stok, sayim, uretim ve operasyon hareketleri. |
| Personel | 2 | Master data kart kaydetme/guncelleme/silme. |
| Proforma Sipariş | 2 | Siparis, teklif ve sart belgeleri. |
| Satın Alma Talep | 2 | Siparis, teklif ve sart belgeleri. |
| Satış Şartı | 5 | Collection icindeki ilgili Mikro API islem grubu. |
| Satin Alma Şartı | 2 | Collection icindeki ilgili Mikro API islem grubu. |
| Sayım Sonuç Kaydet | 5 | Sayim sonuc kaydetme, guncelleme ve silme. |
| Sipariş | 7 | Siparis, teklif ve sart belgeleri. |
| Stok | 1 | Master data kart kaydetme/guncelleme/silme. |
| Tahsilat Tediye | 12 | Finans, tahsilat/tediye, dekont ve muhasebe fisleri. |
| Üretim İş Emri | 1 | Collection icindeki ilgili Mikro API islem grubu. |
| Üretim Talep | 3 | Siparis, teklif ve sart belgeleri. |
| Ürün Reçete | 2 | Collection icindeki ilgili Mikro API islem grubu. |
| Ürün Rota | 2 | Stok, sayim, uretim ve operasyon hareketleri. |
| Ürün Rota Plan | 2 | Stok, sayim, uretim ve operasyon hareketleri. |
| Verilen Teklif | 5 | Siparis, teklif ve sart belgeleri. |

## Endpoint Indeksi

| Grup | Islem | Method | Path | Body ozeti | Kullanim |
|---|---|---|---|---|---|
| Adres | Adres Duzelt V2 Update | `POST` | `/API/APIMethods/AdresDuzeltV2` | top: Mikro; Mikro: adresler, CalismaYili, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.adresler[] | Mevcut kayit/evrak guncelleme |
| Adres | Adres kaydet V2 Save | `POST` | `/API/APIMethods/AdresKaydetV2` | top: Mikro; Mikro: adresler, CalismaYili, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.adresler[] | Yeni kayit/evrak olusturma |
| Adres | Adres Sil V2 Delete | `POST` | `/API/APIMethods/AdresSilV2` | top: Mikro; Mikro: adresler, CalismaYili, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.adresler[] | Kayit/evrak silme |
| Alım Satım Evrağı - Fatura | Alım Satım Evrağı Düzeltme V2 Update | `POST` | `/Api/apiMethods/AlimSatimEvragiDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay.renk_beden[], Mikro.evraklar.detay[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Alım Satım Evrağı - Fatura | Alım Satım Evrağı Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/AlimSatimEvragiSatirSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar[] | GUID ile satir/kayit silme |
| Alım Satım Evrağı - Fatura | Alım Satım Evrağı Kaydet V2 Alış Faturası Save | `POST` | `/Api/apiMethods/AlimSatimEvragiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay.renk_beden[], Mikro.evraklar.detay.user_tablo[], Mikro.evraklar.detay[], Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.user_tablo[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Alım Satım Evrağı - Fatura | Alım Satım Evrağı Kaydet V2 Hizmet Save | `POST` | `/Api/apiMethods/AlimSatimEvragiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.user_tablo[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Alım Satım Evrağı - Fatura | Alım Satım Evrağı Kaydet V2 Masraf Save | `POST` | `/Api/apiMethods/AlimSatimEvragiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.user_tablo[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Alım Satım Evrağı - Fatura | Alım Satım Evrağı Kaydet V2 Save | `POST` | `/Api/apiMethods/AlimSatimEvragiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.detay.renk_beden[], Mikro.evraklar.detay.user_tablo[], Mikro.evraklar.detay[], Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.user_tablo[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Alım Satım Evrağı - Fatura | Alım Satım Evrağı Satır Ekle V2 Add Guid | `POST` | `/Api/apiMethods/AlimSatimEvragiDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay.renk_beden[], Mikro.evraklar.detay[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Alım Satım Evrağı - Fatura | Alım Satım Evrağı Satır Ekle V2 Add Guid Copy | `POST` | `/Api/apiMethods/AlimSatimEvragiDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay.user_tablo[], Mikro.evraklar.detay[], Mikro.evraklar.ebelge_detay[], Mikro.evraklar.odemeler[], Mikro.evraklar.user_tablo[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Alım Satım Evrağı - Fatura | Alım Satım Evrağı Satır Sil V2 Delete Guid | `POST` | `/Api/apiMethods/AlimSatimEvragiSatirSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| Alım Satım Evrağı - Fatura | Alım Satım Evrağı Sil V2 Delete | `POST` | `/Api/apiMethods/AlimSatimEvragiSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar[] | Kayit/evrak silme |
| Alım Satım Evrağı - Fatura | Fatura Kaydet V2 Save | `POST` | `/Api/apiMethods/FaturaKaydetV2` | Raw body var, JSON parse edilemedi | Yeni kayit/evrak olusturma |
| Alım Satım Evrağı - Fatura | Fatura Kaydet V2 Save Copy | `POST` | `/Api/apiMethods/FaturaKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay.user_tablo[], Mikro.evraklar.detay[], Mikro.evraklar.ebelge_detay[], Mikro.evraklar.odemeler[], Mikro.evraklar.user_tablo[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Alım Satım Evrağı - Fatura | Fatura Kaydet V3 Save | `POST` | `/api/APIMethods/FaturaKaydetV3` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay[], Mikro.evraklar.ebelge_detay[], Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Alım Satım Evrağı - Fatura | Fatura Kaydet V3 Save Copy | `POST` | `/api/APIMethods/FaturaKaydetV3` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Alım Satım Evrağı - Fatura | Fatura PDF V2 | `POST` | `/API/APIMethods/FaturaPdfV2` | top: Mikro; Mikro: CalismaYili, Fatura_Guid, FirmaKodu, KullaniciKodu, Sifre | Collection icindeki Mikro API islemi |
| Alınan Teklif | Alınan Teklif Düzelt V2 Update | `POST` | `/Api/apiMethods/AlinanTeklifDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Alınan Teklif | Alınan Teklif Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/AlinanTeklifDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Alınan Teklif | Alınan Teklif Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/AlinanTeklifGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| Alınan Teklif | Alınan Teklif Kaydet V2 Save | `POST` | `/Api/apiMethods/AlinanTeklifKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Alınan Teklif | Alınan Teklif Sil V2 Delete | `POST` | `/Api/apiMethods/AlinanTeklifSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Cari | Cari Güncelle V2 Update | `POST` | `/API/APIMethods/CariGuncelleV2` | top: Mikro; Mikro: CalismaYili, cariler, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.cariler[] | Mevcut kayit/evrak guncelleme |
| Cari | Cari Kaydet V2 Save | `POST` | `/API/APIMethods/CariKaydetV2` | top: Mikro; Mikro: CalismaYili, cariler, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.cariler.adres.yetkili[], Mikro.cariler.adres[], Mikro.cariler[] | Yeni kayit/evrak olusturma |
| Dahili Stok Hareket | Dahili Stok Hareket Düzelt V2 Update | `POST` | `/Api/apiMethods/DahiliStokHareketDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Dahili Stok Hareket | Dahili Stok Hareket Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/DahiliStokHareketDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Dahili Stok Hareket | Dahili Stok Hareket Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/DahiliStokHareketGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| Dahili Stok Hareket | Dahili Stok Hareket Kaydet V2 Save | `POST` | `/Api/apiMethods/DahiliStokHareketKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Dahili Stok Hareket | Dahili Stok Hareket Kaydet V2 Save Copy | `POST` | `/Api/apiMethods/DahiliStokHareketKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Dahili Stok Hareket | Dahili Stok Hareket Sil V2 Delete | `POST` | `/Api/apiMethods/DahiliStokHareketSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Dekont | Bankalar Arası Virman Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Dekont | Borç Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Dekont | Borç Dekontu Kaydet V2 Save Copy | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Dekont | Cari Borç Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Dekont | Cari Hesaplar Arası Virman Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Dekont | Dekont Sil V2 Delete | `POST` | `/Api/apiMethods/DekontSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Dekont | Genel Amaçlı Virman Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Dekont | Kasalar Arası Virman Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Depolar Arası Sipariş | Depolar Arası Sipariş Düzelt V2 Update | `POST` | `/Api/apiMethods/DepolarArasiSiparisDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Depolar Arası Sipariş | Depolar Arası Sipariş Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/DepolarArasiSiparisDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Depolar Arası Sipariş | Depolar Arası Sipariş Guid sil V2 Delete Guid | `POST` | `/Api/apiMethods/DepolarArasiSiparisGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| Depolar Arası Sipariş | Depolar Arası Sipariş Kaydet V2 Save | `POST` | `/Api/apiMethods/DepolarArasiSiparisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Depolar Arası Sipariş | Depolar Arası Sipariş Sil V2 Delete | `POST` | `/Api/apiMethods/DepolarArasiSiparisSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Etiket Basım Kaydet | Etiket Basım Kaydet V2 Save | `POST` | `/Api/apiMethods/EtiketBasimKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Evrak Açıklamaları | Evrak Açıklama Düzelt V2 Update | `POST` | `/Api/apiMethods/EvrakAciklamaDuzeltV2` | top: Mikro; Mikro: CalismaYili, evrak_aciklamalari, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evrak_aciklamalari[] | Mevcut kayit/evrak guncelleme |
| Evrak Açıklamaları | Evrak Açıklama Kaydet V2 Save | `POST` | `/Api/apiMethods/EvrakAciklamaKaydetV2` | top: Mikro; Mikro: CalismaYili, evrak_aciklamalari, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evrak_aciklamalari[] | Yeni kayit/evrak olusturma |
| Evrak Açıklamaları | Evrak Açıklama Sil V2 Delete | `POST` | `/Api/apiMethods/EvrakAciklamaSilV2` | top: Mikro; Mikro: CalismaYili, evrak_aciklamalari, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evrak_aciklamalari[] | Kayit/evrak silme |
| Evrak Belge Resim | Evrak Belge Resim Kaydet V2 Save | `POST` | `/Api/apiMethods/EvrakBelgeResimKaydetV2` | top: Mikro; Mikro: CalismaYili, evrak_resimleri, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evrak_resimleri[] | Yeni kayit/evrak olusturma |
| Evrak Belge Resim | Evrak Belge Resim Sil V2 Delete | `POST` | `/Api/apiMethods/EvrakBelgeResimSilV2` | top: Mikro; Mikro: CalismaYili, evrak_resimleri, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evrak_resimleri[] | Kayit/evrak silme |
| Fiyat Değişikliği | Fiyat Değişikliği Kaydet V2 Save | `POST` | `/Api/apiMethods/FiyatDegisikligiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Image Data | ImageDataGetirV2 | `POST` | `/API/APIMethods/ImageDataGetirV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Image, KullaniciKodu, Sifre | Listeleme veya sorgulama |
| Image Data | ImageDataKaydetV2 | `POST` | `/API/APIMethods/ImageDataKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Image, KullaniciKodu, Sifre | Listeleme veya sorgulama |
| Image Data | ImageDataSilV2 | `POST` | `/API/APIMethods/ImageDataSilV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Image, KullaniciKodu, Sifre | Listeleme veya sorgulama |
| İrsaliye | Irsaliye Düzelt Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/IrsaliyeDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| İrsaliye | Irsaliye Düzelt V2 Update | `POST` | `/Api/apiMethods/IrsaliyeDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| İrsaliye | Irsaliye Satır Sil V2 Delete Guid | `POST` | `/Api/apiMethods/IrsaliyeSatirSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| İrsaliye | Irsaliye Sil V2 Delete | `POST` | `/Api/apiMethods/IrsaliyeSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar[] | Kayit/evrak silme |
| İrsaliye | IrsaliyeKaydet V2 (İhracat Satış ) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| İrsaliye | IrsaliyeKaydet V2 (İhraç Kayıtlı İade Alış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| İrsaliye | IrsaliyeKaydet V2 (İhraç Kayıtlı Mal Satış ) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| İrsaliye | IrsaliyeKaydet V2 (Perakende Alış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| İrsaliye | IrsaliyeKaydet V2 (Perakende İade Alış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| İrsaliye | IrsaliyeKaydet V2 (Perakende İade Çıkış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| İrsaliye | IrsaliyeKaydet V2 (Perakende Satış ) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| İrsaliye | IrsaliyeKaydet V2 (Toptan Alış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| İrsaliye | IrsaliyeKaydet V2 (Toptan İade Alış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| İrsaliye | IrsaliyeKaydet V2 (Toptan İade Çıkış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| İrsaliye | IrsaliyeKaydet V2 (Toptan Satış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| İrsaliye | IrsaliyeKaydet V2 (Toptan Satış) Save Copy | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: ApiKey, CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| İrsaliye | Siparişten İrsaliye Oluşturma V2 Save | `POST` | `/api/APIMethods/SiparistenIrsaliyeOlusturmaV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Kasa Masraf Fişi | Kasa Masraf Fişi Kaydet V2 Save | `POST` | `/Api/apiMethods/KasaMasrafFisiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Kayıt Kaydet | Kayıt Kaydet V2 Delete | `POST` | `/Api/apiMethods/KayitKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Kayit, KullaniciKodu, Sifre, Tablo; arrays: Mikro.Kayit[], Mikro.Tablo[] | Kayit/evrak silme |
| Kayıt Kaydet | Kayıt Kaydet V2 Save | `POST` | `/Api/apiMethods/KayitKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Kayit, KullaniciKodu, Sifre, Tablo; arrays: Mikro.Kayit[], Mikro.Tablo[] | Yeni kayit/evrak olusturma |
| Kayıt Kaydet | Kayıt Kaydet V2 Update | `POST` | `/Api/apiMethods/KayitKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Kayit, KullaniciKodu, Sifre, Tablo; arrays: Mikro.Kayit[], Mikro.Tablo[] | Mevcut kayit/evrak guncelleme |
| Listeler | Cari Listesi V2 | `POST` | `/Api/APIMethods/CariListesiV2` | top: FieldName, Index, Mikro, Size, Sort, WhereStr; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Listeleme veya sorgulama |
| Listeler | Cari Listesi V3 | `POST` | `/Api/APIMethods/CariListesiV3` | top: CariKod, CariVKNTCNo, IlkTarih, Index, Mikro, Size, SonTarih, Sort, TarihTipi; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Listeleme veya sorgulama |
| Listeler | KullaniciListesiV2 | `POST` | `/Api/APIMethods/KullaniciListesiV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Listeleme veya sorgulama |
| Listeler | KullaniciParametreleriV2 | `POST` | `/Api/APIMethods/KullaniciParametreleriV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Collection icindeki Mikro API islemi |
| Listeler | ModoFastSellHızlıSatisOnayDurumV2 | `POST` | `/Api/APIMethods/ModoFastsellHSSozlesmesiOnayDurumV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Collection icindeki Mikro API islemi |
| Listeler | ModoFastSellRestoranOnayDurumV2 | `POST` | `/Api/APIMethods/ModoFastsellRestoranSozlesmesiOnayDurumV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Collection icindeki Mikro API islemi |
| Listeler | PorkodSozlesmeOnayDurumV2 | `POST` | `/Api/APIMethods/PorkodSozlesmeOnayDurumV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Collection icindeki Mikro API islemi |
| Listeler | Stok Listesi V2 | `POST` | `/Api/APIMethods/StokListesiV2` | top: IlkTarih, Index, Mikro, Size, SonTarih, Sort, StokKod, TarihTipi; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Listeleme veya sorgulama |
| Listeler | VergiListesiV2 | `POST` | `/Api/APIMethods/VergiListesiV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Listeleme veya sorgulama |
| Login-Logoff | APILogin | `POST` | `/Api/APIMethods/APILogin` | top: ApiKey, CalismaYili, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo | Oturum acma veya API kullanicisi dogrulama |
| Login-Logoff | HealthCheck | `GET` | `/Api/APIMethods/HealthCheck` | Body yok | Okuma/saglik kontrolu |
| Login-Logoff | HealthCheck2 | `GET` | `/Api/APIMethods/HealthCheck2` | Body yok | Okuma/saglik kontrolu |
| Login-Logoff | LoggerDone-Get | `GET` | `/Api/APIMethods/LoggerDone` | JSON body | Okuma/saglik kontrolu |
| Login-Logoff | Logoff | `POST` | `/Api/apiMethods/APILogoff` | top: KullaniciKodu | Oturumu kapatma |
| Login-Logoff | Logoff V2 | `POST` | `/Api/apiMethods/APILogoffV2` | top: KullaniciKodu, Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Oturumu kapatma |
| Login-Logoff | MikroApiUp | `POST` | `/Api/APIMethods/APILogin` | top: CalismaYili, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo | Oturum acma veya API kullanicisi dogrulama |
| Muhasebe | Dövizli Muhasebe Fişi Kaydet V2 Save | `POST` | `/Api/apiMethods/MuhasebeFisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Muhasebe | Muhasebe Fişi Kaydet V2 Save | `POST` | `/Api/apiMethods/MuhasebeFisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.fis_detay[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Muhasebe | Muhasebe Fişi Sil V2 Delete | `POST` | `/Api/apiMethods/MuhasebeFisSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Muhasebe | Özel Mahsup Fişi Kaydet V2 Save | `POST` | `/Api/apiMethods/MuhasebeFisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Operasyon Tamamlama Fişi | Operasyon Tamamlama Fişi Sil V2 Delete | `POST` | `/Api/apiMethods/OperasyonTamamlamaFisSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Operasyon Tamamlama Fişi | Operasyon Tamamlama Fşi Kaydet V2 Save | `POST` | `/Api/apiMethods/OperasyonTamamlamaFisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.calisan_listesi[], Mikro.evraklar.satirlar.gecikme_listesi[], Mikro.evraklar.satirlar.hata_listesi[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Personel | Personel izin kaydet V2 Save | `POST` | `/API/APIMethods/PersonelizinKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, personelizinler, Sifre; arrays: Mikro.personelizinler[] | Yeni kayit/evrak olusturma |
| Personel | Personel Kaydet V2 Save | `POST` | `/API/APIMethods/PersonelKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, personeller, Sifre; arrays: Mikro.personeller[] | Yeni kayit/evrak olusturma |
| Proforma Sipariş | Proforma Sipariş Kaydet V2 Save | `POST` | `/Api/apiMethods/ProformaSiparisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Proforma Sipariş | Proforma Sipariş Sil V2 Delete | `POST` | `/Api/apiMethods/ProformaSiparisSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Satın Alma Talep | Satın Alma Talep Sil V2 Delete | `POST` | `/api/APIMethods/SatinAlmaTalepSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Satın Alma Talep | Satın Alma Talep V2 Save | `POST` | `/api/APIMethods/SatinAlmaTalepKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Satış Şartı | Satış Şartı Düzelt V2 Update | `POST` | `/api/APIMethods/SatisSartiDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Satış Şartı | Satış Şartı Guid Ekle V2 Add Guid | `POST` | `/api/APIMethods/SatisSartiDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Satış Şartı | Satış Şartı Guid Sil V2 Delete Guid | `POST` | `/api/APIMethods/SatisSartiGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| Satış Şartı | Satış Şartı Kaydet V2 Save | `POST` | `/api/APIMethods/SatisSartiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Satış Şartı | Satış Şartı Sil V2 Delete | `POST` | `/api/APIMethods/SatisSartiSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Satin Alma Şartı | Satın Alma Şartı Kaydet V2 Save | `POST` | `/api/APIMethods/SatinAlmaSartiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Satin Alma Şartı | Satın Alma Şartı Sil V2 Delete | `POST` | `/api/APIMethods/SatinAlmaSartiSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Sayım Sonuç Kaydet | Sayım Sonuç Düzelt V2 Update | `POST` | `/Api/apiMethods/SayimSonuclariDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Sayım Sonuç Kaydet | Sayım Sonuç Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/SayimSonuclariDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Sayım Sonuç Kaydet | Sayım Sonuç Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/SayimSonuclariSatirSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| Sayım Sonuç Kaydet | Sayım Sonuç Kaydet V2 Save | `POST` | `/Api/apiMethods/SayimSonuclariKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Sayım Sonuç Kaydet | Sayım Sonuç Sil V2 Delete | `POST` | `/Api/apiMethods/SayimSonuclariSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Sipariş | Konsinye Sipariş Kaydet V2 Save | `POST` | `/api/APIMethods/SiparisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Sipariş | Normal Alınan Sipariş Kaydet V2 Save | `POST` | `/api/APIMethods/SiparisKaydetV2` | Raw body var, JSON parse edilemedi | Yeni kayit/evrak olusturma |
| Sipariş | Sipariş Düzelt V2 Update | `POST` | `/Api/apiMethods/SiparisDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Sipariş | Sipariş Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/SiparisDuzeltV2` | Raw body var, JSON parse edilemedi | Mevcut kayit/evrak guncelleme |
| Sipariş | Sipariş Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/SiparisGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| Sipariş | Sipariş Kaydet V2 Save | `POST` | `/api/APIMethods/SiparisKaydetV2` | Raw body var, JSON parse edilemedi | Yeni kayit/evrak olusturma |
| Sipariş | Sipariş Sil V2 Delete | `POST` | `/Api/apiMethods/SiparisSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Stok | Stok Kaydet V2 Save | `POST` | `/API/APIMethods/StokKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre, stoklar; arrays: Mikro.stoklar.barkodlar[], Mikro.stoklar.satis_fiyatlari[], Mikro.stoklar[] | Yeni kayit/evrak olusturma |
| Tahsilat Tediye | Tahsilat Tediye Çek Çıkış Bordrosu Kaydet Save | `POST` | `/Api/apiMethods/TahsilatTediyeSCKaydet` | top: Mikro; Mikro: evraklar; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Tahsilat Tediye | Tahsilat Tediye Çek Giriş Bordrosu Kaydet Save | `POST` | `/Api/apiMethods/TahsilatTediyeSCKaydet` | top: Mikro; Mikro: evraklar; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Tahsilat Tediye | Tahsilat Tediye Düzelt V2 Update | `POST` | `/Api/apiMethods/TahsilatTediyeDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Tahsilat Tediye | Tahsilat Tediye Giden Havale Kaydet Save | `POST` | `/Api/apiMethods/TahsilatTediyeSCKaydet` | top: Mikro; Mikro: evraklar; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Tahsilat Tediye | Tahsilat Tediye Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/TahsilatTediyeGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| Tahsilat Tediye | Tahsilat Tediye Kaydet V2 Save | `POST` | `/Api/apiMethods/TahsilatTediyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Tahsilat Tediye | Tahsilat Tediye Kaydet V2 Save Copy | `POST` | `/Api/apiMethods/TahsilatTediyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Tahsilat Tediye | Tahsilat Tediye Kaydet V3 Çek Giriş Save | `POST` | `/Api/apiMethods/TahsilatTediyeKaydetV3` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Tahsilat Tediye | Tahsilat Tediye Kaydet V3 Save | `POST` | `/Api/apiMethods/TahsilatTediyeKaydetV3` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Tahsilat Tediye | Tahsilat Tediye Senet Çıkış Bordrosu Kaydet Save | `POST` | `/Api/apiMethods/TahsilatTediyeSCKaydet` | top: Mikro; Mikro: evraklar; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Tahsilat Tediye | Tahsilat Tediye Senet Giriş Bordrosu Kaydet Save | `POST` | `/Api/apiMethods/TahsilatTediyeSCKaydet` | top: Mikro; Mikro: evraklar; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Tahsilat Tediye | Tahsilat Tediye Sil V2 Delete | `POST` | `/Api/apiMethods/TahsilatTediyeSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Üretim İş Emri | Üretim İş Emri Oluştur V2 Save | `POST` | `/API/APIMethods/UretimIsEmriOlusturV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Satirlar, Sifre; arrays: Mikro.Satirlar[] | Yeni kayit/evrak olusturma |
| Üretim Talep | Üretim Talep Guid Sil V2 Delete Guid | `POST` | `/Api/APIMethods/UretimTalepGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| Üretim Talep | Üretim Talep Kaydet V2 Save | `POST` | `/Api/APIMethods/UretimTalepKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Üretim Talep | Üretim Talep Sil V2 Delete | `POST` | `/Api/APIMethods/UretimTalepSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Ürün Reçete | Ürün Reçete Kaydet V2 Save | `POST` | `/Api/apiMethods/UrunReceteKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.recete_kriterler[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Ürün Reçete | Ürün Reçete Sil V2 Delete | `POST` | `/Api/apiMethods/UrunReceteSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Ürün Rota | Ürün Rota Kaydet V2 Save | `POST` | `/Api/apiMethods/UrunRotaKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.rota_detaylar[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Ürün Rota | Ürün Rota Sil V2 Delete | `POST` | `/Api/apiMethods/UrunRotaSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Ürün Rota Plan | Ürün Rota Plan Kaydet V2 Save | `POST` | `/Api/apiMethods/UretimRotaPlanKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Ürün Rota Plan | Ürün Rota Plan Sil V2 Delete | `POST` | `/Api/apiMethods/UretimRotaPlanSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| Verilen Teklif | Verilen Teklif Düzelt V2 Update | `POST` | `/Api/apiMethods/VerilenTeklifDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Verilen Teklif | Verilen Teklif Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/VerilenTeklifDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| Verilen Teklif | Verilen Teklif Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/VerilenTeklifGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| Verilen Teklif | Verilen Teklif Kaydet V2 Save | `POST` | `/Api/apiMethods/VerilenTeklifKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| Verilen Teklif | Verilen Teklif Sil V2 Delete | `POST` | `/Api/apiMethods/VerilenTeklifSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |

## Endpoint Detaylari

### Adres

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Adres Duzelt V2 Update | `POST` | `/API/APIMethods/AdresDuzeltV2` | top: Mikro; Mikro: adresler, CalismaYili, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.adresler[] | Mevcut kayit/evrak guncelleme |
| 2 | Adres kaydet V2 Save | `POST` | `/API/APIMethods/AdresKaydetV2` | top: Mikro; Mikro: adresler, CalismaYili, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.adresler[] | Yeni kayit/evrak olusturma |
| 3 | Adres Sil V2 Delete | `POST` | `/API/APIMethods/AdresSilV2` | top: Mikro; Mikro: adresler, CalismaYili, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.adresler[] | Kayit/evrak silme |

#### Adres Duzelt V2 Update

- Method: `POST`
- Path: `/API/APIMethods/AdresDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: adresler, CalismaYili, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.adresler[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "adresler": [
            {
                "adr_Guid":"FE7B2F3B-2257-450A-83BA-B6974EDE43A5",                
                "adr_cadde": "cadde3",
                "adr_mahalle": "mahalle3",
                "adr_sokak": "sokak3"
            }
        ]
    }
}
```

#### Adres kaydet V2 Save

- Method: `POST`
- Path: `/API/APIMethods/AdresKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: adresler, CalismaYili, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.adresler[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "adresler": [
            {
                "adr_cari_kod": "CR01",      
                "adr_cadde": "cadde1",
                "adr_mahalle": "mahalle1",
                "adr_sokak": "sokak1",
                "adr_Semt": "semt1",
                "adr_Apt_No": "A1",
                "adr_Daire_No": "2",
                "adr_posta_kodu": 34340,
                "adr_ilce": "Sarıyer",
                "adr_il": "İstanbul",
                "adr_ulke": "TÜRKİYE",
                "adr_tel_ulke_kodu": "090",
                "adr_tel_bolge_kodu": "212",
                "adr_tel_no1": "4444444",
                "adr_tel_no2": "",
                "adr_tel_faxno": ""
            },
            {
                "adr_cari_kod": "CR01",
                "adr_cadde": "cadde1",
                "adr_mahalle": "mahalle1",
                "adr_sokak": "sokak1",
                "adr_Semt": "semt1",
                "adr_Apt_No": "A1",
                "adr_Daire_No": "2",
                "adr_posta_kodu": 34340,
                "adr_ilce": "Sarıyer",
                "adr_il": "İstanbul",
                "adr_ulke": "TÜRKİYE",
                "adr_tel_ulke_kodu": "090",
                "adr_tel_bolge_kodu": "212",
                "adr_tel_no1": "4444444",
                "adr_tel_no2": "",
                "adr_tel_faxno": ""
            }
        ]
    }
}
```

#### Adres Sil V2 Delete

- Method: `POST`
- Path: `/API/APIMethods/AdresSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: adresler, CalismaYili, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.adresler[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "adresler": [
            {
                "adr_Guid":"027C2F3B-2257-450A-83BA-B6974EDE43A5"
            }
        ]
    }
}
```

### Alım Satım Evrağı - Fatura

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Alım Satım Evrağı Düzeltme V2 Update | `POST` | `/Api/apiMethods/AlimSatimEvragiDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay.renk_beden[], Mikro.evraklar.detay[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 2 | Alım Satım Evrağı Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/AlimSatimEvragiSatirSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar[] | GUID ile satir/kayit silme |
| 3 | Alım Satım Evrağı Kaydet V2 Alış Faturası Save | `POST` | `/Api/apiMethods/AlimSatimEvragiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay.renk_beden[], Mikro.evraklar.detay.user_tablo[], Mikro.evraklar.detay[], Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.user_tablo[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 4 | Alım Satım Evrağı Kaydet V2 Hizmet Save | `POST` | `/Api/apiMethods/AlimSatimEvragiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.user_tablo[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 5 | Alım Satım Evrağı Kaydet V2 Masraf Save | `POST` | `/Api/apiMethods/AlimSatimEvragiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.user_tablo[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 6 | Alım Satım Evrağı Kaydet V2 Save | `POST` | `/Api/apiMethods/AlimSatimEvragiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.detay.renk_beden[], Mikro.evraklar.detay.user_tablo[], Mikro.evraklar.detay[], Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.user_tablo[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 7 | Alım Satım Evrağı Satır Ekle V2 Add Guid | `POST` | `/Api/apiMethods/AlimSatimEvragiDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay.renk_beden[], Mikro.evraklar.detay[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 8 | Alım Satım Evrağı Satır Ekle V2 Add Guid Copy | `POST` | `/Api/apiMethods/AlimSatimEvragiDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay.user_tablo[], Mikro.evraklar.detay[], Mikro.evraklar.ebelge_detay[], Mikro.evraklar.odemeler[], Mikro.evraklar.user_tablo[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 9 | Alım Satım Evrağı Satır Sil V2 Delete Guid | `POST` | `/Api/apiMethods/AlimSatimEvragiSatirSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| 10 | Alım Satım Evrağı Sil V2 Delete | `POST` | `/Api/apiMethods/AlimSatimEvragiSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar[] | Kayit/evrak silme |
| 11 | Fatura Kaydet V2 Save | `POST` | `/Api/apiMethods/FaturaKaydetV2` | Raw body var, JSON parse edilemedi | Yeni kayit/evrak olusturma |
| 12 | Fatura Kaydet V2 Save Copy | `POST` | `/Api/apiMethods/FaturaKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay.user_tablo[], Mikro.evraklar.detay[], Mikro.evraklar.ebelge_detay[], Mikro.evraklar.odemeler[], Mikro.evraklar.user_tablo[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 13 | Fatura Kaydet V3 Save | `POST` | `/api/APIMethods/FaturaKaydetV3` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay[], Mikro.evraklar.ebelge_detay[], Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 14 | Fatura Kaydet V3 Save Copy | `POST` | `/api/APIMethods/FaturaKaydetV3` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 15 | Fatura PDF V2 | `POST` | `/API/APIMethods/FaturaPdfV2` | top: Mikro; Mikro: CalismaYili, Fatura_Guid, FirmaKodu, KullaniciKodu, Sifre | Collection icindeki Mikro API islemi |

#### Alım Satım Evrağı Düzeltme V2 Update

- Method: `POST`
- Path: `/Api/apiMethods/AlimSatimEvragiDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay.renk_beden[], Mikro.evraklar.detay[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "cha_evrak_tip": 63,
                "cha_evrakno_seri": "AS",
                "cha_evrakno_sira": 1,
                "cha_satir_no": 0,
                "cha_tarihi": "21.12.2023",
                "detay": [
                    {
                        "sth_Guid": "F625C73E-53DB-49EC-93F1-95BEAD6A3FDA",
                        "sth_stok_kod": "SK04",
                        "sth_miktar": 10,
                        "seriler": "",
                        "renk_beden": [
                        {
                            "renk_no": 1,
                            "beden_no": 1,
                            "miktar": 6
                        },
                        {
                            "renk_no": 2,
                            "beden_no": 1,
                            "miktar": 4
                        }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Alım Satım Evrağı Guid Sil V2 Delete Guid

- Method: `POST`
- Path: `/Api/apiMethods/AlimSatimEvragiSatirSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: GUID ile satir/kayit silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "cha_Guid": "F125C73E-53DB-49EC-93F1-95BEAD6A3FDA"
            }
        ]
    }
}
```

#### Alım Satım Evrağı Kaydet V2 Alış Faturası Save

- Method: `POST`
- Path: `/Api/apiMethods/AlimSatimEvragiKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay.renk_beden[], Mikro.evraklar.detay.user_tablo[], Mikro.evraklar.detay[], Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.user_tablo[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "cha_tarihi": "21.12.2023",
                "cha_tip": "1",
                "cha_cinsi": "6",
                "cha_normal_Iade": 0,
                "cha_evrak_tip": "0",
                "cha_evrakno_seri": "AF",
                "cha_cari_cins": "0",
                "cha_kod": "CR01",
                "cha_d_kurtar": null,
                "cha_d_cins": "0",
                "cha_d_kur": "1",
                "cha_ciro_cari_kodu": "CR01",
                "cha_tpoz": "0",
                "cha_kasa_hizkod": "",
                "cha_kasa_hizmet": "0",
                "cha_miktari": "0",
                "cha_aratoplam": 0,
                "cha_vergipntr": 0.0,
                "cha_ft_iskonto1": 0.0,
                "cha_isk_mas1": "0",
                "cha_satici_kodu": "",
                "cha_srmrkkodu": "",
                "cha_projekodu": "",
                "cha_aciklama": "",
                "cha_EArsiv_unvani_ad": "",
                "cha_EArsiv_unvani_soyad": "",
                "cha_EArsiv_daire_adi": "",
                "cha_EArsiv_Vkn": "",
                "cha_EArsiv_ulke": "",
                "cha_EArsiv_Il": "",
                "cha_EArsiv_tel_ulke_kod": "",
                "cha_EArsiv_tel_bolge_kod": "",
                "cha_EArsiv_tel_no": "",
                "cha_EArsiv_mail": "",
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1aaa"
                    },
                    {
                        "aciklama": "Test2ş"
                    },
                    {
                        "aciklama": "Test3ğ"
                    },
                    {
                        "aciklama": "Test4ç"
                    }
                ],
                "user_tablo": [
                    {
                        "SubDealer": "1",
                        "CreditRelationCustomer": "2",
                        "CreditReferenceNumber": "3",
                        "WebSupportCustomer": "4",
                        "RegisteredEMailAccount": "5",
                        "WebSupportStartDate": "21.10.2019",
                        "RentalCustomer": "7",
                        "DetailDescription1": "9",
                        "DetailDescription2": "10"
                    }
                ],
                "detay": [
                    {
                        "sth_tarih": "21.12.2023",
                        "sth_tip": "0",
                        "sth_cins": "0",
                        "sth_normal_iade": "0",
                        "sth_evraktip": "3",
                        "sth_stok_kod": "SK02",
                        "sth_cari_cinsi": "0",
                        "sth_cari_kodu": "CR01",
                        "sth_miktar": 200.0,
                        "sth_birim_pntr": 1,
                        "sth_tutar": 1500,
                        "sth_vergi_pntr": 4,
                        "sth_vergi": 270,
                        "sth_vergisiz_fl": false,
                        "sth_iskonto1": 0.0,
                        "sth_iskonto2": 0.0,
                        "sth_giris_depo_no": 1,
                        "sth_cikis_depo_no": 1,
                        "sth_plasiyer_kodu": "",
                        "sth_stok_srm_merkezi": "",
                        "sth_cari_srm_merkezi": "",
                        "sth_proje_kodu": "",
                        "seriler": "",
                        "renk_beden": [
                            {
                                "renk_kirilim_kodu": "Yeşil",
                                "beden_kirilim_kodu": "L",
                                "miktar": 10
                            },
                            {
                                "renk_no": 1,
                                "beden_no": 2,
                                "miktar": 150
                            },
                            {
                                "renk_no": 2,
                                "beden_no": 1,
                                "miktar": 40
                            }
                        ],
                        "user_tablo": [
                            {
                                "aciklama": "test stok hareket user tablo"
                            }
                        ]
                    },
                    {
                        "sth_tarih": "21.12.2023",
                        "sth_tip": "0",
                        "sth_cins": "0",
                        "sth_normal_iade": "0",
                        "sth_evraktip": "3",
                        "sth_stok_kod": "SK02",
                        "sth_cari_cinsi": "0",
                        "sth_cari_kodu": "CR01",
                        "sth_miktar": 1500,
                        "sth_birim_pntr": 1,
                        "sth_tutar": 4500,
                        "sth_vergi_pntr": 4,
                        "sth_vergi": 810,
                        "sth_vergisiz_fl": false,
                        "sth_iskonto1": 0.0,
                        "sth_iskonto2": 0.0,
                        "sth_giris_depo_no": 1,
                        "sth_cikis_depo_no": 1,
                        "sth_plasiyer_kodu": "",
                        "sth_stok_srm_merkezi": "",
                        "sth_cari_srm_merkezi": "",
                        "sth_proje_kodu": "",
                        "seriler": "",
                        "user_tablo": [
                            {
                                "aciklama": "test stok hareket user tablo"
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Alım Satım Evrağı Kaydet V2 Hizmet Save

- Method: `POST`
- Path: `/Api/apiMethods/AlimSatimEvragiKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.user_tablo[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "cha_tarihi": "22.01.2024",
        "cha_tip": "0",
        "cha_cinsi": "8",
        "cha_normal_Iade": "0",
        "cha_evrak_tip": "63",
        "cha_evrakno_seri": "HIZ",        
        "cha_cari_cins": "0",
        "cha_kod": "CR01",
        "cha_d_kurtar": null,
        "cha_d_cins": "0",
        "cha_d_kur": "1",
        "cha_ciro_cari_kodu": "CR01",
        "cha_tpoz": "0",				        
        "cha_kasa_hizkod": "HZM01",
        "cha_kasa_hizmet": "3",
        "cha_miktari": "0",
        "cha_aratoplam": 0,
        "cha_vergipntr": 4,
        "cha_ft_iskonto1": 0.0,
        "cha_isk_mas1": "0",
        "cha_satici_kodu": "",
        "cha_pos_hareketi": 0,
        "cha_srmrkkodu": "",
        "cha_projekodu": "",
        "cha_aciklama": "",
        "cha_EArsiv_unvani_ad": "",
        "cha_EArsiv_unvani_soyad": "",
        "cha_EArsiv_daire_adi": "",
        "cha_EArsiv_Vkn": "",
        "cha_EArsiv_ulke": "",
        "cha_EArsiv_Il": "",
        "cha_EArsiv_tel_ulke_kod": "",
        "cha_EArsiv_tel_bolge_kod": "",
        "cha_EArsiv_tel_no": "",
        "cha_EArsiv_mail": "",
				"evrak_aciklamalari": [
                            {
                            "aciklama": "Test1aaa"
                            },
                            {
                            "aciklama": "Test2ş"
                            },
                            {
                            "aciklama": "Test3ğ"
                            },
                            {
                            "aciklama": "Test4ç"
                            }
                        ],
        "user_tablo": [
          {
            "SubDealer": "1",
            "CreditRelationCustomer": "2",
            "CreditReferenceNumber": "3",
            "WebSupportCustomer": "4",
            "RegisteredEMailAccount": "5",
            "WebSupportStartDate": "21.10.2019",
            "RentalCustomer": "7",
            "DetailDescription1": "9",
            "DetailDescription2": "10"
          }
        ]
      },
      {
        "cha_tarihi": "22.01.2024",
        "cha_tip": "0",
        "cha_cinsi": "8",
        "cha_normal_Iade": "0",
        "cha_evrak_tip": "63",
        "cha_evrakno_seri": "HIZ",        
        "cha_cari_cins": "0",
        "cha_kod": "CR01",
        "cha_d_kurtar": null,
        "cha_d_cins": "0",
        "cha_d_kur": "1",
        "cha_ciro_cari_kodu": "CR01",
        "cha_tpoz": "0",				        
        "cha_kasa_hizkod": "HZM02",
        "cha_kasa_hizmet": "3",
        "cha_miktari": "0",
        "cha_aratoplam": 0,
        "cha_vergipntr": 4,
        "cha_ft_iskonto1": 0.0,
        "cha_isk_mas1": "0",
        "cha_satici_kodu": "",
        "cha_pos_hareketi": 0,
        "cha_srmrkkodu": "",
        "cha_projekodu": "",
        "cha_aciklama": "",
        "cha_EArsiv_unvani_ad": "",
        "cha_EArsiv_unvani_soyad": "",
        "cha_EArsiv_daire_adi": "",
        "cha_EArsiv_Vkn": "",
        "cha_EArsiv_ulke": "",
        "cha_EArsiv_Il": "",
        "cha_EArsiv_tel_ulke_kod": "",
        "cha_EArsiv_tel_bolge_kod": "",
        "cha_EArsiv_tel_no": "",
        "cha_EArsiv_mail": "",
				"evrak_aciklamalari": [
                            {
                            "aciklama": "Test1aaa"
                            },
                            {
                            "aciklama": "Test2ş"
                            },
                            {
                            "aciklama": "Test3ğ"
                            },
                            {
                            "aciklama": "Test4ç"
                            }
                        ],
        "user_tablo": [
          {
            "SubDealer": "1",
            "CreditRelationCustomer": "2",
            "CreditReferenceNumber": "3",
            "WebSupportCustomer": "4",
            "RegisteredEMailAccount": "5",
            "WebSupportStartDate": "21.10.2019",
            "RentalCustomer": "7",
            "DetailDescription1": "9",
            "DetailDescription2": "10"
          }
        ]
      }
    ]
  }
}
```

#### Alım Satım Evrağı Kaydet V2 Masraf Save

- Method: `POST`
- Path: `/Api/apiMethods/AlimSatimEvragiKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.user_tablo[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "cha_tarihi": "15.02.2024",
        "cha_tip": "0",
        "cha_cinsi": "8",
        "cha_normal_Iade": "0",
        "cha_evrak_tip": "63",
        "cha_evrakno_seri": "MAS",        
        "cha_cari_cins": "0",
        "cha_kod": "CR01",
        "cha_d_kurtar": null,
        "cha_d_cins": "0",
        "cha_d_kur": "1",
        "cha_ciro_cari_kodu": "CR01",
        "cha_tpoz": "0",				        
        "cha_kasa_hizkod": "MS01",
        "cha_kasa_hizmet": "5",
        "cha_miktari": "1",
        "cha_aratoplam": 500,
        "cha_vergipntr": 4,
        "cha_ft_iskonto1": 0.0,
        "cha_isk_mas1": "0",
        "cha_satici_kodu": "",
        "cha_pos_hareketi": 0,
        "cha_srmrkkodu": "",
        "cha_projekodu": "",
        "cha_aciklama": "",
        "cha_EArsiv_unvani_ad": "",
        "cha_EArsiv_unvani_soyad": "",
        "cha_EArsiv_daire_adi": "",
        "cha_EArsiv_Vkn": "",
        "cha_EArsiv_ulke": "",
        "cha_EArsiv_Il": "",
        "cha_EArsiv_tel_ulke_kod": "",
        "cha_EArsiv_tel_bolge_kod": "",
        "cha_EArsiv_tel_no": "",
        "cha_EArsiv_mail": "",
				"evrak_aciklamalari": [
                            {
                            "aciklama": "Test1aaa"
                            },
                            {
                            "aciklama": "Test2ş"
                            },
                            {
                            "aciklama": "Test3ğ"
                            },
                            {
                            "aciklama": "Test4ç"
                            }
                        ],
        "user_tablo": [
          {
            "SubDealer": "1",
            "CreditRelationCustomer": "2",
            "CreditReferenceNumber": "3",
            "WebSupportCustomer": "4",
            "RegisteredEMailAccount": "5",
            "WebSupportStartDate": "21.10.2019",
            "RentalCustomer": "7",
            "DetailDescription1": "9",
            "DetailDescription2": "10"
          }
        ]
      }
    ]
  }
}
```

#### Alım Satım Evrağı Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/AlimSatimEvragiKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.detay.renk_beden[], Mikro.evraklar.detay.user_tablo[], Mikro.evraklar.detay[], Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.user_tablo[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu": "MIKROFLY",
    "CalismaYili": 2023,
    "KullaniciKodu": "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "cha_tarihi": "22.01.2024",
        "cha_tip": "0",
        "cha_cinsi": "6",
        "cha_normal_Iade": 0,
        "cha_evrak_tip": "63",
        "cha_evrakno_seri": "AS",
        "cha_evrakno_sira": 0,
        "cha_cari_cins": "0",
        "cha_kod": "CR01",
        "cha_d_kurtar": null,
        "cha_d_cins": "0",
        "cha_d_kur": "1",
        "cha_ciro_cari_kodu": "CR01",
        "cha_tpoz": "0",
        "cha_kasa_hizkod": "",
        "cha_kasa_hizmet": "0",
        "cha_miktari": "0",
        "cha_aratoplam": 0,
        "cha_vergipntr": 0.0,
        "cha_ft_iskonto1": 0.0,
        "cha_isk_mas1": "0",
        "cha_satici_kodu": "",
        "cha_pos_hareketi": 3,
        "cha_srmrkkodu": "",
        "cha_projekodu": "",
        "cha_aciklama": "",
        "cha_EArsiv_unvani_ad": "",
        "cha_EArsiv_unvani_soyad": "",
        "cha_EArsiv_daire_adi": "",
        "cha_EArsiv_Vkn": "",
        "cha_EArsiv_ulke": "",
        "cha_EArsiv_Il": "",
        "cha_EArsiv_tel_ulke_kod": "",
        "cha_EArsiv_tel_bolge_kod": "",
        "cha_EArsiv_tel_no": "",
        "cha_EArsiv_mail": "",
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1aaa"
          },
          {
            "aciklama": "Test2ş"
          },
          {
            "aciklama": "Test3ğ"
          },
          {
            "aciklama": "Test4ç"
          }
        ],
        "user_tablo": [
          {
            "SubDealer": "1",
            "CreditRelationCustomer": "2",
            "CreditReferenceNumber": "3",
            "WebSupportCustomer": "4",
            "RegisteredEMailAccount": "5",
            "WebSupportStartDate": "22.01.2024",
            "RentalCustomer": "7",
            "DetailDescription1": "9",
            "DetailDescription2": "10"
          }
        ],
        
        "detay": [
          {
            "sth_tarih": "22.01.2024",
            "sth_tip": "1",
            "sth_cins": "0",
            "sth_subeno": 1,
            "sth_normal_iade": "0",
            "sth_evraktip": "4",
            "sth_evrakno_seri": "AS",
            "sth_evrakno_sira": 0,
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 200.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 1500,
            "sth_vergi_pntr": 4,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,
            "sth_plasiyer_kodu": "",
            "sth_stok_srm_merkezi": "",
            "sth_cari_srm_merkezi": "",
            "sth_proje_kodu": "",
            "seriler": "",
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 10
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 150
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 40
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          },
          {
            "sth_tarih": "22.01.2024",
            "sth_tip": "1",
            "sth_cins": "0",
            "sth_subeno": 1,
            "sth_normal_iade": "0",
            "sth_evraktip": "4",
            "sth_evrakno_seri": "AS",
            "sth_evrakno_sira": 0,
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 100,
            "sth_birim_pntr": 1,
            "sth_tutar": 15000,
            "sth_vergi_pntr": 4,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,
            "sth_plasiyer_kodu": "",
            "sth_stok_srm_merkezi": "",
            "sth_cari_srm_merkezi": "",
            "sth_proje_kodu": "",
            "seriler": ""
          },
          {
            "sth_tarih": "22.01.2024",
            "sth_tip": "1",
            "sth_cins": "0",
            "sth_subeno": 1,
            "sth_normal_iade": "0",
            "sth_evraktip": "4",
            "sth_evrakno_seri": "AS",
            "sth_evrakno_sira": 0,
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 30,
            "sth_birim_pntr": 1,
            "sth_tutar": 12000,
            "sth_vergi_pntr": 4,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,
            "sth_plasiyer_kodu": "",
            "sth_stok_srm_merkezi": "",
            "sth_cari_srm_merkezi": "",
            "sth_proje_kodu": "",
            "seriler": ""
          },
          {
            "sth_tarih": "22.01.2024",
            "sth_tip": "1",
            "sth_cins": "0",
            "sth_subeno": 1,
            "sth_normal_iade": "0",
            "sth_evraktip": "4",
            "sth_evrakno_seri": "AS",
            "sth_evrakno_sira": 0,
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 50,
            "sth_birim_pntr": 1,
            "sth_tutar": 10000,
            "sth_vergi_pntr": 4,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,
            "sth_plasiyer_kodu": "",
            "sth_stok_srm_merkezi": "",
            "sth_cari_srm_merkezi": "",
            "sth_proje_kodu": "",
            "seriler": ""
          },
          
      {
        "cha_tip": 0,
        "cha_cinsi": 8,
        "cha_normal_Iade": 0,
        "cha_evrak_tip": 63,
        "cha_cari_cins": 0,
        "cha_d_cins": 0,
        "cha_d_kur": 1,
        "cha_tarihi": "22.01.2024",
        "cha_evrakno_seri": "AS",
        "cha_kod": "CR01",
        "cha_projekodu": "",
        "cha_srmrkkodu": "",
        "cha_vade": 0,
        "cha_subeno": 0,
        "cha_aciklama": "",
        "kdv_istisna_kodu": "",       
        "cha_kasa_hizkod": "HZM01",
        "cha_kasa_hizmet": "3",
        "cha_miktari": "1",
        "cha_aratoplam": 296.61,
        "cha_vergipntr": 0.0,
        "cha_ft_iskonto1": 0.0,
        "cha_isk_mas1": "0",
        "cha_satici_kodu": "",    
        "cha_EArsiv_unvani_ad": "",
        "cha_EArsiv_unvani_soyad": "",
        "cha_EArsiv_daire_adi": "",
        "cha_EArsiv_Vkn": "",
        "cha_EArsiv_ulke": "",
        "cha_EArsiv_Il": "",
        "cha_EArsiv_tel_ulke_kod": "",
        "cha_EArsiv_tel_bolge_kod": "",
        "cha_EArsiv_tel_no": "",
        "cha_EArsiv_mail": ""
      }
        ]
      }
    ]
  }
}
```

#### Alım Satım Evrağı Satır Ekle V2 Add Guid

- Method: `POST`
- Path: `/Api/apiMethods/AlimSatimEvragiDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay.renk_beden[], Mikro.evraklar.detay[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "cha_evrak_tip": 63,
                "cha_evrakno_seri": "AS",
                "cha_evrakno_sira": 1,
                "cha_satir_no": 0,
                "detay": [
                    {                        
                        "sth_tip": "1",
                        "sth_cins": "0",                        
                        "sth_normal_iade": "0",
                        "sth_evraktip": "4",                      
                        "sth_stok_kod": "SK05",
                        "sth_cari_cinsi": "0",
                        "sth_cari_kodu": "CR01",
                        "sth_miktar": 2,
                        "sth_birim_pntr": 1,
                        "sth_tutar": 15000,
                        "sth_vergi_pntr": 4,
                        "sth_vergisiz_fl": false,
                        "sth_iskonto1": 220.0,
                        "sth_iskonto2": 0.0,
                        "sth_giris_depo_no": 1,
                        "sth_cikis_depo_no": 1,
                        "sth_plasiyer_kodu": "",
                        "sth_stok_srm_merkezi": "",
                        "sth_cari_srm_merkezi": "",
                        "sth_proje_kodu": "",
                        "seriler": "",
                        "renk_beden": [
                            {
                                "renk_no": 1,
                                "beden_no": 1,
                                "miktar": 1
                            },
                            {
                                "renk_no": 2,
                                "beden_no": 1,
                                "miktar": 1
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Alım Satım Evrağı Satır Ekle V2 Add Guid Copy

- Method: `POST`
- Path: `/Api/apiMethods/AlimSatimEvragiDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay.user_tablo[], Mikro.evraklar.detay[], Mikro.evraklar.ebelge_detay[], Mikro.evraklar.odemeler[], Mikro.evraklar.user_tablo[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
             {
        "cha_tip": 0,
        "cha_cinsi": 7,
        "cha_normal_Iade": 0,
        "cha_evrak_tip": 63,
        "cha_cari_cins": 0,
        "cha_d_cins": 0,
        "cha_d_kur": 1,
        "cha_tarihi": "22.01.2024",
        "cha_evrakno_seri": "MYT",
        "cha_evrakno_sira": 39,
        "cha_kod": "CR01",
        "cha_projekodu": "",
        "cha_srmrkkodu": "",
        "cha_vade": 0,
        "cha_subeno": 0,
        "cha_aciklama": "10000006636 - INTER PAZARLAMA MMC",
        "kdv_istisna_kodu": "",
        "detay": [
          {
            "sth_tarih": "22.01.2024",
            "sth_tip": 1,
            "sth_cins": 0,
            "sth_normal_iade": 0,
            "sth_evraktip": 4,
            "sth_evrakno_seri": "MYT",
            "sth_stok_kod": "SKT01",
            "sth_cari_cinsi": 0,
            "sth_cari_kodu": "CR01",
            "sth_miktar": 1,
            "sth_birim_pntr": 1,
            "sth_tutar": 275,
            "sth_vergi": 55,
            "sth_aciklama": "10000006636 - INTER PAZARLAMA MMC",
            "sth_cari_srm_merkezi": "",
            "sth_stok_srm_merkezi": "",
            "sth_subeno": 0,
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,
            "user_tablo": [
              {
                "CreditRelationCustomerId": "36bef32e-94c5-4cab-a4dd-d347002ccc92",
                "CreditReferenceNumber": "658016d11cdbf44898e2f80a",
                "test": "Fake_62_PtUUSR_OrderId",                
                "Craftgate_Id": "Fake_62_PtUUSR_OrderId",                
                "WebSupportCustomerId": "36bef32e-94c5-4cab-a4dd-d347002ccc92",
                "RentalCustomerId": "36bef32e-94c5-4cab-a4dd-d347002ccc92",
                "TransactionReferenceId": "658016d11cdbf44898e2f80a",
                "IntallmentCount": 0,
                "InterestAmount": 0
              }
            ]
          }
        ],

        "ebelge_detay": [
          {
            "ebh_odeme_sekli": 1,
            "ebh_satisin_webadresi": "http://www.emikro.com.tr"
          }
        ],
         "odemeler": [
          
        ],
        "cha_kasa_hizkod": "",
        "cha_kasa_hizmet": 0,
        "cha_miktari": "1",
        "cha_aratoplam": 296.61,
        "cha_vergipntr": 0.0,
        "cha_ft_iskonto1": 0.0,
        "cha_isk_mas1": "0",
        "cha_satici_kodu": "",    
        "cha_EArsiv_unvani_ad": "",
        "cha_EArsiv_unvani_soyad": "",
        "cha_EArsiv_daire_adi": "",
        "cha_EArsiv_Vkn": "",
        "cha_EArsiv_ulke": "",
        "cha_EArsiv_Il": "",
        "cha_EArsiv_tel_ulke_kod": "",
        "cha_EArsiv_tel_bolge_kod": "",
        "cha_EArsiv_tel_no": "",
        "cha_EArsiv_mail": "",
        "user_tablo": [
          {
            "SubDealer": "1",
            "CreditRelationCustomer": "2",
            "CreditReferenceNumber": "3",
            "WebSupportCustomer": "4",
            "RegisteredEMailAccount": "5",
            "WebSupportStartDate": "16.11.2020",
            "RentalCustomer": "7",
            "DetailDescription1": "9",
            "DetailDescription2": "10"
          }
        ] 
      },
           {
        "cha_tip": 0,
        "cha_cinsi": 8,
        "cha_normal_Iade": 0,
        "cha_evrak_tip": 63,
        "cha_cari_cins": 0,
        "cha_d_cins": 0,
        "cha_d_kur": 1,
        "cha_tarihi": "22.01.2024",
        "cha_evrakno_seri": "MYT",
        "cha_evrakno_sira": 31,
        "cha_kod": "CR01",
        "cha_projekodu": "",
        "cha_srmrkkodu": "",
        "cha_vade": 0,
        "cha_subeno": 0,
        "cha_aciklama": "10000006636 - INTER PAZARLAMA MMC",
        "kdv_istisna_kodu": "",       
        "cha_kasa_hizkod": "HZM01",
        "cha_kasa_hizmet": "3",
        "cha_miktari": "1",
        "cha_aratoplam": 296.61,
        "cha_vergipntr": 0.0,
        "cha_ft_iskonto1": 0.0,
        "cha_isk_mas1": "0",
        "cha_satici_kodu": "",    
        "cha_EArsiv_unvani_ad": "",
        "cha_EArsiv_unvani_soyad": "",
        "cha_EArsiv_daire_adi": "",
        "cha_EArsiv_Vkn": "",
        "cha_EArsiv_ulke": "",
        "cha_EArsiv_Il": "",
        "cha_EArsiv_tel_ulke_kod": "",
        "cha_EArsiv_tel_bolge_kod": "",
        "cha_EArsiv_tel_no": "",
        "cha_EArsiv_mail": ""
      },
        {
        "cha_tip": 0,
        "cha_cinsi": 8,
        "cha_normal_Iade": 0,
        "cha_evrak_tip": 63,
        "cha_cari_cins": 0,
        "cha_d_cins": 0,
        "cha_d_kur": 1,
        "cha_tarihi": "22.01.2024",
        "cha_evrakno_seri": "MYT",
        "cha_evrakno_sira": 31,
        "cha_kod": "CR01",
        "cha_projekodu": "",
        "cha_srmrkkodu": "",
        "cha_vade": 0,
        "cha_subeno": 0,
        "cha_aciklama": "10000006636 - INTER PAZARLAMA MMC",
        "kdv_istisna_kodu": "",       
        "cha_kasa_hizkod": "HZM02",
        "cha_kasa_hizmet": "3",
        "cha_miktari": "1",
        "cha_aratoplam": 296.61,
        "cha_vergipntr": 0.0,
        "cha_ft_iskonto1": 0.0,
        "cha_isk_mas1": "0",
        "cha_satici_kodu": "",    
        "cha_EArsiv_unvani_ad": "",
        "cha_EArsiv_unvani_soyad": "",
        "cha_EArsiv_daire_adi": "",
        "cha_EArsiv_Vkn": "",
        "cha_EArsiv_ulke": "",
        "cha_EArsiv_Il": "",
        "cha_EArsiv_tel_ulke_kod": "",
        "cha_EArsiv_tel_bolge_kod": "",
        "cha_EArsiv_tel_no": "",
        "cha_EArsiv_mail": ""
      }
      

            
        ]
    }
}
```

#### Alım Satım Evrağı Satır Sil V2 Delete Guid

- Method: `POST`
- Path: `/Api/apiMethods/AlimSatimEvragiSatirSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: GUID ile satir/kayit silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "detay": [
                    {
                        "sth_Guid": "EC25C73E-53DB-49EC-93F1-95BEAD6A3FDA"
                    }
                ]
            }
        ]
    }
}
```

#### Alım Satım Evrağı Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/AlimSatimEvragiSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {        
        "cha_evrak_tip": "63",
        "cha_evrakno_seri": "AS", 
		"cha_evrakno_sira": 2
      }
    ]
  }
}
```

#### Fatura Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/FaturaKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: Raw body var, JSON parse edilemedi
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
     "evraklar": [
      {
        "cha_tip": 0,
        "cha_cinsi": 7,
        "cha_normal_Iade": 0,
        "cha_evrak_tip": 63,
        "cha_cari_cins": 0,
        "cha_d_cins": 0,
        "cha_d_kur": 1,
        "cha_tarihi": "22.01.2024",
        "cha_evrakno_seri": "MYT",
        "cha_kod": "CR01",
        "cha_projekodu": "",
        "cha_srmrkkodu": "",
        "cha_vade": 0,
        "cha_subeno": 0,
        "cha_aciklama": "10000006636 - INTER PAZARLAMA MMC",
        "kdv_istisna_kodu": "",
        "detay": [
          {
            "sth_tarih": "22.01.2024",
            "sth_tip": 1,
            "sth_cins": 0,
            "sth_normal_iade": 0,
            "sth_evraktip": 4,
            "sth_evrakno_seri": "MYT",
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": 0,
            "sth_cari_kodu": "CR01",
            "sth_miktar": 1,
            "sth_birim_pntr": 1,
            "sth_tutar": 275,
            "sth_vergi": 55,
            "sth_aciklama": "10000006636 - INTER PAZARLAMA MMC",
            "sth_cari_srm_merkezi": "",
            "sth_stok_srm_merkezi": "",
            "sth_subeno": 0,
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,
            "user_tablo": [
              {
                "CreditRelationCustomerId": "36bef32e-94c5-4cab-a4dd-d347002ccc92",
                "CreditReferenceNumber": "658016d11cdbf44898e2f80a",
                "test": "Fake_62_PtUUSR_OrderId",                
                "Craftgate_Id": "Fake_62_PtUUSR_OrderId",                
                "WebSupportCustomerId": "36bef32e-94c5-4cab-a4dd-d347002ccc92",
                "RentalCustomerId": "36bef32e-94c5-4cab-a4dd-d347002ccc92",
                "TransactionReferenceId": "658016d11cdbf44898e2f80a",
                "IntallmentCount": 0,
                "InterestAmount": 0
              }
            ]
          }, {
            "sth_tarih": "22.01.2024",
            "sth_tip": 1,
            "sth_cins": 0,
            "sth_normal_iade": 0,
            "sth_evraktip": 4,
            "sth_evrakno_seri": "MYT",
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": 0,
            "sth_cari_kodu": "CR01",
            "sth_miktar": 1,
            "sth_birim_pntr": 1,
            "sth_tutar": 275,
            "sth_vergi": 55,
            "sth_aciklama": "10000006636 - INTER PAZARLAMA MMC",
            "sth_cari_srm_merkezi": "",
            "sth_stok_srm_merkezi": "",
            "sth_subeno": 0,
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,
            "user_tablo": [
              {
                "CreditRelationCustomerId": "36bef32e-94c5-4cab-a4dd-d347002ccc92",
                "CreditReferenceNumber": "658016d11cdbf44898e2f80a",
                "test": "Fake_62_PtUUSR_OrderId",                
                "Craftgate_Id": "Fake_62_PtUUSR_OrderId",                
                "WebSupportCustomerId": "36bef32e-94c5-4cab-a4dd-d347002ccc92",
                "RentalCustomerId": "36bef32e-94c5-4cab-a4dd-d347002ccc92",
                "TransactionReferenceId": "658016d11cdbf44898e2f80a",
                "IntallmentCount": 0,
                "InterestAmount": 0
              }
            ]
          }
        ],

        "ebelge_detay": [
          {
            "ebh_odeme_sekli": 1,
            "ebh_satisin_webadresi": "http://www.emikro.com.tr"
          }
        ],
         "odemeler": [
          
        ],
        "cha_kasa_hizkod": "HZM01",
        "cha_kasa_hizmet": "3",
        "cha_miktari": "1",
        /*"cha_aratoplam": 296.61,*/
        "cha_vergipntr": 0.0,
        "cha_ft_iskonto1": 0.0,
        "cha_isk_mas1": "0",
        "cha_satici_kodu": "",    
        "cha_EArsiv_unvani_ad": "",
        "cha_EArsiv_unvani_soyad": "",
        "cha_EArsiv_daire_adi": "",
        "cha_EArsiv_Vkn": "",
        "cha_EArsiv_ulke": "",
        "cha_EArsiv_Il": "",
        "cha_EArsiv_tel_ulke_kod": "",
        "cha_EArsiv_tel_bolge_kod": "",
        "cha_EArsiv_tel_no": "",
        "cha_EArsiv_mail": "",
        "user_tablo": [
          {
            "SubDealer": "1",
            "CreditRelationCustomer": "2",
            "CreditReferenceNumber": "3",
            "WebSupportCustomer": "4",
            "RegisteredEMailAccount": "5",
            "WebSupportStartDate": "16.11.2020",
            "RentalCustomer": "7",
            "DetailDescription1": "9",
            "DetailDescription2": "10"
          }
        ]
      }
    ]

  }
}
```

#### Fatura Kaydet V2 Save Copy

- Method: `POST`
- Path: `/Api/apiMethods/FaturaKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay.user_tablo[], Mikro.evraklar.detay[], Mikro.evraklar.ebelge_detay[], Mikro.evraklar.odemeler[], Mikro.evraklar.user_tablo[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
     "evraklar": [
      {
        "cha_tip": 0,
        "cha_cinsi": 7,
        "cha_normal_Iade": 0,
        "cha_evrak_tip": 63,
        "cha_cari_cins": 0,
        "cha_d_cins": 0,
        "cha_d_kur": 1,
        "cha_tarihi": "22.01.2024",
        "cha_evrakno_seri": "MYT",
        "cha_kod": "CR01",
        "cha_projekodu": "",
        "cha_srmrkkodu": "",
        "cha_vade": 0,
        "cha_subeno": 0,
        "cha_aciklama": "10000006636 - INTER PAZARLAMA MMC",
        "kdv_istisna_kodu": "",
        "detay": [
          {
            "sth_tarih": "22.01.2024",
            "sth_tip": 1,
            "sth_cins": 0,
            "sth_normal_iade": 0,
            "sth_evraktip": 4,
            "sth_evrakno_seri": "MYT",
            "sth_stok_kod": "SKT01",
            "sth_cari_cinsi": 0,
            "sth_cari_kodu": "CR01",
            "sth_miktar": 1,
            "sth_birim_pntr": 1,
            "sth_tutar": 275,
            "sth_vergi": 55,
            "sth_aciklama": "10000006636 - INTER PAZARLAMA MMC",
            "sth_cari_srm_merkezi": "",
            "sth_stok_srm_merkezi": "",
            "sth_subeno": 0,
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,
            "user_tablo": [
              {
                "CreditRelationCustomerId": "36bef32e-94c5-4cab-a4dd-d347002ccc92",
                "CreditReferenceNumber": "658016d11cdbf44898e2f80a",
                "test": "Fake_62_PtUUSR_OrderId",                
                "Craftgate_Id": "Fake_62_PtUUSR_OrderId",                
                "WebSupportCustomerId": "36bef32e-94c5-4cab-a4dd-d347002ccc92",
                "RentalCustomerId": "36bef32e-94c5-4cab-a4dd-d347002ccc92",
                "TransactionReferenceId": "658016d11cdbf44898e2f80a",
                "IntallmentCount": 0,
                "InterestAmount": 0
              }
            ]
          }
        ],

        "ebelge_detay": [
          {
            "ebh_odeme_sekli": 1,
            "ebh_satisin_webadresi": "http://www.emikro.com.tr"
          }
        ],
         "odemeler": [
          
        ],
        "cha_kasa_hizkod": "",
        "cha_kasa_hizmet": 0,
        "cha_miktari": "1",
        "cha_aratoplam": 296.61,
        "cha_vergipntr": 0.0,
        "cha_ft_iskonto1": 0.0,
        "cha_isk_mas1": "0",
        "cha_satici_kodu": "",    
        "cha_EArsiv_unvani_ad": "",
        "cha_EArsiv_unvani_soyad": "",
        "cha_EArsiv_daire_adi": "",
        "cha_EArsiv_Vkn": "",
        "cha_EArsiv_ulke": "",
        "cha_EArsiv_Il": "",
        "cha_EArsiv_tel_ulke_kod": "",
        "cha_EArsiv_tel_bolge_kod": "",
        "cha_EArsiv_tel_no": "",
        "cha_EArsiv_mail": "",
        "user_tablo": [
          {
            "SubDealer": "1",
            "CreditRelationCustomer": "2",
            "CreditReferenceNumber": "3",
            "WebSupportCustomer": "4",
            "RegisteredEMailAccount": "5",
            "WebSupportStartDate": "16.11.2020",
            "RentalCustomer": "7",
            "DetailDescription1": "9",
            "DetailDescription2": "10"
          }
        ] 
      }
      
    ]

  }
}
```

#### Fatura Kaydet V3 Save

- Method: `POST`
- Path: `/api/APIMethods/FaturaKaydetV3`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.detay[], Mikro.evraklar.ebelge_detay[], Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "cha_tip": 0,
                "cha_cinsi": 6,
                "cha_normal_Iade": 0,
                "cha_evrak_tip": 63,
                "cha_cari_cins": 0,
                "cha_d_cins": 0,
                "cha_d_kur": 1,
                "cha_tarihi": "22.01.2024",
                "cha_evrakno_seri": "MYT",
                "cha_kod": "CR01",
                "cha_projekodu": "",
                "cha_srmrkkodu": "",
                "cha_subeno": 0,
                "cha_aciklama": "0010713277 - KESKİN GAYRİMENKUL TURİZM Y",
                "kdv_istisna_kodu": null,
                "detay": [
                    {
                        "sth_tarih": "21.01.2024",
                        "sth_tip": 1,
                        "sth_cins": 0,
                        "sth_normal_iade": 0,
                        "sth_evraktip": 4,
                        "sth_evrakno_seri": "MYT",
                        "sth_stok_kod": "SKT01",
                        "sth_cari_cinsi": 0,
                        "sth_cari_kodu": "2000A",
                        "sth_miktar": 1.0,
                        "sth_birim_pntr": 1,
                        "sth_tutar": 15,
                        "sth_aciklama": "0010713277 - KESKİN GAYRİMENKUL TURİZM YATIRIM SAN",
                        "sth_cari_srm_merkezi": "",
                        "sth_stok_srm_merkezi": "",
                        "sth_subeno": 0,
                        "sth_iskonto1": 10,
                        "sth_isk_mas1": "2",
                        "sth_giris_depo_no":"2",
                        "sth_cikis_depo_no":"2"                    }
                ],
                "ebelge_detay": [
                    {
                        "ebh_odeme_sekli": 5,
                        "ebh_satisin_webadresi": "https://partner.mikro.com.tr"
                    }
                ],
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1"
                    },
                    {
                        "aciklama": "Test2​"
                    },
                    {
                        "aciklama": "Test3"
                    },
                    {
                        "aciklama": "Test4​"
                    }
                ]
            }
            ,
      {
        "cha_tip": 0,
        "cha_cinsi": 8,
        "cha_normal_Iade": 0,
        "cha_evrak_tip": 63,
        "cha_cari_cins": 0,
        "cha_d_cins": 0,
        "cha_d_kur": 1,
        "cha_tarihi": "22.01.2024",
        "cha_evrakno_seri": "MYT",
        "cha_kod": "CR01",
        "cha_projekodu": "",
        "cha_srmrkkodu": "",
        "cha_vade": 0,
        "cha_subeno": 0,
        "cha_aciklama": "10000006636 - INTER PAZARLAMA MMC",
        "kdv_istisna_kodu": "",       
        "cha_kasa_hizkod": "HZM01",
        "cha_kasa_hizmet": "3",
        "cha_miktari": "1",
        "cha_aratoplam": 296.61,
        "cha_vergipntr": 0.0,
        "cha_ft_iskonto1": 0.0,
        "cha_isk_mas1": "0",
        "cha_satici_kodu": "",    
        "cha_EArsiv_unvani_ad": "",
        "cha_EArsiv_unvani_soyad": "",
        "cha_EArsiv_daire_adi": "",
        "cha_EArsiv_Vkn": "",
        "cha_EArsiv_ulke": "",
        "cha_EArsiv_Il": "",
        "cha_EArsiv_tel_ulke_kod": "",
        "cha_EArsiv_tel_bolge_kod": "",
        "cha_EArsiv_tel_no": "",
        "cha_EArsiv_mail": ""
      }
              
        ]
    }
}
```

#### Fatura Kaydet V3 Save Copy

- Method: `POST`
- Path: `/api/APIMethods/FaturaKaydetV3`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
           {
        "cha_tip": 0,
        "cha_cinsi": 8,
        "cha_normal_Iade": 0,
        "cha_evrak_tip": 63,
        "cha_cari_cins": 0,
        "cha_d_cins": 0,
        "cha_d_kur": 1,
        "cha_tarihi": "22.01.2024",
        "cha_evrakno_seri": "MYT",

        "cha_kod": "CR01",
        "cha_projekodu": "",
        "cha_srmrkkodu": "",
        "cha_vade": 0,
        "cha_subeno": 0,
        "cha_aciklama": "10000006636 - INTER PAZARLAMA MMC",
        "kdv_istisna_kodu": "",       
        "cha_kasa_hizkod": "HZM01",
        "cha_kasa_hizmet": "3",
        "cha_miktari": "1",
        "cha_aratoplam": 296.61,
        "cha_vergipntr": 0.0,
        "cha_ft_iskonto1": 0.0,
        "cha_isk_mas1": "0",
        "cha_satici_kodu": "",    
        "cha_EArsiv_unvani_ad": "",
        "cha_EArsiv_unvani_soyad": "",
        "cha_EArsiv_daire_adi": "",
        "cha_EArsiv_Vkn": "",
        "cha_EArsiv_ulke": "",
        "cha_EArsiv_Il": "",
        "cha_EArsiv_tel_ulke_kod": "",
        "cha_EArsiv_tel_bolge_kod": "",
        "cha_EArsiv_tel_no": "",
        "cha_EArsiv_mail": ""
      },
        {
        "cha_tip": 0,
        "cha_cinsi": 8,
        "cha_normal_Iade": 0,
        "cha_evrak_tip": 63,
        "cha_cari_cins": 0,
        "cha_d_cins": 0,
        "cha_d_kur": 1,
        "cha_tarihi": "22.01.2024",
        "cha_evrakno_seri": "MYT",

        "cha_kod": "CR01",
        "cha_projekodu": "",
        "cha_srmrkkodu": "",
        "cha_vade": 0,
        "cha_subeno": 0,
        "cha_aciklama": "10000006636 - INTER PAZARLAMA MMC",
        "kdv_istisna_kodu": "",       
        "cha_kasa_hizkod": "HZM02",
        "cha_kasa_hizmet": "3",
        "cha_miktari": "1",
        "cha_aratoplam": 296.61,
        "cha_vergipntr": 0.0,
        "cha_ft_iskonto1": 0.0,
        "cha_isk_mas1": "0",
        "cha_satici_kodu": "",    
        "cha_EArsiv_unvani_ad": "",
        "cha_EArsiv_unvani_soyad": "",
        "cha_EArsiv_daire_adi": "",
        "cha_EArsiv_Vkn": "",
        "cha_EArsiv_ulke": "",
        "cha_EArsiv_Il": "",
        "cha_EArsiv_tel_ulke_kod": "",
        "cha_EArsiv_tel_bolge_kod": "",
        "cha_EArsiv_tel_no": "",
        "cha_EArsiv_mail": ""
      }
            
        ]
    }
}
```

#### Fatura PDF V2

- Method: `POST`
- Path: `/API/APIMethods/FaturaPdfV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Collection icindeki Mikro API islemi
- Body ozeti: top: Mikro; Mikro: CalismaYili, Fatura_Guid, FirmaKodu, KullaniciKodu, Sifre
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
      "FirmaKodu": "MIKROFLY",
      "CalismaYili": "2023",
      "KullaniciKodu": "SRV",
      "Sifre": "{{MikroSifreHash}}",
      "Fatura_Guid":"E525C73E-53DB-49EC-93F1-95BEAD6A3FDA"
  }
}
```

### Alınan Teklif

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Alınan Teklif Düzelt V2 Update | `POST` | `/Api/apiMethods/AlinanTeklifDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 2 | Alınan Teklif Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/AlinanTeklifDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 3 | Alınan Teklif Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/AlinanTeklifGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| 4 | Alınan Teklif Kaydet V2 Save | `POST` | `/Api/apiMethods/AlinanTeklifKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 5 | Alınan Teklif Sil V2 Delete | `POST` | `/Api/apiMethods/AlinanTeklifSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |

#### Alınan Teklif Düzelt V2 Update

- Method: `POST`
- Path: `/Api/apiMethods/AlinanTeklifDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
"Mikro": {
"FirmaKodu": "MIKROFLY",
"CalismaYili": "2023",
"KullaniciKodu": "SRV",
"Sifre": "{{MikroSifreHash}}",
"evraklar": [
      { 
        "satirlar": [
          {
            "altkl_Guid": "49FACDD8-298A-4CD2-9541-0BE0DE7C705A",
            "altkl_miktar": 10 
         }
        ]
      }
    ]
  }
}
```

#### Alınan Teklif Guid Ekle V2 Add Guid

- Method: `POST`
- Path: `/Api/apiMethods/AlinanTeklifDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1cc"
                    },
                    {
                        "aciklama": "Test2hh"
                    },
                    {
                        "aciklama": "Testşlş"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "altkl_teklif_kodu": "T1",
                        "altkl_sira_no": "3",                        
                        "altkl_tarihi": "16.01.2024",
                        "altkl_belge_no": "",
                        "altkl_belge_tarih": "16.01.2024",
                        "altkl_cari_kodu": "CR01",
                        "altkl_cari_adres_no": 1,
                        "altkl_teslimat_tarihi": "16.01.2024",
                        "altkl_teslim_turu": "",
                        "altkl_hareket_tipi": 0,
                        "altkl_hareket_kodu": "SK05",
                        "altkl_miktar": 1,
                        "altkl_birim_fiyati": 5,
                        "altkl_tutar": 5,
                        "altkl_vergi_pntr": 4,
                        "altkl_doviz_cins": 1,
                        "altkl_iskonto1": 0,
                        "altkl_iskonto2": 0,
                        "altkl_iskonto3": 0,
                        "altkl_iskonto4": 0,
                        "altkl_iskonto5": 0,
                        "altkl_iskonto6": 0,
                        "altkl_masraf1": 0,
                        "altkl_masraf2": 0,
                        "altkl_masraf3": 0,
                        "altkl_masraf4": 0,
                        "altkl_isk_mas1": 0,
                        "altkl_isk_mas2": 1,
                        "altkl_isk_mas3": 1,
                        "altkl_isk_mas4": 1,
                        "altkl_isk_mas5": 1,
                        "altkl_isk_mas6": 1,
                        "altkl_isk_mas7": 1,
                        "altkl_isk_mas8": 1,
                        "altkl_isk_mas9": 1,
                        "altkl_isk_mas10": 1,
                        "altkl_sat_iskmas1": 0,
                        "altkl_sat_iskmas2": 0,
                        "altkl_sat_iskmas3": 0,
                        "altkl_sat_iskmas4": 0,
                        "altkl_sat_iskmas5": 0,
                        "altkl_sat_iskmas6": 0,
                        "altkl_sat_iskmas7": 0,
                        "altkl_sat_iskmas8": 0,
                        "altkl_sat_iskmas9": 0,
                        "altkl_sat_iskmas10": 0,
                        "altkl_vergisiz_fl": false,
                        "altkl_fiyat_liste_no": 0,
                        "altkl_paket_kod": "",
                        "altkl_teslimdepo": 0,
                        "altkl_aciklama": "",
                        "altkl_birim_pntr": 1,
                        "altkl_cari_tipi": 0,
                        "user_tablo": [
                            {
                                "aciklama": "test SAS user tablo"
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Alınan Teklif Guid Sil V2 Delete Guid

- Method: `POST`
- Path: `/Api/apiMethods/AlinanTeklifGuidSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: GUID ile satir/kayit silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
"Mikro": {
"FirmaKodu": "MIKROFLY",
"CalismaYili": "2023",
"KullaniciKodu": "SRV",
"Sifre": "{{MikroSifreHash}}",
"evraklar": [
      { 
        "satirlar": [
          {
            "altkl_Guid": "6a709d1b-3113-4ebf-b936-3cf7cbc7df9e"
         }
        ]
      }
    ]
  }
}
```

#### Alınan Teklif Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/AlinanTeklifKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1cc"
                    },
                    {
                        "aciklama": "Test2hh"
                    },
                    {
                        "aciklama": "Testşlş"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "altkl_teklif_kodu": "T1",
                        "altkl_tarihi": "16.01.2024",
                        "altkl_belge_no": "",
                        "altkl_belge_tarih": "16.01.2024",
                        "altkl_cari_kodu": "CR01",
                        "altkl_cari_adres_no": 1,
                        "altkl_teslimat_tarihi": "16.01.2024",
                        "altkl_teslim_turu": "",
                        "altkl_hareket_tipi": 0,
                        "altkl_hareket_kodu": "SK02",
                        "altkl_miktar": 1,
                        "altkl_birim_fiyati": 5,
                        "altkl_tutar": 5,
                        "altkl_vergi_pntr": 4,
                        "altkl_doviz_cins": 1,
                        "altkl_iskonto1": 0,
                        "altkl_iskonto2": 0,
                        "altkl_iskonto3": 0,
                        "altkl_iskonto4": 0,
                        "altkl_iskonto5": 0,
                        "altkl_iskonto6": 0,
                        "altkl_masraf1": 0,
                        "altkl_masraf2": 0,
                        "altkl_masraf3": 0,
                        "altkl_masraf4": 0,
                        "altkl_isk_mas1": 0,
                        "altkl_isk_mas2": 1,
                        "altkl_isk_mas3": 1,
                        "altkl_isk_mas4": 1,
                        "altkl_isk_mas5": 1,
                        "altkl_isk_mas6": 1,
                        "altkl_isk_mas7": 1,
                        "altkl_isk_mas8": 1,
                        "altkl_isk_mas9": 1,
                        "altkl_isk_mas10": 1,
                        "altkl_sat_iskmas1": 0,
                        "altkl_sat_iskmas2": 0,
                        "altkl_sat_iskmas3": 0,
                        "altkl_sat_iskmas4": 0,
                        "altkl_sat_iskmas5": 0,
                        "altkl_sat_iskmas6": 0,
                        "altkl_sat_iskmas7": 0,
                        "altkl_sat_iskmas8": 0,
                        "altkl_sat_iskmas9": 0,
                        "altkl_sat_iskmas10": 0,
                        "altkl_vergisiz_fl": false,
                        "altkl_fiyat_liste_no": 0,
                        "altkl_paket_kod": "",
                        "altkl_teslimdepo": 0,
                        "altkl_aciklama": "",
                        "altkl_birim_pntr": 1,
                        "altkl_cari_tipi": 0,
                        "user_tablo": [
                            {
                                "aciklama": "test SAS user tablo"
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Alınan Teklif Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/AlinanTeklifSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "satirlar": [
                    {
                        "altkl_teklif_kodu": "T1",
                        "altkl_sira_no": 1
                    }
                ]
            }
        ]
    }
}
```

### Cari

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Cari Güncelle V2 Update | `POST` | `/API/APIMethods/CariGuncelleV2` | top: Mikro; Mikro: CalismaYili, cariler, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.cariler[] | Mevcut kayit/evrak guncelleme |
| 2 | Cari Kaydet V2 Save | `POST` | `/API/APIMethods/CariKaydetV2` | top: Mikro; Mikro: CalismaYili, cariler, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.cariler.adres.yetkili[], Mikro.cariler.adres[], Mikro.cariler[] | Yeni kayit/evrak olusturma |

#### Cari Güncelle V2 Update

- Method: `POST`
- Path: `/API/APIMethods/CariGuncelleV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, cariler, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.cariler[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
      "FirmaKodu": "MIKROFLY",
      "CalismaYili": "2023",
      "KullaniciKodu": "SRV",
      "Sifre": "{{MikroSifreHash}}",
    "cariler": [
      {
        "cari_Guid": "0CB53E17-7137-4855-8A52-DA6CF27D3BB4",
        "cari_unvan2": "xxxxxxxx"
      }
    ]
  }
}
```

#### Cari Kaydet V2 Save

- Method: `POST`
- Path: `/API/APIMethods/CariKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, cariler, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.cariler.adres.yetkili[], Mikro.cariler.adres[], Mikro.cariler[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
      "FirmaKodu": "MIKROFLY",
      "CalismaYili": "2023",
      "KullaniciKodu": "SRV",
      "Sifre": "{{MikroSifreHash}}"
    , 
    "cariler": [
      {
        "cari_kod": "YCK141",
        "cari_unvan1": "yeni cari unvan",
        "cari_unvan2": "yeni cari unvan2",
        "cari_vdaire_no": "11111111111",
        "cari_vdaire_adi": "TAŞOVA VERGİ DAİRESİ",
        "cari_doviz_cinsi1": 0,
        "cari_doviz_cinsi2": 255,
        "cari_doviz_cinsi3": 255,
        "cari_vade_fark_yuz": 25,
        "cari_KurHesapSekli": 1,
        "cari_sevk_adres_no": 0,
        "cari_fatura_adres_no": 0,
        "cari_EMail": "",
        "cari_CepTel": "",
        "cari_efatura_fl": 0,
        "cari_def_efatura_cinsi": 0,
        "cari_efatura_baslangic_tarihi": "",
        "cari_vergidairekodu":"",
        "cari_muh_kod2":"",
        "adres": [
          {
            "adr_cadde": "cadde",
            "adr_mahalle": "mahalle",
            "adr_sokak": "sokak",
            "adr_Semt": "semt",
            "adr_Apt_No": "A1",
            "adr_Daire_No": "2",
            "adr_posta_kodu": 34340,
            "adr_ilce": "Sarıyer",
            "adr_il": "İstanbul",
            "adr_ulke": "TÜRKİYE",
            "adr_tel_ulke_kodu": "090",
            "adr_tel_bolge_kodu": "212",
            "adr_tel_no1": "4444444",
            "adr_tel_no2": "",
            "adr_tel_faxno": "",
            "yetkili": [
              {
                "mye_isim": "test yetkili isim 1",
                "mye_soyisim": "test yetkili soyisim 1",
                "mye_dahili_telno": "",
                "mye_email_adres": "adasda@adasda.com.tr",
                "mye_cep_telno": "05551234567"
              },
              {
                "mye_isim": "test yetkili isim 3",
                "mye_soyisim": "test yetkili soyisim 3",
                "mye_dahili_telno": "",
                "mye_email_adres": "adasda@adasda.com.tr",
                "mye_cep_telno": "05551234567"
              }
            ]
          },
          {
            "adr_cadde": "cadde2",
            "adr_mahalle": "mahalle2",
            "adr_sokak": "sokak2",
            "adr_Semt": "semt2",
            "adr_Apt_No": "A1",
            "adr_Daire_No": "2",
            "adr_posta_kodu": 34340,
            "adr_ilce": "Sarıyer",
            "adr_il": "İstanbul",
            "adr_ulke": "TÜRKİYE",
            "adr_tel_ulke_kodu": "090",
            "adr_tel_bolge_kodu": "212",
            "adr_tel_no1": "4444444",
            "adr_tel_no2": "",
            "adr_tel_faxno": "",
            "yetkili": [
              {
                "mye_isim": "test yetkili isim 2",
                "mye_soyisim": "test yetkili soyisim 2",
                "mye_dahili_telno": "",
                "mye_email_adres": "fafafafa@adasda.com.tr",
                "mye_cep_telno": "05551234589"
              }
            ]
          }
        ]
      }			
    ]
  }
}
```

### Dahili Stok Hareket

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Dahili Stok Hareket Düzelt V2 Update | `POST` | `/Api/apiMethods/DahiliStokHareketDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 2 | Dahili Stok Hareket Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/DahiliStokHareketDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 3 | Dahili Stok Hareket Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/DahiliStokHareketGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| 4 | Dahili Stok Hareket Kaydet V2 Save | `POST` | `/Api/apiMethods/DahiliStokHareketKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 5 | Dahili Stok Hareket Kaydet V2 Save Copy | `POST` | `/Api/apiMethods/DahiliStokHareketKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 6 | Dahili Stok Hareket Sil V2 Delete | `POST` | `/Api/apiMethods/DahiliStokHareketSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |

#### Dahili Stok Hareket Düzelt V2 Update

- Method: `POST`
- Path: `/Api/apiMethods/DahiliStokHareketDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
           "sth_Guid":"0E31C73E-53DB-49EC-93F1-95BEAD6A3FDA",	        					 
           "sth_miktar": 250 
          }
        ]      
      }
    ]
  }
}
```

#### Dahili Stok Hareket Guid Ekle V2 Add Guid

- Method: `POST`
- Path: `/Api/apiMethods/DahiliStokHareketDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "FirmaNo": 0,
        "SubeNo": 0,
        "evraklar": [
            {
                "satirlar": [
                    {
                        "sth_tarih": "21.12.2023",
                        "sth_tip": "2",
                        "sth_cins": "6",
                        "sth_normal_iade": "0",
                        "sth_evraktip": "2",
                        "sth_evrakno_seri": "DA",
                        "sth_evrakno_siRA": 6,
                        "sth_stok_kod": "SK04",
                        "sth_miktar": 150,
                        "sth_birim_pntr": 4,
                        "sth_tutar": 3000,
                        "sth_vergi_pntr": 4,
                        "sth_vergisiz_fl": false,
                        "sth_isk_mas1": "0",
                        "sth_isk_mas2": "1",
                        "sth_giris_depo_no": 2,
                        "sth_cikis_depo_no": 1
                    }
                ]
            }
        ]
    }
}
```

#### Dahili Stok Hareket Guid Sil V2 Delete Guid

- Method: `POST`
- Path: `/Api/apiMethods/DahiliStokHareketGuidSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: GUID ile satir/kayit silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {						
            "sth_Guid" : "0032C73E-53DB-49EC-93F1-95BEAD6A3FDA"
          }
        ]      
      }
    ]
  }
}
```

#### Dahili Stok Hareket Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/DahiliStokHareketKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "FirmaNo": 0,
        "SubeNo": 0,
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1cc"
                    },
                    {
                        "aciklama": "Test2hh"
                    },
                    {
                        "aciklama": "Testşlş"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "sth_tarih": "20.02.2024",
                        "sth_tip": "2",
                        "sth_cins": "6",
                        "sth_normal_iade": "0",
                        "sth_evraktip": "2",
                        "sth_evrakno_seri": "DA",
                        "sth_stok_kod": "SK02",
                        "sth_cari_cinsi": "0",
                        "sth_cari_kodu": "",
                        "sth_miktar": 200.0,
                        "sth_birim_pntr": 4,
                        "sth_tutar": 296.61,
                        "sth_vergi_pntr": 4,
                        "sth_vergisiz_fl": false,
                        "sth_isk_mas1": "0",
                        "sth_isk_mas2": "1",
                        "sth_giris_depo_no": 2,
                        "sth_cikis_depo_no": 1,
                        "renk_beden": [
                            {
                                "renk_kirilim_kodu": "Yeşil",
                                "beden_kirilim_kodu": "L",
                                "miktar": 10
                            },
                            {
                                "renk_no": 1,
                                "beden_no": 2,
                                "miktar": 150
                            },
                            {
                                "renk_no": 2,
                                "beden_no": 1,
                                "miktar": 40
                            }
                        ],
                        "user_tablo": [
                            {
                                "aciklama": "test stok hareket user tablo"
                            }
                        ]
                    }
                ]
            },
            {
                "satirlar": [
                    {
                        "sth_tarih": "20.02.2024",
                        "sth_tip": "2",
                        "sth_cins": "6",
                        "sth_normal_iade": "0",
                        "sth_evraktip": "2",
                        "sth_evrakno_seri": "DA",
                        "sth_stok_kod": "SK04",
                        "sth_miktar": 150,
                        "sth_birim_pntr": 4,
                        "sth_tutar": 3000,
                        "sth_vergi_pntr": 4,
                        "sth_vergisiz_fl": false,
                        "sth_isk_mas1": "0",
                        "sth_isk_mas2": "1",
                        "sth_giris_depo_no": 2,
                        "sth_cikis_depo_no": 1,
                        "renk_beden": [
                            {
                                "renk_kirilim_kodu": "Yeşil",
                                "miktar": 150
                            }
                        ],
                        "user_tablo": [
                            {
                                "aciklama": "test stok hareket user tablo"
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Dahili Stok Hareket Kaydet V2 Save Copy

- Method: `POST`
- Path: `/Api/apiMethods/DahiliStokHareketKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "FirmaNo": 0,
        "SubeNo": 0,
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1cc"
                    },
                    {
                        "aciklama": "Test2hh"
                    },
                    {
                        "aciklama": "Testşlş"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "sth_tarih": "21.12.2023",
                        "sth_tip": "2",
                        "sth_cins": "6",
                        "sth_normal_iade": "0",
                        "sth_evraktip": "2",
                        "sth_evrakno_seri": "DA",
                        "sth_stok_kod": "SK02",
                        "sth_cari_cinsi": "0",
                        "sth_cari_kodu": "",
                        "sth_miktar": 200.0,
                        "sth_birim_pntr": 4,
                        "sth_tutar": 296.61,
                        "sth_vergi_pntr": 4,
                        "sth_vergisiz_fl": false,
                        "sth_isk_mas1": "0",
                        "sth_isk_mas2": "1",
                        "sth_giris_depo_no": 2,
                        "sth_cikis_depo_no": 1,
                        "renk_beden": [
                            {
                                "renk_kirilim_kodu": "Yeşil",
                                "beden_kirilim_kodu": "L",
                                "miktar": 10
                            },
                            {
                                "renk_no": 1,
                                "beden_no": 2,
                                "miktar": 150
                            },
                            {
                                "renk_no": 2,
                                "beden_no": 1,
                                "miktar": 40
                            }
                        ],
                        "user_tablo": [
                            {
                                "aciklama": "test stok hareket user tablo"
                            }
                        ]
                    }
                ]
            },
            {
                "satirlar": [
                    {
                        "sth_tarih": "21.12.2023",
                        "sth_tip": "2",
                        "sth_cins": "6",
                        "sth_normal_iade": "0",
                        "sth_evraktip": "2",
                        "sth_evrakno_seri": "DA",
                        "sth_stok_kod": "SK04",
                        "sth_miktar": 150,
                        "sth_birim_pntr": 4,
                        "sth_tutar": 3000,
                        "sth_vergi_pntr": 4,
                        "sth_vergisiz_fl": false,
                        "sth_isk_mas1": "0",
                        "sth_isk_mas2": "1",
                        "sth_giris_depo_no": 2,
                        "sth_cikis_depo_no": 1,
                        "renk_beden": [
                            {
                                "renk_kirilim_kodu": "Yeşil",
                                "miktar": 150
                            }
                        ],
                        "user_tablo": [
                            {
                                "aciklama": "test stok hareket user tablo"
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Dahili Stok Hareket Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/DahiliStokHareketSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {						
            "sth_evraktip" : 2,
            "sth_evrakno_seri" : "DA",
            "sth_evrakno_sira" : 5
          }
        ]      
      }
    ]
  }
}
```

### Dekont

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Bankalar Arası Virman Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 2 | Borç Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 3 | Borç Dekontu Kaydet V2 Save Copy | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 4 | Cari Borç Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 5 | Cari Hesaplar Arası Virman Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 6 | Dekont Sil V2 Delete | `POST` | `/Api/apiMethods/DekontSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| 7 | Genel Amaçlı Virman Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 8 | Kasalar Arası Virman Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |

#### Bankalar Arası Virman Dekontu Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/DekontKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu": "MIKROFLY",
    "CalismaYili": "2023",
    "KullaniciKodu": "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "evrak_aciklamalari": [
              {
                "aciklama": "Test1bb"
              },
              {
                "aciklama": "Test2fg"
              },
              {
                "aciklama": "Testgghh"
              },
              {
                "aciklama": "Test4jkjjk"
              }
            ],   
        "satirlar": [
          {
            "cha_tarihi": "21.12.2023",
            "cha_tip": 0,
            "cha_normal_Iade": 0,
            "cha_evrak_tip": 58,
            "cha_evrakno_seri": "BVD",
            "cha_cari_cins": 2,
            "cha_kod": "1",
            "cha_d_kurtar": null,
            "cha_d_cins": 0,
            "cha_d_kur": 1,
            "cha_srmrkkodu": "",
            "cha_projekodu": "",            
            "cha_meblag": "1000",                         
            "user_tablo": [
              {
                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
              }
            ]
          },
          {
            "cha_tarihi": "21.12.2023",
            "cha_tip": 1,
            "cha_normal_Iade": 0,
            "cha_evrak_tip": 58,
            "cha_evrakno_seri": "BVD",
            "cha_cari_cins": 2,
            "cha_kod": "2",
            "cha_d_kurtar": null,
            "cha_d_cins": 0,
            "cha_d_kur": 1,
            "cha_srmrkkodu": "",
            "cha_projekodu": "",
            "cha_meblag": "1000",                         
            "user_tablo": [
              {
                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### Borç Dekontu Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/DekontKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1bb"
                    },
                    {
                        "aciklama": "Test2fg"
                    },
                    {
                        "aciklama": "Testgghh"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "cha_tarihi": "21.12.2023",
                        "cha_tip": 0,
                        "cha_normal_Iade": 0,
                        "cha_evrak_tip": 31,
                        "cha_evrakno_seri": "BD",
                        "cha_cari_cins": 0,
                        "cha_kod": "CR01",
                        "cha_d_kurtar": null,
                        "cha_d_cins": 0,
                        "cha_d_kur": 1,
                        "cha_srmrkkodu": "",
                        "cha_projekodu": "",
                        "cha_kasa_hizmet": 2,
                        "cha_kasa_hizkod": "1",
                        "cha_meblag": "1000",
                        "user_tablo": [
                            {
                                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Borç Dekontu Kaydet V2 Save Copy

- Method: `POST`
- Path: `/Api/apiMethods/DekontKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1bb"
                    },
                    {
                        "aciklama": "Test2fg"
                    },
                    {
                        "aciklama": "Testgghh"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "cha_tarihi": "30.01.2024",
                        "cha_tip": 0,
                        "cha_normal_Iade": 0,
                        "cha_evrak_tip": 34,
                        "cha_evrakno_seri": "GH",
                        "cha_cari_cins": 0,
                        "cha_kod": "CR01",
                        "cha_d_kurtar": null,
                        "cha_d_cins": 0,
                        "cha_d_kur": 1,
                        "cha_srmrkkodu": "",
                        "cha_projekodu": "",
                        "cha_kasa_hizmet": 2,
                        "cha_kasa_hizkod": "1",
                        "cha_meblag": "1000",
                        "user_tablo": [
                            {
                                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Cari Borç Dekontu Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/DekontKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1bb"
                    },
                    {
                        "aciklama": "Test2fg"
                    },
                    {
                        "aciklama": "Testgghh"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "cha_tarihi": "21.12.2023",
                        "cha_tip": 0,
                        "cha_normal_Iade": 0,
                        "cha_evrak_tip": 100,
                        "cha_evrakno_seri": "CBD",
                        "cha_cari_cins": 0,
                        "cha_kod": "CR01",
                        "cha_d_kurtar": null,
                        "cha_d_cins": 0,
                        "cha_d_kur": 1,
                        "cha_srmrkkodu": "",
                        "cha_projekodu": "",
                        "cha_kasa_hizmet": 3,
                        "cha_kasa_hizkod": "HZM01",
                        "cha_meblag": "500",                         
                        "user_tablo": [
                        {
                            "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
                        }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Cari Hesaplar Arası Virman Dekontu Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/DekontKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu": "MIKROFLY",
    "CalismaYili": "2023",
    "KullaniciKodu": "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "evrak_aciklamalari": [
              {
                "aciklama": "Test1bb"
              },
              {
                "aciklama": "Test2fg"
              },
              {
                "aciklama": "Testgghh"
              },
              {
                "aciklama": "Test4jkjjk"
              }
            ],   
        "satirlar": [
          {
            "cha_tarihi": "21.12.2023",
            "cha_tip": 0,
            "cha_normal_Iade": 0,
            "cha_evrak_tip": 57,
            "cha_evrakno_seri": "CBD",
            "cha_cari_cins": 0,
            "cha_kod": "Tutku100003",
            "cha_d_kurtar": null,
            "cha_d_cins": 0,
            "cha_d_kur": 1,
            "cha_srmrkkodu": "",
            "cha_projekodu": "",            
            "cha_meblag": "1000",                         
            "user_tablo": [
              {
                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
              }
            ]
          },
          {
            "cha_tarihi": "21.12.2023",
            "cha_tip": 1,
            "cha_normal_Iade": 0,
            "cha_evrak_tip": 57,
            "cha_evrakno_seri": "CBD",
            "cha_cari_cins": 0,
            "cha_kod": "Tutku100013",
            "cha_d_kurtar": null,
            "cha_d_cins": 0,
            "cha_d_kur": 1,
            "cha_srmrkkodu": "",
            "cha_projekodu": "",
            "cha_meblag": "1000",                         
            "user_tablo": [
              {
                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### Dekont Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/DekontSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "satirlar": [
                    {
                        "cha_evrakno_seri": "KAV",
                        "cha_evrakno_sira": 2,
                        "cha_evrak_tip": "110"
                    }
                ]
            }
        ]
    }
}
```

#### Genel Amaçlı Virman Dekontu Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/DekontKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu": "MIKROFLY",
    "CalismaYili": "2023",
    "KullaniciKodu": "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "evrak_aciklamalari": [
              {
                "aciklama": "Test1bb"
              },
              {
                "aciklama": "Test2fg"
              },
              {
                "aciklama": "Testgghh"
              },
              {
                "aciklama": "Test4jkjjk"
              }
            ],   
        "satirlar": [
          {
            "cha_tarihi": "21.12.2023",
            "cha_tip": 0,
            "cha_normal_Iade": 0,
            "cha_evrak_tip": 33,
            "cha_evrakno_seri": "VD",
            "cha_cari_cins": 0,
            "cha_kod": "CR01",
            "cha_d_kurtar": null,
            "cha_d_cins": 0,
            "cha_d_kur": 1,
            "cha_srmrkkodu": "",
            "cha_projekodu": "",            
            "cha_meblag": "1000",                         
            "user_tablo": [
              {
                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
              }
            ]
          },
          {
            "cha_tarihi": "21.12.2023",
            "cha_tip": 1,
            "cha_normal_Iade": 0,
            "cha_evrak_tip": 33,
            "cha_evrakno_seri": "VD",
            "cha_cari_cins": 0,
            "cha_kod": "CR01",
            "cha_d_kurtar": null,
            "cha_d_cins": 0,
            "cha_d_kur": 1,
            "cha_srmrkkodu": "",
            "cha_projekodu": "",
            "cha_meblag": "1000",                         
            "user_tablo": [
              {
                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### Kasalar Arası Virman Dekontu Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/DekontKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu": "MIKROFLY",
    "CalismaYili": "2023",
    "KullaniciKodu": "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "evrak_aciklamalari": [
              {
                "aciklama": "Test1bb"
              },
              {
                "aciklama": "Test2fg"
              },
              {
                "aciklama": "Testgghh"
              },
              {
                "aciklama": "Test4jkjjk"
              }
            ],   
        "satirlar": [
          {
            "cha_tarihi": "21.12.2023",
            "cha_tip": 0,
            "cha_normal_Iade": 0,
            "cha_evrak_tip": 110,
            "cha_evrakno_seri": "KAV",
            "cha_cari_cins": 4,
            "cha_kod": "001",
            "cha_d_kurtar": null,
            "cha_d_cins": 0,
            "cha_d_kur": 1,
            "cha_srmrkkodu": "",
            "cha_projekodu": "",            
            "cha_meblag": "1000",                         
            "user_tablo": [
              {
                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
              }
            ]
          },
          {
            "cha_tarihi": "21.12.2023",
            "cha_tip": 1,
            "cha_normal_Iade": 0,
            "cha_evrak_tip": 110,
            "cha_evrakno_seri": "KAV",
            "cha_cari_cins": 4,
            "cha_kod": "NAKIT01",
            "cha_d_kurtar": null,
            "cha_d_cins": 0,
            "cha_d_kur": 1,
            "cha_srmrkkodu": "",
            "cha_projekodu": "",
            "cha_meblag": "1000",                         
            "user_tablo": [
              {
                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

### Depolar Arası Sipariş

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Depolar Arası Sipariş Düzelt V2 Update | `POST` | `/Api/apiMethods/DepolarArasiSiparisDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 2 | Depolar Arası Sipariş Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/DepolarArasiSiparisDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 3 | Depolar Arası Sipariş Guid sil V2 Delete Guid | `POST` | `/Api/apiMethods/DepolarArasiSiparisGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| 4 | Depolar Arası Sipariş Kaydet V2 Save | `POST` | `/Api/apiMethods/DepolarArasiSiparisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 5 | Depolar Arası Sipariş Sil V2 Delete | `POST` | `/Api/apiMethods/DepolarArasiSiparisSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |

#### Depolar Arası Sipariş Düzelt V2 Update

- Method: `POST`
- Path: `/Api/apiMethods/DepolarArasiSiparisDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
           "ssip_Guid":"B88BE259-8384-44A2-A1BB-131F410A5E99",
           "ssip_miktar": 6,
            "renk_beden":[
                    {
                        "renk_no":1,
                        "beden_no":2,
                        "miktar":1                            
                    },
                    {
                        "renk_no":2,
                        "beden_no":1,
                        "miktar":1
                    }
                ]
          }
        ]      
      }
    ]
  }
}
```

#### Depolar Arası Sipariş Guid Ekle V2 Add Guid

- Method: `POST`
- Path: `/Api/apiMethods/DepolarArasiSiparisDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
            "ssip_tarih": "19.12.2023",
            "ssip_belgeno": "",
            "ssip_evrakno_seri": "DS",
            "ssip_evrakno_siRA": 1,
            "ssip_stok_kod": "SK05",
            "ssip_b_fiyat": 10,
            "ssip_miktar": 4,
            "ssip_tutar": 20,
            "ssip_girdepo": 1,
            "ssip_cikdepo": 2,
            "ssip_aciklama": "",
            "ssip_birim_pntr": 1,
            "ssip_projekodu": "",
            "ssip_sormerkezi": "",
            "renk_beden":[
                    {
                        "renk_no":1,
                        "beden_no":2,
                        "miktar":1                            
                    },
                    {
                        "renk_no":2,
                        "beden_no":1,
                        "miktar":3                            
                    }
                ]
          }
        ]
      }
    ]
  }
}
```

#### Depolar Arası Sipariş Guid sil V2 Delete Guid

- Method: `POST`
- Path: `/Api/apiMethods/DepolarArasiSiparisGuidSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: GUID ile satir/kayit silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
            "ssip_Guid" : "6911C73E-53DB-49EC-93F1-95BEAD6A3FDA"
          }
        ]
      }
    ]
  }
}
```

#### Depolar Arası Sipariş Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/DepolarArasiSiparisKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "satirlar": [
          {
            "ssip_tarih": "19.12.2023",
            "ssip_teslim_tarih": "19.12.2023",
            "ssip_belge_tarih": "19.12.2023",
            "ssip_belgeno": "",
            "ssip_evrakno_seri": "DS",
            "ssip_stok_kod": "SK04",
            "ssip_b_fiyat": 15,
            "ssip_miktar": 3,
            "ssip_tutar": 1500,
            "ssip_girdepo": 1,
            "ssip_cikdepo": 2,
            "ssip_aciklama": "",
            "ssip_birim_pntr": 1,
            "ssip_projekodu": "",
            "ssip_sormerkezi": "",
            "ssip_gecerlilik_tarihi": "",
            "seriler": "A1;B1;C1",
            "user_tablo": [
              {
                "aciklama": "test sipariş user tablo"
              }
            ]
          },
          {
            "ssip_tarih": "19.12.2023",
            "ssip_teslim_tarih": "19.12.2023",
            "ssip_belge_tarih": "19.12.2023",
            "ssip_belgeno": "",
            "ssip_evrakno_seri": "DS",
            "ssip_stok_kod": "SK05",
            "ssip_b_fiyat": 45,
            "ssip_miktar": 10,
            "ssip_tutar": 450,
            "ssip_girdepo": 1,
            "ssip_cikdepo": 2,
            "ssip_aciklama": "",
            "ssip_birim_pntr": 1,
            "ssip_projekodu": "",
            "ssip_sormerkezi": "",
            "ssip_gecerlilik_tarihi": "",
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          }
        ]
      },
      {
        "satirlar": [
          {
            "ssip_tarih": "19.12.2023",
            "ssip_teslim_tarih": "19.12.2023",
            "ssip_belge_tarih": "19.12.2023",
            "ssip_belgeno": "",
            "ssip_evrakno_seri": "DS",
            "ssip_stok_kod": "SK02",
            "ssip_b_fiyat": 10,
            "ssip_miktar": 180,
            "ssip_tutar": 1800,
            "ssip_girdepo": 1,
            "ssip_cikdepo": 2,
            "ssip_aciklama": "",
            "ssip_birim_pntr": 1,
            "ssip_projekodu": "",
            "ssip_sormerkezi": "",
            "ssip_gecerlilik_tarihi": "",
            "renk_beden":[
                {                    
                    "renk_kirilim_kodu":"Yeşil",
                    "beden_kirilim_kodu":"L",
                    "miktar":50
                },
                {
                    "renk_no":1,
                    "beden_no":2,
                    "miktar":100                            
                },
                {
                    "renk_no":2,
                    "beden_no":1,
                    "miktar":30                            
                }
            ]
          }
        ]
      }
    ]
  }
}
```

#### Depolar Arası Sipariş Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/DepolarArasiSiparisSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "satirlar": [
                    {
                        "ssip_evrakno_seri": "DS",
                        "ssip_evrakno_sira": 2
                    }
                ]
            }
        ]
    }
}
```

### Etiket Basım Kaydet

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Etiket Basım Kaydet V2 Save | `POST` | `/Api/apiMethods/EtiketBasimKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |

#### Etiket Basım Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/EtiketBasimKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "satirlar": [
          {
            "Etkb_evrakno_seri": "ETK",
            "Etkb_evrak_tarihi": "21/12/2023",
            "Etkb_aciklama": "açıklama test",
            "Etkb_belge_no": "AZ1",
            "Etkb_belge_tarih": "21/12/2023",
            "Etkb_EtiketTip": 1,
            "Etkb_BasimTipi": 1,
            "Etkb_BasimAdet": 1,
            "Etkb_DepoNo": 1,
            "Etkb_StokKodu": "SK02",            
            "Etkb_BasilacakMiktar": 1,
            "user_tablo": [
              {
                "aciklama": ""
              }
            ]
          }
        ]
      },
      {
        "satirlar": [
          {
            "Etkb_evrakno_seri": "ETK",
            "Etkb_evrak_tarihi": "21/12/2023",
            "Etkb_aciklama": "açıklama test",
            "Etkb_belge_no": "AZ1",
            "Etkb_belge_tarih": "21/12/2023",
            "Etkb_EtiketTip": 1,
            "Etkb_BasimTipi": 0,
            "Etkb_BasimAdet": 1,
            "Etkb_DepoNo": 1,
            "Etkb_StokKodu": "SK04",            
            "Etkb_BasilacakMiktar": 1,
            "user_tablo": [
              {
                "aciklama": ""
              }
            ]
          }
        ]
      }
    ]
  }
}
```

### Evrak Açıklamaları

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Evrak Açıklama Düzelt V2 Update | `POST` | `/Api/apiMethods/EvrakAciklamaDuzeltV2` | top: Mikro; Mikro: CalismaYili, evrak_aciklamalari, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evrak_aciklamalari[] | Mevcut kayit/evrak guncelleme |
| 2 | Evrak Açıklama Kaydet V2 Save | `POST` | `/Api/apiMethods/EvrakAciklamaKaydetV2` | top: Mikro; Mikro: CalismaYili, evrak_aciklamalari, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evrak_aciklamalari[] | Yeni kayit/evrak olusturma |
| 3 | Evrak Açıklama Sil V2 Delete | `POST` | `/Api/apiMethods/EvrakAciklamaSilV2` | top: Mikro; Mikro: CalismaYili, evrak_aciklamalari, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evrak_aciklamalari[] | Kayit/evrak silme |

#### Evrak Açıklama Düzelt V2 Update

- Method: `POST`
- Path: `/Api/apiMethods/EvrakAciklamaDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evrak_aciklamalari, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evrak_aciklamalari[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evrak_aciklamalari": [
      {
        "egk_dosyano": 16,
        "egk_hareket_tip": 1,
        "egk_evr_tip": 1,
        "egk_evr_seri": "IRS",
        "egk_evr_sira": 18,
        "egk_evr_ustkod": "",
        "egk_evracik3":"1113",
        "egk_evracik4":"1114",
        "egk_evracik6":"1116111166"
      }
    ]
  }
}
```

#### Evrak Açıklama Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/EvrakAciklamaKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evrak_aciklamalari, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evrak_aciklamalari[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
     "evrak_aciklamalari": [
      {
        "egk_dosyano": 16,
        "egk_hareket_tip": 1,
        "egk_evr_tip": 1,
        "egk_evr_seri": "IRS",
        "egk_evr_sira": 18,
        "egk_evr_ustkod": "",
        "egk_evracik1":"ABABSABAS",
        "egk_evracik2":"ABA2",
        "egk_evracik3":"ABABSABAS333",
        "egk_evracik4":"AB4444",
        "egk_evracik5":"55555555555555"
      }
    ]
  }
}
```

#### Evrak Açıklama Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/EvrakAciklamaSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evrak_aciklamalari, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evrak_aciklamalari[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evrak_aciklamalari": [
      {
        "egk_dosyano": 16,
        "egk_hareket_tip": 1,
        "egk_evr_tip": 1,
        "egk_evr_seri": "IRS",
        "egk_evr_sira": 18,
        "egk_evr_ustkod": ""
      }
    ]
  }
}
```

### Evrak Belge Resim

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Evrak Belge Resim Kaydet V2 Save | `POST` | `/Api/apiMethods/EvrakBelgeResimKaydetV2` | top: Mikro; Mikro: CalismaYili, evrak_resimleri, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evrak_resimleri[] | Yeni kayit/evrak olusturma |
| 2 | Evrak Belge Resim Sil V2 Delete | `POST` | `/Api/apiMethods/EvrakBelgeResimSilV2` | top: Mikro; Mikro: CalismaYili, evrak_resimleri, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evrak_resimleri[] | Kayit/evrak silme |

#### Evrak Belge Resim Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/EvrakBelgeResimKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evrak_resimleri, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evrak_resimleri[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "1",
        "Sifre": "{{MikroSifreHash}}",
        "evrak_resimleri": [
            {
                "ei_dosyano": 51,
                "ei_hareket_tip": 0,
                "ei_evr_tip": 63,
                "ei_image": "C:\\Users\\ercank\\Documents\\Samples\\nakış.jpg",
                "ei_evr_seri": "A",
                "ei_evr_sira": 5,
                "ei_evr_ustkod": "",
                "ei_aciklama": "AÇÇŞŞĞİÜÖÇÖ"
            }
        ]
    }
}
```

#### Evrak Belge Resim Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/EvrakBelgeResimSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evrak_resimleri, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evrak_resimleri[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "1",
        "Sifre": "{{MikroSifreHash}}",
        "evrak_resimleri": [
            {
                "ei_Key": 6
            },
            {
                "ei_dosyano": 51,
                "ei_hareket_tip": 0,
                "ei_evr_tip": 63,
                "ei_evr_seri": "A",
                "ei_evr_sira": 5,
                "ei_evr_ustkod": ""
            }
        ]
    }
}
```

### Fiyat Değişikliği

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Fiyat Değişikliği Kaydet V2 Save | `POST` | `/Api/apiMethods/FiyatDegisikligiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |

#### Fiyat Değişikliği Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/FiyatDegisikligiKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "VERİLEN TEKLİF AÇIKLAMA-1"
                    },
                    {
                        "aciklama": "VERİLEN TEKLİF AÇIKLAMA-2"
                    },
                    {
                        "aciklama": "VERİLEN TEKLİF AÇIKLAMA-3"
                    }
                ],
                "satirlar": [
                    {
                        "fid_evrak_tarih": "20.12.2023",
                        "fid_evrak_seri_no": "S",
                        "fid_belge_no": "BBB22",
                        "fid_stok_kod": "SK02",
                        "fid_belge_tarih": "20.12.2023",
                        "fid_tarih": "20.12.2023",
                        "fid_saat": 15,
                        "fid_fiyat_no": 1,
                        "fid_yenifiy_tutar": 15,
                        "fid_depo_no": 1,
                        "fid_birim_pntr": 1,
                        "fid_fiyat_deg_neden": 5,
                        "user_tablo": [
                            {
                                "aciklama": "test veri user tablo"
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

### Image Data

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | ImageDataGetirV2 | `POST` | `/API/APIMethods/ImageDataGetirV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Image, KullaniciKodu, Sifre | Listeleme veya sorgulama |
| 2 | ImageDataKaydetV2 | `POST` | `/API/APIMethods/ImageDataKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Image, KullaniciKodu, Sifre | Listeleme veya sorgulama |
| 3 | ImageDataSilV2 | `POST` | `/API/APIMethods/ImageDataSilV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Image, KullaniciKodu, Sifre | Listeleme veya sorgulama |

#### ImageDataGetirV2

- Method: `POST`
- Path: `/API/APIMethods/ImageDataGetirV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Listeleme veya sorgulama
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, Image, KullaniciKodu, Sifre
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
      "FirmaKodu": "MIKROFLY",
      "CalismaYili": "2023",
      "KullaniciKodu": "SRV",
      "Sifre": "{{MikroSifreHash}}",
      "Image":{
          "TableID":13,
          "RecordUid":"25838eb4-98ee-4574-845c-adfb4ec998fd"          
      }
  }
}
```

#### ImageDataKaydetV2

- Method: `POST`
- Path: `/API/APIMethods/ImageDataKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Listeleme veya sorgulama
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, Image, KullaniciKodu, Sifre
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
      "FirmaKodu": "MIKROFLY",
      "CalismaYili": "2023",
      "KullaniciKodu": "SRV",
      "Sifre": "{{MikroSifreHash}}",
      "Image":{
          "TableID":13,
          "RecordUid":"0EBD2536-428D-49E6-8300-509556130837",
          "ImageData":"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"
      }
  }
}
```

#### ImageDataSilV2

- Method: `POST`
- Path: `/API/APIMethods/ImageDataSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Listeleme veya sorgulama
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, Image, KullaniciKodu, Sifre
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
      "FirmaKodu": "MIKROFLY",
      "CalismaYili": "2023",
      "KullaniciKodu": "SRV",
      "Sifre": "{{MikroSifreHash}}",
      "Image":{
          "TableID":13,
          "RecordUid":"0EBD2536-428D-49E6-8300-509556130837"          
      }
  }
}
```

### İrsaliye

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Irsaliye Düzelt Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/IrsaliyeDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 2 | Irsaliye Düzelt V2 Update | `POST` | `/Api/apiMethods/IrsaliyeDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 3 | Irsaliye Satır Sil V2 Delete Guid | `POST` | `/Api/apiMethods/IrsaliyeSatirSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| 4 | Irsaliye Sil V2 Delete | `POST` | `/Api/apiMethods/IrsaliyeSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar[] | Kayit/evrak silme |
| 5 | IrsaliyeKaydet V2 (İhracat Satış ) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 6 | IrsaliyeKaydet V2 (İhraç Kayıtlı İade Alış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 7 | IrsaliyeKaydet V2 (İhraç Kayıtlı Mal Satış ) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 8 | IrsaliyeKaydet V2 (Perakende Alış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 9 | IrsaliyeKaydet V2 (Perakende İade Alış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 10 | IrsaliyeKaydet V2 (Perakende İade Çıkış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 11 | IrsaliyeKaydet V2 (Perakende Satış ) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 12 | IrsaliyeKaydet V2 (Toptan Alış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 13 | IrsaliyeKaydet V2 (Toptan İade Alış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 14 | IrsaliyeKaydet V2 (Toptan İade Çıkış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 15 | IrsaliyeKaydet V2 (Toptan Satış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 16 | IrsaliyeKaydet V2 (Toptan Satış) Save Copy | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: ApiKey, CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 17 | Siparişten İrsaliye Oluşturma V2 Save | `POST` | `/api/APIMethods/SiparistenIrsaliyeOlusturmaV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar[] | Yeni kayit/evrak olusturma |

#### Irsaliye Düzelt Guid Ekle V2 Add Guid

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",   
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "e_irsaliye_detaylari": {
          "eir_tasiyici_firma_kodu": "",
          "eir_tasiyici_arac_plaka": "",
          "eir_tasiyici_dorse_plaka1": "",
          "eir_tasiyici_dorse_plaka2": "",
          "eir_toptanci_firma_kodu": "",
          "eir_bayi_firma_kodu": "",
          "eir_sofor_adi": "test şoför",
          "eir_sofor_soyadi": "test soyad",
          "eir_sofor2_adi": "",
          "eir_sofor2_soyadi": "",
          "eir_matbu_belgeno": "",
          "eir_matbu_tarih": "2023-11-19 00:00:00.000",
          "eir_sofor_tckn": "",
          "eir_sofor2_tckn": "",
          "eir_eirs_olrk_gonderilsin": 0,
          "eir_kargo_no": "",
          "eir_asama_no": "",
          "eir_tasima_yontemi": "",
          "eir_arac_tipi": "",
          "eir_guzergah_kodu": "",
          "eir_detay_bilgi": "",
          "eir_baslama_zamani": "2023-11-19 10:52:32.000",
          "eir_bitis_zamani": "2023-11-19 10:52:32.000"
        },      
        "satirlar": [
          {
            "sth_evraktip": "1",
            "sth_evrakno_seri": "A",    
            "sth_evrakno_sira":1,
            "sth_tarih": "21.12.2023",
            "sth_tip": "1",
            "sth_cins": "0",
            "sth_normal_iade": "0",            
            "sth_stok_kod": "SK05",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 6,
            "sth_birim_pntr": 1,
            "sth_tutar": 100,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,            
            "sth_malkbl_sevk_tarihi":"21.12.2023",
            "sth_yetkili_uid":"",
            "seriler": "",
            "renk_beden": [
              {
                "renk_no": 1,
                "beden_no": 1,
                "miktar": 3
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 3
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]           
          }		
        ]
      }
	]		
  }
}
```

#### Irsaliye Düzelt V2 Update

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",   
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
            "sth_Guid":"bb1ec73e-53db-49ec-93f1-95bead6a3fda",
            "sth_miktar": 206
          }		
        ]
      }
	]		
  }
}
```

#### Irsaliye Satır Sil V2 Delete Guid

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeSatirSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: GUID ile satir/kayit silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
            "sth_Guid" : "8B1DC73E-53DB-49EC-93F1-95BEAD6A3FDA"
          }
        ]
      }
    ]
  }
}
```

#### Irsaliye Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",   
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "sth_evraktip":1,
        "sth_tip":1,         
        "sth_evrakno_seri":"IRS", 
        "sth_evrakno_sira":8
      }
	]		
  }
}
```

#### IrsaliyeKaydet V2 (İhracat Satış ) Save

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "e_irsaliye_detaylari": {
          "eir_tasiyici_firma_kodu": "",
          "eir_tasiyici_arac_plaka": "",
          "eir_tasiyici_dorse_plaka1": "",
          "eir_tasiyici_dorse_plaka2": "",
          "eir_toptanci_firma_kodu": "",
          "eir_bayi_firma_kodu": "",
          "eir_sofor_adi": "test şoför",
          "eir_sofor_soyadi": "test soyad",
          "eir_sofor2_adi": "",
          "eir_sofor2_soyadi": "",
          "eir_matbu_belgeno": "",
          "eir_matbu_tarih": "2026-08-01 00:00:00.000",
          "eir_sofor_tckn": "",
          "eir_sofor2_tckn": "",
          "eir_eirs_olrk_gonderilsin": 0,
          "eir_kargo_no": "",
          "eir_asama_no": "",
          "eir_tasima_yontemi": "",
          "eir_arac_tipi": "",
          "eir_guzergah_kodu": "",
          "eir_detay_bilgi": "",
          "eir_baslama_zamani": "2026-08-01 10:52:32.000",
          "eir_bitis_zamani": "2026-08-01 10:52:32.000"
        },
        "satirlar": [
          {
            "sth_tarih": "20.12.2023",
            "sth_tip": "1",
            "sth_cins": "12",
            "sth_normal_iade": "0",
            "sth_evraktip": "1",
            "sth_exim_kodu": "IHR01",
            "sth_evrakno_seri": "IHR",             
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 180.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 296.61,
            "sth_vergi_pntr": 4,
            "sth_vergi": 53.39,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,            
            "sth_malkbl_sevk_tarihi":"20.12.2023",
            "sth_yetkili_uid":"",
            "seriler": "",            
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 50
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 100
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 30
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          },
          {
            "sth_tarih": "20.12.2023",
            "sth_tip": "1",
            "sth_cins": "12",
            "sth_normal_iade": "0",
            "sth_evraktip": "1",
            "sth_exim_kodu": "IHR01",
            "sth_evrakno_seri": "IHR",            
            "sth_stok_kod": "SK05",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 180.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 296.61,
            "sth_vergi_pntr": 4,
            "sth_vergi": 53.39,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,            
            "sth_malkbl_sevk_tarihi":"20.12.2023",
            "sth_yetkili_uid":"",
            "seriler": "",            
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 50
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 100
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 30
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### IrsaliyeKaydet V2 (İhraç Kayıtlı İade Alış) Save

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "e_irsaliye_detaylari": {
          "eir_tasiyici_firma_kodu": "CR01",
          "eir_tasiyici_arac_plaka": "",
          "eir_tasiyici_dorse_plaka1": "",
          "eir_tasiyici_dorse_plaka2": "",
          "eir_toptanci_firma_kodu": "",
          "eir_bayi_firma_kodu": "",
          "eir_sofor_adi": "test şoför",
          "eir_sofor_soyadi": "test soyad",
          "eir_sofor2_adi": "",
          "eir_sofor2_soyadi": "",
          "eir_matbu_belgeno": "",
          "eir_matbu_tarih": "2022-06-06 00:00:00.000",
          "eir_sofor_tckn": "",
          "eir_sofor2_tckn": "",
          "eir_eirs_olrk_gonderilsin": 0,
          "eir_kargo_no": "",
          "eir_asama_no": "",
          "eir_tasima_yontemi": "",
          "eir_arac_tipi": "",
          "eir_guzergah_kodu": "",
          "eir_detay_bilgi": "",
          "eir_baslama_zamani": "2022-06-06 10:52:32.000",
          "eir_bitis_zamani": "2022-06-06 10:52:32.000"
        },
        "satirlar": [
          {
            "sth_tarih": "21.12.2023",
            "sth_tip": "0",
            "sth_cins": "2",
            "sth_normal_iade": "1",
            "sth_evraktip": "13",
            "sth_evrakno_seri": "IKI",
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 180.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 296.61,
            "sth_vergi_pntr": 4,
            "sth_vergi": 53.39,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,
            "sth_pos_satis": 2,        
            "seriler": "",
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 50
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 100
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 30
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### IrsaliyeKaydet V2 (İhraç Kayıtlı Mal Satış ) Save

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "e_irsaliye_detaylari": {
          "eir_tasiyici_firma_kodu": "",
          "eir_tasiyici_arac_plaka": "",
          "eir_tasiyici_dorse_plaka1": "",
          "eir_tasiyici_dorse_plaka2": "",
          "eir_toptanci_firma_kodu": "",
          "eir_bayi_firma_kodu": "",
          "eir_sofor_adi": "test şoför",
          "eir_sofor_soyadi": "test soyad",
          "eir_sofor2_adi": "",
          "eir_sofor2_soyadi": "",
          "eir_matbu_belgeno": "",
          "eir_matbu_tarih": "2026-08-01 00:00:00.000",
          "eir_sofor_tckn": "",
          "eir_sofor2_tckn": "",
          "eir_eirs_olrk_gonderilsin": 0,
          "eir_kargo_no": "",
          "eir_asama_no": "",
          "eir_tasima_yontemi": "",
          "eir_arac_tipi": "",
          "eir_guzergah_kodu": "",
          "eir_detay_bilgi": "",
          "eir_baslama_zamani": "2026-08-01 10:52:32.000",
          "eir_bitis_zamani": "2026-08-01 10:52:32.000"
        },
        "satirlar": [
          {
            "sth_tarih": "20.12.2023",
            "sth_tip": "1",
            "sth_cins": "2",
            "sth_normal_iade": "0",
            "sth_evraktip": "1",
            "sth_evrakno_seri": "IH",             
            "sth_stok_kod": "SK04",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 180.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 296.61,
            "sth_vergi_pntr": 4,
            "sth_vergi": 53.39,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,            
            "sth_malkbl_sevk_tarihi":"20.12.2023",
            "sth_yetkili_uid":"",
            "seriler": "",            
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 50
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 100
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 30
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          },
          {
            "sth_tarih": "20.12.2023",
            "sth_tip": "1",
            "sth_cins": "2",
            "sth_normal_iade": "0",
            "sth_evraktip": "1",
            "sth_evrakno_seri": "IH",            
            "sth_stok_kod": "SKT01",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 180.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 296.61,
            "sth_vergi_pntr": 4,
            "sth_vergi": 53.39,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,            
            "sth_malkbl_sevk_tarihi":"20.12.2023",
            "sth_yetkili_uid":"",
            "seriler": "",            
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 50
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 100
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 30
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### IrsaliyeKaydet V2 (Perakende Alış) Save

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "e_irsaliye_detaylari": {
          "eir_tasiyici_firma_kodu": "CR01",
          "eir_tasiyici_arac_plaka": "",
          "eir_tasiyici_dorse_plaka1": "",
          "eir_tasiyici_dorse_plaka2": "",
          "eir_toptanci_firma_kodu": "",
          "eir_bayi_firma_kodu": "",
          "eir_sofor_adi": "test şoför",
          "eir_sofor_soyadi": "test soyad",
          "eir_sofor2_adi": "",
          "eir_sofor2_soyadi": "",
          "eir_matbu_belgeno": "",
          "eir_matbu_tarih": "2022-06-06 00:00:00.000",
          "eir_sofor_tckn": "",
          "eir_sofor2_tckn": "",
          "eir_eirs_olrk_gonderilsin": 0,
          "eir_kargo_no": "",
          "eir_asama_no": "",
          "eir_tasima_yontemi": "",
          "eir_arac_tipi": "",
          "eir_guzergah_kodu": "",
          "eir_detay_bilgi": "",
          "eir_baslama_zamani": "2022-06-06 10:52:32.000",
          "eir_bitis_zamani": "2022-06-06 10:52:32.000"
        },
        "satirlar": [
          {
            "sth_tarih": "21.12.2023",
            "sth_tip": "0",
            "sth_cins": "1",
            "sth_normal_iade": "0",
            "sth_evraktip": "13",
            "sth_evrakno_seri": "PAI",
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 180.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 296.61,
            "sth_vergi_pntr": 4,
            "sth_vergi": 53.39,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,
            "sth_pos_satis": 2,        
            "seriler": "",
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 50
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 100
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 30
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### IrsaliyeKaydet V2 (Perakende İade Alış) Save

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "e_irsaliye_detaylari": {
          "eir_tasiyici_firma_kodu": "CR01",
          "eir_tasiyici_arac_plaka": "",
          "eir_tasiyici_dorse_plaka1": "",
          "eir_tasiyici_dorse_plaka2": "",
          "eir_toptanci_firma_kodu": "",
          "eir_bayi_firma_kodu": "",
          "eir_sofor_adi": "test şoför",
          "eir_sofor_soyadi": "test soyad",
          "eir_sofor2_adi": "",
          "eir_sofor2_soyadi": "",
          "eir_matbu_belgeno": "",
          "eir_matbu_tarih": "2024-06-06 00:00:00.000",
          "eir_sofor_tckn": "",
          "eir_sofor2_tckn": "",
          "eir_eirs_olrk_gonderilsin": 0,
          "eir_kargo_no": "",
          "eir_asama_no": "",
          "eir_tasima_yontemi": "",
          "eir_arac_tipi": "",
          "eir_guzergah_kodu": "",
          "eir_detay_bilgi": "",
          "eir_baslama_zamani": "2024-06-06 10:52:32.000",
          "eir_bitis_zamani": "2024-06-06 10:52:32.000"
        },
        "satirlar": [
          {
            "sth_tarih": "17.01.2024",
            "sth_tip": "0",
            "sth_cins": "1",
            "sth_normal_iade": "1",
            "sth_evraktip": "13",
            "sth_evrakno_seri": "PI",
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 180.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 296.61,
            "sth_vergi_pntr": 4,
            "sth_vergi": 53.39,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,
            "sth_pos_satis": 2,        
            "seriler": "",
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 50
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 100
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 30
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### IrsaliyeKaydet V2 (Perakende İade Çıkış) Save

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "e_irsaliye_detaylari": {
          "eir_tasiyici_firma_kodu": "CR01",
          "eir_tasiyici_arac_plaka": "",
          "eir_tasiyici_dorse_plaka1": "",
          "eir_tasiyici_dorse_plaka2": "",
          "eir_toptanci_firma_kodu": "",
          "eir_bayi_firma_kodu": "",
          "eir_sofor_adi": "test şoför",
          "eir_sofor_soyadi": "test soyad",
          "eir_sofor2_adi": "",
          "eir_sofor2_soyadi": "",
          "eir_matbu_belgeno": "",
          "eir_matbu_tarih": "2024-06-06 00:00:00.000",
          "eir_sofor_tckn": "",
          "eir_sofor2_tckn": "",
          "eir_eirs_olrk_gonderilsin": 0,
          "eir_kargo_no": "",
          "eir_asama_no": "",
          "eir_tasima_yontemi": "",
          "eir_arac_tipi": "",
          "eir_guzergah_kodu": "",
          "eir_detay_bilgi": "",
          "eir_baslama_zamani": "2024-06-06 10:52:32.000",
          "eir_bitis_zamani": "2024-06-06 10:52:32.000"
        },
        "satirlar": [
          {
            "sth_tarih": "07.01.2024",
            "sth_tip": "1",
            "sth_cins": "1",
            "sth_normal_iade": "1",
            "sth_evraktip": "1",
            "sth_evrakno_seri": "PIC",
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 180.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 296.61,
            "sth_vergi_pntr": 4,
            "sth_vergi": 53.39,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,
            "sth_pos_satis": 2,        
            "seriler": "",
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 50
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 100
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 30
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### IrsaliyeKaydet V2 (Perakende Satış ) Save

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2024,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "e_irsaliye_detaylari": {
          "eir_tasiyici_firma_kodu": "",
          "eir_tasiyici_arac_plaka": "",
          "eir_tasiyici_dorse_plaka1": "",
          "eir_tasiyici_dorse_plaka2": "",
          "eir_toptanci_firma_kodu": "",
          "eir_bayi_firma_kodu": "",
          "eir_sofor_adi": "test şoför",
          "eir_sofor_soyadi": "test soyad",
          "eir_sofor2_adi": "",
          "eir_sofor2_soyadi": "",
          "eir_matbu_belgeno": "",
          "eir_matbu_tarih": "2026-08-01 00:00:00.000",
          "eir_sofor_tckn": "",
          "eir_sofor2_tckn": "",
          "eir_eirs_olrk_gonderilsin": 0,
          "eir_kargo_no": "",
          "eir_asama_no": "",
          "eir_tasima_yontemi": "",
          "eir_arac_tipi": "",
          "eir_guzergah_kodu": "",
          "eir_detay_bilgi": "",
          "eir_baslama_zamani": "2026-08-01 10:52:32.000",
          "eir_bitis_zamani": "2026-08-01 10:52:32.000"
        },
        "satirlar": [
          {
            "sth_tarih": "20.02.2024",
            "sth_tip": "1",
            "sth_cins": "1",
            "sth_normal_iade": "0",
            "sth_evraktip": "1",
            "sth_evrakno_seri": "PR",             
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 180.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 296.61,
            "sth_vergi_pntr": 4,
            "sth_vergi": 53.39,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,            
            "sth_malkbl_sevk_tarihi":"20.02.2024",
            "sth_yetkili_uid":"",
            "seriler": "",            
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 50
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 100
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 30
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          },
          {
            "sth_tarih": "20.02.2024",
            "sth_tip": "1",
            "sth_cins": "1",
            "sth_normal_iade": "0",
            "sth_evraktip": "1",
            "sth_evrakno_seri": "PR",            
            "sth_stok_kod": "SK05",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 180.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 296.61,
            "sth_vergi_pntr": 4,
            "sth_vergi": 53.39,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,            
            "sth_malkbl_sevk_tarihi":"20.02.2024",
            "sth_yetkili_uid":"",
            "seriler": "",            
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 50
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 100
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 30
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### IrsaliyeKaydet V2 (Toptan Alış) Save

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "e_irsaliye_detaylari": {
          "eir_tasiyici_firma_kodu": "CR01",
          "eir_tasiyici_arac_plaka": "",
          "eir_tasiyici_dorse_plaka1": "",
          "eir_tasiyici_dorse_plaka2": "",
          "eir_toptanci_firma_kodu": "",
          "eir_bayi_firma_kodu": "",
          "eir_sofor_adi": "test şoför",
          "eir_sofor_soyadi": "test soyad",
          "eir_sofor2_adi": "",
          "eir_sofor2_soyadi": "",
          "eir_matbu_belgeno": "",
          "eir_matbu_tarih": "2022-06-06 00:00:00.000",
          "eir_sofor_tckn": "",
          "eir_sofor2_tckn": "",
          "eir_eirs_olrk_gonderilsin": 0,
          "eir_kargo_no": "",
          "eir_asama_no": "",
          "eir_tasima_yontemi": "",
          "eir_arac_tipi": "",
          "eir_guzergah_kodu": "",
          "eir_detay_bilgi": "",
          "eir_baslama_zamani": "2022-06-06 10:52:32.000",
          "eir_bitis_zamani": "2022-06-06 10:52:32.000"
        },
        "satirlar": [
          {
            "sth_tarih": "21.12.2023",
            "sth_tip": "0",
            "sth_cins": "0",
            "sth_normal_iade": "0",
            "sth_evraktip": "13",
            "sth_evrakno_seri": "TAI",
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 180.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 296.61,
            "sth_vergi_pntr": 4,
            "sth_vergi": 53.39,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,
            "sth_pos_satis": 2,        
            "seriler": "",
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 50
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 100
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 30
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### IrsaliyeKaydet V2 (Toptan İade Alış) Save

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "e_irsaliye_detaylari": {
          "eir_tasiyici_firma_kodu": "CR01",
          "eir_tasiyici_arac_plaka": "",
          "eir_tasiyici_dorse_plaka1": "",
          "eir_tasiyici_dorse_plaka2": "",
          "eir_toptanci_firma_kodu": "",
          "eir_bayi_firma_kodu": "",
          "eir_sofor_adi": "test şoför",
          "eir_sofor_soyadi": "test soyad",
          "eir_sofor2_adi": "",
          "eir_sofor2_soyadi": "",
          "eir_matbu_belgeno": "",
          "eir_matbu_tarih": "2022-06-06 00:00:00.000",
          "eir_sofor_tckn": "",
          "eir_sofor2_tckn": "",
          "eir_eirs_olrk_gonderilsin": 0,
          "eir_kargo_no": "",
          "eir_asama_no": "",
          "eir_tasima_yontemi": "",
          "eir_arac_tipi": "",
          "eir_guzergah_kodu": "",
          "eir_detay_bilgi": "",
          "eir_baslama_zamani": "2022-06-06 10:52:32.000",
          "eir_bitis_zamani": "2022-06-06 10:52:32.000"
        },
        "satirlar": [
          {
            "sth_tarih": "21.12.2023",
            "sth_tip": "0",
            "sth_cins": "0",
            "sth_normal_iade": "1",
            "sth_evraktip": "13",
            "sth_evrakno_seri": "TI",
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 180.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 296.61,
            "sth_vergi_pntr": 4,
            "sth_vergi": 53.39,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,
            "sth_pos_satis": 2,        
            "seriler": "",
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 50
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 100
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 30
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### IrsaliyeKaydet V2 (Toptan İade Çıkış) Save

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "e_irsaliye_detaylari": {
          "eir_tasiyici_firma_kodu": "CR01",
          "eir_tasiyici_arac_plaka": "",
          "eir_tasiyici_dorse_plaka1": "",
          "eir_tasiyici_dorse_plaka2": "",
          "eir_toptanci_firma_kodu": "",
          "eir_bayi_firma_kodu": "",
          "eir_sofor_adi": "test şoför",
          "eir_sofor_soyadi": "test soyad",
          "eir_sofor2_adi": "",
          "eir_sofor2_soyadi": "",
          "eir_matbu_belgeno": "",
          "eir_matbu_tarih": "2022-06-06 00:00:00.000",
          "eir_sofor_tckn": "",
          "eir_sofor2_tckn": "",
          "eir_eirs_olrk_gonderilsin": 0,
          "eir_kargo_no": "",
          "eir_asama_no": "",
          "eir_tasima_yontemi": "",
          "eir_arac_tipi": "",
          "eir_guzergah_kodu": "",
          "eir_detay_bilgi": "",
          "eir_baslama_zamani": "2022-06-06 10:52:32.000",
          "eir_bitis_zamani": "2022-06-06 10:52:32.000"
        },
        "satirlar": [
          {
            "sth_tarih": "21.12.2023",
            "sth_tip": "1",
            "sth_cins": "0",
            "sth_normal_iade": "1",
            "sth_evraktip": "1",
            "sth_evrakno_seri": "TIC",
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 180.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 296.61,
            "sth_vergi_pntr": 4,
            "sth_vergi": 53.39,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,
            "sth_pos_satis": 2,        
            "seriler": "",
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 50
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 100
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 30
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### IrsaliyeKaydet V2 (Toptan Satış) Save

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "e_irsaliye_detaylari": {
          "eir_tasiyici_firma_kodu": "",
          "eir_tasiyici_arac_plaka": "",
          "eir_tasiyici_dorse_plaka1": "",
          "eir_tasiyici_dorse_plaka2": "",
          "eir_toptanci_firma_kodu": "",
          "eir_bayi_firma_kodu": "",
          "eir_sofor_adi": "test şoför",
          "eir_sofor_soyadi": "test soyad",
          "eir_sofor2_adi": "",
          "eir_sofor2_soyadi": "",
          "eir_matbu_belgeno": "",
          "eir_matbu_tarih": "2026-08-01 00:00:00.000",
          "eir_sofor_tckn": "",
          "eir_sofor2_tckn": "",
          "eir_eirs_olrk_gonderilsin": 0,
          "eir_kargo_no": "",
          "eir_asama_no": "",
          "eir_tasima_yontemi": "",
          "eir_arac_tipi": "",
          "eir_guzergah_kodu": "",
          "eir_detay_bilgi": "",
          "eir_baslama_zamani": "2026-08-01 10:52:32.000",
          "eir_bitis_zamani": "2026-08-01 10:52:32.000"
        },
        "satirlar": [
          {
            "sth_tarih": "07.01.2024",
            "sth_tip": "1",
            "sth_cins": "0",
            "sth_normal_iade": "0",
            "sth_evraktip": "1",
            "sth_evrakno_seri": "IRS",             
            "sth_stok_kod": "SK02",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 180.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 296.61,
            "sth_vergi_pntr": 4,
            "sth_vergi": 53.39,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,            
            "sth_malkbl_sevk_tarihi":"07.01.2024",
            "sth_yetkili_uid":"",
            "seriler": "",            
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 50
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 100
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 30
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          },
          {
            "sth_tarih": "07.01.2024",
            "sth_tip": "1",
            "sth_cins": "0",
            "sth_normal_iade": "0",
            "sth_evraktip": "1",
            "sth_evrakno_seri": "IRS",            
            "sth_stok_kod": "SK05",
            "sth_cari_cinsi": "0",
            "sth_cari_kodu": "CR01",
            "sth_miktar": 180.0,
            "sth_birim_pntr": 1,
            "sth_tutar": 296.61,
            "sth_vergi_pntr": 4,
            "sth_vergi": 53.39,
            "sth_vergisiz_fl": false,
            "sth_iskonto1": 0.0,
            "sth_iskonto2": 0.0,
            "sth_isk_mas1": "2",
            "sth_isk_mas2": "2",
            "sth_giris_depo_no": 1,
            "sth_cikis_depo_no": 1,            
            "sth_malkbl_sevk_tarihi":"07.01.2024",
            "sth_yetkili_uid":"",
            "seriler": "",            
            "renk_beden": [
              {
                "renk_kirilim_kodu": "Yeşil",
                "beden_kirilim_kodu": "L",
                "miktar": 50
              },
              {
                "renk_no": 1,
                "beden_no": 2,
                "miktar": 100
              },
              {
                "renk_no": 2,
                "beden_no": 1,
                "miktar": 30
              }
            ],
            "user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### IrsaliyeKaydet V2 (Toptan Satış) Save Copy

- Method: `POST`
- Path: `/Api/apiMethods/IrsaliyeKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: ApiKey, CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2024",
        "ApiKey": "{{MikroApiKey}}",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "FirmaNo": 0,
        "SubeNo": 0,
        "evraklar": [
            {
                "satirlar": [
                    {
                        "sth_DBCno": "0",
                        "sth_create_date": "19.02.2024",
                        "sth_tarih": "19.02.2024", 
                        "sth_tip": "1",
                        "sth_cins": "0",
                        "sth_normal_iade": "0",
                        "sth_evraktip": "1",
                        "sth_evrakno_seri": "IRS",
                        "sth_stok_kod": "SK05",
                        "sth_cari_cinsi": "0",   
                        "sth_cari_kodu": "CR01",
                        "sth_miktar": "3",   
                        "sth_birim_pntr": 1,
                        "sth_tutar": 1.61,
                        "sth_vergi_pntr": 4,
                        "sth_vergi": 53.39,
                        "sth_vergisiz_fl": false,
                        "sth_iskonto1": 0.0,
                        "sth_iskonto2": 0.0,
                        "sth_isk_mas1": "",
                        "sth_isk_mas2": "",
                        "sth_giris_depo_no": 1,
                        "sth_cikis_depo_no": 1,
                        "sth_malkbl_sevk_tarihi": "19.02.2024",
                        "seriler": ""
                    }
                ]
            }
        ]
    }
}
```

#### Siparişten İrsaliye Oluşturma V2 Save

- Method: `POST`
- Path: `/api/APIMethods/SiparistenIrsaliyeOlusturmaV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
     "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",   
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {   "irsaliye_evrak_seri":"A",
          "sip_tip" : 0, 
          "sip_cins" : 0, 
          "sip_evrakno_seri" : "T", 
          "sip_evrakno_sira" :  1     
      } 
    ]
  }
}
```

### Kasa Masraf Fişi

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Kasa Masraf Fişi Kaydet V2 Save | `POST` | `/Api/apiMethods/KasaMasrafFisiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |

#### Kasa Masraf Fişi Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/KasaMasrafFisiKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1cc"
                    },
                    {
                        "aciklama": "Test2hh"
                    },
                    {
                        "aciklama": "Testşlş"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "cha_tarihi": "09.02.2024",
                        "cha_evrakno_seri": "MS",
                        "cha_kod": "001",
                        "cha_d_cins": "0",
                        "cha_d_kur": "1",
                        "cha_kasa_hizkod": "MS01",
                        "cha_miktari": "0",
                        "cha_aratoplam": 1000,
                        "cha_srmrkkodu": "",
                        "cha_projekodu": "",
                        "cha_aciklama": "",
                        "user_tablo": [
                            {
                                "aciklama": "test stok hareket user tablo"
                            }
                        ]
                    },
                    {
                        "cha_tarihi": "09.02.2024",
                        "cha_evrakno_seri": "MS",
                        "cha_kod": "001",
                        "cha_d_cins": "0",
                        "cha_d_kur": "1",
                        "cha_kasa_hizkod": "MS01",
                        "cha_miktari": "0",
                        "cha_aratoplam": 1000,
                        "cha_srmrkkodu": "",
                        "cha_projekodu": "",
                        "cha_aciklama": "",
                        "user_tablo": [
                            {
                                "aciklama": "test stok hareket user tablo"
                            }
                        ]
                    }
                ]
            },
            {
                "satirlar": [
                    {
                        "cha_tarihi": "09.02.2024",
                        "cha_evrakno_seri": "MS",
                        "cha_kod": "001",
                        "cha_d_cins": "0",
                        "cha_d_kur": "1",
                        "cha_kasa_hizkod": "MS01",
                        "cha_miktari": "0",
                        "cha_aratoplam": 1000,
                        "cha_srmrkkodu": "",
                        "cha_projekodu": "",
                        "cha_aciklama": "",
                        "user_tablo": [
                            {
                                "aciklama": "test stok hareket user tablo"
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

### Kayıt Kaydet

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Kayıt Kaydet V2 Delete | `POST` | `/Api/apiMethods/KayitKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Kayit, KullaniciKodu, Sifre, Tablo; arrays: Mikro.Kayit[], Mikro.Tablo[] | Kayit/evrak silme |
| 2 | Kayıt Kaydet V2 Save | `POST` | `/Api/apiMethods/KayitKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Kayit, KullaniciKodu, Sifre, Tablo; arrays: Mikro.Kayit[], Mikro.Tablo[] | Yeni kayit/evrak olusturma |
| 3 | Kayıt Kaydet V2 Update | `POST` | `/Api/apiMethods/KayitKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Kayit, KullaniciKodu, Sifre, Tablo; arrays: Mikro.Kayit[], Mikro.Tablo[] | Mevcut kayit/evrak guncelleme |

#### Kayıt Kaydet V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/KayitKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, Kayit, KullaniciKodu, Sifre, Tablo; arrays: Mikro.Kayit[], Mikro.Tablo[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "Tablo": [
            {
                "No": "104",
                "KayitTipi": "2"
            }
        ],
        "Kayit": [

            {
                "cari_per_kod": "T6T6871567577512451"
            } ,
           
            {
                "cari_per_kod": "T6T687156757751232451"
            }                
        ]
        
    }
}
```

#### Kayıt Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/KayitKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, Kayit, KullaniciKodu, Sifre, Tablo; arrays: Mikro.Kayit[], Mikro.Tablo[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "Tablo": [
            {
                "No": "104",
                "KayitTipi": "0"
            }
        ],
        "Kayit": [
            {
                "cari_per_kod": "T6T6",
                "cari_per_adi": "DENEM T5T5" 
            }                                       
        ]
    }
}
```

#### Kayıt Kaydet V2 Update

- Method: `POST`
- Path: `/Api/apiMethods/KayitKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, Kayit, KullaniciKodu, Sifre, Tablo; arrays: Mikro.Kayit[], Mikro.Tablo[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "Tablo": [
            {
                "No": "-1",
                "KayitTipi": "3"
            }
        ],
        "Kayit": [
            {
                "cari_per_kod": "T6T6871567577512451",
                "cari_per_adi": "testPOSTMAN debxxxxxs1ax" 
            },
            {
                "cari_per_kod": "T6T687156757751232451",
                "cari_per_adi": "testPOSTMAN debxxxxxs1b" 
            }
        ]
    }
}
```

### Listeler

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Cari Listesi V2 | `POST` | `/Api/APIMethods/CariListesiV2` | top: FieldName, Index, Mikro, Size, Sort, WhereStr; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Listeleme veya sorgulama |
| 2 | Cari Listesi V3 | `POST` | `/Api/APIMethods/CariListesiV3` | top: CariKod, CariVKNTCNo, IlkTarih, Index, Mikro, Size, SonTarih, Sort, TarihTipi; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Listeleme veya sorgulama |
| 3 | KullaniciListesiV2 | `POST` | `/Api/APIMethods/KullaniciListesiV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Listeleme veya sorgulama |
| 4 | KullaniciParametreleriV2 | `POST` | `/Api/APIMethods/KullaniciParametreleriV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Collection icindeki Mikro API islemi |
| 5 | ModoFastSellHızlıSatisOnayDurumV2 | `POST` | `/Api/APIMethods/ModoFastsellHSSozlesmesiOnayDurumV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Collection icindeki Mikro API islemi |
| 6 | ModoFastSellRestoranOnayDurumV2 | `POST` | `/Api/APIMethods/ModoFastsellRestoranSozlesmesiOnayDurumV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Collection icindeki Mikro API islemi |
| 7 | PorkodSozlesmeOnayDurumV2 | `POST` | `/Api/APIMethods/PorkodSozlesmeOnayDurumV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Collection icindeki Mikro API islemi |
| 8 | Stok Listesi V2 | `POST` | `/Api/APIMethods/StokListesiV2` | top: IlkTarih, Index, Mikro, Size, SonTarih, Sort, StokKod, TarihTipi; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Listeleme veya sorgulama |
| 9 | VergiListesiV2 | `POST` | `/Api/APIMethods/VergiListesiV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Listeleme veya sorgulama |

#### Cari Listesi V2

- Method: `POST`
- Path: `/Api/APIMethods/CariListesiV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Listeleme veya sorgulama
- Body ozeti: top: FieldName, Index, Mikro, Size, Sort, WhereStr; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
      "FirmaKodu": "MIKROFLY",
      "CalismaYili": "2023",
      "KullaniciKodu": "SRV",
      "Sifre": "{{MikroSifreHash}}"
      },
  "FieldName": "cari_kod, cari_unvan1,cari_unvan2,cari_hareket_tipi,cari_baglanti_tipi,cari_stok_alim_cinsi,cari_stok_satim_cinsi,cari_muh_kod,cari_doviz_cinsi",
  "WhereStr": "cari_baglanti_tipi=0 and cari_lastup_date > '2020/01/01'",
  "Sort": "cari_kod, cari_unvan1",
  "Size": "5",
  "Index": 0
}
```

#### Cari Listesi V3

- Method: `POST`
- Path: `/Api/APIMethods/CariListesiV3`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Listeleme veya sorgulama
- Body ozeti: top: CariKod, CariVKNTCNo, IlkTarih, Index, Mikro, Size, SonTarih, Sort, TarihTipi; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
      "FirmaKodu": "MIKROFLY",
      "CalismaYili": "2023",
      "KullaniciKodu": "SRV",
      "Sifre": "{{MikroSifreHash}}"
      },
  "CariKod":"",  
  "CariVKNTCNo":"",
  "TarihTipi": 2,
  "IlkTarih":"1899-12-30",
  "SonTarih":"2023-12-21",
  "Sort": "-cari_kod",
  "Size": "5",
  "Index": 0
}
```

#### KullaniciListesiV2

- Method: `POST`
- Path: `/Api/APIMethods/KullaniciListesiV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Listeleme veya sorgulama
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
      "FirmaKodu": "MIKROFLY",
      "CalismaYili": "2023",
      "KullaniciKodu": "SRV",
      "Sifre": "{{MikroSifreHash}}"
      }
}
```

#### KullaniciParametreleriV2

- Method: `POST`
- Path: `/Api/APIMethods/KullaniciParametreleriV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Collection icindeki Mikro API islemi
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
      "FirmaKodu": "V16XX",
      "CalismaYili": "2023",
      "KullaniciKodu": "SRV",
      "Sifre": "{{MikroSifreHash}}"
      }
}
```

#### ModoFastSellHızlıSatisOnayDurumV2

- Method: `POST`
- Path: `/Api/APIMethods/ModoFastsellHSSozlesmesiOnayDurumV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Collection icindeki Mikro API islemi
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
      "FirmaKodu": "MIKROFLY",
      "CalismaYili": "2024",
      "KullaniciKodu": "SRV",
      "Sifre": "{{MikroSifreHash}}"
      }
}
```

#### ModoFastSellRestoranOnayDurumV2

- Method: `POST`
- Path: `/Api/APIMethods/ModoFastsellRestoranSozlesmesiOnayDurumV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Collection icindeki Mikro API islemi
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
      "FirmaKodu": "MIKROFLY",
      "CalismaYili": "2023",
      "KullaniciKodu": "SRV",
      "Sifre": "{{MikroSifreHash}}"
      }
}
```

#### PorkodSozlesmeOnayDurumV2

- Method: `POST`
- Path: `/Api/APIMethods/PorkodSozlesmeOnayDurumV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Collection icindeki Mikro API islemi
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
      "FirmaKodu": "MIKROFLY",
      "CalismaYili": "2023",
      "KullaniciKodu": "SRV",
      "Sifre": "{{MikroSifreHash}}"
      }
}
```

#### Stok Listesi V2

- Method: `POST`
- Path: `/Api/APIMethods/StokListesiV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Listeleme veya sorgulama
- Body ozeti: top: IlkTarih, Index, Mikro, Size, SonTarih, Sort, StokKod, TarihTipi; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
      "FirmaKodu": "MIKROFLY",
      "CalismaYili": "2024",
      "KullaniciKodu": "SRV",
      "Sifre": "{{MikroSifreHash}}"
      },
  "StokKod":"",  
  "TarihTipi": 2,
  "IlkTarih":"2023-01-01",
  "SonTarih":"2023-12-01",
  "Sort": "-sto_kod",
  "Size": "5",
  "Index": 0
}
```

#### VergiListesiV2

- Method: `POST`
- Path: `/Api/APIMethods/VergiListesiV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Listeleme veya sorgulama
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
      "FirmaKodu": "MIKROFLY",
      "CalismaYili": "20202322",
      "KullaniciKodu": "SRV",
      "Sifre": "{{MikroSifreHash}}"
      }
}
```

### Login-Logoff

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | APILogin | `POST` | `/Api/APIMethods/APILogin` | top: ApiKey, CalismaYili, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo | Oturum acma veya API kullanicisi dogrulama |
| 2 | HealthCheck | `GET` | `/Api/APIMethods/HealthCheck` | Body yok | Okuma/saglik kontrolu |
| 3 | HealthCheck2 | `GET` | `/Api/APIMethods/HealthCheck2` | Body yok | Okuma/saglik kontrolu |
| 4 | LoggerDone-Get | `GET` | `/Api/APIMethods/LoggerDone` | JSON body | Okuma/saglik kontrolu |
| 5 | Logoff | `POST` | `/Api/apiMethods/APILogoff` | top: KullaniciKodu | Oturumu kapatma |
| 6 | Logoff V2 | `POST` | `/Api/apiMethods/APILogoffV2` | top: KullaniciKodu, Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre | Oturumu kapatma |
| 7 | MikroApiUp | `POST` | `/Api/APIMethods/APILogin` | top: CalismaYili, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo | Oturum acma veya API kullanicisi dogrulama |

#### APILogin

- Method: `POST`
- Path: `/Api/APIMethods/APILogin`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Oturum acma veya API kullanicisi dogrulama
- Body ozeti: top: ApiKey, CalismaYili, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "FirmaKodu": "SOPHIGET",
    "CalismaYili": "2026",
    "ApiKey": "{{MikroApiKey}}",
    "KullaniciKodu": "API",
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0
}
```

#### HealthCheck

- Method: `GET`
- Path: `/Api/APIMethods/HealthCheck`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Okuma/saglik kontrolu
- Body ozeti: Body yok
- Collection response ornegi sayisi: `0`

Request body ornegi: body yok.

#### HealthCheck2

- Method: `GET`
- Path: `/Api/APIMethods/HealthCheck2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Okuma/saglik kontrolu
- Body ozeti: Body yok
- Collection response ornegi sayisi: `0`

Request body ornegi: body yok.

#### LoggerDone-Get

- Method: `GET`
- Path: `/Api/APIMethods/LoggerDone`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Okuma/saglik kontrolu
- Body ozeti: JSON body
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    
}
```

#### Logoff

- Method: `POST`
- Path: `/Api/apiMethods/APILogoff`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Oturumu kapatma
- Body ozeti: top: KullaniciKodu
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{"KullaniciKodu":"SRV"}
```

#### Logoff V2

- Method: `POST`
- Path: `/Api/apiMethods/APILogoffV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Oturumu kapatma
- Body ozeti: top: KullaniciKodu, Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "V16XX",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}"
    },
    "KullaniciKodu": "1"
}
```

#### MikroApiUp

- Method: `POST`
- Path: `/Api/APIMethods/APILogin`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Oturum acma veya API kullanicisi dogrulama
- Body ozeti: top: CalismaYili, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "FirmaKodu": "V16XX",
    "CalismaYili": "2023",
    "KullaniciKodu": "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0
}
```

### Muhasebe

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Dövizli Muhasebe Fişi Kaydet V2 Save | `POST` | `/Api/apiMethods/MuhasebeFisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 2 | Muhasebe Fişi Kaydet V2 Save | `POST` | `/Api/apiMethods/MuhasebeFisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.fis_detay[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 3 | Muhasebe Fişi Sil V2 Delete | `POST` | `/Api/apiMethods/MuhasebeFisSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| 4 | Özel Mahsup Fişi Kaydet V2 Save | `POST` | `/Api/apiMethods/MuhasebeFisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |

#### Dövizli Muhasebe Fişi Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/MuhasebeFisKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "satirlar": [
                    {
                        "fis_firmano": 0,
                        "fis_subeno": 0,
                        "fis_tarih": "21.12.2023",
                        "fis_tur": 0,
                        "fis_hesap_kod": 120,
                        "fis_aciklama1": "cari borç",
                        "fis_meblag0": 3540,
                        "fis_meblag1": 1180,
                        "fis_meblag2": 1180,
                        "fis_sorumluluk_kodu": "",
                        "fis_ticari_tip": 2,
                        "fis_kurfarkifl": 0,
                        "fis_ticari_evraktip": 63,
                        "fis_tic_belgeno": "",
                        "fis_tic_belgetarihi": "21.12.2023",
                        "fis_katagori": 0,
                        "fis_fmahsup_tipi": 0,
                        "user_tablo": []
                    },
                    {
                        "fis_firmano": 0,
                        "fis_subeno": 0,
                        "fis_tarih": "21.12.2023",
                        "fis_tur": 0,
                        "fis_hesap_kod": 391,
                        "fis_aciklama1": "kdv alacak",
                        "fis_meblag0": -540,
                        "fis_meblag1": -180,
                        "fis_meblag2": -180,
                        "fis_sorumluluk_kodu": "",
                        "fis_ticari_tip": 2,
                        "fis_kurfarkifl": 0,
                        "fis_ticari_evraktip": 63,
                        "fis_tic_belgeno": "",
                        "fis_tic_belgetarihi": "21.12.2023",
                        "fis_katagori": 0,
                        "fis_fmahsup_tipi": 0,
                        "user_tablo": []
                    },
                    {
                        "fis_firmano": 0,
                        "fis_subeno": 0,
                        "fis_tarih": "21.12.2023",
                        "fis_tur": 0,
                        "fis_hesap_kod": 600,
                        "fis_aciklama1": "mal alacak",
                        "fis_meblag0": -3000,
                        "fis_meblag1": -1000,
                        "fis_meblag2": -1000,
                        "fis_sorumluluk_kodu": "",
                        "fis_ticari_tip": 2,
                        "fis_kurfarkifl": 0,
                        "fis_ticari_evraktip": 63,
                        "fis_tic_belgeno": "",
                        "fis_tic_belgetarihi": "21.12.2023",
                        "fis_katagori": 0,
                        "fis_fmahsup_tipi": 0,
                        "user_tablo": []
                    }
                ]
            }
        ]
    }
}
```

#### Muhasebe Fişi Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/MuhasebeFisKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.fis_detay[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1cc"
                    },
                    {
                        "aciklama": "Test2hh"
                    },
                    {
                        "aciklama": "Testşlş"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "fis_firmano": 0,
                        "fis_subeno": 0,
                        "fis_tarih": "21.12.2023",
                        "fis_tur": 0,
                        "fis_hesap_kod": 120,
                        "fis_aciklama1": "cari borç",
                        "fis_meblag0": 1180,
                        "fis_sorumluluk_kodu": "",
                        "fis_ticari_tip": 2,
                        "fis_kurfarkifl": 0,
                        "fis_ticari_evraktip": 63,
                        "fis_tic_belgeno": "",
                        "fis_tic_belgetarihi": "21.12.2023",
                        "fis_katagori": 0,
                        "fis_fmahsup_tipi": 0,
                        "user_tablo": []
                    },
                    {
                        "fis_firmano": 0,
                        "fis_subeno": 0,
                        "fis_tarih": "21.12.2023",
                        "fis_tur": 0,
                        "fis_hesap_kod": 391,
                        "fis_aciklama1": "kdv alacak",
                        "fis_meblag0": -180,
                        "fis_sorumluluk_kodu": "",
                        "fis_ticari_tip": 2,
                        "fis_kurfarkifl": 0,
                        "fis_ticari_evraktip": 63,
                        "fis_tic_belgeno": "",
                        "fis_tic_belgetarihi": "21.12.2023",
                        "fis_katagori": 0,
                        "fis_fmahsup_tipi": 0,
                        "user_tablo": []
                    },
                    {
                        "fis_firmano": 0,
                        "fis_subeno": 0,
                        "fis_tarih": "21.12.2023",
                        "fis_tur": 0,
                        "fis_hesap_kod": 600,
                        "fis_aciklama1": "mal alacak",
                        "fis_meblag0": -1000,
                        "fis_sorumluluk_kodu": "",
                        "fis_ticari_tip": 2,
                        "fis_kurfarkifl": 0,
                        "fis_ticari_evraktip": 63,
                        "fis_tic_belgeno": "",
                        "fis_tic_belgetarihi": "21.12.2023",
                        "fis_katagori": 0,
                        "fis_fmahsup_tipi": 0,
                        "user_tablo": []
                    }
                ],
                "fis_detay": [
                    {
                        "mfd_ticari_tip": 2,
                        "mfd_evraktip": 63,
                        "mfd_cariunvan": "MÜŞTERİMİZ",
                        "mfd_carivergidaireadi": "",
                        "mfd_carivergidaireno": 1234567890,
                        "mfd_bsbakonututar": 1000,
                        "mfd_bsbatabii": 1,
                        "mfd_cariulkekodno": "052",
                        "mfd_belgetarihi": "21.12.2023",
                        "mfd_tutarnereden": 0,
                        "mfd_caritipi": 1,
                        "mfd_carikodu": "MUSTERI",
                        "mfd_carimuhkodu": 120,
                        "mfd_belgeno": "",
                        "mfd_kdvid": 0,
                        "mfd_kdvtutar": 0,
                        "mfd_kisaevraktipi": 1,
                        "mfd_satistipi": 0,
                        "mfd_alistipi": 0,
                        "mfd_tahtedtipi": 0,
                        "mfd_digerevrakadi": "Satış faturası",
                        "mfd_evraktur": 0
                    }
                ]
            },
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1cc"
                    },
                    {
                        "aciklama": "Test2hh"
                    },
                    {
                        "aciklama": "Testşlş"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "fis_firmano": 0,
                        "fis_subeno": 0,
                        "fis_tarih": "21.12.2023",
                        "fis_tur": 0,
                        "fis_hesap_kod": 120,
                        "fis_aciklama1": "cari borç",
                        "fis_meblag0": 1180,
                        "fis_sorumluluk_kodu": "",
                        "fis_ticari_tip": 2,
                        "fis_kurfarkifl": 0,
                        "fis_ticari_evraktip": 63,
                        "fis_tic_belgeno": "",
                        "fis_tic_belgetarihi": "21.12.2023",
                        "fis_katagori": 0,
                        "fis_fmahsup_tipi": 0,
                        "user_tablo": []
                    },
                    {
                        "fis_firmano": 0,
                        "fis_subeno": 0,
                        "fis_tarih": "21.12.2023",
                        "fis_tur": 0,
                        "fis_hesap_kod": 391,
                        "fis_aciklama1": "kdv alacak",
                        "fis_meblag0": -180,
                        "fis_sorumluluk_kodu": "",
                        "fis_ticari_tip": 2,
                        "fis_kurfarkifl": 0,
                        "fis_ticari_evraktip": 63,
                        "fis_tic_belgeno": "",
                        "fis_tic_belgetarihi": "21.12.2023",
                        "fis_katagori": 0,
                        "fis_fmahsup_tipi": 0,
                        "user_tablo": []
                    },
                    {
                        "fis_firmano": 0,
                        "fis_subeno": 0,
                        "fis_tarih": "21.12.2023",
                        "fis_tur": 0,
                        "fis_hesap_kod": 600,
                        "fis_aciklama1": "mal alacak",
                        "fis_meblag0": -1000,
                        "fis_sorumluluk_kodu": "",
                        "fis_ticari_tip": 2,
                        "fis_kurfarkifl": 0,
                        "fis_ticari_evraktip": 63,
                        "fis_tic_belgeno": "",
                        "fis_tic_belgetarihi": "21.12.2023",
                        "fis_katagori": 0,
                        "fis_fmahsup_tipi": 0,
                        "user_tablo": []
                    }
                ],
                "fis_detay": [
                    {
                        "mfd_ticari_tip": 2,
                        "mfd_evraktip": 63,
                        "mfd_cariunvan": "MÜŞTERİMİZ",
                        "mfd_carivergidaireadi": "",
                        "mfd_carivergidaireno": 1234567890,
                        "mfd_bsbakonututar": 1000,
                        "mfd_bsbatabii": 1,
                        "mfd_cariulkekodno": "052",
                        "mfd_belgetarihi": "21.12.2023",
                        "mfd_tutarnereden": 0,
                        "mfd_caritipi": 1,
                        "mfd_carikodu": "MUSTERI",
                        "mfd_carimuhkodu": 120,
                        "mfd_belgeno": "",
                        "mfd_kdvid": 0,
                        "mfd_kdvtutar": 0,
                        "mfd_kisaevraktipi": 1,
                        "mfd_satistipi": 0,
                        "mfd_alistipi": 0,
                        "mfd_tahtedtipi": 0,
                        "mfd_digerevrakadi": "Satış faturası",
                        "mfd_evraktur": 0
                    }
                ]
            }
        ]
    }
}
```

#### Muhasebe Fişi Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/MuhasebeFisSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "satirlar": [
                    {
                        "fis_tarih": "21.12.2023",
                        "fis_sira_no": 3
                    }
                ]
            }
        ]
    }
}
```

#### Özel Mahsup Fişi Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/MuhasebeFisKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "satirlar": [
                    {
                        "fis_firmano": 0,
                        "fis_subeno": 0,
                        "fis_tarih": "21.12.2023",
                        "fis_tur": 0,
                        "fis_hesap_kod": 120,
                        "fis_aciklama1": "cari borç",
                        "fis_meblag0": 3540,
                        "fis_sorumluluk_kodu": "",
                        "fis_ticari_tip": 0,
                        "fis_kurfarkifl": 0,
                        "fis_ticari_evraktip": 0,
                        "fis_tic_belgeno": "",
                        "fis_tic_belgetarihi": "21.12.2023",
                        "fis_katagori": 0,
                        "fis_fmahsup_tipi": 11,
                        "user_tablo": []
                    },
                    {
                        "fis_firmano": 0,
                        "fis_subeno": 0,
                        "fis_tarih": "21.12.2023",
                        "fis_tur": 0,
                        "fis_hesap_kod": 391,
                        "fis_aciklama1": "kdv alacak",
                        "fis_meblag0": -180,
                        "fis_sorumluluk_kodu": "",
                        "fis_ticari_tip": 0,
                        "fis_kurfarkifl": 0,
                        "fis_ticari_evraktip": 0,
                        "fis_tic_belgeno": "",
                        "fis_tic_belgetarihi": "21.12.2023",
                        "fis_katagori": 0,
                        "fis_fmahsup_tipi": 11,
                        "user_tablo": []
                    },
                    {
                        "fis_firmano": 0,
                        "fis_subeno": 0,
                        "fis_tarih": "21.12.2023",
                        "fis_tur": 0,
                        "fis_hesap_kod": 600,
                        "fis_aciklama1": "mal alacak",
                        "fis_meblag0": -1000,
                        "fis_sorumluluk_kodu": "",
                        "fis_ticari_tip": 0,
                        "fis_kurfarkifl": 0,
                        "fis_ticari_evraktip": 0,
                        "fis_tic_belgeno": "",
                        "fis_tic_belgetarihi": "21.12.2023",
                        "fis_katagori": 0,
                        "fis_fmahsup_tipi": 11,
                        "user_tablo": []
                    }
                ]
            }
        ]
    }
}
```

### Operasyon Tamamlama Fişi

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Operasyon Tamamlama Fişi Sil V2 Delete | `POST` | `/Api/apiMethods/OperasyonTamamlamaFisSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| 2 | Operasyon Tamamlama Fşi Kaydet V2 Save | `POST` | `/Api/apiMethods/OperasyonTamamlamaFisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.calisan_listesi[], Mikro.evraklar.satirlar.gecikme_listesi[], Mikro.evraklar.satirlar.hata_listesi[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |

#### Operasyon Tamamlama Fişi Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/OperasyonTamamlamaFisSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "satirlar": [
                    {
                        "OpT_EvrakNoSeri": "A",
                        "OpT_EvrakNoSira": 1
                    }
                ]
            },
            {
                "satirlar": [
                    {
                        "OpT_EvrakNoSeri": "A",
                        "OpT_EvrakNoSira": 2
                    }
                ]
            }
        ]
    }
}
```

#### Operasyon Tamamlama Fşi Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/OperasyonTamamlamaFisKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.calisan_listesi[], Mikro.evraklar.satirlar.gecikme_listesi[], Mikro.evraklar.satirlar.hata_listesi[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "op_tam açıklama 1"
                    },
                    {
                        "aciklama": "op_tam açıklama 2"
                    },
                    {
                        "aciklama": "op_tam açıklama 3"
                    },
                    {
                        "aciklama": "op_tam açıklama 4"
                    }
                ],
                "satirlar": [
                    {
                        "OpT_EvrakNoSeri": "A",
                        "OpT_EvrakNoSira": "1",
                        "OpT_IsEmriKodu": "0000003 0001",
                        "OpT_OperasyonSafhaNo": 1,
                        "user_tablo": [
                            {
                                "aciklama": "test veri user tablo"
                            }
                        ],
                        "hata_listesi": [
                            {
                                "hata_kodlari": "'001','002'",
                                "personel": "PERSONEL.01",
                                "tarih": "21.12.2023",
                                "hatali_miktar": 5
                            }
                        ],
                        "gecikme_listesi": [
                            {
                                "gecikme_kodu": "01",
                                "gecikme_bosluk_baslama_tarihi": "21.12.2023",
                                "gecikme_bosluk_baslama_saati": "15:00",
                                "gecikme_bosluk_bitis_tarihi": "21.12.2023",
                                "gecikme_bosluk_bitis_saati": "19:00",
                                "gecikme_suresi": "00:03:00:00",
                                "aciklama": "gecikme 1"
                            }
                        ],
                        "calisan_listesi": [
                            {
                                "personel_kodu": "PERSONEL.02",
                                "calistigi_sure": "00:03:00:00"
                            },
                            {
                                "personel_kodu": "PERSONEL.03",
                                "calistigi_sure": "00:05:00:00"
                            }
                        ]
                    },
                    {
                        "OpT_EvrakNoSeri": "A",
                        "OpT_EvrakNoSira": "1",
                        "OpT_IsEmriKodu": "0000003 0001",
                        "OpT_OperasyonSafhaNo": 0
                    },
                    {
                        "OpT_EvrakNoSeri": "A",
                        "OpT_EvrakNoSira": "1",
                        "OpT_IsEmriKodu": "000002 001",
                        "OpT_OperasyonSafhaNo": 0,
                        "OpT_OperasyonKodu": "OPERSAYON.01",
                        "OpT_UrunKodu": "010",
                        "OpT_ismerkezi": "ISMRK.01",
                        "OpT_TamamlananMiktar": 25
                    }
                ]
            },
            {
                "satirlar": [
                    {
                        "OpT_EvrakNoSeri": "A",
                        "OpT_IsEmriKodu": "000002 001",
                        "OpT_OperasyonSafhaNo": 0,
                        "OpT_OperasyonKodu": "OPERSAYON.01",
                        "OpT_UrunKodu": "11745",
                        "OpT_ismerkezi": "ISMRK.01",
                        "renk_beden": [
                            {
                                "renk_kirilim_kodu": "Kızıl kahve",
                                "beden_kirilim_kodu": "37",
                                "miktar": 2
                            },
                            {
                                "renk_no": 5,
                                "beden_no": 3,
                                "miktar": 1
                            },
                            {
                                "renk_no": 5,
                                "beden_no": 4,
                                "miktar": 1
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

### Personel

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Personel izin kaydet V2 Save | `POST` | `/API/APIMethods/PersonelizinKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, personelizinler, Sifre; arrays: Mikro.personelizinler[] | Yeni kayit/evrak olusturma |
| 2 | Personel Kaydet V2 Save | `POST` | `/API/APIMethods/PersonelKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, personeller, Sifre; arrays: Mikro.personeller[] | Yeni kayit/evrak olusturma |

#### Personel izin kaydet V2 Save

- Method: `POST`
- Path: `/API/APIMethods/PersonelizinKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, personelizinler, Sifre; arrays: Mikro.personelizinler[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "personelizinler": [
            {
                "pz_izin_yil": "2023",
                "pz_pers_kod": "1278",
                "pz_gun_sayisi": "2",
                "pz_baslangictarih": "02.12.2023",
                "pz_izin_aciklama": "DENEME YAPIYORUZ",
                "pz_bitistarihi": "20.12.2023",
                "pz_isbasitarihi": "21.12.2023"
            }
        ]
    }
}
```

#### Personel Kaydet V2 Save

- Method: `POST`
- Path: `/API/APIMethods/PersonelKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, personeller, Sifre; arrays: Mikro.personeller[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "personeller": [
      {
        "per_kod": "1278",
        "per_adi": "yeni cari unvand 2",
        "per_soyadi": "yeni cari unvan3",
        "per_ucret": "10000",
        "per_muh_grpkod": "",
        "per_muh_ozelc1": ""
      }
    ]
  }
}
```

### Proforma Sipariş

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Proforma Sipariş Kaydet V2 Save | `POST` | `/Api/apiMethods/ProformaSiparisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 2 | Proforma Sipariş Sil V2 Delete | `POST` | `/Api/apiMethods/ProformaSiparisSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |

#### Proforma Sipariş Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/ProformaSiparisKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1cc"
                    },
                    {
                        "aciklama": "Test2hh"
                    },
                    {
                        "aciklama": "Testşlş"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "pro_tarihi": "19.12.2023",
                        "pro_tipi": "0",
                        "pro_cinsi": "2",
                        "pro_evrakno_seri": "ProS",
                        "pro_mustkodu": "CR01",
                        "pro_stokkodu": "SK02",
                        "pro_bfiyati": 15,
                        "pro_miktar": 3,
                        "pro_birim_pntr": 1,
                        "pro_tutari": 45,
                        "pro_vergipntr": 4,
                        "pro_depono": 1,
                        "pro_vergisiz": false,
                        "seriler": "A1;B1;C1",
                        "pro_stok_sormerk": "",
                        "user_tablo": [
                            {
                                "aciklama": "test sipariş user tablo"
                            }
                        ]
                    },
                    {
                        "pro_tarihi": "19.12.2023",
                        "pro_tipi": "0",
                        "pro_cinsi": "2",
                        "pro_evrakno_seri": "ProS",
                        "pro_mustkodu": "CR01",
                        "pro_stokkodu": "SK05",
                        "pro_bfiyati": 20,
                        "pro_miktar": 180,
                        "pro_birim_pntr": 1,
                        "pro_tutari": 3600,
                        "pro_vergipntr": 4,
                        "pro_depono": 1,
                        "pro_vergisiz": false,
                        "pro_stok_sormerk": "",
                        "renk_beden": [
                            {
                                "renk_kirilim_kodu": "Yeşil",
                                "beden_kirilim_kodu": "L",
                                "miktar": 50
                            },
                            {
                                "renk_no": 1,
                                "beden_no": 2,
                                "miktar": 100
                            },
                            {
                                "renk_no": 2,
                                "beden_no": 1,
                                "miktar": 30
                            }
                        ],
                        "user_tablo": [
                            {
                                "aciklama": "test stok hareket user tablo"
                            }
                        ]
                    }
                ]
            },
            {
                "satirlar": [
                    {
                        "pro_tarihi": "19.12.2023",
                        "pro_tipi": "0",
                        "pro_cinsi": "2",
                        "pro_evrakno_seri": "ProS",
                        "pro_mustkodu": "CR01",
                        "pro_stokkodu": "SK04",
                        "pro_bfiyati": 15,
                        "pro_miktar": 20,
                        "pro_birim_pntr": 1,
                        "pro_tutari": 300,
                        "pro_vergipntr": 4,
                        "pro_depono": 1,
                        "pro_vergisiz": false
                    }
                ]
            }
        ]
    }
}
```

#### Proforma Sipariş Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/ProformaSiparisSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "satirlar": [
                    {
                        "pro_tipi": 0,
                        "pro_cinsi": 2,
                        "pro_evrakno_seri": "ProS",
                        "pro_evrakno_sira": 1
                    }
                ]
            }
        ]
    }
}
```

### Satın Alma Talep

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Satın Alma Talep Sil V2 Delete | `POST` | `/api/APIMethods/SatinAlmaTalepSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| 2 | Satın Alma Talep V2 Save | `POST` | `/api/APIMethods/SatinAlmaTalepKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |

#### Satın Alma Talep Sil V2 Delete

- Method: `POST`
- Path: `/api/APIMethods/SatinAlmaTalepSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "satirlar": [
                    {
                        "stl_evrak_seri": "SAT",
                        "stl_evrak_sira": 2
                    }
                ]
            }
        ]
    }
}
```

#### Satın Alma Talep V2 Save

- Method: `POST`
- Path: `/api/APIMethods/SatinAlmaTalepKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1cc"
                    },
                    {
                        "aciklama": "Test2hh"
                    },
                    {
                        "aciklama": "Testşlş"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "stl_tarihi": "20.12.2023",
                        "stl_belge_no": "20210000124563",
                        "stl_teslim_tarihi": "20.12.2023",
                        "stl_belge_tarihi": "20.12.2023",
                        "stl_evrak_seri": "SAT",
                        "stl_Stok_kodu": "SK02",
                        "stl_Satici_Kodu": "",
                        "stl_projekodu": "",
                        "stl_Sor_Merk": "",
                        "stl_miktari": 360,
                        "stl_teslim_miktari": 0,
                        "stl_cagrilabilir_fl": 1,
                        "stl_parti_kodu": "",
                        "stl_lot_no": 0,
                        "stl_talep_eden": "",
                        "renk_beden": [
                            {
                                "renk_kirilim_kodu": "Yeşil",
                                "beden_kirilim_kodu": "L",
                                "miktar": 20
                            },
                            {
                                "renk_no": 1,
                                "beden_no": 2,
                                "miktar": 300
                            },
                            {
                                "renk_no": 2,
                                "beden_no": 1,
                                "miktar": 40
                            }
                        ],
                        "user_tablo": [
                            {
                                "aciklama": "test SAT user tablo"
                            }
                        ]
                    },
                    {
                        "stl_tarihi": "20.12.2023",
                        "stl_belge_no": "20210000124563",
                        "stl_teslim_tarihi": "20.12.2023",
                        "stl_belge_tarihi": "20.12.2023",
                        "stl_evrak_seri": "SAT",
                        "stl_Stok_kodu": "SK05",
                        "stl_Satici_Kodu": "",
                        "stl_projekodu": "",
                        "stl_Sor_Merk": "",
                        "stl_miktari": 100,
                        "stl_teslim_miktari": 0,
                        "stl_cagrilabilir_fl": 1,
                        "stl_parti_kodu": "",
                        "stl_lot_no": 0,
                        "stl_talep_eden": ""
                    }
                ]
            }
        ]
    }
}
```

### Satış Şartı

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Satış Şartı Düzelt V2 Update | `POST` | `/api/APIMethods/SatisSartiDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 2 | Satış Şartı Guid Ekle V2 Add Guid | `POST` | `/api/APIMethods/SatisSartiDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 3 | Satış Şartı Guid Sil V2 Delete Guid | `POST` | `/api/APIMethods/SatisSartiGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| 4 | Satış Şartı Kaydet V2 Save | `POST` | `/api/APIMethods/SatisSartiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 5 | Satış Şartı Sil V2 Delete | `POST` | `/api/APIMethods/SatisSartiSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |

#### Satış Şartı Düzelt V2 Update

- Method: `POST`
- Path: `/api/APIMethods/SatisSartiDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      { 
        "satirlar": [
          {
            "sat_Guid": "44a3d109-162a-4267-af76-d5bffe45f374",
            "sat_miktar": 5 
         }
        ]
      }
    ]
  }
}
```

#### Satış Şartı Guid Ekle V2 Add Guid

- Method: `POST`
- Path: `/api/APIMethods/SatisSartiDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "satirlar": [
          {
            "sat_evrak_tarih": "23.01.2024",
            "sat_belge_no": "",
            "sat_belge_tarih": "23.01.2024",
            "sat_evrakno_seri": "SS",
            "sat_evrakno_sira": 1,            
            "sat_stok_kod": "SK05",
            "sat_cari_kod": "CR01",
            "sat_basla_tarih": "23.01.2024",
            "sat_bitis_tarih": "23.01.2024",
            "sat_brut_fiyat": 0,
            "sat_det_isk_uyg1": 0,
            "sat_det_isk_yuzde1": 10,
            "sat_det_isk_uyg2": 1,
            "sat_det_isk_yuzde2": 0,
            "sat_det_isk_uyg3": 1,
            "sat_det_isk_yuzde3": 0,
            "sat_det_mas_uyg1": 0,
            "sat_det_mas_yuzde1": 0,
            "sat_det_mas_uyg2": 1,
            "sat_det_mas_yuzde2": 0,
            "sat_det_mas_uyg3": 1,
            "sat_det_mas_yuzde3": 0,
            "sat_odeme_plan": 0,
            "sat_doviz_cinsi": 0,
            "sat_depo_no": 0,
            "sat_miktar_tip": 0,
            "sat_miktar": 100,
            "sat_proje_kodu": "",
            "sat_srmmrk_kodu": "",
            "user_tablo": [
              {
                "aciklama": "test SAT user tablo"
              }
            ]
          }
        ]
      }    
    ]
  }
}
```

#### Satış Şartı Guid Sil V2 Delete Guid

- Method: `POST`
- Path: `/api/APIMethods/SatisSartiGuidSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: GUID ile satir/kayit silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
			"sat_Guid": "EF0A7451-A92F-42C8-9D7B-BB95207AAF14"
          }
        ]
      }
	]		
  }
}
```

#### Satış Şartı Kaydet V2 Save

- Method: `POST`
- Path: `/api/APIMethods/SatisSartiKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2024,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "satirlar": [
          {
            "sat_evrak_tarih": "23.01.2024",
            "sat_belge_no": "",
            "sat_belge_tarih": "23.01.2024",
            "sat_evrakno_seri": "SS",
            "sat_stok_kod": "SK05",
            "sat_cari_kod": "CR01",
            "sat_basla_tarih": "23.01.2024",
            "sat_bitis_tarih": "23.01.2024",
            "sat_brut_fiyat": 0,
            "sat_det_isk_uyg1": 0,
            "sat_det_isk_yuzde1": 10,
            "sat_det_isk_uyg2": 1,
            "sat_det_isk_yuzde2": 0,
            "sat_det_isk_uyg3": 1,
            "sat_det_isk_yuzde3": 0,
            "sat_det_mas_uyg1": 0,
            "sat_det_mas_yuzde1": 0,
            "sat_det_mas_uyg2": 1,
            "sat_det_mas_yuzde2": 0,
            "sat_det_mas_uyg3": 1,
            "sat_det_mas_yuzde3": 0,
            "sat_odeme_plan": 0,
            "sat_doviz_cinsi": 0,
            "sat_depo_no": 0,
            "sat_miktar_tip": 0,
            "sat_miktar": 100,
            "sat_proje_kodu": "",
            "sat_srmmrk_kodu": "",
            "user_tablo": [
              {
                "aciklama": "test SAT user tablo"
              }
            ]
          },
          {
            "sat_evrak_tarih": "23.01.2024",
            "sat_belge_no": "20210000124563",
            "sat_belge_tarih": "23.01.2024",
            "sat_evrakno_seri": "SS",
            "sat_stok_kod": "SK04",
            "sat_cari_kod": "CR01",
            "sat_basla_tarih": "23.01.2024",
            "sat_bitis_tarih": "23.01.2024",
            "sat_brut_fiyat": 0,
            "sat_det_isk_uyg1": 0,
            "sat_det_isk_yuzde1": 10,
            "sat_det_isk_uyg2": 1,
            "sat_det_isk_yuzde2": 0,
            "sat_det_isk_uyg3": 1,
            "sat_det_isk_yuzde3": 0,
            "sat_det_mas_uyg1": 0,
            "sat_det_mas_yuzde1": 0,
            "sat_det_mas_uyg2": 1,
            "sat_det_mas_yuzde2": 0,
            "sat_det_mas_uyg3": 1,
            "sat_det_mas_yuzde3": 0,
            "sat_odeme_plan": 0,
            "sat_doviz_cinsi": 0,
            "sat_depo_no": 0,
            "sat_miktar_tip": 0,
            "sat_miktar": 100,
            "sat_proje_kodu": "",
            "sat_srmmrk_kodu": ""
          }
        ]
      },
      {
        "satirlar": [
          {
            "sat_evrak_tarih": "23.01.2024",
            "sat_belge_no": "",
            "sat_belge_tarih": "23.01.2024",
            "sat_evrakno_seri": "SS",
            "sat_stok_kod": "SK02",
            "sat_cari_kod": "CR01",
            "sat_basla_tarih": "23.01.2024",
            "sat_bitis_tarih": "23.01.2024",
            "sat_brut_fiyat": 100,
            "sat_det_isk_uyg1": 0,
            "sat_det_isk_yuzde1": 10,
            "sat_det_isk_uyg2": 1,
            "sat_det_isk_yuzde2": 0,
            "sat_det_isk_uyg3": 1,
            "sat_det_isk_yuzde3": 0,
            "sat_det_mas_uyg1": 0,
            "sat_det_mas_yuzde1": 0,
            "sat_det_mas_uyg2": 1,
            "sat_det_mas_yuzde2": 0,
            "sat_det_mas_uyg3": 1,
            "sat_det_mas_yuzde3": 0,
            "sat_odeme_plan": 0,
            "sat_doviz_cinsi": 0,
            "sat_depo_no": 0,
            "sat_miktar_tip": 0,
            "sat_miktar": 100,
            "sat_proje_kodu": "",
            "sat_srmmrk_kodu": "",
            "user_tablo": [
              {
                "aciklama": "test SAT user tablo"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### Satış Şartı Sil V2 Delete

- Method: `POST`
- Path: `/api/APIMethods/SatisSartiSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
			"sat_evrakno_seri": "SS",
            "sat_evrakno_sira":7
          }
        ]
      }
	]		
  }
}
```

### Satin Alma Şartı

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Satın Alma Şartı Kaydet V2 Save | `POST` | `/api/APIMethods/SatinAlmaSartiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 2 | Satın Alma Şartı Sil V2 Delete | `POST` | `/api/APIMethods/SatinAlmaSartiSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |

#### Satın Alma Şartı Kaydet V2 Save

- Method: `POST`
- Path: `/api/APIMethods/SatinAlmaSartiKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "satirlar": [
          {
            "sas_evrak_tarih": "19.12.2023",
            "sas_belge_no": "",
            "sas_belge_tarih": "19.12.2023",
            "sas_evrak_no_seri": "S",
            "sas_stok_kod": "SK05DD",
            "sas_cari_kod": "CR01",
            "sas_basla_tarih": "19.12.2023",
            "sas_bitis_tarih": "19.12.2023",
            "sas_brut_fiyat": 100,
            "sas_isk_uyg1": 0,
            "sas_isk_yuzde1": 10,
            "sas_isk_uyg2": 1,
            "sas_isk_yuzde2": 0,
            "sas_isk_uyg3": 1,
            "sas_isk_yuzde3": 0,
            "sas_mas_uyg1": 0,
            "sas_mas_yuzde1": 0,
            "sas_mas_uyg2": 1,
            "sas_mas_yuzde2": 0,
            "sas_mas_uyg3": 1,
            "sas_mas_yuzde3": 0,
            "sas_odeme_plan": 0,
            "sas_kar_oran": 15,
            "sas_doviz_cinsi": 0,
            "sas_aciklama": "",
            "sas_depo_no": 0,
            "sas_miktar_tip": 0,
            "sas_miktar": 100,
            "sas_proje_kodu": "",
            "sas_srmmrk_kodu": "",
            "user_tablo": [
              {
                "aciklama": "test SAS user tablo"
              }
            ]
          },
          {
            "sas_evrak_tarih": "19.12.2023",
            "sas_belge_no": "20210000124563",
            "sas_belge_tarih": "19.12.2023",
            "sas_evrak_no_seri": "S",
            "sas_stok_kod": "SK04",
            "sas_cari_kod": "CR01",
            "sas_basla_tarih": "19.12.2023",
            "sas_bitis_tarih": "19.12.2023",
            "sas_brut_fiyat": 100,
            "sas_isk_uyg1": 0,
            "sas_isk_yuzde1": 10,
            "sas_isk_uyg2": 1,
            "sas_isk_yuzde2": 20,
            "sas_isk_uyg3": 1,
            "sas_isk_yuzde3": 0,
            "sas_mas_uyg1": 0,
            "sas_mas_yuzde1": 0,
            "sas_mas_uyg2": 0,
            "sas_mas_yuzde2": 0,
            "sas_mas_uyg3": 0,
            "sas_mas_yuzde3": 0,
            "sas_odeme_plan": 0,
            "sas_kar_oran": 15,
            "sas_doviz_cinsi": 0,
            "sas_aciklama": "",
            "sas_depo_no": 0,
            "sas_miktar_tip": 0,
            "sas_miktar": 100,
            "sas_proje_kodu": "",
            "sas_srmmrk_kodu": ""
          }
        ]
      },
      {
        "satirlar": [
          {
            "sas_evrak_tarih": "19.12.2023",
            "sas_belge_no": "",
            "sas_belge_tarih": "19.12.2023",
            "sas_evrak_no_seri": "S",
            "sas_stok_kod": "SK02",
            "sas_cari_kod": "CR01",
            "sas_basla_tarih": "19.12.2023",
            "sas_bitis_tarih": "19.12.2023",
            "sas_brut_fiyat": 100,
            "sas_isk_uyg1": 0,
            "sas_isk_yuzde1": 10,
            "sas_isk_uyg2": 1,
            "sas_isk_yuzde2": 0,
            "sas_isk_uyg3": 1,
            "sas_isk_yuzde3": 0,
            "sas_mas_uyg1": 0,
            "sas_mas_yuzde1": 0,
            "sas_mas_uyg2": 1,
            "sas_mas_yuzde2": 0,
            "sas_mas_uyg3": 1,
            "sas_mas_yuzde3": 0,
            "sas_odeme_plan": 0,
            "sas_kar_oran": 15,
            "sas_doviz_cinsi": 0,
            "sas_aciklama": "",
            "sas_depo_no": 0,
            "sas_miktar_tip": 0,
            "sas_miktar": 100,
            "sas_proje_kodu": "",
            "sas_srmmrk_kodu": "",
            "user_tablo": [
              {
                "aciklama": "test SAS user tablo"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### Satın Alma Şartı Sil V2 Delete

- Method: `POST`
- Path: `/api/APIMethods/SatinAlmaSartiSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
			"sas_evrak_no_seri": "S",
            "sas_evrak_no_sira":2
          }
        ]
      }
	]		
  }
}
```

### Sayım Sonuç Kaydet

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Sayım Sonuç Düzelt V2 Update | `POST` | `/Api/apiMethods/SayimSonuclariDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 2 | Sayım Sonuç Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/SayimSonuclariDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 3 | Sayım Sonuç Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/SayimSonuclariSatirSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| 4 | Sayım Sonuç Kaydet V2 Save | `POST` | `/Api/apiMethods/SayimSonuclariKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 5 | Sayım Sonuç Sil V2 Delete | `POST` | `/Api/apiMethods/SayimSonuclariSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |

#### Sayım Sonuç Düzelt V2 Update

- Method: `POST`
- Path: `/Api/apiMethods/SayimSonuclariDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
            "sym_Guid": "2C33C73E-53DB-49EC-93F1-95BEAD6A3FDA",
            "sym_miktar1": 56
          }
        ]
      }
    ]
   }
}
```

#### Sayım Sonuç Guid Ekle V2 Add Guid

- Method: `POST`
- Path: `/Api/apiMethods/SayimSonuclariDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
            "sym_tarihi": "21.12.2023",
            "sym_depono": 1,
            "sym_evrakno": 1,
            "sym_Stokkodu": "SKT01",
            "sym_reyonkodu": "",
            "sym_koridorkodu": "",
            "sym_rafkodu": "",
            "sym_miktar1": 38,
            "sym_miktar2": 0,
            "sym_miktar3": 0,
            "sym_miktar4": 0,
            "sym_miktar5": 0,
            "sym_birim_pntr": 1,
            "sym_barkod": "",
            "sym_renkno": 0,
            "sym_bedenno": 0,
            "sym_parti_kodu": "",
            "sym_lot_no": 0,
            "sym_serino": ""
          }
        ]
      }
    ]
   }
}
```

#### Sayım Sonuç Guid Sil V2 Delete Guid

- Method: `POST`
- Path: `/Api/apiMethods/SayimSonuclariSatirSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: GUID ile satir/kayit silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",   
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
            "sym_Guid":"7C33C73E-53DB-49EC-93F1-95BEAD6A3FDA"
          }	
        ]
      }
    ]
  }
}
```

#### Sayım Sonuç Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/SayimSonuclariKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "Test4jkjjk"
          }
        ],
        "satirlar": [
          {
            "sym_tarihi": "21.12.2023",
            "sym_depono": 1,
            "sym_Stokkodu": "SK02",
            "sym_reyonkodu": "1",
            "sym_koridorkodu": "",
            "sym_rafkodu": "",
            "sym_miktar1": 50,
            "sym_miktar2": 0,
            "sym_miktar3": 0,
            "sym_miktar4": 0,
            "sym_miktar5": 0,
            "sym_birim_pntr": 1,
            "sym_barkod": "",
            "sym_renkno": 0,
            "sym_bedenno": 0,
            "sym_parti_kodu": "",
            "sym_lot_no": 0,
            "sym_serino": "",            
            "user_tablo": [
              {
                "aciklama": "test sipariş user tablo"
              }
            ]
          },
          {
            "sym_tarihi": "21.12.2023",
            "sym_depono": 1,
            "sym_Stokkodu": "SK04",
            "sym_reyonkodu": "",
            "sym_koridorkodu": "",
            "sym_rafkodu": "",
            "sym_miktar1": 36,
            "sym_miktar2": 0,
            "sym_miktar3": 0,
            "sym_miktar4": 0,
            "sym_miktar5": 0,
            "sym_birim_pntr": 1,
            "sym_barkod": "",
            "sym_renkno": 1,
            "sym_bedenno": 1,
            "sym_parti_kodu": "",
            "sym_lot_no": 0,
            "sym_serino": "",            
            "user_tablo": [
              {
                "aciklama": "test sipariş user tablo"
              }
            ]
          }
        ]
      },
      {
        "satirlar": [
          {
            "sym_tarihi": "21.12.2023",
            "sym_depono": 1,
            "sym_Stokkodu": "SK05",
            "sym_reyonkodu": "",
            "sym_koridorkodu": "",
            "sym_rafkodu": "",
            "sym_miktar1": 10,
            "sym_miktar2": 0,
            "sym_miktar3": 0,
            "sym_miktar4": 0,
            "sym_miktar5": 0,
            "sym_birim_pntr": 1,
            "sym_barkod": "1234500000614",
            "sym_renkno": 0,
            "sym_bedenno": 0,
            "sym_parti_kodu": "",
            "sym_lot_no": 0,
            "sym_serino": "",
            "user_tablo": [
              {
                "aciklama": "test sipariş user tablo"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### Sayım Sonuç Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/SayimSonuclariSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
            "sym_tarihi": "07.11.2023",
            "sym_depono": 1,
            "sym_evrakno": 3
          }
        ]
      }
    ]
  }
}
```

### Sipariş

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Konsinye Sipariş Kaydet V2 Save | `POST` | `/api/APIMethods/SiparisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 2 | Normal Alınan Sipariş Kaydet V2 Save | `POST` | `/api/APIMethods/SiparisKaydetV2` | Raw body var, JSON parse edilemedi | Yeni kayit/evrak olusturma |
| 3 | Sipariş Düzelt V2 Update | `POST` | `/Api/apiMethods/SiparisDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 4 | Sipariş Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/SiparisDuzeltV2` | Raw body var, JSON parse edilemedi | Mevcut kayit/evrak guncelleme |
| 5 | Sipariş Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/SiparisGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| 6 | Sipariş Kaydet V2 Save | `POST` | `/api/APIMethods/SiparisKaydetV2` | Raw body var, JSON parse edilemedi | Yeni kayit/evrak olusturma |
| 7 | Sipariş Sil V2 Delete | `POST` | `/Api/apiMethods/SiparisSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |

#### Konsinye Sipariş Kaydet V2 Save

- Method: `POST`
- Path: `/api/APIMethods/SiparisKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "evrak_aciklamalari": [
                {
                "aciklama": "Test1cc"
                },
                {
                "aciklama": "Test2hh"
                },
                {
                "aciklama": "Testşlş"
                },
                {
                "aciklama": "Test4jkjjk"
                }
            ], 
        "satirlar": [
          {
            "sip_tarih": "19.12.2023",
            "sip_tip": "0",
            "sip_cins": "1",                        
            "sip_evrakno_seri": "K",
			"sip_musteri_kod": "CR01",
            "sip_stok_kod": "SK02",                        
			"sip_b_fiyat" : 15,
            "sip_miktar": 3,
            "sip_birim_pntr": 1,            
			"sip_tutar" : 1500,
            "sip_vergi_pntr": 4,           
            "sip_depono" : 1,
			"sip_vergisiz_fl" : false, 
			"seriler":"A1;B1;C1",
            "user_tablo": [
              {
                "aciklama": "test sipariş user tablo"
              }
            ]
          },
          {
            "sip_tarih": "19.12.2023",
            "sip_tip": "0",
            "sip_cins": "1",                        
            "sip_evrakno_seri": "K",
			"sip_musteri_kod": "CR01",
            "sip_stok_kod": "SK04",                        
			"sip_b_fiyat" : 20,
            "sip_miktar": 180,
            "sip_birim_pntr": 1,            
			"sip_tutar" : 2000,
            "sip_vergi_pntr": 4,           
            "sip_depono" : 1,
			"sip_vergisiz_fl" : false, 
            "renk_beden":[
                {                    
                    "renk_kirilim_kodu":"Yeşil",
                    "beden_kirilim_kodu":"L",
                    "miktar":50
                },
                {
                    "renk_no":1,
                    "beden_no":2,
                    "miktar":100                            
                },
                {
                    "renk_no":2,
                    "beden_no":1,
                    "miktar":30                            
                }
            ],          
			"user_tablo": [
              {
                "aciklama": "test stok hareket user tablo"
              }
            ]
          }
        ]
      },
      {
        "satirlar": [
          {
            "sip_tarih": "19.12.2023",
            "sip_tip": "0",
            "sip_cins": "1",                        
            "sip_evrakno_seri": "K",
			"sip_musteri_kod": "CR01",
            "sip_stok_kod": "SK05",                        
			"sip_b_fiyat" : 3,
            "sip_miktar": 1000.0,
            "sip_birim_pntr": 1,            
			"sip_tutar" : 3000,
            "sip_vergi_pntr": 4,           
            "sip_depono" : 1,
			"sip_vergisiz_fl" : false          
          }
        ]
      }
    ]
  }
}
```

#### Normal Alınan Sipariş Kaydet V2 Save

- Method: `POST`
- Path: `/api/APIMethods/SiparisKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: Raw body var, JSON parse edilemedi
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2024,
    "KullaniciKodu" : "1",
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "4444"
          }
        ],
        "satirlar": [
          {
            "sip_tarih": "19.01.2024",
            "sip_tip": "0",
            "sip_cins": "0",            
            "sip_evrakno_seri": "T",
            "sip_musteri_kod": "CR01",
            "sip_stok_kod": "SK02",
            "sip_b_fiyat": 15,
            "sip_miktar": 1,
            "sip_birim_pntr": 1,
            "sip_tutar": 1500,
            "sip_vergi_pntr": 4,
            "sip_depono": 1,
            "sip_vergisiz_fl": false,
            "sip_stok_sormerk":"",
            /*"sip_projekodu":"PRPR",*/
            "seriler": "A1;B1;C1",
            "user_tablo": [
              {
                "aciklama": "test sipariş user tablo"
              }
            ],
            "renk_beden":[
                {
                    "renk_no":1,
                    "beden_no":2,
                    "miktar":3                            
                },
                {
                    "renk_no":2,
                    "beden_no":1,
                    "miktar":3                            
                }
            ]
          }
        ]
      }
    ]
  }
}
```

#### Sipariş Düzelt V2 Update

- Method: `POST`
- Path: `/Api/apiMethods/SiparisDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "satirlar": [
          {
			"sip_Guid" : "578ce259-8384-44a2-a1bb-131f410a5e99",
            "sip_miktar": 5,
            "renk_beden":[
                    {
                        "renk_no":1,
                        "beden_no":1,
                        "miktar":1                            
                    },
                    {
                        "renk_no":1,
                        "beden_no":2,
                        "miktar":1
                    }
                ]
          }
        ]
      }
	]		
  }
}
```

#### Sipariş Guid Ekle V2 Add Guid

- Method: `POST`
- Path: `/Api/apiMethods/SiparisDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: Raw body var, JSON parse edilemedi
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "444444"
          }
        ],
        "satirlar": [
          {
            "sip_tarih": "19.01.2024",
            "sip_tip": "1",
            "sip_cins": "0",            
            "sip_evrakno_seri": "T",
            "sip_evrakno_sira": 1,
            "sip_musteri_kod": "CR01",
            "sip_stok_kod": "SK02",
            "sip_b_fiyat": 15,
            "sip_miktar": 1,
            "sip_birim_pntr": 1,
            "sip_tutar": 1500,
            "sip_vergi_pntr": 4,
            "sip_depono": 1,
            "sip_vergisiz_fl": false,
            "sip_stok_sormerk":"",
            /*"sip_projekodu":"PRPR",*/
            "seriler": "A1;B1;C1",
            "user_tablo": [
              {
                "aciklama": "test sipariş user tablo"
              }
            ],
            "renk_beden":[
                    {
                        "renk_no":1,
                        "beden_no":1,
                        "miktar":1                            
                    },
                    {
                        "renk_no":1,
                        "beden_no":2,
                        "miktar":3                            
                    }
                ]
          }
        ]
      }
    ]
  }
}
```

#### Sipariş Guid Sil V2 Delete Guid

- Method: `POST`
- Path: `/Api/apiMethods/SiparisGuidSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: GUID ile satir/kayit silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
            "sip_Guid" : "450a7451-a92f-42c8-9d7b-bb95207aaf14"
          }
        ]
      }
    ]
  }
}
```

#### Sipariş Kaydet V2 Save

- Method: `POST`
- Path: `/api/APIMethods/SiparisKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: Raw body var, JSON parse edilemedi
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "FirmaNo": 0,
    "SubeNo": 0,
    "evraklar": [
      {
        "evrak_aciklamalari": [
          {
            "aciklama": "Test1cc"
          },
          {
            "aciklama": "Test2hh"
          },
          {
            "aciklama": "Testşlş"
          },
          {
            "aciklama": "4444"
          }
        ],
        "satirlar": [
          {
            "sip_tarih": "28.12.2023",
            "sip_tip": "1",
            "sip_cins": "0",            
            "sip_evrakno_seri": "T",
            "sip_musteri_kod": "CR01",
            "sip_stok_kod": "SK02",
            "sip_b_fiyat": 15,
            "sip_miktar": 1,
            "sip_birim_pntr": 1,
            "sip_tutar": 1500,
            "sip_vergi_pntr": 4,
            "sip_depono": 1,
            "sip_vergisiz_fl": false,
            "sip_stok_sormerk":"",
            /*"sip_projekodu":"PRPR",*/
            "seriler": "A1;B1;C1",
            "user_tablo": [
              {
                "aciklama": "test sipariş user tablo"
              }
            ],
            "renk_beden":[
                {
                    "renk_no":1,
                    "beden_no":2,
                    "miktar":3                            
                },
                {
                    "renk_no":2,
                    "beden_no":1,
                    "miktar":3                            
                }
            ]
          }
        ]
      }
    ]
  }
}
```

#### Sipariş Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/SiparisSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
            "sip_tip" : 1,
            "sip_cins" : 0,
            "sip_evrakno_seri" : "Y",
            "sip_evrakno_sira" : 10
          }					
        ]
      }
	]		
  }
}
```

### Stok

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Stok Kaydet V2 Save | `POST` | `/API/APIMethods/StokKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre, stoklar; arrays: Mikro.stoklar.barkodlar[], Mikro.stoklar.satis_fiyatlari[], Mikro.stoklar[] | Yeni kayit/evrak olusturma |

#### Stok Kaydet V2 Save

- Method: `POST`
- Path: `/API/APIMethods/StokKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Sifre, stoklar; arrays: Mikro.stoklar.barkodlar[], Mikro.stoklar.satis_fiyatlari[], Mikro.stoklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu": "MIKROFLY",
    "CalismaYili": "2023",
    "KullaniciKodu": "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "stoklar": [
        {
        "sto_kod": "YS12347",
        "sto_isim": "mikro api stok",
        "sto_kisa_ismi": "",
        "sto_cins": 0,
        "sto_doviz_cinsi": 0,
        "sto_birim1_ad": "ADET",
        "sto_perakende_vergi": 18,
        "sto_toptan_vergi": 18,
        "barkodlar": [
          {
            "bar_kodu":"2022000000010",            
            "bar_barkodtipi":0,        
            "bar_birimpntr":1,
            "bar_master":false
          }
        ],
        "satis_fiyatlari":[
          {
            "sfiyat_listesirano":1,
            "sfiyat_deposirano":1,
            "sfiyat_odemeplan":0,
            "sfiyat_birim_pntr":1,
            "sfiyat_fiyati":32.5,
            "sfiyat_doviz":0           
          }
        ]
      }			
    ]
  }
}
```

### Tahsilat Tediye

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Tahsilat Tediye Çek Çıkış Bordrosu Kaydet Save | `POST` | `/Api/apiMethods/TahsilatTediyeSCKaydet` | top: Mikro; Mikro: evraklar; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| 2 | Tahsilat Tediye Çek Giriş Bordrosu Kaydet Save | `POST` | `/Api/apiMethods/TahsilatTediyeSCKaydet` | top: Mikro; Mikro: evraklar; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| 3 | Tahsilat Tediye Düzelt V2 Update | `POST` | `/Api/apiMethods/TahsilatTediyeDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| 4 | Tahsilat Tediye Giden Havale Kaydet Save | `POST` | `/Api/apiMethods/TahsilatTediyeSCKaydet` | top: Mikro; Mikro: evraklar; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| 5 | Tahsilat Tediye Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/TahsilatTediyeGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| 6 | Tahsilat Tediye Kaydet V2 Save | `POST` | `/Api/apiMethods/TahsilatTediyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| 7 | Tahsilat Tediye Kaydet V2 Save Copy | `POST` | `/Api/apiMethods/TahsilatTediyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| 8 | Tahsilat Tediye Kaydet V3 Çek Giriş Save | `POST` | `/Api/apiMethods/TahsilatTediyeKaydetV3` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| 9 | Tahsilat Tediye Kaydet V3 Save | `POST` | `/Api/apiMethods/TahsilatTediyeKaydetV3` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| 10 | Tahsilat Tediye Senet Çıkış Bordrosu Kaydet Save | `POST` | `/Api/apiMethods/TahsilatTediyeSCKaydet` | top: Mikro; Mikro: evraklar; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| 11 | Tahsilat Tediye Senet Giriş Bordrosu Kaydet Save | `POST` | `/Api/apiMethods/TahsilatTediyeSCKaydet` | top: Mikro; Mikro: evraklar; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |
| 12 | Tahsilat Tediye Sil V2 Delete | `POST` | `/Api/apiMethods/TahsilatTediyeSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |

#### Tahsilat Tediye Çek Çıkış Bordrosu Kaydet Save

- Method: `POST`
- Path: `/Api/apiMethods/TahsilatTediyeSCKaydet`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: evraklar; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1bb"
                    },
                    {
                        "aciklama": "Test2fg"
                    },
                    {
                        "aciklama": "Testgghh"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "cha_tarihi": "21.12.2023",
                        "cha_tip": 0,
                        "cha_cinsi": 1,
                        "cha_normal_Iade": 0,
                        "cha_evrak_tip": 67,
                        "cha_evrakno_seri": "CG",
                        "cha_cari_cins": 0,
                        "cha_kod": "CR01",
                        "cha_d_kurtar": null,
                        "cha_d_cins": 0,
                        "cha_d_kur": 1,
                        "cha_srmrkkodu": "",
                        "cha_projekodu": "",
                        "cha_vade": "20221005",
                        "cha_trefno": "MC-000-000-2023-00000002",
                        "user_tablo": [
                            {
                                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Tahsilat Tediye Çek Giriş Bordrosu Kaydet Save

- Method: `POST`
- Path: `/Api/apiMethods/TahsilatTediyeSCKaydet`
- Base URL: `http://10.0.0.207:8094`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: evraklar; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1bb"
                    },
                    {
                        "aciklama": "Test2fg"
                    },
                    {
                        "aciklama": "Testgghh"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "cha_tarihi": "02.08.2024",
                        "cha_tip": 1,
                        "cha_cinsi": 1,
                        "cha_normal_Iade": 0,
                        "cha_evrak_tip": 4,
                        "cha_evrakno_seri": "CG",
                        "cha_cari_cins": 0,
                        "cha_kod": "Tutku1001",
                        "cha_d_kurtar": null,
                        "cha_d_cins": 0,
                        "cha_d_kur": 1,
                        "cha_srmrkkodu": "",
                        "cha_projekodu": "",
                        "cha_kasa_hizmet": 4,
                        "cha_kasa_hizkod": "ÇK",
                        "cha_vade": "20221005",
                        "cha_meblag": "1000",
                        "cha_karsisrmrkkodu": "SRM M2",
                        "odeme_emirleri": {
                            "sck_banka_adres1": "T.C. MERKEZ BANKASI",
                            "sck_sube_adres2": "ESKİŞEHİR ŞUBESİ",
                            "sck_hesapno_sehir": "123456789",
                            "sck_no": "121211",
                            "sck_srmmrk": "SRM M1",
                            "sck_kesideyeri": "1213131",
                            "Sck_TCMB_Banka_kodu": "0001",
                            "Sck_TCMB_Sube_kodu": "00007",
                            "Sck_TCMB_il_kodu": "026"
                        },
                        "user_tablo": [
                            {
                                "TransactionReferenceId": "5f86e5b8a2d78b353c9fe8d7"
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Tahsilat Tediye Düzelt V2 Update

- Method: `POST`
- Path: `/Api/apiMethods/TahsilatTediyeDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "FirmaNo": 0,
        "SubeNo": 0,
        "evraklar": [
            {
                "satirlar": [
                    {
                        "cha_Guid": "A12CC73E-53DB-49EC-93F1-95BEAD6A3FDA",
                        "cha_evrak_tip": "3",
                        "cha_meblag": 2000
                    }
                ]
            }
        ]
    }
}
```

#### Tahsilat Tediye Giden Havale Kaydet Save

- Method: `POST`
- Path: `/Api/apiMethods/TahsilatTediyeSCKaydet`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: evraklar; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "evraklar": [
      {
        "evrak_aciklamalari": [
              {
                "aciklama": "Test1bb"
              },
              {
                "aciklama": "Test2fg"
              },
              {
                "aciklama": "Testgghh"
              },
              {
                "aciklama": "Test4jkjjk"
              }
            ],     
        "satirlar": [
          {
            "cha_tarihi": "21.12.2023",
            "cha_tip": 1,
            "cha_cinsi": 0,
            "cha_normal_Iade": 0,
            "cha_evrak_tip": 34,
            "cha_evrakno_seri": "GH",
            "cha_cari_cins": 3,
            "cha_kod": "HZM01",
            "cha_d_kurtar": null,
            "cha_d_cins": 0,
            "cha_d_kur": 1,
            "cha_srmrkkodu": "",
            "cha_projekodu": "",
            "cha_kasa_hizmet": 2,
            "cha_kasa_hizkod": "1",
            "cha_vade": "20220809",
            "cha_meblag": "1000",                     
            "user_tablo": [
              {
                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
              }
            ]
          },
          {
            "cha_tarihi": "21.12.2023",
            "cha_tip": 1,
            "cha_cinsi": 0,
            "cha_normal_Iade": 0,
            "cha_evrak_tip": 34,
            "cha_evrakno_seri": "GH",
            "cha_cari_cins": 3,
            "cha_kod": "HZM01",
            "cha_d_kurtar": null,
            "cha_d_cins": 0,
            "cha_d_kur": 1,
            "cha_srmrkkodu": "",
            "cha_projekodu": "",
            "cha_kasa_hizmet": 2,
            "cha_kasa_hizkod": "1",
            "cha_vade": "20220809",
            "cha_meblag": "500",            
            "user_tablo": [
              {
                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### Tahsilat Tediye Guid Sil V2 Delete Guid

- Method: `POST`
- Path: `/Api/apiMethods/TahsilatTediyeGuidSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: GUID ile satir/kayit silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
			"cha_Guid":"D4AD833E-BE48-4B9C-A485-537647ADF330",
			"cha_evrak_tip": "3"            
          }
        ]
      }
    ]
  }
}
```

#### Tahsilat Tediye Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/TahsilatTediyeKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "V16XX",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "FirmaNo": 0,
        "SubeNo": 0,
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1bb"
                    },
                    {
                        "aciklama": "Test2fg"
                    },
                    {
                        "aciklama": "Testgghh"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "cha_tarihi": "19.09.2023",
                        "cha_tip": 0,
                        "cha_cinsi": 19,
                        "cha_normal_Iade": 0,
                        "cha_evrak_tip": 34,
                        "cha_evrakno_seri": "KSTED",
                        "cha_cari_cins": 0,
                        "cha_kod": "GC",
                        "cha_d_kurtar": null,
                        "cha_d_cins": 0,
                        "cha_d_kur": 1,
                        "cha_srmrkkodu": "",
                        "cha_projekodu": "",
                        "cha_kasa_hizmet": 4,
                        "cha_kasa_hizkod": "NK",
                        "cha_vade": "20230919",
                        "cha_meblag": "1050",
                        "user_tablo": [
                            {
                                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
                            }
                        ],
                        "kredi_karti_taksit_bilgisi": {
                            "Firma_taksit_sayisi": 5,
                            "Musteri_taksit_sayisi": 5,
                            "Sorumluluk_merkezi": "",
                            "Toplam_tutar": "6000",
                            "Kredi_kart_no": 1,
                            "Uye_isyeri_no": "2",
                            "Kart_cekim_tarihi": "19.09.2023",
                            "Kart_sahip_tipi": 0
                        }
                    }
                ]
            }
        ]
    }
}
```

#### Tahsilat Tediye Kaydet V2 Save Copy

- Method: `POST`
- Path: `/Api/apiMethods/TahsilatTediyeKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2024,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "FirmaNo": 0,
        "SubeNo": 0,
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1bb"
                    },
                    {
                        "aciklama": "Test2fg"
                    },
                    {
                        "aciklama": "Testgghh"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "cha_tarihi": "30.01.2024",
                        "cha_tip": 0,
                        "cha_cinsi": 0,
                        "cha_normal_Iade": 0,
                        "cha_evrak_tip": 35,
                        "cha_evrakno_seri": "GH",
                        "cha_cari_cins": 0,
                        "cha_kod": "CR01",
                        "cha_d_kurtar": null,
                        "cha_d_cins": 0,
                        "cha_d_kur": 1,
                        "cha_srmrkkodu": "",
                        "cha_projekodu": "",
                        "cha_kasa_hizmet": 2,
                        "cha_kasa_hizkod": "1",
                        "cha_vade": "20240130",
                        "cha_meblag": "1050",
                        "user_tablo": [
                            {
                                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
                            }
                        ],
                        "kredi_karti_taksit_bilgisi": {
                            "Firma_taksit_sayisi": 5,
                            "Musteri_taksit_sayisi": 5,
                            "Sorumluluk_merkezi": "",
                            "Toplam_tutar": "6000",
                            "Kredi_kart_no": 1,
                            "Uye_isyeri_no": "2",
                            "Kart_cekim_tarihi": "19.09.2023",
                            "Kart_sahip_tipi": 0
                        }
                    }
                ]
            }
        ]
    }
}
```

#### Tahsilat Tediye Kaydet V3 Çek Giriş Save

- Method: `POST`
- Path: `/Api/apiMethods/TahsilatTediyeKaydetV3`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",  
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
         "evrak_aciklamalari": [
              {
                "aciklama": "Test1bb"
              },
              {
                "aciklama": "Test2fg"
              },
              {
                "aciklama": "Testgghh"
              },
              {
                "aciklama": "Test4jkjjk"
              }
            ],  
        "satirlar": [
          {
            "cha_tarihi": "21.12.2023",
            "cha_tip": 1,
            "cha_cinsi": 1,
            "cha_normal_Iade": 0,
            "cha_evrak_tip": 4,
            "cha_evrakno_seri": "CG",
            "cha_cari_cins": 0,
            "cha_kod": "CR01",
            "cha_d_kurtar": null,
            "cha_d_cins": 0,
            "cha_d_kur": 1,
            "cha_srmrkkodu": "",
            "cha_projekodu": "",
            "cha_kasa_hizmet": 4,
            "cha_kasa_hizkod": "ÇK",
            "cha_vade": "20230317",
            "cha_meblag": "1000",
            "cha_karsisrmrkkodu": "SRM M2",
            "odeme_emirleri": {
              "sck_banka_adres1": "T.C. MERKEZ BANKASI",
              "sck_sube_adres2": "ESKİŞEHİR ŞUBESİ",
              "sck_hesapno_sehir": "123456789",
              "sck_no": "121211",              
              "sck_kesideyeri": "1213131",
              "Sck_TCMB_Banka_kodu": "0001",
              "Sck_TCMB_Sube_kodu": "00007",
              "Sck_TCMB_il_kodu": "026"
            },            
            "user_tablo": [
              {
                "TransactionReferenceId": "5f86e5b8a2d78b353c9fe8d7"
              }
            ]
          }
        ]
      }
    ]
  }
}
```

#### Tahsilat Tediye Kaydet V3 Save

- Method: `POST`
- Path: `/Api/apiMethods/TahsilatTediyeKaydetV3`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "FirmaNo": 0,
        "SubeNo": 0,
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1bb"
                    },
                    {
                        "aciklama": "Test2fg"
                    },
                    {
                        "aciklama": "Testgghh"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "cha_tarihi": "21.12.2023",
                        "cha_tip": 0,
                        "cha_cinsi": 19,
                        "cha_normal_Iade": 0,
                        "cha_evrak_tip": 34,
                        "cha_evrakno_seri": "KSTED",
                        "cha_cari_cins": 0,
                        "cha_kod": "CR01",
                        "cha_d_kurtar": null,
                        "cha_d_cins": 0,
                        "cha_d_kur": 1,
                        "cha_srmrkkodu": "",
                        "cha_projekodu": "",
                        "cha_kasa_hizmet": 4,
                        "cha_kasa_hizkod": "NK1",
                        "cha_vade": "20210406",
                        "cha_meblag": "1050",
                        "user_tablo": [
                            {
                                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
                            }
                        ],
                        "kredi_karti_taksit_bilgisi": {
                            "Firma_taksit_sayisi": 5,
                            "Musteri_taksit_sayisi": 5,
                            "Sorumluluk_merkezi": "",
                            "Toplam_tutar": "6000",
                            "Kredi_kart_no": 1,
                            "Uye_isyeri_no": "2",
                            "Kart_cekim_tarihi": "21.12.2023",
                            "Kart_sahip_tipi": 0
                        }
                    }
                ]
            }
        ]
    }
}
```

#### Tahsilat Tediye Senet Çıkış Bordrosu Kaydet Save

- Method: `POST`
- Path: `/Api/apiMethods/TahsilatTediyeSCKaydet`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: evraklar; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1bb"
                    },
                    {
                        "aciklama": "Test2fg"
                    },
                    {
                        "aciklama": "Testgghh"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "cha_tarihi": "21.12.2023",
                        "cha_tip": 0,
                        "cha_cinsi": 2,
                        "cha_normal_Iade": 0,
                        "cha_evrak_tip": 66,
                        "cha_evrakno_seri": "SC",
                        "cha_cari_cins": 0,
                        "cha_kod": "CR01",
                        "cha_d_kurtar": null,
                        "cha_d_cins": 0,
                        "cha_d_kur": 1,
                        "cha_srmrkkodu": "",
                        "cha_projekodu": "",
                        "cha_kasa_hizmet": 4,
                        "cha_kasa_hizkod": "SÇ",
                        "cha_vade": "20221005",
                        "cha_trefno": "MS-000-000-2023-00000001",
                        "user_tablo": [
                            {
                                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Tahsilat Tediye Senet Giriş Bordrosu Kaydet Save

- Method: `POST`
- Path: `/Api/apiMethods/TahsilatTediyeSCKaydet`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: evraklar; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1bb"
                    },
                    {
                        "aciklama": "Test2fg"
                    },
                    {
                        "aciklama": "Testgghh"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "cha_tarihi": "21.12.2023",
                        "cha_tip": 1,
                        "cha_cinsi": 2,
                        "cha_normal_Iade": 0,
                        "cha_evrak_tip": 3,
                        "cha_evrakno_seri": "SG",
                        "cha_cari_cins": 0,
                        "cha_kod": "CR01",
                        "cha_d_kurtar": null,
                        "cha_d_cins": 0,
                        "cha_d_kur": 1,
                        "cha_srmrkkodu": "",
                        "cha_projekodu": "",
                        "cha_kasa_hizmet": 4,
                        "cha_kasa_hizkod": "SK",
                        "cha_vade": "20231221",
                        "cha_meblag": "4000",
                        "user_tablo": [
                            {
                                "TransactionReferenceId ": "5f86e5b8a2d78b353c9fe8d7"
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Tahsilat Tediye Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/TahsilatTediyeSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
            "cha_evrakno_seri": "CG",
            "cha_evrakno_sira": 1,
            "cha_evrak_tip": "3"
          }
        ]
      }
    ]
  }
}
```

### Üretim İş Emri

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Üretim İş Emri Oluştur V2 Save | `POST` | `/API/APIMethods/UretimIsEmriOlusturV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Satirlar, Sifre; arrays: Mikro.Satirlar[] | Yeni kayit/evrak olusturma |

#### Üretim İş Emri Oluştur V2 Save

- Method: `POST`
- Path: `/API/APIMethods/UretimIsEmriOlusturV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Satirlar, Sifre; arrays: Mikro.Satirlar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "Satirlar": [
            {
                "UrunKodu": "A",
                "UretilecekMiktar": 10.50
            },
            {
                "UrunKodu": "A-MAM",
                "UretilecekMiktar": 8
            }
        ]
    }
}
```

### Üretim Talep

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Üretim Talep Guid Sil V2 Delete Guid | `POST` | `/Api/APIMethods/UretimTalepGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| 2 | Üretim Talep Kaydet V2 Save | `POST` | `/Api/APIMethods/UretimTalepKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 3 | Üretim Talep Sil V2 Delete | `POST` | `/Api/APIMethods/UretimTalepSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |

#### Üretim Talep Guid Sil V2 Delete Guid

- Method: `POST`
- Path: `/Api/APIMethods/UretimTalepGuidSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: GUID ile satir/kayit silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
			"utl_Guid": "40a3d109-162a-4267-af76-d5bffe45f374"
          }
        ]
      }
	]		
  }
}
```

#### Üretim Talep Kaydet V2 Save

- Method: `POST`
- Path: `/Api/APIMethods/UretimTalepKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "Test1cc"
                    },
                    {
                        "aciklama": "Test2hh"
                    },
                    {
                        "aciklama": "Testşlş"
                    },
                    {
                        "aciklama": "Test4jkjjk"
                    }
                ],
                "satirlar": [
                    {
                        "utl_tarihi": "22.09.2022",
                        "utl_teslim_tarihi": "22.09.2022",
                        "utl_evrak_seri": "UT",
                        "utl_belge_no": "UTLP-444",
                        "utl_belge_tarihi": "22.09.2022",
                        "utl_Sor_Merk": "",
                        "utl_Stok_kodu": "BEDEN-1",
                        "utl_miktari": 20,
                        "utl_depo_no": 1,
                        "utl_projekodu": "",
                        "renk_beden": [
                            {
                                "renk_kirilim_kodu": "SİYAH",
                                "beden_kirilim_kodu": "S",
                                "miktar": 2
                            },
                            {
                                "renk_no": 3,
                                "beden_no": 1,
                                "miktar": 4
                            },
                            {
                                "renk_no": 3,
                                "beden_no": 2,
                                "miktar": 5
                            }
                        ],
                        "user_tablo": [
                            {
                                "aciklama": "test stok hareket user tablo"
                            }
                        ]
                    },
                    {
                        "utl_tarihi": "22.09.2022",
                        "utl_teslim_tarihi": "22.09.2022",
                        "utl_evrak_seri": "UT",
                        "utl_belge_no": "UTLP-444",
                        "utl_belge_tarihi": "22.09.2022",
                        "utl_Sor_Merk": "",
                        "utl_Stok_kodu": "A-MAM",
                        "utl_miktari": 5,
                        "utl_depo_no": 1,
                        "utl_projekodu": "",
                        "user_tablo": [
                            {
                                "aciklama": "test stok hareket user tablo"
                            }
                        ]
                    }
                ]
            },
            {
                "satirlar": [
                    {
                        "utl_tarihi": "22.09.2022",
                        "utl_teslim_tarihi": "22.09.2022",
                        "utl_evrak_seri": "UT",
                        "utl_belge_no": "UTLP-444",
                        "utl_belge_tarihi": "22.09.2022",
                        "utl_Sor_Merk": "",
                        "utl_Stok_kodu": "A-MAM",
                        "utl_miktari": 5,
                        "utl_depo_no": 1,
                        "utl_projekodu": "",
                        "user_tablo": [
                            {
                                "aciklama": "test stok hareket user tablo"
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Üretim Talep Sil V2 Delete

- Method: `POST`
- Path: `/Api/APIMethods/UretimTalepSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2024,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      {
        "satirlar": [
          {
			"utl_evrak_seri": "UT",
            "utl_evrak_sira":1
          }
        ]
      }
	]		
  }
}
```

### Ürün Reçete

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Ürün Reçete Kaydet V2 Save | `POST` | `/Api/apiMethods/UrunReceteKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.recete_kriterler[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 2 | Ürün Reçete Sil V2 Delete | `POST` | `/Api/apiMethods/UrunReceteSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |

#### Ürün Reçete Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/UrunReceteKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.recete_kriterler[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "açıklama 1"
                    },
                    {
                        "aciklama": "açıklama 2"
                    },
                    {
                        "aciklama": "açıklama 3"
                    },
                    {
                        "aciklama": "açıklama 4"
                    }
                ],
                "satirlar": [
                    {
                        "rec_anatipi": 0,
                        "rec_anakod": "SK02",
                        "rec_cinsi": 0,
                        "rec_anabirim": 1,
                        "rec_anamiktar": 1,
                        "rec_tuketim_tur": 0,
                        "rec_tuketim_kod": "SK04",
                        "rec_tuketim_recete_cinsi": 1,
                        "rec_tuketim_miktar": 1,
                        "rec_tuketim_birim": 1,
                        "rec_alt_tukkod1": "SK05",
                        "rec_eklenme_sarti": 2,
                        "user_tablo": [
                            {
                                "aciklama": "test veri user tablo"
                            }
                        ],
                        "recete_kriterler": [
                            {
                                "rk_tablo": 0,
                                "rk_alan_adi": "sip_miktar",
                                "rk_islem": 0,
                                "rk_stringdata": "2"
                            },
                            {
                                "rk_tablo": 1,
                                "rk_alan_adi": "sto_kod",
                                "rk_islem": 1,
                                "rk_stringdata": "SK04"
                            }
                        ]
                    },
                    {
                        "rec_anatipi": 0,
                        "rec_anakod": "010",
                        "rec_cinsi": 0,
                        "rec_anabirim": 1,
                        "rec_anamiktar": 1,
                        "rec_tuketim_tur": 1,
                        "rec_tuketim_kod": "HİZ.01",
                        "rec_tuketim_recete_cinsi": 1,
                        "rec_tuketim_miktar": 1,
                        "rec_tuketim_birim": 1,
                        "rec_eklenme_sarti": 3,
                        "user_tablo": [],
                        "recete_kriterler": [
                            {
                                "rk_tablo": 0,
                                "rk_alan_adi": "Siparis_test"
                            }
                        ]
                    }
                ]
            },
            {
                "satirlar": [
                    {
                        "rec_anatipi": 1,
                        "rec_anakod": "HİZ.02",
                        "rec_cinsi": 0,
                        "rec_anabirim": 1,
                        "rec_anamiktar": 1,
                        "rec_tuketim_tur": 0,
                        "rec_tuketim_kod": "001",
                        "rec_tuketim_recete_cinsi": 1,
                        "rec_tuketim_miktar": 1,
                        "rec_tuketim_birim": 1
                    }
                ]
            },
            {
                "satirlar": [
                    {
                        "rec_anatipi": 0,
                        "rec_anakod": "11745",
                        "rec_cinsi": 1,
                        "rec_anabirim": 1,
                        "rec_anamiktar": 1,
                        "rec_ana_renk_no": 2,
                        "rec_tuketim_tur": 0,
                        "rec_tuketim_kod": "K-0003",
                        "rec_tuketim_recete_cinsi": 1,
                        "rec_tuketim_miktar": 1,
                        "rec_tuketim_birim": 1
                    },
                    {
                        "rec_anatipi": 0,
                        "rec_anakod": "11745",
                        "rec_cinsi": 1,
                        "rec_anabirim": 1,
                        "rec_anamiktar": 1,
                        "rec_ana_renk_no": 0,
                        "rec_ana_beden_no": 1,
                        "rec_tuketim_tur": 0,
                        "rec_tuketim_kod": "12.0002",
                        "rec_tuketim_recete_cinsi": 1,
                        "rec_tuketim_miktar": 1,
                        "rec_tuketim_birim": 1,
                        "rec_tuketim_renk_no": 1
                    }
                ]
            }
        ]
    }
}
```

#### Ürün Reçete Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/UrunReceteSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "revizyon_aciklamasi": "api reçete sil 1",
                "satirlar": [
                    {
                        "rec_anatipi": 0,
                        "rec_anakod": "010",
                        "rec_cinsi": 0
                    }
                ]
            },
            {
                "revizyon_aciklamasi": "api reçete sil 2",
                "satirlar": [
                    {
                        "rec_anatipi": 1,
                        "rec_anakod": "HİZ.02",
                        "rec_cinsi": 0
                    }
                ]
            },
            {
                "revizyon_aciklamasi": "api reçete sil 3",
                "satirlar": [
                    {
                        "rec_anatipi": 0,
                        "rec_anakod": "11745",
                        "rec_cinsi": 1
                    }
                ]
            }
        ]
    }
}
```

### Ürün Rota

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Ürün Rota Kaydet V2 Save | `POST` | `/Api/apiMethods/UrunRotaKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.rota_detaylar[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 2 | Ürün Rota Sil V2 Delete | `POST` | `/Api/apiMethods/UrunRotaSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |

#### Ürün Rota Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/UrunRotaKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.rota_detaylar[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "açıklama 1"
                    },
                    {
                        "aciklama": "açıklama 2"
                    },
                    {
                        "aciklama": "açıklama 3"
                    },
                    {
                        "aciklama": "açıklama 4"
                    }
                ],
                "satirlar": [
                    {
                        "URt_RotaUrunKodu": "SK02",
                        "URt_cinsi": 1,
                        "URt_ID": 1,
                        "URt_BagliRotaID": 2,
                        "URt_OpKod": "OPERASYON.01",
                        "user_tablo": [
                            {
                                "aciklama": "test veri user tablo"
                            }
                        ],
                        "rota_detaylar": [
                            {
                                "urd_IsmerkeziveyaGrupKod": "ISMRK.01",
                                "urd_KriterDegeri1": 1,
                                "urd_MaxDeger1": 3,
                                "urd_MinDeger1": 1
                            },
                            {
                                "urd_IsmerkeziveyaGrupKod": "ISMRK.01",
                                "urd_KriterDegeri1": 2,
                                "urd_MaxDeger1": 7,
                                "urd_MinDeger1": 5
                            }
                        ]
                    },
                    {
                        "URt_RotaUrunKodu": "SK02",
                        "URt_cinsi": 1,
                        "URt_ID": 2,
                        "URt_BagliRotaID": 1,
                        "URt_OpKod": "OPERASYON.01",
                        "user_tablo": [],
                        "rota_detaylar": [
                            {
                                "urd_ismerkezi_tipi": 1,
                                "urd_IsmerkeziveyaGrupKod": "01",
                                "urd_KriterDegeri1": 1,
                                "urd_MaxDeger1": 3,
                                "urd_MinDeger1": 1
                            }
                        ]
                    }
                ]
            },
            {
                "satirlar": [
                    {
                        "URt_RotaUrunKodu": "SK02",
                        "URt_cinsi": 0,
                        "URt_ID": 1,
                        "URt_OpKod": "OPERASYON.01",
                        "URt_IsmerkeziveyaGrupKod": "ISMRK.01"
                    }
                ]
            }
        ]
    }
}
```

#### Ürün Rota Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/UrunRotaSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "revizyon_aciklamasi": "api rota sil 1",
                "satirlar": [
                    {
                        "URt_RotaUrunKodu": "007",
                        "URt_cinsi": 0
                    }
                ]
            },
            {
                "revizyon_aciklamasi": "api rota sil 2",
                "satirlar": [
                    {
                        "URt_RotaUrunKodu": "007",
                        "URt_cinsi": 1
                    }
                ]
            }
        ]
    }
}
```

### Ürün Rota Plan

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Ürün Rota Plan Kaydet V2 Save | `POST` | `/Api/apiMethods/UretimRotaPlanKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 2 | Ürün Rota Plan Sil V2 Delete | `POST` | `/Api/apiMethods/UretimRotaPlanSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |

#### Ürün Rota Plan Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/UretimRotaPlanKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "açıklama 1"
                    },
                    {
                        "aciklama": "açıklama 2"
                    },
                    {
                        "aciklama": "açıklama 3"
                    },
                    {
                        "aciklama": "açıklama 4"
                    }
                ],
                "satirlar": [
                    {
                        "RtP_IsEmriKodu": "0000004 0001",
                        "RtP_UrunKodu": "URUN_BEDEN",
                        "RtP_OperasyonSafhaNo": 0,
                        "RtP_OperasyonKodu": "OP1",
                        "RtP_PlanlananSure": 60,
                        "RtP_TamamlananSure": 0,
                        "RtP_PlanlananMiktar": 4,
                        "RtP_TamamlananMiktar": 0,
                        "Rtp_PlanlananBaslamaTarihi": "27.01.2024",
                        "RtP_PlanlananIsMerkezi": "İMER1",
                        "RtP_PlanlananKalipKodu": "KLP1",
                        "RtP_Aciklama": "Açıklaması",
                        "renk_beden": [
                            {
                                "renk_kirilim_kodu": "SİYAH",
                                "beden_kirilim_kodu": "S",
                                "miktar": 2
                            },
                            {
                                "renk_no": 2,
                                "beden_no": 2,
                                "miktar": 1
                            },
                            {
                                "renk_no": 2,
                                "beden_no": 3,
                                "miktar": 1
                            }
                        ],
                        "user_tablo": [
                            {
                                "user_deger": "48"
                            }
                        ]
                    }
                ]
            },
            {
                "satirlar": [
                    {
                        "RtP_IsEmriKodu": "000001 001",
                        "RtP_UrunKodu": "MAM",
                        "RtP_OperasyonSafhaNo": 0,
                        "RtP_OperasyonKodu": "OP1",
                        "RtP_PlanlananSure": 120,
                        "RtP_TamamlananSure": 0,
                        "RtP_PlanlananMiktar": 2,
                        "RtP_TamamlananMiktar": 0,
                        "Rtp_PlanlananBaslamaTarihi": "27.10.2022",
                        "RtP_PlanlananIsMerkezi": "İMER1",
                        "RtP_PlanlananKalipKodu": "KLP1",
                        "RtP_Aciklama": "Açıklaması"
                    }
                ]
            }
        ]
    }
}
```

#### Ürün Rota Plan Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/UretimRotaPlanSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": "2023",
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "satirlar": [
                    {
                        "RtP_IsEmriKodu": "0000004 0001"
                    }
                ]
            }
        ]
    }
}
```

### Verilen Teklif

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Verilen Teklif Düzelt V2 Update | `POST` | `/Api/apiMethods/VerilenTeklifDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 2 | Verilen Teklif Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/VerilenTeklifDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Mevcut kayit/evrak guncelleme |
| 3 | Verilen Teklif Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/VerilenTeklifGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | GUID ile satir/kayit silme |
| 4 | Verilen Teklif Kaydet V2 Save | `POST` | `/Api/apiMethods/VerilenTeklifKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[] | Yeni kayit/evrak olusturma |
| 5 | Verilen Teklif Sil V2 Delete | `POST` | `/Api/apiMethods/VerilenTeklifSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[] | Kayit/evrak silme |

#### Verilen Teklif Düzelt V2 Update

- Method: `POST`
- Path: `/Api/apiMethods/VerilenTeklifDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      { 
        "satirlar": [
          {
            "tkl_Guid": "4873d69d-657b-4903-ae2c-f031503273ce",
            "tkl_miktar": 10 
         }
        ]
      }
    ]
  }
}
```

#### Verilen Teklif Guid Ekle V2 Add Guid

- Method: `POST`
- Path: `/Api/apiMethods/VerilenTeklifDuzeltV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Mevcut kayit/evrak guncelleme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2024,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "VERİLEN TEKLİF AÇIKLAMA-1"
                    },
                    {
                        "aciklama": "VERİLEN TEKLİF AÇIKLAMA-2"
                    },
                    {
                        "aciklama": "VERİLEN TEKLİF AÇIKLAMA-3"
                    }
                ],
                "satirlar": [
                    {
                        "tkl_evrak_tarihi": "16.01.2024",
                        "tkl_evrakno_seri": "S",
                        "tkl_evrakno_sira": "1",
                        "tkl_belge_no": "BBB22",
                        "tkl_cari_kod": "CR01",
                        "tkl_harekettipi": 0,
                        "tkl_stok_kod": "SK04",
                        "tkl_Aciklama": "Satır açıklaması",
                        "tkl_Alisfiyati": 15,
                        "tkl_baslangic_tarihi": "16.01.2024",
                        "tkl_miktar": 3,
                        "tkl_birim_pntr": 1,
                        "tkl_vergi_pntr": 4,
                        "tkl_cari_tipi": "0",
                        "tkl_karorani": 10,
                        "tkl_ProjeKodu": "",
                        "tkl_cari_sormerk": "SRM1",
                        "tkl_stok_sormerk": "SRM1",
                        "user_tablo": [
                            {
                                "aciklama": "test veri user tablo"
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Verilen Teklif Guid Sil V2 Delete Guid

- Method: `POST`
- Path: `/Api/apiMethods/VerilenTeklifGuidSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: GUID ile satir/kayit silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
  "Mikro": {
    "FirmaKodu" : "MIKROFLY",
    "CalismaYili" : 2023,
    "KullaniciKodu" : "SRV",
    "Sifre": "{{MikroSifreHash}}",
    "evraklar": [
      { 
        "satirlar": [
          {
            "tkl_Guid": "3f2940d9-ee6c-4792-9222-8cc6a3bdc972"
         }
        ]
      }
    ]
  }
}
```

#### Verilen Teklif Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/VerilenTeklifKaydetV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Yeni kayit/evrak olusturma
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.evrak_aciklamalari[], Mikro.evraklar.satirlar.renk_beden[], Mikro.evraklar.satirlar.user_tablo[], Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2024,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "evrak_aciklamalari": [
                    {
                        "aciklama": "VERİLEN TEKLİF AÇIKLAMA-1"
                    },
                    {
                        "aciklama": "VERİLEN TEKLİF AÇIKLAMA-2"
                    },
                    {
                        "aciklama": "VERİLEN TEKLİF AÇIKLAMA-3"
                    }
                ],
                "satirlar": [
                    {
                        "tkl_evrak_tarihi": "16.01.2024",
                        "tkl_evrakno_seri": "S",
                        "tkl_belge_no": "BBB22",
                        "tkl_cari_kod": "CR01",
                        "tkl_harekettipi": 0,
                        "tkl_stok_kod": "SK02",
                        "tkl_Aciklama": "Satır açıklaması",
                        "tkl_Alisfiyati": 15,
                        "tkl_baslangic_tarihi": "16.01.20243",
                        "tkl_miktar": 3,
                        "tkl_birim_pntr": 1,
                        "tkl_vergi_pntr": 4,
                        "tkl_cari_tipi": "0",
                        "tkl_karorani": 10,
                        "tkl_ProjeKodu": "",
                        "tkl_cari_sormerk": "SRM1",
                        "tkl_stok_sormerk": "SRM1",
                        "user_tablo": [
                            {
                                "aciklama": "test veri user tablo"
                            }
                        ]
                    },
                    {
                        "tkl_evrak_tarihi": "16.01.2024",
                        "tkl_evrakno_seri": "S",
                        "tkl_belge_no": "BBB22",
                        "tkl_cari_kod": "CR01",
                        "tkl_harekettipi": 0,
                        "tkl_stok_kod": "SK05",
                        "tkl_Aciklama": "Satır açıklaması",
                        "tkl_Alisfiyati": 22,
                        "tkl_baslangic_tarihi": "16.01.2024",
                        "tkl_miktar": 4,
                        "tkl_birim_pntr": 1,
                        "tkl_vergi_pntr": 4,
                        "tkl_cari_tipi": "0",
                        "tkl_karorani": 10,
                        "tkl_ProjeKodu": "",
                        "tkl_cari_sormerk": "SRM1",
                        "tkl_stok_sormerk": "SRM1",
                        "renk_beden": [
                            {
                                "renk_kirilim_kodu": "RNK",
                                "beden_kirilim_kodu": "GB",
                                "miktar": 2
                            },
                            {
                                "renk_no": 1,
                                "beden_no": 2,
                                "miktar": 1
                            },
                            {
                                "renk_no": 2,
                                "beden_no": 1,
                                "miktar": 1
                            }
                        ]
                    }
                ]
            }
        ]
    }
}
```

#### Verilen Teklif Sil V2 Delete

- Method: `POST`
- Path: `/Api/apiMethods/VerilenTeklifSilV2`
- Base URL: `http://10.0.0.207:8084`
- Kullanim: Kayit/evrak silme
- Body ozeti: top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu, Sifre; arrays: Mikro.evraklar.satirlar[], Mikro.evraklar[]
- Collection response ornegi sayisi: `0`

Request body ornegi:

```json
{
    "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2024,
        "KullaniciKodu": "SRV",
        "Sifre": "{{MikroSifreHash}}",
        "evraklar": [
            {
                "satirlar": [
                    {
                        "tkl_evrakno_seri": "S",
                        "tkl_evrakno_sira": 3
                    }
                ]
            }
        ]
    }
}
```

