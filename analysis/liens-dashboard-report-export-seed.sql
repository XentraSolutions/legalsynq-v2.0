-- Seed data for:
--   POST /api/liens/cases/dashboard/lawfirm-case-report-export/v3
--   POST /api/liens/cases/dashboard/medical-provider-report-export/v3
--
-- Apply this after restoring:
--   C:/Users/Serrano/Downloads/dump-liens_db-202606301435.sql
--
-- This script reuses existing dump records:
--   Case: 26-000101 / 8e966eb8-ee0d-49d7-9b38-dff28a808fb9
--   Lien: LN-26-01001 / ede7d668-7c71-4c6d-9f7b-04a9654e0efa

SET @tenant_id = '2ed78610-5bef-4bc3-a43d-9887466335fb';
SET @org_id = 'a4508726-ed74-456a-a905-ff8b079f1f71';
SET @user_id = '517c6c33-b951-49db-92f5-6139104ac61c';

SET @case_id = '8e966eb8-ee0d-49d7-9b38-dff28a808fb9';
SET @lien_id = 'ede7d668-7c71-4c6d-9f7b-04a9654e0efa';

SET @law_firm_id = '40000000-0000-0000-0000-000000000010';
SET @medical_provider_id = '40000000-0000-0000-0000-000000000011';
SET @case_manager_id = '9d566680-d27b-4f3e-a2cb-cd3816829778';
SET @facility_id = '50000000-0000-0000-0000-000000000001';

SET @facility_info_task_id = '71000000-0000-0000-0000-000000000001';
SET @medical_code_task_id = '71000000-0000-0000-0000-000000000002';

INSERT INTO `liens_Contacts`
(
  `Id`, `TenantId`, `OrgId`, `FacilityId`, `LawFirmId`, `ContactType`, `ContactSubtype`,
  `FirstName`, `LastName`, `DisplayName`, `Title`, `Organization`, `Email`, `Phone`,
  `Fax`, `Website`, `AddressLine1`, `City`, `State`, `PostalCode`, `Notes`, `IsActive`,
  `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`
)
VALUES
(
  @law_firm_id, @tenant_id, @org_id, NULL, NULL, 'LawFirm', NULL,
  'Smith', 'Associates', 'Smith Associates', NULL, 'Smith & Associates LLP', 'intake@smithllp.demo', '(702) 555-2100',
  NULL, NULL, '100 Main St', 'Las Vegas', 'NV', '89010', 'Dashboard export seed law firm', 1,
  '2025-07-07 12:00:00.000000', '2025-07-07 12:00:00.000000', @user_id, @user_id
),
(
  @medical_provider_id, @tenant_id, @org_id, NULL, NULL, 'Provider', NULL,
  'City', 'Medical', 'City Medical', NULL, 'City Medical Center', 'records@citymedical.demo', '(702) 555-2200',
  NULL, NULL, '200 Health Ave', 'Las Vegas', 'NV', '89011', 'Dashboard export seed medical provider', 1,
  '2025-07-07 12:05:00.000000', '2025-07-07 12:05:00.000000', @user_id, @user_id
),
(
  @case_manager_id, @tenant_id, @org_id, NULL, @law_firm_id, 'CaseManager', NULL,
  'Taylor', 'Manager', 'Taylor Manager', 'Case Manager', 'Smith & Associates LLP', 'case.manager@smithllp.demo', '(702) 555-2300',
  NULL, NULL, '100 Main St', 'Las Vegas', 'NV', '89010', 'Dashboard export seed case manager', 1,
  '2025-07-07 12:10:00.000000', '2025-07-07 12:10:00.000000', @user_id, @user_id
)
ON DUPLICATE KEY UPDATE
  `OrgId` = VALUES(`OrgId`),
  `FacilityId` = VALUES(`FacilityId`),
  `LawFirmId` = VALUES(`LawFirmId`),
  `ContactType` = VALUES(`ContactType`),
  `ContactSubtype` = VALUES(`ContactSubtype`),
  `FirstName` = VALUES(`FirstName`),
  `LastName` = VALUES(`LastName`),
  `DisplayName` = VALUES(`DisplayName`),
  `Title` = VALUES(`Title`),
  `Organization` = VALUES(`Organization`),
  `Email` = VALUES(`Email`),
  `Phone` = VALUES(`Phone`),
  `AddressLine1` = VALUES(`AddressLine1`),
  `City` = VALUES(`City`),
  `State` = VALUES(`State`),
  `PostalCode` = VALUES(`PostalCode`),
  `Notes` = VALUES(`Notes`),
  `IsActive` = VALUES(`IsActive`),
  `UpdatedAtUtc` = VALUES(`UpdatedAtUtc`),
  `UpdatedByUserId` = VALUES(`UpdatedByUserId`);

