#Requires -Version 5.1
<#
.SYNOPSIS
    Custom installer for GoTweaks Xbox Game Bar Widget.

.DESCRIPTION
    Professional installer that handles Debug-to-Release upgrades, dependency checking,
    and blocking process management with user-friendly prompts.

.PARAMETER Force
    Suppress confirmation prompts for silent/unattended installation.

.PARAMETER SkipCertificate
    Skip certificate installation (use if certificate is already trusted).

.PARAMETER CleanInstall
    Remove existing package before installing (loses user settings/profiles).
    Default is to update in-place which preserves user data.

.EXAMPLE
    .\Install.ps1
    Interactive installation with prompts. Updates existing install, preserving settings.

.EXAMPLE
    .\Install.ps1 -Force
    Silent installation without prompts.

.EXAMPLE
    .\Install.ps1 -CleanInstall
    Remove existing package first (fresh install, loses settings).

.NOTES
    Must be run as Administrator.
#>

param(
    [switch]$Force = $false,
    [switch]$SkipCertificate = $false,
    [switch]$CleanInstall = $false
)

$ErrorActionPreference = "Stop"

# Global error handler
trap {
    Write-Host ""
    Write-Host "=============================================" -ForegroundColor Red
    Write-Host "  UNEXPECTED ERROR" -ForegroundColor Red
    Write-Host "=============================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Stack Trace:" -ForegroundColor Yellow
    Write-Host $_.ScriptStackTrace -ForegroundColor Gray
    Write-Host ""
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

#region EXE-Compatible Path Detection

function Get-InstallerPath {
    $exePath = [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
    if ($exePath -and $exePath -match '\.exe$' -and $exePath -notmatch 'powershell\.exe$|pwsh\.exe$') {
        return $exePath
    }
    if ($PSCommandPath) { return $PSCommandPath }
    if ($MyInvocation.MyCommand.Path) { return $MyInvocation.MyCommand.Path }
    return $null
}

function Get-InstallerDirectory {
    $installerPath = Get-InstallerPath
    if ($installerPath) {
        return [System.IO.Path]::GetDirectoryName($installerPath)
    }
    if ($PSScriptRoot) { return $PSScriptRoot }
    return (Get-Location).Path
}

function Test-RunningAsExe {
    $exePath = [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
    return ($exePath -and $exePath -match '\.exe$' -and $exePath -notmatch 'powershell\.exe$|pwsh\.exe$')
}

#endregion

$PackageName = "PlayandBuildCustom.10365195AA1EC"

# Processes that may block installation (helper copies PresentMon to LocalCache and keeps it running)
$BlockingProcesses = @(
    "XboxGamingBarHelper",
    "PresentMon",
    "XboxGamingBar",
    "GameBar",
    "GameBarFTServer",
    "GameBarPresenceWriter",
    "XboxGamingBarWidget"
)

#region Helper Functions

function Write-Step {
    param([int]$Step, [int]$Total, [string]$Message)
    Write-Host "`n[$Step/$Total] " -ForegroundColor Cyan -NoNewline
    Write-Host $Message -ForegroundColor White
}

function Write-Success {
    param([string]$Message)
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "       $Message" -ForegroundColor Gray
}

function Write-Warn {
    param([string]$Message)
    Write-Host "  [!] $Message" -ForegroundColor Yellow
}

function Write-Err {
    param([string]$Message)
    Write-Host "  [X] $Message" -ForegroundColor Red
}

function Exit-WithPause {
    param([int]$ExitCode = 0)
    Write-Host ""
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    try {
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    }
    catch {
        Read-Host "Press Enter to exit"
    }
    exit $ExitCode
}

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-CertificateThumbprint {
    param([string]$Path)
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($Path)
    try { return $cert.Thumbprint.ToUpperInvariant() }
    finally { $cert.Dispose() }
}

function Test-CertificateInLocalMachineStore {
    param(
        [string]$Thumbprint,
        [string[]]$StoreNames = @("Root", "TrustedPeople")
    )
    $normalized = ($Thumbprint -replace '\s', '').ToUpperInvariant()
    foreach ($storeName in $StoreNames) {
        $storePath = "Cert:\LocalMachine\$storeName"
        if (-not (Test-Path $storePath)) { continue }
        $found = Get-ChildItem -Path $storePath -ErrorAction SilentlyContinue |
            Where-Object { ($_.Thumbprint -replace '\s', '').ToUpperInvariant() -eq $normalized }
        if ($found) { return $true }
    }
    return $false
}

function Test-SideloadCertificateTrusted {
    param([string]$Thumbprint)
    # MSIX sideload without Developer Mode needs the signing cert in Root at minimum.
    return (Test-CertificateInLocalMachineStore -Thumbprint $Thumbprint -StoreNames @("Root"))
}

function Install-SideloadSigningCertificate {
    param([string]$CertificatePath)
    $stores = @(
        @{ Label = "Root"; Location = "Cert:\LocalMachine\Root" },
        @{ Label = "TrustedPeople"; Location = "Cert:\LocalMachine\TrustedPeople" }
    )
    $thumbprint = Get-CertificateThumbprint -Path $CertificatePath
    foreach ($store in $stores) {
        if (Test-CertificateInLocalMachineStore -Thumbprint $thumbprint -StoreNames @($store.Label)) {
            Write-Info "Certificate already in LocalMachine\$($store.Label)"
            continue
        }
        Import-Certificate -FilePath $CertificatePath -CertStoreLocation $store.Location | Out-Null
        Write-Success "Certificate trusted in LocalMachine\$($store.Label)"
    }
}

function Enable-AppPackageSideloading {
    # Both keys are required on many Windows builds to bypass the legacy Store
    # developer-license prompt for sideloaded MSIX (Developer Mode sets these).
    $path = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"
    if (-not (Test-Path $path)) {
        New-Item -Path $path -Force | Out-Null
    }
    Set-ItemProperty -Path $path -Name AllowAllTrustedApps -Value 1 -Type DWord -Force
    Set-ItemProperty -Path $path -Name AllowDevelopmentWithoutDevLicense -Value 1 -Type DWord -Force
    Write-Success "Sideloading enabled (AllowAllTrustedApps + AllowDevelopmentWithoutDevLicense)"
}

function Resolve-SigningCertificatePath {
    param(
        [string]$ScriptDir,
        [System.IO.FileInfo]$MainPackage
    )

    $cer = Get-ChildItem -Path $ScriptDir -Filter "*.cer" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($cer) { return $cer.FullName }

    if ($MainPackage) {
        try {
            $sig = Get-AuthenticodeSignature -FilePath $MainPackage.FullName -ErrorAction Stop
            if ($sig.SignerCertificate) {
                $tempCer = Join-Path $env:TEMP ("GoTweaksSigning_{0}.cer" -f [guid]::NewGuid().ToString("N"))
                Export-Certificate -Cert $sig.SignerCertificate -FilePath $tempCer | Out-Null
                Write-Info "Extracted signing certificate from package"
                return $tempCer
            }
        }
        catch {
            Write-Warn "Could not extract certificate from package: $($_.Exception.Message)"
        }
    }

    return $null
}

function Unblock-InstallerFiles {
    param([string]$Dir)

    $paths = @()
    if ($script:ScriptPath) { $paths += $script:ScriptPath }
    if ($Dir -and (Test-Path -LiteralPath $Dir)) {
        $paths += Get-ChildItem -LiteralPath $Dir -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Extension -in @('.ps1', '.cmd', '.cer', '.msixbundle') }
    }

    foreach ($item in $paths) {
        $path = if ($item -is [string]) { $item } else { $item.FullName }
        if ($path -and (Test-Path -LiteralPath $path)) {
            Unblock-File -LiteralPath $path -ErrorAction SilentlyContinue
        }
    }
}

function Request-Elevation {
    $elevateScriptPath = $script:ScriptPath
    $workDir = Get-InstallerDirectory

    if (-not $elevateScriptPath) {
        Write-Err "Cannot determine installer path for elevation."
        Write-Host "Please right-click Install GoTweaks.cmd and choose Run as administrator." -ForegroundColor Yellow
        Exit-WithPause -ExitCode 1
    }

    $switchArgs = @()
    if ($Force) { $switchArgs += '-Force' }
    if ($SkipCertificate) { $switchArgs += '-SkipCertificate' }
    if ($CleanInstall) { $switchArgs += '-CleanInstall' }

    try {
        if (Test-RunningAsExe) {
            $exeArgs = @()
            if ($switchArgs.Count -gt 0) { $exeArgs = $switchArgs }
            $proc = Start-Process -FilePath $elevateScriptPath -Verb RunAs -WorkingDirectory $workDir `
                -ArgumentList $exeArgs -PassThru -Wait
        }
        else {
            # ArgumentList must be an array — a single string breaks -File and exits instantly
            $psArgs = @(
                '-NoProfile',
                '-ExecutionPolicy', 'Bypass',
                '-NoLogo',
                '-File', $elevateScriptPath
            ) + $switchArgs

            $proc = Start-Process -FilePath 'powershell.exe' -Verb RunAs -WorkingDirectory $workDir `
                -ArgumentList $psArgs -PassThru -Wait
        }

        $exitCode = if ($null -ne $proc.ExitCode) { $proc.ExitCode } else { 1 }
        exit $exitCode
    }
    catch {
        Write-Err "Failed to elevate to Administrator: $_"
        Write-Host "UAC was cancelled or denied. Run Install GoTweaks.cmd again and click Yes." -ForegroundColor Yellow
        Exit-WithPause -ExitCode 1
    }
}

function Get-RunningBlockers {
    $running = @()
    foreach ($procName in $BlockingProcesses) {
        $procs = Get-Process -Name $procName -ErrorAction SilentlyContinue
        if ($procs) {
            $running += $procName
        }
    }
    return $running
}

function Stop-GoTweaksScheduledTask {
    $taskPath = "\GoTweaks\"
    $taskName = "GoTweaksHelper"
    $schtasksName = "GoTweaks\GoTweaksHelper"

    try {
        & schtasks.exe /End /TN $schtasksName 2>$null | Out-Null
        & schtasks.exe /Change /TN $schtasksName /DISABLE 2>$null | Out-Null
    }
    catch { }

    try {
        Stop-ScheduledTask -TaskPath $taskPath -TaskName $taskName -ErrorAction SilentlyContinue | Out-Null
        Disable-ScheduledTask -TaskPath $taskPath -TaskName $taskName -ErrorAction SilentlyContinue | Out-Null
    }
    catch { }

    Start-Sleep -Milliseconds 500
}

function Stop-ProcessesByPathPattern {
    param([string[]]$Patterns)

    $killed = @()
    $processes = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue
    foreach ($proc in $processes) {
        $path = $proc.ExecutablePath
        if (-not $path) { continue }

        foreach ($pattern in $Patterns) {
            if ($path -like $pattern) {
                try {
                    Stop-Process -Id $proc.ProcessId -Force -ErrorAction Stop
                    $killed += [System.IO.Path]::GetFileNameWithoutExtension($path)
                }
                catch { }
                break
            }
        }
    }

    return ($killed | Select-Object -Unique)
}

function Stop-BlockingProcesses {
    param([switch]$Quiet)

    Stop-GoTweaksScheduledTask

    $killed = @()
    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        foreach ($procName in $BlockingProcesses) {
            try {
                & taskkill.exe /F /T /IM "$procName.exe" 2>$null | Out-Null
            }
            catch { }

            $procs = Get-Process -Name $procName -ErrorAction SilentlyContinue
            if ($procs) {
                foreach ($proc in $procs) {
                    try {
                        $proc | Stop-Process -Force -ErrorAction Stop
                        $killed += $procName
                    }
                    catch {
                        if (-not $Quiet) {
                            Write-Warn "Could not stop $procName (PID: $($proc.Id))"
                        }
                    }
                }
            }
        }

        $killed += Stop-ProcessesByPathPattern -Patterns @(
            "*\Packages\PlayandBuildCustom*\LocalCache\GoTweaks\*",
            "*\WindowsApps\PlayandBuildCustom*\*XboxGamingBarHelper*",
            "*\GoTweaks\Helper\*"
        )

        if ((Get-RunningBlockers).Count -eq 0) {
            break
        }

        Start-Sleep -Milliseconds 1000
    }

    if ($killed.Count -gt 0) {
        Start-Sleep -Milliseconds 2000
    }

    return ($killed | Select-Object -Unique)
}

function Remove-AllGoTweaksPackages {
    $removed = @()
    Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            Write-Info "Removing package: $($_.PackageFullName)"
            Remove-AppxPackage -Package $_.PackageFullName -ErrorAction Stop
            $removed += $_.PackageFullName
        }
        catch {
            Write-Warn "Could not remove $($_.PackageFullName): $($_.Exception.Message)"
        }
    }

    try {
        Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -eq $PackageName } |
            ForEach-Object {
                Write-Info "Removing provisioned package: $($_.PackageName)"
                Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue | Out-Null
            }
    }
    catch { }

    if ($removed.Count -gt 0) {
        Start-Sleep -Seconds 2
    }
    return $removed
}

