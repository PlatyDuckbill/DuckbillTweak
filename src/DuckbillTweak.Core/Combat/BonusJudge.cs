namespace DuckbillTweak.Core.Combat;

public enum PositionalSide { None, Flank, Rear }

/// <summary>Potency-raising state that the server counts in the base, not in the bonus.</summary>
public enum PositionalModifier
{
    None,
    /// <summary>Read from a player status id.</summary>
    Status,
    /// <summary>Monk gauge CoeurlStacks &gt; 0.</summary>
    MonkCoeurlFury,
    /// <summary>Ninja gauge Kazematoi &gt; 0.</summary>
    NinjaKazematoi,
}

/// <param name="Base">Potency without combo and positional bonus.</param>
/// <param name="Combo">Potency with combo bonus only; equal to Base when the action has no combo bonus.</param>
/// <param name="Positional">Positional potency increment; 0 when <paramref name="Side"/> is None.</param>
/// <param name="ComboFrom">Previous action name from the tooltip at this level (diagnostics only).</param>
/// <param name="ComboOnly">Usable only as a combo step, so the combo bonus always applies and cannot fail.</param>
public sealed record BonusAction(uint ActionId, PositionalSide Side, int Base, int Combo, int Positional,
    string? ComboFrom, bool ComboOnly = false,
    PositionalModifier Modifier = PositionalModifier.None, uint ModifierStatus = 0, int ModifierPotency = 0)
{
    public bool HasPositional => Side != PositionalSide.None;
    /// <summary>The combo bonus exists and can be missed.</summary>
    public bool CanMissCombo => Combo != Base && !ComboOnly;
}

/// <summary>Client state read before the game applies the result (statuses/gauges are still the pre-hit values).</summary>
public readonly record struct BonusContext(IReadOnlyCollection<uint> Statuses, int MonkCoeurlStacks, int NinjaKazematoi);

/// <summary>Null members do not apply to the action (no combo bonus, or not a positional action).</summary>
public readonly record struct BonusResult(bool? ComboApplied, bool? PositionalHit);

/// <summary>
/// Server damage bonus byte = floor((applied - unbonused) * 100 / applied). Only combo and positional count as bonus;
/// potency-raising buffs/gauges raise both values (verified on 100+ logged hits, docs/PLANS.md).
/// The byte is decoded against every reachable combo/positional state, so neither is guessed from client combo state.
/// </summary>
public static class BonusJudge
{
    public static bool ModifierApplies(BonusAction action, BonusContext context) => action.Modifier switch
    {
        PositionalModifier.Status => context.Statuses.Contains(action.ModifierStatus),
        PositionalModifier.MonkCoeurlFury => context.MonkCoeurlStacks > 0,
        PositionalModifier.NinjaKazematoi => context.NinjaKazematoi > 0,
        _ => false,
    };

    public static int Byte(BonusAction action, bool combo, bool positional, bool modifier)
    {
        var shift = modifier ? action.ModifierPotency : 0;
        var unbonused = action.Base + shift;
        var applied = (combo ? action.Combo : action.Base) + (positional ? action.Positional : 0) + shift;
        return (applied - unbonused) * 100 / applied;
    }

    public static IEnumerable<(bool Combo, bool Positional)> States(BonusAction action)
    {
        bool[] combos = action.ComboOnly ? [true] : action.Combo != action.Base ? [false, true] : [false];
        bool[] positions = action.HasPositional ? [false, true] : [false];
        foreach (var combo in combos)
        foreach (var positional in positions)
            yield return (combo, positional);
    }

    /// <summary>True when every reachable state gives a different byte for both modifier states.</summary>
    public static bool StatesDistinct(BonusAction action, out string? collision)
    {
        bool[] modifiers = action.Modifier == PositionalModifier.None ? [false] : [false, true];
        foreach (var modifier in modifiers)
        {
            var seen = new Dictionary<int, (bool, bool)>();
            foreach (var state in States(action))
            {
                var value = Byte(action, state.Combo, state.Positional, modifier);
                if (!seen.TryAdd(value, state))
                {
                    collision = $"byte {value} for {seen[value]} and {state} (modifier={modifier})";
                    return false;
                }
            }
        }
        collision = null;
        return true;
    }

    /// <summary>Unique state matching the server byte, or null when none matches (a model gap; never guessed).</summary>
    public static BonusResult? Decode(BonusAction action, BonusContext context, int bonus)
    {
        var modifier = ModifierApplies(action, context);
        (bool Combo, bool Positional)? match = null;
        foreach (var state in States(action))
        {
            if (Byte(action, state.Combo, state.Positional, modifier) != bonus) continue;
            if (match is not null) return null;
            match = state;
        }
        if (match is not { } found) return null;
        return new(action.CanMissCombo ? found.Combo : null, action.HasPositional ? found.Positional : null);
    }

    public static string Describe(BonusAction action, BonusContext context) => string.Join(' ',
        States(action).Select(state => $"{(state.Combo ? 'C' : '-')}{(state.Positional ? 'P' : '-')}={Byte(action, state.Combo, state.Positional, ModifierApplies(action, context))}"));
}
