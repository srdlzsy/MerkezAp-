param(
    [string]$SettingsPath = "",
    [string]$OutputPath = "",

    [ValidateRange(1, 65535)]
    [int]$TargetPort = 8084,

    [ValidateRange(1, 500)]
    [int]$LineCount = 1,

    [ValidateRange(0.001, 999999999)]
    [double]$QuantityPerLine = 10,

    [ValidateNotNullOrEmpty()]
    [string]$StockCode = "015550"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($SettingsPath)) {
    $SettingsPath = Join-Path $repoRoot "src\FurpaMerkezApi.WebApi\appsettings.Production.json"
}

if (-not (Test-Path -LiteralPath $SettingsPath)) {
    throw "Settings file was not found: $SettingsPath"
}

$settings = Get-Content -Raw -LiteralPath $SettingsPath | ConvertFrom-Json
$mikro = $settings.MikroApi
$requiredSettings = @("FirmaKodu", "CalismaYili", "KullaniciKodu", "SifreAnahtari", "ApiKey")

foreach ($settingName in $requiredSettings) {
    if ([string]::IsNullOrWhiteSpace([string]$mikro.$settingName)) {
        throw "MikroApi:$settingName is not configured in $SettingsPath."
    }
}

$offsetHours = if ($null -eq $mikro.HashDateUtcOffsetHours) { 3 } else { [int]$mikro.HashDateUtcOffsetHours }
$hashDate = [datetime]::UtcNow.AddHours($offsetHours)
$validDate = $hashDate.ToString("yyyy-MM-dd", [System.Globalization.CultureInfo]::InvariantCulture)
$movementDate = Get-Date
$formattedDate = $movementDate.ToString("dd.MM.yyyy", [System.Globalization.CultureInfo]::InvariantCulture)
$epoch = [datetime]::SpecifyKind([datetime]"2020-01-01T00:00:00", [System.DateTimeKind]::Utc)
$documentOrderNo = [int][math]::Floor(([datetime]::UtcNow - $epoch).TotalSeconds)
$documentSerie = "TAPI56"
$traceKey = "FT{0}" -f $movementDate.ToString("yyMMddHHmmssfff", [System.Globalization.CultureInfo]::InvariantCulture)
$description = "Mikro API localhost testi"

$rawPassword = "{0} {1}" -f $validDate, [string]$mikro.SifreAnahtari
$md5 = [System.Security.Cryptography.MD5]::Create()
try {
    $passwordHashBytes = $md5.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($rawPassword))
    $passwordHash = ([System.BitConverter]::ToString($passwordHashBytes)).Replace("-", "").ToLowerInvariant()
}
finally {
    $md5.Dispose()
}

$payloadObject = [ordered]@{
    Mikro = [ordered]@{
        FirmaKodu = ([string]$mikro.FirmaKodu).Trim()
        CalismaYili = [int]$mikro.CalismaYili
        KullaniciKodu = ([string]$mikro.KullaniciKodu).Trim()
        Sifre = $passwordHash
        FirmaNo = [int]$mikro.FirmaNo
        SubeNo = [int]$mikro.SubeNo
        ApiKey = ([string]$mikro.ApiKey).Trim()
        evraklar = @(
            [ordered]@{
                satirlar = @(
                    [ordered]@{
                        sth_tarih = $formattedDate
                        sth_tip = 2
                        sth_cins = 6
                        sth_normal_iade = 0
                        sth_evraktip = 17
                        sth_evrakno_seri = $documentSerie
                        sth_evrakno_sira = $documentOrderNo
                        sth_satirno = 0
                        sth_belge_no = ""
                        sth_belge_tarih = $formattedDate
                        sth_stok_kod = $StockCode.Trim()
                        sth_cari_cinsi = 0
                        sth_cari_kodu = ""
                        sth_isemri_gider_kodu = ""
                        sth_miktar = $QuantityPerLine
                        sth_miktar2 = 0
                        sth_birim_pntr = 1
                        sth_tutar = 0
                        sth_vergi_pntr = 0
                        sth_vergi = 0
                        sth_vergisiz_fl = $false
                        sth_iskonto1 = 0
                        sth_iskonto2 = 0
                        sth_isk_mas1 = 0
                        sth_isk_mas2 = 1
                        sth_subesip_uid = $null
                        sth_giris_depo_no = 60
                        sth_cikis_depo_no = 56
                        sth_malkbl_sevk_tarihi = $formattedDate
                        sth_aciklama = $description
                        sth_cari_srm_merkezi = ""
                        sth_stok_srm_merkezi = ""
                        sth_parti_kodu = ""
                        sth_lot_no = 0
                        sth_proje_kodu = ""
                        sth_fiyat_liste_no = -1
                        sth_nakliyedeposu = 110
                        sth_nakliyedurumu = 0
                        sth_yetkili_uid = ""
                        sth_HareketGrupKodu1 = ""
                        sth_HareketGrupKodu2 = ""
                        sth_HareketGrupKodu3 = ""
                        sth_teslim_tarihi = $formattedDate
                        sth_eticaret_kanal_kodu = $traceKey
                        seriler = ""
                        renk_beden = @()
                        user_tablo = @()
                    }
                )
                evrak_aciklamalari = @(
                    [ordered]@{ aciklama = $description }
                )
            }
        )
    }
}

$baseLine = $payloadObject.Mikro.evraklar[0].satirlar[0]
$lines = for ($rowNo = 0; $rowNo -lt $LineCount; $rowNo++) {
    $line = [ordered]@{}
    foreach ($field in $baseLine.GetEnumerator()) {
        $line[$field.Key] = $field.Value
    }

    $line.sth_satirno = $rowNo
    $line
}
$payloadObject.Mikro.evraklar[0].satirlar = @($lines)

