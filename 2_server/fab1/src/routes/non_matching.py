# pylint: disable=W0718
"""Matching Rate 상세 조회용 더미 DataFrame 및 컬럼 시각화 메타데이터 API Blueprint."""

from __future__ import annotations

from flask import Blueprint

from src.core.response import dataframe_payload, ok
from src.config.config import CURRENT_FAB
from src.data.non_matching_data import (
    non_matching_detail,
    default_column_metadata,
    apply_default_rules,
)


non_matching_bp = Blueprint("non_matching", __name__)


@non_matching_bp.get("/data")
def get_non_matching_detail():
    """불일치 Parameter 존재 Recipe 상세 정보를 API로 반환한다."""
    frame = non_matching_detail()
    column_metadata = default_column_metadata(frame)
    # Header + Detail Row의 Header 행을 시각적으로 구분한다.
    for column_name in (
        "master_key",
        "model",
        "equip",
        "device",
        "recipe",
        "dt_edit_time",
        "tech_prod",
        "tech",
        "prod_group",
        "level",
        "layer",
        "mode",
    ):
        column_metadata[column_name].setdefault("rules", []).append(
            {
                "condition_column": "_row_type",
                "operator": "eq",
                "value": "HEADER",
                "priority": 100,
                "style": {
                    "background": "#EAF2F8",
                    "foreground": "#1F4E78",
                    "font_weight": "Bold",
                },
            }
        )
    column_metadata = apply_default_rules(column_metadata)
    return ok(
        data={
            "table": dataframe_payload(frame.to_dict(orient="records")),
            "column_metadata": column_metadata,
        },
        data_type="dict",
        message="Non Matching Recipe 상세 조회 성공",
        meta={"fab": CURRENT_FAB, "row_count": len(frame)},
    )
