# Fatura Gonderimi Liste Indexleri

Bu not `GET /api/fatura-islemleri/fatura-gonderimi` liste ekraninin Mikro tarafinda
daha hizli gelmesi icin hazirlanan index scriptini aciklar.

Script:

```text
docs/FATURA_GONDERIMI_LISTE_INDEXLERI.sql
```

## Neyi Hizlandirir?

- `CARI_HESAP_HAREKETLERI` uzerindeki tarih, seri/sira ve gonderildi/gonderilmedi liste filtrelerini.
- Faturaya bagli irsaliye/depo bilgisini okuyan `STOK_HAREKETLERI.sth_fat_uid` aramasini.
- Fatura adresi icin kullanilan `CARI_HESAP_ADRESLERI` adres no 1 join'ini.
- Detay/send sirasinda fatura kalemlerinin `sth_fat_uid` ile okunmasini.

Kod tarafinda liste sorgusu da sadeleştirildi:

- Faturaya bagli sevkiyat bilgisi artik her cari hareket satirinda ayri `OUTER APPLY`
  ile aranmaz.
- Secilen fatura hareketleri icin `STOK_HAREKETLERI` bilgisi tek CTE ile okunur.
- Adres join'i `adr_adres_no = 1` filtresiyle daraltildi.

## Uygulama

Script Mikro DB uzerinde calistirilmalidir.

Canlida mumkunse mesai disinda uygulanmasi onerilir. Index olusturma sirasinda tablo
buyuklugune gore gecici CPU/IO yuku olabilir.

```sql
:r docs/FATURA_GONDERIMI_LISTE_INDEXLERI.sql
```

SSMS'te dosyayi acip dogrudan calistirmak da yeterlidir.

## Geri Alma

Sorun olursa indexler drop edilerek eski DB davranisina donulebilir. Veri silinmez,
yalnizca eklenen yardimci indexler kalkar.

```sql
DROP INDEX IF EXISTS IX_FR_CHH_FaturaGonderimi_Liste
ON dbo.CARI_HESAP_HAREKETLERI;

DROP INDEX IF EXISTS IX_FR_STH_FaturaGonderimi_FatUid
ON dbo.STOK_HAREKETLERI;

DROP INDEX IF EXISTS IX_FR_CHA_FaturaGonderimi_Adres1
ON dbo.CARI_HESAP_ADRESLERI;
```

## Maliyet

- Okuma hizlanir.
- Disk kullanimi artar.
- Fatura/cari hareket ve stok hareket insert/update islemlerinde cok az ek index yazma maliyeti olur.
- Index create sirasinda tablo buyukse gecici yuk olusabilir.

Bu indexler unique degildir; duplicate kayit veya belge numarasi kurali uretmez.
