# DM RMS Renewal – Frontend / Auth Patch (2026-11-10)

This package is a **source-code patch**, not a compiled installer or a GitHub commit. The connected GitHub integration allowed reading `MinY96/DMRMS_Renewal` but refused branch creation with `403 Resource not accessible by integration`. The script therefore applies changes to a **fresh local checkout of that repository** and backs up original changed files.

## Included scope

- Equipment summary: suppress legacy `no_master` column if received from an old server (also excludes it from copy/Excel export). Current `equipment-summary` SQL already excludes `no_master`.
- Master management: top-level Model selector loaded from `/api/master/models`; reinitialize filters and data on Model changes; Model injected into list query; remove lower reset button; disallow creating without Model/admin; prevent duplicated Fab2 filters with single initialization, generation check, normalized filter keys/options.
- New Master window: display locked Model, only Model-specific criteria and parameter options/template, include Model in create request and URL; reject mismatch on backend.
- Backend Model options: union of Models already in `dm_matching_rate` and `dm_master_recipe_info`.
- Backend template bug fix: select latest master **for the specified Model**, rather than latest master globally.
- Authentication: replace mock `/api/auth/login`/`register` endpoints with PostgreSQL-backed accounts using salted PBKDF2-HMAC-SHA256 (600,000 iterations). Registration creates only `user` role, duplicate IDs return HTTP 409. No passwords saved in plaintext.
- FAB1 and FAB2: separate `f1.dm_users` and `f2.dm_users` tables in the existing `dm_rms` DB; schema is selected through the existing server's `POSTGRES_SCHEMA` configuration.
- `1_backup`: **not modified**. UI appearance beyond requested changes is **not modified**.

## 1. Patch source locally

Make sure you have a clean checkout of `https://github.com/MinY96/DMRMS_Renewal` with the folder structure `1_backup/`, `2_server/`, and `3_frontend/`.

```bash
python apply_patch.py --root /path/to/DMRMS_Renewal --dry-run
python apply_patch.py --root /path/to/DMRMS_Renewal
```

On Windows, use, for example:

```powershell
py -3.12 .\apply_patch.py --root 'D:\DMRMS_Renewal' --dry-run
py -3.12 .\apply_patch.py --root 'D:\DMRMS_Renewal'
```

**Do not run the patch twice.** The script performs an all-file preflight first, replaces matched anchors, and backs up original files under `.dmrms_patch_backup_20261110/`. If your checkout has since changed, it will report an anchor mismatch without modifying the source. Review the Git diff before deploying.

## 2. Apply database migration before switching authentication

The GitHub `dump-dm_rms-202611051725` is a 22 MB PostgreSQL custom-format dump. The available repository connector cannot decode this binary backup, so the migration below is based on the current server's connection code and separate `f1`/`f2` PostgreSQL schema design, **not** on verified dump contents.

The patcher copies SQL files into the repository `db_migrations/` folder. Run against the existing **dm_rms** database as a user with `CREATE` permission in the respective schema:

```bash
psql -h YOUR_DB_HOST -U YOUR_MIGRATION_USER -d dm_rms -v ON_ERROR_STOP=1 -f db_migrations/fab1_dm_users.sql
psql -h YOUR_DB_HOST -U YOUR_MIGRATION_USER -d dm_rms -v ON_ERROR_STOP=1 -f db_migrations/fab2_dm_users.sql
```

Check that each server's `.env` sets the correct `POSTGRES_SCHEMA=f1` or `POSTGRES_SCHEMA=f2` (the backend uses `search_path={schema},public`). Existing accounts are **not** migrated automatically because the current app uses hardcoded demo credentials, not an account table.

## 3. Create initial admin accounts

Before deploying the new backend, run in each server folder with the real PostgreSQL environment configured:

```bash
cd 2_server/fab1
python scripts/bootstrap_admin.py --user-id admin --display-name 관리자
cd ../fab2
python scripts/bootstrap_admin.py --user-id admin --display-name 관리자
```

f1
admin/f1공정관리

f2
admin/f2공정관리

Enter admin passwords when prompted. The script also rotates the password if the specified ID already exists. **Hardcoded `admin/admin` and `user/user` test credentials will no longer work.** New registration always assigns the `user` role.

## 4. Verify

1. With the Python 3.12 virtual environments and dependencies installed on FAB1/FAB2, check `POSTGRES_SCHEMA` and start both API servers.
2. On each FAB, register a new user in the WPF client, then log in with the newly registered account. Verify with `SELECT user_id, display_name, role, is_active FROM f1.dm_users;` (and separately `f2.dm_users`).
3. Check duplicate user ID returns `409 USER_EXISTS`; incorrect password returns `401 INVALID_CREDENTIALS`.
4. On FAB2 select two different Models repeatedly and use **필터 새로고침**; confirm each criterion appears once.
5. Query Master list: result rows must match top-selected Model. Confirm New Master button is disabled until Model selected, and popup shows selected Model.
6. Create Master after loading a model-specific template, verify only selected Model's data is read and inserted.
7. Confirm equipment summary does not display or export `no_master`, while recipe list behavior is unchanged.
8. Verify normal user cannot use Master modification UI and cannot delete Recipes from the UI. **Server-side authorization enforcement is an unresolved security requirement; see below.**

Unit tests (no DB needed): `python -m pytest tests/test_user_accounts.py -q` from this package directory. After applying the patch, set `DMRMS_REPO_ROOT` to the checkout path and run `python -m pytest tests/test_applied_contracts.py -q` for source contract checks. Python syntax and XAML structure were statically checked in the patch assembly environment. Building/running the Windows-only WPF app, executing actual Postgres migrations, and integration testing real FAB hosts are **not possible** in that environment. Run `dotnet build 3_frontend/DM_RMS.Wpf.sln -c Release` in Windows/Visual Studio.

## Security and operational notes

- **Urgent: repository is public and includes `dump-dm_rms-202611051725`**. A production recipe/database dump must not be stored in a public repository. Remove public access and purge its Git history as required by your organization's rules. Simply deleting the file in a later commit does not remove old copies.
- Existing Master/delete endpoints do **not** enforce server-side authorization tokens. Hiding buttons for `user` in WPF is not a security boundary. Add server-side authentication and role checks before any deployment to an untrusted network.
- The sign-up API is currently self-service and unapproved. Internal production deployments should consider restricting registration by invitation/admin approval, adding rate limiting, and using HTTPS/TLS with robust session authentication.
- DB schema migration, initial accounts, server deployment and ClickOnce client update should be coordinated. Never upgrade clients before model-aware APIs are available, and never replace the login backend before admin accounts have been provisioned.

## Proposed UI/UX improvements (not implemented)

1. Shared Master Model selection as a persistent header chip (show current model in both list and modal) and disable search while criteria load.
2. Replace free-text `Level`/`Layer` with suggestion-enabled search after measuring option cardinality, for more discoverability without overly large ComboBoxes.
3. Show an unsaved-change warning on Master edit cancellation/selection change; current cancellation retains edits in memory.
4. Move destructive Recipe deletion to an explicit two-stage confirmation with success/failure counts and safe partial refresh after timeouts.
5. Centralize WPF loading state, error dialogs and request cancellation to avoid race conditions in other tabs.

The separate list of suggested, unimplemented interface improvements is in `UIUX_SUGGESTIONS_NOT_APPLIED.md`.
