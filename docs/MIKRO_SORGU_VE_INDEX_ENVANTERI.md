# Mikro Sorgu ve Index Envanteri

Bu dokuman, Furpa Merkez API'nin Mikro uzerindeki liste, detay ve create sonrasi
read-back sorgularini; bu sorgulari destekleyen indexleri ve `NOLOCK` politikasini
tek yerde toplar. Son canli kontrol 23.11.2026 tarihinde
`MikroDesktop_FURPA_2024`, `Furpa` ve `FurpaMerkezDb` veritabanlarinda
salt okunur sorgularla dogrulanmistir.

Tek yurutulebilir index kaynagi: `docs/UYGULAMA_INDEXLERI.sql`.
Parcali/eski index SQL dosyalari kullanilmaz.

## Kisa Sonuc

- Liste sorgularinin tamami SQL tarafinda filtreleniyor ve belge bazinda
  toplulastiriliyor. Firma hareket listesindeki karsi depo secimi ile virman
  listesi de artik tum kalemleri API bellegine tasimiyor.
- Detay sorgulari seri + sira + belge tipi ile daraliyor. Mikro'nun standart
  evrak indexleri bu erisim yolunu karsiliyor.
- Read-back sorgulari trace anahtari ve satir icerigini birlikte dogruluyor.
  Bu sorgulara `NOLOCK` eklenmemelidir.
- Canli veritabaninda `READ_COMMITTED_SNAPSHOT` kapali. Bu nedenle uzun liste
  sorgularinda kontrollu `ReadUncommitted` bloklamayi azaltir; ancak kirli,
  eksik, tekrarlanmis veya birbiriyle tutarsiz satir okuma ihtimali vardir.
- Yeni genis index eklenmedi. Firma mal kabul ve firma cikis listelerinin mevcut
  indexlerine yalniz `sth_satirno` include alani ekleyen guvenli upgrade
  05.11.2026 tarihinde canlida uygulandi.

## Sorgu Envanteri

### Liste sorgulari

| Alan | Ana tablo | Temel filtre / gruplama | Index | Sonuc |
|---|---|---|---|---|
| Firma mal kabul | `STOK_HAREKETLERI` | giris depo + `sth_tarih`, tip 13/0/normal | `IX_FR_STH_FirmaMalKabul_Liste` | SQL gruplama; ilk depo satiri SQL'de seciliyor |
| Firma sevk / iade | `STOK_HAREKETLERI` | cikis depo + iade tipi + belge tarihi, tip 1/1 | `IX_FR_STH_FirmaCikis_Liste` | SQL gruplama; ilk depo satiri SQL'de seciliyor |
| Giden depo sevk | `STOK_HAREKETLERI` | cikis depo + hareket tarihi, tip 17 | `IX_FR_STH_DepoSevkCikis_Liste` | SQL gruplama |
| Gelen / nakliyedeki sevk | `STOK_HAREKETLERI` | nakliye veya giris depo + hareket tarihi | `IX_FR_STH_DepoSevkNakliye_Liste`, `IX_FR_STH_DepoSevkGiris_Liste` | SQL gruplama |
| Zayiat / masraf | `STOK_HAREKETLERI` | cikis depo + cins + belge tarihi, tip 0/1 | `IX_FR_STH_StokFisi_Liste` | SQL gruplama |
| Virman | `STOK_HAREKETLERI` | cikis depo + belge tarihi, tip 6/cins 3 | `IX_FR_STH_Virman_Liste` | SQL belge + hareket tipi ozeti; API'ye belge basina en fazla iki satir gelir |
| Verilen / alinan depo siparisi | `DEPOLAR_ARASI_SIPARISLER` | giris veya cikis depo + siparis tarihi | `IX_FR_DAS_GirisTarih_Liste`, `IX_FR_DAS_CikisTarih_Liste` | SQL gruplama |
| Verilen / alinan firma siparisi | `SIPARISLER` | depo + tip/cins + tarih | Mikro standart siparis indexleri | SQL gruplama |
| Sayim sonucu | `SAYIM_SONUCLARI` | tarih + opsiyonel depo | `NDX_SAYIM_SONUCLARI_02` | SQL gruplama; yeni index icin yeterli kullanim kaniti yok |

`CompanyMovementListQueryExecutor` daha once ikinci bir sorguda tarih araligindaki
tum hareketleri alip C# tarafinda ilk depo satirini seciyordu. Artik SQL yalniz
belge basina ilk satiri dondurur. Bu nedenle mevcut firma liste indexlerinde
`sth_satirno` include alani gerekir.

