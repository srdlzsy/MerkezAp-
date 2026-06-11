# MikroAPI-33d Postman Endpoint Dokumani

Bu dokuman `pasted-text.txt` icindeki Postman collection JSON dosyasindan cikarilmistir. Response ornekleri collection icinde bulunmadigi icin response semasi runtime testleri veya resmi Mikro API dokumani ile dogrulanmalidir.

## Kisa Ozet

- Collection adi: `MikroAPI-33d`
- Toplam endpoint/request: `150`
- Base URL: `http://10.0.0.207:8084`
- Not: Collection icinde `localhost:8084` ve `localhost:8094` geciyordu. Canli denemede calisan sunucu `http://10.0.0.207:8084`; `8094` portu timeout verdi.
- Request body formati genel olarak `raw JSON`.
- Cogu business endpoint `POST` ile calisiyor; silme/guncelleme islemlerinde de HTTP method olarak genelde `POST` kullanilmis.
- Path casing collection icinde karisik: `/Api/APIMethods`, `/Api/apiMethods`, `/API/APIMethods`, `/api/APIMethods`. Entegrasyonda once collection pathini aynen kullanmak guvenlidir.
- Bu dokumanda `Sifre`, `ApiKey`, `Token` benzeri alanlar repo guvenligi icin degisken/placeholder olarak tutuldu. Postman environment icinde gerçek degerler saklanmalidir.

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

Dogru calistigi test edilen endpointler:

| Islem | Method | URL | Beklenen sonuc |
|---|---|---|---|
| HealthCheck2 | `GET` | `{{MikroBaseUrl}}/Api/APIMethods/HealthCheck2` | `ApiStatus = Up`, `StatusCode = 200` |
| APILogin | `POST` | `{{MikroBaseUrl}}/Api/APIMethods/APILogin` | `StatusCode = 200`, `IsError = false` |
| Stok Listesi V2 | `POST` | `{{MikroBaseUrl}}/Api/APIMethods/StokListesiV2` | `StatusCode = 200`, `Data.StokListesi` dolu |

## Kimlik Dogrulama

Mikro ERP REST API'de `Sifre` alani sabit sifre degil, gunluk tarih ile uretilen MD5 hash degeridir:

```text
Sifre = MD5("YYYY-MM-DD <MikroSifreAnahtari>")
```

Ornek:

