# 설치 목록과 릴리스 배포

현재 공개 버전은 **0.2.0.0**입니다. DuckbillTweak와 DuckbillFisher는 [DuckbillRepo](https://github.com/PlatyDuckbill/DuckbillRepo)의 공용 설치 URL을 사용합니다.
이 문서는 DuckbillFisher의 설치 목록/릴리스/내장 업데이트 안내 구성을 참고해 작성했습니다.
고정 후보와 실제 게시 결과는 [배포 확인](RELEASE.md)에서 관리합니다.

## 배포 구성

| 파일 | 역할 |
|---|---|
| `src/DuckbillTweak.Plugin/DuckbillTweak.Plugin.csproj` | 기준 버전, 설치 화면 설명과 영문 Changelog |
| `data/releases.json` | 내장 한글·영문 릴리스 안내, 최근 10개 표시 |
| `CHANGELOG.md`, `docs/releases/버전.md` | 사용자 관점의 한글·영문 업데이트 내역 |
| `assets/duckbilltweak-icon.png` | 사용자 제공 원본에서 축소한512×512 설치 목록 대표 아이콘 |
| `repo/release.json` | 공용 자동화가 읽는 단일 배포 객체. 확인한 공개 Release만 연결하며 Dalamud 설치 목록으로 직접 등록하지 않음 |
| 버전 ZIP과 `.sha256` | GitHub Release에 올릴 패키지와 체크섬 |

`IconUrl`은 설치 목록 대표 아이콘, `ImageUrls`는 소개 이미지 배열입니다. `Changelog`는 설치/업데이트 화면의 안내입니다. [공식 메타데이터](https://dalamud.dev/plugin-development/plugin-metadata/), [커스텀 저장소](https://dalamud.dev/plugin-publishing/custom-repositories/)를2026-10-27에 대조했습니다. 현재 대표 아이콘은 버전 태그 경로를 사용하며 소개 스크린샷은 아직 없어 ImageUrls는 넣지 않습니다. 패키징 시 대표 PNG의 규격과 manifest의 IconUrl을 대조합니다.

## 후보 준비

PowerShell7/저장소 루트에서 실행합니다. 테스트나 게시를 실행하지 않습니다.

```powershell
./scripts/prepare-release.ps1 -OutputDirectory artifacts/release/0.2.0.1/candidate1
```

1. 고정 SDK/참조로 의존성까지 Release 구성으로 빌드합니다.
2. Plugin DLL/manifest/API15/4자리 버전과 내장 릴리스 안내·설치 Changelog 일치를 확인합니다.
3. 정해진 DLL·manifest·의존성 정보·KamiToolKit Assets·라이선스/고지만 ZIP에 넣습니다. SDK/게임 DLL/캐시/자격 증명을 넣지 않습니다.
4. ZIP을 `.cache/release-validation/버전/후보명`에 추출하고 파일별 해시와 내장 자료를 검사합니다.
5. 버전 ZIP/SHA256/후보 `release.json`/영문 릴리스 노트/validation.json을 만듭니다. 기존 도구 호환용 `pluginmaster.json` 후보는 로컬에만 남기며 공개 경로로 복원하지 않습니다. 원격 게시와 실제 설치 목록 변경은 하지 않습니다.

기존 후보를 덮어쓰지 않습니다. 이미지/소스가 바뀌면 새로운 후보 폴더를 사용합니다. `validation.json`의 InGameVerified/Published는 자동으로 true가 되지 않습니다.

## 확인과 게시

1. 추출한 고정 DLL로 설정/업데이트 안내 한영 전환/상점 필터·재화/입찰/PvP 중 해당 확인 결과와 ZIP 해시를 기록합니다. Debug 로드 결과를 고정 Release ZIP 실기 확인으로 확대하지 않습니다.
2. 사용자 이미지가 오면 원본을 추가하고 새 후보를 만듭니다. 이미지 URL과 버전은 태그에 고정합니다.
3. 소스 커밋을 push하고, GitHub Release 초안에 해당 커밋의 버전 ZIP과 체크섬을 올립니다. 초안은 설치 가능한 공개 릴리스가 아닙니다.
4. 공개할 후보가 확인되면 해당 커밋으로 버전 태그/Release를 공개합니다. 이미 공개한 `v0.2.0.0` 태그/ZIP은 덮어쓰지 않으며, 다음 배포는 더 높은 버전을 사용합니다.
5. 인증 없는 다운로드로 ZIP/체크섬/이미지의 성공과 해시를 확인한 **후에만** 후보 `release.json`을 `repo/release.json`으로 반영해 커밋·push합니다. 기존 `repo/pluginmaster.json` 경로는 복원하지 않습니다.
6. DuckbillRepo가 30분 주기로 원본·ZIP을 검증해 공용 목록을 갱신합니다. 즉시 반영하려면 공용 저장소의 Sync plugin repository를 수동 실행합니다.
7. 아래 실제 공용 raw URL에서 버전/API/Changelog/다운로드/아이콘 링크를 다시 확인합니다. 공개 설치/업데이트 결과와 HTTP/자료 확인은 구분해 기록합니다.

실제 설치 목록 URL:

```text
https://raw.githubusercontent.com/PlatyDuckbill/DuckbillRepo/main/pluginmaster.json
```

기존 개별 URL은 종료했습니다. 기존 사용자는 [공용 저장소 전환 절차](https://github.com/PlatyDuckbill/DuckbillRepo#기존-개별-저장소에서-전환)에 따라 URL 교체 후 설정·기록을 유지한 채 플러그인만 한 번 제거·재설치해 설치 출처를 전환합니다. 설정·기록 삭제/초기화를 선택하지 않습니다.

## 다음 업데이트

사용자가 할 수 있게 된 일부터 한 항목·한 문장으로 적습니다. 개발 중 수정 순서나 내부 검사 수치는 공개 안내에 나열하지 않습니다. 첫 공개판은 개발판 변경을 공개판 사이의 업데이트로 소개하지 않습니다.

csproj Version/Changelog, releases.json의 한영 안내, CHANGELOG와 영문 릴리스 노트를 함께 갱신합니다. 패키징 시 설치 Changelog와 내장 영문 안내의 일치를 검사합니다. 설정에서 열람한 버전만 저장하며 상점 체크 상태는 저장하지 않습니다. 플러그인 런타임 HTTP 없이 내장 자료로 표시합니다.