INSERT INTO `liens_Facilities`
(
  `Id`, `TenantId`, `OrgId`, `Name`, `Code`, `ExternalReference`, `AddressLine1`, `AddressLine2`,
  `City`, `State`, `PostalCode`, `Phone`, `Email`, `Fax`, `IsActive`, `OrganizationId`,
  `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`
)
VALUES
(
  @facility_id, @tenant_id, @org_id, 'Sunrise Clinic', 'FAC-001', 'DASHBOARD-SEED-FACILITY', '300 Wellness Blvd', NULL,
  'Las Vegas', 'NV', '89012', '(702) 555-2400', 'frontdesk@sunrise.demo', NULL, 1, NULL,
  '2025-07-07 12:15:00.000000', '2025-07-07 12:15:00.000000', @user_id, @user_id
)
ON DUPLICATE KEY UPDATE
  `OrgId` = VALUES(`OrgId`),
  `Name` = VALUES(`Name`),
  `Code` = VALUES(`Code`),
  `ExternalReference` = VALUES(`ExternalReference`),
  `AddressLine1` = VALUES(`AddressLine1`),
  `City` = VALUES(`City`),
  `State` = VALUES(`State`),
  `PostalCode` = VALUES(`PostalCode`),
  `Phone` = VALUES(`Phone`),
  `Email` = VALUES(`Email`),
  `IsActive` = VALUES(`IsActive`),
  `UpdatedAtUtc` = VALUES(`UpdatedAtUtc`),
  `UpdatedByUserId` = VALUES(`UpdatedByUserId`);

UPDATE `liens_Cases`
SET
  `Notes` = 'Client intake completed and documents indexed for sample record 01.

[legacy-meta]
gender=Male; accidentType=Dog Bite; accidentTypeId=DOG-BITE; currentMedicalStatus=Treatment Ongoing; currentMedicalStatusId=TRT-ONGOING; accidentState=NV; trackingFollowUpDate=08/04/2025; leadId=LEAD-2001; caseManagerId=9d566680-d27b-4f3e-a2cb-cd3816829778; caseManager=Taylor Manager; lawFirmId=40000000-0000-0000-0000-000000000010; lawFirm=Smith & Associates LLP',
  `UpdatedAtUtc` = '2025-07-10 14:30:00.000000',
  `UpdatedByUserId` = @user_id
WHERE `Id` = @case_id
  AND `TenantId` = @tenant_id;

UPDATE `liens_Liens`
SET
  `FacilityId` = @facility_id,
  `UpdatedAtUtc` = '2025-07-10 14:35:00.000000',
  `UpdatedByUserId` = @user_id
WHERE `Id` = @lien_id
  AND `TenantId` = @tenant_id;