```text
MD5("2026-04-15 159753") = "9fce5c5935a265b4e6f54754c25158fd"
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
6. Islem sonunda hata/sonuc modeli collection icinde yok; bu nedenle entegrasyon kodu HTTP status + response body icindeki basari/hata alanlarini loglamalidir.

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
| Dahili Stok Hareket | 6 | Dahili stok hareket evraklari. |
| Dekont | 8 | Finans, tahsilat/tediye, dekont ve muhasebe fisleri. |
| Depolar Arası Sipariş | 5 | Depolar arasi siparis kaydetme/guncelleme/silme. |
| Etiket Basım Kaydet | 1 | Stok, sayim, uretim ve operasyon hareketleri. |
| Evrak Açıklamaları | 3 | Collection icindeki ilgili Mikro API islem grubu. |
| Evrak Belge Resim | 2 | Collection icindeki ilgili Mikro API islem grubu. |
| Fiyat Değişikliği | 1 | Collection icindeki ilgili Mikro API islem grubu. |
| Image Data | 3 | Collection icindeki ilgili Mikro API islem grubu. |
| İrsaliye | 17 | Farkli tiplerde irsaliye kaydetme, guncelleme ve silme. |
| Kasa Masraf Fişi | 1 | Finans, tahsilat/tediye, dekont ve muhasebe fisleri. |
| Kayıt Kaydet | 3 | Collection icindeki ilgili Mikro API islem grubu. |
| Listeler | 9 | Stok, cari, kullanici, vergi ve parametre listeleri. |
| Login-Logoff | 7 | Oturum, saglik kontrolu ve logoff. |
| Muhasebe | 4 | Finans, tahsilat/tediye, dekont ve muhasebe fisleri. |
| Operasyon Tamamlama Fişi | 2 | Stok, sayim, uretim ve operasyon hareketleri. |
| Personel | 2 | Collection icindeki ilgili Mikro API islem grubu. |
| Proforma Sipariş | 2 | Proforma siparis kaydetme ve silme. |
| Satın Alma Talep | 2 | Siparis, teklif ve sart belgeleri. |
| Satış Şartı | 5 | Satis sarti kaydetme, guncelleme ve silme. |
| Satin Alma Şartı | 2 | Satin alma sarti kaydetme ve silme. |
| Sayım Sonuç Kaydet | 5 | Sayim sonuc kaydetme, guncelleme ve silme. |
| Sipariş | 7 | Siparis kaydetme, guncelleme ve silme. |
| Stok | 1 | Master data kart kaydetme/guncelleme/silme. |
| Tahsilat Tediye | 12 | Finans, tahsilat/tediye, dekont ve muhasebe fisleri. |
| Üretim İş Emri | 1 | Collection icindeki ilgili Mikro API islem grubu. |
| Üretim Talep | 3 | Siparis, teklif ve sart belgeleri. |
| Ürün Reçete | 2 | Urun recete kaydetme ve guncelleme. |
| Ürün Rota | 2 | Stok, sayim, uretim ve operasyon hareketleri. |
| Ürün Rota Plan | 2 | Stok, sayim, uretim ve operasyon hareketleri. |
| Verilen Teklif | 5 | Siparis, teklif ve sart belgeleri. |

## Kullanim Ornekleri

### APILogin

- Method: `POST`
- Path: `/Api/APIMethods/APILogin`
- Amac: Oturum acma veya API kullanicisi dogrulama.

Request body ornegi:

```json
{
    "FirmaKodu":  "{{MikroFirmaKodu}}",
    "CalismaYili":  {{MikroCalismaYili}},
    "ApiKey":  "{{MikroApiKey}}",
    "KullaniciKodu":  "{{MikroKullaniciKodu}}",
    "Sifre":  "{{MikroSifreHash}}",
    "FirmaNo":  0,
    "SubeNo":  0
}
```

### Logoff V2

- Method: `POST`
- Path: `/Api/apiMethods/APILogoffV2`
- Amac: Oturumu kapatma.

Request body ornegi:

```json
{
    "Mikro":  {
                  "FirmaKodu":  "V16XX",
                  "CalismaYili":  "2023",
                  "KullaniciKodu":  "SRV",
                  "Sifre":  "<REDACTED>"
              },
    "KullaniciKodu":  "1"
}
```

### Stok Listesi V2

- Method: `POST`
- Path: `/Api/APIMethods/StokListesiV2`
- Amac: Listeleme veya sorgulama.

Request body ornegi:

```json
{
    "Mikro":  {
                  "FirmaKodu":  "{{MikroFirmaKodu}}",
                  "CalismaYili":  {{MikroCalismaYili}},
                  "KullaniciKodu":  "{{MikroKullaniciKodu}}",
                  "Sifre":  "{{MikroSifreHash}}",
                  "FirmaNo":  0,
                  "SubeNo":  0,
                  "ApiKey":  "{{MikroApiKey}}"
              },
    "StokKod":  "",
    "TarihTipi":  2,
    "IlkTarih":  "2026-06-01",
    "SonTarih":  "2026-06-11",
    "Sort":  "-sto_kod",
    "Size":  "5",
    "Index":  0
}
```

### Cari Listesi V3

- Method: `POST`
- Path: `/Api/APIMethods/CariListesiV3`
- Amac: Listeleme veya sorgulama.

Request body ornegi:

```json
{
    "Mikro":  {
                  "FirmaKodu":  "MIKROFLY",
                  "CalismaYili":  "2023",
                  "KullaniciKodu":  "SRV",
                  "Sifre":  "<REDACTED>"
              },
    "CariKod":  "",
    "CariVKNTCNo":  "",
    "TarihTipi":  2,
    "IlkTarih":  "1899-12-30",
    "SonTarih":  "2023-12-21",
    "Sort":  "-cari_kod",
    "Size":  "5",
    "Index":  0
}
```

### Cari Kaydet V2 Save

- Method: `POST`
- Path: `/API/APIMethods/CariKaydetV2`
- Amac: Yeni kayit/evrak olusturma.

Request body ornegi:

```json
{
    "Mikro":  {
                  "FirmaKodu":  "MIKROFLY",
                  "CalismaYili":  "2023",
                  "KullaniciKodu":  "SRV",
                  "Sifre":  "<REDACTED>",
                  "cariler":  {
                                  "cari_kod":  "YCK141",
                                  "cari_unvan1":  "yeni cari unvan",
                                  "cari_unvan2":  "yeni cari unvan2",
                                  "cari_vdaire_no":  "11111111111",
                                  "cari_vdaire_adi":  "TAŞOVA VERGİ DAİRESİ",
                                  "cari_doviz_cinsi1":  0,
                                  "cari_doviz_cinsi2":  255,
                                  "cari_doviz_cinsi3":  255,
                                  "cari_vade_fark_yuz":  25,
                                  "cari_KurHesapSekli":  1,
                                  "cari_sevk_adres_no":  0,
                                  "cari_fatura_adres_no":  0,
                                  "cari_EMail":  "",
                                  "cari_CepTel":  "",
                                  "cari_efatura_fl":  0,
                                  "cari_def_efatura_cinsi":  0,
                                  "cari_efatura_baslangic_tarihi":  "",
                                  "cari_vergidairekodu":  "",
                                  "cari_muh_kod2":  "",
                                  "adres":  [
                                                {
                                                    "adr_cadde":  "cadde",
                                                    "adr_mahalle":  "mahalle",
                                                    "adr_sokak":  "sokak",
                                                    "adr_Semt":  "semt",
                                                    "adr_Apt_No":  "A1",
                                                    "adr_Daire_No":  "2",
                                                    "adr_posta_kodu":  34340,
                                                    "adr_ilce":  "Sarıyer",
                                                    "adr_il":  "İstanbul",
                                                    "adr_ulke":  "TÜRKİYE",
                                                    "adr_tel_ulke_kodu":  "090",
                                                    "adr_tel_bolge_kodu":  "212",
                                                    "adr_tel_no1":  "4444444",
                                                    "adr_tel_no2":  "",
                                                    "adr_tel_faxno":  "",
                                                    "yetkili":  [
                                                                    {
                                                                        "mye_isim":  "test yetkili isim 1",
                                                                        "mye_soyisim":  "test yetkili soyisim 1",
                                                                        "mye_dahili_telno":  "",
                                                                        "mye_email_adres":  "adasda@adasda.com.tr",
                                                                        "mye_cep_telno":  "05551234567"
                                                                    },
                                                                    {
                                                                        "mye_isim":  "test yetkili isim 3",
                                                                        "mye_soyisim":  "test yetkili soyisim 3",
                                                                        "mye_dahili_telno":  "",
                                                                        "mye_email_adres":  "adasda@adasda.com.tr",
                                                                        "mye_cep_telno":  "05551234567"
                                                                    }
                                                                ]
                                                },
                                                {
                                                    "adr_cadde":  "cadde2",
                                                    "adr_mahalle":  "mahalle2",
                                                    "adr_sokak":  "sokak2",
                                                    "adr_Semt":  "semt2",
                                                    "adr_Apt_No":  "A1",
                                                    "adr_Daire_No":  "2",
                                                    "adr_posta_kodu":  34340,
                                                    "adr_ilce":  "Sarıyer",
                                                    "adr_il":  "İstanbul",
                                                    "adr_ulke":  "TÜRKİYE",
                                                    "adr_tel_ulke_kodu":  "090",
                                                    "adr_tel_bolge_kodu":  "212",
                                                    "adr_tel_no1":  "4444444",
                                                    "adr_tel_no2":  "",
                                                    "adr_tel_faxno":  "",
                                                    "yetkili":  {
                                                                    "mye_isim":  "test yetkili isim 2",
                                                                    "mye_soyisim":  "test yetkili soyisim 2",
                                                                    "mye_dahili_telno":  "",
                                                                    "mye_email_adres":  "fafafafa@adasda.com.tr",
                                                                    "mye_cep_telno":  "05551234589"
                                                                }
                                                }
                                            ]
                              }
              }
}
```

### Stok Kaydet V2 Save

- Method: `POST`
- Path: `/API/APIMethods/StokKaydetV2`
- Amac: Yeni kayit/evrak olusturma.

Request body ornegi:

```json
{
    "Mikro":  {
                  "FirmaKodu":  "MIKROFLY",
                  "CalismaYili":  "2023",
                  "KullaniciKodu":  "SRV",
                  "Sifre":  "<REDACTED>",
                  "stoklar":  {
                                  "sto_kod":  "YS12347",
                                  "sto_isim":  "mikro api stok",
                                  "sto_kisa_ismi":  "",
                                  "sto_cins":  0,
                                  "sto_doviz_cinsi":  0,
                                  "sto_birim1_ad":  "ADET",
                                  "sto_perakende_vergi":  18,
                                  "sto_toptan_vergi":  18,
                                  "barkodlar":  {
                                                    "bar_kodu":  "2022000000010",
                                                    "bar_barkodtipi":  0,
                                                    "bar_birimpntr":  1,
                                                    "bar_master":  false
                                                },
                                  "satis_fiyatlari":  {
                                                          "sfiyat_listesirano":  1,
                                                          "sfiyat_deposirano":  1,
                                                          "sfiyat_odemeplan":  0,
                                                          "sfiyat_birim_pntr":  1,
                                                          "sfiyat_fiyati":  32.5,
                                                          "sfiyat_doviz":  0
                                                      }
                              }
              }
}
```

### Fatura Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/FaturaKaydetV2`
- Amac: Yeni kayit/evrak olusturma.

