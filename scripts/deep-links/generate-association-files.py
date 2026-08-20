#!/usr/bin/env python3
import argparse
import json
import re
import sys
from pathlib import Path

FINGERPRINT_RE = re.compile(r"^[0-9A-F]{2}(?::[0-9A-F]{2}){31}$")
HOST_RE = re.compile(r"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+$")
TEAM_ID_RE = re.compile(r"^[A-Z0-9]{10}$")
PACKAGE_RE = re.compile(r"^[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+$")
ENVIRONMENT_RE = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")


class ConfigError(Exception):
    pass


def load_json(path: Path):
    try:
        with path.open("r", encoding="utf-8") as handle:
            return json.load(handle)
    except FileNotFoundError as exc:
        raise ConfigError(f"required file not found: {path}") from exc
    except json.JSONDecodeError as exc:
        raise ConfigError(f"invalid JSON in {path}: {exc}") from exc


def route_items(registry):
    if isinstance(registry, list):
        return registry
    if isinstance(registry, dict):
        for key in ("routes", "deepLinks", "deep_links"):
            value = registry.get(key)
            if isinstance(value, list):
                return value
    raise ConfigError("route registry must be a JSON array or object with a routes array")


def route_path(route):
    if isinstance(route, str):
        return route
    if isinstance(route, dict):
        for key in ("pathTemplate", "path", "route", "pattern"):
            value = route.get(key)
            if isinstance(value, str):
                return value
    raise ConfigError(f"route entry does not contain a path string: {route!r}")


def aasa_component_path(route: str) -> str:
    if not route.startswith("/"):
        raise ConfigError(f"route must start with '/': {route}")
    parts = route.split("/")
    converted = ["*" if part.startswith(":") else part for part in parts]
    return "/".join(converted)


def load_routes(routes_path: Path):
    registry = load_json(routes_path)
    paths = [route_path(item) for item in route_items(registry)]
    if not paths:
        raise ConfigError("route registry contains no routes")
    return paths


def validate_environment(name: str, env_config: dict):
    if not ENVIRONMENT_RE.fullmatch(name):
        raise ConfigError(f"{name}: environment name must be a lowercase slug")

    host = env_config.get("host")
    apple_team_id = env_config.get("appleTeamId")
    ios_bundle_id = env_config.get("iosBundleId")
    android_package = env_config.get("androidPackage")
    fingerprints = env_config.get("androidSha256Fingerprints")

    missing = [
        key
        for key, value in (
            ("host", host),
            ("appleTeamId", apple_team_id),
            ("iosBundleId", ios_bundle_id),
            ("androidPackage", android_package),
        )
        if not isinstance(value, str) or not value.strip()
    ]
    if missing:
        raise ConfigError(f"{name}: missing required values: {', '.join(missing)}")
    if not HOST_RE.fullmatch(host):
        raise ConfigError(f"{name}: host is not a valid hostname: {host}")
    if not TEAM_ID_RE.fullmatch(apple_team_id):
        raise ConfigError(f"{name}: appleTeamId must be a 10-character Apple Team ID")
    if not PACKAGE_RE.fullmatch(ios_bundle_id):
        raise ConfigError(f"{name}: iosBundleId is not a valid reverse-DNS identifier")
    if not PACKAGE_RE.fullmatch(android_package):
        raise ConfigError(f"{name}: androidPackage is not a valid package identifier")
    if not isinstance(fingerprints, list) or not fingerprints:
        raise ConfigError(f"{name}: androidSha256Fingerprints must contain at least one public SHA-256 fingerprint")
    for fingerprint in fingerprints:
        if not isinstance(fingerprint, str) or not FINGERPRINT_RE.fullmatch(fingerprint):
            raise ConfigError(f"{name}: invalid SHA-256 fingerprint format: {fingerprint!r}")


def build_aasa(env_config: dict, routes: list[str]):
    app_id = f"{env_config['appleTeamId']}.{env_config['iosBundleId']}"
    components = [
        {
            "/": aasa_component_path(route),
            "comment": f"LegalSynq deep link route {route}"
        }
        for route in routes
    ]
    return {
        "applinks": {
            "details": [
                {
                    "appIDs": [app_id],
                    "components": components
                }
            ]
        }
    }


def build_assetlinks(env_config: dict):
    return [
        {
            "relation": ["delegate_permission/common.handle_all_urls"],
            "target": {
                "namespace": "android_app",
                "package_name": env_config["androidPackage"],
                "sha256_cert_fingerprints": env_config["androidSha256Fingerprints"]
            }
        }
    ]


def write_json(path: Path, payload):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as handle:
        json.dump(payload, handle, indent=2)
        handle.write("\n")


def environment_output_directory(output_root: Path, environment_name: str) -> Path:
    output_root = output_root.resolve()
    env_output = (output_root / environment_name).resolve()
    if env_output != output_root and output_root not in env_output.parents:
        raise ConfigError(f"{environment_name}: output path escapes configured output root")
    return env_output


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate Apple and Android deep-link association files.")
    parser.add_argument("--config", default="config/deep-links/association-config.json", help="Approved association config JSON.")
    parser.add_argument("--routes", default="shared/contracts/deep-links/routes.json", help="Read-only deep-link route registry JSON.")
    parser.add_argument("--output", default="apps/gateway/Gateway.Api/DeepLinks/Associations", help="Output root for environment association files.")
    args = parser.parse_args()

    try:
        config = load_json(Path(args.config))
        routes = load_routes(Path(args.routes))
        environments = config.get("environments")
        if not isinstance(environments, dict) or not environments:
            raise ConfigError("config must contain an environments object")

        for name, env_config in environments.items():
            if not isinstance(env_config, dict):
                raise ConfigError(f"{name}: environment config must be an object")
            validate_environment(name, env_config)
            env_output = environment_output_directory(Path(args.output), name)
            write_json(env_output / "apple-app-site-association", build_aasa(env_config, routes))
            write_json(env_output / "assetlinks.json", build_assetlinks(env_config))
            print(f"generated {env_output}")
    except ConfigError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
