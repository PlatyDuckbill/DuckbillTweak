#Requires -Version 7.0
param([string]$DalamudPath = $env:DALAMUD_HOME)
if (!$DalamudPath) { $DalamudPath = & (Join-Path $PSScriptRoot 'prepare-dalamud-reference.ps1') }
if (!(Test-Path -LiteralPath (Join-Path $DalamudPath 'Dalamud.dll') -PathType Leaf)) {
    throw 'Provide -DalamudPath pointing to a complete KR Dalamud API 15 installation.'
}
$duckbillReferencePath = (Resolve-Path -LiteralPath $DalamudPath).Path
$reference = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../build/dalamud-reference.json') -Raw | ConvertFrom-Json
foreach ($module in $reference.Modules.PSObject.Properties) {
    $path = Join-Path $duckbillReferencePath $module.Name
    if (!(Test-Path -LiteralPath $path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $module.Value) {
        throw "Use the pinned compile-only KR reference bundle: scripts/prepare-dalamud-reference.ps1 ($($module.Name) mismatch)."
    }
}
$env:DALAMUD_HOME = $duckbillReferencePath
Write-Output "DALAMUD_HOME=$env:DALAMUD_HOME"
