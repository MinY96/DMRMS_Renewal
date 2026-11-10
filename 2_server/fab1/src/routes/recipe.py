# pylint: disable=W0718
"""Recipe 조회·백업·삭제·보고서 API Blueprint."""

from __future__ import annotations

import json
import logging
from pathlib import Path
import shutil
import zipfile
from io import BytesIO

from flask import Blueprint, request, send_file
from openpyxl import Workbook

from src.core.response import dataframe_payload, fail, ok
from src.config.config import CURRENT_FAB
from src.data.recipe_data import equipment_summary, recipe_detail, recipe_list
from src.services.recipe_operations import create_backup_archive, delete_after_backup


recipe_bp = Blueprint("recipe", __name__)
logger = logging.getLogger(__name__)


@recipe_bp.get("/equipment-summary")
def get_equipment_summary():
    """장비별 Recipe Matching 요약을 API로 반환한다."""
    try:
        rows = equipment_summary()
        return ok(
            data=dataframe_payload(rows),
            data_type="dataframe",
            message="장비별 Recipe 현황 조회 성공",
            meta={"fab": CURRENT_FAB},
        )
    except Exception as ex:
        return fail(
            str(ex), 500, [{"code": "RECIPE_SUMMARY_QUERY_ERROR", "message": str(ex)}]
        )


@recipe_bp.post("/list")
def get_recipe_list():
    """선택 장비의 Recipe 목록을 API로 반환한다."""
    try:
        body = request.get_json(silent=True) or {}
        equipment_ids = body.get("equipment_ids") or []
        if not equipment_ids:
            return fail(
                "선택된 장비가 없습니다.",
                400,
                [
                    {
                        "code": "NO_EQUIPMENT_SELECTED",
                        "message": "equipment_ids가 비어 있음",
                    }
                ],
            )
        rows = recipe_list(equipment_ids, body.get("filters") or {})
        return ok(
            data=dataframe_payload(rows),
            data_type="dataframe",
            message="Recipe 리스트 조회 성공",
            meta={"fab": CURRENT_FAB},
        )
    except Exception as ex:
        return fail(
            str(ex), 500, [{"code": "RECIPE_LIST_QUERY_ERROR", "message": str(ex)}]
        )


@recipe_bp.post("/detail")
def get_recipe_detail():
    """선택 Recipe 상세 Parameter를 API로 반환한다."""
    try:
        body = request.get_json(silent=True) or {}
        eqp_id = body.get("eqp_id")
        recipe_name = body.get("recipe_name")
        if not eqp_id or not recipe_name:
            return fail("eqp_id와 recipe_name이 필요합니다.", 400)
        rows = recipe_detail(eqp_id, recipe_name)
        return ok(
            data=dataframe_payload(rows),
            data_type="dataframe",
            message="Recipe 상세 조회 성공",
            meta={"fab": CURRENT_FAB},
        )
    except Exception as ex:
        return fail(
            str(ex), 500, [{"code": "RECIPE_DETAIL_QUERY_ERROR", "message": str(ex)}]
        )


def _write_result_manifest(archive_path: Path, payload: dict) -> None:
    """ZIP 파일 내부에 UTF-8 ``result.json`` 처리 결과를 기록한다.

    HTTP 응답 헤더는 WSGI 환경에서 latin-1 제약과 크기 제한이 있으므로,
    한글 및 상세 결과는 ZIP 내부 JSON 파일로 전달한다.

    Args:
        archive_path: 결과 JSON을 추가할 ZIP 파일 경로.
        payload: Frontend에서 읽을 작업 결과 사전.

    Raises:
        FileNotFoundError: ZIP 파일이 존재하지 않을 때.
    """
    archive_path = archive_path.resolve()
    if not archive_path.is_file():
        raise FileNotFoundError(f"결과를 기록할 ZIP 파일이 없습니다: {archive_path}")

    manifest = json.dumps(
        payload,
        ensure_ascii=False,
        indent=2,
        default=str,
    ).encode("utf-8")

    # append 모드로 Recipe 파일을 다시 압축하지 않고 result.json만 추가한다.
    with zipfile.ZipFile(
        archive_path, mode="a", compression=zipfile.ZIP_DEFLATED
    ) as archive:
        archive.writestr("result.json", manifest)


def _cleanup_work_directory(work_root: Path) -> None:
    """파일 응답 종료 후 서버 임시 작업 폴더를 제거한다.

    정리 실패는 이미 완료된 다운로드 응답을 실패로 변경하지 않고 로그에만
    기록한다.

    Args:
        work_root: 삭제할 임시 작업 디렉터리.
    """
    try:
        shutil.rmtree(work_root)
        logger.info("Recipe 임시 작업 폴더 삭제 완료: %s", work_root)
    except FileNotFoundError:
        logger.debug("Recipe 임시 작업 폴더가 이미 삭제됨: %s", work_root)
    except OSError:
        logger.exception("Recipe 임시 작업 폴더 삭제 실패: %s", work_root)


