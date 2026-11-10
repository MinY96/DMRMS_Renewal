"""DM RMS API 공통 응답 포맷 생성 함수."""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any
from uuid import uuid4

from flask import Response, jsonify

from .json_utils import to_json_safe


def _now_utc_iso() -> str:
    """현재 UTC 시각을 ISO-8601 문자열로 반환한다.

    Returns:
        timezone 정보가 포함된 UTC ISO-8601 문자열.
    """
    return datetime.now(timezone.utc).isoformat()


def _base_meta(meta: dict[str, Any] | None) -> dict[str, Any]:
    """모든 API 응답에 포함할 공통 메타데이터를 생성한다.

    Args:
        meta: 호출부에서 추가할 선택 메타데이터.

    Returns:
        request_id와 서버 시각이 포함된 JSON 안전 dict.
    """
    return to_json_safe(
        {
            "request_id": str(uuid4()),
            "server_time_utc": _now_utc_iso(),
            **(meta or {}),
        }
    )


def ok(
    data: Any = None,
    data_type: str = "dict",
    message: str = "OK",
    meta: dict[str, Any] | None = None,
) -> Response:
    """성공 응답을 공통 JSON 포맷으로 생성한다.

    Args:
        data: Frontend로 전달할 실제 데이터.
        data_type: 데이터 종류를 나타내는 문자열.
        message: 사용자 또는 로그에 표시할 성공 메시지.
        meta: FAB 등 추가 메타데이터.

    Returns:
        Flask JSON Response 객체.
    """
    return jsonify(
        {
            "success": True,
            "message": message,
            "data_type": data_type,
            "data": to_json_safe(data),
            "errors": [],
            "meta": _base_meta(meta),
        }
    )


def fail(
    message: str,
    status_code: int = 400,
    errors: list[dict[str, Any]] | None = None,
    meta: dict[str, Any] | None = None,
) -> tuple[Response, int]:
    """실패 응답과 HTTP 상태 코드를 공통 포맷으로 생성한다.

    Args:
        message: 대표 오류 메시지.
        status_code: 반환할 HTTP 상태 코드.
        errors: 세부 오류 목록. 없으면 UNKNOWN_ERROR를 자동 생성한다.
        meta: FAB 등 추가 메타데이터.

    Returns:
        ``(Flask Response, HTTP status code)`` 튜플.
    """
    normalized_errors = errors or [{"code": "UNKNOWN_ERROR", "message": message}]
    response = jsonify(
        {
            "success": False,
            "message": message,
            "data_type": "error",
            "data": None,
            "errors": to_json_safe(normalized_errors),
            "meta": _base_meta(meta),
        }
    )
    return response, status_code


def dataframe_payload(rows: list[dict[str, Any]]) -> dict[str, Any]:
    """레코드 목록을 Frontend DataTable 포맷으로 변환한다.

    Args:
        rows: ``DataFrame.to_dict(orient='records')`` 형태의 행 목록.

    Returns:
        columns, rows, row_count가 포함된 dict.
    """
    normalized_rows = to_json_safe(rows)
    columns: list[dict[str, str]] = []

    if normalized_rows:
        # 첫 행이 NULL일 수 있으므로 컬럼별 첫 non-null 값을 찾아 타입을 추론한다.
        for key in normalized_rows[0]:
            sample = next(
                (row.get(key) for row in normalized_rows if row.get(key) is not None),
                None,
            )
            columns.append({"name": key, "type": infer_type(sample)})

    return {
        "columns": columns,
        "rows": normalized_rows,
        "row_count": len(normalized_rows),
    }


def infer_type(value: Any) -> str:
    """Python scalar 타입을 Frontend 공통 데이터 타입 문자열로 변환한다.

    Args:
        value: 타입을 판정할 대표 값.

    Returns:
        bool, int, float, list, dict 또는 str 중 하나.
    """
    if isinstance(value, bool):
        return "bool"
    if isinstance(value, int):
        return "int"
    if isinstance(value, float):
        return "float"
    if isinstance(value, list):
        return "list"
    if isinstance(value, dict):
        return "dict"
    return "str"
