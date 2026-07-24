# Rapor Modulu Envanteri

Kaynak proje: `Depo Stok Listeleme`

Bu belge, mevcut WinForms projesindeki rapor ve operasyon ekranlarini yeni API yapisina tasimak icin hazirlanmis ayrintili envanterdir. Amac sadece ekran adlarini listelemek degil; her ekranin hangi veritabanina dokundugunu, hangi tablo/fonksiyonlardan beslendigini, hangi parametrelerle calistigini, hangi ciktiyi urettigini ve yeni API'de hangi sinira tasinmasi gerektigini aciklamaktir.

Not: Kodda DB kullanici/sifre ve SMTP bilgileri hard-coded halde bulunuyor. Bu belgeye hassas degerler alinmadi. Yeni API'de connection string, SMTP credential ve benzeri gizli bilgiler mutlaka config/secret manager uzerinden gelmeli.

## Kapsam

Bu dosya su sorulara cevap verir:

- Hangi WinForms ekranlari gercek rapordur?
- Hangi ekranlar veri degistirdigi icin command/admin operasyonudur?
- Hangi veritabanlari, tablolar, view'ler ve fonksiyonlar kullaniliyor?
- Ortak sorgu kaliplari nelerdir?
- Yeni API'de hangi endpoint tasarimi daha dogru olur?
- Tasinirken hangi guvenlik, transaction, audit ve performans riskleri temizlenmelidir?

Bu belge tam SQL metinlerini kopyalamaz. SQL metinleri ilgili `.cs` dosyalarinda kalir. Burada her sorgunun is mantigi, kaynak tablolari, filtreleri ve API'ye tasinirken korunmasi gereken davranis yazilir. Tam SQL cikarmak gerekirse ilgili dosyada `SqlCommand`, `SqlDataAdapter`, `SELECT`, `INSERT`, `UPDATE`, `DELETE` bloklari taranmalidir.

## DB Haritasi

### `MikroDB_V16_FURPA_2024`

Ana Mikro ERP veritabanidir. Raporlarin buyuk cogunlugu bu DB'ye baglidir.

Kullanim alanlari:

- Stok kartlari: `STOKLAR`
- Stok hareketleri: `STOK_HAREKETLERI`
- Depo/sube tanimlari: `DEPOLAR`
- Cari hesaplar: `CARI_HESAPLAR`
- Satin alma sartlari: `SATINALMA_SARTLARI`
- Siparisler: `SIPARISLER`, `DEPOLAR_ARASI_SIPARISLER`
- Sayim: `SAYIM_SONUCLARI`
- Stok depo yetkileri/detaylari: `STOK_DEPO_DETAYLARI`
- Kategori, reyon, uretici, personel lookup tablolari

API tasima notu: Bu DB read-heavy raporlar icin ana kaynaktir. Insert/update/delete yapan ekranlar ayrica transaction ve audit ile ele alinmalidir.

### `Furpa`

POS, bulten, dagilim ve fiyat teklifi gibi uygulama/veri toplama tarafini tutar.

Kullanim alanlari:

- POS fisleri: `PosFaturas`, `PosFaturaSatirs`, `PosFaturaOdemes`, `Cashiers`
- Bulten: `BULTEN_TANIMLARI`, `BULTEN_URUN_TANIMLARI`, `BULTEN_SUBE_TANIMLARI`
- Dagilim: `STOK_DAGILIM`, `Bolge_Yoneticileri`
- Fiyat teklifi: `Fiyat_Teklifi_Firma`, `Fiyat_Teklifi_Urunler`

API tasima notu: `Furpa` hem rapor hem command tarafina hizmet ediyor. POS okumalar rapordur; bulten/dagilim/fiyat teklifi kayit islemleri command modulu olmalidir.

### `PROMOSYONLAR`

Fis kopyasi ve program bilgisi gibi yardimci rapor verilerinde kullaniliyor.

API tasima notu: Fis yazdirma veya fis kopyasi endpointlerinde yardimci lookup olarak kalmali.

### `AxataWM_CANLI`

Axata WMS stok ve entegrasyon tablolari icin kullaniliyor.

Kullanim alanlari:

- WMS stok karsilastirma
- Axata'dan Mikro'ya tekrar gonderim operasyonu
- Depoda var/subede yok ve depoda yoka dusen stok raporlari

API tasima notu: WMS read raporlari `reports/inventory` altina gidebilir. Entegrasyon status update islemi command/admin modulu olmalidir.

### `MikroDB_V16`

Bazi sorgularda kur/para birimi isimleri icin 3-part name ile okunuyor.

API tasima notu: Bu DB ana veri kaynagi degil, yardimci lookup kaynagi olarak ele alinmali.

## Ortak Domain Sozlugu

- `depoNo`: Mikro `DEPOLAR.dep_no`. Kodda depo ve sube ayrimi genelde `dep_no > 100` subeler, `50/53/56` dagitim depolari gibi kullaniliyor.
- `stokKod`: Mikro `STOKLAR.sto_kod`.
- `cariKod`: Mikro `CARI_HESAPLAR.cari_kod`.
- `baslangic`, `bitis`: Tarih aralikli rapor parametreleri. API'de `DateOnly` veya `DateTime` olarak alinmali.
- `tarih`: Son stok veya tek gun bazli rapor tarihi.
- `filterType`: Ayni rapor cekirdegini stok, uretici, kategori, reyon, ambalaj veya satin alma sorumlusu gibi farkli filtrelerle calistirmak icin kullanilacak enum.
- `filterValue`: `filterType` ile uyumlu kod/deger.
- `includeDls`: DLS urunlerinin dahil edilip edilmeyecegini belirleyen boolean.

## Ortak Sorgu Kaliplari

Bu kaliplar birden fazla ekranda kopyalanmis. API'de ortak repository/service haline getirilmeli.

### `son_stok`

`STOK_HAREKETLERI` hareketlerinden stok/depo/tarih bazli kalan miktar hesabi uretir.

Tipik mantik:

- Girdi: `stokKod`, `depoNo`, `tarih`
- Kaynak: `STOK_HAREKETLERI`
- Hareketin giris/cikis yonu `sth_tip` ile ayrilir.
- `sth_cins in (9,15)` gibi istenmeyen hareket tipleri dislanir.
- Tarih filtresi rapor tarihine kadar calisir.
- Cikti: stok kodu, depo, kalan miktar, maliyet/satis degeri hesaplarina temel miktar.

Tasima notu: Bu hesap birden fazla ekranda tekrarlandigi icin tek bir SQL view, stored procedure veya reusable query builder haline getirilmeli.

### `depo_stok`

Mikro fonksiyonu ile depo miktari alir.

Kullanim:

```sql
dbo.fn_depodakiMiktar(sto_kod, dep_no, @tarih)
```

Tipik cikti: belirli stokun belirli depo/subedeki miktari.

Tasima notu: Raporlarin tutarliligi icin bu fonksiyon kullanan endpointler ayni tarih parametre semantigini kullanmali.

### `satis_araligi`

`STOK_HAREKETLERI` icinde satis hareketlerini toplar.

Tipik filtre:

```sql
sth_tip = 1
and sth_cins = 1
and sth_evraktip in (1, 4)
and sth_normal_iade = 0
and sth_tarih between @baslangic and @bitis
```

Tipik cikti:

- Miktar
- Tutar
- Sube/depo bazli satis
- Stok, kategori, uretici veya satin alma sorumlusu bazli satis

Tasima notu: Tarih aralikli satis raporlari, karlilik, birlikte satildi ve dagilim onerisi bu kaliba yaslanir.

### `giris_araligi`

Alis/giris hareketlerini toplar.

Tipik filtre:

```sql
sth_tip = 0
and sth_cins = 0
and sth_evraktip in (3, 13)
```

Tasima notu: Giris-cikis karsilastirma, DLS ve maliyet hesaplarinda ortaklasabilir.

### `iade_araligi`

Iade hareketlerini getirir. Ekrana gore `sth_normal_iade=1`, iade evrak tipi veya transfer/iade deposu filtresi kullanilir.

Tasima notu: Iade raporlari tek bir returns repository altinda toplanmali, ancak satis iadesi ve depoya iade ayrimi korunmali.

### `sas_fiyat`

`SATINALMA_SARTLARI` icinden stok/tarih bazli son net alis fiyati alir. Bazi ekranlarda KDV dahil fiyat kullaniliyor.

Tasima notu: Maliyet ve karlilik hesaplarinda ayni fiyat kaynagi kullanilmali. Eski kodda birden fazla maliyet modu var; API parametresi olarak `costMode=sas|avgPurchase` korunmali.

### `raf_fiyat`

Raf/satis fiyati icin `fn_StokSatisFiyati` veya `STOK_SATIS_FIYATLARI_F1_D0_VIEW` kullaniliyor.

Tasima notu: Endpoint ciktisinda fiyat alanlari nullable olmali; fiyat bulunamazsa 0 yerine null donmek daha temiz olabilir. Geriye uyumluluk gerekiyorsa mevcut 0 davranisi korunabilir.

### `kategori`

Kategori, segment, alt segment ve reyon raporlari icin kullanilir.

Kaynaklar:

- `STOK_KATEGORILERI`
- `VW_KATEGORILER`
- `VW_KATEGORILER_UZN`
- `fn_KategoriIsmi`
- Reyon/ambalaj/model/uretici alanlari

Tasima notu: API'de kategori agaci icin ayrica lookup endpointleri gerekebilir.

### `lookup`

Ekranlarda arama/listbox/detay secimi icin kullanilan ortak kaynaklar:

- `STOKLAR`
- `BARKOD_TANIMLARI`
- `CARI_HESAPLAR`
- `DEPOLAR`
- `STOK_URETICILERI`
- `CARI_PERSONEL_TANIMLARI`

Tasima notu: WinForms tarafinda lookup sorgulari cogunlukla string birlestirme ile yazilmis. API'de hepsi parametreli, sayfalanabilir ve limitli olmali.

## Rapor Olarak Tasinacak Moduller

### 1. `FrmSonStok.cs` - Sube/depo genel son stok