def _send_archive(archive_path: Path):
    """ZIP 파일 바이트를 HTTP 응답 본문으로 전송한다.

    서버 파일 경로를 Frontend에 전달하지 않는다. ``send_file``이 ZIP 바이트를
    스트리밍하며, 응답이 완전히 닫힌 뒤 임시 폴더를 제거한다.

    Args:
        archive_path: Frontend로 전송할 ZIP 파일의 서버 로컬 경로.

    Returns:
        ZIP 파일 다운로드용 Flask Response.

    Raises:
        FileNotFoundError: 전송 직전 ZIP 파일이 존재하지 않을 때.
    """
    archive_path = archive_path.resolve()
    work_root = archive_path.parent
    if not archive_path.is_file():
        raise FileNotFoundError(f"전송할 ZIP 파일이 없습니다: {archive_path}")

    response = send_file(
        archive_path,
        as_attachment=True,
        download_name=archive_path.name,
        mimetype="application/zip",
        conditional=False,
        max_age=0,
    )
    response.call_on_close(lambda: _cleanup_work_directory(work_root))
    return response


@recipe_bp.post("/backup")
def backup_recipes():
    """선택 Recipe를 백업하여 ZIP 파일로 반환한다."""
    try:
        body = request.get_json(silent=True) or {}
        archive_path, results = create_backup_archive(body.get("items") or [])
        payload = {
            "requested_count": len(results),
            "success_count": sum(result.success for result in results),
            "failure_count": sum(not result.success for result in results),
            "results": [result.__dict__ for result in results],
        }
        if payload["success_count"] == 0:
            shutil.rmtree(archive_path.parent, ignore_errors=True)
            return fail(
                "백업에 성공한 Recipe가 없습니다.",
                500,
                [
                    {
                        "code": "RECIPE_BACKUP_FAILED",
                        "message": result.message,
                        "recipe_key": result.recipe_key,
                    }
                    for result in results
                ],
            )
        payload["action"] = "backup"
        payload["success"] = payload["failure_count"] == 0
        _write_result_manifest(archive_path, payload)
        return _send_archive(archive_path)
    except ValueError as ex:
        return fail(
            str(ex),
            400,
            [{"code": "RECIPE_BACKUP_VALIDATION_ERROR", "message": str(ex)}],
        )
    except Exception as ex:
        return fail(str(ex), 500, [{"code": "RECIPE_BACKUP_ERROR", "message": str(ex)}])


@recipe_bp.post("/delete")
def delete_recipes():
    """선행 백업 후 장비 Recipe를 삭제한다."""
    try:
        body = request.get_json(silent=True) or {}
        archive_path, backup_results, delete_results = delete_after_backup(
            body.get("items") or []
        )
        payload = {
            "requested_count": len(delete_results),
            "backup_success_count": sum(result.success for result in backup_results),
            "deleted_count": sum(result.success for result in delete_results),
            "failed_count": sum(not result.success for result in delete_results),
            "backup_results": [result.__dict__ for result in backup_results],
            "delete_results": [result.__dict__ for result in delete_results],
            "deleted_items": [
                {
                    "eqp_id": result.recipe_key.split("-", 1)[0],
                    "recipe_name": result.recipe_key.split("-", 1)[1],
                }
                for result in delete_results
                if result.success
            ],
        }
        if payload["backup_success_count"] == 0:
            shutil.rmtree(archive_path.parent, ignore_errors=True)
            return fail("선행 백업에 성공한 Recipe가 없어 삭제하지 않았습니다.", 500)
        payload["action"] = "delete"
        payload["success"] = payload["failed_count"] == 0
        _write_result_manifest(archive_path, payload)
        return _send_archive(archive_path)
    except ValueError as ex:
        return fail(
            str(ex),
            400,
            [{"code": "RECIPE_DELETE_VALIDATION_ERROR", "message": str(ex)}],
        )
    except Exception as ex:
        return fail(str(ex), 500, [{"code": "RECIPE_DELETE_ERROR", "message": str(ex)}])


@recipe_bp.get("/report")
def download_report():
    """장비별 Recipe Summary Excel 파일을 생성한다."""
    wb = Workbook()
    ws = wb.active
    ws.title = "RecipeSummary"
    rows = equipment_summary()
    if rows:
        ws.append(list(rows[0].keys()))
        for row in rows:
            ws.append(list(row.values()))
    stream = BytesIO()
    wb.save(stream)
    stream.seek(0)
    return send_file(
        stream,
        as_attachment=True,
        download_name=f"{CURRENT_FAB}_recipe_summary.xlsx",
        mimetype="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    )
