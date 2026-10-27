param([Parameter(Mandatory)][string]$Root, [Parameter(Mandatory)][string]$Report, [ValidatePattern('^\d+\.\d+\.\d+$')][string]$PreviousVersion = '0.7.6')
$ErrorActionPreference = 'Stop'
if ($env:COMPUTERNAME -ne 'WINDESK-QA') { throw 'Upgrade testing is restricted to the disposable VM.' }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if ([Security.Principal.WindowsPrincipal]::new($identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run without elevation.' }
$version = ([xml](Get-Content "$Root\src\WinDesk.App\WinDesk.App.csproj" -Raw)).Project.PropertyGroup.Version
$oldVersion = $PreviousVersion
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{C984BBCE-651F-4B55-ACB5-8531C17B7C38}_is1'
$menu = 'HKCU:\Software\Classes\DesktopBackground\Shell\WinDesk'
if ((Test-Path $key) -or (Get-Process WinDesk -ErrorAction SilentlyContinue)) { throw 'VM must have no existing installation or running WinDesk.' }
$case = Join-Path $Root 'artifacts\installed-update'
New-Item -ItemType Directory -Path $case -Force | Out-Null
$install = Join-Path $case 'Custom installation'
$data = Join-Path $env:LOCALAPPDATA 'WinDesk'
$workspace = Join-Path $data 'workspace.json'
$ownerFile = Join-Path $data 'upgrade-test-owner.txt'
if (Test-Path -LiteralPath $workspace) {
    if (-not (Test-Path $ownerFile) -or (Get-Content $ownerFile -Raw).Trim() -ne 'WinDesk QA installed update fixture') {
        throw 'Use a clean VM profile; test will not replace existing user data.'
    }
    $sourcePath = [IO.Path]::GetFullPath($data)
    $backupPath = [IO.Path]::GetFullPath((Join-Path $case 'previous-fixture-data'))
    if ($sourcePath -ne ([IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'WinDesk'))) -or
        -not $backupPath.StartsWith([IO.Path]::GetFullPath($case) + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected fixture paths' }
    Move-Item -LiteralPath $sourcePath -Destination $backupPath
}
$checks = [Collections.Generic.List[string]]::new()
function Check([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message }; $checks.Add($Message); Write-Output "PASS $Message" }
function Run-Process([string]$File, [string]$Arguments, [int]$Timeout = 180000) {
    $start = [Diagnostics.ProcessStartInfo]::new($File, $Arguments)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $process = [Diagnostics.Process]::Start($start)
    try { if (-not $process.WaitForExit($Timeout)) { $process.Kill(); throw "Test process timed out: $File" }; if ($process.ExitCode -ne 0) { throw "Test process exited $($process.ExitCode): $File" } }
    finally { $process.Dispose() }
}
function Check-Started([string]$Label) {
    $diagnostic = Join-Path $case "$Label-diagnostics.txt"
    $process = Start-Process -FilePath "$install\WinDesk.exe" -ArgumentList @('--diagnostics', ('"' + $diagnostic + '"')) -WindowStyle Hidden -PassThru
    try {
        $deadline = (Get-Date).AddSeconds(30)
        while (-not (Test-Path $diagnostic) -and (Get-Date) -lt $deadline -and -not $process.HasExited) { Start-Sleep -Milliseconds 200 }
        Check ((Test-Path $diagnostic) -and (Get-Content $diagnostic -Raw) -match 'loaded=True visible=True') "$Label installed EXE opens and renders its preserved groups"
    } finally {
        # Only the VM process just created by this test is stopped; its seeded
        # workspace is unchanged. The handoff case below exercises graceful exit.
        if (-not $process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() }
        $process.Dispose()
    }
}
$oldSetup = "$Root\dist\releases\$oldVersion\WinDesk-Setup-$oldVersion-x64.exe"
$newSetup = "$Root\dist\releases\$version\WinDesk-Setup-$version-x64.exe"
$expectedHash = (Get-FileHash "$Root\dist\releases\$version\WinDesk-$version-win-x64.exe").Hash
try {
    Run-Process $oldSetup ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS="contextmenu,startup" /DIR="' + $install + '" /GROUP="WinDesk Upgrade QA" /LOG="' + "$case\old-install.log" + '"')
    Check ((Get-ItemProperty $key).DisplayVersion -eq $oldVersion) "Real $oldVersion installer registered in a custom installation directory"
    New-Item -ItemType Directory -Path "$data\Shortcuts" -Force | Out-Null
    'WinDesk QA installed update fixture' | Set-Content -LiteralPath $ownerFile -Encoding ascii
    $shortcut = Join-Path $data 'Shortcuts\Türkçe-日本語.url'
    "[InternetShortcut]`r`nURL=https://github.com/LogicalTR/WinDesk`r`n" | Set-Content -LiteralPath $shortcut -Encoding ascii
    $groupA = [guid]::NewGuid().ToString(); $groupB = [guid]::NewGuid().ToString(); $stack = [guid]::NewGuid().ToString()
    @{Version=4;CloseDelayMs=850;Panels=@(
        @{Id=$groupA;Name='Çalışma 日本語';Color='#12ABEF';X=100;Y=90;IsPinned=$true;StackId=$stack;Items=@(@{Name='Kayıtlı bağlantı';Path=$shortcut;OwnedShortcutPath=$shortcut;Arguments='';WorkingDirectory=''})},
        @{Id=$groupB;Name='İkinci grup';Color='#73D9C3';X=100;Y=370;IsPinned=$false;StackId=$stack;Items=@()}
    );Stacks=@(@{Id=$stack;Name='Birleşik alan';X=100;Y=90})} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $workspace -Encoding utf8
    $before = (Get-FileHash $workspace).Hash; $shortcutHash = (Get-FileHash $shortcut).Hash
    Check-Started $oldVersion
    Run-Process $newSetup ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="' + "$case\upgrade.log" + '"')
    Check ((Get-ItemProperty $key).DisplayVersion -eq $version -and (Get-FileHash "$install\WinDesk.exe").Hash -eq $expectedHash) "Upgrade replaces $oldVersion with the exact $version release EXE and remembers the custom directory"
    Check ((Get-FileHash $workspace).Hash -eq $before -and (Get-FileHash $shortcut).Hash -eq $shortcutHash) 'Upgrade preserves the complete workspace and saved Unicode shortcut byte for byte'
    $startup = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run').WinDesk
    Check ($startup -eq ('"' + "$install\WinDesk.exe" + '"')) 'Upgrade retains the selected Windows startup task'
    foreach ($pair in @(@('01-new','new-group'),@('02-toggle','toggle-panels'),@('03-settings','settings'))) {
        Check ((Get-Item "$menu\shell\$($pair[0])\command").GetValue('') -eq ('"' + "$install\WinDesk.exe" + '" --command ' + $pair[1])) "Upgrade retains desktop menu command $($pair[1])"
    }
    Check-Started $version

    $handoff = Join-Path $case 'handoff'; New-Item -ItemType Directory -Path $handoff -Force | Out-Null
    $request = Join-Path $case 'handoff-request.json'
    @{Installer=$newSetup;Version=$version;PreviousVersion=$oldVersion;Report=$handoff} | ConvertTo-Json | Set-Content -LiteralPath $request -Encoding utf8
    Run-Process "$Root\tests\app\WinDesk.App.Tests.exe" ('--installed-update-test "' + $request + '"')
    if (Test-Path "$handoff\handoff-error.txt") { throw (Get-Content "$handoff\handoff-error.txt" -Raw) }
    $receipt = Get-Content "$handoff\handoff.json" -Raw | ConvertFrom-Json
    $installerProcess = Get-Process -Id $receipt.InstallerPid -ErrorAction SilentlyContinue
    if ($installerProcess) { if (-not $installerProcess.WaitForExit(180000)) { throw 'Handoff installer did not exit' } }
    $log = Get-Content "$handoff\handoff-setup.log" -Raw
    Check ($log -match 'Installation process succeeded' -and $log -match '/WINDESKUPDATE=1') 'Production update handoff starts and completes the real release installer'
    Check ((Get-Content "$handoff\application-exited.txt" -Raw) -match 'IsExiting=True; Code=0') 'Application saves and exits normally before the update completes'
    $saved = Get-Content $workspace -Raw | ConvertFrom-Json
    Check ($receipt.SavedBeforeLaunch -and $saved.Panels[0].Name -eq 'Kaydedilen güncelleme · 日本語' -and $saved.CloseDelayMs -eq 1250) 'Unsaved in-memory edits are saved before launching Setup and survive installation'
    Check ($saved.Panels.Count -eq 2 -and $saved.Stacks.Count -eq 1 -and $saved.Panels[0].Id -eq $groupA -and $saved.Panels[0].IsPinned -and (Get-FileHash $shortcut).Hash -eq $shortcutHash) 'Groups, accordion membership, pins and owned shortcuts survive the app update path'
    Check ((Get-FileHash "$install\WinDesk.exe").Hash -eq $expectedHash) 'Update handoff installs the exact release binary'
    Check-Started 'after-handoff'
    $savedHash = (Get-FileHash $workspace).Hash
    Run-Process "$install\unins000.exe" ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="' + "$case\uninstall.log" + '"')
    Check (-not (Test-Path $key) -and -not (Test-Path $menu) -and -not (Test-Path "$install\WinDesk.exe")) 'Uninstall after upgrading removes the program and menu registrations'
    Check ((Get-FileHash $workspace).Hash -eq $savedHash -and (Get-FileHash $shortcut).Hash -eq $shortcutHash) 'Uninstall after updating preserves the user layout and saved shortcut'
    @{Status='passed';From=$oldVersion;To=$version;Checks=$checks.ToArray();ReleaseHash=$expectedHash;SetupHash=(Get-FileHash $newSetup).Hash;Environment=[Environment]::OSVersion.Version.ToString();Elevated=$false;Finished=(Get-Date).ToString('o')} |
        ConvertTo-Json -Depth 5 | Set-Content "$case\result.json"
} catch {
    @{Status='failed';Checks=$checks.ToArray();Error=$_.Exception.ToString()} | ConvertTo-Json -Depth 5 | Set-Content "$case\result.json"
    throw
} finally {
    # Retain the seeded VM user data for inspection; never erase the profile.
    Compress-Archive -LiteralPath $case -DestinationPath "$Root\installed-update-evidence.zip" -Force
    Copy-Item -LiteralPath "$Root\installed-update-evidence.zip" -Destination "$Report\installed-update-evidence.zip" -Force
    if (Test-Path "$case\result.json") { Copy-Item "$case\result.json" "$Report\installed-update-result.json" -Force }
}
