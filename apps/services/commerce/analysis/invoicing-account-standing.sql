CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423230303_InitialCreate') THEN

    ALTER DATABASE CHARACTER SET utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423230303_InitialCreate') THEN

    CREATE TABLE `commerce_schema_marker` (
        `Id` int NOT NULL,
        `SchemaName` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `SchemaVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_commerce_schema_marker` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423230303_InitialCreate') THEN

    INSERT INTO `commerce_schema_marker` (`Id`, `CreatedAtUtc`, `SchemaName`, `SchemaVersion`)
    VALUES (1, TIMESTAMP '2026-04-23 00:00:00', 'commerce', '1.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423230303_InitialCreate') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260423230303_InitialCreate', '8.0.10');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

START TRANSACTION;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE TABLE `catalog_bundles` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `Key` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `Description` varchar(2000) CHARACTER SET utf8mb4 NULL,
        `Status` int NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_catalog_bundles` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE TABLE `catalog_products` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `Key` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `Description` varchar(2000) CHARACTER SET utf8mb4 NULL,
        `Status` int NOT NULL,
        `SortOrder` int NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_catalog_products` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE TABLE `catalog_addons` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `ProductId` char(36) COLLATE ascii_general_ci NULL,
        `Key` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `Description` varchar(2000) CHARACTER SET utf8mb4 NULL,
        `Status` int NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_catalog_addons` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_catalog_addons_catalog_products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `catalog_products` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE TABLE `catalog_features` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `ProductId` char(36) COLLATE ascii_general_ci NOT NULL,
        `Key` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `Description` varchar(2000) CHARACTER SET utf8mb4 NULL,
        `FeatureType` int NOT NULL,
        `Status` int NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_catalog_features` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_catalog_features_catalog_products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `catalog_products` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE TABLE `catalog_plans` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `ProductId` char(36) COLLATE ascii_general_ci NULL,
        `Key` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `Description` varchar(2000) CHARACTER SET utf8mb4 NULL,
        `Status` int NOT NULL,
        `BillingInterval` int NOT NULL,
        `TrialDays` int NULL,
        `SortOrder` int NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_catalog_plans` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_catalog_plans_catalog_products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `catalog_products` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE TABLE `catalog_bundle_items` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `BundleId` char(36) COLLATE ascii_general_ci NOT NULL,
        `ProductId` char(36) COLLATE ascii_general_ci NULL,
        `PlanId` char(36) COLLATE ascii_general_ci NULL,
        `AddonId` char(36) COLLATE ascii_general_ci NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_catalog_bundle_items` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_catalog_bundle_items_catalog_addons_AddonId` FOREIGN KEY (`AddonId`) REFERENCES `catalog_addons` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_catalog_bundle_items_catalog_bundles_BundleId` FOREIGN KEY (`BundleId`) REFERENCES `catalog_bundles` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_catalog_bundle_items_catalog_plans_PlanId` FOREIGN KEY (`PlanId`) REFERENCES `catalog_plans` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_catalog_bundle_items_catalog_products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `catalog_products` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE TABLE `catalog_plan_features` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `PlanId` char(36) COLLATE ascii_general_ci NOT NULL,
        `FeatureId` char(36) COLLATE ascii_general_ci NOT NULL,
        `IsEnabled` tinyint(1) NOT NULL,
        `LimitValue` bigint NULL,
        `MeteredIncludedUnits` bigint NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_catalog_plan_features` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_catalog_plan_features_catalog_features_FeatureId` FOREIGN KEY (`FeatureId`) REFERENCES `catalog_features` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_catalog_plan_features_catalog_plans_PlanId` FOREIGN KEY (`PlanId`) REFERENCES `catalog_plans` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE TABLE `catalog_prices` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `PlanId` char(36) COLLATE ascii_general_ci NULL,
        `AddonId` char(36) COLLATE ascii_general_ci NULL,
        `BundleId` char(36) COLLATE ascii_general_ci NULL,
        `Currency` varchar(3) CHARACTER SET utf8mb4 NOT NULL,
        `AmountMinor` bigint NOT NULL,
        `BillingInterval` int NOT NULL,
        `Status` int NOT NULL,
        `EffectiveFromUtc` datetime(6) NOT NULL,
        `EffectiveToUtc` datetime(6) NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_catalog_prices` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_catalog_prices_catalog_addons_AddonId` FOREIGN KEY (`AddonId`) REFERENCES `catalog_addons` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_catalog_prices_catalog_bundles_BundleId` FOREIGN KEY (`BundleId`) REFERENCES `catalog_bundles` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_catalog_prices_catalog_plans_PlanId` FOREIGN KEY (`PlanId`) REFERENCES `catalog_plans` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE INDEX `ix_catalog_addons_product_id` ON `catalog_addons` (`ProductId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE UNIQUE INDEX `ux_catalog_addons_key` ON `catalog_addons` (`Key`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE INDEX `IX_catalog_bundle_items_AddonId` ON `catalog_bundle_items` (`AddonId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE INDEX `ix_catalog_bundle_items_bundle_id` ON `catalog_bundle_items` (`BundleId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE INDEX `IX_catalog_bundle_items_PlanId` ON `catalog_bundle_items` (`PlanId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE INDEX `IX_catalog_bundle_items_ProductId` ON `catalog_bundle_items` (`ProductId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE UNIQUE INDEX `ux_catalog_bundles_key` ON `catalog_bundles` (`Key`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE INDEX `ix_catalog_features_product_id` ON `catalog_features` (`ProductId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE UNIQUE INDEX `ux_catalog_features_product_key` ON `catalog_features` (`ProductId`, `Key`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE INDEX `IX_catalog_plan_features_FeatureId` ON `catalog_plan_features` (`FeatureId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE UNIQUE INDEX `ux_catalog_plan_features_plan_feature` ON `catalog_plan_features` (`PlanId`, `FeatureId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE INDEX `ix_catalog_plans_product_id` ON `catalog_plans` (`ProductId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE UNIQUE INDEX `ux_catalog_plans_key` ON `catalog_plans` (`Key`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE INDEX `ix_catalog_prices_addon_lookup` ON `catalog_prices` (`AddonId`, `Currency`, `BillingInterval`, `Status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE INDEX `ix_catalog_prices_bundle_lookup` ON `catalog_prices` (`BundleId`, `Currency`, `BillingInterval`, `Status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE INDEX `ix_catalog_prices_plan_lookup` ON `catalog_prices` (`PlanId`, `Currency`, `BillingInterval`, `Status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    CREATE UNIQUE INDEX `ux_catalog_products_key` ON `catalog_products` (`Key`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260423232809_CatalogCore') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260423232809_CatalogCore', '8.0.10');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

START TRANSACTION;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424011949_BillingAccountCore') THEN

    CREATE TABLE `billing_accounts` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `AccountNumber` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `DisplayName` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `LegalName` varchar(400) CHARACTER SET utf8mb4 NULL,
        `Status` int NOT NULL,
        `DefaultCurrency` varchar(3) CHARACTER SET utf8mb4 NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_billing_accounts` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424011949_BillingAccountCore') THEN

    CREATE TABLE `billing_account_audit_events` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `BillingAccountId` char(36) COLLATE ascii_general_ci NOT NULL,
        `EventType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `Description` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
        `ActorType` int NOT NULL,
        `ActorId` varchar(128) CHARACTER SET utf8mb4 NULL,
        `MetadataJson` text CHARACTER SET utf8mb4 NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_billing_account_audit_events` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_billing_account_audit_events_billing_accounts_BillingAccount~` FOREIGN KEY (`BillingAccountId`) REFERENCES `billing_accounts` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424011949_BillingAccountCore') THEN

    CREATE TABLE `billing_account_contacts` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `BillingAccountId` char(36) COLLATE ascii_general_ci NOT NULL,
        `ContactType` int NOT NULL,
        `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `Email` varchar(320) CHARACTER SET utf8mb4 NOT NULL,
        `Phone` varchar(64) CHARACTER SET utf8mb4 NULL,
        `IsPrimary` tinyint(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_billing_account_contacts` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_billing_account_contacts_billing_accounts_BillingAccountId` FOREIGN KEY (`BillingAccountId`) REFERENCES `billing_accounts` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424011949_BillingAccountCore') THEN

    CREATE TABLE `billing_account_external_refs` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `BillingAccountId` char(36) COLLATE ascii_general_ci NOT NULL,
        `HostPlatformKey` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `ExternalTenantId` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `ExternalCustomerRef` varchar(128) CHARACTER SET utf8mb4 NULL,
        `IsPrimary` tinyint(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_billing_account_external_refs` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_billing_account_external_refs_billing_accounts_BillingAccoun~` FOREIGN KEY (`BillingAccountId`) REFERENCES `billing_accounts` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424011949_BillingAccountCore') THEN

    CREATE TABLE `billing_account_profiles` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `BillingAccountId` char(36) COLLATE ascii_general_ci NOT NULL,
        `AddressLine1` varchar(200) CHARACTER SET utf8mb4 NULL,
        `AddressLine2` varchar(200) CHARACTER SET utf8mb4 NULL,
        `City` varchar(120) CHARACTER SET utf8mb4 NULL,
        `StateRegion` varchar(120) CHARACTER SET utf8mb4 NULL,
        `PostalCode` varchar(40) CHARACTER SET utf8mb4 NULL,
        `Country` varchar(2) CHARACTER SET utf8mb4 NULL,
        `TaxId` varchar(64) CHARACTER SET utf8mb4 NULL,
        `TaxExempt` tinyint(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_billing_account_profiles` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_billing_account_profiles_billing_accounts_BillingAccountId` FOREIGN KEY (`BillingAccountId`) REFERENCES `billing_accounts` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424011949_BillingAccountCore') THEN

    CREATE INDEX `ix_billing_audit_events_account_created` ON `billing_account_audit_events` (`BillingAccountId`, `CreatedAtUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424011949_BillingAccountCore') THEN

    CREATE INDEX `ix_billing_contacts_account_id` ON `billing_account_contacts` (`BillingAccountId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424011949_BillingAccountCore') THEN

    CREATE INDEX `ix_billing_contacts_account_type` ON `billing_account_contacts` (`BillingAccountId`, `ContactType`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424011949_BillingAccountCore') THEN

    CREATE INDEX `ix_billing_external_refs_account_id` ON `billing_account_external_refs` (`BillingAccountId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424011949_BillingAccountCore') THEN

    CREATE UNIQUE INDEX `ux_billing_external_refs_host_tenant` ON `billing_account_external_refs` (`HostPlatformKey`, `ExternalTenantId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424011949_BillingAccountCore') THEN

    CREATE UNIQUE INDEX `ux_billing_profiles_account_id` ON `billing_account_profiles` (`BillingAccountId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424011949_BillingAccountCore') THEN

    CREATE UNIQUE INDEX `ux_billing_accounts_account_number` ON `billing_accounts` (`AccountNumber`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424011949_BillingAccountCore') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260424011949_BillingAccountCore', '8.0.10');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

START TRANSACTION;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424014922_SubscriptionEngine') THEN

    CREATE TABLE `subscriptions` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `BillingAccountId` char(36) COLLATE ascii_general_ci NOT NULL,
        `SubscriptionNumber` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `Status` int NOT NULL,
        `StartDateUtc` datetime(6) NOT NULL,
        `CurrentPeriodStartUtc` datetime(6) NOT NULL,
        `CurrentPeriodEndUtc` datetime(6) NOT NULL,
        `TrialStartUtc` datetime(6) NULL,
        `TrialEndUtc` datetime(6) NULL,
        `CancelAtPeriodEnd` tinyint(1) NOT NULL,
        `CancelledAtUtc` datetime(6) NULL,
        `CancellationReason` varchar(500) CHARACTER SET utf8mb4 NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_subscriptions` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_subscriptions_billing_accounts_BillingAccountId` FOREIGN KEY (`BillingAccountId`) REFERENCES `billing_accounts` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424014922_SubscriptionEngine') THEN

    CREATE TABLE `subscription_changes` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `SubscriptionId` char(36) COLLATE ascii_general_ci NOT NULL,
        `ChangeType` int NOT NULL,
        `FromPlanId` char(36) COLLATE ascii_general_ci NULL,
        `ToPlanId` char(36) COLLATE ascii_general_ci NULL,
        `FromPriceId` char(36) COLLATE ascii_general_ci NULL,
        `ToPriceId` char(36) COLLATE ascii_general_ci NULL,
        `EffectiveAtUtc` datetime(6) NOT NULL,
        `ProrationBehavior` int NOT NULL,
        `Reason` varchar(500) CHARACTER SET utf8mb4 NULL,
        `MetadataJson` text CHARACTER SET utf8mb4 NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_subscription_changes` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_subscription_changes_subscriptions_SubscriptionId` FOREIGN KEY (`SubscriptionId`) REFERENCES `subscriptions` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424014922_SubscriptionEngine') THEN

    CREATE TABLE `subscription_items` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `SubscriptionId` char(36) COLLATE ascii_general_ci NOT NULL,
        `PlanId` char(36) COLLATE ascii_general_ci NOT NULL,
        `PriceId` char(36) COLLATE ascii_general_ci NOT NULL,
        `Quantity` int NOT NULL,
        `UnitAmountMinor` bigint NOT NULL,
        `Currency` varchar(3) CHARACTER SET utf8mb4 NOT NULL,
        `BillingInterval` int NOT NULL,
        `Status` int NOT NULL,
        `EffectiveFromUtc` datetime(6) NOT NULL,
        `EffectiveToUtc` datetime(6) NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_subscription_items` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_subscription_items_catalog_plans_PlanId` FOREIGN KEY (`PlanId`) REFERENCES `catalog_plans` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_subscription_items_catalog_prices_PriceId` FOREIGN KEY (`PriceId`) REFERENCES `catalog_prices` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_subscription_items_subscriptions_SubscriptionId` FOREIGN KEY (`SubscriptionId`) REFERENCES `subscriptions` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424014922_SubscriptionEngine') THEN

    CREATE INDEX `ix_subscription_changes_subscription_created` ON `subscription_changes` (`SubscriptionId`, `CreatedAtUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424014922_SubscriptionEngine') THEN

    CREATE INDEX `IX_subscription_items_PlanId` ON `subscription_items` (`PlanId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424014922_SubscriptionEngine') THEN

    CREATE INDEX `IX_subscription_items_PriceId` ON `subscription_items` (`PriceId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424014922_SubscriptionEngine') THEN

    CREATE INDEX `ix_subscription_items_subscription_id` ON `subscription_items` (`SubscriptionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424014922_SubscriptionEngine') THEN

    CREATE INDEX `ix_subscription_items_subscription_status` ON `subscription_items` (`SubscriptionId`, `Status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424014922_SubscriptionEngine') THEN

    CREATE INDEX `ix_subscriptions_billing_account_id` ON `subscriptions` (`BillingAccountId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424014922_SubscriptionEngine') THEN

    CREATE INDEX `ix_subscriptions_status` ON `subscriptions` (`Status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424014922_SubscriptionEngine') THEN

    CREATE UNIQUE INDEX `ux_subscriptions_subscription_number` ON `subscriptions` (`SubscriptionNumber`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424014922_SubscriptionEngine') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260424014922_SubscriptionEngine', '8.0.10');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

START TRANSACTION;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424022726_PaymentProviderIntegration') THEN

    CREATE TABLE `payment_method_references` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `BillingAccountId` char(36) COLLATE ascii_general_ci NOT NULL,
        `Provider` int NOT NULL,
        `ProviderPaymentMethodId` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `ProviderCustomerId` varchar(128) CHARACTER SET utf8mb4 NULL,
        `Brand` varchar(32) CHARACTER SET utf8mb4 NULL,
        `Last4` varchar(4) CHARACTER SET utf8mb4 NULL,
        `ExpMonth` int NULL,
        `ExpYear` int NULL,
        `IsDefault` tinyint(1) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_payment_method_references` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_payment_method_references_billing_accounts_BillingAccountId` FOREIGN KEY (`BillingAccountId`) REFERENCES `billing_accounts` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424022726_PaymentProviderIntegration') THEN

    CREATE TABLE `payment_provider_customers` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `BillingAccountId` char(36) COLLATE ascii_general_ci NOT NULL,
        `Provider` int NOT NULL,
        `ProviderCustomerId` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `Email` varchar(320) CHARACTER SET utf8mb4 NULL,
        `Name` varchar(200) CHARACTER SET utf8mb4 NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_payment_provider_customers` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_payment_provider_customers_billing_accounts_BillingAccountId` FOREIGN KEY (`BillingAccountId`) REFERENCES `billing_accounts` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424022726_PaymentProviderIntegration') THEN

    CREATE TABLE `payment_provider_event_logs` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `Provider` int NOT NULL,
        `ProviderEventId` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `EventType` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
        `PayloadJson` longtext CHARACTER SET utf8mb4 NOT NULL,
        `ProcessingStatus` int NOT NULL,
        `ErrorMessage` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `ProcessedAtUtc` datetime(6) NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_payment_provider_event_logs` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424022726_PaymentProviderIntegration') THEN

    CREATE TABLE `payment_provider_subscriptions` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `SubscriptionId` char(36) COLLATE ascii_general_ci NOT NULL,
        `Provider` int NOT NULL,
        `ProviderSubscriptionId` varchar(128) CHARACTER SET utf8mb4 NULL,
        `ProviderCheckoutSessionId` varchar(128) CHARACTER SET utf8mb4 NULL,
        `ProviderCustomerId` varchar(128) CHARACTER SET utf8mb4 NULL,
        `Status` int NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_payment_provider_subscriptions` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_payment_provider_subscriptions_subscriptions_SubscriptionId` FOREIGN KEY (`SubscriptionId`) REFERENCES `subscriptions` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424022726_PaymentProviderIntegration') THEN

    CREATE INDEX `ix_payment_method_refs_account_provider` ON `payment_method_references` (`BillingAccountId`, `Provider`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424022726_PaymentProviderIntegration') THEN

    CREATE UNIQUE INDEX `ux_payment_method_refs_provider_pmid` ON `payment_method_references` (`Provider`, `ProviderPaymentMethodId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424022726_PaymentProviderIntegration') THEN

    CREATE UNIQUE INDEX `ux_payment_provider_customers_account_provider` ON `payment_provider_customers` (`BillingAccountId`, `Provider`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424022726_PaymentProviderIntegration') THEN

    CREATE UNIQUE INDEX `ux_payment_provider_customers_provider_pcid` ON `payment_provider_customers` (`Provider`, `ProviderCustomerId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424022726_PaymentProviderIntegration') THEN

    CREATE INDEX `ix_payment_provider_event_logs_provider_created` ON `payment_provider_event_logs` (`Provider`, `CreatedAtUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424022726_PaymentProviderIntegration') THEN

    CREATE INDEX `ix_payment_provider_event_logs_status` ON `payment_provider_event_logs` (`ProcessingStatus`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424022726_PaymentProviderIntegration') THEN

    CREATE UNIQUE INDEX `ux_payment_provider_event_logs_provider_eventid` ON `payment_provider_event_logs` (`Provider`, `ProviderEventId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424022726_PaymentProviderIntegration') THEN

    CREATE UNIQUE INDEX `ux_payment_provider_subs_provider_csid` ON `payment_provider_subscriptions` (`Provider`, `ProviderCheckoutSessionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424022726_PaymentProviderIntegration') THEN

    CREATE UNIQUE INDEX `ux_payment_provider_subs_provider_psid` ON `payment_provider_subscriptions` (`Provider`, `ProviderSubscriptionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424022726_PaymentProviderIntegration') THEN

    CREATE UNIQUE INDEX `ux_payment_provider_subs_sub_provider` ON `payment_provider_subscriptions` (`SubscriptionId`, `Provider`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424022726_PaymentProviderIntegration') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260424022726_PaymentProviderIntegration', '8.0.10');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

START TRANSACTION;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE TABLE `account_standings` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `BillingAccountId` char(36) COLLATE ascii_general_ci NOT NULL,
        `Status` int NOT NULL,
        `Reason` varchar(500) CHARACTER SET utf8mb4 NULL,
        `GracePeriodEndsAtUtc` datetime(6) NULL,
        `PastDueSinceUtc` datetime(6) NULL,
        `SuspendedAtUtc` datetime(6) NULL,
        `LastEvaluatedAtUtc` datetime(6) NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_account_standings` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_account_standings_billing_accounts_BillingAccountId` FOREIGN KEY (`BillingAccountId`) REFERENCES `billing_accounts` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE TABLE `invoices` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `BillingAccountId` char(36) COLLATE ascii_general_ci NOT NULL,
        `SubscriptionId` char(36) COLLATE ascii_general_ci NULL,
        `InvoiceNumber` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
        `Status` int NOT NULL,
        `Currency` varchar(3) CHARACTER SET utf8mb4 NOT NULL,
        `SubtotalAmountMinor` bigint NOT NULL,
        `DiscountAmountMinor` bigint NOT NULL,
        `TaxAmountMinor` bigint NOT NULL,
        `TotalAmountMinor` bigint NOT NULL,
        `AmountPaidMinor` bigint NOT NULL,
        `AmountDueMinor` bigint NOT NULL,
        `IssueDateUtc` datetime(6) NOT NULL,
        `DueDateUtc` datetime(6) NULL,
        `PaidAtUtc` datetime(6) NULL,
        `VoidedAtUtc` datetime(6) NULL,
        `Provider` int NULL,
        `ProviderInvoiceId` varchar(128) CHARACTER SET utf8mb4 NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_invoices` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_invoices_billing_accounts_BillingAccountId` FOREIGN KEY (`BillingAccountId`) REFERENCES `billing_accounts` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_invoices_subscriptions_SubscriptionId` FOREIGN KEY (`SubscriptionId`) REFERENCES `subscriptions` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE TABLE `invoice_lines` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `InvoiceId` char(36) COLLATE ascii_general_ci NOT NULL,
        `SubscriptionItemId` char(36) COLLATE ascii_general_ci NULL,
        `Description` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
        `Quantity` int NOT NULL,
        `UnitAmountMinor` bigint NOT NULL,
        `LineAmountMinor` bigint NOT NULL,
        `Currency` varchar(3) CHARACTER SET utf8mb4 NOT NULL,
        `ServicePeriodStartUtc` datetime(6) NULL,
        `ServicePeriodEndUtc` datetime(6) NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_invoice_lines` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_invoice_lines_invoices_InvoiceId` FOREIGN KEY (`InvoiceId`) REFERENCES `invoices` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_invoice_lines_subscription_items_SubscriptionItemId` FOREIGN KEY (`SubscriptionItemId`) REFERENCES `subscription_items` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE TABLE `payments` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `BillingAccountId` char(36) COLLATE ascii_general_ci NOT NULL,
        `InvoiceId` char(36) COLLATE ascii_general_ci NULL,
        `SubscriptionId` char(36) COLLATE ascii_general_ci NULL,
        `Provider` int NOT NULL,
        `ProviderPaymentId` varchar(128) CHARACTER SET utf8mb4 NULL,
        `ProviderCustomerId` varchar(128) CHARACTER SET utf8mb4 NULL,
        `AmountMinor` bigint NOT NULL,
        `Currency` varchar(3) CHARACTER SET utf8mb4 NOT NULL,
        `Status` int NOT NULL,
        `PaidAtUtc` datetime(6) NULL,
        `FailureCode` varchar(64) CHARACTER SET utf8mb4 NULL,
        `FailureMessage` varchar(500) CHARACTER SET utf8mb4 NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_payments` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_payments_billing_accounts_BillingAccountId` FOREIGN KEY (`BillingAccountId`) REFERENCES `billing_accounts` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_payments_invoices_InvoiceId` FOREIGN KEY (`InvoiceId`) REFERENCES `invoices` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_payments_subscriptions_SubscriptionId` FOREIGN KEY (`SubscriptionId`) REFERENCES `subscriptions` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE TABLE `payment_attempts` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `PaymentId` char(36) COLLATE ascii_general_ci NULL,
        `BillingAccountId` char(36) COLLATE ascii_general_ci NOT NULL,
        `SubscriptionId` char(36) COLLATE ascii_general_ci NULL,
        `Provider` int NOT NULL,
        `ProviderEventId` varchar(128) CHARACTER SET utf8mb4 NULL,
        `AttemptedAtUtc` datetime(6) NOT NULL,
        `Status` int NOT NULL,
        `ErrorCode` varchar(64) CHARACTER SET utf8mb4 NULL,
        `ErrorMessage` varchar(500) CHARACTER SET utf8mb4 NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_payment_attempts` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_payment_attempts_billing_accounts_BillingAccountId` FOREIGN KEY (`BillingAccountId`) REFERENCES `billing_accounts` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_payment_attempts_payments_PaymentId` FOREIGN KEY (`PaymentId`) REFERENCES `payments` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_payment_attempts_subscriptions_SubscriptionId` FOREIGN KEY (`SubscriptionId`) REFERENCES `subscriptions` (`Id`) ON DELETE RESTRICT
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE UNIQUE INDEX `ux_account_standings_billing_account_id` ON `account_standings` (`BillingAccountId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE INDEX `ix_invoice_lines_invoice_id` ON `invoice_lines` (`InvoiceId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE INDEX `ix_invoice_lines_subscription_item_id` ON `invoice_lines` (`SubscriptionItemId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE INDEX `ix_invoices_billing_account_id` ON `invoices` (`BillingAccountId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE INDEX `ix_invoices_provider_provider_invoice_id` ON `invoices` (`Provider`, `ProviderInvoiceId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE INDEX `ix_invoices_status_due` ON `invoices` (`Status`, `DueDateUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE INDEX `ix_invoices_subscription_id` ON `invoices` (`SubscriptionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE UNIQUE INDEX `ux_invoices_invoice_number` ON `invoices` (`InvoiceNumber`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE INDEX `ix_payment_attempts_billing_account_id` ON `payment_attempts` (`BillingAccountId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE INDEX `ix_payment_attempts_payment_id` ON `payment_attempts` (`PaymentId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE UNIQUE INDEX `ux_payment_attempts_provider_event_id` ON `payment_attempts` (`Provider`, `ProviderEventId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE INDEX `IX_payment_attempts_SubscriptionId` ON `payment_attempts` (`SubscriptionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE INDEX `ix_payments_billing_account_id` ON `payments` (`BillingAccountId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE INDEX `ix_payments_invoice_id` ON `payments` (`InvoiceId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE INDEX `ix_payments_subscription_id` ON `payments` (`SubscriptionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    CREATE UNIQUE INDEX `ux_payments_provider_provider_payment_id` ON `payments` (`Provider`, `ProviderPaymentId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260424031253_InvoicingAccountStanding') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260424031253_InvoicingAccountStanding', '8.0.10');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

