# pylint: disable=W0718
"""Recipe 파일 백업 및 장비 FTP 삭제 서비스."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from ftplib import FTP, all_errors
from pathlib import Path, PurePosixPath
import shutil
import tempfile
import zipfile

from src.config.config import BACKUP_BASE_DIR, WORK_DIR, load_equipment_config
from src.domain.equipment import create_equipment
from src.databases import PostgresCRUD

EQUIPMENTS = load_equipment_config(
    Path(__file__).parent.parent / "config" / "equipments.yaml"
)


@dataclass(frozen=True)
class RecipeRef:
    """장비와 Recipe명을 함께 보관하는 불변 참조 모델."""

    eqp_id: str
    recipe_name: str

    @property
    def recipe_key(self) -> str:
        """DB에서 사용하는 장비-Recipe 복합 키를 반환한다.

        Returns:
            str: 처리 결과.
        """
        return f"{self.eqp_id}-{self.recipe_name}"


@dataclass
class OperationResult:
    """Recipe 백업 또는 삭제의 개별 처리 결과 모델."""

    recipe_key: str
    success: bool
    source: str
    message: str


def normalize_items(items: list[dict]) -> list[RecipeRef]:
    """Frontend 선택 Recipe 목록을 중복 없는 참조 목록으로 정규화한다.

    Args:
        items: 입력값 (list[dict]).

    Returns:
        list[RecipeRef]: 처리 결과.
    """
    result: list[RecipeRef] = []
    seen: set[str] = set()
    for item in items:
        eqp = str(item.get("eqp_id") or "").strip()
        recipe = str(item.get("recipe_name") or "").strip()
        if not eqp or not recipe:
            continue
        ref = RecipeRef(eqp, recipe)
        if ref.recipe_key not in seen:
            result.append(ref)
            seen.add(ref.recipe_key)
    if not result:
        raise ValueError("처리할 Recipe가 없습니다.")
    return result


def _load_recipe_info(refs: list[RecipeRef]) -> dict[str, dict]:
    """선택 Recipe의 파일 및 백업 경로 정보를 DB에서 일괄 조회한다.

    Args:
        refs: 입력값 (list[RecipeRef]).

    Returns:
        dict[str, dict]: 처리 결과.
    """
    keys = [ref.recipe_key for ref in refs]
    dataframe = PostgresCRUD.select_query(
        """
        SELECT 
            recipe_key, 
            equip,
            device,
            recipe,
            folder_path,
            file_path,
            backup_folder_path,
            backup_file_path
        FROM dm_recipe_info
        WHERE recipe_key = ANY(%s)
            AND is_remove = false
    """,
        (keys,),
    )
    # to_dict(records)는 iterrows보다 Python 객체 생성 횟수가 적어 대량 조회에 유리하다.
    records = dataframe.to_dict(orient="records")
    return {str(record["recipe_key"]): record for record in records}


def _safe_relative(path_value: str | None) -> Path | None:
    """DB 경로를 경로탈출이 불가능한 상대 Path로 변환한다.

    Args:
        path_value: 입력값 (str | None).

    Returns:
        Path | None: 처리 결과.
    """
    if not path_value:
        return None
    normalized = str(path_value).replace("\\", "/").lstrip("/")
    if ".." in PurePosixPath(normalized).parts:
        raise ValueError(f"허용되지 않은 상대 경로입니다: {path_value}")
    return Path(*PurePosixPath(normalized).parts)


def _copy_local_backup(info: dict, target_dir: Path) -> bool:
    """서버에 기존 백업이 있으면 작업 폴더로 복사한다.

    Args:
        info: 입력값 (dict).
        target_dir: 입력값 (Path).

    Returns:
        bool: 처리 결과.
    """
    folder_rel = _safe_relative(info.get("backup_folder_path"))
    file_rel = _safe_relative(info.get("backup_file_path"))
    candidates: list[Path] = []
    if folder_rel:
        candidates.append(BACKUP_BASE_DIR / folder_rel)
    if file_rel:
        candidates.append(BACKUP_BASE_DIR / file_rel)

    for source in candidates:
        if source.is_dir():
            shutil.copytree(source, target_dir, dirs_exist_ok=True)
            return True
        if source.is_file():
            target_dir.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target_dir / source.name)
            return True
    return False


def _ftp_download_tree(ftp: FTP, remote_dir: str, local_dir: Path) -> None:
    """FTP 디렉터리 트리를 로컬 폴더로 재귀 다운로드한다.

    Args:
        ftp: 입력값 (FTP).
        remote_dir: 입력값 (str).
        local_dir: 입력값 (Path).

    Returns:
        None: 처리 결과.
    """
    local_dir.mkdir(parents=True, exist_ok=True)
    current = ftp.pwd()
    ftp.cwd(remote_dir)
    try:
        names = ftp.nlst()
        for name in names:
            leaf = PurePosixPath(name).name
            if leaf in {".", ".."}:
                continue
            try:
                ftp.cwd(name)
                ftp.cwd("..")
                _ftp_download_tree(ftp, name, local_dir / leaf)
            except all_errors:
                with (local_dir / leaf).open("wb") as fp:
                    ftp.retrbinary(f"RETR {name}", fp.write)
    finally:
        ftp.cwd(current)


def _download_from_equipment(info: dict, target_dir: Path) -> None:
    """장비 FTP에서 Recipe 파일 트리를 내려받는다.

    Args:
        info: 입력값 (dict).
        target_dir: 입력값 (Path).

    Returns:
        None: 처리 결과.
    """
    eq_name = str(info["equip"])
    equip_info = [
        eq_info for eq_info in EQUIPMENTS if eq_info["equipment_name"] == eq_name
    ]
    if len(equip_info) > 0:
        equip = create_equipment(equip_info[0])
    else:
        raise ValueError(f"{eq_name} FTP 접속정보가 없습니다.")
    remote_dir = equip.ftp_info.remote_directory
    if not remote_dir:
        raise ValueError(f"{info['recipe_key']}의 장비 Recipe 경로가 없습니다.")
    with FTP(
        timeout=equip.ftp_info.timeout_seconds, encoding=equip.ftp_info.encoding
    ) as ftp:
        ftp.connect(equip.ftp_info.host, equip.ftp_info.port)
        ftp.login(equip.ftp_info.username, equip.ftp_info.password)
        _ftp_download_tree(ftp, remote_dir, target_dir)


def create_backup_archive(items: list[dict]) -> tuple[Path, list[OperationResult]]:
    """선택 Recipe를 수집하고 ZIP 백업 파일을 생성한다.

    Args:
        items: 입력값 (list[dict]).

    Returns:
        tuple[Path, list[OperationResult]]: 처리 결과.
    """
    refs = normalize_items(items)
    infos = _load_recipe_info(refs)
    WORK_DIR.mkdir(parents=True, exist_ok=True)
    work_root = Path(tempfile.mkdtemp(prefix="recipe_backup_", dir=WORK_DIR))
    output_root = work_root / "recipes"
    results: list[OperationResult] = []
    succeeded_keys: list[str] = []

    for ref in refs:
        info = infos.get(ref.recipe_key)
        if info is None:
            results.append(
                OperationResult(
                    ref.recipe_key, False, "db", "DB에 활성 Recipe 정보가 없습니다."
                )
            )
            continue
        target = output_root / ref.eqp_id / ref.recipe_name
        try:
            if _copy_local_backup(info, target):
                source = "server_backup"
            else:
                _download_from_equipment(info, target)
                source = "ftp"
            succeeded_keys.append(ref.recipe_key)
            results.append(OperationResult(ref.recipe_key, True, source, "백업 완료"))
        except Exception as ex:
            results.append(OperationResult(ref.recipe_key, False, "ftp", str(ex)))

    if succeeded_keys:
        PostgresCRUD.execute(
            "UPDATE dm_recipe_info SET dt_backup=%s WHERE recipe_key = ANY(%s)",
            (datetime.now(), succeeded_keys),
        )

    archive_path = work_root / f"recipe_backup_{datetime.now():%Y%m%d_%H%M%S}.zip"
    with zipfile.ZipFile(
        archive_path, "w", compression=zipfile.ZIP_DEFLATED
    ) as archive:
        for file_path in output_root.rglob("*"):
            if file_path.is_file():
                archive.write(file_path, file_path.relative_to(output_root))
    return archive_path, results


def _ftp_remove_tree(ftp: FTP, remote_dir: str) -> None:
    """FTP 디렉터리 트리를 하위 파일부터 재귀 삭제한다.

    Args:
        ftp: 입력값 (FTP).
        remote_dir: 입력값 (str).

    Returns:
        None: 처리 결과.
    """
    current = ftp.pwd()
    ftp.cwd(remote_dir)
    try:
        for name in ftp.nlst():
            leaf = PurePosixPath(name).name
            if leaf in {".", ".."}:
                continue
            try:
                ftp.cwd(name)
                ftp.cwd("..")
                _ftp_remove_tree(ftp, name)
            except all_errors:
                ftp.delete(name)
    finally:
        ftp.cwd(current)
    ftp.rmd(remote_dir)


def delete_after_backup(
    items: list[dict],
) -> tuple[Path, list[OperationResult], list[OperationResult]]:
    """선행 백업 성공 Recipe만 장비와 DB에서 삭제 처리한다.

    Args:
        items: 입력값 (list[dict]).

    Returns:
        tuple[Path, list[OperationResult], list[OperationResult]]: 처리 결과.
    """
    refs = normalize_items(items)
    archive_path, backup_results = create_backup_archive(items)
    successful_backup = {r.recipe_key for r in backup_results if r.success}
    infos = _load_recipe_info(
        [ref for ref in refs if ref.recipe_key in successful_backup]
    )
    delete_results: list[OperationResult] = []

    for ref in refs:
        if ref.recipe_key not in successful_backup:
            delete_results.append(
                OperationResult(
                    ref.recipe_key, False, "backup", "백업 실패로 삭제를 건너뜁니다."
                )
            )
            continue
        info = infos.get(ref.recipe_key)
        if info is None:
            delete_results.append(
                OperationResult(
                    ref.recipe_key, False, "db", "DB Recipe 정보를 찾을 수 없습니다."
                )
            )
            continue
        try:
            equip_info = [
                eq_info
                for eq_info in EQUIPMENTS
                if eq_info["equipment_name"] == ref.eqp_id
            ]
            if len(equip_info) > 0:
                equip = create_equipment(equip_info[0])
            else:
                raise ValueError(f"{ref.eqp_id} FTP 접속정보가 없습니다.")
            remote_dir = equip.ftp_info.remote_directory
            with FTP(
                timeout=equip.ftp_info.timeout_seconds, encoding=equip.ftp_info.encoding
            ) as ftp:
                ftp.connect(equip.ftp_info.host, equip.ftp_info.port)
                ftp.login(equip.ftp_info.username, equip.ftp_info.password)
                _ftp_remove_tree(ftp, remote_dir)
            # 장비 삭제가 성공한 건만 DB 상태를 변경한다.
            PostgresCRUD.execute(
                "UPDATE dm_recipe_info SET is_remove=true WHERE recipe_key=%s",
                (ref.recipe_key,),
            )
            delete_results.append(
                OperationResult(
                    ref.recipe_key, True, "ftp", "장비 삭제 및 DB 상태 변경 완료"
                )
            )
        except Exception as ex:
            delete_results.append(
                OperationResult(ref.recipe_key, False, "ftp", str(ex))
            )
    return archive_path, backup_results, delete_results
