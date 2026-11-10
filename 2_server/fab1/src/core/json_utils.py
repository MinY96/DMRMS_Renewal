"""JSON 직렬화 전용 변환 유틸리티.

Pandas/NumPy 값은 표준 JSON에 없는 ``NaN`` 또는 ``NaT``를 포함할 수 있다.
이 모듈은 API 응답 직전에 값을 재귀적으로 정규화하여 .NET의
``System.Text.Json``에서도 안전하게 역직렬화할 수 있게 한다.
"""

from __future__ import annotations

import math
from datetime import date, datetime
from decimal import Decimal
from typing import Any

import pandas as pd


def to_json_safe(value: Any) -> Any:
    """임의의 값을 표준 JSON 직렬화가 가능한 Python 기본 타입으로 변환한다.

    Args:
        value: 변환할 값. dict/list/Pandas/NumPy/날짜/Decimal을 포함할 수 있다.

    Returns:
        JSON 직렬화 가능한 값. 결측치와 무한대는 ``None``으로 변환된다.

    Notes:
        dict와 sequence는 재귀적으로 처리한다. 일반 scalar는 불필요한 복사를
        피하기 위해 가능한 한 원본 값을 그대로 반환한다.
    """
    if value is None:
        return None

    if isinstance(value, dict):
        return {str(key): to_json_safe(item) for key, item in value.items()}

    if isinstance(value, (list, tuple, set)):
        return [to_json_safe(item) for item in value]

    # pd.isna는 scalar뿐 아니라 일부 객체에서 배열을 반환할 수 있으므로
    # bool 변환이 가능한 경우에만 결측치로 판단한다.
    try:
        if bool(pd.isna(value)):
            return None
    except (TypeError, ValueError):
        pass

    if isinstance(value, (datetime, date)):
        return value.isoformat()

    if isinstance(value, Decimal):
        return float(value)

    # NumPy scalar는 item()으로 Python 기본 scalar로 변환한다.
    item_method = getattr(value, "item", None)
    if callable(item_method) and not isinstance(value, (str, bytes, bytearray)):
        try:
            native_value = item_method()
            if native_value is not value:
                return to_json_safe(native_value)
        except (TypeError, ValueError):
            pass

    if isinstance(value, float) and not math.isfinite(value):
        return None

    return value
