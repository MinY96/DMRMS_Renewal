"""FAB1 집계 제외 Recipe 상세 조회 로직."""

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


def _matching_exclude_code(code: str):
    """집계 제외 코드 매칭"""
    match code:
        case "EXC_TEST":
            return "Test 목적으로 생성된 Recipe의 경우"
        case "EXC_DEVICE":
            return "Device 가 4자리가 아닌 경우"
        case "EXC_LAYER":
            return "Layer 가 8자리가 아닌 경우"
        case "EXC_MASTER":
            return "매칭되는 Master Recipe가 없는 경우"
        case "EXC_PARSE":
            return "Recipe 파싱 시 에러가 발생한 경우"
        case "EXC_RECIPE":
            return "정규 Recipe명이 아닌 경우"
        case "EXC_IGBT":
            return "IGBT 양산 전"
        case "EXC_MISS":
            return "기준 정보를 찾을 수 없는 경우"
    return ""


def default_column_metadata(frame: pd.DataFrame) -> dict[str, dict]:
    """집계 제외 Recipe 상세 테이블 컬럼 별 메타정보 초기 세팅

    Args:
        frame (pd.DataFrame): 집계 제외 Recipe 테이블

    Returns:
        dict[str, dict]: 컬럼 별 메타정보
    """
    return {
        "exclude_code": {
            "header": "집계 제외 코드",
            "filter": {"type": "combobox", "options": _options(frame, "exclude_code")},
            "header_color": "#FFF2CC",
            "format": {"type": "uppercase"},
            "rules": [
                {
                    "operator": "eq",
                    "value": "EXC_TEST",
                    "priority": 10,
                    "style": {
                        "foreground": "#006100",
                        "background": "#C6EFCE",
                        "font_weight": "Bold",
                    },
                },
                {
                    "operator": "eq",
                    "value": "EXC_DEVICE",
                    "priority": 10,
                    "style": {
                        "foreground": "#9C6500",
                        "background": "#FFEB9C",
                        "font_weight": "Bold",
                    },
                },
                {
                    "operator": "eq",
                    "value": "EXC_LAYER",
                    "priority": 10,
                    "style": {
                        "foreground": "#9C0006",
                        "background": "#FFC7CE",
                        "font_weight": "Bold",
                    },
                },
                {
                    "operator": "eq",
                    "value": "EXC_PARSE",
                    "priority": 10,
                    "style": {
                        "foreground": "#1F4E78",
                        "background": "#D9EAF7",
                        "font_weight": "Bold",
                    },
                },
                {
                    "operator": "eq",
                    "value": "EXC_RECIPE",
                    "priority": 10,
                    "style": {
                        "foreground": "#7030A0",
                        "background": "#E4DFEC",
                        "font_weight": "Bold",
                    },
                },
                {
                    "operator": "eq",
                    "value": "EXC_IGBT",
                    "priority": 10,
                    "style": {
                        "foreground": "#008080",
                        "background": "#DDEBF7",
                        "font_weight": "Bold",
                    },
                },
                {
                    "operator": "eq",
                    "value": "EXC_MISS",
                    "priority": 10,
                    "style": {
                        "foreground": "#C0007A",
                        "background": "#FCE4EC",
                        "font_weight": "Bold",
                    },
                },
            ],
        },
        "description": {
            "header": "제외 상세",
            "filter": {"type": "like", "options": []},
            "header_color": "#FFF2CC",
            "format": {"type": "uppercase"},
            "rules": [],
        },
        "model": {
            "header": "Model",
            "filter": {"type": "combobox", "options": _options(frame, "model")},
            "header_color": "#DCE6F1",
            "format": {"type": "uppercase"},
            "rules": [],
        },
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


def exclude_detail() -> pd.DataFrame:
    """최신 집계 제외 Recipe 현황을 조회한다.

    Returns:
        pd.DataFrame: 처리 결과.
    """
    columns = [
        "exclude_code",
        "description",
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
    ]
    query = """
    SELECT 
        dri.exclude_code,
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
        dri.mode 
    FROM dm_recipe_info dri 
    WHERE dri.is_remove = false 
        AND dri.exclude_matching = true
    ORDER BY dri.model, dri.equip, dri.device, dri.recipe
    """
    frame = PostgresCRUD.select_query(query)
    if frame.empty:
        return pd.DataFrame(columns=columns)
    frame["description"] = frame.apply(
        lambda x: _matching_exclude_code(x["exclude_code"]), axis=1
    )
    return frame[columns]
