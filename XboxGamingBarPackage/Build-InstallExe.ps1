<#
.SYNOPSIS
    Sets up InstallGoTweaks.cmd + _install payload for each *_Test package folder.

    User-facing: only InstallGoTweaks.cmd at the package root. Package files live in _install\.

    Optionally builds Install.exe via ps2exe (-BuildExe). ps2exe wrappers are often flagged by AV.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDir,

    [string]$IconPath = $null,

    [switch]$BuildExe
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Building package installers" -ForegroundColor White
if ($BuildExe) {
    Write-Host "  (including Install.exe via ps2exe)" -ForegroundColor Gray
}
else {
    Write-Host "  (InstallGoTweaks.cmd + _install\ only)" -ForegroundColor Gray
}
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

if ($BuildExe) {
    if (-not (Get-Module -ListAvailable -Name ps2exe)) {
        Write-Host "Installing ps2exe module..." -ForegroundColor Yellow
        try {
            $ProgressPreference = 'SilentlyContinue'
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            $null = Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope CurrentUser -ErrorAction SilentlyContinue
            Install-Module -Name ps2exe -Force -Scope CurrentUser -AllowClobber -SkipPublisherCheck -ErrorAction Stop
            Write-Host "ps2exe module installed successfully." -ForegroundColor Green
        }
        catch {
            Write-Host "ERROR: Failed to install ps2exe module: $_" -ForegroundColor Red
            exit 1
        }
    }
    Import-Module ps2exe -ErrorAction Stop
}

$packageFolders = Get-ChildItem -Path $PackageDir -Directory -Filter "*_Test" -ErrorAction SilentlyContinue
if (-not $packageFolders -or $packageFolders.Count -eq 0) {
    Write-Host "No *_Test package folders found in: $PackageDir" -ForegroundColor Yellow
    exit 0
}

Write-Host "Found $($packageFolders.Count) package folder(s)" -ForegroundColor Gray

$templateScript = Join-Path $PSScriptRoot "InstallTemplate\_install\InstallGoTweaks.ps1"
$templateCmd = Join-Path $PSScriptRoot "InstallTemplate\InstallGoTweaks.cmd"
$pfxPath = Join-Path $PSScriptRoot "XboxGamingBarPackage_TemporaryKey.pfx"

foreach ($required in @($templateScript, $templateCmd)) {
    if (-not (Test-Path $required)) {
        Write-Host "ERROR: Template not found: $required" -ForegroundColor Red
        exit 1
    }
}

$legacyInstallerFiles = @(
    "Install GoTweaks.cmd",
    "Install GoTweaks.ps1",
    "Install-GoTweaks.ps1",
    "Elevate-And-Run.ps1",
    "Install.ps1",
    "Install.exe"
)