INSERT INTO `liens_ServicingItems`
(
  `Id`, `TenantId`, `OrgId`, `TaskNumber`, `TaskType`, `Description`, `Status`, `Priority`,
  `AssignedTo`, `AssignedToUserId`, `CaseId`, `LienId`, `DueDate`, `Notes`, `Resolution`,
  `StartedAtUtc`, `CompletedAtUtc`, `EscalatedAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`,
  `CreatedByUserId`, `UpdatedByUserId`
)
VALUES
(
  @facility_info_task_id, @tenant_id, @org_id, 'SVC-DASH-0001', 'LegacyMedicalFacilityInfo',
  'Dashboard export medical provider seed', 'Completed', 'Normal',
  'Codex Seeder', @user_id, @case_id, @lien_id, NULL,
  'facilityId=50000000-0000-0000-0000-000000000001; facilityName=Sunrise Clinic; facilityContactPerson=Alice Nurse; email=frontdesk@sunrise.demo; phone=(702) 555-2400; medicalProviderId=40000000-0000-0000-0000-000000000011; medicalProvider=City Medical Center',
  'Seeded for dashboard report export',
  '2025-07-10 15:00:00.000000', '2025-07-10 15:30:00.000000', NULL, '2025-07-10 15:00:00.000000', '2025-07-10 15:30:00.000000',
  @user_id, @user_id
),
(
  @medical_code_task_id, @tenant_id, @org_id, 'SVC-DASH-0002', 'LegacyMedicalCode',
  'Dashboard export billing seed', 'Completed', 'Normal',
  'Codex Seeder', @user_id, @case_id, @lien_id, NULL,
  'code=12345; medicareCost=75.00; billingAmount=150.00; purchaseAmount=100.00; payee=Health System; outboundCheckNumber=CHK-100',
  'Seeded for dashboard report export',
  '2025-07-10 15:35:00.000000', '2025-07-10 16:00:00.000000', NULL, '2025-07-10 15:35:00.000000', '2025-07-10 16:00:00.000000',
  @user_id, @user_id
)
ON DUPLICATE KEY UPDATE
  `OrgId` = VALUES(`OrgId`),
  `TaskNumber` = VALUES(`TaskNumber`),
  `TaskType` = VALUES(`TaskType`),
  `Description` = VALUES(`Description`),
  `Status` = VALUES(`Status`),
  `Priority` = VALUES(`Priority`),
  `AssignedTo` = VALUES(`AssignedTo`),
  `AssignedToUserId` = VALUES(`AssignedToUserId`),
  `CaseId` = VALUES(`CaseId`),
  `LienId` = VALUES(`LienId`),
  `DueDate` = VALUES(`DueDate`),
  `Notes` = VALUES(`Notes`),
  `Resolution` = VALUES(`Resolution`),
  `StartedAtUtc` = VALUES(`StartedAtUtc`),
  `CompletedAtUtc` = VALUES(`CompletedAtUtc`),
  `EscalatedAtUtc` = VALUES(`EscalatedAtUtc`),
  `UpdatedAtUtc` = VALUES(`UpdatedAtUtc`),
  `UpdatedByUserId` = VALUES(`UpdatedByUserId`);

-- Additional 100 records for pagination and export validation.
DROP TEMPORARY TABLE IF EXISTS `tmp_dashboard_seed_rows`;
CREATE TEMPORARY TABLE `tmp_dashboard_seed_rows`
(
  `seq` int NOT NULL PRIMARY KEY,
  `case_id` char(36) NOT NULL,
  `lien_id` char(36) NOT NULL,
  `facility_task_id` char(36) NOT NULL,
  `medical_code_task_id` char(36) NOT NULL,
  `case_number` varchar(50) NOT NULL,
  `lien_number` varchar(50) NOT NULL,
  `client_first_name` varchar(100) NOT NULL,
  `client_last_name` varchar(100) NOT NULL,
  `case_status` varchar(50) NOT NULL,
  `lien_status` varchar(50) NOT NULL,
  `incident_date` date NOT NULL,
  `opened_at_utc` datetime(6) NOT NULL,
  `created_at_utc` datetime(6) NOT NULL,
  `updated_at_utc` datetime(6) NOT NULL,
  `original_amount` decimal(18,2) NOT NULL,
  `current_balance` decimal(18,2) NOT NULL,
  `purchase_amount` decimal(18,2) NOT NULL,
  `billing_amount` decimal(18,2) NOT NULL
);

