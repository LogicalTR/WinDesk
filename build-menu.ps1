param([switch]$Sign, [switch]$PackageOnly)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$toolPaths = & (Join-Path $root 'tools\prepare-shell-tools.ps1')
$target = Join-Path $root 'dist\WinDesk\shell'
$nativeDll = Join-Path $target 'WinDesk.ContextMenu.v074.dll'
New-Item -ItemType Directory -Path $target -Force | Out-Null
if (-not $PackageOnly) {
    & $toolPaths.Zig cc -shared -O2 -Wall -Wextra -Werror -Wno-unused-parameter (Join-Path $root 'src\WinDesk.Shell\WinDeskShell.c') (Join-Path $root 'src\WinDesk.Shell\WinDeskShell.def') -o $nativeDll -lole32 -loleaut32 -lshlwapi -luuid
    if ($LASTEXITCODE -ne 0) { throw 'Native context menu build failed.' }
}
$shellTest = Join-Path $root 'artifacts\WinDesk.Shell.Tests.exe'
New-Item -ItemType Directory -Path (Split-Path -Parent $shellTest) -Force | Out-Null
& $toolPaths.Zig cc -O2 -Wall -Wextra -Werror -municode (Join-Path $root 'tests\WinDesk.Shell.Tests\ShellTests.c') -o $shellTest -lole32 -luuid
if ($LASTEXITCODE -ne 0) { throw 'Native context menu test build failed.' }
& $shellTest $nativeDll
if ($LASTEXITCODE -ne 0) { throw 'Native context menu tests failed.' }
$identityRoot = Join-Path $root 'artifacts\menu-identity'
New-Item -ItemType Directory -Path (Join-Path $identityRoot 'Assets') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'src\WinDesk.Shell\AppxManifest.xml') -Destination $identityRoot
foreach ($asset in 'Square150x150Logo.png','Square44x44Logo.png','StoreLogo.png') {
    Copy-Item -LiteralPath (Join-Path $root "src\WinDesk.App\Assets\$asset") -Destination (Join-Path $identityRoot 'Assets')
}
$package = Join-Path $root 'dist\WinDesk.DesktopMenu.msix'
& $toolPaths.MakeAppx pack /o /nv /d $identityRoot /p $package
if ($LASTEXITCODE -ne 0) { throw 'Context menu package creation failed.' }
if ($Sign) {
    # Creates a private development signing key in this user's personal store.
    # Trust is a separate, explicit installation step; never add a root CA.
    $subject = 'CN=WinDesk Local Development'
    $certificate = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date).AddDays(30) -and $_.HasPrivateKey } | Select-Object -First 1
    if (-not $certificate) {
        $certificate = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject -CertStoreLocation Cert:\CurrentUser\My -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(1)
    }
    $signTool = Join-Path (Split-Path -Parent $toolPaths.MakeAppx) 'signtool.exe'
    & $signTool sign /fd SHA256 /s My /sha1 $certificate.Thumbprint $package
    if ($LASTEXITCODE -ne 0) { throw 'Context menu package signing failed.' }
    Export-Certificate -Cert $certificate -FilePath (Join-Path $root 'dist\WinDesk.DesktopMenu.cer') | Out-Null
}
Write-Output $package
