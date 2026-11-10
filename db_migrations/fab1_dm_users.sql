-- Run once against the dm_rms database, using credentials allowed to create
-- tables in schema f1. Do NOT apply both migrations to the same schema.
BEGIN;
CREATE TABLE IF NOT EXISTS f1.dm_users (
    user_id VARCHAR(64) PRIMARY KEY,
    password_hash TEXT NOT NULL,
    display_name VARCHAR(100) NOT NULL,
    role VARCHAR(10) NOT NULL DEFAULT 'user' CHECK (role IN ('admin', 'user')),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
COMMIT;
