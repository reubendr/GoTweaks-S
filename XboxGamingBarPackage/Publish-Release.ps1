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
foreach ($f in @($installCmd, $installPs1, $bundle, $notesPath)) {
    if (-not (Test-Path $f)) { throw "Missing file: $f" }
}

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

# Drop ps2exe installer and any misnamed uploads from earlier publishes
Remove-ReleaseAssetsByName -Names @(
    "Install.exe",
    "Install.GoTweaks.cmd",
    "Install.GoTweaks.ps1"
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

foreach ($asset in @($installCmd, $installPs1, $bundle)) {
    Upload-ReleaseAsset -Path $asset -Hdrs $headers -Release $release
}

Write-Host ""
Write-Host "Published: $($release.html_url)" -ForegroundColor Green
