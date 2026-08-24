#!/usr/bin/env bash
set -euo pipefail

if (($# != 6)); then
  echo "usage: $0 DUMP HOST PORT DATABASE USER PGPASSFILE" >&2
  exit 64
fi

dump_path=$1
host=$2
port=$3
database=$4
username=$5
password_file=$6

command -v pg_restore >/dev/null
command -v psql >/dev/null
test -f "$dump_path"
test -f "$dump_path.sha256"
test -f "$password_file"
(cd "$(dirname "$dump_path")" && sha256sum --check "$(basename "$dump_path").sha256")

export PGPASSFILE
PGPASSFILE=$(cd "$(dirname "$password_file")" && pwd)/$(basename "$password_file")
table_count=$(
  psql \
    --host "$host" \
    --port "$port" \
    --username "$username" \
    --dbname "$database" \
    --no-password \
    --tuples-only \
    --no-align \
    --command "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema');"
)
if [[ $table_count != 0 ]]; then
  echo "restore target is not clean; refusing to overwrite user tables" >&2
  exit 65
fi

pg_restore \
  --host "$host" \
  --port "$port" \
  --username "$username" \
  --dbname "$database" \
  --no-password \
  --exit-on-error \
  --single-transaction \
  --no-owner \
  --no-acl \
  "$dump_path"
