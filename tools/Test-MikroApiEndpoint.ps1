param(
    [ValidateSet("Connectivity", "Write")]
    [string]$Mode = "Connectivity",

    [ValidateSet("All", "Localhost8094", "Direct8094", "Current8084")]
    [string]$Target = "All",

    [string]$PayloadPath = "",

    [string]$SettingsPath = "",

    [ValidateNotNullOrEmpty()]
    [string]$DocumentSerie = "TAPI56",

    [ValidateRange(0, 2147483647)]
    [int]$DocumentOrderNo = 0,

    [ValidateRange(1, 9999)]
    [int]$SourceWarehouseNo = 56,

    [ValidateRange(1, 9999)]
    [int]$TransitWarehouseNo = 60,

    [ValidateRange(1, 9999)]
    [int]$TargetWarehouseNo = 110,

    [ValidateNotNullOrEmpty()]
    [string]$StockCode = "015550",

    [ValidateRange(0.001, 999999999)]
    [double]$Quantity = 10,

    [ValidateRange(1, 10)]
    [int]$UnitPointer = 1,

    [ValidateRange(5, 600)]
    [int]$TimeoutSeconds = 75,

    [switch]$ExecuteWriteTest,

    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

$targets = [ordered]@{
    Localhost8094 = "http://localhost:8094"
    Direct8094    = "http://10.0.0.207:8094"
    Current8084   = "http://10.0.0.207:8084"
}

$apiPath = "/Api/apiMethods/DahiliStokHareketKaydetV2"
$repoRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($SettingsPath)) {
    $SettingsPath = Join-Path $repoRoot "src\FurpaMerkezApi.WebApi\appsettings.Production.json"
}

function Get-StringSha256 {
    param([string]$Value)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        $hash = $sha256.ComputeHash($bytes)
        return ([System.BitConverter]::ToString($hash)).Replace("-", "")
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-DailyPasswordHash {
    param(
        [datetime]$Date,
        [string]$PasswordSeed
    )

    $rawValue = "{0} {1}" -f $Date.ToString("yyyy-MM-dd", [System.Globalization.CultureInfo]::InvariantCulture), $PasswordSeed
    $md5 = [System.Security.Cryptography.MD5]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($rawValue)
        $hash = $md5.ComputeHash($bytes)
        return ([System.BitConverter]::ToString($hash)).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $md5.Dispose()
    }
}

