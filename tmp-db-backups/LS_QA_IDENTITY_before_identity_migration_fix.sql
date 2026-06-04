-- MySQL dump 10.13  Distrib 8.0.46, for macos26.4 (arm64)
--
-- Host: localhost    Database: LS_QA_IDENTITY
-- ------------------------------------------------------
-- Server version	9.7.0

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;
SET @MYSQLDUMP_TEMP_LOG_BIN = @@SESSION.SQL_LOG_BIN;
SET @@SESSION.SQL_LOG_BIN= 0;

--
-- GTID state at the beginning of the backup 
--

SET @@GLOBAL.GTID_PURGED=/*!80000 '+'*/ '3641d024-4d35-11f1-ac4c-821e0eed7ba2:1-143813';

--
-- Current Database: `LS_QA_IDENTITY`
--

CREATE DATABASE /*!32312 IF NOT EXISTS*/ `LS_QA_IDENTITY` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci */ /*!80016 DEFAULT ENCRYPTION='N' */;

USE `LS_QA_IDENTITY`;

--
-- Table structure for table `__EFMigrationsHistory`
--

DROP TABLE IF EXISTS `__EFMigrationsHistory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `__EFMigrationsHistory` (
  `MigrationId` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ProductVersion` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `__EFMigrationsHistory`
--

