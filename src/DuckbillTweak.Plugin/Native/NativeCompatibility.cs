using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using InteropGenerator.Runtime;

namespace DuckbillTweak.Plugin.Native;

internal sealed record NativeCompatibility(bool Supported, nint ImageBase, string Reason)
{
    public static NativeCompatibility Inspect()
    {
        var dalamudAssembly = typeof(IDalamudPluginInterface).Assembly;
        var bindingAssembly = typeof(AtkUnitBase).Assembly;
        var interopAssembly = typeof(Address).Assembly;
        var dalamud = ReadIdentity(dalamudAssembly);
        var bindings = ReadIdentity(bindingAssembly);
        var interop = ReadIdentity(interopAssembly);
        if (dalamud is null || bindings is null || interop is null)
            return new(false, 0, "한국 클라이언트 UI 참조 파일을 확인할 수 없습니다.");
        var profile = NativeRuntimeProfiles.Match(dalamud, bindings, interop);
        if (profile is null)
            return new(false, 0, $"현재 한국 클라이언트 UI 참조를 지원하지 않습니다. (Dalamud={dalamudAssembly.GetName().Version}; FFCS MVID={bindings.Mvid})");
        using var process = Process.GetCurrentProcess();
        var module = process.MainModule;
        if (module is null || !Path.GetFileName(module.FileName).Equals("ffxiv_dx11.exe", StringComparison.OrdinalIgnoreCase))
            return new(false, 0, "게임 프로세스를 확인할 수 없습니다.");
        using var stream = File.OpenRead(module.FileName);
        if (Convert.ToHexString(SHA256.HashData(stream)) != NativeRuntimeProfiles.GameSha256)
            return new(false, 0, "게임 업데이트 이후 UI 호환성 확인이 필요합니다.");
        if (module.ModuleMemorySize <= 0xCD598E + 6)
            return new(false, 0, "게임 모듈 크기가 일치하지 않습니다.");
        return new(true, module.BaseAddress, $"KR 2026.10.06 / {profile.Id}");
    }

    private static NativeModuleIdentity? ReadIdentity(Assembly assembly)
    {
        try
        {
            if (string.IsNullOrEmpty(assembly.Location)) return null;
            using var stream = File.OpenRead(assembly.Location);
            return new(assembly.ManifestModule.ModuleVersionId, Convert.ToHexString(SHA256.HashData(stream)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }
}
