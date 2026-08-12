#!/usr/bin/env bash

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
SCRIPT="$REPO_ROOT/scripts/bootstrap-worktree-local-config.sh"
TEST_ROOT="$(mktemp -d)"
trap 'rm -rf "$TEST_ROOT"' EXIT

initialize_repo() {
  local repo="$1"
  mkdir -p "$repo/config"
  git -C "$repo" init -q
  git -C "$repo" config user.email test@example.com
  git -C "$repo" config user.name "Bootstrap Test"
  printf 'config/.env.local\n' > "$repo/.gitignore"
  printf '{"value":"tracked-default"}\n' > "$repo/config/appsettings.Development.json"
  git -C "$repo" add .gitignore config/appsettings.Development.json
  git -C "$repo" commit -qm initial
}

SOURCE="$TEST_ROOT/source"
DESTINATION="$TEST_ROOT/destination"
initialize_repo "$SOURCE"
initialize_repo "$DESTINATION"

printf 'IGNORED_SECRET=source\n' > "$SOURCE/config/.env.local"
printf '{"value":"source-local"}\n' > "$SOURCE/config/appsettings.Development.json"
git -C "$SOURCE" update-index --skip-worktree -- config/appsettings.Development.json

(
  cd "$DESTINATION"
  bash "$SCRIPT" --source "$SOURCE" --include-skip-worktree
)

grep -q 'IGNORED_SECRET=source' "$DESTINATION/config/.env.local"
grep -q 'source-local' "$DESTINATION/config/appsettings.Development.json"
git -C "$DESTINATION" ls-files -v config/appsettings.Development.json | grep -q '^S '

git -C "$DESTINATION" update-index --no-skip-worktree -- config/appsettings.Development.json
printf '{"value":"destination-local"}\n' > "$DESTINATION/config/appsettings.Development.json"
(
  cd "$DESTINATION"
  output="$(bash "$SCRIPT" --source "$SOURCE" --include-skip-worktree)"
  [[ "$output" == *"destination has local changes"* ]]
)
grep -q 'destination-local' "$DESTINATION/config/appsettings.Development.json"

echo "bootstrap-worktree-local-config tests passed"
