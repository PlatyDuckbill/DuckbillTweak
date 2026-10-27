# 구조와 참고 자료

0.1.2.10은 위에 기록된 고정 VanillaPlus StickyShopCategories의 열기/종료 수명을 재참고했으나, 사용자 요청에 따라 그 기능의 저장 동작은 채택하지 않았다. 외부 구현을 복사하지 않았다. 상점 체크는 메모리에만 공유하고 Configuration v3에는 기능 on/off만 남긴다. 우주 화폐 행 매핑은 로컬 KR 실행 파일의 refresh/renderer/tooltip을 직접 대조한 별도 구현이다. 추가 필드/명령의 근거와 읽기 범위는 NATIVE-UI.md에 기록했다.

## 코드 구조

- `src/DuckbillTweak.Core`: 보유 상태, 품목 필터, 교환식 선택과 재화 합계, 전리품 관측 기록. 게임 의존성 없음.
- `src/DuckbillTweak.Plugin`: Dalamud 서비스 수명, 설정/명령, Framework에서 관리 값으로 복사하는 관측기와 네이티브 게임 화면.
- `tools/GameDataProbe`: 설치된 게임 데이터를 오프라인으로 읽는 도구. 전리품 로그 매크로와 재화 참조를 검사.
- `scripts/build.ps1`: 잠금 파일 복원/빌드 후 GameDataProbe resources로 실제 DLL의 내장 자료를 검사. 리소스 검사는 PE 메타데이터/바이트만 읽으며 게임 서비스나 플러그인 생성자를 실행하지 않음.
- `data`: 버전을 지정한 특수 화폐 ID 관계. 런타임 원격 요청 없음.

각 기능은 설정값에 따라 독립적으로 동작하며, Plugin.DisposeAsync가 Framework에서 네이티브 노드/이벤트/패치를 복원한 뒤 화면과 라이브러리 해제를 기다린다. 설정은 Dalamud 플러그인 설정에 저장한다. 채팅/입찰/보유량 관측은 파일로 저장하지 않는다.

상인 카탈로그/가격은 NPC 변경 시 읽는다. 인벤토리/수집품 상태는 상점/재화 화면이 열렸을 때500ms 간격, 파티 명단은1초 간격이다. 네이티브 상점 상태는 addon update에서300ms 간격으로 복사한다. ImGui Draw나 별도 채팅 오브젝트/좌표 관측기는 없다. [네이티브 계약](NATIVE-UI.md)에 구조와 제한을 기록한다.

미확인 비용을 합계에서 빠뜨려 완전한 합계처럼 표시하지 않는다. 해당 교환식을 선택하면 부분 합계 경고가 나온다. 현재 원본 상점에 표시된 판매는 구매 조건 충족 여부와 비용 계산을 분리하며, 조건 미확인도 예상 필요량에 포함하고 안내한다. 단가 할인이나 최저 비용 경로 최적화는 하지 않는다.

## 조사한 프로젝트

2026-10-26에 아래 고정 revision을 읽었다. 캐시의 참고 저장소는 Git에 포함하지 않는다. AGPL/GPL 프로젝트의 구현을 제품 소스로 복사하지 않았다. MIT KamiToolKit은 별도 의존성으로 도입했다.

