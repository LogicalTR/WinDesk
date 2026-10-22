$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'tools\release-common.ps1')
$assertions = 0
function Check([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:assertions++; Write-Output "PASS $Message"
}
function Rejects([scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    Check $rejected $Message
}
$artifacts = @(@{name='app.exe';size=10;sha256=('a' * 64)}, @{name='source.zip';size=20;sha256=('b' * 64)})
function Release($Names = @('app.exe','source.zip')) {
    return @{assets=@($artifacts | Where-Object name -In $Names | ForEach-Object { @{name=$_.name;size=$_.size;digest="sha256:$($_.sha256)";state='uploaded'} })}
}
Assert-ReleaseAssets (Release) $artifacts
Check $true 'Matching remote digests and sizes pass'
Assert-ReleaseAssets (Release @('app.exe')) $artifacts -AllowMissing
Check $true 'A draft may resume with missing assets'
Rejects { Assert-ReleaseAssets (Release @('app.exe')) $artifacts } 'Missing assets block publication'
$wrong = Release; $wrong.assets[0].digest = 'sha256:' + ('c' * 64)
Rejects { Assert-ReleaseAssets $wrong $artifacts } 'Changed content blocks publication even when size matches'
$wrong = Release; $wrong.assets[0].size = 9
Rejects { Assert-ReleaseAssets $wrong $artifacts } 'Truncated remote assets block publication'
$wrong = Release; $wrong.assets[0].state = 'starter'
Rejects { Assert-ReleaseAssets $wrong $artifacts } 'Incomplete upload blocks publication'
$wrong = Release; $wrong.assets += $wrong.assets[0]
Rejects { Assert-ReleaseAssets $wrong $artifacts } 'Duplicate assets block publication'
$wrong = Release; $wrong.assets += @{name='unexpected.exe';size=1;digest='';state='uploaded'}
Rejects { Assert-ReleaseAssets $wrong $artifacts } 'Unexpected assets block publication'
$notes = Get-ReleaseNotes "# WinDesk 0.7.7`n`nNew features`n`n# WinDesk 0.7.6`nOld features`n" '0.7.7'
Check ($notes.Contains('New features') -and -not $notes.Contains('Old features')) 'Only the selected version notes are published'
Rejects { Get-ReleaseNotes '# WinDesk 0.7.6' '0.7.7' } 'Missing version notes block publication'
$existing = "Keep before`n<!-- windesk-release:start -->`nOld downloads`n<!-- windesk-release:end -->`nKeep after"
$readme = Get-DistributionReadme $existing 'any' 'Download {{VERSION}}' '0.7.7'
Check ($readme.Contains('Keep before') -and $readme.Contains('Keep after') -and $readme.Contains('Download 0.7.7') -and -not $readme.Contains('Old downloads')) 'README edits outside the managed section are preserved'
Rejects { Get-DistributionReadme 'User-written README' 'unknown' 'Download {{VERSION}}' '0.7.7' } 'Unknown README content cannot be overwritten'
Rejects { Get-DistributionReadme ($existing + $existing) 'unknown' 'Download {{VERSION}}' '0.7.7' } 'Ambiguous managed README sections are rejected'
Write-Output "$assertions publisher assertions passed."
