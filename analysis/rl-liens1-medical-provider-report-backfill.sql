-- Backfill one medical-provider dashboard export row for the rl-liens1 tenant.
--
-- Why this is needed:
--   The report endpoint /api/liens/cases/dashboard/medical-provider-report-export/v3
--   reads from liens_Cases + liens_Liens + liens_ServicingItems.
--   Contacts alone are not enough to produce rows.
--
-- Target tenant context:
--   tenant_id = 019ea7f6-21e9-7421-ab54-7846cdc6bc76
--   org_id    = 019ea7f6-283d-7891-a78b-3838cdecca0c
--   user_id   = 019ea7f6-284d-7310-9c92-349f2d97b154
--
-- Usage:
--   1. Restore your current liens DB
--   2. Run this script
--   3. Refresh the dashboard export request

SET @tenant_id = '019ea7f6-21e9-7421-ab54-7846cdc6bc76';
SET @org_id    = '019ea7f6-283d-7891-a78b-3838cdecca0c';
SET @user_id   = '019ea7f6-284d-7310-9c92-349f2d97b154';

SET @law_firm_contact_id       = 'b1000000-0000-0000-0000-000000000001';
SET @medical_provider_id       = 'b1000000-0000-0000-0000-000000000011';
SET @case_manager_id           = 'b1000000-0000-0000-0000-000000000036';
SET @facility_contact_id       = '019f2326-6e35-7fc6-ae89-419b9bd91394';
SET @backing_facility_id       = '019f3300-0000-7000-8000-000000000001';

SET @case_id                   = '019f3300-0000-7000-8000-000000000101';
SET @lien_id                   = '019f3300-0000-7000-8000-000000000201';
SET @facility_info_task_id     = '019f3300-0000-7000-8000-000000000301';
SET @medical_code_task_id      = '019f3300-0000-7000-8000-000000000302';

INSERT INTO `liens_Contacts`
(
  `Id`, `TenantId`, `OrgId`, `FacilityId`, `LawFirmId`, `ContactType`, `ContactSubtype`,
  `FirstName`, `LastName`, `DisplayName`, `Title`, `Organization`, `Email`, `Phone`,
  `Fax`, `Website`, `AddressLine1`, `City`, `State`, `PostalCode`, `Notes`, `IsActive`,
  `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`
)
VALUES
(
  @law_firm_contact_id, @tenant_id, @org_id, NULL, NULL, 'LawFirm', NULL,
  'RL1', 'Law', 'RL1 Law', 'Managing Partner', 'RL Injury Law Group 1', 'rl-liens1-contact-001@demo.legalsynq.test', '(702) 555-2001',
  NULL, NULL, '101 Lien Service Rd', 'Henderson', 'NV', '89101', 'Dashboard report backfill law firm', 1,
  '2026-06-02 09:00:00.000000', '2026-07-02 09:00:00.000000', @user_id, @user_id
),
(
  @medical_provider_id, @tenant_id, @org_id, NULL, NULL, 'Provider', NULL,
  'Provider11', 'Medical', 'Provider11 Medical', 'Provider Relations', 'RL Medical Provider Network 01', 'rl-liens1-contact-011@demo.legalsynq.test', '(702) 555-2011',
  NULL, NULL, '111 Lien Service Rd', 'Henderson', 'NV', '89111', 'Dashboard report backfill medical provider', 1,
  '2026-06-12 09:00:00.000000', '2026-07-02 09:00:00.000000', @user_id, @user_id
),
(
  @case_manager_id, @tenant_id, @org_id, NULL, NULL, 'CaseManager', 'CaseManager',
  'Case36', 'Manager', 'Case36 Manager', 'Case Manager', 'RL Liens Case Team 01', 'rl-liens1-contact-036@demo.legalsynq.test', '(702) 555-2036',
  NULL, NULL, '136 Lien Service Rd', 'Henderson', 'NV', '89136', 'Dashboard report backfill case manager', 1,
  '2026-07-07 09:00:00.000000', '2026-07-02 09:00:00.000000', @user_id, @user_id
),
(
  @facility_contact_id, @tenant_id, @org_id, @backing_facility_id, NULL, 'Facility', NULL,
  'facility', 'standalone', 'facility standalone', '', 'facility standalone', '', '',
  NULL, '', NULL, NULL, NULL, NULL, 'Dashboard report backfill facility contact', 1,
  '2026-06-29 17:53:50.600000', '2026-07-02 09:00:00.000000', @user_id, @user_id
)
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

