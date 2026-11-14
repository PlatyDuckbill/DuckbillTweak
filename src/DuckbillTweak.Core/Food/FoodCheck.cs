namespace DuckbillTweak.Core.Food;

public enum FoodDuty { None, Extreme, Savage, Ultimate }

/// <summary>Managed food observation. Null is unknown; zero means a readable status list had no food.</summary>
public sealed record FoodMember(ulong ContentId, string Name, double? Seconds)
{
    public string Display => Seconds is not { } seconds ? "미확인" : seconds <= 0 ? "없음" :
        $"{(int)Math.Ceiling(seconds) / 60}:{(int)Math.Ceiling(seconds) % 60:00}";
    public bool IsLow(int minutes) => Seconds is { } seconds && seconds < minutes * 60;
}

public static class FoodCheck
{
    // KR ContentFinderCondition / ContentUICategory inspected with GameDataProbe food-data.
    // Do not classify normal raids, Unreal (which shares an Extreme category), or Chaotic as Savage.
    public static FoodDuty Classify(uint type, uint category, string name) => type switch
    {
        28 when name.StartsWith("절 ", StringComparison.Ordinal) => FoodDuty.Ultimate,
        4 when category is 19 or 20 or 22 or 33 or 39 or 47 && !name.StartsWith("환 ", StringComparison.Ordinal) => FoodDuty.Extreme,
        5 when category is 12 or 13 or 17 or 35 or 41 or 49 => FoodDuty.Savage,
        _ => FoodDuty.None,
    };
}
