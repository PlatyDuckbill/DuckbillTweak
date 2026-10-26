# 구조와 참고 자료

## 코드 구조

- `src/DuckbillTweak.Core`: 보유 상태, 품목 필터, 교환식 선택과 재화 합계, 전리품 관측 기록. 게임 의존성 없음.
- `src/DuckbillTweak.Plugin`: Dalamud 서비스 수명, 설정/명령, Framework에서 관리 값으로 복사하는 관측기와 네이티브 게임 화면.
- `tools/GameDataProbe`: 설치된 게임 데이터를 오프라인으로 읽는 도구. 전리품 로그 매크로와 재화 참조를 검사.
- `scripts/build.ps1`: 잠금 파일 복원/빌드 후 GameDataProbe resources로 실제 DLL의 내장 자료를 검사. 리소스 검사는 PE 메타데이터/바이트만 읽으며 게임 서비스나 플러그인 생성자를 실행하지 않음.
- `data`: 버전을 지정한 특수 화폐 ID 관계. 런타임 원격 요청 없음.

각 기능은 설정값에 따라 독립적으로 동작하며, Plugin.DisposeAsync가 Framework에서 네이티브 노드/이벤트/패치를 복원한 뒤 화면과 라이브러리 해제를 기다린다. 설정은 Dalamud 플러그인 설정에 저장한다. 채팅/입찰/보유량 관측은 파일로 저장하지 않는다.

상인 카탈로그/가격은 NPC 변경 시 읽는다. 인벤토리/수집품 상태는 상점/재화 화면이 열렸을 때500ms 간격, 파티 명단은1초 간격이다. 네이티브 상점 상태는 addon update에서300ms 간격으로 복사한다. ImGui Draw나 별도 채팅 오브젝트/좌표 관측기는 없다. [네이티브 계약](NATIVE-UI.md)에 구조와 제한을 기록한다.

미확인 비용을 합계에서 빠뜨려 완전한 합계처럼 표시하지 않는다. 해당 교환식을 선택하면 부분 합계 경고가 나온다. 조건이 있는 판매는 기본 계산 선택에서 제외한다. 단가 할인이나 최저 비용 경로 최적화는 하지 않는다.

## 조사한 프로젝트

2026-10-26에 아래 고정 revision을 읽었다. 캐시의 참고 저장소는 Git에 포함하지 않는다. AGPL/GPL 프로젝트의 구현을 제품 소스로 복사하지 않았다. MIT KamiToolKit은 별도 의존성으로 도입했다.

| 참고 대상 | 고정 revision | 라이선스/참고 내용 |
|---|---|---|
| [VanillaPlus](https://github.com/MidoriKami/VanillaPlus/tree/17bddd82ca73e099f1a7af9b00c4ee13fa8a701c) | 17bddd82ca73e099f1a7af9b00c4ee13fa8a701c | AGPL-3.0. 기능별 설정·시작/종료와 상점/전리품 기능 구분 |
| [SimpleTweaksPlugin](https://github.com/Caraxi/SimpleTweaksPlugin/tree/c039932e40b0c752c09005cc4fdc362b6b786da4) | c039932e40b0c752c09005cc4fdc362b6b786da4 | AGPL-3.0. 개별 트윅과 일반 전투 말풍선 방식 조사. 글로벌 패치/시그니처 미도입, KR 실행 파일 별도 분석 |
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

## 전리품 계약

KR LogMessage1231은 lnum1=아이템, lstr2=입찰 종류, lnum3=숫자다. Dalamud 매개변수 인덱스는0부터 시작한다. 5183은 전리품 추가,5180은 선택 완료,1232/1233/5187/5188은 획득/분배다. 설치된 시트의 매크로가 필요한 참조를 포함하는지 확인한다.

다른 사람의 숫자는 입찰이 끝나 게임이 결과 로그를 보낸 이후 표시된다. 진행 중 다른 사람의 숫자를 미리 읽는 기능으로 설명하지 않는다. 참고한 LootView 고정 revision의 README Need/Greed rolls 설명도 결과 확정 후 공개를 명시한다. 로컬 실기에서도 선택5180 이후 숫자1231과 획득1232가 함께 수신됐다.

공개 로그에는 보상 슬롯 키가 없으므로 같은 raw item ID의 동시 드롭을 구분하지 않는다. SourceEntity의 이름과 HomeWorldId를 키로 사용하고 이름만 같은 플레이어를 합치지 않는다. 전리품에 없는 일반 획득 로그는 입찰 관측에 연결하지 않는다. 기능 활성화 이전이나 접속/지역 전환 이전의 기록은 복원하지 않는다.
