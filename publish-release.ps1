param([switch]$Foreground, [switch]$ValidateOnly)
$ErrorActionPreference = 'Stop'
$worker = Join-Path $PSScriptRoot 'tools\publish-release.ps1'
$run = Join-Path $PSScriptRoot ('artifacts\publishing\' + (Get-Date -Format yyyyMMdd-HHmmss) + '-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
if ($Foreground) {
    & $worker -RunDirectory $run -ValidateOnly:$ValidateOnly
    return
}
$pwsh = (Get-Command pwsh -ErrorAction Stop).Source
# EncodedCommand avoids command-line quoting problems with spaces/Unicode paths.
$command = "& '" + $worker.Replace("'", "''") + "' -RunDirectory '" + $run.Replace("'", "''") + "'"
if ($ValidateOnly) { $command += ' -ValidateOnly' }
$command += '; if (-not $?) { exit 1 }'
$encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
$process = Start-Process -FilePath $pwsh -ArgumentList @('-NoProfile', '-NonInteractive', '-OutputFormat', 'Text', '-EncodedCommand', $encoded) `
    -WindowStyle Hidden -WorkingDirectory $PSScriptRoot -RedirectStandardOutput (Join-Path $run 'console.log') `
    -RedirectStandardError (Join-Path $run 'error.log') -PassThru
[pscustomobject]@{ ProcessId = $process.Id; StatusFile = (Join-Path $run 'status.json'); Log = (Join-Path $run 'publish.log'); ValidationOnly = [bool]$ValidateOnly }
