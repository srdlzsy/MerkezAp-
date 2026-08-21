# STOK_HAREKETLERI Liste Indexleri

Bu dokuman, `STOK_HAREKETLERI` tablosu uzerinden calisan liste ekranlarini hizlandirmak icin hazirlanan index scriptinin ne zaman ve nasil kullanilacagini anlatir.

SQL script:

- `docs/STOK_HAREKETLERI_LISTE_INDEXLERI.sql`

## Amac

Bu indexler ozellikle tarih + depo + hareket tipi ile acilan liste ekranlarini hizlandirmak icindir.

Hedef ekranlar:

- Firma mal kabul listesi
- Firma sevk listesi
- Firma iade listesi
- Depolar arasi giden sevk listesi
- Depolar arasi gelen sevk listesi
- Depo mal kabul bekleyen/gelen listeleri
- Zayiat fisi listesi
- Masraf fisi listesi
- Virman listesi

## Neden Filtered Index

`STOK_HAREKETLERI` cok buyuk ve yogun yazilan bir Mikro tablosudur. Bu yuzden genel/genis index eklemek yerine sadece ilgili evrak tiplerini kapsayan `filtered index` kullanilir.

Bu yaklasim:

- Liste okumalarini hizlandirir.
- Tum tabloyu indexlemedigi icin disk maliyetini azaltir.
- Insert/update uzerindeki ek maliyeti daha sinirli tutar.
- Unique olmadigi icin duplicate key riski olusturmaz.

## Eklenen Indexler

| Index | Hedef |
|---|---|
| `IX_FR_STH_FirmaMalKabul_Liste` | Firma mal kabul listesi |
| `IX_FR_STH_FirmaCikis_Liste` | Firma sevk ve firma iade listesi |
| `IX_FR_STH_DepoSevkCikis_Liste` | Depolar arasi giden sevk listesi |
| `IX_FR_STH_DepoSevkNakliye_Liste` | Depo mal kabul bekleyen/gelen nakliye deposu filtresi |
| `IX_FR_STH_DepoSevkGiris_Liste` | Depolar arasi gelen sevk giris depo filtresi |
| `IX_FR_STH_StokFisi_Liste` | Zayiat ve masraf fisi listesi |
| `IX_FR_STH_Virman_Liste` | Virman listesi |

Depo sevk tarafinda birden fazla index olmasinin sebebi sorgularin farkli depo kolonlarini kullanmasidir:

```text
Giden sevk       sth_cikis_depo_no
Bekleyen kabul   sth_nakliyedeposu
Gelen sevk       sth_nakliyedeposu OR sth_giris_depo_no
```

Tek index bu uc farkli erisim yolunu ayni verimle karsilamaz.

## Uygulama Oncesi

Canliya gecmeden once onerilen kontrol:

1. Script test Mikro DB uzerinde calistirilir.
2. Ayni depo ve tarih araligi ile liste sureleri once/sonra olculur.
3. Ozellikle su endpointler kontrol edilir:

```text
GET /api/mal-kabul-islemleri/firma-mal-kabulleri
GET /api/sevk-islemleri/firma-sevkleri/giden
GET /api/iade-islemleri/firma-iadeleri
GET /api/sevk-islemleri/depolar-arasi-sevkler/giden
GET /api/sevk-islemleri/depolar-arasi-sevkler/gelen
GET /api/mal-kabul-islemleri/depo-mal-kabulleri
GET /api/stok-islemleri/zayiat-fisleri
GET /api/stok-islemleri/masraf-fisleri
GET /api/stok-islemleri/virmanlar
```

## Canlida Uygulama

Canli ortamda mesai disi veya dusuk kullanim saatinde uygulanmalidir.

Sebep:

- `CREATE INDEX` sirasinda tablo buyuklugune gore IO/CPU yuku olusabilir.
- Index olusturma tamamlanana kadar SQL Server uzerinde ek kaynak kullanimi olur.
- Veri kaybi riski yoktur, fakat yogun saatte calistirmak liste ve yazma islemlerini yavaslatabilir.

Script tekrar calistirilabilir sekilde yazilmistir. Index varsa tekrar olusturmaz.

## Geri Alma

Indexler beklenen faydayi vermezse veya yazma tarafinda kabul edilmeyen bir yavaslama olursa asagidaki script ile geri alinabilir.

```sql
DROP INDEX IF EXISTS IX_FR_STH_FirmaMalKabul_Liste
ON dbo.STOK_HAREKETLERI;

DROP INDEX IF EXISTS IX_FR_STH_FirmaCikis_Liste
ON dbo.STOK_HAREKETLERI;

DROP INDEX IF EXISTS IX_FR_STH_DepoSevkCikis_Liste
ON dbo.STOK_HAREKETLERI;

DROP INDEX IF EXISTS IX_FR_STH_DepoSevkNakliye_Liste
ON dbo.STOK_HAREKETLERI;

DROP INDEX IF EXISTS IX_FR_STH_DepoSevkGiris_Liste
ON dbo.STOK_HAREKETLERI;

DROP INDEX IF EXISTS IX_FR_STH_StokFisi_Liste
ON dbo.STOK_HAREKETLERI;

DROP INDEX IF EXISTS IX_FR_STH_Virman_Liste
ON dbo.STOK_HAREKETLERI;
```

Geri alma sadece indexleri kaldirir. Tablo verisini degistirmez.

## Izleme

Uygulamadan sonra su noktalar izlenmelidir:

- Liste endpoint sureleri
- SQL timeout loglari
- Sevk, mal kabul, iade, zayiat, masraf ve virman create sureleri
- SQL Server disk kullanimi
- SQL Server CPU/IO kullanimi

Indexler okuma tarafinda fayda saglarken her yeni `STOK_HAREKETLERI` kaydinda ek index bakimi yapilir. Bu beklenen bir maliyettir; filtered index oldugu icin maliyet genel indexlere gore daha sinirlidir.

## Not

Bu indexler liste performansi icin ilk kontrollu adimdir. Daha sonraki iyilestirmeler:

- Liste endpointlerine `take` / sayfalama eklemek
- Buyuk tarih araliklarini UI tarafinda sinirlamak
- `VirmanListQueryExecutor` gibi bellege alip gruplayan sorgulari SQL tarafinda gruplamak
- Liste ekranlarinda sadece gerekli kolonlari gostermek