function New-BuiltInWritePayload {
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

    $hashDateUtcOffsetHours = if ($null -eq $mikro.HashDateUtcOffsetHours) {
        3
    }
    else {
        [int]$mikro.HashDateUtcOffsetHours
    }

    $hashDate = [datetime]::UtcNow.AddHours($hashDateUtcOffsetHours)
    $effectiveDocumentOrderNo = $DocumentOrderNo
    if ($effectiveDocumentOrderNo -eq 0) {
        $epoch = [datetime]::SpecifyKind([datetime]"2020-01-01T00:00:00", [System.DateTimeKind]::Utc)
        $effectiveDocumentOrderNo = [int][math]::Floor(([datetime]::UtcNow - $epoch).TotalSeconds)
    }

    $movementDate = Get-Date
    $formattedDate = $movementDate.ToString("dd.MM.yyyy", [System.Globalization.CultureInfo]::InvariantCulture)
    $traceKey = "FT{0}" -f $movementDate.ToString("yyMMddHHmmssfff", [System.Globalization.CultureInfo]::InvariantCulture)
    $description = "Mikro API performans testi"

    $payloadObject = [ordered]@{
        Mikro = [ordered]@{
            FirmaKodu = ([string]$mikro.FirmaKodu).Trim()
            CalismaYili = [int]$mikro.CalismaYili
            KullaniciKodu = ([string]$mikro.KullaniciKodu).Trim()
            Sifre = Get-DailyPasswordHash -Date $hashDate -PasswordSeed ([string]$mikro.SifreAnahtari)
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
                            sth_evrakno_seri = $DocumentSerie.Trim()
                            sth_evrakno_sira = $effectiveDocumentOrderNo
                            sth_satirno = 0
                            sth_belge_no = ""
                            sth_belge_tarih = $formattedDate
                            sth_stok_kod = $StockCode.Trim()
                            sth_cari_cinsi = 0
                            sth_cari_kodu = ""
                            sth_isemri_gider_kodu = ""
                            sth_miktar = $Quantity
                            sth_miktar2 = 0
                            sth_birim_pntr = $UnitPointer
                            sth_tutar = 0
                            sth_vergi_pntr = 0
                            sth_vergi = 0
                            sth_vergisiz_fl = $false
                            sth_iskonto1 = 0
                            sth_iskonto2 = 0
                            sth_isk_mas1 = 0
                            sth_isk_mas2 = 1
                            sth_subesip_uid = $null
                            sth_giris_depo_no = $TransitWarehouseNo
                            sth_cikis_depo_no = $SourceWarehouseNo
                            sth_malkbl_sevk_tarihi = $formattedDate
                            sth_aciklama = $description
                            sth_cari_srm_merkezi = ""
                            sth_stok_srm_merkezi = ""
                            sth_parti_kodu = ""
                            sth_lot_no = 0
                            sth_proje_kodu = ""
                            sth_fiyat_liste_no = -1
                            sth_nakliyedeposu = $TargetWarehouseNo
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

    return [pscustomobject]@{
        Json = $payloadObject | ConvertTo-Json -Depth 12 -Compress
        Source = "built-in"
        DocumentSerie = $DocumentSerie.Trim()
        DocumentOrderNo = $effectiveDocumentOrderNo
        TraceKey = $traceKey
    }
}

function Resolve-Targets {
    if ($Target -eq "All") {
        return @($targets.GetEnumerator())
    }

    return @([pscustomobject]@{
        Key = $Target
        Value = $targets[$Target]
    })
}

function Test-TcpEndpoint {
    param(
        [string]$Name,
        [string]$BaseUrl
    )

    $uri = [Uri]$BaseUrl
    $client = New-Object System.Net.Sockets.TcpClient
    $watch = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        $connectTask = $client.ConnectAsync($uri.Host, $uri.Port)
        if (-not $connectTask.Wait(5000)) {
            throw "TCP connection timed out after 5000 ms."
        }

        $watch.Stop()
        return [pscustomobject]@{
            Machine = $env:COMPUTERNAME
            Timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss.fff zzz")
            Mode = "Connectivity"
            Target = $Name
            BaseUrl = $BaseUrl
            Host = $uri.Host
            Port = $uri.Port
            Success = $true
            ElapsedMs = [math]::Round($watch.Elapsed.TotalMilliseconds, 1)
            Detail = "TCP connection succeeded."
        }
    }
    catch {
        $watch.Stop()
        return [pscustomobject]@{
            Machine = $env:COMPUTERNAME
            Timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss.fff zzz")
            Mode = "Connectivity"
            Target = $Name
            BaseUrl = $BaseUrl
            Host = $uri.Host
            Port = $uri.Port
            Success = $false
            ElapsedMs = [math]::Round($watch.Elapsed.TotalMilliseconds, 1)
            Detail = $_.Exception.Message
        }
    }
    finally {
        $client.Dispose()
    }
}

function Get-ErrorResponseBody {
    param([System.Management.Automation.ErrorRecord]$ErrorRecord)

    if (-not [string]::IsNullOrWhiteSpace($ErrorRecord.ErrorDetails.Message)) {
        return $ErrorRecord.ErrorDetails.Message
    }

    $response = $ErrorRecord.Exception.Response
    if ($null -eq $response) {
        return ""
    }

    if ($response -is [System.Net.Http.HttpResponseMessage]) {
        return $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    }

    try {
        $stream = $response.GetResponseStream()
        if ($null -eq $stream) {
            return ""
        }

        $reader = New-Object System.IO.StreamReader($stream)
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
            $stream.Dispose()
        }
    }
    catch {
        return ""
    }
}

function Get-StatusCodeFromError {
    param([System.Management.Automation.ErrorRecord]$ErrorRecord)

    $response = $ErrorRecord.Exception.Response
    if ($null -eq $response) {
        return $null
    }

    try {
        return [int]$response.StatusCode
    }
    catch {
        return $null
    }
}

function Get-MikroResultFields {
    param([string]$ResponseBody)

    $success = $null
    $errorText = $null

    if (-not [string]::IsNullOrWhiteSpace($ResponseBody)) {
        try {
            $json = $ResponseBody | ConvertFrom-Json
            $firstResult = @($json.result) | Select-Object -First 1
            if ($null -ne $firstResult) {
                $success = $firstResult.success
                $errorText = $firstResult.errorText
            }
        }
        catch {
            # Keep raw HTTP diagnostics when the body is not JSON.
        }
    }

    return [pscustomobject]@{
        Success = $success
        ErrorText = $errorText
    }
}

