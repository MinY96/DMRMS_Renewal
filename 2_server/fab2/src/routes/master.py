# pylint: disable=W0718
"""Master Recipe 관리 API Blueprint."""

from __future__ import annotations

from io import BytesIO
import json

from flask import Blueprint, request, send_file

from src.core.response import dataframe_payload, fail, ok
from src.databases import PostgresCRUD
from src.data.master_data import (
    create_master,
    default_new_master_template,
    delete_master as delete_master_data,
    master_detail,
    master_filter_options,
    master_list,
    parameter_options,
    update_master_detail as update_master_detail_data,
)
from src.config.config import CURRENT_FAB

master_bp = Blueprint("master", __name__)


def _selected_model() -> str:
    """요청 Model을 반환한다. TOTAL도 실제 DB 값이므로 그대로 유지한다."""
    return (request.args.get("model") or "").strip()


@master_bp.get("/models")
def models():
    """GET: 모델 리스트 정보 요청"""
    values = PostgresCRUD.select_query_by_list("""
        SELECT model FROM (
            SELECT model FROM dm_matching_rate
            UNION
            SELECT model FROM dm_master_recipe_info
        ) t
        WHERE model IS NOT NULL AND BTRIM(model) <> ''
        ORDER BY model
        """)
    return ok(values, "list", "Model 목록 조회 성공", {"fab": CURRENT_FAB})


@master_bp.get("/filter-options")
def get_filter_options():
    """Master Recipe 필터 선택지를 API로 반환한다."""
    selected_model = _selected_model()
    try:
        return ok(
            data=master_filter_options(selected_model),
            data_type="dict",
            message="Master 필터 옵션 조회 성공",
            meta={"fab": CURRENT_FAB},
        )
    except Exception as ex:
        return fail(
            str(ex), 500, [{"code": "MASTER_FILTER_QUERY_ERROR", "message": str(ex)}]
        )


@master_bp.post("/list")
def get_master_list():
    """Master Recipe 기준정보 목록을 API로 반환한다."""
    try:
        body = request.get_json(silent=True) or {}
        rows = master_list(body.get("filters") or {})
        return ok(
            data=dataframe_payload(rows),
            data_type="dataframe",
            message="Master 기준정보 조회 성공",
            meta={"fab": CURRENT_FAB},
        )
    except Exception as ex:
        return fail(
            str(ex), 500, [{"code": "MASTER_LIST_QUERY_ERROR", "message": str(ex)}]
        )


@master_bp.get("/detail/<master_key>")
def get_master_detail(master_key: str):
    """Master Recipe 상세정보를 API로 반환한다.

    Args:
        master_key: 입력값 (str).
    """
    try:
        return ok(
            data=master_detail(master_key),
            data_type="dict",
            message="Master 상세 조회 성공",
            meta={"fab": CURRENT_FAB},
        )
    except Exception as ex:
        return fail(
            str(ex), 500, [{"code": "MASTER_DETAIL_QUERY_ERROR", "message": str(ex)}]
        )


@master_bp.put("/detail/<master_key>")
def update_master_detail(master_key: str):
    """지정한 Master Recipe Parameter를 transaction으로 수정한다.

    Args:
        master_key: 입력값 (str).
    """
    try:
        body = request.get_json(silent=True) or {}
        result = update_master_detail_data(master_key, body.get("parameters") or [])
        return ok(
            data=result,
            data_type="dict",
            message=f"Master 업데이트 완료: 변경 {result['updated_count']}개",
            meta={"fab": CURRENT_FAB},
        )
    except ValueError as ex:
        return fail(
            str(ex),
            400,
            [{"code": "MASTER_UPDATE_VALIDATION_ERROR", "message": str(ex)}],
        )
    except Exception as ex:
        return fail(str(ex), 500, [{"code": "MASTER_UPDATE_ERROR", "message": str(ex)}])


@master_bp.delete("/detail/<master_key>")
def delete_master(master_key: str):
    """지정한 Master Recipe와 종속 Parameter를 삭제한다.

    Args:
        master_key: 입력값 (str).
    """
    try:
        deleted = delete_master_data(master_key)
        if not deleted:
            return fail(
                "삭제 대상 Master가 없습니다.",
                404,
                [{"code": "MASTER_NOT_FOUND", "message": master_key}],
            )
        return ok(
            data={"master_key": master_key, "deleted": True},
            data_type="dict",
            message="Master 삭제 완료",
            meta={"fab": CURRENT_FAB},
        )
    except Exception as ex:
        return fail(str(ex), 500, [{"code": "MASTER_DELETE_ERROR", "message": str(ex)}])


@master_bp.get("/parameters/options")
def get_parameter_options():
    """Master Parameter 선택지를 API로 반환한다."""
    selected_model = _selected_model()
    try:
        return ok(
            data=parameter_options(selected_model),
            data_type="list",
            message="Parameter 목록 조회 성공",
            meta={"fab": CURRENT_FAB},
        )
    except Exception as ex:
        return fail(
            str(ex), 500, [{"code": "MASTER_PARAMETER_QUERY_ERROR", "message": str(ex)}]
        )


@master_bp.get("/template")
def get_template():
    """신규 Master 기본 템플릿을 API로 반환한다."""
    selected_model = _selected_model()
    try:
        return ok(
            data=default_new_master_template(selected_model),
            data_type="dict",
            message="신규 Master 기본 템플릿 조회 성공",
            meta={"fab": CURRENT_FAB},
        )
    except Exception as ex:
        return fail(
            str(ex), 500, [{"code": "MASTER_TEMPLATE_QUERY_ERROR", "message": str(ex)}]
        )


@master_bp.post("/create")
def create_master_route():
    """신규 Master Recipe 생성 API 요청을 처리한다."""
    selected_model = _selected_model()
    try:
        body = request.get_json(silent=True) or {}
        result = create_master(
            selected_model, body.get("criteria") or {}, body.get("parameters") or []
        )
        return ok(
            data=result,
            data_type="dict",
            message=f"신규 Master 생성 완료: {result['master_key']}",
            meta={"fab": CURRENT_FAB},
        )
    except ValueError as ex:
        return fail(
            str(ex),
            400,
            [{"code": "MASTER_CREATE_VALIDATION_ERROR", "message": str(ex)}],
        )
    except Exception as ex:
        return fail(str(ex), 500, [{"code": "MASTER_CREATE_ERROR", "message": str(ex)}])


@master_bp.get("/download/<master_key>")
def download_master_json(master_key: str):
    """Master Recipe를 JSON 파일로 내려준다.

    Args:
        master_key: 입력값 (str).
    """
    payload = master_detail(master_key)
    export_payload = {
        "master_key": payload["master_key"],
        "parameters": [
            {"parameter_name": p["parameter_name"], "display_value": p["display_value"]}
            for p in payload["parameters"]
        ],
    }
    stream = BytesIO(
        json.dumps(export_payload, ensure_ascii=False, indent=2).encode("utf-8")
    )
    return send_file(
        stream,
        as_attachment=True,
        download_name=f"{master_key}.json",
        mimetype="application/json; charset=utf-8",
    )


@master_bp.post("/upload-json")
def upload_master_json():
    """업로드된 Master JSON 구조를 검증한다."""
    try:
        body = request.get_json(silent=True) or {}
        return ok(
            data=body,
            data_type="dict",
            message="Master JSON 검증 완료",
            meta={"fab": CURRENT_FAB},
        )
    except Exception as ex:
        return fail(str(ex), 500)
