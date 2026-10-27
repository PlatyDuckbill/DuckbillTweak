using System.Diagnostics;
using Dalamud.Game.Chat;
using Dalamud.Plugin.Services;
using DuckbillTweak.Core.Loot;
using Lumina.Excel.Sheets;

namespace DuckbillTweak.Plugin.Loot;

internal sealed class LootService : IDisposable
{
    private readonly IChatGui chat;
    private readonly IDataManager data;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly IPartyList party;
    private readonly IFramework framework;
    private readonly Configuration config;
    private readonly IPluginLog log;
    private readonly HashSet<uint> supportedMessages = [];
    private readonly HashSet<uint> receivedMessages = [];
    private readonly HashSet<string> rejectionReasons = [];
    private readonly HashSet<RollKind> observedKinds = [];
    private bool recordedRoll;
    private ulong character;
    private uint territory;
    private long lastRead;
    private readonly NativeLootObserver native = new();
    public bool NativeObservationEnabled { set => native.Enabled = value; }
    public PlayerKey Self => new(player.CharacterName, player.HomeWorld.RowId);
    public LootLedger Ledger { get; } = new();
    public bool Supported => supportedMessages.Contains(1231);
    public int Unattributed { get; private set; }
    public IReadOnlyList<PlayerKey> PartyMembers { get; private set; } = [];
    public IReadOnlyList<PlayerKey> OtherMembers { get; private set; } = [];
    public uint Icon(uint rawId) => data.GetExcelSheet<Item>().TryGetRow(rawId % 1_000_000, out var item) ? item.Icon : 0u;
    public string PlayerTooltip(PlayerKey key) => data.GetExcelSheet<World>().TryGetRow(key.WorldId, out var world)
        ? $"{key.Name}@{world.Name.ExtractText()}" : key.Name;
    public void CaptureRoster(LootEntry entry) => Ledger.CaptureRoster(entry, PartyMembers, OtherMembers, false);

    public LootService(IChatGui chat, IDataManager data, IClientState client, IPlayerState player,
        IPartyList party, IFramework framework, Configuration config, IPluginLog log)
    {
        this.chat = chat; this.data = data; this.client = client; this.player = player;
        this.party = party; this.framework = framework; this.config = config;
        this.log = log;
        var sheet = data.GetExcelSheet<LogMessage>();
        // Contracts checked against the installed client's macros, rather than parsing translated chat text.
        foreach (var id in new uint[] { 1231, 1232, 1233, 5180, 5183, 5187, 5188 })
        {
            if (!sheet.TryGetRow(id, out var row)) continue;
            var macro = row.Text.ToMacroString();
            if (!macro.Contains("sheet(Item,lnum1,0)")) continue;
            if (id == 1231 && (!macro.Contains("string(lstr2)") || !macro.Contains("num(lnum3)"))) continue;
            supportedMessages.Add(id);
        }
        chat.LogMessage += Receive;
        framework.Update += Update;
        log.Information("Loot log contract: RollsSupported={Supported}; MessageTypes={Count}", Supported, supportedMessages.Count);
    }

