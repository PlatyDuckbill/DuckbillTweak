# 네이티브 UI 계약

0.1.1.3의 UI는 KamiToolKit의 게임 컴포넌트를 사용한다. 보조 ImGui 목록은 없다. 라이브 native pointer는 각 UI lifecycle/Framework 콜백 안에서만 읽는다. 최종 화면·입력 동작의 실기 확인은 별도다.

## 고정 환경

- 게임: KR2026.10.06.0000.0000. 실행 파일 SHA-256: DB9824EFAC6D5370AB64B02353E882931DDA447E160EE1A5D9E5DCBA43695728.
- 바인딩: 설치된 KR FFXIVClientStructs7.57.0.9415, MVID93c1ca2b-80f3-43ef-9d58-ab0e99bb8909.
- KamiToolKit: MIT, revision1eaccb5c6d6fcc697f7e1b98b5528580f5edd201. 소스 수정 없이 submodule로 빌드하며 라이선스를 출력에 포함한다.
- SDK/API 버전은 변경하지 않았다. NativeCompatibility가 게임 해시·바인딩 모듈 ID를 확인한다. 틀리면 사용자 명령에 미지원 사유만 출력한다.

## 상점

로컬 ULD에서 확인한 루트/노드를 사용한다.

| ULD | 루트 크기 | 목록 노드 | 이동하는 헤더/구분선 |
|---|---|---|---|
| Shop | 584×512 | 16 | 7,12,13 |
| InclusionShop | 760×586 | 19 | 11,17 |
| ShopExchangeItem | 760×556 | 20 | 12,18 |

기존 행 배열/agent 인덱스는 변경하지 않는다. 네이티브 체크박스/종류 선택/버튼을 위한 한 줄을 확보하고, 필터를 사용할 때만 같은 게임 화면 안에 네이티브 행 목록을 표시한다. 필터 해제/탭 변경/게임 화면 종료/플러그인 언로드 시 원래 목록의 위치·높이·투명도·입력 상태를 복원하고 추가 노드를 해제한다.

아이템 표시명은 Item 시트의 ExtractText로 복사한다. 네이티브 Utf8String.ToString 결과를 일반 문자열로 UI에 넣지 않는다(색/아이콘의 STX/ETX payload가 남을 수 있음). 행 렌더를 성공시킨 뒤에만 원본 입력을 차단한다. 관리 렌더 오류는 원본 입력을 먼저 복원하고 해당 화면을 재열기 전까지 반복하지 않는다. 이는 무효 포인터를 예외 처리로 보호한다는 뜻이 아니며 네이티브 수명/형태 검사는 계속 필요하다.

Shop/ShopExchangeItem은 AgentShop의 addon ID와 수신 품목/비용 배열 크기를 대조한다. 비용 배열은 수신 품목당3개 구조가 일치할 때만 사용한다. 일반 길 상점은 실제 비용 항목이 Item1인지 확인한다. 가격을 추측해 다른 품목의 값을 표시하지 않는다.

InclusionShop은 CategoryIndexMap으로 화면 선택→실제 Category를 매핑한다. SelectedSubCategoryIndex는 실제 subcategory이고, ItemIndexMap으로 원본 UI 행→원본 품목을 연결한다. map 범위와 중복을 검사한다. 카테고리/품목 배열을 재정렬하거나 자동 카테고리 선택을 호출하지 않는다.

마우스 좌클릭에서만 원본 인덱스의 ListItemClick을 전달한다. 전달 직전에 addon ID·품목 ID·컨텍스트/비용·비활성 상태를 재확인한다. 필터 행 번호를 구매 인덱스로 사용하지 않는다. 구매 확인 버튼이나 서버 명령을 호출하지 않는다. 원본을 대조할 수 없는 화면은 필터를 비활성화하고 원래 목록을 유지한다.

