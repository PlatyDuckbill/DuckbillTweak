using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace DuckbillTweak.Plugin.Chat;

internal sealed record BubbleMessage(string Name, uint World, string Text, long Received, uint Territory);
internal sealed record BubbleView(string Id, string Name, string Text, Vector2 Position, float Alpha);

internal sealed class PvpBubbleService : IDisposable
{
    private readonly IChatGui chat;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly ICondition conditions;
    private readonly IObjectTable objects;
    private readonly IGameGui gui;
    private readonly IFramework framework;
    private readonly Configuration config;
    private readonly ConcurrentQueue<BubbleMessage> pending = new();
    private readonly Dictionary<(string, uint), BubbleMessage> active = [];
    private uint territory;
    private ulong character;
    public IReadOnlyList<BubbleView> View { get; private set; } = [];

    public PvpBubbleService(IChatGui chat, IClientState client, IPlayerState player, ICondition conditions, IObjectTable objects, IGameGui gui,
        IFramework framework, Configuration config)
    {
        this.chat = chat; this.client = client; this.player = player; this.conditions = conditions;
        this.objects = objects; this.gui = gui;
        this.framework = framework; this.config = config;
        chat.ChatMessageUnhandled += Receive;
        framework.Update += Update;
    }

    private bool InScope => config.PvpBubbles && client.IsLoggedIn && player.IsLoaded && client.IsPvPExcludingDen &&
        !conditions[ConditionFlag.BetweenAreas] && !conditions[ConditionFlag.BetweenAreas51];

    private void Receive(IChatMessage message)
    {
        if (!InScope || !Allowed(message.LogKind)) return;
        var payload = message.Sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
        if (payload is null) return; // Never guess an actor from an abbreviated display name.
        var text = message.Message.TextValue.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        text = string.Concat(text.EnumerateRunes().Take(350).Select(r => r.ToString()));
        if (pending.Count >= 64) return;
        pending.Enqueue(new(payload.PlayerName, payload.World.RowId, text, Stopwatch.GetTimestamp(), client.TerritoryType));
    }

    private bool Allowed(XivChatType kind) => kind switch
    {
        XivChatType.Say => config.SayBubbles,
        XivChatType.Party or XivChatType.CrossParty or XivChatType.PvPTeam => config.PartyBubbles,
        XivChatType.Alliance => config.AllianceBubbles,
        XivChatType.Shout or XivChatType.Yell => config.ShoutBubbles,
        _ => false,
    };

    private void Update(IFramework _)
    {
        if (!InScope || territory != client.TerritoryType || character != player.ContentId)
        {
            pending.Clear(); active.Clear(); View = []; territory = client.TerritoryType;
            character = player.IsLoaded ? player.ContentId : 0;
            return;
        }
        while (pending.TryDequeue(out var message))
        {
            if (message.Territory != territory) continue;
            active[(message.Name, message.World)] = message;
            if (active.Count > 12) active.Remove(active.MinBy(x => x.Value.Received).Key);
        }
        var duration = Math.Clamp(config.BubbleSeconds, 2, 15);
        foreach (var key in active.Where(x => Stopwatch.GetElapsedTime(x.Value.Received).TotalSeconds >= duration)
                     .Select(x => x.Key).ToArray()) active.Remove(key);
        if (active.Count == 0) { View = []; return; }
        var positions = new Dictionary<(string, uint), Vector3>();
        foreach (var actor in objects.OfType<IPlayerCharacter>())
            positions[(actor.Name.TextValue, actor.HomeWorld.RowId)] = actor.Position + new Vector3(0, 2.2f, 0);
        var result = new List<BubbleView>();
        foreach (var (key, message) in active)
        {
            if (!positions.TryGetValue(key, out var world) || !gui.WorldToScreen(world, out var screen)) continue;
            var age = Stopwatch.GetElapsedTime(message.Received).TotalSeconds;
            result.Add(new($"{key.Item1}:{key.Item2}", message.Name, message.Text, screen,
                (float)Math.Clamp((duration - age) / 0.6, 0, 1)));
        }
        View = result;
    }

    public void Dispose()
    {
        chat.ChatMessageUnhandled -= Receive;
        framework.Update -= Update;
        pending.Clear(); active.Clear(); View = [];
    }
}
