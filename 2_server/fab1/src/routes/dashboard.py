"""Recipe Matching 현황 Dashboard API."""

from __future__ import annotations

from io import BytesIO
from typing import Any

import pandas as pd
from flask import Blueprint, request, send_file

from src.core.response import dataframe_payload, ok
from src.core.convert import to_records
from src.config.config import CURRENT_FAB
from src.databases import PostgresCRUD
from src.data.dashboard_data import (
    cards,
    matching_history,
    update_history,
    exclude_status,
    export_status,
    non_matching_parameter,
    matching_chart_html,
    update_chart_html,
    horizontal_bar_chart_html,
)

dashboard_bp = Blueprint("dashboard", __name__)


def _selected_model() -> str:
    """요청 Model을 반환한다. TOTAL도 실제 DB 값이므로 그대로 유지한다."""
    return (request.args.get("model") or "").strip()


def _selected_equipment() -> str:
    """요청 Equip를 반환한다. TOTAL도 실제 DB 값이므로 그대로 유지한다."""
    return (request.args.get("equipment") or "").strip()


@dashboard_bp.get("/models")
def models():
    """GET: 모델 리스트 정보 요청"""
    values = PostgresCRUD.select_query_by_list("""
        SELECT DISTINCT dmr.model 
        FROM dm_matching_rate dmr 
        ORDER BY dmr.model
        """)
    return ok(values, "list", "Model 목록 조회 성공", {"fab": CURRENT_FAB})


@dashboard_bp.get("/equipments")
def equipments():
    """GET: 장비 리스트 정보 요청"""
    selected_model = _selected_model()
    if not selected_model:
        values: list[Any] = []
    else:
        values = PostgresCRUD.select_query_by_list(
            """
            SELECT DISTINCT dmr.equip 
            FROM dm_matching_rate dmr 
            WHERE dmr.model = %s 
            ORDER BY dmr.equip
            """,
            (selected_model,),
        )
    return ok(
        values,
        "list",
        "장비 목록 조회 성공",
        {"fab": CURRENT_FAB, "model": selected_model},
    )


@dashboard_bp.get("/summary")
def summary():
    """GET: 선택한 장비/모델 요약 정보(Matching Rate/Update history/집계 제외 현황/불일치 현황) 요청"""
    model = _selected_model()
    equipment = _selected_equipment()
    matching_frame = matching_history(model, equipment)
    update_frame = update_history(model, equipment)
    exclude_frame = exclude_status(model, equipment)
    non_matching_para_frame = non_matching_parameter(model, equipment)
    matching_records = to_records(
        matching_frame.sort_values("aggregate_time", ascending=False).reset_index(
            drop=True
        )
    )
    update_records = to_records(
        update_frame.sort_values("aggregate_time", ascending=False).reset_index(
            drop=True
        )
    )

    return ok(
        {
            "cards": cards(matching_frame, model, equipment),
            "matching_rate": dataframe_payload(matching_records),
            "update_history": dataframe_payload(update_records),
            "exclude_status": dataframe_payload(to_records(exclude_frame)),
            "non_matching_parameter": dataframe_payload(
                to_records(non_matching_para_frame)
            ),
        },
        "dict",
        "Dashboard 조회 성공",
        {
            "fab": CURRENT_FAB,
            "model": model,
            "equipment": equipment,
            "row_count": len(matching_frame),
        },
    )


@dashboard_bp.get("/charts")
def charts():
    """GET: 선택한 장비/모델 차트 요청"""
    model = _selected_model()
    equipment = _selected_equipment()
    matching_frame = matching_history(model, equipment)
    update_frame = update_history(model, equipment)
    exclude_frame = (
        exclude_status(model, equipment, False)
        .sort_values("exclude_count", ascending=True)
        .reset_index(drop=True)
    )
    non_matching_para_frame = (
        non_matching_parameter(model, equipment, False)
        .sort_values("parameter_count", ascending=True)
        .reset_index(drop=True)
    )
    return ok(
        {
            "matching_rate_html": matching_chart_html(matching_frame, model, equipment),
            "update_history_html": update_chart_html(update_frame, model, equipment),
            "exclude_status_html": horizontal_bar_chart_html(
                exclude_frame["exclude_count"].to_list(),
                exclude_frame["exclude_code"].to_list(),
                "건수",
                "집계 제외 코드",
                "집계 제외 코드 별 건수",
            ),
            "non_matching_parameter_html": horizontal_bar_chart_html(
                non_matching_para_frame["parameter_count"].to_list(),
                non_matching_para_frame["parameter_name"].to_list(),
                "건수",
                "Parameter",
                "Parameter 별 불일치 건수",
            ),
        },
        "dict",
        "Dashboard 차트 조회 성공",
        {
            "fab": CURRENT_FAB,
            "model": model,
            "equipment": equipment,
            "row_count": len(matching_frame),
        },
    )


@dashboard_bp.get("/matching-detail/latest")
def matching_detail_latest():
    """GET: 선택한 장비/모델 최신 상세 Recipe 현황 요청"""
    model = _selected_model()
    equipment = _selected_equipment()
    frame = export_status(model, equipment)
    output = BytesIO()
    with pd.ExcelWriter(output, engine="openpyxl") as writer:
        frame.to_excel(writer, index=False, sheet_name="Matching_상세정보")
    output.seek(0)
    return send_file(
        output,
        as_attachment=True,
        download_name=f"matching_detail_{model or 'EMPTY'}_{equipment or 'EMPTY'}.xlsx",
        mimetype="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    )
