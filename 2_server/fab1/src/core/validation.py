"""공통 입력값 검증 함수."""

VALID_FABS = {"fab1", "fab2"}


def normalize_fab(fab: str) -> str:
    """FAB 문자열을 검증하고 표준 소문자 값으로 반환한다.

    Args:
        fab: 입력값 (str).

    Returns:
        str: 처리 결과.
    """
    fab_norm = (fab or "").lower().strip()
    if fab_norm not in VALID_FABS:
        raise ValueError(f"지원하지 않는 fab입니다: {fab}")
    return fab_norm
