-- MySQL 8.0+ controlled SL-CORE Program 1 contact/facility migration.
--
-- Scope: active legacy contacts (law firms, providers, funding companies,
-- medical facilities, leads, and law-firm staff), active facilities, facility
-- contact people, and the FacilityId relation on already-imported lien headers.
-- This does not create Identity users, modify cases, change lien amounts, or
-- overwrite an existing FacilityId/contact record.  Run only in LS_QA_LIENS.
--
-- Deploy in DBeaver with Execute SQL Script (Alt+X), then run:
--   CALL liens_import_sl_core_contacts_facilities_tenant_only('<tenant-guid>', '0');
--   CALL liens_import_sl_core_contacts_facilities_tenant_only('<tenant-guid>', '1');

USE `LS_QA_LIENS`;

DROP PROCEDURE IF EXISTS liens_import_sl_core_contacts_facilities_tenant_only;

DELIMITER $$

CREATE PROCEDURE liens_import_sl_core_contacts_facilities_tenant_only(
    IN p_tenant_id VARCHAR(64),
    IN p_apply VARCHAR(16)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id CHAR(36);
    DECLARE v_apply BOOLEAN;
    DECLARE v_lock_name VARCHAR(64);
    DECLARE v_lock_acquired INT DEFAULT 0;
    DECLARE v_in_transaction BOOLEAN DEFAULT FALSE;
    DECLARE v_original_time_zone VARCHAR(64);
    DECLARE v_time_zone_changed BOOLEAN DEFAULT FALSE;
    DECLARE v_table_count INT DEFAULT 0;
    DECLARE v_provenance_count INT DEFAULT 0;
    DECLARE v_core_run_count INT DEFAULT 0;
    DECLARE v_contact_run_count INT DEFAULT 0;
    DECLARE v_other_contact_run_count INT DEFAULT 0;
    DECLARE v_source_contact_count INT DEFAULT 0;
    DECLARE v_source_law_firm_count INT DEFAULT 0;
    DECLARE v_source_provider_count INT DEFAULT 0;
    DECLARE v_facility_contacts_to_insert INT DEFAULT 0;
    DECLARE v_contact_crosswalks_to_repair INT DEFAULT 0;
    DECLARE v_source_facility_count INT DEFAULT 0;
    DECLARE v_source_person_count INT DEFAULT 0;
    DECLARE v_source_link_count INT DEFAULT 0;
    DECLARE v_contacts_to_insert INT DEFAULT 0;
    DECLARE v_facilities_to_insert INT DEFAULT 0;
    DECLARE v_people_to_insert INT DEFAULT 0;
    DECLARE v_links_to_apply INT DEFAULT 0;
    DECLARE v_contacts_inserted INT DEFAULT 0;
    DECLARE v_facility_contacts_inserted INT DEFAULT 0;
    DECLARE v_facilities_inserted INT DEFAULT 0;
    DECLARE v_people_inserted INT DEFAULT 0;
    DECLARE v_links_applied INT DEFAULT 0;
    DECLARE v_core_run_id CHAR(36);
    DECLARE v_contact_run_id CHAR(36);
    DECLARE v_org_id CHAR(36);
    DECLARE v_migration_user_id CHAR(36);
    DECLARE v_source_fingerprint VARCHAR(128);
    DECLARE v_mapping_version VARCHAR(100) DEFAULT 'sl-core-contact-facility-v1';
    DECLARE v_mapping_manifest_hash CHAR(64) DEFAULT '94fe9f0822713a646e7c54b07242eaaf10945e5c88e5105a4d754e29af949fe2';
    DECLARE v_created_new_run BOOLEAN DEFAULT FALSE;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        IF v_in_transaction THEN ROLLBACK; SET v_in_transaction = FALSE; END IF;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_lien_facility_links;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_facility_people;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_facilities;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_law_firm_parents;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_contacts;
        IF v_time_zone_changed THEN SET @@session.time_zone = v_original_time_zone; END IF;
        IF v_lock_acquired = 1 THEN DO RELEASE_LOCK(v_lock_name); END IF;
        RESIGNAL;
    END;

    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    IF v_tenant_id IS NULL
       OR v_tenant_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-001 invalid tenant GUID';
    END IF;
    IF p_apply IS NULL OR p_apply NOT IN ('0', '1') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-002 apply must be exactly 0 or 1';
    END IF;
    SET v_apply = p_apply = '1';

    SET v_original_time_zone = @@session.time_zone;
    SET @@session.time_zone = '+00:00';
    SET v_time_zone_changed = TRUE;

    SET v_lock_name = CONCAT('liens:slcore:contacts:', v_tenant_id);
    SELECT GET_LOCK(v_lock_name, 10) INTO v_lock_acquired;
    IF COALESCE(v_lock_acquired, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-003 tenant contact migration is already active';
    END IF;

    SELECT COUNT(*) INTO v_table_count
    FROM information_schema.tables
    WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE'
      AND table_name IN (
          'liens_Contacts', 'liens_Facilities', 'liens_FacilityContactPersons',
          'liens_Liens', 'liens_LegacyIdCrosswalks', 'liens_LegacyImportRuns');
    IF v_table_count <> 6 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-004 target contact-import schema is incomplete';
    END IF;

    SELECT COUNT(*) INTO v_table_count
    FROM information_schema.tables
    WHERE table_schema = 'SL-CORE' AND table_type = 'BASE TABLE'
      AND table_name IN (
          'SL_CONTACT', 'SL_CONTACT_TYPE', 'SL_FACILITY',
          'SL_FACILITY_CONTACT_PERSON', 'SL_LEINS_MEDICAL',
          'SL_LEINS_MEDICAL_INFORMATION_FACILITY', 'SL_MIGRATION_SOURCE_PROVENANCE');
    IF v_table_count <> 7 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-004 controlled SL-CORE contact source tables are unavailable';
    END IF;

    -- The contact wave is tied to one completed Program 1 core import.  The
    -- mapping-version predicate is deliberately omitted: it supports the
    -- earlier reviewed core-import deployment while still requiring its case
    -- and lien crosswalks to be present.
    SELECT COUNT(*) INTO v_core_run_count
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id
      AND r.SourceSystem = 'SL-CORE'
      AND r.LegacyProgram = '1'
      AND r.Status = 'Completed'
      AND EXISTS (
          SELECT 1 FROM liens_LegacyIdCrosswalks x
          WHERE x.ImportRunId = r.Id AND x.TenantId = v_tenant_id
            AND x.SourceSystem = 'SL-CORE' AND x.SourceTable = 'SL_CASE'
            AND x.TargetEntity = 'Case')
      AND EXISTS (
          SELECT 1 FROM liens_LegacyIdCrosswalks x
          WHERE x.ImportRunId = r.Id AND x.TenantId = v_tenant_id
            AND x.SourceSystem = 'SL-CORE' AND x.SourceTable = 'SL_LEINS_MEDICAL'
            AND x.TargetEntity = 'Lien');
    IF v_core_run_count <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-005 exactly one completed Program 1 core import with case and lien crosswalks is required';
    END IF;

    SELECT r.Id, r.OrgId, r.CreatedByUserId, LOWER(r.SourceFingerprint)
      INTO v_core_run_id, v_org_id, v_migration_user_id, v_source_fingerprint
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id
      AND r.SourceSystem = 'SL-CORE'
      AND r.LegacyProgram = '1'
      AND r.Status = 'Completed'
      AND EXISTS (
          SELECT 1 FROM liens_LegacyIdCrosswalks x
          WHERE x.ImportRunId = r.Id AND x.TenantId = v_tenant_id
            AND x.SourceSystem = 'SL-CORE' AND x.SourceTable = 'SL_CASE'
            AND x.TargetEntity = 'Case')
      AND EXISTS (
          SELECT 1 FROM liens_LegacyIdCrosswalks x
          WHERE x.ImportRunId = r.Id AND x.TenantId = v_tenant_id
            AND x.SourceSystem = 'SL-CORE' AND x.SourceTable = 'SL_LEINS_MEDICAL'
            AND x.TargetEntity = 'Lien');

    SELECT COUNT(*) INTO v_provenance_count
    FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE`
    WHERE PROVENANCE_KEY = 'sl-core-current'
      AND LOWER(SOURCE_FINGERPRINT) = v_source_fingerprint
      AND IMPORT_SCOPE = 'sl-core-core-liens-v1';
    IF v_provenance_count <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-006 source provenance does not match the completed core import';
    END IF;

    SELECT COUNT(*) INTO v_contact_run_count
    FROM liens_LegacyImportRuns
    WHERE TenantId = v_tenant_id
      AND SourceSystem = 'SL-CORE'
      AND LegacyProgram = '1'
      AND MappingVersion = v_mapping_version
      AND Status = 'Completed';
    SELECT COUNT(*) INTO v_other_contact_run_count
    FROM liens_LegacyImportRuns
    WHERE TenantId = v_tenant_id
      AND SourceSystem = 'SL-CORE'
      AND LegacyProgram = '1'
      AND MappingVersion = v_mapping_version
      AND Status <> 'Completed';
    IF v_contact_run_count > 1 OR v_other_contact_run_count <> 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-007 existing contact migration runs require reconciliation';
    END IF;

    IF v_contact_run_count = 1 THEN
        SELECT Id INTO v_contact_run_id
        FROM liens_LegacyImportRuns
        WHERE TenantId = v_tenant_id
          AND SourceSystem = 'SL-CORE'
          AND LegacyProgram = '1'
          AND MappingVersion = v_mapping_version
          AND Status = 'Completed';
    ELSE
        SET v_contact_run_id = UUID();
    END IF;

    -- Build parent IDs directly from the permanent source table.  Do not
    -- derive this map from tmp_sl_core_contacts: some MySQL deployments throw
    -- ERROR 1137 when a temporary table is reopened in later statements.
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_law_firm_parents;
    CREATE TEMPORARY TABLE tmp_sl_core_law_firm_parents AS
    SELECT
        parent_contact.CONTACT_ID AS LegacyContactId,
        CAST(
            CASE
              WHEN CHAR_LENGTH(TRIM(parent_x.TargetId)) = 36
               AND SUBSTRING(TRIM(parent_x.TargetId), 9, 1) = '-'
               AND SUBSTRING(TRIM(parent_x.TargetId), 14, 1) = '-'
               AND SUBSTRING(TRIM(parent_x.TargetId), 19, 1) = '-'
               AND SUBSTRING(TRIM(parent_x.TargetId), 24, 1) = '-'
               AND REPLACE(TRIM(parent_x.TargetId), '-', '') NOT REGEXP '[^0-9A-Fa-f]'
                THEN TRIM(parent_x.TargetId)
              ELSE UUID()
            END AS CHAR(36)
        ) AS TargetContactId
    FROM `SL-CORE`.`SL_CONTACT` parent_contact
    INNER JOIN `SL-CORE`.`SL_CONTACT_TYPE` parent_type
      ON parent_type.CT_ID = parent_contact.CONTACT_TYPE
     AND COALESCE(parent_type.CT_STATUS, 'A') = 'A'
    LEFT JOIN liens_LegacyIdCrosswalks parent_x
      ON parent_x.TenantId = v_tenant_id
     AND parent_x.SourceSystem = 'SL-CORE'
     AND parent_x.SourceTable = 'SL_CONTACT'
     AND parent_x.LegacyId = CAST(parent_contact.CONTACT_ID AS CHAR)
    WHERE parent_contact.CONTACT_PROGRAM = 1
      AND COALESCE(parent_contact.CONTACT_STATUS, 'A') = 'A'
      AND parent_contact.CONTACT_TYPE = 1;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_contacts;
    CREATE TEMPORARY TABLE tmp_sl_core_contacts AS
    SELECT
        c.CONTACT_ID AS LegacyContactId,
        CASE c.CONTACT_TYPE
          WHEN 1 THEN 'LawFirm'
          WHEN 2 THEN 'Provider'
          WHEN 3 THEN 'FundingCompany'
          WHEN 4 THEN 'MedicalFacility'
          WHEN 5 THEN 'Lead'
          WHEN 6 THEN 'LawFirm'
          WHEN 7 THEN 'LawFirm'
          WHEN 8 THEN 'LawFirm'
        END AS TargetContactType,
        CASE c.CONTACT_TYPE
          WHEN 6 THEN 'CaseManager'
          WHEN 7 THEN 'Attorney'
          WHEN 8 THEN 'Other'
          ELSE NULL
        END AS TargetContactSubtype,
        CAST(c.CT_LAW_FIRM_ROLE_ID AS CHAR) AS ParentLegacyContactId,
        LEFT(COALESCE(NULLIF(TRIM(c.CONTACT_NAME), ''),
                      NULLIF(TRIM(CONCAT_WS(' ', NULLIF(TRIM(c.CONTACT_FIRSTNAME), ''), NULLIF(TRIM(c.CONTACT_LASTNAME), ''))), ''),
                      CONCAT('Legacy Contact ', c.CONTACT_ID)), 250) AS DisplayName,
        LEFT(COALESCE(NULLIF(TRIM(c.CONTACT_FIRSTNAME), ''),
                      NULLIF(TRIM(SUBSTRING_INDEX(COALESCE(NULLIF(TRIM(c.CONTACT_NAME), ''), ''), ' ', 1)), ''),
                      'Legacy'), 100) AS FirstName,
        LEFT(COALESCE(NULLIF(TRIM(c.CONTACT_LASTNAME), ''),
                      NULLIF(TRIM(SUBSTRING(COALESCE(NULLIF(TRIM(c.CONTACT_NAME), ''), ''),
                          CHAR_LENGTH(SUBSTRING_INDEX(COALESCE(NULLIF(TRIM(c.CONTACT_NAME), ''), ''), ' ', 1)) + 1)), ''),
                      CASE WHEN c.CONTACT_TYPE = 1 THEN '' ELSE 'Legacy' END), 100) AS LastName,
        LEFT(NULLIF(TRIM(c.CONTACT_NAME), ''), 200) AS Organization,
        NULLIF(TRIM(c.CONTACT_EMAIL), '') AS Email,
        NULLIF(TRIM(c.CONTACT_PHONE), '') AS Phone,
        NULLIF(TRIM(c.CONTACT_ADDRESS), '') AS AddressLine1,
        NULLIF(TRIM(c.CONTACT_CITY), '') AS City,
        NULLIF(TRIM(c.CONTACT_STATE), '') AS State,
        NULLIF(TRIM(c.CONTACT_ZIP), '') AS PostalCode,
        COALESCE(c.CONTACT_CREATED, UTC_TIMESTAMP(6)) AS CreatedAtUtc,
        COALESCE(c.CONTACT_UPDATED, c.CONTACT_CREATED, UTC_TIMESTAMP(6)) AS UpdatedAtUtc,
        SHA2(CONCAT_WS('|', c.CONTACT_ID, c.CONTACT_TYPE, c.CONTACT_FIRSTNAME, c.CONTACT_LASTNAME,
                       c.CONTACT_EMAIL, c.CONTACT_PHONE, c.CONTACT_ADDRESS, c.CONTACT_CITY,
                       c.CONTACT_STATE, c.CONTACT_ZIP, c.CONTACT_STATUS, c.CONTACT_PROGRAM,
                       c.CONTACT_NAME, c.CT_LAW_FIRM_ROLE_ID, c.CONTACT_CREATED, c.CONTACT_UPDATED), 256) AS SourceHash,
        x.Id AS ExistingCrosswalkId,
        CASE
          WHEN CHAR_LENGTH(TRIM(x.TargetId)) = 36
           AND SUBSTRING(TRIM(x.TargetId), 9, 1) = '-'
           AND SUBSTRING(TRIM(x.TargetId), 14, 1) = '-'
           AND SUBSTRING(TRIM(x.TargetId), 19, 1) = '-'
           AND SUBSTRING(TRIM(x.TargetId), 24, 1) = '-'
           AND REPLACE(TRIM(x.TargetId), '-', '') NOT REGEXP '[^0-9A-Fa-f]'
            THEN TRIM(x.TargetId)
          ELSE NULL
        END AS ExistingTargetId,
        x.TargetEntity AS ExistingTargetEntity,
        x.SourceHash AS ExistingSourceHash,
        x.ImportRunId AS ExistingImportRunId,
        CAST(CASE
               WHEN c.CONTACT_TYPE = 1 THEN law_firm_parent.TargetContactId
               WHEN CHAR_LENGTH(TRIM(x.TargetId)) = 36
                AND SUBSTRING(TRIM(x.TargetId), 9, 1) = '-'
                AND SUBSTRING(TRIM(x.TargetId), 14, 1) = '-'
                AND SUBSTRING(TRIM(x.TargetId), 19, 1) = '-'
                AND SUBSTRING(TRIM(x.TargetId), 24, 1) = '-'
                AND REPLACE(TRIM(x.TargetId), '-', '') NOT REGEXP '[^0-9A-Fa-f]'
                 THEN TRIM(x.TargetId)
               ELSE UUID()
             END AS CHAR(36)) AS TargetContactId,
        CAST(CASE WHEN c.CONTACT_TYPE IN (6,7,8) THEN law_firm_parent.TargetContactId
                  ELSE NULL END AS CHAR(36)) AS TargetLawFirmId
    FROM `SL-CORE`.`SL_CONTACT` c
    INNER JOIN `SL-CORE`.`SL_CONTACT_TYPE` ct
      ON ct.CT_ID = c.CONTACT_TYPE AND COALESCE(ct.CT_STATUS, 'A') = 'A'
    LEFT JOIN liens_LegacyIdCrosswalks x
      ON x.TenantId = v_tenant_id
     AND x.SourceSystem = 'SL-CORE'
     AND x.SourceTable = 'SL_CONTACT'
     AND x.LegacyId = CAST(c.CONTACT_ID AS CHAR)
    LEFT JOIN tmp_sl_core_law_firm_parents law_firm_parent
      ON law_firm_parent.LegacyContactId = CASE
            WHEN c.CONTACT_TYPE = 1 THEN c.CONTACT_ID
            WHEN c.CONTACT_TYPE IN (6,7,8) THEN c.CT_LAW_FIRM_ROLE_ID
            ELSE NULL
          END
    WHERE c.CONTACT_PROGRAM = 1
      AND COALESCE(c.CONTACT_STATUS, 'A') = 'A'
      AND c.CONTACT_TYPE IN (1,2,3,4,5,6,7,8);

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_law_firm_parents;

    SELECT COUNT(*) INTO v_source_contact_count FROM tmp_sl_core_contacts;
    IF v_source_contact_count = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-008 no active Program 1 contacts were found';
    END IF;
    IF EXISTS (SELECT 1 FROM tmp_sl_core_contacts WHERE TargetContactSubtype IS NOT NULL AND TargetLawFirmId IS NULL) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-009 law-firm role contact has no active Program 1 law-firm parent';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sl_core_contacts
        WHERE ExistingCrosswalkId IS NOT NULL
          AND ExistingTargetId IS NULL
          AND COALESCE(ExistingTargetEntity, '') <> 'Contact'
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-030 invalid SL_CONTACT crosswalk has an unexpected target entity';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sl_core_contacts
        WHERE TargetContactId IS NULL
           OR CHAR_LENGTH(TRIM(TargetContactId)) <> 36
           OR SUBSTRING(TRIM(TargetContactId), 9, 1) <> '-'
           OR SUBSTRING(TRIM(TargetContactId), 14, 1) <> '-'
           OR SUBSTRING(TRIM(TargetContactId), 19, 1) <> '-'
           OR SUBSTRING(TRIM(TargetContactId), 24, 1) <> '-'
           OR REPLACE(TRIM(TargetContactId), '-', '') REGEXP '[^0-9A-Fa-f]'
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-027 generated or mapped contact ID is not a UUID';
    END IF;
    SELECT COUNT(*) INTO v_contact_crosswalks_to_repair
    FROM tmp_sl_core_contacts
    WHERE ExistingCrosswalkId IS NOT NULL AND ExistingTargetId IS NULL;
    IF EXISTS (SELECT 1 FROM tmp_sl_core_contacts WHERE CHAR_LENGTH(COALESCE(Phone, '')) > 30) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-010 legacy contact phone exceeds the target limit';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sl_core_contacts
        GROUP BY TargetContactType, COALESCE(TargetContactSubtype, ''), COALESCE(ParentLegacyContactId, ''), DisplayName
        HAVING COUNT(*) > 1
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-011 duplicate legacy contact natural keys require reconciliation';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sl_core_contacts s
        WHERE s.ExistingTargetId IS NOT NULL
          AND (s.ExistingTargetEntity <> 'Contact' OR s.ExistingSourceHash <> s.SourceHash OR s.ExistingImportRunId <> v_contact_run_id
               OR NOT EXISTS (
                   SELECT 1 FROM liens_Contacts t
                   WHERE t.Id = s.ExistingTargetId AND t.TenantId = v_tenant_id AND t.OrgId = v_org_id
                     AND t.ContactType = s.TargetContactType
                     AND t.ContactSubtype <=> s.TargetContactSubtype
                     AND t.LawFirmId <=> s.TargetLawFirmId
                     AND t.FirstName = s.FirstName AND t.LastName = s.LastName
                     AND t.DisplayName = s.DisplayName AND t.Organization <=> s.Organization
                     AND t.Email <=> s.Email AND t.Phone <=> s.Phone
                     AND t.AddressLine1 <=> s.AddressLine1 AND t.City <=> s.City
                     AND t.State <=> s.State AND t.PostalCode <=> s.PostalCode
                     AND t.IsActive = 1))
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-012 existing contact crosswalks conflict with source or target data';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sl_core_contacts s
        INNER JOIN liens_Contacts t
          ON t.TenantId = v_tenant_id AND t.OrgId = v_org_id
         AND t.ContactType = s.TargetContactType
         AND t.ContactSubtype <=> s.TargetContactSubtype
         AND t.LawFirmId <=> s.TargetLawFirmId
         AND LOWER(t.DisplayName) = LOWER(s.DisplayName)
        WHERE s.ExistingTargetId IS NULL
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-013 existing target contact collides with a legacy contact';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_facilities;
    CREATE TEMPORARY TABLE tmp_sl_core_facilities AS
    SELECT
        f.FACILITY_ID AS LegacyFacilityId,
        LEFT(NULLIF(TRIM(f.FACILITY_NAME), ''), 200) AS Name,
        CONCAT('SL-CORE:SL_FACILITY:', f.FACILITY_ID) AS ExternalReference,
        NULLIF(TRIM(f.FACILITY_ADDRESS), '') AS AddressLine1,
        NULLIF(TRIM(f.FACILITY_CITY), '') AS City,
        NULLIF(TRIM(f.FACILITY_STATE), '') AS State,
        NULLIF(TRIM(f.FACILITY_ZIP), '') AS PostalCode,
        NULLIF(TRIM(f.FACILITY_PHONE), '') AS Phone,
        NULLIF(TRIM(f.FACILITY_EMAIL), '') AS Email,
        COALESCE(f.FACILITY_CREATED, UTC_TIMESTAMP(6)) AS CreatedAtUtc,
        COALESCE(f.FACILITY_UPDATED, f.FACILITY_CREATED, UTC_TIMESTAMP(6)) AS UpdatedAtUtc,
        SHA2(CONCAT_WS('|', f.FACILITY_ID, f.FACILITY_NAME, f.FACILITY_EMAIL, f.FACILITY_PHONE,
                       f.FACILITY_ADDRESS, f.FACILITY_CITY, f.FACILITY_STATE, f.FACILITY_ZIP,
                       f.FACILITY_STATUS, f.FACILITY_PROGRAM, f.FACILITY_CREATED, f.FACILITY_UPDATED), 256) AS SourceHash,
        x.TargetId AS ExistingTargetId,
        x.TargetEntity AS ExistingTargetEntity,
        x.SourceHash AS ExistingSourceHash,
        x.ImportRunId AS ExistingImportRunId,
        CAST(COALESCE(NULLIF(TRIM(x.TargetId), ''), UUID()) AS CHAR(36)) AS TargetFacilityId
    FROM `SL-CORE`.`SL_FACILITY` f
    LEFT JOIN liens_LegacyIdCrosswalks x
      ON x.TenantId = v_tenant_id
     AND x.SourceSystem = 'SL-CORE'
     AND x.SourceTable = 'SL_FACILITY'
     AND x.LegacyId = CAST(f.FACILITY_ID AS CHAR)
    WHERE f.FACILITY_PROGRAM = '1'
      AND COALESCE(f.FACILITY_STATUS, 'A') = 'A';

    SELECT COUNT(*) INTO v_source_facility_count FROM tmp_sl_core_facilities;
    IF v_source_facility_count = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-014 no active Program 1 facilities were found';
    END IF;
    IF EXISTS (SELECT 1 FROM tmp_sl_core_facilities WHERE Name IS NULL OR Name = '' OR CHAR_LENGTH(COALESCE(Phone, '')) > 30) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-015 legacy facility name is blank or phone exceeds the target limit';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sl_core_facilities
        WHERE TargetFacilityId IS NULL
           OR CHAR_LENGTH(TRIM(TargetFacilityId)) <> 36
           OR SUBSTRING(TRIM(TargetFacilityId), 9, 1) <> '-'
           OR SUBSTRING(TRIM(TargetFacilityId), 14, 1) <> '-'
           OR SUBSTRING(TRIM(TargetFacilityId), 19, 1) <> '-'
           OR SUBSTRING(TRIM(TargetFacilityId), 24, 1) <> '-'
           OR REPLACE(TRIM(TargetFacilityId), '-', '') REGEXP '[^0-9A-Fa-f]'
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-028 generated or mapped facility ID is not a UUID';
    END IF;
    IF EXISTS (SELECT 1 FROM tmp_sl_core_facilities GROUP BY LOWER(Name) HAVING COUNT(*) > 1) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-016 duplicate legacy facility names require reconciliation';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sl_core_facilities s
        WHERE s.ExistingTargetId IS NOT NULL
          AND (s.ExistingTargetEntity <> 'Facility' OR s.ExistingSourceHash <> s.SourceHash OR s.ExistingImportRunId <> v_contact_run_id
               OR NOT EXISTS (
                   SELECT 1 FROM liens_Facilities t
                   WHERE t.Id = s.ExistingTargetId AND t.TenantId = v_tenant_id AND t.OrgId = v_org_id
                     AND t.Name = s.Name AND t.ExternalReference = s.ExternalReference
                     AND t.AddressLine1 <=> s.AddressLine1 AND t.City <=> s.City
                     AND t.State <=> s.State AND t.PostalCode <=> s.PostalCode
                     AND t.Phone <=> s.Phone AND t.Email <=> s.Email AND t.IsActive = 1))
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-017 existing facility crosswalks conflict with source or target data';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sl_core_facilities s
        INNER JOIN liens_Facilities t
          ON t.TenantId = v_tenant_id AND t.OrgId = v_org_id AND LOWER(t.Name) = LOWER(s.Name)
        WHERE s.ExistingTargetId IS NULL
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-018 existing target facility collides with a legacy facility';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_facility_people;
    CREATE TEMPORARY TABLE tmp_sl_core_facility_people AS
    SELECT
        p.FCP_ID AS LegacyFacilityPersonId,
        f.TargetFacilityId,
        LEFT(COALESCE(NULLIF(TRIM(p.FCP_FIRSTNAME), ''), NULLIF(TRIM(SUBSTRING_INDEX(COALESCE(NULLIF(TRIM(p.FCP_NAME), ''), ''), ' ', 1)), ''), 'Legacy'), 100) AS FirstName,
        LEFT(COALESCE(NULLIF(TRIM(p.FCP_LASTNAME), ''), NULLIF(TRIM(SUBSTRING(COALESCE(NULLIF(TRIM(p.FCP_NAME), ''), ''), CHAR_LENGTH(SUBSTRING_INDEX(COALESCE(NULLIF(TRIM(p.FCP_NAME), ''), ''), ' ', 1)) + 1)), ''), 'Contact'), 100) AS LastName,
        NULLIF(TRIM(p.FCP_EMAIL), '') AS Email,
        NULLIF(TRIM(p.FCP_PHONE), '') AS Phone,
        COALESCE(p.FCP_CREATED, UTC_TIMESTAMP(6)) AS CreatedAtUtc,
        COALESCE(p.FCP_UPDATED, p.FCP_CREATED, UTC_TIMESTAMP(6)) AS UpdatedAtUtc,
        SHA2(CONCAT_WS('|', p.FCP_ID, p.FCP_FACILITY_ID, p.FCP_NAME, p.FCP_FIRSTNAME, p.FCP_LASTNAME,
                       p.FCP_EMAIL, p.FCP_PHONE, p.FCP_STATUS, p.FCP_PROGRAM, p.FCP_CREATED, p.FCP_UPDATED), 256) AS SourceHash,
        x.TargetId AS ExistingTargetId,
        x.TargetEntity AS ExistingTargetEntity,
        x.SourceHash AS ExistingSourceHash,
        x.ImportRunId AS ExistingImportRunId,
        CAST(COALESCE(NULLIF(TRIM(x.TargetId), ''), UUID()) AS CHAR(36)) AS TargetFacilityPersonId
    FROM `SL-CORE`.`SL_FACILITY_CONTACT_PERSON` p
    INNER JOIN tmp_sl_core_facilities f ON f.LegacyFacilityId = p.FCP_FACILITY_ID
    LEFT JOIN liens_LegacyIdCrosswalks x
      ON x.TenantId = v_tenant_id
     AND x.SourceSystem = 'SL-CORE'
     AND x.SourceTable = 'SL_FACILITY_CONTACT_PERSON'
     AND x.LegacyId = CAST(p.FCP_ID AS CHAR)
    WHERE p.FCP_PROGRAM = 1 AND COALESCE(p.FCP_STATUS, 'A') = 'A';

    SELECT COUNT(*) INTO v_source_person_count FROM tmp_sl_core_facility_people;
    IF EXISTS (SELECT 1 FROM tmp_sl_core_facility_people WHERE CHAR_LENGTH(COALESCE(Phone, '')) > 30) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-019 facility-contact phone exceeds the target limit';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sl_core_facility_people
        WHERE TargetFacilityPersonId IS NULL
           OR CHAR_LENGTH(TRIM(TargetFacilityPersonId)) <> 36
           OR SUBSTRING(TRIM(TargetFacilityPersonId), 9, 1) <> '-'
           OR SUBSTRING(TRIM(TargetFacilityPersonId), 14, 1) <> '-'
           OR SUBSTRING(TRIM(TargetFacilityPersonId), 19, 1) <> '-'
           OR SUBSTRING(TRIM(TargetFacilityPersonId), 24, 1) <> '-'
           OR REPLACE(TRIM(TargetFacilityPersonId), '-', '') REGEXP '[^0-9A-Fa-f]'
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-029 generated or mapped facility-contact ID is not a UUID';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sl_core_facility_people
        GROUP BY TargetFacilityId, LOWER(FirstName), LOWER(LastName), COALESCE(LOWER(Email), '')
        HAVING COUNT(*) > 1
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-020 duplicate facility-contact natural keys require reconciliation';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sl_core_facility_people s
        WHERE s.ExistingTargetId IS NOT NULL
          AND (s.ExistingTargetEntity <> 'FacilityContactPerson' OR s.ExistingSourceHash <> s.SourceHash OR s.ExistingImportRunId <> v_contact_run_id
               OR NOT EXISTS (
                   SELECT 1 FROM liens_FacilityContactPersons t
                   WHERE t.Id = s.ExistingTargetId AND t.TenantId = v_tenant_id
                     AND t.FacilityId = s.TargetFacilityId
                     AND t.FirstName = s.FirstName AND t.LastName = s.LastName
                     AND t.Email <=> s.Email AND t.Phone <=> s.Phone AND t.IsActive = 1))
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-021 existing facility-contact crosswalks conflict with source or target data';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sl_core_facility_people s
        INNER JOIN liens_FacilityContactPersons t
          ON t.TenantId = v_tenant_id AND t.FacilityId = s.TargetFacilityId
         AND LOWER(t.FirstName) = LOWER(s.FirstName) AND LOWER(t.LastName) = LOWER(s.LastName)
         AND COALESCE(LOWER(t.Email), '') = COALESCE(LOWER(s.Email), '')
        WHERE s.ExistingTargetId IS NULL
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-022 existing target facility contact collides with a legacy contact';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_lien_facility_links;
    CREATE TEMPORARY TABLE tmp_sl_core_lien_facility_links AS
    SELECT
        i.LMI_ID AS LegacyLienFacilityLinkId,
        lien_x.TargetId AS TargetLienId,
        facility.TargetFacilityId,
        SHA2(CONCAT_WS('|', i.LMI_ID, i.LMI_LM_ID, i.LMI_FACILITY_ID, i.LMI_FACILITY_CONTACT_ID,
                       i.LMI_EMAIL, i.LMI_PHONE, i.LMI_MEDICAL_PROVIDER, i.LMI_CREATED, i.LMI_UPDATED), 256) AS SourceHash,
        link_x.TargetId AS ExistingTargetId,
        link_x.TargetEntity AS ExistingTargetEntity,
        link_x.SourceHash AS ExistingSourceHash,
        link_x.ImportRunId AS ExistingImportRunId
    FROM `SL-CORE`.`SL_LEINS_MEDICAL_INFORMATION_FACILITY` i
    INNER JOIN liens_LegacyIdCrosswalks lien_x
      ON lien_x.TenantId = v_tenant_id AND lien_x.SourceSystem = 'SL-CORE'
     AND lien_x.SourceTable = 'SL_LEINS_MEDICAL' AND lien_x.TargetEntity = 'Lien'
     AND lien_x.ImportRunId = v_core_run_id AND lien_x.LegacyId = CAST(i.LMI_LM_ID AS CHAR)
    INNER JOIN tmp_sl_core_facilities facility ON facility.LegacyFacilityId = i.LMI_FACILITY_ID
    LEFT JOIN liens_LegacyIdCrosswalks link_x
      ON link_x.TenantId = v_tenant_id AND link_x.SourceSystem = 'SL-CORE'
     AND link_x.SourceTable = 'SL_LEINS_MEDICAL_INFORMATION_FACILITY'
     AND link_x.LegacyId = CAST(i.LMI_ID AS CHAR)
    WHERE i.LMI_FACILITY_ID IS NOT NULL;

    SELECT COUNT(*) INTO v_source_link_count FROM tmp_sl_core_lien_facility_links;
    IF EXISTS (
        SELECT 1 FROM tmp_sl_core_lien_facility_links
        GROUP BY TargetLienId HAVING COUNT(DISTINCT TargetFacilityId) > 1
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-023 a migrated lien has multiple distinct legacy facilities';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sl_core_lien_facility_links s
        WHERE s.ExistingTargetId IS NOT NULL
          AND (s.ExistingTargetEntity <> 'LienFacilityLink' OR s.ExistingTargetId <> s.TargetLienId OR s.ExistingSourceHash <> s.SourceHash
               OR s.ExistingImportRunId <> v_contact_run_id)
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-024 existing lien-facility crosswalks conflict with source data';
    END IF;
    IF EXISTS (
        SELECT 1 FROM tmp_sl_core_lien_facility_links s
        INNER JOIN liens_Liens l ON l.Id = s.TargetLienId
        WHERE l.TenantId <> v_tenant_id OR l.OrgId <> v_org_id
           OR (l.FacilityId IS NOT NULL AND l.FacilityId <> s.TargetFacilityId)
           OR (s.ExistingTargetId IS NOT NULL AND l.FacilityId <> s.TargetFacilityId)
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-025 migrated lien ownership or FacilityId conflicts with the legacy link';
    END IF;

    SELECT COUNT(*) INTO v_contacts_to_insert FROM tmp_sl_core_contacts WHERE ExistingTargetId IS NULL;
    SELECT COUNT(*) INTO v_facility_contacts_to_insert
    FROM tmp_sl_core_facilities facility
    LEFT JOIN liens_Contacts contact
      ON contact.TenantId = v_tenant_id
     AND contact.OrgId = v_org_id
     AND contact.ContactType = 'MedicalFacility'
     AND (contact.ContactSubtype IS NULL OR contact.ContactSubtype = '')
     AND contact.FacilityId = facility.TargetFacilityId
     AND contact.IsActive = 1
    WHERE contact.Id IS NULL;
    SELECT COUNT(*) INTO v_source_law_firm_count
    FROM tmp_sl_core_contacts
    WHERE TargetContactType = 'LawFirm' AND TargetContactSubtype IS NULL;
    SELECT COUNT(*) INTO v_source_provider_count
    FROM tmp_sl_core_contacts
    WHERE TargetContactType = 'Provider';
    SELECT COUNT(*) INTO v_facilities_to_insert FROM tmp_sl_core_facilities WHERE ExistingTargetId IS NULL;
    SELECT COUNT(*) INTO v_people_to_insert FROM tmp_sl_core_facility_people WHERE ExistingTargetId IS NULL;
    SELECT COUNT(*) INTO v_links_to_apply
    FROM (
        SELECT DISTINCT s.TargetLienId, s.TargetFacilityId
        FROM tmp_sl_core_lien_facility_links s
        INNER JOIN liens_Liens l ON l.Id = s.TargetLienId
        WHERE l.FacilityId IS NULL
    ) pending_links;

    IF v_contact_run_count = 1
       AND (v_contacts_to_insert <> 0 OR v_facility_contacts_to_insert <> 0 OR v_facilities_to_insert <> 0 OR v_people_to_insert <> 0
            OR EXISTS (SELECT 1 FROM tmp_sl_core_lien_facility_links WHERE ExistingTargetId IS NULL)) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTC-026 completed contact migration is incomplete and requires reconciliation';
    END IF;

    IF NOT v_apply THEN
        SELECT
            'contact-facility-preflight-passed' AS Result,
            v_core_run_id AS CoreImportRunId,
            v_contact_run_id AS ContactImportRunId,
            v_source_contact_count AS SourceContacts,
            v_source_law_firm_count AS SourceLawFirms,
            v_source_provider_count AS SourceProviders,
            v_facility_contacts_to_insert AS FacilityContactsToInsert,
            v_contact_crosswalks_to_repair AS ContactCrosswalksToRepair,
            v_source_facility_count AS SourceFacilities,
            v_source_person_count AS SourceFacilityContactPeople,
            v_source_link_count AS SourceLienFacilityLinks,
            v_contacts_to_insert AS ContactsToInsert,
            v_facilities_to_insert AS FacilitiesToInsert,
            v_people_to_insert AS FacilityContactPeopleToInsert,
            v_links_to_apply AS LienFacilityLinksToApply,
            v_contact_run_count AS ExistingCompletedContactImportRuns;
    ELSE
        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
        START TRANSACTION;
        SET v_in_transaction = TRUE;

        IF v_contact_run_count = 0 THEN
            INSERT INTO liens_LegacyImportRuns (
                Id, TenantId, OrgId, SourceSystem, SourceFingerprint, LegacyProgram,
                MappingVersion, MappingManifestHash, MappingApprovalReference, Status,
                StartedAtUtc, CreatedByUserId, ApprovalId)
            VALUES (
                v_contact_run_id, v_tenant_id, v_org_id, 'SL-CORE', v_source_fingerprint, '1',
                v_mapping_version, v_mapping_manifest_hash,
                CONCAT('Completed core import ', v_core_run_id), 'Running', UTC_TIMESTAMP(6),
                v_migration_user_id, NULL);
            SET v_created_new_run = TRUE;

            INSERT INTO liens_Contacts (
                Id, TenantId, OrgId, ContactType, FirstName, LastName, DisplayName,
                Title, Organization, Email, Phone, Fax, Website, AddressLine1, City, State,
                PostalCode, Notes, IsActive, CreatedAtUtc, UpdatedAtUtc, CreatedByUserId,
                UpdatedByUserId, ContactSubtype, FacilityId, LawFirmId)
            SELECT TargetContactId, v_tenant_id, v_org_id, TargetContactType, FirstName, LastName,
                   DisplayName, NULL, Organization, Email, Phone, NULL, NULL, AddressLine1, City,
                   State, PostalCode, NULL, 1, CreatedAtUtc, UpdatedAtUtc, v_migration_user_id,
                   v_migration_user_id, TargetContactSubtype, NULL, TargetLawFirmId
            FROM tmp_sl_core_contacts
            WHERE ExistingTargetId IS NULL
              AND TargetContactSubtype IS NULL;
            SET v_contacts_inserted = ROW_COUNT();

            INSERT INTO liens_Contacts (
                Id, TenantId, OrgId, ContactType, FirstName, LastName, DisplayName,
                Title, Organization, Email, Phone, Fax, Website, AddressLine1, City, State,
                PostalCode, Notes, IsActive, CreatedAtUtc, UpdatedAtUtc, CreatedByUserId,
                UpdatedByUserId, ContactSubtype, FacilityId, LawFirmId)
            SELECT TargetContactId, v_tenant_id, v_org_id, TargetContactType, FirstName, LastName,
                   DisplayName, NULL, Organization, Email, Phone, NULL, NULL, AddressLine1, City,
                   State, PostalCode, NULL, 1, CreatedAtUtc, UpdatedAtUtc, v_migration_user_id,
                   v_migration_user_id, TargetContactSubtype, NULL, TargetLawFirmId
            FROM tmp_sl_core_contacts
            WHERE ExistingTargetId IS NULL
              AND TargetContactSubtype IS NOT NULL;
            SET v_contacts_inserted = v_contacts_inserted + ROW_COUNT();

            INSERT INTO liens_Facilities (
                Id, TenantId, OrgId, Name, Code, ExternalReference, AddressLine1, AddressLine2,
                City, State, PostalCode, Phone, Email, Fax, IsActive, OrganizationId,
                CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, UpdatedByUserId)
            SELECT TargetFacilityId, v_tenant_id, v_org_id, Name, NULL, ExternalReference, AddressLine1,
                   NULL, City, State, PostalCode, Phone, Email, NULL, 1, NULL, CreatedAtUtc,
                   UpdatedAtUtc, v_migration_user_id, v_migration_user_id
            FROM tmp_sl_core_facilities
            WHERE ExistingTargetId IS NULL;
            SET v_facilities_inserted = ROW_COUNT();

            INSERT INTO liens_Contacts (
                Id, TenantId, OrgId, ContactType, FirstName, LastName, DisplayName,
                Title, Organization, Email, Phone, Fax, Website, AddressLine1, City, State,
                PostalCode, Notes, IsActive, CreatedAtUtc, UpdatedAtUtc, CreatedByUserId,
                UpdatedByUserId, ContactSubtype, FacilityId, LawFirmId)
            SELECT UUID(), v_tenant_id, v_org_id, 'MedicalFacility',
                   LEFT(COALESCE(NULLIF(TRIM(SUBSTRING_INDEX(Name, ' ', 1)), ''), 'Legacy'), 100),
                   LEFT(COALESCE(NULLIF(TRIM(SUBSTRING(Name, CHAR_LENGTH(SUBSTRING_INDEX(Name, ' ', 1)) + 1)), ''), 'Facility'), 100),
                   LEFT(Name, 250),
                   NULL, LEFT(Name, 200), Email, Phone, NULL, NULL, AddressLine1, City, State,
                   PostalCode, CONCAT('legacySource=SL-CORE:SL_FACILITY:FacilityId=', TargetFacilityId),
                   1, CreatedAtUtc, UTC_TIMESTAMP(6), v_migration_user_id, v_migration_user_id,
                   NULL, TargetFacilityId, NULL
            FROM tmp_sl_core_facilities facility
            LEFT JOIN liens_Contacts contact
              ON contact.TenantId = v_tenant_id
             AND contact.OrgId = v_org_id
             AND contact.ContactType = 'MedicalFacility'
             AND (contact.ContactSubtype IS NULL OR contact.ContactSubtype = '')
             AND contact.FacilityId = facility.TargetFacilityId
             AND contact.IsActive = 1
            WHERE contact.Id IS NULL;
            SET v_facility_contacts_inserted = ROW_COUNT();

            INSERT INTO liens_FacilityContactPersons (
                Id, TenantId, FacilityId, FirstName, LastName, Position, Email, Phone, IsActive,
                CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, UpdatedByUserId)
            SELECT TargetFacilityPersonId, v_tenant_id, TargetFacilityId, FirstName, LastName, NULL,
                   Email, Phone, 1, CreatedAtUtc, UpdatedAtUtc, v_migration_user_id, v_migration_user_id
            FROM tmp_sl_core_facility_people
            WHERE ExistingTargetId IS NULL;
            SET v_people_inserted = ROW_COUNT();

            UPDATE liens_Liens l
            INNER JOIN (
                SELECT DISTINCT TargetLienId, TargetFacilityId
                FROM tmp_sl_core_lien_facility_links
            ) source_link ON source_link.TargetLienId = l.Id
            SET l.FacilityId = source_link.TargetFacilityId,
                l.UpdatedAtUtc = UTC_TIMESTAMP(6),
                l.UpdatedByUserId = v_migration_user_id
            WHERE l.TenantId = v_tenant_id AND l.OrgId = v_org_id AND l.FacilityId IS NULL;
            SET v_links_applied = ROW_COUNT();

            INSERT INTO liens_LegacyIdCrosswalks (
                Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity,
                TargetId, SourceHash, ImportRunId, CreatedAtUtc)
            SELECT UUID(), v_tenant_id, 'SL-CORE', 'SL_CONTACT', CAST(LegacyContactId AS CHAR),
                   'Contact', TargetContactId, SourceHash, v_contact_run_id, UTC_TIMESTAMP(6)
            FROM tmp_sl_core_contacts
            WHERE ExistingTargetId IS NULL
              AND ExistingCrosswalkId IS NULL;

            -- A legacy contact crosswalk with no usable UUID cannot refer to a
            -- target contact. Repair only that mapping, in the same transaction
            -- that creates the replacement contact; valid mappings are immutable.
            UPDATE liens_LegacyIdCrosswalks x
            INNER JOIN tmp_sl_core_contacts s
              ON s.ExistingCrosswalkId = x.Id
            SET x.TargetEntity = 'Contact',
                x.TargetId = s.TargetContactId,
                x.SourceHash = s.SourceHash,
                x.ImportRunId = v_contact_run_id,
                x.CreatedAtUtc = UTC_TIMESTAMP(6)
            WHERE s.ExistingTargetId IS NULL;

            INSERT INTO liens_LegacyIdCrosswalks (
                Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity,
                TargetId, SourceHash, ImportRunId, CreatedAtUtc)
            SELECT UUID(), v_tenant_id, 'SL-CORE', 'SL_FACILITY', CAST(LegacyFacilityId AS CHAR),
                   'Facility', TargetFacilityId, SourceHash, v_contact_run_id, UTC_TIMESTAMP(6)
            FROM tmp_sl_core_facilities
            WHERE ExistingTargetId IS NULL;

            INSERT INTO liens_LegacyIdCrosswalks (
                Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity,
                TargetId, SourceHash, ImportRunId, CreatedAtUtc)
            SELECT UUID(), v_tenant_id, 'SL-CORE', 'SL_FACILITY_CONTACT_PERSON',
                   CAST(LegacyFacilityPersonId AS CHAR), 'FacilityContactPerson', TargetFacilityPersonId,
                   SourceHash, v_contact_run_id, UTC_TIMESTAMP(6)
            FROM tmp_sl_core_facility_people
            WHERE ExistingTargetId IS NULL;

            INSERT INTO liens_LegacyIdCrosswalks (
                Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity,
                TargetId, SourceHash, ImportRunId, CreatedAtUtc)
            SELECT UUID(), v_tenant_id, 'SL-CORE', 'SL_LEINS_MEDICAL_INFORMATION_FACILITY',
                   CAST(LegacyLienFacilityLinkId AS CHAR), 'LienFacilityLink', TargetLienId,
                   SourceHash, v_contact_run_id, UTC_TIMESTAMP(6)
            FROM tmp_sl_core_lien_facility_links
            WHERE ExistingTargetId IS NULL;

            UPDATE liens_LegacyImportRuns
            SET Status = 'Completed', CompletedAtUtc = UTC_TIMESTAMP(6),
                SummaryJson = JSON_OBJECT(
                    'SourceContacts', v_source_contact_count,
                    'FacilityContactsInserted', v_facility_contacts_inserted,
                    'SourceFacilities', v_source_facility_count,
                    'SourceFacilityContactPeople', v_source_person_count,
                    'SourceLienFacilityLinks', v_source_link_count,
                    'ContactsInserted', v_contacts_inserted,
                    'FacilitiesInserted', v_facilities_inserted,
                    'FacilityContactPeopleInserted', v_people_inserted,
                    'LienFacilityLinksApplied', v_links_applied),
                ErrorSummary = NULL
            WHERE Id = v_contact_run_id AND Status = 'Running';
        END IF;

        COMMIT;
        SET v_in_transaction = FALSE;

        SELECT
            CASE WHEN v_created_new_run THEN 'contact-facility-import-applied'
                 ELSE 'contact-facility-import-already-complete' END AS Result,
            v_core_run_id AS CoreImportRunId,
            v_contact_run_id AS ContactImportRunId,
            v_contacts_inserted AS ContactsInserted,
            v_facility_contacts_inserted AS FacilityContactsInserted,
            v_facilities_inserted AS FacilitiesInserted,
            v_people_inserted AS FacilityContactPeopleInserted,
            v_links_applied AS LienFacilityLinksApplied;
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_lien_facility_links;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_facility_people;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_facilities;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_law_firm_parents;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_contacts;
    IF v_time_zone_changed THEN SET @@session.time_zone = v_original_time_zone; END IF;
    IF v_lock_acquired = 1 THEN DO RELEASE_LOCK(v_lock_name); END IF;
END$$

DELIMITER ;