- Tur: Read-only rapor.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`, `STOKLAR`, `DEPOLAR`, `SATINALMA_SARTLARI`, satis fiyat kaynaklari.
- Mevcut is: Secilen depo ve tarihte stoklarin kalan miktarini, alis fiyatini, satis fiyatini, toplam maliyetini, toplam satis degerini ve kar yuzdesini hesaplar.
- Girdiler: `depoNo`, `tarih`.
- Cikti alanlari: stok kodu, stok adi, kategori/uretici bilgisi, miktar, alis fiyat, satis fiyat, alis toplam, satis toplam, kar yuzdesi.
- Onerilen API: `GET /reports/stocks/by-depot?depoNo&tarih`.
- Tasima notu: `son_stok`, `sas_fiyat` ve `raf_fiyat` ortak servisleri kullanilmali.

### 2. `FrnUrun.cs` - Urun bazli tum subeler son stok

- Tur: Read-only rapor + lookup.
- DB: `MikroDB_V16_FURPA_2024`, yardimci olarak `MikroDB_V16`.
- Ana kaynaklar: `STOKLAR x DEPOLAR`, `fn_depodakiMiktar`, `SATINALMA_SARTLARI`, satis fiyat view, kur isimleri.
- Mevcut is: Secilen stokun tum sube ve depolardaki miktarini listeler. Stok adi/kodu/barkod arama davranisi bulunur.
- Girdiler: `stokKod` veya `stokAdi`, `tarih`.
- Cikti alanlari: depo no, depo adi, miktar, alis fiyat, satis fiyat, toplam degerler, kur bilgisi.
- Onerilen API: `GET /reports/stocks/by-product?stokKod|stokAdi&tarih`.
- Tasima notu: Urun arama lookup endpointi ayrilmali: `GET /lookups/stocks?query=`.

### 3. `FrmCari.cs` - Cari/tedarikci bazli son stok

- Tur: Read-only rapor + lookup.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`, `STOKLAR`, `SATINALMA_SARTLARI`, `CARI_HESAPLAR`.
- Mevcut is: Secilen tedarikciye ait urunlerin belirli depo/tarih icin son stok durumunu getirir.
- Girdiler: `cariKod`, `depoNo`, `tarih`.
- Cikti alanlari: cari bilgisi, stok kodu/adi, miktar, alis/satis fiyat, toplamlar, kar.
- Onerilen API: `GET /reports/stocks/by-supplier?cariKod&depoNo&tarih`.
- Tasima notu: Cari secim aramasi rapordan ayrilip lookup olmalidir.

### 4. `FrmKategori.cs` - Kategori bazli son stok

- Tur: Read-only rapor + kategori lookup.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`, `STOKLAR`, `STOK_KATEGORILERI`, `VW_KATEGORILER*`.
- Mevcut is: Kategori, segment veya alt segment bazli stok raporu uretir.
- Girdiler: `kategoriKod`, `depoNo`, `tarih`.
- Cikti alanlari: kategori bilgileri, stok bilgileri, miktar, fiyat ve toplamlar.
- Onerilen API: `GET /reports/stocks/by-category?kategoriKod&depoNo&tarih`.
- Tasima notu: Kategori agaci lookup endpointi ayrica tasarlanmali.

### 5. `FrmUreticiSonStok.cs` - Uretici bazli son stok

- Tur: Read-only rapor.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`, `STOKLAR`, `STOK_URETICILERI`.
- Mevcut is: Ureticiye ait urunlerin sube/depo stoklarini ve fiyat/kar bilgilerini listeler.
- Girdiler: `ureticiKod`, `tarih`.
- Cikti alanlari: uretici, stok, depo/sube, miktar, fiyat, toplam, kar.
- Onerilen API: `GET /reports/stocks/by-producer?ureticiKod&tarih`.
- Tasima notu: `FrmSonStok`, `FrmCari`, `FrmKategori` ile ayni son stok cekirdegi kullanilmali.

### 6. `Detay.cs` - Stok depo detay

- Tur: Read-only detay raporu.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOKLAR x DEPOLAR`, `fn_depodakiMiktar`.
- Mevcut is: Secilen stok icin depolara gore sifir olmayan miktarlari gosterir.
- Girdiler: `stokKod`, `tarih`.
- Cikti alanlari: depo no, depo adi, miktar.
- Onerilen API: `GET /reports/stocks/product-depot-detail?stokKod&tarih`.
- Tasima notu: Bu endpoint baska ekranlardan drill-down olarak da kullanilabilir.

### 7. `DetaySubeSatis.cs` - Stok sube satis detay

- Tur: Read-only satis detay raporu.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`, `STOKLAR`, `DEPOLAR`.
- Mevcut is: Secilen stokun tarih araliginda sube bazli satis miktar/tutar detayini getirir.
- Girdiler: `stokKod`, `baslangic`, `bitis`.
- Cikti alanlari: sube, miktar, tutar, tarih araligi toplam bilgisi.
- Onerilen API: `GET /reports/sales/product-branch-detail?stokKod&baslangic&bitis`.
- Tasima notu: `satis_araligi` kalibi ile ortaklastirilmali.

### 8. `SubelerEnvanterDurumu.cs` - Sube/depo envanter degeri

- Tur: Read-only, uzun calisan rapor.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOKLAR x DEPOLAR`, `fn_depodakiMiktar`, `SATINALMA_SARTLARI`, satis fiyatlari.
- Mevcut is: Sube veya depo bazli stok miktari, alis degeri ve satis degeri hesaplar.
- Girdiler: `tarih`, `type=sube|depo`.
- Cikti alanlari: lokasyon, stok sayisi, miktar, toplam alis degeri, toplam satis degeri.
- Onerilen API: `GET /reports/inventory/value-by-location?tarih&type=sube|depo`.
- Tasima notu: Uzun surdugu icin async job, cache veya materialized summary dusunulmeli.

### 9. `FrmBirlikteNeSatildi.cs` - Birlikte satilan urunler

- Tur: Read-only POS/satis analizi.
- DB: `Furpa`, `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `Furpa.dbo.PosFaturaSatirs`, `STOKLAR`, `STOK_HAREKETLERI`.
- Mevcut is: Secilen urunun bulundugu fisleri bulur, ayni fislerde en cok satilan ilk 20 urunu hesaplar. Miktar, tutar, tahmini alis maliyeti ve kar hesaplari vardir.
- Girdiler: `stokKod`, `baslangic`, `bitis`.
- Cikti alanlari: birlikte satilan stok kodu/adi, miktar, net tutar, alis toplam, kar yuzdesi, fis sayisi gibi ozetler.
- Onerilen API: `GET /reports/sales/co-purchased?stokKod&baslangic&bitis`.
- Tasima notu: POS ve Mikro arasindaki stok kodu eslesmesi korunmali. Maliyet hesabi icin son alis veya ortalama alis stratejisi netlestirilmeli.

### 10. `BirlikteNeSatildi/FrmFisDetay.cs` - POS fis detayi

- Tur: Read-only detay raporu.
- DB: `Furpa`, `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `PosFaturas`, `PosFaturaSatirs`, `PosFaturaOdemes`, `Cashiers`, `DEPOLAR`, `STOKLAR`.
- Mevcut is: Secilen fis GUID icin fis baslik bilgisi, kasa/kasiyer/sube bilgisi ve satir detaylarini getirir.
- Girdiler: `fisGuid`.
- Cikti alanlari: fis baslik, sube, kasa, odeme, stok satirlari, miktar, tutar.
- Onerilen API: `GET /reports/receipts/{fisGuid}`.
- Tasima notu: Fis baslik ve satirlar tek DTO icinde nested donmeli.

### 11. `FrmFisKopyasi.cs`, `XtraReport1.cs` - Fis arama ve yazdirma verisi

- Tur: Read-only arama + print-data.
- DB: `Furpa`, `MikroDB_V16_FURPA_2024`, `PROMOSYONLAR`.
- Ana kaynaklar: POS fatura tablolar, kasa, sube ve program bilgileri.
- Mevcut is: Sube/tarih/kasa/fis no ile fis arar; fis kopyasi ve yazdirma icin gerekli veri setini uretir.
- Girdiler: `subeNo`, `tarih`, `kasaNo`, `fisNo` veya `fisGuid`.
- Cikti alanlari: fis baslik, fis satirlari, odeme bilgisi, yazdirma metadatasi.
- Onerilen API: `GET /reports/receipts/search`, `GET /reports/receipts/{fisGuid}/print-data`.
- Tasima notu: `XtraReport1` UI/print layout sorumlulugudur; API sadece veri uretmelidir.

### 12. `BultanPerformans/FrmBultenPerformans.cs` - Bulten performansi

- Tur: Read-only performans raporu.
- DB: `Furpa`, `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `BULTEN_*`, `STOK_HAREKETLERI`, `SATINALMA_SARTLARI`, `STOKLAR`.
- Mevcut is: Secilen bultendeki urunlerin guncel satis, gecen ay satis, gecen yil satis, raf fiyat, bulten fiyat, maliyet, indirim ve kar yuzdelerini hesaplar.
- Girdiler: `bultenId`.
- Cikti alanlari: bulten bilgisi, stok, raf fiyat, bulten fiyat, maliyet, satis miktar/tutar, kar/indirim yuzdeleri.
- Onerilen API: `GET /reports/promotions/performance?bultenId`.
- Tasima notu: Bulten tarih araligi ve sube kapsami bulten tanimindan okunmali; rapor parametresi bulten ID ile sinirli kalabilir.

### 13. `BultanPerformans/FrmKayitlar.cs` - Bulten kayit listesi

- Tur: Read-only lookup/list.
- DB: `Furpa`.
- Ana kaynaklar: `BULTEN_TANIMLARI`.
- Mevcut is: Kayitli bultenleri listeler.
- Girdiler: opsiyonel arama/tarih filtresi.
- Cikti alanlari: bulten ID, ad, baslangic, bitis, hedef.
- Onerilen API: `GET /reports/promotions` veya `GET /promotions`.
- Tasima notu: Bu endpoint rapor secim lookup'i olarak da kullanilabilir.