function Clear-GoTweaksLocalCache {
    $cleared = $false
    $packageRoots = Get-ChildItem -Path (Join-Path $env:LOCALAPPDATA "Packages") -Filter "PlayandBuildCustom*" -ErrorAction SilentlyContinue
    foreach ($root in $packageRoots) {
        $pathsToClear = @(
            (Join-Path $root.FullName "LocalCache\GoTweaks"),
            (Join-Path $root.FullName "LocalCache\GoTweaks\Helper"),
            (Join-Path $root.FullName "LocalCache\GoTweaks\Helper\PresentMon.exe")
        )

        foreach ($cachePath in $pathsToClear) {
            if (-not (Test-Path $cachePath)) { continue }

            $removedPath = $false
            for ($attempt = 0; $attempt -lt 3 -and -not $removedPath; $attempt++) {
                try {
                    Remove-Item -Path $cachePath -Recurse -Force -ErrorAction Stop
                    Write-Success "Cleared LocalCache: $cachePath"
                    $cleared = $true
                    $removedPath = $true
                }
                catch {
                    Stop-BlockingProcesses -Quiet | Out-Null
                    try {
                        $newName = "$(Split-Path $cachePath -Leaf).old.$([guid]::NewGuid().ToString('N'))"
                        Rename-Item -Path $cachePath -NewName $newName -ErrorAction Stop
                        Remove-Item -Path (Join-Path (Split-Path $cachePath -Parent) $newName) -Recurse -Force -ErrorAction Stop
                        Write-Success "Renamed and cleared locked LocalCache: $cachePath"
                        $cleared = $true
                        $removedPath = $true
                    }
                    catch {
                        if ($attempt -eq 2) {
                            Write-Warn "Could not clear LocalCache ($cachePath): $($_.Exception.Message)"
                        }
                    }
                }
            }
        }
    }
    return $cleared
}

