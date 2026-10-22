# Runs only in the disposable guest's interactive, unelevated desktop session.
$ErrorActionPreference = 'Stop'
if ($env:COMPUTERNAME -ne 'WINDESK-QA') { throw 'This runner is restricted to the WinDesk test VM.' }
$share = '\\host.lan\Data'
$stateFile = Join-Path $env:LOCALAPPDATA 'WinDeskQA-last-request.txt'
$lastRequest = if (Test-Path -LiteralPath $stateFile) { (Get-Content -LiteralPath $stateFile -Raw).Trim() } else { '' }
while ($true) {
    if (Test-Path -LiteralPath "$share\run-request.txt") {
        $request = (Get-Content -LiteralPath "$share\run-request.txt" -Raw).Trim()
        if ($request -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Invalid test run name.' }
        if ($request -and $request -ne $lastRequest) {
            $lastRequest = $request
            $report = Join-Path "$share\results" $request
            New-Item -ItemType Directory -Path $report -Force | Out-Null
            try {
                # A fresh copy allows the host to replace the test bundle between runs.
                $local = Join-Path $env:USERPROFILE "WinDeskQA\$request"
                New-Item -ItemType Directory -Path $local -Force | Out-Null
                Copy-Item -Path "$share\bundle\*" -Destination $local -Recurse -Force
                & "$local\tools\vm\guest-tests.ps1" -Root $local -Report $report
            } catch {
                $_ | Out-String | Set-Content -LiteralPath "$report\runner-error.txt"
                @{ Status = 'failed'; Error = $_.Exception.Message } | ConvertTo-Json | Set-Content -LiteralPath "$report\result.json"
            }
            $request | Set-Content -LiteralPath $stateFile
        }
    }
    Start-Sleep -Seconds 2
}
