# 네이티브 UI 계약

0.1.1.8의 UI는 KamiToolKit의 게임 컴포넌트를 사용한다. 보조 ImGui 목록은 없다. 라이브 native pointer는 각 UI lifecycle/Framework 콜백 안에서만 읽는다. 최종 화면·입력 동작의 실기 확인은 별도다.

## 고정 환경

- 게임: KR2026.10.06.0000.0000. 실행 파일 SHA-256: DB9824EFAC6D5370AB64B02353E882931DDA447E160EE1A5D9E5DCBA43695728.
- 바인딩: 설치된 KR FFXIVClientStructs7.57.0.9415, MVID93c1ca2b-80f3-43ef-9d58-ab0e99bb8909.
- KamiToolKit: MIT, revision1eaccb5c6d6fcc697f7e1b98b5528580f5edd201. 소스 수정 없이 submodule로 빌드하며 라이선스를 출력에 포함한다.
- SDK/API 버전은 변경하지 않았다. NativeCompatibility가 게임 해시·바인딩 모듈 ID를 확인한다. 틀리면 사용자 명령에 미지원 사유만 출력한다.

## 상점

### 0.1.1.8 원본 렌더러 풀 대응

0.1.1.6/7의 렌더러 중복 금지 검사는 잘못된 계약이었다. KR SetupItem RVA6A1FF0은 CategoryItemRendererList의 템플릿과 NodeList를 NodeCount+1로 순환 배정한다. 행 수가 화면 행 수보다 많으면 서로 다른 품목이 같은 렌더러를 공유한다. 이를 비정상으로 거부하면 필터가 비활성화되고, 검증만 제거한 채 OwnerNode를 숨기면 표시할 다른 품목도 사라진다.

현재 구현은 원본 Items 벡터/UIntValues/StringValues/ItemIndexMap/인덱스를 유지한다. 카테고리별 원본 렌더러 풀을 확인하고 표시 품목에만 순환 배정한다. 숨긴 품목은 높이0·IsHidden·Disabled와 그림/충돌이 없는 단일 전용 렌더러를 사용한다. 따라서 Height0 항목도 순회하는 native draw가 다른 품목을 중복해서 그리지 않는다. 숨김 전용 렌더러는 고정 KamiToolKit의 ComponentNode로 생성하며 표시 행을 재구성하지 않는다.

평면 구조, 고유 항목, 유효한 풀/노드, 한 뷰포트에 렌더러가 중복 배정되지 않는 용량을 검사한다. 충돌 노드가 없는 평면 SectionHeader 플래그는 허용하되 Group/LastItemInGroup/Depth는 거부한다. 실제 표시 범위의 렌더러만 보이게 하고 refresh/해제 전에 원래 연결/높이/숨김/비활성/표시를 복원한다. 제외 품목이 없으면 변경하지 않는다.

KR Populate RVA6A62C0의 6A631D는 원본 인덱스를 Renderer.ListItemIndex에 저장하며 6A6405–6A6412에서도 같은 인덱스와 현재 행 렌더러를 populator에 전달한다. 이 경로와 native 수량·미리보기·구매 이벤트를 그대로 사용하고 구매 명령/콜백을 생성하지 않는다. 목록 계산/스크롤 보정은 설치된 바인딩을 사용한다. 새 런타임 주소/후크는 추가하지 않았다.

ShopExchangeCurrency의 KR ULD는680×528, node20/component1022 TreeList, header13/구분선19이며 새로운 컨트롤은y92에 들어간다. AgentShop의 addon ID/수신 배열/3배 비용 배열 일치를 확인한 현재 탭만 계산한다. 일치하지 않으면 이유/개수만 로그에 남기며 다른 목록으로 추정하지 않는다. 필터 적용 실패에서는 원본을 복구하고 체크박스를 잠그지 않아 해제할 수 있다.

### 이전 0.1.1.6/7 구현과 실패 원인

