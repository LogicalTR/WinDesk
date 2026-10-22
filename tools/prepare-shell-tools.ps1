$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$toolRoot = Join-Path $root '.tools'
$zigDirectory = Join-Path $toolRoot 'zig-x86_64-windows-0.15.2'
if (-not (Test-Path -LiteralPath (Join-Path $zigDirectory 'zig.exe'))) {
    $archive = Join-Path $toolRoot 'zig-0.15.2.zip'
    Invoke-WebRequest -Uri 'https://ziglang.org/download/0.15.2/zig-x86_64-windows-0.15.2.zip' -OutFile $archive
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '3a0ed1e8799a2f8ce2a6e6290a9ff22e6906f8227865911fb7ddedc3cc14cb0c') { throw 'Zig archive checksum mismatch.' }
    [IO.Compression.ZipFile]::ExtractToDirectory($archive, $toolRoot)
}
$sdkDirectory = Join-Path $toolRoot 'windows-sdk-buildtools'
if (-not (Test-Path -LiteralPath $sdkDirectory)) {
    $archive = Join-Path $toolRoot 'windows-sdk-buildtools.nupkg'
    Invoke-WebRequest -Uri 'https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/10.0.26100.8249/microsoft.windows.sdk.buildtools.10.0.26100.8249.nupkg' -OutFile $archive
    [IO.Compression.ZipFile]::ExtractToDirectory($archive, $sdkDirectory)
}
$makeAppx = Get-ChildItem -LiteralPath $sdkDirectory -Recurse -Filter makeappx.exe | Where-Object { $_.Directory.Name -eq 'x64' } | Select-Object -First 1
if (-not $makeAppx -or (Get-AuthenticodeSignature -LiteralPath $makeAppx.FullName).Status -ne 'Valid') { throw 'Microsoft packaging tool signature could not be verified.' }
[pscustomobject]@{Zig=(Join-Path $zigDirectory 'zig.exe'); MakeAppx=$makeAppx.FullName}
