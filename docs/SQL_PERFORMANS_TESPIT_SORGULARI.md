# SQL Performans Tespit Sorgulari

Bu dokuman, Mikro DB tarafinda liste indexlerini kontrol etmek ve create/yazma yavasliklarini anlik incelemek icin kullanilacak temel sorgulari toplar.

Sorgular Mikro veritabaninda, ornek olarak `MikroDB_V16_FURPA_2024` uzerinde calistirilmalidir.

## 1. STOK_HAREKETLERI Indexlerini Gorme

Bu sorgu `STOK_HAREKETLERI` uzerindeki Furpa liste indexlerinin aktif olup olmadigini, filtered index kosullarini, key kolonlarini ve include kolonlarini gosterir.

Ne ise yarar:

- Index var mi yok mu kontrol edilir.
- `is_disabled = 0` ise index aktiftir.
- `has_filter = 1` ise filtered index oldugunu gosterir.
- `key_ordinal > 0` olan kolonlar asil index key kolonlaridir.
- `is_included_column = 1` olan kolonlar sadece query sonucu icin indexe eklenmis include kolonlaridir.
- Firma mal kabul icin `IX_FR_STH_FirmaMalKabul_Liste` indexinde `key_ordinal = 2` kolonunun `sth_tarih` olmasi beklenir.

```sql
SELECT
    i.name,
    i.is_disabled,
    i.has_filter,
    i.filter_definition,
    ic.key_ordinal,
    ic.is_included_column,
    c.name AS column_name
FROM sys.indexes AS i
INNER JOIN sys.index_columns AS ic
    ON ic.object_id = i.object_id
   AND ic.index_id = i.index_id
INNER JOIN sys.columns AS c
    ON c.object_id = ic.object_id
   AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
  AND i.name LIKE N'IX_FR_STH_%'
ORDER BY
    i.name,
    ic.key_ordinal,
    ic.is_included_column,
    ic.index_column_id;
```

Beklenen ana indexler:

```text
IX_FR_STH_FirmaMalKabul_Liste
IX_FR_STH_FirmaCikis_Liste
IX_FR_STH_DepoSevkCikis_Liste
IX_FR_STH_DepoSevkNakliye_Liste
IX_FR_STH_DepoSevkGiris_Liste
IX_FR_STH_StokFisi_Liste
IX_FR_STH_Virman_Liste
```

## 2. Index Kullanimini ve Yazma Maliyetini Gorme

Bu sorgu indexlerin ne kadar okuma faydasi verdigini ve insert/update/delete sirasinda ne kadar bakim maliyeti aldigini gosterir.

Ne ise yarar:

- `user_seeks`: Sorgular indexi hedefli sekilde kullanmis. Bu iyi sinyaldir.
- `user_scans`: Index taranmis. Kotu olmak zorunda degildir ama seek kadar net degildir.
- `user_lookups`: Bu indexlerden sonra ek lookup yapilmis mi gosterir.
- `user_updates`: Tabloya yazma geldikce indexin kac kez guncellendigini gosterir.
- `last_user_seek` ve `last_user_scan`: Index en son ne zaman okuma icin kullanildi.
- `last_user_update`: Index en son ne zaman yazma maliyeti aldi.

Yorumlama:

- `user_seeks` yuksek, `user_updates` yuksek ise index calisiyor ve yazma maliyeti kabul edilebilir olabilir.
- `user_seeks/scans` cok dusuk, `user_updates` cok yuksek ise index az fayda verip yazmaya yuk bindiriyor olabilir.
- Bu sorgudaki sayaclar SQL Server restart/service restart sonrasi sifirlanabilir; tek anlik bakisla hemen drop karari verilmemelidir.

```sql
SELECT
    i.name,
    COALESCE(us.user_seeks, 0) AS user_seeks,
    COALESCE(us.user_scans, 0) AS user_scans,
    COALESCE(us.user_lookups, 0) AS user_lookups,
    COALESCE(us.user_updates, 0) AS user_updates,
    us.last_user_seek,
    us.last_user_scan,
    us.last_user_update
FROM sys.indexes AS i
LEFT JOIN sys.dm_db_index_usage_stats AS us
    ON us.database_id = DB_ID()
   AND us.object_id = i.object_id
   AND us.index_id = i.index_id
WHERE i.object_id = OBJECT_ID(N'dbo.STOK_HAREKETLERI')
  AND i.name LIKE N'IX_FR_STH_%'
ORDER BY
    COALESCE(us.user_updates, 0) DESC,
    COALESCE(us.user_seeks, 0) + COALESCE(us.user_scans, 0) DESC;
```

Pratik karar notu:

- Depolar arasi sevk listelerinde kullanilan `DepoSevk*` indexleri cok kullaniliyorsa tutulmalidir.
- Firma mal kabul listesi hizli calisiyorsa `IX_FR_STH_FirmaMalKabul_Liste` tutulmalidir.
- `FirmaCikis`, `StokFisi` veya `Virman` indexleri uzun sure cok az okunup cok fazla update aliyorsa mesai disi testle kaldirma degerlendirilebilir.

