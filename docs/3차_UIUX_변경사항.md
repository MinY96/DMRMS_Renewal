# DM RMS 리뉴얼 — 3차 UI/UX 변경 사항

> 작성일: 2026-11-11  
> 기준: `MinY96/DMRMS_Renewal` 저장소의 `master` 커밋 `e2086fb6963a0655b4a9ed32d0da6b499134595e` 버전.  
> **프론트엔드 변경만 수행**하며 `1_backup`, `2_server`, DB 스키마는 이번 패치에서 수정하지 않습니다.

## 적용된 8가지 개선

| 구분 | 개선 내용 | 동작 |
|---|---|---|
| 1. Model 로딩 피드백 | 선택된 Model 안내, 로딩 인디케이터, 조회 버튼 제한 | Model 변경 중 이전 화면을 비우고 서버 조회가 끝난 뒤에만 조회 가능. 이전 요청 응답은 기존 `generation` 체크로 무시 |
| 2. 필터 정렬 | 기준정보 `WrapPanel` → `UniformGrid(3열)` | 각 열의 넓이와 여백을 맞춰 1920×1080 작업 화면에서 정렬 일관성 강화 |
| 3. 신규 Master 검증 | 필수 필드 `*`, `필수 입력` 안내, 생성 활성화 조건 | `Tech Prod`, `Tech`, `Prod Group`, `Level`, `Layer` 필수, PUMA가 아닌 Model에서 `Mode`도 필수. Parameter가 1개 이상이어야 함 |
| 4. Recipe 실행 결과 | 개별 결과/오류 메시지 및 성공·실패·미처리(확인 필요) 건수 | ZIP 내부 `result.json`의 `results`/`delete_results`를 표시. 삭제 성공 항목만 기존 목록에서 제거, 실패 항목은 목록에 남김 |
| 5. 사이드 패널 크기 | 패널별 폭 조절 슬라이더 | 왼쪽 440–850px, 오른쪽 470–900px. 패널의 슬라이드 시작/종료 위치도 최대 폭 밖으로 조정 |
| 6. 공통 비동기 상태 | `UiOperationRunner` 추가 | 로그인·신규 등록의 Busy, 진행 상태 및 예외 다이얼로그 공통 관리. Master는 기존 `IDialogService`로 관리 |
| 7. 저장 전후 비교 | Master 변경 미리보기 모달 + 변경 건수 | 기존값/변경값을 모든 변경 항목에 대해 스크롤 테이블로 표시하고 사용자 확인 후 저장 API 전송 |
| 8. ComboBox 정렬 | 전역 ComboBox + ComboBoxItem 수직 중앙 정렬 | 팝업의 로컬 ComboBox 스타일도 전역 스타일 상속하여 동일 적용 |

## 백업·삭제 결과 해석

API의 `/api/recipe/backup`, `/api/recipe/delete` 응답은 ZIP 파일이고, 내부 `result.json`에 항목별 결과가 담겨 있습니다. 서버가 명시적으로 `success=true`를 반환한 항목만 성공으로 표시합니다.

- 성공: 서버 응답에서 성공이 확인된 항목
- 실패: `result.json`의 개별 항목에 `success=false`가 기록된 항목
- 미처리·확인 필요: 요청한 항목에 대한 개별 결과가 없거나 통신 오류로 결과를 받지 못한 항목

**중요:** 통신 오류로 응답을 받지 못한 경우 서버에서 삭제가 이미 수행되었을 가능성이 있습니다. UI에서는 처리가 안 된 것으로 확정하지 않으며, 장비/DB를 재조회한 후 재시도해야 합니다. 작업이 완료되어도 사용자가 선택한 500개 제한은 변경하지 않습니다.

## 배포 확인 절차

1. `python apply_patch.py --root <저장소경로> --dry-run`으로 사전 검증
2. `python apply_patch.py --root <저장소경로>`로 적용 (기존 파일은 `.dmrms_patch_backup_v3_*`에 별도 저장)
3. `dotnet build 3_frontend/DM_RMS.Wpf.sln -c Release`를 **Windows** 환경에서 수행
4. FAB1·FAB2 각각 로그인 → Master 선택/필터 변경/신규 생성 팝업/편집 미리보기 확인
5. Recipe 2~3개 정도로 백업 전체 성공 및 일부 실패, 삭제 일부 성공 상태를 안전한 테스트 장비에서 검증
6. 패널 슬라이더(최소/최대 폭), 1920×1080·1366×768 창 축소 동작 및 콤보 수직 정렬 점검

## 수정 대상 파일

- `Resources/Theme.xaml`
- `Views/MasterManagementView.xaml`, `Views/NewMasterWindow.xaml`, `Views/RecipeManagementView.xaml`
- `ViewModels/MasterManagementViewModel.cs`, `ViewModels/NewMasterViewModel.cs`, `ViewModels/RecipeManagementViewModel.cs`
- `ViewModels/LoginViewModel.cs`, `ViewModels/RegisterViewModel.cs`
- `Models/MasterModels.cs`, `Models/RecipeSelectionItem.cs`, `Models/RecipeArchiveModels.cs`
- `Services/DialogService.cs`
- **신규:** `Services/UiOperationRunner.cs`, `Models/MasterChangePreviewItem.cs`, `ViewModels/MasterChangePreviewViewModel.cs`, `Views/MasterChangePreviewWindow.xaml(.cs)`

## 검증 범위와 제한

- 현재 실행 환경에는 Windows/.NET WPF SDK와 실제 Fab DB·설비 서버가 연결되어 있지 않습니다.
- Python 변환 로직 사전 점검, XML 구조 검사, 결과 매핑 정적 검사를 수행합니다.
- 실제 WPF 컴파일·ClickOnce 배포·DB/FTP 처리 실험은 **미실시**이며 운영 적용 전 별도 검증이 필요합니다.