InclusionShop/ShopExchangeItem의 단순화한 대체 ListNode는 제거된 상태를 유지한다. ULD의 component1024/1025는 TreeList다. 0.1.1.5의 IsHidden/LayoutRefreshPending만으로는 충분하지 않았다. KR CalculateVisibleItemHeightAndCount RVA6A5BB0은 IsHidden을 읽지 않고 Height/Depth/Type으로 높이와 개수를 계산한다. UpdateListItems RVA6A4F70과 Draw RVA6A3840도 Height만큼 위치를 진행시킨다. 해당 원본 실행 파일의 정적 분석으로 확인했다.

0.1.1.6은 평면 목록(Depth0, Group/SectionHeader 없음, 항목·렌더러 중복 없음)만 처리한다. 숨길 항목의 Height를0, IsHidden을true, Disabled 비트를true로 두고 원본 렌더러 OwnerNode의 표시를 끈다. 원래 높이/숨김/비활성 비트/표시 상태는 관리 값으로 저장한다. CalculateVisibleItemHeightAndCount 후 RecalculateVisibleItems(false)를 호출해 전체 높이·스크롤 한계·원본 행 위치를 다시 계산하고 충돌 목록을 갱신한다. 후자는 로컬 vtable 슬롯38/RVA6A5A40에서 스크롤 보정→위치 갱신→행 갱신을 호출함을 대조했다. 새 주소나 후크를 런타임에 추가하지 않았다.

표시되는 원본의 묶음 수량/보유 수/교환 입력/± 버튼/재화 아이콘/색은 재구성하지 않는다. 구매 콜백 전달과 인덱스 재매핑도 하지 않는다. 계산에 사용하는 활성 상태는 필터가 임시로 설정한 Disabled 비트가 아닌 저장된 원래 값을 반환한다.

원본 Items 벡터 수를 agent 전체 표시 행 수와 대조한다. 원본 벡터 순서와 ItemIndexMap은 유지한다. refresh 이전/체크 해제/finalize/해제에서 살아 있는 항목의 저장 상태를 복원한다. 이전 렌더러 주소는 현재 항목의 렌더러와 일치할 때만 사용한다. 구조가 다르면 원본을 복원하고 필터를 비활성화한다. 일반 길 Shop의 component1014는 List이므로 기존 구현을 유지하되 refresh 이전에도 원본 입력을 복원한다. 추가 컨트롤 제거·원래 위치/크기 복원 이후 충돌 목록도 다시 생성한다.

### 기본 계약과 이전 구현

아래 대체 목록/입력 차단 설명은0.1.1.4의 방식이며0.1.1.5에서는 일반 길 Shop에만 적용된다. 교환 상점에는 위 원본 행 필터를 사용한다.

로컬 ULD에서 확인한 루트/노드를 사용한다.

| ULD | 루트 크기 | 목록 노드 | 이동하는 헤더/구분선 |
|---|---|---|---|
| Shop | 584×512 | 16 | 7,12,13 |
| InclusionShop | 760×586 | 19 | 11,17 |
| ShopExchangeItem | 760×556 | 20 | 12,18 |
| ShopExchangeCurrency | 680×528 | 20 | 13,19 |

기존 행 배열/agent 인덱스는 변경하지 않는다. 네이티브 체크박스/종류 선택/버튼을 위한 한 줄을 확보하고, 필터를 사용할 때만 같은 게임 화면 안에 네이티브 행 목록을 표시한다. 필터 해제/탭 변경/게임 화면 종료/플러그인 언로드 시 원래 목록의 위치·높이·투명도·입력 상태를 복원하고 추가 노드를 해제한다.

아이템 표시명은 Item 시트의 ExtractText로 복사한다. 네이티브 Utf8String.ToString 결과를 일반 문자열로 UI에 넣지 않는다(색/아이콘의 STX/ETX payload가 남을 수 있음). 행 렌더를 성공시킨 뒤에만 원본 입력을 차단한다. 관리 렌더 오류는 원본 입력을 먼저 복원하고 해당 화면을 재열기 전까지 반복하지 않는다. 이는 무효 포인터를 예외 처리로 보호한다는 뜻이 아니며 네이티브 수명/형태 검사는 계속 필요하다.