Request body ornegi:

```json
{
  "Mikro": {
        "FirmaKodu": "MIKROFLY",
        "CalismaYili": 2023,
        "KullaniciKodu": "SRV",
        "Sifre": "<REDACTED>",    
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

### Tahsilat Tediye Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/TahsilatTediyeKaydetV2`
- Amac: Yeni kayit/evrak olusturma.

Request body ornegi:

```json
{
    "Mikro":  {
                  "FirmaKodu":  "V16XX",
                  "CalismaYili":  2023,
                  "KullaniciKodu":  "SRV",
                  "Sifre":  "<REDACTED>",
                  "FirmaNo":  0,
                  "SubeNo":  0,
                  "evraklar":  {
                                   "evrak_aciklamalari":  [
                                                              {
                                                                  "aciklama":  "Test1bb"
                                                              },
                                                              {
                                                                  "aciklama":  "Test2fg"
                                                              },
                                                              {
                                                                  "aciklama":  "Testgghh"
                                                              },
                                                              {
                                                                  "aciklama":  "Test4jkjjk"
                                                              }
                                                          ],
                                   "satirlar":  {
                                                    "cha_tarihi":  "19.09.2023",
                                                    "cha_tip":  0,
                                                    "cha_cinsi":  19,
                                                    "cha_normal_Iade":  0,
                                                    "cha_evrak_tip":  34,
                                                    "cha_evrakno_seri":  "KSTED",
                                                    "cha_cari_cins":  0,
                                                    "cha_kod":  "GC",
                                                    "cha_d_kurtar":  null,
                                                    "cha_d_cins":  0,
                                                    "cha_d_kur":  1,
                                                    "cha_srmrkkodu":  "",
                                                    "cha_projekodu":  "",
                                                    "cha_kasa_hizmet":  4,
                                                    "cha_kasa_hizkod":  "NK",
                                                    "cha_vade":  "20230919",
                                                    "cha_meblag":  "1050",
                                                    "user_tablo":  {
                                                                       "TransactionReferenceId ":  "5f86e5b8a2d78b353c9fe8d7"
                                                                   },
                                                    "kredi_karti_taksit_bilgisi":  {
                                                                                       "Firma_taksit_sayisi":  5,
                                                                                       "Musteri_taksit_sayisi":  5,
                                                                                       "Sorumluluk_merkezi":  "",
                                                                                       "Toplam_tutar":  "6000",
                                                                                       "Kredi_kart_no":  1,
                                                                                       "Uye_isyeri_no":  "2",
                                                                                       "Kart_cekim_tarihi":  "19.09.2023",
                                                                                       "Kart_sahip_tipi":  0
                                                                                   }
                                                }
                               }
              }
}
```

### Borç Dekontu Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/DekontKaydetV2`
- Amac: Yeni kayit/evrak olusturma.

Request body ornegi:

```json
{
    "Mikro":  {
                  "FirmaKodu":  "MIKROFLY",
                  "CalismaYili":  "2023",
                  "KullaniciKodu":  "SRV",
                  "Sifre":  "<REDACTED>",
                  "evraklar":  {
                                   "evrak_aciklamalari":  [
                                                              {
                                                                  "aciklama":  "Test1bb"
                                                              },
                                                              {
                                                                  "aciklama":  "Test2fg"
                                                              },
                                                              {
                                                                  "aciklama":  "Testgghh"
                                                              },
                                                              {
                                                                  "aciklama":  "Test4jkjjk"
                                                              }
                                                          ],
                                   "satirlar":  {
                                                    "cha_tarihi":  "21.12.2023",
                                                    "cha_tip":  0,
                                                    "cha_normal_Iade":  0,
                                                    "cha_evrak_tip":  31,
                                                    "cha_evrakno_seri":  "BD",
                                                    "cha_cari_cins":  0,
                                                    "cha_kod":  "CR01",
                                                    "cha_d_kurtar":  null,
                                                    "cha_d_cins":  0,
                                                    "cha_d_kur":  1,
                                                    "cha_srmrkkodu":  "",
                                                    "cha_projekodu":  "",
                                                    "cha_kasa_hizmet":  2,
                                                    "cha_kasa_hizkod":  "1",
                                                    "cha_meblag":  "1000",
                                                    "user_tablo":  {
                                                                       "TransactionReferenceId ":  "5f86e5b8a2d78b353c9fe8d7"
                                                                   }
                                                }
                               }
              }
}
```

### Muhasebe Fişi Kaydet V2 Save

- Method: `POST`
- Path: `/Api/apiMethods/MuhasebeFisKaydetV2`
- Amac: Yeni kayit/evrak olusturma.

Request body ornegi:

```json
{
    "Mikro":  {
                  "FirmaKodu":  "MIKROFLY",
                  "CalismaYili":  "2023",
                  "KullaniciKodu":  "SRV",
                  "Sifre":  "<REDACTED>",
                  "evraklar":  [
                                   {
                                       "evrak_aciklamalari":  [
                                                                  {
                                                                      "aciklama":  "Test1cc"
                                                                  },
                                                                  {
                                                                      "aciklama":  "Test2hh"
                                                                  },
                                                                  {
                                                                      "aciklama":  "Testşlş"
                                                                  },
                                                                  {
                                                                      "aciklama":  "Test4jkjjk"
                                                                  }
                                                              ],
                                       "satirlar":  [
                                                        {
                                                            "fis_firmano":  0,
                                                            "fis_subeno":  0,
                                                            "fis_tarih":  "21.12.2023",
                                                            "fis_tur":  0,
                                                            "fis_hesap_kod":  120,
                                                            "fis_aciklama1":  "cari borç",
                                                            "fis_meblag0":  1180,
                                                            "fis_sorumluluk_kodu":  "",
                                                            "fis_ticari_tip":  2,
                                                            "fis_kurfarkifl":  0,
                                                            "fis_ticari_evraktip":  63,
                                                            "fis_tic_belgeno":  "",
                                                            "fis_tic_belgetarihi":  "21.12.2023",
                                                            "fis_katagori":  0,
                                                            "fis_fmahsup_tipi":  0,
                                                            "user_tablo":  {

                                                                           }
                                                        },
                                                        {
                                                            "fis_firmano":  0,
                                                            "fis_subeno":  0,
                                                            "fis_tarih":  "21.12.2023",
                                                            "fis_tur":  0,
                                                            "fis_hesap_kod":  391,
                                                            "fis_aciklama1":  "kdv alacak",
                                                            "fis_meblag0":  -180,
                                                            "fis_sorumluluk_kodu":  "",
                                                            "fis_ticari_tip":  2,
                                                            "fis_kurfarkifl":  0,
                                                            "fis_ticari_evraktip":  63,
                                                            "fis_tic_belgeno":  "",
                                                            "fis_tic_belgetarihi":  "21.12.2023",
                                                            "fis_katagori":  0,
                                                            "fis_fmahsup_tipi":  0,
                                                            "user_tablo":  {

                                                                           }
                                                        },
                                                        {
                                                            "fis_firmano":  0,
                                                            "fis_subeno":  0,
                                                            "fis_tarih":  "21.12.2023",
                                                            "fis_tur":  0,
                                                            "fis_hesap_kod":  600,
                                                            "fis_aciklama1":  "mal alacak",
                                                            "fis_meblag0":  -1000,
                                                            "fis_sorumluluk_kodu":  "",
                                                            "fis_ticari_tip":  2,
                                                            "fis_kurfarkifl":  0,
                                                            "fis_ticari_evraktip":  63,
                                                            "fis_tic_belgeno":  "",
                                                            "fis_tic_belgetarihi":  "21.12.2023",
                                                            "fis_katagori":  0,
                                                            "fis_fmahsup_tipi":  0,
                                                            "user_tablo":  {

                                                                           }
                                                        }
                                                    ],
                                       "fis_detay":  {
                                                         "mfd_ticari_tip":  2,
                                                         "mfd_evraktip":  63,
                                                         "mfd_cariunvan":  "MÜŞTERİMİZ",
                                                         "mfd_carivergidaireadi":  "",
                                                         "mfd_carivergidaireno":  1234567890,
                                                         "mfd_bsbakonututar":  1000,
                                                         "mfd_bsbatabii":  1,
                                                         "mfd_cariulkekodno":  "052",
                                                         "mfd_belgetarihi":  "21.12.2023",
                                                         "mfd_tutarnereden":  0,
                                                         "mfd_caritipi":  1,
                                                         "mfd_carikodu":  "MUSTERI",
                                                         "mfd_carimuhkodu":  120,
                                                         "mfd_belgeno":  "",
                                                         "mfd_kdvid":  0,
                                                         "mfd_kdvtutar":  0,
                                                         "mfd_kisaevraktipi":  1,
                                                         "mfd_satistipi":  0,
                                                         "mfd_alistipi":  0,
                                                         "mfd_tahtedtipi":  0,
                                                         "mfd_digerevrakadi":  "Satış faturası",
                                                         "mfd_evraktur":  0
                                                     }
                                   },
                                   {
                                       "evrak_aciklamalari":  [
                                                                  {
                                                                      "aciklama":  "Test1cc"
                                                                  },
                                                                  {
                                                                      "aciklama":  "Test2hh"
                                                                  },
                                                                  {
                                                                      "aciklama":  "Testşlş"
                                                                  },
                                                                  {
                                                                      "aciklama":  "Test4jkjjk"
                                                                  }
                                                              ],
                                       "satirlar":  [
                                                        {
                                                            "fis_firmano":  0,
                                                            "fis_subeno":  0,
                                                            "fis_tarih":  "21.12.2023",
                                                            "fis_tur":  0,
                                                            "fis_hesap_kod":  120,
                                                            "fis_aciklama1":  "cari borç",
                                                            "fis_meblag0":  1180,
                                                            "fis_sorumluluk_kodu":  "",
                                                            "fis_ticari_tip":  2,
                                                            "fis_kurfarkifl":  0,
                                                            "fis_ticari_evraktip":  63,
                                                            "fis_tic_belgeno":  "",
                                                            "fis_tic_belgetarihi":  "21.12.2023",
                                                            "fis_katagori":  0,
                                                            "fis_fmahsup_tipi":  0,
                                                            "user_tablo":  {

                                                                           }
                                                        },
                                                        {
                                                            "fis_firmano":  0,
                                                            "fis_subeno":  0,
                                                            "fis_tarih":  "21.12.2023",
                                                            "fis_tur":  0,
                                                            "fis_hesap_kod":  391,
                                                            "fis_aciklama1":  "kdv alacak",
                                                            "fis_meblag0":  -180,
                                                            "fis_sorumluluk_kodu":  "",
                                                            "fis_ticari_tip":  2,
                                                            "fis_kurfarkifl":  0,
                                                            "fis_ticari_evraktip":  63,
                                                            "fis_tic_belgeno":  "",
                                                            "fis_tic_belgetarihi":  "21.12.2023",
                                                            "fis_katagori":  0,
                                                            "fis_fmahsup_tipi":  0,
                                                            "user_tablo":  {

                                                                           }
                                                        },
                                                        {
                                                            "fis_firmano":  0,
                                                            "fis_subeno":  0,
                                                            "fis_tarih":  "21.12.2023",
                                                            "fis_tur":  0,
                                                            "fis_hesap_kod":  600,
                                                            "fis_aciklama1":  "mal alacak",
                                                            "fis_meblag0":  -1000,
                                                            "fis_sorumluluk_kodu":  "",
                                                            "fis_ticari_tip":  2,
                                                            "fis_kurfarkifl":  0,
                                                            "fis_ticari_evraktip":  63,
                                                            "fis_tic_belgeno":  "",
                                                            "fis_tic_belgetarihi":  "21.12.2023",
                                                            "fis_katagori":  0,
                                                            "fis_fmahsup_tipi":  0,
                                                            "user_tablo":  {

                                                                           }
                                                        }
                                                    ],
                                       "fis_detay":  {
                                                         "mfd_ticari_tip":  2,
                                                         "mfd_evraktip":  63,
                                                         "mfd_cariunvan":  "MÜŞTERİMİZ",
                                                         "mfd_carivergidaireadi":  "",
                                                         "mfd_carivergidaireno":  1234567890,
                                                         "mfd_bsbakonututar":  1000,
                                                         "mfd_bsbatabii":  1,
                                                         "mfd_cariulkekodno":  "052",
                                                         "mfd_belgetarihi":  "21.12.2023",
                                                         "mfd_tutarnereden":  0,
                                                         "mfd_caritipi":  1,
                                                         "mfd_carikodu":  "MUSTERI",
                                                         "mfd_carimuhkodu":  120,
                                                         "mfd_belgeno":  "",
                                                         "mfd_kdvid":  0,
                                                         "mfd_kdvtutar":  0,
                                                         "mfd_kisaevraktipi":  1,
                                                         "mfd_satistipi":  0,
                                                         "mfd_alistipi":  0,
                                                         "mfd_tahtedtipi":  0,
                                                         "mfd_digerevrakadi":  "Satış faturası",
                                                         "mfd_evraktur":  0
                                                     }
                                   }
                               ]
              }
}
```

