using System.Diagnostics;
using DuckbillTweak.Core.Loot;
using DuckbillTweak.Plugin.Loot;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

internal sealed unsafe class NativeLootAddon(LootService loot, Configuration config) : NativeAddon
{
    private long entryId;
    private long lastRead;
    private IconImageNode? icon;
    private TextNode? itemName;
    private TextNode? status;
    private TextNode? progress;
    private readonly List<ListNode<LootPlayerRow, LootPlayerRowNode>> columns = [];
    private readonly List<List<TextNode>> headings = [];
    private readonly List<HorizontalLineNode> lines = [];

    public bool ShowItem(uint rawItemId, bool duplicate)
    {
        var entry = loot.FindCurrentEntry(rawItemId, duplicate);
        if (entry is null) return false;
        if (duplicate) loot.Ledger.MarkAmbiguous(entry);
        entryId = entry.Id; lastRead = 0;
        Size = new(entry.Alliance ? 1164 : 420, entry.Alliance ? 446 : 420);
        if (columns.Count > 0) SetWindowSize(Size);
        foreach (var column in columns) { column.OptionsList = []; column.ResetScroll(); }
        Open();
        return true;
    }

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        icon = new IconImageNode { Position = new(28, 48), Size = new(40, 40), FitTexture = true };
        icon.AttachNode(this);
        itemName = Label("", 80, 48, Size.X - 108, 36, 16);
        status = Label("", 28, 94, Size.X - 56, 24, 14);
        progress = Label("", 28, 120, Size.X - 56, 24, 12);
        for (var i = 0; i < 3; i++)
        {
            var x = 28 + i * 374;
            headings.Add([
                Label(i == 0 ? "내 파티" : $"다른 연합원 · {i}", x, 148, 344, 24, 14),
                Label("참여자", x + 2, 174, 180, 24, 12),
                Label("선택 / 상태", x + 190, 174, 104, 24, 12),
                Label("주사위", x + 300, 174, 44, 24, 12, AlignmentType.Right),
            ]);
            var line = new HorizontalLineNode { Position = new(x, 198), Size = new(344, 2) };
            line.AttachNode(this); lines.Add(line);
            var list = new ListNode<LootPlayerRow, LootPlayerRowNode>
            { Position = new(x, 202), Size = new(360, 224), OptionsList = [], AutoResetScroll = false };
            list.NoResultsTextNode.String = "참여자 없음";
            list.AttachNode(this); columns.Add(list);
        }
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        if (!config.LootRolls) { Close(); return; }
        if (itemName is null || status is null || progress is null || icon is null) return;
        var entry = loot.Ledger.Entries.FirstOrDefault(e => e.Id == entryId);
        if (entry is null) { Close(); return; }
        foreach (var list in columns) if (list.IsVisible) list.Update();
        if (lastRead != 0 && Stopwatch.GetElapsedTime(lastRead).TotalMilliseconds < 250) return;
        lastRead = Stopwatch.GetTimestamp();
        itemName.String = entry.Name; itemName.ItemTooltip = entry.RawItemId;
        icon.IconId = loot.Icon(entry.RawItemId); icon.ItemTooltip = entry.RawItemId;
        itemName.Width = Size.X - 108;
        status.Width = progress.Width = Size.X - 56;
        status.String = !loot.Supported ? "입찰 결과 로그 형식을 확인할 수 없습니다." : entry.Ambiguous ? "동일 아이템 중복 · 보상별 결과 구분 불가" :
            entry.Winner is { } winner ? $"획득: {winner.Name}" : entry.IsOpen ? "입찰 진행 중" : "입찰 종료";
        status.TextTooltip = entry.Winner is { } won ? loot.PlayerTooltip(won) : "숫자는 결과가 공개된 뒤 표시됩니다.";
        var known = entry.Members.Select(m => m.Player).Concat(entry.Rolls.Keys).Concat(entry.Decided).Concat(entry.Choices.Keys)
            .Concat(entry.Winner is { } w ? new[] { w } : []).Distinct().ToArray();
        var own = entry.Members.Where(m => m.OwnParty).Select(m => m.Player).ToHashSet();
        var need = known.Count(key => !entry.Left.Contains(key) && LootLedger.ChoiceFor(entry, key) == RollKind.Need);
        var greed = known.Count(key => !entry.Left.Contains(key) && LootLedger.ChoiceFor(entry, key) == RollKind.Greed);
        var inactive = known.Count(key => entry.Left.Contains(key) || LootLedger.ChoiceFor(entry, key) == RollKind.Pass);
        progress.String = entry.Ambiguous ? "선입찰자 — / 입찰자 — / 포기·이탈자 —" :
            $"선입찰자 {need} / 입찰자 {greed} / 포기·이탈자 {inactive}";
        progress.TextTooltip = "확인된 선택만 집계합니다. 미확인은 포기로 세지 않습니다.";
        var others = LootLedger.OrderPlayers(entry, known.Where(k => !own.Contains(k))).ToArray();
        var groups = entry.Alliance ? new[] { LootLedger.OrderPlayers(entry, known.Where(own.Contains)).ToArray(), others.Take(8).ToArray(), others.Skip(8).ToArray() }
            : new[] { LootLedger.OrderPlayers(entry, known).ToArray() };
        var listTop = entry.Alliance ? 202 : 176;
        var listHeight = Math.Clamp(groups.Max(group => group.Length), 1, 8) * LootPlayerRowNode.ItemHeight;
        var height = listTop + listHeight + 20;
        var dirty = false;
        if (Size.Y != height) { SetWindowSize(new(Size.X, height)); dirty = true; }
        for (var c = 0; c < columns.Count; c++)
        {
            var show = c < groups.Length;
            dirty |= columns[c].IsVisible != show;
            columns[c].IsVisible = show; lines[c].IsVisible = show;
            for (var h = 0; h < headings[c].Count; h++)
            {
                headings[c][h].IsVisible = show && (h != 0 || entry.Alliance);
                if (h != 0) headings[c][h].Y = listTop - 28;
            }
            if (!show) continue;
            lines[c].Y = listTop - 4;
            columns[c].Y = listTop; columns[c].Height = listHeight;
            var rows = groups[c].Select(key => BuildRow(entry, key)).ToList();
            if (columns[c].OptionsList.SequenceEqual(rows)) continue;
            columns[c].OptionsList = rows; dirty = true;
        }
        if (dirty) addon->UpdateCollisionNodeList(false);
    }

    private LootPlayerRow BuildRow(LootEntry entry, PlayerKey key)
    {
        var won = !entry.Ambiguous && entry.Winner == key;
        var left = entry.Left.Contains(key);
        var detail = loot.PlayerTooltip(key);
        string state; string number = "—";
        if (entry.Ambiguous) state = "구분 불가";
        else if (entry.Rolls.TryGetValue(key, out var roll))
        {
            state = ChoiceText(roll.Kind);
            number = roll.Value.ToString();
            if (roll.Kind == RollKind.Unknown) detail += "\n수신된 입찰 종류: " + roll.KindText;
        }
        else if (entry.Choices.TryGetValue(key, out var choice)) state = ChoiceText(choice);
        else if (entry.Decided.Contains(key)) state = entry.IsOpen ? "선택 완료" : "결과 미수신";
        else state = "미확인";
        detail += "\n" + state + (number == "—" ? " · 숫자 미수신" : $" · {number}");
        if (left)
        {
            detail += "\n파티 이탈";
            state = state == "미확인" ? "이탈" : state + "·이탈";
        }
        if (won) detail += "\n획득자";
        return new(key.Name + (key == loot.Self ? " (나)" : ""), detail, state, number, won, left);
    }

    private static string ChoiceText(RollKind kind) => kind switch
    { RollKind.Need => "선입찰", RollKind.Greed => "입찰", RollKind.Pass => "포기", _ => "종류 미확인" };

    private TextNode Label(string text, float x, float y, float width, float height, byte size, AlignmentType alignment = AlignmentType.Left)
    {
        var node = new TextNode { Position = new(x, y), Size = new(width, height), String = text, FontSize = size,
            LineSpacing = 18, AlignmentType = alignment, TextFlags = TextFlags.Ellipsis };
        node.AttachNode(this); return node;
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    { itemName = status = progress = null; icon = null; columns.Clear(); headings.Clear(); lines.Clear(); lastRead = 0; }
}
