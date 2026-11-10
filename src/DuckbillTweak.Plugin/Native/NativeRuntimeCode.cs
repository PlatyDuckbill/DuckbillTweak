using Dalamud;
using Dalamud.Plugin.Services;

namespace DuckbillTweak.Plugin.Native;

/// <summary>Running-game code reader: public Dalamud ISigScanner for .text matches, SafeMemory for reads.</summary>
internal sealed class NativeRuntimeCode(ISigScanner scanner) : INativeCodeReader
{
    public IReadOnlyList<nint> ScanAll(string pattern) => scanner.ScanAllText(pattern).ToArray();

    public bool TryRead(nint address, int length, out byte[] bytes) => SafeMemory.ReadBytes(address, length, out bytes);

    /// <summary>Resolves every code contract once at load. A failure only disables its own feature.</summary>
    public static (NativeCodeResult<NativePatch[]> Pvp, NativeCodeResult<ExchangeCurrencyLayout> Exchange) Inspect(ISigScanner scanner)
    {
        var reader = new NativeRuntimeCode(scanner);
        return (Guard(() => NativeCode.ResolvePvpBubbles(reader)), Guard(() => NativeCode.ResolveExchangeCurrency(reader)));
    }

    private static NativeCodeResult<T> Guard<T>(Func<NativeCodeResult<T>> resolve) where T : class
    {
        try { return resolve(); }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or KeyNotFoundException)
        { return NativeCodeResult<T>.Fail($"scan failed ({ex.GetType().Name})"); }
    }
}
