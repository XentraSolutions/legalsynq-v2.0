-- Seed contacts for the live tenant context shown in the rl-liens1 session token.
--
-- Target request:
--   GET /api/lien/api/liens/contacts?pageSize=100
--
-- Tenant context from platform_session:
--   tenant_id = 019ea7f6-21e9-7421-ab54-7846cdc6bc76
--   org_id    = 019ea7f6-283d-7891-a78b-3838cdecca0c
--   user_id   = 019ea7f6-284d-7310-9c92-349f2d97b154
--
-- Usage:
--   1. Restore your current database
--   2. Run this script
--   3. Refresh http://rl-liens1.localhost:3000/lien/contacts

SET @tenant_id = '019ea7f6-21e9-7421-ab54-7846cdc6bc76';
SET @org_id    = '019ea7f6-283d-7891-a78b-3838cdecca0c';
SET @user_id   = '019ea7f6-284d-7310-9c92-349f2d97b154';

DROP TEMPORARY TABLE IF EXISTS `tmp_rl_liens1_contacts`;
CREATE TEMPORARY TABLE `tmp_rl_liens1_contacts`
(
  `seq` int NOT NULL PRIMARY KEY,
  `id` char(36) NOT NULL,
  `contact_type` varchar(50) NOT NULL,
  `contact_subtype` varchar(50) NULL,
  `law_firm_id` char(36) NULL,
  `first_name` varchar(100) NOT NULL,
  `last_name` varchar(100) NOT NULL,
  `display_name` varchar(250) NOT NULL,
  `title` varchar(100) NULL,
  `organization` varchar(200) NULL,
  `email` varchar(320) NULL,
  `phone` varchar(30) NULL,
  `address_line1` varchar(300) NULL,
  `city` varchar(100) NULL,
  `state` varchar(100) NULL,
  `postal_code` varchar(20) NULL,
  `notes` varchar(4000) NULL,
  `created_at_utc` datetime(6) NOT NULL,
  `updated_at_utc` datetime(6) NOT NULL
);

INSERT INTO `tmp_rl_liens1_contacts`
(
  `seq`, `id`, `contact_type`, `contact_subtype`, `law_firm_id`,
  `first_name`, `last_name`, `display_name`, `title`, `organization`,
  `email`, `phone`, `address_line1`, `city`, `state`, `postal_code`,
  `notes`, `created_at_utc`, `updated_at_utc`
)
WITH RECURSIVE seq_cte AS
(
  SELECT 1 AS `seq`
  UNION ALL
  SELECT `seq` + 1
  FROM seq_cte
  WHERE `seq` < 50
)
SELECT
  s.`seq`,
  CONCAT('b1000000-0000-0000-0000-', LPAD(s.`seq`, 12, '0')) AS `id`,
  CASE
    WHEN s.`seq` BETWEEN 1  AND 10 THEN 'LawFirm'
    WHEN s.`seq` BETWEEN 11 AND 25 THEN 'Provider'
    WHEN s.`seq` BETWEEN 26 AND 35 THEN 'LienHolder'
    WHEN s.`seq` BETWEEN 36 AND 45 THEN 'CaseManager'
    ELSE 'InternalUser'
  END AS `contact_type`,
  CASE
    WHEN s.`seq` BETWEEN 6  AND 10 THEN 'Attorney'
    WHEN s.`seq` BETWEEN 36 AND 40 THEN 'CaseManager'
    ELSE NULL
  END AS `contact_subtype`,
  CASE
    WHEN s.`seq` BETWEEN 6 AND 10 THEN CONCAT('b1000000-0000-0000-0000-', LPAD(s.`seq` - 5, 12, '0'))
    ELSE NULL
  END AS `law_firm_id`,
  CASE
    WHEN s.`seq` BETWEEN 1  AND 10 THEN CONCAT('RL', s.`seq`)
    WHEN s.`seq` BETWEEN 11 AND 25 THEN CONCAT('Provider', s.`seq`)
    WHEN s.`seq` BETWEEN 26 AND 35 THEN CONCAT('Funding', s.`seq`)
    WHEN s.`seq` BETWEEN 36 AND 45 THEN CONCAT('Case', s.`seq`)
    ELSE CONCAT('Internal', s.`seq`)
  END AS `first_name`,
  CASE
    WHEN s.`seq` BETWEEN 1  AND 10 THEN 'Law'
    WHEN s.`seq` BETWEEN 11 AND 25 THEN 'Medical'
    WHEN s.`seq` BETWEEN 26 AND 35 THEN 'Capital'
    WHEN s.`seq` BETWEEN 36 AND 45 THEN 'Manager'
    ELSE 'Ops'
  END AS `last_name`,
  CASE
    WHEN s.`seq` BETWEEN 1  AND 10 THEN CONCAT('RL', s.`seq`, ' Law')
    WHEN s.`seq` BETWEEN 11 AND 25 THEN CONCAT('Provider', s.`seq`, ' Medical')
    WHEN s.`seq` BETWEEN 26 AND 35 THEN CONCAT('Funding', s.`seq`, ' Capital')
    WHEN s.`seq` BETWEEN 36 AND 45 THEN CONCAT('Case', s.`seq`, ' Manager')
    ELSE CONCAT('Internal', s.`seq`, ' Ops')
  END AS `display_name`,
  CASE
    WHEN s.`seq` BETWEEN 1  AND 5  THEN 'Managing Partner'
    WHEN s.`seq` BETWEEN 6  AND 10 THEN 'Attorney'
    WHEN s.`seq` BETWEEN 11 AND 25 THEN 'Provider Relations'
    WHEN s.`seq` BETWEEN 26 AND 35 THEN 'Funding Director'
    WHEN s.`seq` BETWEEN 36 AND 45 THEN 'Case Manager'
    ELSE 'Operations Analyst'
  END AS `title`,
  CASE
    WHEN s.`seq` BETWEEN 1  AND 5  THEN CONCAT('RL Injury Law Group ', s.`seq`)
    WHEN s.`seq` BETWEEN 6  AND 10 THEN CONCAT('RL Injury Law Group ', s.`seq` - 5)
    WHEN s.`seq` BETWEEN 11 AND 25 THEN CONCAT('RL Medical Provider Network ', LPAD(s.`seq` - 10, 2, '0'))
    WHEN s.`seq` BETWEEN 26 AND 35 THEN CONCAT('RL Funding Partners ', LPAD(s.`seq` - 25, 2, '0'))
    WHEN s.`seq` BETWEEN 36 AND 45 THEN CONCAT('RL Liens Case Team ', LPAD(s.`seq` - 35, 2, '0'))
    ELSE 'RL Liens Operations'
  END AS `organization`,
  CONCAT('rl-liens1-contact-', LPAD(s.`seq`, 3, '0'), '@demo.legalsynq.test') AS `email`,
  CONCAT('(702) 555-', LPAD(2000 + s.`seq`, 4, '0')) AS `phone`,
  CONCAT(100 + s.`seq`, ' Lien Service Rd') AS `address_line1`,
  CASE MOD(s.`seq`, 5)
    WHEN 0 THEN 'Las Vegas'
    WHEN 1 THEN 'Henderson'
    WHEN 2 THEN 'Reno'
    WHEN 3 THEN 'Phoenix'
    ELSE 'Los Angeles'
  END AS `city`,
  CASE MOD(s.`seq`, 5)
    WHEN 0 THEN 'NV'
    WHEN 1 THEN 'NV'
    WHEN 2 THEN 'NV'
    WHEN 3 THEN 'AZ'
    ELSE 'CA'
  END AS `state`,
  LPAD(89100 + s.`seq`, 5, '0') AS `postal_code`,
  CONCAT('Seeded for rl-liens1 contacts page. seq=', s.`seq`) AS `notes`,
  DATE_ADD('2026-06-01 09:00:00.000000', INTERVAL s.`seq` DAY) AS `created_at_utc`,
  DATE_ADD('2026-06-01 14:00:00.000000', INTERVAL s.`seq` DAY) AS `updated_at_utc`
