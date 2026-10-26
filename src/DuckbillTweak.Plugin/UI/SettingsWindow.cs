using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DuckbillTweak.Plugin.UI;

internal sealed class SettingsWindow : Window
{
    private readonly Configuration config;
    private readonly Action save;
    private readonly Action showShop;
    private readonly Action showLoot;

    public SettingsWindow(Configuration config, Action save, Action showShop, Action showLoot) : base("DuckbillTweak###DuckbillTweakSettings")
    {
        this.config = config; this.save = save; this.showShop = showShop; this.showLoot = showLoot;
        Size = new Vector2(650, 620); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(550, 420), MaximumSize = new Vector2(1400, 1200) };
    }

    public override void Draw()
    {
        ImGui.TextWrapped("필요한 편의 기능만 켜서 사용합니다. /dbtw · /dbtw shop · /dbtw loot");
        ImGui.Spacing();
        var enabled = config.PvpBubbles;
        if (ImGui.Checkbox("PvP 전장 채팅 말풍선###PvpBubbles", ref enabled)) { config.PvpBubbles = enabled; save(); }
        ImGui.TextWrapped("PvP 지역(늑대우리 제외)에서 수신한 채팅을 근처 발언자의 머리 위에 표시합니다. 서버에서 전달하지 않은 채팅과 주변에 없는 플레이어는 표시할 수 없습니다.");
        ImGui.Indent();
        Toggle("일반 대화###Say", config.SayBubbles, x => config.SayBubbles = x);
        ImGui.SameLine();
        Toggle("파티/팀###Party", config.PartyBubbles, x => config.PartyBubbles = x);
        ImGui.SameLine();
        Toggle("연합###Alliance", config.AllianceBubbles, x => config.AllianceBubbles = x);
        ImGui.SameLine();
        Toggle("외치기###Shout", config.ShoutBubbles, x => config.ShoutBubbles = x);
        var seconds = config.BubbleSeconds;
        if (ImGui.SliderFloat("표시 시간 (초)###Seconds", ref seconds, 2, 15, "%.1f")) { config.BubbleSeconds = seconds; save(); }
        var width = config.BubbleWidth;
        if (ImGui.SliderFloat("말풍선 너비###Width", ref width, 180, 500, "%.0f")) { config.BubbleWidth = width; save(); }
        ImGui.Unindent();
        ImGui.Separator();
        Toggle("상인 목록과 필요 재화###Merchants", config.MerchantTools, x => config.MerchantTools = x);
        ImGui.TextWrapped("등록한 수집품/소지품 제외, 종류별 필터와 재화 계산을 별도 보조 화면에서 제공합니다.");
        Toggle("상점 이용 시 보조 화면 열기###AutoShop", config.AutoOpenShop, x => config.AutoOpenShop = x);
        if (ImGui.Button("상인 보조 화면###OpenShop")) showShop();
        ImGui.Separator();
        Toggle("아이템별 입찰 숫자###Loot", config.LootRolls, x => config.LootRolls = x);
        ImGui.TextWrapped("전리품 로그에서 실제 수신한 숫자를 모읍니다. 8인은 목록, 24인 연합은 3열로 표시합니다. 숫자 미수신과 포기를 구분하며 같은 아이템의 동시 드롭은 구분 불가로 표시합니다.");
        Toggle("전리품 추가/입찰 시 보조 화면 열기###AutoLoot", config.AutoOpenLoot, x => config.AutoOpenLoot = x);
        if (ImGui.Button("입찰 보조 화면###OpenLoot")) showLoot();
        ImGui.Separator();
        ImGui.TextDisabled("개발판 0.1.0 · 한국 클라이언트 인게임 확인 대기");
    }

    private void Toggle(string text, bool value, Action<bool> update)
    {
        if (!ImGui.Checkbox(text, ref value)) return;
        update(value); save();
    }
}