### 14. `FrmCariAnaliz.cs` - Cari borc/alacak analizi

- Tur: Read-only finans/cari raporu.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `CARI_HESAPLAR_CHOOSE_3AA`, `CARI_HESAPLAR`, `CARI_PERSONEL_TANIMLARI`.
- Mevcut is: Cari hesaplari pozitif veya negatif bakiye filtresi ile listeler.
- Girdiler: `type=borc|alacak`.
- Cikti alanlari: cari kod, cari unvan, personel, bakiye.
- Onerilen API: `GET /reports/current-accounts/balance?type=borc|alacak`.
- Tasima notu: Finansal rapor oldugu icin yetki kontrolu ayrica ele alinmali.

### 15. `StokSevkYeri/StokSevkYeri.cs` - Stok sevk yeri

- Tur: Read-only stok/siparis siniflandirma raporu.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOKLAR`, `SATINALMA_SARTLARI`, `SIPARISLER`, `DEPOLAR_ARASI_SIPARISLER`.
- Mevcut is: Cari bazli urunlerin sevk tipini siniflandirir. Depo urunu, firma urunu, DLS kapali gibi durumlar ve onceki gun siparis toplamlarini gosterir.
- Girdiler: `cariKod`.
- Cikti alanlari: stok kodu, stok adi, sevk tipi, depo siparis, firma siparis.
- Onerilen API: `GET /reports/stocks/shipment-source?cariKod`.
- Tasima notu: Cari arama lookup'i ayrilmali.

### 16. `StokSevkYeri/FrmDepoStokGoster.cs` - Sevk yeri icinden depo stok detayi

- Tur: Read-only drill-down.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOKLAR x DEPOLAR`, `fn_depodakiMiktar`, fiyat kaynaklari.
- Mevcut is: Secilen stok icin sube/depo stok detayini fiyat ve toplam degerlerle gosterir.
- Girdiler: `stokKod`, `tarih`.
- Cikti alanlari: depo, miktar, alis fiyat, satis fiyat, toplam alis/satis, kar.
- Onerilen API: `GET /reports/stocks/product-depot-detail?stokKod&tarih`.
- Tasima notu: `Detay.cs` ile ayni endpoint veya ayni servis kullanilmali.

### 17. `FrmGirisCikis.cs` - Giris cikis karsilastirma

- Tur: Read-only hareket raporu.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`, `STOKLAR`, kategori/reyon/ambalaj/uretici lookup.
- Mevcut is: Alis girisleri ve satis cikislarini miktar/tutar olarak karsilastirir. Filtre tipi ekrandaki secime gore degisir.
- Girdiler: `filterType`, `filterValue`, `baslangic`, `bitis`.
- Cikti alanlari: stok, giris miktar/tutar, cikis miktar/tutar, farklar.
- Onerilen API: `GET /reports/movements/in-out?filterType&filterValue&baslangic&bitis`.
- Tasima notu: Dosyada cok sayida benzer SQL parcasi var. API'de tek endpoint + parametreli filtre modeli tercih edilmeli.

### 18. `FrmAxataMikro.cs` - Axata Mikro stok karsilastirma

- Tur: Read-only entegrasyon/stok fark raporu.
- DB: `MikroDB_V16_FURPA_2024`, `AxataWM_CANLI`.
- Ana kaynaklar: `STOK_HAREKETLERI`, `AxataWM_CANLI.dbo.vw_WMS_Stock_WH`, `STOKLAR`.
- Mevcut is: Mikro depo stogu ile Axata WMS stokunu karsilastirir. Sadece farkli veya non-zero kayitlari gosterir.
- Girdiler: `depoNo`, `tarih`.
- Cikti alanlari: stok, Mikro miktar, Axata miktar, fark.
- Onerilen API: `GET /reports/inventory/axata-mikro-diff?depoNo&tarih`.
- Tasima notu: Cross-db sorgu performansi izlenmeli.

### 19. `FrmSatisIadeKarsilastir.cs` - Satis iade karsilastirma

- Tur: Read-only satis/iade raporu.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`, `STOKLAR`, `SATINALMA_SARTLARI`, satis fiyatlari.
- Mevcut is: Tedarikci bazli urun satis ve iade miktar/tutar karsilastirmasi yapar.
- Girdiler: `cariKod`, `baslangic`, `bitis`.
- Cikti alanlari: stok, satis miktar/tutar, iade miktar/tutar, net durum.
- Onerilen API: `GET /reports/sales/return-comparison?cariKod&baslangic&bitis`.
- Tasima notu: Satis ve iade kaliplari ayri query olarak tanimlanip endpoint icinde birlestirilmeli.

### 20. `FrmIadeVerenler.cs` - Iade veren subeler

- Tur: Read-only iade detayi.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`, `fn_StokIsmi`, `fn_DepoIsmi`.
- Mevcut is: Secilen stok ve tarih araliginda hangi subelerin iade verdigini evrak ve tarih bilgisiyle listeler.
- Girdiler: `stokKod`, `baslangic`, `bitis`.
- Cikti alanlari: sube, evrak, tarih, miktar, tutar.
- Onerilen API: `GET /reports/returns/branches?stokKod&baslangic&bitis`.
- Tasima notu: Drill-down ihtiyaci icin satir seviyesinde veri donmeli.

### 21. `FrmDepodaVarSubedeYok.cs` - Depoda var subede yok

- Tur: Read-only stok firsat/eksik raporu.
- DB: `MikroDB_V16_FURPA_2024`, `AxataWM_CANLI`.
- Ana kaynaklar: Axata WMS stock view, `STOK_HAREKETLERI`, `DEPOLAR`, `STOKLAR`.
- Mevcut is: Merkez/Axata stokta var olup secili subede yok veya eksi olan urunleri listeler.
- Girdiler: `depoNo`.
- Cikti alanlari: stok, merkez/Axata miktar, sube miktar, fark/durum.
- Onerilen API: `GET /reports/inventory/warehouse-has-branch-missing?depoNo`.
- Tasima notu: Sube ve merkez depo kavramlari parametre veya config ile netlestirilmeli.

### 22. `FrmSatmayanUrunler.cs` - Satmayan urunler

- Tur: Read-only satis analizi.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOKLAR`, `STOK_HAREKETLERI`, `CARI_PERSONEL_TANIMLARI`.
- Mevcut is: Satin alma sorumlusuna gore secilen tarih araliginda hic satmayan urunleri bulur. DLS haric secenegi bulunur.
- Girdiler: `satinalmaKod`, `baslangic`, `bitis`, `includeDls`.
- Cikti alanlari: stok, satin alma sorumlusu, son satis/giris bilgisi, stok durumu.
- Onerilen API: `GET /reports/sales/not-sold?satinalmaKod&baslangic&bitis&includeDls`.
- Tasima notu: "Hic satmadi" kriteri SQL'de net korunmali; sadece satis hareketi yoklugu mu, miktar sifir mi ayrimi test edilmeli.

### 23. `StokKartDetay.cs` - Stok kart detaylari

- Tur: Read-only stok kart lookup/rapor.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOKLAR`, `BARKOD_TANIMLARI`, `STOK_REYONLARI`, `SATINALMA_SARTLARI`, `CARI_PERSONEL_TANIMLARI`.
- Mevcut is: Barkod, kategori, reyon, cari, satin alma sorumlusu veya stok kodu ile stok kartlarini filtreler.
- Girdiler: `filterType`, `filterValue`.
- Cikti alanlari: stok kodu/adi, barkod, kategori, reyon, cari, satin alma sorumlusu, fiyat/maliyet alanlari.
- Onerilen API: `GET /reports/stocks/card-details?filterType&filterValue`.
- Tasima notu: Bu ekran hem lookup hem rapor gibi kullaniliyor; API'de sayfalama zorunlu olmali.

### 24. `FrmKarlilik.cs` - Karlilik raporu

- Tur: Read-only karlilik raporu.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`, `STOKLAR`, `SATINALMA_SARTLARI`, kategori, uretici, satin alma personeli.
- Mevcut is: Uretici veya satin alma sorumlusuna gore satis, maliyet, kar TL ve kar yuzdesi hesaplar. SAS maliyeti veya hareket ortalama maliyeti modlari vardir.
- Girdiler: `scope=producer|buyer|trendyol`, `costMode=sas|avgPurchase`, `baslangic`, `bitis`, ilgili scope kodu.
- Cikti alanlari: stok, satis miktar/tutar, maliyet, kar TL, kar yuzdesi.
- Onerilen API: `GET /reports/profitability?scope=producer|buyer|trendyol&costMode=sas|avgPurchase&baslangic&bitis`.
- Tasima notu: Maliyet modu explicit olmali; ayni tarih araliginda eski sonuc ile API sonucu karsilastirilarak dogrulanmali.

### 25. `FrmDepoYokaDusenStok.cs` - Depoda yoka dusen stok

- Tur: Read-only WMS/stok raporu.
- DB: `MikroDB_V16_FURPA_2024`, `AxataWM_CANLI`.
- Ana kaynaklar: `STOKLAR`, `AxataWM_CANLI.dbo.vw_WMS_Stock_WH`.
- Mevcut is: Merkez/Axata WMS stogu sifira dusen uygun model kodlu urunleri listeler.
- Girdiler: genelde tarih veya depo sabitleri/config.
- Cikti alanlari: stok, model/kategori, WMS miktar, Mikro durum.
- Onerilen API: `GET /reports/inventory/warehouse-zero`.
- Tasima notu: "Uygun model kodu" filtresi sabit koddan config'e alinmali.

### 26. `FrmDepolaraIade.cs` - Depolara iade edilen urunler

