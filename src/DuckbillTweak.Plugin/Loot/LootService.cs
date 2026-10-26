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
    private readonly HashSet<uint> supportedMessages = [];
    private ulong character;
    private uint territory;
    private long lastRead;
    public LootLedger Ledger { get; } = new();
    public bool Supported => supportedMessages.Contains(1231);
    public int Unattributed { get; private set; }
    public IReadOnlyList<PlayerKey> PartyMembers { get; private set; } = [];
    public IReadOnlyList<PlayerKey> OtherMembers { get; private set; } = [];

    public LootService(IChatGui chat, IDataManager data, IClientState client, IPlayerState player,
        IPartyList party, IFramework framework, Configuration config)
    {
        this.chat = chat; this.data = data; this.client = client; this.player = player;
        this.party = party; this.framework = framework; this.config = config;
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
    }

    private void Receive(ILogMessage message)
    {
        if (!config.LootRolls || !client.IsLoggedIn || !player.IsLoaded || !Supported ||
            !supportedMessages.Contains(message.LogMessageId)) return;
        // Reject queued events from a previous login/zone until Update establishes a new session.
        if (character != player.ContentId || territory != client.TerritoryType) return;
        if (!message.TryGetIntParameter(0, out var raw) || raw <= 0) return;
        var rawId = (uint)raw;
        var baseId = rawId >= 1_000_000 ? rawId - 1_000_000 : rawId;
        if (!data.GetExcelSheet<Item>().TryGetRow(baseId, out var item)) return;
        var name = item.Name.ExtractText() + (rawId >= 1_000_000 ? " HQ" : "");
        var now = DateTimeOffset.UtcNow;
        if (message.LogMessageId == 5183)
        {
            Ledger.Add(rawId, name, party.IsAlliance, now);
            return;
        }
        var awarded = message.LogMessageId is 1232 or 1233 or 5187 or 5188;
        if (awarded && !Ledger.Entries.Any(e => e.RawItemId == rawId && e.Winner is null)) return;
        var actor = message.SourceEntity;
        if (actor is null || !actor.IsPlayer || actor.Name.IsEmpty) { Unattributed++; return; }
        var key = new PlayerKey(actor.Name.ExtractText(), actor.HomeWorldId);
        if (key.WorldId == 0)
        {
            var matches = PartyMembers.Concat(OtherMembers).Where(p => p.Name == key.Name).Distinct().ToArray();
            if (matches.Length == 1) key = matches[0];
        }
        ObservedRoll? roll = null;
        if (message.LogMessageId == 1231)
        {
            if (!message.TryGetIntParameter(2, out var value) || value is < 1 or > 99 ||
                !message.TryGetStringParameter(1, out var text)) { Unattributed++; return; }
            var kindText = text.ExtractText().Trim();
            var kind = kindText switch { "선입찰" => RollKind.Need, "입찰" => RollKind.Greed, _ => RollKind.Unknown };
            roll = new(key, kind, kindText, value);
        }
        if (!Ledger.Observe(rawId, name, party.IsAlliance, key, roll, awarded, now)) Unattributed++;
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
        Ledger.Prune(DateTimeOffset.UtcNow);
    }

    public void Clear() { Ledger.Clear(); PartyMembers = []; OtherMembers = []; Unattributed = 0; }

    public void Dispose()
    {
        chat.LogMessage -= Receive;
        framework.Update -= Update;
        Clear();
    }
}
