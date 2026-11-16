namespace DuckbillTweak.Plugin;

internal enum FeatureState
{
    Off, LoginRequired, Loading, Ready, Observing, PvpOnly, ExcludedArea, NoActions,
    ContractUnavailable, InitializationFailed, ObservationFailed, ReadyCheckFailed,
    PvpPatchFailed, WaitingFood, WaitingEmote, Syncing, CombatBlocked, ShopPartial, Stale
}
internal sealed record FeatureStatus(FeatureState State, string Diagnostic = "");
internal sealed record FeatureStatuses(FeatureStatus Food, FeatureStatus Positional, FeatureStatus Pvp,
    FeatureStatus Merchant, FeatureStatus Loot, FeatureStatus Emote, long Tick);
