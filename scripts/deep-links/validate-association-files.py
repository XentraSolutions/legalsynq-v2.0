#!/usr/bin/env python3
import argparse
import json
import re
import sys
from pathlib import Path

FINGERPRINT_RE = re.compile(r"^[0-9A-F]{2}(?::[0-9A-F]{2}){31}$")


def fail(message: str) -> int:
    print(f"ERROR: {message}", file=sys.stderr)
    return 1


def load_json(path: Path):
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate generated deep-link association files.")
    parser.add_argument("--association-scope", required=True, choices=("portal-root",))
    parser.add_argument("--routes", help=argparse.SUPPRESS)
    parser.add_argument("--directory", required=True, help="Directory containing apple-app-site-association and assetlinks.json")
    parser.add_argument("--apple-app-id", required=True)
    parser.add_argument("--android-package", required=True)
    args = parser.parse_args()

    directory = Path(args.directory)
    aasa_path = directory / "apple-app-site-association"
    assetlinks_path = directory / "assetlinks.json"
    try:
        aasa = load_json(aasa_path)
        details = aasa.get("applinks", {}).get("details", [])
        if len(details) != 1:
            return fail("AASA must contain exactly one applinks.details entry")
        app_ids = details[0].get("appIDs", [])
        if app_ids != [args.apple_app_id]:
            return fail(f"AASA appIDs mismatch: {app_ids!r}")
        components = details[0].get("components", [])
        component_paths = [component.get("/") for component in components]
        if component_paths != ["/"]:
            return fail(f"AASA portal-root scope must contain exactly ['/']: {component_paths!r}")

        assetlinks = load_json(assetlinks_path)
        if not isinstance(assetlinks, list) or len(assetlinks) != 1:
            return fail("assetlinks.json must contain exactly one statement")
        statement = assetlinks[0]
        if statement.get("relation") != ["delegate_permission/common.handle_all_urls"]:
            return fail("assetlinks relation mismatch")
        target = statement.get("target", {})
        if target.get("namespace") != "android_app":
            return fail("assetlinks target namespace mismatch")
        if target.get("package_name") != args.android_package:
            return fail("assetlinks package mismatch")
        fingerprints = target.get("sha256_cert_fingerprints")
        if not isinstance(fingerprints, list) or not fingerprints:
            return fail("assetlinks fingerprints missing")
        for fingerprint in fingerprints:
            if not isinstance(fingerprint, str) or not FINGERPRINT_RE.fullmatch(fingerprint):
                return fail(f"invalid fingerprint format: {fingerprint!r}")
    except FileNotFoundError as exc:
        return fail(f"required file not found: {exc.filename}")
    except json.JSONDecodeError as exc:
        return fail(f"invalid JSON: {exc}")
    except ValueError as exc:
        return fail(str(exc))

    print("deep-link association validation passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
