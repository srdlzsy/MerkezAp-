# Mikro Muhasebe Akis Rehberi

Bu dokuman Furpa sisteminde kasadan, faturadan, iadeden, tahsilattan ve
tediyeden baslayip Mikro ERP veritabaninda hangi kayitlarin olustugunu aciklar.
Amac sadece tablo adi ezberlemek degil; muhasebecinin gordugu is olayi ile
Mikro DB'deki karsiligini birlikte okumaktir.

## 1. En Buyuk Resim

Sistemde iki ana veritabani mantigi var:

| Katman | Gorev | Ornek tablolar |
|---|---|---|
| Furpa DB | Kasa/POS ham verisi, operasyonel ara kayitlar, raporlama yardimci tablolari | `PosFaturas`, `PosFaturaSatirs`, `PosFaturaOdemes`, `ZReportTotals`, `TurnoverTotals` |
| Mikro DB | Resmi ERP hareketleri, stok, cari, kasa, depo, siparis kayitlari | `STOK_HAREKETLERI`, `CARI_HESAP_HAREKETLERI`, `STOKLAR`, `CARI_HESAPLAR`, `KASALAR`, `DEPOLAR` |

Basit cumleyle:

```text
Furpa DB = kasadan gelen ham islem ve operasyonel takip
Mikro DB = resmi stok, cari, fatura, tahsilat, tediye ve muhasebe izi
```

Kasa satisinda Furpa once ham fisi tutar. Sonra aktarim proseduru bu veriyi
Mikro'nun anlayacagi stok ve cari hareketlerine cevirir.

Firma faturasi, mal kabul, iade, tahsilat, tediye gibi olaylarda ise asil
okunacak yer Mikro'nun hareket tablolaridir.

## 2. Mikro'nun Temel Mantigi

Mikro'da her islem tek bir "fatura tablosuna" dusuyor gibi dusunulmemeli.
Mikro daha cok iki buyuk defter gibi calisir:

| Defter | Tablo | Ne anlatir? |
|---|---|---|
| Stok defteri | `STOK_HAREKETLERI` | Mal/urun ne yapti? Girdi mi, cikti mi, hangi depoya girdi, hangi depodan cikti? |
| Cari defteri | `CARI_HESAP_HAREKETLERI` | Para, borc, alacak ne yapti? Kim borclandi, kimden tahsil edildi, kime odeme yapildi? |

Bu iki tablo ayni evrak uzerinden birbirine baglanir:

```text
STOK_HAREKETLERI.sth_evrakno_seri  + sth_evrakno_sira
CARI_HESAP_HAREKETLERI.cha_evrakno_seri + cha_evrakno_sira
```

Yani ayni seri/sira genelde ayni is belgesinin stok ve cari tarafini gosterir.

## 3. Mikro Ana Tablolari

| Tablo | Anlami |
|---|---|
| `STOKLAR` | Urun kartlari. Stok kodu, urun adi, birim, KDV, grup, marka vb. |
| `CARI_HESAPLAR` | Musteri, tedarikci, firma, sahis, bazen market/kasa iliskili cari kartlar |
| `STOK_HAREKETLERI` | Stok giris/cikis hareketleri. Satis, alis, iade, sevk, mal kabul burada izlenir |
| `CARI_HESAP_HAREKETLERI` | Fatura, tahsilat, tediye, cari borc/alacak hareketleri |
| `KASALAR` | Kasa tanimlari. Nakit kasa, POS kasasi, sube kasalari, cek/senet kasalari |
| `DEPOLAR` | Depo/sube/market tanimlari |
| `SIPARISLER` | Firma siparisleri |
| `DEPOLAR_ARASI_SIPARISLER` | Depolar arasi siparisler |

## 4. En Onemli Alanlar

### 4.1 Stok hareketi alanlari

| Alan | Ne anlatir? |
|---|---|
| `sth_tip` | Stok hareket yonu. Genelde `0=giris`, `1=cikis`, `2=depolar arasi` gibi okunur |
| `sth_cins` | Hareketin cinsi. Satis, alis, virman, sevk gibi alt anlam tasir |
| `sth_normal_iade` | `0=normal`, `1=iade` |
| `sth_evraktip` | Evrak tipi. Fatura, irsaliye, fis vb. ayrimi |
| `sth_evrakno_seri` | Evrak seri kodu. Ornek: `PS149`, `FRP26`, `F50` |
| `sth_evrakno_sira` | Evrak sira numarasi |
| `sth_satirno` | Evrak icindeki satir numarasi |
| `sth_stok_kod` | Urun kodu |
| `sth_miktar` | Miktar |
| `sth_tutar` | Satir tutari |
| `sth_vergi` | Vergi tutari |
| `sth_cari_kodu` | Ilgili cari kod |
| `sth_giris_depo_no` | Urunun girdigi depo/sube |
| `sth_cikis_depo_no` | Urunun ciktigi depo/sube |
| `sth_pos_satis` | POS/kasa kaynakli satis hareketi isareti |
| `sth_fat_uid` | Ayni faturaya bagli stok satirlarini gruplayan UID |

