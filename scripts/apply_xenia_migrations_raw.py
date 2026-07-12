#!/usr/bin/env python3
"""
apply_xenia_migrations_raw.py

Applies all 8 Xenia EF migrations as raw MySQL DDL, bypassing
dotnet-ef entirely. This avoids the Pomelo 8.0.2 NullReferenceException
in FindCollectionMapping which fires for any string-CLR-type property
when GenerateUpSql calls FinalizeModel on the live DbContext model.

Usage:
  python3 scripts/apply_xenia_migrations_raw.py

Requires: pymysql  (pip install pymysql)
"""

import sys

try:
    import pymysql
except ImportError:
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "pymysql", "-q"])
    import pymysql

CONN_PARAMS = dict(
    host="127.0.0.1",
    port=13308,
    user="root",
    password="xeniatest123",
    database="xeniadb",
    charset="utf8mb4",
)

# ---------------------------------------------------------------------------
# EF product version recorded in __EFMigrationsHistory
# ---------------------------------------------------------------------------
EF_VERSION = "8.0.10"

# ---------------------------------------------------------------------------
# Migration SQL — derived directly from the C# migration Up() methods.
# Column type mapping:
#   Column<string>(type:"char(36)")  → CHAR(36)
#   Column<string>(maxLength:N)      → VARCHAR(N)
#   Column<string>(type:"mediumtext")→ MEDIUMTEXT
#   Column<string>(type:"text")      → TEXT
#   Column<string>(type:"longtext")  → LONGTEXT
#   Column<bool>                     → TINYINT(1)
#   Column<int>                      → INT
#   Column<long>                     → BIGINT
#   Column<uint>                     → INT UNSIGNED
#   Column<DateTime>(type:"datetime(6)") → DATETIME(6)
# nullable:false → NOT NULL, nullable:true → NULL
# defaultValue   → DEFAULT ...
# ---------------------------------------------------------------------------