INSERT INTO `tmp_dashboard_seed_rows`
(
  `seq`, `case_id`, `lien_id`, `facility_task_id`, `medical_code_task_id`,
  `case_number`, `lien_number`, `client_first_name`, `client_last_name`,
  `case_status`, `lien_status`, `incident_date`, `opened_at_utc`,
  `created_at_utc`, `updated_at_utc`, `original_amount`, `current_balance`,
  `purchase_amount`, `billing_amount`
)
WITH RECURSIVE seq_cte AS
(
  SELECT 1 AS `seq`
  UNION ALL
  SELECT `seq` + 1
  FROM seq_cte
  WHERE `seq` < 100
)
SELECT
  `seq`,
  CONCAT('90000000-0000-0000-0000-', LPAD(`seq`, 12, '0')) AS `case_id`,
  CONCAT('91000000-0000-0000-0000-', LPAD(`seq`, 12, '0')) AS `lien_id`,
  CONCAT('92000000-0000-0000-0000-', LPAD(`seq`, 12, '0')) AS `facility_task_id`,
  CONCAT('93000000-0000-0000-0000-', LPAD(`seq`, 12, '0')) AS `medical_code_task_id`,
  CONCAT('DASH-CASE-', LPAD(`seq`, 4, '0')) AS `case_number`,
  CONCAT('DASH-LIEN-', LPAD(`seq`, 4, '0')) AS `lien_number`,
  CONCAT('Demo', `seq`) AS `client_first_name`,
  CONCAT('Client', `seq`) AS `client_last_name`,
  CASE MOD(`seq`, 4)
    WHEN 0 THEN 'Closed'
    WHEN 1 THEN 'PreDemand'
    WHEN 2 THEN 'DemandSent'
    ELSE 'InNegotiation'
  END AS `case_status`,
  CASE MOD(`seq`, 4)
    WHEN 0 THEN 'Settled'
    WHEN 1 THEN 'Draft'
    WHEN 2 THEN 'Active'
    ELSE 'UnderReview'
  END AS `lien_status`,
  DATE_ADD('2025-09-01', INTERVAL `seq` DAY) AS `incident_date`,
  DATE_ADD('2025-09-01 09:00:00.000000', INTERVAL `seq` DAY) AS `opened_at_utc`,
  DATE_ADD('2025-09-01 12:00:00.000000', INTERVAL `seq` DAY) AS `created_at_utc`,
  DATE_ADD('2025-09-01 15:00:00.000000', INTERVAL `seq` DAY) AS `updated_at_utc`,
  5000.00 + (`seq` * 250.00) AS `original_amount`,
  4500.00 + (`seq` * 225.00) AS `current_balance`,
  100.00 + (`seq` * 10.00) AS `purchase_amount`,
  150.00 + (`seq` * 12.50) AS `billing_amount`
FROM seq_cte;

INSERT INTO `liens_Cases`
(
  `Id`, `TenantId`, `OrgId`, `CaseNumber`, `ExternalReference`, `Title`,
  `ClientFirstName`, `ClientLastName`, `ClientDob`, `ClientPhone`, `ClientEmail`,
  `ClientAddress`, `Status`, `DateOfIncident`, `OpenedAtUtc`, `ClosedAtUtc`,
  `InsuranceCarrier`, `PolicyNumber`, `ClaimNumber`, `DemandAmount`, `SettlementAmount`,
  `Description`, `Notes`, `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`
)
SELECT
  r.`case_id`, @tenant_id, @org_id, r.`case_number`, CONCAT('EXT-', r.`case_number`), CONCAT(r.`case_number`, ' Injury Claim'),
  r.`client_first_name`, r.`client_last_name`, DATE_ADD('1985-01-01', INTERVAL r.`seq` * 30 DAY), CONCAT('(702) 555-', LPAD(3000 + r.`seq`, 4, '0')),
  CONCAT('dashboard+', LPAD(r.`seq`, 4, '0'), '@demo.legalsynq.test'),
  CONCAT(r.`seq`, ' Export Seed St, Las Vegas, NV, ', LPAD(89000 + r.`seq`, 5, '0')),
  r.`case_status`, r.`incident_date`, r.`opened_at_utc`,
  CASE WHEN r.`case_status` = 'Closed' THEN DATE_ADD(r.`opened_at_utc`, INTERVAL 10 DAY) ELSE NULL END,
  'StateShield', CONCAT('POL-DASH-', LPAD(r.`seq`, 4, '0')), CONCAT('CLM-DASH-', LPAD(r.`seq`, 4, '0')),
  r.`original_amount` * 2, CASE WHEN r.`case_status` = 'Closed' THEN r.`original_amount` * 1.5 ELSE NULL END,
  CONCAT('Dashboard export seed case ', LPAD(r.`seq`, 4, '0')),
  CONCAT(
    'Seeded dashboard export case ', LPAD(r.`seq`, 4, '0'), '.',
    '\n\n[legacy-meta]\n',
    'gender=Unknown; accidentType=Auto Accident; accidentTypeId=AUTO-ACC; ',
    'currentMedicalStatus=Treatment Ongoing; currentMedicalStatusId=TRT-ONGOING; ',
    'accidentState=NV; trackingFollowUpDate=10/01/2025; ',
    'leadId=LEAD-DASH-', LPAD(r.`seq`, 4, '0'), '; ',
    'caseManagerId=', @case_manager_id, '; caseManager=Taylor Manager; ',
    'lawFirmId=', @law_firm_id, '; lawFirm=Smith & Associates LLP'
  ),
  r.`created_at_utc`, r.`updated_at_utc`, @user_id, @user_id
