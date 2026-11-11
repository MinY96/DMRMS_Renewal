"""FAB1 대시보드 테이블, 차트 생성 로직."""

from __future__ import annotations

from typing import List

import numpy as np
import pandas as pd
import plotly.graph_objects as go
from plotly.subplots import make_subplots

from src.databases import PostgresCRUD

MATCHING_HISTORY_SQL = """
SELECT
    dmr.aggregation_time AS aggregate_time,
    dmr.matching_rate AS matching_rate,
    dmr.recipe AS recipe_count,
    dmr.matching AS matching_recipe_count,
    dmr.non_matching AS non_matching_recipe_count,
    dmr.exclude_matching AS exclude_recipe_count
FROM dm_matching_rate AS dmr
WHERE dmr.model = %s
  AND dmr.equip = %s
  AND dmr.aggregation_time >= to_char(now() - '7 days'::interval, 'YYYY-MM-DD')
ORDER BY dmr.aggregation_time
"""

UPDATE_HISTORY_SQL = """
SELECT 
	duh.aggregation_time aggregate_time, 
	coalesce(duh.new_rcp, 0) new_recipe_count,
	coalesce(duh.update_rcp, 0) update_recipe_count,
	coalesce(duh.remove_rcp, 0) remove_recipe_count
FROM dm_update_history duh
WHERE duh.model = %s 
	AND duh.equip = %s
	AND duh.aggregation_time >= to_char(now() - '7 days'::interval, 'YYYY-MM-DD')
ORDER BY duh.aggregation_time 
"""

EXCLUDE_MATCHING_SQL = """
SELECT dri.exclude_code, count(dri.exclude_code) exclude_count
FROM dm_recipe_info dri
WHERE dri.is_remove = false 
	and dri.exclude_matching = true
	and dri.model = %s
	and dri.equip = %s
GROUP BY dri.exclude_code
ORDER BY count(dri.exclude_code) desc, dri.exclude_code
"""

NON_MATCHING_PARA_SQL = """
SELECT drp.para_nm parameter_name,
    count(drp.para_nm) parameter_count 
FROM dm_recipe_info dri 
LEFT JOIN dm_recipe_para drp 
ON (dri.recipe_key = drp.recipe_key)
WHERE dri.is_remove = false
	AND dri.exclude_matching = false
	AND dri.model = %s
	AND dri.equip = %s
	AND dri.matching = false
	AND drp.matching = false
GROUP BY drp.para_nm
ORDER BY count(drp.para_nm) desc, drp.para_nm
"""

EXPORT_STATUS_SQL = """
SELECT dri.recipe_key, dri.model, dri.equip, dri.device, dri.recipe, dri.dt_edit_time,
	dri.tech_prod, dri.tech, dri.prod_group, dri.level, dri.layer, dri.mode,
	drp.para_nm, drp.para_value, dri.matching, drp.matching para_matching
FROM dm_recipe_info dri 
LEFT JOIN dm_recipe_para drp 
ON (dri.recipe_key = drp.recipe_key)
WHERE dri.model = %s
	AND dri.equip LIKE %s
	AND dri.is_remove = false
	AND dri.exclude_matching = false
	AND dri.master_key is not null
	AND dri.matching is not null
	AND drp.matching is not null
    AND drp.para_nm != 'mode'
ORDER BY dri.tech_prod, dri.tech, dri.prod_group, dri.level,
    dri.layer, dri.mode, dri.equip, dri.device, dri.recipe
"""


def matching_history(model: str, equipment: str) -> pd.DataFrame:
    """장비 단위 Matching Rate 최근 7일 Trend 테이블 반환

    Args:
        model (str): Model명
        equipment (str): 장비명

    Returns:
        pd.DataFrame: Matching Rate history 테이블
    """
    columns = [
        "matching_rate",
        "recipe_count",
        "matching_recipe_count",
        "non_matching_recipe_count",
        "exclude_recipe_count",
        "aggregate_time",
    ]
    if not model or not equipment:
        return pd.DataFrame(columns=columns)

    frame = PostgresCRUD.select_query(MATCHING_HISTORY_SQL, (model, equipment))
    if frame.empty:
        return pd.DataFrame(columns=columns)

    numeric_columns = columns[:-1]
    for column in numeric_columns:
        frame[column] = pd.to_numeric(frame[column], errors="coerce").fillna(0)

    frame["aggregate_time"] = pd.to_datetime(frame["aggregate_time"], errors="coerce")
    return (
        frame.dropna(subset=["aggregate_time"])
        .sort_values("aggregate_time")
        .reset_index(drop=True)
    )