### 4.2 Cari hareketi alanlari

| Alan | Ne anlatir? |
|---|---|
| `cha_tip` | Cari hareket yonu. Borc/alacak etkisini belirler |
| `cha_cinsi` | Cari hareket cinsi. Fatura, tahsilat, tediye, virman vb. |
| `cha_normal_Iade` | `0=normal`, `1=iade` |
| `cha_evrak_tip` | Cari evrak tipi |
| `cha_evrakno_seri` | Evrak seri kodu |
| `cha_evrakno_sira` | Evrak sira numarasi |
| `cha_satir_no` | Cari evrak satir numarasi |
| `cha_kod` | Asil cari kod |
| `cha_ciro_cari_kodu` | Ciro/musteri carisi |
| `cha_meblag` | Hareket tutari |
| `cha_aratoplam` | Ara toplam |
| `cha_vergi1..cha_vergi20` | Vergi kirilimlari |
| `cha_kasa_hizmet` | Kasa/hizmet/banka tarafini ayiran alan |
| `cha_kasa_hizkod` | Kasa/hizmet/banka kodu. Ornek: `0002`, `0008` |
| `cha_pos_hareketi` | POS/kasa kaynakli cari hareket isareti |
| `cha_srmrkkodu` | Sorumluluk merkezi/sube kodu |

## 5. Kasa Satisi: Bastan Sona Akis

Market kasasinda satis oldugunda once ham veri Furpa DB'ye gelir.

### 5.1 Furpa DB tarafinda olusan kayitlar

| Tablo | Ne tutar? |
|---|---|
| `PosFaturas` | Fis/fatura basligi. Sube, tarih, fis no, kasa no, toplam, KDV, indirim |
| `PosFaturaSatirs` | Fisteki urun satirlari. Urun kodu, miktar, fiyat, KDV |
| `PosFaturaOdemes` | Odeme detaylari. Nakit, kredi karti, yemek karti, cek vb. |
| `PosFaturaIptals` | Iptal edilen fis basliklari |
| `PosFaturaSatirIptals` | Iptal edilen fis satirlari |
| `PosFaturaPromosyons` | Promosyon/indirim detaylari |

`PosFaturas.MikroAktarimDurumu` alaninin anlami:

| Deger | Anlam |
|---|---|
| `0` | Henuz Mikro'ya aktarilmamis |
| `1` | Mikro'ya aktarilmis |

### 5.2 Aktarim servisi

Kod tarafinda kasa hareket aktarimi su servis uzerinden yurur:

```text
KasaHareketAktarimiService
```

Bu servis:

1. Kasa dosyalarini/ham hareketleri okur.
2. `PosFaturas`, `PosFaturaSatirs`, `PosFaturaOdemes` tablolarina yazar.
3. Mikro'ya aktarim icin `StokHareketYaz` prosedurunu calistirir.

### 5.3 Mikro'ya aktarim proseduru

Asil donusum proseduru:

```text
Furpa.dbo.StokHareketYaz
```

Bu prosedur Furpa DB'deki POS verisini okur ve Mikro DB'ye yazar:

```text
Furpa.dbo.PosFaturas
Furpa.dbo.PosFaturaSatirs
        |
        v
MikroDB_V16_FURPA_2024.dbo.CARI_HESAP_HAREKETLERI
MikroDB_V16_FURPA_2024.dbo.STOK_HAREKETLERI
```

Aktarim bitince:

```sql
UPDATE PosFaturas
SET MikroAktarimDurumu = 1
WHERE MikroAktarimDurumu = 0
```

### 5.4 Mikro DB'deki POS satis karsiligi

Satis icin stok hareketi genelde su anlamdadir:

```text
sth_evrakno_seri = 'PS' + Sube
sth_tip = 1
sth_cins = 1
sth_normal_iade = 0
sth_evraktip = 4
sth_pos_satis = 1
```

Ornek:

```text
Sube 149 satisi -> PS149 serisi
```

Bu stok tarafinda sunu demektir:

```text
Urun sube/depo stokundan cikti.
Bu hareket POS/kasa kaynakli satis hareketidir.
```

Cari tarafinda genelde:

```text
cha_evrakno_seri = 'PS' + Sube
cha_evrak_tip = 63
cha_tip = 0
cha_cinsi = 7
cha_normal_Iade = 0
cha_pos_hareketi = 1
```

Bu cari tarafinda sunu demektir:

```text
Perakende satis/cari hareketi olustu.
Satis tutari, KDV ve cari etkisi Mikro'ya yazildi.
```

## 6. POS Odeme Detaylari Nerede?

