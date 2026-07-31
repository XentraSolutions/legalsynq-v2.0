-- liens_migrate_sl_core_complete
-- Single-pass SL-CORE Program 1 migration.  One procedure replaces all
-- existing waves: core (cases / liens / case notes), contacts, facilities,
-- facility-contact persons, lien-facility links, medical-code servicing items,
-- and medical-provider servicing items.
--
-- Amount mapping (both amounts populated; LienAmountSource policy is ignored):
--   liens_Liens.OriginalAmount   = SUM(LMC_BILLING_AMOUNT)  per lien
--   liens_Liens.PurchasePrice    = SUM(LMC_PURCHASE_AMOUNT) per lien
--   liens_Liens.CurrentBalance   = OriginalAmount unless Status='Settled', then 0
--   liens_Cases.DemandAmount     = SUM(OriginalAmount) across all liens per case
--   liens_Cases.SettlementAmount = SL_SETTLEMENT_HEADER.SH_TOTAL_AMOUNT per case
--
-- Case Notes [legacy-meta] metadata (built before any writes, in one pass):
--   lawFirmId=<contact UUID>
--   caseManagerId=<contact UUID>
--   accidentTypeId=<lookup UUID>; accidentType=<Name>
--   (only present when the source value is non-null and resolves to exactly one target)
--
-- Prerequisites:
--   * One active (Status='Approved', not yet consumed) liens_LegacyImportApprovals
--     row for the tenant in the SL-CORE source system.
--   * No prior SL-CORE crosswalks for this tenant (clean-slate import only).
--   * SL-CORE SL_MIGRATION_SOURCE_PROVENANCE row matches the approval fingerprint.
--
-- Usage:
--   CALL liens_migrate_sl_core_complete('<tenant-guid>', '0');  -- preflight
--   CALL liens_migrate_sl_core_complete('<tenant-guid>', '1');  -- apply
--
-- Error prefix: LSLTE-

DROP PROCEDURE IF EXISTS liens_migrate_sl_core_complete;

DELIMITER $$

