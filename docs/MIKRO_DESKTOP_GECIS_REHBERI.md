# Mikro Desktop Veritabani Gecis Rehberi

Hedef veritabani: `MikroDesktop_FURPA_2024`

Eski veritabani: `MikroDB_V16_FURPA_2024`

Mikro API firma kodu: `FURPA_2024`

## Hazir Durum

- Kaynak `appsettings.Production.json` ve Production'dan sonra yuklenen `appsettings.Local.json`, Mikro okuma, yazma ve `MikroDatabase:ExpectedDatabase` icin `MikroDesktop_FURPA_2024` kullanir.
- Uygulama Production baslangicinda okuma ve yazma connection string'lerinin ayni DB'yi gosterdigini ve bu DB'nin `ExpectedDatabase` ile eslestigini kontrol eder. Uyusmazlikta uygulama acilmaz.
- Mikro API `FURPA_2024` firma eslemesi uygulama ayarindan bagimsizdir. Bu esleme Mikro API sunucusunda ayrica `MikroDesktop_FURPA_2024` yapilmalidir.
- `tools/Set-FurpaMikroDatabaseReferences.ps1`, Furpa DB icindeki eski veya yeni DB adini kullanan tum `dbo` stored procedure'leri otomatik bulur. Yalniz uc sabit prosedurle sinirli degildir.
- Son envanterde eski DB'ye bagli 14 Furpa proseduru bulunmustur. Arac calisma aninda yeniden tarama yaptigi icin listeye sonradan eklenen prosedurler de atlanmaz.
- Hedef DB ve prosedurlerin statik olarak baglandigi hedef tablo, view ve fonksiyonlar dogrulanmadan degisiklik yapilmaz.
- Prosedur donusumu tek SQL transaction'i icinde yapilir. Her degisen modul `sp_refreshsqlmodule` ile dogrulanir; hata halinde transaction geri alinir.

## On Kontrol

SQL Server ve Mikro API sunucusu acikken, degisiklik yapmadan kontrol:

```powershell
.\tools\Switch-MikroDatabase.ps1 `
  -Target Desktop `
  -ConfigurationPath 'C:\inetpub\FurpaMerkezApi\appsettings.Local.json' `
  -SiteName 'FurpaMerkez' `
  -HealthBaseUrl 'http://10.0.0.100:7508'
```

Bu komut:

- etkin Mikro okuma/yazma ayarini gosterir,
- Furpa DB'deki eski DB referanslarini sayar,
- Mikro API uzerinden yazilan route sayisini gosterir,
- IIS, JSON veya SQL nesnelerini degistirmez.

Sunucu `10.0.0.207` kapaliysa bu kontrolun baglanti hatasi vermesi normaldir. Sunucu kapaliyken `-Apply` kullanilmaz.

## Canli Gecis

1. SQL Server ve Mikro API servislerini acin.
2. Yeni create isteklerini durdurun; devam eden Mikro yazmalari ve readback islemleri bitene kadar bekleyin.
3. Mikro API yonetiminde `FURPA_2024` firma kodunun veritabani eslemesini `MikroDesktop_FURPA_2024` yapin.
4. Yeni DB'nin guncel oldugunu ve gerekli yedegin alindigini kontrol edin.
5. Yayin sunucusunda yonetici PowerShell ile tek kontrollu yayin komutunu calistirin:

```powershell
.\tools\Publish-Iis.ps1 `
  -SiteName 'FurpaMerkez' `
  -HealthBaseUrl 'http://10.0.0.100:7508' `
  -MikroDatabaseTarget Desktop `
  -ConfirmMikroApiMapping
```

`-ConfirmMikroApiMapping`, operatorun Mikro API tarafindaki harici firma eslemesini gercekten degistirdigini beyan eder. Bu parametre olmadan Desktop gecisi uygulanmaz.

Kontrollu yayin sirasi:

1. IIS app pool durdurulur.
2. Release publish alinir ve `appsettings.Local.json` canli klasore kopyalanir.
3. IIS `AlwaysRunning`, preload ve `idleTimeout=0` ayarlari uygulanir.
4. Etkin ayar dosyasi secilir. `appsettings.Local.json` varsa Production ayarini ezdigi icin gecis bu dosya uzerinden yapilir.
5. Furpa DB'deki tum eski Mikro DB prosedur referanslari bulunur ve transaction icinde Desktop DB'ye cevrilir.
6. Mikro okuma, yazma ve beklenen DB alanlari Desktop hedefiyle tekrar dogrulanir.
7. IIS baslatilir.
8. `GET /` cevabindaki `businessDatabase` alaninin `MikroDesktop_FURPA_2024` oldugu dogrulanir.
9. `/health/ready` basarili olmadan yayin tamamlanmis sayilmaz.

SQL donusumu, ayar yazimi veya uygulama DB dogrulamasi hata verirse uygulama havuzu acik birakilmaz. Bu davranis eski ve yeni DB'ye ayni anda yazilmasini engeller.

## Manuel SQL Araci

Yalniz Furpa prosedurlerini incelemek icin:

```powershell
.\tools\Set-FurpaMikroDatabaseReferences.ps1 `
  -ConfigurationPath 'C:\inetpub\FurpaMerkezApi\appsettings.Local.json' `
  -SourceDatabase 'MikroDB_V16_FURPA_2024' `
  -TargetDatabase 'MikroDesktop_FURPA_2024'
```

Degisiklikleri transaction icinde deneyip kalici yazmadan geri almak icin:

```powershell
.\tools\Set-FurpaMikroDatabaseReferences.ps1 `
  -ConfigurationPath 'C:\inetpub\FurpaMerkezApi\appsettings.Local.json' `
  -SourceDatabase 'MikroDB_V16_FURPA_2024' `
  -TargetDatabase 'MikroDesktop_FURPA_2024' `
  -Apply `
  -RollbackAfterValidation
```

Kalici uygulama normalde `Publish-Iis.ps1` veya `Switch-MikroDatabase.ps1` uzerinden yapilmalidir. Boylece SQL degisikligi uygulamanin gercek DB hedefi ve health kontroluyle birlikte yonetilir.

## Smoke Test

Gecis sonrasi sirasiyla:

```powershell
$root = Invoke-RestMethod 'http://10.0.0.100:7508/'
$root.businessDatabase
Invoke-WebRequest 'http://10.0.0.100:7508/health/ready' -UseBasicParsing
```

Beklenen `businessDatabase`: `MikroDesktop_FURPA_2024`

Ardindan:

1. Bir salt-okuma stok veya belge listesi acin.
2. Kontrollu tek satirli bir create islemi yapin.
3. Response ve readback sonucundaki seri, sira, satir sayisi ve trace bilgisini kontrol edin.
4. Mikro API audit kaydinin `Verified` veya gerekiyorsa `RecoveredAfterUnknown` oldugunu kontrol edin.
5. Furpa prosedurlerinden calisan ilgili operasyon icin yeni DB'de veri olustugunu kontrol edin.

## Geri Donus

Geri donus karari verilirse once Mikro API tarafinda `FURPA_2024` firma eslemesini tekrar `MikroDB_V16_FURPA_2024` yapin. Sonra:

```powershell
.\tools\Switch-MikroDatabase.ps1 `
  -Target Legacy `
  -ConfigurationPath 'C:\inetpub\FurpaMerkezApi\appsettings.Local.json' `
  -SiteName 'FurpaMerkez' `
  -HealthBaseUrl 'http://10.0.0.100:7508' `
  -Apply `
  -ConfirmMikroApiMapping
```

Bu komut Furpa prosedur referanslarini, etkin Mikro okuma/yazma ayarini ve beklenen DB'yi Legacy hedefe cevirir; IIS'i baslatir ve gercek hedefi health cevabindan dogrular.

Mikro API firma eslemesi harici urunun ayari oldugu icin repo araci bu eslemeyi kendi basina degistiremez. Esleme degistirilmeden `-ConfirmMikroApiMapping` verilmemelidir.
