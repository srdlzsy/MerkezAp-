# Mikro Stored Procedure Analizi

Bu dokuman, `MikroConnection` uzerindeki stored procedure'leri ileride tekrar bakabilmek icin ozetler.

## Onemli Not

- Bu analiz sirasinda stored procedure calistirilmadi.
- Sadece `sys.procedures`, `sys.parameters` ve `sys.sql_modules` uzerinden metadata/definition okundu.
- Mikro canli veritabaninda API tarafindan su an yazma yapilmamali.
- Stored procedure kullanilacaksa once read-only olup olmadigi ayrica dogrulanmali.
- Procedure icinde `INSERT`, `UPDATE`, `DELETE`, `DROP` gecmesi her zaman canli tabloya yazdigi anlamina gelmez; bazi Mikro procedure'leri temp tablo kullanir. Yine de whitelist olmadan API'ye alinmamali.

## Genel Durum

- Toplam procedure sayisi: `289`
- Procedure definition'lari okunabiliyor.
- Isimize yarayabilecek ana gruplar:
  - stok / barkod / fiyat arama
  - cari listeleme
  - siparis operasyon/listeleme
  - depo siparis / depo nakliye
  - stok ozet / cari ozet / siparis ozet
  - kasa / dashboard / POS raporlari

## Ilk Bakilacak Read-Only Adaylar

### `__StokveFiyatArama_Gokhan`

Amac:

- Stok, barkod ve fiyat arama icin guclu aday.
- UI tarafinda urun arama, barkod arama, fiyat gosterimi gibi ekranlarda ise yarayabilir.

Parametre izleri:

- `@sfiyat_deposirano`
- `@bar_kodu`
- `@sfiyat_stokkod`
- `@sto_isim`
- `@tedarikci`

Risk:

- Ilk taramada ana tabloya yazma komutu gorunmedi.
- Read-only adapter icin uygun aday.

### `msp_ApiStokListesi`

Amac:

- Stok listeleme icin hazir API mantigi gibi duruyor.

Parametre izleri:

- `@FieldName`
- `@WhereStr`
- `@Size`
- `@Index`
- `@Sort`

Risk:

- Dinamik filtre parametreleri var.
- Guvenli kullanmak icin ham `WhereStr` UI'dan direkt alinmamali.
- Bizim tarafta kontrollu filtre objesinden uretilmeli.

### `msp_ApiStokListesiV2`

Amac:

- Stok listeleme icin daha temiz V2 aday.

Parametre izleri:

- `@StokKod`
- `@TarihTipi`
- `@IlkTarih`
- `@SonTarih`
- `@Size`
- `@Index`
- `@Sort`

Risk:

- Read-only aday gibi duruyor.
- Stok listesi endpoint'i yaparken tercih edilebilir.

### `msp_ApiCariListesi`

Amac:

- Cari listeleme icin hazir API mantigi.

Parametre izleri:

- `@FieldName`
- `@WhereStr`
- `@Size`
- `@Index`
- `@Sort`

Risk:

- `WhereStr` nedeniyle dikkatli kullanilmali.
- UI'dan gelen ham query direkt procedure'e verilmemeli.

### `msp_ApiCariListesiV2`

Amac:

- Cari listeleme icin daha kontrollu V2 aday.

Parametre izleri:

- `@CariKod`
- `@CariVKNTCNo`
- `@TarihTipi`
- `@IlkTarih`
- `@SonTarih`
- `@Size`
- `@Index`
- `@Sort`

Risk:

- Read-only aday gibi duruyor.
- Cari arama/listeleme icin ileride kullanilabilir.

### `msp_ApiStokSatisFiyatTanimListesi`

Amac:

- Stok satis fiyat liste tanimlarini almak icin aday.
- Bizde `STOK_SATIS_FIYAT_LISTELERI` satirlari var; fiyat liste basligi/aciklamasi icin bu procedure faydali olabilir.

Parametre izleri:

- `@Size`
- `@Index`
- `@Sort`
- `@Search_text`

Risk:

- Read-only aday gibi duruyor.

## Siparis ve Depo Operasyon Adaylari

### `msp_siparis_listesi`

Amac:

- Siparis listeleme icin kullanilabilecek Mikro procedure.

Risk:

