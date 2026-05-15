#!/usr/bin/env sh
#
# generate-openapi.sh
#
# Generate the canonical Billing OpenAPI contract snapshot.
#
# Usage (from anywhere):
#   services/billing/scripts/generate-openapi.sh
#
# Output:
#   services/billing/openapi/billing-openapi.json
#
# Requirements:
#   - .NET 8 SDK on PATH (`dotnet --version` must report 8.x)
#   - Network access for `dotnet tool restore` and `dotnet restore` on first run
#
# What it does:
#   1. Resolves the repo root from the script's own location.
#   2. Forces a tenant-safe environment so generation never touches a real
#      database, never runs production migrations, and never exposes the
#      disabled-by-default platform-template endpoints in the contract.
#   3. Restores local tools (Swashbuckle.AspNetCore.Cli) and the Billing
#      solution.
#   4. Builds Billing.Api in Release.
#   5. Runs `dotnet swagger tofile` to write the document to disk.
#
# Safety:
#   - BILLING_INTERNAL_TOKEN is set to a non-production placeholder solely so
#     the host satisfies its fail-closed startup precondition. The token is
#     never written to the generated contract.
#   - BILLING_DB_CONNECTION and ConnectionStrings__Billing are unset so the
#     Billing infrastructure falls through to the in-memory provider, which
#     never opens a real socket and never runs migrations.
#
set -eu

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BILLING_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_ROOT="$(cd "$BILLING_DIR/../.." && pwd)"

SOLUTION="$BILLING_DIR/Billing.sln"
API_PROJECT="$BILLING_DIR/src/Billing.Api/Billing.Api.csproj"
API_ASSEMBLY_DIR="$BILLING_DIR/src/Billing.Api/bin/Release/net8.0"
API_ASSEMBLY="$API_ASSEMBLY_DIR/Billing.Api.dll"
SWAGGER_DOC_NAME="v1"

OUT_DIR="$BILLING_DIR/openapi"
OUT_FILE="$OUT_DIR/billing-openapi.json"

# ---------------------------------------------------------------------------
# Pre-flight
# ---------------------------------------------------------------------------
if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: 'dotnet' is not installed or not on PATH. Install the .NET 8 SDK and retry." >&2
  exit 2
fi

mkdir -p "$OUT_DIR"

# ---------------------------------------------------------------------------
# Force a tenant-safe generation environment.
# These exports apply only to this shell process (and its dotnet children).
# ---------------------------------------------------------------------------
export ASPNETCORE_ENVIRONMENT="Development"
export BILLING_INTERNAL_TOKEN="dev-only-openapi-token"
export BILLING_RUN_MIGRATIONS="false"
export BILLING_ENABLE_PLATFORM_TEMPLATES="false"
unset BILLING_DB_CONNECTION 2>/dev/null || true
unset ConnectionStrings__Billing 2>/dev/null || true
unset ConnectionStrings__DefaultConnection 2>/dev/null || true

# ---------------------------------------------------------------------------
# Restore + build (idempotent)
# ---------------------------------------------------------------------------
cd "$BILLING_DIR"

echo "==> dotnet tool restore (Swashbuckle.AspNetCore.Cli, dotnet-ef)"
dotnet tool restore

echo "==> dotnet restore $SOLUTION"
dotnet restore "$SOLUTION"

echo "==> dotnet build $API_PROJECT (Release)"
dotnet build "$API_PROJECT" -c Release --no-restore

if [ ! -f "$API_ASSEMBLY" ]; then
  echo "ERROR: built assembly not found at $API_ASSEMBLY" >&2
  exit 3
fi

# ---------------------------------------------------------------------------
# Generate
# ---------------------------------------------------------------------------
echo "==> dotnet swagger tofile --output $OUT_FILE $API_ASSEMBLY $SWAGGER_DOC_NAME"
dotnet swagger tofile \
  --output "$OUT_FILE" \
  "$API_ASSEMBLY" \
  "$SWAGGER_DOC_NAME"

# ---------------------------------------------------------------------------
# Sanity post-checks (non-fatal probes; emit warnings only)
# ---------------------------------------------------------------------------
if [ ! -s "$OUT_FILE" ]; then
  echo "ERROR: generated file is empty: $OUT_FILE" >&2
  exit 4
fi

if ! grep -q '"openapi"' "$OUT_FILE"; then
  echo "WARN: '\"openapi\"' field not found in generated document — output may be malformed." >&2
fi
if ! grep -q '"Billing API"' "$OUT_FILE"; then
  echo "WARN: 'Billing API' title not found in generated document." >&2
fi
if ! grep -q 'X-Internal-Token' "$OUT_FILE"; then
  echo "WARN: 'X-Internal-Token' not found in generated document." >&2
fi
if ! grep -q 'X-Tenant-Id' "$OUT_FILE"; then
  echo "WARN: 'X-Tenant-Id' not found in generated document." >&2
fi
if grep -q '/api/invoice-templates/platform' "$OUT_FILE"; then
  echo "WARN: platform-template paths leaked into the generated contract — check that BILLING_ENABLE_PLATFORM_TEMPLATES=false and the document filter is registered." >&2
fi

REL_OUT="${OUT_FILE#"$REPO_ROOT/"}"
echo "OK: generated $REL_OUT"
