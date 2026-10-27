#Requires -Version 7.0
# Offline inspection only; never loads a plugin or enables a runtime profile.
# Metadata descriptions adapted from DuckbillFisher, see docs/NATIVE-UI.md.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaselinePath,
    [Parameter(Mandatory)][string]$CandidatePath,
    [Parameter(Mandatory)][string]$PluginDirectory,
    [Parameter(Mandatory)][string]$ReportPath
)

$ErrorActionPreference = 'Stop'
$baseline = (Resolve-Path -LiteralPath $BaselinePath).Path
$candidate = (Resolve-Path -LiteralPath $CandidatePath).Path
Add-Type -Path (Join-Path $baseline 'Mono.Cecil.dll')

function Get-Attributes($value) {
    @($value.CustomAttributes | ForEach-Object {
        $_.AttributeType.FullName + ':' + [Convert]::ToHexString($_.GetBlob())
    })
}

function Get-TypeDescription($type) {
    [ordered]@{
        Name = $type.FullName; Attributes = [string]$type.Attributes
        Base = [string]$type.BaseType; Size = $type.ClassSize; Packing = $type.PackingSize
        AttributesData = @(Get-Attributes $type)
        Interfaces = @($type.Interfaces | ForEach-Object { $_.InterfaceType.FullName })
        Fields = @($type.Fields | ForEach-Object {
            [ordered]@{
                Name = $_.Name; Type = $_.FieldType.FullName; Offset = $_.Offset
                Attributes = [string]$_.Attributes; AttributesData = @(Get-Attributes $_)
                Constant = if ($_.HasConstant) { [string]$_.Constant } else { $null }
                Data = [Convert]::ToHexString($_.InitialValue)
            }
        })
        Methods = @($type.Methods | ForEach-Object {
            [ordered]@{
                Signature = $_.FullName; Attributes = [string]$_.Attributes
                ImplAttributes = [string]$_.ImplAttributes; AttributesData = @(Get-Attributes $_)
                ReturnAttributes = @(Get-Attributes $_.MethodReturnType)
                Parameters = @($_.Parameters | ForEach-Object {
                    [ordered]@{
                        Name = $_.Name; Type = $_.ParameterType.FullName; Attributes = [string]$_.Attributes
                        AttributesData = @(Get-Attributes $_)
                        Constant = if ($_.HasConstant) { [string]$_.Constant } else { $null }
                    }
                })
                Instructions = if ($_.HasBody) { @($_.Body.Instructions | ForEach-Object ToString) } else { @() }
                Locals = if ($_.HasBody) { @($_.Body.Variables | ForEach-Object { $_.VariableType.FullName }) } else { @() }
                Handlers = if ($_.HasBody) { @($_.Body.ExceptionHandlers | ForEach-Object {
                    [ordered]@{
                        Kind = [string]$_.HandlerType; Catch = [string]$_.CatchType
                        TryStart = [string]$_.TryStart; TryEnd = [string]$_.TryEnd
                        HandlerStart = [string]$_.HandlerStart; HandlerEnd = [string]$_.HandlerEnd
                        FilterStart = [string]$_.FilterStart
                    }
                }) } else { @() }
            }
        })
        Nested = @($type.NestedTypes | ForEach-Object { Get-TypeDescription $_ })
    }
}

# Use the actual built plugin AND pinned UI library, including unused library types:
# checking fishing-only types cannot establish Tweak's UI compatibility.
$groups = [ordered]@{
    'Dalamud' = [Collections.Generic.HashSet[string]]::new()
    'FFXIVClientStructs' = [Collections.Generic.HashSet[string]]::new()
    'InteropGenerator.Runtime' = [Collections.Generic.HashSet[string]]::new()
    'Lumina' = [Collections.Generic.HashSet[string]]::new()
    'Lumina.Excel' = [Collections.Generic.HashSet[string]]::new()
}
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory($baseline)
$readerParameters = [Mono.Cecil.ReaderParameters]::new()
$readerParameters.AssemblyResolver = $resolver
$requiredMembers = @()
$artifacts = @()
foreach ($file in @('DuckbillTweak.dll', 'KamiToolKit.dll')) {
    $path = Join-Path $PluginDirectory $file
    $artifact = [Mono.Cecil.ModuleDefinition]::ReadModule($path, $readerParameters)
    try {
        $artifacts += [ordered]@{
            Module = $file; Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
            References = @($artifact.AssemblyReferences | Where-Object { $groups.Contains($_.Name) } |
                ForEach-Object { [ordered]@{ Name = $_.Name; Version = $_.Version.ToString() } })
        }
        foreach ($reference in $artifact.GetTypeReferences()) {
            $scope = $reference.Scope.Name
            if ($groups.Contains($scope)) {
                $root = $reference
                while ($null -ne $root.DeclaringType) { $root = $root.DeclaringType }
                [void]$groups[$scope].Add($root.FullName)
            }
        }
        foreach ($reference in $artifact.GetMemberReferences()) {
            $scope = $reference.DeclaringType.Scope.Name
            if (!$groups.Contains($scope)) { continue }
            $definition = $reference.Resolve()
            if ($null -eq $definition) { throw "Unresolved baseline member: $reference" }
            $requiredMembers += [ordered]@{ Artifact = $file; Module = "$scope.dll"; Member = $definition.FullName }
        }
    } finally { $artifact.Dispose() }
}

