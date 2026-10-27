namespace DuckbillTweak.Core.Shops;

/// <summary>Resolves sheet currency references. Missing mappings stay unknown, never Item #2 (a crystal).</summary>
public sealed class SpecialCurrencyResolver(IReadOnlyDictionary<uint, uint> tokens, IReadOnlyDictionary<uint, uint> tomestones)
{
    public uint? Resolve(byte costType, uint rawId, Func<uint, uint?>? specialItem = null)
    {
        if (rawId == 0) return null;
        // Current KR rows carry a type on every cost. UseCurrencyType is a legacy shop-wide field.
        return costType switch
        {
            0 or 1 => rawId,
            2 => Tome(rawId),
            3 => specialItem?.Invoke(rawId) ?? Token(rawId),
            _ => null,
        };
    }

    private uint? Token(uint id) => tokens.TryGetValue(id, out var item) ? item : null;
    private uint? Tome(uint id) => tomestones.TryGetValue(id, out var item) ? item : null;
}
