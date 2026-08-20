-- SL-CORE core import for tenant/org 019ea7f6-21e9-7421-ab54-7846cdc6bc76.
--
-- MySQL 8.0+ only. Connect to the TARGET LiensDb schema before running this
-- script. It expects a read-only, controlled staging restore in the same MySQL
-- server under the exact schema name `SL-CORE`. Change only that quoted schema
-- name if the staging restore has a different approved name.
--
-- Scope: cases, medical-lien headers, and case notes. It never imports legacy
-- credentials, documents, facilities, detailed medical-code lines, payments,
-- settlements, contacts, or workflow state.
--
-- The script refuses reruns and refuses invalid/unsupported source values. It
-- defaults to a non-writing preflight. An Identity-owned release process must
-- create the matching trusted approval row before @apply may be set to 1.

SET @tenant_id = '019ea7f6-21e9-7421-ab54-7846cdc6bc76';
SET @org_id = '019ea7f6-21e9-7421-ab54-7846cdc6bc76';

-- The approval row contains the authorized actor, legacy program, source
-- fingerprint, amount policy, numeric legacy lien-status mappings, and signed
-- mapping evidence. The SQL runner never creates or edits that approval.
SET @approval_id = NULL; -- Existing liens_LegacyImportApprovals.Id from the Identity-owned release process.
SET @apply = 0; -- 0 = preflight only (default); 1 = consume approval and write.

DROP PROCEDURE IF EXISTS liens_import_sl_core_core_019ea7f6;

DELIMITER $$