POS satisinin stok ve cari etkisi Mikro'ya gider. Ancak odeme kirilimi en temiz
sekilde Furpa DB'de okunur:

```text
Furpa.dbo.PosFaturaOdemes
```

Onemli alanlar:

| Alan | Anlam |
|---|---|
| `OdemeTipi` | Odeme tipi. Nakit, kart, cek vb. |
| `SdxTipKodu` | Alt odeme/kart/yemek karti kodu |
| `Tutar` | Odeme tutari |
| `KasaKodu` | Kasa numarasi |
| `Sube` | Sube |
| `Tarih` | Islem tarihi |

Mikro tarafinda kasa tanimlari:

```text
Mikro.dbo.KASALAR
```

Ornek kasa kodlari:

| Kasa kodu | Anlam |
|---|---|
| `0002` | Sube TL kasasi gibi kullaniliyor |
| `0008` | Sube POS kasasi gibi kullaniliyor |
| `101`, `102`, `149` vb. | Sube bazli kasa tanimlari |

## 7. Gun Sonu / Z Raporu / Ciro

Gun sonu kontrolunde Furpa DB daha operasyonel bilgi verir.

| Tablo | Ne anlatir? |
|---|---|
| `ZReportTotals` | Z raporu genel toplami |
| `ZReportDetails` | KDV oranina gore Z raporu detaylari |
| `ZReportBankDetails` | Banka/POS detaylari |
| `TurnoverTotals` | Gunluk ciro toplam kaydi |
| `TurnoverDetails` | Kasa bazli nakit, kredi, hediye karti, cek vb. detay |

Muhasebeci gun sonunda su kontrolleri yapar:

```text
Kasadan gelen toplam satis = Furpa POS kayitlari
Mikro'ya aktarilan resmi satis = STOK_HAREKETLERI + CARI_HESAP_HAREKETLERI
Z raporu / banka / nakit = Furpa ZReport ve Turnover tablolari
```

Eger fark varsa:

1. Fis Furpa'ya gelmis mi?
2. `MikroAktarimDurumu` 1 mi?
3. Mikro'da `PS+Sube` serili hareket var mi?
4. POS/nakit/cek odeme kirilimi Furpa'da dogru mu?
5. Mikro cari/stok toplamiyla Z raporu tutuyor mu?

## 8. Firmaya Fatura Kestik: Biz Sattik

Bu islem toptan/musteri satisidir.

Muhasebe anlami:

```text
Stok azalir.
Musteri bize borclanir.
Satis geliri ve hesaplanan KDV olusur.
```

Mikro karsiligi:

| Etki | Mikro tablo |
|---|---|
| Urun stoktan cikar | `STOK_HAREKETLERI` |
| Musteri carisine borc yazilir | `CARI_HESAP_HAREKETLERI` |
| KDV/tutar islenir | `sth_vergi`, `cha_vergi*`, `sth_tutar`, `cha_meblag` |

Genel stok okuma mantigi:

```sql
SELECT *
FROM STOK_HAREKETLERI
WHERE sth_tip = 1
  AND sth_normal_iade = 0
  AND sth_cari_kodu = '<musteri_cari_kodu>';
```

Genel cari okuma mantigi:

```sql
SELECT *
FROM CARI_HESAP_HAREKETLERI
WHERE cha_kod = '<musteri_cari_kodu>'
  AND cha_normal_Iade = 0;
```

Bu islemi okurken asil soru:

```text
Ayni seri/sira ile stok cikisi ve cari borc hareketi olusmus mu?
```

## 9. Firma Bize Fatura Kesti: Biz Aldik

Bu islem alis/mal kabul tarafidir.

Muhasebe anlami:

```text
Stok artar.
Tedarikciye borclaniriz.
Indirilecek KDV olusur.
```

Mikro karsiligi:

| Etki | Mikro tablo |
|---|---|
| Urun depoya girer | `STOK_HAREKETLERI` |
| Tedarikci carisine borc/alacak hareketi yazilir | `CARI_HESAP_HAREKETLERI` |
| Fatura/mal kabul bilgisi baglanir | Evrak seri/sira, belge no, tarih alanlari |

Genel stok okuma mantigi:

```sql
SELECT *
FROM STOK_HAREKETLERI
WHERE sth_tip = 0
  AND sth_normal_iade = 0
  AND sth_cari_kodu = '<tedarikci_cari_kodu>';
```

Burada `sth_giris_depo_no` urunun hangi depoya/subeye girdigini gosterir.

Muhasebeci bu olayda sunlara bakar:

1. Gelen e-fatura var mi?
2. Irsaliye/mal kabul ile fatura tutuyor mu?
3. Stok miktari dogru girmis mi?
4. Tedarikci carisine dogru borc yazilmis mi?
5. KDV dogru ayrilmis mi?

## 10. Musteri Iadesi

Musteri aldigi urunu geri getirdiginde veya firmadan iade geldiginde islem
satisin tersidir.

