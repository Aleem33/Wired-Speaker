[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    $devices = Get-CimInstance Win32_SoundDevice |
        Select-Object -ExpandProperty Name |
        Where-Object { $_ -match 'CABLE|VB-Audio' }

    if ($devices) {
        Write-Host "VB-CABLE style audio device detected:"
        $devices | ForEach-Object { Write-Host " - $_" }
        exit 0
    }

    Write-Host "VB-CABLE was not detected." -ForegroundColor Yellow
    Write-Host "Install it from https://vb-audio.com/Cable/ to make the phone mic appear as a selectable PC microphone."
    exit 1
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
