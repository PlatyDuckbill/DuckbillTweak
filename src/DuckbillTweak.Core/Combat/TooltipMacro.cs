using System.Text;
using System.Text.RegularExpressions;

namespace DuckbillTweak.Core.Combat;

/// <summary>
/// Minimal evaluator for KR action tooltip macro strings (Lumina ToMacroString form).
/// Supports only &lt;if([gnumN op V],then,else)&gt;, &lt;br&gt; and color tags. Anything else throws, so an
/// unexpected tooltip shape disables that action instead of producing a guessed value.
/// The same code runs offline (GameDataProbe) and in game, so offline coverage matches runtime behavior.
/// </summary>
public static class TooltipMacro
{
    private static readonly Regex Condition = new(@"^\[gnum(\d+)(==|!=|>=|<=|>|<)(\d+)\]$", RegexOptions.CultureInvariant);

    public static string Evaluate(string macro, Func<int, int> globalNumber)
    {
        var index = 0;
        var result = EvaluateUntil(macro, ref index, globalNumber, argument: false);
        if (index != macro.Length) throw new FormatException($"Unparsed macro tail at {index}.");
        return result;
    }

    private static string EvaluateUntil(string text, ref int index, Func<int, int> globalNumber, bool argument)
    {
        var output = new StringBuilder();
        while (index < text.Length)
        {
            var c = text[index];
            if (argument && (c == ',' || c == ')')) break;
            if (c == '\\' && index + 1 < text.Length) { output.Append(text[index + 1]); index += 2; continue; }
            if (c != '<') { output.Append(c); index++; continue; }
            if (string.CompareOrdinal(text, index, "<if(", 0, 4) == 0)
            {
                index += 4;
                var condition = EvaluateUntil(text, ref index, globalNumber, argument: true);
                Expect(text, index, ","); index++;
                var then = EvaluateUntil(text, ref index, globalNumber, argument: true);
                Expect(text, index, ","); index++;
                var otherwise = EvaluateUntil(text, ref index, globalNumber, argument: true);
                Expect(text, index, ")>"); index += 2;
                output.Append(Holds(condition, globalNumber) ? then : otherwise);
                continue;
            }
            var close = text.IndexOf('>', index);
            if (close < 0) throw new FormatException("Unclosed tag.");
            var tag = text[(index + 1)..close];
            if (tag == "br") output.Append('\n');
            else if (!tag.StartsWith("colortype(", StringComparison.Ordinal) && !tag.StartsWith("edgecolortype(", StringComparison.Ordinal))
                throw new FormatException($"Unsupported tag <{tag}>.");
            index = close + 1;
        }
        return output.ToString();
    }

    private static void Expect(string text, int index, string token)
    {
        if (string.CompareOrdinal(text, index, token, 0, token.Length) != 0) throw new FormatException($"Expected '{token}' at {index}.");
    }

    private static bool Holds(string condition, Func<int, int> globalNumber)
    {
        var match = Condition.Match(condition);
        if (!match.Success) throw new FormatException($"Unsupported condition {condition}.");
        var left = globalNumber(int.Parse(match.Groups[1].Value));
        var right = int.Parse(match.Groups[3].Value);
        return match.Groups[2].Value switch
        {
            "==" => left == right, "!=" => left != right, ">=" => left >= right,
            "<=" => left <= right, ">" => left > right, _ => left < right,
        };
    }
}
