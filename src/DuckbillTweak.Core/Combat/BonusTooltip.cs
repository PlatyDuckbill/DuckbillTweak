using System.Text.RegularExpressions;

namespace DuckbillTweak.Core.Combat;

/// <summary>
/// Builds a <see cref="BonusAction"/> for the current job/level from the action's KR tooltip macro.
/// The tooltip must fit the verified model exactly (combined lines equal the sums of their parts, at most one known
/// modifier, a distinct server byte for every combo/positional state). Anything else is rejected with a reason and
/// that action is left native for this job/level instead of being guessed.
/// </summary>
public static class BonusTooltip
{
    /// <summary>Player actions whose KR tooltip states a flank/rear potency (GameDataProbe positional-actions).</summary>
    public static readonly IReadOnlyList<uint> PositionalCandidates =
        [56, 66, 88, 2255, 2258, 3554, 3556, 3563, 7481, 7482, 24382, 24383, 25772,
         34610, 34611, 34612, 34613, 34621, 34622, 36947, 36970, 36971];

    /// <summary>Non-PvP job actions whose KR tooltip has a combo potency line (GameDataProbe combo-actions).</summary>
    public static readonly IReadOnlyList<uint> ComboCandidates =
        [15, 21, 37, 42, 45, 78, 84, 87, 88, 2242, 2255, 2868, 2873, 3539, 3554, 3556, 3563, 3623, 3632, 7397, 7412, 7413,
         7478, 7479, 7480, 7481, 7482, 7484, 7485, 7512, 7516, 7528, 7529, 15990, 15994, 16139, 16145, 16149, 16457, 16462,
         16468, 16477, 16488, 24374, 24375, 24377, 25771, 25772, 36954, 36955, 44883, 44885, 45961, 45962];

    public static readonly IReadOnlyList<uint> Candidates = PositionalCandidates.Concat(ComboCandidates).Distinct().ToArray();

    private sealed record ModifierRule(string Condition, PositionalModifier Kind, uint Status = 0);

    // "<condition> 시 위력" lines: modifier states listed as separate potencies.
    private static readonly Dictionary<uint, ModifierRule> LabelModifiers = new()
    {
        [56] = new("호랑이 공력", PositionalModifier.MonkCoeurlFury),
        [36947] = new("호랑이 공력", PositionalModifier.MonkCoeurlFury),
        [24382] = new("효수형 효과 향상", PositionalModifier.Status, 2588),
        [36970] = new("효수형 효과 향상", PositionalModifier.Status, 2588),
        [24383] = new("교수형 효과 향상", PositionalModifier.Status, 2589),
        [36971] = new("교수형 효과 향상", PositionalModifier.Status, 2589),
    };

    // "<condition>이/가 부여된 경우/상태일 때에는 모든 위력이 N(씩) 증가" sentences.
    private static readonly Dictionary<uint, ModifierRule> SentenceModifiers = new()
    {
        [2255] = new("바람칼", PositionalModifier.NinjaKazematoi),
        [34610] = new("예리한 송곳니[측격]", PositionalModifier.Status, 3645),
        [34611] = new("예리한 송곳니[측열]", PositionalModifier.Status, 3646),
        [34612] = new("예리한 송곳니[후격]", PositionalModifier.Status, 3647),
        [34613] = new("예리한 송곳니[후열]", PositionalModifier.Status, 3648),
    };

    private sealed record HiddenBase(int Level, int TooltipPotency, int Base);

    // Combo-only actions whose tooltip shows the combo potency as "위력" and hides the non-combo base.
    // Measured in game (Viper finishers, Lv100: tooltip 340, base 160). Other levels stay unsupported until measured.
    private static readonly Dictionary<uint, HiddenBase[]> HiddenBases = new()
    {
        [34610] = [new(100, 340, 160)], [34611] = [new(100, 340, 160)],
        [34612] = [new(100, 340, 160)], [34613] = [new(100, 340, 160)],
    };

