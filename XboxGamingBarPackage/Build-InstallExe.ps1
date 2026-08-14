<#
.SYNOPSIS
    Converts Install.ps1 to package installers for each *_Test folder.

    Always copies Install GoTweaks.ps1 + Install GoTweaks.cmd (recommended: double-click
    the .cmd — plain PowerShell, auto-elevates, rarely flagged by Defender).

    Optionally builds Install.exe via ps2exe (-BuildExe). ps2exe wrappers are often
    reported as trojans/heuristics by Windows Defender even when harmless.

.PARAMETER PackageDir
    The AppPackages directory containing the built packages.

.PARAMETER IconPath
    Optional path to an ICO file for the EXE icon.

.PARAMETER BuildExe
    Also build Install.exe via ps2exe (may trigger antivirus false positives).

.EXAMPLE
    .\Build-InstallExe.ps1 -PackageDir ".\AppPackages"
#>
param(
    [Parameter(Mandatory=$true)]
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
    Write-Host "  (Install GoTweaks.cmd + .ps1 only; pass -BuildExe for Install.exe)" -ForegroundColor Gray
}
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# ps2exe only needed when building Install.exe
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

# Find package folders
$packageFolders = Get-ChildItem -Path $PackageDir -Directory -Filter "*_Test" -ErrorAction SilentlyContinue

if (-not $packageFolders -or $packageFolders.Count -eq 0) {
    Write-Host "No *_Test package folders found in: $PackageDir" -ForegroundColor Yellow
    exit 0
}

Write-Host "Found $($packageFolders.Count) package folder(s)" -ForegroundColor Gray

$successCount = 0
$failCount = 0

$templateScript = Join-Path $PSScriptRoot "InstallTemplate\Install GoTweaks.ps1"
$templateCmd = Join-Path $PSScriptRoot "InstallTemplate\Install GoTweaks.cmd"
$pfxPath = Join-Path $PSScriptRoot "XboxGamingBarPackage_TemporaryKey.pfx"
if (-not (Test-Path $templateScript)) {
    Write-Host "ERROR: Template script not found: $templateScript" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $templateCmd)) {
    Write-Host "ERROR: Template launcher not found: $templateCmd" -ForegroundColor Red
    exit 1
}

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

foreach ($folder in $packageFolders) {
    $scriptPath = Join-Path $folder.FullName "Install GoTweaks.ps1"
    $cmdPath = Join-Path $folder.FullName "Install GoTweaks.cmd"
    $exePath = Join-Path $folder.FullName "Install.exe"

    # Copy our custom installer and overwrite MSBuild default Install.ps1
    # (the stock one wraps Add-AppDevPackage.ps1 and needs a developer license).
    Write-Host "  Copying custom installer to $($folder.Name)..." -ForegroundColor Gray
    Copy-Item -Path $templateScript -Destination $scriptPath -Force
    Copy-Item -Path $templateScript -Destination (Join-Path $folder.FullName "Install.ps1") -Force
    Copy-Item -Path $templateCmd -Destination $cmdPath -Force
    Export-PackageSigningCertificate -OutputDir $folder.FullName -PfxPath $pfxPath | Out-Null

    if (-not (Test-Path $scriptPath) -or -not (Test-Path $cmdPath)) {
        Write-Host "  SKIP: $($folder.Name) - Failed to copy installer scripts" -ForegroundColor Yellow
        $failCount++
        continue
    }

    Write-Host "  SUCCESS: Install GoTweaks.cmd + Install GoTweaks.ps1" -ForegroundColor Green
    $successCount++

    if (-not $BuildExe) {
        continue
    }

    Write-Host ""
    Write-Host "Converting: $($folder.Name) -> Install.exe" -ForegroundColor Cyan

    # Build ps2exe parameters
    $ps2exeParams = @{
        InputFile = $scriptPath
        OutputFile = $exePath
        NoConsole = $false           # Keep console for user feedback
        RequireAdmin = $false        # Script handles elevation itself
        Title = "GoTweaks S"
        Description = "Installer for GoTweaks S Xbox Game Bar Widget"
        Company = "GoTweaks S"
        Product = "GoTweaks S"
        Copyright = "Copyright (c) GoTweaks S"
        Version = "1.0.0.0"
    }

    # Add icon if provided and exists
    if ($IconPath -and (Test-Path $IconPath)) {
        $ps2exeParams.IconFile = $IconPath
        Write-Host "  Using icon: $IconPath" -ForegroundColor Gray
    }

    try {
        # ps2exe writes to host, capture it
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

if ($failCount -gt 0) {
    exit 1
}
exit 0
