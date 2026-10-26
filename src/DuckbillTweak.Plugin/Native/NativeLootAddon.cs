using System.Numerics;
using DuckbillTweak.Core.Loot;
using DuckbillTweak.Plugin.Loot;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

internal sealed unsafe class NativeLootAddon(LootService loot, Configuration config) : NativeAddon
{
    private long entryId;
    private TextNode? itemName;
    private TextNode? status;
    private readonly List<TextNode> names = [];
    private readonly List<TextNode> rolls = [];

    public void ShowItem(uint rawItemId, string name, bool alliance, bool duplicate)
    {
        var entries = loot.Ledger.Entries.Where(e => e.RawItemId == rawItemId).ToArray();
        var entry = entries.LastOrDefault(e => e.Winner is null)
            ?? loot.Ledger.Add(rawItemId, name, alliance, DateTimeOffset.UtcNow);
        if (duplicate) loot.Ledger.MarkAmbiguous(rawItemId);
        entryId = entry.Id;
        Size = new(alliance ? 854 : 370, 362);
        Open();
    }

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        itemName = new TextNode { Position = new(28, 48), Size = new(Size.X - 56, 30), FontSize = 16, TextFlags = TextFlags.Ellipsis };
        itemName.AttachNode(this);
        status = new TextNode { Position = new(28, 80), Size = new(Size.X - 56, 38), FontSize = 12,
            TextFlags = TextFlags.MultiLine | TextFlags.WordWrap };
        status.AttachNode(this);
        for (var i = 0; i < 24; i++)
        {
            var name = new TextNode { Size = new(192, 24), FontSize = 14, TextFlags = TextFlags.Ellipsis };
            var number = new TextNode { Size = new(48, 24), FontSize = 16, AlignmentType = AlignmentType.Right };
            name.AttachNode(this); number.AttachNode(this); names.Add(name); rolls.Add(number);
        }
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        if (!config.LootRolls) { Close(); return; }
        if (itemName is null || status is null) return;
        var entry = loot.Ledger.Entries.FirstOrDefault(e => e.Id == entryId);
        if (entry is null) { Close(); return; }
        itemName.String = entry.Name;
        itemName.ItemTooltip = entry.RawItemId;
        status.Size = new(Size.X - 56, 38); itemName.Size = new(Size.X - 56, 30);
        status.String = !loot.Supported ? "현재 게임의 입찰 숫자 로그 형식을 지원하지 않습니다." :
            entry.Ambiguous ? "같은 아이템이 동시에 나와 보상별 숫자를 구분할 수 없습니다." :
            entry.Winner is { } won ? $"획득: {won.Name}   ·   — 숫자 미수신" : "입찰 종료 후 공개된 숫자만 표시합니다.   ·   — 미수신";
        var known = entry.Rolls.Keys.Concat(entry.Decided).Distinct().ToArray();
        var own = loot.PartyMembers.ToArray();
        var other = loot.OtherMembers.Concat(known.Where(k => !own.Contains(k))).Distinct().Take(16).ToArray();
        var players = entry.Alliance ? own.Take(8).Concat(Enumerable.Repeat(default(PlayerKey), Math.Max(0, 8 - own.Length)))
            .Concat(other).ToArray() : own.Concat(known).Distinct().Take(8).ToArray();
        var slots = entry.Alliance ? 24 : 8;
        for (var i = 0; i < 24; i++)
        {
            names[i].IsVisible = i < slots; rolls[i].IsVisible = i < slots;
            if (i >= slots) continue;
            var x = 28 + (i / 8) * 266;
            var y = 124 + (i % 8) * 26;
            names[i].Position = new Vector2(x, y); rolls[i].Position = new Vector2(x + (entry.Alliance ? 196 : 236), y);
            names[i].Width = entry.Alliance ? 192 : 232;
            if (i >= players.Length || string.IsNullOrEmpty(players[i].Name))
            { names[i].String = "—"; rolls[i].String = "—"; continue; }
            var key = players[i];
            names[i].String = key.Name;
            rolls[i].String = !entry.Ambiguous && entry.Rolls.TryGetValue(key, out var roll) ? roll.Value.ToString() : "—";
        }
    }

    protected override void OnFinalize(AtkUnitBase* addon) { itemName = null; status = null; names.Clear(); rolls.Clear(); }
}
