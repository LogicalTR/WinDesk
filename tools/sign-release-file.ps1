param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$ConfigurationPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'release-signing.ps1')
# Inno Setup calls this for Setup, its temporary copies, and the uninstaller.
$configuration = Get-Content -LiteralPath $ConfigurationPath -Raw | ConvertFrom-Json
Invoke-ReleaseSigning -Path $Path -Configuration $configuration
