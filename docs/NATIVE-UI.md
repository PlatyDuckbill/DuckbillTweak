# 네이티브 UI 계약

0.1.1.8의 UI는 KamiToolKit의 게임 컴포넌트를 사용한다. 보조 ImGui 목록은 없다. 라이브 native pointer는 각 UI lifecycle/Framework 콜백 안에서만 읽는다. 최종 화면·입력 동작의 실기 확인은 별도다.

## 고정 환경

- 게임: KR2026.10.06.0000.0000. 실행 파일 SHA-256: DB9824EFAC6D5370AB64B02353E882931DDA447E160EE1A5D9E5DCBA43695728.
- 런타임: 검토한 wasdcxz15.0.3.9/dal4kr15.0.3.7/dal4kr15.0.3.9의 세 모듈 조합. 버전 번호만으로 허용하지 않는다. 새 dal4kr15.0.3.9 지원은0.2.0.4부터 제공한다.
- KamiToolKit: MIT, revision1eaccb5c6d6fcc697f7e1b98b5528580f5edd201. 소스 수정 없이 submodule로 빌드하며 라이선스를 출력에 포함한다.
- SDK/API 버전은 변경하지 않았다. NativeCompatibility가 게임 해시와 로드된 세 모듈의 MVID/디스크 SHA256 조합을 확인한다. 틀리면 사용자 명령에 미지원 사유만 출력한다.

### 2026-10-30 dal4kr15.0.3.9 추가

