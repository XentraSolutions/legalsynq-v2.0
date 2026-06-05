#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST_ROOT="$ROOT/dist/frontend"

package_runtime_artifact() {
  local app_dir="$1"
  local artifact_dir="$2"
  local app_name="$3"

  rm -rf "$artifact_dir"
  mkdir -p "$artifact_dir"

  cp -R "$app_dir/.next" "$artifact_dir/.next"
  cp "$app_dir/package.json" "$artifact_dir/package.json"

  if [ -f "$app_dir/next.config.mjs" ]; then
    cp "$app_dir/next.config.mjs" "$artifact_dir/next.config.mjs"
  fi

  if [ -d "$app_dir/public" ]; then
    cp -R "$app_dir/public" "$artifact_dir/public"
  fi

  echo "[$app_name] Packaged runtime artifact at $artifact_dir"
}

find_next_bin() {
  local app_dir="$1"
  local candidate=""

  for candidate in \
    "$app_dir/node_modules/next/dist/bin/next" \
    "$ROOT/node_modules/next/dist/bin/next" \
    "$(npm root --prefix "$app_dir" 2>/dev/null)/next/dist/bin/next" \
    "$(npm root 2>/dev/null)/next/dist/bin/next"; do
    if [ -f "$candidate" ]; then
      echo "$candidate"
      return 0
    fi
  done

  echo "ERROR: Unable to find Next.js binary for $app_dir" >&2
  return 1
}

install_app_dependencies() {
  local app_dir="$1"
  local app_name="$2"

  echo "[$app_name] Installing dependencies..."
  if (
    cd "$app_dir"
    pnpm install --frozen-lockfile
  ); then
    return 0
  fi

  echo "[$app_name] WARNING: pnpm install failed; continuing with existing dependencies" >&2
}

build_web() {
  local app_dir="$ROOT/apps/web"
  local next_bin

  install_app_dependencies "$app_dir" "web"
  next_bin="$(find_next_bin "$app_dir")"

  echo "====== Building apps/web ======"
  echo "[web] Using next binary: $next_bin"

  cd "$app_dir"
  rm -rf .next

  NODE_OPTIONS="${WEB_NODE_OPTIONS:---max-old-space-size=2048}" \
  NEXT_PUBLIC_ENV="${NEXT_PUBLIC_ENV:-production}" \
  NEXT_PUBLIC_TENANT_CODE="${NEXT_PUBLIC_TENANT_CODE:-}" \
  GATEWAY_URL="${GATEWAY_URL:-http://127.0.0.1:5010}" \
  CC_COMMON_PORTAL_HOSTNAME="${CC_COMMON_PORTAL_HOSTNAME:-careconnect-demo.legalsynq.com}" \
  node "$next_bin" build

  mkdir -p "$DIST_ROOT"
  package_runtime_artifact "$app_dir" "$DIST_ROOT/web" "web"
}

build_control_center() {
  local app_dir="$ROOT/apps/control-center"
  local next_bin

  install_app_dependencies "$app_dir" "control-center"
  next_bin="$(find_next_bin "$app_dir")"

  echo "====== Building apps/control-center ======"
  echo "[control-center] Using next binary: $next_bin"

  cd "$app_dir"
  rm -rf .next

  NODE_OPTIONS="${CC_NODE_OPTIONS:---max-old-space-size=1024}" \
  NODE_ENV="${NODE_ENV:-production}" \
  GATEWAY_URL="${GATEWAY_URL:-http://127.0.0.1:5010}" \
  CONTROL_CENTER_API_BASE="${CONTROL_CENTER_API_BASE:-${GATEWAY_URL:-http://127.0.0.1:5010}}" \
  NEXT_PUBLIC_CONTROL_CENTER_ORIGIN="${NEXT_PUBLIC_CONTROL_CENTER_ORIGIN:-http://localhost:5004}" \
  CONTROL_CENTER_SELF_URL="${CONTROL_CENTER_SELF_URL:-http://localhost:5004}" \
  REPORTS_SERVICE_URL="${REPORTS_SERVICE_URL:-http://127.0.0.1:5029}" \
  COMMERCE_SERVICE_URL="${COMMERCE_SERVICE_URL:-http://127.0.0.1:5030}" \
  BILLING_SERVICE_URL="${BILLING_SERVICE_URL:-http://127.0.0.1:5031}" \
  node "$next_bin" build

  mkdir -p "$DIST_ROOT"
  package_runtime_artifact "$app_dir" "$DIST_ROOT/control-center" "control-center"
}

main() {
  echo "====== LegalSynq local frontend build ======"
  echo "[root] $ROOT"
  rm -rf "$DIST_ROOT"
  mkdir -p "$DIST_ROOT"

  build_web
  build_control_center

  echo "====== Frontend build complete ======"
}

main "$@"
