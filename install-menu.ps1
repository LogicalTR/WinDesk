$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$packagePath = Join-Path $root 'dist\WinDesk.DesktopMenu.msix'
$certificatePath = Join-Path $root 'dist\WinDesk.DesktopMenu.cer'
$externalLocation = Join-Path $root 'dist\WinDesk'
foreach ($path in @($packagePath, $certificatePath, (Join-Path $externalLocation 'WinDesk.exe'), (Join-Path $externalLocation 'shell\WinDesk.ContextMenu.v074.dll'))) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing file: $path. Run build.ps1 and build-menu.ps1 -Sign first." }
}
$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
$signature = Get-AuthenticodeSignature -LiteralPath $packagePath
if ($certificate.Subject -ne 'CN=WinDesk Local Development' -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
    throw 'The package signer does not match the WinDesk development certificate.'
}
# AppX deployment requires this development signer in the machine's TrustedPeople
# store. The narrowly scoped, separate trust script requires a visible UAC approval.
if (-not (Test-Path -LiteralPath "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)")) {
    throw 'Approve tools\trust-menu-certificate.ps1 as administrator first; then run this installer as your normal user.'
}
Add-AppxPackage -Path $packagePath -ExternalLocation $externalLocation
Get-AppxPackage -Name WinDesk.DesktopMenu | Select-Object Name, Version, Status, PackageFullName
Write-Host 'WinDesk desktop menu installed for the current user. If Explorer caches the old menu, sign out and back in.'
