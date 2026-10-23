param([switch]$Test, [switch]$Unsigned,
      [string]$CertificateThumbprint = $env:WINDESK_SIGN_CERT_SHA1,
      [string]$TimestampUrl = 'http://timestamp.digicert.com', [switch]$MachineStore)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
. (Join-Path $root 'tools\release-signing.ps1')
$signing = if (-not $Unsigned) {
    Get-ReleaseSigningConfiguration -Thumbprint $CertificateThumbprint -TimestampUrl $TimestampUrl -MachineStore:$MachineStore
}
$dotnetExe = (Get-Command dotnet -ErrorAction Stop).Source
$project = Join-Path $root 'src\WinDesk.App\WinDesk.App.csproj'
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a three-part release version.' }
$stage = Join-Path $root ('artifacts\release-' + [guid]::NewGuid().ToString('N'))
$app = Join-Path $stage 'app'
$output = if ($Unsigned) { Join-Path $root "dist\unsigned\$version" } else { Join-Path $root "dist\releases\$version" }
if ($Unsigned) { Write-Warning 'LOCAL TEST BUILD: unsigned files can trigger Windows security warnings and cannot be published.' }
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

& (Join-Path $root 'tools\publish-release-app.ps1') -OutputDirectory $app
if ($signing) { Invoke-ReleaseSigning -Path (Join-Path $app 'WinDesk.exe') -Configuration $signing }
$exe = Join-Path $output "WinDesk-$version-win-x64.exe"
Copy-Item -LiteralPath (Join-Path $app 'WinDesk.exe') -Destination $exe -Force

Compress-Archive -Path (Join-Path $app '*') -DestinationPath (Join-Path $output "WinDesk-$version-win-x64.zip") -Force

$compiler = & (Join-Path $root 'tools\prepare-release-tools.ps1')
$compilerArguments = @("/DAppVersion=$version", "/DAppSource=$app", "/DReleaseOutput=$output")
if ($signing) {
    $configurationPath = Join-Path $stage 'signing.json'
    $signing | ConvertTo-Json | Set-Content -LiteralPath $configurationPath -Encoding utf8
    $pwsh = (Get-Command pwsh -ErrorAction Stop).Source
    $signScript = Join-Path $root 'tools\sign-release-file.ps1'
    # Inno expands $q to a quote and $f to the quoted filename. Escape literal $ in paths.
    $command = '$q' + $pwsh.Replace('$', '$$') + '$q -NoProfile -NonInteractive -File $q' +
        $signScript.Replace('$', '$$') + '$q -ConfigurationPath $q' + $configurationPath.Replace('$', '$$') + '$q -Path $f'
    $compilerArguments += @('/DSignRelease=1', "/SWinDeskRelease=$command")
} else { $compilerArguments += '/DUnsignedBuild=1' }
& $compiler @compilerArguments (Join-Path $root 'packaging\WinDesk.iss')
if ($LASTEXITCODE -ne 0) { throw 'Setup build failed.' }
if ($signing) { Assert-ReleasePackageSignatures -Directory $output -Version $version }
$assetNames = @("WinDesk-$version-win-x64.exe", "WinDesk-$version-win-x64.zip", "WinDesk-Setup-$version-x64.exe")
$checksums = foreach ($name in $assetNames) {
    $hash = Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256
    $hash.Hash.ToLowerInvariant() + '  ' + $name
}
$checksums | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Release files: $output"
