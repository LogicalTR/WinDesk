param([Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$RunId)
$ErrorActionPreference = 'Stop'
$requestFile = Join-Path $env:TEMP ('windesk-request-' + [guid]::NewGuid().ToString('N') + '.txt')
try {
    Set-Content -LiteralPath $requestFile -Value $RunId -Encoding ascii
    & docker cp $requestFile 'windesk-win10-22h2:/tmp/windesk-run-request.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Cannot copy the request into the running VM container.' }
    # Write through Samba so its guest read lease is invalidated correctly.
    # Direct host writes can fail or remain invisible to the guest's cache.
    & docker exec windesk-win10-22h2 smbclient -N //20.20.20.1/Data -c 'put /tmp/windesk-run-request.txt run-request.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Cannot submit the test request through the guest share.' }
    Write-Output "Submitted $RunId. Results: shared/results/$RunId under the VM folder."
} finally { Remove-Item -LiteralPath $requestFile -Force -ErrorAction SilentlyContinue }