    private void Receive(ILogMessage message)
    {
        if (!config.LootRolls || !client.IsLoggedIn || !player.IsLoaded || !Supported ||
            !supportedMessages.Contains(message.LogMessageId)) return;
        // Reject queued events from a previous login/zone until Update establishes a new session.
        if (character != player.ContentId || territory != client.TerritoryType) return;
        if (receivedMessages.Add(message.LogMessageId))
            log.Information("Loot log received: MessageType={Type}", message.LogMessageId);
        if (!message.TryGetIntParameter(0, out var raw) || raw <= 0) return;
        var rawId = (uint)raw;
        var baseId = rawId >= 1_000_000 ? rawId - 1_000_000 : rawId;
        if (!data.GetExcelSheet<Item>().TryGetRow(baseId, out var item)) return;
        var name = item.Name.ExtractText() + (rawId >= 1_000_000 ? " HQ" : "");
        var now = DateTimeOffset.UtcNow;
        if (message.LogMessageId == 5183)
        {
            CaptureRoster(Ledger.Add(rawId, name, party.IsAlliance, now));
            return;
        }
        var awarded = message.LogMessageId is 1232 or 1233 or 5187 or 5188;
        if (awarded && !Ledger.Entries.Any(e => e.RawItemId == rawId)) return;
        var actor = message.SourceEntity;
        if (actor is null || !actor.IsPlayer || actor.Name.IsEmpty) { Reject("missing player identity"); return; }
        var key = new PlayerKey(actor.Name.ExtractText(), actor.HomeWorldId);
        if (key.WorldId == 0)
        {
            var matches = PartyMembers.Concat(OtherMembers).Concat(Ledger.Entries.Where(e => e.RawItemId == rawId)
                    .SelectMany(e => e.Members).Select(m => m.Player))
                .Where(p => p.WorldId != 0 && p.Name == key.Name).Distinct().ToArray();
            if (matches.Length == 1) key = matches[0];
        }
        ObservedRoll? roll = null;
        if (message.LogMessageId == 1231)
        {
            if (!message.TryGetIntParameter(2, out var value) || value is < 1 or > 99 ||
                !message.TryGetStringParameter(1, out var text)) { Reject("invalid roll parameters"); return; }
            var kindText = text.ExtractText().Trim();
            var kind = kindText.ToUpperInvariant() switch { "선입찰" or "NEED" => RollKind.Need, "입찰" or "GREED" => RollKind.Greed, _ => RollKind.Unknown };
            roll = new(key, kind, kindText, value);
            if (observedKinds.Add(kind)) log.Information("Loot roll kind decoded: {Kind}", kind);
            if (kind == RollKind.Unknown && rejectionReasons.Add("unknown roll kind"))
                log.Warning("Loot roll type not recognized; keeping the value without assigning Need/Greed or a rank.");
        }
        if (!Ledger.Observe(rawId, name, party.IsAlliance, key, roll, awarded, now)) Reject("ambiguous or completed item");
        else if (roll is not null && !recordedRoll)
        {
            recordedRoll = true;
            log.Information("Loot roll recording active for this session");
        }
        foreach (var entry in Ledger.Entries.Where(e => e.RawItemId == rawId && e.IsOpen)) CaptureRoster(entry);
    }

    private void Reject(string reason)
    {
        Unattributed++;
        if (rejectionReasons.Add(reason)) log.Warning("Loot log could not be attributed: {Reason}", reason);
    }

    private void Update(IFramework _)
    {
        if (!config.LootRolls || !client.IsLoggedIn || !player.IsLoaded)
        {
            Clear(); character = 0; return;
        }
        if (character != player.ContentId || territory != client.TerritoryType)
        {
            Clear(); character = player.ContentId; territory = client.TerritoryType;
        }
        if (lastRead != 0 && Stopwatch.GetElapsedTime(lastRead).TotalSeconds < 1) return;
        lastRead = Stopwatch.GetTimestamp();
        PartyMembers = party.Select(m => new PlayerKey(m.Name.TextValue, m.World.RowId)).ToArray();
        if (PartyMembers.Count == 0 && !string.IsNullOrWhiteSpace(Self.Name)) PartyMembers = [Self];
        var others = new List<PlayerKey>();
        if (party.IsAlliance)
        {
            // Public Dalamud alliance accessor checks its own bounds. No GroupManager offsets here.
            for (var index = 0; index < 20; index++)
            {
                var address = party.GetAllianceMemberAddress(index);
                if (address == 0) continue;
                var member = party.CreateAllianceMemberReference(address);
                if (member is null || string.IsNullOrWhiteSpace(member.Name.TextValue)) continue;
                var key = new PlayerKey(member.Name.TextValue, member.World.RowId);
                if (!PartyMembers.Contains(key) && !others.Contains(key)) others.Add(key);
            }
        }
        OtherMembers = others;
        foreach (var entry in Ledger.Entries.Where(e => e.IsOpen)) Ledger.CaptureRoster(entry, PartyMembers, OtherMembers, true);
        native.Update(Ledger, Self, DateTimeOffset.UtcNow);
        Ledger.Prune(DateTimeOffset.UtcNow);
    }

    public void Clear()
    {
        Ledger.Clear(); PartyMembers = []; OtherMembers = []; Unattributed = 0;
        receivedMessages.Clear(); rejectionReasons.Clear(); observedKinds.Clear(); recordedRoll = false;
        native.Clear();
    }

    public void Dispose()
    {
        chat.LogMessage -= Receive;
        framework.Update -= Update;
        Clear();
    }
}
