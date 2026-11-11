"""FAB1 Master Recipe 조회·생성·수정·삭제 데이터 접근 로직."""

from __future__ import annotations

import re
from typing import Any

from src.databases import PostgresCRUD
from src.databases.postgre_db import transaction

MASTER_KEY_PATTERN = re.compile(r"^(.*?)(\d+)$")
REQUIRED_CRITERIA_KLA = (
    "model",
    "tech_prod",
    "tech",
    "prod_group",
    "level",
    "layer",
    "mode",
)
REQUIRED_CRITERIA_PUMA = (
    "model",
    "tech_prod",
    "tech",
    "prod_group",
    "level",
    "layer",
)


def master_filter_options(model: str) -> dict:
    """파싱 Recipe 및 기존 Master에서 선택 Model의 기준정보 선택지를 만든다."""
    options: dict[str, object] = {
        "tech_prod": [],
        "tech": [],
        "prod_group": [],
        "level_like": "",
        "layer_like": "",
    }
    if model != "PUMA":
        options["mode"] = []
    if not model:
        return options

    # Master가 없는 신규 Model도 화면에 선택지를 표시할 수 있어야 합니다.
    frame = PostgresCRUD.select_query(
        """
        SELECT tech_prod, tech, prod_group, level, layer, mode
        FROM dm_recipe_info
        WHERE model=%s AND is_remove=false
        UNION
        SELECT tech_prod, tech, prod_group, level, layer, mode
        FROM dm_master_recipe_info
        WHERE model=%s
        """,
        (model, model),
    )
    for column in ("tech_prod", "tech", "prod_group", "mode"):
        if column not in options:
            continue
        if frame.empty:
            continue
        series = frame[column]
        unique = set(series.dropna().astype(str).str.strip()) - {""}
        if series.isna().any():
            unique.add("NULL")
        options[column] = sorted(unique, key=str.casefold)
    return options


def master_list(filters: dict | None = None) -> list[dict]:
    """입력 필터에 맞는 Master Recipe 기준정보 목록을 조회한다.

    Args:
        filters: 입력값 (dict | None).

    Returns:
        list[dict]: 처리 결과.
    """
    filters = filters or {}
    clauses: list[str] = []
    params: list[Any] = []
    allowed = {"model", "tech_prod", "tech", "prod_group", "level", "layer", "mode"}
    for key, condition in filters.items():
        normalized_key = key.replace("_like", "")
        if normalized_key not in allowed or condition in (None, ""):
            continue
        if isinstance(condition, dict):
            mode = condition.get("mode", "equals")
            value = condition.get("value", "")
        else:
            mode = "like" if normalized_key in {"level", "layer"} else "equals"
            value = condition
        if str(value).upper() == "NULL":
            clauses.append(f"{normalized_key} IS NULL")
        elif mode == "like" or normalized_key in {"level", "layer"}:
            clauses.append(f"{normalized_key} ILIKE %s")
            params.append(f"%{value}%")
        else:
            clauses.append(f"{normalized_key} = %s")
            params.append(value)

    query = "SELECT * FROM dm_master_recipe_info"
    if clauses:
        query += " WHERE " + " AND ".join(clauses)
    query += " ORDER BY model, tech_prod, tech, prod_group, level, layer, mode"
    return PostgresCRUD.select_query(query, tuple(params)).to_dict(orient="records")


