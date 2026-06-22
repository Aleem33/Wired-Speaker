[CmdletBinding()]
param(
    [string] $WindowsAppPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'CableSpeaker.Tools.psm1') -Force

& (Join-Path $PSScriptRoot 'Check-Phone.ps1')
& (Join-Path $PSScriptRoot 'Setup-UsbTunnel.ps1')

if ($WindowsAppPath) {
    $resolvedApp = Resolve-Path -LiteralPath $WindowsAppPath
    Start-Process -FilePath $resolvedApp.Path
    Write-Host "Started Windows app: $($resolvedApp.Path)"
    return
}

$repoRoot = Get-CableSpeakerRepoRoot
$publishedExe = Get-ChildItem -Path $repoRoot -Recurse -Filter 'CableSpeaker.exe' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\publish\\|\\artifacts\\|\\Release\\' } |
    Select-Object -First 1

if ($publishedExe) {
    Start-Process -FilePath $publishedExe.FullName
    Write-Host "Started Windows app: $($publishedExe.FullName)"
} else {
    Write-Host "USB tunnel is ready. Start CableSpeaker.exe from the Windows build artifact, then press Start."
}