- Ilk taramada ana tablo yazma komutu gorunmedi.
- Mevcut EF sorgularimiz calistigi icin hemen gecmek sart degil.
- Performans/uyumluluk ihtiyaci olursa karsilastirma yapilabilir.

### `msp_siparis_detay_listesi`

Amac:

- Siparis detay listeleme icin aday.

Risk:

- Procedure icinde `INSERT INTO #SIPARISLER` gorundu.
- Bu temp tablo gibi duruyor, ana tabloya yazma gorunmedi.
- Yine de kullanmadan once result kolonlari ve execution plani test DB'de incelenmeli.

### `sp_SiparisOperasyonlari`

Amac:

- Firma siparis operasyonlari icin guclu aday.
- Tarih araligi, acik/kapali, tamamlanmis, onayli, depo tipi gibi filtreleri var.

Parametre izleri:

- `@siptip`
- `@ilktar`
- `@sontar`
- `@acikkapali`
- `@tamamlanmis`
- `@onayli`
- `@DepoTipi`
- `@VerilenDepo`
- `@TarihTipi`
- `@Depolarstr`
- `@BirimNo`
- `@carikodu`
- `@amac`
- `@durum`
- `@dagitim`
- `@yuvarlama`
- `@Sadece_rezervasyonlu_siparisler_fl`
- `@firmalar`

Risk:

- Ilk taramada `UPDATE #tmpSiparisOperasyonlari` gorundu.
- Temp tablo update ediyor gibi duruyor, ana tablo yazimi net gorunmedi.
- Read-only adapter icin aday olabilir ama once sonuc seti netlestirilmeli.

### `sp_DahiliSiparisOperasyonlari`

Amac:

- Depolar arasi / dahili siparis operasyonlari icin guclu aday.

Parametre izleri:

- `@ilktar`
- `@sontar`
- `@acikkapali`
- `@tamamlanmis`
- `@TarihTipi`
- `@DepoTipi`
- `@VerilenDepo`
- `@Depolarstr`
- `@durum`
- `@dagitim`
- `@yuvarlama`
- `@firmalar`

Risk:

- Temp tablo insert/update kullaniyor gibi duruyor.
- Ana tablo yazimi ilk bakista gorunmedi.
- Mevcut depo siparis EF sorgulari calistigi icin sadece performans veya farkli sonuc ihtiyacinda degerlendirilmeli.

### `SiparisListele`

Amac:

- Seri/sira ile siparis listeleme veya detay cekme icin kucuk procedure.

Parametre izleri:

- `@SERI`
- `@SIRA`

Risk:

- Ilk taramada yazma gorunmedi.
- Basit detail kontrolu icin incelenebilir.

### `DepoNakliyeListesi`

Amac:

- Depo nakliye/sevk listesi icin kucuk ve temiz aday.

Parametre izleri:

- `@DEPONO`

Risk:

- Ilk taramada yazma gorunmedi.
- Sevk listesiyle karsilastirmak icin iyi aday.

### `DepoSiparisListesiBolumGruplu`

Amac:

- Depo siparis kalemlerini bolum/grup mantigiyla listelemek icin aday.

Parametre izleri:

- `@Seri`
- `@Sira`
- `@Bolum`

Risk:

- Ilk taramada yazma gorunmedi.
- UI detay ekraninda kategori/bolum gruplama istenirse bakilabilir.

### `DepoSiparisListesiBolumGrupluModelKod`

Amac:

- Depo siparis kalemlerini seri/sira bazli model kod mantigiyla almak icin aday.

Parametre izleri:

- `@Seri`
- `@Sira`

Risk:

- Ilk taramada yazma gorunmedi.

### `DepoOnerilenSiparisler`

Amac:

- Onerilen depo siparisi uretmek/listelemek icin aday.

Parametre izleri:

- `@CIKIS_DEPO_NO`
- `@DEPO_NO`
- `@STOK_KODU`

Risk:

- Ilk taramada yazma gorunmedi.
- Otomatik siparis onerisi ekrani gelirse degerli.

### `DepoOnerilenSiparislerFurpa`

Amac:

- Onerilen depo siparisi icin Furpa ozel versiyon gibi duruyor.

Parametre izleri:

- `@sube`
- `@depo`
- `@STOK_KODU`

Risk:

- Ilk taramada yazma gorunmedi.

