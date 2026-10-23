. (Join-Path (Split-Path -Parent $PSScriptRoot) 'release-signing.ps1')
if (-not ('WinDesk.Signing.PeSigningContent' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'PeSigningContent.cs')
}
function Assert-SignPathMetadata([string]$Path, [string]$Version) {
    $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
    # Inno Setup reserves fixed-size resource strings padded with spaces.
    if (([string]$info.ProductName).TrimEnd() -cne 'WinDesk' -or ([string]$info.ProductVersion).TrimEnd() -cne "$Version.0") {
        throw "Wrong product name/version: $Path ($($info.ProductName), $($info.ProductVersion))"
    }
}
function Assert-SignPathResult([string]$Original, [string]$Signed, [string]$Version, [string]$Thumbprint) {
    if ($Thumbprint -notmatch '^[A-Fa-f0-9]{40}$') { throw 'Set the approved SignPath certificate SHA-1 thumbprint.' }
    Assert-ReleaseSignature -Path $Signed -ExpectedThumbprint $Thumbprint | Out-Null
    Assert-SignPathMetadata $Signed $Version
    [WinDesk.Signing.PeSigningContent]::AssertUnchanged($Original, $Signed)
}
function Assert-SignPathFiles([string]$Directory, [string[]]$Expected) {
    $actual = @(Get-ChildItem -LiteralPath $Directory -File -Recurse | ForEach-Object {
        [IO.Path]::GetRelativePath($Directory, $_.FullName).Replace('\', '/')
    })
    if (@(Compare-Object @($Expected | Sort-Object) @($actual | Sort-Object)).Count) {
        throw "Unexpected SignPath output file set in $Directory"
    }
}
