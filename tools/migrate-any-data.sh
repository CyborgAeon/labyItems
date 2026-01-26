#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET_BIN="${DOTNET:-$HOME/.dotnet/dotnet}"
DB_PATH="${1:-}"
SEED_JSON="${2:-$ROOT/labyItems/Resources/Raw/druids_way/evocs.json}"

if [[ -z "$DB_PATH" ]]; then
  echo "Usage: $0 <path-to-sqlite-db> [seed-json-path]" >&2
  echo "If the DB does not exist, it will be generated from the seed JSON before migrations run." >&2
  exit 1
fi

mkdir -p "$(dirname "$DB_PATH")"

if [[ ! -f "$SEED_JSON" ]]; then
  echo "Seed JSON not found at $SEED_JSON" >&2
  exit 2
fi

if [[ ! -f "$DB_PATH" ]]; then
  echo "Database not found at $DB_PATH; generating from $SEED_JSON..."
  "$DOTNET_BIN" run --project "$ROOT/tools/evocdbgen/evocdbgen.csproj" -c Release -- "$SEED_JSON" "$DB_PATH"
fi

echo "Running migrations against $DB_PATH..."
"$DOTNET_BIN" run --project "$ROOT/tools/migrator/migrator.csproj" -c Release -- "$DB_PATH"
echo "Migrations complete."
