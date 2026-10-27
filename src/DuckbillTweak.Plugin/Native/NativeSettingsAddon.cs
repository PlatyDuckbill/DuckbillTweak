using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

internal sealed unsafe class NativeSettingsAddon(Configuration config, Action save, Action openChangelog) : NativeAddon
{
    private TextButtonNode? changelogButton;
    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        AddToggle("PvP 전장 말풍선", 60, config.PvpBubbles, value => config.PvpBubbles = value);
        AddText("채널·색상·표시 방식은 캐릭터 설정의 대화 말풍선 설정을 따릅니다.", 88);
        AddToggle("상점 품목 필터와 필요 재화", 138, config.MerchantTools, value => config.MerchantTools = value);
        AddText("상점 안의 '보유 수집품 제외'와 '필요 재화'를 사용합니다.", 166);
        AddToggle("아이템별 입찰 숫자", 216, config.LootRolls, value => config.LootRolls = value);
        AddText("입찰 목록에서 우클릭 → '입찰 숫자 보기'를 선택합니다.", 244);
        var unread = config.LastSeenChangelogVersion != ReleaseHistory.CurrentVersion;
        changelogButton = new TextButtonNode { Position = new(28, 296), Size = new(190, 28),
            String = unread ? "업데이트 내역 · NEW" : "업데이트 내역", OnClick = openChangelog };
        changelogButton.AttachNode(this);
        new TextNode { Position = new(360, 298), Size = new(190, 24), FontSize = 13,
            AlignmentType = AlignmentType.Right, String = $"v{ReleaseHistory.CurrentVersion}" }.AttachNode(this);
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        if (changelogButton is null) return;
        var label = config.LastSeenChangelogVersion != ReleaseHistory.CurrentVersion ? "업데이트 내역 · NEW" : "업데이트 내역";
        if (changelogButton.String != label) changelogButton.String = label;
    }

    protected override void OnFinalize(AtkUnitBase* addon) => changelogButton = null;

    private void AddToggle(string text, float y, bool value, Action<bool> changed)
    {
        var node = new CheckboxNode { Position = new(28, y), Size = new(430, 24), String = text, IsChecked = value };
        node.OnClick = next => { changed(next); save(); };
        node.AttachNode(this);
    }

    private void AddText(string text, float y) => new TextNode
    {
        Position = new Vector2(48, y), Size = new Vector2(490, 36), String = text,
        FontSize = 13, TextFlags = TextFlags.MultiLine | TextFlags.WordWrap,
    }.AttachNode(this);
}