## Endpoint Envanteri

Asagidaki tablolar collection icindeki tum requestleri kapsar. `Body ozeti` kolonu top-level alanlari ve varsa `Mikro` altindaki ana alanlari gosterir.

### Adres

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Adres Duzelt V2 Update | `POST` | `/API/APIMethods/AdresDuzeltV2` | top: Mikro; Mikro: adresler, CalismaYili, FirmaKodu, KullaniciKodu; arrays: adresler[] | Mevcut kayit/evrak guncelleme. |
| 2 | Adres kaydet V2 Save | `POST` | `/API/APIMethods/AdresKaydetV2` | top: Mikro; Mikro: adresler, CalismaYili, FirmaKodu, KullaniciKodu; arrays: adresler[] | Yeni kayit/evrak olusturma. |
| 3 | Adres Sil V2 Delete | `POST` | `/API/APIMethods/AdresSilV2` | top: Mikro; Mikro: adresler, CalismaYili, FirmaKodu, KullaniciKodu; arrays: adresler[] | Kayit/evrak silme. |

### Alım Satım Evrağı - Fatura

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Fatura PDF V2 | `POST` | `/API/APIMethods/FaturaPdfV2` | top: Mikro; Mikro: CalismaYili, Fatura_Guid, FirmaKodu, KullaniciKodu | Collection ornegindeki business islem. |
| 2 | Fatura Kaydet V3 Save | `POST` | `/api/APIMethods/FaturaKaydetV3` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 3 | Fatura Kaydet V3 Save Copy | `POST` | `/api/APIMethods/FaturaKaydetV3` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 4 | Alım Satım Evrağı Düzeltme V2 Update | `POST` | `/Api/apiMethods/AlimSatimEvragiDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Mevcut kayit/evrak guncelleme. |
| 5 | Alım Satım Evrağı Satır Ekle V2 Add Guid | `POST` | `/Api/apiMethods/AlimSatimEvragiDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Collection ornegindeki business islem. |
| 6 | Alım Satım Evrağı Satır Ekle V2 Add Guid Copy | `POST` | `/Api/apiMethods/AlimSatimEvragiDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Collection ornegindeki business islem. |
| 7 | Alım Satım Evrağı Kaydet V2 Alış Faturası Save | `POST` | `/Api/apiMethods/AlimSatimEvragiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 8 | Alım Satım Evrağı Kaydet V2 Hizmet Save | `POST` | `/Api/apiMethods/AlimSatimEvragiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 9 | Alım Satım Evrağı Kaydet V2 Masraf Save | `POST` | `/Api/apiMethods/AlimSatimEvragiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 10 | Alım Satım Evrağı Kaydet V2 Save | `POST` | `/Api/apiMethods/AlimSatimEvragiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 11 | Alım Satım Evrağı Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/AlimSatimEvragiSatirSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | GUID ile satir/kayit silme. |
| 12 | Alım Satım Evrağı Satır Sil V2 Delete Guid | `POST` | `/Api/apiMethods/AlimSatimEvragiSatirSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | GUID ile satir/kayit silme. |
| 13 | Alım Satım Evrağı Sil V2 Delete | `POST` | `/Api/apiMethods/AlimSatimEvragiSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Kayit/evrak silme. |
| 14 | Fatura Kaydet V2 Save | `POST` | `/Api/apiMethods/FaturaKaydetV2` | raw body var, JSON parse edilemedi | Yeni kayit/evrak olusturma. |
| 15 | Fatura Kaydet V2 Save Copy | `POST` | `/Api/apiMethods/FaturaKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |

### Alınan Teklif

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Alınan Teklif Düzelt V2 Update | `POST` | `/Api/apiMethods/AlinanTeklifDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Mevcut kayit/evrak guncelleme. |
| 2 | Alınan Teklif Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/AlinanTeklifDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Collection ornegindeki business islem. |
| 3 | Alınan Teklif Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/AlinanTeklifGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | GUID ile satir/kayit silme. |
| 4 | Alınan Teklif Kaydet V2 Save | `POST` | `/Api/apiMethods/AlinanTeklifKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 5 | Alınan Teklif Sil V2 Delete | `POST` | `/Api/apiMethods/AlinanTeklifSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Cari

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Cari Güncelle V2 Update | `POST` | `/API/APIMethods/CariGuncelleV2` | top: Mikro; Mikro: CalismaYili, cariler, FirmaKodu, KullaniciKodu; arrays: cariler[] | Mevcut kayit/evrak guncelleme. |
| 2 | Cari Kaydet V2 Save | `POST` | `/API/APIMethods/CariKaydetV2` | top: Mikro; Mikro: CalismaYili, cariler, FirmaKodu, KullaniciKodu; arrays: cariler[] | Yeni kayit/evrak olusturma. |

### Dahili Stok Hareket

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Dahili Stok Hareket Düzelt V2 Update | `POST` | `/Api/apiMethods/DahiliStokHareketDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Mevcut kayit/evrak guncelleme. |
| 2 | Dahili Stok Hareket Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/DahiliStokHareketDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Collection ornegindeki business islem. |
| 3 | Dahili Stok Hareket Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/DahiliStokHareketGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | GUID ile satir/kayit silme. |
| 4 | Dahili Stok Hareket Kaydet V2 Save | `POST` | `/Api/apiMethods/DahiliStokHareketKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 5 | Dahili Stok Hareket Kaydet V2 Save Copy | `POST` | `/Api/apiMethods/DahiliStokHareketKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 6 | Dahili Stok Hareket Sil V2 Delete | `POST` | `/Api/apiMethods/DahiliStokHareketSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Dekont

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Bankalar Arası Virman Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 2 | Borç Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 3 | Borç Dekontu Kaydet V2 Save Copy | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 4 | Cari Borç Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 5 | Cari Hesaplar Arası Virman Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 6 | Genel Amaçlı Virman Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 7 | Kasalar Arası Virman Dekontu Kaydet V2 Save | `POST` | `/Api/apiMethods/DekontKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 8 | Dekont Sil V2 Delete | `POST` | `/Api/apiMethods/DekontSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Depolar Arası Sipariş

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Depolar Arası Sipariş Düzelt V2 Update | `POST` | `/Api/apiMethods/DepolarArasiSiparisDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Mevcut kayit/evrak guncelleme. |
| 2 | Depolar Arası Sipariş Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/DepolarArasiSiparisDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Collection ornegindeki business islem. |
| 3 | Depolar Arası Sipariş Guid sil V2 Delete Guid | `POST` | `/Api/apiMethods/DepolarArasiSiparisGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | GUID ile satir/kayit silme. |
| 4 | Depolar Arası Sipariş Kaydet V2 Save | `POST` | `/Api/apiMethods/DepolarArasiSiparisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 5 | Depolar Arası Sipariş Sil V2 Delete | `POST` | `/Api/apiMethods/DepolarArasiSiparisSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Etiket Basım Kaydet

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Etiket Basım Kaydet V2 Save | `POST` | `/Api/apiMethods/EtiketBasimKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |

### Evrak Açıklamaları

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Evrak Açıklama Düzelt V2 Update | `POST` | `/Api/apiMethods/EvrakAciklamaDuzeltV2` | top: Mikro; Mikro: CalismaYili, evrak_aciklamalari, FirmaKodu, KullaniciKodu; arrays: evrak_aciklamalari[] | Mevcut kayit/evrak guncelleme. |
| 2 | Evrak Açıklama Kaydet V2 Save | `POST` | `/Api/apiMethods/EvrakAciklamaKaydetV2` | top: Mikro; Mikro: CalismaYili, evrak_aciklamalari, FirmaKodu, KullaniciKodu; arrays: evrak_aciklamalari[] | Yeni kayit/evrak olusturma. |
| 3 | Evrak Açıklama Sil V2 Delete | `POST` | `/Api/apiMethods/EvrakAciklamaSilV2` | top: Mikro; Mikro: CalismaYili, evrak_aciklamalari, FirmaKodu, KullaniciKodu; arrays: evrak_aciklamalari[] | Kayit/evrak silme. |

### Evrak Belge Resim

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Evrak Belge Resim Kaydet V2 Save | `POST` | `/Api/apiMethods/EvrakBelgeResimKaydetV2` | top: Mikro; Mikro: CalismaYili, evrak_resimleri, FirmaKodu, KullaniciKodu; arrays: evrak_resimleri[] | Yeni kayit/evrak olusturma. |
| 2 | Evrak Belge Resim Sil V2 Delete | `POST` | `/Api/apiMethods/EvrakBelgeResimSilV2` | top: Mikro; Mikro: CalismaYili, evrak_resimleri, FirmaKodu, KullaniciKodu; arrays: evrak_resimleri[] | Kayit/evrak silme. |

### Fiyat Değişikliği

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Fiyat Değişikliği Kaydet V2 Save | `POST` | `/Api/apiMethods/FiyatDegisikligiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |

### Image Data

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | ImageDataGetirV2 | `POST` | `/API/APIMethods/ImageDataGetirV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Image, KullaniciKodu | Collection ornegindeki business islem. |
| 2 | ImageDataKaydetV2 | `POST` | `/API/APIMethods/ImageDataKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Image, KullaniciKodu | Yeni kayit/evrak olusturma. |
| 3 | ImageDataSilV2 | `POST` | `/API/APIMethods/ImageDataSilV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Image, KullaniciKodu | Kayit/evrak silme. |