CREATE PROCEDURE liens_migrate_sl_core_complete(
    IN p_tenant_id CHAR(36),
    IN p_apply     CHAR(1)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id            CHAR(36);
    DECLARE v_apply                BOOLEAN;
    DECLARE v_orig_tz              VARCHAR(64);
    DECLARE v_tz_changed           BOOLEAN DEFAULT FALSE;
    DECLARE v_core_lock            VARCHAR(64);
    DECLARE v_contact_lock         VARCHAR(64);
    DECLARE v_core_locked          INT DEFAULT 0;
    DECLARE v_contact_locked       INT DEFAULT 0;
    DECLARE v_in_transaction       BOOLEAN DEFAULT FALSE;
    -- Approval / run IDs
    DECLARE v_approval_id          CHAR(36);
    DECLARE v_org_id               CHAR(36);
    DECLARE v_user_id              CHAR(36);
    DECLARE v_legacy_program       VARCHAR(50);
    DECLARE v_fingerprint          CHAR(64);
    DECLARE v_mapping_version      VARCHAR(100);
    DECLARE v_mapping_hash         CHAR(64);
    DECLARE v_mapping_ref          VARCHAR(200);
    DECLARE v_status_one           VARCHAR(50);
    DECLARE v_status_two           VARCHAR(50);
    DECLARE v_core_run_id          CHAR(36);
    DECLARE v_contact_run_id       CHAR(36);
    -- Counts (preflight reporting)
    DECLARE v_table_count          INT DEFAULT 0;
    DECLARE v_candidate_count      INT DEFAULT 0;
    DECLARE v_provenance_count     INT DEFAULT 0;
    DECLARE v_existing_crosswalks  INT DEFAULT 0;
    DECLARE v_lookups_seeded       INT DEFAULT 0;
    DECLARE v_case_count           INT DEFAULT 0;
    DECLARE v_lien_count           INT DEFAULT 0;
    DECLARE v_note_count           INT DEFAULT 0;
    DECLARE v_blank_notes          INT DEFAULT 0;
    DECLARE v_contact_count        INT DEFAULT 0;
    DECLARE v_facility_count       INT DEFAULT 0;
    DECLARE v_person_count         INT DEFAULT 0;
    DECLARE v_fac_link_count       INT DEFAULT 0;
    DECLARE v_med_code_count       INT DEFAULT 0;
    DECLARE v_provider_count       INT DEFAULT 0;
    -- Applied row counts
    DECLARE v_cases_ins            INT DEFAULT 0;
    DECLARE v_liens_ins            INT DEFAULT 0;
    DECLARE v_notes_ins            INT DEFAULT 0;
    DECLARE v_contacts_ins         INT DEFAULT 0;
    DECLARE v_fac_contacts_ins     INT DEFAULT 0;
    DECLARE v_facilities_ins       INT DEFAULT 0;
    DECLARE v_persons_ins          INT DEFAULT 0;
    DECLARE v_fac_links_upd        INT DEFAULT 0;
    DECLARE v_med_svc_ins          INT DEFAULT 0;
    DECLARE v_prov_svc_ins         INT DEFAULT 0;
    DECLARE v_postcondition_errors INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        IF v_in_transaction THEN ROLLBACK; SET v_in_transaction = FALSE; END IF;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_providers;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_med_codes;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_fac_links;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_fac_persons;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_facilities;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_notes;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_liens;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_cases;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_settlements;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_amounts;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_contacts;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_at_lookups;
        IF v_tz_changed THEN SET @@session.time_zone = v_orig_tz; END IF;
        IF v_contact_locked = 1 THEN DO RELEASE_LOCK(v_contact_lock); END IF;
        IF v_core_locked    = 1 THEN DO RELEASE_LOCK(v_core_lock);    END IF;
        RESIGNAL;
    END;

    -- -------------------------------------------------------------------------
    -- 1. Parameter validation
    -- -------------------------------------------------------------------------
    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    IF v_tenant_id IS NULL
       OR v_tenant_id NOT REGEXP
              '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR p_apply IS NULL OR p_apply NOT IN ('0','1') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-001 invalid tenant GUID or apply flag';
    END IF;
    SET v_apply = (p_apply = '1');

    IF DATABASE() NOT IN ('LS_LIENS','LS_QA_LIENS') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-002 target schema must be LS_LIENS or LS_QA_LIENS';
    END IF;

    -- Read TIMESTAMP source columns in UTC; restore on every exit path.
    SET v_orig_tz = @@session.time_zone;
    SET @@session.time_zone = '+00:00';
    SET v_tz_changed = TRUE;

    -- Acquire advisory locks in the same order used by the individual wave
    -- procedures so a concurrent partial-migration run is blocked.
    SET v_core_lock    = CONCAT('liens:slcore:', v_tenant_id);
    SET v_contact_lock = CONCAT('liens:slcore:contacts:', v_tenant_id);

    SELECT GET_LOCK(v_core_lock, 10)    INTO v_core_locked;
    IF COALESCE(v_core_locked, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-003 core migration lock is already held for this tenant';
    END IF;
    SELECT GET_LOCK(v_contact_lock, 10) INTO v_contact_locked;
    IF COALESCE(v_contact_locked, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-004 contact migration lock is already held for this tenant';
    END IF;

    -- -------------------------------------------------------------------------
    -- 2. Schema contract: target tables (11) + source tables (12) = 23
    -- -------------------------------------------------------------------------
    SELECT COUNT(*) INTO v_table_count
    FROM information_schema.tables
    WHERE (table_schema = DATABASE() AND table_type = 'BASE TABLE'
           AND table_name IN (
               'liens_Cases','liens_Liens','liens_CaseNotes',
               'liens_Contacts','liens_Facilities','liens_FacilityContactPersons',
               'liens_ServicingItems','liens_LookupValues',
               'liens_LegacyImportApprovals','liens_LegacyImportRuns',
               'liens_LegacyIdCrosswalks'))
       OR (table_schema = 'SL-CORE' AND table_type = 'BASE TABLE'
           AND table_name IN (
               'SL_CASE','SL_LEINS_MEDICAL','SL_LEINS_MEDICAL_CODE',
               'SL_CASE_NOTES','SL_CONTACT','SL_CONTACT_TYPE',
               'SL_FACILITY','SL_FACILITY_CONTACT_PERSON',
               'SL_ACCIDENT_TYPE','SL_SETTLEMENT_HEADER',
               'SL_LEINS_MEDICAL_INFORMATION_FACILITY',
               'SL_MIGRATION_SOURCE_PROVENANCE'));
    IF v_table_count <> 23 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-005 required source or target tables are unavailable (expected 23)';
    END IF;

    -- -------------------------------------------------------------------------
    -- 3. Approval record
    -- -------------------------------------------------------------------------
    SELECT COUNT(*) INTO v_candidate_count
    FROM liens_LegacyImportApprovals
    WHERE TenantId = v_tenant_id AND SourceSystem = 'SL-CORE' AND Status = 'Approved'
      AND ConsumedAtUtc IS NULL
      AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > UTC_TIMESTAMP(6));
    IF v_candidate_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-006 exactly one active SL-CORE approval is required';
    END IF;

    SELECT Id, OrgId, MigrationUserId, LegacyProgram, SourceFingerprint,
           MappingVersion, MappingManifestHash, MappingApprovalReference,
           LegacyStatusOneTarget, LegacyStatusTwoTarget
      INTO v_approval_id, v_org_id, v_user_id, v_legacy_program, v_fingerprint,
           v_mapping_version, v_mapping_hash, v_mapping_ref,
           v_status_one, v_status_two
    FROM liens_LegacyImportApprovals
    WHERE TenantId = v_tenant_id AND SourceSystem = 'SL-CORE' AND Status = 'Approved'
      AND ConsumedAtUtc IS NULL
      AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > UTC_TIMESTAMP(6));

    IF v_org_id IS NULL OR v_user_id IS NULL
       OR v_legacy_program NOT IN ('1','2','3')
       OR LOWER(v_fingerprint) NOT REGEXP '^[0-9a-f]{64}$'
       OR v_status_one NOT IN ('Draft','Active','Settled')
       OR v_status_two NOT IN ('Draft','Active','Settled') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-007 malformed approved import policy';
    END IF;

    SELECT COUNT(*) INTO v_provenance_count
    FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE`
    WHERE PROVENANCE_KEY = 'sl-core-current'
      AND LOWER(SOURCE_FINGERPRINT) = LOWER(v_fingerprint)
      AND IMPORT_SCOPE = 'sl-core-core-liens-v1';
    IF v_provenance_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-008 source provenance does not match the approval fingerprint';
    END IF;

    -- Clean-slate guard: no prior SL-CORE crosswalks for this tenant.
    SELECT COUNT(*) INTO v_existing_crosswalks
    FROM liens_LegacyIdCrosswalks
    WHERE TenantId = v_tenant_id AND SourceSystem = 'SL-CORE';
    IF v_existing_crosswalks <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-009 existing SL-CORE crosswalks require reconciliation before a fresh migration';
    END IF;

    -- -------------------------------------------------------------------------
    -- 4. Seed missing AccidentType lookup values (idempotent; runs in both modes)
    -- Medical Malpractice (AT_ID=39) was absent from the initial target seed.
    -- -------------------------------------------------------------------------
    INSERT INTO liens_LookupValues
        (Id, TenantId, Category, Code, Name, Description, SortOrder,
         IsActive, IsSystem, CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, UpdatedByUserId)
    SELECT UUID(), NULL, 'AccidentType', c.Code, c.Name, NULL, c.SortOrder,
           1, 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), v_user_id, NULL
    FROM (
        SELECT 'MedicalMalpractice' AS Code, 'Medical Malpractice' AS Name, 39 AS SortOrder
    ) c
    WHERE NOT EXISTS (
        SELECT 1 FROM liens_LookupValues lv
        WHERE lv.TenantId IS NULL AND lv.Category = 'AccidentType' AND lv.Code = c.Code
    );
    SET v_lookups_seeded = ROW_COUNT();

    -- -------------------------------------------------------------------------
    -- 5. Accident-type lookup map  (AT_ID → LookupId / Name / MatchCount)
    -- Built after the seed so Medical Malpractice is included.
    -- -------------------------------------------------------------------------
    DROP TEMPORARY TABLE IF EXISTS tmp_sle_at_lookups;
    CREATE TEMPORARY TABLE tmp_sle_at_lookups AS
    SELECT
        at.AT_ID  AS LegacyAtId,
        lv.Id     AS LookupId,
        lv.Name   AS LookupName,
        COALESCE(cnt.MatchCount, 0) AS MatchCount
    FROM `SL-CORE`.`SL_ACCIDENT_TYPE` at
    LEFT JOIN (
        SELECT LOWER(TRIM(Name)) AS Norm, MIN(Id) AS Id, MIN(Name) AS Name
        FROM liens_LookupValues
        WHERE TenantId IS NULL AND Category = 'AccidentType'
          AND IsActive = 1 AND IsSystem = 1
        GROUP BY LOWER(TRIM(Name))
    ) lv  ON lv.Norm = LOWER(TRIM(at.AT_DESCRIPTION))
    LEFT JOIN (
        SELECT LOWER(TRIM(Name)) AS Norm, COUNT(*) AS MatchCount
        FROM liens_LookupValues
        WHERE TenantId IS NULL AND Category = 'AccidentType'
          AND IsActive = 1 AND IsSystem = 1
        GROUP BY LOWER(TRIM(Name))
    ) cnt ON cnt.Norm = LOWER(TRIM(at.AT_DESCRIPTION));

    -- Ambiguous lookup entries (> 1 match) block the migration.
    IF EXISTS (SELECT 1 FROM tmp_sle_at_lookups WHERE MatchCount > 1) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-010 ambiguous AccidentType lookup entries require deduplication';
    END IF;

    -- -------------------------------------------------------------------------
    -- 6. Law-firm parent UUID pre-assignment
    -- Type-1 contacts need a stable UUID before any other contact type is staged,
    -- because role contacts (types 6/7/8) embed the parent's UUID as LawFirmId.
    -- -------------------------------------------------------------------------
    DROP TEMPORARY TABLE IF EXISTS tmp_sle_lf_parents;
    CREATE TEMPORARY TABLE tmp_sle_lf_parents AS
    SELECT c.CONTACT_ID AS LegacyContactId, UUID() AS TargetContactId
    FROM `SL-CORE`.`SL_CONTACT` c
    INNER JOIN `SL-CORE`.`SL_CONTACT_TYPE` ct
      ON ct.CT_ID = c.CONTACT_TYPE AND COALESCE(ct.CT_STATUS,'A') = 'A'
    WHERE c.CONTACT_PROGRAM = CAST(v_legacy_program AS UNSIGNED)
      AND COALESCE(c.CONTACT_STATUS,'A') = 'A'
      AND c.CONTACT_TYPE = 1;

    -- -------------------------------------------------------------------------
    -- 7. All contacts staging (pre-assigned UUIDs; used later for case Notes)
    -- -------------------------------------------------------------------------
    DROP TEMPORARY TABLE IF EXISTS tmp_sle_contacts;
    CREATE TEMPORARY TABLE tmp_sle_contacts AS
    SELECT
        c.CONTACT_ID AS LegacyContactId,
        CASE c.CONTACT_TYPE
          WHEN 1 THEN 'LawFirm'       WHEN 2 THEN 'Provider'
          WHEN 3 THEN 'FundingCompany' WHEN 4 THEN 'MedicalFacility'
          WHEN 5 THEN 'Lead'           WHEN 6 THEN 'LawFirm'
          WHEN 7 THEN 'LawFirm'        WHEN 8 THEN 'LawFirm'
        END AS TargetContactType,
        CASE c.CONTACT_TYPE
          WHEN 6 THEN 'CaseManager'
          WHEN 7 THEN 'Attorney'
          WHEN 8 THEN 'Other'
          ELSE NULL
        END AS TargetContactSubtype,
        -- Law firm / role contacts reuse the parent's pre-assigned UUID.
        CAST(CASE
               WHEN c.CONTACT_TYPE = 1 THEN lfp.TargetContactId
               ELSE UUID()
             END AS CHAR(36)) AS TargetContactId,
        CAST(CASE WHEN c.CONTACT_TYPE IN (6,7,8) THEN lfp.TargetContactId
                  ELSE NULL END AS CHAR(36)) AS TargetLawFirmId,
        LEFT(COALESCE(
            NULLIF(TRIM(c.CONTACT_NAME),''),
            NULLIF(TRIM(CONCAT_WS(' ',
                NULLIF(TRIM(c.CONTACT_FIRSTNAME),''),
                NULLIF(TRIM(c.CONTACT_LASTNAME),''))), ''),
            CONCAT('Legacy Contact ', c.CONTACT_ID)), 250) AS DisplayName,
        LEFT(COALESCE(
            NULLIF(TRIM(c.CONTACT_FIRSTNAME),''),
            NULLIF(TRIM(SUBSTRING_INDEX(
                COALESCE(NULLIF(TRIM(c.CONTACT_NAME),''),''), ' ', 1)),''),
            'Legacy'), 100) AS FirstName,
        LEFT(COALESCE(
            NULLIF(TRIM(c.CONTACT_LASTNAME),''),
            NULLIF(TRIM(SUBSTRING(
                COALESCE(NULLIF(TRIM(c.CONTACT_NAME),''),''),
                CHAR_LENGTH(SUBSTRING_INDEX(
                    COALESCE(NULLIF(TRIM(c.CONTACT_NAME),''),''), ' ', 1)) + 1)),''),
            CASE WHEN c.CONTACT_TYPE = 1 THEN '' ELSE 'Legacy' END), 100) AS LastName,
        LEFT(NULLIF(TRIM(c.CONTACT_NAME),''), 200) AS Organization,
        NULLIF(TRIM(c.CONTACT_EMAIL),'')   AS Email,
        LEFT(NULLIF(TRIM(c.CONTACT_PHONE),''), 30) AS Phone,
        NULLIF(TRIM(c.CONTACT_ADDRESS),'') AS AddressLine1,
        NULLIF(TRIM(c.CONTACT_CITY),'')    AS City,
        NULLIF(TRIM(c.CONTACT_STATE),'')   AS State,
        NULLIF(TRIM(c.CONTACT_ZIP),'')     AS PostalCode,
        COALESCE(c.CONTACT_CREATED, UTC_TIMESTAMP(6)) AS CreatedAtUtc,
        COALESCE(c.CONTACT_UPDATED, c.CONTACT_CREATED, UTC_TIMESTAMP(6)) AS UpdatedAtUtc,
        SHA2(CONCAT_WS('|', c.CONTACT_ID, c.CONTACT_TYPE,
                       c.CONTACT_FIRSTNAME, c.CONTACT_LASTNAME,
                       c.CONTACT_EMAIL, c.CONTACT_PHONE,
                       c.CONTACT_ADDRESS, c.CONTACT_CITY, c.CONTACT_STATE, c.CONTACT_ZIP,
                       c.CONTACT_STATUS, c.CONTACT_PROGRAM,
                       c.CONTACT_NAME, c.CT_LAW_FIRM_ROLE_ID,
                       c.CONTACT_CREATED, c.CONTACT_UPDATED), 256) AS SourceHash
    FROM `SL-CORE`.`SL_CONTACT` c
    INNER JOIN `SL-CORE`.`SL_CONTACT_TYPE` ct
      ON ct.CT_ID = c.CONTACT_TYPE AND COALESCE(ct.CT_STATUS,'A') = 'A'
    LEFT JOIN tmp_sle_lf_parents lfp
      ON lfp.LegacyContactId = CASE
            WHEN c.CONTACT_TYPE = 1             THEN c.CONTACT_ID
            WHEN c.CONTACT_TYPE IN (6, 7, 8)    THEN c.CT_LAW_FIRM_ROLE_ID
            ELSE NULL
         END
    WHERE c.CONTACT_PROGRAM = CAST(v_legacy_program AS UNSIGNED)
      AND COALESCE(c.CONTACT_STATUS,'A') = 'A'
      AND c.CONTACT_TYPE IN (1, 2, 3, 4, 5, 6, 7, 8);

    DROP TEMPORARY TABLE IF EXISTS tmp_sle_lf_parents;

    SELECT COUNT(*) INTO v_contact_count FROM tmp_sle_contacts;
    IF v_contact_count = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-011 no active Program source contacts were found';
    END IF;
    IF EXISTS (SELECT 1 FROM tmp_sle_contacts
               WHERE TargetContactSubtype IS NOT NULL AND TargetLawFirmId IS NULL) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-012 a law-firm role contact has no active law-firm parent';
    END IF;
    IF EXISTS (SELECT 1 FROM tmp_sle_contacts WHERE CHAR_LENGTH(COALESCE(Phone,'')) > 30) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-013 contact phone number exceeds the 30-character target limit';
    END IF;

    -- -------------------------------------------------------------------------
    -- 8. Amount aggregates per lien (SUM over all LMC rows for that LM_ID)
    -- -------------------------------------------------------------------------
    DROP TEMPORARY TABLE IF EXISTS tmp_sle_amounts;
    CREATE TEMPORARY TABLE tmp_sle_amounts AS
    SELECT
        mc.LMC_LM_ID AS LegacyLienId,
        SUM(CASE
            WHEN NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT, ',',''), '$','')), '') IS NULL THEN 0
            WHEN TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT, ',',''), '$',''))
                 REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$'
            THEN CAST(TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT,',',''),'$','')) AS DECIMAL(20,2))
            ELSE 0 END) AS BillingAmount,
        SUM(CASE
            WHEN NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$','')), '') IS NULL THEN 0
            WHEN TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$',''))
                 REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$'
            THEN CAST(TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$','')) AS DECIMAL(20,2))
            ELSE 0 END) AS PurchaseAmount,
        -- Non-zero count of rows that were neither NULL/blank nor valid numeric.
        SUM(CASE
            WHEN NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT,',',''),'$','')), '') IS NULL THEN 0
            WHEN TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT,',',''),'$',''))
                 REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$' THEN 0
            ELSE 1 END) AS InvalidBilling,
        SUM(CASE
            WHEN NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$','')), '') IS NULL THEN 0
            WHEN TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$',''))
                 REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$' THEN 0
            ELSE 1 END) AS InvalidPurchase
    FROM `SL-CORE`.`SL_LEINS_MEDICAL_CODE` mc
    INNER JOIN `SL-CORE`.`SL_LEINS_MEDICAL` lm ON lm.LM_ID = mc.LMC_LM_ID
    WHERE COALESCE(lm.LM_IS_DELETED,'N') <> 'Y'
    GROUP BY mc.LMC_LM_ID;

    IF EXISTS (SELECT 1 FROM tmp_sle_amounts WHERE InvalidBilling <> 0 OR InvalidPurchase <> 0) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-014 non-numeric medical-code amount value in source data';
    END IF;

    -- -------------------------------------------------------------------------
    -- 9. Case-level settlement amounts from SL_SETTLEMENT_HEADER
    -- -------------------------------------------------------------------------
    DROP TEMPORARY TABLE IF EXISTS tmp_sle_settlements;
    CREATE TEMPORARY TABLE tmp_sle_settlements AS
    SELECT
        sh.SH_CASE_ID AS LegacyCaseId,
        -- Take the maximum settlement row per case (most recent or largest header).
        MAX(CASE
            WHEN NULLIF(TRIM(REPLACE(REPLACE(CAST(sh.SH_TOTAL_AMOUNT AS CHAR),',',''),'$','')), '') IS NULL
            THEN NULL
            WHEN TRIM(REPLACE(REPLACE(CAST(sh.SH_TOTAL_AMOUNT AS CHAR),',',''),'$',''))
                 REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$'
            THEN CAST(TRIM(REPLACE(REPLACE(CAST(sh.SH_TOTAL_AMOUNT AS CHAR),',',''),'$',''))
                      AS DECIMAL(20,2))
            ELSE NULL
        END) AS SettlementAmount
    FROM `SL-CORE`.`SL_SETTLEMENT_HEADER` sh
    WHERE sh.SH_CASE_ID IS NOT NULL
    GROUP BY sh.SH_CASE_ID;

    -- -------------------------------------------------------------------------
    -- 10. Cases staging
    -- Contacts (step 7) and accident-type lookups (step 5) are already built,
    -- so case Notes with [legacy-meta] metadata are computed here in one pass.
    -- -------------------------------------------------------------------------
    DROP TEMPORARY TABLE IF EXISTS tmp_sle_cases;
    CREATE TEMPORARY TABLE tmp_sle_cases AS
    SELECT
        c.CASE_ID AS LegacyCaseId,
        UUID()    AS TargetCaseId,
        CASE WHEN NULLIF(TRIM(c.CASE_CODE),'') IS NULL
             THEN CONCAT('SL-CORE-CASE-', c.CASE_ID)
             ELSE TRIM(c.CASE_CODE) END AS CaseNumber,
        TRIM(c.CASE_FNAME) AS FirstName,
        TRIM(c.CASE_LNAME) AS LastName,
        c.CASE_DOB AS DateOfBirth,
        NULLIF(CONCAT_WS(', ',
            NULLIF(TRIM(c.CASE_ADDRESS),''), NULLIF(TRIM(c.CASE_CITY),''),
            NULLIF(TRIM(c.CASE_STATE),''),   NULLIF(TRIM(c.CASE_ZIPCODE),'')), '') AS Address,
        CASE COALESCE(UPPER(TRIM(c.CASE_STATUS)),'')
          WHEN ''            THEN 'PreDemand'
          WHEN 'N'           THEN 'PreDemand'
          WHEN 'P'           THEN 'PreDemand'
          WHEN 'PD'          THEN 'PreDemand'
          WHEN 'NEW'         THEN 'PreDemand'
          WHEN 'PROCESSING'  THEN 'PreDemand'
          WHEN 'PRE-DEMAND'  THEN 'PreDemand'
          WHEN 'PREDEMAND'   THEN 'PreDemand'
          WHEN 'DS'          THEN 'DemandSent'
          WHEN 'DEMAND SENT' THEN 'DemandSent'
          WHEN 'NT'          THEN 'InNegotiation'
          WHEN 'LP'          THEN 'InNegotiation'
          WHEN 'LO'          THEN 'InNegotiation'
          WHEN 'LC'          THEN 'InNegotiation'
          WHEN 'NEGOTIATIONS' THEN 'InNegotiation'
          WHEN 'LITIGATION'  THEN 'InNegotiation'
          WHEN 'CS'          THEN 'CaseSettled'
          WHEN 'CASE SETTLED' THEN 'CaseSettled'
          WHEN 'C'           THEN 'Closed'
          WHEN 'CLOSED'      THEN 'Closed'
          ELSE NULL
        END AS Status,
        CASE
          WHEN c.CASE_DATE_OF_LOSS IS NULL OR TRIM(c.CASE_DATE_OF_LOSS) = ''
            THEN NULL
          WHEN TRIM(c.CASE_DATE_OF_LOSS) REGEXP '^[0-9]{4}-[0-9]{2}-[0-9]{2}$'
            THEN STR_TO_DATE(TRIM(c.CASE_DATE_OF_LOSS), '%Y-%m-%d')
          WHEN TRIM(c.CASE_DATE_OF_LOSS) REGEXP '^[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}$'
            THEN STR_TO_DATE(TRIM(c.CASE_DATE_OF_LOSS), '%c/%e/%Y')
          ELSE NULL
        END AS IncidentDate,
        c.CASE_DATE_OF_LOSS AS IncidentDateText,
        NULLIF(TRIM(c.CASE_NOTE),'') AS RawNotes,
        -- Pre-resolved contact UUIDs for [legacy-meta] block.
        lf.TargetContactId  AS LawFirmContactId,
        mgr.TargetContactId AS CaseManagerContactId,
        at_lv.LookupId      AS AccidentTypeLookupId,
        at_lv.LookupName    AS AccidentTypeLookupName,
        s.SettlementAmount,
        c.CASE_CREATED AS CreatedAtUtc,
        c.CASE_UPDATED AS UpdatedAtUtc,
        SHA2(CONCAT_WS('|', c.CASE_ID, c.CASE_CODE, c.CASE_FNAME, c.CASE_LNAME,
                       c.CASE_DOB, c.CASE_ADDRESS, c.CASE_CITY, c.CASE_STATE,
                       c.CASE_ZIPCODE, c.CASE_STATUS, c.CASE_DATE_OF_LOSS,
                       c.CASE_NOTE, c.CASE_CREATED, c.CASE_UPDATED, v_fingerprint), 256) AS SourceHash
    FROM `SL-CORE`.`SL_CASE` c
    -- Resolve law-firm contact UUID (CASE_LAW_FIRM → type-1 LawFirm contact)
    LEFT JOIN tmp_sle_contacts lf
      ON lf.LegacyContactId   = NULLIF(c.CASE_LAW_FIRM, 0)
     AND lf.TargetContactType = 'LawFirm'
     AND lf.TargetContactSubtype IS NULL
    -- Resolve case-manager contact UUID (CASE_MANAGER → type-6 CaseManager)
    LEFT JOIN tmp_sle_contacts mgr
      ON mgr.LegacyContactId    = NULLIF(c.CASE_MANAGER, 0)
     AND mgr.TargetContactType  = 'LawFirm'
     AND mgr.TargetContactSubtype = 'CaseManager'
    -- Resolve accident-type lookup (MatchCount=1 guard applied when building Notes)
    LEFT JOIN tmp_sle_at_lookups at_lv
      ON at_lv.LegacyAtId = NULLIF(c.CASE_ACCIDENT_TYPE, 0)
    -- Case-level settlement
    LEFT JOIN tmp_sle_settlements s ON s.LegacyCaseId = c.CASE_ID
    WHERE c.CASE_PROGRAM = CAST(v_legacy_program AS UNSIGNED)
      AND COALESCE(c.CASE_IS_DELETED,'N') <> 'Y';

    -- Compute [legacy-meta] block and final Notes in two UPDATE passes to avoid
    -- repeating the CONCAT_WS expression inside a CASE inside a CASE.
    ALTER TABLE tmp_sle_cases ADD COLUMN MetaBlock VARCHAR(1000) NULL;
    ALTER TABLE tmp_sle_cases ADD COLUMN NotesAfter VARCHAR(4000) NULL;

    UPDATE tmp_sle_cases
    SET MetaBlock = NULLIF(CONCAT_WS('; ',
        CASE WHEN LawFirmContactId IS NOT NULL
             THEN CONCAT('lawFirmId=', LawFirmContactId)           ELSE NULL END,
        CASE WHEN CaseManagerContactId IS NOT NULL
             THEN CONCAT('caseManagerId=', CaseManagerContactId)   ELSE NULL END,
        CASE WHEN AccidentTypeLookupId IS NOT NULL
             THEN CONCAT('accidentTypeId=', AccidentTypeLookupId)  ELSE NULL END,
        CASE WHEN AccidentTypeLookupName IS NOT NULL
             THEN CONCAT('accidentType=', AccidentTypeLookupName)  ELSE NULL END
    ), '');

    UPDATE tmp_sle_cases
    SET NotesAfter = CASE
        WHEN MetaBlock IS NULL
          THEN RawNotes
        WHEN RawNotes IS NULL OR TRIM(RawNotes) = ''
          THEN CONCAT('[legacy-meta]', CHAR(10), MetaBlock)
        ELSE
          CONCAT(RawNotes, CHAR(10), CHAR(10), '[legacy-meta]', CHAR(10), MetaBlock)
    END;

    -- Validate cases
    SELECT COUNT(*) INTO v_case_count FROM tmp_sle_cases;
    IF v_case_count = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-015 no eligible source cases found';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sle_cases
        WHERE FirstName IS NULL OR FirstName = ''
           OR LastName  IS NULL OR LastName  = ''
           OR Status    IS NULL
           OR CHAR_LENGTH(CaseNumber)              > 50
           OR CHAR_LENGTH(COALESCE(Address,''))    > 500
           OR CHAR_LENGTH(COALESCE(NotesAfter,'')) > 4000
           OR (IncidentDateText IS NOT NULL
               AND TRIM(IncidentDateText) <> ''
               AND IncidentDate IS NULL)
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-016 invalid case mapping (status, name, address, notes, or date)';
    END IF;
    IF EXISTS (SELECT CaseNumber FROM tmp_sle_cases GROUP BY CaseNumber HAVING COUNT(*) > 1) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-017 duplicate source case numbers';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sle_cases s
        INNER JOIN liens_Cases t ON t.TenantId = v_tenant_id AND t.CaseNumber = s.CaseNumber
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-018 target case number collision with existing data';
    END IF;

    -- -------------------------------------------------------------------------
    -- 11. Liens staging
    -- OriginalAmount = BillingAmount, PurchasePrice = PurchaseAmount.
    -- DemandAmount on the case is set after the INSERT (requires all lien rows).
    -- -------------------------------------------------------------------------
    DROP TEMPORARY TABLE IF EXISTS tmp_sle_liens;
    CREATE TEMPORARY TABLE tmp_sle_liens AS
    SELECT
        lm.LM_ID   AS LegacyLienId,
        UUID()      AS TargetLienId,
        c.TargetCaseId,
        c.LegacyCaseId,
        CASE WHEN NULLIF(TRIM(lm.LM_CODE),'') IS NULL
             THEN CONCAT('SL-CORE-LIEN-', lm.LM_ID)
             ELSE TRIM(lm.LM_CODE) END AS LienNumber,
        CASE COALESCE(UPPER(TRIM(lm.LM_STATUS)),'')
          WHEN '1'    THEN v_status_one
          WHEN '2'    THEN v_status_two
          WHEN ''     THEN 'Draft'
          WHEN 'DRAFT'  THEN 'Draft'
          WHEN 'OPEN'   THEN 'Active'
          WHEN 'ACTIVE' THEN 'Active'
          ELSE NULL
        END AS Status,
        NULLIF(TRIM(lm.LM_NOTE),'') AS Notes,
        lm.LM_CREATED           AS CreatedAtUtc,
        lm.LM_UPDATED           AS UpdatedAtUtc,
        lm.LM_INITIAL_SERVICE_DATE AS InitialServiceDate,
        lm.LM_END_SERVICE_DATE     AS EndServiceDate,
        CASE UPPER(TRIM(lm.LM_IS_BULK))
          WHEN 'Y' THEN 'Yes' WHEN 'YES' THEN 'Yes'
          WHEN 'N' THEN 'No'  WHEN 'NO'  THEN 'No'
          ELSE NULL END AS IsBulk,
        CASE UPPER(TRIM(lm.LM_IS_SERVICING))
          WHEN 'Y' THEN 'Yes' WHEN 'YES' THEN 'Yes'
          WHEN 'N' THEN 'No'  WHEN 'NO'  THEN 'No'
          ELSE NULL END AS IsServicing,
        c.FirstName AS SubjectFirstName,
        c.LastName  AS SubjectLastName,
        c.IncidentDate,
        COALESCE(a.BillingAmount,  0) AS BillingAmount,
        COALESCE(a.PurchaseAmount, 0) AS PurchaseAmount,
        SHA2(CONCAT_WS('|', lm.LM_ID, lm.LM_CASE_ID, lm.LM_STATUS, lm.LM_CODE,
                       lm.LM_NOTE, lm.LM_CREATED, lm.LM_UPDATED,
                       lm.LM_INITIAL_SERVICE_DATE, lm.LM_END_SERVICE_DATE,
                       lm.LM_IS_BULK, lm.LM_IS_SERVICING,
                       COALESCE(a.BillingAmount,0), COALESCE(a.PurchaseAmount,0),
                       v_fingerprint), 256) AS SourceHash
    FROM `SL-CORE`.`SL_LEINS_MEDICAL` lm
    INNER JOIN tmp_sle_cases c ON c.LegacyCaseId = lm.LM_CASE_ID
    LEFT JOIN  tmp_sle_amounts a ON a.LegacyLienId = lm.LM_ID
    WHERE COALESCE(lm.LM_IS_DELETED,'N') <> 'Y';

    SELECT COUNT(*) INTO v_lien_count FROM tmp_sle_liens;
    IF EXISTS (
        SELECT 1 FROM tmp_sle_liens
        WHERE Status IS NULL
           OR CHAR_LENGTH(LienNumber)          > 50
           OR CHAR_LENGTH(COALESCE(Notes,''))  > 4000
           OR BillingAmount  < 0 OR PurchaseAmount  < 0
           OR BillingAmount  > 9999999999999999.99
           OR PurchaseAmount > 9999999999999999.99
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-019 invalid lien mapping (status, number, notes, or amount)';
    END IF;
    IF EXISTS (SELECT LienNumber FROM tmp_sle_liens GROUP BY LienNumber HAVING COUNT(*) > 1) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-020 duplicate source lien numbers';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sle_liens s
        INNER JOIN liens_Liens t ON t.TenantId = v_tenant_id AND t.LienNumber = s.LienNumber
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-021 target lien number collision with existing data';
    END IF;

    -- -------------------------------------------------------------------------
    -- 12. Case notes staging
    -- -------------------------------------------------------------------------
    SELECT COUNT(*) INTO v_blank_notes
    FROM `SL-CORE`.`SL_CASE_NOTES` n
    INNER JOIN tmp_sle_cases c ON c.LegacyCaseId = n.CN_CASE_ID
    WHERE NULLIF(TRIM(n.CN_NOTE),'') IS NULL;

    DROP TEMPORARY TABLE IF EXISTS tmp_sle_notes;
    CREATE TEMPORARY TABLE tmp_sle_notes AS
    SELECT
        n.CN_ID  AS LegacyNoteId,
        UUID()   AS TargetNoteId,
        c.TargetCaseId,
        NULLIF(TRIM(n.CN_NOTE),'')    AS Content,
        n.CN_CREATED                  AS CreatedAtUtc,
        NULLIF(TRIM(n.CN_CREATED_BY),'') AS CreatedByName,
        n.CN_IS_DELETED               AS IsDeleted,
        SHA2(CONCAT_WS('|', n.CN_ID, n.CN_CASE_ID, n.CN_NOTE, n.CN_CREATED,
                       n.CN_CREATED_BY, n.CN_IS_DELETED, v_fingerprint), 256) AS SourceHash
    FROM `SL-CORE`.`SL_CASE_NOTES` n
    INNER JOIN tmp_sle_cases c ON c.LegacyCaseId = n.CN_CASE_ID
    WHERE NULLIF(TRIM(n.CN_NOTE),'') IS NOT NULL;

    SELECT COUNT(*) INTO v_note_count FROM tmp_sle_notes;
    IF EXISTS (
        SELECT 1 FROM tmp_sle_notes
        WHERE CHAR_LENGTH(Content) > 5000
           OR CHAR_LENGTH(COALESCE(CreatedByName,'')) > 250
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-022 case note content or author name exceeds target limit';
    END IF;

    -- -------------------------------------------------------------------------
    -- 13. Facilities staging
    -- -------------------------------------------------------------------------
    DROP TEMPORARY TABLE IF EXISTS tmp_sle_facilities;
    CREATE TEMPORARY TABLE tmp_sle_facilities AS
    SELECT
        f.FACILITY_ID AS LegacyFacilityId,
        UUID()         AS TargetFacilityId,
        LEFT(NULLIF(TRIM(f.FACILITY_NAME),''), 200)    AS Name,
        CONCAT('SL-CORE:SL_FACILITY:', f.FACILITY_ID)  AS ExternalReference,
        NULLIF(TRIM(f.FACILITY_ADDRESS),'')             AS AddressLine1,
        NULLIF(TRIM(f.FACILITY_CITY),'')                AS City,
        NULLIF(TRIM(f.FACILITY_STATE),'')               AS State,
        NULLIF(TRIM(f.FACILITY_ZIP),'')                 AS PostalCode,
        LEFT(NULLIF(TRIM(f.FACILITY_PHONE),''), 30)     AS Phone,
        NULLIF(TRIM(f.FACILITY_EMAIL),'')               AS Email,
        COALESCE(f.FACILITY_CREATED, UTC_TIMESTAMP(6))  AS CreatedAtUtc,
        COALESCE(f.FACILITY_UPDATED, f.FACILITY_CREATED, UTC_TIMESTAMP(6)) AS UpdatedAtUtc,
        SHA2(CONCAT_WS('|', f.FACILITY_ID, f.FACILITY_NAME, f.FACILITY_EMAIL,
                       f.FACILITY_PHONE, f.FACILITY_ADDRESS, f.FACILITY_CITY,
                       f.FACILITY_STATE, f.FACILITY_ZIP, f.FACILITY_STATUS,
                       f.FACILITY_PROGRAM, f.FACILITY_CREATED, f.FACILITY_UPDATED), 256) AS SourceHash
    FROM `SL-CORE`.`SL_FACILITY` f
    WHERE CAST(f.FACILITY_PROGRAM AS CHAR) = CAST(v_legacy_program AS CHAR)
      AND COALESCE(f.FACILITY_STATUS,'A') = 'A';

    SELECT COUNT(*) INTO v_facility_count FROM tmp_sle_facilities;
    IF v_facility_count = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-023 no active source facilities found';
    END IF;
    IF EXISTS (SELECT 1 FROM tmp_sle_facilities WHERE Name IS NULL OR Name = '') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-024 source facility has a blank name';
    END IF;
    IF EXISTS (
        SELECT LOWER(Name) FROM tmp_sle_facilities GROUP BY LOWER(Name) HAVING COUNT(*) > 1
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-025 duplicate facility names require reconciliation';
    END IF;

    -- -------------------------------------------------------------------------
    -- 14. Facility contact persons staging
    -- -------------------------------------------------------------------------
    DROP TEMPORARY TABLE IF EXISTS tmp_sle_fac_persons;
    CREATE TEMPORARY TABLE tmp_sle_fac_persons AS
    SELECT
        p.FCP_ID AS LegacyPersonId,
        f.TargetFacilityId,
        UUID() AS TargetPersonId,
        LEFT(COALESCE(
            NULLIF(TRIM(p.FCP_FIRSTNAME),''),
            NULLIF(TRIM(SUBSTRING_INDEX(COALESCE(NULLIF(TRIM(p.FCP_NAME),''),''), ' ', 1)),''),
            'Legacy'), 100) AS FirstName,
        LEFT(COALESCE(
            NULLIF(TRIM(p.FCP_LASTNAME),''),
            NULLIF(TRIM(SUBSTRING(
                COALESCE(NULLIF(TRIM(p.FCP_NAME),''),''),
                CHAR_LENGTH(SUBSTRING_INDEX(COALESCE(NULLIF(TRIM(p.FCP_NAME),''),''), ' ', 1)) + 1)),''),
            'Contact'), 100) AS LastName,
        NULLIF(TRIM(p.FCP_EMAIL),'')               AS Email,
        LEFT(NULLIF(TRIM(p.FCP_PHONE),''), 30)     AS Phone,
        COALESCE(p.FCP_CREATED, UTC_TIMESTAMP(6))  AS CreatedAtUtc,
        COALESCE(p.FCP_UPDATED, p.FCP_CREATED, UTC_TIMESTAMP(6)) AS UpdatedAtUtc,
        SHA2(CONCAT_WS('|', p.FCP_ID, p.FCP_FACILITY_ID, p.FCP_NAME,
                       p.FCP_FIRSTNAME, p.FCP_LASTNAME, p.FCP_EMAIL, p.FCP_PHONE,
                       p.FCP_STATUS, p.FCP_PROGRAM,
                       p.FCP_CREATED, p.FCP_UPDATED), 256) AS SourceHash
    FROM `SL-CORE`.`SL_FACILITY_CONTACT_PERSON` p
    INNER JOIN tmp_sle_facilities f ON f.LegacyFacilityId = p.FCP_FACILITY_ID
    WHERE p.FCP_PROGRAM = CAST(v_legacy_program AS UNSIGNED)
      AND COALESCE(p.FCP_STATUS,'A') = 'A';

    SELECT COUNT(*) INTO v_person_count FROM tmp_sle_fac_persons;

    -- -------------------------------------------------------------------------
    -- 15. Lien-facility links staging
    -- One facility per lien enforced; the MIN() in the apply step is a safety net.
    -- -------------------------------------------------------------------------
    DROP TEMPORARY TABLE IF EXISTS tmp_sle_fac_links;
    CREATE TEMPORARY TABLE tmp_sle_fac_links AS
    SELECT
        i.LMI_ID           AS LegacyLinkId,
        lien.TargetLienId,
        fac.TargetFacilityId,
        SHA2(CONCAT_WS('|', i.LMI_ID, i.LMI_LM_ID, i.LMI_FACILITY_ID,
                       i.LMI_CREATED, i.LMI_UPDATED), 256) AS SourceHash
    FROM `SL-CORE`.`SL_LEINS_MEDICAL_INFORMATION_FACILITY` i
    INNER JOIN tmp_sle_liens     lien ON lien.LegacyLienId  = i.LMI_LM_ID
    INNER JOIN tmp_sle_facilities fac ON fac.LegacyFacilityId = i.LMI_FACILITY_ID
    WHERE i.LMI_FACILITY_ID IS NOT NULL;

    IF EXISTS (
        SELECT TargetLienId FROM tmp_sle_fac_links
        GROUP BY TargetLienId HAVING COUNT(DISTINCT TargetFacilityId) > 1
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTE-026 a lien maps to multiple distinct legacy facilities';
    END IF;
    SELECT COUNT(DISTINCT TargetLienId) INTO v_fac_link_count FROM tmp_sle_fac_links;

    -- -------------------------------------------------------------------------
    -- 16. Medical-code ServicingItems staging (one row per LMC_ID)
    -- Each row stores individual amounts, not the lien aggregate.
    -- -------------------------------------------------------------------------
    DROP TEMPORARY TABLE IF EXISTS tmp_sle_med_codes;
    CREATE TEMPORARY TABLE tmp_sle_med_codes AS
    SELECT
        mc.LMC_ID  AS LegacyCodeId,
        lien.TargetLienId,
        lien.TargetCaseId,
        CONCAT('SLCORE-LMC-', mc.LMC_ID) AS TaskNumber,
        NULLIF(REPLACE(REPLACE(NULLIF(TRIM(mc.LMC_CODE),''), ';',' '), '=',' '),'') AS SafeCode,
        CASE
          WHEN NULLIF(TRIM(REPLACE(REPLACE(CAST(mc.LMC_MEDICARE_COST AS CHAR),',',''),'$','')), '') IS NULL
            THEN NULL
          WHEN TRIM(REPLACE(REPLACE(CAST(mc.LMC_MEDICARE_COST AS CHAR),',',''),'$',''))
               REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$'
          THEN CAST(TRIM(REPLACE(REPLACE(CAST(mc.LMC_MEDICARE_COST AS CHAR),',',''),'$',''))
                    AS DECIMAL(20,2))
          ELSE NULL
        END AS MedicareAmount,
        CASE
          WHEN NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT,',',''),'$','')), '') IS NULL THEN 0
          WHEN TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT,',',''),'$',''))
               REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$'
          THEN CAST(TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT,',',''),'$','')) AS DECIMAL(20,2))
          ELSE 0
        END AS BillingAmount,
        CASE
          WHEN NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$','')), '') IS NULL THEN 0
          WHEN TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$',''))
               REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$'
          THEN CAST(TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$','')) AS DECIMAL(20,2))
          ELSE 0
        END AS PurchaseAmount,
        CONCAT(
            'legacySource=SL-CORE:SL_LEINS_MEDICAL_CODE:', mc.LMC_ID, '; ',
            'code=', COALESCE(NULLIF(REPLACE(REPLACE(NULLIF(TRIM(mc.LMC_CODE),''),';',' '),'=',' '),''),''), '; ',
            'description=; ',
            'medicareCost=', COALESCE(CAST(
                CASE
                  WHEN NULLIF(TRIM(REPLACE(REPLACE(CAST(mc.LMC_MEDICARE_COST AS CHAR),',',''),'$','')), '') IS NULL
                    THEN NULL
                  WHEN TRIM(REPLACE(REPLACE(CAST(mc.LMC_MEDICARE_COST AS CHAR),',',''),'$',''))
                       REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$'
                  THEN CAST(TRIM(REPLACE(REPLACE(CAST(mc.LMC_MEDICARE_COST AS CHAR),',',''),'$',''))
                            AS DECIMAL(20,2))
                  ELSE NULL
                END AS CHAR), ''), '; ',
            'billingAmount=', CAST(
                CASE
                  WHEN NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT,',',''),'$','')), '') IS NULL THEN 0
                  WHEN TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT,',',''),'$',''))
                       REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$'
                  THEN CAST(TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT,',',''),'$','')) AS DECIMAL(20,2))
                  ELSE 0
                END AS CHAR), '; ',
            'purchaseAmount=', CAST(
                CASE
                  WHEN NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$','')), '') IS NULL THEN 0
                  WHEN TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$',''))
                       REGEXP '^-?[0-9]+(\\.[0-9]{1,2})?$'
                  THEN CAST(TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT,',',''),'$','')) AS DECIMAL(20,2))
                  ELSE 0
                END AS CHAR), '; ',
            'payee=; outboundCheckNumber='
        ) AS SvcNotes
    FROM `SL-CORE`.`SL_LEINS_MEDICAL_CODE` mc
    INNER JOIN tmp_sle_liens lien ON lien.LegacyLienId = mc.LMC_LM_ID;

    SELECT COUNT(*) INTO v_med_code_count FROM tmp_sle_med_codes;

    -- -------------------------------------------------------------------------
    -- 17. Medical-provider ServicingItems staging (one row per lien with provider)
    -- -------------------------------------------------------------------------
    DROP TEMPORARY TABLE IF EXISTS tmp_sle_providers;
    CREATE TEMPORARY TABLE tmp_sle_providers AS
    SELECT
        src.LegacyLienId,
        lien.TargetLienId,
        lien.TargetCaseId,
        CONCAT('SLCORE-LMFI-', src.LegacyLienId) AS TaskNumber,
        src.LegacyProviderCount,
        prov.TargetContactId AS TargetProviderId,
        CASE WHEN prov.TargetContactId IS NOT NULL
             THEN CONCAT('medicalProviderId=', prov.TargetContactId)
             ELSE NULL END AS SvcNotes
    FROM (
        -- Aggregate provider references per lien; a lien with > 1 distinct
        -- provider is ambiguous and must be skipped (matches backfill behavior).
        SELECT
            info.LMI_LM_ID AS LegacyLienId,
            COUNT(DISTINCT NULLIF(NULLIF(TRIM(CAST(info.LMI_MEDICAL_PROVIDER AS CHAR)),''),'0'))
                AS LegacyProviderCount,
            MIN(NULLIF(NULLIF(TRIM(CAST(info.LMI_MEDICAL_PROVIDER AS CHAR)),''),'0'))
                AS LegacyProviderId
        FROM `SL-CORE`.`SL_LEINS_MEDICAL_INFORMATION_FACILITY` info
        WHERE NULLIF(NULLIF(TRIM(CAST(info.LMI_MEDICAL_PROVIDER AS CHAR)),''),'0') IS NOT NULL
        GROUP BY info.LMI_LM_ID
    ) src
    INNER JOIN tmp_sle_liens lien ON lien.LegacyLienId = src.LegacyLienId
    LEFT JOIN tmp_sle_contacts prov
      ON prov.LegacyContactId    = CAST(src.LegacyProviderId AS UNSIGNED)
     AND prov.TargetContactType  = 'Provider';

    SELECT COUNT(*) INTO v_provider_count
    FROM tmp_sle_providers
    WHERE LegacyProviderCount = 1 AND TargetProviderId IS NOT NULL;

    -- Assign run IDs before branching so they appear in the preflight report.
    SET v_core_run_id    = UUID();
    SET v_contact_run_id = UUID();

    -- -------------------------------------------------------------------------
    -- 18. Preflight report (p_apply = '0') — no permanent writes
    -- -------------------------------------------------------------------------
    IF NOT v_apply THEN
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_providers;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_med_codes;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_fac_links;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_fac_persons;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_facilities;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_notes;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_liens;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_cases;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_settlements;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_amounts;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_contacts;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_at_lookups;
        SET @@session.time_zone = v_orig_tz; SET v_tz_changed = FALSE;
        DO RELEASE_LOCK(v_contact_lock); SET v_contact_locked = 0;
        DO RELEASE_LOCK(v_core_lock);    SET v_core_locked    = 0;
        SELECT
            'complete-migration-preflight-passed'       AS Result,
            v_approval_id                               AS ApprovalId,
            v_lookups_seeded                            AS AccidentTypeLookupsSeeded,
            v_case_count                                AS CasesToInsert,
            v_lien_count                                AS LiensToInsert,
            v_note_count                                AS CaseNotesToInsert,
            v_blank_notes                               AS BlankCaseNotesSkipped,
            v_contact_count                             AS ContactsToInsert,
            v_facility_count                            AS FacilitiesToInsert,
            v_person_count                              AS FacilityPersonsToInsert,
            v_fac_link_count                            AS LienFacilityLinksToApply,
            v_med_code_count                            AS MedicalCodeServicingItemsToInsert,
            v_provider_count                            AS MedicalProviderServicingItemsToInsert;
        -- Normal exit: locks released, temp tables dropped above.
    ELSE
        -- -------------------------------------------------------------------
        -- 19. Apply: one SERIALIZABLE transaction for all writes
        -- -------------------------------------------------------------------
        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
        START TRANSACTION;
        SET v_in_transaction = TRUE;

        -- Lock the approval row before consuming it.
        SELECT Id FROM liens_LegacyImportApprovals
        WHERE Id = v_approval_id
          AND TenantId = v_tenant_id AND SourceSystem = 'SL-CORE'
          AND Status = 'Approved' AND ConsumedAtUtc IS NULL
          AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > UTC_TIMESTAMP(6))
        FOR UPDATE;

        -- -------------------------------------------------------------------
        -- Wave A: Core import run (cases / liens / case notes)
        -- -------------------------------------------------------------------
        INSERT INTO liens_LegacyImportRuns (
            Id, ApprovalId, TenantId, OrgId, SourceSystem, SourceFingerprint,
            LegacyProgram, MappingVersion, MappingManifestHash,
            MappingApprovalReference, Status, StartedAtUtc, CreatedByUserId)
        VALUES (
            v_core_run_id, v_approval_id, v_tenant_id, v_org_id,
            'SL-CORE', LOWER(v_fingerprint), v_legacy_program,
            v_mapping_version, LOWER(v_mapping_hash),
            v_mapping_ref, 'Running', UTC_TIMESTAMP(6), v_user_id);

        -- Cases: DemandAmount set to NULL here; updated after liens are inserted.
        INSERT INTO liens_Cases (
            Id, TenantId, OrgId, CaseNumber, ExternalReference, Title,
            ClientFirstName, ClientLastName, ClientDob, ClientPhone, ClientEmail,
            ClientAddress, Status, DateOfIncident, OpenedAtUtc, ClosedAtUtc,
            InsuranceCarrier, PolicyNumber, ClaimNumber,
            DemandAmount, SettlementAmount,
            Description, Notes,
            CreatedByUserId, UpdatedByUserId, CreatedAtUtc, UpdatedAtUtc)
        SELECT
            TargetCaseId, v_tenant_id, v_org_id,
            CaseNumber, CONCAT('SL-CORE:SL_CASE:', LegacyCaseId), NULL,
            FirstName, LastName, DateOfBirth, NULL, NULL, Address,
            Status, IncidentDate,
            COALESCE(CreatedAtUtc, UTC_TIMESTAMP(6)),
            CASE WHEN Status IN ('Closed','CaseSettled')
                 THEN UpdatedAtUtc ELSE NULL END,
            NULL, NULL, NULL,
            NULL,                 -- DemandAmount: back-filled below
            SettlementAmount,
            NULL, NotesAfter,
            v_user_id, v_user_id,
            COALESCE(CreatedAtUtc, UTC_TIMESTAMP(6)),
            COALESCE(UpdatedAtUtc, CreatedAtUtc, UTC_TIMESTAMP(6))
        FROM tmp_sle_cases;
        SET v_cases_ins = ROW_COUNT();

        -- Liens:
        --   OriginalAmount  = BillingAmount   (total billed face value)
        --   CurrentBalance  = BillingAmount unless Settled, then 0
        --   PurchasePrice   = PurchaseAmount  (acquisition cost — previously NULL)
        INSERT INTO liens_Liens (
            Id, TenantId, OrgId, LienNumber, ExternalReference, LienType, Status,
            CaseId, FacilityId, SubjectPartyId, SubjectFirstName, SubjectLastName,
            IsConfidential, OriginalAmount, CurrentBalance, OfferPrice, PurchasePrice,
            PayoffAmount, Jurisdiction, Description, Notes, IncidentDate,
            InitialServiceDate, EndServiceDate, IsBulk, IsServicing,
            OpenedAtUtc, ClosedAtUtc, SellingOrgId, BuyingOrgId, HoldingOrgId,
            SellerStatus, ListingVisibility,
            CreatedByUserId, UpdatedByUserId, CreatedAtUtc, UpdatedAtUtc)
        SELECT
            TargetLienId, v_tenant_id, v_org_id,
            LienNumber, CONCAT('SL-CORE:SL_LEINS_MEDICAL:', LegacyLienId),
            'MedicalLien', Status,
            TargetCaseId, NULL, NULL, SubjectFirstName, SubjectLastName, 0,
            BillingAmount,                                                     -- OriginalAmount
            CASE WHEN Status = 'Settled' THEN 0 ELSE BillingAmount END,        -- CurrentBalance
            NULL,                                                               -- OfferPrice
            PurchaseAmount,                                                     -- PurchasePrice
            NULL, NULL, NULL, Notes, IncidentDate,
            InitialServiceDate, EndServiceDate, IsBulk, IsServicing,
            COALESCE(CreatedAtUtc, UTC_TIMESTAMP(6)),
            CASE WHEN Status = 'Settled'
                 THEN COALESCE(UpdatedAtUtc, CreatedAtUtc, UTC_TIMESTAMP(6))
                 ELSE NULL END,
            v_org_id, NULL, NULL, 'Draft', 'Private',
            v_user_id, v_user_id,
            COALESCE(CreatedAtUtc, UTC_TIMESTAMP(6)),
            COALESCE(UpdatedAtUtc, CreatedAtUtc, UTC_TIMESTAMP(6))
        FROM tmp_sle_liens;
        SET v_liens_ins = ROW_COUNT();

        -- DemandAmount = SUM(BillingAmount) per case across all imported liens.
        UPDATE liens_Cases tc
        INNER JOIN (
            SELECT TargetCaseId, SUM(BillingAmount) AS TotalBilling
            FROM tmp_sle_liens
            GROUP BY TargetCaseId
        ) demand ON demand.TargetCaseId = tc.Id
        SET tc.DemandAmount    = demand.TotalBilling,
            tc.UpdatedAtUtc    = UTC_TIMESTAMP(6),
            tc.UpdatedByUserId = v_user_id
        WHERE tc.TenantId = v_tenant_id AND tc.OrgId = v_org_id;

        -- Case notes
        INSERT INTO liens_CaseNotes (
            Id, CaseId, TenantId, Content, Category, IsPinned,
            CreatedByUserId, CreatedByName, IsEdited, IsDeleted,
            CreatedAtUtc, UpdatedAtUtc)
        SELECT
            TargetNoteId, TargetCaseId, v_tenant_id, Content,
            'general', 0, v_user_id,
            COALESCE(CreatedByName,'Legacy SL-CORE'), 0,
            CASE WHEN UPPER(COALESCE(IsDeleted,'N')) = 'Y' THEN 1 ELSE 0 END,
            COALESCE(CreatedAtUtc, UTC_TIMESTAMP(6)), NULL
        FROM tmp_sle_notes;
        SET v_notes_ins = ROW_COUNT();

        -- Core crosswalks
        INSERT INTO liens_LegacyIdCrosswalks (
            Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity,
            TargetId, SourceHash, ImportRunId, CreatedAtUtc)
        SELECT UUID(), v_tenant_id, 'SL-CORE', 'SL_CASE',
               CAST(LegacyCaseId AS CHAR), 'Case', TargetCaseId,
               SourceHash, v_core_run_id, UTC_TIMESTAMP(6)
        FROM tmp_sle_cases;

        INSERT INTO liens_LegacyIdCrosswalks (
            Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity,
            TargetId, SourceHash, ImportRunId, CreatedAtUtc)
        SELECT UUID(), v_tenant_id, 'SL-CORE', 'SL_LEINS_MEDICAL',
               CAST(LegacyLienId AS CHAR), 'Lien', TargetLienId,
               SourceHash, v_core_run_id, UTC_TIMESTAMP(6)
        FROM tmp_sle_liens;

        INSERT INTO liens_LegacyIdCrosswalks (
            Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity,
            TargetId, SourceHash, ImportRunId, CreatedAtUtc)
        SELECT UUID(), v_tenant_id, 'SL-CORE', 'SL_CASE_NOTES',
               CAST(LegacyNoteId AS CHAR), 'CaseNote', TargetNoteId,
               SourceHash, v_core_run_id, UTC_TIMESTAMP(6)
        FROM tmp_sle_notes;

        -- Postcondition: every imported lien must be owned by the correct tenant/org.
        SELECT COUNT(*) INTO v_postcondition_errors
        FROM liens_LegacyIdCrosswalks x
        LEFT JOIN liens_Liens  l ON x.TargetEntity = 'Lien' AND l.Id = x.TargetId
        LEFT JOIN liens_Cases  c ON l.CaseId = c.Id
        WHERE x.ImportRunId = v_core_run_id AND x.TargetEntity = 'Lien'
          AND (l.Id IS NULL
               OR l.TenantId <> v_tenant_id OR l.OrgId <> v_org_id
               OR c.Id IS NULL
               OR c.TenantId <> v_tenant_id OR c.OrgId <> v_org_id);
        IF v_postcondition_errors <> 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLTE-027 lien/case tenant-ownership postcondition failed';
        END IF;

        UPDATE liens_LegacyImportRuns
        SET Status = 'Completed', CompletedAtUtc = UTC_TIMESTAMP(6),
            SummaryJson = JSON_OBJECT(
                'casesInserted',         v_cases_ins,
                'liensInserted',         v_liens_ins,
                'caseNotesInserted',     v_notes_ins,
                'blankCaseNotesSkipped', v_blank_notes,
                'legacyProgram',         v_legacy_program,
                'runner',                'complete-migration-v1')
        WHERE Id = v_core_run_id AND TenantId = v_tenant_id;

        UPDATE liens_LegacyImportApprovals
        SET Status = 'Consumed', ConsumedAtUtc = UTC_TIMESTAMP(6),
            ConsumedByRunId = v_core_run_id
        WHERE Id = v_approval_id AND TenantId = v_tenant_id
          AND SourceSystem = 'SL-CORE' AND Status = 'Approved' AND ConsumedAtUtc IS NULL;
        IF ROW_COUNT() <> 1 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLTE-028 approval claim failed (concurrent consume race)';
        END IF;

        -- -------------------------------------------------------------------
        -- Wave B: Contacts / facilities import run
        -- Uses the same mapping versions as the standalone wave procedures so
        -- existing backfill checks recognise the completed runs.
        -- -------------------------------------------------------------------
        INSERT INTO liens_LegacyImportRuns (
            Id, ApprovalId, TenantId, OrgId, SourceSystem, SourceFingerprint,
            LegacyProgram, MappingVersion, MappingManifestHash,
            MappingApprovalReference, Status, StartedAtUtc, CreatedByUserId)
        VALUES (
            v_contact_run_id, NULL, v_tenant_id, v_org_id,
            'SL-CORE', LOWER(v_fingerprint), v_legacy_program,
            'sl-core-contact-facility-v1',
            '94fe9f0822713a646e7c54b07242eaaf10945e5c88e5105a4d754e29af949fe2',
            CONCAT('Core import ', v_core_run_id),
            'Running', UTC_TIMESTAMP(6), v_user_id);

        -- Law-firm parents and other non-role contacts first (no TargetLawFirmId dependency).
        INSERT INTO liens_Contacts (
            Id, TenantId, OrgId, ContactType, FirstName, LastName, DisplayName,
            Title, Organization, Email, Phone, Fax, Website,
            AddressLine1, City, State, PostalCode,
            Notes, IsActive, CreatedAtUtc, UpdatedAtUtc,
            CreatedByUserId, UpdatedByUserId,
            ContactSubtype, FacilityId, LawFirmId)
        SELECT
            TargetContactId, v_tenant_id, v_org_id, TargetContactType,
            FirstName, LastName, DisplayName,
            NULL, Organization, Email, Phone, NULL, NULL,
            AddressLine1, City, State, PostalCode,
            NULL, 1, CreatedAtUtc, UpdatedAtUtc,
            v_user_id, v_user_id,
            TargetContactSubtype, NULL, TargetLawFirmId
        FROM tmp_sle_contacts
        WHERE TargetContactSubtype IS NULL;
        SET v_contacts_ins = ROW_COUNT();

        -- Role contacts (CaseManager / Attorney / Other): TargetLawFirmId parents exist now.
        INSERT INTO liens_Contacts (
            Id, TenantId, OrgId, ContactType, FirstName, LastName, DisplayName,
            Title, Organization, Email, Phone, Fax, Website,
            AddressLine1, City, State, PostalCode,
            Notes, IsActive, CreatedAtUtc, UpdatedAtUtc,
            CreatedByUserId, UpdatedByUserId,
            ContactSubtype, FacilityId, LawFirmId)
        SELECT
            TargetContactId, v_tenant_id, v_org_id, TargetContactType,
            FirstName, LastName, DisplayName,
            NULL, Organization, Email, Phone, NULL, NULL,
            AddressLine1, City, State, PostalCode,
            NULL, 1, CreatedAtUtc, UpdatedAtUtc,
            v_user_id, v_user_id,
            TargetContactSubtype, NULL, TargetLawFirmId
        FROM tmp_sle_contacts
        WHERE TargetContactSubtype IS NOT NULL;
        SET v_contacts_ins = v_contacts_ins + ROW_COUNT();

        -- Facilities
        INSERT INTO liens_Facilities (
            Id, TenantId, OrgId, Name, Code, ExternalReference,
            AddressLine1, AddressLine2, City, State, PostalCode,
            Phone, Email, Fax, IsActive, OrganizationId,
            CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, UpdatedByUserId)
        SELECT
            TargetFacilityId, v_tenant_id, v_org_id, Name, NULL, ExternalReference,
            AddressLine1, NULL, City, State, PostalCode,
            Phone, Email, NULL, 1, NULL,
            CreatedAtUtc, UpdatedAtUtc, v_user_id, v_user_id
        FROM tmp_sle_facilities;
        SET v_facilities_ins = ROW_COUNT();

        -- MedicalFacility contact projection per facility (mirrors standalone wave).
        INSERT INTO liens_Contacts (
            Id, TenantId, OrgId, ContactType, FirstName, LastName, DisplayName,
            Title, Organization, Email, Phone, Fax, Website,
            AddressLine1, City, State, PostalCode,
            Notes, IsActive, CreatedAtUtc, UpdatedAtUtc,
            CreatedByUserId, UpdatedByUserId,
            ContactSubtype, FacilityId, LawFirmId)
        SELECT
            UUID(), v_tenant_id, v_org_id, 'MedicalFacility',
            LEFT(COALESCE(NULLIF(TRIM(SUBSTRING_INDEX(Name,' ',1)),''), 'Legacy'), 100),
            LEFT(COALESCE(NULLIF(TRIM(SUBSTRING(Name, CHAR_LENGTH(SUBSTRING_INDEX(Name,' ',1))+1)),''), 'Facility'), 100),
            LEFT(Name, 250),
            NULL, LEFT(Name, 200), Email, Phone, NULL, NULL,
            AddressLine1, City, State, PostalCode,
            CONCAT('legacySource=SL-CORE:SL_FACILITY:FacilityId=', TargetFacilityId),
            1, CreatedAtUtc, UTC_TIMESTAMP(6),
            v_user_id, v_user_id,
            NULL, TargetFacilityId, NULL
        FROM tmp_sle_facilities;
        SET v_fac_contacts_ins = ROW_COUNT();

        -- Facility contact persons
        INSERT INTO liens_FacilityContactPersons (
            Id, TenantId, FacilityId, FirstName, LastName, Position,
            Email, Phone, IsActive, CreatedAtUtc, UpdatedAtUtc,
            CreatedByUserId, UpdatedByUserId)
        SELECT
            TargetPersonId, v_tenant_id, TargetFacilityId, FirstName, LastName, NULL,
            Email, Phone, 1, CreatedAtUtc, UpdatedAtUtc, v_user_id, v_user_id
        FROM tmp_sle_fac_persons;
        SET v_persons_ins = ROW_COUNT();

        -- Lien-facility links: set FacilityId on liens_Liens rows.
        UPDATE liens_Liens l
        INNER JOIN (
            SELECT TargetLienId, MIN(TargetFacilityId) AS TargetFacilityId
            FROM tmp_sle_fac_links
            GROUP BY TargetLienId
        ) link ON link.TargetLienId = l.Id
        SET l.FacilityId       = link.TargetFacilityId,
            l.UpdatedAtUtc    = UTC_TIMESTAMP(6),
            l.UpdatedByUserId = v_user_id
        WHERE l.TenantId = v_tenant_id AND l.OrgId = v_org_id AND l.FacilityId IS NULL;
        SET v_fac_links_upd = ROW_COUNT();

        -- Contact crosswalks
        INSERT INTO liens_LegacyIdCrosswalks (
            Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity,
            TargetId, SourceHash, ImportRunId, CreatedAtUtc)
        SELECT UUID(), v_tenant_id, 'SL-CORE', 'SL_CONTACT',
               CAST(LegacyContactId AS CHAR), 'Contact', TargetContactId,
               SourceHash, v_contact_run_id, UTC_TIMESTAMP(6)
        FROM tmp_sle_contacts;

        -- Facility crosswalks
        INSERT INTO liens_LegacyIdCrosswalks (
            Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity,
            TargetId, SourceHash, ImportRunId, CreatedAtUtc)
        SELECT UUID(), v_tenant_id, 'SL-CORE', 'SL_FACILITY',
               CAST(LegacyFacilityId AS CHAR), 'Facility', TargetFacilityId,
               SourceHash, v_contact_run_id, UTC_TIMESTAMP(6)
        FROM tmp_sle_facilities;

        -- Facility contact person crosswalks
        INSERT INTO liens_LegacyIdCrosswalks (
            Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity,
            TargetId, SourceHash, ImportRunId, CreatedAtUtc)
        SELECT UUID(), v_tenant_id, 'SL-CORE', 'SL_FACILITY_CONTACT_PERSON',
               CAST(LegacyPersonId AS CHAR), 'FacilityContactPerson', TargetPersonId,
               SourceHash, v_contact_run_id, UTC_TIMESTAMP(6)
        FROM tmp_sle_fac_persons;

        -- Lien-facility link crosswalks
        INSERT INTO liens_LegacyIdCrosswalks (
            Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity,
            TargetId, SourceHash, ImportRunId, CreatedAtUtc)
        SELECT UUID(), v_tenant_id, 'SL-CORE',
               'SL_LEINS_MEDICAL_INFORMATION_FACILITY',
               CAST(LegacyLinkId AS CHAR), 'LienFacilityLink', TargetLienId,
               SourceHash, v_contact_run_id, UTC_TIMESTAMP(6)
        FROM tmp_sle_fac_links;

        UPDATE liens_LegacyImportRuns
        SET Status = 'Completed', CompletedAtUtc = UTC_TIMESTAMP(6),
            SummaryJson = JSON_OBJECT(
                'ContactsInserted',         v_contacts_ins,
                'FacilitiesInserted',        v_facilities_ins,
                'FacilityContactsInserted',  v_fac_contacts_ins,
                'FacilityPersonsInserted',   v_persons_ins,
                'LienFacilityLinksApplied',  v_fac_links_upd)
        WHERE Id = v_contact_run_id AND TenantId = v_tenant_id;

        -- -------------------------------------------------------------------
        -- Wave C: ServicingItems — medical code amounts (one per LMC_ID)
        -- -------------------------------------------------------------------
        INSERT INTO liens_ServicingItems (
            Id, TenantId, OrgId, TaskNumber, TaskType, Description, Status,
            Priority, AssignedTo, AssignedToUserId,
            CaseId, LienId, DueDate,
            Notes, Resolution, StartedAtUtc, CompletedAtUtc, EscalatedAtUtc,
            CreatedByUserId, UpdatedByUserId, CreatedAtUtc, UpdatedAtUtc)
        SELECT
            UUID(), v_tenant_id, v_org_id, TaskNumber, 'LegacyMedicalCode',
            CASE WHEN SafeCode IS NULL THEN 'Legacy medical code entry'
                 ELSE CONCAT('Medical code ', SafeCode) END,
            'Pending', 'Normal', 'system', NULL,
            TargetCaseId, TargetLienId, NULL,
            SvcNotes, NULL, NULL, NULL, NULL,
            v_user_id, v_user_id, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
        FROM tmp_sle_med_codes;
        SET v_med_svc_ins = ROW_COUNT();

        -- -------------------------------------------------------------------
        -- Wave D: ServicingItems — lien medical providers (one per lien)
        -- Skips liens with ambiguous (> 1) provider references.
        -- -------------------------------------------------------------------
        INSERT INTO liens_ServicingItems (
            Id, TenantId, OrgId, TaskNumber, TaskType, Description, Status,
            Priority, AssignedTo, AssignedToUserId,
            CaseId, LienId, DueDate,
            Notes, Resolution, StartedAtUtc, CompletedAtUtc, EscalatedAtUtc,
            CreatedByUserId, UpdatedByUserId, CreatedAtUtc, UpdatedAtUtc)
        SELECT
            UUID(), v_tenant_id, v_org_id, TaskNumber, 'LegacyMedicalFacilityInfo',
            'Legacy medical facility information',
            'Pending', 'Normal', 'system', NULL,
            TargetCaseId, TargetLienId, NULL,
            SvcNotes, NULL, NULL, NULL, NULL,
            v_user_id, v_user_id, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
        FROM tmp_sle_providers
        WHERE LegacyProviderCount = 1 AND TargetProviderId IS NOT NULL;
        SET v_prov_svc_ins = ROW_COUNT();

        COMMIT;
        SET v_in_transaction = FALSE;

        -- Post-commit cleanup
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_providers;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_med_codes;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_fac_links;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_fac_persons;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_facilities;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_notes;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_liens;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_cases;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_settlements;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_amounts;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_contacts;
        DROP TEMPORARY TABLE IF EXISTS tmp_sle_at_lookups;
        SET @@session.time_zone = v_orig_tz; SET v_tz_changed = FALSE;
        DO RELEASE_LOCK(v_contact_lock); SET v_contact_locked = 0;
        DO RELEASE_LOCK(v_core_lock);    SET v_core_locked    = 0;

        SELECT
            'complete-migration-applied'                AS Result,
            v_core_run_id                               AS CoreImportRunId,
            v_contact_run_id                            AS ContactImportRunId,
            v_cases_ins                                 AS CasesInserted,
            v_liens_ins                                 AS LiensInserted,
            v_notes_ins                                 AS CaseNotesInserted,
            v_blank_notes                               AS BlankCaseNotesSkipped,
            v_contacts_ins                              AS ContactsInserted,
            v_fac_contacts_ins                          AS FacilityContactsInserted,
            v_facilities_ins                            AS FacilitiesInserted,
            v_persons_ins                               AS FacilityPersonsInserted,
            v_fac_links_upd                             AS LienFacilityLinksApplied,
            v_med_svc_ins                               AS MedicalCodeServicingItemsInserted,
            v_prov_svc_ins                              AS MedicalProviderServicingItemsInserted,
            v_lookups_seeded                            AS AccidentTypeLookupsSeeded;
    END IF;
END$$

DELIMITER ;

-- Deploy with DBeaver "Execute SQL Script" (Alt+X).
--
-- Step 1 — preflight (no permanent writes; seeds missing AccidentType lookups):
--   CALL liens_migrate_sl_core_complete('<tenant-guid>', '0');
--
-- Step 2 — apply (run only after preflight reports the expected counts):
--   CALL liens_migrate_sl_core_complete('<tenant-guid>', '1');
--
-- The procedure creates two liens_LegacyImportRuns rows with the same mapping
-- versions used by the individual wave procedures:
--   Core:     MappingVersion = <from approval record>,  e.g. 'sl-core-core-liens-v1'
--   Contacts: MappingVersion = 'sl-core-contact-facility-v1'
-- This ensures all existing backfill procedures that check for those completed
-- runs continue to function (they will simply find everything AlreadyCorrect).
