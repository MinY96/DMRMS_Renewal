"""Provision/rotate an admin account using this FAB's configured PostgreSQL schema.

Run from 2_server/fab1 or 2_server/fab2 after applying the SQL migration:
    python scripts/bootstrap_admin.py --user-id admin --display-name 관리자
Password input is prompted (not echoed, not saved to shell history).
"""
from __future__ import annotations

import argparse
import getpass
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from src.databases import PostgresCRUD  # noqa: E402
from src.services.user_accounts import hash_password  # noqa: E402


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--user-id", required=True)
    parser.add_argument("--display-name", default="관리자")
    args = parser.parse_args()
    if not re.fullmatch(r"[A-Za-z0-9_.-]{3,64}", args.user_id):
        parser.error("user-id must be 3~64 characters")
    password = getpass.getpass("New admin password: ")
    confirmation = getpass.getpass("Confirm password: ")
    if password != confirmation:
        parser.error("Passwords do not match")
    password_hash = hash_password(password)
    PostgresCRUD.execute(
        """INSERT INTO dm_users (user_id, password_hash, display_name, role, is_active)
           VALUES (%s, %s, %s, 'admin', TRUE)
           ON CONFLICT (user_id) DO UPDATE SET
               password_hash = EXCLUDED.password_hash,
               display_name = EXCLUDED.display_name,
               role = 'admin',
               is_active = TRUE,
               updated_at = NOW()""",
        (args.user_id, password_hash, args.display_name),
    )
    print(f"Admin provisioned: {args.user_id}")


if __name__ == "__main__":
    main()