LOCK TABLES `__EFMigrationsHistory` WRITE;
/*!40000 ALTER TABLE `__EFMigrationsHistory` DISABLE KEYS */;
INSERT INTO `__EFMigrationsHistory` VALUES ('20260328024003_InitialIdentitySchema','8.0.0'),('20260328200000_AddMultiOrgProductRoleModel','8.0.0'),('20260328200001_SeedAdminOrgMembership','8.0.0'),('20260329000001_AddTenantDomains','8.0.0'),('20260329000002_SeedTenantDomains','8.0.0'),('20260329000003_CorrectSynqLienRoleMappings','8.0.0'),('20260330000001_DropStaleApplicationsTable','8.0.0'),('20260330000002_AddAuditLogsTable','8.0.0'),('20260330000003_SeedAuditLogs','8.0.0'),('20260330000004_SeedCareConnectTestTenants','8.0.0'),('20260330000005_FixCareConnectOrgProducts','8.0.0'),('20260331100000_AddTenantSessionTimeoutMinutes','8.0.0'),('20260401200001_AddTenantLogoDocumentId','8.0.0');
/*!40000 ALTER TABLE `__EFMigrationsHistory` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `AuditLogs`
--

DROP TABLE IF EXISTS `AuditLogs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `AuditLogs` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ActorName` varchar(320) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ActorType` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Action` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `EntityType` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `EntityId` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `MetadataJson` varchar(4000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_AuditLogs_ActorType` (`ActorType`),
  KEY `IX_AuditLogs_CreatedAtUtc` (`CreatedAtUtc`),
  KEY `IX_AuditLogs_EntityType` (`EntityType`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `AuditLogs`
--

LOCK TABLES `AuditLogs` WRITE;
/*!40000 ALTER TABLE `AuditLogs` DISABLE KEYS */;
INSERT INTO `AuditLogs` VALUES ('80000000-0000-0000-0000-000000000001','admin@legalsynq.com','Admin','user.invite','User','o.chen@hartwell.law','{\"tenantCode\":\"HARTWELL\",\"role\":\"CaseManager\"}','2025-03-10 11:05:00.000000'),('80000000-0000-0000-0000-000000000002','admin@legalsynq.com','Admin','user.deactivate','User','a.diallo@meridiancare.com','{\"tenantCode\":\"MERIDIAN\",\"reason\":\"extended-leave\"}','2024-12-10 10:00:00.000000'),('80000000-0000-0000-0000-000000000003','admin@legalsynq.com','Admin','user.lock','User','tanya@bluehavenrecovery.org','{\"tenantCode\":\"BLUEHAVEN\",\"reason\":\"policy-violation\"}','2024-09-15 08:30:00.000000'),('80000000-0000-0000-0000-000000000004','n.patel@legalsynq.com','Admin','user.invite','User','s.kirk@thornfieldlaw.com','{\"tenantCode\":\"THORNFIELD\",\"role\":\"CaseManager\"}','2025-02-20 14:05:00.000000'),('80000000-0000-0000-0000-000000000005','admin@legalsynq.com','Admin','user.lock','User','p.langford@graystonegov.org','{\"tenantCode\":\"GRAYSTONE\",\"reason\":\"account-suspended\"}','2024-10-01 12:10:00.000000'),('80000000-0000-0000-0000-000000000006','n.patel@legalsynq.com','Admin','user.invite','User','y.tanaka@nexushealth.net','{\"tenantCode\":\"NEXUSHEALTH\",\"role\":\"ReadOnly\"}','2025-03-15 11:05:00.000000'),('80000000-0000-0000-0000-000000000007','admin@legalsynq.com','Admin','user.unlock','User','j.whitmore@hartwell.law','{\"tenantCode\":\"HARTWELL\"}','2025-01-15 14:05:00.000000'),('80000000-0000-0000-0000-000000000008','admin@legalsynq.com','Admin','user.password_reset','User','r.moss@pinnaclelegal.com','{\"tenantCode\":\"PINNACLE\",\"method\":\"email-link\"}','2025-03-05 08:15:00.000000'),('80000000-0000-0000-0000-000000000009','admin@legalsynq.com','Admin','tenant.create','Tenant','HARTWELL','{\"tenantType\":\"LawFirm\"}','2024-02-15 08:30:00.000000'),('80000000-0000-0000-0000-000000000010','admin@legalsynq.com','Admin','tenant.create','Tenant','NEXUSHEALTH','{\"tenantType\":\"Provider\"}','2024-06-18 08:45:00.000000'),('80000000-0000-0000-0000-000000000011','admin@legalsynq.com','Admin','tenant.suspend','Tenant','GRAYSTONE','{\"previousStatus\":\"Active\",\"reason\":\"non-payment\"}','2024-10-01 12:00:00.000000'),('80000000-0000-0000-0000-000000000012','admin@legalsynq.com','Admin','tenant.deactivate','Tenant','BLUEHAVEN','{\"previousStatus\":\"Active\"}','2024-09-01 09:00:00.000000'),('80000000-0000-0000-0000-000000000013','n.patel@legalsynq.com','Admin','tenant.create','Tenant','THORNFIELD','{\"tenantType\":\"LawFirm\"}','2024-06-05 11:30:00.000000'),('80000000-0000-0000-0000-000000000014','n.patel@legalsynq.com','Admin','tenant.update','Tenant','MERIDIAN','{\"field\":\"primaryContactEmail\",\"previous\":\"old@meridiancare.com\",\"next\":\"ops@meridiancare.com\"}','2025-01-05 14:30:00.000000'),('80000000-0000-0000-0000-000000000015','admin@legalsynq.com','Admin','entitlement.enable','Entitlement','HARTWELL:SynqFund','{\"tenantCode\":\"HARTWELL\",\"product\":\"SynqFund\"}','2024-02-16 09:00:00.000000'),('80000000-0000-0000-0000-000000000016','admin@legalsynq.com','Admin','entitlement.enable','Entitlement','MERIDIAN:CareConnect','{\"tenantCode\":\"MERIDIAN\",\"product\":\"CareConnect\"}','2024-03-02 10:15:00.000000'),('80000000-0000-0000-0000-000000000017','n.patel@legalsynq.com','Admin','entitlement.disable','Entitlement','BLUEHAVEN:CareConnect','{\"tenantCode\":\"BLUEHAVEN\",\"product\":\"CareConnect\",\"reason\":\"subscription-lapsed\"}','2024-09-02 10:00:00.000000'),('80000000-0000-0000-0000-000000000018','admin@legalsynq.com','Admin','entitlement.enable','Entitlement','THORNFIELD:SynqLien','{\"tenantCode\":\"THORNFIELD\",\"product\":\"SynqLien\"}','2024-06-06 08:00:00.000000'),('80000000-0000-0000-0000-000000000019','n.patel@legalsynq.com','Admin','entitlement.enable','Entitlement','NEXUSHEALTH:SynqRx','{\"tenantCode\":\"NEXUSHEALTH\",\"product\":\"SynqRx\"}','2024-07-01 11:00:00.000000'),('80000000-0000-0000-0000-000000000020','admin@legalsynq.com','Admin','entitlement.disable','Entitlement','GRAYSTONE:SynqBill','{\"tenantCode\":\"GRAYSTONE\",\"product\":\"SynqBill\",\"reason\":\"account-suspended\"}','2024-10-02 08:00:00.000000'),('80000000-0000-0000-0000-000000000021','admin@legalsynq.com','Admin','role.assign','Role','PlatformAdmin','{\"assignedTo\":\"n.patel@legalsynq.com\"}','2024-01-05 08:10:00.000000'),('80000000-0000-0000-0000-000000000022','admin@legalsynq.com','Admin','role.assign','Role','SupportAdmin','{\"assignedTo\":\"support@legalsynq.com\"}','2024-03-15 10:00:00.000000'),('80000000-0000-0000-0000-000000000023','admin@legalsynq.com','Admin','role.revoke','Role','ReadOnly','{\"revokedFrom\":\"temp@legalsynq.com\",\"reason\":\"contract-ended\"}','2024-11-30 17:00:00.000000'),('80000000-0000-0000-0000-000000000024','identity-service','System','system.migration','System','identity-db','{\"migration\":\"20260328200000_AddMultiOrgProductRoleModel\",\"result\":\"applied\"}','2026-03-28 20:00:10.000000'),('80000000-0000-0000-0000-000000000025','identity-service','System','system.health_check','System','identity-service','{\"status\":\"healthy\",\"durationMs\":12}','2025-03-29 06:00:00.000000'),('80000000-0000-0000-0000-000000000026','identity-service','System','user.session_expired','User','p.langford@graystonegov.org','{\"tenantCode\":\"GRAYSTONE\",\"reason\":\"jwt-ttl\"}','2024-09-20 18:00:00.000000'),('80000000-0000-0000-0000-000000000027','admin@legalsynq.com','Admin','tenant.activate','Tenant','PINNACLE','{\"previousStatus\":\"Inactive\"}','2024-04-10 14:30:00.000000'),('80000000-0000-0000-0000-000000000028','n.patel@legalsynq.com','Admin','user.deactivate','User','h.bates@graystonegov.org','{\"tenantCode\":\"GRAYSTONE\",\"reason\":\"account-suspended\"}','2024-09-30 10:05:00.000000');
/*!40000 ALTER TABLE `AuditLogs` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `Capabilities`
--

DROP TABLE IF EXISTS `Capabilities`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Capabilities` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ProductId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Code` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Description` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Capabilities_Code` (`Code`),
  KEY `IX_Capabilities_ProductId` (`ProductId`),
  CONSTRAINT `FK_Capabilities_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Capabilities`
--

LOCK TABLES `Capabilities` WRITE;
/*!40000 ALTER TABLE `Capabilities` DISABLE KEYS */;
INSERT INTO `Capabilities` VALUES ('60000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000003','referral:create','Create Referral','Create a new referral',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000003','referral:read:own','Read Own Referrals','View referrals you initiated',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000003','10000000-0000-0000-0000-000000000003','referral:cancel','Cancel Referral','Cancel a referral you initiated',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000004','10000000-0000-0000-0000-000000000003','referral:read:addressed','Read Addressed Referrals','View referrals addressed to your organization',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000005','10000000-0000-0000-0000-000000000003','referral:accept','Accept Referral','Accept an incoming referral',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000006','10000000-0000-0000-0000-000000000003','referral:decline','Decline Referral','Decline an incoming referral',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000007','10000000-0000-0000-0000-000000000003','provider:search','Search Providers','Search for providers by criteria',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000008','10000000-0000-0000-0000-000000000003','provider:map','View Provider Map','View providers on a geographic map',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000009','10000000-0000-0000-0000-000000000003','appointment:create','Create Appointment','Schedule an appointment',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000010','10000000-0000-0000-0000-000000000003','appointment:update','Update Appointment','Modify an existing appointment',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000011','10000000-0000-0000-0000-000000000003','appointment:read:own','Read Own Appointments','View your organization\'s appointments',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000012','10000000-0000-0000-0000-000000000002','lien:create','Create Lien','Create a new lien record',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000013','10000000-0000-0000-0000-000000000002','lien:offer','Offer Lien','Offer a lien for sale',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000014','10000000-0000-0000-0000-000000000002','lien:read:own','Read Own Liens','View liens you created',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000015','10000000-0000-0000-0000-000000000002','lien:browse','Browse Liens','Browse available liens for purchase',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000016','10000000-0000-0000-0000-000000000002','lien:purchase','Purchase Lien','Purchase a lien',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000017','10000000-0000-0000-0000-000000000002','lien:read:held','Read Held Liens','View liens you hold',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000018','10000000-0000-0000-0000-000000000002','lien:service','Service Lien','Service an active lien',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000019','10000000-0000-0000-0000-000000000002','lien:settle','Settle Lien','Settle and close a lien',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000020','10000000-0000-0000-0000-000000000001','application:create','Create Application','Submit a new fund application',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000021','10000000-0000-0000-0000-000000000001','application:read:own','Read Own Applications','View applications you submitted',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000022','10000000-0000-0000-0000-000000000001','application:cancel','Cancel Application','Cancel a pending application',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000023','10000000-0000-0000-0000-000000000001','application:read:addressed','Read Addressed Applications','View applications addressed to your organization',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000024','10000000-0000-0000-0000-000000000001','application:evaluate','Evaluate Application','Perform underwriting evaluation',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000025','10000000-0000-0000-0000-000000000001','application:approve','Approve Application','Approve and fund an application',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000026','10000000-0000-0000-0000-000000000001','application:decline','Decline Application','Decline a fund application',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000027','10000000-0000-0000-0000-000000000001','party:create','Create Party','Create a party profile for a client',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000028','10000000-0000-0000-0000-000000000001','party:read:own','Read Own Party','View party profiles you created',1,'2024-01-01 00:00:00.000000'),('60000000-0000-0000-0000-000000000029','10000000-0000-0000-0000-000000000001','application:status:view','View Application Status','View the status of a fund application',1,'2024-01-01 00:00:00.000000');
/*!40000 ALTER TABLE `Capabilities` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `OrganizationDomains`
--

DROP TABLE IF EXISTS `OrganizationDomains`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `OrganizationDomains` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `OrganizationId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Domain` varchar(253) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `DomainType` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `IsPrimary` tinyint(1) NOT NULL,
  `IsVerified` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_OrganizationDomains_Domain` (`Domain`),
  KEY `IX_OrganizationDomains_OrganizationId` (`OrganizationId`),
  CONSTRAINT `FK_OrganizationDomains_Organizations_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organizations` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `OrganizationDomains`
--

LOCK TABLES `OrganizationDomains` WRITE;
/*!40000 ALTER TABLE `OrganizationDomains` DISABLE KEYS */;
INSERT INTO `OrganizationDomains` VALUES ('40000000-0000-0000-0000-000000000002','40000000-0000-0000-0000-000000000001','legalsynq.legalsynq.com','SUBDOMAIN',1,1,'2024-01-01 00:00:00.000000');
/*!40000 ALTER TABLE `OrganizationDomains` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `OrganizationProducts`
--

DROP TABLE IF EXISTS `OrganizationProducts`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `OrganizationProducts` (
  `OrganizationId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ProductId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `IsEnabled` tinyint(1) NOT NULL,
  `EnabledAtUtc` datetime(6) DEFAULT NULL,
  `GrantedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`OrganizationId`,`ProductId`),
  KEY `IX_OrganizationProducts_ProductId` (`ProductId`),
  CONSTRAINT `FK_OrganizationProducts_Organizations_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organizations` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_OrganizationProducts_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `OrganizationProducts`
--

LOCK TABLES `OrganizationProducts` WRITE;
/*!40000 ALTER TABLE `OrganizationProducts` DISABLE KEYS */;
INSERT INTO `OrganizationProducts` VALUES ('40000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001',1,'2024-01-01 00:00:00.000000',NULL),('40000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000002',1,'2024-01-01 00:00:00.000000',NULL),('40000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000003',1,'2024-01-01 00:00:00.000000',NULL),('40000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000004',1,'2024-01-01 00:00:00.000000',NULL),('40000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000005',1,'2024-01-01 00:00:00.000000',NULL),('41000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000003',1,'2024-02-16 09:00:00.000000',NULL),('42000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000003',1,'2024-03-02 10:00:00.000000',NULL);
/*!40000 ALTER TABLE `OrganizationProducts` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `Organizations`
--

DROP TABLE IF EXISTS `Organizations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Organizations` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `DisplayName` varchar(300) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `OrgType` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Organizations_TenantId_Name` (`TenantId`,`Name`),
  KEY `IX_Organizations_TenantId_OrgType` (`TenantId`,`OrgType`),
  CONSTRAINT `FK_Organizations_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Organizations`
--

LOCK TABLES `Organizations` WRITE;
/*!40000 ALTER TABLE `Organizations` DISABLE KEYS */;
INSERT INTO `Organizations` VALUES ('40000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001','LegalSynq Platform','LegalSynq Internal','INTERNAL',1,'2024-01-01 00:00:00.000000','2024-01-01 00:00:00.000000',NULL,NULL),('41000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000002','Hartwell & Associates','Hartwell & Associates','LAW_FIRM',1,'2024-02-15 08:30:00.000000','2024-02-15 08:30:00.000000',NULL,NULL),('42000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000003','Meridian Care Group','Meridian Care Group','PROVIDER',1,'2024-03-01 09:00:00.000000','2024-03-01 09:00:00.000000',NULL,NULL);
/*!40000 ALTER TABLE `Organizations` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `ProductRoles`
--

DROP TABLE IF EXISTS `ProductRoles`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `ProductRoles` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ProductId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Code` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Description` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `EligibleOrgType` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ProductRoles_Code` (`Code`),
  KEY `IX_ProductRoles_ProductId_EligibleOrgType` (`ProductId`,`EligibleOrgType`),
  CONSTRAINT `FK_ProductRoles_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `ProductRoles`
--

LOCK TABLES `ProductRoles` WRITE;
/*!40000 ALTER TABLE `ProductRoles` DISABLE KEYS */;
INSERT INTO `ProductRoles` VALUES ('50000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000003','CARECONNECT_REFERRER','CareConnect Referrer','Law firm that refers clients to providers','LAW_FIRM',1,'2024-01-01 00:00:00.000000'),('50000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000003','CARECONNECT_RECEIVER','CareConnect Receiver','Provider that receives referrals','PROVIDER',1,'2024-01-01 00:00:00.000000'),('50000000-0000-0000-0000-000000000003','10000000-0000-0000-0000-000000000002','SYNQLIEN_SELLER','SynqLien Seller','Law firm that creates and offers liens','PROVIDER',1,'2024-01-01 00:00:00.000000'),('50000000-0000-0000-0000-000000000004','10000000-0000-0000-0000-000000000002','SYNQLIEN_BUYER','SynqLien Buyer','Lien owner that purchases liens','LIEN_OWNER',1,'2024-01-01 00:00:00.000000'),('50000000-0000-0000-0000-000000000005','10000000-0000-0000-0000-000000000002','SYNQLIEN_HOLDER','SynqLien Holder','Lien owner that services and settles liens','LIEN_OWNER',1,'2024-01-01 00:00:00.000000'),('50000000-0000-0000-0000-000000000006','10000000-0000-0000-0000-000000000001','SYNQFUND_REFERRER','SynqFund Referrer','Law firm that submits fund applications on behalf of clients','LAW_FIRM',1,'2024-01-01 00:00:00.000000'),('50000000-0000-0000-0000-000000000007','10000000-0000-0000-0000-000000000001','SYNQFUND_FUNDER','SynqFund Funder','Funder that evaluates and funds applications','FUNDER',1,'2024-01-01 00:00:00.000000'),('50000000-0000-0000-0000-000000000008','10000000-0000-0000-0000-000000000001','SYNQFUND_APPLICANT_PORTAL','SynqFund Applicant Portal','Limited read-only portal access for fund applicants',NULL,1,'2024-01-01 00:00:00.000000');
/*!40000 ALTER TABLE `ProductRoles` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `Products`
--

DROP TABLE IF EXISTS `Products`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Products` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Code` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Description` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Products_Code` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Products`
--

LOCK TABLES `Products` WRITE;
/*!40000 ALTER TABLE `Products` DISABLE KEYS */;
INSERT INTO `Products` VALUES ('10000000-0000-0000-0000-000000000001','SynqFund','SYNQ_FUND','Fund management platform',1,'2024-01-01 00:00:00.000000'),('10000000-0000-0000-0000-000000000002','SynqLiens','SYNQ_LIENS','Lien management platform',1,'2024-01-01 00:00:00.000000'),('10000000-0000-0000-0000-000000000003','SynqCareConnect','SYNQ_CARECONNECT','Care coordination platform',1,'2024-01-01 00:00:00.000000'),('10000000-0000-0000-0000-000000000004','SynqPay','SYNQ_PAY','Payment processing platform',1,'2024-01-01 00:00:00.000000'),('10000000-0000-0000-0000-000000000005','SynqAI','SYNQ_AI','AI-powered legal intelligence platform',1,'2024-01-01 00:00:00.000000');
/*!40000 ALTER TABLE `Products` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `RoleCapabilities`
--

DROP TABLE IF EXISTS `RoleCapabilities`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `RoleCapabilities` (
  `ProductRoleId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `CapabilityId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  PRIMARY KEY (`ProductRoleId`,`CapabilityId`),
  KEY `IX_RoleCapabilities_CapabilityId` (`CapabilityId`),
  CONSTRAINT `FK_RoleCapabilities_Capabilities_CapabilityId` FOREIGN KEY (`CapabilityId`) REFERENCES `Capabilities` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_RoleCapabilities_ProductRoles_ProductRoleId` FOREIGN KEY (`ProductRoleId`) REFERENCES `ProductRoles` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `RoleCapabilities`
--

LOCK TABLES `RoleCapabilities` WRITE;
/*!40000 ALTER TABLE `RoleCapabilities` DISABLE KEYS */;
INSERT INTO `RoleCapabilities` VALUES ('50000000-0000-0000-0000-000000000001','60000000-0000-0000-0000-000000000001'),('50000000-0000-0000-0000-000000000001','60000000-0000-0000-0000-000000000002'),('50000000-0000-0000-0000-000000000001','60000000-0000-0000-0000-000000000003'),('50000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000004'),('50000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000005'),('50000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000006'),('50000000-0000-0000-0000-000000000001','60000000-0000-0000-0000-000000000007'),('50000000-0000-0000-0000-000000000001','60000000-0000-0000-0000-000000000008'),('50000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000009'),('50000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000010'),('50000000-0000-0000-0000-000000000001','60000000-0000-0000-0000-000000000011'),('50000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000011'),('50000000-0000-0000-0000-000000000003','60000000-0000-0000-0000-000000000012'),('50000000-0000-0000-0000-000000000003','60000000-0000-0000-0000-000000000013'),('50000000-0000-0000-0000-000000000003','60000000-0000-0000-0000-000000000014'),('50000000-0000-0000-0000-000000000004','60000000-0000-0000-0000-000000000015'),('50000000-0000-0000-0000-000000000004','60000000-0000-0000-0000-000000000016'),('50000000-0000-0000-0000-000000000004','60000000-0000-0000-0000-000000000017'),('50000000-0000-0000-0000-000000000005','60000000-0000-0000-0000-000000000017'),('50000000-0000-0000-0000-000000000005','60000000-0000-0000-0000-000000000018'),('50000000-0000-0000-0000-000000000005','60000000-0000-0000-0000-000000000019'),('50000000-0000-0000-0000-000000000006','60000000-0000-0000-0000-000000000020'),('50000000-0000-0000-0000-000000000006','60000000-0000-0000-0000-000000000021'),('50000000-0000-0000-0000-000000000006','60000000-0000-0000-0000-000000000022'),('50000000-0000-0000-0000-000000000007','60000000-0000-0000-0000-000000000023'),('50000000-0000-0000-0000-000000000007','60000000-0000-0000-0000-000000000024'),('50000000-0000-0000-0000-000000000007','60000000-0000-0000-0000-000000000025'),('50000000-0000-0000-0000-000000000007','60000000-0000-0000-0000-000000000026'),('50000000-0000-0000-0000-000000000006','60000000-0000-0000-0000-000000000027'),('50000000-0000-0000-0000-000000000006','60000000-0000-0000-0000-000000000028'),('50000000-0000-0000-0000-000000000008','60000000-0000-0000-0000-000000000028'),('50000000-0000-0000-0000-000000000008','60000000-0000-0000-0000-000000000029');
/*!40000 ALTER TABLE `RoleCapabilities` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `Roles`
--

DROP TABLE IF EXISTS `Roles`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Roles` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Description` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `IsSystemRole` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Roles_TenantId_Name` (`TenantId`,`Name`),
  CONSTRAINT `FK_Roles_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Roles`
--

LOCK TABLES `Roles` WRITE;
/*!40000 ALTER TABLE `Roles` DISABLE KEYS */;
INSERT INTO `Roles` VALUES ('30000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001','PlatformAdmin','Full platform administration access',1,'2024-01-01 00:00:00.000000','2024-01-01 00:00:00.000000'),('30000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000001','TenantAdmin','Tenant-level administration access',1,'2024-01-01 00:00:00.000000','2024-01-01 00:00:00.000000'),('30000000-0000-0000-0000-000000000003','20000000-0000-0000-0000-000000000001','StandardUser','Standard user access',1,'2024-01-01 00:00:00.000000','2024-01-01 00:00:00.000000'),('31000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000002','TenantAdmin','Tenant-level administration access',1,'2024-02-15 08:30:00.000000','2024-02-15 08:30:00.000000'),('31000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000002','StandardUser','Standard user access',1,'2024-02-15 08:30:00.000000','2024-02-15 08:30:00.000000'),('32000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000003','TenantAdmin','Tenant-level administration access',1,'2024-03-01 09:00:00.000000','2024-03-01 09:00:00.000000'),('32000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000003','StandardUser','Standard user access',1,'2024-03-01 09:00:00.000000','2024-03-01 09:00:00.000000');
/*!40000 ALTER TABLE `Roles` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `TenantDomains`
--

DROP TABLE IF EXISTS `TenantDomains`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `TenantDomains` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Domain` varchar(253) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `DomainType` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `IsPrimary` tinyint(1) NOT NULL,
  `IsVerified` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_TenantDomains_Domain` (`Domain`),
  KEY `IX_TenantDomains_TenantId` (`TenantId`),
  KEY `IX_TenantDomains_TenantId_IsPrimary` (`TenantId`,`IsPrimary`),
  CONSTRAINT `FK_TenantDomains_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `TenantDomains`
--

LOCK TABLES `TenantDomains` WRITE;
/*!40000 ALTER TABLE `TenantDomains` DISABLE KEYS */;
INSERT INTO `TenantDomains` VALUES ('70000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001','legalsynq.legalsynq.com','SUBDOMAIN',1,1,'2024-01-01 00:00:00.000000');
/*!40000 ALTER TABLE `TenantDomains` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `TenantProducts`
--

DROP TABLE IF EXISTS `TenantProducts`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `TenantProducts` (
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `ProductId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `IsEnabled` tinyint(1) NOT NULL,
  `EnabledAtUtc` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`TenantId`,`ProductId`),
  KEY `IX_TenantProducts_ProductId` (`ProductId`),
  CONSTRAINT `FK_TenantProducts_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_TenantProducts_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `TenantProducts`
--

LOCK TABLES `TenantProducts` WRITE;
/*!40000 ALTER TABLE `TenantProducts` DISABLE KEYS */;
INSERT INTO `TenantProducts` VALUES ('20000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000003',1,'2024-02-16 09:00:00.000000'),('20000000-0000-0000-0000-000000000003','10000000-0000-0000-0000-000000000003',1,'2024-03-02 10:00:00.000000');
/*!40000 ALTER TABLE `TenantProducts` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `Tenants`
--

DROP TABLE IF EXISTS `Tenants`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Tenants` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Code` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `SessionTimeoutMinutes` int DEFAULT NULL,
  `LogoDocumentId` char(36) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Tenants_Code` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Tenants`
--

LOCK TABLES `Tenants` WRITE;
/*!40000 ALTER TABLE `Tenants` DISABLE KEYS */;
INSERT INTO `Tenants` VALUES ('20000000-0000-0000-0000-000000000001','LegalSynq Internal','LEGALSYNQ',1,'2024-01-01 00:00:00.000000','2024-01-01 00:00:00.000000',NULL,NULL),('20000000-0000-0000-0000-000000000002','Hartwell & Associates','HARTWELL',1,'2024-02-15 08:30:00.000000','2024-02-15 08:30:00.000000',NULL,NULL),('20000000-0000-0000-0000-000000000003','Meridian Care Group','MERIDIAN',1,'2024-03-01 09:00:00.000000','2024-03-01 09:00:00.000000',NULL,NULL);
/*!40000 ALTER TABLE `Tenants` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `UserOrganizationMemberships`
--

DROP TABLE IF EXISTS `UserOrganizationMemberships`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `UserOrganizationMemberships` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `UserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `OrganizationId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `MemberRole` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `JoinedAtUtc` datetime(6) NOT NULL,
  `GrantedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_UserOrganizationMemberships_UserId_OrganizationId` (`UserId`,`OrganizationId`),
  KEY `IX_UserOrganizationMemberships_OrganizationId` (`OrganizationId`),
  KEY `IX_UserOrganizationMemberships_UserId_IsActive` (`UserId`,`IsActive`),
  CONSTRAINT `FK_UserOrganizationMemberships_Organizations_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organizations` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_UserOrganizationMemberships_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `UserOrganizationMemberships`
--

LOCK TABLES `UserOrganizationMemberships` WRITE;
/*!40000 ALTER TABLE `UserOrganizationMemberships` DISABLE KEYS */;
INSERT INTO `UserOrganizationMemberships` VALUES ('41000000-0000-0000-0000-000000000002','21000000-0000-0000-0000-000000000001','41000000-0000-0000-0000-000000000001','ADMIN',1,'2024-02-15 08:30:00.000000',NULL),('41000000-0000-0000-0000-000000000003','21000000-0000-0000-0000-000000000002','41000000-0000-0000-0000-000000000001','MEMBER',1,'2024-02-16 09:00:00.000000','21000000-0000-0000-0000-000000000001'),('41000000-0000-0000-0000-000000000004','21000000-0000-0000-0000-000000000003','41000000-0000-0000-0000-000000000001','MEMBER',1,'2024-02-17 09:30:00.000000','21000000-0000-0000-0000-000000000001'),('42000000-0000-0000-0000-000000000002','22000000-0000-0000-0000-000000000001','42000000-0000-0000-0000-000000000001','ADMIN',1,'2024-03-01 09:00:00.000000',NULL),('42000000-0000-0000-0000-000000000003','22000000-0000-0000-0000-000000000002','42000000-0000-0000-0000-000000000001','MEMBER',1,'2024-03-02 09:00:00.000000','22000000-0000-0000-0000-000000000001');
/*!40000 ALTER TABLE `UserOrganizationMemberships` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `UserRoleAssignments`
--

DROP TABLE IF EXISTS `UserRoleAssignments`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `UserRoleAssignments` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `UserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `RoleId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `OrganizationId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  `AssignedAtUtc` datetime(6) NOT NULL,
  `AssignedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_UserRoleAssignments_UserId_RoleId_OrganizationId` (`UserId`,`RoleId`,`OrganizationId`),
  KEY `IX_UserRoleAssignments_RoleId` (`RoleId`),
  KEY `IX_UserRoleAssignments_OrganizationId` (`OrganizationId`),
  CONSTRAINT `FK_UserRoleAssignments_Organizations_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organizations` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_UserRoleAssignments_Roles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `Roles` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_UserRoleAssignments_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `UserRoleAssignments`
--

LOCK TABLES `UserRoleAssignments` WRITE;
/*!40000 ALTER TABLE `UserRoleAssignments` DISABLE KEYS */;
/*!40000 ALTER TABLE `UserRoleAssignments` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `UserRoles`
--

DROP TABLE IF EXISTS `UserRoles`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `UserRoles` (
  `UserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `RoleId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `AssignedAtUtc` datetime(6) NOT NULL,
  PRIMARY KEY (`UserId`,`RoleId`),
  KEY `IX_UserRoles_RoleId` (`RoleId`),
  CONSTRAINT `FK_UserRoles_Roles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `Roles` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_UserRoles_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `UserRoles`
--

LOCK TABLES `UserRoles` WRITE;
/*!40000 ALTER TABLE `UserRoles` DISABLE KEYS */;
INSERT INTO `UserRoles` VALUES ('21000000-0000-0000-0000-000000000001','31000000-0000-0000-0000-000000000001','2024-02-15 08:30:00.000000'),('21000000-0000-0000-0000-000000000002','31000000-0000-0000-0000-000000000002','2024-02-16 09:00:00.000000'),('21000000-0000-0000-0000-000000000003','31000000-0000-0000-0000-000000000002','2024-02-17 09:30:00.000000'),('22000000-0000-0000-0000-000000000001','32000000-0000-0000-0000-000000000001','2024-03-01 09:00:00.000000'),('22000000-0000-0000-0000-000000000002','32000000-0000-0000-0000-000000000002','2024-03-02 09:00:00.000000');
/*!40000 ALTER TABLE `UserRoles` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `Users`
--

DROP TABLE IF EXISTS `Users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Users` (
  `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `Email` varchar(320) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `PasswordHash` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `FirstName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `LastName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `IsActive` tinyint(1) NOT NULL,
  `CreatedAtUtc` datetime(6) NOT NULL,
  `UpdatedAtUtc` datetime(6) NOT NULL,
  `IsLocked` tinyint(1) NOT NULL DEFAULT '0',
  `LockedAtUtc` datetime(6) DEFAULT NULL,
  `LockedByAdminId` char(36) DEFAULT NULL,
  `LastLoginAtUtc` datetime(6) DEFAULT NULL,
  `SessionVersion` int NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Users_TenantId_Email` (`TenantId`,`Email`),
  CONSTRAINT `FK_Users_Tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `Tenants` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `Users`
--

LOCK TABLES `Users` WRITE;
/*!40000 ALTER TABLE `Users` DISABLE KEYS */;
INSERT INTO `Users` VALUES ('21000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000002','margaret@hartwell.law','$2a$12$FhcogSUbGGiLl/sRLJxylOFE.UJU2i5rACVAyO4wiX7jYxxEnuGkS','Margaret','Hartwell',1,'2024-02-15 08:30:00.000000','2024-02-15 08:30:00.000000',0,NULL,NULL,NULL,0),('21000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000002','james.whitmore@hartwell.law','$2a$12$FhcogSUbGGiLl/sRLJxylOFE.UJU2i5rACVAyO4wiX7jYxxEnuGkS','James','Whitmore',1,'2024-02-16 09:00:00.000000','2024-02-16 09:00:00.000000',0,NULL,NULL,NULL,0),('21000000-0000-0000-0000-000000000003','20000000-0000-0000-0000-000000000002','olivia.chen@hartwell.law','$2a$12$FhcogSUbGGiLl/sRLJxylOFE.UJU2i5rACVAyO4wiX7jYxxEnuGkS','Olivia','Chen',1,'2024-02-17 09:30:00.000000','2024-02-17 09:30:00.000000',0,NULL,NULL,NULL,0),('22000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000003','dr.ramirez@meridiancare.com','$2a$12$CIXHD3tNU7bpPleD5a0fn.aNNcA1uuNo/7btu43Brwt06ciQHv2uS','Elena','Ramirez',1,'2024-03-01 09:00:00.000000','2024-03-01 09:00:00.000000',0,NULL,NULL,NULL,0),('22000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000003','alex.diallo@meridiancare.com','$2a$12$CIXHD3tNU7bpPleD5a0fn.aNNcA1uuNo/7btu43Brwt06ciQHv2uS','Alex','Diallo',1,'2024-03-02 09:00:00.000000','2024-03-02 09:00:00.000000',0,NULL,NULL,NULL,0);
/*!40000 ALTER TABLE `Users` ENABLE KEYS */;
UNLOCK TABLES;
SET @@SESSION.SQL_LOG_BIN = @MYSQLDUMP_TEMP_LOG_BIN;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2026-06-04 17:45:36