def master_detail(master_key: str) -> dict:
    """지정한 Master Key의 Parameter 상세정보를 조회한다.

    Args:
        master_key: 입력값 (str).

    Returns:
        dict: 처리 결과.
    """
    rows = PostgresCRUD.select_query(
        """
        SELECT dmrp.para_nm AS parameter_name,
            TRIM(dmrp.para_value) AS display_value,
            COALESCE(dpt.para_type, 'STRING') AS control_type
        FROM f1.dm_master_recipe_para dmrp
        LEFT JOIN f1.dm_para_type dpt
            ON dmrp.para_nm=dpt.para_nm
        WHERE dmrp.master_key=%s
            AND dmrp.para_nm<>'mode'
        ORDER BY dmrp.para_nm
        """,
        (master_key,),
    ).to_dict(orient="records")
    for row in rows:
        row["allowed_values"] = []
        if str(row["control_type"]).upper() == "STRING":
            row["allowed_values"] = PostgresCRUD.select_query_by_list(
                """
                SELECT DISTINCT para_value 
                FROM dm_master_recipe_para 
                WHERE para_nm=%s 
                ORDER BY para_value
                """,
                (row["parameter_name"],),
            )
    return {"master_key": master_key, "parameters": [_clone_parameter(p) for p in rows]}


def parameter_options(model: str) -> list[dict]:
    """파싱 Recipe를 기준으로 Parameter 명칭, 타입, 문자열 고유값을 일괄 조회한다.

    Master가 이미 존재하는 Model은 Master의 Parameter도 함께 포함합니다.
    문자열 고유값은 N+1 SQL이 아닌 단일 배치 조회로 읽습니다.
    """
    if not model:
        return []
    frame = PostgresCRUD.select_query(
        """
        WITH names AS (
            SELECT p.para_nm
            FROM dm_recipe_para p
            JOIN dm_recipe_info i ON i.recipe_key=p.recipe_key
            WHERE i.model=%s AND i.is_remove=false AND p.para_nm<>'mode'
            UNION
            SELECT p.para_nm
            FROM dm_master_recipe_para p
            JOIN dm_master_recipe_info i ON i.master_key=p.master_key
            WHERE i.model=%s AND p.para_nm<>'mode'
        )
        SELECT n.para_nm AS parameter_name,
               COALESCE(MAX(t.para_type), 'STRING') AS control_type
        FROM names n
        LEFT JOIN dm_para_type t ON t.para_nm=n.para_nm
        GROUP BY n.para_nm
        ORDER BY n.para_nm
        """,
        (model, model),
    )
    if frame.empty:
        return []
    result = frame.to_dict(orient="records")
    for item in result:
        item["control_type"] = str(item.get("control_type") or "STRING").upper()
        item["allowed_values"] = []
    string_names = [item["parameter_name"] for item in result
                    if item["control_type"] == "STRING"]
    if not string_names:
        return result

    # 하나의 모델에 대한 모든 문자열 파라미터 고유값을 한 번에 조회합니다.
    values = PostgresCRUD.select_query(
        """
        SELECT DISTINCT para_nm AS parameter_name, BTRIM(para_value) AS para_value
        FROM (
            SELECT p.para_nm, p.para_value
            FROM dm_recipe_para p
            JOIN dm_recipe_info i ON i.recipe_key=p.recipe_key
            WHERE i.model=%s AND i.is_remove=false AND p.para_nm = ANY(%s)
            UNION
            SELECT p.para_nm, p.para_value
            FROM dm_master_recipe_para p
            JOIN dm_master_recipe_info i ON i.master_key=p.master_key
            WHERE i.model=%s AND p.para_nm = ANY(%s)
        ) value_source
        WHERE para_value IS NOT NULL AND BTRIM(para_value) <> ''
        ORDER BY parameter_name, para_value
        """,
        (model, string_names, model, string_names),
    )
    by_name: dict[str, list[str]] = {name: [] for name in string_names}
    for row in values.to_dict(orient="records"):
        name = row["parameter_name"]
        if name in by_name:
            by_name[name].append(row["para_value"])
    for item in result:
        item["allowed_values"] = by_name.get(item["parameter_name"], [])
    return result


