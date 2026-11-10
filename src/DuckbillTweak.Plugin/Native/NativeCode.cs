namespace DuckbillTweak.Plugin.Native;

/// <summary>Reads code from the running game (Dalamud ISigScanner + SafeMemory) or from an executable file (GameDataProbe).</summary>
internal interface INativeCodeReader
{
    /// <summary>Every match of an IDA-style pattern ("?? " wildcards) in .text, as addresses usable with <see cref="TryRead"/>.</summary>
    IReadOnlyList<nint> ScanAll(string pattern);
    bool TryRead(nint address, int length, out byte[] bytes);
}

internal sealed record NativePatch(nint Address, byte[] Original, byte[] Replacement);

/// <summary>Addon field offsets of ShopExchangeCurrency, read from the instructions that use them.</summary>
internal sealed record ExchangeCurrencyLayout(int MapOffset, int CountOffset, int RecordOffset, int TabOffset)
{
    public const int Capacity = 60;
    public const int RecordStride = 0x40;
}

/// <summary>A per-feature code contract: either the resolved value or the reason it is unavailable.</summary>
internal sealed record NativeCodeResult<T>(T? Value, string Reason) where T : class
{
    public bool Available => Value is not null;
    public static NativeCodeResult<T> Fail(string reason) => new(null, reason);
}

/// <summary>
/// KR instruction signatures for the only features that touch executable-specific code or addon fields that
/// ClientStructs does not describe. Replaces the former exact game SHA-256 gate (2026-11-10, same approach as
/// DuckbillFisher's per-feature runtime contracts): each feature resolves its own code in the running game and only
/// that feature turns off when a pattern is missing, ambiguous or inconsistent. Patterns were derived from the KR
/// 2026.10.06 analysis and re-verified unique on KR 2026.11.03. Evidence: docs/NATIVE-UI.md.
/// The same resolver runs offline in GameDataProbe native-contract, so the plugin and the probe cannot diverge.
/// </summary>
internal static class NativeCode
{
    // PvP chat bubble suppression gates (KR 2026.10.06 RVA CD5682/CD568A/CD571E; 2026.11.03 CD53C2/CD53CA/CD545E).
    //   test byte [g],4 / je / cmp byte [g],40h / jae / mov sil,1 (patch 1) / jmp / xor sil,sil / call state (patch 2) / or al,sil
    internal const string PvpGatePattern =
        "F6 05 ?? ?? ?? ?? 04 74 0E 80 3D ?? ?? ?? ?? 40 73 05 40 B6 01 EB 03 40 32 F6 E8 ?? ?? ?? ?? 40 0A C6 48 8D 0D";
    //   switch on content type byte [rax+3] → mov sil,1 (patch 3) / mov rax,[g]
    internal const string PvpContextPattern =
        "0F B6 48 03 83 E9 02 74 14 83 E9 01 74 0F 83 E9 03 74 0A 83 E9 01 74 05 83 F9 02 75 03 40 B6 01 48 8B 05";
    //   Callee of patch 2: bool query (instance → virtual → test al,3). Replacing the call with xor eax,eax returns false.
    internal const string PvpStateCalleePattern =
        "48 83 EC 28 48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 85 C0 74 17 48 8B 10 48 8B C8 FF 92 ?? ?? ?? ?? A8 03 74 07 B0 01 48 83 C4 28 C3 32 C0 48 83 C4 28 C3";
    private const int PvpGateSet = 18, PvpStateCall = 26, PvpContextSet = 29, PvpMaxSpan = 0x100;

    // ShopExchangeCurrency (KR 2026.10.06 RVA 12C1B59/12C2D5F/12C2BAB/12C26CB; 2026.11.03 12C1909/12C2B0F/12C295B/12C247B).
    //   refresh: mov [rsi+rdi*4+map],eax (compact row map producer)
    internal const string ExchangeMapWritePattern = "8B BC 24 ?? ?? ?? ?? 8B C8 89 84 BE ?? ?? ?? ?? FF C7 89 BC 24";
    //   row render: mov ebp,[rsi+rbx*4+map] / shl rbp,6 / add rbp,rsi (consumer, 0x40 record stride)
    internal const string ExchangeMapReadPattern =
        "0F B6 D0 80 E2 01 8B D8 E8 ?? ?? ?? ?? 8B AC 9E ?? ?? ?? ?? 45 33 C9 48 8B 47 08 45 33 C0 48 C1 E5 06 48 03 EE";
    //   tooltip: add ebp,[rbx+count] / shl rcx,6 / mov eax,[rcx+rbx+record] (record item ID)
    internal const string ExchangeRecordPattern =
        "85 C0 74 06 03 AB ?? ?? ?? ?? 66 44 89 7C 24 ?? 48 63 CD 48 C1 E1 06 8B 84 19 ?? ?? ?? ??";
    //   tab selection: movzx eax,byte [rbx+tab] / ... / mov [rbx+tab],dl
    internal const string ExchangeTabPattern = "0F B6 83 ?? ?? ?? ?? 33 D2 FF C0 41 F7 F1 88 93 ?? ?? ?? ??";

