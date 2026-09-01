#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_DIR="$ROOT/apps/control-center"
APP_NM="$APP_DIR/node_modules"
ROOT_NEXT="$ROOT/node_modules/next"

if [ ! -f "$ROOT_NEXT/dist/bin/next" ]; then
  echo "ERROR: Next.js was not found at $ROOT_NEXT." >&2
  echo "Run 'pnpm install' from the repository root first." >&2
  exit 1
fi

mkdir -p "$APP_NM/.bin"
rm -rf "$APP_NM/next"
ln -s "../../../node_modules/next" "$APP_NM/next"
rm -f "$APP_NM/.bin/next"
ln -s "../next/dist/bin/next" "$APP_NM/.bin/next"

echo "[control-center] Prepared apps/control-center/node_modules/next -> root node_modules/next"
echo "[control-center] Starting Next.js on :${CONTROL_CENTER_PORT:-5004}"

CONTROL_CENTER_API_BASE="${CONTROL_CENTER_API_BASE:-${GATEWAY_URL:-http://127.0.0.1:5010}}" \
GATEWAY_URL="${GATEWAY_URL:-http://127.0.0.1:5010}" \
MONITORING_SOURCE="${MONITORING_SOURCE:-service}" \
pnpm --dir "$APP_DIR" exec next dev -p "${CONTROL_CENTER_PORT:-5004}" --webpack "$@"
