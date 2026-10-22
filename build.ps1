param([switch]$Run, [switch]$Test)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$localSdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (Test-Path -LiteralPath $localSdk) { $dotnetExe = $localSdk }
else { $dotnetExe = (Get-Command dotnet -ErrorAction Stop).Source }
if ($Test) {
    & $dotnetExe run --project (Join-Path $projectRoot 'tests\WinDesk.Core.Tests') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    if (Get-Process -Name WinDesk -ErrorAction SilentlyContinue) { throw 'Close WinDesk before running WPF tests.' }
    & $dotnetExe run --project (Join-Path $projectRoot 'tests\WinDesk.App.Tests') -c Release -- --quiet --data-dir (Join-Path $projectRoot ('artifacts\ui-test-' + [guid]::NewGuid().ToString('N')))
    if ($LASTEXITCODE -ne 0) { throw 'WPF interaction tests failed.' }
}
& $dotnetExe publish (Join-Path $projectRoot 'src\WinDesk.App') -c Release -r win-x64 --self-contained false -o (Join-Path $projectRoot 'dist\WinDesk')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ($Run) { Start-Process -FilePath (Join-Path $projectRoot 'dist\WinDesk\WinDesk.exe') }
