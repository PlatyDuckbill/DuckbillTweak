# 자료 출처

`special-currencies.kr.json`은 특수 화폐 인덱스와 Item ID 사이의 숫자 관계만 보관한다. 아이템 이름/설명/아이콘과 전체 게임 시트는 저장소에 포함하지 않는다.

- 출처: [AllaganLib SpecialShopListing](https://github.com/Critical-Impact/AllaganLib/blob/c34734f75e369777e7ac36dc57c09a8fc3e78fd4/AllaganLib.GameSheets/Sheets/SpecialShopListing.cs), revision `c34734f75e369777e7ac36dc57c09a8fc3e78fd4`.
- 해당 저장소에서 루트 라이선스 파일을 확인하지 못했다. 소스 코드는 복사하거나 의존성으로 도입하지 않았다. 숫자 관계를 자료로 전사하고 설치된 게임 Item 시트에서 ID/이름 존재성을 대조했다.
- 대상 KR 게임 빌드: `2026.10.06.0000.0000`.
- 런타임에서는 대상 게임 버전 파일과 일치할 때만 특수 화폐 표를 사용한다. Item 이름이 없으면 해당 매핑을 제외한다. 인덱스19→Item21173이 이에 해당한다.
- TomestonesItem의 동적 관계는 설치된 클라이언트에서 읽는다. 고정 석판 ID를 위 표에 섞지 않는다.
- 로컬 전체 가격 ID 검사는 `GameDataProbe audit`으로 재현한다. ID 해석 성공이 실제 거래 조건·필요량·보유량 검증을 뜻하지 않는다.

게임 업데이트 시 표의 gameVersion만 바꿔 가드를 통과시키지 않는다. 변경한 시트/게임 동작을 대조하고 결과를 docs에 기록한다.