def update_history(model: str, equipment: str) -> pd.DataFrame:
    """장비 단위 Recipe 업데이트 히스토리 반환

    Args:
        model (str): Model명
        equipment (str): 장비명

    Returns:
        pd.DataFrame: Recipe 업데이트 히스토리 테이블
    """
    columns = [
        "aggregate_time",
        "new_recipe_count",
        "update_recipe_count",
        "remove_recipe_count",
    ]
    if not model or not equipment:
        return pd.DataFrame(columns=columns)

    frame = PostgresCRUD.select_query(UPDATE_HISTORY_SQL, (model, equipment))
    if frame.empty:
        return pd.DataFrame(columns=columns)

    frame["aggregate_time"] = pd.to_datetime(frame["aggregate_time"], errors="coerce")
    return (
        frame.dropna(subset=["aggregate_time"])
        .sort_values("aggregate_time")
        .reset_index(drop=True)
    )


def _matching_exclude_code(code: str):
    """집계 제외 코드 매칭"""
    match code:
        case "EXC_MASTER":
            return "매칭되는 Master Recipe가 없는 경우"
        case "EXC_TEST":
            return "Test 목적으로 생성된 Recipe의 경우"
        case "EXC_DEVICE":
            return "Device 가 4자리가 아닌 경우"
        case "EXC_LAYER":
            return "Layer 가 8자리가 아닌 경우"
        case "EXC_PARSE":
            return "Recipe 파싱 시 에러가 발생한 경우"
        case "EXC_RECIPE":
            return "정규 Recipe명이 아닌 경우"
        case "EXC_IGBT":
            return "IGBT 양산 전"
        case "EXC_MISS":
            return "기준 정보를 찾을 수 없는 경우"
    return ""


def exclude_status(model: str, equipment: str, fill_mode: bool = True) -> pd.DataFrame:
    """장비 단위 집계 제외 현황 테이블 조회

    Args:
        model (str): Model명
        equipment (str): 장비명
        fill_mode (bool, optional): 공백 '-' 자동 채우기 여부. Defaults to True.

    Returns:
        pd.DataFrame: 장비 단위 집계 제외 현황 히스토리 테이블
    """
    columns = [
        "exclude_code",
        "exclude_count",
    ]
    if not model or not equipment:
        return pd.DataFrame(columns=columns)

    frame = PostgresCRUD.select_query(EXCLUDE_MATCHING_SQL, (model, equipment))
    frame["description"] = frame.apply(
        lambda x: _matching_exclude_code(x["exclude_code"]), axis=1
    )
    if frame.empty and fill_mode:
        return pd.DataFrame([{col: "-" for col in columns}])

    return frame


def non_matching_parameter(
    model: str, equipment: str, fill_mode: bool = True
) -> pd.DataFrame:
    """장비 내 존재하는 파라미터 별 불일치 건수 테이블 조회

    Args:
        model (str): Model명
        equipment (str): 장비명
        fill_mode (bool, optional): 공백 '-' 자동 채우기 여부. Defaults to True.

    Returns:
        pd.DataFrame: 장비 내 파라미터 별 불일치 건수 테이블
    """
    columns = [
        "parameter_name",
        "parameter_count",
    ]
    if not model or not equipment:
        return pd.DataFrame(columns=columns)

    frame = PostgresCRUD.select_query(NON_MATCHING_PARA_SQL, (model, equipment))
    if frame.empty and fill_mode:
        return pd.DataFrame([{col: "-" for col in columns}])

    return frame


