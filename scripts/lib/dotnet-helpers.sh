#!/usr/bin/env bash
# Shared .NET startup helpers.
# Sourced by scripts/run-prod.sh at runtime and by tests in scripts/tests/.

# launch_svc <name> <path/to/Service.csproj> [cmd-prefix...]
#
# Verifies the Release DLL exists, then starts the service in the background
# using the supplied command prefix (e.g. env ASPNETCORE_URLS=...) followed
# by `dotnet run --no-build --no-launch-profile --configuration Release`.
# Exits with code 1 and an informative message when the binary is missing.
launch_svc() {
  local name="$1" project="$2"
  shift 2
  local dll_dir dll_name
  dll_dir="$(dirname "$project")/bin/Release/net8.0"
  dll_name="$(basename "$project" .csproj).dll"
  if [ ! -f "$dll_dir/$dll_name" ]; then
    echo "[dotnet] ERROR: $name binary not found at $dll_dir/$dll_name — aborting"
    exit 1
  fi
  (cd "$(dirname "$project")" && "$@" dotnet run --no-build --no-launch-profile --configuration Release) &
  echo "[dotnet] $name launched (pid $!)"
}
