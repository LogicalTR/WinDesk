$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'tools\release-signing.ps1')
$assertions = 0
function Check([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:assertions++; Write-Output "PASS $Message"
}
function Rejects([scriptblock]$Action, [string]$Pattern, [string]$Message) {
    $errorMessage = ''
    try { & $Action | Out-Null } catch { $errorMessage = $_.Exception.Message }
    Check ($errorMessage -like $Pattern) "$Message ($errorMessage)"
}
Rejects { Get-ReleaseSigningConfiguration -Thumbprint '' } '*CA-issued*' 'Missing signing identity blocks a public build'
Rejects { Get-ReleaseSigningConfiguration -Thumbprint 'not-a-thumbprint' } '*CA-issued*' 'Malformed signing identity is rejected'
Rejects { Get-ReleaseSigningConfiguration -Thumbprint ('A' * 40) -TimestampUrl 'file:///tmp/timestamp' } '*HTTP(S)*' 'Non-HTTP timestamp services are rejected'
Rejects { Get-ReleaseSigningConfiguration -Thumbprint ('A' * 40) -TimestampUrl 'https://name:secret@example.com' } '*without credentials*' 'Credentials cannot enter a timestamp command'

# Test the publication policy with controlled WinVerifyTrust results. These tests
# do not establish real certificate trust or claim to test SmartScreen reputation.
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('windesk-signing-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$version = '1.2.3'
$exe = Join-Path $fixture "WinDesk-$version-win-x64.exe"
$archive = Join-Path $fixture "WinDesk-$version-win-x64.zip"
$payload = Join-Path $fixture 'WinDesk.exe'
[IO.File]::WriteAllText($exe, 'fixture payload')
Copy-Item -LiteralPath $exe -Destination $payload
Compress-Archive -LiteralPath $payload -DestinationPath $archive
function New-TestSignature {
    return [pscustomobject]@{ Status='Valid'; SignatureType='Authenticode';
        SignerCertificate=[pscustomobject]@{ Subject='CN=Publisher'; Issuer='CN=CA'; Thumbprint=('A' * 40) };
        TimeStamperCertificate=[pscustomobject]@{Subject='CN=Timestamp'} }
}
function Get-AuthenticodeSignature {
    param([string]$LiteralPath)
    if ($script:setupSignature -and $LiteralPath -like '*Setup*') { return $script:setupSignature }
    return $script:testSignature
}
try {
    $script:setupSignature = $null
    $script:testSignature = New-TestSignature
    Assert-ReleasePackageSignatures -Directory $fixture -Version $version
    Check $true 'Matching timestamped publisher signatures and identical ZIP payload pass'
    $script:testSignature.Status = 'NotSigned'
    Rejects { Assert-ReleasePackageSignatures $fixture $version } '*embedded Authenticode*' 'Unsigned cached releases cannot be published'
    $script:testSignature = New-TestSignature
    $script:testSignature.Status = 'HashMismatch'
    Rejects { Assert-ReleaseSignature $exe } '*embedded Authenticode*' 'Modified signed files are rejected'
    $script:testSignature = New-TestSignature
    $script:testSignature.SignatureType = 'Catalog'
    Rejects { Assert-ReleaseSignature $exe } '*embedded Authenticode*' 'Machine-local catalog trust cannot replace an embedded signature'
    $script:testSignature = New-TestSignature
    $script:testSignature.TimeStamperCertificate = $null
    Rejects { Assert-ReleaseSignature $exe } '*timestamp*' 'Missing timestamps block publication'
    $script:testSignature = New-TestSignature
    $script:testSignature.SignerCertificate.Issuer = 'CN=Publisher'
    Rejects { Assert-ReleaseSignature $exe } '*Self-signed*' 'Even locally trusted self-signed files are rejected'
    $script:testSignature = New-TestSignature
    $script:setupSignature = New-TestSignature
    $script:setupSignature.SignerCertificate.Thumbprint = 'B' * 40
    Rejects { Assert-ReleasePackageSignatures $fixture $version } '*Unexpected signing certificate*' 'Setup must use the same signing certificate as the application'
    $script:setupSignature = $null
    [IO.File]::WriteAllText($payload, 'different payload')
    Compress-Archive -LiteralPath $payload -DestinationPath $archive -Force
    Rejects { Assert-ReleasePackageSignatures $fixture $version } '*ZIP differs*' 'Unsigned or altered ZIP payloads cannot hide behind signed loose assets'
    [IO.File]::WriteAllText((Join-Path $fixture 'extra.dll'), 'extra')
    Compress-Archive -LiteralPath $payload, (Join-Path $fixture 'extra.dll') -DestinationPath $archive -Force
    Rejects { Assert-ReleasePackageSignatures $fixture $version } '*Unexpected portable ZIP*' 'Additional executable payloads are rejected'
} finally {
    Remove-Item Function:\Get-AuthenticodeSignature
    # Delete only this test's uniquely created directory, after checking its parent.
    $resolved = [IO.Path]::GetFullPath($fixture)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    if ([IO.Path]::GetDirectoryName($resolved) -ne $tempRoot -or [IO.Path]::GetFileName($resolved) -notlike 'windesk-signing-tests-*') { throw 'Unexpected test cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
Write-Output "$assertions release signing assertions passed."