CREATE PROCEDURE liens_import_sl_core_core_019ea7f6()
BEGIN
    DECLARE v_required_tables INT DEFAULT 0;
    DECLARE v_source_columns INT DEFAULT 0;
    DECLARE v_provenance_rows INT DEFAULT 0;
    DECLARE v_approval_rows INT DEFAULT 0;
    DECLARE v_source_rows INT DEFAULT 0;
    DECLARE v_conflicts INT DEFAULT 0;
    DECLARE v_cases_inserted INT DEFAULT 0;
    DECLARE v_liens_inserted INT DEFAULT 0;
    DECLARE v_notes_inserted INT DEFAULT 0;
    DECLARE v_run_id CHAR(36) DEFAULT UUID();
    DECLARE v_approval_id CHAR(36);
    DECLARE v_legacy_program VARCHAR(50);
    DECLARE v_source_fingerprint VARCHAR(128);
    DECLARE v_mapping_version VARCHAR(100);
    DECLARE v_mapping_manifest_hash VARCHAR(128);
    DECLARE v_mapping_approval_reference VARCHAR(200);
    DECLARE v_lien_amount_source VARCHAR(20);
    DECLARE v_legacy_status_one_target VARCHAR(50);
    DECLARE v_legacy_status_two_target VARCHAR(50);
    DECLARE v_migration_user_id CHAR(36);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF @tenant_id <> '019ea7f6-21e9-7421-ab54-7846cdc6bc76'
       OR @org_id <> '019ea7f6-21e9-7421-ab54-7846cdc6bc76' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'This SQL script is bound to tenant/org 019ea7f6-21e9-7421-ab54-7846cdc6bc76.';
    END IF;

    IF @approval_id IS NULL
       OR LOWER(@approval_id) NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = '@approval_id must be an approved legacy-import GUID.';
    END IF;

    IF @apply NOT IN (0, 1) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = '@apply must be 0 for preflight or 1 to write.';
    END IF;

    SELECT COUNT(*) INTO v_required_tables
    FROM information_schema.tables
    WHERE table_schema = DATABASE()
      AND table_name IN (
          'liens_Cases', 'liens_Liens', 'liens_CaseNotes',
          'liens_LegacyImportRuns', 'liens_LegacyIdCrosswalks', 'liens_LegacyImportExceptions',
          'liens_LegacyImportApprovals');

    IF v_required_tables <> 7 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Liens schema incomplete. Apply all legacy-import migrations.';
    END IF;

    SELECT COUNT(*) INTO v_source_columns
    FROM information_schema.columns
    WHERE table_schema = 'SL-CORE'
      AND table_name = 'SL_CASE_NOTES'
      AND column_name IN ('CN_ID','CN_CASE_ID','CN_NOTE','CN_CREATED','CN_CREATED_BY','CN_IS_DELETED','CN_USER_ID');
    IF v_source_columns <> 7 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'SL_CASE_NOTES source column contract is incomplete; CN_USER_ID is required.';
    END IF;

    SELECT COUNT(*) INTO v_approval_rows
    FROM liens_LegacyImportApprovals
    WHERE Id = @approval_id
      AND TenantId = @tenant_id
      AND OrgId = @org_id
      AND SourceSystem = 'SL-CORE'
      AND Status = 'Approved'
      AND ConsumedAtUtc IS NULL
      AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > UTC_TIMESTAMP(6));

    IF v_approval_rows <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'No active trusted approval matches this tenant/org import.';
    END IF;

    SELECT Id, LegacyProgram, SourceFingerprint, MappingVersion, MappingManifestHash,
           MappingApprovalReference, LienAmountSource, LegacyStatusOneTarget,
           LegacyStatusTwoTarget, MigrationUserId
    INTO v_approval_id, v_legacy_program, v_source_fingerprint, v_mapping_version,
         v_mapping_manifest_hash, v_mapping_approval_reference, v_lien_amount_source,
         v_legacy_status_one_target, v_legacy_status_two_target, v_migration_user_id
    FROM liens_LegacyImportApprovals
    WHERE Id = @approval_id;

    IF v_legacy_program NOT IN ('1', '2', '3')
       OR v_lien_amount_source NOT IN ('billing', 'purchase')
       OR LOWER(v_source_fingerprint) NOT REGEXP '^[0-9a-f]{64}$'
       OR LOWER(v_mapping_manifest_hash) NOT REGEXP '^[0-9a-f]{64}$'
       OR v_legacy_status_one_target NOT IN ('Draft', 'Active')
       OR v_legacy_status_two_target NOT IN ('Draft', 'Active') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Trusted approval contains an unsupported program, amount policy, hash, or lien status map.';
    END IF;

    SELECT COUNT(*) INTO v_provenance_rows
    FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE`
    WHERE PROVENANCE_KEY = 'sl-core-current'
      AND SOURCE_FINGERPRINT = v_source_fingerprint
      AND IMPORT_SCOPE = 'sl-core-core-liens-v1';

    IF v_provenance_rows <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'The approved dump hash and staging restore receipt do not match.';
    END IF;

    SELECT COUNT(*) INTO v_conflicts
    FROM liens_LegacyIdCrosswalks
    WHERE TenantId = @tenant_id
      AND SourceSystem = 'SL-CORE'
      AND SourceTable IN ('SL_CASE', 'SL_LEINS_MEDICAL', 'SL_CASE_NOTES');

    IF v_conflicts <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'SL-CORE crosswalks already exist; reconcile before rerun.';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_cases;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_map;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_lien_amounts;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_lien_map;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_note_map;

    CREATE TEMPORARY TABLE tmp_sl_core_cases AS
    SELECT
        c.CASE_ID AS LegacyCaseId,
        c.CASE_CODE AS CaseCode,
        c.CASE_FNAME AS FirstName,
        c.CASE_LNAME AS LastName,
        c.CASE_DOB AS DateOfBirth,
        c.CASE_ADDRESS AS Address,
        c.CASE_CITY AS City,
        c.CASE_STATE AS State,
        c.CASE_ZIPCODE AS ZipCode,
        c.CASE_STATUS AS LegacyStatus,
        c.CASE_DATE_OF_LOSS AS DateOfLossText,
        c.CASE_NOTE AS CaseNotes,
        c.CASE_CREATED AS CreatedAtUtc,
        c.CASE_UPDATED AS UpdatedAtUtc,
        c.CASE_PROGRAM AS LegacyProgram,
        CASE
            WHEN c.CASE_DATE_OF_LOSS IS NULL OR TRIM(c.CASE_DATE_OF_LOSS) = '' THEN NULL
            WHEN TRIM(c.CASE_DATE_OF_LOSS) REGEXP '^[0-9]{4}-[0-9]{2}-[0-9]{2}$' THEN STR_TO_DATE(TRIM(c.CASE_DATE_OF_LOSS), '%Y-%m-%d')
            WHEN TRIM(c.CASE_DATE_OF_LOSS) REGEXP '^[0-9]{2}/[0-9]{2}/[0-9]{4}$' THEN STR_TO_DATE(TRIM(c.CASE_DATE_OF_LOSS), '%m/%d/%Y')
            WHEN TRIM(c.CASE_DATE_OF_LOSS) REGEXP '^[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}$' THEN STR_TO_DATE(TRIM(c.CASE_DATE_OF_LOSS), '%c/%e/%Y')
            ELSE NULL
        END AS DateOfIncident,
        CASE
            WHEN c.CASE_CODE IS NULL OR TRIM(c.CASE_CODE) = '' THEN CONCAT('SL-CORE-CASE-', c.CASE_ID)
            ELSE TRIM(c.CASE_CODE)
        END AS TargetCaseNumber,
        CONCAT_WS(', ', NULLIF(TRIM(c.CASE_ADDRESS), ''), NULLIF(TRIM(c.CASE_CITY), ''), NULLIF(TRIM(c.CASE_STATE), ''), NULLIF(TRIM(c.CASE_ZIPCODE), '')) AS TargetAddress,
        CASE COALESCE(UPPER(TRIM(c.CASE_STATUS)), '')
            WHEN 'N' THEN 'PreDemand'
            WHEN 'P' THEN 'PreDemand'
            WHEN 'PD' THEN 'PreDemand'
            WHEN 'NEW' THEN 'PreDemand'
            WHEN 'PROCESSING' THEN 'PreDemand'
            WHEN 'PRE-DEMAND' THEN 'PreDemand'
            WHEN 'PREDEMAND' THEN 'PreDemand'
            WHEN 'DS' THEN 'DemandSent'
            WHEN 'DEMAND SENT' THEN 'DemandSent'
            WHEN 'NT' THEN 'InNegotiation'
            WHEN 'LP' THEN 'InNegotiation'
            WHEN 'LO' THEN 'InNegotiation'
            WHEN 'LC' THEN 'InNegotiation'
            WHEN 'NEGOTIATIONS' THEN 'InNegotiation'
            WHEN 'LITIGATION' THEN 'InNegotiation'
            WHEN 'LITIGATION (PENDING)' THEN 'InNegotiation'
            WHEN 'LITIGATION (OPEN)' THEN 'InNegotiation'
            WHEN 'LITIGATION (CLOSED)' THEN 'InNegotiation'
            WHEN 'CS' THEN 'CaseSettled'
            WHEN 'CASE SETTLED' THEN 'CaseSettled'
            WHEN 'C' THEN 'Closed'
            WHEN 'CLOSED' THEN 'Closed'
            WHEN '' THEN 'PreDemand'
            ELSE NULL
        END AS TargetStatus
    FROM `SL-CORE`.`SL_CASE` c
    WHERE c.CASE_PROGRAM = v_legacy_program
      AND COALESCE(c.CASE_IS_DELETED, 'N') <> 'Y';

    SELECT COUNT(*) INTO v_source_rows FROM tmp_sl_core_cases;
    IF v_source_rows = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'No non-deleted SL-CORE cases exist for the approved legacy program.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_cases
        WHERE FirstName IS NULL OR TRIM(FirstName) = ''
           OR LastName IS NULL OR TRIM(LastName) = ''
           OR CHAR_LENGTH(TRIM(FirstName)) > 100
           OR CHAR_LENGTH(TRIM(LastName)) > 100
           OR CHAR_LENGTH(TargetCaseNumber) > 50
           OR CHAR_LENGTH(TargetAddress) > 500
           OR CHAR_LENGTH(CaseNotes) > 4000
           OR TargetStatus IS NULL
           OR (DateOfLossText IS NOT NULL AND TRIM(DateOfLossText) <> '' AND DateOfIncident IS NULL)) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'One or more SL-CORE cases are missing required data, exceed target limits, or have an unsupported status/date.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_cases
        GROUP BY TargetCaseNumber
        HAVING COUNT(*) > 1) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Duplicate SL-CORE case numbers require an explicit approved collision mapping; this SQL script will not suffix values.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_cases source_case
        INNER JOIN liens_Cases target_case
            ON target_case.TenantId = @tenant_id
           AND target_case.CaseNumber = source_case.TargetCaseNumber) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'A target case-number collision exists. Reconcile it before running this import.';
    END IF;

    CREATE TEMPORARY TABLE tmp_sl_core_case_map AS
    SELECT source_case.*, UUID() AS TargetCaseId
    FROM tmp_sl_core_cases source_case;

    CREATE TEMPORARY TABLE tmp_sl_core_lien_amounts AS
    SELECT
        code.LMC_LM_ID AS LegacyLienId,
        SUM(CASE
            WHEN NULLIF(TRIM(REPLACE(REPLACE(code.LMC_BILLING_AMOUNT, ',', ''), '$', '')), '') IS NULL THEN 0
            WHEN TRIM(REPLACE(REPLACE(code.LMC_BILLING_AMOUNT, ',', ''), '$', '')) REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$'
                THEN CAST(TRIM(REPLACE(REPLACE(code.LMC_BILLING_AMOUNT, ',', ''), '$', '')) AS DECIMAL(20,2))
            ELSE 0
        END) AS BillingAmount,
        SUM(CASE
            WHEN NULLIF(TRIM(REPLACE(REPLACE(code.LMC_PURCHASE_AMOUNT, ',', ''), '$', '')), '') IS NULL THEN 0
            WHEN TRIM(REPLACE(REPLACE(code.LMC_PURCHASE_AMOUNT, ',', ''), '$', '')) REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$'
                THEN CAST(TRIM(REPLACE(REPLACE(code.LMC_PURCHASE_AMOUNT, ',', ''), '$', '')) AS DECIMAL(20,2))
            ELSE 0
        END) AS PurchaseAmount,
        SUM(CASE
            WHEN NULLIF(TRIM(REPLACE(REPLACE(code.LMC_BILLING_AMOUNT, ',', ''), '$', '')), '') IS NULL THEN 0
            WHEN TRIM(REPLACE(REPLACE(code.LMC_BILLING_AMOUNT, ',', ''), '$', '')) REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$' THEN 0
            ELSE 1
        END) AS InvalidBillingValues,
        SUM(CASE
            WHEN NULLIF(TRIM(REPLACE(REPLACE(code.LMC_PURCHASE_AMOUNT, ',', ''), '$', '')), '') IS NULL THEN 0
            WHEN TRIM(REPLACE(REPLACE(code.LMC_PURCHASE_AMOUNT, ',', ''), '$', '')) REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$' THEN 0
            ELSE 1
        END) AS InvalidPurchaseValues,
        SUM(CASE
            WHEN NULLIF(TRIM(REPLACE(REPLACE(code.LMC_BILLING_AMOUNT, ',', ''), '$', '')), '') IS NULL THEN 0
            WHEN TRIM(REPLACE(REPLACE(code.LMC_BILLING_AMOUNT, ',', ''), '$', '')) REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$' THEN 1
            ELSE 0
        END) AS BillingValueCount,
        SUM(CASE
            WHEN NULLIF(TRIM(REPLACE(REPLACE(code.LMC_PURCHASE_AMOUNT, ',', ''), '$', '')), '') IS NULL THEN 0
            WHEN TRIM(REPLACE(REPLACE(code.LMC_PURCHASE_AMOUNT, ',', ''), '$', '')) REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$' THEN 1
            ELSE 0
        END) AS PurchaseValueCount
    FROM `SL-CORE`.`SL_LEINS_MEDICAL_CODE` code
    INNER JOIN `SL-CORE`.`SL_LEINS_MEDICAL` medical_lien
        ON medical_lien.LM_ID = code.LMC_LM_ID
    INNER JOIN tmp_sl_core_case_map source_case
        ON source_case.LegacyCaseId = medical_lien.LM_CASE_ID
    WHERE COALESCE(medical_lien.LM_IS_DELETED, 'N') <> 'Y'
    GROUP BY code.LMC_LM_ID;

    CREATE TEMPORARY TABLE tmp_sl_core_lien_map AS
    SELECT
        medical_lien.LM_ID AS LegacyLienId,
        source_case.LegacyCaseId,
        source_case.TargetCaseId,
        UUID() AS TargetLienId,
        CASE
            WHEN medical_lien.LM_CODE IS NULL OR TRIM(medical_lien.LM_CODE) = '' THEN CONCAT('SL-CORE-LIEN-', medical_lien.LM_ID)
            ELSE TRIM(medical_lien.LM_CODE)
        END AS TargetLienNumber,
        CASE COALESCE(UPPER(TRIM(medical_lien.LM_STATUS)), '')
            WHEN '1' THEN v_legacy_status_one_target
            WHEN '2' THEN v_legacy_status_two_target
            WHEN 'OPEN' THEN 'Active'
            WHEN 'ACTIVE' THEN 'Active'
            WHEN 'DRAFT' THEN 'Draft'
            WHEN '' THEN 'Draft'
            ELSE NULL
        END AS TargetStatus,
        medical_lien.LM_STATUS AS LegacyStatus,
        medical_lien.LM_NOTE AS LienNotes,
        medical_lien.LM_CREATED AS CreatedAtUtc,
        medical_lien.LM_UPDATED AS UpdatedAtUtc,
        medical_lien.LM_INITIAL_SERVICE_DATE AS InitialServiceDate,
        medical_lien.LM_END_SERVICE_DATE AS EndServiceDate,
        medical_lien.LM_IS_BULK AS IsBulk,
        medical_lien.LM_IS_SERVICING AS IsServicing,
        source_case.FirstName AS SubjectFirstName,
        source_case.LastName AS SubjectLastName,
        source_case.State AS Jurisdiction,
        source_case.DateOfIncident AS IncidentDate,
        COALESCE(amounts.BillingAmount, 0) AS BillingAmount,
        COALESCE(amounts.PurchaseAmount, 0) AS PurchaseAmount,
        COALESCE(amounts.InvalidBillingValues, 0) AS InvalidBillingValues,
        COALESCE(amounts.InvalidPurchaseValues, 0) AS InvalidPurchaseValues,
        COALESCE(amounts.BillingValueCount, 0) AS BillingValueCount,
        COALESCE(amounts.PurchaseValueCount, 0) AS PurchaseValueCount,
        CASE v_lien_amount_source
            WHEN 'billing' THEN COALESCE(amounts.BillingAmount, 0)
            WHEN 'purchase' THEN COALESCE(amounts.PurchaseAmount, 0)
        END AS TargetAmount
    FROM `SL-CORE`.`SL_LEINS_MEDICAL` medical_lien
    INNER JOIN tmp_sl_core_case_map source_case
        ON source_case.LegacyCaseId = medical_lien.LM_CASE_ID
    LEFT JOIN tmp_sl_core_lien_amounts amounts
        ON amounts.LegacyLienId = medical_lien.LM_ID
    WHERE COALESCE(medical_lien.LM_IS_DELETED, 'N') <> 'Y';

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_lien_map
        WHERE TargetStatus IS NULL
           OR CHAR_LENGTH(TargetLienNumber) > 50
           OR CHAR_LENGTH(LienNotes) > 4000
           OR CHAR_LENGTH(SubjectFirstName) > 100
           OR CHAR_LENGTH(SubjectLastName) > 100
           OR CHAR_LENGTH(Jurisdiction) > 100
           OR InvalidBillingValues <> 0
           OR InvalidPurchaseValues <> 0
           OR BillingAmount < 0 OR PurchaseAmount < 0
           OR BillingAmount > 9999999999999999.99 OR PurchaseAmount > 9999999999999999.99
           OR (v_lien_amount_source = 'billing' AND BillingValueCount = 0)
           OR (v_lien_amount_source = 'purchase' AND PurchaseValueCount = 0)) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'SL-CORE lien data has invalid status, amount, or target length.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_lien_map
        GROUP BY TargetLienNumber
        HAVING COUNT(*) > 1) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Duplicate SL-CORE lien numbers require an explicit approved collision mapping; this SQL script will not suffix values.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_lien_map source_lien
        INNER JOIN liens_Liens target_lien
            ON target_lien.TenantId = @tenant_id
           AND target_lien.LienNumber = source_lien.TargetLienNumber) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'A target lien-number collision exists. Reconcile it before running this import.';
    END IF;

    CREATE TEMPORARY TABLE tmp_sl_core_note_map AS
    SELECT
        source_note.CN_ID AS LegacyNoteId,
        source_note.CN_CASE_ID AS LegacyCaseId,
        source_case.TargetCaseId,
        UUID() AS TargetNoteId,
        source_note.CN_NOTE AS Content,
        source_note.CN_CREATED AS CreatedAtUtc,
        source_note.CN_CREATED_BY AS CreatedByName,
        source_note.CN_IS_DELETED AS IsDeleted,
        source_note.CN_USER_ID AS LegacyUserId
    FROM `SL-CORE`.`SL_CASE_NOTES` source_note
    INNER JOIN tmp_sl_core_case_map source_case
        ON source_case.LegacyCaseId = source_note.CN_CASE_ID;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_note_map
        WHERE Content IS NULL OR TRIM(Content) = ''
           OR CHAR_LENGTH(TRIM(Content)) > 5000
           OR CHAR_LENGTH(CreatedByName) > 250) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'One or more SL-CORE case notes are empty or exceed target limits.';
    END IF;

    IF @apply = 0 THEN
        SELECT 'Preflight passed; no rows were written.' AS Result,
               @approval_id AS ApprovalId,
               (SELECT COUNT(*) FROM tmp_sl_core_case_map) AS CasesToInsert,
               (SELECT COUNT(*) FROM tmp_sl_core_lien_map) AS LiensToInsert,
               (SELECT COUNT(*) FROM tmp_sl_core_note_map) AS CaseNotesToInsert;
    ELSE
        START TRANSACTION;

    INSERT INTO liens_LegacyImportRuns
        (Id, ApprovalId, TenantId, OrgId, SourceSystem, SourceFingerprint, LegacyProgram, MappingVersion,
         MappingManifestHash, MappingApprovalReference, Status, StartedAtUtc, CreatedByUserId)
    VALUES
        (v_run_id, v_approval_id, @tenant_id, @org_id, 'SL-CORE', LOWER(v_source_fingerprint), v_legacy_program,
         v_mapping_version, LOWER(v_mapping_manifest_hash), v_mapping_approval_reference,
         'Running', UTC_TIMESTAMP(6), v_migration_user_id);

    INSERT INTO liens_Cases
        (Id, TenantId, OrgId, CaseNumber, ExternalReference, Title, ClientFirstName, ClientLastName,
         ClientDob, ClientPhone, ClientEmail, ClientAddress, Status, DateOfIncident, OpenedAtUtc,
         ClosedAtUtc, InsuranceCarrier, PolicyNumber, ClaimNumber, DemandAmount, SettlementAmount,
         Description, Notes, CreatedByUserId, UpdatedByUserId, CreatedAtUtc, UpdatedAtUtc)
    SELECT
        TargetCaseId, @tenant_id, @org_id, TargetCaseNumber, CONCAT('SL-CORE:SL_CASE:', LegacyCaseId), NULL,
        TRIM(FirstName), TRIM(LastName), DateOfBirth, NULL, NULL, NULLIF(TargetAddress, ''), TargetStatus,
        DateOfIncident, COALESCE(CreatedAtUtc, UTC_TIMESTAMP(6)),
        CASE WHEN TargetStatus IN ('Closed', 'CaseSettled') THEN UpdatedAtUtc ELSE NULL END,
        NULL, NULL, NULL, NULL, NULL, NULL, CaseNotes,
        v_migration_user_id, v_migration_user_id,
        COALESCE(CreatedAtUtc, UTC_TIMESTAMP(6)), COALESCE(UpdatedAtUtc, CreatedAtUtc, UTC_TIMESTAMP(6))
    FROM tmp_sl_core_case_map;
    SET v_cases_inserted = ROW_COUNT();

    INSERT INTO liens_Liens
        (Id, TenantId, OrgId, LienNumber, ExternalReference, LienType, Status, CaseId, FacilityId,
         SubjectPartyId, SubjectFirstName, SubjectLastName, IsConfidential, OriginalAmount, CurrentBalance,
         OfferPrice, PurchasePrice, PayoffAmount, Jurisdiction, Description, Notes, IncidentDate,
         InitialServiceDate, EndServiceDate, IsBulk, IsServicing, OpenedAtUtc, ClosedAtUtc,
         SellingOrgId, BuyingOrgId, HoldingOrgId, SellerStatus, ListingVisibility,
         CreatedByUserId, UpdatedByUserId, CreatedAtUtc, UpdatedAtUtc)
    SELECT
        TargetLienId, @tenant_id, @org_id, TargetLienNumber, CONCAT('SL-CORE:SL_LEINS_MEDICAL:', LegacyLienId),
        'MedicalLien', TargetStatus, TargetCaseId, NULL, NULL,
        NULLIF(TRIM(SubjectFirstName), ''), NULLIF(TRIM(SubjectLastName), ''), 0, TargetAmount, TargetAmount,
        NULL, NULL, NULL, NULLIF(TRIM(Jurisdiction), ''), NULL, NULLIF(LienNotes, ''), IncidentDate,
        InitialServiceDate, EndServiceDate,
        CASE UPPER(TRIM(IsBulk)) WHEN 'Y' THEN 'Yes' WHEN 'YES' THEN 'Yes' WHEN 'N' THEN 'No' WHEN 'NO' THEN 'No' ELSE NULL END,
        CASE UPPER(TRIM(IsServicing)) WHEN 'Y' THEN 'Yes' WHEN 'YES' THEN 'Yes' WHEN 'N' THEN 'No' WHEN 'NO' THEN 'No' ELSE NULL END,
        COALESCE(CreatedAtUtc, UTC_TIMESTAMP(6)), NULL,
        @org_id, NULL, NULL, 'Draft', 'Private',
        v_migration_user_id, v_migration_user_id,
        COALESCE(CreatedAtUtc, UTC_TIMESTAMP(6)), COALESCE(UpdatedAtUtc, CreatedAtUtc, UTC_TIMESTAMP(6))
    FROM tmp_sl_core_lien_map;
    SET v_liens_inserted = ROW_COUNT();

    INSERT INTO liens_CaseNotes
        (Id, CaseId, TenantId, Content, Category, IsPinned, CreatedByUserId, CreatedByName,
         IsEdited, IsDeleted, CreatedAtUtc, UpdatedAtUtc)
    SELECT
        TargetNoteId, TargetCaseId, @tenant_id, TRIM(Content),
        CASE WHEN LegacyUserId IS NULL THEN 'general' ELSE 'feed' END, 0,
        v_migration_user_id, COALESCE(NULLIF(TRIM(CreatedByName), ''), 'Legacy SL-CORE'),
        0, CASE WHEN UPPER(TRIM(IsDeleted)) = 'Y' THEN 1 ELSE 0 END,
        COALESCE(CreatedAtUtc, UTC_TIMESTAMP(6)), NULL
    FROM tmp_sl_core_note_map;
    SET v_notes_inserted = ROW_COUNT();

    INSERT INTO liens_LegacyIdCrosswalks
        (Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity, TargetId, SourceHash, ImportRunId, CreatedAtUtc)
    SELECT
        UUID(), @tenant_id, 'SL-CORE', 'SL_CASE', CAST(LegacyCaseId AS CHAR), 'Case', TargetCaseId,
        SHA2(CONCAT('sql-v1|SL_CASE|', LegacyCaseId, '|', LOWER(v_source_fingerprint)), 256), v_run_id, UTC_TIMESTAMP(6)
    FROM tmp_sl_core_case_map;

    INSERT INTO liens_LegacyIdCrosswalks
        (Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity, TargetId, SourceHash, ImportRunId, CreatedAtUtc)
    SELECT
        UUID(), @tenant_id, 'SL-CORE', 'SL_LEINS_MEDICAL', CAST(LegacyLienId AS CHAR), 'Lien', TargetLienId,
        SHA2(CONCAT('sql-v1|SL_LEINS_MEDICAL|', LegacyLienId, '|', LOWER(v_source_fingerprint)), 256), v_run_id, UTC_TIMESTAMP(6)
    FROM tmp_sl_core_lien_map;

    INSERT INTO liens_LegacyIdCrosswalks
        (Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity, TargetId, SourceHash, ImportRunId, CreatedAtUtc)
    SELECT
        UUID(), @tenant_id, 'SL-CORE', 'SL_CASE_NOTES', CAST(LegacyNoteId AS CHAR), 'CaseNote', TargetNoteId,
        CONCAT('case-note-v2:', SHA2(CONCAT_WS('|', LegacyNoteId, LegacyCaseId, Content, CreatedAtUtc,
               CreatedByName, IsDeleted, LegacyUserId, LOWER(v_source_fingerprint)), 256)), v_run_id, UTC_TIMESTAMP(6)
    FROM tmp_sl_core_note_map;

    UPDATE liens_LegacyImportRuns
    SET Status = 'Completed',
        CompletedAtUtc = UTC_TIMESTAMP(6),
        SummaryJson = JSON_OBJECT(
            'casesInserted', v_cases_inserted,
            'liensInserted', v_liens_inserted,
            'caseNotesInserted', v_notes_inserted,
            'legacyProgram', v_legacy_program,
            'lienAmountSource', v_lien_amount_source,
            'runner', 'sql-v1')
    WHERE Id = v_run_id;

    UPDATE liens_LegacyImportApprovals
    SET Status = 'Consumed',
        ConsumedAtUtc = UTC_TIMESTAMP(6),
        ConsumedByRunId = v_run_id
    WHERE Id = v_approval_id
      AND Status = 'Approved'
      AND ConsumedAtUtc IS NULL
      AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > UTC_TIMESTAMP(6));

    IF ROW_COUNT() <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Approval was no longer active; import rolled back.';
    END IF;

    COMMIT;

    SELECT v_run_id AS ImportRunId,
           v_cases_inserted AS CasesInserted,
           v_liens_inserted AS LiensInserted,
           v_notes_inserted AS CaseNotesInserted;
    END IF;
END$$

DELIMITER ;

CALL liens_import_sl_core_core_019ea7f6();
DROP PROCEDURE liens_import_sl_core_core_019ea7f6;
