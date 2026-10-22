$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$version = '6.7.3'
$expectedHash = 'F780898E402FF80612CC8D9FCB8C6E02932BD1CB4C900FFDAA31F9341CFB49F4'
$directory = Join-Path $root '.tools\inno-portable'
$compiler = Join-Path $directory 'tools\ISCC.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    New-Item -ItemType Directory -Path (Join-Path $root '.tools') -Force | Out-Null
    $package = Join-Path $root ".tools\tools.innosetup.$version.nupkg"
    Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/tools.innosetup/$version/tools.innosetup.$version.nupkg" -OutFile $package
    if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expectedHash) {
        throw 'Inno Setup package checksum mismatch.'
    }
    # NuGet is only a transport for the portable compiler; no system install.
    Expand-Archive -LiteralPath $package -DestinationPath $directory -Force
}
$signature = Get-AuthenticodeSignature -LiteralPath $compiler
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'CN=Pyrsys B\.V\.') {
    throw 'The Inno Setup compiler must have a valid Pyrsys B.V. signature.'
}
Write-Output $compiler
