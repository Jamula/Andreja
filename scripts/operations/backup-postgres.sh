#!/usr/bin/env bash
set -euo pipefail

if (($# != 6)); then
  echo "usage: $0 HOST PORT DATABASE USER PGPASSFILE OUTPUT_DIRECTORY" >&2
  exit 64
fi

host=$1
port=$2
database=$3
username=$4
password_file=$5
output_directory=$6

command -v pg_dump >/dev/null
test -f "$password_file"
mkdir -p "$output_directory"

stamp=$(date -u +%Y%m%dT%H%M%SZ)
dump_path="$output_directory/andreja-$stamp.dump"
export PGPASSFILE
PGPASSFILE=$(cd "$(dirname "$password_file")" && pwd)/$(basename "$password_file")

trap 'rm -f "$dump_path"' ERR
pg_dump \
  --host "$host" \
  --port "$port" \
  --username "$username" \
  --dbname "$database" \
  --format custom \
  --compress 9 \
  --no-owner \
  --no-acl \
  --no-password \
  --file "$dump_path"
trap - ERR

(cd "$output_directory" && sha256sum "$(basename "$dump_path")" >"$(basename "$dump_path").sha256")
{
  printf 'created_at_utc=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  printf 'database=%s\nhost=%s\nport=%s\n' "$database" "$host" "$port"
  printf 'pg_dump=%s\n' "$(pg_dump --version)"
  printf 'sha256=%s\n' "$(sha256sum "$dump_path" | awk '{print $1}')"
} >"$dump_path.metadata.txt"

printf '%s\n' "$dump_path"