def export_status(model: str, equipment: str) -> pd.DataFrame:
    """장비 내 상세 Recipe 정보 테이블 조회

    Args:
        model (str): Model명
        equipment (str): 장비명

    Returns:
        pd.DataFrame: 장비 단위 상세 Recipe 정보 테이블
    """
    master_columns = [
        "model",
        "equip",
        "device",
        "recipe",
        "dt_edit_time",
    ]
    main_columns = [
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
    ]
    main_columns = (
        main_columns + ["mode", "matching"]
        if model != "PUMA"
        else main_columns + ["matching"]
    )
    parameters = []
    if equipment.upper() == "TOTAL":
        equipment = "%"
    else:
        equipment = "%" + equipment
    frame = PostgresCRUD.select_query(EXPORT_STATUS_SQL, (model, equipment))
    if frame.empty:
        return pd.DataFrame(columns=main_columns + parameters)
    parameters = sorted(frame["para_nm"].unique().tolist())
    pivot = frame.pivot_table(
        index=["recipe_key"],
        columns=["para_nm"],
        values=["para_value", "para_matching"],
        fill_value="",
        aggfunc="first",
    )
    export_frame = frame.drop_duplicates(subset="recipe_key").merge(
        pivot["para_value"].reset_index(), on="recipe_key", how="left"
    )
    mask_frame = frame.drop_duplicates(subset="recipe_key").merge(
        pivot["para_matching"].reset_index(), on="recipe_key", how="left"
    )

    mask_frame = mask_frame.astype(bool)
    mask_frame[master_columns] = mask_frame[master_columns].fillna(True)
    mask_frame[master_columns] = True
    mask_frame = mask_frame.fillna(True)

    def _highlight_false(row, b_df):
        styles = np.where(
            ~b_df, "backgroud-color: yellow; color: red; font-weight: bold", ""
        )
        return pd.DataFrame(styles, index=row.index, columns=row.columns)

    export_frame = export_frame[main_columns + parameters]
    mask_frame = mask_frame[main_columns + parameters]
    styled_df = export_frame.style.apply(_highlight_false, b_df=mask_frame, axis=None)
    return styled_df


def matching_chart_html(frame: pd.DataFrame, model: str, equipment: str) -> str:
    figure = make_subplots(specs=[[{"secondary_y": True}]])

    if frame.empty:
        figure.add_annotation(
            text="Model과 Equip를 선택하거나 조회 데이터를 확인해 주세요.",
            x=0.5,
            y=0.5,
            xref="paper",
            yref="paper",
            showarrow=False,
            font={"size": 14, "color": "#718096"},
        )
        figure.update_xaxes(visible=False)
        figure.update_yaxes(visible=False)
    else:
        labels = frame["aggregate_time"].dt.strftime("%y/%m/%d<br>%H:%M")

        figure.add_trace(
            go.Bar(
                x=labels,
                y=frame["non_matching_recipe_count"],
                name="Non Matching Recipe",
                width=0.45,
                text=frame["non_matching_recipe_count"].map(
                    lambda value: f"{int(value):,}"
                ),
                textposition="outside",
                textfont={"size": 11},
                marker={
                    "color": "#E45C5C",
                    "line": {"color": "#C94444", "width": 1},
                },
                hovertemplate=(
                    "<b>%{x}</b><br>" "Non Matching : %{y:,}" "<extra></extra>"
                ),
            ),
            secondary_y=False,
        )

        figure.add_trace(
            go.Scatter(
                x=labels,
                y=frame["matching_rate"] * 100,
                name="Matching Rate",
                mode="lines+markers+text",
                line={"color": "#2F6BFF", "width": 3},
                marker={
                    "size": 8,
                    "color": "white",
                    "line": {"color": "#2F6BFF", "width": 2},
                },
                text=[f"{value*100:.2f}%" for value in frame["matching_rate"]],
                textposition="top center",
                cliponaxis=False,
                hovertemplate=(
                    "<b>%{x}</b><br>" "Matching Rate : %{y:.2f}%" "<extra></extra>"
                ),
            ),
            secondary_y=True,
        )

        recipe_max = (
            float(frame["non_matching_recipe_count"].max()) if not frame.empty else 0.0
        )
        rate_min = float(frame["matching_rate"].min())
        rate_max = float(frame["matching_rate"].max())
        figure.update_yaxes(
            title_text="Non Matching Recipe Count",
            range=[0, max(recipe_max * 1.15, 1)],
            tickformat=",",
            gridcolor="#E7ECF3",
            zeroline=False,
            secondary_y=False,
        )
        figure.update_yaxes(
            title_text="Matching Rate (%)",
            ticksuffix="%",
            showgrid=False,
            zeroline=False,
            secondary_y=True,
        )
        figure.update_xaxes(title_text="집계 시간", type="category", showgrid=False)

    figure.update_layout(
        template="plotly_white",
        barmode="overlay",
        hovermode="x unified",
        legend={"orientation": "h", "yanchor": "bottom", "y": 1.2, "x": 0.01},
        margin={"l": 65, "r": 65, "t": 65, "b": 75},
        autosize=True,
        paper_bgcolor="#FFFFFF",
        plot_bgcolor="#FFFFFF",
        font={"family": "Malgun Gothic, Arial, sans-serif", "size": 11},
        title={
            "text": f"{model or '-'} / {equipment or '-'} Recipe Matching History",
            "x": 0.5,
            "y": 0.98,
            "font": {"size": 14},
        },
    )
    return figure.to_html(
        full_html=True,
        include_plotlyjs="cdn",
        config={"displaylogo": False, "responsive": True, "scrollZoom": False},
    )


