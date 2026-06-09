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

