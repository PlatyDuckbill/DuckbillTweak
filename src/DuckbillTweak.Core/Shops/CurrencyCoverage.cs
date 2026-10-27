namespace DuckbillTweak.Core.Shops;

// Stored quantities are recorded evidence, not proof that every container was scanned.
public sealed record CurrencyCoverage(long Required, long? Usable, long RecordedStorage)
{
    public long KnownTotal => checked((Usable ?? 0) + RecordedStorage);
    public long AdditionalAtMost => Math.Max(0, Required - KnownTotal);
    public long? Retrieve => Usable is { } usable ? Math.Min(RecordedStorage, Math.Max(0, Required - usable)) : null;
}