`VirmanListQueryExecutor` daha once tum virman kalemlerini API'ye tasiyip C#
tarafinda grupluyordu. Artik SQL belge + hareket tipi bazinda sayi ve toplam
uretir; C# yalniz bu kucuk ozetleri tek belgeye birlestirir.

### Detay sorgulari

| Alan | Kimlik filtresi | Kullanilan erisim yolu | NOLOCK |
|---|---|---|---|
| Firma hareket detayi | seri + sira + evrak/hareket/iade tipi + depo | `NDX_STOK_HAREKETLERI_05` | Kullanilmaz |
| Depo sevk detayi | seri + sira + evrak tipi + depo | `NDX_STOK_HAREKETLERI_05` | Kullanilmaz |
| Zayiat / masraf detayi | seri + sira + evrak/hareket/cins + depo | `NDX_STOK_HAREKETLERI_05` | Kullanilmaz |
| Virman detayi | seri + sira + evrak/cins + depo | `NDX_STOK_HAREKETLERI_05` | Kullanilmaz |
| Depo siparis detayi | seri + sira + yon deposu | `NDX_DEPOLAR_ARASI_SIPARISLER_05` | Kullanilmaz |
| Firma siparis detayi | tip + cins + seri + sira + depo | `NDX_SIPARISLER_06` | Kullanilmaz |
| Sayim detayi | depo + evrak no + tek gun | `NDX_SAYIM_SONUCLARI_02` | Kullanilmaz |

Detay sorgulari bir evrakin tum kalemlerini tutarli gostermek zorundadir. Kirli
okuma kullanilirsa ayni cevap icinde header ve satir toplamlarinin farkli commit
anlarindan gelmesi mumkundur.

### Create read-back ve recovery sorgulari

| Akis | Birinci arama | Ikinci dogrulama | Index |
|---|---|---|---|
| Firma mal kabul | `sth_eticaret_kanal_kodu` trace | seri/sira ile tum satir icerigi | `IX_FR_STH_FirmaMalKabul_Trace`, `NDX_STOK_HAREKETLERI_05` |
| Firma sevk / iade | trace ve belge kimligi | stok, miktar, depo, cari, satir sayisi | standart evrak indexi + liste indexi |
| Depo sevk / iade | trace anahtari | seri/sira ve tam satir eslesmesi | `IX_FR_STH_DepoSevk_Trace`, `IX_FR_STH_DepoIade_Trace`, `IX_FR_STH_DepoSevk_Duplicate` |
| Zayiat / masraf | trace anahtari | seri/sira ve kalem eslesmesi | `IX_FR_STH_StokFisi_Trace`, `NDX_STOK_HAREKETLERI_05` |
| Virman | trace anahtari | giris/cikis ciftleri ve kalem eslesmesi | stok hareket trace/standart evrak erisim yolu |
| Depo siparisi | `ssip_special1/2/3` trace | seri/sira ve satir eslesmesi | `IX_Furpa_SafeCreateTrace_WarehouseOrder`, `NDX_DEPOLAR_ARASI_SIPARISLER_05` |
| Firma siparisi | `sip_eticaret_kanal_kodu` trace | seri/sira ve satir eslesmesi | `IX_Furpa_SafeCreateTrace_CompanyOrder`, `NDX_SIPARISLER_06` |

Read-back sorgulari API timeout sonrasinda Mikro kaydinin gercekten olusup
olusmadigina karar verir. `NOLOCK` burada yarim evraki tamamlanmis veya tamamlanmis
evraki yokmus gibi gosterebilir. Sonuc duplicate belge ya da gereksiz destek
durumu olabilecegi icin bu akislar `ReadCommitted` kalmalidir.

## NOLOCK Karari

| Sorgu turu | Karar | Gerekce |
|---|---|---|
| Salt okunur liste | Yalniz kontrollu olarak kullanilabilir | Kisa sureli goruntu sapmasi kabul edilebilir; yazan Mikro kullanicilarini bloke etmez |
| Detay | Kullanilmaz | Header, satir ve toplamlar ayni tutarli veriyi temsil etmeli |
| Create read-back / recovery | Kesinlikle kullanilmaz | Duplicate onleme karari kirli okumaya dayanmamali |
| Numara uretme / duplicate kontrolu | Kesinlikle kullanilmaz | Eszamanli kayit guvenligini bozar |
| Rapor / finansal mutabakat | Varsayilan olarak kullanilmaz | Toplamlar commit edilmis veriyle hesaplanmali |