function Prepare-GoTweaksForInstall {
    param([switch]$RemoveExistingPackage)

    Stop-BlockingProcesses -Quiet | Out-Null
    if ($RemoveExistingPackage) {
        Remove-AllGoTweaksPackages | Out-Null
    }
    Clear-GoTweaksLocalCache | Out-Null
    Stop-BlockingProcesses -Quiet | Out-Null
    Start-Sleep -Seconds 1
}

function Get-DependencyPackages {
    param([string]$DependenciesDir)

    $packages = @()

    # Only get x64 dependencies - this project only supports x64
    $x64Path = Join-Path $DependenciesDir "x64"
    if (Test-Path $x64Path) {
        $packages += Get-ChildItem -Path $x64Path -Filter "*.appx" -ErrorAction SilentlyContinue
        $packages += Get-ChildItem -Path $x64Path -Filter "*.msix" -ErrorAction SilentlyContinue
    }

    return $packages
}

function Test-DependencyInstalled {
    param([string]$PackageBaseName)

    # Extract the core package name (remove .Debug suffix if present)
    # e.g., "Microsoft.VCLibs.140.00.Debug" -> "Microsoft.VCLibs.140.00"
    $coreName = $PackageBaseName -replace '\.Debug$', ''

    # Check if either the exact package or the non-debug variant is installed
    $installedPackages = Get-AppxPackage -ErrorAction SilentlyContinue

    foreach ($pkg in $installedPackages) {
        # Check for exact match (e.g., Microsoft.VCLibs.140.00.Debug)
        if ($pkg.Name -eq $PackageBaseName) {
            return $true
        }
        # Check for non-debug variant (e.g., Microsoft.VCLibs.140.00)
        if ($pkg.Name -eq $coreName) {
            return $true
        }
        # Check for x64 specific variants
        if ($pkg.Name -eq "$coreName.x64" -or $pkg.Name -eq "$PackageBaseName.x64") {
            return $true
        }
    }

    return $false
}

