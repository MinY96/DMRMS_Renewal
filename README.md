# DM RMS 리뉴얼 통합 프로그램

FAB1/FAB2 사업장에서 사용되는 Recipe Matching Rate 및 Master Recipe 관리 프로그램입니다.

## 프로젝트 구성

- `1_backup/fab1`, `1_backup/fab2`: 장비 FTP 백업, 파싱 및 DB 갱신 배치
- `2_server/fab1`, `2_server/fab2`: Flask 기반 사업장별 API 서버
- `3_frontend`: WPF MVVM Windows 사용자 클라이언트

클라이언트는 DB에 직접 연결하지 않으며, 로그인한 FAB의 API 서버를 호출합니다. PostgreSQL `dm_rms` 데이터베이스에서 사업장별 스키마 `f1`, `f2`를 사용합니다.

## 2차 수정 사항 (2026-11-10)

- 장비별 Recipe 현황의 Matching Rate를 실제 백분율로 표시합니다.
- 파싱된 Recipe를 이용해 Master 미등록 Model의 기준정보/Parameter/고유값 선택지를 제공합니다.
- No Master Recipe 조회 탭에 Model 선택 기능과 자동 조회를 추가합니다.
- 불일치 Recipe 및 집계 제외 Recipe 조회 탭을 열 때 서버 데이터를 다시 조회합니다.
- 사업장별 No Master 0건 반환 형식과 NULL 기준정보 그룹 처리 문제를 보완합니다.

## 설치 및 실행

서버 운영 환경 구성, Python 3.12 가상환경, Waitress 실행, FAB별 백업 배치 실행, API 상태 확인은 [`docs/서버_운영_명령어.md`](docs/서버_운영_명령어.md)를 참고하세요.

Windows PC에서 프론트엔드 빌드:

```powershell
dotnet build 3_frontend\DM_RMS.Wpf.sln -c Release
```

소스 버전 변경 시에는 반드시 FAB1/FAB2 API와 프론트엔드 버전을 함께 검증하세요. 데이터베이스 덤프, `.env`, 비밀번호 및 장비 설정값은 GitHub 공개 저장소에 게시하지 마세요.