## 3. Yavaslik Aninda Wait ve Blocking Tespiti

Bu sorgu o anda SQL Server icinde calisan istekleri, bekleme tiplerini ve varsa hangi session tarafindan bloklandigini gosterir.

Ne ise yarar:

- Create/kaydetme 60-120 saniye surerken calistirilir.
- `blocking_session_id` doluysa bir session digerini kilitlemistir.
- `wait_type` bekleme sebebini verir.
- `running_statement` sadece calisan aktif statement'i gosterir.
- `full_sql` sorgunun tamamini gosterir.

```sql
SELECT
    r.session_id,
    r.status,
    r.command,
    r.wait_type,
    r.wait_time,
    r.blocking_session_id,
    r.cpu_time,
    r.logical_reads,
    r.reads,
    r.writes,
    r.total_elapsed_time,
    DB_NAME(r.database_id) AS database_name,
    SUBSTRING(
        t.text,
        (r.statement_start_offset / 2) + 1,
        CASE
            WHEN r.statement_end_offset = -1
                THEN LEN(CONVERT(nvarchar(max), t.text))
            ELSE (r.statement_end_offset - r.statement_start_offset) / 2 + 1
        END
    ) AS running_statement,
    t.text AS full_sql
FROM sys.dm_exec_requests AS r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) AS t
WHERE r.database_id = DB_ID()
ORDER BY
    r.wait_time DESC,
    r.total_elapsed_time DESC;
```

Bekleme tipleri icin hizli yorum:

```text
LCK_M_*        Baska islem kilit tutuyor olabilir.
WRITELOG       Transaction log yazimi yavas veya yogun olabilir.
PAGEIOLATCH_*  Diskten sayfa okuma/yazma bekleniyor olabilir.
PAGELATCH_*    Bellek ici hot page/tempdb/index baskisi olabilir.
ASYNC_NETWORK_IO Istemci sonucu yavas okuyordur.
NULL / 0       O anda bekleme yakalanmamis olabilir.
```

Ornek yorum:

- `blocking_session_id = 0`, `wait_type = NULL`, `wait_time = 0` ise o an icin net kilit/bekleme yoktur.
- `INSERT INTO STOK_HAREKETLERI` satiri gorunup `wait_type = LCK_M_*` ise create baska islemden kilit bekliyordur.
- `INSERT INTO STOK_HAREKETLERI` satiri gorunup `wait_type = WRITELOG` ise log/disk tarafina bakilmalidir.
- `fn_DepodakiMiktar`, `fn_StokFoy` gibi agir okuma fonksiyonlari calisiyor ama blocking yoksa bunlar sistemi yorabilir, fakat dogrudan kilit sebebi olmayabilir.

## 4. Blocking Varsa Bloklayan Session'i Bulma

Onceki sorguda `blocking_session_id` dolu gelirse, bloklayan session'in ne calistirdigini gormek icin bu sorgu kullanilir.

```sql
DECLARE @blockingSessionId int = 0; -- Buraya blocking_session_id degeri yazilir.

SELECT
    r.session_id,
    r.status,
    r.command,
    r.wait_type,
    r.wait_time,
    r.blocking_session_id,
    r.cpu_time,
    r.logical_reads,
    r.reads,
    r.writes,
    r.total_elapsed_time,
    DB_NAME(r.database_id) AS database_name,
    s.host_name,
    s.program_name,
    s.login_name,
    t.text AS full_sql
FROM sys.dm_exec_sessions AS s
LEFT JOIN sys.dm_exec_requests AS r
    ON r.session_id = s.session_id
OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) AS t
WHERE s.session_id = @blockingSessionId;
```

Not:

- `@blockingSessionId` degerini onceki sorguda gorunen `blocking_session_id` ile degistir.
- Cikti bos veya SQL null gelirse session istek tamamlamis olabilir; yavaslik aninda tekrar calistirilmalidir.

## 5. Kullanim Sekli

Normal kontrol:

1. Once index yapisi sorgusu calistirilir.
2. Sonra index usage sorgusu calistirilir.
3. Okuma faydasi olmayan ama cok update alan indexler not edilir.

Yavas create aninda:

1. Kullanici kaydetmeye basar ve istek 30 saniyeyi gecerse wait/blocking sorgusu calistirilir.
2. `STOK_HAREKETLERI INSERT` satiri, `wait_type` ve `blocking_session_id` birlikte okunur.
3. Blocking varsa bloklayan session sorgusu ile sebep bulunur.
4. Sonuc log saati, API endpoint'i ve evrak seri/sira bilgisiyle birlikte not edilir.

Karar verirken:

- Tek anlik ciktidan index silinmez.
- En az bir yogun saat ve bir sakin saat karsilastirilir.
- Liste hizlari iyi, create yavasligi sadece yogun anda cikiyorsa once blocking/wait sebebi aranir.
- Index kaldirma gerekiyorsa mesai disi denenir ve liste/create sureleri tekrar olculur.
