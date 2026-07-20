[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [datetime]$StartDate,

    [Parameter(Mandatory = $true)]
    [datetime]$EndDate,

    [string]$BaseUrl = "http://localhost:5228",

    [string]$Token = $env:FURPA_API_TOKEN,

    [string]$Username = $env:FURPA_API_USERNAME,

    [securestring]$Password,

    [string]$PasswordPlainText = $env:FURPA_API_PASSWORD,

    [switch]$IncludeStatuses,

    [switch]$Wait,

    [int]$PollSeconds = 5
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($EndDate.Date -lt $StartDate.Date) {
    throw "EndDate, StartDate'den once olamaz."
}

if ($PollSeconds -lt 1) {
    throw "PollSeconds en az 1 olmali."
}

function Join-ApiUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return $Root.TrimEnd("/") + "/" + $Path.TrimStart("/")
}

function ConvertTo-PlainText {
    param([securestring]$SecureValue)

    if ($null -eq $SecureValue) {
        return $null
    }

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)

    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Invoke-FurpaJson {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Get", "Post")]
        [string]$Method,

        [Parameter(Mandatory = $true)]
        [string]$Uri,

        [hashtable]$Headers,

        [object]$Body
    )

    $request = @{
        Method = $Method
        Uri = $Uri
        Headers = $Headers
        ContentType = "application/json"
    }

    if ($null -ne $Body) {
        $request.Body = $Body | ConvertTo-Json -Depth 10
    }

    try {
        return Invoke-RestMethod @request
    }
    catch {
        $response = $_.Exception.Response
        $details = $null

        if ($null -ne $response) {
            try {
                $stream = $response.GetResponseStream()
                if ($null -ne $stream) {
                    $reader = New-Object System.IO.StreamReader($stream)
                    $details = $reader.ReadToEnd()
                }
            }
            catch {
                $details = $null
            }
        }

        if ([string]::IsNullOrWhiteSpace($details)) {
            throw
        }

        throw "$($_.Exception.Message)`n$details"
    }
}

function Get-AccessToken {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ApiBaseUrl
    )

    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        return $Token
    }

    if ([string]::IsNullOrWhiteSpace($Username)) {
        $script:Username = Read-Host "API kullanici adi"
    }

    $loginPassword = $PasswordPlainText

    if ([string]::IsNullOrWhiteSpace($loginPassword)) {
        if ($null -eq $Password) {
            $script:Password = Read-Host "API sifre" -AsSecureString
        }

        $loginPassword = ConvertTo-PlainText $Password
    }

    if ([string]::IsNullOrWhiteSpace($Username) -or [string]::IsNullOrWhiteSpace($loginPassword)) {
        throw "Token yoksa Username ve Password gerekli."
    }

    $loginBody = @{
        usernameOrEmail = $Username
        password = $loginPassword
    }

    $loginResponse = Invoke-FurpaJson `
        -Method Post `
        -Uri (Join-ApiUrl $ApiBaseUrl "/api/auth/login") `
        -Headers @{} `
        -Body $loginBody

    $accessToken = $loginResponse.accessToken

    if ([string]::IsNullOrWhiteSpace($accessToken)) {
        $accessToken = $loginResponse.AccessToken
    }

    if ([string]::IsNullOrWhiteSpace($accessToken)) {
        throw "Login basarili dondu ama accessToken bulunamadi."
    }

    return $accessToken
}

$accessToken = Get-AccessToken -ApiBaseUrl $BaseUrl
$headers = @{
    Authorization = "Bearer $accessToken"
}

$syncBody = @{
    startDate = $StartDate.ToString("yyyy-MM-dd")
    endDate = $EndDate.ToString("yyyy-MM-dd")
    includeStatuses = [bool]$IncludeStatuses
}

$syncUri = Join-ApiUrl $BaseUrl "/api/fatura-islemleri/fatura-goruntuleme/senkronize"
$progressUri = Join-ApiUrl $BaseUrl "/api/fatura-islemleri/fatura-goruntuleme/senkronize/progress"

Write-Host ("Fatura goruntuleme sync API'ye gonderiliyor: {0} - {1}" -f $syncBody.startDate, $syncBody.endDate)
$initialProgress = Invoke-FurpaJson -Method Post -Uri $syncUri -Headers $headers -Body $syncBody

Write-Host ("Durum: {0} | {1}" -f $initialProgress.status, $initialProgress.message)

if (-not $Wait) {
    Write-Host "Is API worker kuyuguna alindi. Takip icin ayni komutu -Wait ile calistirabilirsin."
    return
}

$lastLine = $null

while ($true) {
    Start-Sleep -Seconds $PollSeconds
    $progress = Invoke-FurpaJson -Method Get -Uri $progressUri -Headers $headers

    $line = "Durum: {0} | %{1} | Sayfa {2}/{3} | Gelen {4} | Eslesen {5} | TarihDisi {6} | Tekrar {7} | Eklenen {8} | Guncellenen {9} | {10}" -f `
        $progress.status,
        $progress.progressPercent,
        $progress.pageNumber,
        $progress.totalPage,
        $progress.fetchedCount,
        $progress.matchedCount,
        $progress.skippedInvoiceDateOutOfRangeCount,
        $progress.skippedDuplicateDocumentCount,
        $progress.insertedCount,
        $progress.updatedCount,
        $progress.message

    if ($line -ne $lastLine) {
        Write-Host $line
        $lastLine = $line
    }

    if (-not [bool]$progress.isRunning) {
        if ($progress.status -eq "failed") {
            throw $progress.message
        }

        Write-Host "Senkronizasyon tamamlandi."
        break
    }
}