def update_chart_html(frame: pd.DataFrame, model: str, equipment: str) -> str:
    figure = make_subplots()

    if frame.empty:
        figure.add_annotation(
            text="Model과 Equip를 선택하거나 조회 데이터를 확인해 주세요.",
            x=0.5,
            y=0.5,
            xref="paper",
            yref="paper",
            showarrow=False,
            font={"size": 14, "color": "#718096"},
        )
        figure.update_xaxes(visible=False)
        figure.update_yaxes(visible=False)
    else:
        labels = frame["aggregate_time"].dt.strftime("%y/%m/%d<br>%H:%M")

        figure.add_trace(
            go.Bar(
                x=labels,
                y=frame["new_recipe_count"],
                name="신규 Recipe",
                text=frame["new_recipe_count"].map(lambda value: f"{int(value):,}"),
                textposition="outside",
                textfont={"size": 11},
                hovertemplate=("신규 : %{y:,}" "<extra></extra>"),
            ),
        )
        figure.add_trace(
            go.Bar(
                x=labels,
                y=frame["update_recipe_count"],
                name="수정 Recipe",
                text=frame["update_recipe_count"].map(lambda value: f"{int(value):,}"),
                textposition="outside",
                textfont={"size": 11},
                hovertemplate=("수정 : %{y:,}" "<extra></extra>"),
            ),
        )
        figure.add_trace(
            go.Bar(
                x=labels,
                y=frame["remove_recipe_count"],
                name="삭제 Recipe",
                text=frame["remove_recipe_count"].map(lambda value: f"{int(value):,}"),
                textposition="outside",
                textfont={"size": 11},
                hovertemplate=("삭제 : %{y:,}" "<extra></extra>"),
            ),
        )
        recipe_max = max(
            [
                frame["new_recipe_count"].max(),
                frame["update_recipe_count"].max(),
                frame["remove_recipe_count"].max(),
            ]
        )

        recipe_max = float(recipe_max) if not frame.empty else 0.0
        figure.update_yaxes(
            title_text="Recipe Count",
            tickformat=",",
            range=[0, max(recipe_max * 1.3, 1)],
            gridcolor="#E7ECF3",
            zeroline=False,
        )
        figure.update_xaxes(title_text="집계 시간", type="category", showgrid=False)

    figure.update_layout(
        template="plotly_white",
        barmode="group",
        hovermode="x unified",
        legend={"orientation": "h", "yanchor": "bottom", "y": 1.2, "x": 0.01},
        margin={"l": 65, "r": 65, "t": 65, "b": 75},
        autosize=True,
        paper_bgcolor="#FFFFFF",
        plot_bgcolor="#FFFFFF",
        font={"family": "Malgun Gothic, Arial, sans-serif", "size": 11},
        title={
            "text": f"{model or '-'} / {equipment or '-'} Recipe 업데이트 History",
            "x": 0.5,
            "y": 0.98,
            "font": {"size": 14},
        },
    )
    return figure.to_html(
        full_html=True,
        include_plotlyjs="cdn",
        config={"displaylogo": False, "responsive": True, "scrollZoom": False},
    )