function Invoke-WriteProbe {
    param(
        [string]$Name,
        [string]$BaseUrl,
        [string]$Payload,
        [string]$PayloadHash
    )

    $requestUri = "$($BaseUrl.TrimEnd('/'))$apiPath"
    $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($Payload)
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $statusCode = $null
    $responseBody = ""
    $transportError = $null
    $transportStatus = $null

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
        $statusCode = Get-StatusCodeFromError -ErrorRecord $_
        $responseBody = Get-ErrorResponseBody -ErrorRecord $_
        $transportError = $_.Exception.Message

        if ($null -ne $_.Exception.Status) {
            $transportStatus = [string]$_.Exception.Status
        }
        elseif ($null -ne $_.Exception.InnerException -and
                $null -ne $_.Exception.InnerException.Status) {
            $transportStatus = [string]$_.Exception.InnerException.Status
        }
    }
    finally {
        $watch.Stop()
    }

    $mikroResult = Get-MikroResultFields -ResponseBody $responseBody
    $timedOut = $transportStatus -eq "Timeout" -or
        $transportError -match "timed out|timeout|zaman.*a.*m" -or
        $mikroResult.ErrorText -match "TimeOut|Timeout"

    return [pscustomobject]@{
        Machine = $env:COMPUTERNAME
        Timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss.fff zzz")
        Mode = "Write"
        Target = $Name
        RequestUri = $requestUri
        PayloadSha256 = $PayloadHash
        TimeoutSeconds = $TimeoutSeconds
        ElapsedMs = [math]::Round($watch.Elapsed.TotalMilliseconds, 1)
        HttpStatusCode = $statusCode
        MikroSuccess = $mikroResult.Success
        MikroErrorText = $mikroResult.ErrorText
        TimedOut = $timedOut
        TransportStatus = $transportStatus
        TransportError = $transportError
        ResponseBody = $responseBody
    }
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $outputDirectory = Join-Path $repoRoot "outputs"
    if (-not (Test-Path -LiteralPath $outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory | Out-Null
    }

    $OutputPath = Join-Path $outputDirectory (
        "mikro-api-endpoint-test-{0}.json" -f (Get-Date).ToString("yyyyMMdd-HHmmss"))
}

$resolvedTargets = Resolve-Targets

if ($Mode -eq "Connectivity") {
    $results = foreach ($entry in $resolvedTargets) {
        Test-TcpEndpoint -Name ([string]$entry.Key) -BaseUrl ([string]$entry.Value)
    }

    $results | Format-Table Target, Host, Port, Success, ElapsedMs, Detail -AutoSize
    $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding UTF8

    Write-Host ""
    Write-Host "Machine    : $env:COMPUTERNAME"
    Write-Host "Result file: $OutputPath"
    Write-Host "Note       : Localhost8094 is meaningful only when this script runs on the Mikro API server."
    exit 0
}

if ($Target -eq "All") {
    throw "Write mode accepts exactly one target. Use -Target Localhost8094, Direct8094, or Current8084."
}

if (-not $ExecuteWriteTest) {
    throw "Write mode creates a real Mikro stock movement. Add -ExecuteWriteTest after checking the built-in defaults or the external payload."
}

if (-not [string]::IsNullOrWhiteSpace($PayloadPath)) {
    if (-not (Test-Path -LiteralPath $PayloadPath)) {
        throw "Payload file was not found: $PayloadPath"
    }

    $resolvedPayloadPath = (Resolve-Path -LiteralPath $PayloadPath).Path
    $payload = [System.IO.File]::ReadAllText($resolvedPayloadPath, [System.Text.Encoding]::UTF8)
    $payloadSource = $resolvedPayloadPath
    $testDocument = "external payload"
    $traceKey = "external payload"
}
else {
    $builtInPayload = New-BuiltInWritePayload
    $payload = $builtInPayload.Json
    $payloadSource = $builtInPayload.Source
    $testDocument = "$($builtInPayload.DocumentSerie)/$($builtInPayload.DocumentOrderNo)"
    $traceKey = $builtInPayload.TraceKey
}

try {
    $null = $payload | ConvertFrom-Json
}
catch {
    throw "Payload is not valid JSON: $($_.Exception.Message)"
}

$payloadHash = Get-StringSha256 -Value $payload
$selected = $resolvedTargets | Select-Object -First 1

Write-Warning "This request can create a real Mikro stock movement. The script sends exactly one POST and does not retry it."
Write-Host "Machine     : $env:COMPUTERNAME"
Write-Host "Target      : $($selected.Key)"
Write-Host "Base URL    : $($selected.Value)"
Write-Host "Payload     : $payloadSource"
Write-Host "Test document: $testDocument"
Write-Host "Trace key   : $traceKey"
Write-Host "Payload hash: $payloadHash"
Write-Host ""

$result = Invoke-WriteProbe `
    -Name ([string]$selected.Key) `
    -BaseUrl ([string]$selected.Value) `
    -Payload $payload `
    -PayloadHash $payloadHash

$result | Add-Member -NotePropertyName PayloadSource -NotePropertyValue $payloadSource
$result | Add-Member -NotePropertyName TestDocument -NotePropertyValue $testDocument
$result | Add-Member -NotePropertyName TraceKey -NotePropertyValue $traceKey

$result | Format-List
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding UTF8

Write-Host ""
Write-Host "Result file: $OutputPath"

if ($result.TimedOut) {
    exit 2
}

if ($null -ne $result.MikroSuccess -and -not $result.MikroSuccess) {
    exit 3
}

if ($null -ne $result.TransportError) {
    exit 4
}
