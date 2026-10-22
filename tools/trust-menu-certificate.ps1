param([switch]$Remove)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'This one-time certificate operation needs administrator approval.' }
$certificatePath = Join-Path $root 'dist\WinDesk.DesktopMenu.cer'
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
$signature = Get-AuthenticodeSignature -LiteralPath (Join-Path $root 'dist\WinDesk.DesktopMenu.msix')
if ($certificate.Subject -ne 'CN=WinDesk Local Development' -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
    throw 'The certificate does not match the local WinDesk package.'
}
$trustPath = "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)"
if ($Remove) {
    if (Test-Path -LiteralPath $trustPath) { Remove-Item -LiteralPath $trustPath }
} elseif (-not (Test-Path -LiteralPath $trustPath)) {
    Import-Certificate -FilePath $certificatePath -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
}
# This imports only the public leaf certificate. It does not add a root CA,
# enable Developer Mode, alter execution policy or change any other certificate.
