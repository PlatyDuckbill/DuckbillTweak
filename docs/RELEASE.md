# 배포 확인

## 0.4.0.0 · 감정표현 상태 표시

사용자가 감정표현 상태 아이콘·툴팁/보유 문구 개선 후 공개 배포를 지시했다. 개발판의 사각 아이콘/이름·보유 툴팁 대표 확인과 최종 위쪽 뾰족한 프레임/고정 ZIP의 실기는 구분하며 InGameVerified=false를 유지한다.

| 항목 | 값 |
|---|---|
| 소스·태그 | `33a955db4382ea4ab121f45f8562296dbeab59ef` · v0.4.0.0 |
| 고정 후보 | `artifacts/release/0.4.0.0/emote-status-20261116` · SourceDirty=false |
| ZIP | `DuckbillTweak-0.4.0.0.zip` · 512,887바이트/16파일 |
| ZIP SHA256 | `08ffc7413d31eaed59e660dae2eb062268ae9dcae448c3220709be2d8df4d488` |
| [공개 Release](https://github.com/PlatyDuckbill/DuckbillTweak/releases/tag/v0.4.0.0) | ID446950881 · draft=false/prerelease=false |
| 공개 시각 | 2026-11-16T11:29:25Z / 2026-11-16 20:29:25 KST |
| 설치 원본 | `214c65b1f1909917077ba8de928f51d08d3f843e` · repo/release.json |
| [공용 동기화](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/43858274165) | success · 공용 `ff0f4ca9f4668960c26a0af798bde0b1318c25d3` |
| 고정 확인 DLL | `.cache/release-validation/0.4.0.0/emote-status-20261116/DuckbillTweak.dll` |

- 고정 KR15.0.3.7로 별도→정규 Release 잠금 복원/빌드 경고0/오류0, 필수16파일·추출 해시·한국어 위성 이름/문화권/버전·한영114키/값 원본 일치·재화708바이트/28매핑·업데이트 내역·DLL 의존성과 공용 참조 상한을 확인했다. SDK/의존성은 변경하지 않았고 테스트는 추가/실행하지 않았다.
- 내장 한영·설치·GitHub 안내는 같은 대표2항목이다. prepare-release.ps1로 SourceDirty=false ZIP을 고정하고 새 태그/초안 자산 크기/digest·소스/본문을 대조한 뒤 공개했다. 기존 태그/ZIP은 수정하지 않았다.
- 인증 없는 ZIP·체크섬·태그 아이콘 HTTP200/원본 해시 일치 후 설치 원본을 갱신했다. 아이콘은 기존 승인본 SHA256 `853f8a8ce08e97332156e4ab5298927f73e135944223c77d766f3863dd103a3d`를 유지한다.
- 공용 sync.ps1 -Check가 세 공개 ZIP/매니페스트와 목록 일치를 확인했다. 2026-11-16T11:31:40Z에 공용 고정 커밋과 실제 main/pluginmaster.json에서 Tweak0.4.0.0·Fisher0.10.2.0·Calendar0.6.0.0의 각22필드가 소스와 일치했다. 최초 실제 URL의 이전 버전 응답은 재조회 후 새 버전으로 확인했으며 Relic 첫 배포 대기는 유지한다.
- 실제 설치/업데이트·새 윤곽/배율·본인/대상 호버/보유·일회성 종료/취소·언어/토글/지역/HUD 재생성/재로드는 확인 대기다. [개발판 근거](VERIFICATION.md#2026-11-16-감정표현-상태-표시-개발본)를 고정 ZIP의 실기로 확대하지 않는다.

게시 영수증·익명 자산·실제 공용 URL 대조는 `.cache/publication-0.4.0.0/`에 보존한다.

## 0.3.1.0 · 기능별 동작 상태

사용자 지시로 기능별 상태 안내·색 점과 정상 대기의 초록 표시를 공개 배포했다. 개발판 PvP/우클릭 따라하기 사용 가능 보고와 새 고정 ZIP의 실기는 구분하며 InGameVerified=false를 유지한다.

| 항목 | 값 |
|---|---|
| 소스·태그 | `7a90c48c5ce23a607f120a4626fb5afa39506338` · v0.3.1.0 |
| 고정 후보 | `artifacts/release/0.3.1.0/status-release-20261116` · SourceDirty=false |
| ZIP | `DuckbillTweak-0.3.1.0.zip` · 501,438바이트/16파일 |
| ZIP SHA256 | `7ee907de389a9d74b544273fcc06458f7805f45fd627bdf77a843dc9d450d590` |
| [공개 Release](https://github.com/PlatyDuckbill/DuckbillTweak/releases/tag/v0.3.1.0) | ID446792018 · draft=false/prerelease=false |
| 공개 시각 | 2026-11-16T08:52:46Z / 2026-11-16 17:52:46 KST |
| 설치 원본 | `cfcec81` · repo/release.json |
| [공용 동기화](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/43835152193) | success · 공용 `43e9069c5eabf3f0d2a8f90637716d5e4532d9e3` |
| 고정 확인 DLL | `.cache/release-validation/0.3.1.0/status-release-20261116/DuckbillTweak.dll` |

- 고정 KR15.0.3.7 Release 잠금 복원·빌드 경고0/오류0, 필수16파일/추출 해시·한국어 위성 이름/문화권/버전·한영107키/값 원본 일치, 재화708바이트/28매핑·업데이트 내역·DLL 의존성과 공용 참조 상한을 확인했다. SDK/의존성/계약 판정·게임 동작은 변경하지 않았다.
- 내장 한영/설치/GitHub 안내는 대표2·기타1의 같은3항목이다. prepare-release.ps1로 SourceDirty=false 후보를 고정했으며 테스트 추가/실행은 없다.
- 새 태그/초안 자산 digest·크기를 대조한 뒤 공개했다. 인증 없는 ZIP·체크섬·태그 아이콘 HTTP200/원본 해시 일치 후 설치 원본을 갱신했다. 아이콘은 기존 승인본 SHA256 `853f8a8ce08e97332156e4ab5298927f73e135944223c77d766f3863dd103a3d`를 유지한다.
- 고정 공용 커밋과 실제 main/pluginmaster.json에서 Tweak0.3.1.0·Calendar0.6.0.0·Fisher0.10.2.0의 각22필드가 설치 원본과 일치하고 sync.ps1 -Check도 통과했다. 기존 다른 항목을 유지했다.
- 실제 설치/업데이트·색 대비/상태·한영·호버/스크롤/배율·기능 토글·저장/재로드와 기존 게임 UI의 확인 대기는 유지한다. 패키지/다운로드 검사를 실기로 확대하지 않는다.

게시 영수증·익명 자산·실제 공용 URL 검사는 Fisher의 `.cache/release-20261116/`에 보존한다. 기존 태그·공개 ZIP은 수정하지 않았다.

## 0.3.0.1 · 배포판 한국어 파일 누락 수정

0.3.0.0 설치판이 영어로만 표시된다는 사용자 제보를 확인했다. 언어 선택/저장 로직은 정상이었으나 포장 허용 목록에서 `ko/DuckbillTweak.resources.dll`이 빠져 기본 영어로 돌아갔다. 아래 0.3.0.0의 한영86키/ko 위성 확인은 빌드 폴더 대상이었고, 해당 ZIP에 한국어가 포함됐다는 근거가 아니었다. 당시 패키지 검사가 이 누락을 잡지 못했음을 정정한다.

| 항목 | 값 |
|---|---|
| 소스 | `2c9e77b3911ec1297b10fae926e120582d1e8905` |
| 고정 후보 | `artifacts/release/0.3.0.1/korean-resource-fix` · SourceDirty=false |
| ZIP | `DuckbillTweak-0.3.0.1.zip` · 495,717바이트/16파일 |
| ZIP SHA256 | `558167c05e9530355670d67d953c80981dcb39d57b561a0985bd202856b6954b` |
| [공개 Release](https://github.com/PlatyDuckbill/DuckbillTweak/releases/tag/v0.3.0.1) | ID445993330 · draft=false/prerelease=false |
| 공개 시각 | 2026-11-15T11:03:05Z / 2026-11-15 20:03:05 KST |
| 설치 원본 | `32667e1` · repo/release.json |
| [공용 동기화](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/43707060639) | success · 공용 `0b81ed92494a8ca0643caadeb241cc2eb002f052` |

포장 목록은 번역 resx에 대응하는 위성 DLL을 포함한다. 패키지 리소스 검사는 추출본의 위성 존재·어셈블리 이름/문화권/버전·내장 리소스 이름과 한영 각86개 키/값의 원본 일치를 요구한다. 기존 공개0.3.0.0 ZIP은 새 검사에서 한국어 파일 누락으로 실패했고 새 ZIP은 통과했다. 별도→정규 Debug와 고정 Release 빌드는 경고0/오류0이다. 게임을 실행하거나 플러그인을 생성하지 않은 ResourceManager 조회에서 기존 ZIP의 한국어 `Settings` → 새 ZIP의 `설정` 및 한영 각86문구 일치를 확인했다. 설정 초기화·언어 강제 변경·게임 기능 변경·테스트 추가/실행은 없다. 실제 게임 내 설치/언어 전환은 대기이며 InGameVerified=false다.

익명 ZIP·체크섬·태그 아이콘 HTTP200/원본 해시 일치 후 설치 목록을 갱신했다. 공용 고정 커밋과 기존 `main/pluginmaster.json` URL 양쪽에서 Tweak0.3.0.1·Fisher0.10.0.1·Calendar0.5.0.0 세 항목과 각각22필드가 원본과 일치한다. 아이콘은 승인된0.3.0.0과 같은 PNG(SHA256 `853f8a8ce08e97332156e4ab5298927f73e135944223c77d766f3863dd103a3d`)를 새 태그에도 유지한다. 공개 증빙과 실제 공용 URL 대조 결과는 Fisher의 `.cache/tweak-0.3.0.1/`에 보관한다. 기존 태그/자산은 수정하지 않았다.

아이콘 미갱신 제보는 세 플러그인의 공개 PNG가 승인본 해시와 같은 것을 재확인했다. 설치된 Dalamud의 `PluginImageCache`를 읽어 확인한 결과, 일반 설치판은 플러그인 ID(없으면 InternalName)를 키로 메모리에 캐시하며 기존 항목이 있으면 URL이 바뀌어도 재다운로드하지 않는다. 전체 게임 종료/재실행으로 메모리 캐시를 비우면 다시 받는다. 서버 이미지 검사와 실제 사용자 화면의 캐시 갱신 확인은 구분한다.

## 0.3.0.0 · 공통 디자인과 새 대표 아이콘

사용자가 최종 아이콘으로 세 플러그인 배포를 명시 요청했다. 고정 ZIP의 인게임 확인은 하지 않았으며 InGameVerified=false를 유지한다. 이전 개발판의 음식/측후·춤 확인을 새 ZIP의 실기로 소급하지 않는다.

| 항목 | 값 |
|---|---|
| 소스 | `f25c997d40574ab0b4887c3fa8c1cb86d0810330` |
| 고정 후보 | `artifacts/release/0.3.0.0/context-release-20261115` · SourceDirty=false |
| ZIP | `DuckbillTweak-0.3.0.0.zip` · 489,680바이트/15파일 |
| ZIP SHA256 | `fa5803bb67aae14f59b73193f48da9cc2ad1ccb10cb360039d742f24b42ac1ff` |
| 태그 아이콘 SHA256 | `853f8a8ce08e97332156e4ab5298927f73e135944223c77d766f3863dd103a3d` |
| [공개 Release](https://github.com/PlatyDuckbill/DuckbillTweak/releases/tag/v0.3.0.0) | ID445983076 · draft=false/prerelease=false |
| 공개 시각 | 2026-11-15T10:44:13Z / 2026-11-15 19:44:13 KST |
| 설치 원본 | `f3fe6dc` · repo/release.json |
| [공용 동기화](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/43705600164) | success · 공용 `5cb4a754332c0b3ca7bba19edc4e27ba4a4cc679` |

설정/세부 설정·업데이트 내역의 ImGui 전환, 기능/하위 상세 계층과 스모크/펄·실시간 화면 조절, 제목줄 접기/닫기 겹침 수정 및 새 대표 아이콘을 묶었다. 준비 확인 음식·상점/필요 재화·입찰은 게임 UI를 유지한다. KR 고정 참조 Release 빌드 경고0/오류0·한영86키/ko 위성·실제 내장 자료/원본·API 참조, DLL/manifest/API/ZIP15파일·영문 내장/manifest/GitHub 노트8항목 일치를 확인했다. 포장 스크립트도 대표 기능/기타 개선을 함께 검사하도록 정리했다. 제품 테스트 추가/실행은 하지 않았다.

초안 자산 digest/크기·태그 소스를 확인한 뒤 공개했다. 인증 없는 ZIP·체크섬·태그 아이콘 HTTP200/원본 해시 일치 후 설치 원본을 게시했다. 고정 공용 커밋과 실제 main/pluginmaster.json 모두22필드가 설치 후보와 일치하고 Fisher0.10.0.1/Calendar0.5.0.0을 포함한 세 항목을 유지한다. 실제 Dalamud 업데이트/로드·제목줄 클릭, 세부 설정/팝업/휠·저장·실시간 조절·기존 네이티브 게임 UI 보존 확인은 대기다. 게시 영수증은 Fisher의 `.cache/release-suite-2026-11-15/`에 보존한다.

## 0.2.3.0 · 음식 알리미와 감정표현 따라하기 대기 개선

사용자가 개발판의 준비 확인 음식 표시, 극 조디아크 초읽기 중앙 경고·내 채팅 요약을 확인하고 춤 싱크도 “잘 되는 것으로 보임”이라고 확인한 뒤 업데이트 노트를 포함한 공개 배포를 명시 요청했다. 고정 후보를 재빌드 없이 게시했으며 고정 Release ZIP 자체를 실기 확인한 것은 아니므로 InGameVerified=false를 유지한다. 영식·절, 타인 시작 초읽기, 취소·재초읽기와 춤별 전수 검증은 별도다. [확인 기록](VERIFICATION.md#2026-11-15-음식-알리미와-춤-즉시-실행-경로-보완).

| 항목 | 값 |
|---|---|
| 소스 | `0c96c485382b18060c63e4c48e19591ebd863f44` |
| 고정 후보 | `artifacts/release/0.2.3.0/candidate1` · SourceDirty=false |
| ZIP | `DuckbillTweak-0.2.3.0.zip` · 474,118바이트/15파일 |
| SHA256 | `f30cdde0814c08380db6b6c2d66dcb7135b6e2e960c609b5dc5d8aaab0d62436` |
| 공개 Release | ID445658332 · `v0.2.3.0` · draft=false/prerelease=false |
| 공개 시각 | 2026-11-14T23:06:49Z / 2026-11-15 08:06:49 KST |
| 설치 원본 | `repo/release.json` · 0.2.3.0/API15 · LastUpdate1794665209 |
| 공용 동기화 | [workflow43651748780](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/43651748780) 성공 · 공용원본`a526371c6397f3a5c2454dea0a846b71d294ffec` |
| 실기 | 개발판 준비 확인·극 초읽기 중앙/채팅·춤 싱크 사용자 확인 · 고정 ZIP InGameVerified=false |

[공개 릴리스](https://github.com/PlatyDuckbill/DuckbillTweak/releases/tag/v0.2.3.0). 정규 Release 빌드 경고·오류0, 내장 한영 안내5항목·설치 manifest 일치, 패키지15파일·추출본 해시·리소스 일치. Release DLL의 조사 타입/음식 진단 문자열0개. 업로드 자산 digest/크기·태그 대상 커밋을 대조한 뒤 공개했다. 인증 없는 ZIP·체크섬·태그 아이콘 다운로드가 모두 HTTP200·원본 해시 일치임을 확인한 후 설치 원본을 작성했다. 설치 원본6413a82 push 후 공용 동기화를 실행했으며, 2026-11-14T23:10:27Z에 고정 공용 커밋과 기존 [공용 설치 URL](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json) 모두 인증 없는 HTTP200/0.2.3.0/API15/원본22필드 일치를 확인했다. Fisher0.9.0.0과 Calendar0.4.0.1도 목록에 유지됐다. 최초 기존 URL의 이전 버전 응답은 재조회 후 새 버전으로 확인했다. 게시 근거는 `.cache/publication-0.2.3.0/`에 보관한다. 공개 배포·공용 목록 반영은 완료이며 실제 공개 버전 설치·업데이트 성공은 HTTP 검사나 개발판 확인과 구분한다.

## 0.2.2.2 · 게임 SHA 가드 제거(KR 2026.11.03 대응)와 업데이트 내역 한 화면 표시

사용자가 개발판에서 측후 표시가 정상 동작하는 것을 확인한 뒤 배포를 명시 요청했다("일단 측후 잘되니까 배포 진행하고"). 고정 후보를 재빌드 없이 게시했으며 InGameVerified=false를 유지한다. 새 빌드의 PvP 말풍선·재화 교환 상점과 업데이트 내역 한 화면 표시는 실기 대기다. 근거는 [확인 기록](VERIFICATION.md#2026-11-10-게임-sha-가드-제거-kr-20261103-대응)을 따른다.

| 항목 | 값 |
|---|---|
| 소스 | `5465dd2f33f93eaed1fc60bf21eeb7d8320fffaa` |
| 고정 후보 | `artifacts/release/0.2.2.2/candidate1` · SourceDirty=false |
| ZIP | `DuckbillTweak-0.2.2.2.zip` · 462,842바이트/15파일 |
| SHA256 | `1a74157d96b3591faa4e04871d66b8437f4c28b786dee17ce5d80ed7677ed2cc` |
| 공개 Release | ID440183401 · `v0.2.2.2` · draft=false/prerelease=false |
| 공개 시각 | 2026-11-10T09:52:35Z / 2026-11-10 18:52:35 KST |
| 설치 원본 | `repo/release.json` · 0.2.2.2/API15 · LastUpdate1794271955 |
| 공용 동기화 | [workflow42794466201](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/42794466201) 성공 · 공용원본`12613de21b0436144b96f8bef9e172eb46c8fa29` |
| 실기 | 측후 표시(개발판) 사용자 확인 · 공개 후 재화 교환 상점·업데이트 내역 한 화면 표시 사용자 확인(2026-11-10, 빌드 출처 미기재) · PvP 말풍선 미확인 · InGameVerified=false(일부 미확인) |

[공개 릴리스](https://github.com/PlatyDuckbill/DuckbillTweak/releases/tag/v0.2.2.2). Release 빌드 경고·오류0, 내장 한영 안내(3항목)·manifest 일치, Release DLL에 조사 도구 문자열 없음. 업로드 자산 digest/크기와 태그 대상 커밋을 대조한 뒤 공개했다. 인증 없는 ZIP/체크섬/태그 아이콘 HTTP200·원본 해시 일치를 확인한 후 설치 원본을 작성했다. 게시 근거는 `.cache/publication-0.2.2.2/`에 보관한다. 설치 원본`0764828` push 후 공용 동기화를 수동 실행했다. 2026-11-10T10:03:41Z에 고정 공용 커밋과 기존 [공용 설치 URL](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json) 모두 익명HTTP200/0.2.2.2/API15/22필드 원본 일치를 확인했다. 다른 Fisher 항목은0.9.0.0을 유지한다. 공개 배포·공용 목록 반영은 완료이며 실제 Dalamud 업데이트·인게임 성공과 구분한다.

## 0.2.2.1 · 감정표현 따라하기 방향 선택과 판정 수정

테스트 대상이 없어 사용자가 코드 점검 후 배포를 명시 요청했다("너가 코드로 한 번만 점검해주고 배포하자"). 고정 후보를 재빌드 없이 게시했으며 InGameVerified=false를 유지한다. "지금 방향 그대로"(실행 순간 타겟 비우기)가 실제로 방향을 유지하는지는 방향 로그(`Emote follow facing`)로 확인 대기다. 변경 근거는 [계획 기록](PLANS.md)의 0.2.2.0 배포 후 코드 점검 수정을 따른다.

| 항목 | 값 |
|---|---|
| 소스 | `159736d3bc68e46d135c28bdc5310aeb564f9a76` |
| 고정 후보 | `artifacts/release/0.2.2.1/candidate1` · SourceDirty=false |
| ZIP | `DuckbillTweak-0.2.2.1.zip` · 457,244바이트/15파일 |
| SHA256 | `f3458e2ee658ac4cd59c9b0d49f7d6161280a3bc5862199fba7665719f5fdcdd` |
| 공개 Release | ID439649656 · `v0.2.2.1` · draft=false/prerelease=false |
| 공개 시각 | 2026-11-09T21:11:03Z / 2026-11-10 06:11:03 KST |
| 설치 원본 | `repo/release.json` · 0.2.2.1/API15 · LastUpdate1794226263 |
| 공용 동기화 | [workflow42700139605](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/42700139605) 성공 · 공용원본`53c01a62eee5e4f8011af451903228a925739991` |
| 실기 | 고정 ZIP·방향 유지·재실행 대기 미확인 · InGameVerified=false |

[공개 릴리스](https://github.com/PlatyDuckbill/DuckbillTweak/releases/tag/v0.2.2.1). Release 빌드 경고·오류0, 내장 한영 안내·manifest 일치, Release DLL에 조사 도구 문자열 없음. 업로드 자산 digest/크기와 태그 대상 커밋을 대조한 뒤 공개했다. 인증 없는 ZIP/체크섬/태그 아이콘 HTTP200·원본 해시 일치를 확인한 후 설치 원본을 작성했다. 게시 근거는 `.cache/publication-0.2.2.1/`에 보관한다. 설치 원본`8c32192` push 후 공용 동기화를 수동 실행했다. 2026-11-09T21:12:04Z에 고정 공용 커밋과 기존 [공용 설치 URL](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json) 모두 익명HTTP200/0.2.2.1/API15/22필드 원본 일치를 확인했다. 다른 Fisher 항목은0.9.0.0을 유지한다. 공개 배포·공용 목록 반영은 완료이며 실제 Dalamud 업데이트·인게임 성공과 구분한다.
## 0.2.2.0 · 감정표현 따라하기와 색 선택 창 수정

사용자가 최신 따라하기 보완(박자 기준·종족별 길이·꺼지는 감정표현 재시도)의 인게임 확인 전에 배포를 명시 요청했다("나머진 해보면서 할게"). 고정 후보를 재빌드 없이 게시했으며 InGameVerified=false를 유지한다. 따라하기 1차 구현과 색 선택 창 수정의 개발판 확인 범위는 [계획 기록](PLANS.md)을 따른다.

| 항목 | 값 |
|---|---|
| 소스 | `eedac750b19ef0e763acc86f1b03ead0d73556fa` |
| 고정 후보 | `artifacts/release/0.2.2.0/candidate1` · SourceDirty=false |
| ZIP | `DuckbillTweak-0.2.2.0.zip` · 455,580바이트/15파일 |
| SHA256 | `c58bfc233957849ad69d62c186b1272c0037905d146cd7f694d3c2f7a5c13ec0` |
| 공개 Release | ID439584091 · `v0.2.2.0` · draft=false/prerelease=false |
| 공개 시각 | 2026-11-09T20:06:32Z / 2026-11-10 05:06:32 KST |
| 설치 원본 | `repo/release.json` · 0.2.2.0/API15 · LastUpdate1794222392 |
| 공용 동기화 | [workflow42689868878](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/42689868878) 성공 · 공용원본`a750f1eef74c78d55e9dce629a91d4891eceb73a` |
| 실기 | 고정 ZIP 미확인 · 최신 따라하기 보완 미확인 · InGameVerified=false |

[공개 릴리스](https://github.com/PlatyDuckbill/DuckbillTweak/releases/tag/v0.2.2.0). Release 빌드 경고·오류0, 내장 한영 안내·manifest 일치, Release DLL에 조사 도구(PositionalProbe/EmoteProbe/EmoteSyncExperiment) 문자열 없음. 업로드 자산 digest/크기와 태그 대상 커밋을 대조한 뒤 공개했다. 인증 없는 ZIP/체크섬/태그 아이콘 HTTP200·원본 해시 일치를 확인한 후 설치 원본을 작성했다. 게시 근거는 `.cache/publication-0.2.2.0/`에 보관한다. 설치 원본`bada284` push 후 공용 동기화를 수동 실행했다. 2026-11-09T20:11:13Z에 고정 공용 커밋과 기존 [공용 설치 URL](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json) 모두 익명HTTP200/0.2.2.0/API15/22필드 원본 일치를 확인했다. 다른 Fisher 항목은0.9.0.0을 유지한다. 공개 배포·공용 목록 반영은 완료이며 실제 Dalamud 업데이트·인게임 성공과 구분한다.
## 0.2.1.0 · 측후·콤보 표시

사용자가 배포를 명시 요청했다. 업데이트 내역의 게임 업데이트 문구를 사용자 요청으로 명확히 고친 뒤 후보2를 만들어 재빌드 없이 게시했다. 고정 Release ZIP 자체의 인게임 확인은 하지 않았으므로 InGameVerified=false를 유지한다. 같은 소스의 개발판 인게임 확인 범위는 [검증 기록](VERIFICATION.md#2026-11-09-측후콤보-표시-0210-후보-전)을 따른다.

| 항목 | 값 |
|---|---|
| 소스 | `6d35943656784274aa9b7228987b85f7f21fa6c0` |
| 고정 후보 | `artifacts/release/0.2.1.0/candidate2` · SourceDirty=false (candidate1은 문구 수정 전 후보로 게시하지 않음) |
| ZIP | `DuckbillTweak-0.2.1.0.zip` · 445,762바이트/15파일 |
| SHA256 | `a1f1db4f94836fc9dfe285b80e836e723b4e7cb35c2e07d8dff8b4152c75243b` |
| 공개 Release | ID439263342 · `v0.2.1.0` · draft=false/prerelease=false |
| 공개 시각 | 2026-11-09T14:54:19Z / 23:54:19 KST |
| 설치 원본 | `repo/release.json` · 0.2.1.0/API15 · LastUpdate1794203659 |
| 공용 동기화 | [workflow42639392498](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/42639392498) 성공 · 공용원본`680f1fd5ecef89ba794c9139f39e02f33bc34ebb` |
| 실기 | 고정 ZIP 미확인 · InGameVerified=false |

[공개 릴리스](https://github.com/PlatyDuckbill/DuckbillTweak/releases/tag/v0.2.1.0). Release 빌드 경고·오류0, 내장 한영 4항목·manifest 일치, Release DLL에 조사 도구 없음. 업로드 자산 digest/크기와 태그 대상 커밋을 대조한 뒤 공개했다. 인증 없는 ZIP/체크섬/태그 아이콘 HTTP200·원본 해시 일치를 확인한 후 설치 원본을 작성했고 LastUpdate는 실제 공개 시각이다. 게시 근거는 `.cache/publication-0.2.1.0/`에 보관한다. 설치 원본`138594d` push 후 공용 동기화를 수동 실행했다. 2026-11-09T14:57:29Z에 고정 공용 커밋과 기존 [공용 설치 URL](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json) 모두 익명HTTP200/0.2.1.0/API15/22필드 원본 일치를 확인했다(main 주소는 캐시로 약90초 늦게 반영). 다른 Fisher 항목은0.9.0.0을 유지한다. 공개 배포·공용 목록 반영은 완료이며 실제 Dalamud 업데이트·인게임 성공과 구분한다.
## 0.2.0.4 · dal4kr15.0.3.9 UI 참조 차단 대응

사용자가 문제 대응 후 버전 상승·공개 배포를 명시 요청했다. 고정 후보를 재빌드 없이 게시했으며 실제 새 런타임 로드·화면 확인은 대기이므로 InGameVerified=false를 유지한다.

| 항목 | 값 |
|---|---|
| 소스 | `02cfa4d8c1effc384d8358bdd1f8a403a59971c0` |
| 고정 후보 | `artifacts/release/0.2.0.4/candidate1` · SourceDirty=false |
| ZIP | `DuckbillTweak-0.2.0.4.zip` · 418,391바이트/15파일 |
| SHA256 | `4f060667001fb5bc80b0d7931bf5be6ad8c251b1e2bb80b23f22d9e202dc82c6` |
| 공개 Release | ID428917707 · `v0.2.0.4` · draft=false/prerelease=false |
| 공개 시각 | 2026-10-30T10:48:53Z / 19:48:53 KST |
| 설치 원본 | `repo/release.json` · 0.2.0.4/API15 · LastUpdate1793324933 |
| 공용 동기화 | [workflow41043625095](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/41043625095) 성공 · 공용원본`0133abd67bff2afad3d7bf0438e5670ab24a02b0` |
| 실기 | 새 런타임 로드·대표 기능 확인 대기 · InGameVerified=false |

[공개 릴리스](https://github.com/PlatyDuckbill/DuckbillTweak/releases/tag/v0.2.0.4). 정규 Release 빌드 경고·오류0, 추출15파일 해시·실제 DLL 재화708바이트/28매핑·내장 한영 안내·manifest/참조 버전 일치. 고정 산출물의291타입/1,172멤버 전체 메타데이터는 조사한 Debug 결과와 동일하며 기존 차이/상위 참조의 별도 검토 근거를 유지한다. 새 dal4kr15.0.3.9 및 기존 두 런타임 각각 정확한 KR 게임SHA/원본10명령/세 모듈 조합 일치를 확인했다. SDK/API/lock/KamiToolKit 원본/게임 가드 변경과 자동 테스트·게임 조작은 없다.

소스push/태그대상/초안 자산digest·크기를 대조한 뒤 고정 ZIP을 공개했다. 인증 없는 ZIP/체크섬/태그아이콘HTTP200·원본해시 일치를 확인한 후 설치 원본을 작성했고 LastUpdate는 실제 공개 시각으로 설정했다. 게시 근거는`.cache/publication-0.2.0.4/`에 보관한다. 설치 원본325264a push와 공용동기화41043625095 성공 후2026-10-30T10:50:36Z에 고정 공용 커밋 및 기존 [공용 설치 URL](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json) 모두 익명HTTP200/0.2.0.4/API15/22필드 원본 일치를 확인했다. 다른Fisher항목은0.6.0.0을 유지한다. 공개 배포·공용 목록 반영은 완료이며 실제 Dalamud 업데이트·인게임 성공과 구분한다. 기존 태그/Release와DuckbillFisher 저장소는 변경하지 않았다.

## 0.2.0.3 · 상점 마우스 판정과 일반 채팅 말풍선 순서

사용자가 말풍선 코드 전면 검수 후 공개 배포를 명시 요청했다. 원래 말풍선 발생 원인은 미확정이며 겹침 재현이 되지 않아 수정 후 실기를 완료한 것으로 처리하지 않는다. 상점은 사용자 테스트에서 정상으로 보인다는 보고가 있으나 고정 Release ZIP 전체의 InGameVerified=false를 유지한다.

| 항목 | 값 |
|---|---|
| 소스 | `e1cb3d3e83b30358aee04fedaea5c3e87bc91ef9` |
| 고정 후보 | `artifacts/release/0.2.0.3/candidate1` · SourceDirty=false |
| ZIP | `DuckbillTweak-0.2.0.3.zip` · 417,990바이트/15파일 |
| SHA256 | `72883c85af9c022e184e7b19d541ca11d08e3f5ca21b51fa3511e30a45ba9a73` |
| 공개 Release | ID426628923 · `v0.2.0.3` · draft=false/prerelease=false |
| 공개 시각 | 2026-10-28T12:16:59Z / 21:16:59 KST |
| 실기 | 말풍선 미확인 · InGameVerified=false |

[공개 릴리스](https://github.com/PlatyDuckbill/DuckbillTweak/releases/tag/v0.2.0.3). Release 빌드 경고·오류0, 추출15파일 해시·실제 DLL 내장 재화708바이트/28매핑·한영3항목·manifest/참조 버전 일치. 정확한 KR 게임SHA/지원 두 런타임 각각3모듈 조합/원본10명령 일치. 전체 메타데이터 차이는 이전 배포 보고서와 같으며 새 참조10개가 두 런타임 모두 일치하고 누락0이다. 자동 테스트/게임 조작 없음. 교차 레이어 이동은 전용 함수로 먼저 이전 목록을 정리하고 같은 레이어에서만 순서를 조정한다.

초안 자산 digest/크기·태그 소스를 대조한 뒤 고정 ZIP을 재빌드 없이 공개했다. 인증 없는 ZIP/체크섬/태그아이콘 다운로드HTTP200/원본SHA 일치를 확인한 후 설치 원본0.2.0.3을 작성했다. LastUpdate는 실제 공개 시각으로 설정했다. 게시 근거는`.cache/publication-0.2.0.3/`에 보관한다. 공용 목록 동기화와 실제 업데이트 후 인게임 확인은 별도 기록한다. 기존 태그·Release·DuckbillFisher 저장소는 변경하지 않았다.

## 0.2.0.2 · 입찰 창 지연 열기 수정

사용자가 인게임 확인 전 공개 배포를 명시 요청했다. 확인된candidate1을 재빌드하지 않고 게시했으며 InGameVerified=false를 유지한다.

| 항목 | 값 |
|---|---|
| 소스 커밋 | `8ba8a571b33ef24e02595bca9c746895b02d3574` |
| 후보 | `artifacts/release/0.2.0.2/candidate1` |
| ZIP | `DuckbillTweak-0.2.0.2.zip` · 413,880바이트 |
| SHA256 | `5c76266b0b3014e2f3038b9b2d4bf259890b553c6fbe5355dafdb567c93d684e` |
| 추출 DLL | `.cache/release-validation/0.2.0.2/candidate1/DuckbillTweak.dll` |
| GitHub Release | ID425697138 · tag `v0.2.0.2` · draft=false · prerelease=false |
| ZIP / 체크섬 자산 | ID682128277 / ID682128305 · 서버digest/크기와 로컬 일치 |
| 공개 시각 | 2026-10-27 16:13:04 UTC / 2026-10-28 01:13:04 KST |
| 설치 원본 | `repo/release.json` · 0.2.0.2/API15 · LastUpdate1793117584(실제공개UTC) |
| 공용 동기화 | [workflow40538869411](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/40538869411) 성공 · 공용원본`4c5dc72b87bc1fe17c70dc6c69151406a76be8bb` |
| 실기 상태 | InGameVerified=false · 실제 업데이트/로드/창을 나중에 열기 확인 대기 |

[공개 릴리스](https://github.com/PlatyDuckbill/DuckbillTweak/releases/tag/v0.2.0.2). 고정SDK/API/컴파일 참조와KamiToolKit revision을 유지한 Release 빌드 경고·오류0, 허용15파일과 추출본 전부의해시/내장자료/한영업데이트 내역/manifest 버전 일치를 확인했다. `native-contract`의정확한 KR 게임SHA256/원본명령7개/설치3모듈 조합도 재확인했다. 자동 테스트/게임 조작은 하지 않았다.

공개 전에소스·ZIP 해시·추출파일·태그대상·서버자산digest/크기를 대조했다. 공개후인증없이 ZIP/체크섬/태그아이콘을 다운로드해HTTP200과원본SHA256 일치를확인한뒤 설치 원본을 작성했다. 게시근거는`.cache/publication-0.2.0.2/`에보관한다. 이전태그/자산과DuckbillFisher 저장소는변경하지않았다.

설치 원본은`612bf5b`로push했다. 공용 동기화 성공 후 고정 공용 커밋의 목록을 익명HTTP200으로 조회해0.2.0.2/API15/다운로드·아이콘·업데이트 내역·LastUpdate를 포함한22필드 원본 일치를 확인했다. 다른 Fisher 항목은0.6.0.0을 유지한다. 기존 공용main raw URL은 초기 캐시가0.2.0.1을 반환했으나2026-10-27T16:16:28Z에 실제 [공용 설치 URL](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json)에서도익명HTTP200/0.2.0.2/API15/22필드 원본 일치를 확인했다. 공개 배포와 공용 목록 반영은 완료이며 실제 게임 내 업데이트/기능 확인은 별도다.

개발DLL의최근자동재로드는공개판과개발판중복으로`/duckbilltweak`명령이충돌해실패했다. 이로그를수정본로드성공으로보지않으며, 공개업데이트확인시개발등록을끄고설치판하나만활성화해야한다. 패키지/HTTP확인을실제Dalamud 업데이트나입찰화면성공으로확대하지않는다.

## 0.2.0.1 · 공개 업데이트를 통한 확인

사용자가 2026-10-28에 공개 업데이트로 테스트하겠다고 명시해 배포를 요청했다. 실제 인게임 확인을 완료한 것으로 기록하지 않으며, 고정candidate1을 재빌드하지 않고 공개했다.

| 항목 | 값 |
|---|---|
| 소스 커밋 | `837c4df08c9e98d5554300302eb1dca0a6d7090f` |
| 후보 | `artifacts/release/0.2.0.1/candidate1` |
| ZIP | `DuckbillTweak-0.2.0.1.zip` · 412,228바이트 |
| SHA256 | `3a7290fd3ea455a9f943372e355ca0d067e7c2536b269d34ad08f1d74f65bfe1` |
| 추출 DLL | `.cache/release-validation/0.2.0.1/candidate1/DuckbillTweak.dll` |
| GitHub Release | ID425652037 · tag `v0.2.0.1` · draft=false · prerelease=false |
| ZIP / 체크섬 자산 | ID682035850 / ID682035895 · 서버digest와 로컬SHA256 일치 |
| 공개 시각 | 2026-10-27 15:38:34 UTC / 2026-10-28 00:38:34 KST |
| 설치 원본 | `repo/release.json` · 0.2.0.1/API15 · LastUpdate1793115514(실제공개UTC) |
| 공용 동기화 | [workflow40533243105](https://github.com/PlatyDuckbill/DuckbillRepo/actions/runs/40533243105) 성공 · 공용원본`5f9e20f2b8f9cd6f59a0291491c3e24963524445` |
| 실기 상태 | InGameVerified=false · 본인/친구의 실제 업데이트·로드·대표 기능 확인 대기 |

[공개 릴리스](https://github.com/PlatyDuckbill/DuckbillTweak/releases/tag/v0.2.0.1). 업로드 전에 ZIP 해시와 허용 목록15개 파일/기존 추출본을 다시 대조하고 소스837c4df 이후 변경이 검증 문서뿐임을 확인했다. GitHub 자산digest/크기를 로컬과 대조했으며 공개 후 인증 없이 ZIP/체크섬/태그 아이콘을 다운로드해HTTP200/원본SHA256 일치를 확인했다. 그 뒤 설치 원본을 갱신했다. SDK/게임 DLL·캐시·개인 설정은 패키지에 포함하지 않았다. 이전0.2.0.0 태그/자산은 변경하지 않았다.

설치 원본은`f217919`로push했다. 공용동기화 성공 후 고정 공용커밋과새원본22필드를대조했고, 캐시갱신 후2026-10-27T15:44:42Z에 실제 기존[공용raw URL](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json)에서도익명HTTP200/0.2.0.1/API15/22필드일치를확인했다. 설치·업데이트·아이콘URL/Changelog/LastUpdate도모두원본과같다. 다른Fisher항목은0.6.0.0이다. 게시완료와실제Dalamud 업데이트·인게임성공은구분하며후자는대기다.

게시 근거는`.cache/publication-0.2.0.1/`에 보관한다. 빌드·리소스·정적 계약 검사는[VERIFICATION](VERIFICATION.md), 배포판별 차이/남은 실제 호스트 로드 확인은[NATIVE-UI](NATIVE-UI.md)를 따른다. 공개 업데이트 후 `/dbtw`가 열리는지부터 확인하고, 다시 차단되면 짧은 호환성 로그의버전/MVID/프로필로 현재 배포판을 확인한다.

## 0.2.0.0 배포 확인

현재 상태: **0.2.0.0 공개 완료. 현재 설치는 [DuckbillRepo 공용 목록](https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json)을 사용합니다.** 아래 전용 목록 기록은 최초 게시 당시의 근거입니다.

| 항목 | 값 |
|---|---|
| 소스 커밋 | `f19c07aa89ffd596f1ef4380d7e1771ee0eec719` |
| 후보 | `artifacts/release/0.2.0.0/candidate6` |
| ZIP | `DuckbillTweak-0.2.0.0.zip` · 409,068바이트 |
| SHA256 | `90fb837c45a4a134a1eabc1f044cc69697f043654c7b2e91fe9ba8f28bef5bf6` |
| 추출 DLL | `.cache/release-validation/0.2.0.0/candidate6/DuckbillTweak.dll` |
| GitHub Release | ID425486053 · tag `v0.2.0.0` · draft=false · prerelease=false |
| 공개 시각 | 2026-10-27 13:38:24 UTC / 22:38:24 KST |
| GitHub ZIP 자산 | ID681693784 · digest와 로컬 SHA256 일치 |
| 체크섬 자산 | ID681693813 · 92바이트 · digest 대조 완료 |
| 설치 목록 | `repo/pluginmaster.json` · DuckbillTweak0.2.0.0 한 항목 |
| 목록 이미지 | 512×512 투명 PNG · IconUrl 반영 · 소개 스크린샷 없음 |

[공개 릴리스](https://github.com/PlatyDuckbill/DuckbillTweak/releases/tag/v0.2.0.0). ZIP과 체크섬은 고정 candidate6에서 변경하지 않았습니다. 사용자가 각 프로젝트 전용 URL로 배포를 요청했으며 Fisher 저장소는 변경하지 않았습니다.

확인한 내용:

- 정규 Release 빌드 경고·오류0. KamiToolKit도 고정 revision의 Release 구성으로 빌드했다. candidate1~5는 중간 후보이며 현재 고정 후보는6이다. 기존 후보를 로컬에서 덮어쓰지 않았다.
- Git이 깨끗한 소스 커밋에서 candidate6을 생성했다. 패키지15개 파일과 추출 파일의 해시를 모두 대조했다. 게임/SDK DLL이나 PDB/개인 설정/자격 증명은 넣지 않았다.
- DLL/manifest/패키지0.2.0.0, API15, 비동기 언로드 속성을 대조했다. 내장 재화 자료708바이트/28매핑 및 원본 일치, 내장 한영5항목/설치 Changelog/영문 릴리스/한글 CHANGELOG 일치를 확인했다.
- 정확한 게임 SHA/FFCS MVID/기존 네이티브 명령 대조를 통과했다. 가드 완화/새 런타임 의존성/자동 테스트/게임 입력은 없다.
- GitHub 대상 커밋과 ZIP·체크섬의 크기/서버 digest를 확인했다. 공개 후 인증 없이 버전 ZIP/체크섬/태그 아이콘을 실제 URL에서 내려받아 HTTP200과 원본 SHA256 일치를 확인한 뒤 전용 설치 JSON을 작성했다. LastUpdate는 실제 공개 시각의 Unix 초로 반영했다.
- 실제 공유 raw URL도 인증 없이HTTP200으로 조회했고 DuckbillTweak 한 항목/0.2.0.0/API15/설치·업데이트 URL/아이콘/Changelog를 확인했다. LastUpdate는UTC2026-10-27T13:38:24Z에 해당하는1793108304다. 실제 Dalamud 설치 성공을 이 HTTP 확인만으로 판단하지 않는다.
- 사용자 제공1254×1254 PNG 원본을assets/source에 바이트 변경 없이 보관하고512×512 투명 PNG로 축소했다. README에는192×192 표시로 사용한다. 아이콘 SHA256은 `91a35805cd22ea2861b505d9b0475839d30157642ee7301d74ab1e45665ba868`이며 main raw/버전 태그 URL의 다운로드 해시가 모두 일치한다.

남은 내용:

1. 고정 Release 후보의 설정/업데이트 내역 한영 전환·NEW/상점 필터·재화/입찰/PvP 대표 동작을 확인한다. 기존 Debug 실기 로그를 이 ZIP의 실기 확인으로 확대하지 않는다.
2. 실제 Dalamud 설치 목록에서 공용 URL 등록/검색/설치·업데이트를 확인한다. HTTP/자료 검사와 실제 설치 결과는 구분한다. 이후 배포는 [배포 절차](DISTRIBUTION.md)를 따른다.

0.2.0.3 설치 원본은c4057fb로push했다. 공용 동기화40686617430이 성공했고 공용 커밋710127f53f35bc3b9d518be896e7277f428c10cb의 고정raw URL에서 익명HTTP200/0.2.0.3/API15/22필드 일치를 확인했다. 다른Fisher항목은0.6.0.0을 유지한다. 기존main 설치URL은 초기캐시가0.2.0.2를 반환했으나2026-10-28T12:22:50Z에 같은 기존URL에서도 익명HTTP200/0.2.0.3/API15/22필드 원본 일치를 확인했다. 공개 배포와 공용 목록 반영은 완료이며 실제Dalamud 업데이트/말풍선 인게임 검증은 별도 대기다.
