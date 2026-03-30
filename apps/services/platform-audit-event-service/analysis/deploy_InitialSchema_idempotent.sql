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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE TABLE `AuditEventRecords` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `AuditId` char(36) COLLATE ascii_general_ci NOT NULL,
        `EventId` char(36) COLLATE ascii_general_ci NULL,
        `EventType` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `EventCategory` tinyint NOT NULL,
        `SourceSystem` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `SourceService` varchar(200) CHARACTER SET utf8mb4 NULL,
        `SourceEnvironment` varchar(100) CHARACTER SET utf8mb4 NULL,
        `PlatformId` char(36) COLLATE ascii_general_ci NULL,
        `TenantId` varchar(100) CHARACTER SET utf8mb4 NULL,
        `OrganizationId` varchar(100) CHARACTER SET utf8mb4 NULL,
        `UserScopeId` varchar(200) CHARACTER SET utf8mb4 NULL,
        `ScopeType` tinyint NOT NULL,
        `ActorId` varchar(200) CHARACTER SET utf8mb4 NULL,
        `ActorType` tinyint NOT NULL,
        `ActorName` varchar(300) CHARACTER SET utf8mb4 NULL,
        `ActorIpAddress` varchar(45) CHARACTER SET utf8mb4 NULL,
        `ActorUserAgent` varchar(500) CHARACTER SET utf8mb4 NULL,
        `EntityType` varchar(200) CHARACTER SET utf8mb4 NULL,
        `EntityId` varchar(200) CHARACTER SET utf8mb4 NULL,
        `Action` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `Description` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
        `BeforeJson` mediumtext CHARACTER SET utf8mb4 NULL,
        `AfterJson` mediumtext CHARACTER SET utf8mb4 NULL,
        `MetadataJson` text CHARACTER SET utf8mb4 NULL,
        `CorrelationId` varchar(200) CHARACTER SET utf8mb4 NULL,
        `RequestId` varchar(200) CHARACTER SET utf8mb4 NULL,
        `SessionId` varchar(200) CHARACTER SET utf8mb4 NULL,
        `VisibilityScope` tinyint NOT NULL,
        `Severity` tinyint NOT NULL,
        `OccurredAtUtc` datetime(6) NOT NULL,
        `RecordedAtUtc` datetime(6) NOT NULL,
        `Hash` varchar(64) CHARACTER SET utf8mb4 NULL,
        `PreviousHash` varchar(64) CHARACTER SET utf8mb4 NULL,
        `IdempotencyKey` varchar(300) CHARACTER SET utf8mb4 NULL,
        `IsReplay` tinyint(1) NOT NULL DEFAULT FALSE,
        `TagsJson` text CHARACTER SET utf8mb4 NULL,
        CONSTRAINT `PK_AuditEventRecords` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE TABLE `AuditExportJobs` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `ExportId` char(36) COLLATE ascii_general_ci NOT NULL,
        `RequestedBy` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `ScopeType` tinyint NOT NULL,
        `ScopeId` varchar(200) CHARACTER SET utf8mb4 NULL,
        `FilterJson` text CHARACTER SET utf8mb4 NULL,
        `Format` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
        `Status` tinyint NOT NULL,
        `FilePath` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `ErrorMessage` text CHARACTER SET utf8mb4 NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `CompletedAtUtc` datetime(6) NULL,
        CONSTRAINT `PK_AuditExportJobs` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE TABLE `IngestSourceRegistrations` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `SourceSystem` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `SourceService` varchar(200) CHARACTER SET utf8mb4 NULL,
        `IsActive` tinyint(1) NOT NULL DEFAULT TRUE,
        `Notes` text CHARACTER SET utf8mb4 NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_IngestSourceRegistrations` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE TABLE `IntegrityCheckpoints` (
        `Id` bigint NOT NULL AUTO_INCREMENT,
        `CheckpointType` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `FromRecordedAtUtc` datetime(6) NOT NULL,
        `ToRecordedAtUtc` datetime(6) NOT NULL,
        `AggregateHash` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
        `RecordCount` bigint NOT NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        CONSTRAINT `PK_IntegrityCheckpoints` PRIMARY KEY (`Id`)
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
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditEventRecords_ActorId` ON `AuditEventRecords` (`ActorId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditEventRecords_CorrelationId` ON `AuditEventRecords` (`CorrelationId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditEventRecords_EntityType_EntityId` ON `AuditEventRecords` (`EntityType`, `EntityId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditEventRecords_EventCategory` ON `AuditEventRecords` (`EventCategory`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditEventRecords_EventType` ON `AuditEventRecords` (`EventType`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditEventRecords_OccurredAtUtc` ON `AuditEventRecords` (`OccurredAtUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditEventRecords_OrganizationId` ON `AuditEventRecords` (`OrganizationId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditEventRecords_RecordedAtUtc` ON `AuditEventRecords` (`RecordedAtUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditEventRecords_RequestId` ON `AuditEventRecords` (`RequestId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditEventRecords_SessionId` ON `AuditEventRecords` (`SessionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditEventRecords_Severity_RecordedAt` ON `AuditEventRecords` (`Severity`, `RecordedAtUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditEventRecords_TenantId` ON `AuditEventRecords` (`TenantId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditEventRecords_TenantId_Category_OccurredAt` ON `AuditEventRecords` (`TenantId`, `EventCategory`, `OccurredAtUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditEventRecords_TenantId_OccurredAt` ON `AuditEventRecords` (`TenantId`, `OccurredAtUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditEventRecords_VisibilityScope` ON `AuditEventRecords` (`VisibilityScope`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE UNIQUE INDEX `UX_AuditEventRecords_AuditId` ON `AuditEventRecords` (`AuditId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE UNIQUE INDEX `UX_AuditEventRecords_IdempotencyKey` ON `AuditEventRecords` (`IdempotencyKey`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditExportJobs_CreatedAtUtc` ON `AuditExportJobs` (`CreatedAtUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditExportJobs_RequestedBy` ON `AuditExportJobs` (`RequestedBy`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditExportJobs_RequestedBy_Status_CreatedAt` ON `AuditExportJobs` (`RequestedBy`, `Status`, `CreatedAtUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditExportJobs_ScopeType_ScopeId` ON `AuditExportJobs` (`ScopeType`, `ScopeId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_AuditExportJobs_Status` ON `AuditExportJobs` (`Status`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE UNIQUE INDEX `UX_AuditExportJobs_ExportId` ON `AuditExportJobs` (`ExportId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_IngestSourceRegistrations_IsActive` ON `IngestSourceRegistrations` (`IsActive`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE UNIQUE INDEX `UX_IngestSourceRegistrations_SourceSystem_SourceService` ON `IngestSourceRegistrations` (`SourceSystem`, `SourceService`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_IntegrityCheckpoints_CheckpointType` ON `IntegrityCheckpoints` (`CheckpointType`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_IntegrityCheckpoints_CheckpointType_FromAt` ON `IntegrityCheckpoints` (`CheckpointType`, `FromRecordedAtUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_IntegrityCheckpoints_CreatedAtUtc` ON `IntegrityCheckpoints` (`CreatedAtUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    CREATE INDEX `IX_IntegrityCheckpoints_Window` ON `IntegrityCheckpoints` (`FromRecordedAtUtc`, `ToRecordedAtUtc`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260330140138_InitialSchema') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260330140138_InitialSchema', '8.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

