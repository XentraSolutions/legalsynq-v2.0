#!/usr/bin/env bash
# Copy ignored local configuration from an existing LegalSynq checkout into
# the current worktree without printing secret values.

set -euo pipefail

usage() {
  cat <<'EOF'
Usage: bash scripts/bootstrap-worktree-local-config.sh [options]

Options:
  --source PATH  Existing LegalSynq checkout containing local config.
  --force        Overwrite local config files that already exist.
  --dry-run      Show the files that would be copied without copying them.
  -h, --help     Show this help.

The source may also be set with LEGALSYNQ_CONFIG_SOURCE. Only ignored files
named .env, .env.*, or appsettings.*.json are copied. Generated directories
and tracked configuration files are excluded.
EOF
}

SOURCE_ROOT="${LEGALSYNQ_CONFIG_SOURCE:-}"
FORCE=false
DRY_RUN=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --source)
      [[ $# -ge 2 ]] || { echo "error: --source requires a path" >&2; exit 2; }
      SOURCE_ROOT="$2"
      shift 2
      ;;
    --force)
      FORCE=true
      shift
      ;;
    --dry-run)
      DRY_RUN=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "error: unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

[[ -n "$SOURCE_ROOT" ]] || {
  echo "error: set LEGALSYNQ_CONFIG_SOURCE or pass --source PATH" >&2
  exit 2
}

SOURCE_ROOT="$(cd "$SOURCE_ROOT" && pwd -P)"
DEST_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || true)"

[[ -n "$DEST_ROOT" ]] || { echo "error: run this script inside a Git worktree" >&2; exit 2; }
[[ -d "$SOURCE_ROOT/.git" || -f "$SOURCE_ROOT/.git" ]] || {
  echo "error: source is not a Git checkout: $SOURCE_ROOT" >&2
  exit 2
}
[[ "$SOURCE_ROOT" != "$DEST_ROOT" ]] || {
  echo "error: source and destination are the same checkout" >&2
  exit 2
}

config_files=()
while IFS= read -r -d '' source_file; do
  relative_path="${source_file#"$SOURCE_ROOT"/}"
  if git -C "$SOURCE_ROOT" check-ignore -q -- "$relative_path"; then
    config_files+=("$relative_path")
  fi
done < <(
  find "$SOURCE_ROOT" \
    \( -path '*/.git' -o -path '*/node_modules' -o -path '*/bin' -o -path '*/obj' \
       -o -path '*/.next' -o -path '*/dist' -o -path '*/publish' -o -path '*/artifacts' \) -prune \
    -o -type f \( -name '.env' -o -name '.env.*' -o -name 'appsettings.*.json' \) -print0
)

if [[ ${#config_files[@]} -eq 0 ]]; then
  echo "No ignored local configuration files found in $SOURCE_ROOT"
  exit 0
fi

copied=0
skipped=0
for relative_path in "${config_files[@]}"; do
  source_file="$SOURCE_ROOT/$relative_path"
  destination_file="$DEST_ROOT/$relative_path"

  if [[ -e "$destination_file" && "$FORCE" != true ]]; then
    echo "skip  $relative_path (already exists)"
    skipped=$((skipped + 1))
    continue
  fi

  if [[ "$DRY_RUN" == true ]]; then
    echo "copy  $relative_path (dry run)"
  else
    mkdir -p "$(dirname "$destination_file")"
    install -m 600 "$source_file" "$destination_file"
    echo "copy  $relative_path"
  fi
  copied=$((copied + 1))
done

echo "Local config bootstrap complete: $copied copied, $skipped skipped."