function Get-MissingDependencies {
    param([array]$DependencyPackages)

    $missing = @()

    foreach ($dep in $DependencyPackages) {
        # Extract package name from filename
        # e.g., "Microsoft.VCLibs.x64.14.00.Desktop.Debug.appx" -> base name analysis
        $baseName = $dep.BaseName -replace '\.x64$|\.x86$|\.arm64$|\.arm$', ''

        # Try to extract a cleaner package name for checking
        # Handle patterns like "Microsoft.VCLibs.x64.14.00.Desktop.Debug"
        if ($baseName -match '^(Microsoft\.[^.]+)\.x64\.(.+)$') {
            # Reformat: Microsoft.VCLibs.x64.14.00 -> Microsoft.VCLibs.14.00
            $baseName = "$($Matches[1]).$($Matches[2])"
        }

        if (-not (Test-DependencyInstalled -PackageBaseName $baseName)) {
            $missing += $dep
        }
    }

    return $missing
}

function Get-PackageVersion {
    param([string]$PackagePath)

    # Extract version from package filename
    # e.g., "XboxGamingBarPackage_0.3.1137.0_x64.msixbundle" -> "0.3.1137.0"
    if ($PackagePath -match '_(\d+\.\d+\.\d+\.\d+)_') {
        return $Matches[1]
    }
    return "Unknown"
}

