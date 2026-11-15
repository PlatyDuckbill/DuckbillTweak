# Package only plugin-owned files and the pinned KamiToolKit runtime assets.
function Get-RequiredPluginFiles {
    'DuckbillTweak.dll'
    'DuckbillTweak.Core.dll'
    'DuckbillTweak.json'
    'DuckbillTweak.deps.json'
    'KamiToolKit.dll'
    'KamiToolKit.LICENSE.txt'
    'THIRD-PARTY-NOTICES.txt'
    $duckbillRoot = Split-Path $PSScriptRoot -Parent
    foreach ($duckbillResource in Get-ChildItem -LiteralPath (Join-Path $duckbillRoot 'src/DuckbillTweak.Plugin/Localization') -Filter 'Strings.*.resx') {
        $duckbillCulture = $duckbillResource.BaseName.Substring('Strings.'.Length)
        if ([Globalization.CultureInfo]::GetCultureInfo($duckbillCulture).Name -cne $duckbillCulture) {
            throw "Noncanonical resource culture: $duckbillCulture"
        }
        "$duckbillCulture/DuckbillTweak.resources.dll"
    }
    $duckbillToolkit = Join-Path $duckbillRoot 'third_party/KamiToolKit'
    foreach ($duckbillAsset in Get-ChildItem -LiteralPath (Join-Path $duckbillToolkit 'Assets') -Recurse -File) {
        [IO.Path]::GetRelativePath($duckbillToolkit, $duckbillAsset.FullName).Replace('\', '/')
    }
}