def default_new_master_template(model: str) -> dict:
    """신규 Master 생성 화면의 기본 Parameter 템플릿을 조회한다.
    Args:
        model: 선택한 Model명 (str).

    Returns:
        dict: 처리 결과.
    """
    rows = PostgresCRUD.select_query(
        """
        SELECT p.para_nm AS parameter_name, p.para_value AS display_value,
        COALESCE(t.para_type, 'STRING') AS control_type
        FROM dm_master_recipe_para p
        LEFT JOIN dm_para_type t ON p.para_nm=t.para_nm
        WHERE p.master_key = (
            SELECT i.master_key
            FROM dm_master_recipe_info i
            WHERE i.model = %s
            ORDER BY i.master_key DESC
            LIMIT 1
        )
            AND p.para_nm<>'mode'
        ORDER BY p.para_nm
        """,
        (model,),
    ).to_dict(orient="records")
    return {"parameters": rows}


def update_master_detail(master_key: str, parameters: list[dict]) -> dict:
    """지정한 Master Recipe Parameter를 transaction으로 수정한다.

    Args:
        master_key: 입력값 (str).
        parameters: 입력값 (list[dict]).

    Returns:
        dict: 처리 결과.
    """
    normalized = _normalize_parameters(parameters)
    if not normalized:
        return {"master_key": master_key, "updated_count": 0}

    with transaction() as (_, cursor):
        cursor.execute(
            "SELECT 1 FROM dm_master_recipe_info WHERE master_key=%s FOR UPDATE",
            (master_key,),
        )
        if cursor.fetchone() is None:
            raise ValueError(f"Master Key가 존재하지 않습니다: {master_key}")
        updated_count = 0
        for parameter_name, display_value in normalized:
            cursor.execute(
                """
                UPDATE dm_master_recipe_para
                SET para_value=%s
                WHERE master_key=%s AND para_nm=%s
                """,
                (display_value, master_key, parameter_name),
            )
            if cursor.rowcount == 0:
                raise ValueError(
                    f"Master Parameter가 존재하지 않습니다: {parameter_name}"
                )
            updated_count += cursor.rowcount
    return {"master_key": master_key, "updated_count": updated_count}


def delete_master(master_key: str) -> bool:
    """지정한 Master Recipe와 종속 Parameter를 삭제한다.

    Args:
        master_key: 입력값 (str).

    Returns:
        bool: 처리 결과.
    """
    with transaction() as (_, cursor):
        cursor.execute(
            "DELETE FROM dm_master_recipe_info WHERE master_key=%s", (master_key,)
        )
        return cursor.rowcount > 0


