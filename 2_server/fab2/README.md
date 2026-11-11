# DM RMS FAB별 API 서버

이 폴더는 `DM_RMS` WPF 프론트엔드가 호출하는 **Flask API 서버**입니다. Recipe FTP 수집과 파싱은 `1_backup/fab1`, `1_backup/fab2`에서 별도로 실행합니다.

## 설치 (Python 3.12 / Windows PowerShell)

현재 폴더에서 실행합니다.

```powershell
py -3.12 -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
```

`.env`에서 PostgreSQL/Oracle 연결 정보, `POSTGRES_SCHEMA=f1` 또는 `f2`, `DM_RMS_SERVER_PORT` 및 `DM_RMS_DEBUG=false`를 확인하세요.

## 실행

```powershell
# 개발/테스트
.\.venv\Scripts\python.exe run.py

# 운영 권장: Waitress (아래 포트는 예시)
.\.venv\Scripts\waitress-serve.exe --listen=0.0.0.0:5051 --call src:create_app
```

## API 확인

```powershell
Invoke-RestMethod 'http://127.0.0.1:5051/api/health'
Invoke-RestMethod 'http://127.0.0.1:5051/api/master/models'
Invoke-RestMethod 'http://127.0.0.1:5051/api/master/parameters/options?model=KLA2365'
Invoke-RestMethod 'http://127.0.0.1:5051/api/no_master_detail/models'
Invoke-RestMethod 'http://127.0.0.1:5051/api/no_master_detail/data?model=KLA2365'
```

운영 원칙, FAB별 백업 실행 명령, 모델별 조회, 방화벽 및 보안 주의사항은 프로젝트 루트의 [`docs/서버_운영_명령어.md`](../../docs/서버_운영_명령어.md)를 확인하세요.
