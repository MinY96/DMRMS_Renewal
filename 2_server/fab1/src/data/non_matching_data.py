"""FAB1 Matching Rate 상세 조회 로직."""

from __future__ import annotations

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
    return sorted(str(value) for value in frame[column].dropna().unique())


def default_column_metadata(frame: pd.DataFrame) -> dict[str, dict]:
    """Non Matching 상세 테이블 컬럼 별 메타정보 초기 세팅

    Args:
        frame (pd.DataFrame): 불일치 테이블

    Returns:
        dict[str, dict]: 컬럼 별 메타정보
    """
    return {
        "master_key": {
            "tree_role": "parent",
            "header": "Master Key",
            "filter": {"type": "combobox", "options": _options(frame, "master_key")},
            "header_color": "#DCE6F1",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "model": {
            "tree_role": "parent",
            "header": "Model",
            "filter": {"type": "combobox", "options": _options(frame, "model")},
            "header_color": "#DCE6F1",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "equip": {
            "tree_role": "parent",
            "header": "Equip",
            "filter": {"type": "combobox", "options": _options(frame, "equip")},
            "header_color": "#DCE6F1",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "device": {
            "tree_role": "parent",
            "header": "Device",
            "filter": {"type": "like", "options": []},
            "header_color": "#DCE6F1",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "recipe": {
            "tree_role": "parent",
            "header": "Recipe",
            "filter": {"type": "combobox", "options": _options(frame, "recipe")},
            "header_color": "#DCE6F1",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "dt_edit_time": {
            "tree_role": "parent",
            "header": "수정시간",
            "filter": {"type": "like", "options": []},
            "header_color": "#DCE6F1",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "tech_prod": {
            "tree_role": "parent",
            "header": "Tech Prod",
            "filter": {"type": "like", "options": []},
            "header_color": "#E2F0D9",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "tech": {
            "tree_role": "parent",
            "header": "Tech",
            "filter": {"type": "like", "options": []},
            "header_color": "#E2F0D9",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "prod_group": {
            "tree_role": "parent",
            "header": "Prod Group",
            "filter": {"type": "like", "options": []},
            "header_color": "#E2F0D9",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "level": {
            "tree_role": "parent",
            "header": "Level",
            "filter": {"type": "like", "options": []},
            "header_color": "#E2F0D9",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "layer": {
            "tree_role": "parent",
            "header": "Layer",
            "filter": {"type": "like", "options": []},
            "header_color": "#E2F0D9",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "mode": {
            "tree_role": "parent",
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
        "para_nm": {
            "tree_role": "detail",
            "header": "Parameter",
            "filter": {"type": "like", "options": []},
            "header_color": "#FFF2CC",
            "format": {"type": "lowercase"},
            "rules": [
                {
                    "condition_column": "matching",
                    "operator": "eq",
                    "value": False,
                    "priority": 20,
                    "style": {
                        "background": "#FFF2CC",
                        "foreground": "#C00000",
                        "font_weight": "Bold",
                    },
                }
            ],
        },
        "para_value": {
            "tree_role": "detail",
            "header": "Value",
            "filter": {"type": "like", "options": []},
            "header_color": "#FFF2CC",
            "format": {"type": "uppercase"},
            "rules": [
                {
                    "condition_column": "matching",
                    "operator": "eq",
                    "value": False,
                    "priority": 20,
                    "style": {
                        "background": "#FFF2CC",
                        "foreground": "#C00000",
                        "font_weight": "Bold",
                    },
                }
            ],
        },
        "matching": {
            "tree_role": "detail",
            "header": "Matching",
            "filter": {"type": "combobox", "options": _options(frame, "matching")},
            "header_color": "#FFF2CC",
            "format": {"type": "uppercase"},
            "rules": [
                {
                    "operator": "eq",
                    "value": "FALSE",
                    "priority": 10,
                    "style": {
                        "foreground": "#9C6500",
                        "background": "#FFEB9C",
                        "font_weight": "Bold",
                    },
                },
            ],
        },
        "master_value": {
            "tree_role": "detail",
            "header": "Master Value",
            "filter": {"type": "like", "options": []},
            "header_color": "#FFF2CC",
            "format": {"type": "uppercase"},
            "rules": [
                {
                    "condition_column": "matching",
                    "operator": "eq",
                    "value": False,
                    "priority": 20,
                    "style": {
                        "background": "#FFF2CC",
                        "foreground": "#C00000",
                        "font_weight": "Bold",
                    },
                }
            ],
        },
        "_row_type": {
            "header": "",
            "filter": {"type": "like", "options": []},
            "header_color": "#FFFFFF",
            "format": {"type": "text"},
            "rules": [],
        },
    }


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


def non_matching_detail() -> pd.DataFrame:
    """최신 장비별 Recipe Matching 현황을 조회한다.

    Returns:
        pd.DataFrame: 처리 결과.
    """
    query = """
    SELECT 
        dri.master_key,
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
        drp.para_value,
        drp.matching,
        dmrp.para_value master_value
    FROM dm_recipe_info dri 
    LEFT JOIN dm_recipe_para drp 
        ON (dri.recipe_key = drp.recipe_key)
    LEFT JOIN dm_master_recipe_para dmrp 
        ON (dri.master_key = dmrp.master_key
            AND drp.para_nm = dmrp.para_nm)
    WHERE dri.is_remove = false
        AND dri.exclude_matching = false
        AND dri.master_key IS NOT NULL
        AND dri.matching = false
        AND drp.matching IS NOT NULL
    ORDER BY dri.model, dri.equip, dri.device, dri.recipe, drp.para_nm 
    """
    frame = PostgresCRUD.select_query(query)
    if frame.empty:
        return pd.DataFrame(
            columns=[
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
                "para_nm",
                "para_value",
                "matching",
                "master_value",
            ]
        )
    return frame
