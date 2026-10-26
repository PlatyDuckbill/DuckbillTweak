# DuckbillTweak 작업 지침

한국 FFXIV용 Dalamud 편의 기능 모음. InternalName/AssemblyName은 DuckbillTweak.
시작할 때 git status와 docs/STATUS.md를 확인한다. 변경 이유와 결과는 한국어로 설명한다.

- 사용자 파일 tmp_note.md는 읽기·검색·수정·스테이징하지 않는다.
- DuckbillFisher는 구조/개발 환경의 참고 대상이다. 그 저장소의 파일을 변경하지 않는다.
- Core는 Dalamud/Lumina/ImGui/게임 프로세스에 의존하지 않는다. 게임 접근과 UI는 Plugin에 둔다.
- 각 트윅은 별도로 켜고 끈다. 신규 기능은 기본 비활성이다. 종료 시 모든 이벤트/명령을 해제한다.
- 게임의 입력·이동·전투·구매·입찰을 자동 실행하거나 서버 채팅을 보내지 않는다.
- 공개 Dalamud 서비스를 우선한다. 글로벌 시그니처/오프셋을 한국 클라이언트에 그대로 적용하지 않는다.
- 상점은 원래 게임 화면 안의 컨트롤, 재화는 상점 버튼으로 여는 네이티브 화면, 입찰은 아이템 우클릭 화면을 사용한다. 별도 ImGui 보조 목록/자동 팝업으로 되돌리지 않는다. PvP 말풍선은 on/off만 제공하고 기본 말풍선 설정을 따른다.
- NativeCompatibility의 게임 SHA-256/클라이언트 구조체 MVID와 UI 형태 검사를 유지한다. 주석/버전만 바꿔 가드를 통과시키지 않는다. 필터 행 번호를 구매 행 번호로 보내지 말고 원본 인덱스와 아이템/컨텍스트를 재검증한다.
- KamiToolKit은 third_party의 고정 submodule revision과 MIT 고지를 유지한다. 원본은 직접 수정하지 않으며 호환 수정이 필요하면 별도로 근거를 기록한다.
- 게임 객체/인벤토리 span은 Framework에서 읽고 관리 값으로 복사한다. Draw에서 게임 시트 전체를 탐색하거나 파일을 읽지 않는다.
- 읽지 못한 보유량/등록 여부/숫자는 Unknown이다. Unknown을 0·미등록·포기로 바꾸지 않는다.
- 캐릭터/지역 전환과 기능 해제 시 이전 세션의 상태를 비운다. 채팅/플레이어 기록은 메모리에만 둔다.
- 외부 코드/자료를 도입할 때 출처·고정 revision·라이선스·가공 내역을 기록한다. 참고만 한 코드도 출처를 남긴다.
- .NET SDK와 Dalamud API는 global.json/csproj/lock 파일을 따른다. SDK/API 변경은 별도 근거가 필요하다.
- 일반 작업은 scripts/build.ps1(잠금 파일 복원·빌드·산출물 리소스 검사)과 오프라인 데이터 검사다. 수동 dotnet build 후에도 GameDataProbe resources로 실제 DLL의 내장 자료/원본 일치를 확인한다. 테스트는 사용자 명시 요청 시 추가/실행한다.
- 빌드 성공·정적 자료 검사·인게임 확인·공개 배포를 구분한다. 인게임 확인을 추정해 완료 처리하지 않는다.
- 완료한 변경만 명시적으로 스테이징/커밋한다. 자격 증명·캐시·SDK·게임 파일·빌드 산출물을 커밋하지 않는다.
- 현재 요청의 소스 저장소는 https://github.com/PlatyDuckbill/DuckbillTweak 이다. 공개 Release/설치 JSON 갱신은 별도 배포 요청과 확인된 산출물로 진행한다.
