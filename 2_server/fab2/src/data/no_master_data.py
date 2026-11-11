"""FAB2 No Master Recipe 상세 조회 로직."""

from __future__ import annotations
from typing import Any, List, Tuple

import pandas as pd

from src.databases import PostgresCRUD


def _options(frame: pd.DataFrame, column: str) -> list[str]:
    """컬럼의 고윳값 리스트 반환 함수

    Args:
        frame (pd.DataFrame): 테이블
        column (str): 컬럼

    Returns:
        list[str]: 고윳값 리스트
    """
    if column not in frame.columns:
        return []
    return sorted(str(value) for value in frame[column].dropna().unique())


def default_column_metadata(
    frame: pd.DataFrame,
    id_columns: List[str],
    master_columns: List[str],
    params: List[str],
) -> dict[str, dict]:
    """No Master 상세 테이블 컬럼 별 메타정보 초기 세팅

    Args:
        frame (pd.DataFrame): No Master 테이블

    Returns:
        dict[str, dict]: 컬럼 별 메타정보
    """
    default_metadata = {
        "equip": {
            "header": "Equip",
            "filter": {"type": "combobox", "options": _options(frame, "equip")},
            "header_color": "#DCE6F1",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "device": {
            "header": "Device",
            "filter": {"type": "like", "options": []},
            "header_color": "#DCE6F1",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "recipe": {
            "header": "Recipe",
            "filter": {"type": "like", "options": []},
            "header_color": "#DCE6F1",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "dt_edit_time": {
            "header": "수정시간",
            "filter": {"type": "like", "options": []},
            "header_color": "#DCE6F1",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "master_group": {
            "header": "기준정보 그룹",
            "filter": {"type": "combobox", "options": _options(frame, "master_group")},
            "header_color": "#9FC785",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "tech_prod": {
            "header": "Tech Prod",
            "filter": {"type": "like", "options": []},
            "header_color": "#E2F0D9",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "tech": {
            "header": "Tech",
            "filter": {"type": "like", "options": []},
            "header_color": "#E2F0D9",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "prod_group": {
            "header": "Prod Group",
            "filter": {"type": "like", "options": []},
            "header_color": "#E2F0D9",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "level": {
            "header": "Level",
            "filter": {"type": "like", "options": []},
            "header_color": "#E2F0D9",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "layer": {
            "header": "Layer",
            "filter": {"type": "like", "options": []},
            "header_color": "#E2F0D9",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "mode": {
            "header": "Mode",
            "filter": {"type": "combobox", "options": _options(frame, "mode")},
            "header_color": "#E2F0D9",
            "format": {"type": "uppercase"},
            "rules": [
                {
                    "operator": "eq",
                    "value": "AUTO",
                    "priority": 10,
                    "style": {"background": "#DDEBF7", "foreground": "#1F4E78"},
                },
                {
                    "operator": "eq",
                    "value": "SAT",
                    "priority": 10,
                    "style": {"background": "#E4DFEC", "foreground": "#5F497A"},
                },
            ],
        },
    }
    default_metadata = {
        col: meta
        for col, meta in default_metadata.items()
        if col in id_columns + ["master_group"] + master_columns
    }
    for param in params:
        default_metadata.update(
            {
                param: {
                    "header": param,
                    "filter": {"type": "like", "options": []},
                    "header_color": "#FFF2CC",
                    "format": {"type": "uppercase"},
                    "rules": [],
                }
            }
        )
    return default_metadata


def apply_default_rules(metadata: dict[str, dict]) -> dict[str, dict]:
    """Rule이 없는 컬럼에도 기본 Template 표시가 유지되도록 always rule을 추가한다."""
    for info in metadata.values():
        rules = info.setdefault("rules", [])
        if not rules:
            rules.append(
                {
                    "operator": "always",
                    "priority": 0,
                    "style": {},
                }
            )
    return metadata


def no_master_detail(model: str) -> Tuple[pd.DataFrame, Tuple[Any, Any, Any]]:
    """최신 모델 단위 No Master Recipe를 상세 조회한다.
    Args:
        model: Model명 (list[str]).

    Returns:
        pd.DataFrame: 처리 결과.
    """
    query = """
    SELECT 
        dri.recipe_key,
        dri.model,
        dri.equip,
        dri.device,
        dri.recipe,
        dri.dt_edit_time,
        dri.tech_prod,
        dri.tech,
        dri.prod_group,
        dri.level,
        dri.layer,
        dri.mode,
        drp.para_nm,
        drp.para_value
    FROM dm_recipe_info dri 
    LEFT JOIN dm_recipe_para drp 
    ON (dri.recipe_key = drp.recipe_key)
    WHERE dri.model = %s
        AND dri.is_remove = false
        AND dri.exclude_matching = true
        AND dri.exclude_code = 'EXC_MASTER'
        AND drp.para_nm != 'mode'
    ORDER BY dri.tech_prod, dri.tech, dri.prod_group, dri.level,
        dri.layer, dri.mode, dri.equip, dri.device, dri.recipe
    """
    frame = PostgresCRUD.select_query(query, (model,))
    params = PostgresCRUD.select_query_by_list(
        """
        SELECT DISTINCT drp.para_nm
        FROM dm_recipe_info dri
        LEFT JOIN dm_recipe_para drp
        ON dri.recipe_key = drp.recipe_key
        WHERE dri.model=%s
            AND drp.para_nm IS NOT NULL
            AND drp.para_nm != 'mode'
        ORDER BY drp.para_nm
        """,
        (model,),
    )
    id_columns = ["equip", "device", "recipe", "dt_edit_time"]
    master_columns = ["tech_prod", "tech", "prod_group", "level", "layer"]
    if model != "PUMA":
        master_columns.append("mode")
    if frame.empty:
        return pd.DataFrame(
            columns=id_columns + ["master_group"] + master_columns + params
        ), (id_columns, master_columns, params)
    pivot = frame.pivot_table(
        index=["recipe_key"],
        columns=["para_nm"],
        values=["para_value"],
        fill_value="",
        aggfunc="first",
    )
    frame = frame.drop_duplicates(subset="recipe_key").merge(
        pivot["para_value"].reset_index(), on="recipe_key", how="left"
    )
    # null 기준정보가 있어도 그룹 번호가 NaN/float가 되지 않도록 처리합니다.
    group_numbers = frame.groupby(master_columns, dropna=False).ngroup()
    frame["master_group"] = group_numbers.map(lambda n: f"Group_{int(n) + 1:02d}")
    return frame, (id_columns, master_columns, params)
