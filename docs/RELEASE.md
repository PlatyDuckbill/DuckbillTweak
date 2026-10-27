# 배포 확인

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
