# Operasyon Hizli Mudahale Rehberi

Bu dokuman, sistem yavasladiginda veya kullanicilar "ekran acilmiyor / islem yapamiyoruz" dediginde hizli teshis ve gecici rahatlatma icin kullanilir.

## Sistem Adresleri

- Frontend: `http://10.0.0.100:5002`
- Eski API: `http://10.0.0.100:5001`
- Mikro RDP sunucusu: `10.0.0.60`
- SQL Server: `10.0.0.207`

## 1. IIS Site Ve App Pool Listesi

Ne ise yarar: Hangi portun hangi siteye ve app pool'a bagli oldugunu gosterir.

Ne zaman kullanilir: `5001`, `5002` veya baska bir port hata verdiginde once dogru siteyi bulmak icin.

```powershell
Import-Module WebAdministration

Get-Website | ForEach-Object {
    $site = $_
    Get-WebBinding -Name $site.Name | ForEach-Object {
        [pscustomobject]@{
            Site = $site.Name
            State = $site.State
            AppPool = $site.ApplicationPool
            Binding = $_.bindingInformation
            Path = $site.PhysicalPath
        }
    }
} | Sort-Object Binding | Format-Table -AutoSize
```

Beklenen ana eslesmeler:

- `*:5001:` -> `WebApi`
- `*:5002:` -> `FurpaAngularUI`

## 2. App Pool Durumu Ve Restart

Ne ise yarar: IIS uygulamasini tum sunucuyu yeniden baslatmadan tazeler.

Ne zaman kullanilir: API `503 Service Unavailable` verirse, frontend takilirsa veya uygulama cevap vermiyorsa.

```powershell
Import-Module WebAdministration

Get-WebAppPoolState "WebApi"
Get-WebAppPoolState "FurpaAngularUI"

Restart-WebAppPool "WebApi"
Restart-WebAppPool "FurpaAngularUI"
```

Risk: O app pool'a bagli kullanicilarin aktif istekleri kesilebilir. `iisreset` kadar genis etkili degildir.

## 3. Frontend Ve API Hiz Testi

Ne ise yarar: Sorun frontend mi, API mi, yoksa ag mi ayirmaya yardim eder.

Sunucunun kendi icinden:

```powershell
Measure-Command { Invoke-WebRequest "http://localhost:5002/login" -UseBasicParsing }
Measure-Command { Invoke-WebRequest "http://localhost:5002/vendor-es2015.js" -UseBasicParsing }
Measure-Command { Invoke-WebRequest "http://localhost:5001/" -UseBasicParsing }
```

Kendi PC'den:

```powershell
Measure-Command { Invoke-WebRequest "http://10.0.0.100:5002/login" -UseBasicParsing }
Measure-Command { Invoke-WebRequest "http://10.0.0.100:5002/vendor-es2015.js" -UseBasicParsing }
Measure-Command { Invoke-WebRequest "http://10.0.0.100:5001/" -UseBasicParsing }
```

Yorum:

- Sunucuda hizli, PC'de yavas: ag, switch, firewall veya istemci tarafina bak.
- Sunucuda da yavas: `10.0.0.100` uzerinde IIS, disk, antivirus veya uygulama sorunu.
- `5001` icin `503`: API app pool veya IIS problemi.
- `5001` icin `404`: API ayakta olabilir; `/` endpoint olmayabilir.
- `5002/login` hizli ama `vendor-es2015.js` yavas: static dosya/IIS/disk/antivirus problemi.

## 4. Frontend Dosyasi Diskten Hizli Okunuyor Mu?

Ne ise yarar: Buyuk JS dosyasi IIS yuzunden mi, disk/antivirus yuzunden mi yavas ayirir.

```powershell
$path = "C:\inetpub\wwwroot\FurpaAngularUI\vendor-es2015.js"

Get-Item $path | Select FullName, Length

Measure-Command { [System.IO.File]::ReadAllBytes($path) | Out-Null }
```

Yorum:

- Diskten okuma hizli, HTTP yavas: IIS static file, compression veya filter sorunu.
- Diskten okuma da yavas: disk, antivirus, dosya sistemi veya storage sorunu.

## 5. IIS Compression Kapatma

Ne ise yarar: Static/dynamic compression sorun cikariyorsa buyuk JS dosyalarinin takilmasini engelleyebilir.

Ne zaman kullanilir: Diskten okuma hizli ama `http://localhost:5002/vendor-es2015.js` 10-15 saniye suruyorsa.

```powershell
Import-Module WebAdministration

Set-WebConfigurationProperty -Filter system.webServer/urlCompression -Name doStaticCompression -Value False
Set-WebConfigurationProperty -Filter system.webServer/urlCompression -Name doDynamicCompression -Value False

iisreset
```

Risk: `iisreset` tum IIS sitelerini kisa sure keser. Mumkunse once app pool restart denenmelidir.

Kontrol:

```powershell
Measure-Command { Invoke-WebRequest "http://localhost:5002/vendor-es2015.js" -UseBasicParsing }
```

## 6. Crash Atan Kasa/Cek Servisini Kapatma

Ne ise yarar: Surekli crash/restart olan servis sunucuyu yoruyorsa gecici durdurur.

Bugun gorulen servis:

- `Furpa.BirlikPremiumKartHareketAktarimServis`
- Exe: `C:\Furpa Programlar\CekHareketAktarimService\Furpa.CekHareketAktarimService.exe`
- Hata: `\\10.0.0.55\kasa\...` altindaki dosya baska process tarafindan kullanildigi icin servis crash oluyor.

Durdurma:

```powershell
Stop-Service "Furpa.BirlikPremiumKartHareketAktarimServis" -Force
Set-Service "Furpa.BirlikPremiumKartHareketAktarimServis" -StartupType Disabled

Get-Service "Furpa.BirlikPremiumKartHareketAktarimServis"
```

Tekrar acma:

```powershell
Set-Service "Furpa.BirlikPremiumKartHareketAktarimServis" -StartupType Automatic
Start-Service "Furpa.BirlikPremiumKartHareketAktarimServis"
```

Risk: Bu servis kapaliyken ilgili cek/kasa hareket aktarimi calismayabilir. Sorun cozulunce tekrar acilmalidir.

## 7. IIS Ve .NET Event Log Kontrolu

Ne ise yarar: Uygulama crash, startup hata, 503 sebebi ve .NET uyarilarini gosterir.

Application log:

```powershell
Get-EventLog -LogName Application -Newest 50 |
    Where-Object { $_.Source -match "IIS|ASP.NET|AspNetCore|\.NET|Application Error|WAS|W3SVC" } |
    Select TimeGenerated, Source, EntryType, Message |
    Format-List
```

System log:

```powershell
Get-EventLog -LogName System -Newest 50 |
    Where-Object { $_.Source -match "WAS|W3SVC|IIS" } |
    Select TimeGenerated, Source, EntryType, Message |
    Format-List
```

Yorum:

- `Application started successfully`: iyi.
- `Application Error`: crash var.
- `IIS AspNetCore Module V2`: ASP.NET Core startup/shutdown bilgisi.
- EF warning'leri genelde acil crash sebebi degildir, performans veya model uyarisi olabilir.

## 8. IIS Access Log Bulma Ve Okuma

Ne ise yarar: HTTP status, substatus ve win32 status ile gercek hata kodunu gosterir.

Log dosyalarini bul:

```powershell
Get-ChildItem "C:\inetpub\logs\LogFiles" -Recurse -Filter "*.log" |
    Sort-Object LastWriteTime -Descending |
    Select -First 10 FullName, LastWriteTime
```

Log oku:

```powershell
Get-Content "LOG_DOSYASI_YOLU" -Tail 80
```

Site ID eslestirme:

```powershell
Import-Module WebAdministration

Get-Website | Select Name, Id, State, Bindings, ApplicationPool |
    Sort-Object Id |
    Format-Table -AutoSize
```

Ornek:

- Site `Id = 3` ise log klasoru `W3SVC3`
- Site `Id = 9` ise log klasoru `W3SVC9`

## 9. Worker Process Kontrolu

Ne ise yarar: Hangi app pool'un gercekten worker process actigini gosterir.

```powershell
Get-WmiObject Win32_Process -Filter "name='w3wp.exe'" |
    Select ProcessId, CommandLine |
    Format-List
```

Yorum:

- App pool `Started` gorunup `w3wp.exe -ap "WebApi"` yoksa, worker process kalkmiyor veya request alinca hemen dusuyor olabilir.
- Bu durumda Event Log, access log ve app pool ayarlari kontrol edilir.

## 10. HTTPERR Log Kontrolu

Ne ise yarar: IIS'e ulasmadan HTTP.sys seviyesinde dusen baglantilari gosterir.

```powershell
Get-Content "C:\Windows\System32\LogFiles\HTTPERR\httperr*.log" -Tail 100
```

Yorum:

- `Timer_MinBytesPerSecond`: istemciye yeterli hizda veri akmiyor olabilir.
- `Timer_ConnectionIdle`: bosta kalan baglanti kapanmis.
- Guncel tarih yoksa o log o anki sorunu gostermiyor olabilir.

## 11. Ag Testi

Ne ise yarar: Sunucular arasinda paket kaybi var mi gosterir.

```cmd
ping 10.0.0.100 -n 100
ping 10.0.0.60 -n 100
ping 10.0.0.207 -n 100
```

Beklenen:

```text
Lost = 0
```

Yorum:

- Paket kaybi varsa RDP siyah ekran, frontend dosyalarinda pending, API timeout gibi belirtiler olabilir.
- Sunucunun kendi icinden kendi IP'sine ping `0ms` cikabilir; asil test kullanici PC'sinden yapilmalidir.

## 12. SQL Anlik Yuk Kontrolu

Ne ise yarar: SQL'de CPU/IO yiyen, uzun suren veya bloklayan sorgulari gosterir.

```sql
SELECT TOP (30)
    r.session_id,
    DB_NAME(r.database_id) AS database_name,
    r.status,
    r.command,
    COALESCE(r.wait_type, r.last_wait_type) AS wait_type,
    r.total_elapsed_time / 1000 AS elapsed_sec,
    r.cpu_time,
    r.logical_reads,
    r.reads,
    r.writes,
    r.blocking_session_id,
    s.host_name,
    s.program_name,
    s.login_name
FROM sys.dm_exec_requests r
JOIN sys.dm_exec_sessions s ON s.session_id = r.session_id
WHERE s.is_user_process = 1
ORDER BY r.cpu_time DESC, r.logical_reads DESC;
```

Yorum:

- `logical_reads` milyonlara cikiyorsa sorgu cok okuma yapiyor.
- `blocking_session_id` sifir degilse bloklama var.
- `wait_type = ASYNC_NETWORK_IO`: SQL sonucu uretmis, istemci yavas tuketiyor olabilir.
- `PAGEIOLATCH_*`: disk okuma beklemesi.
- `SOS_SCHEDULER_YIELD`: CPU baskisi veya agir sorgu.
- `RESOURCE_SEMAPHORE`: bellek bekleniyor.

## 13. SQL Scheduler CPU Kuyrugu

Ne ise yarar: SQL CPU kuyrugu var mi gosterir.

```sql
SELECT
    scheduler_id,
    current_tasks_count,
    runnable_tasks_count,
    active_workers_count,
    work_queue_count,
    load_factor
FROM sys.dm_os_schedulers
WHERE status = 'VISIBLE ONLINE'
ORDER BY runnable_tasks_count DESC;
```

Yorum:

- `runnable_tasks_count = 0`: SQL CPU kuyrugu yok.
- Surekli `2+`, `5+` gibi degerler: SQL CPU baskisi olabilir.

## 14. SQL Memory Grant Kontrolu

Ne ise yarar: Sorgular bellek bekliyor mu gosterir.