### `DepoOnerilenSiparislerGokhan`

Amac:

- Onerilen depo siparisi icin baska ozel versiyon.

Parametre izleri:

- `@sube`
- `@depo`
- `@STOK_KODU`

Risk:

- Ilk taramada yazma gorunmedi.

## Ozet / Dashboard Adaylari

### `msp_Ozetten_Stok_Ozet_Oku`

Amac:

- Stok ozet okumak icin aday.

Parametre izleri:

- `@StokKodu`
- `@MaliYil`

Risk:

- Ilk taramada yazma gorunmedi.
- Stok durum/ozet ekranlarinda kullanilabilir.

### `msp_Ozetten_Siparis_Ozet_Oku`

Amac:

- Siparis ozet okumak icin aday.

Parametre izleri:

- `@HareketTip`
- `@HareketKodu`
- `@Normal_Proforma`
- `@Depono`
- `@SonTarih`

Risk:

- Ilk taramada yazma gorunmedi.

### `msp_Ozetten_Cari_Ozet_Oku`

Amac:

- Cari ozet/bakiye okumak icin aday.

Parametre izleri:

- `@FIRMALAR`
- `@CARICINSI`
- `@CARIKODU`
- `@MALIYIL`
- `@SORMERKKODU`
- `@PROJEKODU`
- `@GRUPNO`

Risk:

- Ilk taramada yazma gorunmedi.
- Cari bakiye/dash ekranlarinda kullanilabilir.

### `sp_Dashboard_Stok_Hareket_Bilgi_Mobile`

Amac:

- Mobil dashboard icin stok hareket bilgisi.

Parametre izleri:

- `@firmalar`
- `@stok_kod`
- `@mali_yil`
- `@depo_detay_fl`

Risk:

- Ilk taramada yazma gorunmedi.

### `sp_Dashboard_Stok_Hareket_Foyu_Mobile`

Amac:

- Stok hareket foyu / hareket listesi.

Risk:

- Temp tablo insert/update kullaniyor gibi duruyor.
- Ana tablo yazimi ilk bakista gorunmedi.

### `sp_Dashboard_Cari_Hareket_Foyu_Mobile`

Amac:

- Cari hareket foyu.

Risk:

- Temp tablo insert/update kullaniyor gibi duruyor.
- Ana tablo yazimi ilk bakista gorunmedi.

### `sp_Dashboard_Cari_Ekstre_Listesi_Mobile`

Amac:

- Cari ekstre listesi.

Risk:

- Temp tablo olusturma/drop izleri var.
- Rapor amacli olabilir ama ayrica test edilmeli.

## Kasa / POS Adaylari

### `msp_POS_Odemeleri`

Amac:

- POS odeme raporlari icin aday.

Risk:

- Temp tablo `#POS_ODEMELERI` insert/update/drop kullaniyor.
- Canli ana tabloya yazma ilk bakista gorunmedi.
- Kasa modulu gelince detayli test edilmeli.

### `msp_KasiyerCiro`

Amac:

- Kasiyer ciro raporu icin aday.

Risk:

- Temp tablo `#KASIYERCIRO` insert/update/drop kullaniyor.
- Kasa raporlari icin incelenebilir.

### `sp_Dashboard_Kasa_Bakiye_Detay2_Mobile`

Amac:

- Kasa bakiye detay raporu.

Parametre izleri:

- `@firmalar`
- `@kod`
- `@doviz_no`
- `@ilktarih`
- `@sontarih`
- `@size`
- `@index`

Risk:

- Temp tablo insert/update kullaniyor.
- Ana tablo yazimi ilk bakista gorunmedi.

### `sp_Dashboard_Toplam_Kasa_Bakiye_Mobile`

Amac:

- Toplam kasa bakiye raporu.

Parametre izleri:

- `@firmalar`
- `@tarih`
- `@TLDolarEuro_fl`

Risk:

- Ilk taramada yazma gorunmedi.

## Kesinlikle Calistirmayalim / Yazma Riski Yuksek

### `SiparisTeslim`

Risk:

- `INSERT INTO STOK_HAREKETLERI`
- `UPDATE SIPARISLER`

Karar:

- API tarafinda su an kesinlikle kullanilmaz.
- Canli siparis teslim / sevk yazma sureci tasarlanana kadar dokunulmaz.

