#!/usr/bin/env python3
"""
Detect repo changes that usually require README.md, AGENTS.md, or scoped docs updates.

Codex hook mode:
  - SessionStart records the dirty worktree baseline for this session.
  - Stop compares the current dirty worktree against that baseline and asks Codex
    to continue if new doc-sensitive changes were made without doc updates.

Manual mode:
  - Run from the repo root with `python3 scripts/check-doc-sync.py`.
  - It checks all current worktree changes against HEAD.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import subprocess
import sys
from pathlib import Path
from typing import Any


DOC_FILENAMES = {"README.md", "AGENTS.md"}

DOC_SENSITIVE_EXACT = {
    "LegalSynq.sln",
    "package.json",
    "pnpm-lock.yaml",
    "package-lock.json",
    "scripts/run-dev.sh",
    "scripts/stop-dev.sh",
    "scripts/dev-proxy.js",
    "apps/gateway/Gateway.Api/appsettings.json",
}

DOC_SENSITIVE_SUFFIXES = (
    ".csproj",
    ".sln",
    "Directory.Build.props",
    "Directory.Packages.props",
    "global.json",
    "appsettings.json",
    "appsettings.Development.json",
    "launchSettings.json",
)

DOC_SENSITIVE_PREFIXES = (
    ".codex/",
    ".agents/",
    "scripts/tests/",
    "shared/building-blocks/BuildingBlocks/Authorization/",
)


def run_git(repo_root: Path, *args: str) -> str:
    result = subprocess.run(
        ["git", *args],
        cwd=repo_root,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    return result.stdout


def find_repo_root(cwd: str | None) -> Path | None:
    try:
        start = cwd or os.getcwd()
        output = subprocess.run(
            ["git", "rev-parse", "--show-toplevel"],
            cwd=start,
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        ).stdout.strip()
        return Path(output)
    except (OSError, subprocess.CalledProcessError):
        return None


def status_paths(repo_root: Path) -> set[str]:
    output = run_git(repo_root, "status", "--porcelain=v1", "-z")
    entries = output.split("\0")
    paths: set[str] = set()

    i = 0
    while i < len(entries):
        entry = entries[i]
        i += 1
        if not entry:
            continue

        status = entry[:2]
        payload = entry[3:]
        if status.startswith("R") or status.startswith("C"):
            old_path = payload
            if i < len(entries) and entries[i]:
                paths.add(entries[i])
                paths.add(old_path)
                i += 1
            continue

        paths.add(payload)

    return paths


def file_fingerprint(repo_root: Path, path: str) -> str:
    full_path = repo_root / path
    if not full_path.exists():
        return "missing"
    if full_path.is_dir():
        return "dir"

    digest = hashlib.sha256()
    try:
        with full_path.open("rb") as handle:
            for chunk in iter(lambda: handle.read(65536), b""):
                digest.update(chunk)
    except OSError:
        return "unreadable"
    return digest.hexdigest()


def snapshot(repo_root: Path) -> dict[str, str]:
    return {path: file_fingerprint(repo_root, path) for path in sorted(status_paths(repo_root))}


def is_documentation(path: str) -> bool:
    name = Path(path).name
    if name in DOC_FILENAMES:
        return True
    if path.startswith("docs/") and path.endswith(".md"):
        return True
    if "/docs/" in path and path.endswith(".md"):
        return True
    return False


def is_doc_sensitive(path: str) -> bool:
    if is_documentation(path):
        return False
    if path in DOC_SENSITIVE_EXACT:
        return True
    if path.startswith(DOC_SENSITIVE_PREFIXES):
        return True
    if path.endswith(DOC_SENSITIVE_SUFFIXES):
        return True
    if path.startswith("apps/services/") and (
        path.endswith("/Program.cs") or "/Endpoints/" in path or "/Migrations/" in path
    ):
        return True
    if path.startswith("apps/gateway/") and (path.endswith(".json") or path.endswith("/Program.cs")):
        return True
    if path.startswith("apps/web/src/app/") or path.startswith("apps/control-center/src/app/"):
        return True
    return False


def changed_since_baseline(repo_root: Path, baseline: dict[str, str] | None) -> set[str]:
    current = snapshot(repo_root)
    if baseline is None:
        return set(current)
    return {path for path, fingerprint in current.items() if baseline.get(path) != fingerprint}


def state_path(repo_root: Path, session_id: str | None) -> Path:
    safe_session = "".join(ch if ch.isalnum() or ch in "._-" else "_" for ch in (session_id or "manual"))
    return repo_root / ".local" / "state" / "codex-doc-sync" / f"{safe_session}.json"


def load_hook_input() -> dict[str, Any]:
    try:
        raw = sys.stdin.read()
        if not raw.strip():
            return {}
        return json.loads(raw)
    except json.JSONDecodeError:
        return {}


def write_hook_json(payload: dict[str, Any]) -> None:
    print(json.dumps(payload, separators=(",", ":")))


def record_baseline(repo_root: Path, hook_input: dict[str, Any]) -> int:
    path = state_path(repo_root, hook_input.get("session_id"))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(snapshot(repo_root), indent=2, sort_keys=True), encoding="utf-8")
    return 0


def read_baseline(repo_root: Path, hook_input: dict[str, Any]) -> dict[str, str] | None:
    path = state_path(repo_root, hook_input.get("session_id"))
    if not path.exists():
        return None
    try:
        loaded = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    if not isinstance(loaded, dict):
        return None
    return {str(key): str(value) for key, value in loaded.items()}


def check_paths(paths: set[str]) -> tuple[list[str], list[str]]:
    docs = sorted(path for path in paths if is_documentation(path))
    sensitive = sorted(path for path in paths if is_doc_sensitive(path))
    return docs, sensitive


def hook_main() -> int:
    hook_input = load_hook_input()
    repo_root = find_repo_root(hook_input.get("cwd"))
    if repo_root is None:
        return 0

    event_name = hook_input.get("hook_event_name")
    if event_name == "SessionStart":
        return record_baseline(repo_root, hook_input)

    if event_name != "Stop":
        return 0

    baseline = read_baseline(repo_root, hook_input)
    if baseline is None:
        return 0

    changed = changed_since_baseline(repo_root, baseline)
    docs, sensitive = check_paths(changed)
    if not sensitive or docs:
        return 0

    sample = ", ".join(sensitive[:8])
    if len(sensitive) > 8:
        sample += f", and {len(sensitive) - 8} more"

    reason = (
        "Documentation sync required before final response. "
        f"Doc-sensitive files changed without README.md/AGENTS.md/scoped docs changes: {sample}. "
        "Review whether root README.md, AGENTS.md, relevant service README.md, scoped AGENTS.md, "
        "startup docs, port maps, product/auth docs, or validation commands need updates. "
        "If no docs are needed, state that explicitly in the final response."
    )
    write_hook_json({"decision": "block", "reason": reason})
    return 0


def manual_main() -> int:
    repo_root = find_repo_root(os.getcwd())
    if repo_root is None:
        print("Not inside a git repository.", file=sys.stderr)
        return 2

    changed = set(run_git(repo_root, "diff", "--name-only", "HEAD").splitlines())
    docs, sensitive = check_paths(changed)
    if not sensitive:
        print("Documentation sync check passed: no doc-sensitive changes detected.")
        return 0
    if docs:
        print("Documentation sync check passed: doc-sensitive changes include documentation updates.")
        print("Docs touched:")
        for path in docs:
            print(f"  - {path}")
        return 0

    print("Documentation sync check failed: doc-sensitive changes need documentation review.")
    print("Doc-sensitive files:")
    for path in sensitive:
        print(f"  - {path}")
    print("Review README.md, AGENTS.md, relevant service README.md, or scoped AGENTS.md.")
    return 1


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--codex-hook", action="store_true", help="Run using Codex hook stdin/stdout protocol.")
    args = parser.parse_args()

    if args.codex_hook:
        return hook_main()
    return manual_main()


if __name__ == "__main__":
    raise SystemExit(main())