- Tur: Read-only iade raporu.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`, `STOKLAR`, `DEPOLAR`.
- Mevcut is: Secilen iade deposuna tarih araliginda hangi subelerden hangi urunlerin iade geldigini listeler.
- Girdiler: `depoNo`, `baslangic`, `bitis`.
- Cikti alanlari: kaynak sube, hedef depo, stok, miktar, tarih/evrak.
- Onerilen API: `GET /reports/returns/to-warehouse?depoNo&baslangic&bitis`.
- Tasima notu: Iade deposu lookup'i ayrilmali.

### 27. `FrmSarkuteriDepodaYok.cs` - Sarkuteri depoda yok

- Tur: Read-only kategori/stok raporu.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOKLAR`, `fn_DepodakiMiktar`, kategori alanlari.
- Mevcut is: Sarkuteri model/kategori kodlu urunlerde depoda stogu yok veya eksi olanlari listeler.
- Girdiler: `tarih`.
- Cikti alanlari: stok, kategori/model, depo miktari.
- Onerilen API: `GET /reports/inventory/deli-warehouse-missing?tarih`.
- Tasima notu: Sarkuteri filtresi config veya lookup tablosundan okunmali.

### 28. `FrmTarihAralikliSatis.cs` - Tarih aralikli sube detayli satis

- Tur: Read-only satis raporu.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`, `STOKLAR`, `DEPOLAR`, `fn_DepodakiMiktar`.
- Mevcut is: Tarih araliginda sube detayli satis getirir. Filtreler stok, uretici veya kategoriye gore degisebilir.
- Girdiler: `filterType`, `filterValue`, `baslangic`, `bitis`.
- Cikti alanlari: sube, stok, satis miktar/tutar, mevcut stok.
- Onerilen API: `GET /reports/sales/branch-detail?filterType&filterValue&baslangic&bitis`.
- Tasima notu: `FrmGirisCikis` ve yil karsilastirma ekranlari ile ortak satis query altyapisi kullanilmali.

### 29. `FrmIkiTarihAralikliSatisKarsliastirma.cs` - Iki tarih araligi/yil karsilastirma

- Tur: Read-only satis karsilastirma raporu.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`, `STOKLAR`, kategori/reyon/uretici kaynaklari.
- Mevcut is: Secilen tarih araligi ile gecen yil ayni araligi miktar/tutar bazinda karsilastirir.
- Girdiler: `filterType`, `filterValue`, `baslangic`, `bitis`.
- Cikti alanlari: stok/kategori, bu donem miktar/tutar, onceki donem miktar/tutar, fark ve yuzde degisim.
- Onerilen API: `GET /reports/sales/year-comparison?filterType&filterValue&baslangic&bitis`.
- Tasima notu: Onceki donem tarihleri API icinde deterministik hesaplanmali veya parametre olarak alinmali.

### 30. `Siparis/FrmSiparisVerilen.cs` - Firmaya verilen siparisler

- Tur: Read-only siparis raporu.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `SIPARISLER`, `CARI_HESAPLAR`.
- Mevcut is: Secilen tarihte depo 50 icin firmaya verilen siparis evraklarini listeler.
- Girdiler: `tarih`, varsayilan `depoNo=50`.
- Cikti alanlari: evrak seri/sira, tedarikci, siparis tarihi, toplam satir/miktar.
- Onerilen API: `GET /reports/orders/supplier-orders?tarih&depoNo=50`.
- Tasima notu: Depo 50 sabiti config veya parametre olmalidir.

### 31. `Siparis/FrmSipDetay.cs` - Siparis evrak detayi

- Tur: Read-only detay raporu.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `SIPARISLER`, `STOKLAR`.
- Mevcut is: Secilen siparis evrakinin stok/miktar/tarih detaylarini getirir.
- Girdiler: `evrakSeri`, `evrakSira` veya birlesik `evrakSeriSira`.
- Cikti alanlari: stok kodu/adi, miktar, tarih, teslim bilgisi.
- Onerilen API: `GET /reports/orders/{evrakSeriSira}`.
- Tasima notu: Evrak seri/sira path yerine query parametre de olabilir; route tasarimi tutarli olmali.

### 32. `FrmSayimKarsilastir.cs` - Sayim karsilastirma

- Tur: Read-only sayim raporu.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `SAYIM_SONUCLARI`, `STOKLAR`, `DEPOLAR`, `fn_depodakiMiktar`, fiyat kaynaklari.
- Mevcut is: Sayim sonucu ile sistem stokunu karsilastirir; fark miktarini ve maliyet etkisini hesaplar.
- Girdiler: `depoNo`, `tarih`, `ambalajKod`.
- Cikti alanlari: stok, sayim miktari, sistem miktari, fark, birim maliyet, fark tutari.
- Onerilen API: `GET /reports/counting/comparison?depoNo&tarih&ambalajKod`.
- Tasima notu: Sayim raporu read-only kalmali; sayim guncelleme islemleri `FrmSayim.cs` altinda command'dir.

### 33. `FrmStokHareketleri.cs` - Stok hareketleri

- Tur: Read-only hareket listesi.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`, Mikro `fn_*` hareket/evrak/depo/cari isim fonksiyonlari.
- Mevcut is: Secilen depo ve tarih araliginda stok hareketlerini okunabilir hareket tipleriyle listeler.
- Girdiler: `depoNo`, `baslangic`, `bitis`.
- Cikti alanlari: tarih, stok, hareket tipi, evrak tipi, cari, giris/cikis depo, miktar, tutar.
- Onerilen API: `GET /reports/movements/stock?depoNo&baslangic&bitis`.
- Tasima notu: Sonuc buyuk olabilir; sayfalama ve limit zorunlu olmali.

### 34. `FrmDLST.cs` - DLS urunleri

- Tur: Read-only stok/satis raporu.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOKLAR`, `STOK_HAREKETLERI`, `SATINALMA_SARTLARI`, fiyat kaynaklari.
- Mevcut is: DLS urunleri icin son alis/satis tarihleri, miktarlari, guncel fiyatlar ve stok bilgisi uretir.
- Girdiler: DLS filtresi/config.
- Cikti alanlari: stok, son alis, son satis, miktarlar, fiyatlar, stok durumu.
- Onerilen API: `GET /reports/stocks/dls`.
- Tasima notu: DLS tanimi sabit kod yerine config/lookup olmalidir.

### 35. `FrmFiyatTeklifKayitlari.cs` - Fiyat teklif kayitlari

- Tur: Read-only liste.
- DB: `Furpa`.
- Ana kaynaklar: `Fiyat_Teklifi_Firma`, `Fiyat_Teklifi_Urunler`.
- Mevcut is: Kayitli fiyat tekliflerini listeler.
- Girdiler: opsiyonel firma, tarih veya durum filtresi.
- Cikti alanlari: teklif ID, firma, tarih, urun sayisi, toplam/ozet bilgiler.
- Onerilen API: `GET /reports/price-offers` veya `GET /price-offers`.
- Tasima notu: Fiyat teklifi CRUD tarafindan ayrilmali; bu endpoint sadece liste/okuma yapmali.

## Rapor Degil, Komut/Operasyon Olarak Ayrilmali

Bu ekranlar veri degistiriyor. Yeni API'de `reports` altina konmamalidir. Ayrica yetki, audit log, transaction ve idempotency dusunulmelidir.

### `FrmDagilim.cs` - Urun dagilim workflow

- Tur: Operasyonel workflow.
- DB: `Furpa`, `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`, `DEPOLAR`, `STOKLAR`, `BARKOD_TANIMLARI`, `Furpa.dbo.STOK_DAGILIM`, `Furpa.dbo.Bolge_Yoneticileri`, `DEPOLAR_ARASI_SIPARISLER`.
- Yaptigi islemler: Dagilim onerisi hesaplar, `STOK_DAGILIM` insert/update/delete yapar, bolge yoneticilerine mail atar, `STOKLAR.sto_siparis_dursun` alanini gunceller, `DEPOLAR_ARASI_SIPARISLER` kaydi olusturur.
- Is akisi:
  - Stok secilir.
  - Dagitim merkezi secilir.
  - Toplam dagitilacak koli girilir.
  - Son 42 gun satis hizina gore sube bazli dagilim onerisi hesaplanir.
  - Kullanici koli dagilimini manuel duzeltebilir.
  - Toplam koli farki sifirsa `STOK_DAGILIM` kaydedilir.
  - Bilgilendirme yapilinca durum `1` olur ve mail atilir.
  - Onaylaninca `DEPOLAR_ARASI_SIPARISLER` insert edilir ve durum `2` olur.
- Durum degerleri:
  - `0`: Bilgilendirme yapilmadi.
  - `1`: Onay bekliyor.
  - `2`: Dagilim yapildi.
- Onerilen API siniri:
  - `POST /distribution/proposals`
  - `POST /distribution/{evrakNo}/save`
  - `POST /distribution/{evrakNo}/notify`
  - `POST /distribution/{evrakNo}/approve`
  - `DELETE /distribution/{evrakNo}`
- Kritik tasima notlari:
  - Onay ve siparis olusturma tek transaction icinde olmali.
  - Mail gonderimi transaction disina alinmali veya outbox pattern ile yapilmali.
  - `Evrak_No` uretimi `max()+1` yerine sequence/identity/idempotent key ile yapilmali.
  - SQL string birlestirme temizlenmeli.
  - Mevcut kodda onay dialogu sonrasinda parantez hatasi riski var; kullanici "Hayir" dese bile islem blogu calisabilir. API'ye tasinirken bu davranis duzeltilmeli.

## Dagilim Modulu Detayli Analiz

Bu bolum ozellikle `FrmDagilim.cs` ekraninin yeni API'ye tasinmasi icin detaylandirilmistir. Bu ekran bir rapor degildir; rapor gibi veri hesaplar ama sonunda kayit, mail, stok karti update ve siparis insert gibi kalici etkiler uretir.

### Ekranin Amaci

Merkez satin alma veya depo kullanicisi belirli bir stok icin toplam dagitilacak koli miktarini girer. Sistem son 42 gunluk satis hizina bakarak bu koli miktarini subelere boler. Kullanici oneriyi manuel duzeltir. Toplam koli farki sifirsa dagilim kaydedilir. Sonra bolge yoneticilerine mail atilir ve en son Mikro'da depolar arasi siparis olusturularak dagilim kesinlestirilir.

### Kullanilan Form Alanlari