    public static NativeCodeResult<NativePatch[]> ResolvePvpBubbles(INativeCodeReader reader)
    {
        if (Single(reader, PvpGatePattern, "gate", out var gate) is { } gateError) return NativeCodeResult<NativePatch[]>.Fail(gateError);
        if (Single(reader, PvpContextPattern, "context", out var context) is { } contextError) return NativeCodeResult<NativePatch[]>.Fail(contextError);
        var set1 = gate + PvpGateSet; var call = gate + PvpStateCall; var set3 = context + PvpContextSet;
        if (set3 <= call || set3 - set1 > PvpMaxSpan) return NativeCodeResult<NativePatch[]>.Fail("gate and context are not in one function");
        if (!reader.TryRead(call, 5, out var callBytes) || callBytes[0] != 0xE8) return NativeCodeResult<NativePatch[]>.Fail("state call unreadable");
        var callee = call + 5 + BitConverter.ToInt32(callBytes, 1);
        var calleePattern = Parse(PvpStateCalleePattern);
        if (!reader.TryRead(callee, calleePattern.Length, out var calleeBytes) || !Match(calleeBytes, calleePattern))
            return NativeCodeResult<NativePatch[]>.Fail("state call target differs from the inspected query");
        return new([
            new(set1, [0x40, 0xB6, 0x01], [0x40, 0x30, 0xF6]),
            new(call, callBytes, [0x31, 0xC0, 0x90, 0x90, 0x90]),
            new(set3, [0x40, 0xB6, 0x01], [0x90, 0x90, 0x90]),
        ], $"sites={set1:X},{call:X},{set3:X}");
    }

    public static NativeCodeResult<ExchangeCurrencyLayout> ResolveExchangeCurrency(INativeCodeReader reader)
    {
        if (Single(reader, ExchangeMapWritePattern, "map write", out var producer) is { } e1) return NativeCodeResult<ExchangeCurrencyLayout>.Fail(e1);
        if (Single(reader, ExchangeMapReadPattern, "map read", out var renderer) is { } e2) return NativeCodeResult<ExchangeCurrencyLayout>.Fail(e2);
        if (Single(reader, ExchangeRecordPattern, "record", out var tooltip) is { } e3) return NativeCodeResult<ExchangeCurrencyLayout>.Fail(e3);
        if (Single(reader, ExchangeTabPattern, "tab", out var selector) is { } e4) return NativeCodeResult<ExchangeCurrencyLayout>.Fail(e4);
        int? Disp(nint address) => reader.TryRead(address, 4, out var b) ? BitConverter.ToInt32(b, 0) : null;
        var mapWrite = Disp(producer + 12); var mapRead = Disp(renderer + 16);
        var count = Disp(tooltip + 6); var record = Disp(tooltip + 26);
        var tabRead = Disp(selector + 3); var tabWrite = Disp(selector + 16);
        if (mapWrite is not { } map || count is not { } c || record is not { } r || tabRead is not { } tab)
            return NativeCodeResult<ExchangeCurrencyLayout>.Fail("field displacement unreadable");
        if (mapRead != map) return NativeCodeResult<ExchangeCurrencyLayout>.Fail("map producer and consumer disagree");
        if (tabWrite != tab) return NativeCodeResult<ExchangeCurrencyLayout>.Fail("tab read and write disagree");
        var layout = new ExchangeCurrencyLayout(map, c, r, tab);
        // Fields must sit in a plausible addon range and must not overlap one another.
        (int Start, int Length)[] fields = [(map, ExchangeCurrencyLayout.Capacity * 4), (c, 4),
            (r, ExchangeCurrencyLayout.Capacity * ExchangeCurrencyLayout.RecordStride), (tab, 1)];
        if (fields.Any(f => f.Start is < 0x100 or > 0x10000))
            return NativeCodeResult<ExchangeCurrencyLayout>.Fail("field offset outside addon range");
        for (var i = 0; i < fields.Length; i++)
            for (var j = i + 1; j < fields.Length; j++)
                if (fields[i].Start < fields[j].Start + fields[j].Length && fields[j].Start < fields[i].Start + fields[i].Length)
                    return NativeCodeResult<ExchangeCurrencyLayout>.Fail("field offsets overlap");
        return new(layout, $"map={map:X}; count={c:X}; record={r:X}; tab={tab:X}");
    }

    private static string? Single(INativeCodeReader reader, string pattern, string name, out nint address)
    {
        var matches = reader.ScanAll(pattern);
        address = matches.Count == 1 ? matches[0] : 0;
        return matches.Count switch { 1 => null, 0 => $"{name} pattern not found", var n => $"{name} pattern ambiguous ({n})" };
    }

    internal static byte?[] Parse(string pattern) =>
        pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => x == "??" ? (byte?)null : Convert.ToByte(x, 16)).ToArray();

    internal static bool Match(ReadOnlySpan<byte> bytes, byte?[] pattern)
    {
        if (bytes.Length < pattern.Length) return false;
        for (var i = 0; i < pattern.Length; i++)
            if (pattern[i] is { } value && bytes[i] != value) return false;
        return true;
    }
}
