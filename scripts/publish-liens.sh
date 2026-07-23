#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
PUBLISH_ROOT="${PUBLISH_ROOT:-$ROOT/publish}"
NO_RESTORE="${NO_RESTORE:-0}"
VERBOSITY="${VERBOSITY:-minimal}"

usage() {
  cat <<USAGE
Usage:
  scripts/publish-local.sh [service ...]

Examples:
  scripts/publish-local.sh
  scripts/publish-local.sh identity tenant gateway
  PUBLISH_ROOT=/tmp/legalsynq-publish scripts/publish-local.sh identity
  NO_RESTORE=1 scripts/publish-local.sh

Environment:
  CONFIGURATION   Build configuration. Default: Release
  PUBLISH_ROOT    Local publish output root. Default: <repo>/publish
  NO_RESTORE      Set to 1 to pass --no-restore. Default: 0
  VERBOSITY       dotnet publish verbosity. Default: minimal
USAGE
}

SERVICE_PROJECTS=(
  "liens|apps/services/liens/Liens.Api/Liens.Api.csproj"
)

SERVICES=(
  liens
)

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

if [[ "$#" -gt 0 ]]; then
  SERVICES=("$@")
fi

project_for_service() {
  local service="$1"
  local entry name path

  for entry in "${SERVICE_PROJECTS[@]}"; do
    name="${entry%%|*}"
    path="${entry#*|}"
    if [[ "$name" == "$service" ]]; then
      echo "$path"
      return 0
    fi
  done

  return 1
}

known_services() {
  local entry name

  for entry in "${SERVICE_PROJECTS[@]}"; do
    name="${entry%%|*}"
    printf "%s " "$name"
  done
}

publish_args=(-c "$CONFIGURATION" --verbosity "$VERBOSITY")
if [[ "$NO_RESTORE" == "1" ]]; then
  publish_args+=(--no-restore)
fi

echo "Publishing LegalSynq backend services"
echo "  configuration: $CONFIGURATION"
echo "  output root:   $PUBLISH_ROOT"
echo

mkdir -p "$PUBLISH_ROOT"

for service in "${SERVICES[@]}"; do
  if ! project="$(project_for_service "$service")"; then
    echo "ERROR: unknown service '$service'" >&2
    echo "Known services: $(known_services)" >&2
    exit 1
  fi

  project_path="$ROOT/$project"
  output_dir="$PUBLISH_ROOT/$service"

  if [[ ! -f "$project_path" ]]; then
    echo "ERROR: project file not found for $service: $project_path" >&2
    exit 1
  fi

  echo "==> Publishing $service"
  dotnet publish "$project_path" "${publish_args[@]}" -o "$output_dir"
  echo "    $output_dir"
done

echo
echo "Publish complete."
