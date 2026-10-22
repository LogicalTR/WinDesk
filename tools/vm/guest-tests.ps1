param([Parameter(Mandatory)][string]$Root, [Parameter(Mandatory)][string]$Report)
$ErrorActionPreference = 'Stop'
if ($env:COMPUTERNAME -ne 'WINDESK-QA') { throw 'Guest tests may only run in the disposable WinDesk VM.' }
$os = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
if ($os.CurrentBuildNumber -ne '19045' -or $os.DisplayVersion -ne '22H2') { throw 'Expected real Windows 10 22H2, build 19045.' }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Tests must run without elevation.' }
$version = ([xml](Get-Content "$Root\src\WinDesk.App\WinDesk.App.csproj" -Raw)).Project.PropertyGroup.Version
$details = [ordered]@{
    Product = $os.ProductName; DisplayVersion = $os.DisplayVersion
    Build = "$($os.CurrentBuildNumber).$($os.UBR)"; Architecture = $env:PROCESSOR_ARCHITECTURE
    Elevated = $false; DotNetInstalled = (Test-Path 'C:\Program Files\dotnet\dotnet.exe')
    AppVersion = $version
    ReleaseSha256 = (Get-FileHash "$Root\dist\releases\$version\WinDesk-$version-win-x64.exe" -Algorithm SHA256).Hash
    Started = (Get-Date).ToString('o')
}
$details | ConvertTo-Json | Set-Content -LiteralPath "$Report\environment.json"
Get-CimInstance SoftwareLicensingProduct -Filter "ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey IS NOT NULL" |
    Select-Object Name, LicenseStatus, GracePeriodRemaining, EvaluationEndDate |
    ConvertTo-Json | Set-Content -LiteralPath "$Report\windows-license.json"
Set-Location $Root
$results = [System.Collections.Generic.List[object]]::new()
function Run-Case([string]$Name, [scriptblock]$Action) {
    try {
        & $Action *> "$Report\$Name.log"
        $results.Add(@{ Name = $Name; Status = 'passed' })
    } catch {
        $_ | Out-String | Add-Content -LiteralPath "$Report\$Name.log"
        $results.Add(@{ Name = $Name; Status = 'failed'; Error = $_.Exception.Message })
    }
}
function Run-Executable([string]$Executable, [string[]]$Arguments) {
    # Start-Process -PassThru can lose ExitCode after WaitForExit on Windows
    # PowerShell 5.1. Own the process handle and drain both pipes asynchronously.
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Arguments = ($Arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $process = [Diagnostics.Process]::Start($start)
    $output = $process.StandardOutput.ReadToEndAsync()
    $errors = $process.StandardError.ReadToEndAsync()
    try {
        if (-not $process.WaitForExit(180000)) { Stop-Process -Id $process.Id; throw "$Executable timed out after 180 seconds" }
        $process.WaitForExit()
        $output.GetAwaiter().GetResult()
        $errors.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "$Executable exited with $($process.ExitCode)" }
    } finally { $process.Dispose() }
}

# Run Setup before the test bundles: proves the released app needs no global .NET.
Run-Case 'setup' { & "$Root\tools\test-release.ps1" -Version $version }
Run-Case 'core' { Run-Executable "$Root\tests\core\WinDesk.Core.Tests.exe" @() }
Run-Case 'desktop' { Run-Executable "$Root\tests\app\WinDesk.App.Tests.exe" @('--quiet', '--data-dir', "$Root\artifacts\desktop") }
Run-Case 'shortcut-import' { Run-Executable "$Root\tests\app\WinDesk.App.Tests.exe" @('--import-test') }
Run-Case 'recovery' { Run-Executable "$Root\tests\app\WinDesk.App.Tests.exe" @('--recovery-test') }
Run-Case 'display' { Run-Executable "$Root\tests\app\WinDesk.App.Tests.exe" @('--display-test', '--quiet', '--data-dir', "$Root\artifacts\display") }
Run-Case 'modern-menu-guard' {
    $rejected = $false
    try { & "$Root\install-menu.ps1" } catch {
        if ($_.Exception.Message -notlike '*requires Windows 11 22H2*') { throw }
        $rejected = $true
    }
    if (-not $rejected) { throw 'Modern menu installation was not rejected on Windows 10.' }
    'PASS Modern menu directs Windows 10 to the classic menu before any package changes'
}
Run-Case 'explorer-restart' {
    $env:WINDESK_ALLOW_EXPLORER_RESTART = '1'
    try { Run-Executable "$Root\tests\app\WinDesk.App.Tests.exe" @('--explorer-restart-test', '--quiet', '--data-dir', "$Root\artifacts\restart") }
    finally { Remove-Item Env:\WINDESK_ALLOW_EXPLORER_RESTART }
}

# Keep evidence from an actual guest desktop, including WinDesk's child windows.
Run-Case 'desktop-capture' {
    $exe = "$Root\dist\releases\$version\WinDesk-$version-win-x64.exe"
    $app = Start-Process -FilePath $exe -ArgumentList @('--quiet', '--data-dir', ('"' + "$Root\artifacts\capture" + '"'), '--diagnostics', ('"' + "$Report\desktop-diagnostics.txt" + '"')) -WindowStyle Hidden -PassThru
    try {
        Start-Sleep -Seconds 5
        if ($app.HasExited) { throw 'Released application exited before the desktop capture.' }
        Add-Type -AssemblyName System.Windows.Forms,System.Drawing
        $screen = [System.Windows.Forms.SystemInformation]::VirtualScreen
        $bitmap = [System.Drawing.Bitmap]::new($screen.Width, $screen.Height)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.CopyFromScreen($screen.Location, [System.Drawing.Point]::Empty, $screen.Size); $bitmap.Save("$Report\desktop.png") }
        finally { $graphics.Dispose(); $bitmap.Dispose() }
    } finally { if (-not $app.HasExited) { Stop-Process -Id $app.Id; $app.WaitForExit() } }
}
Run-Case 'evidence-archive' {
    if (Test-Path "$Root\artifacts") {
        # Transfer one file: Windows directory attributes do not round-trip
        # reliably through the Docker/Samba host share during recursive copy.
        Compress-Archive -LiteralPath "$Root\artifacts" -DestinationPath "$Root\evidence.zip" -Force
        Copy-Item -LiteralPath "$Root\evidence.zip" -Destination "$Report\evidence.zip" -Force
    }
}
$failed = @($results | Where-Object Status -eq 'failed')
@{ Status = $(if ($failed.Count) { 'failed' } else { 'passed' }); Environment = $details; Cases = $results.ToArray(); Finished = (Get-Date).ToString('o') } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath "$Report\result.json"
