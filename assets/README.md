# 설치 목록 이미지

Dalamud 설치 목록의 대표 아이콘은 `assets/duckbilltweak-icon.png`에 둡니다.
이미지를 받은 뒤 원본을 추가하면 csproj의 조건부 `IconUrl`이 버전 태그의 파일을 가리킵니다.
현재는 사용자가 이미지를 준비 중이므로 아이콘 URL을 manifest에 넣지 않습니다.

소개용 스크린샷은 `assets/previews/`에 추가하고 `ImageUrls`로 연결합니다.
대표 아이콘과 소개 이미지는 서로 다른 필드입니다. 가짜 URL이나 임시 이미지를 배포하지 않습니다.

이미지를 추가한 뒤 새 Release 후보를 만들고, 태그의 공개 이미지 URL을 확인한 후 설치 목록을 게시합니다.