#endregion

#region Main Script

$script:ScriptPath = Get-InstallerPath
$ScriptDir = Get-InstallerDirectory
Unblock-InstallerFiles -Dir $ScriptDir

# Elevate before any UI — fixes double-click launches and Run-with-PowerShell
if (-not (Test-Administrator)) {
    Write-Host ""
    Write-Host "  GoTweaks S Installer" -ForegroundColor Cyan
    Write-Host "  Requesting Administrator access (approve the UAC prompt)..." -ForegroundColor Gray
    Write-Host ""
    Request-Elevation
}

Clear-Host

# Find main package early so we can show version
$MainPackage = Get-ChildItem -Path $ScriptDir -Filter "*.msixbundle" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $MainPackage) {
    $MainPackage = Get-ChildItem -Path $ScriptDir -Filter "*.appxbundle" -ErrorAction SilentlyContinue | Select-Object -First 1
}
if (-not $MainPackage) {
    $MainPackage = Get-ChildItem -Path $ScriptDir -Filter "*.msix" -ErrorAction SilentlyContinue | Select-Object -First 1
}

$packageVersion = if ($MainPackage) { Get-PackageVersion -PackagePath $MainPackage.Name } else { "Unknown" }

# Welcome Banner
Write-Host ""
Write-Host "  =============================================" -ForegroundColor Cyan
Write-Host "                                               " -ForegroundColor Cyan
Write-Host "         GoTweaks S                                " -ForegroundColor White
Write-Host "         Xbox Game Bar Widget                  " -ForegroundColor Gray
Write-Host "                                               " -ForegroundColor Cyan
Write-Host "         Version: $packageVersion                      " -ForegroundColor DarkGray
Write-Host "                                               " -ForegroundColor Cyan
Write-Host "  =============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  This installer will set up GoTweaks S on your system." -ForegroundColor Gray
Write-Host "  GoTweaks S provides TDP control, performance monitoring," -ForegroundColor Gray
Write-Host "  and more for handheld gaming devices." -ForegroundColor Gray
Write-Host ""

