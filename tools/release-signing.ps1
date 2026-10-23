# Authenticode for public releases. Never create certificates or change machine trust.
function Get-ReleaseSignTool {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    $path = if ($command) { $command.Source } else {
        $sdk = Join-Path (Split-Path -Parent $PSScriptRoot) '.tools\windows-sdk-buildtools'
        Get-ChildItem -LiteralPath $sdk -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.Directory.Name -eq 'x64' } | Sort-Object FullName -Descending |
            Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $path) { throw 'Install Windows SDK SignTool or run tools\prepare-shell-tools.ps1 first.' }
    $signature = Get-AuthenticodeSignature -LiteralPath $path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation(,|$)') {
        throw 'SignTool must have a valid Microsoft signature.'
    }
    return $path
}

function Get-ReleaseSigningConfiguration {
    param([string]$Thumbprint = $env:WINDESK_SIGN_CERT_SHA1,
          [string]$TimestampUrl = 'http://timestamp.digicert.com', [switch]$MachineStore)
    $Thumbprint = ($Thumbprint -replace '\s', '').ToUpperInvariant()
    if ($Thumbprint -notmatch '^[0-9A-F]{40}$') {
        throw 'Public releases require a CA-issued code-signing certificate. Set WINDESK_SIGN_CERT_SHA1 or -CertificateThumbprint. Use -Unsigned only for local tests.'
    }
    $uri = $null
    if (-not [Uri]::TryCreate($TimestampUrl, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -notin @('http', 'https') -or $uri.UserInfo) {
        throw 'TimestampUrl must be an HTTP(S) RFC 3161 timestamp service without credentials.'
    }
    $store = if ($MachineStore) { 'LocalMachine' } else { 'CurrentUser' }
    $certificate = Get-Item -LiteralPath "Cert:\$store\My\$Thumbprint" -ErrorAction Stop
    if (-not $certificate.HasPrivateKey -or $certificate.Subject -eq $certificate.Issuer -or
        $certificate.NotBefore -gt (Get-Date) -or $certificate.NotAfter -le (Get-Date)) {
        throw 'A current CA-issued certificate with a private key is required; local/self-signed certificates cannot sign public releases.'
    }
    if ('1.3.6.1.5.5.7.3.3' -notin @($certificate.EnhancedKeyUsageList.ObjectId.Value)) {
        throw 'The certificate must allow Code Signing (1.3.6.1.5.5.7.3.3).'
    }
    if ($certificate.PublicKey.Oid.Value -ne '1.2.840.113549.1.1.1') {
        throw 'Use an RSA code-signing certificate for Windows Smart App Control compatibility.'
    }
    if (-not $certificate.Verify()) { throw 'The code-signing certificate chain could not be verified.' }
    return [pscustomobject]@{ Tool = (Get-ReleaseSignTool); Thumbprint = $Thumbprint; TimestampUrl = $TimestampUrl; MachineStore = [bool]$MachineStore }
}

function Assert-ReleaseSignature {
    param([Parameter(Mandatory)][string]$Path, [string]$ExpectedThumbprint)
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or -not $signature.SignerCertificate -or
        $signature.SignatureType -ne 'Authenticode') {
        throw "A valid embedded Authenticode signature is required: $Path ($($signature.Status))."
    }
    if ($signature.SignerCertificate.Subject -eq $signature.SignerCertificate.Issuer) {
        throw "Self-signed certificates are not accepted for public releases: $Path"
    }
    if (-not $signature.TimeStamperCertificate) { throw "A verified timestamp is required: $Path" }
    if ($ExpectedThumbprint -and $signature.SignerCertificate.Thumbprint -ne $ExpectedThumbprint) {
        throw "Unexpected signing certificate: $Path"
    }
    return $signature
}

function Invoke-ReleaseSigning {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)]$Configuration)
    $arguments = @('sign', '/fd', 'SHA256', '/s', 'My', '/sha1', $Configuration.Thumbprint,
        '/tr', $Configuration.TimestampUrl, '/td', 'SHA256', '/d', 'WinDesk', '/du', 'https://github.com/LogicalTR/WinDesk')
    if ($Configuration.MachineStore) { $arguments += '/sm' }
    & $Configuration.Tool @arguments $Path | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Signing or timestamping failed: $Path" }
    & $Configuration.Tool verify /pa /all /tw $Path | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Signature verification failed: $Path" }
    Assert-ReleaseSignature -Path $Path -ExpectedThumbprint $Configuration.Thumbprint | Out-Null
}

function Assert-ReleasePackageSignatures {
    param([Parameter(Mandatory)][string]$Directory, [Parameter(Mandatory)][string]$Version)
    $exe = Join-Path $Directory "WinDesk-$Version-win-x64.exe"
    $signature = Assert-ReleaseSignature -Path $exe
    Assert-ReleaseSignature -Path (Join-Path $Directory "WinDesk-Setup-$Version-x64.exe") `
        -ExpectedThumbprint $signature.SignerCertificate.Thumbprint | Out-Null
    # Compare the archive's bytes as well; a signed loose EXE must not hide an unsigned ZIP payload.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $Directory "WinDesk-$Version-win-x64.zip"))
    try {
        $executables = @($zip.Entries | Where-Object { $_.FullName -match '\.(exe|dll|msi|ps1|bat|cmd)$' })
        if ($executables.Count -ne 1 -or $executables[0].FullName -cne 'WinDesk.exe') { throw 'Unexpected portable ZIP payload.' }
        $stream = $executables[0].Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $sha.Dispose(); $stream.Dispose() }
        if ($hash -ne (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash) { throw 'Portable ZIP differs from the signed release executable.' }
    } finally { $zip.Dispose() }
}
