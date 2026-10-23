#requires -Version 7.4
param(
    [Parameter(Mandatory)][ValidateSet('Prepare', 'Assemble', 'Complete')][string]$Stage,
    [string]$WorkDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\signpath'),
    [string]$SignedDirectory,
    [string]$CertificateThumbprint = $env:SIGNPATH_CERTIFICATE_SHA1
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'signpath\common.ps1')
$work = [IO.Path]::GetFullPath($WorkDirectory)
$manifestPath = Join-Path $work 'manifest.json'
$compiler = & (Join-Path $PSScriptRoot 'prepare-release-tools.ps1')
$app = Join-Path $work 'app'
$cache = Join-Path $work 'inno-cache'
$components = Join-Path $work 'unsigned-components'
$setup = Join-Path $work 'unsigned-setup'
$project = Join-Path $root 'src\WinDesk.App\WinDesk.App.csproj'
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a three-part release version.' }
$commit = & git -C $root rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source commit.' }
$compilerArgs = @("/DAppVersion=$version", "/DAppSource=$app", "/DReleaseOutput=$setup", "/DExternalSigning=$cache")
$iss = Join-Path $root 'packaging\WinDesk.iss'

if ($Stage -eq 'Prepare') {
    if (Test-Path -LiteralPath $work) { throw 'Use a fresh signing work directory; do not reuse previous signing inputs.' }
    New-Item -ItemType Directory -Path $work, $cache, $setup, (Join-Path $components 'app'), (Join-Path $components 'installer') | Out-Null
    & (Join-Path $PSScriptRoot 'publish-release-app.ps1') -OutputDirectory $app
    Assert-SignPathMetadata (Join-Path $app 'WinDesk.exe') $version
    # Inno's documented offline signing mode deliberately stops on the first
    # compile, leaving the exact uninstaller/runtime that needs a signature.
    $log = @(& $compiler @compilerArgs $iss 2>&1)
    $exitCode = $LASTEXITCODE
    $log | Set-Content -LiteralPath (Join-Path $work 'inno-prepare.log')
    $stubs = @(Get-ChildItem -LiteralPath $cache -File)
    if ($exitCode -eq 0 -or $stubs.Count -ne 1 -or $stubs[0].Name -notmatch '^uninst-.+\.e32$' -or
        ($log -join "`n") -notmatch 'Signed uninstaller mode is enabled.*please attach your digital signature') {
        throw "Inno did not stop at the expected offline-signing boundary. Read $work\inno-prepare.log"
    }
    # GitHub's pwsh wrapper propagates LASTEXITCODE. This one compiler failure
    # was the documented preparation boundary, not a failed workflow step.
    $global:LASTEXITCODE = 0
    $stub = $stubs[0]
    Assert-SignPathMetadata $stub.FullName $version
    Copy-Item -LiteralPath (Join-Path $app 'WinDesk.exe') -Destination (Join-Path $components 'app\WinDesk.exe')
    # Give the PE a conventional extension for Authenticode and SignPath.
    Copy-Item -LiteralPath $stub.FullName -Destination (Join-Path $components 'installer\WinDesk.Uninstaller.exe')
    @{ version=$version; commit=$commit; uninstaller=$stub.Name
       appHash=(Get-FileHash -LiteralPath (Join-Path $components 'app\WinDesk.exe')).Hash
       uninstallerHash=(Get-FileHash -LiteralPath $stub.FullName).Hash } |
        ConvertTo-Json | Set-Content -LiteralPath $manifestPath
    if ($env:GITHUB_OUTPUT) {
        "version=$version`nproduct-version=$version.0" | Out-File -LiteralPath $env:GITHUB_OUTPUT -Append -Encoding utf8
    }
    Write-Output "Prepared unsigned components: $components"
    return
}

