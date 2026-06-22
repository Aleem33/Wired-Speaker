[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$downloadDir = Join-Path $PSScriptRoot 'downloads'
$zipPath = Join-Path $downloadDir 'platform-tools-latest-windows.zip'
$extractTemp = Join-Path $downloadDir 'platform-tools-extract'
$targetDir = Join-Path $PSScriptRoot 'platform-tools'
$url = 'https://dl.google.com/android/repository/platform-tools-latest-windows.zip'

New-Item -ItemType Directory -Force -Path $downloadDir | Out-Null

Write-Host "Downloading Android SDK Platform-Tools..."
$curl = Get-Command curl.exe -ErrorAction SilentlyContinue
if ($curl) {
    & $curl.Source -L --fail --retry 3 $url -o $zipPath
    if ($LASTEXITCODE -ne 0) {
        throw "curl.exe failed to download Android SDK Platform-Tools."
    }
} else {
    Invoke-WebRequest -Uri $url -OutFile $zipPath
}

if (Test-Path -LiteralPath $extractTemp) {
    Remove-Item -LiteralPath $extractTemp -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $extractTemp | Out-Null

Expand-Archive -LiteralPath $zipPath -DestinationPath $extractTemp -Force

$extractedPlatformTools = Join-Path $extractTemp 'platform-tools'
if (-not (Test-Path -LiteralPath $extractedPlatformTools)) {
    throw "The downloaded archive did not contain a platform-tools folder."
}

if (Test-Path -LiteralPath $targetDir) {
    Remove-Item -LiteralPath $targetDir -Recurse -Force
}

Move-Item -LiteralPath $extractedPlatformTools -Destination $targetDir

$adb = Join-Path $targetDir 'adb.exe'
Write-Host "Installed: $adb"
& $adb version