Muhasebe anlami:

```text
Stok geri girer.
Musteri borcu azalir veya musteriye odeme/iade dogar.
Satis ve KDV ters calisir.
```

Mikro'da ana isaret:

```text
normal_iade = 1
```

Stok tarafinda:

```sql
SELECT *
FROM STOK_HAREKETLERI
WHERE sth_normal_iade = 1;
```

Cari tarafinda:

```sql
SELECT *
FROM CARI_HESAP_HAREKETLERI
WHERE cha_normal_Iade = 1;
```

Canli sistemde musteri iadesi icin `FRP26` gibi seriler gorulebilir.

## 11. Tedarikciye Iade

Biz tedarikciden aldigimiz mali geri gonderirsek alisin tersi olur.

Muhasebe anlami:

```text
Stok azalir.
Tedarikciye olan borcumuz azalir.
Alis/KDV etkisi ters kayitla duzelir.
```

Mikro karsiligi:

| Etki | Mikro tablo |
|---|---|
| Urun depodan cikar | `STOK_HAREKETLERI` |
| Tedarikci cari bakiyesi duzelir | `CARI_HESAP_HAREKETLERI` |

Stok tarafinda bakilacak alanlar:

```text
sth_tip
sth_normal_iade
sth_cari_kodu
sth_cikis_depo_no
sth_evrakno_seri
sth_evrakno_sira
```

## 12. Tahsilat: Musteri Bize Odedi

Tahsilat, mal hareketi degil para/cari hareketidir.

Muhasebe anlami:

```text
Banka veya kasa artar.
Musterinin bize olan borcu azalir.
```

Mikro'da ana tablo:

```text
CARI_HESAP_HAREKETLERI
```

Bakilacak alanlar:

| Alan | Anlam |
|---|---|
| `cha_kod` | Odeme yapan cari |
| `cha_meblag` | Tahsil edilen tutar |
| `cha_tip` | Hareket yonu |
| `cha_cinsi` | Tahsilat/odeme cinsi |
| `cha_evrak_tip` | Tahsilat evrak tipi |
| `cha_kasa_hizmet` | Kasa/banka/hizmet ayrimi |
| `cha_kasa_hizkod` | Hangi kasa/banka kodu |

Pratik okuma:

```sql
SELECT *
FROM CARI_HESAP_HAREKETLERI
WHERE cha_kod = '<musteri_cari_kodu>'
ORDER BY cha_tarihi DESC;
```

Muhasebeci tahsilatta sunu yapar:

1. Para hangi banka/kasaya geldi?
2. Hangi cariden geldi?
3. Hangi faturayi kapatti?
4. Acik bakiye kaldi mi?

## 13. Tediye: Biz Odedik

Tediye, tedarikciye veya bir cariye yapilan para cikisidir.

Muhasebe anlami:

```text
Banka veya kasa azalir.
Tedarikciye olan borcumuz azalir.
```

Mikro'da ana tablo yine:

```text
CARI_HESAP_HAREKETLERI
```

Tahsilatla ayni alanlar okunur ama `cha_tip`, `cha_cinsi`, `cha_evrak_tip`
islemin para cikisi oldugunu gosterir.

Muhasebeci tediyede sunu yapar:

1. Odeme hangi bankadan/kasadan cikti?
2. Hangi tedarikciye yapildi?
3. Hangi faturalari kapatti?
4. Vade ve acik bakiye dogru mu?

## 14. Cari Kartlar

Musteri ve tedarikci kartlari Mikro'da:

```text
CARI_HESAPLAR
```

Onemli alanlar:

| Alan | Anlam |
|---|---|
| `cari_kod` | Cari kod |
| `cari_unvan1`, `cari_unvan2` | Unvan/ad |
| `cari_VergiKimlikNo`, `cari_vdaire_no` | Vergi/TCKN bilgisi |
| `cari_fatura_adres_no` | Fatura adresi |
| `cari_sevk_adres_no` | Sevk adresi |
| `cari_temsilci_kodu` | Temsilci/plasiyer |
| `cari_cari_kilitli_flg` | Cari kilitli mi |
| `cari_firma_acik_kapal` | Firma acik/kapali |

Bir faturanin dogru cariye gitmesi icin `cha_kod`, `sth_cari_kodu` ve
`CARI_HESAPLAR.cari_kod` birbiriyle uyumlu olmalidir.

## 15. Stok Kartlari

Urun kartlari Mikro'da:

```text
STOKLAR
```

Onemli alanlar:

| Alan | Anlam |
|---|---|
| `sto_kod` | Stok kodu |
| `sto_isim` | Urun adi |
| `sto_birim1_ad` | Ana birim |
| `sto_perakende_vergi` | Perakende KDV |
| `sto_toptan_vergi` | Toptan KDV |
| `sto_reyon_kodu`, `sto_marka_kodu`, `sto_anagrup_kod` | Raporlama/gruplama alanlari |
| `sto_pasif_fl` | Pasif urun |