`NOLOCK`, kotu execution planini veya taramayi hizli bir seek'e cevirmez. Yalniz
kilit beklemesini azaltir. Kalici cozum; secici filtre, SQL tarafinda gruplama ve
sorguyla ayni anahtar sirasina sahip dar index kullanmaktir.

Kodda su anda yalniz `CompanyMovementListQueryExecutor`,
`WarehouseShippingListQueryExecutor` ve `StockReceiptListQueryExecutor` SQL
Server uzerinde kisa omurlu `ReadUncommitted` transaction kullanir. Bunlar
operasyonel liste goruntuleridir. Detay ve recovery siniflarinda bu izolasyon
kullanilmaz; tarama bu ayrimi da dogrulamistir.

## Canli Mikro Indexleri

Kullanim sayaclari SQL Server'in 05.11.2026 08:57 acilisindan sonraki degerlerdir;
uzun donem kullanim karari icin tek basina yeterli degildir.

| Index | Tablo | Boyut MB | Seek / Scan | Amac | Durum |
|---|---|---:|---:|---|---|
| `IX_FR_STH_FirmaMalKabul_Liste` | STOK_HAREKETLERI | 684.5 | Rebuild sonrasi 8 / 0 | Firma mal kabul listesi | Aktif; `sth_satirno` include uygulandi |
| `IX_FR_STH_FirmaCikis_Liste` | STOK_HAREKETLERI | 149.8 | Rebuild sonrasi 10 / 0 | Firma sevk/iade listesi | Aktif; `sth_satirno` include uygulandi |
| `IX_FR_STH_FirmaMalKabul_Trace` | STOK_HAREKETLERI | 27.5 | 5 / 0 | Mal kabul recovery trace | Aktif |
| `IX_FR_STH_DepoSevkCikis_Liste` | STOK_HAREKETLERI | 1640.3 | 155 / 0 | Giden sevk listesi | Aktif |
| `IX_FR_STH_DepoSevkNakliye_Liste` | STOK_HAREKETLERI | 1647.8 | 44 / 13 | Nakliyedeki/gelen sevk | Aktif |
| `IX_FR_STH_DepoSevkGiris_Liste` | STOK_HAREKETLERI | 1648.6 | 15 / 0 | Gelen sevk giris deposu | Aktif |
| `IX_FR_STH_DepoSevk_Trace` | STOK_HAREKETLERI | 263.5 | 1 / 0 | Depo sevk recovery trace | Aktif |
| `IX_FR_STH_DepoSevk_Duplicate` | STOK_HAREKETLERI | 1537.4 | 19 / 0 | Son create duplicate kontrolu | Aktif |
| `IX_FR_STH_StokFisi_Liste` | STOK_HAREKETLERI | 267.2 | 30 / 0 | Zayiat/masraf listesi | Aktif |
| `IX_FR_STH_StokFisi_Trace` | STOK_HAREKETLERI | 112.9 | 0 / 0 | Zayiat/masraf recovery | Aktif, kisa sayac penceresi |
| `IX_FR_STH_Virman_Liste` | STOK_HAREKETLERI | 61.7 | 11 / 0 | Virman listesi | Aktif |
| `IX_FR_STH_AXATA_G01_SipUid` | STOK_HAREKETLERI | Canli sorgudan okunur | Yeni | AXATA G01 siparis GUID read-back | Aktif |
| `IX_FR_DAS_GirisTarih_Liste` | DEPOLAR_ARASI_SIPARISLER | 825.2 | 46 / 0 | Verilen depo siparisi | Aktif |
| `IX_FR_DAS_CikisTarih_Liste` | DEPOLAR_ARASI_SIPARISLER | 824.8 | 14 / 0 | Alinan depo siparisi | Aktif |
| `IX_Furpa_SafeCreateTrace_WarehouseOrder` | DEPOLAR_ARASI_SIPARISLER | 607.5 | 0 / 0 | Depo siparisi trace | Aktif, kisa sayac penceresi |
| `IX_Furpa_SafeCreateTrace_CompanyOrder` | SIPARISLER | 41.3 | 4 / 0 | Firma siparisi trace | Aktif |
| `IX_FR_STHEK_SubeSipUid` | STOK_HAREKETLERI_EK | 268.9 | 2 / 0 | Siparis-sevk baglantisi | Aktif |
| `IX_FR_FID_EtiketFiyatDegisikligi` | STOK_FIYAT_DEGISIKLIKLERI | 3133.8 | 104 / 0 | Etiket fiyat degisikligi | Aktif |

