using System.Diagnostics;
using System.Security.Cryptography;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DuckbillTweak.Plugin.Native;

internal sealed record NativeCompatibility(bool Supported, nint ImageBase, string Reason)
{
    // Inspected local KR game and binding. Matching version labels alone are insufficient.
    internal const string GameSha256 = "DB9824EFAC6D5370AB64B02353E882931DDA447E160EE1A5D9E5DCBA43695728";
    private static readonly Guid BindingId = new("93c1ca2b-80f3-43ef-9d58-ab0e99bb8909");

    public static NativeCompatibility Inspect()
    {
        if (typeof(AtkUnitBase).Module.ModuleVersionId != BindingId)
            return new(false, 0, "현재 한국 클라이언트 UI 참조를 지원하지 않습니다.");
        using var process = Process.GetCurrentProcess();
        var module = process.MainModule;
        if (module is null || !Path.GetFileName(module.FileName).Equals("ffxiv_dx11.exe", StringComparison.OrdinalIgnoreCase))
            return new(false, 0, "게임 프로세스를 확인할 수 없습니다.");
        using var stream = File.OpenRead(module.FileName);
        if (Convert.ToHexString(SHA256.HashData(stream)) != GameSha256)
            return new(false, 0, "게임 업데이트 이후 UI 호환성 확인이 필요합니다.");
        if (module.ModuleMemorySize <= 0xCD598E + 6)
            return new(false, 0, "게임 모듈 크기가 일치하지 않습니다.");
        return new(true, module.BaseAddress, "KR 2026.10.06 / inspected bindings");
    }
}