Bir POS satis satirinda `PosFaturaSatirs.UrunKodu`, Mikro tarafinda
`STOK_HAREKETLERI.sth_stok_kod` olarak gorulur.

## 16. Depo / Sube Mantigi

Mikro'da subeler genelde depo gibi de davranir.

```text
DEPOLAR.dep_no
DEPOLAR.dep_adi
```

Stok hareketinde:

| Alan | Anlam |
|---|---|
| `sth_giris_depo_no` | Urun nereye girdi |
| `sth_cikis_depo_no` | Urun nereden cikti |

POS satisinda genelde giris ve cikis depo alanlari ayni sube numarasini
tutabilir; asil anlam `sth_tip=1` oldugu icin stok cikisidir.

## 17. Depolar Arasi Hareketler

Depolar arasi sevklerde mantik farklidir:

```text
Bir depodan cikar, diger depoya girer.
```

Okunacak tablo:

```text
STOK_HAREKETLERI
```

Bakilacak alanlar:

```text
sth_tip
sth_cins
sth_evraktip
sth_giris_depo_no
sth_cikis_depo_no
sth_normal_iade
```

Siparis bazli depo islemlerinde:

```text
DEPOLAR_ARASI_SIPARISLER
STOK_HAREKETLERI_EK.sth_subesip_uid
```

baglantisi da devreye girebilir.

## 18. Bir Evragi Mikro'da Nasil Takip Ederiz?

Bir belgeyi takip etmek icin once seri ve sira bulunur.

Ornek:

```text
Seri: PS149
Sira: 28798
```

Stok tarafini oku:

```sql
SELECT *
FROM STOK_HAREKETLERI
WHERE sth_evrakno_seri = 'PS149'
  AND sth_evrakno_sira = 28798
ORDER BY sth_satirno;
```

Cari tarafini oku:

```sql
SELECT *
FROM CARI_HESAP_HAREKETLERI
WHERE cha_evrakno_seri = 'PS149'
  AND cha_evrakno_sira = 28798
ORDER BY cha_satir_no;
```

Bu iki sorgu birlikte belgenin tam resmini verir:

```text
Stok tarafinda hangi urunler hareket etti?
Cari tarafinda hangi cari/tutar/KDV hareketi olustu?
```

## 19. Satis, Alis, Iade, Tahsilat, Tediye Kisa Harita

| Olay | Stok etkisi | Cari/para etkisi | Mikro ana tablosu |
|---|---|---|---|
| Kasa satisi | Stok cikar | POS/perakende satis cari hareketi olusur | `STOK_HAREKETLERI` + `CARI_HESAP_HAREKETLERI` |
| Firmaya satis faturasi | Stok cikar | Musteri borclanir | `STOK_HAREKETLERI` + `CARI_HESAP_HAREKETLERI` |
| Firma bize fatura kesti | Stok/hizmet/maliyet girer | Tedarikciye borclaniriz | `STOK_HAREKETLERI` + `CARI_HESAP_HAREKETLERI` |
| Musteri iadesi | Stok geri girer | Musteri borcu azalir/para iadesi dogar | `normal_iade=1` hareketleri |
| Tedarikciye iade | Stok cikar | Tedarikci borcu azalir | `STOK_HAREKETLERI` + `CARI_HESAP_HAREKETLERI` |
| Tahsilat | Stok yok | Musteri borcu azalir, kasa/banka artar | `CARI_HESAP_HAREKETLERI` |
| Tediye | Stok yok | Tedarikci borcu azalir, kasa/banka azalir | `CARI_HESAP_HAREKETLERI` |
| Z raporu/ciro | Stoktan cok kontrol/rapor | Nakit/POS/ciro kontrolu | Furpa `ZReport*`, `Turnover*` + Mikro hareketleri |

## 20. Muhasebeci Bu Sistemde Ne Kontrol Eder?

Muhasebecinin derdi tablo degil, dengenin tutmasidir.

### 20.1 Satis kontrolu

```text
Furpa POS toplam satisi
= Mikro PS serili stok/cari satis hareketleri
= Z raporu / gun sonu ciro
```

Kontrol sorulari:

1. Kasa fisleri Furpa'ya gelmis mi?
2. `MikroAktarimDurumu` 1 olmus mu?
3. Mikro'da `PS+Sube` serisiyle hareket olusmus mu?
4. Stok cikisi dogru mu?
5. Cari ve KDV toplam tutuyor mu?
6. Nakit/POS/cek/yemek karti toplam tutuyor mu?

### 20.2 Alis kontrolu

```text
Gelen fatura
= Mal kabul / irsaliye
= Stok girisi
= Tedarikci cari borcu
```

Kontrol sorulari:

1. Fatura dogru tedarikciden mi geldi?
2. Mal gercekten geldi mi?
3. Miktar ve fiyat tutuyor mu?
4. KDV dogru mu?
5. Tedarikci cari bakiyesi dogru mu?

### 20.3 Tahsilat kontrolu

```text
Musteri odedi
= Banka/kasa artti
= Musteri carisi kapandi/azaldi
```

Kontrol sorulari:

1. Odeme dogru cariye mi islenmis?
2. Hangi faturayi kapatmis?
3. Banka dekontu/kasa kaydi var mi?
4. Acik bakiye kalmis mi?

### 20.4 Tediye kontrolu

```text
Biz odedik
= Banka/kasa azaldi
= Tedarikci borcu kapandi/azaldi
```

Kontrol sorulari:

1. Odeme dogru tedarikciye mi gitmis?
2. Hangi faturalar kapandi?
3. Vade dogru mu?
4. Banka/kasa cikisi tutuyor mu?

## 21. En Pratik Debug Sirasi

Bir olayda sorun varsa su sirayla bak:

### 21.1 Kasa satisi Mikro'ya dusmedi mi?

1. Furpa `PosFaturas` icinde fis var mi?
2. `PosFaturaSatirs` icinde satirlari var mi?
3. `PosFaturaOdemes` icinde odeme var mi?
4. `PosFaturas.MikroAktarimDurumu` kac?
5. Mikro `STOK_HAREKETLERI` icinde `PS+Sube` seri var mi?
6. Mikro `CARI_HESAP_HAREKETLERI` icinde ayni seri/sira var mi?

### 21.2 Stok tutmuyor mu?

1. Urun `STOKLAR` kartinda var mi?
2. `STOK_HAREKETLERI` hareketleri dogru mu?
3. Giris/cikis depolari dogru mu?
4. Iade hareketi `normal_iade=1` ile terslenmis mi?
5. Ayni evrak iki kere aktarilmis mi?

### 21.3 Cari bakiye tutmuyor mu?

1. Cari `CARI_HESAPLAR` kartinda var mi?
2. Fatura hareketi `CARI_HESAP_HAREKETLERI` icinde var mi?
3. Tahsilat/tediye ayni cariye islenmis mi?
4. Iade hareketi ters kayit olarak var mi?
5. Seri/sira ile stok ve cari tarafi ayni belgeye mi ait?

### 21.4 Kasa/POS tutmuyor mu?

1. `PosFaturaOdemes` odeme kirilimi dogru mu?
2. `ZReportTotals` ve `TurnoverTotals` gun sonu toplami dogru mu?
3. Mikro `CARI_HESAP_HAREKETLERI` POS/cari hareketi var mi?
4. `KASALAR` taniminda kasa kodu dogru mu?
5. Banka/POS gecisi ile kasa raporu tutuyor mu?

## 22. Kisa Ozet

Sistemin kalbi su cumledir:

```text
Furpa kasadan ham veriyi toplar.
Mikro resmi stok ve cari hareketi tutar.
Muhasebeci ikisinin toplam ve belge bazinda tutup tutmadigini kontrol eder.
```

Mikro'da bir olayi anlamak icin once su sorular sorulur:

1. Bu olay mal hareketi mi?
   - Evetse `STOK_HAREKETLERI`.
2. Bu olay para/borc/alacak hareketi mi?
   - Evetse `CARI_HESAP_HAREKETLERI`.
3. Bu olay kasa/POS kaynakli mi?
   - Furpa `PosFaturas`, `PosFaturaSatirs`, `PosFaturaOdemes` ve Mikro'da `sth_pos_satis`, `cha_pos_hareketi`.
4. Bu olay iade mi?
   - `sth_normal_iade` veya `cha_normal_Iade`.
5. Hangi belgeye ait?
   - `evrakno_seri + evrakno_sira`.

Bu mantik oturunca satis, alis, iade, tahsilat, tediye, gun sonu ve cari
mutabakat ayni harita uzerinden okunur.

## 23. Cari Hareket Tip/Cins Okuma Tablosu

Bu bolum `CARI_HESAP_HAREKETLERI` tablosunu okurken en cok kafa karistiran
alanlari aciklar:

```text
cha_evrak_tip
cha_tip
cha_cinsi
cha_normal_Iade
```

Onemli not:

```text
Mikro'da tek basina cha_tip veya cha_cinsi okumak yeterli degildir.
Anlam genelde cha_evrak_tip + cha_tip + cha_cinsi + cha_normal_Iade
kombinasyonundan cikar.
```

Asagidaki tablo canli veride gorulen kullanimlara gore hazirlanmistir. Mikro'nun
ic sabitleri sirket kurulumuna ve belge tiplerine gore farkli yorumlanabilir;
bu yuzden yeni bir kombinasyon gorulurse mutlaka ornek evrak seri/sira ile
stok ve cari taraf birlikte incelenmelidir.

### 23.1 `cha_tip` genel yonu

