"""DM RMS 클라이언트 사용 세션 시작/종료 이력 API."""

from __future__ import annotations

from datetime import datetime, timezone
from uuid import UUID

from flask import Blueprint, request

from src.core.response import fail, ok
from src.config.config import CURRENT_FAB
from src.databases.postgre_db import PostgresCRUD

usage_bp = Blueprint("usage", __name__)


def _client_ip() -> str:
    """사용자 ip"""
    forwarded = request.headers.get("X-Forwarded-For", "")
    if forwarded:
        return forwarded.split(",", 1)[0].strip()
    return request.remote_addr or ""


def _normalize_session_id(raw_value: object) -> str:
    """세션 ID 정규화"""
    return str(UUID(str(raw_value)))


@usage_bp.post("/start")
def start_usage_session():
    "사용자 세션 시작"
    body = request.get_json(silent=True) or {}
    try:
        session_id = _normalize_session_id(body.get("session_id", ""))
    except (ValueError, TypeError, AttributeError):
        return fail(
            "유효한 session_id가 필요합니다.",
            400,
            [
                {
                    "code": "INVALID_SESSION_ID",
                    "message": "session_id는 UUID 형식이어야 합니다.",
                }
            ],
        )

    user_id = str(body.get("user_id", "")).strip()
    pc_name = str(body.get("pc_name", "")).strip()
    if not user_id or not pc_name:
        return fail(
            "user_id와 pc_name이 필요합니다.",
            400,
            [{"code": "USAGE_REQUIRED_FIELDS", "message": "user_id 또는 pc_name 누락"}],
        )

    recorded_at = datetime.now(timezone.utc)
    PostgresCRUD.execute(
        """
        INSERT INTO dm_program_usage_history (
            session_id, fab, user_id, display_name, user_role,
            pc_name, os_user, os_version, process_architecture,
            app_version, client_ip, login_time, logout_time
        ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, NULLIF(%s, '')::inet, %s, NULL)
        ON CONFLICT (session_id) DO UPDATE SET
            fab = EXCLUDED.fab,
            user_id = EXCLUDED.user_id,
            display_name = EXCLUDED.display_name,
            user_role = EXCLUDED.user_role,
            pc_name = EXCLUDED.pc_name,
            os_user = EXCLUDED.os_user,
            os_version = EXCLUDED.os_version,
            process_architecture = EXCLUDED.process_architecture,
            app_version = EXCLUDED.app_version,
            client_ip = EXCLUDED.client_ip,
            login_time = EXCLUDED.login_time,
            logout_time = NULL
        """,
        (
            session_id,
            CURRENT_FAB,
            user_id,
            str(body.get("display_name", "")).strip(),
            str(body.get("role", "")).strip(),
            pc_name,
            str(body.get("os_user", "")).strip(),
            str(body.get("os_version", "")).strip(),
            str(body.get("process_architecture", "")).strip(),
            str(body.get("app_version", "")).strip(),
            _client_ip(),
            recorded_at,
        ),
    )
    return ok(
        data={"session_id": session_id, "recorded_at": recorded_at.isoformat()},
        data_type="dict",
        message="프로그램 사용 시작 이력 저장 성공",
        meta={"fab": CURRENT_FAB},
    )


@usage_bp.post("/end")
def end_usage_session():
    "사용자 세션 종료"
    body = request.get_json(silent=True) or {}
    try:
        session_id = _normalize_session_id(body.get("session_id", ""))
    except (ValueError, TypeError, AttributeError):
        return fail(
            "유효한 session_id가 필요합니다.",
            400,
            [
                {
                    "code": "INVALID_SESSION_ID",
                    "message": "session_id는 UUID 형식이어야 합니다.",
                }
            ],
        )

    user_id = str(body.get("user_id", "")).strip()
    recorded_at = datetime.now(timezone.utc)
    affected = PostgresCRUD.execute(
        """
        UPDATE dm_program_usage_history
           SET logout_time = %s
         WHERE session_id = %s
           AND (%s = '' OR user_id = %s)
           AND logout_time IS NULL
        """,
        (recorded_at, session_id, user_id, user_id),
    )
    if affected == 0:
        return fail(
            "종료할 사용 세션을 찾을 수 없습니다.",
            404,
            [{"code": "USAGE_SESSION_NOT_FOUND", "message": session_id}],
        )

    return ok(
        data={"session_id": session_id, "recorded_at": recorded_at.isoformat()},
        data_type="dict",
        message="프로그램 사용 종료 이력 저장 성공",
        meta={"fab": CURRENT_FAB},
    )
