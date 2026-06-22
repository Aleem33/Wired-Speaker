[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    Import-Module (Join-Path $PSScriptRoot 'CableSpeaker.Tools.psm1') -Force

    $adb = Get-CableSpeakerAdb
    Write-Host "Using ADB: $adb"

    $device = Assert-CableSpeakerPhoneReady
    Write-Host "Phone ready: $device"

    $reverseList = Invoke-CableSpeakerAdb -Arguments @('reverse', '--list')
    if ($reverseList.ExitCode -eq 0 -and $reverseList.Output.Trim()) {
        Write-Host "Current reverse tunnels:"
        Write-Host $reverseList.Output
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