MIGRATIONS = [
    {
        "id": "20260710000001_XeniaInitial",
        "statements": [
            # xn_modules
            """CREATE TABLE `xn_modules` (
  `id` CHAR(36) NOT NULL,
  `module_key` VARCHAR(100) NOT NULL,
  `name` VARCHAR(200) NOT NULL,
  `version` VARCHAR(50) NOT NULL,
  `description` VARCHAR(1000) NULL,
  `global_enabled` TINYINT(1) NOT NULL,
  `status` INT NOT NULL,
  `configuration_namespace` VARCHAR(200) NOT NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `ix_xn_modules_module_key` ON `xn_modules` (`module_key`)",

            # xn_tenant_modules
            """CREATE TABLE `xn_tenant_modules` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `module_key` VARCHAR(100) NOT NULL,
  `enabled` TINYINT(1) NOT NULL,
  `module_configuration` VARCHAR(8000) NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE INDEX `ix_xn_tenant_modules_tenant_id` ON `xn_tenant_modules` (`tenant_id`)",
            "CREATE UNIQUE INDEX `ix_xn_tenant_modules_tenant_module` ON `xn_tenant_modules` (`tenant_id`, `module_key`)",

            # xn_platform_adapters
            """CREATE TABLE `xn_platform_adapters` (
  `id` CHAR(36) NOT NULL,
  `adapter_key` VARCHAR(100) NOT NULL,
  `adapter_type` INT NOT NULL,
  `name` VARCHAR(200) NOT NULL,
  `version` VARCHAR(50) NOT NULL,
  `configuration_status` INT NOT NULL,
  `availability_status` INT NOT NULL,
  `health_status` INT NOT NULL,
  `last_health_check_at` DATETIME(6) NULL,
  `diagnostic_message` VARCHAR(500) NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `ix_xn_platform_adapters_key` ON `xn_platform_adapters` (`adapter_key`)",

            # xn_configuration
            """CREATE TABLE `xn_configuration` (
  `id` CHAR(36) NOT NULL,
  `scope_type` INT NOT NULL,
  `scope_id` VARCHAR(300) NULL,
  `namespace` VARCHAR(200) NOT NULL,
  `configuration_key` VARCHAR(200) NOT NULL,
  `configuration_value` VARCHAR(4000) NULL,
  `value_type` VARCHAR(50) NULL,
  `is_secret` TINYINT(1) NOT NULL,
  `version` INT NOT NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `ix_xn_configuration_scope_key` ON `xn_configuration` (`scope_type`, `scope_id`, `namespace`, `configuration_key`)",

            # xn_tenant_settings
            """CREATE TABLE `xn_tenant_settings` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `enabled` TINYINT(1) NOT NULL,
  `settings` VARCHAR(8000) NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `ix_xn_tenant_settings_tenant_id` ON `xn_tenant_settings` (`tenant_id`)",
        ],
    },

    {
        "id": "20260710000002_AddAdapterCriticality",
        "statements": [
            # defaultValue: "Optional" in C# is the string label — but column is INT.
            # The int value for Optional is typically 1 (Disabled=0, Optional=1, Mandatory=2).
            # However the migration says defaultValue: "Optional" on an INT column which is odd.
            # EF Migration for AddColumn<int> with defaultValue:"Optional" would fail SQL-side.
            # Looking at the original intent: default should be a numeric value.
            # From AdapterCriticality enum: Optional is likely 1 (first non-zero).
            # Use DEFAULT 1 to match "Optional" semantics safely.
            "ALTER TABLE `xn_platform_adapters` ADD COLUMN `criticality` INT NOT NULL DEFAULT 1",
        ],
    },

    {
        "id": "20260710000003_AddEmailModule",
        "statements": [
            # xn_email_sources
            """CREATE TABLE `xn_email_sources` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `module_key` VARCHAR(100) NOT NULL,
  `display_name` VARCHAR(200) NOT NULL,
  `description` VARCHAR(1000) NULL,
  `provider_type` INT NOT NULL,
  `auth_type` INT NOT NULL,
  `email_address` VARCHAR(320) NOT NULL,
  `username` VARCHAR(255) NULL,
  `incoming_host` VARCHAR(255) NULL,
  `incoming_port` INT NULL,
  `use_tls` TINYINT(1) NOT NULL,
  `mailbox_folder` VARCHAR(255) NULL,
  `secret_reference_id` VARCHAR(500) NULL,
  `oauth_connection_ref` VARCHAR(500) NULL,
  `enabled` TINYINT(1) NOT NULL,
  `status` INT NOT NULL,
  `health_status` INT NOT NULL,
  `validation_status` INT NOT NULL,
  `last_validated_at` DATETIME(6) NULL,
  `last_successful_validation_at` DATETIME(6) NULL,
  `last_validation_latency_ms` INT NULL,
  `last_connection_at` DATETIME(6) NULL,
  `last_error_code` VARCHAR(100) NULL,
  `last_error_summary` VARCHAR(500) NULL,
  `created_by` CHAR(36) NULL,
  `updated_by` CHAR(36) NULL,
  `row_version` INT NOT NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE INDEX `ix_xn_email_sources_tenant_id` ON `xn_email_sources` (`tenant_id`)",
            "CREATE INDEX `ix_xn_email_sources_tenant_provider` ON `xn_email_sources` (`tenant_id`, `provider_type`)",
            "CREATE INDEX `ix_xn_email_sources_tenant_status` ON `xn_email_sources` (`tenant_id`, `status`)",

            # xn_email_provider_settings
            """CREATE TABLE `xn_email_provider_settings` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `email_source_id` CHAR(36) NOT NULL,
  `provider_type` INT NOT NULL,
  `configuration_json` VARCHAR(8000) NULL,
  `configuration_version` INT NOT NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `ix_xn_email_prov_settings_source` ON `xn_email_provider_settings` (`email_source_id`)",
            "CREATE INDEX `ix_xn_email_prov_settings_tenant` ON `xn_email_provider_settings` (`tenant_id`)",

            # xn_email_validation_history
            """CREATE TABLE `xn_email_validation_history` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `email_source_id` CHAR(36) NOT NULL,
  `provider_type` INT NOT NULL,
  `validation_type` VARCHAR(50) NOT NULL,
  `started_at` DATETIME(6) NOT NULL,
  `completed_at` DATETIME(6) NULL,
  `duration_ms` INT NULL,
  `result` INT NOT NULL,
  `error_code` VARCHAR(100) NULL,
  `error_summary` VARCHAR(500) NULL,
  `correlation_id` VARCHAR(200) NULL,
  `actor_id` CHAR(36) NULL,
  `created_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE INDEX `ix_xn_email_val_history_tenant` ON `xn_email_validation_history` (`tenant_id`)",
            "CREATE INDEX `ix_xn_email_val_history_source` ON `xn_email_validation_history` (`email_source_id`)",
            "CREATE INDEX `ix_xn_email_val_history_started` ON `xn_email_validation_history` (`started_at`)",
        ],
    },

    {
        "id": "20260710000004_AddSoftDeleteAndSettings",
        "statements": [
            "ALTER TABLE `xn_email_sources` ADD COLUMN `is_deleted` TINYINT(1) NOT NULL DEFAULT 0",
            "ALTER TABLE `xn_email_sources` ADD COLUMN `deleted_at` DATETIME(6) NULL",
            "ALTER TABLE `xn_email_sources` ADD COLUMN `deleted_by` CHAR(36) NULL",
            "CREATE INDEX `ix_xn_email_sources_not_deleted` ON `xn_email_sources` (`tenant_id`, `is_deleted`)",

            """CREATE TABLE `xn_email_settings` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `connection_timeout_seconds` INT NOT NULL DEFAULT 30,
  `allowed_provider_types` VARCHAR(500) NOT NULL DEFAULT 'M365,GoogleWorkspace,Imap,Pop3,ExchangeImap',
  `validation_retry_limit` INT NOT NULL DEFAULT 2,
  `validation_history_retention_days` INT NOT NULL DEFAULT 90,
  `allowed_ports` VARCHAR(200) NOT NULL DEFAULT '993,995,443',
  `require_tls` TINYINT(1) NOT NULL DEFAULT 1,
  `allow_custom_hosts` TINYINT(1) NOT NULL DEFAULT 0,
  `ssrf_policy_mode` VARCHAR(50) NOT NULL DEFAULT 'Strict',
  `default_source_enabled` TINYINT(1) NOT NULL DEFAULT 0,
  `version` INT NOT NULL DEFAULT 0,
  `updated_by` CHAR(36) NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `ix_xn_email_settings_tenant_id_unique` ON `xn_email_settings` (`tenant_id`)",
        ],
    },

    {
        "id": "20260710000005_AddIngestionEngine",
        "statements": [
            # xn_email_messages
            """CREATE TABLE `xn_email_messages` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `email_source_id` CHAR(36) NOT NULL,
  `provider_type` INT NOT NULL,
  `provider_message_id` VARCHAR(1024) NOT NULL,
  `internet_message_id` VARCHAR(998) NULL,
  `thread_id` VARCHAR(500) NULL,
  `conversation_id` VARCHAR(500) NULL,
  `subject` VARCHAR(998) NULL,
  `from_address` VARCHAR(320) NULL,
  `from_name` VARCHAR(500) NULL,
  `sender_address` VARCHAR(320) NULL,
  `sender_name` VARCHAR(500) NULL,
  `reply_to_addresses` TEXT NULL,
  `sent_at` DATETIME(6) NULL,
  `received_at` DATETIME(6) NULL,
  `importance` INT NOT NULL,
  `is_read` TINYINT(1) NULL,
  `has_attachments` TINYINT(1) NOT NULL DEFAULT 0,
  `attachment_count` INT NOT NULL DEFAULT 0,
  `body_type` INT NOT NULL,
  `body_text` MEDIUMTEXT NULL,
  `body_html` MEDIUMTEXT NULL,
  `body_preview` VARCHAR(500) NULL,
  `headers_json` TEXT NULL,
  `provider_metadata_json` TEXT NULL,
  `content_hash` VARCHAR(128) NULL,
  `import_status` INT NOT NULL,
  `processing_state` INT NOT NULL,
  `imported_at` DATETIME(6) NULL,
  `last_observed_at` DATETIME(6) NULL,
  `last_ingestion_run_id` CHAR(36) NULL,
  `version` INT NOT NULL DEFAULT 0,
  `created_at_utc` DATETIME(6) NOT NULL,
  `updated_at_utc` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            # provider_message_id VARCHAR(1024)*4bytes = 4096; total would exceed 3072-byte MySQL key limit.
            # Use a prefix of 600 chars on provider_message_id (600*4=2400, total ~2692 — safely under limit).
            "CREATE UNIQUE INDEX `ux_email_messages_provider_unique` ON `xn_email_messages` (`tenant_id`, `email_source_id`, `provider_type`, `provider_message_id`(600))",
            # internet_message_id VARCHAR(998)*4 = 3992 bytes; exceeds 3072-byte limit. Use prefix(700).
            "CREATE INDEX `ix_email_messages_internet_message_id` ON `xn_email_messages` (`tenant_id`, `internet_message_id`(700))",
            "CREATE INDEX `ix_email_messages_tenant` ON `xn_email_messages` (`tenant_id`)",
            "CREATE INDEX `ix_email_messages_source` ON `xn_email_messages` (`tenant_id`, `email_source_id`)",
            "CREATE INDEX `ix_email_messages_received_at` ON `xn_email_messages` (`tenant_id`, `received_at`)",
            "CREATE INDEX `ix_email_messages_import_status` ON `xn_email_messages` (`tenant_id`, `import_status`)",
            "CREATE INDEX `ix_email_messages_has_attachments` ON `xn_email_messages` (`tenant_id`, `has_attachments`)",

            # xn_email_recipients
            """CREATE TABLE `xn_email_recipients` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `email_message_id` CHAR(36) NOT NULL,
  `recipient_type` INT NOT NULL,
  `email_address` VARCHAR(320) NOT NULL,
  `display_name` VARCHAR(500) NULL,
  `created_at_utc` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE INDEX `ix_email_recipients_message` ON `xn_email_recipients` (`email_message_id`)",
            "CREATE INDEX `ix_email_recipients_address` ON `xn_email_recipients` (`tenant_id`, `email_address`)",

            # xn_email_attachment_references
            """CREATE TABLE `xn_email_attachment_references` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `email_message_id` CHAR(36) NOT NULL,
  `provider_attachment_id` VARCHAR(1024) NULL,
  `document_reference_id` CHAR(36) NULL,
  `file_name` VARCHAR(500) NOT NULL,
  `mime_type` VARCHAR(255) NULL,
  `size_bytes` BIGINT NULL,
  `content_hash` VARCHAR(128) NULL,
  `is_inline` TINYINT(1) NOT NULL DEFAULT 0,
  `content_id` VARCHAR(500) NULL,
  `disposition` VARCHAR(100) NULL,
  `dispatch_status` INT NOT NULL,
  `error_code` VARCHAR(100) NULL,
  `safe_error_summary` VARCHAR(500) NULL,
  `created_at_utc` DATETIME(6) NOT NULL,
  `updated_at_utc` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            # provider_attachment_id VARCHAR(1024)*4 = 4096 bytes; exceeds 3072-byte limit. Use prefix(600).
            "CREATE INDEX `ix_email_attachments_provider_id` ON `xn_email_attachment_references` (`tenant_id`, `email_message_id`, `provider_attachment_id`(600))",
            "CREATE INDEX `ix_email_attachments_message` ON `xn_email_attachment_references` (`email_message_id`)",
            "CREATE INDEX `ix_email_attachments_dispatch_status` ON `xn_email_attachment_references` (`tenant_id`, `dispatch_status`)",

            # xn_email_sync_state
            """CREATE TABLE `xn_email_sync_state` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `email_source_id` CHAR(36) NOT NULL,
  `provider_type` INT NOT NULL,
  `cursor_type` INT NOT NULL,
  `cursor_value` VARCHAR(4000) NULL,
  `cursor_metadata_json` VARCHAR(2000) NULL,
  `safe_cursor_summary` VARCHAR(200) NULL,
  `last_successful_sync_at` DATETIME(6) NULL,
  `last_attempted_sync_at` DATETIME(6) NULL,
  `last_processed_provider_timestamp` DATETIME(6) NULL,
  `last_processed_provider_message_id` VARCHAR(1024) NULL,
  `initial_sync_completed` TINYINT(1) NOT NULL DEFAULT 0,
  `consecutive_failure_count` INT NOT NULL DEFAULT 0,
  `next_eligible_sync_at` DATETIME(6) NULL,
  `last_error_code` VARCHAR(100) NULL,
  `safe_last_error_summary` VARCHAR(500) NULL,
  `state_version` INT NOT NULL DEFAULT 0,
  `created_at_utc` DATETIME(6) NOT NULL,
  `updated_at_utc` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `ux_email_sync_state_source_unique` ON `xn_email_sync_state` (`email_source_id`)",
            "CREATE INDEX `ix_email_sync_state_tenant` ON `xn_email_sync_state` (`tenant_id`)",
            "CREATE INDEX `ix_email_sync_state_next_eligible` ON `xn_email_sync_state` (`tenant_id`, `next_eligible_sync_at`)",
            "CREATE INDEX `ix_email_sync_state_last_success` ON `xn_email_sync_state` (`tenant_id`, `last_successful_sync_at`)",

            # xn_email_ingestion_runs
            """CREATE TABLE `xn_email_ingestion_runs` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `email_source_id` CHAR(36) NOT NULL,
  `trigger_type` INT NOT NULL,
  `status` INT NOT NULL,
  `started_at` DATETIME(6) NOT NULL,
  `completed_at` DATETIME(6) NULL,
  `duration_ms` BIGINT NULL,
  `correlation_id` VARCHAR(200) NULL,
  `actor_id` CHAR(36) NULL,
  `worker_instance_id` VARCHAR(200) NULL,
  `messages_discovered` INT NOT NULL DEFAULT 0,
  `messages_imported` INT NOT NULL DEFAULT 0,
  `messages_updated` INT NOT NULL DEFAULT 0,
  `messages_duplicated` INT NOT NULL DEFAULT 0,
  `messages_failed` INT NOT NULL DEFAULT 0,
  `attachments_discovered` INT NOT NULL DEFAULT 0,
  `attachments_dispatched` INT NOT NULL DEFAULT 0,
  `attachments_failed` INT NOT NULL DEFAULT 0,
  `pages_processed` INT NOT NULL DEFAULT 0,
  `retry_count` INT NOT NULL DEFAULT 0,
  `cursor_before_safe_summary` VARCHAR(200) NULL,
  `cursor_after_safe_summary` VARCHAR(200) NULL,
  `error_code` VARCHAR(100) NULL,
  `safe_error_summary` VARCHAR(500) NULL,
  `created_at_utc` DATETIME(6) NOT NULL,
  `updated_at_utc` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE INDEX `ix_ingestion_runs_tenant` ON `xn_email_ingestion_runs` (`tenant_id`)",
            "CREATE INDEX `ix_ingestion_runs_source` ON `xn_email_ingestion_runs` (`tenant_id`, `email_source_id`)",
            "CREATE INDEX `ix_ingestion_runs_status` ON `xn_email_ingestion_runs` (`tenant_id`, `status`)",
            "CREATE INDEX `ix_ingestion_runs_started_at` ON `xn_email_ingestion_runs` (`tenant_id`, `started_at`)",
        ],
    },

    {
        "id": "20260710000006_AddDurableSyncLock",
        "statements": [
            """CREATE TABLE `xn_email_source_sync_locks` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `email_source_id` CHAR(36) NOT NULL,
  `lease_owner_id` VARCHAR(200) NOT NULL,
  `acquired_at` DATETIME(6) NOT NULL,
  `renewed_at` DATETIME(6) NOT NULL,
  `expires_at` DATETIME(6) NOT NULL,
  `version` INT NOT NULL DEFAULT 1,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `ux_email_source_sync_locks_source` ON `xn_email_source_sync_locks` (`tenant_id`, `email_source_id`)",
            "CREATE INDEX `ix_email_source_sync_locks_expires_at` ON `xn_email_source_sync_locks` (`expires_at`)",
        ],
    },

    {
        "id": "20260710000007_AddOperationsDomain",
        "statements": [
            # xn_email_operational_alerts
            """CREATE TABLE `xn_email_operational_alerts` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `email_source_id` CHAR(36) NULL,
  `provider_type` INT NULL,
  `alert_type` INT NOT NULL,
  `severity` INT NOT NULL,
  `status` INT NOT NULL,
  `deduplication_key` VARCHAR(300) NOT NULL,
  `title` VARCHAR(200) NOT NULL,
  `safe_description` VARCHAR(1000) NOT NULL,
  `first_observed_at` DATETIME(6) NOT NULL,
  `last_observed_at` DATETIME(6) NOT NULL,
  `occurrence_count` INT NOT NULL,
  `acknowledged_at` DATETIME(6) NULL,
  `acknowledged_by` CHAR(36) NULL,
  `resolved_at` DATETIME(6) NULL,
  `resolved_by` CHAR(36) NULL,
  `resolution_reason` VARCHAR(500) NULL,
  `suppressed_until` DATETIME(6) NULL,
  `correlation_id` VARCHAR(200) NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  `version` INT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE INDEX `ix_op_alerts_tenant` ON `xn_email_operational_alerts` (`tenant_id`)",
            "CREATE INDEX `ix_op_alerts_source` ON `xn_email_operational_alerts` (`tenant_id`, `email_source_id`)",
            "CREATE INDEX `ix_op_alerts_type` ON `xn_email_operational_alerts` (`tenant_id`, `alert_type`)",
            "CREATE INDEX `ix_op_alerts_severity` ON `xn_email_operational_alerts` (`tenant_id`, `severity`)",
            "CREATE INDEX `ix_op_alerts_status` ON `xn_email_operational_alerts` (`tenant_id`, `status`)",
            "CREATE INDEX `ix_op_alerts_first_observed` ON `xn_email_operational_alerts` (`first_observed_at`)",
            "CREATE INDEX `ix_op_alerts_last_observed` ON `xn_email_operational_alerts` (`last_observed_at`)",
            "CREATE INDEX `ix_op_alerts_dedup_key` ON `xn_email_operational_alerts` (`tenant_id`, `deduplication_key`)",

            # xn_email_operational_settings
            """CREATE TABLE `xn_email_operational_settings` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `default_dashboard_range_days` INT NOT NULL DEFAULT 7,
  `source_failure_alert_threshold` INT NOT NULL DEFAULT 3,
  `stale_sync_threshold_minutes` INT NOT NULL DEFAULT 120,
  `lock_warning_threshold_minutes` INT NOT NULL DEFAULT 30,
  `maximum_retry_count` INT NOT NULL DEFAULT 5,
  `cancellation_timeout_seconds` INT NOT NULL DEFAULT 60,
  `metrics_enabled` TINYINT(1) NOT NULL DEFAULT 1,
  `notification_alerts_enabled` TINYINT(1) NOT NULL DEFAULT 0,
  `default_run_page_size` INT NOT NULL DEFAULT 50,
  `default_message_page_size` INT NOT NULL DEFAULT 50,
  `operational_polling_interval_seconds` INT NOT NULL DEFAULT 30,
  `message_metadata_retention_days` INT NOT NULL DEFAULT 365,
  `message_body_retention_days` INT NOT NULL DEFAULT 90,
  `validation_history_retention_days` INT NOT NULL DEFAULT 90,
  `ingestion_run_retention_days` INT NOT NULL DEFAULT 180,
  `alert_retention_days` INT NOT NULL DEFAULT 90,
  `attachment_reference_retention_days` INT NOT NULL DEFAULT 365,
  `purge_batch_size` INT NOT NULL DEFAULT 500,
  `retention_dry_run_default` TINYINT(1) NOT NULL DEFAULT 1,
  `legal_hold_enabled` TINYINT(1) NOT NULL DEFAULT 0,
  `retention_enabled` TINYINT(1) NOT NULL DEFAULT 0,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  `updated_by` VARCHAR(200) NULL,
  `version` INT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `ux_op_settings_tenant` ON `xn_email_operational_settings` (`tenant_id`)",

            # xn_email_retention_runs
            """CREATE TABLE `xn_email_retention_runs` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `mode` INT NOT NULL,
  `status` INT NOT NULL,
  `started_at` DATETIME(6) NOT NULL,
  `completed_at` DATETIME(6) NULL,
  `messages_eligible` INT NOT NULL,
  `messages_deleted` INT NOT NULL,
  `bodies_cleared` INT NOT NULL,
  `runs_deleted` INT NOT NULL,
  `alerts_deleted` INT NOT NULL,
  `attachment_references_deleted` INT NOT NULL,
  `failures` INT NOT NULL,
  `safe_error_summary` VARCHAR(500) NULL,
  `correlation_id` VARCHAR(200) NULL,
  `actor_id` CHAR(36) NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE INDEX `ix_retention_runs_tenant` ON `xn_email_retention_runs` (`tenant_id`)",
            "CREATE INDEX `ix_retention_runs_status` ON `xn_email_retention_runs` (`tenant_id`, `status`)",
            "CREATE INDEX `ix_retention_runs_started` ON `xn_email_retention_runs` (`tenant_id`, `started_at`)",

            # Alter xn_email_source_sync_locks
            "ALTER TABLE `xn_email_source_sync_locks` ADD COLUMN `fencing_token` BIGINT NOT NULL DEFAULT 1",
            "ALTER TABLE `xn_email_source_sync_locks` ADD COLUMN `renewal_failure_count` INT NOT NULL DEFAULT 0",
            "CREATE INDEX `ix_email_source_sync_locks_fencing_token` ON `xn_email_source_sync_locks` (`fencing_token`)",

            # Alter xn_email_ingestion_runs
            "ALTER TABLE `xn_email_ingestion_runs` ADD COLUMN `retry_of_run_id` CHAR(36) NULL",
        ],
    },

    {
        "id": "20260710000008_AddDurableAutomationState",
        "statements": [
            # xn_automation_registry
            """CREATE TABLE `xn_automation_registry` (
  `id` CHAR(36) NOT NULL,
  `automation_key` VARCHAR(200) NOT NULL,
  `provider` VARCHAR(200) NOT NULL,
  `category` VARCHAR(100) NOT NULL,
  `current_version` VARCHAR(50) NOT NULL,
  `lifecycle_status` INT NOT NULL,
  `globally_enabled` TINYINT(1) NOT NULL DEFAULT 0,
  `manifest_hash` VARCHAR(64) NOT NULL,
  `minimum_platform_version` VARCHAR(50) NULL,
  `registered_at` DATETIME(6) NOT NULL,
  `last_reconciled_at` DATETIME(6) NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  `row_version` INT UNSIGNED NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `uq_xn_automation_registry_key` ON `xn_automation_registry` (`automation_key`)",
            "CREATE INDEX `ix_xn_automation_registry_lifecycle_status` ON `xn_automation_registry` (`lifecycle_status`)",

            # xn_automation_versions
            """CREATE TABLE `xn_automation_versions` (
  `id` CHAR(36) NOT NULL,
  `automation_key` VARCHAR(200) NOT NULL,
  `version` VARCHAR(50) NOT NULL,
  `manifest_json` LONGTEXT NOT NULL,
  `manifest_schema_version` VARCHAR(50) NOT NULL,
  `compatibility_json` TEXT NULL,
  `registered_at` DATETIME(6) NOT NULL,
  `activated_at` DATETIME(6) NULL,
  `retired_at` DATETIME(6) NULL,
  `status` INT NOT NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  `row_version` INT UNSIGNED NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `uq_xn_automation_versions_key_version` ON `xn_automation_versions` (`automation_key`, `version`)",
            "CREATE INDEX `ix_xn_automation_versions_key` ON `xn_automation_versions` (`automation_key`)",

            # xn_tenant_automations
            """CREATE TABLE `xn_tenant_automations` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `automation_key` VARCHAR(200) NOT NULL,
  `enabled` TINYINT(1) NOT NULL DEFAULT 0,
  `lifecycle_override` VARCHAR(50) NULL,
  `configuration_version` VARCHAR(50) NULL,
  `last_validated_at` DATETIME(6) NULL,
  `updated_by` VARCHAR(200) NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  `row_version` INT UNSIGNED NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `uq_xn_tenant_automations_tenant_key` ON `xn_tenant_automations` (`tenant_id`, `automation_key`)",
            "CREATE INDEX `ix_xn_tenant_automations_tenant` ON `xn_tenant_automations` (`tenant_id`)",

            # xn_automation_configuration
            """CREATE TABLE `xn_automation_configuration` (
  `id` CHAR(36) NOT NULL,
  `scope_type` INT NOT NULL,
  `tenant_id` CHAR(36) NULL,
  `automation_key` VARCHAR(200) NOT NULL,
  `configuration_namespace` VARCHAR(200) NOT NULL,
  `configuration_json` LONGTEXT NOT NULL,
  `schema_version` VARCHAR(50) NOT NULL,
  `secret_references_json` TEXT NULL,
  `updated_by` VARCHAR(200) NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  `row_version` INT UNSIGNED NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `uq_xn_automation_configuration_scope` ON `xn_automation_configuration` (`scope_type`, `tenant_id`, `automation_key`, `configuration_namespace`)",
            "CREATE INDEX `ix_xn_automation_configuration_tenant` ON `xn_automation_configuration` (`tenant_id`)",
            "CREATE INDEX `ix_xn_automation_configuration_key` ON `xn_automation_configuration` (`automation_key`)",

            # xn_automation_runtime_state
            """CREATE TABLE `xn_automation_runtime_state` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `automation_key` VARCHAR(200) NOT NULL,
  `automation_version` VARCHAR(50) NOT NULL DEFAULT '',
  `global_state` INT NOT NULL,
  `tenant_state` INT NULL,
  `lifecycle_state` INT NOT NULL,
  `health_state` INT NOT NULL,
  `last_execution_at` DATETIME(6) NULL,
  `last_successful_execution_at` DATETIME(6) NULL,
  `consecutive_failure_count` INT NOT NULL DEFAULT 0,
  `total_executions` INT NOT NULL DEFAULT 0,
  `active_executions` INT NOT NULL DEFAULT 0,
  `total_failure_count` INT NOT NULL DEFAULT 0,
  `next_eligible_execution_at` DATETIME(6) NULL,
  `last_safe_error_category` VARCHAR(100) NULL,
  `last_safe_error_summary` VARCHAR(500) NULL,
  `worker_instance_id` VARCHAR(200) NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  `row_version` INT UNSIGNED NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `uq_xn_automation_runtime_state_tenant_key` ON `xn_automation_runtime_state` (`tenant_id`, `automation_key`)",
            "CREATE INDEX `ix_xn_automation_runtime_state_tenant` ON `xn_automation_runtime_state` (`tenant_id`)",
            "CREATE INDEX `ix_xn_automation_runtime_state_health` ON `xn_automation_runtime_state` (`health_state`)",
            "CREATE INDEX `ix_xn_automation_runtime_state_next_eligible` ON `xn_automation_runtime_state` (`tenant_id`, `next_eligible_execution_at`)",

            # xn_automation_executions
            """CREATE TABLE `xn_automation_executions` (
  `id` CHAR(36) NOT NULL,
  `execution_id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `automation_key` VARCHAR(200) NOT NULL,
  `automation_version` VARCHAR(50) NOT NULL,
  `trigger_type` INT NOT NULL,
  `status` INT NOT NULL,
  `idempotency_key` VARCHAR(200) NULL,
  `correlation_id` CHAR(36) NULL,
  `actor_id` VARCHAR(200) NULL,
  `queued_at` DATETIME(6) NOT NULL,
  `started_at` DATETIME(6) NULL,
  `completed_at` DATETIME(6) NULL,
  `retry_count` INT NOT NULL DEFAULT 0,
  `parent_execution_id` CHAR(36) NULL,
  `dead_letter_id` CHAR(36) NULL,
  `safe_result_summary` VARCHAR(500) NULL,
  `safe_error_category` VARCHAR(100) NULL,
  `safe_error_summary` VARCHAR(500) NULL,
  `worker_instance_id` VARCHAR(200) NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  `row_version` INT UNSIGNED NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `uq_xn_automation_executions_execution_id` ON `xn_automation_executions` (`execution_id`)",
            "CREATE INDEX `ix_xn_automation_executions_tenant` ON `xn_automation_executions` (`tenant_id`)",
            "CREATE INDEX `ix_xn_automation_executions_tenant_key` ON `xn_automation_executions` (`tenant_id`, `automation_key`)",
            "CREATE INDEX `ix_xn_automation_executions_tenant_status` ON `xn_automation_executions` (`tenant_id`, `status`)",
            "CREATE INDEX `ix_xn_automation_executions_correlation` ON `xn_automation_executions` (`correlation_id`)",

            # xn_automation_dead_letters
            """CREATE TABLE `xn_automation_dead_letters` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `automation_key` VARCHAR(200) NOT NULL,
  `automation_version` VARCHAR(50) NOT NULL,
  `execution_id` CHAR(36) NULL,
  `trigger_type` INT NOT NULL,
  `failure_category` VARCHAR(100) NOT NULL,
  `safe_error_summary` VARCHAR(500) NULL,
  `retry_count` INT NOT NULL DEFAULT 0,
  `replay_count` INT NOT NULL DEFAULT 0,
  `first_failed_at` DATETIME(6) NOT NULL,
  `last_failed_at` DATETIME(6) NOT NULL,
  `next_eligible_retry_at` DATETIME(6) NULL,
  `status` INT NOT NULL,
  `resolution` VARCHAR(500) NULL,
  `correlation_id` CHAR(36) NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  `row_version` INT UNSIGNED NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE INDEX `ix_xn_automation_dead_letters_tenant` ON `xn_automation_dead_letters` (`tenant_id`)",
            "CREATE INDEX `ix_xn_automation_dead_letters_tenant_key` ON `xn_automation_dead_letters` (`tenant_id`, `automation_key`)",
            "CREATE INDEX `ix_xn_automation_dead_letters_next_retry` ON `xn_automation_dead_letters` (`tenant_id`, `next_eligible_retry_at`)",
            "CREATE INDEX `ix_xn_automation_dead_letters_tenant_status` ON `xn_automation_dead_letters` (`tenant_id`, `status`)",

            # xn_automation_schedules
            """CREATE TABLE `xn_automation_schedules` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `automation_key` VARCHAR(200) NOT NULL,
  `schedule_type` INT NOT NULL,
  `expression` VARCHAR(200) NULL,
  `interval_seconds` INT NULL,
  `time_zone` VARCHAR(100) NOT NULL,
  `enabled` TINYINT(1) NOT NULL DEFAULT 1,
  `next_run_at` DATETIME(6) NULL,
  `last_run_at` DATETIME(6) NULL,
  `misfire_policy` INT NOT NULL,
  `concurrency_policy` INT NOT NULL,
  `created_by` VARCHAR(200) NULL,
  `updated_by` VARCHAR(200) NULL,
  `created_at` DATETIME(6) NOT NULL,
  `updated_at` DATETIME(6) NOT NULL,
  `row_version` INT UNSIGNED NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE INDEX `ix_xn_automation_schedules_tenant` ON `xn_automation_schedules` (`tenant_id`)",
            "CREATE INDEX `ix_xn_automation_schedules_tenant_key` ON `xn_automation_schedules` (`tenant_id`, `automation_key`)",
            "CREATE INDEX `ix_xn_automation_schedules_enabled_next_run` ON `xn_automation_schedules` (`enabled`, `next_run_at`)",

            # xn_automation_idempotency
            """CREATE TABLE `xn_automation_idempotency` (
  `id` CHAR(36) NOT NULL,
  `tenant_id` CHAR(36) NOT NULL,
  `automation_key` VARCHAR(200) NOT NULL,
  `idempotency_key` VARCHAR(200) NOT NULL,
  `request_fingerprint` VARCHAR(64) NOT NULL,
  `execution_id` CHAR(36) NULL,
  `expires_at` DATETIME(6) NOT NULL,
  `created_at` DATETIME(6) NOT NULL,
  `row_version` INT UNSIGNED NOT NULL,
  PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4""",
            "CREATE UNIQUE INDEX `uq_xn_automation_idempotency_tenant_key_idkey` ON `xn_automation_idempotency` (`tenant_id`, `automation_key`, `idempotency_key`)",
            "CREATE INDEX `ix_xn_automation_idempotency_tenant_key` ON `xn_automation_idempotency` (`tenant_id`, `automation_key`)",
            "CREATE INDEX `ix_xn_automation_idempotency_expires` ON `xn_automation_idempotency` (`expires_at`)",
        ],
    },
]


def main():
    print(f"\n=== Xenia raw-SQL migration runner ===")
    print(f"Target: {CONN_PARAMS['host']}:{CONN_PARAMS['port']}/{CONN_PARAMS['database']}\n")

    conn = pymysql.connect(**CONN_PARAMS)
    cur = conn.cursor()

    # Create __EFMigrationsHistory if absent
    cur.execute("""
        CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
          `MigrationId` VARCHAR(150) NOT NULL,
          `ProductVersion` VARCHAR(32) NOT NULL,
          PRIMARY KEY (`MigrationId`)
        ) CHARACTER SET utf8mb4
    """)
    conn.commit()

    # Fetch applied migrations
    cur.execute("SELECT `MigrationId` FROM `__EFMigrationsHistory`")
    applied = {row[0] for row in cur.fetchall()}
    print(f"Already applied: {sorted(applied) or '(none)'}\n")

    errors = 0
    for mig in MIGRATIONS:
        mid = mig["id"]
        if mid in applied:
            print(f"  SKIP   {mid} (already applied)")
            continue

        print(f"  APPLY  {mid}")
        mig_ok = True
        for sql in mig["statements"]:
            try:
                cur.execute(sql)
                conn.commit()
            except pymysql.err.OperationalError as e:
                # 1050 = table exists, 1060 = duplicate column, 1061 = duplicate index — treat as idempotent
                if e.args[0] in (1050, 1060, 1061):
                    conn.rollback()
                    print(f"    WARN: {e.args[1]} (skipped — already exists)")
                else:
                    conn.rollback()
                    print(f"    ERROR: {e}")
                    mig_ok = False
                    errors += 1
                    break
            except Exception as e:
                conn.rollback()
                print(f"    ERROR: {e}")
                mig_ok = False
                errors += 1
                break

        if mig_ok:
            cur.execute(
                "INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES (%s, %s)",
                (mid, EF_VERSION),
            )
            conn.commit()
            print(f"    OK — recorded in __EFMigrationsHistory")

    print()
    if errors:
        print(f"COMPLETED WITH {errors} ERROR(S). Review output above.")
        sys.exit(1)
    else:
        # Verify final state
        cur.execute("SELECT `MigrationId` FROM `__EFMigrationsHistory` ORDER BY `MigrationId`")
        rows = cur.fetchall()
        print("=== Migration history ===")
        for r in rows:
            print(f"  ✓  {r[0]}")
        print(f"\nAll {len(rows)} migrations applied successfully.")

    cur.close()
    conn.close()


if __name__ == "__main__":
    main()
