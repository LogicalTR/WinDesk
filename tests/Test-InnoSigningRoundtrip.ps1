#requires -Version 7.4
param([Parameter(Mandatory)][string]$PreparedDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $root 'tools\signpath\common.ps1')
$prepared = [IO.Path]::GetFullPath($PreparedDirectory)
$manifest = Get-Content -LiteralPath (Join-Path $prepared 'manifest.json') -Raw | ConvertFrom-Json
$fixture = Join-Path $root ('artifacts\inno-signing-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture, (Join-Path $fixture 'cache'), (Join-Path $fixture 'output') | Out-Null
Copy-Item -LiteralPath (Join-Path $prepared 'app') -Destination (Join-Path $fixture 'app') -Recurse
$stub = Join-Path $fixture 'WinDesk.Uninstaller.exe'
Copy-Item -LiteralPath (Join-Path $prepared 'unsigned-components\installer\WinDesk.Uninstaller.exe') -Destination $stub
# In-memory test identity only. Never installed in any certificate store, never
# trusted, never timestamped, and never accepted by the release verifier.
$rsa = [Security.Cryptography.RSA]::Create(2048)
$request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=WinDesk Offline Test ONLY', $rsa,
    [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
$usages = [Security.Cryptography.OidCollection]::new()
[void]$usages.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
$request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($usages, $false))
$certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-1), [DateTimeOffset]::UtcNow.AddHours(1))
try {
    foreach ($pair in @(
        @((Join-Path $prepared 'unsigned-components\app\WinDesk.exe'), (Join-Path $fixture 'app\WinDesk.exe')),
        @((Join-Path $prepared 'unsigned-components\installer\WinDesk.Uninstaller.exe'), $stub)
    )) {
        $signature = Set-AuthenticodeSignature -LiteralPath $pair[1] -Certificate $certificate -HashAlgorithm SHA256
        if ($signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) { throw 'Test PE signing failed.' }
        [WinDesk.Signing.PeSigningContent]::AssertUnchanged($pair[0], $pair[1])
    }
    Copy-Item -LiteralPath $stub -Destination (Join-Path $fixture "cache\$($manifest.uninstaller)")
    $compiler = & (Join-Path $root 'tools\prepare-release-tools.ps1')
    & $compiler "/DAppVersion=$($manifest.version)" "/DAppSource=$fixture\app" "/DReleaseOutput=$fixture\output" `
        "/DExternalSigning=$fixture\cache" (Join-Path $root 'packaging\WinDesk.iss') > (Join-Path $fixture 'compile.log')
    if ($LASTEXITCODE -ne 0) { throw "Inno could not consume the signed cache. See $fixture\compile.log" }
    $installer = Join-Path $fixture "output\WinDesk-Setup-$($manifest.version)-x64.exe"
    $originalSetup = Join-Path $fixture 'original-setup.exe'
    Copy-Item -LiteralPath $installer -Destination $originalSetup
    $signature = Set-AuthenticodeSignature -LiteralPath $installer -Certificate $certificate -HashAlgorithm SHA256
    if ($signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) { throw 'Test installer signing failed.' }
    [WinDesk.Signing.PeSigningContent]::AssertUnchanged($originalSetup, $installer)
    $rejected = $false
    try { Assert-ReleaseSignature -Path $installer | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'The production verifier accepted an untrusted test identity.' }
    Write-Output 'PASS Actual app/uninstaller/Setup signing preserves payload; Inno consumes the signed cache.'
    Write-Output 'PASS Production verifier rejects the test signature. No trust store was changed.'
    Write-Output "Test evidence only (not a distributable release): $fixture"
} finally { $certificate.Dispose(); $rsa.Dispose() }
