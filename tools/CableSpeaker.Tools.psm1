Set-StrictMode -Version Latest

function Get-CableSpeakerRepoRoot {
    $toolsDir = Split-Path -Parent $PSScriptRoot
    if ((Split-Path -Leaf $PSScriptRoot) -eq 'tools') {
        return $toolsDir
    }

    return (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

function Get-CableSpeakerAdb {
    $repoRoot = Get-CableSpeakerRepoRoot
    $localAdb = Join-Path $repoRoot 'tools\platform-tools\adb.exe'
    if (Test-Path -LiteralPath $localAdb) {
        return (Resolve-Path -LiteralPath $localAdb).Path
    }

    $pathAdb = Get-Command adb.exe -ErrorAction SilentlyContinue
    if ($pathAdb) {
        return $pathAdb.Source
    }

    throw "ADB was not found. Run tools\Get-PlatformTools.ps1 first."
}

function Invoke-CableSpeakerAdb {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [int] $TimeoutSeconds = 20
    )

    $adb = Get-CableSpeakerAdb
    $stdoutPath = [System.IO.Path]::GetTempFileName()
    $stderrPath = [System.IO.Path]::GetTempFileName()

    try {
        $process = Start-Process -FilePath $adb `
            -ArgumentList $Arguments `
            -NoNewWindow `
            -PassThru `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath

        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            throw "ADB timed out after $TimeoutSeconds seconds: adb $($Arguments -join ' ')"
        }

        $stdout = Get-Content -LiteralPath $stdoutPath -Raw -ErrorAction SilentlyContinue
        $stderr = Get-Content -LiteralPath $stderrPath -Raw -ErrorAction SilentlyContinue
        $output = (($stdout, $stderr) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join [Environment]::NewLine

        [pscustomobject]@{
            ExitCode = $process.ExitCode
            Output = $output
            Adb = $adb
        }
    } finally {
        Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Assert-CableSpeakerPhoneReady {
    $result = Invoke-CableSpeakerAdb -Arguments @('devices')
    $deviceLines = @($result.Output -split "`r?`n" | Where-Object { $_ -match "`t" })
    if (-not $deviceLines -or $deviceLines.Count -eq 0) {
        throw "No Android phone was detected. Connect the phone by USB and enable USB debugging."
    }

    if ($result.ExitCode -ne 0) {
        throw $result.Output
    }

    $authorized = $deviceLines | Where-Object { $_ -match "`tdevice$" } | Select-Object -First 1
    if (-not $authorized) {
        throw "Phone was detected but is not authorized. Unlock the phone and accept the USB debugging prompt.`n$result.Output"
    }

    return $authorized
}

Export-ModuleMember -Function Get-CableSpeakerRepoRoot, Get-CableSpeakerAdb, Invoke-CableSpeakerAdb, Assert-CableSpeakerPhoneReady
