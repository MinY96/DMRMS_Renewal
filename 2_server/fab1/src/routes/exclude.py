# pylint: disable=W0718
"""집계 제외 Recipe 상세 조회용  컬럼 시각화 메타데이터 API Blueprint."""

from __future__ import annotations

from flask import Blueprint

from src.core.response import dataframe_payload, ok
from src.config.config import CURRENT_FAB
from src.data.exclude_data import (
    exclude_detail,
    default_column_metadata,
    apply_default_rules,
)


exclude_bp = Blueprint("exclude", __name__)


@exclude_bp.get("/data")
def get_exclude_detail():
    """집계 제외 Recipe 상세 정보를 API로 반환한다."""
    frame = exclude_detail()
    column_metadata = apply_default_rules(default_column_metadata(frame))
    return ok(
        data={
            "table": dataframe_payload(frame.to_dict(orient="records")),
            "column_metadata": column_metadata,
        },
        data_type="dict",
        message="Exclude Recipe 상세 조회 성공",
        meta={"fab": CURRENT_FAB, "row_count": len(frame)},
    )
