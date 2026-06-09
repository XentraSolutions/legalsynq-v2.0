#!/usr/bin/env bash
set -euo pipefail

SYSTEMD_DIR="${SYSTEMD_DIR:-/etc/systemd/system}"
PUBLISH_ROOT="${PUBLISH_ROOT:-/opt/legalsynq/publish}"
ENV_DIR="${ENV_DIR:-/etc/legalsynq}"
SUDO="${SUDO-sudo}"

run_cmd() {
  if [[ -n "${SUDO}" ]]; then
    "${SUDO}" "$@"
  else
    "$@"
  fi
}

write_unit() {
  local unit_name="$1"

  if [[ -n "${SUDO}" ]]; then
    "${SUDO}" tee "${SYSTEMD_DIR}/${unit_name}" >/dev/null
  else
    tee "${SYSTEMD_DIR}/${unit_name}" >/dev/null
  fi
}

run_cmd mkdir -p "${SYSTEMD_DIR}"

create_legalsynq_service() {
  local name="$1"
  local description="$2"
  local publish_dir="$3"
  local dll="$4"
  local port="$5"
  local env_file="$6"
  local after_units="${7:-network-online.target}"
  local wants_units="${8:-network-online.target}"

  write_unit "legalsynq-${name}.service" <<EOF
[Unit]
Description=${description}
After=${after_units}
Wants=${wants_units}

[Service]
Type=simple
User=legalsynq
Group=legalsynq
WorkingDirectory=${PUBLISH_ROOT}/${publish_dir}
EnvironmentFile=${ENV_DIR}/shared.env
EnvironmentFile=${ENV_DIR}/${env_file}
Environment=ASPNETCORE_URLS=http://127.0.0.1:${port}
ExecStart=/usr/bin/dotnet ${PUBLISH_ROOT}/${publish_dir}/${dll}
Restart=always
RestartSec=5
KillSignal=SIGINT
TimeoutStopSec=30
SyslogIdentifier=legalsynq-${name}
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=full
ProtectHome=true
ReadWritePaths=/var/log/legalsynq /tmp

[Install]
WantedBy=multi-user.target
EOF
}

create_legalsynq_service gateway \
  "LegalSynq Gateway API" \
  gateway Gateway.Api.dll 5010 gateway.env \
  "network-online.target legalsynq-identity.service legalsynq-tenant.service" \
  "network-online.target"

create_legalsynq_service identity \
  "LegalSynq Identity API" \
  identity Identity.Api.dll 5001 identity.env \
  "network-online.target legalsynq-notifications.service legalsynq-audit.service" \
  "network-online.target"

create_legalsynq_service tenant \
  "LegalSynq Tenant API" \
  tenant Tenant.Api.dll 5005 tenant.env \
  "network-online.target legalsynq-identity.service" \
  "network-online.target"

create_legalsynq_service careconnect \
  "LegalSynq CareConnect API" \
  careconnect CareConnect.Api.dll 5003 careconnect.env \
  "network-online.target legalsynq-identity.service legalsynq-tenant.service legalsynq-documents.service legalsynq-notifications.service legalsynq-audit.service legalsynq-flow.service" \
  "network-online.target"

create_legalsynq_service fund \
  "LegalSynq Fund API" \
  fund Fund.Api.dll 5002 fund.env \
  "network-online.target legalsynq-flow.service legalsynq-audit.service" \
  "network-online.target"

create_legalsynq_service liens \
  "LegalSynq Liens API" \
  liens Liens.Api.dll 5009 liens.env \
  "network-online.target legalsynq-identity.service legalsynq-documents.service legalsynq-notifications.service legalsynq-audit.service legalsynq-flow.service legalsynq-task.service" \
  "network-online.target"

create_legalsynq_service documents \
  "LegalSynq Documents API" \
  documents Documents.Api.dll 5006 documents.env \
  "network-online.target" \
  "network-online.target"

create_legalsynq_service audit \
  "LegalSynq Audit Event API" \
  audit PlatformAuditEventService.dll 5007 audit.env \
  "network-online.target" \
  "network-online.target"

create_legalsynq_service notifications \
  "LegalSynq Notifications API" \
  notifications Notifications.Api.dll 5008 notifications.env \
  "network-online.target legalsynq-audit.service" \
  "network-online.target"

create_legalsynq_service flow \
  "LegalSynq Flow API" \
  flow Flow.Api.dll 5012 flow.env \
  "network-online.target legalsynq-audit.service legalsynq-notifications.service legalsynq-task.service" \
  "network-online.target"

create_legalsynq_service task \
  "LegalSynq Task API" \
  task Task.Api.dll 5016 task.env \
  "network-online.target legalsynq-notifications.service legalsynq-monitoring.service legalsynq-audit.service" \
  "network-online.target"

create_legalsynq_service monitoring \
  "LegalSynq Monitoring API" \
  monitoring Monitoring.Api.dll 5015 monitoring.env \
  "network-online.target" \
  "network-online.target"

create_legalsynq_service reports \
  "LegalSynq Reports API" \
  reports Reports.Api.dll 5029 reports.env \
  "network-online.target legalsynq-audit.service legalsynq-documents.service legalsynq-notifications.service" \
  "network-online.target"

create_legalsynq_service support \
  "LegalSynq Support API" \
  support Support.Api.dll 5017 support.env \
  "network-online.target legalsynq-identity.service legalsynq-tenant.service legalsynq-notifications.service legalsynq-audit.service legalsynq-documents.service" \
  "network-online.target"

create_legalsynq_service commerce \
  "LegalSynq Commerce API" \
  commerce Commerce.Api.dll 5030 commerce.env \
  "network-online.target legalsynq-tenant.service legalsynq-audit.service" \
  "network-online.target"

create_legalsynq_service billing \
  "LegalSynq Billing API" \
  billing Billing.Api.dll 5031 billing.env \
  "network-online.target legalsynq-commerce.service legalsynq-tenant.service legalsynq-audit.service" \
  "network-online.target"

create_legalsynq_service comms \
  "LegalSynq Comms API" \
  comms Comms.Api.dll 5011 comms.env \
  "network-online.target legalsynq-documents.service legalsynq-notifications.service legalsynq-audit.service" \
  "network-online.target"

cat <<EOF
Created Step 7 systemd service units in ${SYSTEMD_DIR}.

Next commands:
  sudo systemctl daemon-reload
  sudo systemctl enable legalsynq-gateway legalsynq-identity legalsynq-tenant legalsynq-careconnect legalsynq-fund legalsynq-liens legalsynq-documents legalsynq-audit legalsynq-notifications legalsynq-flow legalsynq-task legalsynq-monitoring legalsynq-reports legalsynq-support legalsynq-commerce legalsynq-billing legalsynq-comms
EOF