```sql
SELECT
    session_id,
    request_time,
    grant_time,
    requested_memory_kb,
    granted_memory_kb,
    required_memory_kb,
    used_memory_kb,
    ideal_memory_kb
FROM sys.dm_exec_query_memory_grants
ORDER BY requested_memory_kb DESC;
```

Yorum:

- `grant_time` bos ise sorgu bellek bekliyor olabilir.
- Tum `grant_time` dolu ve rakamlar kucukse bellek baskisi dusuk ihtimaldir.

## 15. SQL Uzun Sorguyu Kesme

Ne ise yarar: Sistemi yoran uzun sorguyu sonlandirir.

```sql
KILL SESSION_ID;
```

Risk:

- Uzun `SELECT` kesmek genelde daha az risklidir.
- `INSERT`, `UPDATE`, `DELETE` kesilirse rollback baslayabilir ve sistem daha uzun sure yorulabilir.
- Emin degilsen once sorgu metni ve command tipine bak.

## 16. Mikro Dashboard Sorgulari

Bugun gorulen agir sorgular:

- `fn_Dashboard_Satis_Tutar_1`
- `fn_Dashboard_Satis_Miktar_1`

Kaynak:

- Host: `02FRPRDP`
- IP: `10.0.0.60`
- Program: `Fly`, `MikroFly`
- DB: `MikroDB_V16_FURPA_2024`

Yorum:

Bu sorgular dashboard/satis raporu gibi calisir. Kullanici RDP'de dashboard ekrani acik birakirsa SQL'i yorabilir. Veri kaydi degilse uzun `SELECT`/fonksiyon sorgusu kesilebilir, ancak once kontrol edilmelidir.

## 17. RDP/Mikro Sunucusu Kontrolu

Ne ise yarar: Mikro uzak masaustu sunucusunda cok kullanici veya agir process var mi gosterir.

```cmd
quser /server:10.0.0.60
```

RDP sunucusunda Task Manager'dan bak:

- CPU
- RAM
- Disk
- Kullanici sayisi
- `Fly`, `MikroFly`, `AxataWM` processleri

Yorum:

SQL iyi oldugu halde Mikro RDP kasiyorsa sorun `10.0.0.60` uzerindeki oturum/yuk olabilir.

## 18. Hizli Karar Tablosu

| Belirti | En olasi yer | Ilk bakilacak komut |
| --- | --- | --- |
| `5001` 503 | API app pool / IIS | `Get-WebAppPoolState`, Event Log, access log |
| `5002/login` hizli, JS yavas | IIS static/disk/antivirus | `ReadAllBytes`, compression kapatma |
| Sunucuda hizli, PC'de yavas | Ag/switch/firewall | `ping -n 100` |
| SQL sorgulari milyon read | SQL sorgu yuku | SQL anlik yuk sorgusu |
| RDP siyah ekran | Ag veya RDP sunucusu | `ping`, `quser`, Task Manager |
| App pool started ama w3wp yok | Worker kalkmiyor | `w3wp.exe` kontrolu, Event Log |
| Surekli .NET crash | Servis/app crash | Application Event Log |

## 19. Bugunku Ozet

Bugun gorulen ana bulgular:

- `5001 WebApi` bir ara `503 Service Unavailable` verdi; sonra `404` donmeye basladi. Bu, API'nin tekrar ayaga kalktigini gosterebilir.
- `5002 FurpaAngularUI` icin `vendor-es2015.js` localhost'ta bile 14-15 saniye surdu. Bu frontend static dosya servisinde, IIS compression/filter tarafinda, disk veya antivirus tarafinda sorun olabilecegini gosterir.
- `Furpa.BirlikPremiumKartHareketAktarimServis` kilitli `\\10.0.0.55\kasa\...` dosyalarini okumaya calisirken crash oluyordu. Gecici olarak disabled yapildi.
- SQL tarafinda ara ara Mikro dashboard sorgulari agir calisti, ancak son kontrollerde SQL CPU kuyrugu ana problem gibi gorunmedi.
