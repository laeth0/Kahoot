#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${POSTGRES_MONITOR_USER:-}" ]]; then
  echo "ERROR: POSTGRES_MONITOR_USER must be set" >&2
  exit 1
fi

if [[ -z "${POSTGRES_MONITOR_PASSWORD:-}" ]]; then
  echo "ERROR: POSTGRES_MONITOR_PASSWORD must be set" >&2
  exit 1
fi

export PGHOST="${PGHOST:-db}"
export PGPORT="${PGPORT:-5432}"
export PGUSER="${PGUSER:-${POSTGRES_USER:-postgres}}"
export PGDATABASE="${PGDATABASE:-${POSTGRES_DB:-kahoot}}"
export PGPASSWORD="${PGPASSWORD:-${POSTGRES_PASSWORD:-}}"

psql -v ON_ERROR_STOP=1 \
  -v monitor_user="$POSTGRES_MONITOR_USER" \
  -v monitor_pass="$POSTGRES_MONITOR_PASSWORD" \
  <<'EOSQL'
SELECT format('
DO $do$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = %L) THEN
    CREATE ROLE %I WITH LOGIN PASSWORD %L;
  ELSE
    ALTER ROLE %I WITH LOGIN PASSWORD %L;
  END IF;
  GRANT pg_monitor TO %I;
  GRANT CONNECT ON DATABASE %I TO %I;
END
$do$;',
  :'monitor_user',
  :'monitor_user', :'monitor_pass',
  :'monitor_user', :'monitor_pass',
  :'monitor_user',
  current_database(), :'monitor_user'
) \gexec

CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
EOSQL
