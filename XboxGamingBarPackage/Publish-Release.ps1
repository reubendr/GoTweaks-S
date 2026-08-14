# Publishes GitHub release v0.3.2876.0 with installer scripts + msixbundle
$ErrorActionPreference = "Stop"
$repo = "reubendr/GoTweaks-S"
$tag = "v0.3.2876.0"
$pkg = Join-Path $PSScriptRoot "AppPackages\XboxGamingBarPackage_0.3.2876.0_Test"
$notesPath = Join-Path $PSScriptRoot "release-notes-v0.3.2876.0.md"

if (-not (Test-Path $pkg)) { throw "Package folder not found: $pkg" }
$installCmd = Join-Path $pkg "Install GoTweaks.cmd"
$installPs1 = Join-Path $pkg "Install GoTweaks.ps1"
$bundle = Join-Path $pkg "XboxGamingBarPackage_0.3.2876.0_x86_x64.msixbundle"
$zipName = "GoTweaksS-0.3.2876.0.zip"
$zipPath = Join-Path $PSScriptRoot "AppPackages\$zipName"
foreach ($f in @($installCmd, $installPs1, $bundle, $notesPath)) {
    if (-not (Test-Path $f)) { throw "Missing file: $f" }
}

function New-InstallReleaseZip {
    param(
        [string]$SourceDir,
        [string]$DestinationZip
    )

    $staging = Join-Path ([IO.Path]::GetTempPath()) ("GoTweaksS-staging-" + [guid]::NewGuid().ToString("N"))
    try {
        New-Item -ItemType Directory -Path $staging -Force | Out-Null

        Copy-Item -Path (Join-Path $SourceDir "Install GoTweaks.cmd") -Destination $staging -Force
        Copy-Item -Path (Join-Path $SourceDir "Install GoTweaks.ps1") -Destination $staging -Force

        $bundleFile = Get-ChildItem -Path $SourceDir -Filter "*.msixbundle" -ErrorAction Stop | Select-Object -First 1
        Copy-Item -Path $bundleFile.FullName -Destination $staging -Force

        $cerFiles = Get-ChildItem -Path $SourceDir -Filter "*.cer" -ErrorAction SilentlyContinue
        foreach ($cer in $cerFiles) {
            Copy-Item -Path $cer.FullName -Destination $staging -Force
        }
        if (-not $cerFiles -or $cerFiles.Count -eq 0) {
            throw "No .cer signing certificate found in $SourceDir"
        }

        $depsDir = Join-Path $SourceDir "Dependencies"
        if (Test-Path $depsDir) {
            Copy-Item -Path $depsDir -Destination (Join-Path $staging "Dependencies") -Recurse -Force
        }
        else {
            throw "Dependencies folder not found in $SourceDir"
        }

        if (Test-Path $DestinationZip) { Remove-Item $DestinationZip -Force }
        Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $DestinationZip -Force
        Write-Host "Created install zip: $DestinationZip ($([math]::Round((Get-Item $DestinationZip).Length / 1MB, 1)) MB)"
    }
    finally {
        Remove-Item -Path $staging -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Building release zip (installer + bundle + cert + dependencies)..."
New-InstallReleaseZip -SourceDir $pkg -DestinationZip $zipPath

$credIn = "protocol=https`nhost=github.com`n`n"
$credOut = $credIn | git credential fill
$token = ($credOut | Select-String '^password=(.+)$').Matches.Groups[1].Value
if (-not $token) { throw "Could not read GitHub token from git credential manager" }

$headers = @{
    Authorization = "Bearer $token"
    Accept        = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

$notesText = [System.IO.File]::ReadAllText($notesPath)

$bodyObj = @{
    tag_name   = $tag
    name       = "GoTweaks S v0.3.2876.0"
    body       = $notesText
    draft      = $false
    prerelease = $false
}
$body = $bodyObj | ConvertTo-Json -Depth 3 -Compress

Write-Host "Creating release $tag on $repo..."
try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases" -Method Post -Headers $headers -Body $body -ContentType "application/json; charset=utf-8"
}
catch {
    $status = $null
    if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
    $detail = $_.ErrorDetails.Message
    if ($status -eq 422) {
        Write-Host "Create returned 422: $detail"
        Write-Host "Checking for existing release..."
        try {
            $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/tags/$tag" -Headers $headers
        }
        catch {
            # Tag exists but no release yet - create release targeting existing tag
            if ($detail -match 'already exists' -or $detail -match 'Reference already exists') {
                Write-Host "Creating release for existing tag..."
                $bodyObj2 = @{
                    tag_name   = $tag
                    name       = "GoTweaks S v0.3.2876.0"
                    body       = $notesText
                    draft      = $false
                    prerelease = $false
                }
                $body2 = $bodyObj2 | ConvertTo-Json -Depth 3 -Compress
                $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases" -Method Post -Headers $headers -Body $body2 -ContentType "application/json; charset=utf-8"
            }
            else { throw }
        }
    }
    else { throw }
}

# Refresh release notes on existing release
$patchBody = @{ body = $notesText; name = "GoTweaks S v0.3.2876.0" } | ConvertTo-Json -Compress
Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/$($release.id)" -Method Patch -Headers $headers -Body $patchBody -ContentType "application/json; charset=utf-8" | Out-Null

function Remove-ReleaseAssetsByName {
    param([string[]]$Names, [hashtable]$Hdrs, [object]$Release)
    foreach ($name in $Names) {
        $existing = @($Release.assets) | Where-Object { $_.name -eq $name }
        foreach ($asset in $existing) {
            Write-Host "Removing stale asset $($asset.name)..."
            Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/assets/$($asset.id)" -Method Delete -Headers $Hdrs | Out-Null
        }
    }
}

# Drop loose installers / old uploads — users must download the full zip
Remove-ReleaseAssetsByName -Names @(
    "Install.exe",
    "Install GoTweaks.cmd",
    "Install GoTweaks.ps1",
    "Install.GoTweaks.cmd",
    "Install.GoTweaks.ps1",
    "XboxGamingBarPackage_0.3.2876.0_x86_x64.msixbundle",
    $zipName
) -Hdrs $headers -Release $release

# Re-fetch so asset list matches GitHub before we upload replacements
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/tags/$tag" -Headers $headers

function Upload-ReleaseAsset {
    param([string]$Path, [hashtable]$Hdrs, [object]$Release)
    $name = [IO.Path]::GetFileName($Path)
    $existing = @($Release.assets) | Where-Object { $_.name -eq $name }
    foreach ($asset in $existing) {
        Write-Host "Removing old asset $name..."
        Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/assets/$($asset.id)" -Method Delete -Headers $Hdrs | Out-Null
    }
    $uploadHeaders = $Hdrs.Clone()
    $uploadHeaders["Content-Type"] = "application/octet-stream"
    $baseUpload = $Release.upload_url -replace '\{\?name,label\}', ''
    $uri = "$baseUpload`?name=$([uri]::EscapeDataString($name))"
    Write-Host "Uploading $name..."
    Invoke-RestMethod -Uri $uri -Method Post -Headers $uploadHeaders -InFile $Path | Out-Null
}

Upload-ReleaseAsset -Path $zipPath -Hdrs $headers -Release $release

Write-Host ""
Write-Host "Published: $($release.html_url)" -ForegroundColor Green
