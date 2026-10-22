function Get-ReleaseNotes([string]$Text, [string]$Version) {
    $pattern = '(?ms)^# WinDesk ' + [regex]::Escape($Version) + '\s*\r?\n(.*?)(?=^# WinDesk |\z)'
    $match = [regex]::Match($Text, $pattern)
    if (-not $match.Success -or [string]::IsNullOrWhiteSpace($match.Groups[1].Value)) { throw "RELEASE_NOTES.md has no notes for $Version." }
    return ("# WinDesk $Version`n`n" + $match.Groups[1].Value.Trim() + "`n")
}

function Assert-ReleaseAssets($Release, $Artifacts, [switch]$AllowMissing) {
    $seen = @{}
    foreach ($remote in $Release.assets) {
        if ($seen.ContainsKey($remote.name)) { throw "Duplicate remote asset: $($remote.name)" }
        $seen[$remote.name] = $true
        $local = @($Artifacts | Where-Object name -CEQ $remote.name)
        if ($local.Count -ne 1) { throw "Unexpected remote asset: $($remote.name)" }
        if ($remote.state -ne 'uploaded' -or $remote.size -ne $local[0].size -or $remote.digest -ne "sha256:$($local[0].sha256)") {
            throw "Remote asset differs or is incomplete: $($remote.name). Published files are never overwritten."
        }
    }
    if (-not $AllowMissing -and $seen.Count -ne @($Artifacts).Count) { throw 'Some release assets are missing.' }
}

function Get-DistributionReadme([string]$Existing, [string]$BlobSha, [string]$Template, [string]$Version) {
    $start = '<!-- windesk-release:start -->'
    $end = '<!-- windesk-release:end -->'
    $block = $start + "`n" + $Template.Replace('{{VERSION}}', $Version).Trim() + "`n" + $end
    $pattern = '(?s)' + [regex]::Escape($start) + '.*?' + [regex]::Escape($end)
    $matches = [regex]::Matches($Existing, $pattern)
    if ($matches.Count -eq 1) { return [regex]::Replace($Existing, $pattern, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $block }) }
    # One-time migration of the reviewed 0.7.6 distribution README. Refuse unknown content.
    if ($matches.Count -eq 0 -and $BlobSha -eq 'c9d5ba076326ce4f727db819575ed52f49adb1a2') { return $block + "`n" }
    throw 'The GitHub README has no unique managed release section. Preserve its edits and add the section before retrying.'
}