# Check for existing installation
$existingPkg = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue
if ($existingPkg) {
    Write-Host "  Existing installation detected: v$($existingPkg.Version)" -ForegroundColor Yellow
    Write-Host ""
}

if (-not $Force) {
    Write-Host "  Press Enter to continue or Ctrl+C to cancel..." -ForegroundColor DarkGray
    Read-Host | Out-Null
}

# Phase 1: Check Administrator (elevated at script start)
Write-Step -Step 1 -Total 6 -Message "Checking administrator privileges..."
Write-Success "Running as Administrator"

# Phase 2: Locate package files
Write-Step -Step 2 -Total 6 -Message "Locating package files..."

if (-not $MainPackage) {
    $MainPackage = Get-ChildItem -Path $ScriptDir -Filter "*.appx" -ErrorAction SilentlyContinue | Select-Object -First 1
}

if (-not $MainPackage) {
    Write-Err "No package file found in $ScriptDir"
    Write-Info "Expected: .msixbundle, .appxbundle, .msix, or .appx"
    Exit-WithPause -ExitCode 1
}
Write-Success "Package: $($MainPackage.Name)"

# Resolve signing certificate (.cer beside installer, or extract from signed bundle)
$script:SigningCertificatePath = Resolve-SigningCertificatePath -ScriptDir $ScriptDir -MainPackage $MainPackage
if ($script:SigningCertificatePath) {
    Write-Success "Certificate: $(Split-Path $script:SigningCertificatePath -Leaf)"
}
else {
    Write-Warn "No signing certificate found — sideload install will likely fail"
}

# Find dependencies - x64 only
# Note: We don't force-reinstall dependencies to avoid conflicts with other apps (Dolby, DTS, etc.)
$DependenciesDir = Join-Path $ScriptDir "Dependencies"
$depPackages = @()
if (Test-Path $DependenciesDir) {
    $allDepPackages = Get-DependencyPackages -DependenciesDir $DependenciesDir
    if ($allDepPackages.Count -gt 0) {
        $depPackages = Get-MissingDependencies -DependencyPackages $allDepPackages
        $skippedCount = $allDepPackages.Count - $depPackages.Count
        if ($depPackages.Count -gt 0) {
            Write-Success "Dependencies: $($depPackages.Count) missing (will install if needed)"
        }
        else {
            Write-Success "Dependencies: All present (using system libraries)"
        }
    }
}
else {
    Write-Info "No Dependencies folder"
}

# Phase 3: Check for blocking processes
Write-Step -Step 3 -Total 6 -Message "Checking for blocking processes..."

$runningBlockers = Get-RunningBlockers

if ($runningBlockers.Count -gt 0) {
    Write-Warn "The following apps need to be closed:"
    Write-Host ""
    foreach ($proc in $runningBlockers) {
        Write-Host "       - $proc" -ForegroundColor Yellow
    }
    Write-Host ""

    if (-not $Force) {
        Write-Host "       Please close Xbox Game Bar (Win+G then close it)" -ForegroundColor Cyan
        Write-Host "       and any GoTweaks windows, then press Enter." -ForegroundColor Cyan
        Write-Host ""
        Write-Host "       Or press 'F' to force-close these apps: " -ForegroundColor DarkGray -NoNewline
        $response = Read-Host

        # Check again after user says they closed
        $stillRunning = Get-RunningBlockers

        if ($stillRunning.Count -gt 0) {
            if ($response -eq 'F' -or $response -eq 'f') {
                Write-Info "Force-closing blocking processes..."
                $killedProcesses = Stop-BlockingProcesses
                if ($killedProcesses.Count -gt 0) {
                    Write-Success "Closed: $($killedProcesses -join ', ')"
                }
            }
            else {
                Write-Warn "Some apps are still running: $($stillRunning -join ', ')"
                Write-Info "Attempting to close them..."
                $killedProcesses = Stop-BlockingProcesses
                if ($killedProcesses.Count -gt 0) {
                    Write-Success "Closed: $($killedProcesses -join ', ')"
                }
            }
        }
        else {
            Write-Success "All blocking apps closed"
        }
    }
    else {
        Write-Info "Force mode: Closing blocking processes..."
        $killedProcesses = Stop-BlockingProcesses
        if ($killedProcesses.Count -gt 0) {
            Write-Success "Closed: $($killedProcesses -join ', ')"
        }
    }
}
else {
    Write-Success "No blocking processes"
}

