"""Password hashing for local DMRMS users (stdlib only).

Use a unique 16-byte salt, PBKDF2-HMAC-SHA256 and constant-time comparison.
No password or password hash should be returned to frontend.
"""
from __future__ import annotations

import base64
import binascii
import hashlib
import hmac
import secrets

ALGORITHM = "pbkdf2_sha256"
ITERATIONS = 600_000
SALT_SIZE = 16
KEY_SIZE = 32


def hash_password(password: str) -> str:
    if not isinstance(password, str) or not 8 <= len(password) <= 128:
        raise ValueError("비밀번호는 8~128자로 입력하세요.")
    salt = secrets.token_bytes(SALT_SIZE)
    digest = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, ITERATIONS, dklen=KEY_SIZE)
    return "$".join((
        ALGORITHM, str(ITERATIONS),
        base64.b64encode(salt).decode("ascii"),
        base64.b64encode(digest).decode("ascii"),
    ))


def verify_password(password: str, stored: str) -> bool:
    try:
        method, iterations_text, salt_b64, digest_b64 = stored.split("$", 3)
        iterations = int(iterations_text)
        if method != ALGORITHM or not 100_000 <= iterations <= 2_000_000:
            return False
        salt = base64.b64decode(salt_b64, validate=True)
        expected = base64.b64decode(digest_b64, validate=True)
        if len(salt) != SALT_SIZE or len(expected) != KEY_SIZE:
            return False
        actual = hashlib.pbkdf2_hmac(
            "sha256", password.encode("utf-8"), salt, iterations, dklen=len(expected)
        )
        return hmac.compare_digest(actual, expected)
    except (ValueError, TypeError, UnicodeError, binascii.Error):
        return False
