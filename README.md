# DuckbillTweak

**파판의 익숙한 화면에 필요한 편의 기능을 더합니다.**

한국 FFXIV용 Dalamud 플러그인입니다. 상점에서 수집할 품목을 찾고, 필요한 재화를 계산하고, 입찰 결과를 확인할 수 있습니다. 필요한 기능만 따로 켜서 사용합니다.

## 주요 기능

| 기능 | 할 수 있는 일 |
|---|---|
| 보유 수집품 제외 | 이미 보유·등록한 장비와 수집품을 상점 목록에서 제외합니다. 반복 구매하는 소모품·재료는 유지합니다. |
| 수집 위주 보기 | 장비와 등록 수집품 위주로 봅니다. 확인된 소모품·마테리아·재료·하우징 등을 제외합니다. |
| 필요 재화 | 현재 상점 분류의 품목을 각 한 번씩 교환할 때 필요한 재화와 부족분을 계산합니다. |
| 보관 기록 연동 | Allagan Tools가 있으면 현재 캐릭터의 집사·초코보 가방 등의 보관 기록도 참고합니다. |
| 아이템별 입찰 현황 | 원래 우클릭 메뉴에서 참여자의 선택과 공개된 주사위 결과를 봅니다. 일반 파티는 한 열, 연합은 세 열로 표시합니다. |
| PvP 전장 말풍선 | 게임의 기본 대화 말풍선 설정을 따르는 PvP 말풍선을 켤 수 있습니다. |

설정·필터·결과 화면은 게임의 UI 컴포넌트와 글꼴을 사용합니다.

## 설치

**0.2.0.0은 첫 배포 준비 중입니다. 현재 릴리스 초안은 공개 설치 대상이 아닙니다.**
공개 후 Dalamud 설정의 커스텀 플러그인 저장소에 아래 URL을 추가하고, 플러그인 목록에서 **DuckbillTweak**를 설치합니다.

```text
https://raw.githubusercontent.com/PlatyDuckbill/DuckbillTweak/main/repo/pluginmaster.json
```

이후 업데이트도 같은 설치 목록에서 받을 수 있습니다. 배포 전에는 설치 목록을 빈 상태로 유지합니다.

## 사용

1. `/dbtw` 또는 `/duckbilltweak`로 설정을 엽니다. 모든 기능은 처음에 꺼져 있습니다.
2. 상점 기능을 켜고 상점을 다시 열면 `보유 수집품 제외`, `수집 위주 보기`, `필요 재화`가 표시됩니다.
3. 상점을 새로 열거나 실제 탭·분류를 바꾸면 두 체크가 해제됩니다. 필요 재화 버튼은 현재 체크를 그대로 이어받습니다.
4. 입찰 아이템의 원래 우클릭 메뉴에서 `입찰 숫자 보기`를 선택합니다. 미리보기 등 기존 메뉴도 유지됩니다.
5. 설정의 `업데이트 내역` 또는 `/dbtw changelog`에서 최근 릴리스의 한글·영문 안내를 봅니다. 읽지 않은 안내는 설정 버튼에 `NEW`로 표시합니다.

두 상점 체크는 역할이 다릅니다. **보유 수집품 제외**는 보유 여부로, **수집 위주 보기**는 품목 용도로 걸러냅니다. 함께 켜면 아직 보유하지 않은 수집 대상 위주로 볼 수 있습니다. 분류나 보유 여부를 확인하지 못한 물건은 임의로 제외하지 않습니다.

## 지원 환경과 알아둘 점

- **Dalamud API 15**, 검사한 한국 클라이언트 **2026.10.06.0000.0000** 및 KR 바인딩을 대상으로 합니다. 게임·바인딩이 달라지면 호환성 확인까지 네이티브 기능이 꺼질 수 있습니다.
- `Shop`, `InclusionShop`, `ShopExchangeItem`, `ShopExchangeCurrency`의 지원 구조를 사용합니다. 모든 특수 상점을 지원하는 것은 아닙니다.
- 필요 재화는 **현재 상점 분류에서 각 교환을 한 번씩** 계산합니다. 모든 분류 자동 순회나 최저 비용 최적화는 하지 않습니다.
- 보관 기록은 오래되거나 일부가 빠질 수 있습니다. 지금 사용할 수 있는 재화와 개인 보관 기록을 구분하며, 미확인을 0개로 표시하지 않습니다.
- 다른 사람의 주사위 숫자는 게임이 결과를 공개한 후 표시됩니다. `—`는 미수신이며 포기를 뜻하지 않습니다. 연합의 다른 두 열은 실제 A/B/C 파티 구분이 아닙니다.
- PvP 말풍선·8/24인 입찰·상점별 최종 확인 범위는 [검증 기록](docs/VERIFICATION.md)을 따릅니다.

[업데이트 내역](CHANGELOG.md) · [GitHub Releases](https://github.com/PlatyDuckbill/DuckbillTweak/releases) · [문제 제보](https://github.com/PlatyDuckbill/DuckbillTweak/issues)

## 개발

Windows, PowerShell 7, .NET SDK **10.0.402**, Dalamud.NET.Sdk **15.0.0**을 사용합니다. SDK와 의존성은 고정되어 있습니다.

```powershell
git submodule update --init --recursive
./scripts/build.ps1
```

빌드 스크립트는 잠금 파일 복원·컴파일·내장 자료 검사를 수행합니다. 자동 테스트나 게임 입력을 실행하지 않습니다.
개발 DLL은 `src/DuckbillTweak.Plugin/bin/Debug/DuckbillTweak.dll`이며, 같은 폴더의 DLL·Assets·라이선스도 함께 필요합니다.

[배포 절차](docs/DISTRIBUTION.md) · [현재 상태](docs/STATUS.md) · [확인 절차](docs/VERIFICATION.md) · [네이티브 UI 계약](docs/NATIVE-UI.md) · [구조·참고 자료](docs/DEVELOPMENT.md)

네이티브 UI에는 MIT 라이선스 [KamiToolKit](https://github.com/MidoriKami/KamiToolKit)을 고정 revision으로 사용합니다. [서드파티 고지](THIRD-PARTY-NOTICES.txt)를 참고하십시오.
