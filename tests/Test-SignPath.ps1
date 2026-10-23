#requires -Version 7.4
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'tools\signpath\common.ps1')
$assertions = 0
function Check([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:assertions++; Write-Output "PASS $Message"
}
function Rejects([scriptblock]$Action, [string]$Pattern, [string]$Message) {
    $failure = ''
    try { & $Action | Out-Null } catch { $failure = $_.Exception.Message }
    Check ($failure -like $Pattern) "$Message ($failure)"
}
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('windesk-signpath-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$originalPath = Join-Path $fixture 'original.exe'
$signedPath = Join-Path $fixture 'signed.exe'
try {
    # Synthetic PE signing layout only: this is deliberately not a trusted
    # signature. Real trust and timestamp tests live in Test-ReleaseSigning.
    foreach ($magic in 0x10b, 0x20b) {
        $original = [byte[]]::new(1027)
        [BitConverter]::GetBytes([uint16]0x5a4d).CopyTo($original, 0)
        [BitConverter]::GetBytes([int]128).CopyTo($original, 0x3c)
        [BitConverter]::GetBytes([uint32]0x4550).CopyTo($original, 128)
        [BitConverter]::GetBytes([uint16]$magic).CopyTo($original, 152)
        $security = 152 + $(if ($magic -eq 0x20b) { 112 } else { 96 }) + 32
        [IO.File]::WriteAllBytes($originalPath, $original)
        $signed = [byte[]]::new(1040)
        $original.CopyTo($signed, 0)
        [BitConverter]::GetBytes([uint32]1234).CopyTo($signed, 216)
        [BitConverter]::GetBytes([uint32]1032).CopyTo($signed, $security)
        [BitConverter]::GetBytes([uint32]8).CopyTo($signed, $security + 4)
        [IO.File]::WriteAllBytes($signedPath, $signed)
        [WinDesk.Signing.PeSigningContent]::AssertUnchanged($originalPath, $signedPath)
        Check $true "PE $magic permits signing fields and alignment only"
        $changed = $signed.Clone(); $changed[700] = 1
        [IO.File]::WriteAllBytes($signedPath, $changed)
        Rejects { [WinDesk.Signing.PeSigningContent]::AssertUnchanged($originalPath, $signedPath) } '*payload differs*' 'Changed executable code is rejected'
        $changed = $signed.Clone(); $changed[1028] = 1
        [IO.File]::WriteAllBytes($signedPath, $changed)
        Rejects { [WinDesk.Signing.PeSigningContent]::AssertUnchanged($originalPath, $signedPath) } '*padding*' 'Data hidden in signing padding is rejected'
        [IO.File]::WriteAllBytes($signedPath, ($signed + [byte]0))
        Rejects { [WinDesk.Signing.PeSigningContent]::AssertUnchanged($originalPath, $signedPath) } '*extent*' 'Extra trailing payload is rejected'
    }
    Rejects { Assert-SignPathResult $originalPath $signedPath '1.2.3' '' } '*thumbprint*' 'Missing approved certificate blocks signing results'
    Rejects { Assert-SignPathResult $originalPath $originalPath '1.2.3' ('A' * 40) } '*Authenticode*' 'Unsigned result cannot pass production verification'
    Rejects { Assert-SignPathMetadata $originalPath '1.2.3' } '*product name/version*' 'Missing product metadata is rejected'
    Assert-SignPathFiles $fixture @('original.exe', 'signed.exe')
    Check $true 'Exact output file set is accepted'
    [IO.File]::WriteAllText((Join-Path $fixture 'extra.dll'), 'unexpected')
    Rejects { Assert-SignPathFiles $fixture @('original.exe', 'signed.exe') } '*file set*' 'Unexpected extra binary is rejected'
} finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    if ([IO.Path]::GetDirectoryName($resolved) -ne $tempRoot -or [IO.Path]::GetFileName($resolved) -notlike 'windesk-signpath-*') { throw 'Unexpected test cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
Write-Output "$assertions SignPath integrity assertions passed. These are not a live SignPath service test."
