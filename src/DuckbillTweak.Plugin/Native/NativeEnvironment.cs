using System.Reflection;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using InteropGenerator.Runtime;

namespace DuckbillTweak.Plugin.Native;

/// <summary>
/// Report-only description of the running game build and the installed Dalamud/ClientStructs distribution.
/// Nothing here enables or disables a feature: loading is Dalamud's API-level decision, and each native feature checks
/// its own contract (UI shape, row/item identity, resolved functions; NativeExecutable for executable-bound code).
/// Approach adapted from DuckbillFisher 590e506 (same author); see docs/NATIVE-UI.md.
/// </summary>
internal sealed record NativeEnvironment(string Diagnostic)
{
    public static NativeEnvironment Inspect()
    {
        var game = "unknown";
        try
        {
            // Managed file read once during load; never in Draw or a native read.
            if (Environment.ProcessPath is { } path && Path.GetDirectoryName(path) is { } directory)
                game = File.ReadAllText(Path.Combine(directory, "ffxivgame.ver")).Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { game = $"unknown({ex.GetType().Name})"; }

        static string Describe(string name, Assembly assembly) =>
            $"{name}={assembly.GetName().Version?.ToString() ?? "unknown"}/{assembly.ManifestModule.ModuleVersionId}";
        var modules = string.Join("; ",
            Describe("Dalamud", typeof(IDalamudPluginInterface).Assembly),
            Describe("FFXIVClientStructs", typeof(AtkUnitBase).Assembly),
            Describe("InteropGenerator.Runtime", typeof(Address).Assembly));
        return new($"NativeEnvironment: Game={game}; {modules}; ModuleGate=None");
    }
}
