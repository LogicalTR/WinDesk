$ErrorActionPreference = 'Stop'
if ($env:COMPUTERNAME -ne 'WINDESK-QA') { throw 'Unexpected guest name.' }
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File C:\OEM\guest-runner.ps1'
$principal = New-ScheduledTaskPrincipal -UserId "$env:COMPUTERNAME\WinDeskQA" -LogonType Interactive -RunLevel Limited
$trigger = New-ScheduledTaskTrigger -AtLogOn -User "$env:COMPUTERNAME\WinDeskQA"
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName 'WinDesk compatibility QA' -Action $action -Principal $principal -Trigger $trigger -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName 'WinDesk compatibility QA'
'WinDesk interactive test runner started.' | Set-Content '\\host.lan\Data\bootstrap.txt'