제보된 Dalamud15.0.3.9/FFCS MVID `0b9687ac-66d1-439f-8785-f7fff74f3d38`는 [dal4kr 배포 revision6f0bb791](https://github.com/dal4kr/Dalamud.Resources/commit/6f0bb791f643566169bb09c6f384a7bb387e47e6)의 실제 DLL과 일치한다. 해당 배포는2026-10-29T14:01:21Z(한국 시각23:01:21)에15.0.3.7에서15.0.3.9로 갱신됐다. SupportedGameVer는2026.10.06.0000.0000으로 동일하다. 기존 wasdcxz15.0.3.9와 같은 버전 표기여도 별개 빌드이며 이전 두 프로필 어느 쪽에도 해당하지 않았다. 실제 친구 PC의 전체 모듈 해시/설치 버전까지 확인한 것은 아니다.

조사 원본은 [고정 Release.zip](https://raw.githubusercontent.com/dal4kr/Dalamud.Resources/6f0bb791f643566169bb09c6f384a7bb387e47e6/Dalamud/latest/Release.zip), 51,025,094바이트/SHA256 `794AE27BC9B560D385A96022E2C35D32CCB1776411A3BC972AFD90166ED75E42`다. `.cache/native-dal4kr-15.0.3.9/runtime`에만 추출했으며 게임/런처 DLL이나 DuckbillFisher 원본은 변경하지 않았다. DLL의 InformationalVersion은 Dalamud `15.0.3.9+ffe397272e589180ddb6d2718635636c02558c83`, FFCS/Interop `1.0.0+ceaa4c9912e295f99f46e1013fe7243bee1235c2`다. 원본 commit_hash.txt에는 Dalamud revision 뒤에 `-dirty`가 있어 완전한 소스 재현을 주장하지 않으며, 지원 근거는 고정 ZIP의 실제 바이트·메타데이터다. Dalamud는AGPL-3.0, FFCS/Interop은MIT이며 SDK DLL은 조사/참조용으로만 사용한다. 제품에 DLL/외부 구현 소스를 복제하지 않았다.

| 모듈 | AssemblyVersion | MVID | SHA256 |
|---|---|---|---|
| Dalamud | 15.0.3.9 | 396f9566-f391-4d29-8022-f414a19bbfc7 | 0E5FBEF4B66B90D9C5C82B890338D07689F9BB7EB8AEAB6287A4FB2AA645EBA6 |
| FFXIVClientStructs | 7.57.0.9758 | 0b9687ac-66d1-439f-8785-f7fff74f3d38 | 271A16276655A828DAE7A8AC213B3890B2B328A12B1FAB2B936E070931A5D7E1 |
| InteropGenerator.Runtime | 1.0.0.0 | fe8269c0-eb0a-43d0-9032-c90f44f3136c | 4FACB44948B84BDB7A8FE9E336A390D92E4EF58684609F04B0F54A349B161A7B |

실제 최종 Debug DLL과 고정 KamiToolKit의 참조를 고정 컴파일 참조wasdcxz15.0.3.7과 비교했다. 핵심245타입 중238개 동일/7개 차이, 관리형 포함291타입 중277개 동일/14개 차이다. 참조1,172멤버(핵심1,056개) 누락0이며 바뀐 참조는 아래 기존3개뿐이다. 비교 스크립트는 차이/상위 참조를 보고하고 실패하므로 자동 통과로 기록하지 않는다.

- `ImRaii.FontDisposable.InternalPush`: 폰트 처리 IL 차이이며 Tweak의 네이티브 UI에서는 호출하지 않는다. 기존 dal4kr에서 보였던 AddonArgs/지도 타입 차이는 사라져 컴파일 기준 및 지원 wasdcxz15.0.3.9와 동일해졌다.
- `ItemOrderModule.GetActiveRetainerSorter`, `RaptureAtkModule.ChangeUiMode`, `AtkUnitBase.UnsubscribeAtkArrayData`와 `IconSubFolder` 상수는 이미 검토한 dal4kr15.0.3.7과 동일하다. 미사용 함수와 한국/중국 아이콘 enum 차이의 기존 근거를 따른다.
- `RaptureHotbarModule.ExecuteSlot`은 기존 dal4kr와 동일한 참조 차이다. 고정 KamiToolKit의 생성하지 않는 HotbarNode에만 있고 Tweak은 실행하지 않는다. `NumberArrayData`의 미사용4함수 추가는 기존 지원 wasdcxz15.0.3.9와 전체 기술 정보가 동일하다.
- Lumina/Lumina.Excel은 기존 지원 dal4kr15.0.3.7과 파일 SHA256 자체가 같다. 따라서7개 시트의 typeHash 차이와 직접 참조 Item.AdditionalData/PreHandler.Target 차이는 기존 검토 그대로다. Lumina1.0.0.0 표기에 대한 상위 AssemblyRef2개도 그대로이며 새 호스트에서의 해결은 실제 로드 확인 대상으로 남긴다.

`15.0.3.9-dal4kr` 프로필은 위 세 모듈의 정확한 MVID/SHA256 전체 조합으로만 선택한다. 게임SHA, 각 화면 형태/품목/원본 행 검증은 유지한다. SDK/API/컴파일 참조/lock/submodule 변경은 없다. 새 조합과 기존 두 조합 각각 같은 KR 실행 파일 SHA/원본10명령/모듈 조합 일치를 오프라인으로 확인했다. 보고서는 `.cache/native-dal4kr-15.0.3.9/final-comparison.json`이다. 실제 게임 로드·화면 확인은 대기다. 사용자 요청으로 게시한0.2.0.4 고정 Release DLL에서도 전체 메타데이터가 조사 결과와 같음을 확인했으며 게시 근거는 [배포 확인](RELEASE.md)을 따른다.

### 0.2.0.1 한국 Dalamud 배포판 차이 대응 (2026-10-28)

기존 경고는 게임 검사 전 FFCS MVID 하나를 개발 환경 값과 비교하면서 발생했다. 같은 한국 게임이어도 dal4kr의 다른 빌드 DLL은 거부됐다. 기존 Fisher 조사 파일을 읽기 전용으로 재사용하되, 낚시 타입의 검증을 Tweak의 UI 지원 근거로 대체하지 않았다.

| 지원 조합 | 모듈 | AssemblyVersion | MVID | SHA256 |
|---|---|---|---|---|
| wasdcxz15.0.3.9 | Dalamud | 15.0.3.9 | 1e8f635a-5365-40f6-b920-830d6a816d43 | 7255103B62A1BB3687492B78E8FAF4B91BD969826B39D76602D0A861C92E158D |
| wasdcxz15.0.3.9 | FFXIVClientStructs | 7.57.0.9415 | 93c1ca2b-80f3-43ef-9d58-ab0e99bb8909 | 052AC3B331F6DFC2A72B58D61506A1A696A68EB13416B3C3D514FAFDF5EB4F40 |
| wasdcxz15.0.3.9 | InteropGenerator.Runtime | 1.0.0.0 | a296f580-78c4-4617-afef-e85be4bdfe55 | 04C4C92CD05506EDFED442EEE9D331734AF7B9E151A572964365B80EF38B9422 |
| dal4kr15.0.3.7 | Dalamud | 15.0.3.7 | b4e06917-bffd-4e12-89da-82bde6b4c73e | B92AC6B5BB51BB874590EB2E5D4BCDD1F9328786AB3D8A5D493583EFFD447926 |
| dal4kr15.0.3.7 | FFXIVClientStructs | 7.57.0.9700 | 3b9613ec-ea8d-4d0e-9c1e-6f91f3b46d50 | 0E9F985538F800F75B4AABADE1C443F3537D93C695BE368852CA44D9A7D3371E |
| dal4kr15.0.3.7 | InteropGenerator.Runtime | 1.0.0.0 | d5357f8d-0f92-464f-8936-de6fe0816b4d | A75408D83A54D2ED59B3BF178A0474772CE86F4046F2645E76C2B84AE232B178 |

출처는 [wasdcxz 고정4157460b](https://github.com/wasdcxz/dalamud.assets/tree/4157460b5b57c6196b6ee66b363dabdbf1d67758)의7.57-15.0.3.9.zip(SHA256258B3C61A15369D0F902A166A31085132C948F5921DDB62829B5FD57A9B2D218)과 [dal4kr 고정8a3c698b](https://github.com/dal4kr/Dalamud.Resources/tree/8a3c698bc4697e8a68013e156417c31a50a5e263)의Dalamud/latest/Release.zip(SHA256EA859A153E3C1235F6E773EF04B94406680E376A835D3BC158C1D5C03E2AC79C)이다. Fisher에 보관된 실제 파일의 세 해시를 다시 읽어 위 조합과 일치함을 확인했다.

원본 소스 revision은 wasdcxz의 Dalamud0bbbf4f5dc1106da40bd51e0f5184a12ffd1024c/FFCS479ea936b7ecbf6436c16941ece9c9ee83cba494, dal4kr의 Dalamudbf26bb431c62f167abf233a0e983d4e059c9dd1b/FFCS5ecda407c82597890b41f704e087381fc7dfb912이다. Dalamud는AGPL-3.0, FFCS/Interop은MIT이다. SDK DLL은 오프라인 참조·메타데이터 조사용이며 제품 ZIP에 포함하지 않고 구현 소스도 복제하지 않았다.

`scripts/compare-native-runtime.ps1`은 실제 DuckbillTweak.dll과 고정 KamiToolKit.dll의 외부 타입 참조(라이브러리의 미사용 노드 포함)를 읽는다. 핵심3모듈의245개와 Lumina/Lumina.Excel의46개, 총291개 루트 타입/중첩 타입의 크기·정렬·필드 오프셋·상수·특성·메서드 시그니처·이름으로 해석한IL·로컬·예외 구간을 비교한다. 실제 사용 멤버는1,163개(핵심3모듈1,047개)이며 존재·구현·두 산출물의 AssemblyRef도 대조한다. 전체 전이 의존성이나 게임 실행을 증명하는 자동 허용 도구는 아니다. 차이가 있으면 보고서를 쓰고 실패하므로 아래 검토를 반드시 병행한다.

컴파일 기준wasdcxz15.0.3.7 대비 dal4kr15.0.3.7은236/245개 동일,9개 차이이며 사용 멤버1,047개 누락0/상위 참조0이다. 차이 검토:

- `AddonArgs`: 관리 backing field/초기화·이름 getter·미사용 pointer helper 차이. Tweak/KamiToolKit이 참조하는 addon pointer getter는 동일하다. 이름 getter/생성자/setter는 호출하지 않는다.
- `ImRaii`: 미사용 PushClipRect 오버로드 및 내부 Font/Header 처리 차이. Tweak의 설정/상점/입찰은 네이티브 UI이며 이 ImGui 디버그 경로를 생성·표시하지 않는다.
- `AgentMap`/`Atk2DAreaMap`: 미사용 지도 tooltip/position 필드·함수 삭제. Tweak은 지도 UI를 사용하지 않는다.
- `ItemOrderModule`/`RaptureAtkModule`/`AtkUnitBase`: 변경 함수는 각각 GetActiveRetainerSorter/ChangeUiMode/UnsubscribeAtkArrayData와 해당 주소 초기화이다. 실제 참조 멤버에는 없으며, 사용하는 필드 오프셋·함수/가상 함수는 동일하다.
- `RaptureHotbarModule.ExecuteSlot`: 유일한 변경 참조 멤버이나 KamiToolKit의 미사용 HotbarNode에만 존재한다. Tweak은 해당 노드를 생성하거나 게임 핫바/행동 실행을 호출하지 않는다.
- `IconSubFolder`: 한국/중국 enum 상수 차이. 고정 KamiToolKit은 한국 상수를 하드코딩하지 않고 현재 TextureResourceManager.IconLanguage를 읽어 동일한 GetIconPath에 전달하며, 실패 시 공통None만 사용한다.

같은 기준 대비 기존wasdcxz15.0.3.9는244/245개 동일, NumberArrayData에 미사용4개 함수만 추가됐다. 사용하는 멤버는 모두 동일하며 두 후보 모두 원본 KR 실행 파일 SHA256과 말풍선/compact map/selected tab의7개 명령이 일치했다. 프로필은 세 모듈 전체 조합으로만 선택하고, 혼합/변형 DLL은 거부한다. 기존 각 화면의 UI 형태·원본 인덱스/품목 재검증·Framework/수명/Unknown 처리는 그대로다. 실제 친구의 현재 DLL이 보관 조합과 같은지와 인게임 대표 동작은 확인 대기다.

추가 관리형 자료 대조: 실제 사용하는 Lumina 타입의 필드/메서드/IL은 모두 같았다. Lumina.Excel의7개 타입(Achievement/Action/ENpcBase/Item/PreHandler/Quest/TopicSelect)은 각 메서드의정수`typeHash`만 달랐다. 타입 후보 배열·행/열 오프셋·Sheet 특성과 호출은 동일하며, 동일한`RowRef.GetFirstValidRowOrUntyped`→`ExcelModule.FindRowInterval`의 캐시 키 인자임을 IL에서 확인했다. 이중 Tweak의 직접 참조는 Item.AdditionalData/PreHandler.Target2개이다. 관리형까지 합친 결과는 dal4kr275/291개 동일·16개 차이, 기존wasdcxz283/291개 동일·8개 차이이며 누락 멤버는 모두0이다.

dal4kr의 Lumina는AssemblyVersion1.0.0.0/MVID43c52c26-69c9-4a75-9af8-5a520f3ad2f1/SHA256B3CDD54311E4AD9D7713389100B7798AF86F444A0C03A2A337DE70723521CD24로 표기된다. 해당 배포판 자체의 Lumina.Excel7.0.0.0(MVIDb63cafa6-ea4f-4c68-9278-1b4af7278a13/SHA256DF7D5E431E897D2096FF14C0280B631396CD6EB297BECD130EBC29A1071942C0)도Lumina7.0.0.0을 참조한다. 따라서 비교 스크립트는 Plugin/KamiToolKit의 Lumina AssemblyRef2개를 추가 검토 대상으로 보고한다. 버전 표기만으로 이 배포판의 로드 실패/성공을 단정하지 않으며, 실제 호스트에서 이 의존성이 해결되는지는 인게임 로드 확인에 포함한다. 다른 직접 비시스템 의존성(Core/KamiToolKit/Lumina.Excel/ImGui/Serilog)의 버전·공개 키는 두 환경에서 충족된다. SDK DLL 수정·혼합 참조·자체 Lumina 배포나 런타임 로더 변경은 하지 않았다.

컴파일은 두 런타임보다 높지 않은wasdcxz15.0.3.7(FFCS7.57.0.9386) 전체 참조를 사용한다. 고정 출처/ZIP 및 세 모듈 SHA는`build/dalamud-reference.json`에 있고 `prepare-dalamud-reference.ps1`이`.cache`에만 복원한다. 현재 설치15.0.3.9로 빌드하면 Dalamud AssemblyRef가15.0.3.9가 되어 친구15.0.3.7에서 로드가 막힐 수 있으므로 csproj에서도 고정 컴파일 버전을 검사한다. `resources`는 실제 Plugin/KamiToolKit 두 DLL의 AssemblyRef도 확인해 높은 버전으로 남은 UI DLL의 혼입을 거부한다. 이는 컴파일용 참조이며wasdcxz15.0.3.7을 새 런타임 지원 대상으로 등록한 것은 아니다. SDK/API/lock/submodule은 변경하지 않았다.

비교 스크립트의 메타데이터 기술 루틴은 동일 저자의 [DuckbillFisher 고정0d8fcd6a](https://github.com/PlatyDuckbill/DuckbillFisher/blob/0d8fcd6a1efc01197d630ff0c32ddbcf59a0878a/scripts/compare-native-runtime.ps1)에서 이식하고 고정 낚시 목록을 실제 Tweak/KamiToolKit 참조 수집·사용 멤버/AssemblyRef 검사로 확장했다. 해당 저장소는 별도 라이선스 파일이 없는 동일 사용자 작업물이며 원본을 변경하지 않았다. 이 개발용 스크립트는 제품 ZIP에 포함하지 않는다.

## 상점

### 0.1.1.8 원본 렌더러 풀 대응

0.1.1.6/7의 렌더러 중복 금지 검사는 잘못된 계약이었다. KR SetupItem RVA6A1FF0은 CategoryItemRendererList의 템플릿과 NodeList를 NodeCount+1로 순환 배정한다. 행 수가 화면 행 수보다 많으면 서로 다른 품목이 같은 렌더러를 공유한다. 이를 비정상으로 거부하면 필터가 비활성화되고, 검증만 제거한 채 OwnerNode를 숨기면 표시할 다른 품목도 사라진다.

현재 구현은 원본 Items 벡터/UIntValues/StringValues/ItemIndexMap/인덱스를 유지한다. 카테고리별 원본 렌더러 풀을 확인하고 표시 품목에만 순환 배정한다. 숨긴 품목은 높이0·IsHidden·Disabled와 그림/충돌이 없는 단일 전용 렌더러를 사용한다. 따라서 Height0 항목도 순회하는 native draw가 다른 품목을 중복해서 그리지 않는다. 숨김 전용 렌더러는 고정 KamiToolKit의 ComponentNode로 생성하며 표시 행을 재구성하지 않는다.

평면 구조, 고유 항목, 유효한 풀/노드, 한 뷰포트에 렌더러가 중복 배정되지 않는 용량을 검사한다. 충돌 노드가 없는 평면 SectionHeader 플래그는 허용하되 Group/LastItemInGroup/Depth는 거부한다. 실제 표시 범위의 렌더러만 보이게 하고 refresh/해제 전에 원래 연결/높이/숨김/비활성/표시를 복원한다. 제외 품목이 없으면 변경하지 않는다.

KR Populate RVA6A62C0의 6A631D는 원본 인덱스를 Renderer.ListItemIndex에 저장하며 6A6405–6A6412에서도 같은 인덱스와 현재 행 렌더러를 populator에 전달한다. 이 경로와 native 수량·미리보기·구매 이벤트를 그대로 사용하고 구매 명령/콜백을 생성하지 않는다. 목록 계산/스크롤 보정은 설치된 바인딩을 사용한다. 새 런타임 주소/후크는 추가하지 않았다.

ShopExchangeCurrency의 KR ULD는680×528, node20/component1022 TreeList, header13/구분선19이며 새로운 컨트롤은y92에 들어간다. AgentShop의 addon ID/수신 배열/3배 비용 배열 계약을 검증한다. 0.1.2.10부터는 원본 TreeList와 AgentShop의 전체 개수가 같다는 조건을 제거하고 아래 탭 매핑을 사용한다. 정보 미확인/필터 실패에서는 원본을 복구하고 체크박스를 잠그지 않아 해제할 수 있다.

0.1.2.10 우주 화폐 탭 매핑: 실제 로그의 AgentShop51개에 대해 TreeList가 기타20개/장비31개로 나뉜다. 동일 KR 실행 파일의 refresh RVA12C17A0, 렌더12C2D10, 툴팁12C2B74를 정적으로 확인했다. 원본 행 UIntValues[1]은 compact index이며 addon+21A0의60칸 map을 통해 현재 페이지 품목 index로 연결한다. addon+3B38은 페이지 품목 수, addon+320+index×40의 첫 두 uint는 아이템ID/아이콘이다. AtkValues[3] 수량, [42A+index] 아이템ID, [D4+index] 아이콘과 교차 검증한다. 모든 범위/타입/중복을 검증하고 AgentShop에서는 raw Item ID가 유일한 품목만 연결한다(0.1.2.11 수정). UIntValues[0]=0/[2]=0인 단일 보상 행만 지원하며 모호한 행은 추정하지 않는다. 원본 행 순서·map·UIntValues·구매 이벤트는 수정하지 않는다. 관리 카탈로그는 전체 AgentShop 페이지를 해석한 후 표시된 탭 인덱스만 선택한다. 계산과 숨김은 표시된 TreeList 행 순서를 사용한다.

이 추가 읽기는 기존 정확한 게임 SHA/MVID 가드 뒤의 UI update에서만 수행한다. native-contract 오프라인 검사는 map 기록 RVA12C1B59, 렌더 소비12C2D5F, 아이템ID 읽기12C2BAB의 원본 명령도 대조한다. 런타임 시그니처/후크/게임 입력을 추가하지 않았다.

체크 상태는 Configuration v3에서 제거했다. MerchantService의 addon 수명별 메모리 상태 하나를 원본 상점/재화 창이 공유한다. 0.1.2.11 사용자 정정에 따라 상점 Attach와 실제 탭/분류 변경에서 두 bool을 false로 초기화하고 finalize/기능 해제/세션 변경에서 제거한다. 재화 OpenForShop은 기존 체크를 이어받는다. 같은 탭의 수량/잔액/목록 갱신은 상태를 유지한다. 이름은 모두 '보유 수집품 제외'이며, 마테리아/소모품 등을 유지하는 기존 목적 분류와 짧은 툴팁은 그대로다.

0.1.2.11 실기 실패/수정: 코르보 서책과 우주 화폐의 'no unique agent item'은 AgentShop.ItemIconId와 addon이 렌더하는 icon까지 일치하도록 요구한 추가 조건에서 발생했다. 연결은 품질을 포함한 raw Item ID가 유일한지로 판단한다. addon 내부 AtkValues/record의 ID/아이콘 교차 검사, 범위/중복/map 검증은 유지한다. 아이콘은 표시 자료이며 품목 정체성 키가 아니다. 서책 실제18행의 연결/가격 복원과18→3행 필터 적용을 새 로그에서 확인했다. 원본 행/구매 인덱스는 그대로다.

실제 탭은 화폐 교환 addon+318의 selected tab byte(0..3, setup RVA12C1416/선택12C26CB), InclusionShop은 선택한 category/subcategory/상점ID로 관측한다. 이름이 다른 item exchange 페이지도 구분한다. 단순 PreRefresh/원본 행 수 변화/재화 버튼은 초기화 계기로 사용하지 않는다. 중간0행 TreeList를 정상 계산 페이지로 발행하지 않는다. 대기 중 종류 드롭다운에도 '모든 품목 종류'를 표시한다.

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
dotnet run --project tools/GameDataProbe -- native-contract '게임 경로/ffxiv_dx11.exe' '확인할 실제 KR Dalamud 런타임 폴더'
dotnet run --project tools/GameDataProbe -- '게임 경로/sqpack' uld ui/uld/Shop.uld
./scripts/compare-native-runtime.ps1 -BaselinePath $env:DALAMUD_HOME -CandidatePath '확인할 실제 KR Dalamud 런타임 폴더' -PluginDirectory src/DuckbillTweak.Plugin/bin/Debug -ReportPath .cache/native-comparison.json
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

2026-10-28 지연 열기 수정: 관측기가 기존 기록의 존재를 전제로 하던 동작을 제거했다. Framework에서 새 전리품의 상자/슬롯/raw ID를 발견하면 관리 기록과 당시 명단을 생성한다. 먼저 수신한 아이템 로그의 열린 기록이 유일하고 다른 전리품에 연결되지 않았다면 한 번 연결한다. 뒤늦은5183은 같은 열린 기록을 다시 만들지 않으며, 실제 동시 중복은 네이티브 식별자로 판정한다. 본인 선택은 매 Framework update, 명단/소멸 확인은 기존1초 간격으로 관측한다. 이미 연결된Awarded 슬롯도 기록 연결을 유지하고 종료 표시한다. 현재 슬롯에 남아 있는 기록은10분 정리 대상이 되지 않는다. 창은 기록을 생성하지 않으며 현재 유효 스냅샷의 연결을 통해 완료 기록도 표시한다. 연결이 아직 준비되지 않았다면 원본 행/raw ID/세대를 재검증하면서 최대1초 대기하고, 실패 시 빈 기록을 만들지 않고 진단을 남긴다. 같은 ID의 과거 완료 기록만으로 현재 보상을 추정하지 않는다. 세션/기능 해제 시 연결과 기록을 함께 비운다.

기존 KR 실행 파일의AgentLoot 갱신RVA1020190은 Loot 항목 수 조회에완료 제외=false를 넘기고, 항목 조회RVAC39360은16슬롯/44(hex)stride의 같은 배열을 읽는다. 따라서 원본 목록에 남아 있는Awarded6과 기존 기록의 연결을 유지한다. 이번 수정은 이미 검사한 필드/바인딩만 사용하고 새 오프셋·시그니처·후크는 도입하지 않았다. 출처는 기존 로컬 실행 파일과 고정 KR 바인딩이며 외부 코드 추가는 없다.

관측 명단은 항목별로 남기며 공개파티/연합 accessor로만 갱신한다. 진행 중2회 연속 불참은이탈 표시,재관측은복귀다. 기존 숫자와선택은 유지한다. 없음/부재/미수신을포기나불참권한으로 바꾸지 않는다. 숫자순위는Need가Greed보다우선하고,같은종류에서큰값 우선/동점공동순위다. Unknown/Pass/숫자미수신/중복귀속불가에는 순위가없다. 상위3명 색상과등수를 함께 표시하고 실제획득자 표시는로그만 따른다.

뷰 좌표:420×500 또는1164×500,아이템y48,진행y94,관측통계y120,그룹y148/열제목y174,목록y202–426,설명y438/462. 각열360폭/374간격의8행 높이로추가행은스크롤한다. 각열은고정관측순이며순위에따라명단이움직이지 않는다. 다른연합원두열은실제파티A/B/C가아니다.

0.1.2.1: 현재 원본 행의 Disabled/구매 조건 미확인은 필요 재화 계산의 제외 근거가 아니다. 현재 addon에 표시되고 수량/가격을 읽은 교환식은 비용 예상에 포함하며 구매 가능 여부는 별도로 안내한다. 시트의 숨겨진 다른 교환식을 가져오거나 자동 구매하지 않는다. 품목 분류는 원본 행과 계산 목록에 공통 적용하고 지불 재화에는 적용하지 않는다. 보유 수집품과 제외 재료가 섞인 묶음은 각 보상이 두 제외 조건 중 하나에 해당하면 숨긴다. 원본 행/인덱스/구매 함수의 변경은 없다.

0.1.2.3 재화 창: 기존760×586 안에서 재화 이름176px, 필요80px, 사용 가능/개인 보관/총보유/추가 필요 각90px로 열을 맞췄다. 행은30px 유지, 숫자13px, 이름은 줄임/전체 툴팁. ListItem의 전체 입력 비활성화를 제거하고 선택/MouseDown만 차단하여 위치별 툴팁을 사용할 수 있게 했다. 목록 스크롤/원본 상점 수명 연결을 유지하며 네이티브 구매/이동 입력을 추가하지 않는다. 보관 조회는 관리 스냅샷이고 별도 네이티브 주소/시그니처 변경은 없다.

0.1.2.7 입찰 레이아웃: 순위 열 및 footer/legend 노드를 제거했다. 일반 파티는 그룹 제목 없이 y148열 제목/y172구분선/y176목록, 연합은 y148그룹/y174열 제목/y198구분선/y202목록이다. 이름x2너비180/선택x190너비104/주사위x300너비44, 행28px. 목록1~8행+아래20px로 창을 줄이며 고정 KamiToolKit SetWindowSize로 WindowNode까지 함께 변경한다. 한 번에8행을 넘는 기록은 스크롤한다. 항목별 공개 결과 읽기/본인 선택 읽기/호환성 가드/기존 우클릭 메뉴는 유지한다. 동적 높이 및 새 인원 요약의 실기는 별도 확인 대상이다.

0.1.2.8: AgentShop의 현재 addon/수신·비용 배열 계약은 유지한다. ShopName CString은 검증한 필드에서 최대512바이트만 읽고, 원본 페이지 전체의 raw item ID/수량/기존 확인 비용을 관리 카탈로그에 연결한다. 원본 벡터/행/구매 콜백은 변경하지 않는다. CurrencyManager는 기존 FFCS 읽기 함수만 사용하며 게임 SHA/MVID 검사가 통과한 Framework 설정에서 활성화한다. wallet에서 등록을 확인한 수량을 원본 상점/가방 잔액보다 우선하고, 미확인 화폐형 재화를 가방 부재만으로0 처리하지 않는다.

0.1.2.9는 직전의 원본 배열과 시트 순서가 같다는 가정을 제거했다. AgentShop.ItemReceive의 실제 순서가 시트와 달라도 각 원본 행에 올바른 가격을 돌려주도록 관리 카탈로그만 수정했다. NativeTreeFilter의 원본 벡터/연결/구매 인덱스는 변경하지 않았다. 전체 품목의 다중집합과 각 행의ID/수량/이미 확인된 비용을 사용하며, 정렬된 카탈로그 인덱스를 게임 클릭 인덱스로 보내지 않는다.

## 2026-10-28 표시 순서와 충돌 목록 갱신

로컬 KR 실행 파일/설치 바인딩의 읽기 전용 분석을 추가했다. MiniTalk/NamePlate ULD의 root1·Custom1001 노드2와 실제 레이어 목록 범위를 확인한 뒤, 지원 환경에서 로그인한 동안 별도 옵션 없이 일반 채팅/PvP 공통으로 SetAddonOrderAfter를 사용해 MiniTalk를 이름표 앞에 둔다. PvP 말풍선 허용 패치와 가림 방지를 분리했다. SetAddonDepthLayerAndOrder는 같은 레이어의 순서를 바꾸지 않으므로 살아 있는 원래 인접 화면을 다시 조회해 복원한다. 마지막 항목 복원은 즉시 삽입 두 번으로 원래 이웃 관계를 되돌린다. 지역 변경/로그아웃/플러그인 종료 시 이전 상태를 비운다. 가림 방지는 버그 수정으로 기본 적용하며 추가 옵션/설정 필드는 없다. 기본 말풍선 설정은 유지한다. 오버레이/글로벌 주소는 없다.

KR 충돌 수집677A20은 적용된 표시 상태(DrawFlags bit21)도 읽는다. 상점은 표시 변경 직후 갱신에 더해 PostDraw에서 요청된 화면 주소/ID를 검증하고 한 번 더 충돌 목록을 재생성한다. 이전 행 객체가 사라져도 살아 있는 카테고리 풀의 표시 상태는 복원한다. 풀 표시 복원을 끝낸 뒤 현재 스크롤 위치에 맞춰 네이티브 레이아웃을 계산한다. 화면 종료 시 대기 요청을 비우고 dispose에서 공개 IAddonLifecycle 구독을 해제한다.

분석 지점: SetAddonOrderAfter6816A0/인자6816B4, 목록 삽입67AD70, 표시 순서 계산680D50, 레이어·순서 복원67C8E0, 충돌 목록676900/하위677A20(bit조회677ABB). 제품은 설치된 FFCS 함수를 사용하며 주소를 직접 호출하지 않는다. GameDataProbe native-contract는 기존7개에 원본 명령2개를 추가해9개를 대조한다. 게임 SHA/세 모듈 프로필/화면 형태 가드는 완화하지 않았다.

추가 외부 소스 도입 없음. 기존 고정 SimpleTweaksPlugin c039932e40b0c752c09005cc4fdc362b6b786da4의 IncreaseChatBubbleLines를 말풍선 관리 경로 조사에 참고했으며 AGPL-3.0 구현/시그니처/구조체를 복사하지 않았다. 출처와 고정 revision은 DEVELOPMENT.md의 기존 항목을 따른다. MiniTalk/NamePlate 판정과 순서 함수는 로컬 KR 자료에서 별도로 확인했다. 정적 대조를 인게임 화면 성공으로 간주하지 않는다.

0.2.0.3 최종 검수: 교차 레이어는 SetAddonDepthLayer(로컬 KR67C790, 이전 목록 제거67C885)를 먼저 사용하고, SetAddonOrderAfter는 같은 레이어에서만 호출한다. 순서 함수가 레이어 비트를 먼저 바꾸는6816D3–6816FB 경로를 직접 교차 레이어 이동에 사용하지 않는다. null/중복/용량 검사를 보강했고 원본 명령 검사는10개로 늘렸다. 레이어 이동 후 인접 화면을 기준으로 복원하며 인접 화면이 사라졌다면 레이어만 복원한다. 전체 런타임 메타데이터 차이는 기존 배포 보고서와 전체 설명이 일치하며 새10개 참조는 모두 동일하다. 원래 말풍선 제보의 원인은 미확정이며 표시 순서 보정의 실기 검증은 대기다.