| `cha_tip` | Genel okuma | Not |
|---|---|---|
| `0` | Cari hareketin bir yonu; satis/fatura/borclandirma tarafinda cok gorulur | Tek basina "borc" diye ezberlenmemeli; `cha_cinsi` ve `cha_evrak_tip` ile okunur |
| `1` | Cari hareketin ters yonu; tahsilat, tediye, virman veya karsi hareketlerde cok gorulur | Kasa/banka cikisi veya cari kapama hareketlerinde sik gorulur |

Pratik kural:

```text
cha_tip = hareket yonu
cha_cinsi = hareketin turu
cha_evrak_tip = belge/fiş tipi
cha_normal_Iade = normal mi iade mi
```

### 23.2 En cok gorulen cari hareket kombinasyonlari

| `cha_evrak_tip` | `cha_tip` | `cha_cinsi` | `cha_normal_Iade` | Canli sistemdeki pratik anlam | Stok hareketiyle bagli mi? | Nasil baglanir? |
|---:|---:|---:|---:|---|---|---|
| `63` | `0` | `7` | `0` | POS/perakende satis veya satis faturasi cari hareketi | Evet | Ayni `evrakno_seri/sira`; stokta genelde `sth_evraktip=4`, `sth_tip=1`, `sth_cins=1`, `sth_pos_satis=1` |
| `61` | `1` | `26` | `0` | Kasa/POS kaynakli gider pusulasi veya Z raporu benzeri ozel POS belge hareketi | Evet | Ayni `PS+Sube` seriyle stokta genelde `sth_evraktip=16`, `sth_tip=0`, `sth_cins=17` |
| `63` | `0` | `6` | `1` | Iade cari hareketi; musteri iadesi/gider pusulasi gibi ters kayit | Evet | Ayni seri/sira ile stokta `sth_normal_iade=1` veya ters yonlu stok hareketi aranir |
| `63` | `0` | `6` | `0` | Normal fatura/cari hareketi; firma satis/alis senaryosuna gore yorumlanir | Genelde evet | Ayni seri/sira stokta varsa fatura stoklu belgedir; yoksa sadece cari/hizmet olabilir |
| `63` | `0` | `8` | `0` | Fatura/cari hareketinin baska alt tipi; canlida `FEF26` gibi serilerde gorulur | Belgeye gore | Ayni seri/sira stokta aranir; stok yoksa hizmet/cari belge olabilir |
| `63` | `0` | `8` | `1` | Iade isaretli fatura/cari hareket alt tipi | Belgeye gore | Ayni seri/sira stokta `normal_iade=1` aranir |
| `63` | `0` | `15` | `1` | Iade/ters cari belge alt tipi | Belgeye gore | Ayni seri/sira stokta aranir |
| `33` | `0` | `5` | `0` | Cari/kasa/banka karsi hareketlerinden biri; canlida cift yonlu kayit olarak goruluyor | Hayir/genelde yok | Stok hareketi beklenmez; cari-kasa/banka hareketi gibi okunur |
| `33` | `1` | `5` | `0` | `33/0/5` hareketinin karsi yonu gibi calisir | Hayir/genelde yok | Ayni evrak kendi cari satirlariyla birlikte okunur |
| `60` | `0` | `5` | `0` | Kasa/POS/gun sonu tahsilat dagilimi veya mahsup hareketi gibi gorulur | Hayir/genelde yok | Stoktan degil; `cha_kod`, `cha_kasa_hizkod`, aciklama ve sube kodu ile okunur |
| `60` | `1` | `5` | `0` | `60/0/5` hareketinin karsi taraf satiri gibi gorulur | Hayir/genelde yok | Ayni evrak satirlari toplanarak okunur |
| `2` | `1` | `0` | `0` | Tahsilat tipi hareket | Hayir | Para/cari hareketidir; stok hareketi beklenmez |
| `64` | `0` | `0` | `0` | Tediye/odeme cikisi tipi hareket | Hayir | Para/cari hareketidir; stok hareketi beklenmez |
| `64` | `0` | `41` | `0` | Tediye veya kasa/banka odeme alt tipi | Hayir | `cha_kasa_hizmet`, `cha_kasa_hizkod`, aciklama ve cari kod ile okunur |
| `57` | `0` | `5` | `0` | Virman/mahsup benzeri cari hareket | Hayir | Ayni evrak icindeki borc/alacak satirlari birlikte okunur |
| `57` | `1` | `5` | `0` | Virman/mahsup karsi yon satiri | Hayir | Ayni evrak icindeki karsi satirla birlikte okunur |
| `0` | `1` | `6` | `0` | Cari tahsilat/odeme/cek-senet gibi para hareketlerinde gorulen alt tip | Hayir | Stok hareketi beklenmez; cari, kasa/banka ve aciklama uzerinden okunur |
| `0` | `1` | `8` | `0` | Gider/hizmet/kasa baglantili cari hareketlerde gorulur | Hayir | `cha_kasa_hizmet=5` gibi alanlarla yorumlanir |
| `0` | `1` | `35` | `0` | Banka/EFT/odeme benzeri cari hareketlerde gorulur | Hayir | Stok yoktur; banka/kasa/cari hareketi olarak okunur |
| `8` | `0` | `0` | `0` | Cari belge/mahsup alt tipi | Hayir/genelde yok | Stok beklenmez |
| `35` | `0` | `0` | `0` | Cari belge/odeme alt tipi | Hayir/genelde yok | Stok beklenmez |
| `67` | `0` | `3` | `0` | Cari belge alt tipi; canlida az sayida gorulur | Belgeye gore | Stokla bag gerekiyorsa seri/sira aranir |