0.1.1.4는 투명도와 IsItemInteractionEnabled 외에 원본 목록/렌더러/하위 컴포넌트의 HasCollision/RespondToMouse를 저장·차단한다. 매 update에서 살아 있는 현재 트리만 대조하고, 해제 시 현재 트리에 여전히 속한 노드의 두 비트만 복원한다. 목록 표시/숨김/행 변경과 입력 복구 뒤에는 UpdateCollisionNodeList를 호출한다. 숨긴 대체 목록의 캐시된 충돌 영역이 원본 스크롤을 가로채지 않도록 하는 처리다. 데이터 갱신만으로 스크롤을 초기화하지 않으며 행 정체성/필터가 바뀔 때만 맨 위로 돌린다. 교환 목록은52px 대신26px 한 줄/22px 아이콘을 사용한다.

Shop/ShopExchangeItem은 AgentShop의 addon ID와 수신 품목/비용 배열 크기를 대조한다. 비용 배열은 수신 품목당3개 구조가 일치할 때만 사용한다. 일반 길 상점은 실제 비용 항목이 Item1인지 확인한다. 가격을 추측해 다른 품목의 값을 표시하지 않는다.

InclusionShop은 CategoryIndexMap으로 화면 선택→실제 Category를 매핑한다. SelectedSubCategoryIndex는 실제 subcategory이고, ItemIndexMap으로 원본 UI 행→원본 품목을 연결한다. map 범위와 중복을 검사한다. 카테고리/품목 배열을 재정렬하거나 자동 카테고리 선택을 호출하지 않는다.

재화 화면은 검증한 현재 목록의 수신 품목·실제 수량·비용·비용 보유 수를 관리 스냅샷으로 받아 계산한다. 수량0/미확인을1로 추측하지 않는다. 단일 보상에서 원본 품목/품질/비용 키·수량이 모두 일치하는 시트 교환식들의 보상 수량이 하나로 확정될 때만 복구하고, 나머지는 제외 수를 표시한다. 구매 조건은 현재 네이티브 행의 활성 상태 또는 보상/비용이 정확히 일치하는 시트 교환식의 해금 조건 확인으로 판정한다. 퀘스트/업적 ID가 있다는 이유만으로 모두 미확인 처리하지 않고 공개 IUnlockState로 완료를 확인한다. 아직 미확인 조건/축제/공략 조건은 제외한다. 이전 카테고리/다른 화면의 스냅샷은 해제·실패·세션 전환 시 제거한다.

소모품 제외는 로컬 ItemUICategory44(약품),46(요리),55(염료),60(촉매)에 한정한다. 식재료나 일반 기타/수집품을 광범위하게 소비형으로 추측하지 않는다. 이 체크 상태는 상점/재화 화면에서 공유하며 기본은 꺼짐이다.

## 등록과 외형 보관

0.1.1.5는 현재 보관함 정보가 미로드이면 ItemFinderModule의 CharacterContentId가 현재 플레이어와 같은지 확인하고 IsGlamourDresserCached/CabinetState.Loaded를 대조해 게임 캐시를 읽는다. 현재 MirageManager/UIState.Cabinet 정보가 있으면 우선한다. 검색·파일 읽기/쓰기·캐시 적재 요청은 호출하지 않는다. Cabinet 캐시의 비트 범위 밖 행은 미확인으로 남긴다.

환상투영 세트는 MirageStoreSetItem의11개 부위를 확인한다. 현재 정보에서는 IsSetSlotUnlocked를 사용하고, 캐시에서는 missing bit가0인 부위만 포함한다. KR 실행 파일의 IsSetSlotUnlocked RVA892460과 Cabinet.IsItemInCabinet RVAC1BAE0를 오프라인으로 읽어 각각0=보유/1=보유 비트 의미와 Cabinet 행 ID 기준을 대조했다. 런타임에 글로벌 주소를 도입하지 않았다.

