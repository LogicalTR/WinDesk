param([Parameter(Mandatory)][string]$ReleaseDirectory,
      [Parameter(Mandatory)][string]$ReportDirectory, [switch]$ScanDefender, [switch]$RequireSigned)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'release-signing.ps1')
$release = (Resolve-Path -LiteralPath $ReleaseDirectory).Path
New-Item -ItemType Directory -Path $ReportDirectory -Force | Out-Null
$files = @(Get-ChildItem -LiteralPath $release -Filter '*.exe' -File)
if ($files.Count -ne 2) { throw 'Expected the release application and Setup executable.' }
$findings = foreach ($file in $files) {
    $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
    $errorMessage = $null
    try { Assert-ReleaseSignature -Path $file.FullName | Out-Null } catch { $errorMessage = $_.Exception.Message }
    [ordered]@{ File=$file.Name; Sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash;
        SignatureStatus=[string]$signature.Status; Publisher=$signature.SignerCertificate.Subject;
        Timestamped=[bool]$signature.TimeStamperCertificate; ReleaseSignatureError=$errorMessage }
}
$defender = [ordered]@{ ScanStatus='NotRun'; ExitCode=$null }
try {
    $status = Get-MpComputerStatus -ErrorAction Stop
    $defender.State = $status | Select-Object AMServiceEnabled,AntivirusEnabled,RealTimeProtectionEnabled,
        AntivirusSignatureLastUpdated,AntivirusSignatureVersion,AMProductVersion,SmartAppControlState
    if ($ScanDefender) {
        if (-not $status.AMServiceEnabled -or -not $status.AntivirusEnabled) { throw 'Microsoft Defender is not active.' }
        $scanner = Get-ChildItem -LiteralPath "$env:ProgramData\Microsoft\Windows Defender\Platform" -Filter MpCmdRun.exe -Recurse -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1
        # Older Windows versions deny limited users access to the Platform directory.
        if (-not $scanner) { $scanner = Get-Item -LiteralPath "$env:ProgramFiles\Windows Defender\MpCmdRun.exe" -ErrorAction SilentlyContinue }
        if (-not $scanner) { throw 'Microsoft Defender scanner was not found.' }
        & $scanner.FullName -Scan -ScanType 3 -File $release -DisableRemediation *> (Join-Path $ReportDirectory 'defender-scan.log')
        $defender.ExitCode = $LASTEXITCODE
        $defender.ScanStatus = if ($LASTEXITCODE -eq 0) { 'NoThreatsFound' } else { 'DetectionOrScanFailure' }
    }
} catch { $defender.ScanStatus = 'Unavailable'; $defender.Error = $_.Exception.Message }
$report = [ordered]@{
    TestedAtUtc=[DateTime]::UtcNow.ToString('o'); WindowsVersion=[Environment]::OSVersion.Version.ToString();
    ReleaseDirectory=$release; Files=@($findings); Defender=$defender;
    SmartScreen=[ordered]@{ Status='RequiresInteractiveDownloadTest';
        Reason='Authenticode and antivirus scans do not prove SmartScreen reputation. Test an internet-downloaded file through Explorer on a clean Windows desktop; do not bypass or disable protection.' }
}
$reportFile = Join-Path $ReportDirectory 'windows-security.json'
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportFile -Encoding utf8
Write-Output "Security report: $reportFile"
if ($RequireSigned -and @($findings | Where-Object { $_.ReleaseSignatureError }).Count) { throw 'Release signature checks failed; see windows-security.json.' }
if ($ScanDefender -and $defender.ScanStatus -ne 'NoThreatsFound') { throw 'Defender scan did not pass; see windows-security.json and defender-scan.log.' }