Mikro'nun mevcut standart indexlerinden uygulamanin kritik olarak kullandiklari:

| Index | Boyut MB | Seek / Scan | Kullanim |
|---|---:|---:|---|
| `NDX_STOK_HAREKETLERI_05` | 9867.0 | 1921 / 0 | Stok hareket detaylari, seri/sira read-back |
| `NDX_STOK_HAREKETLERI_07` | 9794.6 | 172 / 0 | Fatura UID ile stok satirlari |
| `NDX_DEPOLAR_ARASI_SIPARISLER_05` | 339.1 | 73 / 0 | Depo siparisi detayi |
| `NDX_SIPARISLER_06` | 23.3 | 15 / 0 | Firma siparisi detayi |
| `NDX_SAYIM_SONUCLARI_02` | 52.2 | 4 / 0 | Sayim tarih/depo/evrak detayi |
| `NDX_CARI_HESAP_ADRESLERI_02` | 22.2 | 1013 / 3 | Cari adres joini |

## Diger Veritabanlarindaki Furpa Indexleri

| Veritabani | Index | Durum / amac |
|---|---|---|
| Furpa | `IX_FR_Summaries_DateBranch` | Aktif; icmal tarih/sube listesi |
| Furpa | `IX_FR_Summaries_Document` | Aktif; icmal belge detayi |
| Furpa | `IX_FR_BanknoteMovements_Document` | Aktif; banknot belge detayi |
| Furpa | `IX_FR_GiftCheckMovements_Document` | Aktif; hediye ceki belge detayi |
| Furpa | `IX_CashRegisterDetails_CashNo_PaymentTypeNo` | Aktif; kasa/POS odeme tipi eslesmesi |
| FurpaMerkezDb | `IX_edespatch_submissions_document_key` | Aktif, unique; belge kimligi |
| FurpaMerkezDb | `IX_edespatch_submissions_document_no` | Aktif, unique; belge numarasi |
| FurpaMerkezDb | `IX_edespatch_submissions_status_next_attempt_at_utc` | Aktif; worker kuyrugu |
| FurpaMerkezDb | `IX_edespatch_submissions_uuid` | Aktif, unique; e-belge UUID |

## Tek Index Katalogu

`UYGULAMA_INDEXLERI.sql` su uc veritabanindaki uygulama indexlerini tek dosyada
tutar:

| Veritabani | Kapsam |
|---|---|
| `MikroDesktop_FURPA_2024` | Firma/depo hareket listeleri, safe-create, read-back, AXATA G01 ve etiket sorgulari |
| `Furpa` | Kasa icmal, banknot, hediye ceki ve kasa/POS odeme tipi sorgulari |
| `FurpaMerkezDb` | E-irsaliye gonderim kuyrugu |

Script idempotenttir. Mevcut indexi adiyla kontrol eder; veri ve prosedur
degistirmez. `ONLINE = ON`, dusuk `MAXDOP`, dusuk deadlock onceligi ve sinirli
lock beklemesi kullanir. Buyuk Mikro tablolarinda yine de CPU, IO ve transaction
log maliyeti olacagi icin yalniz dusuk trafik saatinde calistirilir.

Fatura gonderimi ve SHOPIGO icin daha once hazirlanan ancak canlida ihtiyaci
kanitlanmayan genis index onerileri katalogdan kaldirildi. Mikro'nun
`NDX_STOK_HAREKETLERI_07`, `NDX_CARI_HESAP_ADRESLERI_02` ve SHOPIGO'nun mevcut
indexleri bu yollar icin yeterlidir. Yeni index ancak gercek execution plan,
tekrar eden sorgu ve olculmus logical-read kaniti ile eklenir.

Muhasebe duzeltme scriptlerindeki gecici tablo indexleri bu katalogda yer almaz;
yalniz ilgili duzeltme oturumu boyunca yasarlar.

## Uygulama ve Izleme

1. `docs/UYGULAMA_INDEXLERI.sql` SSMS veya sqlcmd ile 10.0.0.207 sunucusunda
   dusuk trafik saatinde calistirilir.
2. Script sonundaki tek rapor sonucu `is_disabled = 0` olacak sekilde kontrol
   edilir.
3. Liste endpoint sureleri, timeout loglari, logical read, SQL CPU/IO ve disk
   kullanimi en az bir is gunu izlenir.
4. Faydasi kanitlanmayan index kaldirilacaksa once kullanim sayaci ve execution
   plan saklanir; yalniz index kaldirilir, tablo verisine dokunulmaz.
