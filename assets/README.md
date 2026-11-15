# 설치 목록 이미지

2026-11-15 공개0.3.0.0: 원본 그림을 직접 편집해 얼굴/몸통을 우선하고, 큰 설정 패널의 슬라이더를 짧은 앞발로 미는 동작으로 바꿨습니다. 아래쪽 얇은 받침 면과 뒤쪽 보조 패널 가장자리만 더해 주변 맥락을 보강했습니다. 렌치/기어는 제거했고 설정 조절 기능을 먼저 구분하도록 패널을 키웠습니다. 또한 글자·원형 배지·파란 배경·작은 소품은 제거했습니다. Fisher/Calendar와 윤곽선·색감을 맞췄습니다.

Dalamud 설치 목록 대표 아이콘은 `assets/duckbilltweak-icon.png`(512×512 RGBA 투명 PNG)입니다.
생성 원본은 `source/duckbilltweak-icon-master.png`(1254×1254), 실제 축소본은 `previews/duckbilltweak-128.png` / `previews/duckbilltweak-64.png`입니다. 스모크/펄 배경에서 기능 구분과 윤곽을 확인했습니다.
[icon-prompt.txt](icon-prompt.txt)에 built-in image_gen의 전체 프롬프트와 기존 Tweak `1007366`에서 이어진 `528a958` 원본의 후속 편집 이력을 기록했습니다. 생성 원본은 수정하지 않았고, 배포용만 alpha>16의 그림 경계(36,152 / 1185×888)로 아주 옅은 외곽 잔상/여백을 정리했습니다. System.Drawing HighQualityBicubic으로 alpha/비율을 유지해 최소7% 여백으로 축소했습니다.
사용자가 제공했던 이전 원본 `source/DuckbillTweak_smooth_edge.png`는 그대로 보존하며 이전 원형 대표 PNG는 Git 이력에 남습니다.

csproj의 `IconUrl`은 `v0.2.0.0` 등 각 버전 태그의 대표 아이콘을 가리킵니다.
README에는 같은 파일을192×192 표시 크기로 사용합니다.
현재 PNG는 v0.3.0.0 태그에 게시했고 ZIP/체크섬·태그 아이콘의 익명 해시와 설치 목록 반영을 확인했습니다. 과거 공개 태그/ZIP은 보존합니다.
[공식 규격](https://dalamud.dev/plugin-publishing/submission/)의 정사각형64–512픽셀 범위를2026-10-27에 확인했습니다.

소개용 스크린샷은 `assets/previews/`에 추가하고 `ImageUrls`로 연결합니다.
대표 아이콘과 소개 이미지는 서로 다른 필드입니다. 가짜 URL이나 임시 이미지를 배포하지 않습니다.

이미지를 바꿀 때는 원본을 보존하고 새 Release 후보를 만듭니다. 태그의 공개 이미지 URL을 확인한 후 설치 목록을 게시합니다.
