"""FAB-specific user registration and authentication (PostgreSQL-backed)."""
from __future__ import annotations

import logging
import re

from flask import Blueprint, request

from src.config.config import CURRENT_FAB
from src.core.response import fail, ok
from src.databases import PostgresCRUD
from src.services.user_accounts import hash_password, verify_password

logger = logging.getLogger(__name__)
auth_bp = Blueprint("auth", __name__)
USER_ID_PATTERN = re.compile(r"^[A-Za-z0-9_.-]{3,64}$")


@auth_bp.post("/login")
def login():
    """Validate credentials against the selected FAB schema's dm_users table."""
    body = request.get_json(silent=True) or {}
    user_id = str(body.get("user_id") or "").strip()
    password = str(body.get("password") or "")
    if not user_id or not password:
        return fail("아이디와 패스워드를 입력하세요.", 400,
                    [{"code": "LOGIN_REQUIRED", "message": "아이디 또는 비밀번호 누락"}])
    try:
        account = PostgresCRUD.select_one(
            """SELECT user_id, password_hash, display_name, role
               FROM dm_users
               WHERE user_id = %s AND is_active = TRUE""", (user_id,)
        )
        if not account or not verify_password(password, account["password_hash"]):
            return fail("아이디 또는 패스워드가 올바르지 않습니다.", 401,
                        [{"code": "INVALID_CREDENTIALS", "message": "인증 실패"}])
        return ok(
            data={"fab": CURRENT_FAB, "user_id": account["user_id"],
                  "display_name": account["display_name"], "role": account["role"]},
            data_type="dict", message="로그인 성공", meta={"fab": CURRENT_FAB},
        )
    except Exception:
        logger.exception("User authentication DB error")
        return fail("사용자 인증 DB 처리에 실패했습니다.", 500,
                    [{"code": "AUTH_DB_ERROR", "message": "DB 조회 오류"}])


@auth_bp.post("/register")
def register():
    """Create an inactive-free, user-role account only in this FAB's schema.

    Public sign-up cannot supply or elevate a role. Admin provisioning is a
    separate operator-only CLI command. The client selects the FAB by API host.
    """
    body = request.get_json(silent=True) or {}
    user_id = str(body.get("user_id") or "").strip()
    password = str(body.get("password") or "")
    display_name = str(body.get("display_name") or "").strip()

    if not USER_ID_PATTERN.fullmatch(user_id):
        return fail("아이디는 영문/숫자/._- 문자 3~64자로 입력하세요.", 400,
                    [{"code": "INVALID_USER_ID", "message": "아이디 형식 오류"}])
    if not 8 <= len(password) <= 128:
        return fail("패스워드는 8~128자로 입력하세요.", 400,
                    [{"code": "INVALID_PASSWORD", "message": "패스워드 길이 오류"}])
    if not 1 <= len(display_name) <= 100:
        return fail("표시 이름은 1~100자로 입력하세요.", 400,
                    [{"code": "INVALID_DISPLAY_NAME", "message": "표시 이름 오류"}])
    try:
        hashed = hash_password(password)
        affected = PostgresCRUD.execute(
            """INSERT INTO dm_users (user_id, password_hash, display_name, role, is_active)
               VALUES (%s, %s, %s, 'user', TRUE)
               ON CONFLICT (user_id) DO NOTHING""",
            (user_id, hashed, display_name),
        )
        if not affected:
            return fail("이미 등록된 아이디입니다.", 409,
                        [{"code": "USER_EXISTS", "message": "아이디 중복"}])
        return ok(data={"registered": True, "user_id": user_id},
                  data_type="dict", message="사용자 등록 완료", meta={"fab": CURRENT_FAB})
    except Exception:
        logger.exception("User registration DB error")
        return fail("사용자 등록 DB 처리에 실패했습니다.", 500,
                    [{"code": "REGISTER_DB_ERROR", "message": "DB 저장 오류"}])
