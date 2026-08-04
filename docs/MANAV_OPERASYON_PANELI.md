# Manav Operasyon Paneli

Bu dokuman `56 MANAV DEPO` icin eklenen GreenGrocer operasyon panelinin neden ve nasil calistigini anlatir. Panelin ana hedefi, manav deposunda alis, ic tartim farki, sube kasa talebi, sevk, sayim ve stok bilgisini ayni ekranda okunur hale getirmektir.

## Neyi Cozuyor?

Manav isinde subeler siparisi kasa/koli olarak verir. Mikro stok ana birimi ise urune gore KG veya ADET olabilir. Bu yuzden eski canli davranista subenin girdigi "1, 2, 3 kasa" bilgisi Mikro'da dogrudan ayni anlamla okunamaz; bazen tahmini KG/ADET olarak, bazen talep niyeti olarak durur.

Manav depo tarafinda ise halden gelen fatura miktari ile Furpa'nin kendi tarttigi gercek miktar her zaman birebir tutmayabilir. Canli sistemde bu farklar `MNV` serili `STOK_HAREKETLERI` ic hareketleri ile dengelenir.

Yeni panel bu resmi tek satira indirir:

- Hal/fatura ile gelen miktar
- Furpa ic tartim/MNV farki
- Subelerin kasa/koli talebi
- Siparis aninda hesaplanan tahmini KG/ADET
- Gercek sevk edilen KG/ADET
- Son sayim miktari
- Guncel Mikro stok miktari

## Canli Mantik

Canli DB analizinden cikan ana akil sudur:

- Alis faturasi Mikro'da olusur; API bu evragi olusturmaz.
- Manav depo kendi ic tartimini yapar.
- Fatura kg ile ic tartim arasindaki fark `MNV` serili ic hareketle stokta dengelenir.
- Sube siparisleri manavda kasa/koli talebi gibi okunur.
- Gercek sevk miktari etiket/terazi barkodu ile olusan KG/ADET miktaridir.
- Eski canli davranista manav sevki siparis satirini teslim kapatmaz.

`GreenGrocerProductCases:OrderLinkingEnabled=false` ise bu eski davranis korunur. `true` yapilirsa UI gercek siparis satiri GUID'ini sevke tasiyabilir ve sevk siparisle baglanabilir.

## Yeni API

Root:

```text
/api/green-grocer/operations
```

Endpointler:

- `GET /api/green-grocer/operations/overview`
- `GET /api/green-grocer/operations/ozet`
- `POST /api/green-grocer/operations/adjustments/preview`
- `POST /api/green-grocer/operations/duzeltmeler/onizleme`
- `POST /api/green-grocer/operations/adjustments`
- `POST /api/green-grocer/operations/duzeltmeler`

## Panelde Okunan Kaynaklar

`overview` endpointi urun bazli toplama yapar.

| Alan | Kaynak |
| --- | --- |
| Guncel stok | `dbo.fn_DepodakiMiktar(stokKodu, warehouseNo, endDate)` |
| Alis/fatura miktari | `STOK_HAREKETLERI`, giris depo 56, `sth_tip=0`, `sth_evraktip=3`, `sth_cins=16` |
| Ic tartim farki | `STOK_HAREKETLERI`, `sth_cins=10`, seri `MNV%` veya `MERC` |
| Sube kasa talebi | Auth DB `green_grocer_order_line_snapshots.input_quantity` |
| Tahmini KG/ADET | Auth DB `green_grocer_order_line_snapshots.estimated_quantity` |
| Mikro siparis miktari | `DEPOLAR_ARASI_SIPARISLER.ssip_miktar`, `ssip_cikdepo=56` |
| Gercek sevk | `STOK_HAREKETLERI`, cikis depo 56, `sth_tip=2`, `sth_evraktip=17`, `sth_cins=6` |
| Son sayim | `SAYIM_SONUCLARI` stok bazli son sayim |

## Yazma Ne Yapar?

