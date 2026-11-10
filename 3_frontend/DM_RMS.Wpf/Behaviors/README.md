# Behaviors

DataGrid 관련 로직은 View code-behind에 넣지 않고 Behavior로 분리합니다.

예정 Behavior 예시:
- DataGridCopyBehavior: 현재 DataView 클립보드 복사
- DataGridExcelExportBehavior: 현재 DataView Excel 저장
- DataGridHighlightBehavior: matching == false 행 하이라이트
- DataGridButtonColumnBehavior: 조회/삭제/취소 버튼 컬럼 동적 생성
- DataGridDoubleClickCommandBehavior: 행 더블클릭 Command 연결
- DataGridColumnFilterBehavior: 컬럼별 Text Like / Combo Equals 필터 연결
