using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

internal sealed unsafe class NativeFoodSettingsAddon(Configuration config, Action save) : NativeAddon
{
    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        new TextNode { Position = new(28, 55), Size = new(430, 44), FontSize = 13,
            TextFlags = TextFlags.MultiLine | TextFlags.WordWrap,
            String = "준비 확인 이름 옆에 음식 시간을 표시합니다.\n극·영식·절의 초읽기 중 기준 미만이면 중앙 경고와 내 채팅창 요약을 한 번 표시합니다." }.AttachNode(this);
        AddMinutes("극 토벌전", 116, config.FoodExtremeMinutes, value => config.FoodExtremeMinutes = value);
        AddMinutes("영식 (영웅)", 164, config.FoodSavageMinutes, value => config.FoodSavageMinutes = value);
        AddMinutes("절", 212, config.FoodUltimateMinutes, value => config.FoodUltimateMinutes = value);
        new TextNode { Position = new(28, 265), Size = new(430, 60), FontSize = 13,
            TextFlags = TextFlags.MultiLine | TextFlags.WordWrap,
            String = "단위: 분 (1~60). 일반·환·멸 콘텐츠는 초읽기 경고 대상에서 제외합니다.\n상태를 읽지 못한 사람은 '미확인'이며 음식 부족 인원에 넣지 않습니다.\n파티원 음식 정보를 서버 채팅으로 전송하지 않습니다." }.AttachNode(this);
    }

    private void AddMinutes(string text, float y, int value, Action<int> changed)
    {
        new TextNode { Position = new(32, y + 3), Size = new(180, 28), FontSize = 14, String = text }.AttachNode(this);
        var input = new NumericInputNode { Position = new(230, y), Size = new(160, 28), Min = 1, Max = 60, Step = 1,
            Value = Math.Clamp(value, 1, 60) };
        input.OnValueUpdate = next => { changed(Math.Clamp(next, 1, 60)); save(); };
        input.AttachNode(this);
        new TextNode { Position = new(400, y + 3), Size = new(30, 28), FontSize = 14, String = "분" }.AttachNode(this);
    }
}