Yazma yalnizca MNV tartim farki/stok duzeltmesi icindir.

Yazma su evraklari olusturmaz:

- Alis faturasi
- Depo siparisi
- Depolar arasi sevk
- Sayim sonucu

Yazma sadece `STOK_HAREKETLERI` uzerine ic hareket/duzeltme satiri ekler.

Artis sablonu:

```text
direction       increase
default seri    MNVE
sth_evraktip    12
sth_tip         0
sth_cins        10
giris depo      56
cikis depo      1
```

Azalis sablonu:

```text
direction       decrease
default seri    MNVF
sth_evraktip    0
sth_tip         1
sth_cins        10
giris depo      1
cikis depo      56
```

Seri kurali:

- `MNVE`, `MNVG`, `MNVI` sadece stok artisi icin kullanilir.
- `MNVF` sadece stok azalisi icin kullanilir.

## Idempotency

`POST /adjustments` requestinde `clientRequestId` zorunludur.

UI kurali:

- Kullanici kaydetmeye bastiginda bir GUID uretilir.
- Internet zayifligi veya timeout olursa ayni GUID ile tekrar denenir.
- Ayni is icin yeni GUID uretilmez.
- Kullanici bilerek yeni duzeltme olusturuyorsa yeni GUID uretilir.

Backend her satirin aciklamasina trace anahtari ekler. Timeout sonrasi ayni `clientRequestId` ile tekrar gelirse onceki hareketler bulunup ayni cevap toparlanmaya calisilir. Bu, "timeout oldu sandik ama Mikro'ya 10 kere ayni evrak yazildi" riskini azaltmak icindir.

## Yetki Modeli

| Yetki | Anlam |
| --- | --- |
| `green-grocer.operations.page` | Menu/route gorunurlugu |
| `green-grocer.operations.list` | Overview ve onizleme |
| `green-grocer.operations.create` | MNV duzeltme yazma |
| `green-grocer.operations.all-warehouses` | 56 disinda depo secme/yazma |

UI `page` yetkisi olmayan kullaniciya paneli gostermemelidir. `create` yetkisi olmayan kullanici paneli okuyabilir ama duzeltme kaydedemez.

## UI Akisi

1. Ekran acilir.
2. UI `GET /api/green-grocer/operations/overview?warehouseNo=56&startDate=...&endDate=...` cagirir.
3. Urun satirinda alis, MNV net fark, sube kasa talebi, tahmini KG/ADET, gercek sevk, son sayim ve guncel stok ayni satirda gosterilir.
4. Kullanici duzeltme yazacaksa stok, direction, miktar ve aciklama girer.
5. UI once `POST /adjustments/preview` cagirir.
6. Kullanici onaylarsa `POST /adjustments` cagrilir.
7. Basarili cevap veya timeout/retry toparlama sonrasinda panel yenilenir.

## Kapatma veya Eski Mantiga Donme

Bu panel permission kontrolludur. Eger ileride kullanilmak istenmezse:

- Role `green-grocer.operations.page` verilmez, ekran gorunmez.
- Yazma tamamen kapatilacaksa `green-grocer.operations.create` verilmez.
- Kasa/koli donusum kuralini kapatmak icin `GreenGrocerProductCases:Enabled=false` kullanilir.
- Siparise bagli manav sevk istenmiyorsa `GreenGrocerProductCases:OrderLinkingEnabled=false` kalir.

Bu ayarlar eski canli mantigi bozmadan yeni paneli kontrollu kullanmayi saglar.

## Sinirlar

- Panel Mikro/Furpa/Auth kaynaklarindan okur; farkli kaynaklardan gelen manuel Excel dosyalari bu kapsama girmez.
- Yazma sadece manav model kodlari `10`, `11`, `12`, `23` icin aciktir.
- Alis faturasi ve sevk evraklari hala kendi Mikro/API ekranlarindan yonetilir.
- Sayim sonucu yazma bu modulun isi degildir; sayim icin `stok-islemleri.sayim-sonuclari` kullanilir.
