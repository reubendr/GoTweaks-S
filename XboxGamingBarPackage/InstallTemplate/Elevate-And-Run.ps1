#Requires -Version 5.1
<#
.SYNOPSIS
    Launches Install-GoTweaks.ps1 with Administrator rights (UAC).
#>
[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$SkipCertificate,
    [switch]$CleanInstall
)

$ErrorActionPreference = 'Stop'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-ProcessExitCode {
    param([System.Diagnostics.Process]$Process)

    if ($null -eq $Process -or -not $Process.HasExited) { return 1 }
    $code = $Process.ExitCode
    if ($null -eq $code) { return 1 }
    # Seen when UAC/launch fails or PowerShell exits abnormally
    if ($code -eq -196608) { return 1 }
    return [int]$code
}

$installRoot = $PSScriptRoot
$installScript = Join-Path $installRoot 'Install-GoTweaks.ps1'
if (-not (Test-Path -LiteralPath $installScript)) {
    $installScript = Join-Path $installRoot 'Install GoTweaks.ps1'
}
if (-not (Test-Path -LiteralPath $installScript)) {
    Write-Host ''
    Write-Host '  ERROR: Installer script not found in this folder.' -ForegroundColor Red
    Write-Host '  Extract the full GoTweaksS zip, then run Install GoTweaks.cmd from that folder.' -ForegroundColor Gray
    Write-Host ''
    Read-Host 'Press Enter to exit'
    exit 1
}

Get-ChildItem -LiteralPath $installRoot -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -in '.ps1', '.cmd' } |
    ForEach-Object { Unblock-File -LiteralPath $_.FullName -ErrorAction SilentlyContinue }

$installArgs = @()
if ($Force) { $installArgs += '-Force' }
if ($SkipCertificate) { $installArgs += '-SkipCertificate' }
if ($CleanInstall) { $installArgs += '-CleanInstall' }

if (Test-IsAdministrator) {
    & $installScript @installArgs
    if ($null -ne $LASTEXITCODE) { exit $LASTEXITCODE }
    exit 0
}

Write-Host ''
Write-Host '  GoTweaks S Installer' -ForegroundColor Cyan
Write-Host '  Click Yes on the Administrator (UAC) prompt...' -ForegroundColor Gray
Write-Host ''

$psExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$psArgs = @(
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-NoLogo',
    '-File', $installScript
) + $installArgs

try {
    $proc = Start-Process -FilePath $psExe -Verb RunAs -WorkingDirectory $installRoot `
        -ArgumentList $psArgs -PassThru -Wait
}
catch {
    Write-Host ''
    Write-Host '  UAC was cancelled or elevation failed.' -ForegroundColor Yellow
    Write-Host '  Try: right-click Install GoTweaks.cmd -> Run as administrator' -ForegroundColor Gray
    Write-Host ''
    Read-Host 'Press Enter to exit'
    exit 1
}

$exitCode = Get-ProcessExitCode -Process $proc
if ($exitCode -ne 0) {
    Write-Host ''
    Write-Host "  Install did not complete (exit code $exitCode)." -ForegroundColor Red
    Write-Host '  If an Admin PowerShell window opened, read the error shown there.' -ForegroundColor Gray
    Write-Host ''
    Read-Host 'Press Enter to exit'
}
exit $exitCode
