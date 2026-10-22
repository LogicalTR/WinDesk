$ErrorActionPreference = 'Stop'
Get-AppxPackage -Name WinDesk.DesktopMenu | Where-Object Publisher -eq 'CN=WinDesk Local Development' | Remove-AppxPackage
$certificatePath = Join-Path $PSScriptRoot 'dist\WinDesk.DesktopMenu.cer'
if (Test-Path -LiteralPath $certificatePath) {
    $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
    if ($certificate.Subject -eq 'CN=WinDesk Local Development') {
        $trustPath = "Cert:\CurrentUser\TrustedPeople\$($certificate.Thumbprint)"
        if (Test-Path -LiteralPath $trustPath) { Remove-Item -LiteralPath $trustPath }
    }
}
Write-Host 'WinDesk menu registration removed. Desktop groups and application files are preserved.'
