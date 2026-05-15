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

