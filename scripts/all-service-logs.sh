#!/usr/bin/env bash
set -euo pipefail

LINES="${1:-200}"

units=(
  legalsynq-audit
  legalsynq-notifications
  legalsynq-monitoring
  legalsynq-identity
  legalsynq-tenant
  legalsynq-documents
  legalsynq-task
  legalsynq-flow
  legalsynq-fund
  legalsynq-careconnect
  legalsynq-liens
  legalsynq-reports
  legalsynq-support
  legalsynq-commerce
  legalsynq-billing
  legalsynq-comms
  legalsynq-gateway
)

args=()
for u in "${units[@]}"; do
  args+=(-u "$u")
done

sudo journalctl -n "$LINES" -f --no-pager "${args[@]}"