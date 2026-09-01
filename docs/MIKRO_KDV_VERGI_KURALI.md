# Mikro KDV / Vergi Pointer Kurali

Bu not, Furpa API'nin Mikro `STOK_HAREKETLERI` satirlarina KDV bilgisini nasil yazdigini kisaca aciklar.

## Mikro'da Temel Mantik

Mikro stok kartinda KDV orani dogrudan yuzde olarak tutulmaz. Stok kartinda vergi tanimina giden bir pointer/kod tutulur.

Baslica alanlar:

| Tablo | Alan | Anlam |
|---|---|---|
| `STOKLAR` | `sto_perakende_vergi` | Stok kartindaki perakende KDV pointer'i |
| `STOKLAR` | `sto_toptan_vergi` | Stok kartindaki toptan KDV pointer'i |
| `STOK_HAREKETLERI` | `sth_vergi_pntr` | Hareket satirina yazilan KDV pointer'i |
| `STOK_HAREKETLERI` | `sth_vergi` | Hareket satirindaki KDV tutari |
| `STOK_HAREKETLERI` | `sth_tutar` | Hareket satirindaki mal tutari |

Pointer oran degildir. Oran, Mikro tarafinda vergi tanim listesinden cozulur:

```sql
SELECT *
FROM dbo.fn_hs_vergi_oran_listesi();
```

Ornek:

```text
sto_perakende_vergi = 4
fn_hs_vergi_oran_listesi() icinde 4 -> %10 ise
sth_vergi_pntr = 4
sth_vergi = sth_tutar * 10 / 100
```

## Furpa API Kurali

Firma hareketlerinde su an canli Mikro davranisina uygun olarak perakende vergi pointer'i esas alinir:

| Akis | Kullanilan pointer |
|---|---|
| Firma sevk | `STOKLAR.sto_perakende_vergi` |
| Firma iade | `STOKLAR.sto_perakende_vergi` |
| Firma mal kabul | `STOKLAR.sto_perakende_vergi` |

API hareket satirini yazarken:

1. Satirdaki `stockCode` ile `STOKLAR` kaydini bulur.
2. `sto_perakende_vergi` degerini alir.
3. `dbo.fn_hs_vergi_oran_listesi()` ile pointer'in yuzde oranini bulur.
4. `STOK_HAREKETLERI.sth_vergi_pntr` alanina pointer'i yazar.
5. `STOK_HAREKETLERI.sth_vergi` alanina hesaplanan KDV tutarini yazar.

Hesap:

```text
sth_vergi = ROUND(sth_tutar * vergiOrani / 100, 2)
```

## Dikkat Edilecek Hata

Mikro'da "vergi tanimsiz" gibi hata gorulurse ilk bakilacak yerler:

- `sth_vergi_pntr = 0` mi?
- `sth_vergi_pntr`, `fn_hs_vergi_oran_listesi()` icinde tanimli mi?
- Stok kartinda `sto_perakende_vergi` bos/0 mi?
- `sth_vergi`, satir tutarina ve orana gore dogru hesaplanmis mi?

Ozetle bizim hareket satirinda `sth_vergi_pntr` bos kalmamali; stok kartindaki perakende pointer ile dolmali.