FROM `tmp_dashboard_seed_rows` r
ON DUPLICATE KEY UPDATE
  `OrgId` = VALUES(`OrgId`),
  `ExternalReference` = VALUES(`ExternalReference`),
  `Title` = VALUES(`Title`),
  `ClientFirstName` = VALUES(`ClientFirstName`),
  `ClientLastName` = VALUES(`ClientLastName`),
  `ClientDob` = VALUES(`ClientDob`),
  `ClientPhone` = VALUES(`ClientPhone`),
  `ClientEmail` = VALUES(`ClientEmail`),
  `ClientAddress` = VALUES(`ClientAddress`),
  `Status` = VALUES(`Status`),
  `DateOfIncident` = VALUES(`DateOfIncident`),
  `OpenedAtUtc` = VALUES(`OpenedAtUtc`),
  `ClosedAtUtc` = VALUES(`ClosedAtUtc`),
  `InsuranceCarrier` = VALUES(`InsuranceCarrier`),
  `PolicyNumber` = VALUES(`PolicyNumber`),
  `ClaimNumber` = VALUES(`ClaimNumber`),
  `DemandAmount` = VALUES(`DemandAmount`),
  `SettlementAmount` = VALUES(`SettlementAmount`),
  `Description` = VALUES(`Description`),
  `Notes` = VALUES(`Notes`),
  `UpdatedAtUtc` = VALUES(`UpdatedAtUtc`),
  `UpdatedByUserId` = VALUES(`UpdatedByUserId`);

INSERT INTO `liens_Liens`
(
  `Id`, `TenantId`, `OrgId`, `LienNumber`, `ExternalReference`, `LienType`, `Status`,
  `CaseId`, `FacilityId`, `SubjectPartyId`, `SubjectFirstName`, `SubjectLastName`,
  `IsConfidential`, `OriginalAmount`, `CurrentBalance`, `OfferPrice`, `PurchasePrice`,
  `PayoffAmount`, `Jurisdiction`, `Description`, `Notes`, `IncidentDate`, `OpenedAtUtc`,
  `ClosedAtUtc`, `SellingOrgId`, `BuyingOrgId`, `HoldingOrgId`, `CreatedAtUtc`,
  `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`
)
SELECT
  r.`lien_id`, @tenant_id, @org_id, r.`lien_number`, NULL, 'MedicalLien', r.`lien_status`,
  r.`case_id`, @facility_id, NULL, r.`client_first_name`, r.`client_last_name`,
  0, r.`original_amount`, r.`current_balance`, NULL, r.`purchase_amount`,
  NULL, 'Clark County', CONCAT('Dashboard export seed lien ', LPAD(r.`seq`, 4, '0')),
  CONCAT('Seeded dashboard export lien ', LPAD(r.`seq`, 4, '0')),
  r.`incident_date`, r.`opened_at_utc`,
  CASE WHEN r.`lien_status` = 'Settled' THEN DATE_ADD(r.`opened_at_utc`, INTERVAL 12 DAY) ELSE NULL END,
  @org_id, NULL, @org_id, r.`created_at_utc`, r.`updated_at_utc`, @user_id, @user_id
