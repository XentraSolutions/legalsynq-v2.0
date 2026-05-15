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