INSERT INTO `liens_Facilities`
(
  `Id`, `TenantId`, `OrgId`, `Name`, `Code`, `ExternalReference`, `AddressLine1`, `AddressLine2`,
  `City`, `State`, `PostalCode`, `Phone`, `Email`, `Fax`, `IsActive`, `OrganizationId`,
  `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`
)
VALUES
(
  @backing_facility_id, @tenant_id, @org_id, 'facility standalone', 'FAC-RL-LIENS1-001', 'RL-LIENS1-DASHBOARD',
  NULL, NULL, NULL, NULL, NULL, '', '', NULL, 1, NULL,
  '2026-06-29 17:54:00.000000', '2026-07-02 09:00:00.000000', @user_id, @user_id
)
ON DUPLICATE KEY UPDATE
  `TenantId`        = VALUES(`TenantId`),
  `OrgId`           = VALUES(`OrgId`),
  `Name`            = VALUES(`Name`),
  `Code`            = VALUES(`Code`),
  `ExternalReference` = VALUES(`ExternalReference`),
  `Phone`           = VALUES(`Phone`),
  `Email`           = VALUES(`Email`),
  `IsActive`        = VALUES(`IsActive`),
  `UpdatedAtUtc`    = VALUES(`UpdatedAtUtc`),
  `UpdatedByUserId` = VALUES(`UpdatedByUserId`);

INSERT INTO `liens_Cases`
(
  `Id`, `TenantId`, `OrgId`, `CaseNumber`, `ExternalReference`, `Title`,
  `ClientFirstName`, `ClientLastName`, `ClientDob`, `ClientPhone`, `ClientEmail`,
  `ClientAddress`, `Status`, `DateOfIncident`, `OpenedAtUtc`, `ClosedAtUtc`,
  `InsuranceCarrier`, `PolicyNumber`, `ClaimNumber`, `DemandAmount`, `SettlementAmount`,
  `Description`, `Notes`, `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`
)
VALUES
(
  @case_id, @tenant_id, @org_id, '26-RL1-0001', 'RL1-DASHBOARD-CASE-001', 'RL Liens1 Dashboard Seed Case',
  'Facility', 'Patient', '1990-01-01', '(702) 555-2401', 'facility.patient@demo.legalsynq.test',
  '201 Dashboard Export Ave, Henderson, NV 89141', 'Open', '2026-06-15', '2026-06-16 09:00:00.000000', NULL,
  'StateShield', 'POL-RL1-0001', 'CLM-RL1-0001', 10000.00, NULL,
  'Backfill seed for medical provider dashboard export.',
  'lawFirmId=b1000000-0000-0000-0000-000000000001; lawFirm=RL Injury Law Group 1; caseManagerId=b1000000-0000-0000-0000-000000000036; caseManager=Case36 Manager; accidentTypeId=MVA; accidentType=Motor Vehicle Accident',
  '2026-06-16 09:00:00.000000', '2026-07-02 09:00:00.000000', @user_id, @user_id
)
ON DUPLICATE KEY UPDATE
  `TenantId`        = VALUES(`TenantId`),
  `OrgId`           = VALUES(`OrgId`),
  `CaseNumber`      = VALUES(`CaseNumber`),
  `ExternalReference` = VALUES(`ExternalReference`),
  `Title`           = VALUES(`Title`),
  `ClientFirstName` = VALUES(`ClientFirstName`),
  `ClientLastName`  = VALUES(`ClientLastName`),
  `ClientDob`       = VALUES(`ClientDob`),
  `ClientPhone`     = VALUES(`ClientPhone`),
  `ClientEmail`     = VALUES(`ClientEmail`),
  `ClientAddress`   = VALUES(`ClientAddress`),
  `Status`          = VALUES(`Status`),
  `DateOfIncident`  = VALUES(`DateOfIncident`),
  `OpenedAtUtc`     = VALUES(`OpenedAtUtc`),
  `ClosedAtUtc`     = VALUES(`ClosedAtUtc`),
  `InsuranceCarrier` = VALUES(`InsuranceCarrier`),
  `PolicyNumber`    = VALUES(`PolicyNumber`),
  `ClaimNumber`     = VALUES(`ClaimNumber`),
  `DemandAmount`    = VALUES(`DemandAmount`),
  `SettlementAmount` = VALUES(`SettlementAmount`),
  `Description`     = VALUES(`Description`),
  `Notes`           = VALUES(`Notes`),
  `UpdatedAtUtc`    = VALUES(`UpdatedAtUtc`),
  `UpdatedByUserId` = VALUES(`UpdatedByUserId`);

