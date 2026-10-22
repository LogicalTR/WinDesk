param([switch]$Test)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dotnetExe = (Get-Command dotnet -ErrorAction Stop).Source
$project = Join-Path $root 'src\WinDesk.App\WinDesk.App.csproj'
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a three-part release version.' }
$stage = Join-Path $root ('artifacts\release-' + [guid]::NewGuid().ToString('N'))
$app = Join-Path $stage 'app'
$output = Join-Path $root "dist\releases\$version"
New-Item -ItemType Directory -Path $stage, $output -Force | Out-Null

if ($Test) {
    & $dotnetExe run --project (Join-Path $root 'tests\WinDesk.Core.Tests') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
    if (Get-Process -Name WinDesk -ErrorAction SilentlyContinue) { throw 'Close WinDesk before running WPF tests.' }
    & $dotnetExe run --project (Join-Path $root 'tests\WinDesk.App.Tests') -c Release -- --quiet --data-dir (Join-Path $stage 'ui-test')
    if ($LASTEXITCODE -ne 0) { throw 'WPF tests failed.' }
    foreach ($suite in '--import-test', '--recovery-test') {
        & $dotnetExe run --project (Join-Path $root 'tests\WinDesk.App.Tests') -c Release --no-build -- $suite
        if ($LASTEXITCODE -ne 0) { throw "Tests failed: $suite" }
    }
}

& $dotnetExe publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -p:DebugType=embedded -o $app
if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }
$files = @(Get-ChildItem -LiteralPath $app -File -Recurse)
if ($files.Count -ne 1 -or $files[0].Name -ne 'WinDesk.exe') { throw 'Expected exactly one self-contained executable.' }
$exe = Join-Path $output "WinDesk-$version-win-x64.exe"
Copy-Item -LiteralPath (Join-Path $app 'WinDesk.exe') -Destination $exe -Force

$portable = Join-Path $stage 'portable'
New-Item -ItemType Directory -Path $portable -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $app 'WinDesk.exe'), (Join-Path $root 'packaging\KULLANIM.txt') -Destination $portable
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath (Join-Path $output "WinDesk-$version-win-x64.zip") -Force

$compiler = & (Join-Path $root 'tools\prepare-release-tools.ps1')
& $compiler "/DAppVersion=$version" "/DAppSource=$app" "/DReleaseOutput=$output" (Join-Path $root 'packaging\WinDesk.iss')
if ($LASTEXITCODE -ne 0) { throw 'Setup build failed.' }
$assetNames = @("WinDesk-$version-win-x64.exe", "WinDesk-$version-win-x64.zip", "WinDesk-Setup-$version-x64.exe")
$checksums = foreach ($name in $assetNames) {
    $hash = Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256
    $hash.Hash.ToLowerInvariant() + '  ' + $name
}
$checksums | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Release files: $output"
