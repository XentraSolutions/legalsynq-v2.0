#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT
GENERATOR_STDOUT="$TMP_DIR/generator.out"
GENERATOR_STDERR="$TMP_DIR/generator.err"

cat > "$TMP_DIR/routes.json" <<'JSON'
{
  "routes": [
    { "pathTemplate": "/dashboard" },
    { "pathTemplate": "/deals/:dealId" },
    { "pathTemplate": "/contacts/:contactId" },
    { "pathTemplate": "/applications/:applicationId" },
    { "pathTemplate": "/reports/:reportId" }
  ]
}
JSON

cat > "$TMP_DIR/config.json" <<'JSON'
{
  "environments": {
    "qa-preview": {
      "host": "links-qa.example.com",
      "appleTeamId": "ABCDE12345",
      "iosBundleId": "com.legalsynq.qa",
      "androidPackage": "com.legalsynq.qa",
      "androidSha256Fingerprints": [
        "AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99"
      ]
    }
  }
}
JSON

python3 "$ROOT/scripts/deep-links/generate-association-files.py" \
  --config "$TMP_DIR/config.json" \
  --routes "$TMP_DIR/routes.json" \
  --output "$TMP_DIR/out"

python3 "$ROOT/scripts/deep-links/validate-association-files.py" \
  --routes "$TMP_DIR/routes.json" \
  --directory "$TMP_DIR/out/qa-preview" \
  --apple-app-id "ABCDE12345.com.legalsynq.qa" \
  --android-package "com.legalsynq.qa"

if python3 "$ROOT/scripts/deep-links/generate-association-files.py" \
  --config "$ROOT/config/deep-links/association-config.example.json" \
  --routes "$TMP_DIR/routes.json" \
  --output "$TMP_DIR/blocked" >"$GENERATOR_STDOUT" 2>"$GENERATOR_STDERR"; then
  echo "expected example config generation to fail until approved values are supplied" >&2
  exit 1
fi

grep -q "missing required values" "$GENERATOR_STDERR"

cat > "$TMP_DIR/unsafe-config.json" <<'JSON'
{
  "environments": {
    "../escaped": {
      "host": "links-qa.example.com",
      "appleTeamId": "ABCDE12345",
      "iosBundleId": "com.legalsynq.qa",
      "androidPackage": "com.legalsynq.qa",
      "androidSha256Fingerprints": [
        "AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99"
      ]
    }
  }
}
JSON

if python3 "$ROOT/scripts/deep-links/generate-association-files.py" \
  --config "$TMP_DIR/unsafe-config.json" \
  --routes "$TMP_DIR/routes.json" \
  --output "$TMP_DIR/out" >"$GENERATOR_STDOUT" 2>"$GENERATOR_STDERR"; then
  echo "expected unsafe environment name generation to fail" >&2
  exit 1
fi

grep -q "environment name must be a lowercase slug" "$GENERATOR_STDERR"

echo "deep-link association tools test passed"
