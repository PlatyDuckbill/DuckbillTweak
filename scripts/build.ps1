#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$DalamudPath = $env:DALAMUD_HOME
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'enter-dev.ps1') -DalamudPath $DalamudPath
$duckbillRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $duckbillRoot
try {
    dotnet restore DuckbillTweak.slnx --locked-mode --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
    dotnet build DuckbillTweak.slnx -c $Configuration --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $duckbillDll = Join-Path $duckbillRoot "src/DuckbillTweak.Plugin/bin/$Configuration/DuckbillTweak.dll"
    dotnet run --project tools/GameDataProbe -c $Configuration --no-build -- resources $duckbillDll
    if ($LASTEXITCODE -ne 0) { throw 'Built plugin resource verification failed.' }
}
finally {
    Pop-Location
}
