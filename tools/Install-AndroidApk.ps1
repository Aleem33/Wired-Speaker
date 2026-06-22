[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ApkPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    Import-Module (Join-Path $PSScriptRoot 'CableSpeaker.Tools.psm1') -Force

    $resolvedApk = Resolve-Path -LiteralPath $ApkPath
    Assert-CableSpeakerPhoneReady | Out-Null

    Write-Host "Installing APK: $($resolvedApk.Path)"
    $result = Invoke-CableSpeakerAdb -Arguments @('install', '-r', $resolvedApk.Path)
    if ($result.ExitCode -ne 0) {
        throw $result.Output
    }

    Write-Host $result.Output
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