INSERT INTO `liens_Liens`
(
  `Id`, `TenantId`, `OrgId`, `LienNumber`, `ExternalReference`, `LienType`, `Status`,
  `CaseId`, `FacilityId`, `SubjectPartyId`, `SubjectFirstName`, `SubjectLastName`,
  `IsConfidential`, `OriginalAmount`, `CurrentBalance`, `OfferPrice`, `PurchasePrice`, `PayoffAmount`,
  `Jurisdiction`, `Description`, `Notes`, `IncidentDate`, `OpenedAtUtc`, `ClosedAtUtc`,
  `SellingOrgId`, `BuyingOrgId`, `HoldingOrgId`, `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`
)
VALUES
(
  @lien_id, @tenant_id, @org_id, 'LIEN-RL1-0001', @medical_provider_id, 'MedicalLien', 'Active',
  @case_id, @backing_facility_id, NULL, 'Facility', 'Patient',
  0, 5000.00, 4500.00, NULL, 3500.00, NULL,
  'NV', 'Backfill seed lien for medical provider dashboard export.', NULL, '2026-06-15', '2026-06-16 10:00:00.000000', NULL,
  @org_id, NULL, @org_id, '2026-06-16 10:00:00.000000', '2026-07-02 09:00:00.000000', @user_id, @user_id
)
ON DUPLICATE KEY UPDATE
  `TenantId`        = VALUES(`TenantId`),
  `OrgId`           = VALUES(`OrgId`),
  `LienNumber`      = VALUES(`LienNumber`),
  `ExternalReference` = VALUES(`ExternalReference`),
  `LienType`        = VALUES(`LienType`),
  `Status`          = VALUES(`Status`),
  `CaseId`          = VALUES(`CaseId`),
  `FacilityId`      = VALUES(`FacilityId`),
  `SubjectFirstName` = VALUES(`SubjectFirstName`),
  `SubjectLastName` = VALUES(`SubjectLastName`),
  `IsConfidential`  = VALUES(`IsConfidential`),
  `OriginalAmount`  = VALUES(`OriginalAmount`),
  `CurrentBalance`  = VALUES(`CurrentBalance`),
  `PurchasePrice`   = VALUES(`PurchasePrice`),
  `Jurisdiction`    = VALUES(`Jurisdiction`),
  `Description`     = VALUES(`Description`),
  `IncidentDate`    = VALUES(`IncidentDate`),
  `OpenedAtUtc`     = VALUES(`OpenedAtUtc`),
  `ClosedAtUtc`     = VALUES(`ClosedAtUtc`),
  `SellingOrgId`    = VALUES(`SellingOrgId`),
  `BuyingOrgId`     = VALUES(`BuyingOrgId`),
  `HoldingOrgId`    = VALUES(`HoldingOrgId`),
  `UpdatedAtUtc`    = VALUES(`UpdatedAtUtc`),
  `UpdatedByUserId` = VALUES(`UpdatedByUserId`);

