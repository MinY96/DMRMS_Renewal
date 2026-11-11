# pylint: disable=W0718
"""Master Recipe 관리 API Blueprint."""

from __future__ import annotations

from flask import Blueprint, request

from src.core.response import dataframe_payload, ok
from src.databases import PostgresCRUD
from src.data.no_master_data import (
    no_master_detail,
    default_column_metadata,
    apply_default_rules,
)
from src.config.config import CURRENT_FAB

no_master_bp = Blueprint("no_master", __name__)


def _selected_model() -> str:
    """요청 Model을 반환한다. TOTAL도 실제 DB 값이므로 그대로 유지한다."""
    return (request.args.get("model") or "").strip()


@no_master_bp.get("/models")
def models():
    """GET: 모델 리스트 정보 요청"""
    values = PostgresCRUD.select_query_by_list("""
        SELECT model FROM (
            SELECT model FROM dm_recipe_info WHERE is_remove=false
            UNION
            SELECT model FROM dm_matching_rate
        ) models
        WHERE model IS NOT NULL AND BTRIM(model) <> ''
        ORDER BY model
        """)
    return ok(values, "list", "Model 목록 조회 성공", {"fab": CURRENT_FAB})


@no_master_bp.get("/data")
def get_no_master_detail():
    """No Master Recipe 상세 정보를 API로 반환한다."""
    selected_model = _selected_model()
    frame, (id_cols, master_cols, params) = no_master_detail(selected_model)
    column_metadata = apply_default_rules(
        default_column_metadata(frame, id_cols, master_cols, params)
    )
    frame = frame[id_cols + ["master_group"] + master_cols + params]

    return ok(
        data={
            "table": dataframe_payload(frame.to_dict(orient="records")),
            "column_metadata": column_metadata,
        },
        data_type="dict",
        message="No Master Recipe 상세 조회 성공",
        meta={"fab": CURRENT_FAB, "source": "dummy", "row_count": len(frame)},
    )