# Phase 4: Handle existing package
if ($CleanInstall) {
    Write-Step -Step 4 -Total 6 -Message "Removing existing package (clean install)..."

    $existingPkg = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue
    if ($existingPkg) {
        Write-Warn "Clean install will remove user settings and profiles!"
        if (-not $Force) {
            Write-Host ""
            $response = Read-Host "       Continue with clean install? (Y/N)"
            if ($response -ne 'Y' -and $response -ne 'y') {
                Write-Info "Switching to update mode (preserving settings)..."
                $CleanInstall = $false
            }
        }

        if ($CleanInstall) {
            Write-Info "Removing: v$($existingPkg.Version)"
            try {
                Prepare-GoTweaksForInstall -RemoveExistingPackage
                Write-Success "Removed existing package and cleared LocalCache"
                Start-Sleep -Seconds 2
            }
            catch {
                Write-Warn "Could not remove: $_"
                Write-Info "Will attempt upgrade instead..."
            }
        }
    }
    else {
        Write-Info "No existing package to remove"
    }
}
else {
    Write-Step -Step 4 -Total 6 -Message "Checking existing installation..."
    $existingPkg = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue
    if ($existingPkg) {
        Write-Info "Will update existing installation (preserving settings)"
        Write-Info "Current version: $($existingPkg.Version)"
    }
    else {
        Write-Info "Fresh installation"
    }
}

# Phase 5: Trust signing cert + enable sideloading (no Developer Mode UI required)
Write-Step -Step 5 -Total 6 -Message "Preparing sideload trust..."

try {
    Enable-AppPackageSideloading
}
catch {
    Write-Err "Failed to enable sideloading: $($_.Exception.Message)"
    Exit-WithPause -ExitCode 1
}

if ($SkipCertificate) {
    Write-Info "Certificate trust skipped (--SkipCertificate)"
}
elseif (-not $script:SigningCertificatePath) {
    Write-Err "Cannot trust package: no .cer file and could not read cert from the bundle."
    Write-Info "Enable Developer Mode, or rebuild so GoTweaksSigning.cer is bundled."
    Exit-WithPause -ExitCode 1
}
else {
    $thumbprint = Get-CertificateThumbprint -Path $script:SigningCertificatePath

    if (Test-SideloadCertificateTrusted -Thumbprint $thumbprint) {
        Write-Success "Signing certificate already trusted (LocalMachine\Root)"
    }
    else {
        if (-not $Force) {
            Write-Host ""
            Write-Host "       Trust the package signing certificate in LocalMachine stores." -ForegroundColor Yellow
            Write-Host "       Thumbprint: $thumbprint" -ForegroundColor DarkGray
            Write-Host ""
            $response = Read-Host "       Continue? (Y/N)"
            if ($response -ne 'Y' -and $response -ne 'y') {
                Write-Err "Certificate trust required. Installation cancelled."
                Exit-WithPause -ExitCode 1
            }
        }

        try {
            Install-SideloadSigningCertificate -CertificatePath $script:SigningCertificatePath
        }
        catch {
            Write-Err "Failed to trust certificate: $($_.Exception.Message)"
            Exit-WithPause -ExitCode 1
        }
    }
}

# Phase 6: Install package
Write-Step -Step 6 -Total 6 -Message "Installing package..."