FROM `tmp_dashboard_seed_rows` r
ON DUPLICATE KEY UPDATE
  `OrgId` = VALUES(`OrgId`),
  `LienType` = VALUES(`LienType`),
  `Status` = VALUES(`Status`),
  `CaseId` = VALUES(`CaseId`),
  `FacilityId` = VALUES(`FacilityId`),
  `SubjectFirstName` = VALUES(`SubjectFirstName`),
  `SubjectLastName` = VALUES(`SubjectLastName`),
  `IsConfidential` = VALUES(`IsConfidential`),
  `OriginalAmount` = VALUES(`OriginalAmount`),
  `CurrentBalance` = VALUES(`CurrentBalance`),
  `PurchasePrice` = VALUES(`PurchasePrice`),
  `Jurisdiction` = VALUES(`Jurisdiction`),
  `Description` = VALUES(`Description`),
  `Notes` = VALUES(`Notes`),
  `IncidentDate` = VALUES(`IncidentDate`),
  `OpenedAtUtc` = VALUES(`OpenedAtUtc`),
  `ClosedAtUtc` = VALUES(`ClosedAtUtc`),
  `SellingOrgId` = VALUES(`SellingOrgId`),
  `HoldingOrgId` = VALUES(`HoldingOrgId`),
  `UpdatedAtUtc` = VALUES(`UpdatedAtUtc`),
  `UpdatedByUserId` = VALUES(`UpdatedByUserId`);

INSERT INTO `liens_ServicingItems`
(
  `Id`, `TenantId`, `OrgId`, `TaskNumber`, `TaskType`, `Description`, `Status`, `Priority`,
  `AssignedTo`, `AssignedToUserId`, `CaseId`, `LienId`, `DueDate`, `Notes`, `Resolution`,
  `StartedAtUtc`, `CompletedAtUtc`, `EscalatedAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`,
  `CreatedByUserId`, `UpdatedByUserId`
)
SELECT
  r.`facility_task_id`, @tenant_id, @org_id, CONCAT('SVC-DASH-FAC-', LPAD(r.`seq`, 4, '0')), 'LegacyMedicalFacilityInfo',
  CONCAT('Dashboard export facility seed ', LPAD(r.`seq`, 4, '0')), 'Completed', 'Normal',
  'Codex Seeder', @user_id, r.`case_id`, r.`lien_id`, NULL,
  CONCAT(
    'facilityId=', @facility_id, '; facilityName=Sunrise Clinic; ',
    'facilityContactPerson=Alice Nurse; email=frontdesk@sunrise.demo; phone=(702) 555-2400; ',
    'medicalProviderId=', @medical_provider_id, '; medicalProvider=City Medical Center'
  ),
  'Seeded for dashboard report export',
  r.`created_at_utc`, DATE_ADD(r.`created_at_utc`, INTERVAL 30 MINUTE), NULL, r.`created_at_utc`, DATE_ADD(r.`created_at_utc`, INTERVAL 30 MINUTE),
  @user_id, @user_id
FROM `tmp_dashboard_seed_rows` r
ON DUPLICATE KEY UPDATE
  `OrgId` = VALUES(`OrgId`),
  `TaskNumber` = VALUES(`TaskNumber`),
  `TaskType` = VALUES(`TaskType`),
  `Description` = VALUES(`Description`),
  `Status` = VALUES(`Status`),
  `Priority` = VALUES(`Priority`),
  `AssignedTo` = VALUES(`AssignedTo`),
  `AssignedToUserId` = VALUES(`AssignedToUserId`),
  `CaseId` = VALUES(`CaseId`),
  `LienId` = VALUES(`LienId`),
  `DueDate` = VALUES(`DueDate`),
  `Notes` = VALUES(`Notes`),
  `Resolution` = VALUES(`Resolution`),
  `StartedAtUtc` = VALUES(`StartedAtUtc`),
  `CompletedAtUtc` = VALUES(`CompletedAtUtc`),
  `EscalatedAtUtc` = VALUES(`EscalatedAtUtc`),
  `UpdatedAtUtc` = VALUES(`UpdatedAtUtc`),
  `UpdatedByUserId` = VALUES(`UpdatedByUserId`);