- `TxtStokAdi`: Stok adi arama ve secim alani.
- `TxtStokKodu`: Stok kodu ile direkt arama alani.
- `LblStokKod`: Secilen stok kodu.
- `LblBarkod`: Secilen stokun barkodu.
- `LblKoliici`: Stokun koli ici katsayisi.
- `TxtDagilimMiktar`: Toplam dagitilacak koli miktari.
- `TxtDagitan`: Dagilimi yapan kullanici/adi.
- `depolar`: Dagitim merkezi combobox'i.
- `LblDepNo`: Secilen dagitim merkezi depo no.
- `LblEvrakNo`: `STOK_DAGILIM.Evrak_No`.
- `LblFark`: Girilen toplam koli ile gridde dagitilan toplam koli arasindaki fark.
- `LblDagKoli`: Gridde dagitilan toplam koli.
- `LblDagAdet`: Gridde dagitilan toplam adet.
- `dataGridView1`: Hesaplanan veya kayitli dagilim detay satirlari.
- `DtgListele`: Durum `0` ve `1` olan dagilim kayitlari.
- `dataGridView3`: Son 3 gunde kesinlesmis, durum `2` olan dagilim kayitlari.
- `dataGridView4`: Mail icin uretilen pivot tablo; ekranda gizli.

### Kullanilan Veritabanlari ve Tablolar

#### `MikroDB_V16_FURPA_2024`

- `STOKLAR`: Stok adi, stok kodu, koli ici katsayi ve siparis durdur bayragi icin kullanilir.
- `BARKOD_TANIMLARI`: Stok barkodu bulmak icin kullanilir.
- `STOK_HAREKETLERI`: Son 42 gun satis miktari ve satis hizi hesabinin ana kaynagidir.
- `DEPOLAR`: Sube, bolge ve dagitim merkezi bilgileri icin kullanilir.
- `DEPOLAR_ARASI_SIPARISLER`: Dagilim kesinlesince Mikro tarafinda depolar arasi siparis satirlari olusturulur.
- `dbo.fn_DepoIsmi`: Depo/sube adini almak icin kullanilir.
- `dbo.fn_depodakimiktar`: Sube son stok miktarini almak icin kullanilir.

#### `Furpa`

- `STOK_DAGILIM`: Dagilim workflow kayit tablosudur.
- `Bolge_Yoneticileri`: Bolge kodu, bolge muduru ve e-posta bilgisi icin kullanilir.

### Ana Tablo: `Furpa.dbo.STOK_DAGILIM`

Koddan gorulen alanlar:

- `secim`: Gridde coklu secim icin kullanilan alan.
- `Evrak_No`: Dagilim kaydinin grup numarasi.
- `Kayit_Tarihi`: Dagilim kayit tarihi.
- `Stok_Kodu`: Dagitilan stok kodu.
- `Bolge`: Sube bolge kodu.
- `Sube_Kodu`: Dagilim yapilan sube/depo kodu.
- `Toplam_Satis_42_Gun`: Son 42 gun sube satis miktari.
- `Sirket_Ortalama_Satisi`: Sirket geneli gunluk ortalama satis.
- `Sube_Ortalama_Satisi`: Sube gunluk ortalama satis.
- `Dagilim_Koli_Miktar`: Sube icin dagitilacak koli miktari.
- `Dagilim_Adet_Miktar`: Koli ici katsayiya gore adet miktari.
- `Dagilimi_Yapan`: Kaydi yapan kisi.
- `Durum`: Workflow status alani.
- `Kesinlestirme_Tarihi`: Durum `2` oldugunda yazilan kesinlestirme tarihi.
- `Dagitim_Merkezi`: Cikis/depo merkezi. Kodda `50`, `53`, `56` gibi depolardan seciliyor.

### Durum Akisi

`STOK_DAGILIM.Durum` alani workflow'u tasir:

- `0`: Dagilim kaydedildi, bilgilendirme yapilmadi.
- `1`: Bolge yoneticilerine mail atildi, onay bekliyor.
- `2`: Dagilim kesinlesti, Mikro `DEPOLAR_ARASI_SIPARISLER` kaydi olustu.

Onerilen enum:

```csharp
public enum DistributionStatus
{
    Draft = 0,
    Notified = 1,
    Finalized = 2
}
```

### Ekran Acilis Akisi

`FrmDagilim_Load` calisinca:

- `listBox1` gizlenir.
- Toplam/fark label'lari sifirlanir.
- `listeyukle()` ile durum `0` ve `1` kayitlar yuklenir.
- `onaylananlarıyukle()` ile son 3 gunde durum `2` olan kayitlar yuklenir.
- Dagitim merkezi combobox'i doldurulur.

Dagitim merkezi sorgusu:

```sql
select dep_no, dep_adi
from DEPOLAR
where dep_no in (50, 56, 53)
order by dep_adi
```

API karsiligi:

```http
GET /lookups/distribution-centers
```

### Stok Secim Akisi

Stok secimi uc yoldan yapiliyor:

- Stok adi yazildikca `STOKLAR.sto_isim like ...` ile listbox doldurulur.
- Listbox double click ile stok secilir.
- `TxtStokKodu` alanina kod yazilip Enter'a basilinca stok adi bulunur.

Kullanilan lookup kaynaklari:

- `STOKLAR`
- `BARKOD_TANIMLARI`

Koli ici katsayi hesabi:

```sql
select
case
    when sto_birim2_ad = 'KOLI' then sto_birim2_katsayi * -1
    else sto_birim3_katsayi * -1
end as Kolisi
from STOKLAR
where sto_kod = @stokKod
```

API karsiligi:

```http
GET /lookups/stocks?query={stokAdi}
GET /lookups/stocks/{stokKod}
GET /lookups/stocks/by-barcode/{barcode}
```

Tasima notu: Mevcut kodda bu lookup sorgularinin cogu string birlestirme ile yazilmis. API'de `@query`, `@stokKod`, `@barcode` parametreleri kullanilmali.

### Dagilim Onerisi Hesaplama

Buton: `BtnEkle_Click`.

Zorunlu alanlar:

- `LblStokKod`
- `TxtDagilimMiktar`
- `TxtDagitan`
- `LblDepNo`

Sabit deger:

- `@gunsayisi = 42`

Sorgunun ana mantigi:

```sql
declare @dagitimmiktar int;
declare @stok_kodu nvarchar(10);
declare @gunsayisi int;

set @stok_kodu = @stokKod;
set @dagitimmiktar = @dagitilacakKoli;
set @gunsayisi = 42;

with satis as (
    select
        sth_cikis_depo_no as Sube_Kodu,
        dbo.fn_DepoIsmi(sth_cikis_depo_no) as Gonderen_Sube,
        sto_kod,
        sto_isim,
        sum(sth_miktar) as Miktar,
        case
            when sto_birim2_ad = 'KOLI' then sto_birim2_katsayi * -1
            else sto_birim3_katsayi * -1
        end as Kolisi,
        (
            select sum(sth_miktar)
            from STOK_HAREKETLERI
            where sth_stok_kod = @stok_kodu
              and sth_tip = 1
              and sth_cins = 1
              and sth_evraktip in (1, 4)
              and sth_normal_iade = 0
              and sth_tarih between cast(getdate() - @gunsayisi as date) and cast(getdate() as date)
        ) as Sirket_Toplam,
        (
            select sum(sth_miktar) / @gunsayisi
            from STOK_HAREKETLERI
            where sth_stok_kod = @stok_kodu
              and sth_tip = 1
              and sth_cins = 1
              and sth_evraktip in (1, 4)
              and sth_normal_iade = 0
              and sth_tarih between cast(getdate() - @gunsayisi as date) and cast(getdate() as date)
        ) as Ort_Toplam
    from STOKLAR
    inner join STOK_HAREKETLERI on sto_kod = sth_stok_kod
    where sto_kod = @stok_kodu
      and sth_tip = 1
      and sth_cins = 1
      and sth_evraktip in (1, 4)
      and sth_normal_iade = 0
      and sth_tarih between cast(getdate() - @gunsayisi as date) and cast(getdate() as date)
    group by sth_cikis_depo_no, sto_birim2_ad, sto_birim2_katsayi, sto_birim3_katsayi, sto_kod, sto_isim
),
subeler as (
    select dep_adi, dep_no, dep_bolge_kodu
    from DEPOLAR
    where dep_no > 100
)
select
    dep_bolge_kodu as Bolge,
    d.dep_no as Depo_Kodu,
    d.dep_adi as Sube,
    isnull(s.Miktar, 0) as Toplam_Satis,
    round(isnull(s.Miktar, 0) / 42, 2) as Gunluk_Ort_Satis,
    isnull(round(s.Ort_Toplam, 2), 0) as Sirket_Gunluk_Ort,
    isnull(
        case
            when ((isnull(s.Miktar, 0) / @gunsayisi) / s.Ort_Toplam) * @dagitimmiktar < 0.5 then 0
            else round(((isnull(s.Miktar, 0) / @gunsayisi) / s.Ort_Toplam) * @dagitimmiktar, 0)
        end,
        0
    ) as Dagitilacak_Koli_Miktar,
    isnull(
        case
            when ((isnull(s.Miktar, 0) / @gunsayisi) / s.Ort_Toplam) * @dagitimmiktar < 0.5 then 0
            else round(((isnull(s.Miktar, 0) / @gunsayisi) / s.Ort_Toplam) * @dagitimmiktar, 0) * s.Kolisi
        end,
        0
    ) as Dagitilacak_Adet_Miktar,
    dbo.fn_depodakimiktar(@stok_kodu, d.dep_no, getdate()) as Son_Stok
from subeler d
left join satis s on d.dep_no = s.Sube_Kodu
order by dep_bolge_kodu, d.dep_adi
```

Hesap formulu:

```text
subeGunlukOrtalama = subeSon42GunSatis / 42
sirketGunlukOrtalama = sirketSon42GunSatis / 42
oran = subeGunlukOrtalama / sirketGunlukOrtalama
onerilenKoli = round(oran * toplamDagitilacakKoli)
onerilenKoli < 0.5 ise 0
onerilenAdet = onerilenKoli * koliIci
```

Cikti kolonlari:

