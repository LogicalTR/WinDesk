param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\WinDesk.App\WinDesk.App.csproj'
$runtime = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.RuntimeFrameworkVersion
$sdk = (Get-Content -LiteralPath (Join-Path $root 'global.json') -Raw | ConvertFrom-Json).sdk.version
if ((& dotnet --version) -ne $sdk) { throw "Release SDK must be $sdk." }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a fresh application output directory.' }
$compiler = & (Join-Path $PSScriptRoot 'prepare-release-tools.ps1')
& dotnet restore $project -r win-x64 -p:SelfContained=true | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Release restore failed.' }
$assets = Get-Content -LiteralPath (Join-Path $root 'src\WinDesk.App\obj\project.assets.json') -Raw | ConvertFrom-Json
$packages = @($assets.packageFolders.PSObject.Properties.Name | Where-Object {
    Test-Path -LiteralPath (Join-Path $_ "microsoft.netcore.app.runtime.win-x64\$runtime")
}) | Select-Object -First 1
if (-not $packages) { throw 'Runtime package directory was not resolved.' }
$sdkLine = @(& dotnet --list-sdks | Where-Object { $_ -match ('^' + [regex]::Escape($sdk) + ' \[') }) | Select-Object -First 1
if ($sdkLine -notmatch '\[(.+)\]$') { throw 'SDK location was not resolved.' }
$desktopSdk = Join-Path $Matches[1] "$sdk\Sdks\Microsoft.NET.Sdk.WindowsDesktop"
$notices = Join-Path ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputDirectory))) 'notices'
New-Item -ItemType Directory -Path $notices -Force | Out-Null
$sources = [ordered]@{
    'dotnet-LICENSE.txt' = Join-Path $packages "microsoft.netcore.app.runtime.win-x64\$runtime\LICENSE.TXT"
    'dotnet-THIRD-PARTY-NOTICES.txt' = Join-Path $packages "microsoft.netcore.app.runtime.win-x64\$runtime\THIRD-PARTY-NOTICES.TXT"
    'windowsdesktop-LICENSE.txt' = Join-Path $packages "microsoft.windowsdesktop.app.runtime.win-x64\$runtime\LICENSE"
    'windowsdesktop-THIRD-PARTY-NOTICES.txt' = Join-Path $desktopSdk 'THIRD-PARTY-NOTICES.TXT'
    'innosetup-LICENSE.txt' = Join-Path (Split-Path -Parent $compiler) 'license.txt'
}
foreach ($entry in $sources.GetEnumerator()) {
    Copy-Item -LiteralPath $entry.Value -Destination (Join-Path $notices $entry.Key)
}
& dotnet publish $project -c Release -r win-x64 --self-contained true --no-restore `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -p:DebugType=embedded `
    "-p:ReleaseNoticesDirectory=$notices" -o $OutputDirectory | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }
$files = @(Get-ChildItem -LiteralPath $OutputDirectory -File -Recurse)
if ($files.Count -ne 1 -or $files[0].Name -ne 'WinDesk.exe') { throw 'Expected exactly one self-contained executable before notices.' }
Copy-Item -LiteralPath $notices -Destination (Join-Path $OutputDirectory 'licenses') -Recurse
foreach ($relative in 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'packaging\PRIVACY.txt', 'packaging\KULLANIM.txt') {
    Copy-Item -LiteralPath (Join-Path $root $relative) -Destination $OutputDirectory
}
