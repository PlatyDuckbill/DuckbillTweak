using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

// Per-feature code contracts resolved in the running game (NativeCode / ClientStructs). A missing one only changes its own text.
internal sealed unsafe class NativeSettingsAddon(Configuration config, Action save, Action openChangelog, Action openColors,
    bool pvpAvailable, bool exchangeCurrencyAvailable, bool positionalAvailable) : NativeAddon
{
    private const string Unavailable = "현재 게임 버전에서 필요한 게임 코드를 찾지 못해 동작하지 않습니다. 플러그인 업데이트를 기다려 주세요.";
    private TextButtonNode? changelogButton;

    // Emote follow timing: how much earlier than our own screen to start (other players see us after our latency).
    private sealed record FollowTiming(string Label, int Ms);
    private static readonly FollowTiming[] FollowTimings =
    [
        new("박자: 내 화면 기준", 0), new("박자: 중간 (40ms)", 40),
        new("박자: 다른 사람 화면 기준 (80ms)", 80), new("박자: 더 앞당김 (120ms)", 120),
    ];
    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        AddToggle("PvP 전장 말풍선", 60, config.PvpBubbles, value => config.PvpBubbles = value);
        AddText(pvpAvailable ? "채널·색상·표시 방식은 캐릭터 설정의 대화 말풍선 설정을 따릅니다." : Unavailable, 88);
        AddToggle("상점 품목 필터와 필요 재화", 138, config.MerchantTools, value => config.MerchantTools = value);
        AddText(exchangeCurrencyAvailable ? "상점 안의 '보유 수집품 제외'와 '필요 재화'를 사용합니다." :
            "상점 안의 '보유 수집품 제외'와 '필요 재화'를 사용합니다. 재화 교환 상점은 현재 게임 버전에서 필요한 게임 코드를 찾지 못해 제외됩니다.", 166);
        AddToggle("아이템별 입찰 숫자", 216, config.LootRolls, value => config.LootRolls = value);
        AddText("입찰 목록에서 우클릭 → '입찰 숫자 보기'를 선택합니다.", 244);
        AddToggle("측후·콤보 표시", 294, config.PositionalFlyText, value => config.PositionalFlyText = value, 300);
        new TextButtonNode { Position = new(400, 292), Size = new(140, 28), String = "세부 설정", OnClick = openColors }.AttachNode(this);
        AddText(positionalAvailable ? "일반 콘텐츠에서 데미지 텍스트에 측면·후방 성공/실패와 콤보 실패를 아이콘과 색으로 표시합니다. 항목별 켜기와 아이콘·색은 '세부 설정'에서 정합니다." :
            Unavailable, 322);
        AddToggle("감정표현 따라하기", 372, config.EmoteFollow, value => config.EmoteFollow = value, 250);
        AddText("반복 감정표현 중인 캐릭터를 우클릭 → '따라하기'. 내 화면은 바로 재생되고 다른 사람에게는 내 인터넷 지연만큼 늦게 보이므로, 박자 기준을 고를 수 있습니다.", 400);
        var unread = config.LastSeenChangelogVersion != ReleaseHistory.CurrentVersion;
        changelogButton = new TextButtonNode { Position = new(28, 456), Size = new(190, 28),
            String = unread ? "업데이트 내역 · NEW" : "업데이트 내역", OnClick = openChangelog };
        changelogButton.AttachNode(this);
        new TextNode { Position = new(360, 458), Size = new(190, 24), FontSize = 13,
            AlignmentType = AlignmentType.Right, String = $"v{ReleaseHistory.CurrentVersion}" }.AttachNode(this);
        // Attached last so the opened list draws above the text below it.
        var timing = new StringDropDownNode { Position = new(290, 370), Size = new(250, 28), Options = [.. FollowTimings.Select(t => t.Label)] };
        timing.SelectedOption = (FollowTimings.FirstOrDefault(t => t.Ms == config.EmoteFollowOffsetMs) ?? FollowTimings[0]).Label;
        timing.OnOptionSelected = label =>
        {
            if (FollowTimings.FirstOrDefault(t => t.Label == label) is { } choice) { config.EmoteFollowOffsetMs = choice.Ms; save(); }
        };
        timing.AttachNode(this);
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        if (changelogButton is null) return;
        var label = config.LastSeenChangelogVersion != ReleaseHistory.CurrentVersion ? "업데이트 내역 · NEW" : "업데이트 내역";
        if (changelogButton.String != label) changelogButton.String = label;
    }

    protected override void OnFinalize(AtkUnitBase* addon) => changelogButton = null;

    private void AddToggle(string text, float y, bool value, Action<bool> changed, float width = 430)
    {
        var node = new CheckboxNode { Position = new(28, y), Size = new(width, 24), String = text, IsChecked = value };
        node.OnClick = next => { changed(next); save(); };
        node.AttachNode(this);
    }

    private void AddText(string text, float y) => new TextNode
    {
        Position = new Vector2(48, y), Size = new Vector2(490, 36), String = text,
        FontSize = 13, TextFlags = TextFlags.MultiLine | TextFlags.WordWrap,
    }.AttachNode(this);
}
