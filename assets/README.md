# 설치 목록 이미지

Dalamud 설치 목록 대표 아이콘은 `assets/duckbilltweak-icon.png`입니다.
사용자가 제공한1254×1254 투명 PNG를 비율/그림/투명도를 유지해512×512 PNG로 축소했습니다.
수정하지 않은 원본은 `assets/source/DuckbillTweak_smooth_edge.png`에 보관합니다.
축소는 System.Drawing의 HighQualityBicubic 보간을 사용했습니다.

csproj의 `IconUrl`은 `v0.2.0.0` 등 각 버전 태그의 대표 아이콘을 가리킵니다.
README에는 같은 파일을192×192 표시 크기로 사용합니다.
[공식 규격](https://dalamud.dev/plugin-publishing/submission/)의 정사각형64–512픽셀 범위를2026-10-27에 확인했습니다.

소개용 스크린샷은 `assets/previews/`에 추가하고 `ImageUrls`로 연결합니다.
대표 아이콘과 소개 이미지는 서로 다른 필드입니다. 가짜 URL이나 임시 이미지를 배포하지 않습니다.

이미지를 바꿀 때는 원본을 보존하고 새 Release 후보를 만듭니다. 태그의 공개 이미지 URL을 확인한 후 설치 목록을 게시합니다.