if (-not $SignedDirectory) { throw 'SignedDirectory is required.' }
$signed = [IO.Path]::GetFullPath($SignedDirectory)
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.version -cne $version -or $manifest.commit -cne $commit) { throw 'Source version or commit changed between signing stages.' }
if ((Get-FileHash -LiteralPath (Join-Path $components 'app\WinDesk.exe')).Hash -cne $manifest.appHash -or
    (Get-FileHash -LiteralPath (Join-Path $components 'installer\WinDesk.Uninstaller.exe')).Hash -cne $manifest.uninstallerHash) {
    throw 'Unsigned signing inputs changed.'
}
if ($Stage -eq 'Assemble') {
    if ($manifest.setupHash) { throw 'Installer has already been assembled. Start a fresh workflow.' }
    Assert-SignPathFiles $signed @('app/WinDesk.exe', 'installer/WinDesk.Uninstaller.exe')
    foreach ($path in 'app\WinDesk.exe', 'installer\WinDesk.Uninstaller.exe') {
        Assert-SignPathResult (Join-Path $components $path) (Join-Path $signed $path) $version $CertificateThumbprint
    }
    Copy-Item -LiteralPath (Join-Path $signed 'app\WinDesk.exe') -Destination (Join-Path $app 'WinDesk.exe') -Force
    Copy-Item -LiteralPath (Join-Path $signed 'installer\WinDesk.Uninstaller.exe') -Destination (Join-Path $cache $manifest.uninstaller) -Force
    & $compiler @compilerArgs $iss | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Installer assembly using the signed Inno cache failed.' }
    $installer = Join-Path $setup "WinDesk-Setup-$version-x64.exe"
    Assert-SignPathMetadata $installer $version
    # Inno additionally compares the cached PE byte-for-byte, excluding signing fields.
    $manifest | Add-Member -NotePropertyName setupHash -NotePropertyValue (Get-FileHash -LiteralPath $installer).Hash
    $manifest | Add-Member -NotePropertyName signedAppHash -NotePropertyValue (Get-FileHash -LiteralPath (Join-Path $app 'WinDesk.exe')).Hash
    $manifest | Add-Member -NotePropertyName signedUninstallerHash -NotePropertyValue (Get-FileHash -LiteralPath (Join-Path $cache $manifest.uninstaller)).Hash
    $manifest | Add-Member -NotePropertyName certificateThumbprint -NotePropertyValue $CertificateThumbprint
    $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath
    Write-Output "Prepared installer for the second signing request: $installer"
    return
}

$installerName = "WinDesk-Setup-$version-x64.exe"
Assert-SignPathFiles $signed @($installerName)
if (-not $manifest.setupHash -or $manifest.certificateThumbprint -ne $CertificateThumbprint -or
    (Get-FileHash -LiteralPath (Join-Path $setup $installerName)).Hash -cne $manifest.setupHash -or
    (Get-FileHash -LiteralPath (Join-Path $app 'WinDesk.exe')).Hash -cne $manifest.signedAppHash -or
    (Get-FileHash -LiteralPath (Join-Path $cache $manifest.uninstaller)).Hash -cne $manifest.signedUninstallerHash) {
    throw 'Assembled signing inputs changed.'
}
Assert-SignPathResult (Join-Path $setup $installerName) (Join-Path $signed $installerName) $version $CertificateThumbprint
$output = Join-Path $work 'release'
if (Test-Path -LiteralPath $output) { throw 'Release output already exists.' }
New-Item -ItemType Directory -Path $output | Out-Null
Copy-Item -LiteralPath (Join-Path $signed $installerName) -Destination $output
Copy-Item -LiteralPath (Join-Path $app 'WinDesk.exe') -Destination (Join-Path $output "WinDesk-$version-win-x64.exe")
Compress-Archive -Path (Join-Path $app '*') -DestinationPath (Join-Path $output "WinDesk-$version-win-x64.zip")
& git -C $root archive --format=zip "--prefix=WinDesk-$version/" "--output=$(Join-Path $output "WinDesk-$version-source.zip")" $commit
if ($LASTEXITCODE -ne 0) { throw 'Source archive failed.' }
Assert-ReleasePackageSignatures -Directory $output -Version $version
$checksums = Get-ChildItem -LiteralPath $output -File | Sort-Object Name | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant() + '  ' + $_.Name
}
$checksums | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Verified signed release bundle: $output"