$payloadJson = $payloadObject | ConvertTo-Json -Depth 12 -Compress
$payloadBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($payloadJson))

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $outputDirectory = "C:\Temp"
    if (-not (Test-Path -LiteralPath $outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory | Out-Null
    }

    $OutputPath = Join-Path $outputDirectory "Test-MikroApi-Localhost$TargetPort-OneShot-$($hashDate.ToString('yyyyMMdd-HHmmss')).ps1"
}

$template = @'
param(
    [ValidateRange(5, 600)]
    [int]$TimeoutSeconds = 75,
    [switch]$ExecuteWriteTest
)

$ErrorActionPreference = "Stop"
$validDate = "__VALID_DATE__"
$document = "__DOCUMENT__"
$traceKey = "__TRACE_KEY__"
$lineCount = __LINE_COUNT__
$quantityPerLine = __QUANTITY_PER_LINE__
$payloadBase64 = "__PAYLOAD_BASE64__"
$requestUri = "http://localhost:__TARGET_PORT__/Api/apiMethods/DahiliStokHareketKaydetV2"

if ((Get-Date).ToString("yyyy-MM-dd") -ne $validDate) {
    throw "This one-shot script expired. Generate a new script for the current date."
}

if (-not $ExecuteWriteTest) {
    throw "This creates a real Mikro stock movement. Run again with -ExecuteWriteTest."
}

$payload = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payloadBase64))
$bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
$watch = [System.Diagnostics.Stopwatch]::StartNew()
$statusCode = $null
$responseBody = ""
$transportError = $null
$transportStatus = $null

Write-Warning "This sends exactly one real write request and never retries it."
Write-Host "Machine      : $env:COMPUTERNAME"
Write-Host "Request URI  : $requestUri"
Write-Host "Test document: $document"
Write-Host "Trace key    : $traceKey"
Write-Host "Line count   : $lineCount"
Write-Host "Qty per line : $quantityPerLine"
Write-Host ""

try {
    $parameters = @{
        Uri = $requestUri
        Method = "Post"
        ContentType = "application/json; charset=utf-8"
        Headers = @{ Accept = "application/json" }
        Body = $bodyBytes
        TimeoutSec = $TimeoutSeconds
    }

    if ($PSVersionTable.PSVersion.Major -le 5) {
        $parameters.UseBasicParsing = $true
    }

    $response = Invoke-WebRequest @parameters
    $statusCode = [int]$response.StatusCode
    $responseBody = [string]$response.Content
}
catch {
    $transportError = $_.Exception.Message
    if ($null -ne $_.Exception.Status) {
        $transportStatus = [string]$_.Exception.Status
    }
    elseif ($null -ne $_.Exception.InnerException -and $null -ne $_.Exception.InnerException.Status) {
        $transportStatus = [string]$_.Exception.InnerException.Status
    }

    if ($null -ne $_.Exception.Response) {
        try {
            $statusCode = [int]$_.Exception.Response.StatusCode
            $stream = $_.Exception.Response.GetResponseStream()
            if ($null -ne $stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                try { $responseBody = $reader.ReadToEnd() }
                finally { $reader.Dispose(); $stream.Dispose() }
            }
        }
        catch {
            # Preserve the original transport diagnostics.
        }
    }
}
finally {
    $watch.Stop()
}

$result = [pscustomobject]@{
    Machine = $env:COMPUTERNAME
    Timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss.fff zzz")
    RequestUri = $requestUri
    TestDocument = $document
    TraceKey = $traceKey
    LineCount = $lineCount
    QuantityPerLine = $quantityPerLine
    TimeoutSeconds = $TimeoutSeconds
    ElapsedMs = [math]::Round($watch.Elapsed.TotalMilliseconds, 1)
    HttpStatusCode = $statusCode
    TimedOut = $transportStatus -eq "Timeout" -or $transportError -match "timed out|timeout|zaman.*a.*m"
    TransportStatus = $transportStatus
    TransportError = $transportError
    ResponseBody = $responseBody
}

$outputPath = Join-Path $PSScriptRoot "mikro-api-localhost-result-$((Get-Date).ToString('yyyyMMdd-HHmmss')).json"
$result | Format-List
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $outputPath -Encoding UTF8
Write-Host ""
Write-Host "Result file: $outputPath"
'@

$portableScript = $template.Replace("__VALID_DATE__", $validDate)
$portableScript = $portableScript.Replace("__DOCUMENT__", "$documentSerie/$documentOrderNo")
$portableScript = $portableScript.Replace("__TRACE_KEY__", $traceKey)
$portableScript = $portableScript.Replace("__PAYLOAD_BASE64__", $payloadBase64)
$portableScript = $portableScript.Replace("__TARGET_PORT__", [string]$TargetPort)
$portableScript = $portableScript.Replace("__LINE_COUNT__", [string]$LineCount)
$portableScript = $portableScript.Replace("__QUANTITY_PER_LINE__", $QuantityPerLine.ToString([System.Globalization.CultureInfo]::InvariantCulture))

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory) -and -not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$portableScript | Set-Content -LiteralPath $OutputPath -Encoding UTF8

Write-Warning "The generated script contains a date-limited Mikro API credential payload. Do not commit it and delete it after the test."
Write-Host "Portable script: $OutputPath"
Write-Host "Valid date     : $validDate"
Write-Host "Target port    : $TargetPort"
Write-Host "Test document  : $documentSerie/$documentOrderNo"
Write-Host "Trace key      : $traceKey"
Write-Host "Line count     : $LineCount"
Write-Host "Quantity/line  : $QuantityPerLine"
