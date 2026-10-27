#Requires -Version 7.0
# Compile references only. Never installs DLLs into the launcher/game or plugin output.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$duckbillRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$reference = Get-Content -LiteralPath (Join-Path $duckbillRoot 'build/dalamud-reference.json') -Raw | ConvertFrom-Json
$directory = Join-Path $duckbillRoot ".cache/dalamud-reference/$($reference.Distribution)"
if (!(Test-Path -LiteralPath $directory -PathType Container)) {
    $archive = "$directory.zip"
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $directory) | Out-Null
    if (!(Test-Path -LiteralPath $archive -PathType Leaf)) {
        Invoke-WebRequest -Uri $reference.Url -OutFile $archive
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -cne $reference.Sha256) {
        throw 'Pinned reference archive hash mismatch. No files have been extracted.'
    }
    Expand-Archive -LiteralPath $archive -DestinationPath $directory
}
foreach ($module in $reference.Modules.PSObject.Properties) {
    $path = Join-Path $directory $module.Name
    if (!(Test-Path -LiteralPath $path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $module.Value) {
        throw "Pinned reference module mismatch: $($module.Name)"
    }
}
Write-Output $directory