- `Bolge`
- `Depo_Kodu`
- `Sube`
- `Toplam_Satis`
- `Gunluk_Ort_Satis`
- `Sirket_Gunluk_Ort`
- `Dagitilacak_Koli_Miktar`
- `Dagitilacak_Adet_Miktar`
- `Son_Stok`

API karsiligi:

```http
POST /distribution/proposals
```

Request:

```json
{
  "stokKod": "12345",
  "dagitimMerkeziDepoNo": 50,
  "toplamKoli": 120,
  "gunSayisi": 42
}
```

Response:

```json
{
  "stokKod": "12345",
  "dagitimMerkeziDepoNo": 50,
  "toplamKoli": 120,
  "gunSayisi": 42,
  "toplamDagitilanKoli": 118,
  "farkKoli": 2,
  "lines": [
    {
      "bolge": 1,
      "subeKodu": 101,
      "subeAdi": "Sube Adi",
      "toplamSatis42Gun": 42.0,
      "subeGunlukOrtalama": 1.0,
      "sirketGunlukOrtalama": 8.5,
      "onerilenKoli": 14,
      "onerilenAdet": 168,
      "sonStok": 4.0
    }
  ]
}
```

Tasima notlari:

- `gunSayisi` default `42` olmali, ama API'de parametre yapilabilir.
- `getdate()` yerine API tarafindan hesaplanan tarih araligi veya DB server tarihi karari netlestirilmeli.
- `s.Ort_Toplam` null veya 0 ise bolme hatasi riski var. API'de bu durum acik yonetilmeli.
- Oneri sonucu toplam koliye her zaman esit olmayabilir. Eski ekran kullanicidan manuel duzeltme bekliyor.

### Grid Uzerinde Manuel Duzeltme

Kullanici `dataGridView1` icinde koli kolonunu degistirebilir.

Degisen kolon:

- `Dagilim_Koli_Miktar`

Otomatik hesaplanan kolon:

- `Dagilim_Adet_Miktar = Dagilim_Koli_Miktar * LblKoliici`

Toplam kontrol:

```text
fark = TxtDagilimMiktar - sum(dataGridView1.Dagilim_Koli_Miktar)
```

Kayit icin sart:

```text
fark == 0
```

Tasima notu: API'de save/finalize request'i mutlaka toplam kontrolu yapmali. Sadece UI tarafina guvenilmemeli.

### Dagilim Kaydetme

Buton: `BtnKaydet_Click`.

Iki mod var:

- `LblEvrakNo` bos degilse mevcut dagilim satirlari update edilir.
- `LblEvrakNo` bossa yeni `Evrak_No` uretilir ve tum grid satirlari insert edilir.

Onaylanmis kayit kontrolu:

```sql
select Durum
from STOK_DAGILIM
where Evrak_No = @evrakNo
group by Durum
```

Eger `Durum = 2` ise update engellenir.

Mevcut kayit update:

```sql
update STOK_DAGILIM
set Dagilim_Koli_Miktar = @Dagilim_Miktari_Koli,
    Dagilim_Adet_Miktar = @Dagilim_Miktari_Adet
where Stok_Kodu = @Stok_Kodu
  and Evrak_No = @Evrak_No
  and Sube_Kodu = @Sube_Kodu
```

Yeni kayit icin evrak no uretimi:

```sql
select max(convert(int, Evrak_No)) + 1 as SiradakiNo
from STOK_DAGILIM
```

Yeni kayit insert alanlari:

```sql
insert into STOK_DAGILIM (
    Evrak_No,
    Kayit_Tarihi,
    Stok_Kodu,
    Bolge,
    Sube_Kodu,
    Toplam_Satis_42_Gun,
    Sirket_Ortalama_Satisi,
    Sube_Ortalama_Satisi,
    Dagilim_Koli_Miktar,
    Dagilim_Adet_Miktar,
    Dagilimi_Yapan,
    Durum,
    Kesinlestirme_Tarihi,
    Dagitim_Merkezi
)
values (
    @Evrak_No,
    convert(date, getdate()),
    @Stok_Kodu,
    @Bolge,
    @Sube_Kodu,
    @Toplam_Satis_42_Gun,
    @Sirket_Ortalama_Satisi,
    @Sube_Ort_Satis,
    @Dagilim_Koli,
    @Dagilim_Adet,
    @Dagilimi_Yapan,
    0,
    '',
    @Dagitim_Merkezi
)
```

API karsiligi:

```http
POST /distribution
PUT /distribution/{evrakNo}
```

Request:

```json
{
  "stokKod": "12345",
  "dagitimMerkeziDepoNo": 50,
  "dagilimiYapan": "KULLANICI",
  "toplamKoli": 120,
  "lines": [
    {
      "bolge": 1,
      "subeKodu": 101,
      "toplamSatis42Gun": 42.0,
      "sirketOrtalamaSatisi": 8.5,
      "subeOrtalamaSatisi": 1.0,
      "dagilimKoliMiktar": 14,
      "dagilimAdetMiktar": 168
    }
  ]
}
```

Tasima notlari:

- `max(Evrak_No)+1` race condition uretir. Sequence, identity veya tekil command ID kullanilmali.
- Insert/update tum satirlar icin tek transaction icinde yapilmali.
- `Dagitim_Merkezi` mevcut kodda string interpolation ile SQL'e giriyor; parametre olmali.
- `Kesinlestirme_Tarihi` bos string yerine nullable date olmali.
- `Durum = 2` kayitlar immutable kabul edilmeli.

### Kayit Listeleme

Metot: `listeyukle()`.

Amac: Bilgilendirme yapilmamis ve onay bekleyen kayitlari listelemek.

Sorgu mantigi:

```sql
select
    secim,
    Evrak_No,
    Stok_Kodu,
    (select sto_isim from MikroDB_V16_FURPA_2024.dbo.STOKLAR where Stok_Kodu = sto_kod) as Stok_Adi,
    sum(convert(int, Dagilim_Koli_Miktar)) as Dagilim_Koli_Miktar,
    sum(convert(int, Dagilim_Adet_Miktar)) as Dagilim_Adet_Miktar,
    case
        when Durum = 0 then 'Bilgilendirme Yapilmadi'
        when Durum = 1 then 'Onay Bekliyor'
        when Durum = 2 then 'Dagilim Yapildi'
    end as Durum,
    (select dep_adi from MikroDB_V16_FURPA_2024.dbo.DEPOLAR where dep_no = Dagitim_Merkezi) as dep_adi
from STOK_DAGILIM
where Durum in (0, 1)
group by secim, Evrak_No, Stok_Kodu, Durum, Dagitim_Merkezi
order by Evrak_No desc
```

API karsiligi:

```http
GET /distribution?status=Draft,Notified
```

### Kesinlesen Dagilimlari Listeleme

Metot: `onaylananlarıyukle()`.

Amac: Son 3 gunde kesinlesmis dagilimlari listelemek.

Sorgu mantigi:

```sql
select
    Evrak_No,
    Stok_Kodu,
    Stok_Adi,
    sum(Dagilim_Koli_Miktar) as Dagilim_Koli_Miktar,
    sum(Dagilim_Adet_Miktar) as Dagilim_Adet_Miktar,
    Dagilimi_Yapan,
    Durum,
    Kayit_Tarihi,
    Kesinlestirme_Tarihi,
    Dagitim_Merkezi
from STOK_DAGILIM
where Durum = 2
  and Kesinlestirme_Tarihi >= cast(getdate() - 3 as date)
group by ...
```

API karsiligi:

```http
GET /distribution?status=Finalized&finalizedFrom={date}
```

Tasima notu: Son 3 gun sabiti API'de default olabilir, ama parametre ile degistirilebilir olmali.

### Detay Gosterme

Context menu: `Detay Goster`.

Amac: Secilen `Evrak_No` icin baslik bilgilerini ve satir detaylarini tekrar ekrana yuklemek.

Baslik sorgusu sunlari toplar:

- Toplam dagilim koli miktari
- Koli ici katsayi
- Stok adi
- Stok kodu
- Dagilimi yapan kisi
- Barkod
- Dagitim merkezi adi

Satir sorgusu sunlari getirir:

- `Bolge`
- `Sube_Kodu`
- `Sube_Adi`
- `Toplam_Satis_42_Gun`
- `Sube_Ortalama_Satisi`
- `Sirket_Ortalama_Satisi`
- `Dagilim_Koli_Miktar`
- `Dagilim_Adet_Miktar`

API karsiligi:

```http
GET /distribution/{evrakNo}
```

Response:

```json
{
  "evrakNo": "1001",
  "stokKod": "12345",
  "stokAdi": "Urun Adi",
  "barkod": "869...",
  "koliIci": 12,
  "dagitimMerkeziDepoNo": 50,
  "dagitimMerkeziAdi": "Merkez Depo",
  "dagilimiYapan": "KULLANICI",
  "status": "Draft",
  "lines": []
}
```

### Bilgilendirme Maili

Buton: `BtnDagilimBilgilendir`.

Metotlar:

- `secilenler()`
- `depogetir()`
- `DagilimBilgilendirme2(int depo_no, string eposta)`

Bolge yoneticileri sorgusu:

```sql
select bolge_kodu, bolge_muduru, bolge_muduru_eposta
from Furpa.dbo.Bolge_Yoneticileri with (nolock)
```

Her bolge icin dagilim detay sorgusu:

```sql
select
    Bolge,
    dep_adi as Sube_Adi,
    Stok_Kodu,
    sto_isim as Stok_Adi,
    Dagilim_Koli_Miktar,
    Dagilim_Adet_Miktar,
    Dagilimi_Yapan
from Furpa.dbo.STOK_DAGILIM
where Evrak_No in (@evrakNoList)
  and Bolge = @bolge
order by Bolge, Sube_Adi
```

Mail icerigi:

- Satirlar pivot tabloya cevriliyor.
- Satir ekseni sube.
- Kolon ekseni stok adi.
- Hucre degeri `Dagilim_Adet_Miktar`.
- Hucre degeri `0` ise "Dagilim Yapilmadi" yaziliyor.

Mail sonrasi yapilan update:

```sql
update STOK_DAGILIM
set Durum = 1
where Evrak_No in (@evrakNoList)
```

Ayrica stok karti siparise kapatiliyor:

```sql
update MikroDB_V16_FURPA_2024.dbo.STOKLAR
set sto_siparis_dursun = 1
where sto_kod in (
    select Stok_Kodu
    from Furpa.dbo.STOK_DAGILIM
    where Evrak_No in (@evrakNoList)
    group by Stok_Kodu
)
```

API karsiligi:

```http
POST /distribution/notify
```

Request:

```json
{
  "evrakNos": ["1001", "1002"]
}
```

Tasima notlari:

- Mail gonderimi API request'i icinde senkron yapilmamali; job/outbox daha saglikli olur.
- `Durum = 1` update'i mail basarili olduktan sonra yapilmali veya outbox status ile izlenmeli.
- Stok kartini siparise kapatma ayri command olarak da dusunulebilir.
- SMTP bilgileri secret manager'dan alinmali.
- CC listesi config'e alinmali.

### Dagilim Silme

Buton: `Kayıt Sil`.

Mevcut davranis:

- Gridde secili `Evrak_No` listesi alinir.
- Kullanici onayi alinir.
- Her `Evrak_No` icin `STOK_DAGILIM` satirlari silinir.

Sorgu:

```sql
delete from STOK_DAGILIM
where Evrak_No = @evrakNo
```

API karsiligi:

```http
DELETE /distribution/{evrakNo}
```

Tasima notlari:

- Sadece `Durum = 0` kayitlar silinebilmeli.
- `Durum = 1` ise geri alma/mail etkisi dusunulmeli.
- `Durum = 2` kesinlesmis kayitlar silinmemeli; iptal command'i ayri tasarlanmali.

### Dagilim Kesinlestirme

Buton: `Dağılımı Onayla`.

Mevcut davranis:

- Secili `Evrak_No` listesi alinir.
- Kullanici onayi istenir.
- `Dagilim_Adet_Miktar > 0` olan satirlar Mikro `DEPOLAR_ARASI_SIPARISLER` tablosuna insert edilir.
- `STOK_DAGILIM.Durum = 2` yapilir.
- `Kesinlestirme_Tarihi = getdate()` yazilir.

Siparis insert kaynagi:

```sql
from STOK_DAGILIM
where Dagilim_Adet_Miktar > 0
  and Evrak_No in (@evrakNoList)
```

Mikro siparis alan eslesmeleri:

- `ssip_Guid`: `NEWID()`
- `ssip_fileid`: `86`
- `ssip_create_user`: `1`
- `ssip_create_date`: `GETDATE()`
- `ssip_lastup_user`: `1`
- `ssip_lastup_date`: `GETDATE()`
- `ssip_tarih`: bugun
- `ssip_teslim_tarih`: bugun
- `ssip_evrakno_seri`: `'D' + Sube_Kodu`
- `ssip_evrakno_sira`: ayni seri/sube icin max sira + 1
- `ssip_satirno`: sube icinde row number - 1
- `ssip_stok_kod`: `STOK_DAGILIM.Stok_Kodu`
- `ssip_miktar`: `STOK_DAGILIM.Dagilim_Adet_Miktar`
- `ssip_aciklama`: `Merkez Depo Otomatik Dagilim`
- `ssip_girdepo`: `Sube_Kodu`
- `ssip_cikdepo`: `Dagitim_Merkezi`
- `ssip_birim_pntr`: `1`
- `ssip_gecerlilik_tarihi`: `1899-12-31`

Durum update:

```sql
update STOK_DAGILIM
set Durum = 2,
    Kesinlestirme_Tarihi = convert(date, getdate())
where Evrak_No in (@evrakNoList)
```

API karsiligi:

```http
POST /distribution/finalize
```

Request:

```json
{
  "evrakNos": ["1001", "1002"],
  "finalizedBy": "KULLANICI"
}
```

Tasima notlari:

- Insert ve status update tek transaction icinde olmali.
- Ayni `Evrak_No` ikinci kez finalize edilememeli.
- `DEPOLAR_ARASI_SIPARISLER` icin idempotency key tutulmali.
- `ssip_evrakno_sira = max + 1` ayni anda iki islemde cakisma uretir. Kilit, sequence veya Mikro'nun evrak no uretim standardi kullanilmali.
- Mevcut WinForms kodunda `if (A == DialogResult.Yes)` satirindan sonra surekli calisabilecek parantez blogu riski var. API'de onay UI'dan ayrildigi icin bu hata tekrarlanmamali.

### Dagilim Excel Aktarimi

Buton: `BtnExcel`.

Mevcut davranis:

- `dataGridView1` icindeki veriyi Microsoft Office Interop ile Excel'e aktarir.
- Dosyayi `c:\output.xls` olarak kaydeder.

API tasima notu:

- API tarafinda Office Interop kullanilmamali.
- Gerekirse `GET /distribution/{evrakNo}/export` gibi endpoint CSV/XLSX stream donmeli.
- Export, rapor verisinden uretilmeli; UI gridine bagli olmamali.

### Dagilim Icin Onerilen API Tasarimi

```http
GET    /lookups/distribution-centers
GET    /distribution?status=Draft,Notified
GET    /distribution/{evrakNo}
POST   /distribution/proposals
POST   /distribution
PUT    /distribution/{evrakNo}
POST   /distribution/notify
POST   /distribution/finalize
DELETE /distribution/{evrakNo}
GET    /distribution/{evrakNo}/export
```

Onerilen servisler:

- `DistributionProposalService`: Son 42 gun satis hizina gore dagilim onerisi uretir.
- `DistributionRepository`: `STOK_DAGILIM` okuma/yazma islemleri.
- `DistributionNotificationService`: Bolge bazli mail/pivot hazirlar.
- `DistributionFinalizationService`: Mikro `DEPOLAR_ARASI_SIPARISLER` insert ve status update yapar.
- `StockLookupService`: Stok/barkod/koli ici bilgilerini getirir.
- `WarehouseLookupService`: Sube, bolge ve dagitim merkezi bilgilerini getirir.

### Dagilim DTO Onerileri

```csharp
public sealed class DistributionHeaderDto
{
    public string EvrakNo { get; set; }
    public string StokKod { get; set; }
    public string StokAdi { get; set; }
    public string Barkod { get; set; }
    public int KoliIci { get; set; }
    public int DagitimMerkeziDepoNo { get; set; }
    public string DagitimMerkeziAdi { get; set; }
    public string DagilimiYapan { get; set; }
    public DistributionStatus Status { get; set; }
    public DateTime KayitTarihi { get; set; }
    public DateTime? KesinlestirmeTarihi { get; set; }
    public List<DistributionLineDto> Lines { get; set; }
}
```

```csharp
public sealed class DistributionLineDto
{
    public int Bolge { get; set; }
    public int SubeKodu { get; set; }
    public string SubeAdi { get; set; }
    public decimal ToplamSatis42Gun { get; set; }
    public decimal SubeOrtalamaSatisi { get; set; }
    public decimal SirketOrtalamaSatisi { get; set; }
    public int DagilimKoliMiktar { get; set; }
    public int DagilimAdetMiktar { get; set; }
    public decimal SonStok { get; set; }
}
```

### Dagilim Test Senaryolari

- Stok secilmeden proposal istenirse validation hatasi donmeli.
- Dagitim merkezi secilmeden proposal/save yapilamamali.
- `toplamKoli <= 0` ise validation hatasi donmeli.
- Satisi olmayan stokta bolme/null durumu hata uretmemeli; tum subeler 0 onerilebilir.
- Oneri toplam koli ile eslesmiyorsa save reddedilmeli veya request'teki manuel satirlar toplam kontrolunden gecmeli.
- `Durum = 2` olan dagilim update/delete edilememeli.
- Notify islemi mail basarisiz olursa status stratejisi net olmali.
- Finalize islemi ikinci kez calistirilirsa duplicate `DEPOLAR_ARASI_SIPARISLER` uretmemeli.
- Finalize sirasinda insert basarili, status update basarisiz olursa transaction rollback olmali.
- `ssip_evrakno_sira` cakisma testi yapilmali.
- Coklu `Evrak_No` notify/finalize islemlerinde tum evraklar ayni transaction veya kontrollu partial result ile yonetilmeli.

### `BultanPerformans/FrmBultenUrunTanitim.cs` - Bulten tanim CRUD

- Tur: Admin/command.
- DB: `Furpa`, `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `BULTEN_TANIMLARI`, `BULTEN_URUN_TANIMLARI`, `BULTEN_SUBE_TANIMLARI`, `STOKLAR`, `DEPOLAR`.
- Yaptigi islemler: Bulten baslik kaydi, bulten urunleri, bultene dahil subeler, Excel'den urun alma.
- Onerilen API siniri: `POST/PUT/DELETE /promotions`, `POST /promotions/{id}/products`, `POST /promotions/{id}/branches`.
- Tasima notu: Excel import API'ye dosya upload olarak alinacaksa validasyon ve hata raporu donmeli.

### `FrmFiyatTeklifi.cs` - Fiyat teklifi CRUD

- Tur: Admin/command.
- DB: `Furpa`, `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `Fiyat_Teklifi_Firma`, `Fiyat_Teklifi_Urunler`, stok/cari lookup tablolari.
- Yaptigi islemler: Teklif firma kaydi, teklif urun kaydi, guncelleme ve silme.
- Onerilen API siniri: `POST/PUT/DELETE /price-offers`.
- Tasima notu: Fiyat alanlari decimal olarak alinmali; para birimi ve KDV semantigi netlestirilmeli.

### `FrmSayim.cs` - Sayim kaydi operasyonlari

- Tur: Admin/command.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `SAYIM_SONUCLARI`, `STOKLAR`, `DEPOLAR`.
- Yaptigi islemler: Sayim kaydi listeler, miktar gunceller, kayit siler.
- Onerilen API siniri: `POST/PUT/DELETE /counting`.
- Tasima notu: Sayim guncellemeleri audit log ile tutulmali.

### `Frmirsaliyeislemleri.cs` - Irsaliye/fire hareket operasyonlari