### İrsaliye

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Siparişten İrsaliye Oluşturma V2 Save | `POST` | `/api/APIMethods/SiparistenIrsaliyeOlusturmaV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 2 | Irsaliye Düzelt Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/IrsaliyeDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Collection ornegindeki business islem. |
| 3 | Irsaliye Düzelt V2 Update | `POST` | `/Api/apiMethods/IrsaliyeDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Mevcut kayit/evrak guncelleme. |
| 4 | IrsaliyeKaydet V2 (İhracat Satış ) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 5 | IrsaliyeKaydet V2 (İhraç Kayıtlı İade Alış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 6 | IrsaliyeKaydet V2 (İhraç Kayıtlı Mal Satış ) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 7 | IrsaliyeKaydet V2 (Perakende Alış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 8 | IrsaliyeKaydet V2 (Perakende İade Alış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 9 | IrsaliyeKaydet V2 (Perakende İade Çıkış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 10 | IrsaliyeKaydet V2 (Perakende Satış ) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 11 | IrsaliyeKaydet V2 (Toptan Alış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 12 | IrsaliyeKaydet V2 (Toptan İade Alış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 13 | IrsaliyeKaydet V2 (Toptan İade Çıkış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 14 | IrsaliyeKaydet V2 (Toptan Satış) Save | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 15 | IrsaliyeKaydet V2 (Toptan Satış) Save Copy | `POST` | `/Api/apiMethods/IrsaliyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 16 | Irsaliye Satır Sil V2 Delete Guid | `POST` | `/Api/apiMethods/IrsaliyeSatirSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | GUID ile satir/kayit silme. |
| 17 | Irsaliye Sil V2 Delete | `POST` | `/Api/apiMethods/IrsaliyeSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Kasa Masraf Fişi

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Kasa Masraf Fişi Kaydet V2 Save | `POST` | `/Api/apiMethods/KasaMasrafFisiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |

### Kayıt Kaydet

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Kayıt Kaydet V2 Delete | `POST` | `/Api/apiMethods/KayitKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Kayit, KullaniciKodu, Tablo; arrays: Kayit[], Tablo[] | Yeni kayit/evrak olusturma. |
| 2 | Kayıt Kaydet V2 Save | `POST` | `/Api/apiMethods/KayitKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Kayit, KullaniciKodu, Tablo; arrays: Kayit[], Tablo[] | Yeni kayit/evrak olusturma. |
| 3 | Kayıt Kaydet V2 Update | `POST` | `/Api/apiMethods/KayitKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, Kayit, KullaniciKodu, Tablo; arrays: Kayit[], Tablo[] | Yeni kayit/evrak olusturma. |

### Listeler

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Cari Listesi V2 | `POST` | `/Api/APIMethods/CariListesiV2` | top: FieldName, Index, Mikro, Size, Sort, WhereStr; Mikro: CalismaYili, FirmaKodu, KullaniciKodu | Listeleme veya sorgulama. |
| 2 | Cari Listesi V3 | `POST` | `/Api/APIMethods/CariListesiV3` | top: CariKod, CariVKNTCNo, IlkTarih, Index, Mikro, Size, SonTarih, Sort, TarihTipi; Mikro: CalismaYili, FirmaKodu, KullaniciKodu | Listeleme veya sorgulama. |
| 3 | KullaniciListesiV2 | `POST` | `/Api/APIMethods/KullaniciListesiV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu | Listeleme veya sorgulama. |
| 4 | KullaniciParametreleriV2 | `POST` | `/Api/APIMethods/KullaniciParametreleriV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu | Collection ornegindeki business islem. |
| 5 | ModoFastSellHızlıSatisOnayDurumV2 | `POST` | `/Api/APIMethods/ModoFastsellHSSozlesmesiOnayDurumV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu | Collection ornegindeki business islem. |
| 6 | ModoFastSellRestoranOnayDurumV2 | `POST` | `/Api/APIMethods/ModoFastsellRestoranSozlesmesiOnayDurumV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu | Collection ornegindeki business islem. |
| 7 | PorkodSozlesmeOnayDurumV2 | `POST` | `/Api/APIMethods/PorkodSozlesmeOnayDurumV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu | Collection ornegindeki business islem. |
| 8 | Stok Listesi V2 | `POST` | `/Api/APIMethods/StokListesiV2` | top: IlkTarih, Index, Mikro, Size, SonTarih, Sort, StokKod, TarihTipi; Mikro: CalismaYili, FirmaKodu, KullaniciKodu | Listeleme veya sorgulama. |
| 9 | VergiListesiV2 | `POST` | `/Api/APIMethods/VergiListesiV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu | Listeleme veya sorgulama. |

### Login-Logoff

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | APILogin | `POST` | `/Api/APIMethods/APILogin` | top: ApiKey, CalismaYili, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo | Oturum acma veya API kullanicisi dogrulama. |
| 2 | MikroApiUp | `POST` | `/Api/APIMethods/APILogin` | top: CalismaYili, FirmaKodu, FirmaNo, KullaniciKodu, Sifre, SubeNo | Collection ornegindeki business islem. |
| 3 | HealthCheck | `GET` | `/Api/APIMethods/HealthCheck` | - | Servis ayakta mi kontrolu. |
| 4 | HealthCheck2 | `GET` | `/Api/APIMethods/HealthCheck2` | - | Servis ayakta mi kontrolu. |
| 5 | LoggerDone-Get | `GET` | `/Api/APIMethods/LoggerDone` | top:  | Collection ornegindeki business islem. |
| 6 | Logoff | `POST` | `/Api/apiMethods/APILogoff` | top: KullaniciKodu | Oturumu kapatma. |
| 7 | Logoff V2 | `POST` | `/Api/apiMethods/APILogoffV2` | top: KullaniciKodu, Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu | Oturumu kapatma. |

### Muhasebe

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Dövizli Muhasebe Fişi Kaydet V2 Save | `POST` | `/Api/apiMethods/MuhasebeFisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 2 | Muhasebe Fişi Kaydet V2 Save | `POST` | `/Api/apiMethods/MuhasebeFisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 3 | Özel Mahsup Fişi Kaydet V2 Save | `POST` | `/Api/apiMethods/MuhasebeFisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 4 | Muhasebe Fişi Sil V2 Delete | `POST` | `/Api/apiMethods/MuhasebeFisSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Operasyon Tamamlama Fişi

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Operasyon Tamamlama Fşi Kaydet V2 Save | `POST` | `/Api/apiMethods/OperasyonTamamlamaFisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 2 | Operasyon Tamamlama Fişi Sil V2 Delete | `POST` | `/Api/apiMethods/OperasyonTamamlamaFisSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Personel

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Personel izin kaydet V2 Save | `POST` | `/API/APIMethods/PersonelizinKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, personelizinler; arrays: personelizinler[] | Yeni kayit/evrak olusturma. |
| 2 | Personel Kaydet V2 Save | `POST` | `/API/APIMethods/PersonelKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, personeller; arrays: personeller[] | Yeni kayit/evrak olusturma. |

### Proforma Sipariş

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Proforma Sipariş Kaydet V2 Save | `POST` | `/Api/apiMethods/ProformaSiparisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 2 | Proforma Sipariş Sil V2 Delete | `POST` | `/Api/apiMethods/ProformaSiparisSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Satın Alma Talep

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Satın Alma Talep V2 Save | `POST` | `/api/APIMethods/SatinAlmaTalepKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 2 | Satın Alma Talep Sil V2 Delete | `POST` | `/api/APIMethods/SatinAlmaTalepSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Satış Şartı

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Satış Şartı Düzelt V2 Update | `POST` | `/api/APIMethods/SatisSartiDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Mevcut kayit/evrak guncelleme. |
| 2 | Satış Şartı Guid Ekle V2 Add Guid | `POST` | `/api/APIMethods/SatisSartiDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Collection ornegindeki business islem. |
| 3 | Satış Şartı Guid Sil V2 Delete Guid | `POST` | `/api/APIMethods/SatisSartiGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | GUID ile satir/kayit silme. |
| 4 | Satış Şartı Kaydet V2 Save | `POST` | `/api/APIMethods/SatisSartiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 5 | Satış Şartı Sil V2 Delete | `POST` | `/api/APIMethods/SatisSartiSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Satin Alma Şartı

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Satın Alma Şartı Kaydet V2 Save | `POST` | `/api/APIMethods/SatinAlmaSartiKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 2 | Satın Alma Şartı Sil V2 Delete | `POST` | `/api/APIMethods/SatinAlmaSartiSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Sayım Sonuç Kaydet

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Sayım Sonuç Düzelt V2 Update | `POST` | `/Api/apiMethods/SayimSonuclariDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Mevcut kayit/evrak guncelleme. |
| 2 | Sayım Sonuç Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/SayimSonuclariDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Collection ornegindeki business islem. |
| 3 | Sayım Sonuç Kaydet V2 Save | `POST` | `/Api/apiMethods/SayimSonuclariKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 4 | Sayım Sonuç Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/SayimSonuclariSatirSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | GUID ile satir/kayit silme. |
| 5 | Sayım Sonuç Sil V2 Delete | `POST` | `/Api/apiMethods/SayimSonuclariSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Sipariş

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Konsinye Sipariş Kaydet V2 Save | `POST` | `/api/APIMethods/SiparisKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 2 | Normal Alınan Sipariş Kaydet V2 Save | `POST` | `/api/APIMethods/SiparisKaydetV2` | raw body var, JSON parse edilemedi | Yeni kayit/evrak olusturma. |
| 3 | Sipariş Kaydet V2 Save | `POST` | `/api/APIMethods/SiparisKaydetV2` | raw body var, JSON parse edilemedi | Yeni kayit/evrak olusturma. |
| 4 | Sipariş Düzelt V2 Update | `POST` | `/Api/apiMethods/SiparisDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Mevcut kayit/evrak guncelleme. |
| 5 | Sipariş Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/SiparisDuzeltV2` | raw body var, JSON parse edilemedi | Collection ornegindeki business islem. |
| 6 | Sipariş Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/SiparisGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | GUID ile satir/kayit silme. |
| 7 | Sipariş Sil V2 Delete | `POST` | `/Api/apiMethods/SiparisSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Stok

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Stok Kaydet V2 Save | `POST` | `/API/APIMethods/StokKaydetV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, stoklar; arrays: stoklar[] | Yeni kayit/evrak olusturma. |

### Tahsilat Tediye

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Tahsilat Tediye Düzelt V2 Update | `POST` | `/Api/apiMethods/TahsilatTediyeDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Mevcut kayit/evrak guncelleme. |
| 2 | Tahsilat Tediye Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/TahsilatTediyeGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | GUID ile satir/kayit silme. |
| 3 | Tahsilat Tediye Kaydet V2 Save | `POST` | `/Api/apiMethods/TahsilatTediyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 4 | Tahsilat Tediye Kaydet V2 Save Copy | `POST` | `/Api/apiMethods/TahsilatTediyeKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 5 | Tahsilat Tediye Kaydet V3 Çek Giriş Save | `POST` | `/Api/apiMethods/TahsilatTediyeKaydetV3` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 6 | Tahsilat Tediye Kaydet V3 Save | `POST` | `/Api/apiMethods/TahsilatTediyeKaydetV3` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, FirmaNo, KullaniciKodu, SubeNo; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 7 | Tahsilat Tediye Çek Çıkış Bordrosu Kaydet Save | `POST` | `/Api/apiMethods/TahsilatTediyeSCKaydet` | top: Mikro; Mikro: evraklar; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 8 | Tahsilat Tediye Çek Giriş Bordrosu Kaydet Save | `POST` | `/Api/apiMethods/TahsilatTediyeSCKaydet` | top: Mikro; Mikro: evraklar; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 9 | Tahsilat Tediye Giden Havale Kaydet Save | `POST` | `/Api/apiMethods/TahsilatTediyeSCKaydet` | top: Mikro; Mikro: evraklar; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 10 | Tahsilat Tediye Senet Çıkış Bordrosu Kaydet Save | `POST` | `/Api/apiMethods/TahsilatTediyeSCKaydet` | top: Mikro; Mikro: evraklar; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 11 | Tahsilat Tediye Senet Giriş Bordrosu Kaydet Save | `POST` | `/Api/apiMethods/TahsilatTediyeSCKaydet` | top: Mikro; Mikro: evraklar; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 12 | Tahsilat Tediye Sil V2 Delete | `POST` | `/Api/apiMethods/TahsilatTediyeSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Üretim İş Emri

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Üretim İş Emri Oluştur V2 Save | `POST` | `/API/APIMethods/UretimIsEmriOlusturV2` | top: Mikro; Mikro: CalismaYili, FirmaKodu, KullaniciKodu, Satirlar; arrays: Satirlar[] | Yeni kayit/evrak olusturma. |

### Üretim Talep

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Üretim Talep Guid Sil V2 Delete Guid | `POST` | `/Api/APIMethods/UretimTalepGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | GUID ile satir/kayit silme. |
| 2 | Üretim Talep Kaydet V2 Save | `POST` | `/Api/APIMethods/UretimTalepKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 3 | Üretim Talep Sil V2 Delete | `POST` | `/Api/APIMethods/UretimTalepSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Ürün Reçete

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Ürün Reçete Kaydet V2 Save | `POST` | `/Api/apiMethods/UrunReceteKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 2 | Ürün Reçete Sil V2 Delete | `POST` | `/Api/apiMethods/UrunReceteSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Ürün Rota

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Ürün Rota Kaydet V2 Save | `POST` | `/Api/apiMethods/UrunRotaKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 2 | Ürün Rota Sil V2 Delete | `POST` | `/Api/apiMethods/UrunRotaSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Ürün Rota Plan

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Ürün Rota Plan Kaydet V2 Save | `POST` | `/Api/apiMethods/UretimRotaPlanKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 2 | Ürün Rota Plan Sil V2 Delete | `POST` | `/Api/apiMethods/UretimRotaPlanSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

### Verilen Teklif

| # | Islem | Method | Path | Body ozeti | Kullanim |
|---:|---|---|---|---|---|
| 1 | Verilen Teklif Düzelt V2 Update | `POST` | `/Api/apiMethods/VerilenTeklifDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Mevcut kayit/evrak guncelleme. |
| 2 | Verilen Teklif Guid Ekle V2 Add Guid | `POST` | `/Api/apiMethods/VerilenTeklifDuzeltV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Collection ornegindeki business islem. |
| 3 | Verilen Teklif Guid Sil V2 Delete Guid | `POST` | `/Api/apiMethods/VerilenTeklifGuidSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | GUID ile satir/kayit silme. |
| 4 | Verilen Teklif Kaydet V2 Save | `POST` | `/Api/apiMethods/VerilenTeklifKaydetV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Yeni kayit/evrak olusturma. |
| 5 | Verilen Teklif Sil V2 Delete | `POST` | `/Api/apiMethods/VerilenTeklifSilV2` | top: Mikro; Mikro: CalismaYili, evraklar, FirmaKodu, KullaniciKodu; arrays: evraklar[] | Kayit/evrak silme. |

## Endpoint Aileleri Icin Pratik Notlar

- `...KaydetV2`: Genelde yeni belge/kart olusturur. Body icinde ilgili array alanlari bulunur.
- `...DuzeltV2` veya `...GuncelleV2`: Mevcut kaydi gunceller. Orneklerde genelde GUID veya belge kimlik alanlari ile birlikte degisen alanlar gonderilir.
- `...SilV2`: Belge/kart siler. Bazi endpointler belge seri/sira ile, bazilari GUID ile calisir.
- `...GuidSilV2` ve `...SatirSilV2`: Satir seviyesinde veya GUID tabanli silme icindir. UI/entegrasyon tarafinda GUID saklamak onemlidir.
- Liste endpointleri `Size` ve `Index` alanlariyla sayfalama destekliyor gibi gorunuyor. Tarih filtrelerinde `IlkTarih`, `SonTarih`, `TarihTipi` alanlari kullanilmis.
- Evrak endpointlerinde `evraklar[]` ana array, bunun altinda `satirlar[]` ve bazen `evrak_aciklamalari[]` bulunur.
- Fatura/irsaliye/siparis gibi belgelerde belge tipi, hareket tipi ve cins alanlari Mikro kolon mantigina baglidir; yanlis tip/cins stok, cari ve muhasebe etkisini degistirebilir.

## Bizim API Icin Entegrasyon Fikri

Bu collection dogrudan Mikro API endpointlerini gosteriyor. FurpaMerkezApi icinden kullanilacaksa onerilen yaklasim:

1. Mikro API icin tek bir typed client/service yazilir.
2. `Mikro` auth blok bilgileri configuration/secret store uzerinden doldurulur; controller bodylerinden sifre alinmaz.
3. Her business islem icin request DTO -> Mikro API payload mapper ayrilir.
4. Mikro API response ham olarak loglanir; basari/hata normalize edilip FurpaMerkezApi response modeline cevrilir.
5. Kritik islemlerde idempotency icin belge seri/sira/GUID veya clientRequestId benzeri iz alanlari tutulur.


