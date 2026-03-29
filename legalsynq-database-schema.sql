-- =============================================================================
-- LegalSynq Platform — Complete Database Schema
-- Generated: 2026-03-29
-- Engine: MySQL 8.x  |  Charset: utf8mb4  |  Collation: utf8mb4_unicode_ci
--
-- Three databases:
--   1. identity_db   — Tenants, Users, Roles, Orgs, Products, Audit
--   2. fund_db       — Funding Applications
--   3. careconnect_db — Providers, Referrals, Appointments, Scheduling
-- =============================================================================


-- =============================================================================
-- DATABASE 1 — identity_db
-- =============================================================================
CREATE DATABASE IF NOT EXISTS `identity_db`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE `identity_db`;

-- ── Products ─────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `Products` (
  `Id`            char(36)      NOT NULL COLLATE ascii_general_ci,
  `Name`          varchar(200)  NOT NULL,
  `Code`          varchar(100)  NOT NULL,
  `Description`   varchar(1000)     NULL,
  `IsActive`      tinyint(1)    NOT NULL DEFAULT 1,
  `CreatedAtUtc`  datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Products_Code` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── Tenants ──────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `Tenants` (
  `Id`            char(36)      NOT NULL COLLATE ascii_general_ci,
  `Name`          varchar(200)  NOT NULL,
  `Code`          varchar(100)  NOT NULL,
  `IsActive`      tinyint(1)    NOT NULL DEFAULT 1,
  `CreatedAtUtc`  datetime(6)   NOT NULL,
  `UpdatedAtUtc`  datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Tenants_Code` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── TenantProducts  (legacy — superseded by OrganizationProducts) ─────────────
CREATE TABLE IF NOT EXISTS `TenantProducts` (
  `TenantId`    char(36)   NOT NULL COLLATE ascii_general_ci,
  `ProductId`   char(36)   NOT NULL COLLATE ascii_general_ci,
  `IsEnabled`   tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`TenantId`, `ProductId`),
  KEY `IX_TenantProducts_ProductId` (`ProductId`),
  CONSTRAINT `FK_TenantProducts_Tenants_TenantId`
    FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_TenantProducts_Products_ProductId`
    FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── Roles ────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `Roles` (
  `Id`            char(36)      NOT NULL COLLATE ascii_general_ci,
  `TenantId`      char(36)      NOT NULL COLLATE ascii_general_ci,
  `Name`          varchar(200)  NOT NULL,
  `Description`   varchar(1000)     NULL,
  `IsSystemRole`  tinyint(1)    NOT NULL DEFAULT 0,
  `CreatedAtUtc`  datetime(6)   NOT NULL,
  `UpdatedAtUtc`  datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Roles_TenantId_Name` (`TenantId`, `Name`),
  CONSTRAINT `FK_Roles_Tenants_TenantId`
    FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── Users ────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `Users` (
  `Id`              char(36)      NOT NULL COLLATE ascii_general_ci,
  `TenantId`        char(36)      NOT NULL COLLATE ascii_general_ci,
  `Email`           varchar(320)  NOT NULL,
  `FullName`        varchar(200)  NOT NULL,
  `PasswordHash`    varchar(500)  NOT NULL,
  `IsActive`        tinyint(1)    NOT NULL DEFAULT 1,
  `LastLoginAtUtc`  datetime(6)       NULL,
  `CreatedAtUtc`    datetime(6)   NOT NULL,
  `UpdatedAtUtc`    datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Users_TenantId_Email` (`TenantId`, `Email`),
  CONSTRAINT `FK_Users_Tenants_TenantId`
    FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── UserRoles ────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `UserRoles` (
  `UserId`  char(36) NOT NULL COLLATE ascii_general_ci,
  `RoleId`  char(36) NOT NULL COLLATE ascii_general_ci,
  PRIMARY KEY (`UserId`, `RoleId`),
  KEY `IX_UserRoles_RoleId` (`RoleId`),
  CONSTRAINT `FK_UserRoles_Users_UserId`
    FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_UserRoles_Roles_RoleId`
    FOREIGN KEY (`RoleId`) REFERENCES `Roles` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── Organizations ─────────────────────────────────────────────────────────────
-- Added in migration: AddMultiOrgProductRoleModel
CREATE TABLE IF NOT EXISTS `Organizations` (
  `Id`                   char(36)      NOT NULL COLLATE ascii_general_ci,
  `TenantId`             char(36)      NOT NULL COLLATE ascii_general_ci,
  `Name`                 varchar(300)  NOT NULL,
  `Code`                 varchar(100)  NOT NULL,
  `Type`                 varchar(50)   NOT NULL,  -- e.g. LawFirm, MedProvider, Platform
  `IsActive`             tinyint(1)    NOT NULL DEFAULT 1,
  `PrimaryContactEmail`  varchar(320)      NULL,
  `CreatedAtUtc`         datetime(6)   NOT NULL,
  `UpdatedAtUtc`         datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Organizations_TenantId_Code` (`TenantId`, `Code`),
  KEY `IX_Organizations_TenantId` (`TenantId`),
  CONSTRAINT `FK_Organizations_Tenants_TenantId`
    FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── OrganizationDomains ───────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `OrganizationDomains` (
  `Id`              char(36)      NOT NULL COLLATE ascii_general_ci,
  `OrganizationId`  char(36)      NOT NULL COLLATE ascii_general_ci,
  `Domain`          varchar(253)  NOT NULL,
  `DomainType`      varchar(20)   NOT NULL,  -- Primary | Custom
  `IsPrimary`       tinyint(1)    NOT NULL DEFAULT 0,
  `IsVerified`      tinyint(1)    NOT NULL DEFAULT 0,
  `CreatedAtUtc`    datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_OrganizationDomains_Domain` (`Domain`),
  KEY `IX_OrganizationDomains_OrganizationId` (`OrganizationId`),
  CONSTRAINT `FK_OrganizationDomains_Organizations_OrganizationId`
    FOREIGN KEY (`OrganizationId`) REFERENCES `Organizations` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── OrganizationProducts ──────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `OrganizationProducts` (
  `OrganizationId`   char(36)   NOT NULL COLLATE ascii_general_ci,
  `ProductId`        char(36)   NOT NULL COLLATE ascii_general_ci,
  `IsEnabled`        tinyint(1) NOT NULL DEFAULT 0,
  `EnabledAtUtc`     datetime(6)    NULL,
  `GrantedByUserId`  char(36)       NULL COLLATE ascii_general_ci,
  PRIMARY KEY (`OrganizationId`, `ProductId`),
  KEY `IX_OrganizationProducts_ProductId` (`ProductId`),
  CONSTRAINT `FK_OrganizationProducts_Organizations_OrganizationId`
    FOREIGN KEY (`OrganizationId`) REFERENCES `Organizations` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_OrganizationProducts_Products_ProductId`
    FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── ProductRoles ──────────────────────────────────────────────────────────────
-- Maps platform product roles (SynqFund:Reviewer, CareConnect:Provider, etc.)
CREATE TABLE IF NOT EXISTS `ProductRoles` (
  `Id`          char(36)     NOT NULL COLLATE ascii_general_ci,
  `ProductId`   char(36)     NOT NULL COLLATE ascii_general_ci,
  `Name`        varchar(200) NOT NULL,
  `Code`        varchar(100) NOT NULL,
  `Description` varchar(1000)    NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ProductRoles_ProductId_Code` (`ProductId`, `Code`),
  CONSTRAINT `FK_ProductRoles_Products_ProductId`
    FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── OrgMemberships ────────────────────────────────────────────────────────────
-- User ↔ Org membership with optional product role
CREATE TABLE IF NOT EXISTS `OrgMemberships` (
  `Id`              char(36)   NOT NULL COLLATE ascii_general_ci,
  `UserId`          char(36)   NOT NULL COLLATE ascii_general_ci,
  `OrganizationId`  char(36)   NOT NULL COLLATE ascii_general_ci,
  `ProductRoleId`   char(36)       NULL COLLATE ascii_general_ci,
  `IsActive`        tinyint(1) NOT NULL DEFAULT 1,
  `JoinedAtUtc`     datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_OrgMemberships_UserId_OrganizationId` (`UserId`, `OrganizationId`),
  KEY `IX_OrgMemberships_OrganizationId` (`OrganizationId`),
  KEY `IX_OrgMemberships_ProductRoleId` (`ProductRoleId`),
  CONSTRAINT `FK_OrgMemberships_Users_UserId`
    FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_OrgMemberships_Organizations_OrganizationId`
    FOREIGN KEY (`OrganizationId`) REFERENCES `Organizations` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_OrgMemberships_ProductRoles_ProductRoleId`
    FOREIGN KEY (`ProductRoleId`) REFERENCES `ProductRoles` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── AuditLogs ─────────────────────────────────────────────────────────────────
-- Added in migration: AddAuditLogsTable
CREATE TABLE IF NOT EXISTS `AuditLogs` (
  `Id`              char(36)      NOT NULL COLLATE ascii_general_ci,
  `ActorEmail`      varchar(320)  NOT NULL,
  `ActorRole`       varchar(100)  NOT NULL,
  `Action`          varchar(200)  NOT NULL,
  `ResourceType`    varchar(100)  NOT NULL,
  `ResourceId`      varchar(200)  NOT NULL,
  `MetadataJson`    varchar(4000)     NULL,
  `OccurredAtUtc`   datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_AuditLogs_ActorEmail` (`ActorEmail`),
  KEY `IX_AuditLogs_Action` (`Action`),
  KEY `IX_AuditLogs_ResourceType` (`ResourceType`),
  KEY `IX_AuditLogs_OccurredAtUtc` (`OccurredAtUtc`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── EFCore migrations history ─────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
  `MigrationId`    varchar(150) NOT NULL,
  `ProductVersion` varchar(32)  NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;


-- =============================================================================
-- DATABASE 2 — fund_db
-- =============================================================================
CREATE DATABASE IF NOT EXISTS `fund_db`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE `fund_db`;

-- ── Applications ─────────────────────────────────────────────────────────────
-- Pre-settlement / litigation funding applications
CREATE TABLE IF NOT EXISTS `Applications` (
  `Id`                  char(36)       NOT NULL COLLATE ascii_general_ci,
  `TenantId`            char(36)       NOT NULL COLLATE ascii_general_ci,
  `ApplicationNumber`   varchar(50)    NOT NULL,
  `ApplicantFirstName`  varchar(100)   NOT NULL,
  `ApplicantLastName`   varchar(100)   NOT NULL,
  `Email`               varchar(320)   NOT NULL,
  `Phone`               varchar(30)    NOT NULL,
  `Status`              varchar(50)    NOT NULL,  -- Pending | UnderReview | Approved | Denied | Funded | Closed
  `RequestedAmount`     decimal(18,2)      NULL,
  `ApprovedAmount`      decimal(18,2)      NULL,
  `CaseType`            varchar(100)       NULL,
  `IncidentDate`        varchar(20)        NULL,  -- ISO 8601 date string
  `AttorneyNotes`       varchar(4000)      NULL,
  `ApprovalTerms`       varchar(4000)      NULL,
  `DenialReason`        varchar(2000)      NULL,
  `FunderId`            char(36)           NULL COLLATE ascii_general_ci,
  `CreatedByUserId`     char(36)       NOT NULL COLLATE ascii_general_ci,
  `UpdatedByUserId`     char(36)           NULL COLLATE ascii_general_ci,
  `CreatedAtUtc`        datetime(6)    NOT NULL,
  `UpdatedAtUtc`        datetime(6)    NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Applications_TenantId_ApplicationNumber` (`TenantId`, `ApplicationNumber`),
  KEY `IX_Applications_TenantId_Status`     (`TenantId`, `Status`),
  KEY `IX_Applications_TenantId_CreatedAtUtc` (`TenantId`, `CreatedAtUtc`),
  KEY `IX_Applications_TenantId_FunderId`   (`TenantId`, `FunderId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── EFCore migrations history ─────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
  `MigrationId`    varchar(150) NOT NULL,
  `ProductVersion` varchar(32)  NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;


-- =============================================================================
-- DATABASE 3 — careconnect_db
-- =============================================================================
CREATE DATABASE IF NOT EXISTS `careconnect_db`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE `careconnect_db`;

-- ── Categories ───────────────────────────────────────────────────────────────
-- Service/specialty categories for providers (e.g. Orthopedics, Physical Therapy)
CREATE TABLE IF NOT EXISTS `Categories` (
  `Id`            char(36)      NOT NULL COLLATE ascii_general_ci,
  `Name`          varchar(200)  NOT NULL,
  `Code`          varchar(50)   NOT NULL,
  `Description`   varchar(1000)     NULL,
  `IsActive`      tinyint(1)    NOT NULL DEFAULT 1,
  `CreatedAtUtc`  datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Categories_Code` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── Providers ────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `Providers` (
  `Id`                char(36)      NOT NULL COLLATE ascii_general_ci,
  `TenantId`          char(36)      NOT NULL COLLATE ascii_general_ci,
  `Name`              varchar(200)  NOT NULL,
  `OrganizationName`  varchar(300)      NULL,
  `Email`             varchar(320)  NOT NULL,
  `Phone`             varchar(50)   NOT NULL,
  `AddressLine1`      varchar(300)  NOT NULL,
  `AddressLine2`      varchar(300)      NULL,
  `City`              varchar(100)  NOT NULL,
  `State`             varchar(100)  NOT NULL,
  `PostalCode`        varchar(20)   NOT NULL,
  `Latitude`          double            NULL,
  `Longitude`         double            NULL,
  `IsActive`          tinyint(1)    NOT NULL DEFAULT 1,
  `AcceptingReferrals` tinyint(1)   NOT NULL DEFAULT 1,
  `CreatedAtUtc`      datetime(6)   NOT NULL,
  `UpdatedAtUtc`      datetime(6)   NOT NULL,
  `CreatedByUserId`   char(36)          NULL COLLATE ascii_general_ci,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Providers_TenantId_Email` (`TenantId`, `Email`),
  KEY `IX_Providers_TenantId_Name`        (`TenantId`, `Name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── ProviderCategories ────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `ProviderCategories` (
  `ProviderId`   char(36) NOT NULL COLLATE ascii_general_ci,
  `CategoryId`   char(36) NOT NULL COLLATE ascii_general_ci,
  PRIMARY KEY (`ProviderId`, `CategoryId`),
  KEY `IX_ProviderCategories_CategoryId` (`CategoryId`),
  CONSTRAINT `FK_ProviderCategories_Providers_ProviderId`
    FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_ProviderCategories_Categories_CategoryId`
    FOREIGN KEY (`CategoryId`) REFERENCES `Categories` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── Referrals ────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `Referrals` (
  `Id`                   char(36)      NOT NULL COLLATE ascii_general_ci,
  `TenantId`             char(36)      NOT NULL COLLATE ascii_general_ci,
  `ProviderId`           char(36)      NOT NULL COLLATE ascii_general_ci,
  `PatientFirstName`     varchar(100)  NOT NULL,
  `PatientLastName`      varchar(100)  NOT NULL,
  `PatientEmail`         varchar(320)      NULL,
  `PatientPhone`         varchar(50)       NULL,
  `PatientDateOfBirth`   varchar(20)       NULL,
  `Status`               varchar(50)   NOT NULL,  -- Pending | Scheduled | Completed | Cancelled
  `ReferralNote`         varchar(4000)     NULL,
  `ReferringOrgId`       char(36)          NULL COLLATE ascii_general_ci,
  `ReferringPartyId`     char(36)          NULL COLLATE ascii_general_ci,
  `ReceivingOrgId`       char(36)          NULL COLLATE ascii_general_ci,
  `ReceivingPartyId`     char(36)          NULL COLLATE ascii_general_ci,
  `CreatedByUserId`      char(36)      NOT NULL COLLATE ascii_general_ci,
  `CreatedAtUtc`         datetime(6)   NOT NULL,
  `UpdatedAtUtc`         datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_Referrals_ProviderId`              (`ProviderId`),
  KEY `IX_Referrals_TenantId_Status`         (`TenantId`, `Status`),
  KEY `IX_Referrals_TenantId_ProviderId`     (`TenantId`, `ProviderId`),
  KEY `IX_Referrals_TenantId_CreatedAtUtc`   (`TenantId`, `CreatedAtUtc`),
  CONSTRAINT `FK_Referrals_Providers_ProviderId`
    FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── ReferralStatusHistories ───────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `ReferralStatusHistories` (
  `Id`               char(36)     NOT NULL COLLATE ascii_general_ci,
  `ReferralId`       char(36)     NOT NULL COLLATE ascii_general_ci,
  `TenantId`         char(36)     NOT NULL COLLATE ascii_general_ci,
  `OldStatus`        varchar(20)  NOT NULL,
  `NewStatus`        varchar(20)  NOT NULL,
  `Reason`           varchar(500)     NULL,
  `ChangedByUserId`  char(36)         NULL COLLATE ascii_general_ci,
  `ChangedAtUtc`     datetime(6)  NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ReferralStatusHistories_ReferralId`  (`ReferralId`),
  KEY `IX_ReferralStatusHistories_TenantId`    (`TenantId`),
  CONSTRAINT `FK_ReferralStatusHistories_Referrals_ReferralId`
    FOREIGN KEY (`ReferralId`) REFERENCES `Referrals` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── Facilities ────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `Facilities` (
  `Id`            char(36)      NOT NULL COLLATE ascii_general_ci,
  `TenantId`      char(36)      NOT NULL COLLATE ascii_general_ci,
  `ProviderId`    char(36)      NOT NULL COLLATE ascii_general_ci,
  `Name`          varchar(200)  NOT NULL,
  `AddressLine1`  varchar(300)  NOT NULL,
  `City`          varchar(100)  NOT NULL,
  `State`         varchar(100)  NOT NULL,
  `PostalCode`    varchar(20)   NOT NULL,
  `IsActive`      tinyint(1)    NOT NULL DEFAULT 1,
  `CreatedAtUtc`  datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_Facilities_TenantId_ProviderId` (`TenantId`, `ProviderId`),
  CONSTRAINT `FK_Facilities_Providers_ProviderId`
    FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── ServiceOfferings ──────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `ServiceOfferings` (
  `Id`             char(36)      NOT NULL COLLATE ascii_general_ci,
  `TenantId`       char(36)      NOT NULL COLLATE ascii_general_ci,
  `ProviderId`     char(36)      NOT NULL COLLATE ascii_general_ci,
  `Name`           varchar(200)  NOT NULL,
  `Code`           varchar(50)       NULL,
  `DurationMinutes` int          NOT NULL DEFAULT 60,
  `IsActive`       tinyint(1)    NOT NULL DEFAULT 1,
  `CreatedAtUtc`   datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ServiceOfferings_TenantId_ProviderId` (`TenantId`, `ProviderId`),
  CONSTRAINT `FK_ServiceOfferings_Providers_ProviderId`
    FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── ProviderAvailabilityTemplates ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `ProviderAvailabilityTemplates` (
  `Id`              char(36)     NOT NULL COLLATE ascii_general_ci,
  `TenantId`        char(36)     NOT NULL COLLATE ascii_general_ci,
  `ProviderId`      char(36)     NOT NULL COLLATE ascii_general_ci,
  `FacilityId`      char(36)         NULL COLLATE ascii_general_ci,
  `DayOfWeek`       int          NOT NULL,  -- 0=Sun … 6=Sat
  `StartTimeUtc`    time(6)      NOT NULL,
  `EndTimeUtc`      time(6)      NOT NULL,
  `SlotDuration`    int          NOT NULL DEFAULT 30,  -- minutes
  `IsActive`        tinyint(1)   NOT NULL DEFAULT 1,
  `CreatedAtUtc`    datetime(6)  NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ProviderAvailabilityTemplates_TenantId_ProviderId` (`TenantId`, `ProviderId`),
  CONSTRAINT `FK_ProviderAvailabilityTemplates_Providers_ProviderId`
    FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── ProviderAvailabilityExceptions ───────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `ProviderAvailabilityExceptions` (
  `Id`           char(36)      NOT NULL COLLATE ascii_general_ci,
  `TenantId`     char(36)      NOT NULL COLLATE ascii_general_ci,
  `ProviderId`   char(36)      NOT NULL COLLATE ascii_general_ci,
  `ExceptionDate` date         NOT NULL,
  `Reason`       varchar(500)      NULL,
  `CreatedAtUtc` datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ProviderAvailabilityExceptions_TenantId_ProviderId_Date`
    (`TenantId`, `ProviderId`, `ExceptionDate`),
  CONSTRAINT `FK_ProviderAvailabilityExceptions_Providers_ProviderId`
    FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── AppointmentSlots ──────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `AppointmentSlots` (
  `Id`                              char(36)     NOT NULL COLLATE ascii_general_ci,
  `TenantId`                        char(36)     NOT NULL COLLATE ascii_general_ci,
  `ProviderId`                      char(36)     NOT NULL COLLATE ascii_general_ci,
  `FacilityId`                      char(36)         NULL COLLATE ascii_general_ci,
  `ServiceOfferingId`               char(36)         NULL COLLATE ascii_general_ci,
  `ProviderAvailabilityTemplateId`  char(36)         NULL COLLATE ascii_general_ci,
  `StartAtUtc`                      datetime(6)  NOT NULL,
  `EndAtUtc`                        datetime(6)  NOT NULL,
  `Status`                          varchar(20)  NOT NULL,  -- Available | Booked | Blocked | Cancelled
  `CreatedAtUtc`                    datetime(6)  NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_AppointmentSlots_TenantId_ProviderId_ProvAvailTmpl_Start`
    (`TenantId`, `ProviderId`, `ProviderAvailabilityTemplateId`, `StartAtUtc`),
  KEY `IX_AppointmentSlots_ProviderId`              (`ProviderId`),
  KEY `IX_AppointmentSlots_FacilityId`             (`FacilityId`),
  KEY `IX_AppointmentSlots_ServiceOfferingId`       (`ServiceOfferingId`),
  KEY `IX_AppointmentSlots_ProviderAvailTmplId`    (`ProviderAvailabilityTemplateId`),
  KEY `IX_AppointmentSlots_TenantId_ProviderId_Start` (`TenantId`, `ProviderId`, `StartAtUtc`),
  KEY `IX_AppointmentSlots_TenantId_Facility_Start`  (`TenantId`, `FacilityId`, `StartAtUtc`),
  KEY `IX_AppointmentSlots_TenantId_Service_Start`   (`TenantId`, `ServiceOfferingId`, `StartAtUtc`),
  KEY `IX_AppointmentSlots_TenantId_Status`          (`TenantId`, `Status`),
  CONSTRAINT `FK_AppointmentSlots_Providers_ProviderId`
    FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── Appointments ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `Appointments` (
  `Id`                  char(36)      NOT NULL COLLATE ascii_general_ci,
  `TenantId`            char(36)      NOT NULL COLLATE ascii_general_ci,
  `ReferralId`          char(36)          NULL COLLATE ascii_general_ci,
  `ProviderId`          char(36)      NOT NULL COLLATE ascii_general_ci,
  `SlotId`              char(36)          NULL COLLATE ascii_general_ci,
  `FacilityId`          char(36)          NULL COLLATE ascii_general_ci,
  `ServiceOfferingId`   char(36)          NULL COLLATE ascii_general_ci,
  `ScheduledAtUtc`      datetime(6)   NOT NULL,
  `DurationMinutes`     int           NOT NULL DEFAULT 60,
  `Status`              varchar(20)   NOT NULL,  -- Scheduled | Confirmed | Completed | Cancelled | NoShow
  `PatientFirstName`    varchar(100)  NOT NULL,
  `PatientLastName`     varchar(100)  NOT NULL,
  `BookingOrgId`        char(36)          NULL COLLATE ascii_general_ci,
  `BookingPartyId`      char(36)          NULL COLLATE ascii_general_ci,
  `ReceivingOrgId`      char(36)          NULL COLLATE ascii_general_ci,
  `ReceivingPartyId`    char(36)          NULL COLLATE ascii_general_ci,
  `BookedByUserId`      char(36)      NOT NULL COLLATE ascii_general_ci,
  `CreatedAtUtc`        datetime(6)   NOT NULL,
  `UpdatedAtUtc`        datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_Appointments_TenantId_ProviderId`   (`TenantId`, `ProviderId`),
  KEY `IX_Appointments_TenantId_Status`       (`TenantId`, `Status`),
  KEY `IX_Appointments_TenantId_Scheduled`    (`TenantId`, `ScheduledAtUtc`),
  CONSTRAINT `FK_Appointments_Providers_ProviderId`
    FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_Appointments_Referrals_ReferralId`
    FOREIGN KEY (`ReferralId`) REFERENCES `Referrals` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── AppointmentStatusHistories ────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `AppointmentStatusHistories` (
  `Id`               char(36)     NOT NULL COLLATE ascii_general_ci,
  `AppointmentId`    char(36)     NOT NULL COLLATE ascii_general_ci,
  `TenantId`         char(36)     NOT NULL COLLATE ascii_general_ci,
  `OldStatus`        varchar(20)  NOT NULL,
  `NewStatus`        varchar(20)  NOT NULL,
  `Reason`           varchar(500)     NULL,
  `ChangedByUserId`  char(36)         NULL COLLATE ascii_general_ci,
  `ChangedAtUtc`     datetime(6)  NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_AppointmentStatusHistories_AppointmentId` (`AppointmentId`),
  KEY `IX_AppointmentStatusHistories_TenantId`      (`TenantId`),
  CONSTRAINT `FK_AppointmentStatusHistories_Appointments_AppointmentId`
    FOREIGN KEY (`AppointmentId`) REFERENCES `Appointments` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── ReferralNotes ────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `ReferralNotes` (
  `Id`                  char(36)      NOT NULL COLLATE ascii_general_ci,
  `ReferralId`          char(36)      NOT NULL COLLATE ascii_general_ci,
  `TenantId`            char(36)      NOT NULL COLLATE ascii_general_ci,
  `OwnerOrganizationId` char(36)          NULL COLLATE ascii_general_ci,
  `VisibilityScope`     varchar(20)   NOT NULL DEFAULT 'SHARED',  -- SHARED | PRIVATE
  `Content`             varchar(4000) NOT NULL,
  `CreatedByUserId`     char(36)      NOT NULL COLLATE ascii_general_ci,
  `CreatedAtUtc`        datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ReferralNotes_ReferralId`                          (`ReferralId`),
  KEY `IX_ReferralNotes_ReferralId_Org_Visibility`
    (`ReferralId`, `OwnerOrganizationId`, `VisibilityScope`),
  CONSTRAINT `FK_ReferralNotes_Referrals_ReferralId`
    FOREIGN KEY (`ReferralId`) REFERENCES `Referrals` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── ReferralAttachments ───────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `ReferralAttachments` (
  `Id`               char(36)      NOT NULL COLLATE ascii_general_ci,
  `ReferralId`       char(36)      NOT NULL COLLATE ascii_general_ci,
  `TenantId`         char(36)      NOT NULL COLLATE ascii_general_ci,
  `FileName`         varchar(500)  NOT NULL,
  `StorageUrl`       varchar(2000) NOT NULL,
  `ContentType`      varchar(200)      NULL,
  `FileSizeBytes`    bigint            NULL,
  `UploadedByUserId` char(36)      NOT NULL COLLATE ascii_general_ci,
  `UploadedAtUtc`    datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ReferralAttachments_ReferralId` (`ReferralId`),
  CONSTRAINT `FK_ReferralAttachments_Referrals_ReferralId`
    FOREIGN KEY (`ReferralId`) REFERENCES `Referrals` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── AppointmentNotes ─────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `AppointmentNotes` (
  `Id`                  char(36)      NOT NULL COLLATE ascii_general_ci,
  `AppointmentId`       char(36)      NOT NULL COLLATE ascii_general_ci,
  `OwnerOrganizationId` char(36)          NULL COLLATE ascii_general_ci,
  `VisibilityScope`     varchar(20)   NOT NULL DEFAULT 'SHARED',  -- SHARED | PRIVATE
  `TenantId`            char(36)      NOT NULL COLLATE ascii_general_ci,
  `Content`             varchar(4000) NOT NULL,
  `CreatedByUserId`     char(36)      NOT NULL COLLATE ascii_general_ci,
  `CreatedAtUtc`        datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_AppointmentNotes_AppointmentId`                         (`AppointmentId`),
  KEY `IX_AppointmentNotes_AppointmentId_Org_Visibility`
    (`AppointmentId`, `OwnerOrganizationId`, `VisibilityScope`),
  CONSTRAINT `FK_AppointmentNotes_Appointments_AppointmentId`
    FOREIGN KEY (`AppointmentId`) REFERENCES `Appointments` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── AppointmentAttachments ────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `AppointmentAttachments` (
  `Id`               char(36)      NOT NULL COLLATE ascii_general_ci,
  `AppointmentId`    char(36)      NOT NULL COLLATE ascii_general_ci,
  `TenantId`         char(36)      NOT NULL COLLATE ascii_general_ci,
  `FileName`         varchar(500)  NOT NULL,
  `StorageUrl`       varchar(2000) NOT NULL,
  `ContentType`      varchar(200)      NULL,
  `FileSizeBytes`    bigint            NULL,
  `UploadedByUserId` char(36)      NOT NULL COLLATE ascii_general_ci,
  `UploadedAtUtc`    datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_AppointmentAttachments_AppointmentId` (`AppointmentId`),
  CONSTRAINT `FK_AppointmentAttachments_Appointments_AppointmentId`
    FOREIGN KEY (`AppointmentId`) REFERENCES `Appointments` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── CareConnectNotifications ─────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `CareConnectNotifications` (
  `Id`              char(36)      NOT NULL COLLATE ascii_general_ci,
  `TenantId`        char(36)      NOT NULL COLLATE ascii_general_ci,
  `RecipientUserId` char(36)      NOT NULL COLLATE ascii_general_ci,
  `ResourceType`    varchar(50)   NOT NULL,  -- Referral | Appointment
  `ResourceId`      char(36)      NOT NULL COLLATE ascii_general_ci,
  `NotificationType` varchar(100) NOT NULL,
  `Message`         varchar(1000)     NULL,
  `IsRead`          tinyint(1)    NOT NULL DEFAULT 0,
  `CreatedAtUtc`    datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_CareConnectNotifications_TenantId_Recipient` (`TenantId`, `RecipientUserId`),
  KEY `IX_CareConnectNotifications_IsRead`             (`IsRead`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── Parties (legal/medical parties involved in referrals/appointments) ─────────
CREATE TABLE IF NOT EXISTS `Parties` (
  `Id`            char(36)      NOT NULL COLLATE ascii_general_ci,
  `TenantId`      char(36)      NOT NULL COLLATE ascii_general_ci,
  `OrgId`         char(36)          NULL COLLATE ascii_general_ci,
  `Name`          varchar(300)  NOT NULL,
  `Role`          varchar(100)  NOT NULL,  -- LawFirm | InsuranceCarrier | Claimant | etc.
  `Email`         varchar(320)      NULL,
  `Phone`         varchar(50)       NULL,
  `IsActive`      tinyint(1)    NOT NULL DEFAULT 1,
  `CreatedAtUtc`  datetime(6)   NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_Parties_TenantId_OrgId` (`TenantId`, `OrgId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── EFCore migrations history ─────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
  `MigrationId`    varchar(150) NOT NULL,
  `ProductVersion` varchar(32)  NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;


-- =============================================================================
-- END OF SCHEMA
-- =============================================================================
-- Table summary:
--
-- identity_db (9 tables):
--   Products, Tenants, TenantProducts, Roles, Users, UserRoles,
--   Organizations, OrganizationDomains, OrganizationProducts,
--   ProductRoles, OrgMemberships, AuditLogs, __EFMigrationsHistory
--
-- fund_db (2 tables):
--   Applications, __EFMigrationsHistory
--
-- careconnect_db (16 tables):
--   Categories, Providers, ProviderCategories, Referrals,
--   ReferralStatusHistories, ReferralNotes, ReferralAttachments,
--   Facilities, ServiceOfferings, ProviderAvailabilityTemplates,
--   ProviderAvailabilityExceptions, AppointmentSlots, Appointments,
--   AppointmentStatusHistories, AppointmentNotes, AppointmentAttachments,
--   CareConnectNotifications, Parties, __EFMigrationsHistory
-- =============================================================================