- Tur: Yuksek riskli command.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`.
- Yaptigi islemler: Irsaliye/fire hareketlerini okur; bazi hareket satirlarini siler veya miktar/stok bilgisi gunceller.
- Onerilen API siniri: `POST /dispatch-notes/{id}/adjust`, `DELETE /dispatch-notes/{id}/lines/{lineId}` gibi net komutlar.
- Tasima notu: Yetki, audit ve geri alma stratejisi olmadan tasinmamali.

### `FrmirsaliyeNoGiris.cs` - Irsaliye belge no guncelleme

- Tur: Command.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_HAREKETLERI`.
- Yaptigi islemler: `sth_belge_no` ve aciklama alanlarini gunceller.
- Onerilen API siniri: `PATCH /stock-movements/{id}/document-info`.
- Tasima notu: Eski/yeni deger audit log'a yazilmali.

### `FrmStokDepoYetki.cs` - Stok depo yetki bayraklari

- Tur: Admin/command.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `STOK_DEPO_DETAYLARI`, `STOKLAR`, `DEPOLAR`.
- Yaptigi islemler: Mal kabul, satis, siparis durdur gibi depo bazli stok bayraklarini gunceller.
- Onerilen API siniri: `PATCH /stocks/{stokKod}/warehouse-permissions`.
- Tasima notu: Toplu update varsa request modelinde acik liste alinmali; hangi depo icin hangi bayrak degisti loglanmali.

### `FrmSonFiyatVirman.cs` - Son fiyat virman

- Tur: Yuksek riskli finans/stok command.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: virman hareketi olusturan `INSERT...SELECT` sorgulari ve silme sorgulari.
- Yaptigi islemler: Virman hareketleri olusturur, son virmanlari silebilir.
- Onerilen API siniri: `POST /transfers/price-adjustments`, `DELETE /transfers/price-adjustments/{id}`.
- Tasima notu: Transaction, idempotency, yetki ve audit zorunlu. Islem oncesi dry-run/preview endpointi dusunulmeli.

### `FrmAxatadanMikroya.cs` - Axata tekrar gonderim

- Tur: Integration/admin command.
- DB: `AxataWM_CANLI`.
- Ana kaynaklar: Axata entegrasyon tablosu `ENT006`.
- Yaptigi islemler: Kayit durumunu kontrol eder ve `S06STAT=0` yaparak Mikro'ya tekrar gonderimi tetikler.
- Onerilen API siniri: `POST /integrations/axata/retry`.
- Tasima notu: Tekrar gonderim idempotent olmali; ayni kaydin pes pese tetiklenmesi kontrol edilmeli.

### `Siparis/FrmSiparisTarihDegistir.cs` - Siparis tarihi guncelleme

- Tur: Command.
- DB: `MikroDB_V16_FURPA_2024`.
- Ana kaynaklar: `SIPARISLER`.
- Yaptigi islemler: `sip_tarih` ve `sip_HareketGrupKodu3` alanlarini gunceller.
- Onerilen API siniri: `PATCH /orders/{id}/date`.
- Tasima notu: Evrak kimligi netlestirilmeli; tarih degisikligi auditlenmeli.

### `FrmStokKontrol.cs` - Stok kontrol menu/util

- Tur: Menu/util; rapor degil.
- DB: `MikroDB_V16_FURPA_2024`.
- Yaptigi islemler: Alt ekran navigasyonu ve bos stok kodu bulma gibi yardimci isler.
- Onerilen API siniri: Gerekirse `GET /lookups/stocks/empty-codes`.
- Tasima notu: UI navigasyonu API'ye tasinmaz; sadece veri uretiyorsa endpoint olur.

## Ana Menu Eslesmesi

`FrmAna.cs` ana ekranda rapor/ekran aciyor. Yeni API karsiliklari:

- Sube/depo genel stok: `FrmSonStok` -> `GET /reports/stocks/by-depot`
- Urun bazli tum subeler son stok: `FrnUrun` -> `GET /reports/stocks/by-product`
- Cari bazli son stok: `FrmCari` -> `GET /reports/stocks/by-supplier`
- Kategori bazli son stok: `FrmKategori` -> `GET /reports/stocks/by-category`
- Birlikte ne satildi: `FrmBirlikteNeSatildi` -> `GET /reports/sales/co-purchased`
- Bulten performans: `FrmBultenPerformans` -> `GET /reports/promotions/performance`
- Cari borc/alacak analiz: `FrmCariAnaliz` -> `GET /reports/current-accounts/balance`
- Stok sevk yeri: `StokSevkYeri` -> `GET /reports/stocks/shipment-source`
- Giris cikis karsilastirma: `FrmGirisCikis` -> `GET /reports/movements/in-out`
- Axata Mikro stok karsilastirma: `FrmAxataMikro` -> `GET /reports/inventory/axata-mikro-diff`
- Satis iade karsilastirma: `FrmSatisIadeKarsilastir` -> `GET /reports/sales/return-comparison`
- Merkez depoda var subede yok: `FrmDepodaVarSubedeYok` -> `GET /reports/inventory/warehouse-has-branch-missing`
- Satmayan urunler: `FrmSatmayanUrunler` -> `GET /reports/sales/not-sold`
- Stok kart detay: `StokKartDetay` -> `GET /reports/stocks/card-details`
- Karlilik: `FrmKarlilik` -> `GET /reports/profitability`
- Merkez depoda yoka dusen stok: `FrmDepoYokaDusenStok` -> `GET /reports/inventory/warehouse-zero`
- Dagilim yap: `FrmDagilim` -> `distribution` workflow, rapor degil
- Depolara iade edilen urun: `FrmDepolaraIade` -> `GET /reports/returns/to-warehouse`
- Sarkuteri depoda yok: `FrmSarkuteriDepodaYok` -> `GET /reports/inventory/deli-warehouse-missing`
- Tarih aralikli sube detayli satis: `FrmTarihAralikliSatis` -> `GET /reports/sales/branch-detail`
- Firmaya verilen siparisler: `FrmSiparisVerilen` -> `GET /reports/orders/supplier-orders`
- Iki tarih aralikli satis karsilastirma: `FrmIkiTarihAralikliSatisKarsliastirma` -> `GET /reports/sales/year-comparison`
- Stok kontrol menu: `FrmStokKontrol` -> API'ye sadece util veri gerekiyorsa tasinir
- Fiyat teklifi: `FrmFiyatTeklifi` -> `price-offers` CRUD, rapor degil
- Uretici bazli tum subeler son stok: `FrmUreticiSonStok` -> `GET /reports/stocks/by-producer`

## API Tasarim Notlari

- Parametreli SQL kullanilmali. Mevcut kodda string interpolation/concatenation cok fazla; SQL injection ve tarih format hatasi riski var.
- Tarihler string olarak SQL'e gomulmemeli. API tarafinda `DateOnly`/`DateTime` parametresi alinip SQL parametresi olarak gecilmeli.
- `NOLOCK` her yerde var. API sonucunun dirty read olabilecegi bilinmeli; kritik raporlarda `NOLOCK` karari yeniden verilmeli.
- Rapor endpointleri read-only olmali. Insert/update/delete yapan ekranlar ayri command/admin modulunde ele alinmali.
- Excel export, MessageBox, GridView kolon basliklari API'den ayrilmali. API sadece typed DTO donmeli.
- Lookup sorgulari endpointlerden ayrilmali: stok arama, cari arama, depo listesi, kategori listesi gibi.
- Uzun calisan raporlar icin async job/cache dusunulmeli.
- Cok satir donebilen raporlarda sayfalama, limit ve export/job modeli tasarlanmali.
- Cross-db sorgularda connection ve timeout politikasi net olmali.
- Para, miktar ve yuzde alanlari `decimal` olmali; string donulmemeli.
- Eski WinForms gridindeki kolon adlari DTO property adlarina birebir tasinmak zorunda degil; API daha temiz ve tutarli isimlendirme kullanmali.

## Guvenlik ve Kalite Riskleri

- Hard-coded DB connection string ve mail credential temizlenmeli.
- SQL injection riski tasiyan string birlestirmeler parametreli sorguya cevrilmeli.
- `max(id)+1` gibi manuel evrak no uretimleri sequence/identity/idempotency ile degistirilmeli.
- Command operasyonlarinda transaction kullanilmali.
- Command operasyonlarinda audit log tutulmali.
- Kritik operasyonlar icin yetki kontrolu eklenmeli.
- Mail gonderimi gibi dis etkiler outbox/job yapisina alinmali.
- Hata durumlarinda yarim kayit kalmamasi icin retry/idempotency tasarlanmali.

## Tasinirken Onerilen Katmanlama

- `ReportsController`: Sadece read-only rapor endpointleri.
- `LookupsController`: Stok, cari, depo, kategori, uretici, personel aramalari.
- `DistributionController`: Dagilim workflow komutlari.
- `PromotionsController`: Bulten CRUD ve bulten performans raporlari.
- `PriceOffersController`: Fiyat teklifi CRUD ve teklif listeleri.
- `CountingController`: Sayim command islemleri ve sayim karsilastirma raporu.
- `OrdersController`: Siparis raporlari ve siparis tarih command islemleri.
- `IntegrationsController`: Axata/Mikro entegrasyon komutlari.

## Dogrulama Plani

Her endpoint icin eski WinForms sonucu ile yeni API sonucu ayni parametrelerle karsilastirilmali.

Kontrol listesi:

- Aynı tarih ve filtreyle satir sayisi esit mi?
- Toplam miktar esit mi?
- Toplam tutar esit mi?
- Kar/maliyet gibi hesaplanmis alanlarda yuvarlama ayni mi?
- Bos/null fiyat davranisi eski ekranla uyumlu mu?
- Sube/depo sabitleri dogru mu?
- DLS, kategori, uretici ve satin alma sorumlusu filtreleri ayni sonucu veriyor mu?
- Command operasyonlarinda transaction rollback test edildi mi?
- Audit log eski/yeni degerleri tutuyor mu?
- Mail veya entegrasyon gibi dis etkiler test/staging ortaminda izole edildi mi?