def horizontal_bar_chart_html(
    x_counts: List[int],
    y_categories: List[str],
    x_axis_title: str,
    y_axis_title: str,
    title: str,
) -> str:
    figure = make_subplots()

    if len(x_counts) == 0:
        figure.add_annotation(
            text="조회되는 Data가 없습니다.",
            x=0.5,
            y=0.5,
            xref="paper",
            yref="paper",
            showarrow=False,
            font={"size": 14, "color": "#718096"},
        )
        figure.update_xaxes(visible=False)
        figure.update_yaxes(visible=False)
    else:
        figure.add_trace(
            go.Bar(
                x=x_counts,
                y=y_categories,
                name=y_axis_title,
                text=[f"{int(value):,}" for value in x_counts],
                textposition="outside",
                textfont={"size": 11},
                hovertemplate=("%{y} : %{x:,}"),
                orientation="h",
            ),
        )
        figure.update_xaxes(
            title_text=x_axis_title,
            tickformat=",",
            gridcolor="#E7ECF3",
            zeroline=True,
        )
        figure.update_yaxes(title_text=y_axis_title, type="category", showgrid=False)

    figure.update_layout(
        template="plotly_white",
        # legend={"orientation": "h", "yanchor": "bottom", "y": 1.2, "x": 0.01},
        margin={"l": 65, "r": 65, "t": 65, "b": 75},
        autosize=True,
        paper_bgcolor="#FFFFFF",
        plot_bgcolor="#FFFFFF",
        font={"family": "Malgun Gothic, Arial, sans-serif", "size": 11},
        title={
            "text": title,
            "x": 0.5,
            "y": 0.98,
            "font": {"size": 14},
        },
    )
    return figure.to_html(
        full_html=True,
        include_plotlyjs="cdn",
        config={"displaylogo": False, "responsive": True, "scrollZoom": False},
    )


def empty_update_chart() -> str:
    figure = go.Figure()
    figure.add_annotation(
        text="조회되는 Data가 없습니다.",
        x=0.5,
        y=0.5,
        xref="paper",
        yref="paper",
        showarrow=False,
        font={"size": 14, "color": "#718096"},
    )
    figure.update_layout(
        template="plotly_white", height=300, margin={"l": 30, "r": 20, "t": 20, "b": 30}
    )
    figure.update_xaxes(visible=False)
    figure.update_yaxes(visible=False)
    return figure.to_html(
        full_html=True,
        include_plotlyjs="cdn",
        config={"displaylogo": False, "responsive": True},
    )


def cards(frame: pd.DataFrame, model: str, equipment: str) -> list[dict[str, str]]:
    if frame.empty:
        return [
            {
                "title": "조회 장비",
                "value": "-",
                "description": "Model / Equip를 선택하세요",
            },
            {
                "title": "최신 Matching Rate",
                "value": "-",
                "description": "조회 데이터 없음",
            },
            {"title": "관리 Recipe", "value": "-", "description": "조회 데이터 없음"},
            {"title": "Non Matching", "value": "-", "description": "조회 데이터 없음"},
            {"title": "No Master", "value": "-", "description": "조회 데이터 없음"},
            {"title": "집계 제외", "value": "-", "description": "조회 데이터 없음"},
        ]
    latest = frame.iloc[-1]
    return [
        {"title": "조회 장비", "value": equipment, "description": f"Model: {model}"},
        {
            "title": "최신 Matching Rate",
            "value": f"{float(latest['matching_rate']*100):.2f}%",
            "description": f"집계 {latest['aggregate_time']:%Y-%m-%d %H:%M}",
        },
        {
            "title": "관리 Recipe",
            "value": f"{int(latest['recipe_count']):,}",
            "description": "전체 Recipe 수",
        },
        {
            "title": "Non Matching",
            "value": f"{int(latest['non_matching_recipe_count']):,}",
            "description": "불일치 Recipe 수",
        },
        {
            "title": "집계 제외",
            "value": f"{int(latest['exclude_recipe_count']):,}",
            "description": "집계 제외된 Recipe 수",
        },
    ]