FROM seq_cte s;

INSERT INTO `liens_Contacts`
(
  `Id`, `TenantId`, `OrgId`, `FacilityId`, `LawFirmId`, `ContactType`, `ContactSubtype`,
  `FirstName`, `LastName`, `DisplayName`, `Title`, `Organization`, `Email`, `Phone`,
  `Fax`, `Website`, `AddressLine1`, `City`, `State`, `PostalCode`, `Notes`, `IsActive`,
  `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`
)
SELECT
  c.`id`, @tenant_id, @org_id, NULL, c.`law_firm_id`, c.`contact_type`, c.`contact_subtype`,
  c.`first_name`, c.`last_name`, c.`display_name`, c.`title`, c.`organization`, c.`email`, c.`phone`,
  NULL, NULL, c.`address_line1`, c.`city`, c.`state`, c.`postal_code`, c.`notes`, 1,
  c.`created_at_utc`, c.`updated_at_utc`, @user_id, @user_id
FROM `tmp_rl_liens1_contacts` c
ON DUPLICATE KEY UPDATE
  `TenantId`        = VALUES(`TenantId`),
  `OrgId`           = VALUES(`OrgId`),
  `FacilityId`      = VALUES(`FacilityId`),
  `LawFirmId`       = VALUES(`LawFirmId`),
  `ContactType`     = VALUES(`ContactType`),
  `ContactSubtype`  = VALUES(`ContactSubtype`),
  `FirstName`       = VALUES(`FirstName`),
  `LastName`        = VALUES(`LastName`),
  `DisplayName`     = VALUES(`DisplayName`),
  `Title`           = VALUES(`Title`),
  `Organization`    = VALUES(`Organization`),
  `Email`           = VALUES(`Email`),
  `Phone`           = VALUES(`Phone`),
  `AddressLine1`    = VALUES(`AddressLine1`),
  `City`            = VALUES(`City`),
  `State`           = VALUES(`State`),
  `PostalCode`      = VALUES(`PostalCode`),
  `Notes`           = VALUES(`Notes`),
  `IsActive`        = VALUES(`IsActive`),
  `UpdatedAtUtc`    = VALUES(`UpdatedAtUtc`),
  `UpdatedByUserId` = VALUES(`UpdatedByUserId`);

DROP TEMPORARY TABLE IF EXISTS `tmp_rl_liens1_contacts`;

-- Verification queries:
-- SELECT COUNT(*) FROM liens_Contacts WHERE TenantId = '019ea7f6-21e9-7421-ab54-7846cdc6bc76';
-- SELECT Id, ContactType, ContactSubtype, DisplayName, Organization
-- FROM liens_Contacts
-- WHERE TenantId = '019ea7f6-21e9-7421-ab54-7846cdc6bc76'
-- ORDER BY DisplayName
-- LIMIT 20;
