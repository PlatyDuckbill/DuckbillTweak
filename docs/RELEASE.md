# 0.2.0.0 배포 확인

현재 상태: **GitHub Release 초안 작성/ZIP 업로드 완료. 공개 전.**

| 항목 | 값 |
|---|---|
| 소스 커밋 | `f19c07aa89ffd596f1ef4380d7e1771ee0eec719` |
| 후보 | `artifacts/release/0.2.0.0/candidate6` |
| ZIP | `DuckbillTweak-0.2.0.0.zip` · 409,068바이트 |
| SHA256 | `90fb837c45a4a134a1eabc1f044cc69697f043654c7b2e91fe9ba8f28bef5bf6` |
| 추출 DLL | `.cache/release-validation/0.2.0.0/candidate6/DuckbillTweak.dll` |
| GitHub Release | ID425486053 · tag `v0.2.0.0` · draft=true |
| GitHub ZIP 자산 | ID681693784 · digest와 로컬 SHA256 일치 |
| 체크섬 자산 | ID681693813 · 92바이트 · digest 대조 완료 |
| 설치 목록 | `repo/pluginmaster.json`은 `[]` · 공개 ZIP 연결 전 |
| 목록 이미지 | 512×512 투명 PNG · IconUrl 반영 · 소개 스크린샷 없음 |

[릴리스 초안](https://github.com/PlatyDuckbill/DuckbillTweak/releases/tag/untagged-3174c478b7cf58407e6b)은 저장소 권한이 있는 사용자만 볼 수 있습니다. 공개 Release/설치 성공으로 해석하지 않습니다.

확인한 내용:

- 정규 Release 빌드 경고·오류0. KamiToolKit도 고정 revision의 Release 구성으로 빌드했다. candidate1~5는 중간 후보이며 현재 고정 후보는6이다. 기존 후보를 로컬에서 덮어쓰지 않았다.
- Git이 깨끗한 소스 커밋에서 candidate6을 생성했다. 패키지15개 파일과 추출 파일의 해시를 모두 대조했다. 게임/SDK DLL이나 PDB/개인 설정/자격 증명은 넣지 않았다.
- DLL/manifest/패키지0.2.0.0, API15, 비동기 언로드 속성을 대조했다. 내장 재화 자료708바이트/28매핑 및 원본 일치, 내장 한영5항목/설치 Changelog/영문 릴리스/한글 CHANGELOG 일치를 확인했다.
- 정확한 게임 SHA/FFCS MVID/기존 네이티브 명령 대조를 통과했다. 가드 완화/새 런타임 의존성/자동 테스트/게임 입력은 없다.
- GitHub 초안 대상 커밋과 ZIP·체크섬의 크기/서버 digest를 확인했다. 초안 파일은 익명 다운로드 대상이 아니므로 익명 다운로드 검증/설치 JSON 갱신은 공개 단계에 남아 있다.
- 사용자 제공1254×1254 PNG 원본을assets/source에 바이트 변경 없이 보관하고512×512 투명 PNG로 축소했다. README에는192×192 표시로 사용한다. 아이콘 SHA256은 `91a35805cd22ea2861b505d9b0475839d30157642ee7301d74ab1e45665ba868`이며, 실제 main raw URL의 익명 다운로드 해시도 일치한다. manifest/후보 설치 JSON은v0.2.0.0 태그의 IconUrl을 가리킨다. 태그 이미지의 익명 확인은 공개 단계에서 수행한다.

남은 내용:

1. 고정 Release 후보의 설정/업데이트 내역 한영 전환·NEW/상점 필터·재화/입찰/PvP 대표 동작을 확인한다. 기존 Debug 실기 로그를 이 ZIP의 실기 확인으로 확대하지 않는다.
2. 확인한 후보를 공개하고, ZIP·체크섬·태그 이미지의 익명 다운로드/해시를 확인한 후 설치 JSON을 게시한다. [배포 절차](DISTRIBUTION.md)를 따른다.
