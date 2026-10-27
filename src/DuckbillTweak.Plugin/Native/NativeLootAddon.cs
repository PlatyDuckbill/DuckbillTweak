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
    private TextNode? footer;
    private TextNode? legend;
    private readonly List<ListNode<LootPlayerRow, LootPlayerRowNode>> columns = [];
    private readonly List<List<TextNode>> headings = [];
    private readonly List<HorizontalLineNode> lines = [];

    public void ShowItem(uint rawItemId, string name, bool alliance, bool duplicate)
    {
        var entry = loot.Ledger.Entries.LastOrDefault(e => e.RawItemId == rawItemId && e.IsOpen)
            ?? loot.Ledger.Add(rawItemId, name, alliance, DateTimeOffset.UtcNow);
        loot.CaptureRoster(entry);
        if (duplicate) loot.Ledger.MarkAmbiguous(rawItemId);
        entryId = entry.Id; lastRead = 0;
        Size = new(alliance ? 1164 : 420, 500);
        foreach (var column in columns) { column.OptionsList = []; column.ResetScroll(); }
        Open();
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
                Label("참여자", x + 2, 174, 142, 24, 12),
                Label("선택 / 상태", x + 150, 174, 98, 24, 12),
                Label("숫자", x + 250, 174, 40, 24, 12, AlignmentType.Right),
                Label("순위", x + 296, 174, 40, 24, 12, AlignmentType.Right),
            ]);
            var line = new HorizontalLineNode { Position = new(x, 198), Size = new(344, 2) };
            line.AttachNode(this); lines.Add(line);
            var list = new ListNode<LootPlayerRow, LootPlayerRowNode>
            { Position = new(x, 202), Size = new(360, 224), OptionsList = [], AutoResetScroll = false };
            list.NoResultsTextNode.String = "관측된 참여자 없음";
            list.AttachNode(this); columns.Add(list);
        }
        footer = Label("선입찰 우선 · 수신 숫자 기준 · 동점은 공동 순위", 28, 438, Size.X - 56, 22, 12);
        footer.TextTooltip = "선입찰은 입찰보다 우선하며, 같은 종류 안에서 숫자가 높은 순서입니다.\n수신된 숫자만 비교합니다. 동점의 실제 획득자는 획득 로그로 표시합니다.\n다른 연합원 열은 A/B/C 파티 구분을 의미하지 않습니다.";
        legend = Label("— 미수신(포기 아님) · ★ 실제 획득자", 28, 462, Size.X - 56, 22, 12);
        legend.TextTooltip = "본인의 포기만 게임 상태로 확인합니다. 다른 사람은 선택 완료만 수신하면 포기로 추측하지 않습니다.\n이탈은 관측 명단에서 연속으로 빠진 상태입니다. 접속 종료나 퇴장 사유를 구분하지 않습니다.\n행에 마우스를 올리면 상세 상태와 서버를 확인할 수 있습니다.";
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        if (!config.LootRolls) { Close(); return; }
        if (itemName is null || status is null || progress is null || icon is null || footer is null || legend is null) return;
        var entry = loot.Ledger.Entries.FirstOrDefault(e => e.Id == entryId);
        if (entry is null) { Close(); return; }
        foreach (var list in columns) if (list.IsVisible) list.Update();
        if (lastRead != 0 && Stopwatch.GetElapsedTime(lastRead).TotalMilliseconds < 250) return;
        lastRead = Stopwatch.GetTimestamp();
        itemName.String = entry.Name; itemName.ItemTooltip = entry.RawItemId;
        icon.IconId = loot.Icon(entry.RawItemId); icon.ItemTooltip = entry.RawItemId;
        itemName.Width = Size.X - 108;
        status.Width = progress.Width = footer.Width = legend.Width = Size.X - 56;
        status.String = !loot.Supported ? "입찰 결과 로그 형식을 확인할 수 없습니다." : entry.Ambiguous ? "동일 아이템 중복 · 보상별 결과 구분 불가" :
            entry.Winner is { } winner ? $"획득: {winner.Name}" : entry.IsOpen ? "입찰 진행 중 · 공개된 정보만 표시" : "입찰 목록에서 종료 · 획득 결과 미수신";
        status.TextTooltip = entry.Winner is { } won ? loot.PlayerTooltip(won) : "숫자는 공개된 결과 로그에서만 읽습니다. 항목 종료만으로 포기나 시간 초과를 추정하지 않습니다.";
        var known = entry.Members.Select(m => m.Player).Concat(entry.Rolls.Keys).Concat(entry.Decided).Concat(entry.Choices.Keys)
            .Concat(entry.Winner is { } w ? new[] { w } : []).Distinct().ToArray();
        var own = entry.Members.Where(m => m.OwnParty).Select(m => m.Player).ToHashSet();
        var ranks = LootLedger.Ranks(entry);
        progress.String = entry.Ambiguous ? "이 항목에는 선택·숫자·순위를 연결하지 않습니다." :
            $"관측 {known.Length}명 · 선택 확인 {entry.Decided.Count}명 · 숫자 수신 {entry.Rolls.Count}명 · 이탈 {entry.Left.Count}명";
        progress.TextTooltip = progress.String.ExtractText() + "\n선택 완료 로그만으로는 선입찰·입찰·포기를 구분할 수 없습니다.";
        var others = known.Where(k => !own.Contains(k)).ToArray();
        var groups = entry.Alliance ? new[] { known.Where(own.Contains).ToArray(), others.Take(8).ToArray(), others.Skip(8).ToArray() } : new[] { known };
        var dirty = false;
        for (var c = 0; c < columns.Count; c++)
        {
            var show = c < groups.Length;
            dirty |= columns[c].IsVisible != show;
            columns[c].IsVisible = show; lines[c].IsVisible = show;
            foreach (var heading in headings[c]) heading.IsVisible = show;
            if (!show) continue;
            var rows = groups[c].Select(key => BuildRow(entry, key, ranks)).ToList();
            if (columns[c].OptionsList.SequenceEqual(rows)) continue;
            columns[c].OptionsList = rows; dirty = true;
        }
        if (dirty) addon->UpdateCollisionNodeList(false);
    }

    private LootPlayerRow BuildRow(LootEntry entry, PlayerKey key, IReadOnlyDictionary<PlayerKey, int> ranks)
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
        else state = entry.IsOpen ? "선택 미확인" : "미수신";
        detail += "\n" + state + (number == "—" ? " · 숫자 미수신" : $" · {number}");
        if (left)
        {
            detail += "\n관측 명단에서 이탈 · 이전 선택/숫자는 보존합니다.";
            state = state is "선택 미확인" or "미수신" ? "이탈·미확인" : state + "·이탈";
        }
        if (won) { detail += "\n획득 로그로 확인된 실제 획득자"; state += "·획득"; }
        int? place = ranks.TryGetValue(key, out var rank) ? rank : null;
        return new(key.Name + (key == loot.Self ? " (나)" : ""), detail, state, number, place, place.HasValue && ranks.Values.Count(r => r == place) > 1, won, left);
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
    { itemName = status = progress = footer = legend = null; icon = null; columns.Clear(); headings.Clear(); lines.Clear(); lastRead = 0; }
}
