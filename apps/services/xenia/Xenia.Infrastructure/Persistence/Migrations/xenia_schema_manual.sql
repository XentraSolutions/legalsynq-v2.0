-- Xenia Platform Schema — Manual Migration Script
-- Generated: 2026-07-10
-- Use this script when EF tooling cannot generate SQL automatically (Pomelo 8 + .NET 10 NullRef).
-- This is idempotent (IF NOT EXISTS / ADD COLUMN IF NOT EXISTS).

-- Migration history table
CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
  `MigrationId` varchar(150) NOT NULL,
  `ProductVersion` varchar(32) NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── 20260710000001_XeniaInitial ──────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS `xn_modules` (
  `id`                      char(36)       NOT NULL,
  `module_key`              varchar(100)   NOT NULL,
  `name`                    varchar(200)   NOT NULL,
  `version`                 varchar(50)    NOT NULL,
  `description`             varchar(1000)  NULL,
  `global_enabled`          tinyint(1)     NOT NULL,
  `status`                  varchar(32)    NOT NULL,
  `configuration_namespace` varchar(200)   NOT NULL,
  `created_at`              datetime(6)    NOT NULL,
  `updated_at`              datetime(6)    NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `ix_xn_modules_module_key` (`module_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `xn_tenant_modules` (
  `id`                   char(36)     NOT NULL,
  `tenant_id`            char(36)     NOT NULL,
  `module_key`           varchar(100) NOT NULL,
  `enabled`              tinyint(1)   NOT NULL,
  `module_configuration` varchar(8000) NULL,
  `created_at`           datetime(6)  NOT NULL,
  `updated_at`           datetime(6)  NOT NULL,
  PRIMARY KEY (`id`),
  KEY `ix_xn_tenant_modules_tenant_id` (`tenant_id`),
  UNIQUE KEY `ix_xn_tenant_modules_tenant_module` (`tenant_id`, `module_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `xn_platform_adapters` (
  `id`                   char(36)     NOT NULL,
  `adapter_key`          varchar(100) NOT NULL,
  `adapter_type`         varchar(32)  NOT NULL,
  `name`                 varchar(200) NOT NULL,
  `version`              varchar(50)  NOT NULL,
  `configuration_status` varchar(32)  NOT NULL,
  `availability_status`  varchar(32)  NOT NULL,
  `health_status`        varchar(32)  NOT NULL,
  `last_health_check_at` datetime(6)  NULL,
  `diagnostic_message`   varchar(500) NULL,
  `created_at`           datetime(6)  NOT NULL,
  `updated_at`           datetime(6)  NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `ix_xn_platform_adapters_key` (`adapter_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `xn_configuration` (
  `id`                  char(36)      NOT NULL,
  `scope_type`          varchar(32)   NOT NULL,
  `scope_id`            varchar(300)  NULL,
  `namespace`           varchar(200)  NOT NULL,
  `configuration_key`   varchar(200)  NOT NULL,
  `configuration_value` varchar(4000) NULL,
  `value_type`          varchar(50)   NULL,
  `is_secret`           tinyint(1)    NOT NULL,
  `version`             int           NOT NULL,
  `created_at`          datetime(6)   NOT NULL,
  `updated_at`          datetime(6)   NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `ix_xn_configuration_scope_key` (`scope_type`, `scope_id`, `namespace`, `configuration_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `xn_tenant_settings` (
  `id`         char(36)     NOT NULL,
  `tenant_id`  char(36)     NOT NULL,
  `enabled`    tinyint(1)   NOT NULL,
  `settings`   varchar(8000) NULL,
  `created_at` datetime(6)  NOT NULL,
  `updated_at` datetime(6)  NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `ix_xn_tenant_settings_tenant_id` (`tenant_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260710000001_XeniaInitial', '8.0.10');

-- ── 20260710000002_AddAdapterCriticality ─────────────────────────────────────
-- Adds AdapterCriticality column (Optional/Mandatory/Disabled) to platform adapters.
-- Optional = 0 is CLR default; avoids EF sentinel conflict.

ALTER TABLE `xn_platform_adapters`
  ADD COLUMN IF NOT EXISTS `criticality` varchar(32) NOT NULL DEFAULT 'Optional';

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260710000002_AddAdapterCriticality', '8.0.10');
