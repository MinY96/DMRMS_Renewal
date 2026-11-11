"""FAB1 Recipe 조회 및 로컬 필터 SQL 생성 로직."""

from __future__ import annotations

from src.databases import PostgresCRUD


def equipment_summary() -> list[dict]:
    """최신 장비별 Recipe Matching 현황을 조회한다.

    Returns:
        list[dict]: 처리 결과.
    """
    rows = []
    query = """
    SELECT 
        FALSE AS is_selected,
        model,
        equip,
        recipe AS recipe_count,
        matching AS matching_count,
        non_matching AS non_matching_count,
        exclude_matching,
        matching_rate,
        aggregation_time
    FROM dm_matching_rate 
    WHERE equip != 'TOTAL' 
        AND aggregation_time = (
            SELECT max(aggregation_time)
            FROM dm_matching_rate
        ) 
    ORDER BY model, equip
    """
    df = PostgresCRUD.select_query(query)
    if len(df) > 0:
        return df.to_dict(orient="records")
    return rows


def recipe_list(equipment_ids: list[str], filters: dict | None = None) -> list[dict]:
    """선택 장비와 필터 조건에 맞는 Recipe 목록을 조회한다.

    Args:
        equipment_ids: 입력값 (list[str]).
        filters: 입력값 (dict | None).

    Returns:
        list[dict]: 처리 결과.
    """
    filters = filters or {}
    rows = []
    query = """
    SELECT
        FALSE AS is_selected,
        model, 
        equip, 
        device, 
        layer, 
        recipe,
        tech_prod, 
        tech, 
        prod_group, 
        mode, 
        dt_edit_time AS last_modified_time, 
        matching,
        (exclude_code IS NOT NULL AND exclude_code = 'EXC_MASTER') AS no_master
    FROM dm_recipe_info
    WHERE is_remove=false 
        AND equip = ANY(%s)
    ORDER BY equip, recipe
    """
    df = PostgresCRUD.select_query(query, (equipment_ids,))
    if len(df) > 0:
        rows = df.to_dict(orient="records")
    return _apply_filters(rows, filters)


def recipe_detail(eqp_id: str, recipe_name: str) -> list[dict]:
    """장비와 Recipe명을 기준으로 Parameter 상세를 조회한다.

    Args:
        eqp_id: 입력값 (str).
        recipe_name: 입력값 (str).

    Returns:
        list[dict]: 처리 결과.
    """
    rows = []
    query = """
    SELECT 
        dri.equip, 
        dri.recipe, 
        drp.para_nm, 
        drp.para_value, 
        dmrp.para_value AS master_value, 
        drp.matching
    FROM dm_recipe_info dri
    INNER JOIN dm_recipe_para drp
        ON (dri.recipe_key=drp.recipe_key)
    LEFT JOIN dm_master_recipe_para dmrp
        ON (dri.master_key=dmrp.master_key 
            AND drp.para_nm=dmrp.para_nm)
    WHERE dri.recipe_key=%s
        AND drp.para_nm!='mode'
    ORDER BY drp.para_nm
    """
    df = PostgresCRUD.select_query(query, (f"{eqp_id}-{recipe_name}",))
    if len(df) > 0:
        return df.to_dict(orient="records")
    return rows


def _apply_filters(rows: list[dict], filters: dict) -> list[dict]:
    """허용된 필터만 SQL WHERE 절과 parameter로 변환한다.

    Args:
        rows: 입력값 (list[dict]).
        filters: 입력값 (dict).

    Returns:
        list[dict]: 처리 결과.
    """
    if not filters:
        return rows
    result = rows
    for key, condition in filters.items():
        if condition in (None, ""):
            continue
        if isinstance(condition, dict):
            mode = condition.get("mode", "equals")
            value = str(condition.get("value", "")).lower()
        else:
            mode = "equals"
            value = str(condition).lower()
        if not value:
            continue
        if mode == "like":
            result = [r for r in result if value in str(r.get(key, "")).lower()]
        else:
            result = [r for r in result if str(r.get(key, "")).lower() == value]
    return result
