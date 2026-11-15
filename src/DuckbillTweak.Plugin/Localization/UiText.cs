using System.Globalization;
using System.Resources;

namespace DuckbillTweak.Plugin.UI;

internal enum TextKey
{
    SettingsTitle,
    SettingsSummary,
    UpdatesTitle,
    UpdatesOpen,
    UpdatesNew,
    Version,
    Published,
    Development,
    Offline,
    Other,
    Confirm,
    Cancel,
    Defaults,
    Details,
    SaveFailed,
    Appearance,
    Theme,
    Graphite,
    Paper,
    Language,
    Combat,
    Shops,
    Social,
    Food,
    FoodDescription,
    FoodMinutes,
    Extreme,
    Savage,
    Ultimate,
    FoodHint,
    FoodScope,
    Position,
    PositionDescription,
    PositionHint,
    Hit,
    Miss,
    Combo,
    Enabled,
    Icon,
    Color,
    ChangeColor,
    Preset,
    PresetDefault,
    PresetVivid,
    Custom,
    Pvp,
    PvpDescription,
    Merchant,
    MerchantDescription,
    Loot,
    LootDescription,
    Emote,
    EmoteDescription,
    Timing,
    TimingOwn,
    TimingMiddle,
    TimingOthers,
    TimingEarly,
    TimingHint,
    Unavailable,
    ExchangeUnavailable,
    FoodUnavailable,
    IconChoice,
}
internal static class UiText
{
    private static readonly ResourceManager Resources = new("DuckbillTweak.Plugin.Localization.Strings", typeof(UiText).Assembly);
    public static bool English { get; set; }
    public static string Get(TextKey key, params object[] args)
    {
        var culture = CultureInfo.GetCultureInfo(English ? "en" : "ko");
        var text = Resources.GetString(key.ToString(), culture) ?? key.ToString();
        return args.Length == 0 ? text : string.Format(culture, text, args);
    }
}
