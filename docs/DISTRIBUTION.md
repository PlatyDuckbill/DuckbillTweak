# 설치 목록과 릴리스 배포

현재 후보는 **0.2.0.0**입니다. 공개 전에 사용자 제공 목록 이미지와 최종 후보 확인을 반영합니다.
이 문서는 DuckbillFisher의 설치 목록/릴리스/내장 업데이트 안내 구성을 참고해 작성했습니다.
고정 후보와 실제 게시 결과는 [배포 확인](RELEASE.md)에서 관리합니다.

## 배포 구성

| 파일 | 역할 |
|---|---|
| `src/DuckbillTweak.Plugin/DuckbillTweak.Plugin.csproj` | 기준 버전, 설치 화면 설명과 영문 Changelog |
| `data/releases.json` | 내장 한글·영문 릴리스 안내, 최근 10개 표시 |
| `CHANGELOG.md`, `docs/releases/버전.md` | 사용자 관점의 한글·영문 업데이트 내역 |
| `assets/duckbilltweak-icon.png` | 사용자가 제공할 설치 목록 대표 아이콘 |
| `repo/pluginmaster.json` | 실제 설치 목록. 공개 전에는 `[]` 유지 |
| 버전 ZIP과 `.sha256` | GitHub Release에 올릴 패키지와 체크섬 |

`IconUrl`은 설치 목록 대표 아이콘, `ImageUrls`는 소개 이미지 배열입니다. `Changelog`는 설치/업데이트 화면의 안내입니다. [공식 메타데이터](https://dalamud.dev/plugin-development/plugin-metadata/), [커스텀 저장소](https://dalamud.dev/plugin-publishing/custom-repositories/)를2026-10-27에 대조했습니다. 이미지를 아직 받지 않았으므로 빈 이미지 링크를 넣지 않습니다. 대표 아이콘을 정해진 경로에 추가하면 csproj의 조건부 IconUrl이 버전 태그 경로를 사용합니다.

## 후보 준비

PowerShell7/저장소 루트에서 실행합니다. 테스트나 게시를 실행하지 않습니다.

```powershell
./scripts/prepare-release.ps1 -OutputDirectory artifacts/release/0.2.0.0/candidate3
```

1. 고정 SDK/참조로 의존성까지 Release 구성으로 빌드합니다.
2. Plugin DLL/manifest/API15/4자리 버전과 내장 릴리스 안내·설치 Changelog 일치를 확인합니다.
3. 정해진 DLL·manifest·의존성 정보·KamiToolKit Assets·라이선스/고지만 ZIP에 넣습니다. SDK/게임 DLL/캐시/자격 증명을 넣지 않습니다.
4. ZIP을 `.cache/release-validation/버전/후보명`에 추출하고 파일별 해시와 내장 자료를 검사합니다.
5. 버전 ZIP/SHA256/후보 설치 JSON/영문 릴리스 노트/validation.json을 만듭니다. 원격 게시와 실제 설치 목록 변경은 하지 않습니다.

기존 후보를 덮어쓰지 않습니다. 이미지/소스가 바뀌면 새로운 후보 폴더를 사용합니다. `validation.json`의 InGameVerified/Published는 자동으로 true가 되지 않습니다.

## 확인과 게시

1. 추출한 고정 DLL로 설정/업데이트 안내 한영 전환/상점 필터·재화/입찰/PvP 중 해당 확인 결과와 ZIP 해시를 기록합니다. Debug 로드 결과를 고정 Release ZIP 실기 확인으로 확대하지 않습니다.
2. 사용자 이미지가 오면 원본을 추가하고 새 후보를 만듭니다. 이미지 URL과 버전은 태그에 고정합니다.
3. 소스 커밋을 push하고, GitHub Release 초안에 해당 커밋의 버전 ZIP과 체크섬을 올립니다. 초안은 설치 가능한 공개 릴리스가 아닙니다.
4. 공개할 후보가 확인되면 해당 커밋으로 `v0.2.0.0` 태그/Release를 공개합니다. 이미 공개한 태그/ZIP은 덮어쓰지 않습니다.
5. 인증 없는 다운로드로 ZIP/체크섬/이미지의 성공과 해시를 확인한 **후에만** 후보 JSON을 `repo/pluginmaster.json`으로 반영해 커밋·push합니다.
6. 실제 공유 raw URL에서 버전/API/Changelog/다운로드/아이콘 링크를 다시 확인합니다. 공개 설치/업데이트 결과와 HTTP/자료 확인은 구분해 기록합니다.

실제 설치 목록 URL:

```text
https://raw.githubusercontent.com/PlatyDuckbill/DuckbillTweak/main/repo/pluginmaster.json
```

## 다음 업데이트

사용자가 할 수 있게 된 일부터 한 항목·한 문장으로 적습니다. 개발 중 수정 순서나 내부 검사 수치는 공개 안내에 나열하지 않습니다. 첫 공개판은 개발판 변경을 공개판 사이의 업데이트로 소개하지 않습니다.

csproj Version/Changelog, releases.json의 한영 안내, CHANGELOG와 영문 릴리스 노트를 함께 갱신합니다. 패키징 시 설치 Changelog와 내장 영문 안내의 일치를 검사합니다. 설정에서 열람한 버전만 저장하며 상점 체크 상태는 저장하지 않습니다. 플러그인 런타임 HTTP 없이 내장 자료로 표시합니다.