### `DepoNakliyeTeslim`

Risk:

- `UPDATE STOK_HAREKETLERI`

Karar:

- API tarafinda su an kesinlikle kullanilmaz.

### `DepoDetaylariIcin_Kayitlar_Olustur`

Risk:

- `DELETE STOK_SATIS_FIYAT_LISTELERI`
- `INSERT INTO STOK_SATIS_FIYAT_LISTELERI`
- `DELETE STOK_DEPO_DETAYLARI`
- `INSERT INTO STOK_DEPO_DETAYLARI`

Karar:

- Canli veride cok riskli.
- API tarafinda kullanilmaz.

### `msp_DepoDurum`

Risk:

- `DROP TABLE dbo.DEPODURUM`
- `INSERT INTO dbo.DEPODURUM`

Karar:

- Rapor amacli olsa bile canli API'den calistirmak dogru degil.
- Depo durum lazimsa mevcut `DEPODURUM` tablosu veya kendi SELECT sorgumuz tercih edilmeli.

### Trigger Enable/Disable Procedure'leri

Ornekler:

- `Disable_ALL_Triggers`
- `Enable_ALL_Triggers`
- `Disable_SIPARISLER_Triggers`
- `Enable_SIPARISLER_Triggers`
- `Disable_STOK_HAREKETLERI_Triggers`
- `Enable_STOK_HAREKETLERI_Triggers`

Karar:

- API kapsaminda degil.
- Kesinlikle calistirilmamali.

## Dikkatli Incelenecek Operasyon Procedure'leri

### `sp_MagazaStokYonetimi`

Amac:

- Magaza stok yonetimi icin kapsamli procedure.

Risk:

- Temp tablo kullaniyor gibi duruyor.
- Cok genis ve karmasik.
- Hemen API'ye almak yerine once result set ve is kurali analiz edilmeli.

### `sp_MagazaStokDagitim`

Amac:

- Magaza stok dagitim hesaplama.

Risk:

- Temp tablo insert/update kullaniyor gibi duruyor.
- Eger sadece hesaplama sonucu donduruyorsa ileride kullanilabilir.

### `sp_MagazalarArasiStokDagitim`

Amac:

- Magazalar arasi stok dagitim hesaplama.

Risk:

- Temp tablo uzerinde insert/update/delete izleri var.
- Ana tablo yazimi netlestirilmeden kullanilmamali.

### `msp_Stok_DepoDetaylari_Operasyonu`

Amac:

- Stok depo detaylari operasyonu.

Risk:

- Ilk taramada yazma komutu gorunmedi.
- Fakat adi operasyon oldugu icin dikkatli incelenmeli.

## Onerilen Kullanim Stratejisi

1. Mevcut EF Core SELECT sorgularini bozmadan devam edelim.
2. Procedure kullanilacaksa once sadece whitelist'e alinmis read-only procedure'ler kullanilsin.
3. Whitelist disi procedure calistirilmasin.
4. Dinamik SQL iceren procedure'lerde UI'dan gelen metin direkt parametreye verilmesin.
5. Her procedure icin once test DB'de result schema cikarilsin.
6. Result schema sabitlenmeden DTO yazilmasin.
7. Yazma yapan procedure'ler icin ayri onay, ayri modul ve ayri audit/log mantigi kurulmadan canliya dokunulmasin.

## Ilk Adapter Adaylari

Bu procedure'ler icin ileride `Infrastructure/Mikro/StoredProcedures` gibi bir adapter katmani acilabilir:

- `__StokveFiyatArama_Gokhan`
- `msp_ApiStokListesiV2`
- `msp_ApiCariListesiV2`
- `msp_ApiStokSatisFiyatTanimListesi`
- `msp_siparis_listesi`
- `msp_siparis_detay_listesi`
- `sp_SiparisOperasyonlari`
- `sp_DahiliSiparisOperasyonlari`
- `DepoNakliyeListesi`
- `DepoSiparisListesiBolumGruplu`

## Simdilik Kullanmayalim Listesi

- `SiparisTeslim`
- `DepoNakliyeTeslim`
- `DepoDetaylariIcin_Kayitlar_Olustur`
- `msp_DepoDurum`
- `Disable_*`
- `Enable_*`