$maxRetries = 2
$retryCount = 0
$installSuccess = $false

while ($retryCount -lt $maxRetries -and -not $installSuccess) {
    try {
        Prepare-GoTweaksForInstall -RemoveExistingPackage:$CleanInstall

        # Install main package without forcing dependency reinstall
        # Windows will use already-installed shared dependencies (VCLibs etc.)
        # This avoids conflicts with other apps using those dependencies
        Write-Info "Installing GoTweaks S package..."
        Add-AppxPackage -Path $MainPackage.FullName `
            -ForceUpdateFromAnyVersion `
            -ErrorAction Stop

        $installSuccess = $true
    }
    catch {
        $retryCount++
        $errorMsg = $_.Exception.Message

        # Check if it's a dependency error
        if ($errorMsg -match "dependency" -and $depPackages.Count -gt 0) {
            Write-Warn "Missing dependencies, attempting to install them..."
            foreach ($dep in $depPackages) {
                try {
                    Write-Info "  -> $($dep.Name)"
                    Add-AppxPackage -Path $dep.FullName -ForceUpdateFromAnyVersion -ErrorAction SilentlyContinue
                }
                catch {
                    Write-Warn "    Could not install (may already be present)"
                }
            }
            # Don't count this as a retry, try main package again
            $retryCount--
            Start-Sleep -Seconds 1
            continue
        }

        # Registration failure: PresentMon/helper often locks LocalCache\GoTweaks\Helper\PresentMon.exe
        if (($errorMsg -match '0x80073CF6|80073CF6|0x80073D05|80073D05|could not be registered|application data') -and -not $script:RetriedRegistrationFailure) {
            $script:RetriedRegistrationFailure = $true
            Write-Warn "Registration failed — stopping GoTweaks/PresentMon, removing stale package, clearing LocalCache..."
            try {
                Prepare-GoTweaksForInstall -RemoveExistingPackage
                Start-Sleep -Seconds 2
            }
            catch {
                Write-Warn "Recovery cleanup failed: $($_.Exception.Message)"
            }
            $retryCount--
            continue
        }

        if ($retryCount -lt $maxRetries) {
            Write-Warn "Attempt $retryCount failed, retrying..."
            Start-Sleep -Seconds 2
        }
        else {
            Write-Err "Installation failed: $errorMsg"
            Write-Host ""
            Write-Host "       Troubleshooting:" -ForegroundColor Yellow
            Write-Host "       - Close Xbox Game Bar (Win+G), then end XboxGamingBarHelper + PresentMon in Task Manager" -ForegroundColor Gray
            Write-Host "       - Disable the GoTweaksHelper scheduled task (Task Scheduler -> GoTweaks folder)" -ForegroundColor Gray
            Write-Host "       - Reboot the Legion Go, then run Install GoTweaks.cmd -Force -CleanInstall as Admin" -ForegroundColor Gray
            Write-Host "       - Use Install GoTweaks.cmd (or .ps1), NOT Add-AppDevPackage.ps1 or double-clicking the .msixbundle" -ForegroundColor Gray
            Write-Host "       - For details: Get-AppPackageLog | Select-Object -Last 1 | ForEach-Object { notepad `$_.FullName }" -ForegroundColor Gray
            Exit-WithPause -ExitCode 1
        }
    }
}

# Verify installation
$installedPkg = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue
if ($installedPkg) {
    Write-Host ""
    Write-Host "  =============================================" -ForegroundColor Green
    Write-Host "                                               " -ForegroundColor Green
    Write-Host "         Installation Complete!                " -ForegroundColor White
    Write-Host "                                               " -ForegroundColor Green
    Write-Host "  =============================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Version: $($installedPkg.Version)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Press Win+G to open Xbox Game Bar" -ForegroundColor Cyan
    Write-Host "  Then click the Widgets menu to add GoTweaks S" -ForegroundColor Cyan
    Write-Host ""
}
else {
    Write-Warn "Installation may have succeeded. Press Win+G to verify."
}

Exit-WithPause -ExitCode 0

#endregion
