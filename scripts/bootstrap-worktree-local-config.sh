#!/usr/bin/env bash
# Copy ignored local configuration from an existing LegalSynq checkout into
# the current worktree without printing secret values.

set -euo pipefail

usage() {
  cat <<'EOF'
Usage: bash scripts/bootstrap-worktree-local-config.sh [options]

Options:
  --source PATH  Existing LegalSynq checkout containing local config.
  --include-skip-worktree
                 Also copy tracked config files marked skip-worktree in the source.
  --force        Overwrite local config files that already exist.
  --dry-run      Show the files that would be copied without copying them.
  -h, --help     Show this help.

The source may also be set with LEGALSYNQ_CONFIG_SOURCE. Only files named .env,
.env.*, or appsettings.*.json are copied. Generated directories are excluded.
Tracked configuration is included only with --include-skip-worktree.
EOF
}

SOURCE_ROOT="${LEGALSYNQ_CONFIG_SOURCE:-}"
FORCE=false
DRY_RUN=false
INCLUDE_SKIP_WORKTREE=false

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
    --include-skip-worktree)
      INCLUDE_SKIP_WORKTREE=true
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
config_file_kinds=()
while IFS= read -r -d '' source_file; do
  relative_path="${source_file#"$SOURCE_ROOT"/}"
  if git -C "$SOURCE_ROOT" check-ignore -q -- "$relative_path"; then
    config_files+=("$relative_path")
    config_file_kinds+=("ignored")
  fi
done < <(
  find "$SOURCE_ROOT" \
    \( -path '*/.git' -o -path '*/node_modules' -o -path '*/bin' -o -path '*/obj' \
       -o -path '*/.next' -o -path '*/dist' -o -path '*/publish' -o -path '*/artifacts' \) -prune \
    -o -type f \( -name '.env' -o -name '.env.*' -o -name 'appsettings.*.json' \) -print0
)

if [[ "$INCLUDE_SKIP_WORKTREE" == true ]]; then
  while IFS= read -r -d '' index_entry; do
    [[ "${index_entry:0:1}" == "S" ]] || continue
    relative_path="${index_entry:2}"
    file_name="${relative_path##*/}"
    case "$file_name" in
      .env|.env.*|appsettings.*.json)
        [[ -f "$SOURCE_ROOT/$relative_path" ]] || continue
        config_files+=("$relative_path")
        config_file_kinds+=("skip-worktree")
        ;;
    esac
  done < <(git -C "$SOURCE_ROOT" ls-files -v -z)
fi

if [[ ${#config_files[@]} -eq 0 ]]; then
  echo "No eligible local configuration files found in $SOURCE_ROOT"
  exit 0
fi

copied=0
skipped=0
for index in "${!config_files[@]}"; do
  relative_path="${config_files[$index]}"
  config_file_kind="${config_file_kinds[$index]}"
  source_file="$SOURCE_ROOT/$relative_path"
  destination_file="$DEST_ROOT/$relative_path"

  if [[ -e "$destination_file" && "$FORCE" != true ]]; then
    if [[ "$config_file_kind" != "skip-worktree" ]]; then
      echo "skip  $relative_path (already exists)"
      skipped=$((skipped + 1))
      continue
    fi

    destination_index_hash="$(git -C "$DEST_ROOT" rev-parse --verify ":$relative_path" 2>/dev/null || true)"
    destination_file_hash="$(git -C "$DEST_ROOT" hash-object -- "$relative_path" 2>/dev/null || true)"
    if [[ -z "$destination_index_hash" || "$destination_file_hash" != "$destination_index_hash" ]]; then
      echo "skip  $relative_path (destination has local changes; use --force to overwrite)"
      skipped=$((skipped + 1))
      continue
    fi
  fi

  if [[ "$DRY_RUN" == true ]]; then
    echo "copy  $relative_path (dry run)"
  else
    mkdir -p "$(dirname "$destination_file")"
    install -m 600 "$source_file" "$destination_file"
    if [[ "$config_file_kind" == "skip-worktree" ]]; then
      git -C "$DEST_ROOT" update-index --skip-worktree -- "$relative_path"
    fi
    echo "copy  $relative_path"
  fi
  copied=$((copied + 1))
done

echo "Local config bootstrap complete: $copied copied, $skipped skipped."
