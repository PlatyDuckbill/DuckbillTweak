using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using DuckbillTweak.Plugin.Native;
using Lumina.Excel.Sheets;

namespace DuckbillTweak.Plugin.Emotes;

internal enum EmoteOwnership { Unknown, Owned, Missing }
internal sealed record EmoteStatusEntry(ulong ActorId, ushort EmoteId, uint IconId, string Name, EmoteOwnership Ownership);
internal sealed record EmoteStatusView(EmoteStatusEntry? Self, EmoteStatusEntry? Target, long Tick)
{
    public static readonly EmoteStatusView Empty = new(null, null, 0);
}

/// <summary>Framework-only managed observations. Unlock state and execution permission are independent.</summary>
internal sealed class EmoteStatusObserver : IDisposable
{
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly IObjectTable objects;
    private readonly ITargetManager targets;
    private readonly IUnlockState unlocks;
    private readonly Configuration config;
    private readonly ICondition conditions;
    private readonly Dictionary<uint, Emote> emotes;
    private long nextRead;
    private ulong character;
    private ulong selfId, targetId;
    private uint territory;
    private bool disposed;
    public EmoteStatusView View { get; private set; } = EmoteStatusView.Empty;

    public EmoteStatusObserver(IFramework framework, IClientState client, IPlayerState player, IObjectTable objects,
        ITargetManager targets, IUnlockState unlocks, IDataManager data, ICondition conditions, Configuration config)
    {
        this.framework = framework; this.client = client; this.player = player; this.objects = objects;
        this.targets = targets; this.unlocks = unlocks; this.config = config;
        this.conditions = conditions;
        emotes = data.GetExcelSheet<Emote>().Where(e => !e.Name.IsEmpty && e.Icon != 0).ToDictionary(e => e.RowId);
        framework.Update += Update;
    }

    private void Update(IFramework _)
    {
        if (disposed) return;
        var now = Environment.TickCount64;
        if (!config.EmoteStatusIcons || !client.IsLoggedIn || !player.IsLoaded || player.ContentId == 0 ||
            conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51] || objects.LocalPlayer is not { } self)
        { View = EmoteStatusView.Empty; character = selfId = targetId = 0; territory = 0; nextRead = 0; return; }
        if (character != player.ContentId || territory != client.TerritoryType)
        { View = EmoteStatusView.Empty; character = player.ContentId; territory = client.TerritoryType; nextRead = 0; }
        var selected = (targets.SoftTarget ?? targets.Target) as ICharacter;
        var selectedId = selected?.GameObjectId ?? 0;
        if (selfId != self.GameObjectId || targetId != selectedId)
        { selfId = self.GameObjectId; targetId = selectedId; View = EmoteStatusView.Empty; nextRead = 0; }
        if (now < nextRead) return;
        nextRead = now + 100;
        var own = Read(self);
        var target = selectedId == self.GameObjectId ? own : selected is not null ? Read(selected) : null;
        View = new(own, target, now);
    }

    private EmoteStatusEntry? Read(ICharacter actor)
    {
        if (!actor.IsValid()) return null;
        var id = NativeEmoteReader.ReadLoop(actor);
        if (id == 0 || !emotes.TryGetValue(id, out var emote)) return null;
        var ownership = EmoteOwnership.Unknown;
        // Managed service failures cannot prove that an emote is missing. No native read is protected by this catch.
        try
        {
            // Internal NPC-only animations without a player command are not proven obtainable by UnlockLink=0.
            if (emote.TextCommand.ValueNullable is { } command && !command.Command.IsEmpty)
                ownership = unlocks.IsEmoteUnlocked(emote) ? EmoteOwnership.Owned : EmoteOwnership.Missing;
        }
        catch (Exception) { }
        return new(actor.GameObjectId, id, emote.Icon, emote.Name.ExtractText(), ownership);
    }

    public void Dispose()
    { disposed = true; framework.Update -= Update; View = EmoteStatusView.Empty; character = selfId = targetId = 0; territory = 0; }
}