function Export-PackageSigningCertificate {
    param(
        [string]$OutputDir,
        [string]$PfxPath
    )
    $cerPath = Join-Path $OutputDir "GoTweaksSigning.cer"
    if (Test-Path $cerPath) { return $cerPath }
    if (-not (Test-Path $PfxPath)) {
        Write-Host "  WARN: PFX not found at $PfxPath - bundle .cer not exported" -ForegroundColor Yellow
        return $null
    }
    try {
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
            $PfxPath, "", [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
        Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
        $cert.Dispose()
        Write-Host "  Exported GoTweaksSigning.cer" -ForegroundColor Green
        return $cerPath
    }
    catch {
        Write-Host "  WARN: Failed to export signing .cer: $_" -ForegroundColor Yellow
        return $null
    }
}

function Move-IfExists {
    param(
        [string]$SourcePath,
        [string]$DestinationDir
    )
    if (-not (Test-Path -LiteralPath $SourcePath)) { return }
    $dest = Join-Path $DestinationDir (Split-Path $SourcePath -Leaf)
    if (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest -Force }
    Move-Item -LiteralPath $SourcePath -Destination $dest -Force
}

$successCount = 0
$failCount = 0

foreach ($folder in $packageFolders) {
    Write-Host "  Layout installer for $($folder.Name)..." -ForegroundColor Gray

    $installDir = Join-Path $folder.FullName "_install"
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null

    Copy-Item -Path $templateScript -Destination (Join-Path $installDir "InstallGoTweaks.ps1") -Force
    Copy-Item -Path $templateCmd -Destination (Join-Path $folder.FullName "InstallGoTweaks.cmd") -Force

    # Consolidate payload under _install (from root or leftover previous layouts)
    foreach ($pattern in @("*.msixbundle", "*.cer")) {
        Get-ChildItem -Path $folder.FullName -Filter $pattern -File -ErrorAction SilentlyContinue | ForEach-Object {
            Move-IfExists -SourcePath $_.FullName -DestinationDir $installDir
        }
        Get-ChildItem -Path $installDir -Filter $pattern -File -ErrorAction SilentlyContinue | Out-Null
        Get-ChildItem -Path $folder.FullName -Filter $pattern -File -ErrorAction SilentlyContinue | ForEach-Object {
            Move-IfExists -SourcePath $_.FullName -DestinationDir $installDir
        }
    }

    $rootDeps = Join-Path $folder.FullName "Dependencies"
    $installDeps = Join-Path $installDir "Dependencies"
    if ((Test-Path $rootDeps) -and -not (Test-Path $installDeps)) {
        Move-Item -LiteralPath $rootDeps -Destination $installDeps -Force
    }

    Export-PackageSigningCertificate -OutputDir $installDir -PfxPath $pfxPath | Out-Null

    foreach ($legacyName in $legacyInstallerFiles) {
        $legacyPath = Join-Path $folder.FullName $legacyName
        if (Test-Path -LiteralPath $legacyPath) {
            Remove-Item -LiteralPath $legacyPath -Force -ErrorAction SilentlyContinue
        }
    }

    $cmdPath = Join-Path $folder.FullName "InstallGoTweaks.cmd"
    $scriptPath = Join-Path $installDir "InstallGoTweaks.ps1"
    $bundle = Get-ChildItem -Path $installDir -Filter "*.msixbundle" -ErrorAction SilentlyContinue | Select-Object -First 1

    if (-not (Test-Path $cmdPath) -or -not (Test-Path $scriptPath) -or -not $bundle) {
        Write-Host "  SKIP: $($folder.Name) - missing InstallGoTweaks.cmd, script, or msixbundle" -ForegroundColor Yellow
        $failCount++
        continue
    }

    Write-Host "  SUCCESS: InstallGoTweaks.cmd + _install\" -ForegroundColor Green
    $successCount++

    if (-not $BuildExe) { continue }

    Write-Host ""
    Write-Host "Converting: $($folder.Name) -> Install.exe" -ForegroundColor Cyan
    $exePath = Join-Path $folder.FullName "Install.exe"

    $ps2exeParams = @{
        InputFile    = $scriptPath
        OutputFile   = $exePath
        NoConsole    = $false
        RequireAdmin = $false
        Title        = "GoTweaks S"
        Description  = "Installer for GoTweaks S Xbox Game Bar Widget"
        Company      = "GoTweaks S"
        Product      = "GoTweaks S"
        Copyright    = "Copyright (c) GoTweaks S"
        Version      = "1.0.0.0"
    }
    if ($IconPath -and (Test-Path $IconPath)) {
        $ps2exeParams.IconFile = $IconPath
    }

    try {
        $null = Invoke-ps2exe @ps2exeParams 2>&1
        if (Test-Path $exePath) {
            $exeSize = (Get-Item $exePath).Length / 1KB
            Write-Host "  SUCCESS: Created Install.exe ($([math]::Round($exeSize, 1)) KB)" -ForegroundColor Green
        }
        else {
            Write-Host "  FAIL: Install.exe was not created" -ForegroundColor Red
            $failCount++
        }
    }
    catch {
        Write-Host "  FAIL: $_" -ForegroundColor Red
        $failCount++
    }
}

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Summary: $successCount succeeded, $failCount failed" -ForegroundColor White
Write-Host "=============================================" -ForegroundColor Cyan

if ($failCount -gt 0) { exit 1 }
exit 0
