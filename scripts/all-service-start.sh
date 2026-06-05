#!/usr/bin/env bash
set -euo pipefail

sudo systemctl start \
  legalsynq-audit \
  legalsynq-notifications \
  legalsynq-monitoring \
  legalsynq-identity \
  legalsynq-tenant \
  legalsynq-documents \
  legalsynq-task \
  legalsynq-flow \
  legalsynq-fund \
  legalsynq-careconnect \
  legalsynq-liens \
  legalsynq-reports \
  legalsynq-support \
  legalsynq-commerce \
  legalsynq-billing \
  legalsynq-comms \
  legalsynq-gateway \
  legalsynq-web