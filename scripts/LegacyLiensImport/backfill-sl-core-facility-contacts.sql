-- Creates the missing Contacts compatibility projection for existing SL-CORE
-- facilities. The Contacts page lists liens_Contacts; imported facilities live
-- in liens_Facilities and are linked here through Contact.FacilityId.

DROP PROCEDURE IF EXISTS liens_backfill_sl_core_facility_contacts;

DELIMITER $$

CREATE PROCEDURE liens_backfill_sl_core_facility_contacts(
    IN p_tenant_id CHAR(36),
    IN p_expected_inserts INT,
    IN p_apply CHAR(1)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id CHAR(36);
    DECLARE v_apply BOOLEAN;
    DECLARE v_lock_name VARCHAR(64);
    DECLARE v_lock_acquired INT DEFAULT 0;
    DECLARE v_in_transaction BOOLEAN DEFAULT FALSE;
    DECLARE v_contact_run_count INT DEFAULT 0;
    DECLARE v_facility_count INT DEFAULT 0;
    DECLARE v_table_count INT DEFAULT 0;
    DECLARE v_inserts_needed INT DEFAULT 0;
    DECLARE v_conflicts INT DEFAULT 0;
    DECLARE v_inserted INT DEFAULT 0;
    DECLARE v_postcondition_errors INT DEFAULT 0;
    DECLARE v_contact_run_id CHAR(36);
    DECLARE v_org_id CHAR(36);
    DECLARE v_user_id CHAR(36);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        IF v_in_transaction THEN ROLLBACK; END IF;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_facility_contact_backfill;
        IF v_lock_acquired = 1 THEN DO RELEASE_LOCK(v_lock_name); END IF;
        RESIGNAL;
    END;

    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    SET v_apply = p_apply = '1';
    SET v_lock_name = CONCAT('liens:slcore:contacts:', v_tenant_id);

    IF DATABASE() NOT IN ('LS_LIENS', 'LS_QA_LIENS') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-001 target schema must be LS_LIENS or LS_QA_LIENS';
    END IF;
    IF v_tenant_id IS NULL
       OR v_tenant_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR p_apply IS NULL OR p_apply NOT IN ('0', '1') OR p_expected_inserts IS NULL
       OR (NOT v_apply AND p_expected_inserts <> -1) OR (v_apply AND p_expected_inserts < 0) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-002 invalid tenant ID, expected insert count, or apply flag';
    END IF;

    SELECT GET_LOCK(v_lock_name, 10) INTO v_lock_acquired;
    IF COALESCE(v_lock_acquired, 0) <> 1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-003 contact import or repair is already active'; END IF;

    SELECT COUNT(*) INTO v_table_count FROM information_schema.tables
    WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE'
      AND table_name IN ('liens_Facilities', 'liens_Contacts', 'liens_LegacyIdCrosswalks', 'liens_LegacyImportRuns');
    IF v_table_count <> 4 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-003 required target tables are unavailable'; END IF;

    SELECT COUNT(*) INTO v_contact_run_count
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id AND r.SourceSystem = 'SL-CORE' AND r.LegacyProgram = '1'
      AND r.MappingVersion IN ('sl-core-contact-facility-v1', 'sl-core-contact-facility-v2') AND r.Status = 'Completed'
      AND EXISTS (SELECT 1 FROM liens_LegacyIdCrosswalks x WHERE x.TenantId = r.TenantId
                  AND x.ImportRunId = r.Id AND x.SourceSystem = 'SL-CORE'
                  AND x.SourceTable = 'SL_FACILITY' AND x.TargetEntity = 'Facility');
    IF v_contact_run_count <> 1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-004 exactly one completed Program 1 contact/facility import is required'; END IF;

    SELECT r.Id, r.OrgId, r.CreatedByUserId INTO v_contact_run_id, v_org_id, v_user_id
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id AND r.SourceSystem = 'SL-CORE' AND r.LegacyProgram = '1'
      AND r.MappingVersion IN ('sl-core-contact-facility-v1', 'sl-core-contact-facility-v2') AND r.Status = 'Completed'
      AND EXISTS (SELECT 1 FROM liens_LegacyIdCrosswalks x WHERE x.TenantId = r.TenantId
                  AND x.ImportRunId = r.Id AND x.SourceSystem = 'SL-CORE'
                  AND x.SourceTable = 'SL_FACILITY' AND x.TargetEntity = 'Facility');

    SELECT COUNT(*) INTO v_facility_count
    FROM liens_Facilities facility
    INNER JOIN liens_LegacyIdCrosswalks facility_x
      ON facility_x.TenantId = v_tenant_id
     AND facility_x.ImportRunId = v_contact_run_id
     AND facility_x.SourceSystem = 'SL-CORE'
     AND facility_x.SourceTable = 'SL_FACILITY'
     AND facility_x.TargetEntity = 'Facility'
     AND facility_x.TargetId = facility.Id
    WHERE facility.TenantId = v_tenant_id AND facility.OrgId = v_org_id AND facility.IsActive = 1;
    IF v_facility_count = 0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-005 no active imported facilities were found for the completed import owner'; END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_facility_contact_backfill;
    CREATE TEMPORARY TABLE tmp_sl_core_facility_contact_backfill AS
    SELECT facility.Id AS FacilityId, facility.Name, facility.AddressLine1, facility.City, facility.State,
           facility.PostalCode, facility.Phone, facility.Email, facility.CreatedAtUtc,
           COALESCE(medical_contact.ContactCount, 0) AS MedicalFacilityContactCount,
           COALESCE(main_contact.ContactCount, 0) AS LinkedMainContactCount,
           CASE
             WHEN COALESCE(medical_contact.ContactCount, 0) > 1 THEN 'DuplicateMedicalFacilityContacts'
             WHEN COALESCE(medical_contact.ContactCount, 0) = 1 THEN 'AlreadyLinked'
             WHEN COALESCE(main_contact.ContactCount, 0) > 0 THEN 'ConflictingLinkedMainContact'
             WHEN facility.Name IS NULL OR TRIM(facility.Name) = '' THEN 'InvalidFacilityName'
             ELSE 'NeedsInsert'
           END AS Resolution
    FROM liens_Facilities facility
    INNER JOIN liens_LegacyIdCrosswalks facility_x
      ON facility_x.TenantId = v_tenant_id
     AND facility_x.ImportRunId = v_contact_run_id
     AND facility_x.SourceSystem = 'SL-CORE'
     AND facility_x.SourceTable = 'SL_FACILITY'
     AND facility_x.TargetEntity = 'Facility'
     AND facility_x.TargetId = facility.Id
    LEFT JOIN (
        SELECT FacilityId, COUNT(*) AS ContactCount
        FROM liens_Contacts
        WHERE TenantId = v_tenant_id AND OrgId = v_org_id AND IsActive = 1
          AND ContactType = 'MedicalFacility' AND (ContactSubtype IS NULL OR ContactSubtype = '')
        GROUP BY FacilityId
    ) medical_contact ON medical_contact.FacilityId = facility.Id
    LEFT JOIN (
        SELECT FacilityId, COUNT(*) AS ContactCount
        FROM liens_Contacts
        WHERE TenantId = v_tenant_id AND OrgId = v_org_id AND IsActive = 1
          AND (ContactSubtype IS NULL OR ContactSubtype = '')
        GROUP BY FacilityId
    ) main_contact ON main_contact.FacilityId = facility.Id
    WHERE facility.TenantId = v_tenant_id AND facility.OrgId = v_org_id AND facility.IsActive = 1;

    SELECT COUNT(*) INTO v_inserts_needed FROM tmp_sl_core_facility_contact_backfill WHERE Resolution = 'NeedsInsert';
    SELECT COUNT(*) INTO v_conflicts FROM tmp_sl_core_facility_contact_backfill WHERE Resolution NOT IN ('NeedsInsert', 'AlreadyLinked');

    IF v_conflicts <> 0 AND NOT v_apply THEN
        SELECT Resolution, COUNT(*) AS Facilities FROM tmp_sl_core_facility_contact_backfill
        WHERE Resolution NOT IN ('NeedsInsert', 'AlreadyLinked') GROUP BY Resolution ORDER BY Resolution;
    END IF;

    IF NOT v_apply THEN
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_facility_contact_backfill;
        DO RELEASE_LOCK(v_lock_name); SET v_lock_acquired = 0;
        SELECT 'facility-contact-backfill-preflight-completed' AS Result,
               v_contact_run_id AS ContactFacilityImportRunId, v_facility_count AS ActiveFacilities,
               v_inserts_needed AS ContactsToInsert, v_conflicts AS Conflicts;
    ELSE
        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
        START TRANSACTION; SET v_in_transaction = TRUE;
        -- Rebuild the plan under SERIALIZABLE isolation. This holds next-key
        -- locks on the contact index through COMMIT, so an API/manual writer
        -- cannot add a competing main contact between validation and insert.
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_facility_contact_backfill;
        CREATE TEMPORARY TABLE tmp_sl_core_facility_contact_backfill AS
        SELECT facility.Id AS FacilityId, facility.Name, facility.AddressLine1, facility.City, facility.State,
               facility.PostalCode, facility.Phone, facility.Email, facility.CreatedAtUtc,
               COALESCE(medical_contact.ContactCount, 0) AS MedicalFacilityContactCount,
               COALESCE(main_contact.ContactCount, 0) AS LinkedMainContactCount,
               CASE
                 WHEN COALESCE(medical_contact.ContactCount, 0) > 1 THEN 'DuplicateMedicalFacilityContacts'
                 WHEN COALESCE(medical_contact.ContactCount, 0) = 1 THEN 'AlreadyLinked'
                 WHEN COALESCE(main_contact.ContactCount, 0) > 0 THEN 'ConflictingLinkedMainContact'
                 WHEN facility.Name IS NULL OR TRIM(facility.Name) = '' THEN 'InvalidFacilityName'
                 ELSE 'NeedsInsert'
               END AS Resolution
        FROM liens_Facilities facility
        INNER JOIN liens_LegacyIdCrosswalks facility_x
          ON facility_x.TenantId = v_tenant_id
         AND facility_x.ImportRunId = v_contact_run_id
         AND facility_x.SourceSystem = 'SL-CORE'
         AND facility_x.SourceTable = 'SL_FACILITY'
         AND facility_x.TargetEntity = 'Facility'
         AND facility_x.TargetId = facility.Id
        LEFT JOIN (
            SELECT FacilityId, COUNT(*) AS ContactCount
            FROM liens_Contacts
            WHERE TenantId = v_tenant_id AND OrgId = v_org_id AND IsActive = 1
              AND ContactType = 'MedicalFacility' AND (ContactSubtype IS NULL OR ContactSubtype = '')
            GROUP BY FacilityId
        ) medical_contact ON medical_contact.FacilityId = facility.Id
        LEFT JOIN (
            SELECT FacilityId, COUNT(*) AS ContactCount
            FROM liens_Contacts
            WHERE TenantId = v_tenant_id AND OrgId = v_org_id AND IsActive = 1
              AND (ContactSubtype IS NULL OR ContactSubtype = '')
            GROUP BY FacilityId
        ) main_contact ON main_contact.FacilityId = facility.Id
        WHERE facility.TenantId = v_tenant_id AND facility.OrgId = v_org_id AND facility.IsActive = 1;

        SELECT COUNT(*) INTO v_inserts_needed FROM tmp_sl_core_facility_contact_backfill WHERE Resolution = 'NeedsInsert';
        SELECT COUNT(*) INTO v_conflicts FROM tmp_sl_core_facility_contact_backfill WHERE Resolution NOT IN ('NeedsInsert', 'AlreadyLinked');
        IF v_conflicts <> 0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-006 facility-contact conflicts require reconciliation'; END IF;
        IF p_expected_inserts <> v_inserts_needed THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-007 expected insert count does not match the validated repair plan'; END IF;
        INSERT INTO liens_Contacts (Id,TenantId,OrgId,ContactType,FirstName,LastName,DisplayName,Title,Organization,Email,Phone,Fax,Website,AddressLine1,City,State,PostalCode,Notes,IsActive,CreatedAtUtc,UpdatedAtUtc,CreatedByUserId,UpdatedByUserId,ContactSubtype,FacilityId,LawFirmId)
        SELECT UUID(),v_tenant_id,v_org_id,'MedicalFacility',
               LEFT(COALESCE(NULLIF(TRIM(SUBSTRING_INDEX(Name, ' ', 1)), ''), 'Legacy'),100),
               LEFT(COALESCE(NULLIF(TRIM(SUBSTRING(Name, CHAR_LENGTH(SUBSTRING_INDEX(Name, ' ', 1)) + 1)), ''), 'Facility'),100),
               LEFT(Name,250),NULL,LEFT(Name,200),Email,Phone,NULL,NULL,AddressLine1,City,State,PostalCode,
               CONCAT('legacySource=SL-CORE:SL_FACILITY:FacilityId=', FacilityId),1,CreatedAtUtc,UTC_TIMESTAMP(6),v_user_id,v_user_id,NULL,FacilityId,NULL
        FROM tmp_sl_core_facility_contact_backfill WHERE Resolution = 'NeedsInsert';
        SET v_inserted = ROW_COUNT();
        IF v_inserted <> v_inserts_needed THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-008 inserted row count did not match the validated repair plan'; END IF;
        SELECT COUNT(*) INTO v_postcondition_errors
        FROM (
            SELECT repair.FacilityId
            FROM tmp_sl_core_facility_contact_backfill repair
            LEFT JOIN liens_Contacts contact ON contact.TenantId = v_tenant_id AND contact.OrgId = v_org_id
                AND contact.ContactType = 'MedicalFacility' AND (contact.ContactSubtype IS NULL OR contact.ContactSubtype = '')
                AND contact.FacilityId = repair.FacilityId AND contact.IsActive = 1
            WHERE repair.Resolution = 'NeedsInsert'
            GROUP BY repair.FacilityId
            HAVING COUNT(contact.Id) <> 1
        ) postcondition_failures;
        IF v_postcondition_errors <> 0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-009 postcondition failed: facility contact is missing or duplicated'; END IF;
        COMMIT; SET v_in_transaction = FALSE;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_facility_contact_backfill;
        DO RELEASE_LOCK(v_lock_name); SET v_lock_acquired = 0;
        SELECT 'facility-contact-backfill-applied' AS Result, v_contact_run_id AS ContactFacilityImportRunId,
               v_facility_count AS ActiveFacilities, v_inserted AS ContactsInserted;
    END IF;
END$$

DELIMITER ;
