#requires -Version 7.0
param([Parameter(Mandatory)][string]$RunDirectory, [switch]$ValidateOnly)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root
. (Join-Path $PSScriptRoot 'release-common.ps1')
$repo = 'LogicalTR/WinDesk'
$gh = Join-Path $root '.tools\github-cli\bin\gh.exe'
New-Item -ItemType Directory -Path $RunDirectory -Force | Out-Null
$statusFile = Join-Path $RunDirectory 'status.json'
$stage = 'preflight'
$releaseUrl = $null
$lock = $null
Start-Transcript -Path (Join-Path $RunDirectory 'publish.log') | Out-Null
function Set-Status([string]$State, [string]$Message) {
    [ordered]@{state=$State;stage=$script:stage;message=$Message;updatedAtUtc=[DateTime]::UtcNow.ToString('o');pid=$PID;url=$script:releaseUrl} |
        ConvertTo-Json | Set-Content -LiteralPath $statusFile -Encoding utf8
    Write-Host "[$script:stage] $Message"
}
function Invoke-Gh([string[]]$Arguments) {
    $result = & $gh @Arguments
    if ($LASTEXITCODE -ne 0) { throw "GitHub CLI failed: $($Arguments[0]) $($Arguments[1]) (exit $LASTEXITCODE). See publish.log/error.log." }
    return $result
}
function Read-Gh([string[]]$Arguments) { return (Invoke-Gh $Arguments | ConvertFrom-Json) }
function Read-Release([long]$Id) { return Read-Gh @('api', "repos/$repo/releases/$Id") }
try {
    Set-Status running 'Checking source, version and tools.'
    Get-Command git,dotnet -ErrorAction Stop | Out-Null
    $lock = [IO.File]::Open((Join-Path $root 'artifacts\publish.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $dirty = & git status --porcelain --untracked-files=all
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect Git working tree.' }
    if ($dirty) { throw 'Commit source, version and release notes first. A release must come from a clean Git commit.' }
    $commit = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read source commit.' }
    $version = ([xml](Get-Content -LiteralPath 'src\WinDesk.App\WinDesk.App.csproj' -Raw)).Project.PropertyGroup.Version
    if ($version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw 'Expected a stable three-part release version.' }
    $tag = "v$version"
    $notes = Get-ReleaseNotes (Get-Content -LiteralPath 'RELEASE_NOTES.md' -Raw) $version
    $output = Join-Path $root "dist\releases\$version"
    $cache = Join-Path $root "artifacts\publish-$version"
    $manifestFile = Join-Path $cache 'manifest.json'
    $existing = $null
    if (-not $ValidateOnly) {
        if (-not (Test-Path -LiteralPath $gh)) { $gh = (Get-Command gh -ErrorAction Stop).Source }
        Invoke-Gh @('auth', 'status', '--hostname', 'github.com') | Out-Host
        $readmeCheck = Read-Gh @('api', "repos/$repo/contents/README.md")
        Get-DistributionReadme ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($readmeCheck.content))) `
            $readmeCheck.sha (Get-Content -LiteralPath 'packaging\github-readme.md' -Raw) $version | Out-Null
        $releases = @(Read-Gh @('api', "repos/$repo/releases?per_page=100"))
        $existing = $releases | Where-Object tag_name -CEQ $tag | Select-Object -First 1
        $newer = $releases | Where-Object { -not $_.draft -and -not $_.prerelease -and $_.tag_name -match '^v\d+\.\d+\.\d+$' -and [version]$_.tag_name.Substring(1) -gt [version]$version }
        if ($newer) { throw 'A newer stable release already exists. Bump the project version; never downgrade Latest.' }
        if ($existing -and -not (Test-Path -LiteralPath $manifestFile)) { throw 'This tag already exists without a matching local release manifest. Bump the version.' }
    }
    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    $notesFile = Join-Path $cache 'release-notes.md'
    $notes | Set-Content -LiteralPath $notesFile -Encoding utf8
    if (Test-Path -LiteralPath $manifestFile) {
        $manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
        if ($manifest.commit -ne $commit -or $manifest.version -ne $version) { throw 'Cached artifacts belong to a different commit. Bump the version or move the unpublished cache aside.' }
        foreach ($asset in $manifest.artifacts) {
            $file = Get-Item -LiteralPath (Join-Path $output $asset.name)
            if ($file.Length -ne $asset.size -or (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $asset.sha256) { throw "Cached artifact changed: $($asset.name)" }
        }
        Set-Status running 'Reusing verified artifacts from this exact source commit.'
    } else {
        $stage = 'tests'; Set-Status running 'Running background tests (no desktop interaction).'
        & dotnet run --project tests\WinDesk.Core.Tests -c Release | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Core/update tests failed.' }
        & (Join-Path $root 'tests\Test-PublishRelease.ps1') | Out-Host
        & dotnet build WinDesk.slnx -c Release --nologo | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Solution build failed.' }
        & dotnet run --project tests\WinDesk.App.Tests -c Release --no-build -- --update-test (Join-Path $RunDirectory 'update-ui') | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Background update UI tests failed.' }
        $stage = 'build'; Set-Status running "Building $tag and the installer."
        & (Join-Path $root 'build-release.ps1') | Out-Host
        $source = "WinDesk-$version-source.zip"
        & git archive --format=zip "--prefix=WinDesk-$version/" "--output=$(Join-Path $output $source)" $commit
        if ($LASTEXITCODE -ne 0) { throw 'Source archive failed.' }
        $names = @("WinDesk-$version-win-x64.exe", "WinDesk-$version-win-x64.zip", "WinDesk-Setup-$version-x64.exe", $source)
        $checksums = foreach ($name in $names) { (Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $name }
        $checksums | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
        $names += 'SHA256SUMS.txt'
        $artifacts = foreach ($name in $names) {
            $file = Get-Item -LiteralPath (Join-Path $output $name)
            @{name=$name;size=$file.Length;sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
        }
        $manifest = @{version=$version;commit=$commit;artifacts=@($artifacts)}
        $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestFile -Encoding utf8
    }
    # Catch concurrent source edits during the build before any upload.
    if ((& git status --porcelain --untracked-files=all) -or (& git rev-parse HEAD) -ne $commit) { throw 'Source changed during the build; publication stopped.' }
    if ($ValidateOnly) { $stage = 'complete'; Set-Status validated "Build and local checks passed. No GitHub writes. Artifacts: $output"; return }

    $stage = 'upload'; Set-Status running 'Preparing a draft release and uploading missing assets.'
    if (-not $existing) {
        Invoke-Gh @('release', 'create', $tag, '--repo', $repo, '--target', 'main', '--draft', '--title', "WinDesk $version — Windows 10/11 x64", '--notes-file', $notesFile) | Out-Host
        $id = (Read-Gh @('release', 'view', $tag, '--repo', $repo, '--json', 'databaseId')).databaseId
        $existing = Read-Release $id
    }
    $releaseUrl = "https://github.com/$repo/releases/tag/$tag"
    if ($existing.prerelease) { throw 'Refusing to reuse a prerelease.' }
    if ($existing.draft) {
        foreach ($asset in @($existing.assets)) {
            # GitHub can leave an empty starter asset after a transport failure.
            if ($asset.state -eq 'starter' -and $asset.size -eq 0 -and $asset.name -cin @($manifest.artifacts.name)) {
                Invoke-Gh @('api', "repos/$repo/releases/assets/$($asset.id)", '--method', 'DELETE') | Out-Null
            }
        }
        $existing = Read-Release $existing.id
        Assert-ReleaseAssets $existing $manifest.artifacts -AllowMissing
        $missing = @($manifest.artifacts | Where-Object name -CNotIn @($existing.assets.name) | ForEach-Object { Join-Path $output $_.name })
        if ($missing.Count) { Invoke-Gh (@('release', 'upload', $tag, '--repo', $repo) + $missing) | Out-Host }
    }
    $stage = 'verify'; Set-Status running 'Comparing all five GitHub sizes and SHA-256 digests.'
    $existing = Read-Release $existing.id
    Assert-ReleaseAssets $existing $manifest.artifacts
    if ($existing.draft) {
        $stage = 'publish'; Set-Status running 'All assets verified; publishing the release as Latest.'
        Invoke-Gh @('release', 'edit', $tag, '--repo', $repo, '--draft=false', '--latest') | Out-Host
    }
    $stage = 'readme'; Set-Status running 'Updating the managed download links in the distribution README.'
    $readme = Read-Gh @('api', "repos/$repo/contents/README.md")
    $old = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($readme.content))
    $new = Get-DistributionReadme $old $readme.sha (Get-Content -LiteralPath 'packaging\github-readme.md' -Raw) $version
    if ($old -cne $new) {
        $request = Join-Path $RunDirectory 'readme-update.json'
        @{message="Update downloads for WinDesk $version";sha=$readme.sha;branch='main';content=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($new))} |
            ConvertTo-Json | Set-Content -LiteralPath $request -Encoding utf8
        Invoke-Gh @('api', "repos/$repo/contents/README.md", '--method', 'PUT', '--input', $request) | Out-Null
    }
    $stage = 'public-check'; Set-Status running 'Checking the public release and installer download.'
    $public = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/tags/$tag"
    Assert-ReleaseAssets $public $manifest.artifacts
    if ($public.draft -or (Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest").tag_name -ne $tag) { throw 'Public Latest verification failed.' }
    $checksumFile = Join-Path $RunDirectory 'github-SHA256SUMS.txt'
    Invoke-WebRequest "https://github.com/$repo/releases/download/$tag/SHA256SUMS.txt" -OutFile $checksumFile
    if ((Get-FileHash -LiteralPath $checksumFile).Hash -ne ($manifest.artifacts | Where-Object name -eq 'SHA256SUMS.txt').sha256) { throw 'Public checksum download differs.' }
    $head = Invoke-WebRequest "https://github.com/$repo/releases/download/$tag/WinDesk-Setup-$version-x64.exe" -Method Head
    if ($head.StatusCode -ne 200) { throw 'Installer is not publicly downloadable.' }
    $stage = 'complete'; Set-Status published "Published and verified: $releaseUrl"
} catch {
    Set-Status failed $_.Exception.Message
    throw
} finally {
    if ($lock) { $lock.Dispose() }
    Stop-Transcript | Out-Null
}