참고: [AgentShop](https://github.com/aers/FFXIVClientStructs/blob/b72bc1f73e5f2b09ba460a9238fbf3b9a5bbeb3c/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentShop.cs), [AgentInclusionShop](https://github.com/aers/FFXIVClientStructs/blob/b72bc1f73e5f2b09ba460a9238fbf3b9a5bbeb3c/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentInclusionShop.cs). 글로벌 소스의 구조 설명만 참고하고 로컬 DLL에서 존재/타입을 대조했다. 글로벌 주소/시그니처를 도입하지 않았다.

## PvP 말풍선

게임 기본 말풍선의 업데이트 경로를 로컬 실행 파일에서 디스어셈블했다. 기본 설정 조회(2C2)와 이후의 말풍선 생성/채널/스타일 처리는 그대로 두고, PvP 안에서만 아래 억제 조건 계산을 조정한다. 전장 종료/기능 해제/언로드 시 직접 바꾼 바이트만 복원한다. 다른 플러그인이 바꾼 바이트는 덮어쓰지 않는다.

| RVA | 원본 바이트 | PvP 활성 시 |
|---|---|---|
| CD5682 | 40 B6 01 | 40 30 F6 |
| CD568A | E8 E1 C6 B6 FF | 31 C0 90 90 90 |
| CD571E | 40 B6 01 | 90 90 90 |

첫 지점은 전투 억제값 설정, 두 번째는 content 상태를 조회하는 호출, 세 번째는 컨텍스트 기반 억제값 설정이다. 세 번째는 이전 기본 설정의 값을 지우지 않고 추가 설정만 생략한다. 이 해석은 정적 제어 흐름 확인이며, 실제 PvP에서 기본 설정을 끈 경우/각 채널/전투 중 동작 확인은 남아 있다. 패치는 Framework에서 켜고 끄며 원래 명령 바이트가 모두 일치할 때만 적용한다.

## 입찰

NeedGreed ULD의 목록6과 현재 바인딩의 AddonNeedGreed.Items를 대조한다. 원래 입찰/포기 버튼과 콜백은 변경하지 않는다. 공개 IAddonEventManager로 렌더러 EventNode/CollisionNodeList의 MouseDown을 구독하고, IAddonLifecycle PreReceiveEvent의 ListItemClick/DoubleClick도 받는다. 마우스 이벤트는 클릭 대상 노드를 현재 렌더러와 대조하고, 목록 이벤트는 오른쪽 버튼과 SelectedIndex/ListItemRenderer를 대조한다. hovered item/툴팁 ID에 의존하지 않는다.

우클릭은 행을 관측할 뿐 소비하지 않는다. PreventOriginal/SetEventIsHandled를 호출하지 않고 기본 메뉴를 그대로 연다. addon ID/원본 행/아이템 ID/시각/화면 세대를 관리 값으로 복사한다. 공개 IContextMenu.OnMenuOpened의 NeedGreed 기본 메뉴에서2초 이내의 행을 재검증하고 '입찰 숫자 보기'를 추가한다. 메뉴가 관측 이벤트보다 먼저 열리면 설치 바인딩의 AgentLoot.HoveredSlotIndex를 사용하되 addon ID·행 범위·HoveredItemId와 원본 행의 raw ID가 모두 일치할 때만 허용한다. 미리보기 등 기존 항목을 재구성하거나 제거하지 않는다.

추가 항목을 선택할 때만 화면 요청을 만든다. 메뉴를 고르는 동안의 시간은 제한하지 않지만, 다음 addon update에서 화면 세대/ID/행/아이템이 그대로인지 재검증하며1초가 지난 미처리 요청은 버린다. addon finalize/기능 해제/플러그인 dispose에서 구독과 대기 요청을 제거한다. 바인딩/메뉴 항목 추가/선택/화면 요청을 각각 기록한다. 로그 수신 형식/숫자 기록 시작/귀속 실패 사유도 구분하며 원문·이름·실제 숫자는 기록하지 않는다.

게임 로그로 받은 숫자만 표시한다. 실기1건에서 숫자는 본인의 선택 완료 이후 획득 결과와 같은 시점에 수신됐고 사용자가 표시를 확인했다. 선택 전 숫자나 모든 배분 방식의 수신 시점을 보장하지 않는다. 다른 파티 명단은 공개 IPartyList accessor로 읽고 A/B/C 소속을 추측하지 않는다. 같은 raw item ID의 동시 드롭은 기존 구분 불가 처리를 유지한다. 0.1.1.3 기본 컨텍스트 메뉴와 추가 항목 병행, 화면 위치, 배율/테마, 전리품 배분 방식별 결과는 대표 확인 대기다.

## 재현 검사

```powershell
. ./scripts/enter-dev.ps1
dotnet run --project tools/GameDataProbe -- native-contract '게임 경로/ffxiv_dx11.exe' "$env:DALAMUD_HOME/FFXIVClientStructs.dll"
dotnet run --project tools/GameDataProbe -- '게임 경로/sqpack' uld ui/uld/Shop.uld
```

이 명령은 디스크의 실행 파일/자료만 읽는다. 게임 프로세스의 메모리를 읽거나 쓰거나 테스트하지 않는다. 빌드/정적 검사 통과를 실기 완료로 기록하지 않는다.
