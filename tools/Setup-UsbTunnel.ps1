[CmdletBinding()]
param(
    [int[]] $Ports = @(38271, 38272)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    Import-Module (Join-Path $PSScriptRoot 'CableSpeaker.Tools.psm1') -Force

    $device = Assert-CableSpeakerPhoneReady
    Write-Host "Phone ready: $device"

    foreach ($port in $Ports) {
        $result = Invoke-CableSpeakerAdb -Arguments @('reverse', "tcp:$port", "tcp:$port")
        if ($result.ExitCode -ne 0) {
            throw $result.Output
        }

        Write-Host "USB tunnel ready: phone tcp:$port -> laptop tcp:$port"
    }

    $reverseList = Invoke-CableSpeakerAdb -Arguments @('reverse', '--list')
    if ($reverseList.ExitCode -eq 0 -and $reverseList.Output.Trim()) {
        Write-Host $reverseList.Output
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
