param([switch]$Disable)

$ErrorActionPreference = 'Stop'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$entryName = 'WinDesk'

if ($Disable) {
    if (Test-Path -LiteralPath $runKey) {
        $key = Get-Item -LiteralPath $runKey
        if ($key.GetValueNames() -contains $entryName) {
            Remove-ItemProperty -LiteralPath $runKey -Name $entryName
        }
    }
    Write-Output 'WinDesk automatic startup disabled for the current user.'
    return
}

$executable = Join-Path $PSScriptRoot 'dist\WinDesk\WinDesk.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Missing application: $executable. Run build.ps1 first."
}
# Windows launches the normal desktop executable after this user signs in.
# No menu command is supplied: toggle-panels would hide the panels on startup.
$command = '"' + $executable + '"'
New-Item -Path $runKey -Force | Out-Null
New-ItemProperty -LiteralPath $runKey -Name $entryName -Value $command -PropertyType String -Force | Out-Null
if ((Get-ItemPropertyValue -LiteralPath $runKey -Name $entryName) -ne $command) {
    throw 'The WinDesk startup registration could not be verified.'
}
Write-Output "WinDesk registered for startup: $command"
Write-Output 'Windows Settings > Apps > Startup can also enable or disable this entry.'
