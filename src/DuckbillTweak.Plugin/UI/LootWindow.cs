using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DuckbillTweak.Core.Loot;
using DuckbillTweak.Plugin.Loot;

namespace DuckbillTweak.Plugin.UI;

internal sealed class LootWindow : Window
{
    private readonly LootService service;
    private readonly Configuration config;

    public LootWindow(LootService service, Configuration config) : base("입찰 보조 · DuckbillTweak###DuckbillLoot")
    {
        this.service = service; this.config = config;
        Size = new Vector2(850, 550); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(520, 300), MaximumSize = new Vector2(2200, 1600) };
    }

    public override void Draw()
    {
        if (!config.LootRolls) { ImGui.TextWrapped("설정에서 '아이템별 입찰 숫자'를 켜십시오."); return; }
        if (!service.Supported) { ImGui.TextWrapped("현재 클라이언트의 전리품 로그 형식을 지원하지 않습니다."); return; }
        ImGui.TextWrapped("실제로 수신한 주사위 숫자만 표시합니다. '선택 완료'는 입찰 종류/포기를 뜻하지 않으며, '미수신'은 아직 숫자를 모른다는 뜻입니다.");
        if (ImGui.Button("기록 비우기###ClearLoot")) service.Clear();
        if (service.Unattributed > 0)
        {
            ImGui.SameLine(); ImGui.TextDisabled($"연결하지 못한 로그 {service.Unattributed}건");
        }
        if (service.Ledger.Entries.Count == 0)
            ImGui.TextWrapped("전리품 수신 대기 중입니다. 기능을 켜기 전에 발생한 입찰은 복원하지 않습니다.");
        foreach (var entry in service.Ledger.Entries.Reverse())
        {
            var suffix = entry.Winner is { } winner ? $" · 획득: {winner.Name}" : "";
            if (!ImGui.CollapsingHeader($"{entry.Name}{suffix} · {(entry.Alliance ? "24인 연합" : "8인 이하")}###Loot:{entry.Id}", ImGuiTreeNodeFlags.DefaultOpen)) continue;
            if (entry.Ambiguous)
            {
                ImGui.TextColored(new Vector4(1, 0.75f, 0.3f, 1), "동일 아이템 동시 드롭: 로그에 보상 슬롯 정보가 없어 숫자를 구분할 수 없습니다.");
                continue;
            }
            var known = entry.Rolls.Keys.Concat(entry.Decided).Distinct().ToArray();
            var ownParty = service.PartyMembers;
            bool IsOwn(PlayerKey key) => ownParty.Contains(key);
            var local = ownParty.Concat(known.Where(IsOwn)).Distinct().ToArray();
            if (!entry.Alliance)
            {
                var people = local.Concat(known).Distinct().ToArray();
                DrawTable($"single:{entry.Id}", people, entry, false);
            }
            else if (ImGui.BeginTable($"###Alliance:{entry.Id}", 3, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.BordersInnerV))
            {
                // Public party data identifies our party. Other groups are display buckets, not guessed A/B/C affiliations.
                var others = known.Where(p => !IsOwn(p)).OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.TextUnformatted("내 파티");
                DrawTable($"own:{entry.Id}", local, entry, true);
                ImGui.TableNextColumn(); ImGui.TextUnformatted("다른/미확인 소속 · 1–8");
                DrawTable($"other1:{entry.Id}", others.Take(8).ToArray(), entry, true);
                ImGui.TableNextColumn(); ImGui.TextUnformatted("다른/미확인 소속 · 9–16");
                DrawTable($"other2:{entry.Id}", others.Skip(8).Take(8).ToArray(), entry, true);
                ImGui.EndTable();
                ImGui.TextDisabled("다른 파티는 수신한 이름순입니다. A/B/C 소속 및 전원 명단을 추정하지 않습니다.");
            }
        }
    }

    private static void DrawTable(string id, IReadOnlyList<PlayerKey> players, LootEntry entry, bool compact)
    {
        if (!ImGui.BeginTable($"###Rolls:{id}", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp)) return;
        ImGui.TableSetupColumn("플레이어"); ImGui.TableSetupColumn("주사위"); ImGui.TableHeadersRow();
        var rows = compact ? Math.Max(8, players.Count) : Math.Max(1, players.Count);
        for (var index = 0; index < rows; index++)
        {
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            if (index >= players.Count) { ImGui.TextDisabled("—"); ImGui.TableNextColumn(); ImGui.TextDisabled("정보 없음"); continue; }
            var player = players[index];
            var observed = entry.Rolls.GetValueOrDefault(player);
            var winner = entry.Winner is { } won && won.Name == player.Name &&
                won.WorldId == player.WorldId;
            ImGui.TextUnformatted((winner ? "★ " : "") + player.Name);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{player.Name} · World #{player.WorldId}");
            ImGui.TableNextColumn();
            if (observed is { } roll)
                ImGui.TextColored(roll.Kind == RollKind.Need ? new Vector4(0.4f, 0.85f, 1, 1) : new Vector4(1, 1, 1, 1),
                    $"{roll.KindText} {roll.Value}");
            else ImGui.TextDisabled(entry.Decided.Contains(player) ? "선택 완료 · 숫자 미수신" : "미수신");
        }
        ImGui.EndTable();
    }
}