| 참고 대상 | 고정 revision | 라이선스/참고 내용 |
|---|---|---|
| [VanillaPlus](https://github.com/MidoriKami/VanillaPlus/tree/17bddd82ca73e099f1a7af9b00c4ee13fa8a701c) | 17bddd82ca73e099f1a7af9b00c4ee13fa8a701c | AGPL-3.0. 기능별 설정·시작/종료와 상점/전리품 기능 구분 |
| [SimpleTweaksPlugin](https://github.com/Caraxi/SimpleTweaksPlugin/tree/c039932e40b0c752c09005cc4fdc362b6b786da4) | c039932e40b0c752c09005cc4fdc362b6b786da4 | AGPL-3.0. 개별 트윅·일반 전투 말풍선·TrackOutfits의 게임 캐시/세트 비트 의미 조사. 구현 복사/글로벌 패치·시그니처 도입 없음, KR 실행 파일 별도 분석 |
| [LootView](https://github.com/GitHixy/LootView/tree/11032cb108f433406ac0f8ed704f77b530024e19) | 11032cb108f433406ac0f8ed704f77b530024e19 | GPL-3.0. 공개 IChatGui.LogMessage 사용 가능성 조사. 구현은 로컬 KR 매크로 계약을 기준으로 작성 |
| [CriticalCommonLib](https://github.com/Critical-Impact/CriticalCommonLib/tree/34d364ea938e585b4f7eeab5b36e4261fdd817d0) | 34d364ea938e585b4f7eeab5b36e4261fdd817d0 | 상점 자료 위치 조사만 수행. 코드/의존성 미도입 |
| [AllaganLib](https://github.com/Critical-Impact/AllaganLib/tree/c34734f75e369777e7ac36dc57c09a8fc3e78fd4) | c34734f75e369777e7ac36dc57c09a8fc3e78fd4 | 조사 시 루트 라이선스 파일 없음. 코드 미도입. 특수 화폐와 아이템 사이 숫자 관계만 자료로 전사 후 로컬 시트에 대조 |
| [EXDSchema SpecialShop](https://github.com/xivdev/EXDSchema/blob/a1e24a0c72058353f30f23beb996792cdb926868/SpecialShop.yml) | a1e24a0c72058353f30f23beb996792cdb926868 | CostType3의 SpecialBucketId 의미 확인. 스키마/코드 미도입 |
| [KamiToolKit](https://github.com/MidoriKami/KamiToolKit/tree/1eaccb5c6d6fcc697f7e1b98b5528580f5edd201) | 1eaccb5c6d6fcc697f7e1b98b5528580f5edd201 | MIT. 게임 UI 컴포넌트/화면 생성용 source submodule. 원본 소스 수정 없음, 라이선스 출력 포함 |

동일 PC의 DuckbillFisher는 SDK/참조 선택, Core/Plugin 분리, 공개 인벤토리 API 사용과 한국 클라이언트 확인 범위 구분을 참고했다. 해당 저장소는 변경하지 않았다.

## 재화 계약

현재 KR SpecialShop의 비용별 CostType을 사용한다. 0은 일반 아이템,1은 HQ,2는 TomestonesItem.Tomestones→Item,3은 특수 화폐 매핑이다. 옛 shop-wide UseCurrencyType만으로 비용 종류를 정하지 않는다. CostType2 해석은 로컬 석판 교환식과 TomestonesItem의 관계를 대조한 결과이며 인게임 대표 확인은 남아 있다.

ID8223은 현재 한국 Item 시트에 이름이 없으므로 비용을 미확인으로 표시한다. 특수 화폐 표는 게임 버전 파일이 일치할 때만 적용하며 이름 없는 매핑을 제거한다. 열린 상점에서는 검증한 현재 목록의 실제 수량/비용을 사용한다. 퀘스트/업적은 공개 IUnlockState의 완료 상태로 확인하며 단순히 ID가 있다는 이유로 모두 제외하지 않는다.

수집품 등록은 공개 IUnlockState의 긍정 판정을 우선하고, 검사된 프로필에서 네이티브 IsItemActionUnlocked의1/2/3/4 상태를 구분한다. KR 코르나고 항아리의 Action50454는 공개 IsItemUnlockable 목록에 없음을 설치 DLL IL과 오프라인 Item 시트에서 대조했다. 보관된 외형도 보유로 보겠다는 사용자 요청에 따라 로드된 환상투영함/추억의 보관함을 읽되 재화 수량과 분리한다. 세부 가드와 미확인 처리는 [네이티브 계약](NATIVE-UI.md)에 기록한다.

`GameDataProbe <sqpack> items <이름>`, `npc <이름>`, `kornago-audit`는 로컬 시트의 품목/동작·NPC 연결·퀘스트 조건·소모품 분류를 읽는 오프라인 조사다. 게임 프로세스나 자동 테스트를 실행하지 않는다.

0.1.1.5 교환 필터는 IsHidden/LayoutRefreshPending만 사용했으나, 0.1.1.6의 KR 실행 코드 정적 점검에서 높이 계산/배치가 IsHidden을 참조하지 않음을 발견했다. 원본 항목의 Height/IsHidden/Disabled/렌더러 표시를 저장·조정·복원하고, 네이티브 높이·스크롤 재계산을 수행하도록 보완했다. AtkValues/ItemIndexMap/수량 컨트롤/구매 경로는 바꾸지 않는다. 근거 RVA와 함수 관계는 [네이티브 계약](NATIVE-UI.md)에 기록했다. `uld` 출력은 InclusionShop1024/ShopExchangeItem1025=TreeList, Shop1014=List를 대조한다. `ownership-audit`는 낡은 악보14273/14274의 완성 악보 연결과 뿔24337의 보관 분류를 확인한다. 뿔은 Cabinet 행이 없어 환상투영함에서 판정한다.

0.1.1.6 자체 점검에서 위 고정 revision의 VanillaPlus `Features/StickyShopCategories/StickyShopCategories.cs`와 `Utilities/Inventory.cs`, SimpleTweaks `Tweaks/Tooltips/TrackOutfits.cs`, LootView README의 Need/Greed 공개 시점을 재참고했다. 개념/수명/데이터 의미만 대조했고 구현을 복사하지 않았다. [FFCS TreeList 구조](https://github.com/aers/FFXIVClientStructs/blob/b72bc1f73e5f2b09ba460a9238fbf3b9a5bbeb3c/FFXIVClientStructs/FFXIV/Component/GUI/AtkComponentTreeList.cs)와 설치 DLL/로컬 실행 파일을 함께 사용했다. 새 서드파티 코드나 종속성은 도입하지 않았다.

보관함 현재 정보가 미로드이면 ItemFinderModule의 캐릭터 ID/캐시 상태를 확인해 게임 저장 정보를 읽는다. 자체 디스크 캐시를 만들거나 아이템 찾기 검색을 실행하지 않는다. 세트/Cabinet 비트는 설치 바인딩/XML과 KR 실행 파일의 읽기 함수에 대조했다.

## 전리품 계약

KR LogMessage1231은 lnum1=아이템, lstr2=입찰 종류, lnum3=숫자다. Dalamud 매개변수 인덱스는0부터 시작한다. 5183은 전리품 추가,5180은 선택 완료,1232/1233/5187/5188은 획득/분배다. 설치된 시트의 매크로가 필요한 참조를 포함하는지 확인한다.

다른 사람의 숫자는 입찰이 끝나 게임이 결과 로그를 보낸 이후 표시된다. 진행 중 다른 사람의 숫자를 미리 읽는 기능으로 설명하지 않는다. 참고한 LootView 고정 revision의 README Need/Greed rolls 설명도 결과 확정 후 공개를 명시한다. 로컬 실기에서도 선택5180 이후 숫자1231과 획득1232가 함께 수신됐다.

공개 로그에는 보상 슬롯 키가 없으므로 같은 raw item ID의 동시 드롭을 구분하지 않는다. SourceEntity의 이름과 HomeWorldId를 키로 사용하고 이름만 같은 플레이어를 합치지 않는다. 전리품에 없는 일반 획득 로그는 입찰 관측에 연결하지 않는다. 기능 활성화 이전이나 접속/지역 전환 이전의 기록은 복원하지 않는다.

0.1.1.7에서는 SimpleTweaks 고정 c039932e40b0c752c09005cc4fdc362b6b786da4의 `Tweaks/UiAdjustment/LootWindowDuplicateUniqueItemIndicator.cs`(Enhanced Loot Window), VanillaPlus 고정17bddd82ca73e099f1a7af9b00c4ee13fa8a701c의 `Features/StickyShopCategories/StickyShopCategories.cs`와 `Features/ArmourySearchBar/ArmourySearchBar.cs`를 참고했다. 등록과 물리적 보유 구분, 목록별 상태, 탭·갱신·해제 흐름을 검토했으며 AGPL 구현을 복사하지 않았다. 공개 API/설치된 KR 바인딩을 사용해 별도로 작성했고 새 네이티브 주소·후크는 추가하지 않았다.

0.1.1.8은 위 고정 KamiToolKit의 ComponentNode 생성/해제와 NativeListController의 원본 populator 수명을 참고했다. VanillaPlus StickyShopCategories와 AllaganLib ShopMonitorService의 addon 분류도 로컬 고정 소스에서 재확인했다. AGPL/라이선스 미명시 코드의 구현 복사나 새로운 의존성은 없다. 필터의 렌더러 풀/원본 인덱스 의미는 한국 실행 파일과 설치된 바인딩을 직접 대조하여 작성했다. 당시의 잘못된 중복 렌더러 가정과 교정한 계약은 NATIVE-UI.md에 기록했다.

0.1.1.9 참고 추가: [InventoryTools IPCService](https://github.com/Critical-Impact/InventoryTools/blob/70a9f410d9a8ab97bef6bcf795415ea2d7c75097/InventoryTools/IPC/IPCService.cs), revision70a9f410d9a8ab97bef6bcf795415ea2d7c75097, GPL-3.0. 공개 호출 계약과 보유 범위만 참고했으며 구현은 복사하지 않았다. 설치된InventoryTools1.15.0.13/동봉CriticalCommonLib의IL과도 대조했다. 런타임 종속성 추가/어셈블리 로딩/외부 플러그인 파일 읽기 없이 Dalamud 공개 IPC만 선택적으로 호출한다. 위 고정SimpleTweaks TrackOutfits도 게임 캐시와 현재 정보 구분을 재참고했다.

0.1.2.0은 위 고정LootView11032cb108f433406ac0f8ed704f77b530024e19의README, LootTrackingService.LogMessages.cs/Recording.cs/Rolls.cs/RollActions.cs에서공개결과와본인선택의시점·표현을참고했다. GPL 구현은복사하지않았으며입찰함수/자동UI숨김도도입하지않았다. 타인의Decided를결과미수신만으로Pass로바꾸는처리는이저장소의Unknown계약과맞지않아사용하지않는다. 실제읽기필드는설치된KR바인딩과로컬실행파일을별도로대조했고구현은독립적으로작성했다.

0.1.2.1은 설치된 KR Item/ItemUICategory/SpecialShop 데이터와 동일한 바인딩을 사용한다. 네이티브 읽기 필드·시그니처·버전 가드는 변경하지 않았다. 분류 정책은 Core의 관리 값 연산, 시트 연결은 Plugin의 ShopItemClassifier에 둔다. GameDataProbe는 같은 분류기 소스를 링크하여 복제 규칙의 불일치를 피한다. 새 외부 구현/의존성은 도입하지 않았다. 게임 자료의 전체 이름/설명/아이콘을 저장소로 추출하지 않는다.

0.1.2.2 분류 근거는 설치된 KR2026.10.06 Item/ItemAction/ItemUICategory/SpecialShop/Recipe/RacingChocoboItem 시트다. 추가 소모 동작851,852,2483,2634,2645,3800,5136,9994,17290,31315,32865,34493,36120,47023의 모든 해당 Item 설명을 오프라인 대조했다. 게임 이름 접미사 판정/외부 구현 복사/새 종속성/새 네이티브 메모리 접근은 없다. HousingFurniture/HousingYardObject/TreasureHuntRank 참조 타입과 실제 제작 재료를 이용한다. 소모 동작과 등록 동작이 다른 것을 확인했으며 알 수 없는 Action/추가 데이터는 기타로 보존한다. 감사 도구도 생산 분류기 소스를 그대로 링크한다.

0.1.2.3 추가 참고: [CriticalCommonLib InventoryItem.ToNumeric](https://github.com/Critical-Impact/CriticalCommonLib/blob/34d364ea938e585b4f7eeab5b36e4261fdd817d0/Models/InventoryItem.cs), [InventoryType](https://github.com/Critical-Impact/CriticalCommonLib/blob/34d364ea938e585b4f7eeab5b36e4261fdd817d0/Enums/InventoryType.cs), [CharacterMonitor](https://github.com/Critical-Impact/CriticalCommonLib/blob/34d364ea938e585b4f7eeab5b36e4261fdd817d0/Services/CharacterMonitor.cs), 고정34d364ea938e585b4f7eeab5b36e4261fdd817d0/GPL-3.0. 위 InventoryTools 고정70a9f410d9a8ab97bef6bcf795415ea2d7c75097의 IPCService도 재대조했다. 공개 반환 배열 형식/열거값/소유자 범위만 참조했고 구현을 복사하거나 DLL 의존성을 추가하지 않았다. 설치된1.15.0.13의 IL과 함께 확인했다. 보관 기록은 사용자 플러그인의 공개 IPC로만 읽으며 개인 설정 파일/데이터베이스/게임 메모리를 추가로 읽지 않는다. 기록 최신 시각과 전체 적재 여부는 반환되지 않아 완전한 실시간 재고로 취급하지 않는다.

0.1.2.8 참고: [FFCS CurrencyManager](https://github.com/aers/FFXIVClientStructs/blob/b72bc1f73e5f2b09ba460a9238fbf3b9a5bbeb3c/FFXIVClientStructs/FFXIV/Client/Game/CurrencyManager.cs), 위 고정b72bc1f73e5f2b09ba460a9238fbf3b9a5bbeb3c/MIT. ItemBucket의 예시와 공개 읽기 함수 의미를 참고했고 설치된 KR DLL/실행 코드와 별도로 대조했다. 구현 복사/새 native address/새 런타임 종속성은 없다. NativeCurrencyReader는 이미 존재하는 설치 바인딩을 기존 한국 프로필 가드 뒤에서 읽기 전용으로 호출한다. Core ExchangeCatalog는 SpecialShop에서 복사한 관리 값만 다루며 현재 페이지 전체를 연결한다. 게임/화폐 이름별 예외가 아니라 구조·ID·가격/수량을 기준으로 한다. 미래의 다른 addon/패킷/가격 인코딩을 추측 지원하거나 게임 업데이트 가드를 건너뛰지는 않는다.