### 23.3 Stok hareketiyle bagli olan cari hareketler

Bir cari hareketin stok hareketiyle bagli olma ihtimali en yuksekse su isaretler
vardir:

| Isaret | Ne anlatir? |
|---|---|
| `cha_evrak_tip = 63` | Fatura/perakende satis/iade gibi stoklu belge olabilir |
| `cha_cinsi IN (6, 7, 8, 15)` | Canlida stoklu fatura/iade tarafinda sik gorulen cinsler |
| `cha_normal_Iade = 1` | Iade/ters hareket olma ihtimali yuksek |
| `cha_pos_hareketi = 1` | POS/kasa kaynakli hareket |
| `cha_evrakno_seri` `PS` ile basliyor | Kasa/POS aktarimindan gelmis olabilir |

Stok tarafinda karsilik ararken:

```sql
SELECT *
FROM STOK_HAREKETLERI
WHERE sth_evrakno_seri = '<cha_evrakno_seri>'
  AND sth_evrakno_sira = <cha_evrakno_sira>
ORDER BY sth_satirno;
```

Cari taraf:

```sql
SELECT *
FROM CARI_HESAP_HAREKETLERI
WHERE cha_evrakno_seri = '<seri>'
  AND cha_evrakno_sira = <sira>
ORDER BY cha_satir_no;
```

### 23.4 Stok hareketi beklenmeyen cari hareketler

Su hareketlerde normalde stok hareketi beklenmez:

| Islem | Neden stok yok? |
|---|---|
| Tahsilat | Musteri para oder; mal hareket etmez |
| Tediye | Biz para oderiz; mal hareket etmez |
| Banka/EFT hareketi | Para hareketidir |
| Kasa virmani | Kasa/banka/cari arasinda para aktarimidir |
| Mahsup | Cari hesaplar arasinda borc/alacak duzeltmesidir |

Bu tiplerde asil okunacak alanlar:

```text
cha_kod
cha_meblag
cha_kasa_hizmet
cha_kasa_hizkod
cha_aciklama
cha_tarihi
cha_evrakno_seri
cha_evrakno_sira
```

### 23.5 POS satisinda cari-stok baglantisi

Kasa satisinda tipik baglanti:

```text
CARI_HESAP_HAREKETLERI
  cha_evrakno_seri = PS + Sube
  cha_evrak_tip = 63
  cha_cinsi = 7
  cha_pos_hareketi = 1

STOK_HAREKETLERI
  sth_evrakno_seri = PS + Sube
  sth_evraktip = 4
  sth_tip = 1
  sth_cins = 1
  sth_pos_satis = 1
```

Ornek:

```text
PS149 / 28798
```

Bu belge icin:

1. Cari taraf satis toplamlarini ve KDV/cari etkisini gosterir.
2. Stok taraf urun urun hangi mallarin ciktigini gosterir.
3. Furpa tarafinda ham fis ve odeme detayi `PosFaturas`, `PosFaturaSatirs`,
   `PosFaturaOdemes` uzerinden okunur.

### 23.6 Bir kombinasyonun anlamini bilmiyorsak

Yeni veya emin olmadigin bir `cha_tip / cha_cinsi / cha_evrak_tip` kombinasyonu
gorursen su sirayla ilerle:

1. Ayni `cha_evrakno_seri + cha_evrakno_sira` icin tum cari satirlarini oku.
2. Ayni seri/sira ile `STOK_HAREKETLERI` icinde hareket var mi bak.
3. `cha_aciklama`, `cha_kasa_hizkod`, `cha_pos_hareketi`, `cha_normal_Iade`
   alanlarini kontrol et.
4. Seri prefix'ine bak:
   - `PS...`: POS/kasa satis aktarimi olabilir.
   - `FRP...`, `FEF...`, `FAF...`: fatura/iade ailesi olabilir.
   - `PST...`, `TDY...`, `THS...`: tahsilat/tediye ailesi olabilir.
5. Stok hareketi varsa bu belge stoklu faturadir; stok hareketi yoksa cari,
   kasa, banka veya mahsup hareketidir.