INSERT INTO `liens_ServicingItems`
(
  `Id`, `TenantId`, `OrgId`, `TaskNumber`, `TaskType`, `Description`, `Status`, `Priority`,
  `AssignedTo`, `AssignedToUserId`, `CaseId`, `LienId`, `DueDate`, `Notes`, `Resolution`,
  `StartedAtUtc`, `CompletedAtUtc`, `EscalatedAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`,
  `CreatedByUserId`, `UpdatedByUserId`
)
VALUES
(
  @facility_info_task_id, @tenant_id, @org_id, 'SVC-RL1-FACILITY-0001', 'LegacyMedicalFacilityInfo',
  'Backfill facility info for medical provider dashboard export', 'Completed', 'Normal',
  'Dashboard Seeder', @user_id, @case_id, @lien_id, NULL,
  'facilityId=019f2326-6e35-7fc6-ae89-419b9bd91394; facilityName=facility standalone; facilityContactPerson=facility staff; email=; phone=; medicalProviderId=b1000000-0000-0000-0000-000000000011; medicalProvider=RL Medical Provider Network 01',
  'Backfill seed for medical provider dashboard export',
  '2026-06-16 11:00:00.000000', '2026-06-16 11:30:00.000000', NULL, '2026-06-16 11:00:00.000000', '2026-07-02 09:00:00.000000',
  @user_id, @user_id
),
(
  @medical_code_task_id, @tenant_id, @org_id, 'SVC-RL1-CODE-0001', 'LegacyMedicalCode',
  'Backfill billing row for medical provider dashboard export', 'Completed', 'Normal',
  'Dashboard Seeder', @user_id, @case_id, @lien_id, NULL,
  'code=12345; medicareCost=75.00; billingAmount=150.00; purchaseAmount=100.00; payee=Health System; outboundCheckNumber=CHK-RL1-0001',
  'Backfill seed for medical provider dashboard export',
  '2026-06-16 11:35:00.000000', '2026-06-16 12:00:00.000000', NULL, '2026-06-16 11:35:00.000000', '2026-07-02 09:00:00.000000',
  @user_id, @user_id
)
ON DUPLICATE KEY UPDATE
  `TenantId`        = VALUES(`TenantId`),
  `OrgId`           = VALUES(`OrgId`),
  `TaskNumber`      = VALUES(`TaskNumber`),
  `TaskType`        = VALUES(`TaskType`),
  `Description`     = VALUES(`Description`),
  `Status`          = VALUES(`Status`),
  `Priority`        = VALUES(`Priority`),
  `AssignedTo`      = VALUES(`AssignedTo`),
  `AssignedToUserId` = VALUES(`AssignedToUserId`),
  `CaseId`          = VALUES(`CaseId`),
  `LienId`          = VALUES(`LienId`),
  `DueDate`         = VALUES(`DueDate`),
  `Notes`           = VALUES(`Notes`),
  `Resolution`      = VALUES(`Resolution`),
  `StartedAtUtc`    = VALUES(`StartedAtUtc`),
  `CompletedAtUtc`  = VALUES(`CompletedAtUtc`),
  `EscalatedAtUtc`  = VALUES(`EscalatedAtUtc`),
  `UpdatedAtUtc`    = VALUES(`UpdatedAtUtc`),
  `UpdatedByUserId` = VALUES(`UpdatedByUserId`);

-- Verification:
-- SELECT COUNT(*) FROM liens_Cases WHERE TenantId = @tenant_id;
-- SELECT COUNT(*) FROM liens_Liens WHERE TenantId = @tenant_id;
-- SELECT COUNT(*) FROM liens_ServicingItems WHERE TenantId = @tenant_id AND LienId = @lien_id;
-- SELECT Id, TaskType, Notes FROM liens_ServicingItems WHERE TenantId = @tenant_id AND LienId = @lien_id;