낡은 악보는 ItemUICategory94/ItemAction0 재료를 Recipe의 Orchestrion 결과 품목에 연결한다. 이름 문자열로 추측하지 않는다. 결과가 여러 곡이면 모두 등록된 경우만 원본 재료를 보유로 친다. 일부 미등록/미확인은 남긴다. 로컬 시트에서 Blood for Blood14273→14239/Orchestrion18, Starved14274→14240/Orchestrion19를 확인했다.

현재/게임 저장/미확인 상태를 체크박스 툴팁에서 안내한다. 게임 캐시도 없으면 사용자가 보관함을 한 번 열어야 할 수 있다. 자체 기록은 캐릭터/지역/해제 시 계속 비우며 새 스냅샷에서 현재 캐릭터의 게임 캐시를 다시 확인한다.

참고: [ItemFinderModule 필드 계약](https://github.com/aers/FFXIVClientStructs/blob/b72bc1f73e5f2b09ba460a9238fbf3b9a5bbeb3c/FFXIVClientStructs/FFXIV/Client/UI/Misc/ItemFinderModule.cs). 로컬 MVID의 타입/배열/상태를 대조했다. SimpleTweaks 고정 revision의 TrackOutfits는 캐시/세트 비트 의미 조사에 참고했으며 구현 소스를 복사하지 않았다.

이하 기본 등록 API와 현재 정보 읽기 계약은 계속 적용된다. 이전에 미지원으로 남겼던 세트는 위 부위 검사로 보완한다.

공개 IUnlockState의 긍정 등록 판정을 우선한다. ItemAction이 있는 품목은 검사된 네이티브 프로필에서 ExdModule.GetItemRowById → UIState.IsItemActionUnlocked를 읽기 전용으로 사용한다. 설치된 바인딩 XML의 반환 계약은1=등록,2=미등록,3=미확인,4=등록 상태 없음이다.3/기타 실패를 미등록으로 바꾸지 않는다. 코르나고 항아리49805–49820은 KR ItemAction.Action50454를 쓰며 현재 Dalamud IsItemUnlockable의 화이트리스트에 없어 이 경로가 필요하다. XBＭPet 시트를 추정해 매핑하지 않는다.

프로필 검사 이후 Framework500ms 스냅샷에서 MirageManager.PrismBoxLoaded/PrismBoxItemIds와 UIState.Cabinet.IsCabinetLoaded/IsItemInCabinet(Cabinet.RowId)를 읽는다. 실제로 읽은 보관품은 보유에 합치지만 Spendable 재화에는 합치지 않는다. 정보 미로드 또는 해석하지 못한 세트는 부재의 근거로 쓰지 않는다. 보관함 적재 상태를 체크박스 툴팁에서 안내하고, 데이터가 없다면 해당 보관함을 사용자가 한 번 열어야 할 수 있다. 로드 요청/아이템 이동을 자동 실행하지 않으며 캐릭터/지역/해제 시 자체 기록을 비운다.

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

우클릭은 행을 관측할 뿐 소비하지 않는다. PreventOriginal/SetEventIsHandled를 호출하지 않고 기본 메뉴를 그대로 연다. addon ID/원본 행/아이템 ID/시각/화면 세대를 관리 값으로 복사한다. 공개 IContextMenu.OnMenuOpened의 NeedGreed 기본 메뉴에 '입찰 숫자 보기'를 추가한다. 0.1.1.6은 설치 바인딩의 AgentLoot.HoveredSlotIndex를 먼저 확인하되 addon ID·행 범위·HoveredItemId와 원본 행의 raw ID가 모두 일치할 때만 허용한다. 현재 네이티브 컨텍스트를 확인할 수 없을 때만2초 이내 관측을 재검증한다. 관측은 메뉴마다 비워 빠른 연속 우클릭에서 이전 항목이 우선하지 않도록 한다. 미리보기 등 기존 항목을 재구성하거나 제거하지 않는다.

추가 항목을 선택할 때만 화면 요청을 만든다. 메뉴를 고르는 동안의 시간은 제한하지 않지만, 다음 addon update에서 화면 세대/ID/행/아이템이 그대로인지 재검증하며1초가 지난 미처리 요청은 버린다. addon finalize/기능 해제/플러그인 dispose에서 구독과 대기 요청을 제거한다. 바인딩/메뉴 항목 추가/선택/화면 요청을 각각 기록한다. 로그 수신 형식/숫자 기록 시작/귀속 실패 사유도 구분하며 원문·이름·실제 숫자는 기록하지 않는다.

게임 로그로 받은 숫자만 표시한다. 실기1건에서 숫자는 본인의 선택 완료 이후 획득 결과와 같은 시점에 수신됐고 사용자가 표시를 확인했다. 선택 전 숫자나 모든 배분 방식의 수신 시점을 보장하지 않는다. 다른 파티 명단은 공개 IPartyList accessor로 읽고 A/B/C 소속을 추측하지 않는다. 같은 raw item ID의 동시 드롭은 기존 구분 불가 처리를 유지한다. 0.1.1.3 기본 컨텍스트 메뉴와 추가 항목 병행, 화면 위치, 배율/테마, 전리품 배분 방식별 결과는 대표 확인 대기다.

## 재현 검사

```powershell
. ./scripts/enter-dev.ps1
dotnet run --project tools/GameDataProbe -- native-contract '게임 경로/ffxiv_dx11.exe' "$env:DALAMUD_HOME/FFXIVClientStructs.dll"
dotnet run --project tools/GameDataProbe -- '게임 경로/sqpack' uld ui/uld/Shop.uld
```

이 명령은 디스크의 실행 파일/자료만 읽는다. 게임 프로세스의 메모리를 읽거나 쓰거나 테스트하지 않는다. 빌드/정적 검사 통과를 실기 완료로 기록하지 않는다.

### 0.1.1.7 상점 연결과 설정 적용

재화 화면은 OpenForShop으로 원본 addon ID와 관리 세션 세대를 저장한다. 공개 addon의 ID/ready/visible 및 로그인/캐릭터/지역 상태가 일치할 때만 현재 표시 스냅샷을 사용한다. 읽기 실패에서 카탈로그로 대체하지 않으며, 원본 detach 또는 세션 변경에서 닫는다. 카탈로그는 정확히 일치하는 판매 조건/보상 수량 보완용으로만 남는다. 프레임/refresh 경계에서는 이전 계산 스냅샷을 비운다.

일반 길 상점의 전체 목록은 각 품목 종류의 제외 설정을 개별 적용한다. 제외 결과가 원본 전체와 같으면 원본 행을 유지한다. 필터 행 클릭은 현재 소속·보상 수량·실제 길 가격도 다시 대조한다. ShopExchangeItem 체크 설정은 NPC ID와 수신 품목/비용 품목 구성의 SHA-256 키로 구분한다. 문자열 이름·보유량·선택 수량·가격을 키에 넣지 않는다. 이전 NPC-only 키는 기본값으로만 읽고 새 선택은 개별 키에 저장한다.

## 0.1.1.9 보유 보완과 계산 명세

선택적 AllaganTools.ItemCount IPC는 (uint itemId, ulong characterId, int inventoryType)→uint다. 설치된InventoryTools1.15.0.13의provider와CriticalCommonLib에서 캐릭터 필터/SortedContainer2501(GlamourChest),2500(Armoire)를 대조했다. 게임 보관함 정보가 미확인인 표시 장비만 조회하며 양수만 보유 근거다. 캐릭터/지역 전환·비활성화에서 관리 캐시를 비우고, 미설치/언로드/오류/0은 기존Unknown 판정을 유지한다. IPC나 외부 캐시를 실제 교환 재화 잔액으로 쓰지 않는다. 게임의 현재 정보가 있으면 우선한다.

소모품 분류는 기존ItemUICategory44/46/55/60에 KR ItemAction.Action816(설명서/우대권),944(PvP 소모품/폭죽)를 추가했다. 전체82개 이름/분류와 장비0개를 감사했으며 기타/잡화 전체로 확대하지 않았다.

재화 창은760×586에서 필터y50/84, 계산 기준y118, 예외 안내y146, 재화 헤더y180/표y206–296, 품목 헤더y338/표y364–546, 계산 단위 안내y552를 사용한다. 안내는한 줄/Ellipsis/툴팁으로 분리하여 LineSpacing12에FontSize13인 이전 다중행 겹침을 제거했다. 원본 교환별1회 비용과 보상 수량을 보여 주며 묶음 보상도 비용은1회만 합산한다. 전체 보기/옵션 갱신 중 선택 콜백은 억제한다.

## 0.1.2.0 입찰 상태와 원본 관측

KR LogMessage1231은 Item lnum1/종류lstr2/숫자lnum3,5180은아이템/선택 완료만 포함한다. 후자에는 Need/Greed/Pass 정보가 없어 다른 사람의 포기를 확정하지 않는다. 결과 종류는 한국어 및NEED/GREED를 인식하고 나머지는Unknown으로 남긴다. 종류 원문은 메모리/UI 툴팁에만 두며 로그에는종류 enum만 기록한다.

검사한KR Loot.Instance 시그니처가 로컬VA1410201C6에서142ACFC70을 가리킴을 대조했다. 로컬 항목 수 함수RVA C39100은 첫ItemId의base+18,RollResult의base+34와44(hex) stride를 읽는다. 설치된MVID의Items는16개,첫항목+10,ItemId+8,RollResult+24,크기44(hex)로 일치한다. AgentLoot 갱신RVA1020190의호출/ID·수량·모드 읽기도 대조했다. 새 런타임 고정 주소/스캔/패치/입찰 호출은 도입하지 않고 기존FFCS 바인딩의 읽기만 사용한다. 게임 SHA/MVID 검사 뒤 Framework에서 활성화한다.

NativeLootObserver는 유효한16슬롯 스냅샷과 enum/아이템/슬롯 범위를 확인한다. 등록된 미완료 항목을 상자/슬롯/raw아이템으로 연결하고 본인의RollResult1/2/5만선입찰/입찰/포기로 반영한다. RollValue는 사용하지 않는다. Awarded6을 새 미완료 중복 항목으로 계산하지 않는다. 유효 스냅샷에서2회 연속 사라지면 항목을 종료 표시하며,창을 닫았다는 이유로 종료하지 않는다. 종료 이후3초 이내 결과는 후보가유일할 때만 연결하고 새 동일 아이템과 충돌하면 거부한다.

관측 명단은 항목별로 남기며 공개파티/연합 accessor로만 갱신한다. 진행 중2회 연속 불참은이탈 표시,재관측은복귀다. 기존 숫자와선택은 유지한다. 없음/부재/미수신을포기나불참권한으로 바꾸지 않는다. 숫자순위는Need가Greed보다우선하고,같은종류에서큰값 우선/동점공동순위다. Unknown/Pass/숫자미수신/중복귀속불가에는 순위가없다. 상위3명 색상과등수를 함께 표시하고 실제획득자 표시는로그만 따른다.

뷰 좌표:420×500 또는1164×500,아이템y48,진행y94,관측통계y120,그룹y148/열제목y174,목록y202–426,설명y438/462. 각열360폭/374간격의8행 높이로추가행은스크롤한다. 각열은고정관측순이며순위에따라명단이움직이지 않는다. 다른연합원두열은실제파티A/B/C가아니다.

0.1.2.1: 현재 원본 행의 Disabled/구매 조건 미확인은 필요 재화 계산의 제외 근거가 아니다. 현재 addon에 표시되고 수량/가격을 읽은 교환식은 비용 예상에 포함하며 구매 가능 여부는 별도로 안내한다. 시트의 숨겨진 다른 교환식을 가져오거나 자동 구매하지 않는다. 품목 분류는 원본 행과 계산 목록에 공통 적용하고 지불 재화에는 적용하지 않는다. 보유 수집품과 제외 재료가 섞인 묶음은 각 보상이 두 제외 조건 중 하나에 해당하면 숨긴다. 원본 행/인덱스/구매 함수의 변경은 없다.
