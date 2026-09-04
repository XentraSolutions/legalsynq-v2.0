#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT
GENERATOR_STDOUT="$TMP_DIR/generator.out"
GENERATOR_STDERR="$TMP_DIR/generator.err"

cat > "$TMP_DIR/config.json" <<'JSON'
{
  "environments": {
    "qa-preview": {
      "host": "links-qa.example.com",
      "appleTeamId": "ABCDE12345",
      "iosBundleId": "com.legalsynq.qa",
      "androidPackage": "com.legalsynq.qa",
      "androidSha256Fingerprints": [
        "AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99",
        "11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00"
      ]
    }
  }
}
JSON

python3 "$ROOT/scripts/deep-links/generate-association-files.py" \
  --config "$TMP_DIR/config.json" \
  --association-scope portal-root \
  --output "$TMP_DIR/out"

python3 "$ROOT/scripts/deep-links/validate-association-files.py" \
  --association-scope portal-root \
  --directory "$TMP_DIR/out/qa-preview" \
  --apple-app-id "ABCDE12345.com.legalsynq.qa" \
  --android-package "com.legalsynq.qa"

python3 - "$TMP_DIR/out/qa-preview" <<'PY'
import json
import sys
from pathlib import Path

directory = Path(sys.argv[1])
aasa = json.loads((directory / "apple-app-site-association").read_text())
details = aasa["applinks"]["details"]
assert details[0]["appIDs"] == ["ABCDE12345.com.legalsynq.qa"]
assert [component["/"] for component in details[0]["components"]] == ["/"]

assetlinks = json.loads((directory / "assetlinks.json").read_text())
assert assetlinks[0]["relation"] == ["delegate_permission/common.handle_all_urls"]
assert assetlinks[0]["target"]["package_name"] == "com.legalsynq.qa"
assert len(assetlinks[0]["target"]["sha256_cert_fingerprints"]) == 2
assert "path" not in json.dumps(assetlinks).lower()
PY

python3 "$ROOT/scripts/deep-links/generate-association-files.py" \
  --config "$TMP_DIR/config.json" \
  --association-scope portal-root \
  --output "$TMP_DIR/out-repeat"
cmp "$TMP_DIR/out/qa-preview/apple-app-site-association" "$TMP_DIR/out-repeat/qa-preview/apple-app-site-association"
cmp "$TMP_DIR/out/qa-preview/assetlinks.json" "$TMP_DIR/out-repeat/qa-preview/assetlinks.json"

mkdir -p "$TMP_DIR/invalid"
cp "$TMP_DIR/out/qa-preview/assetlinks.json" "$TMP_DIR/invalid/assetlinks.json"
python3 - "$TMP_DIR/out/qa-preview/apple-app-site-association" "$TMP_DIR/invalid/apple-app-site-association" <<'PY'
import json
import sys
from pathlib import Path

payload = json.loads(Path(sys.argv[1]).read_text())
payload["applinks"]["details"][0]["components"].append({"/": "/dashboard"})
Path(sys.argv[2]).write_text(json.dumps(payload))
PY

if python3 "$ROOT/scripts/deep-links/validate-association-files.py" \
  --association-scope portal-root \
  --directory "$TMP_DIR/invalid" \
  --apple-app-id "ABCDE12345.com.legalsynq.qa" \
  --android-package "com.legalsynq.qa" >"$GENERATOR_STDOUT" 2>"$GENERATOR_STDERR"; then
  echo "expected portal-root validation to reject a resource claim" >&2
  exit 1
fi

grep -q "must contain exactly" "$GENERATOR_STDERR"

if python3 "$ROOT/scripts/deep-links/generate-association-files.py" \
  --config "$ROOT/config/deep-links/association-config.example.json" \
  --association-scope portal-root \
  --output "$TMP_DIR/blocked" >"$GENERATOR_STDOUT" 2>"$GENERATOR_STDERR"; then
  echo "expected example config generation to fail until approved values are supplied" >&2
  exit 1
fi

grep -q "missing required values" "$GENERATOR_STDERR"

python3 - "$TMP_DIR/config.json" "$TMP_DIR/missing-apple.json" "$TMP_DIR/missing-android.json" <<'PY'
import json
import sys
from pathlib import Path

source = json.loads(Path(sys.argv[1]).read_text())
missing_apple = json.loads(json.dumps(source))
missing_apple["environments"]["qa-preview"]["appleTeamId"] = None
Path(sys.argv[2]).write_text(json.dumps(missing_apple))
missing_android = json.loads(json.dumps(source))
missing_android["environments"]["qa-preview"]["androidPackage"] = None
Path(sys.argv[3]).write_text(json.dumps(missing_android))
PY

for required_case in missing-apple missing-android; do
  if python3 "$ROOT/scripts/deep-links/generate-association-files.py" \
    --config "$TMP_DIR/$required_case.json" \
    --association-scope portal-root \
    --output "$TMP_DIR/$required_case-out" >"$GENERATOR_STDOUT" 2>"$GENERATOR_STDERR"; then
    echo "expected $required_case identity generation to fail" >&2
    exit 1
  fi
  grep -q "missing required values" "$GENERATOR_STDERR"
done

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
  --association-scope portal-root \
  --output "$TMP_DIR/out" >"$GENERATOR_STDOUT" 2>"$GENERATOR_STDERR"; then
  echo "expected unsafe environment name generation to fail" >&2
  exit 1
fi

grep -q "environment name must be a lowercase slug" "$GENERATOR_STDERR"

if python3 "$ROOT/scripts/deep-links/generate-association-files.py" \
  --config "$TMP_DIR/config.json" \
  --output "$TMP_DIR/missing-scope" >"$GENERATOR_STDOUT" 2>"$GENERATOR_STDERR"; then
  echo "expected generation without explicit association scope to fail" >&2
  exit 1
fi

grep -q "association-scope" "$GENERATOR_STDERR"

echo "deep-link association tools test passed"