INSERT INTO `liens_ServicingItems`
(
  `Id`, `TenantId`, `OrgId`, `TaskNumber`, `TaskType`, `Description`, `Status`, `Priority`,
  `AssignedTo`, `AssignedToUserId`, `CaseId`, `LienId`, `DueDate`, `Notes`, `Resolution`,
  `StartedAtUtc`, `CompletedAtUtc`, `EscalatedAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`,
  `CreatedByUserId`, `UpdatedByUserId`
)
SELECT
  r.`medical_code_task_id`, @tenant_id, @org_id, CONCAT('SVC-DASH-CODE-', LPAD(r.`seq`, 4, '0')), 'LegacyMedicalCode',
  CONCAT('Dashboard export billing seed ', LPAD(r.`seq`, 4, '0')), 'Completed', 'Normal',
  'Codex Seeder', @user_id, r.`case_id`, r.`lien_id`, NULL,
  CONCAT(
    'code=', LPAD(12000 + r.`seq`, 5, '0'),
    '; medicareCost=', CAST(r.`purchase_amount` * 0.75 AS DECIMAL(18,2)),
    '; billingAmount=', CAST(r.`billing_amount` AS DECIMAL(18,2)),
    '; purchaseAmount=', CAST(r.`purchase_amount` AS DECIMAL(18,2)),
    '; payee=Health System; outboundCheckNumber=CHK-', LPAD(500 + r.`seq`, 4, '0')
  ),
  'Seeded for dashboard report export',
  DATE_ADD(r.`created_at_utc`, INTERVAL 35 MINUTE), DATE_ADD(r.`created_at_utc`, INTERVAL 60 MINUTE), NULL,
  DATE_ADD(r.`created_at_utc`, INTERVAL 35 MINUTE), DATE_ADD(r.`created_at_utc`, INTERVAL 60 MINUTE),
  @user_id, @user_id
FROM `tmp_dashboard_seed_rows` r
ON DUPLICATE KEY UPDATE
  `OrgId` = VALUES(`OrgId`),
  `TaskNumber` = VALUES(`TaskNumber`),
  `TaskType` = VALUES(`TaskType`),
  `Description` = VALUES(`Description`),
  `Status` = VALUES(`Status`),
  `Priority` = VALUES(`Priority`),
  `AssignedTo` = VALUES(`AssignedTo`),
  `AssignedToUserId` = VALUES(`AssignedToUserId`),
  `CaseId` = VALUES(`CaseId`),
  `LienId` = VALUES(`LienId`),
  `DueDate` = VALUES(`DueDate`),
  `Notes` = VALUES(`Notes`),
  `Resolution` = VALUES(`Resolution`),
  `StartedAtUtc` = VALUES(`StartedAtUtc`),
  `CompletedAtUtc` = VALUES(`CompletedAtUtc`),
  `EscalatedAtUtc` = VALUES(`EscalatedAtUtc`),
  `UpdatedAtUtc` = VALUES(`UpdatedAtUtc`),
  `UpdatedByUserId` = VALUES(`UpdatedByUserId`);

DROP TEMPORARY TABLE IF EXISTS `tmp_dashboard_seed_rows`;

-- Quick checks after seeding:
-- POST /api/liens/cases/dashboard/lawfirm-case-report-export/v3
-- Body: {"page":1,"limit":10,"filterType":"lawfirm","filterId":"40000000-0000-0000-0000-000000000010"}
--
-- POST /api/liens/cases/dashboard/medical-provider-report-export/v3
-- Body: {"page":1,"limit":10,"filterType":"medicalProvider","filterId":"40000000-0000-0000-0000-000000000011"}
