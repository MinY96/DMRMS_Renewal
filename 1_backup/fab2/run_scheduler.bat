@echo off
setlocal

REM 프로젝트트 경로 설정
set "PROJECT_DIR=D:\3_개발\7_DM_RMS_Renewal\1_backup\fab1"

REM 프로젝트 폴더로 이동
cd /d "%PROJECT_DIR%"

REM 로그 폴더 생성
if not exist "logs" mkdir "logs"

REM 실행 로그 저장
echo [%date% %time%] START >> "logs\scheduler.log"

REM 폴더 내 가상환경 Python 스크립트 실행
".venv\Scripts\python.exe" -u run.py >> "logs\scheduler.log" 2>&1

REM 실행 결과 기록
echo [%date% %time%] END (ExitCode=%errorlevel%) >> "logs\scheduler.log"

endlocal