#Requires -Version 7.0
param([string]$DalamudPath = $env:DALAMUD_HOME)
if (!$DalamudPath) { $DalamudPath = Join-Path $env:APPDATA 'XIVLauncherKR/addon/Hooks/dev' }
if (!(Test-Path -LiteralPath (Join-Path $DalamudPath 'Dalamud.dll') -PathType Leaf)) {
    throw 'Provide -DalamudPath pointing to a complete KR Dalamud API 15 installation.'
}
$env:DALAMUD_HOME = (Resolve-Path -LiteralPath $DalamudPath).Path
Write-Output "DALAMUD_HOME=$env:DALAMUD_HOME"
