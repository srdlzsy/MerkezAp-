<#
.SYNOPSIS
Converts a UBL invoice XML to PDF with the FurpaMerkezApi invoice XSLT.

.EXAMPLE
.\tools\Convert-InvoiceXmlToPdf.ps1 `
    -XmlPath ".\docs\FATURA.xml" `
    -OutputPath ".\output\pdf\FATURA.pdf"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$XmlPath,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputPath,

    [string]$XsltPath = "",

    [string]$BrowserPath = "",

    [ValidateRange(0, 60000)]
    [int]$VirtualTimeBudgetMilliseconds = 5000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-ExistingFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue
    if ($null -eq $resolved -or -not (Test-Path -LiteralPath $resolved.ProviderPath -PathType Leaf)) {
        throw "$Description bulunamadi: $Path"
    }

    return $resolved.ProviderPath
}

function Resolve-BrowserExecutable {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return Resolve-ExistingFile -Path $RequestedPath -Description "Browser executable"
    }

    $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
    $programFilesX86 = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
    $candidates = @(
        (Join-Path $programFiles "Google\Chrome\Application\chrome.exe"),
        (Join-Path $programFilesX86 "Google\Chrome\Application\chrome.exe"),
        (Join-Path $programFiles "Microsoft\Edge\Application\msedge.exe"),
        (Join-Path $programFilesX86 "Microsoft\Edge\Application\msedge.exe")
    )

    foreach ($candidate in $candidates) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return [IO.Path]::GetFullPath($candidate)
        }
    }

    foreach ($commandName in @("chrome.exe", "msedge.exe", "chrome", "msedge")) {
        $command = Get-Command $commandName -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $command) {
            return $command.Source
        }
    }

    throw "Chrome veya Edge bulunamadi. -BrowserPath ile executable yolunu belirtin."
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($XsltPath)) {
    $XsltPath = Join-Path $repoRoot "src\FurpaMerkezApi.WebApi\Assets\Xslt\efatura.xslt"
}

$resolvedXmlPath = Resolve-ExistingFile -Path $XmlPath -Description "XML dosyasi"
$resolvedXsltPath = Resolve-ExistingFile -Path $XsltPath -Description "XSLT dosyasi"
$resolvedBrowserPath = Resolve-BrowserExecutable -RequestedPath $BrowserPath

if ([IO.Path]::IsPathRooted($OutputPath)) {
    $resolvedOutputPath = [IO.Path]::GetFullPath($OutputPath)
}
else {
    $resolvedOutputPath = [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $OutputPath))
}

if ([IO.Path]::GetExtension($resolvedOutputPath) -ine ".pdf") {
    throw "OutputPath .pdf uzantili olmalidir: $resolvedOutputPath"
}

$outputDirectory = Split-Path -Parent $resolvedOutputPath
if ([string]::IsNullOrWhiteSpace($outputDirectory)) {
    throw "OutputPath icin hedef klasor belirlenemedi: $resolvedOutputPath"
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$rawXml = [IO.File]::ReadAllText($resolvedXmlPath, [Text.Encoding]::UTF8)
$startIndex = $rawXml.IndexOf("<Invoice", [StringComparison]::OrdinalIgnoreCase)
$endMarker = "</Invoice>"
$endIndex = $rawXml.LastIndexOf($endMarker, [StringComparison]::OrdinalIgnoreCase)

if ($startIndex -lt 0 -or $endIndex -lt $startIndex) {
    throw "XML dosyasinda Invoice kok elemani bulunamadi: $resolvedXmlPath"
}

$invoiceXml = $rawXml.Substring($startIndex, ($endIndex + $endMarker.Length) - $startIndex)
$invoiceDocument = New-Object System.Xml.XmlDocument
$invoiceDocument.PreserveWhitespace = $true
$invoiceDocument.LoadXml($invoiceXml)

$tempId = [Guid]::NewGuid().ToString("N")
$tempHtmlPath = Join-Path ([IO.Path]::GetTempPath()) "furpa-invoice-$tempId.html"
$tempStdOutPath = Join-Path ([IO.Path]::GetTempPath()) "furpa-invoice-$tempId.stdout.log"
$tempStdErrPath = Join-Path ([IO.Path]::GetTempPath()) "furpa-invoice-$tempId.stderr.log"
$tempPdfPath = Join-Path $outputDirectory "furpa-invoice-$tempId.pdf"
$tempBackupPath = Join-Path $outputDirectory "furpa-invoice-$tempId.backup.pdf"

try {
    $transform = New-Object System.Xml.Xsl.XslCompiledTransform
    $transform.Load($resolvedXsltPath)

    $writerSettings = $transform.OutputSettings.Clone()
    $writerSettings.Encoding = New-Object System.Text.UTF8Encoding($false)

    $stringReader = New-Object IO.StringReader($invoiceXml)
    $xmlReaderSettings = New-Object System.Xml.XmlReaderSettings
    $xmlReaderSettings.DtdProcessing = [System.Xml.DtdProcessing]::Parse
    $xmlReaderSettings.XmlResolver = $null
    $xmlReader = [System.Xml.XmlReader]::Create($stringReader, $xmlReaderSettings)
    $htmlWriter = [System.Xml.XmlWriter]::Create($tempHtmlPath, $writerSettings)

    try {
        $transform.Transform($xmlReader, $htmlWriter)
    }
    finally {
        $htmlWriter.Dispose()
        $xmlReader.Dispose()
        $stringReader.Dispose()
    }

    $htmlUri = (New-Object System.Uri($tempHtmlPath)).AbsoluteUri
    $browserArguments = @(
        "--headless=new",
        "--disable-gpu",
        "--no-pdf-header-footer",
        "--virtual-time-budget=$VirtualTimeBudgetMilliseconds",
        "--print-to-pdf=`"$tempPdfPath`"",
        "`"$htmlUri`""
    )

    $process = Start-Process `
        -FilePath $resolvedBrowserPath `
        -ArgumentList $browserArguments `
        -NoNewWindow `
        -Wait `
        -PassThru `
        -RedirectStandardOutput $tempStdOutPath `
        -RedirectStandardError $tempStdErrPath

    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $tempPdfPath -PathType Leaf)) {
        $browserError = ""
        if (Test-Path -LiteralPath $tempStdErrPath -PathType Leaf) {
            $browserError = [IO.File]::ReadAllText($tempStdErrPath).Trim()
        }

        throw "Browser PDF olusturamadi (exit code: $($process.ExitCode)). $browserError"
    }

    $tempPdfInfo = Get-Item -LiteralPath $tempPdfPath
    if ($tempPdfInfo.Length -eq 0) {
        throw "Browser bos bir PDF olusturdu: $resolvedOutputPath"
    }

    if (Test-Path -LiteralPath $resolvedOutputPath -PathType Leaf) {
        [IO.File]::Replace($tempPdfPath, $resolvedOutputPath, $tempBackupPath)
    }
    else {
        [IO.File]::Move($tempPdfPath, $resolvedOutputPath)
    }

    $pdfInfo = Get-Item -LiteralPath $resolvedOutputPath

    [PSCustomObject]@{
        PdfPath = $pdfInfo.FullName
        SizeBytes = $pdfInfo.Length
        XmlPath = $resolvedXmlPath
        XsltPath = $resolvedXsltPath
        BrowserPath = $resolvedBrowserPath
    }
}
finally {
    foreach ($tempPath in @($tempHtmlPath, $tempStdOutPath, $tempStdErrPath, $tempPdfPath, $tempBackupPath)) {
        if (Test-Path -LiteralPath $tempPath -PathType Leaf) {
            Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
        }
    }
}
