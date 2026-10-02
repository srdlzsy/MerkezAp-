# Parcali E-Irsaliye Inceleme Raporu

Tarih: 02.10.2026

Bu rapor Auth DB'deki kalici gonderim snapshot'i ile Uyumsoft `GetOutboxDespatch` cevabinin salt-okuma karsilastirmasidir. Canli veride degisiklik yapilmamistir.

## Ozet

| Mikro Evraki | E-Irsaliye | Mikro Snapshot | Uyumsoft | Eksik |
|---|---|---:|---:|---:|
| `F53/119668` | `FRM2026600131458` | 15 | 1 | 14 |
| `F56/87956` | `FRM2026600131386` | 37 | 15 | 22 |
| `F56/87973` | `FRM2026600131485` | 26 | 21 | 5 |

Bu uc belge gercekten parcali gonderilmistir. Eksik satirlara mevcut FRM/UUID topluca yazilmamalidir; Uyumsoft belgesinde bu satirlar yoktur.

## Kok Neden

Belgelerin olusturma kaynagi ayni degildir:

- `F56/87956` ve `F56/87973` eski arayuzun dogrudan olusturdugu belgeler degildir. Her iki belgenin tum Mikro satirlarinda ayni `FR...` create trace anahtari bulunmaktadir. Bu, sevklerin FurpaMerkezApi create endpointi ve `clientRequestId` akisi ile olusturuldugunu kesinlestirir.
- `F53/119668` satirlarinda `FR...` trace yoktur ve belge Auth DB'deki API create snapshot'ina baglanamamaktadir. Bu sevk FurpaMerkezApi tarafindan olusturulmamis; yalniz e-irsaliye gonderimi legacy kopru uzerinden yapilmistir.

Bu iki belgede eski API surumundeki tamamlama kontrolu su nedenle yetersiz kalmistir:

1. Mikro API satirlari henuz tamamlanirken create readback'i o anda gorunen parcayi okudu.
2. Create kaydi `F56/87956` icin 15, `F56/87973` icin 21 satirli response ile erken `Completed` kabul edildi.
3. Eski e-irsaliye kontrolu beklenen satir sayisini orijinal create requestinden degil, bu erken create response'undan aliyordu.
4. E-irsaliye aninda Mikro'da gorunen 15/21 satir ile create response'taki 15/21 esit oldugu icin kontrol yanlis pozitif verdi.
5. Uyumsoft'a bu parca gonderildikten sonra Mikro belgesi orijinal istekteki 37/26 satira tamamlandi.

Satir desenleri de bu akisi dogrulamaktadir:

- `F53/119668`: bizim API disinda olusturulan depolar arasi sevkin yalniz ilk satiri Mikro'da gorunurken legacy kopru 1 satirli UBL olusturmustur. Trace/create snapshot bulunmadigi icin kopru beklenen toplam 15 satiri bilememistir.
- `F56/87973`: ilk 21 satir gorunurken 21 satirli UBL olusturulmus; son 5 satir daha sonra Mikro belgesine eklenmis.
- `F56/87956`: Mikro API satirlari sirali tamamlamadigi icin o anda gorunen daginik 15 satir UBL'ye girmis; belge daha sonra 37 satira tamamlanmis.

Kesin koruma 30.09.2026 tarihli `738f88b` degisikliginde eklendi. Guncel kod beklenen satir sayisini `MobileOfflineSyncRequest.RequestPayload` icindeki orijinal `Lines.Count` degerinden alir; create response erken ve eksik uretilmis olsa bile onu beklenen toplam olarak kabul etmez. Ayrica belirsiz Mikro API sonucu, orijinal istekteki stok, miktar, birim, satir numarasi ve trace degerleri tam eslesmeden `Completed` durumuna toparlanmaz.

Bu nedenle iki F56 belgesi, bizim API kullanilmadigi icin degil, o tarihteki API surumu create response satir sayisina guvendigi icin parcali gonderilmistir. Mevcut surumde ayni durumda e-irsaliye gonderimi `409 Conflict` ile durdurulmalidir.

`F53/119668` icin ise ayni garanti kendiliginden uretilemez. Legacy arayuz sevki baska sistemde olusturdugu icin kopruye `expectedLineCount` gibi guvenilir bir toplam vermelidir. Aksi halde backend Mikro'da gorunen parcali satir setinin daha sonra buyuyup buyumeyecegini kesin olarak bilemez.

## F53/119668

Uyumsoft'a giden tek satir:

| Satir | Stok | Miktar | Ad |
|---:|---|---:|---|
| 1 | `016330` | 1.69 | DANA KASAP KOFTE KG |

Uyumsoft'a gitmeyen satirlar:

| Satir | Stok | Miktar |
|---:|---|---:|
| 2 | `015614` | 0.77 |
| 3 | `015734` | 1.55 |
| 4 | `016570` | 9.52 |
| 5 | `079858` | 12 |
| 6 | `008368` | 7.5 |
| 7 | `008373` | 13.59 |
| 8 | `008377` | 14.05 |
| 9 | `008375` | 7.05 |
| 10 | `008369` | 7.26 |
| 11 | `008376` | 3.3 |
| 12 | `074036` | 12 |
| 13 | `015937` | 32 |
| 14 | `015898` | 48 |
| 15 | `015368` | 8.45 |

## F56/87956

Uyumsoft'a giden satir numaralari:

```text
1, 2, 3, 4, 5, 6, 7, 8, 14, 17, 23, 26, 28, 33, 35
```

Uyumsoft'a gitmeyen satirlar:

| Satir | Stok | Miktar |
|---:|---|---:|
| 9 | `016154` | 14.57 |
| 10 | `053765` | 252 |
| 11 | `011056` | 20 |
| 12 | `001109` | 50.04 |
| 13 | `016419` | 7.88 |
| 15 | `017360` | 28.96 |
| 16 | `016167` | 30 |
| 18 | `016174` | 12 |
| 19 | `017292` | 47.52 |
| 20 | `010670` | 9.26 |
| 21 | `016257` | 16.56 |
| 22 | `047897` | 19.95 |
| 24 | `016146` | 22 |
| 25 | `016217` | 8.36 |
| 27 | `016201` | 21.93 |
| 29 | `016181` | 8.7 |
| 30 | `016129` | 21.8 |
| 31 | `054150` | 35.82 |
| 32 | `031070` | 12.45 |
| 34 | `047515` | 10 |
| 36 | `016824` | 34.26 |
| 37 | `016178` | 17.88 |

## F56/87973

Uyumsoft'a ilk 21 satir gitmistir. Gitmeyen satirlar:

| Satir | Stok | Miktar |
|---:|---|---:|
| 22 | `047822` | 23.02 |
| 23 | `016824` | 34.26 |
| 24 | `016125` | 16.5 |
| 25 | `047541` | 102.5 |
| 26 | `016178` | 17.88 |

## Islem Karari

- Mevcut FRM/UUID sadece Uyumsoft'ta gercekten bulunan satirlara aittir.
- Eksik satirlar mevcut e-irsaliyeye sonradan eklenemez ve mevcut FRM ile isaretlenmemelidir.
- Karsi tarafin mal kabul edecegi satirlar Uyumsoft belgesindeki satirlarla sinirlidir.
- Eksik kalan satirlar is gereksinimine gore yeni bir sevk/e-irsaliye ile yeniden olusturulmali veya muhasebe/stok kontrol onayi ile iptal-duzeltme surecine alinmalidir.
- Bu uc kayit `NeedsReview` olarak kalmali; otomatik metadata tamamlama uygulanmamalidir.
