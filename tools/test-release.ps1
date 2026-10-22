param([string]$Version = '0.7.5')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$release = Join-Path $root "dist\releases\$Version"
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{C984BBCE-651F-4B55-ACB5-8531C17B7C38}_is1'
$menuKey = 'HKCU:\Software\Classes\DesktopBackground\Shell\WinDesk'
if ((Test-Path $uninstallKey) -or (Test-Path $menuKey) -or (Get-Process WinDesk -ErrorAction SilentlyContinue)) {
    throw 'Close WinDesk and use an account without a Setup installation/classic WinDesk menu for this test.'
}
$case = Join-Path $root ('artifacts\setup-test-' + [guid]::NewGuid().ToString('N'))
$installation = Join-Path $case 'application'
$data = Join-Path $case 'isolated-data'
$diagnostics = Join-Path $case 'diagnostics.txt'
New-Item -ItemType Directory -Path $case -Force | Out-Null
$userWorkspace = Join-Path $env:LOCALAPPDATA 'WinDesk\workspace.json'
$before = if (Test-Path -LiteralPath $userWorkspace) { (Get-FileHash -LiteralPath $userWorkspace).Hash } else { $null }
$application = $null
try {
    $installer = Start-Process -FilePath (Join-Path $release "WinDesk-Setup-$Version-x64.exe") -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS="contextmenu"',
        ('/DIR="' + $installation + '"'), ('/GROUP="WinDesk Release QA ' + (Split-Path $case -Leaf) + '"'),
        ('/LOG="' + (Join-Path $case 'install.log') + '"')
    ) -WindowStyle Hidden -PassThru -Wait
    if ($installer.ExitCode -ne 0) { throw "Installer failed: $($installer.ExitCode)" }
    $installedExe = Join-Path $installation 'WinDesk.exe'
    if ((Get-FileHash -LiteralPath $installedExe).Hash -ne (Get-FileHash -LiteralPath (Join-Path $release "WinDesk-$Version-win-x64.exe")).Hash) {
        throw 'The installed executable differs from the release executable.'
    }
    if (-not (Test-Path $uninstallKey)) { throw 'Missing Windows uninstall registration.' }
    foreach ($pair in @(@('01-new', 'new-group'), @('02-toggle', 'toggle-panels'), @('03-settings', 'settings'))) {
        $command = (Get-Item -LiteralPath "$menuKey\shell\$($pair[0])\command").GetValue('')
        if ($command -ne ('"' + $installedExe + '" --command ' + $pair[1])) { throw 'Incorrect desktop menu command.' }
    }
    Write-Output 'PASS Installer payload, uninstall registration and all three desktop menu commands'
    $application = Start-Process -FilePath $installedExe -ArgumentList @('--quiet', '--data-dir', ('"' + $data + '"'), '--diagnostics', ('"' + $diagnostics + '"')) -WindowStyle Hidden -PassThru
    $deadline = (Get-Date).AddSeconds(25)
    while (-not (Test-Path -LiteralPath $diagnostics) -and (Get-Date) -lt $deadline -and -not $application.HasExited) { Start-Sleep -Milliseconds 250 }
    if (-not (Test-Path -LiteralPath $diagnostics)) { throw 'The packaged application did not report a successful startup.' }
    $report = Get-Content -LiteralPath $diagnostics -Raw
    if (-not $report.Contains((Join-Path $data 'workspace.json')) -or $report -notmatch 'loaded=True visible=True') {
        throw "The packaged application did not render its isolated desktop panels. See $diagnostics"
    }
    Write-Output 'PASS Self-contained packaged EXE starts and renders desktop panels with isolated data'
} finally {
    if ($application -and -not $application.HasExited) { Stop-Process -Id $application.Id; $application.WaitForExit() }
    $uninstallerPath = Join-Path $installation 'unins000.exe'
    if (Test-Path -LiteralPath $uninstallerPath) {
        $uninstaller = Start-Process -FilePath $uninstallerPath -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="' + (Join-Path $case 'uninstall.log') + '"')) -WindowStyle Hidden -PassThru -Wait
        if ($uninstaller.ExitCode -ne 0) { throw "Uninstall failed: $($uninstaller.ExitCode)" }
    }
}
if ((Test-Path -LiteralPath (Join-Path $installation 'WinDesk.exe')) -or (Test-Path $uninstallKey) -or (Test-Path $menuKey)) {
    throw 'Uninstall did not remove the test application and its registrations.'
}
$after = if (Test-Path -LiteralPath $userWorkspace) { (Get-FileHash -LiteralPath $userWorkspace).Hash } else { $null }
if ($before -ne $after) { throw 'The user workspace changed during the test.' }
Write-Output 'PASS Uninstall removes program/menu entries and preserves the existing user workspace'
Write-Output "Test evidence: $case"
