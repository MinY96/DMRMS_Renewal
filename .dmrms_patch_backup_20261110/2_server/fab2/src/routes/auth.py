"""로그인 및 사용자 등록 API Blueprint."""

from flask import Blueprint, request

from src.core.response import fail, ok
from src.config.config import CURRENT_FAB


auth_bp = Blueprint("auth", __name__)

# 개발/화면 테스트용 고정 계정입니다.
# 실제 인증/권한 로직은 운영 DB 또는 사내 인증 연동 시 교체하면 됩니다.
MOCK_USERS = {
    "admin": {
        "password": "admin",
        "display_name": "관리자",
        "role": "admin",
        "allowed_fabs": ["fab1", "fab2"],
    },
    "user": {
        "password": "user",
        "display_name": "일반 사용자",
        "role": "user",
        "allowed_fabs": ["fab1"],
    },
}


@auth_bp.post("/login")
def login():
    """로그인 요청을 검증하고 사용자 세션 정보를 반환한다."""
    body = request.get_json(silent=True) or {}
    fab = CURRENT_FAB
    user_id = str(body.get("user_id", "")).strip()
    password = str(body.get("password", ""))

    if not user_id or not password:
        return fail(
            "아이디와 패스워드를 입력해야 합니다.",
            400,
            [{"code": "LOGIN_REQUIRED", "message": "아이디/패스워드 누락"}],
        )

    user = MOCK_USERS.get(user_id)
    if user is None or user["password"] != password:
        return fail(
            "아이디 또는 패스워드가 올바르지 않습니다. 테스트 계정은 admin / admin 또는 user / user 입니다.",
            401,
            [{"code": "INVALID_CREDENTIALS", "message": "인증 실패"}],
        )

    if fab not in user["allowed_fabs"]:
        return fail(
            f"{user_id} 계정은 {fab} 접속 권한이 없습니다.",
            403,
            [{"code": "FAB_NOT_ALLOWED", "message": "사업장 권한 없음"}],
        )

    return ok(
        data={
            "fab": fab,
            "user_id": user_id,
            "display_name": user["display_name"],
            "role": user["role"],
        },
        data_type="dict",
        message="로그인 성공",
        meta={"fab": fab},
    )


@auth_bp.post("/register")
def register():
    """신규 사용자 등록 요청을 처리한다."""
    body = request.get_json(silent=True) or {}
    user_id = str(body.get("user_id", "")).strip() or "mock_user"
    return ok(
        data={"registered": True, "user_id": user_id},
        data_type="dict",
        message="회원가입 mock 성공",
    )
