# Liste Index Envanteri ve Oneriler

Bu rapor 27.10.2026 tarihinde canli Mikro ve Shopigo veritabanlarinda yapilan kontrollerle hazirlandi. Onay sonrasinda oncelikli yedi liste indexi canli Mikro veritabanina uygulandi.

## Kisa Sonuc

Canlida safe-create, depo sevk cikis, stok fisi, kasa icmal ve etiket sorgularinin temel indexleri mevcut ve aktif kullaniliyor. Asil eksik alanlar firma hareket listeleri, gelen/nakliye depo sevk listeleri ve depo siparis listelerinin iki yonudur.

27.10.2026 09:32-09:42 araliginda su iki script basariyla uygulandi:

1. `STOK_HAREKETLERI_LISTE_INDEXLERI.sql`
2. `DEPOLAR_ARASI_SIPARISLER_LISTE_INDEXLERI.sql`

Bu iki script yeniden calistirilabilir; mevcut indexleri adiyla kontrol eder ve yeniden olusturmaz. Uygulama `ONLINE = ON` ve `MAXDOP = 2` ile tamamlandi. Islem sonunda acik SQL blokaji bulunmadi.

## Canlida Mevcut ve Kullanilan Indexler

SQL Server kullanim sayaclari 20.10.2026 tarihindeki son servis acilisindan itibaren olusmustur.

| Index | Boyut | Seek / Scan | Degerlendirme |
| --- | ---: | ---: | --- |
| `IX_FR_STH_DepoSevkCikis_Liste` | 1.640 MB | 6.424 / 8.783 | Cok aktif, korunmali |
| `IX_FR_STH_StokFisi_Liste` | 267 MB | 2.602 / 28 | Aktif ve hedefli |
| `IX_FR_STH_DepoSevk_Duplicate` | 1.537 MB | 1.752 / 0 | Duplicate kontrolunde aktif |
| `IX_FR_STH_DepoSevk_Trace` | 242 MB | 88 / 0 | Safe-create trace icin gerekli |
| `IX_FR_STH_FirmaMalKabul_Trace` | 29 MB | 386 / 0 | Safe-create trace icin gerekli |
| `IX_FR_STH_StokFisi_Trace` | 110 MB | 65 / 0 | Safe-create trace icin gerekli |
| `IX_Furpa_SafeCreateTrace_WarehouseOrder` | 599 MB | 167 / 1.822 | Trace icin kullaniliyor; liste indexi degil |
| `IX_Furpa_SafeCreateTrace_CompanyOrder` | 41 MB | 2.045 / 0 | Aktif ve verimli |
| `IX_FR_Summaries_DateBranch` | 173 MB | 3.948 / 887 | Icmal sorgularinda aktif |
| `IX_FR_Summaries_Document` | 246 MB | 7.884 / 2.742 | Icmal detaylarinda aktif |
| `IX_FR_BanknoteMovements_Document` | 79 MB | 4.520 / 0 | Banknot detayinda aktif |
| `IX_FR_GiftCheckMovements_Document` | 9 MB | 3.750 / 0 | Hediye ceki detayinda aktif |
| `IX_FR_STHEK_SubeSipUid` | 259 MB | 993 / 0 | AXATA/Mikro baglantisinda aktif |
| `IX_FR_FID_EtiketFiyatDegisikligi` | 3.122 MB | 5.722 / 0 | Etiket sorgusunda aktif |

## Uygulanan Indexler

### 1. STOK_HAREKETLERI liste indexleri

`STOK_HAREKETLERI_LISTE_INDEXLERI.sql` canlida eksik olan su indexleri olusturdu:

- `IX_FR_STH_FirmaMalKabul_Liste`
- `IX_FR_STH_FirmaCikis_Liste`
- `IX_FR_STH_DepoSevkNakliye_Liste`
- `IX_FR_STH_DepoSevkGiris_Liste`
- `IX_FR_STH_Virman_Liste`

Kod sorgularindaki depo, belge tipi, hareket tipi, iade tipi ve tarih kosullari bu indexlerin anahtar sirasi ile uyumludur. Canli missing-index sayacinda firma mal kabul icin 273 seek/yuzde 92, firma cikis icin 148 seek/yuzde 98 civarinda iyilestirme sinyali bulunuyor. Gelen depo sevk sorgulari ise mevcut cikis indexini scan ederek calistigi icin giris ve nakliye indexleri gereklidir.

### 2. DEPOLAR_ARASI_SIPARISLER liste indexleri

`DEPOLAR_ARASI_SIPARISLER_LISTE_INDEXLERI.sql` su iki indexi olusturdu:

- `IX_FR_DAS_GirisTarih_Liste`
- `IX_FR_DAS_CikisTarih_Liste`