function Add-MemberDescriptions($type, $destination) {
    foreach ($field in $type.Fields) { $destination["$($field.Type) $($type.Name)::$($field.Name)"] = $field }
    foreach ($method in $type.Methods) { $destination[$method.Signature] = $method }
    foreach ($nested in $type.Nested) { Add-MemberDescriptions $nested $destination }
}

$modules = @()
$comparisons = @()
$members = @()
$missingMembers = @()
$incompatibleReferences = @()
foreach ($name in $groups.Keys) {
    $dll = "$name.dll"
    $old = [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $baseline $dll))
    $new = [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $candidate $dll))
    try {
        $modules += [ordered]@{
            Module = $dll
            Baseline = [ordered]@{
                Version = $old.Assembly.Name.Version.ToString(); Mvid = $old.Mvid.ToString()
                Sha256 = (Get-FileHash -LiteralPath (Join-Path $baseline $dll) -Algorithm SHA256).Hash
            }
            Candidate = [ordered]@{
                Version = $new.Assembly.Name.Version.ToString(); Mvid = $new.Mvid.ToString()
                Sha256 = (Get-FileHash -LiteralPath (Join-Path $candidate $dll) -Algorithm SHA256).Hash
            }
        }
        foreach ($artifact in $artifacts) {
            foreach ($reference in $artifact.References | Where-Object Name -eq $name) {
                if ([version]$reference.Version -gt $new.Assembly.Name.Version) {
                    $incompatibleReferences += "$($artifact.Module): $name requires $($reference.Version), candidate is $($new.Assembly.Name.Version)"
                }
            }
        }
        $oldMembers = @{}; $newMembers = @{}
        foreach ($typeName in ($groups[$name] | Sort-Object)) {
            $oldType = $old.GetType($typeName)
            $newType = $new.GetType($typeName)
            if ($null -eq $oldType -or $null -eq $newType) { throw "Missing inspection type: $dll / $typeName" }
            $before = Get-TypeDescription $oldType
            $after = Get-TypeDescription $newType
            Add-MemberDescriptions $before $oldMembers
            Add-MemberDescriptions $after $newMembers
            $comparisons += [ordered]@{
                Module = $dll; Type = $typeName
                Equal = (($before | ConvertTo-Json -Depth 60 -Compress) -ceq ($after | ConvertTo-Json -Depth 60 -Compress))
                Baseline = $before; Candidate = $after
            }
        }
        foreach ($member in $requiredMembers | Where-Object Module -eq $dll | Sort-Object { $_.Artifact },{ $_.Member } -Unique) {
            if (!$oldMembers.ContainsKey($member.Member) -or !$newMembers.ContainsKey($member.Member)) {
                $missingMembers += "$($member.Artifact): $($member.Member)"
            } else {
                $members += [ordered]@{
                    Artifact = $member.Artifact; Module = $dll; Member = $member.Member
                    Equal = (($oldMembers[$member.Member] | ConvertTo-Json -Depth 60 -Compress) -ceq
                        ($newMembers[$member.Member] | ConvertTo-Json -Depth 60 -Compress))
                }
            }
        }
    } finally { $old.Dispose(); $new.Dispose() }
}
$resolver.Dispose()
$report = [ordered]@{
    Format = 'tweak-native-metadata/v1'; Artifacts = $artifacts; Modules = $modules; Comparisons = $comparisons
    ReferencedMembers = $members; MissingMembers = $missingMembers; IncompatibleReferences = $incompatibleReferences
}
$report | ConvertTo-Json -Depth 65 | Set-Content -LiteralPath $ReportPath -Encoding utf8
$different = @($comparisons | Where-Object { -not $_.Equal })
Write-Output "Inspected=$($comparisons.Count); Equal=$($comparisons.Count - $different.Count); Different=$($different.Count)"
$different | ForEach-Object { Write-Output "Different: $($_.Module) / $($_.Type)" }
Write-Output "ReferencedMembers=$($members.Count); Missing=$($missingMembers.Count); HigherAssemblyReferences=$($incompatibleReferences.Count)"
$members | Where-Object { !$_.Equal } | ForEach-Object { Write-Output "Changed referenced member: $($_.Artifact) / $($_.Member)" }
$missingMembers | ForEach-Object { Write-Output "Missing: $_" }
$incompatibleReferences | ForEach-Object { Write-Output "Higher reference: $_" }
if ($missingMembers.Count -gt 0) { throw 'Candidate is missing compiled artifact members.' }
if ($incompatibleReferences.Count -gt 0) { throw 'Assembly reference version differences require loader review. No runtime profile has been changed.' }
if ($different.Count -gt 0) { throw 'Metadata differences require review. No runtime profile has been changed.' }
