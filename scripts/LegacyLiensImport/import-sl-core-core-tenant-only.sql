-- MySQL 8.0+ tenant-only SL-CORE core importer.
--
-- A DBA deploys this reviewed SQL SECURITY DEFINER routine once.  The operator
-- receives EXECUTE only and runs one of these commands:
--   CALL liens_import_sl_core_core_tenant_only('<tenant-guid>', '0'); -- preflight
--   CALL liens_import_sl_core_core_tenant_only('<tenant-guid>', '1'); -- apply
--
-- The caller cannot supply an organization, migration actor, program, approval,
-- amount policy, fingerprint, or source schema.  All are derived from exactly
-- one active Liens approval.  Scope is cases, medical-lien headers, and case
-- notes only.  Credentials, documents, contacts, facilities, payments,
-- settlements, medical-code detail, reports, and workflow state are excluded.
-- Do not make this file self-replace a deployed routine: replacement is a DBA
-- change, not an import-operator privilege.

DELIMITER $$

CREATE PROCEDURE liens_import_sl_core_core_tenant_only(
    IN p_tenant_id VARCHAR(64),
    IN p_apply VARCHAR(16)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id CHAR(36);
    DECLARE v_apply BOOLEAN;
    DECLARE v_original_time_zone VARCHAR(64);
    DECLARE v_time_zone_changed BOOLEAN DEFAULT FALSE;
    DECLARE v_lock_name VARCHAR(64);
    DECLARE v_lock_acquired INT DEFAULT 0;
    DECLARE v_in_transaction BOOLEAN DEFAULT FALSE;
    DECLARE v_candidate_count INT DEFAULT 0;
    DECLARE v_table_count INT DEFAULT 0;
    DECLARE v_column_count INT DEFAULT 0;
    DECLARE v_provenance_count INT DEFAULT 0;
    DECLARE v_existing_crosswalks INT DEFAULT 0;
    DECLARE v_postcondition_errors INT DEFAULT 0;
    DECLARE v_cases_inserted INT DEFAULT 0;
    DECLARE v_liens_inserted INT DEFAULT 0;
    DECLARE v_notes_inserted INT DEFAULT 0;
    DECLARE v_blank_notes_skipped INT DEFAULT 0;
    DECLARE v_case_count INT DEFAULT 0;
    DECLARE v_lien_count INT DEFAULT 0;
    DECLARE v_note_count INT DEFAULT 0;
    DECLARE v_run_id CHAR(36) DEFAULT UUID();
    DECLARE v_approval_id CHAR(36);
    DECLARE v_org_id CHAR(36);
    DECLARE v_migration_user_id CHAR(36);
    DECLARE v_legacy_program VARCHAR(50);
    DECLARE v_source_fingerprint CHAR(64);
    DECLARE v_mapping_version VARCHAR(100);
    DECLARE v_mapping_manifest_hash CHAR(64);
    DECLARE v_mapping_approval_reference VARCHAR(200);
    DECLARE v_lien_amount_source VARCHAR(20);
    DECLARE v_status_one VARCHAR(50);
    DECLARE v_status_two VARCHAR(50);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        IF v_in_transaction THEN ROLLBACK; SET v_in_transaction = FALSE; END IF;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_notes;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_liens;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_amounts;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_cases;
        IF v_time_zone_changed THEN SET @@session.time_zone = v_original_time_zone; END IF;
        IF v_lock_acquired = 1 THEN DO RELEASE_LOCK(v_lock_name); END IF;
        RESIGNAL;
    END;

    -- Validate and copy parameters.  Session variables are intentionally unused.
    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    IF v_tenant_id IS NULL
       OR v_tenant_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-001 invalid tenant GUID';
    END IF;
    IF p_apply IS NULL OR p_apply NOT IN ('0', '1') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-002 apply must be exactly 0 or 1';
    END IF;
    SET v_apply = p_apply = '1';

    -- Source LM_CREATED/LM_UPDATED are TIMESTAMP values.  Read them in UTC and
    -- restore the caller's session setting on every procedure exit.
    SET v_original_time_zone = @@session.time_zone;
    SET @@session.time_zone = '+00:00';
    SET v_time_zone_changed = TRUE;

    SET v_lock_name = CONCAT('liens:slcore:', v_tenant_id);
    SELECT GET_LOCK(v_lock_name, 10) INTO v_lock_acquired;
    IF COALESCE(v_lock_acquired, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-003 tenant import is already active';
    END IF;

    SELECT COUNT(*) INTO v_table_count
    FROM information_schema.tables
    WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE'
      AND table_name IN ('liens_Cases', 'liens_Liens', 'liens_CaseNotes',
                         'liens_LegacyImportApprovals', 'liens_LegacyImportRuns',
                         'liens_LegacyIdCrosswalks');
    IF v_table_count <> 6 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-004 target legacy-import schema is incomplete';
    END IF;

    SELECT COUNT(*) INTO v_table_count
    FROM information_schema.tables
    WHERE table_schema = 'SL-CORE' AND table_type = 'BASE TABLE'
      AND table_name IN ('SL_CASE', 'SL_LEINS_MEDICAL', 'SL_LEINS_MEDICAL_CODE',
                         'SL_CASE_NOTES', 'SL_MIGRATION_SOURCE_PROVENANCE');
    IF v_table_count <> 5 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-005 controlled SL-CORE base tables are unavailable';
    END IF;

    SELECT COUNT(*) INTO v_column_count
    FROM information_schema.columns
    WHERE table_schema = 'SL-CORE' AND (
        (table_name = 'SL_CASE' AND column_name IN ('CASE_ID','CASE_CODE','CASE_FNAME','CASE_LNAME','CASE_DOB','CASE_ADDRESS','CASE_CITY','CASE_STATE','CASE_ZIPCODE','CASE_STATUS','CASE_DATE_OF_LOSS','CASE_NOTE','CASE_CREATED','CASE_UPDATED','CASE_PROGRAM','CASE_IS_DELETED')) OR
        (table_name = 'SL_LEINS_MEDICAL' AND column_name IN ('LM_ID','LM_CASE_ID','LM_STATUS','LM_INITIAL_SERVICE_DATE','LM_END_SERVICE_DATE','LM_NOTE','LM_CREATED','LM_UPDATED','LM_CODE','LM_IS_DELETED','LM_IS_BULK','LM_IS_SERVICING')) OR
        (table_name = 'SL_LEINS_MEDICAL_CODE' AND column_name IN ('LMC_LM_ID','LMC_BILLING_AMOUNT','LMC_PURCHASE_AMOUNT')) OR
        (table_name = 'SL_CASE_NOTES' AND column_name IN ('CN_ID','CN_CASE_ID','CN_NOTE','CN_CREATED','CN_CREATED_BY','CN_IS_DELETED','CN_USER_ID')) OR
        (table_name = 'SL_MIGRATION_SOURCE_PROVENANCE' AND column_name IN ('PROVENANCE_KEY','SOURCE_FINGERPRINT','IMPORT_SCOPE'))
    );
    IF v_column_count <> 41 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-006 source column contract is incomplete';
    END IF;

    IF v_apply THEN
        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
        START TRANSACTION;
        SET v_in_transaction = TRUE;
    END IF;

    SELECT COUNT(*) INTO v_candidate_count
    FROM liens_LegacyImportApprovals
    WHERE TenantId = v_tenant_id AND SourceSystem = 'SL-CORE' AND Status = 'Approved'
      AND ConsumedAtUtc IS NULL AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > UTC_TIMESTAMP(6));
    IF v_candidate_count <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-007 exactly one active approval is required';
    END IF;

    IF v_apply THEN
        SELECT Id, OrgId, MigrationUserId, LegacyProgram, SourceFingerprint,
               MappingVersion, MappingManifestHash, MappingApprovalReference,
               LienAmountSource, LegacyStatusOneTarget, LegacyStatusTwoTarget
        INTO v_approval_id, v_org_id, v_migration_user_id, v_legacy_program, v_source_fingerprint,
             v_mapping_version, v_mapping_manifest_hash, v_mapping_approval_reference,
             v_lien_amount_source, v_status_one, v_status_two
        FROM liens_LegacyImportApprovals
        WHERE TenantId = v_tenant_id AND SourceSystem = 'SL-CORE' AND Status = 'Approved'
          AND ConsumedAtUtc IS NULL AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > UTC_TIMESTAMP(6))
        FOR UPDATE;
    ELSE
        SELECT Id, OrgId, MigrationUserId, LegacyProgram, SourceFingerprint,
               MappingVersion, MappingManifestHash, MappingApprovalReference,
               LienAmountSource, LegacyStatusOneTarget, LegacyStatusTwoTarget
        INTO v_approval_id, v_org_id, v_migration_user_id, v_legacy_program, v_source_fingerprint,
             v_mapping_version, v_mapping_manifest_hash, v_mapping_approval_reference,
             v_lien_amount_source, v_status_one, v_status_two
        FROM liens_LegacyImportApprovals
        WHERE TenantId = v_tenant_id AND SourceSystem = 'SL-CORE' AND Status = 'Approved'
          AND ConsumedAtUtc IS NULL AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > UTC_TIMESTAMP(6));
    END IF;

    IF v_org_id IS NULL OR v_migration_user_id IS NULL OR v_legacy_program NOT IN ('1','2','3')
       OR v_lien_amount_source NOT IN ('billing','purchase')
       OR LOWER(v_source_fingerprint) NOT REGEXP '^[0-9a-f]{64}$'
       OR LOWER(v_mapping_manifest_hash) NOT REGEXP '^[0-9a-f]{64}$'
       OR v_status_one NOT IN ('Draft','Active','Settled')
       OR v_status_two NOT IN ('Draft','Active','Settled') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-008 malformed approved import policy';
    END IF;

    SELECT COUNT(*) INTO v_provenance_count
    FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE`
    WHERE PROVENANCE_KEY = 'sl-core-current' AND LOWER(SOURCE_FINGERPRINT) = LOWER(v_source_fingerprint)
      AND IMPORT_SCOPE = 'sl-core-core-liens-v1';
    IF v_provenance_count <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-009 source provenance does not match approval';
    END IF;

    SELECT COUNT(*) INTO v_existing_crosswalks
    FROM liens_LegacyIdCrosswalks
    WHERE TenantId = v_tenant_id AND SourceSystem = 'SL-CORE'
      AND SourceTable IN ('SL_CASE','SL_LEINS_MEDICAL','SL_CASE_NOTES');
    IF v_existing_crosswalks <> 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-010 existing crosswalks require reconciliation';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_cases;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_amounts;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_liens;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_notes;

    CREATE TEMPORARY TABLE tmp_sl_core_cases AS
    SELECT c.CASE_ID AS LegacyCaseId, UUID() AS TargetCaseId,
           CASE WHEN NULLIF(TRIM(c.CASE_CODE),'') IS NULL THEN CONCAT('SL-CORE-CASE-', c.CASE_ID) ELSE TRIM(c.CASE_CODE) END AS CaseNumber,
           TRIM(c.CASE_FNAME) AS FirstName, TRIM(c.CASE_LNAME) AS LastName, c.CASE_DOB AS DateOfBirth,
           NULLIF(CONCAT_WS(', ', NULLIF(TRIM(c.CASE_ADDRESS),''), NULLIF(TRIM(c.CASE_CITY),''), NULLIF(TRIM(c.CASE_STATE),''), NULLIF(TRIM(c.CASE_ZIPCODE),'')), '') AS Address,
           CASE COALESCE(UPPER(TRIM(c.CASE_STATUS)),'')
             WHEN '' THEN 'PreDemand' WHEN 'N' THEN 'PreDemand' WHEN 'P' THEN 'PreDemand' WHEN 'PD' THEN 'PreDemand'
             WHEN 'NEW' THEN 'PreDemand' WHEN 'PROCESSING' THEN 'PreDemand' WHEN 'PRE-DEMAND' THEN 'PreDemand' WHEN 'PREDEMAND' THEN 'PreDemand'
             WHEN 'DS' THEN 'DemandSent' WHEN 'DEMAND SENT' THEN 'DemandSent'
             WHEN 'NT' THEN 'InNegotiation' WHEN 'LP' THEN 'InNegotiation' WHEN 'LO' THEN 'InNegotiation' WHEN 'LC' THEN 'InNegotiation'
             WHEN 'NEGOTIATIONS' THEN 'InNegotiation' WHEN 'LITIGATION' THEN 'InNegotiation'
             WHEN 'CS' THEN 'CaseSettled' WHEN 'CASE SETTLED' THEN 'CaseSettled' WHEN 'C' THEN 'Closed' WHEN 'CLOSED' THEN 'Closed' ELSE NULL END AS Status,
           CASE WHEN c.CASE_DATE_OF_LOSS IS NULL OR TRIM(c.CASE_DATE_OF_LOSS) = '' THEN NULL
                WHEN TRIM(c.CASE_DATE_OF_LOSS) REGEXP '^[0-9]{4}-[0-9]{2}-[0-9]{2}$' THEN STR_TO_DATE(TRIM(c.CASE_DATE_OF_LOSS), '%Y-%m-%d')
                WHEN TRIM(c.CASE_DATE_OF_LOSS) REGEXP '^[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}$' THEN STR_TO_DATE(TRIM(c.CASE_DATE_OF_LOSS), '%c/%e/%Y') ELSE NULL END AS IncidentDate,
           c.CASE_DATE_OF_LOSS AS IncidentDateText, NULLIF(TRIM(c.CASE_NOTE),'') AS Notes,
           c.CASE_CREATED AS CreatedAtUtc, c.CASE_UPDATED AS UpdatedAtUtc,
           SHA2(CONCAT_WS('|',c.CASE_ID,c.CASE_CODE,c.CASE_FNAME,c.CASE_LNAME,c.CASE_DOB,c.CASE_ADDRESS,c.CASE_CITY,c.CASE_STATE,c.CASE_ZIPCODE,c.CASE_STATUS,c.CASE_DATE_OF_LOSS,c.CASE_NOTE,c.CASE_CREATED,c.CASE_UPDATED,v_source_fingerprint),256) AS SourceHash
    FROM `SL-CORE`.`SL_CASE` c
    WHERE c.CASE_PROGRAM = v_legacy_program AND COALESCE(c.CASE_IS_DELETED,'N') <> 'Y';

    IF NOT EXISTS (SELECT 1 FROM tmp_sl_core_cases) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-011 no eligible source cases';
    END IF;
    IF EXISTS (SELECT 1 FROM tmp_sl_core_cases WHERE FirstName IS NULL OR FirstName = '' OR LastName IS NULL OR LastName = '' OR Status IS NULL
               OR CHAR_LENGTH(CaseNumber) > 50 OR CHAR_LENGTH(Address) > 500 OR CHAR_LENGTH(Notes) > 4000
               OR (IncidentDateText IS NOT NULL AND TRIM(IncidentDateText) <> '' AND IncidentDate IS NULL)) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-012 invalid case mapping';
    END IF;
    IF EXISTS (SELECT CaseNumber FROM tmp_sl_core_cases GROUP BY CaseNumber HAVING COUNT(*) > 1) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-013 duplicate source case numbers';
    END IF;
    IF EXISTS (SELECT 1 FROM tmp_sl_core_cases s INNER JOIN liens_Cases t ON t.TenantId = v_tenant_id AND t.CaseNumber = s.CaseNumber) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-014 target case collision';
    END IF;

    CREATE TEMPORARY TABLE tmp_sl_core_amounts AS
    SELECT mc.LMC_LM_ID AS LegacyLienId,
      SUM(CASE WHEN NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT,',',''),'$','')),'') IS NULL THEN 0
               WHEN TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT,',',''),'$','')) REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$' THEN CAST(TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT,',',''),'$','')) AS DECIMAL(20,2)) ELSE 0 END) AS BillingAmount,
      SUM(CASE WHEN NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$','')),'') IS NULL THEN 0
               WHEN TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$','')) REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$' THEN CAST(TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$','')) AS DECIMAL(20,2)) ELSE 0 END) AS PurchaseAmount,
      SUM(CASE WHEN NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT,',',''),'$','')),'') IS NULL OR TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT,',',''),'$','')) REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$' THEN 0 ELSE 1 END) AS InvalidBilling,
      SUM(CASE WHEN NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$','')),'') IS NULL OR TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$','')) REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$' THEN 0 ELSE 1 END) AS InvalidPurchase,
      SUM(CASE WHEN NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT,',',''),'$','')),'') IS NULL THEN 0 ELSE 1 END) AS BillingValues,
      SUM(CASE WHEN NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$','')),'') IS NULL THEN 0 ELSE 1 END) AS PurchaseValues
    FROM `SL-CORE`.`SL_LEINS_MEDICAL_CODE` mc
    INNER JOIN `SL-CORE`.`SL_LEINS_MEDICAL` l ON l.LM_ID = mc.LMC_LM_ID
    INNER JOIN tmp_sl_core_cases c ON c.LegacyCaseId = l.LM_CASE_ID
    WHERE COALESCE(l.LM_IS_DELETED,'N') <> 'Y'
    GROUP BY mc.LMC_LM_ID;

    CREATE TEMPORARY TABLE tmp_sl_core_liens AS
    SELECT l.LM_ID AS LegacyLienId, UUID() AS TargetLienId, c.TargetCaseId,
           CASE WHEN NULLIF(TRIM(l.LM_CODE),'') IS NULL THEN CONCAT('SL-CORE-LIEN-',l.LM_ID) ELSE TRIM(l.LM_CODE) END AS LienNumber,
           CASE COALESCE(UPPER(TRIM(l.LM_STATUS)),'') WHEN '1' THEN v_status_one WHEN '2' THEN v_status_two WHEN '' THEN 'Draft' WHEN 'DRAFT' THEN 'Draft' WHEN 'OPEN' THEN 'Active' WHEN 'ACTIVE' THEN 'Active' ELSE NULL END AS Status,
           NULLIF(TRIM(l.LM_NOTE),'') AS Notes, l.LM_CREATED AS CreatedAtUtc, l.LM_UPDATED AS UpdatedAtUtc,
           l.LM_INITIAL_SERVICE_DATE AS InitialServiceDate, l.LM_END_SERVICE_DATE AS EndServiceDate,
           CASE UPPER(TRIM(l.LM_IS_BULK)) WHEN 'Y' THEN 'Yes' WHEN 'YES' THEN 'Yes' WHEN 'N' THEN 'No' WHEN 'NO' THEN 'No' ELSE NULL END AS IsBulk,
           CASE UPPER(TRIM(l.LM_IS_SERVICING)) WHEN 'Y' THEN 'Yes' WHEN 'YES' THEN 'Yes' WHEN 'N' THEN 'No' WHEN 'NO' THEN 'No' ELSE NULL END AS IsServicing,
           c.FirstName AS SubjectFirstName, c.LastName AS SubjectLastName, c.IncidentDate,
           COALESCE(a.BillingAmount,0) AS BillingAmount, COALESCE(a.PurchaseAmount,0) AS PurchaseAmount,
           COALESCE(a.InvalidBilling,0) AS InvalidBilling, COALESCE(a.InvalidPurchase,0) AS InvalidPurchase,
           COALESCE(a.BillingValues,0) AS BillingValues, COALESCE(a.PurchaseValues,0) AS PurchaseValues,
           CASE v_lien_amount_source WHEN 'billing' THEN COALESCE(a.BillingAmount,0) WHEN 'purchase' THEN COALESCE(a.PurchaseAmount,0) END AS TargetAmount,
           SHA2(CONCAT_WS('|',l.LM_ID,l.LM_CASE_ID,l.LM_STATUS,l.LM_CODE,l.LM_NOTE,l.LM_CREATED,l.LM_UPDATED,l.LM_INITIAL_SERVICE_DATE,l.LM_END_SERVICE_DATE,l.LM_IS_BULK,l.LM_IS_SERVICING,COALESCE(a.BillingAmount,0),COALESCE(a.PurchaseAmount,0),v_source_fingerprint),256) AS SourceHash
    FROM `SL-CORE`.`SL_LEINS_MEDICAL` l
    INNER JOIN tmp_sl_core_cases c ON c.LegacyCaseId = l.LM_CASE_ID
    LEFT JOIN tmp_sl_core_amounts a ON a.LegacyLienId = l.LM_ID
    WHERE COALESCE(l.LM_IS_DELETED,'N') <> 'Y';

    IF EXISTS (SELECT 1 FROM tmp_sl_core_liens WHERE Status IS NULL OR CHAR_LENGTH(LienNumber) > 50 OR CHAR_LENGTH(Notes) > 4000
               OR InvalidBilling <> 0 OR InvalidPurchase <> 0 OR BillingAmount < 0 OR PurchaseAmount < 0
               OR BillingAmount > 9999999999999999.99 OR PurchaseAmount > 9999999999999999.99
               OR (v_lien_amount_source = 'billing' AND BillingValues = 0) OR (v_lien_amount_source = 'purchase' AND PurchaseValues = 0)) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-015 invalid lien mapping';
    END IF;
    IF EXISTS (SELECT LienNumber FROM tmp_sl_core_liens GROUP BY LienNumber HAVING COUNT(*) > 1) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-016 duplicate source lien numbers';
    END IF;
    IF EXISTS (SELECT 1 FROM tmp_sl_core_liens s INNER JOIN liens_Liens t ON t.TenantId = v_tenant_id AND t.LienNumber = s.LienNumber) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-017 target lien collision';
    END IF;

    -- Blank legacy note records contain no target content. Exclude them rather
    -- than inserting a fabricated note or failing the whole approved import.
    -- They receive neither a target record nor a crosswalk and are reported in
    -- both preflight and the completed-run summary.
    SELECT COUNT(*) INTO v_blank_notes_skipped
    FROM `SL-CORE`.`SL_CASE_NOTES` n
    INNER JOIN tmp_sl_core_cases c ON c.LegacyCaseId = n.CN_CASE_ID
    WHERE NULLIF(TRIM(n.CN_NOTE),'') IS NULL;

    CREATE TEMPORARY TABLE tmp_sl_core_notes AS
    SELECT n.CN_ID AS LegacyNoteId, UUID() AS TargetNoteId, c.TargetCaseId, NULLIF(TRIM(n.CN_NOTE),'') AS Content,
           n.CN_CREATED AS CreatedAtUtc, NULLIF(TRIM(n.CN_CREATED_BY),'') AS CreatedByName, n.CN_IS_DELETED AS IsDeleted,
           n.CN_USER_ID AS LegacyUserId,
           CONCAT('case-note-v2:',SHA2(CONCAT_WS('|',n.CN_ID,n.CN_CASE_ID,n.CN_NOTE,n.CN_CREATED,n.CN_CREATED_BY,n.CN_IS_DELETED,n.CN_USER_ID,v_source_fingerprint),256)) AS SourceHash
    FROM `SL-CORE`.`SL_CASE_NOTES` n
    INNER JOIN tmp_sl_core_cases c ON c.LegacyCaseId = n.CN_CASE_ID
    WHERE NULLIF(TRIM(n.CN_NOTE),'') IS NOT NULL;
    IF EXISTS (SELECT 1 FROM tmp_sl_core_notes WHERE CHAR_LENGTH(Content) > 5000 OR CHAR_LENGTH(CreatedByName) > 250) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-018 invalid note mapping';
    END IF;

    IF NOT v_apply THEN
        SELECT COUNT(*) INTO v_case_count FROM tmp_sl_core_cases;
        SELECT COUNT(*) INTO v_lien_count FROM tmp_sl_core_liens;
        SELECT COUNT(*) INTO v_note_count FROM tmp_sl_core_notes;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_notes;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_liens;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_amounts;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_cases;
        SET @@session.time_zone = v_original_time_zone; SET v_time_zone_changed = FALSE;
        DO RELEASE_LOCK(v_lock_name); SET v_lock_acquired = 0;
        SELECT 'preflight-passed' AS Result, v_approval_id AS ApprovalId,
               v_case_count AS CasesToInsert, v_lien_count AS LiensToInsert,
               v_note_count AS CaseNotesToInsert,
               v_blank_notes_skipped AS BlankCaseNotesSkipped;
    ELSE
        INSERT INTO liens_LegacyImportRuns (Id,ApprovalId,TenantId,OrgId,SourceSystem,SourceFingerprint,LegacyProgram,MappingVersion,MappingManifestHash,MappingApprovalReference,Status,StartedAtUtc,CreatedByUserId)
        VALUES (v_run_id,v_approval_id,v_tenant_id,v_org_id,'SL-CORE',LOWER(v_source_fingerprint),v_legacy_program,v_mapping_version,LOWER(v_mapping_manifest_hash),v_mapping_approval_reference,'Running',UTC_TIMESTAMP(6),v_migration_user_id);

        INSERT INTO liens_Cases (Id,TenantId,OrgId,CaseNumber,ExternalReference,Title,ClientFirstName,ClientLastName,ClientDob,ClientPhone,ClientEmail,ClientAddress,Status,DateOfIncident,OpenedAtUtc,ClosedAtUtc,InsuranceCarrier,PolicyNumber,ClaimNumber,DemandAmount,SettlementAmount,Description,Notes,CreatedByUserId,UpdatedByUserId,CreatedAtUtc,UpdatedAtUtc)
        SELECT TargetCaseId,v_tenant_id,v_org_id,CaseNumber,CONCAT('SL-CORE:SL_CASE:',LegacyCaseId),NULL,FirstName,LastName,DateOfBirth,NULL,NULL,Address,Status,IncidentDate,COALESCE(CreatedAtUtc,UTC_TIMESTAMP(6)),CASE WHEN Status IN ('Closed','CaseSettled') THEN UpdatedAtUtc ELSE NULL END,NULL,NULL,NULL,NULL,NULL,NULL,Notes,v_migration_user_id,v_migration_user_id,COALESCE(CreatedAtUtc,UTC_TIMESTAMP(6)),COALESCE(UpdatedAtUtc,CreatedAtUtc,UTC_TIMESTAMP(6)) FROM tmp_sl_core_cases;
        SET v_cases_inserted = ROW_COUNT();

        INSERT INTO liens_Liens (Id,TenantId,OrgId,LienNumber,ExternalReference,LienType,Status,CaseId,FacilityId,SubjectPartyId,SubjectFirstName,SubjectLastName,IsConfidential,OriginalAmount,CurrentBalance,OfferPrice,PurchasePrice,PayoffAmount,Jurisdiction,Description,Notes,IncidentDate,InitialServiceDate,EndServiceDate,IsBulk,IsServicing,OpenedAtUtc,ClosedAtUtc,SellingOrgId,BuyingOrgId,HoldingOrgId,SellerStatus,ListingVisibility,CreatedByUserId,UpdatedByUserId,CreatedAtUtc,UpdatedAtUtc)
        SELECT TargetLienId,v_tenant_id,v_org_id,LienNumber,CONCAT('SL-CORE:SL_LEINS_MEDICAL:',LegacyLienId),'MedicalLien',Status,TargetCaseId,NULL,NULL,SubjectFirstName,SubjectLastName,0,TargetAmount,CASE WHEN Status = 'Settled' THEN 0 ELSE TargetAmount END,NULL,NULL,NULL,NULL,NULL,Notes,IncidentDate,InitialServiceDate,EndServiceDate,IsBulk,IsServicing,COALESCE(CreatedAtUtc,UTC_TIMESTAMP(6)),CASE WHEN Status = 'Settled' THEN COALESCE(UpdatedAtUtc,CreatedAtUtc,UTC_TIMESTAMP(6)) ELSE NULL END,v_org_id,NULL,NULL,'Draft','Private',v_migration_user_id,v_migration_user_id,COALESCE(CreatedAtUtc,UTC_TIMESTAMP(6)),COALESCE(UpdatedAtUtc,CreatedAtUtc,UTC_TIMESTAMP(6)) FROM tmp_sl_core_liens;
        SET v_liens_inserted = ROW_COUNT();

        INSERT INTO liens_CaseNotes (Id,CaseId,TenantId,Content,Category,IsPinned,CreatedByUserId,CreatedByName,IsEdited,IsDeleted,CreatedAtUtc,UpdatedAtUtc)
        SELECT TargetNoteId,TargetCaseId,v_tenant_id,Content,CASE WHEN LegacyUserId IS NULL THEN 'general' ELSE 'feed' END,0,v_migration_user_id,COALESCE(CreatedByName,'Legacy SL-CORE'),0,CASE WHEN UPPER(COALESCE(IsDeleted,'N')) = 'Y' THEN 1 ELSE 0 END,COALESCE(CreatedAtUtc,UTC_TIMESTAMP(6)),NULL FROM tmp_sl_core_notes;
        SET v_notes_inserted = ROW_COUNT();

        INSERT INTO liens_LegacyIdCrosswalks (Id,TenantId,SourceSystem,SourceTable,LegacyId,TargetEntity,TargetId,SourceHash,ImportRunId,CreatedAtUtc)
        SELECT UUID(),v_tenant_id,'SL-CORE','SL_CASE',CAST(LegacyCaseId AS CHAR),'Case',TargetCaseId,SourceHash,v_run_id,UTC_TIMESTAMP(6) FROM tmp_sl_core_cases;
        INSERT INTO liens_LegacyIdCrosswalks (Id,TenantId,SourceSystem,SourceTable,LegacyId,TargetEntity,TargetId,SourceHash,ImportRunId,CreatedAtUtc)
        SELECT UUID(),v_tenant_id,'SL-CORE','SL_LEINS_MEDICAL',CAST(LegacyLienId AS CHAR),'Lien',TargetLienId,SourceHash,v_run_id,UTC_TIMESTAMP(6) FROM tmp_sl_core_liens;
        INSERT INTO liens_LegacyIdCrosswalks (Id,TenantId,SourceSystem,SourceTable,LegacyId,TargetEntity,TargetId,SourceHash,ImportRunId,CreatedAtUtc)
        SELECT UUID(),v_tenant_id,'SL-CORE','SL_CASE_NOTES',CAST(LegacyNoteId AS CHAR),'CaseNote',TargetNoteId,SourceHash,v_run_id,UTC_TIMESTAMP(6) FROM tmp_sl_core_notes;

        -- Target foreign keys are not tenant-composite; enforce ownership explicitly.
        SELECT COUNT(*) INTO v_postcondition_errors
        FROM liens_LegacyIdCrosswalks x LEFT JOIN liens_Liens l ON x.TargetEntity = 'Lien' AND l.Id = x.TargetId
        LEFT JOIN liens_Cases c ON l.CaseId = c.Id
        WHERE x.ImportRunId = v_run_id AND x.TargetEntity = 'Lien'
          AND (l.Id IS NULL OR l.TenantId <> v_tenant_id OR l.OrgId <> v_org_id OR c.Id IS NULL OR c.TenantId <> v_tenant_id OR c.OrgId <> v_org_id);
        IF v_postcondition_errors <> 0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-019 lien/case tenant ownership failed'; END IF;
        SELECT COUNT(*) INTO v_postcondition_errors
        FROM liens_LegacyIdCrosswalks x LEFT JOIN liens_CaseNotes n ON x.TargetEntity = 'CaseNote' AND n.Id = x.TargetId
        LEFT JOIN liens_Cases c ON n.CaseId = c.Id
        WHERE x.ImportRunId = v_run_id AND x.TargetEntity = 'CaseNote'
          AND (n.Id IS NULL OR n.TenantId <> v_tenant_id OR c.Id IS NULL OR c.TenantId <> v_tenant_id OR c.OrgId <> v_org_id);
        IF v_postcondition_errors <> 0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-020 note/case tenant ownership failed'; END IF;

        UPDATE liens_LegacyImportRuns SET Status = 'Completed',CompletedAtUtc = UTC_TIMESTAMP(6),
          SummaryJson = JSON_OBJECT('casesInserted',v_cases_inserted,'liensInserted',v_liens_inserted,'caseNotesInserted',v_notes_inserted,'blankCaseNotesSkipped',v_blank_notes_skipped,'legacyProgram',v_legacy_program,'lienAmountSource',v_lien_amount_source,'runner','tenant-only-sql-v1')
        WHERE Id = v_run_id AND TenantId = v_tenant_id AND OrgId = v_org_id AND ApprovalId = v_approval_id;

        UPDATE liens_LegacyImportApprovals SET Status = 'Consumed',ConsumedAtUtc = UTC_TIMESTAMP(6),ConsumedByRunId = v_run_id
        WHERE Id = v_approval_id AND TenantId = v_tenant_id AND SourceSystem = 'SL-CORE' AND Status = 'Approved' AND ConsumedAtUtc IS NULL AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > UTC_TIMESTAMP(6));
        IF ROW_COUNT() <> 1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTI-021 approval claim failed'; END IF;

        COMMIT; SET v_in_transaction = FALSE;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_notes;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_liens;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_amounts;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_cases;
        SET @@session.time_zone = v_original_time_zone; SET v_time_zone_changed = FALSE;
        DO RELEASE_LOCK(v_lock_name); SET v_lock_acquired = 0;
        SELECT 'applied' AS Result,v_run_id AS ImportRunId,v_cases_inserted AS CasesInserted,v_liens_inserted AS LiensInserted,v_notes_inserted AS CaseNotesInserted,v_blank_notes_skipped AS BlankCaseNotesSkipped;
    END IF;
END$$

DELIMITER ;