def create_master(model: str, criteria: dict, parameters: list[dict]) -> dict:
    """기준정보와 Parameter를 검증한 뒤 신규 Master Recipe를 생성한다.

    Args:
        model: 선택한 Model명 (str).
        criteria: 입력값 (dict).
        parameters: 입력값 (list[dict]).

    Returns:
        dict: 처리 결과.
    """
    if not model:
        raise ValueError("Model을 선택해야 Master Recipe를 생성할 수 있습니다.")
    if criteria.get("model") != model:
        raise ValueError("선택 Model과 요청 기준정보의 Model이 일치하지 않습니다.")
    if model != "PUMA":
        require_criteria = REQUIRED_CRITERIA_KLA
        dup_check_query = """
        SELECT master_key
        FROM dm_master_recipe_info
        WHERE model IS NOT DISTINCT FROM %s
            AND tech_prod IS NOT DISTINCT FROM %s
            AND tech IS NOT DISTINCT FROM %s
            AND prod_group IS NOT DISTINCT FROM %s
            AND level IS NOT DISTINCT FROM %s
            AND layer IS NOT DISTINCT FROM %s
            AND mode IS NOT DISTINCT FROM %s
        """
        insert_query = """
        INSERT INTO dm_master_recipe_info
            (master_key, model, tech_prod, tech, prod_group, level, layer, mode)
        VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
        """
    else:
        require_criteria = REQUIRED_CRITERIA_PUMA
        dup_check_query = """
        SELECT master_key
        FROM dm_master_recipe_info
        WHERE model IS NOT DISTINCT FROM %s
            AND tech_prod IS NOT DISTINCT FROM %s
            AND tech IS NOT DISTINCT FROM %s
            AND prod_group IS NOT DISTINCT FROM %s
            AND level IS NOT DISTINCT FROM %s
            AND layer IS NOT DISTINCT FROM %s
        """
        insert_query = """
        INSERT INTO dm_master_recipe_info
            (master_key, model, tech_prod, tech, prod_group, level, layer)
        VALUES (%s, %s, %s, %s, %s, %s, %s)
        """
    normalized_criteria = {
        key: _normalize_nullable(criteria.get(key)) for key in require_criteria
    }
    missing = [
        key
        for key in require_criteria
        if key != "mode" and not normalized_criteria[key]
    ]
    if missing:
        raise ValueError(f"필수 기준정보가 누락되었습니다: {', '.join(missing)}")
    normalized_parameters = _normalize_parameters(parameters)
    if not normalized_parameters:
        raise ValueError("Parameter를 하나 이상 입력해야 합니다.")
    names = [name for name, _ in normalized_parameters]
    if len(names) != len(set(names)):
        raise ValueError("중복 Parameter가 포함되어 있습니다.")

    with transaction() as (_, cursor):
        cursor.execute(
            dup_check_query,
            tuple(normalized_criteria[key] for key in require_criteria),
        )
        duplicate = cursor.fetchone()
        if duplicate:
            raise ValueError(f"이미 존재하는 기준정보 조합입니다: {duplicate[0]}")

        master_key = _next_master_key(cursor, normalized_criteria["model"])
        cursor.execute(
            insert_query,
            (master_key, *(normalized_criteria[key] for key in require_criteria)),
        )
        cursor.executemany(
            """
            INSERT INTO dm_master_recipe_para (master_key, para_nm, para_value)
            VALUES (%s, %s, %s)
            """,
            [(master_key, name, value) for name, value in normalized_parameters],
        )
    return {
        "master_key": master_key,
        "created": True,
        "parameter_count": len(normalized_parameters),
    }


def _next_master_key(cursor, model: str) -> str:
    """모델별 다음 Master Key를 생성한다.

    Args:
        cursor: 입력값 (Any).
        model: 입력값 (str).

    Returns:
        str: 처리 결과.
    """
    cursor.execute(
        "SELECT master_key FROM dm_master_recipe_info ORDER BY master_key DESC FOR UPDATE"
    )
    keys = [row[0] for row in cursor.fetchall()]
    prefix = f"{model}_"
    max_number = 0
    for key in keys:
        if key.startswith(prefix) and key[len(prefix) :].isdigit():
            max_number = max(max_number, int(key[len(prefix) :]))
    return f"{prefix}{max_number + 1:05d}"


def _normalize_nullable(value: Any) -> str | None:
    """빈 문자열과 NULL 표현을 Python None으로 정규화한다.

    Args:
        value: 입력값 (Any).

    Returns:
        str | None: 처리 결과.
    """
    if value is None:
        return None
    text = str(value).strip()
    return None if not text or text.upper() == "NULL" else text


def _normalize_parameters(parameters: list[dict]) -> list[tuple[str, str]]:
    """Frontend Parameter 목록을 DB 저장용 튜플로 변환한다.

    Args:
        parameters: 입력값 (list[dict]).

    Returns:
        list[tuple[str, str]]: 처리 결과.
    """
    result: list[tuple[str, str]] = []
    for item in parameters:
        name = str(item.get("parameter_name") or "").strip()
        value = str(item.get("display_value") or "").strip()
        if not name:
            raise ValueError("parameter_name이 비어 있습니다.")
        result.append((name, value))
    return result


def _clone_parameter(parameter: dict) -> dict:
    """Parameter 정보를 안전하게 복제한다.

    Args:
        parameter: 입력값 (dict).

    Returns:
        dict: 처리 결과.
    """
    return {
        "parameter_name": parameter.get("parameter_name", ""),
        "control_type": str(parameter.get("control_type", "STRING")).upper(),
        "allowed_values": list(parameter.get("allowed_values") or []),
        "display_value": str(parameter.get("display_value", "")),
    }
