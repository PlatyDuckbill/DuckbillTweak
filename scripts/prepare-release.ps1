#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [string]$DalamudPath = $env:DALAMUD_HOME
)

# Local preparation only. Does not create a tag, upload assets or publish an index.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$duckbillRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'package-files.ps1')
[xml]$duckbillProject = Get-Content -LiteralPath (Join-Path $duckbillRoot 'src/DuckbillTweak.Plugin/DuckbillTweak.Plugin.csproj') -Raw
$duckbillVersion = [string]$duckbillProject.Project.PropertyGroup.Version
if ($duckbillVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Expected a four-component project version.' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $duckbillRoot "artifacts/release/$duckbillVersion/candidate1" }
$duckbillOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $duckbillOutput) { throw 'Output already exists. Use a new OutputDirectory for a new candidate.' }
$duckbillValidation = Join-Path $duckbillRoot ".cache/release-validation/$duckbillVersion/$([IO.Path]::GetFileName($duckbillOutput))"
if (Test-Path -LiteralPath $duckbillValidation) { throw 'Validation directory already exists. Choose a different candidate directory name.' }

Push-Location -LiteralPath $duckbillRoot
try {
    if ((git -C third_party/KamiToolKit rev-parse HEAD) -cne '1eaccb5c6d6fcc697f7e1b98b5528580f5edd201') { throw 'Unexpected KamiToolKit revision.' }
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release -DalamudPath $DalamudPath
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    $duckbillBuild = Join-Path $duckbillRoot 'src/DuckbillTweak.Plugin/bin/Release'
    $duckbillManifest = Get-Content -LiteralPath (Join-Path $duckbillBuild 'DuckbillTweak.json') -Raw | ConvertFrom-Json
    if ($duckbillManifest.InternalName -cne 'DuckbillTweak' -or $duckbillManifest.AssemblyVersion -ne $duckbillVersion -or
        $duckbillManifest.DalamudApiLevel -ne 15 -or !$duckbillManifest.CanUnloadAsync) { throw 'Manifest identity/API/version mismatch.' }
    $duckbillAssembly = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $duckbillBuild 'DuckbillTweak.dll'))
    if ($duckbillAssembly.Name -cne 'DuckbillTweak' -or $duckbillAssembly.Version -ne [version]$duckbillVersion) { throw 'DLL version mismatch.' }
    $duckbillIconRecord = $null
    $duckbillIconPath = Join-Path $duckbillRoot 'assets/duckbilltweak-icon.png'
    if (Test-Path -LiteralPath $duckbillIconPath) {
        $duckbillIconBytes = [IO.File]::ReadAllBytes($duckbillIconPath)
        if ($duckbillIconBytes.Length -lt 24 -or [Convert]::ToHexString($duckbillIconBytes[0..7]) -cne '89504E470D0A1A0A') { throw 'Invalid icon PNG.' }
        $duckbillIconWidth = ([int]$duckbillIconBytes[16] -shl 24) -bor ([int]$duckbillIconBytes[17] -shl 16) -bor ([int]$duckbillIconBytes[18] -shl 8) -bor [int]$duckbillIconBytes[19]
        $duckbillIconHeight = ([int]$duckbillIconBytes[20] -shl 24) -bor ([int]$duckbillIconBytes[21] -shl 16) -bor ([int]$duckbillIconBytes[22] -shl 8) -bor [int]$duckbillIconBytes[23]
        if ($duckbillIconWidth -ne $duckbillIconHeight -or $duckbillIconWidth -lt 64 -or $duckbillIconWidth -gt 512) { throw 'Icon must be a square PNG between 64 and 512 pixels.' }
        $duckbillIconUrl = "https://raw.githubusercontent.com/PlatyDuckbill/DuckbillTweak/v$duckbillVersion/assets/duckbilltweak-icon.png"
        if ($duckbillManifest.IconUrl -cne $duckbillIconUrl) { throw 'Manifest icon URL does not match this release.' }
        $duckbillIconRecord = [ordered]@{Width=$duckbillIconWidth;Height=$duckbillIconHeight;Url=$duckbillIconUrl;Sha256=(Get-FileHash -LiteralPath $duckbillIconPath -Algorithm SHA256).Hash.ToLowerInvariant()}
    }
    $duckbillHistory = @(Get-Content -LiteralPath 'data/releases.json' -Raw | ConvertFrom-Json)
    $duckbillNotes = @($duckbillHistory | Where-Object Version -eq $duckbillVersion)
    if ($duckbillNotes.Count -ne 1) { throw 'Expected exactly one release-history entry for this version.' }
    $duckbillEnglish = (@($duckbillNotes[0].English) | ForEach-Object { "• $_" }) -join "`n"
    if ($duckbillManifest.Changelog.Replace("`r`n", "`n").Trim() -cne $duckbillEnglish) { throw 'Installer and embedded release notes differ.' }
    $duckbillReleaseMarkdown = "# DuckbillTweak $duckbillVersion`n`n$duckbillEnglish`n"
    $duckbillCommittedNotes = [IO.File]::ReadAllText((Join-Path $duckbillRoot "docs/releases/$duckbillVersion.md"))
    if ($duckbillCommittedNotes.Replace("`r`n", "`n").Trim() -cne $duckbillReleaseMarkdown.Trim()) { throw 'GitHub release notes differ from embedded English notes.' }
    $duckbillKoreanLog = [IO.File]::ReadAllText((Join-Path $duckbillRoot 'CHANGELOG.md')).Replace("`r`n", "`n")
    foreach ($duckbillNote in $duckbillNotes[0].Korean) {
        if (!$duckbillKoreanLog.Contains("- $duckbillNote")) { throw 'Korean changelog differs from embedded notes.' }
    }

    $duckbillRequired = @(Get-RequiredPluginFiles)
    foreach ($duckbillFile in $duckbillRequired) {
        if (!(Test-Path -LiteralPath (Join-Path $duckbillBuild $duckbillFile) -PathType Leaf)) { throw "Missing package file: $duckbillFile" }
    }
    New-Item -ItemType Directory -Path $duckbillOutput | Out-Null
    $duckbillAssetName = "DuckbillTweak-$duckbillVersion.zip"
    $duckbillZipPath = Join-Path $duckbillOutput $duckbillAssetName
    $duckbillZip = [IO.Compression.ZipFile]::Open($duckbillZipPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($duckbillFile in $duckbillRequired) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($duckbillZip, (Join-Path $duckbillBuild $duckbillFile), $duckbillFile,
                [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $duckbillZip.Dispose() }
    [IO.Compression.ZipFile]::ExtractToDirectory($duckbillZipPath, $duckbillValidation)
    $duckbillExtracted = @(Get-ChildItem -LiteralPath $duckbillValidation -Recurse -File)
    if ($duckbillExtracted.Count -ne $duckbillRequired.Count) { throw 'Unexpected extracted package file count.' }
    foreach ($duckbillFile in $duckbillRequired) {
        $duckbillOriginalHash = (Get-FileHash -LiteralPath (Join-Path $duckbillBuild $duckbillFile) -Algorithm SHA256).Hash
        $duckbillExtractedHash = (Get-FileHash -LiteralPath (Join-Path $duckbillValidation $duckbillFile) -Algorithm SHA256).Hash
        if ($duckbillOriginalHash -cne $duckbillExtractedHash) { throw "Extracted file differs: $duckbillFile" }
    }
    dotnet run --project tools/GameDataProbe -c Release --no-build -- resources (Join-Path $duckbillValidation 'DuckbillTweak.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Extracted plugin resource validation failed.' }
    $duckbillBaseUrl = "https://github.com/PlatyDuckbill/DuckbillTweak/releases/download/v$duckbillVersion"
    $duckbillDownload = "$duckbillBaseUrl/$duckbillAssetName"
    $duckbillManifest | Add-Member -NotePropertyMembers @{
        DownloadLinkInstall = $duckbillDownload; DownloadLinkUpdate = $duckbillDownload
        LastUpdate = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds(); IsHide = $false; IsTestingExclusive = $false
    } -Force
    $duckbillHash = (Get-FileHash -LiteralPath $duckbillZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $duckbillOutput "$duckbillAssetName.sha256"), "$duckbillHash  $duckbillAssetName`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $duckbillOutput 'pluginmaster.json'), (ConvertTo-Json -InputObject @($duckbillManifest) -Depth 20) + "`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $duckbillOutput 'release-notes.md'), $duckbillReleaseMarkdown, [Text.UTF8Encoding]::new($false))
    $duckbillRecord = [ordered]@{ Version = $duckbillVersion; PreparedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        SourceCommit = (git rev-parse HEAD); SourceDirty = [bool](git status --porcelain)
        Icon = $duckbillIconRecord
        Zip = $duckbillAssetName; Sha256 = $duckbillHash; Files = $duckbillRequired; ExtractedDll = (Join-Path $duckbillValidation 'DuckbillTweak.dll')
        InGameVerified = $false; Published = $false }
    [IO.File]::WriteAllText((Join-Path $duckbillOutput 'validation.json'), (ConvertTo-Json $duckbillRecord -Depth 5) + "`n", [Text.UTF8Encoding]::new($false))
    Write-Output "Prepared: $duckbillZipPath"
    Write-Output "SHA256: $duckbillHash"
    Write-Output "Review DLL: $(Join-Path $duckbillValidation 'DuckbillTweak.dll')"
    Write-Output 'Nothing has been published. repo/pluginmaster.json was not modified.'
} finally { Pop-Location }