Canli DMV kaydi `ssip_girdepo + ssip_tarih` icin 3.004 seek ve yuzde 85 civarinda tahmini iyilestirme gosteriyor. Mevcut safe-create indexi `ssip_special1/2/3` ile basladigi icin depo + tarih liste sorgusunun yerine gecmiyor.

## Simdilik Eklenmemesi Gerekenler

### Fatura Gonderimi

- `IX_FR_CHA_FaturaGonderimi_Adres1` eklenmemeli. Canlida ayni anahtar sirali unique `NDX_CARI_HESAP_ADRESLERI_02 (adr_cari_kod, adr_adres_no)` zaten var ve 84 binden fazla seek almis.
- `IX_FR_STH_FaturaGonderimi_FatUid` simdilik eklenmemeli. Canlida `NDX_STOK_HAREKETLERI_07 (sth_fat_uid)` var, yaklasik 9,2 GB ve 18 binden fazla seek almis. Yeni genis covering index ancak olculmus bir key-lookup sorunu varsa dusunulmeli.
- `IX_FR_CHH_FaturaGonderimi_Liste` mevcut `IX_CHA_EFatura_Liste` ile tam ayni degil, ancak 62 milyon satirlik tabloda cok genis bir index olusturur. Temsili execution plan alinmadan uygulanmamali.

Bu nedenle `FATURA_GONDERIMI_LISTE_INDEXLERI.sql` su an topluca calistirilmamalidir.

### Shopigo yeni kasa ciro indexleri

- `payments.sale_uuid` mevcut indexi 948 binden fazla seek almis.
- `sale_items.sale_uuid` covering indexi 943 binden fazla seek almis.
- `received_sales` tarih tabanli filtreli indexi 7.200 seek almis.
- `payment_methods` yalniz 35 satir; ek index performans kazandirmayacak.

Bu nedenle `YENI_KASA_CIRO_INDEXLERI.sql` su an uygulanmamalidir. Mevcut yapida yalniz `sale_items` filtreli sorgusu icin dusuk seviyeli bir DMV sinyali var; bu sinyal yeni bir buyuk indexin yazma maliyetini hakli cikarmiyor.

## Diger DMV Sinyalleri

`STOK_SATIS_FIYAT_LISTELERI` icin guclu bir missing-index sinyali var. Bu sorgunun hangi API/uygulama akisindan geldigini belirlemeden index eklenmemeli; Mikro veya baska bir uygulamanin sorgusu olabilir. Ayri bir fiyat sorgusu performans calismasinda ele alinmalidir.

`CARI_HESAP_HAREKETLERI` icin cok genis kolon listeleri oneren DMV kayitlari bulunuyor. Bunlar ham haliyle uygulanmamalidir. Tablo 62 milyondan fazla satir ve her ek genis index create/update maliyetini arttirir.

## Uygulama Sonrasi

1. Once `DEPOLAR_ARASI_SIPARISLER_LISTE_INDEXLERI.sql` uygulandi.
2. Ardindan `STOK_HAREKETLERI_LISTE_INDEXLERI.sql` uygulandi.
3. Yedi indexin tamami etkin (`is_disabled = 0`) olarak dogrulandi.
4. Islem sonunda aktif SQL isteklerinde bloklanan oturum bulunmadi.
5. API liste sureleri ve SQL logical read degerleri en az bir is gunu izlenmelidir.
6. Fatura ve Shopigo indexleri ancak olculen sorgu plani ihtiyac gosterirse ayri bakimda ele alinmalidir.

Uygulama sonrasinda olusan boyutlar:

| Index | Boyut |
| --- | ---: |
| `IX_FR_DAS_CikisTarih_Liste` | 824,1 MB |
| `IX_FR_DAS_GirisTarih_Liste` | 824,4 MB |
| `IX_FR_STH_DepoSevkGiris_Liste` | 1.648,3 MB |
| `IX_FR_STH_DepoSevkNakliye_Liste` | 1.648,4 MB |
| `IX_FR_STH_FirmaCikis_Liste` | 143,1 MB |
| `IX_FR_STH_FirmaMalKabul_Liste` | 655,5 MB |
| `IX_FR_STH_Virman_Liste` | 61,6 MB |

## Canli Boyut Bilgisi

- `STOK_HAREKETLERI`: yaklasik 220 milyon satir.
- `CARI_HESAP_HAREKETLERI`: yaklasik 62 milyon satir.
- `DEPOLAR_ARASI_SIPARISLER`: yaklasik 8 milyon satir.
- Disk bos alani: yaklasik 983 GB.
- Mikro data dosyasi index build sonrasinda yaklasik 480.394 MB; ic bos alani yaklasik 24 MB.
- Mikro data/log autogrowth: 64 MB.

Disk kapasitesi yeterli olsa da data dosyasinin icinde bos alan az oldugu icin index build oncesi kontrollu pre-grow onemlidir.