    private static readonly Regex PotencyLine = new(@"^(?<label>[^:\n]*위력)\s*:\s*(?<value>.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex ComboLine = new(@"^콤보 조건\s*:\s*(?<name>.+)$", RegexOptions.CultureInvariant);
    private static readonly Regex AllPotencySentence = new(@"(?<condition>\S.*?)(?:이|가) 부여된 (?:경우|상태일 때에는) 모든 위력이 (?<value>\d+)씩? 증가",
        RegexOptions.CultureInvariant);
    private static readonly Regex AnyPotencyIncrease = new(@"위력이 \d+씩? 증가", RegexOptions.CultureInvariant);
    private static readonly Regex OtherSkillIncrease = new(@"의 위력이 \d+씩? 증가", RegexOptions.CultureInvariant);

    public static BonusAction? Build(uint actionId, string macro, int job, int level, out string? reason)
    {
        string text;
        try
        {
            text = TooltipMacro.Evaluate(macro, number => number switch
            {
                68 => job, 72 => level,
                _ => throw new FormatException($"Unsupported global number gnum{number}."),
            });
        }
        catch (FormatException exception) { reason = exception.Message; return null; }
        return Parse(actionId, text, level, out reason);
    }

    public static BonusAction? Parse(uint actionId, string text, int level, out string? reason)
    {
        var lines = text.Split('\n').Select(line => line.Trim()).ToArray();
        var potencies = new Dictionary<string, int>(StringComparer.Ordinal);
        string? comboFrom = null;
        foreach (var line in lines)
        {
            var combo = ComboLine.Match(line);
            if (combo.Success) { comboFrom = combo.Groups["name"].Value.Trim(); continue; }
            var potency = PotencyLine.Match(line);
            if (!potency.Success) continue;
            var label = potency.Groups["label"].Value.Trim();
            if (label == "지속 피해 위력") continue; // DoT tick potency, not part of the hit
            if (!int.TryParse(potency.Groups["value"].Value.Trim(), out var value) || value <= 0)
                return Fail($"Potency line '{label}' has no number.", out reason);
            if (!potencies.TryAdd(label, value)) return Fail($"Duplicate potency line '{label}'.", out reason);
        }

        if (!potencies.TryGetValue("위력", out var tooltipBase)) return Fail("No base potency line.", out reason);
        var used = new HashSet<string>(StringComparer.Ordinal) { "위력" };

        // Positional (optional).
        var sides = potencies.Keys.Where(label => label.Contains("측면공격") || label.Contains("후방공격"))
            .Select(label => label.Contains("측면공격") ? PositionalSide.Flank : PositionalSide.Rear).Distinct().ToArray();
        if (sides.Length > 1) return Fail("Positional side is mixed.", out reason);
        var side = sides.Length == 1 ? sides[0] : PositionalSide.None;
        var sideWord = side == PositionalSide.Flank ? "측면공격" : "후방공격";
        var positional = 0;
        if (side != PositionalSide.None)
        {
            if (!potencies.TryGetValue($"{sideWord} 시 위력", out var positionalPotency))
                return Fail("No positional-only potency line.", out reason);
            positional = positionalPotency - tooltipBase;
            if (positional <= 0) return Fail("Positional potency is not above base.", out reason);
            used.Add($"{sideWord} 시 위력");
        }

        // Combo (optional).
        var comboPotency = tooltipBase;
        if (potencies.TryGetValue("콤보 시 위력", out var comboValue))
        {
            if (comboFrom is null) return Fail("Combo potency without a combo condition.", out reason);
            if (comboValue <= tooltipBase) return Fail("Combo potency is not above base.", out reason);
            used.Add("콤보 시 위력");
            if (side != PositionalSide.None)
            {
                if (!potencies.TryGetValue($"콤보 및 {sideWord} 시 위력", out var both) || both != comboValue + positional)
                    return Fail("Combo + positional potency is not additive.", out reason);
                used.Add($"콤보 및 {sideWord} 시 위력");
            }
            comboPotency = comboValue;
        }
        else if (comboFrom is not null) return Fail("Combo condition without combo potency.", out reason);

        // Label modifier (optional, known per action).
        var modifier = PositionalModifier.None; uint modifierStatus = 0; var modifierPotency = 0;
        var extra = potencies.Keys.Where(label => !used.Contains(label)).ToArray();
        if (extra.Length > 0)
        {
            if (!LabelModifiers.TryGetValue(actionId, out var rule)) return Fail($"Unknown potency line '{extra[0]}'.", out reason);
            if (comboFrom is not null) return Fail("Combo and modifier lines together are not verified.", out reason);
            if (!potencies.TryGetValue($"{rule.Condition} 시 위력", out var raised) || raised <= tooltipBase)
                return Fail($"Unexpected modifier lines: {string.Join(", ", extra)}.", out reason);
            var expectedLines = side == PositionalSide.None ? 1 : 2;
            if (extra.Length != expectedLines) return Fail($"Unexpected modifier lines: {string.Join(", ", extra)}.", out reason);
            if (side != PositionalSide.None
                && (!potencies.TryGetValue($"{rule.Condition} 및 {sideWord} 시 위력", out var raisedBoth) || raisedBoth != raised + positional))
                return Fail("Modifier potency lines are not additive.", out reason);
            (modifier, modifierStatus, modifierPotency) = (rule.Kind, rule.Status, raised - tooltipBase);
        }

        // Any "all potency +N" sentence must be this action's known modifier; other-skill notes ("X의 위력이") are ignored.
        var sentences = AllPotencySentence.Matches(text);
        var foreignIncreases = AnyPotencyIncrease.Matches(text).Count - OtherSkillIncrease.Matches(text).Count - sentences.Count;
        if (foreignIncreases != 0) return Fail("Unrecognized potency increase sentence.", out reason);
        if (sentences.Count > 0)
        {
            if (sentences.Count != 1 || modifier != PositionalModifier.None) return Fail("Multiple potency modifiers.", out reason);
            var condition = sentences[0].Groups["condition"].Value.Trim();
            if (!SentenceModifiers.TryGetValue(actionId, out var rule) || !condition.EndsWith(rule.Condition, StringComparison.Ordinal))
                return Fail($"Unknown modifier sentence '{condition}'.", out reason);
            (modifier, modifierStatus, modifierPotency) = (rule.Kind, rule.Status, int.Parse(sentences[0].Groups["value"].Value));
        }

        var unbonused = tooltipBase;
        var comboOnly = false;
        if (HiddenBases.TryGetValue(actionId, out var hidden))
        {
            if (comboFrom is not null) return Fail("Hidden-base action unexpectedly lists a combo.", out reason);
            var measured = hidden.FirstOrDefault(entry => entry.Level == level);
            if (measured is null) return Fail("Hidden base not measured at this level.", out reason);
            if (measured.TooltipPotency != tooltipBase) return Fail("Tooltip potency differs from the measured one.", out reason);
            unbonused = measured.Base;
            comboPotency = tooltipBase;
            comboOnly = true;
        }

        if (side == PositionalSide.None && comboPotency == unbonused) return Fail("Neither combo nor positional bonus.", out reason);
        var action = new BonusAction(actionId, side, unbonused, comboPotency, positional, comboFrom, comboOnly,
            modifier, modifierStatus, modifierPotency);
        if (!BonusJudge.StatesDistinct(action, out var collision)) return Fail($"States collide: {collision}.", out reason);
        reason = null;
        return action;
    }

    private static BonusAction? Fail(string message, out string? reason)
    {
        reason = message;
        return null;
    }
}
