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
    """Master Recipe 기준정보 필터 선택지를 조회한다.
    Args:
        model: 선택한 Model (str).

    Returns:
        dict: 처리 결과.
    """
    table_name = "dm_master_recipe_info"
    options = {
        "tech_prod": PostgresCRUD.select_query_by_list(
            f"SELECT DISTINCT tech_prod FROM {table_name} WHERE model=%s ORDER BY tech_prod",
            (model,),
        ),
        "tech": PostgresCRUD.select_query_by_list(
            f"SELECT DISTINCT tech FROM {table_name} WHERE model=%s ORDER BY tech",
            (model,),
        ),
        "prod_group": PostgresCRUD.select_query_by_list(
            f"SELECT DISTINCT prod_group FROM {table_name} WHERE model=%s ORDER BY prod_group",
            (model,),
        ),
        "level_like": "",
        "layer_like": "",
    }
    if model == "PUMA":
        return options
    options.update(
        {
            "mode": PostgresCRUD.select_query_by_list(
                f"SELECT DISTINCT mode FROM {table_name} WHERE model=%s ORDER BY mode",
                (model,),
            )
        }
    )
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
        FROM dm_master_recipe_para dmrp
        LEFT JOIN dm_para_type dpt
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
    """신규 Master 생성에 사용할 Parameter 메타정보를 조회한다.
    Args:
        model: 선택한 Model명 (str).

    Returns:
        list[dict]: 처리 결과.
    """
    parameters = PostgresCRUD.select_query(
        """
        SELECT DISTINCT p.para_nm AS parameter_name,
            COALESCE(t.para_type, 'STRING') AS control_type
        FROM dm_master_recipe_para p
        LEFT JOIN dm_para_type t ON p.para_nm=t.para_nm
        WHERE p.para_nm<>'mode'
            AND p.master_key LIKE %s
        ORDER BY p.para_nm
        """,
        (f"{model}%",),
    ).to_dict(orient="records")
    for parameter in parameters:
        parameter["allowed_values"] = []
        if str(parameter["control_type"]).upper() == "STRING":
            parameter["allowed_values"] = PostgresCRUD.select_query_by_list(
                """
                SELECT DISTINCT para_value 
                FROM dm_master_recipe_para 
                WHERE para_nm=%s 
                    AND master_key LIKE %s
                ORDER BY para_value""",
                (
                    parameter["parameter_name"],
                    f"{model}%",
                ),
            )
    return parameters


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
        WHERE p.master_key like %s
            AND	p.master_key=(
                SELECT master_key 
                FROM dm_master_recipe_info 
                ORDER BY master_key DESC 
                LIMIT 1
            )
            AND p.para_nm<>'mode'
        ORDER BY p.para_nm
        """,
        (f"{model}%",),
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
