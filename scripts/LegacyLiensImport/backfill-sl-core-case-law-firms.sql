DROP PROCEDURE IF EXISTS liens_backfill_sl_core_case_law_firms;

DELIMITER $$

CREATE PROCEDURE liens_backfill_sl_core_case_law_firms(
    IN p_tenant_id CHAR(36),
    IN p_expected_updates INT,
    IN p_apply CHAR(1)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id CHAR(36);
    DECLARE v_apply BOOLEAN;
    DECLARE v_core_lock_name VARCHAR(64);
    DECLARE v_contact_lock_name VARCHAR(64);
    DECLARE v_core_lock_acquired INT DEFAULT 0;
    DECLARE v_contact_lock_acquired INT DEFAULT 0;
    DECLARE v_in_transaction BOOLEAN DEFAULT FALSE;
    DECLARE v_table_count INT DEFAULT 0;
    DECLARE v_column_count INT DEFAULT 0;
    DECLARE v_provenance_count INT DEFAULT 0;
    DECLARE v_core_run_count INT DEFAULT 0;
    DECLARE v_contact_run_count INT DEFAULT 0;
    DECLARE v_source_case_count INT DEFAULT 0;
    DECLARE v_cases_without_legacy_law_firm INT DEFAULT 0;
    DECLARE v_cases_already_correct INT DEFAULT 0;
    DECLARE v_cases_needing_update INT DEFAULT 0;
    DECLARE v_conflict_count INT DEFAULT 0;
    DECLARE v_cases_updated INT DEFAULT 0;
    DECLARE v_postcondition_errors INT DEFAULT 0;
    DECLARE v_core_run_id CHAR(36);
    DECLARE v_contact_run_id CHAR(36);
    DECLARE v_org_id CHAR(36);
    DECLARE v_contact_org_id CHAR(36);
    DECLARE v_migration_user_id CHAR(36);
    DECLARE v_source_fingerprint CHAR(64);
    DECLARE v_legacy_program VARCHAR(50);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        IF v_in_transaction THEN
            ROLLBACK;
        END IF;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_law_firm_repair;
        IF v_contact_lock_acquired = 1 THEN
            DO RELEASE_LOCK(v_contact_lock_name);
        END IF;
        IF v_core_lock_acquired = 1 THEN
            DO RELEASE_LOCK(v_core_lock_name);
        END IF;
        RESIGNAL;
    END;

    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    SET v_apply = p_apply = '1';
    SET v_core_lock_name = CONCAT('liens:slcore:', v_tenant_id);
    SET v_contact_lock_name = CONCAT('liens:slcore:contacts:', v_tenant_id);

    IF DATABASE() <> 'LS_LIENS' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-001 target schema must be LS_LIENS';
    END IF;
    IF v_tenant_id IS NULL
       OR v_tenant_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR p_apply IS NULL
       OR p_apply NOT IN ('0', '1')
       OR p_expected_updates IS NULL
       OR (NOT v_apply AND p_expected_updates <> -1)
       OR (v_apply AND p_expected_updates < 0) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-002 invalid tenant ID, expected update count, or apply flag';
    END IF;

    -- Lock in the same order as the import waves: core, then contacts.
    SELECT GET_LOCK(v_core_lock_name, 10) INTO v_core_lock_acquired;
    IF COALESCE(v_core_lock_acquired, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-003 SL-CORE core migration or repair is already active';
    END IF;
    SELECT GET_LOCK(v_contact_lock_name, 10) INTO v_contact_lock_acquired;
    IF COALESCE(v_contact_lock_acquired, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-004 SL-CORE contact migration or repair is already active';
    END IF;

    SELECT COUNT(*) INTO v_table_count
    FROM information_schema.tables
    WHERE (table_schema = DATABASE() AND table_type = 'BASE TABLE'
           AND table_name IN ('liens_Cases', 'liens_Contacts',
                              'liens_LegacyIdCrosswalks', 'liens_LegacyImportRuns'))
       OR (table_schema = 'SL-CORE' AND table_type = 'BASE TABLE'
           AND table_name IN ('SL_CASE', 'SL_CONTACT',
                              'SL_MIGRATION_SOURCE_PROVENANCE'));
    IF v_table_count <> 7 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-005 required source or target tables are unavailable';
    END IF;

    SELECT COUNT(*) INTO v_column_count
    FROM information_schema.columns
    WHERE (table_schema = DATABASE() AND
           ((table_name = 'liens_Cases' AND column_name IN ('Id', 'TenantId', 'OrgId', 'Notes', 'UpdatedAtUtc', 'UpdatedByUserId'))
         OR (table_name = 'liens_Contacts' AND column_name IN ('Id', 'TenantId', 'OrgId', 'ContactType', 'IsActive'))
         OR (table_name = 'liens_LegacyIdCrosswalks' AND column_name IN ('Id', 'TenantId', 'SourceSystem', 'SourceTable', 'LegacyId', 'TargetEntity', 'TargetId', 'ImportRunId'))
         OR (table_name = 'liens_LegacyImportRuns' AND column_name IN ('Id', 'TenantId', 'OrgId', 'SourceSystem', 'SourceFingerprint', 'LegacyProgram', 'MappingVersion', 'Status', 'CreatedByUserId'))))
       OR (table_schema = 'SL-CORE' AND
           ((table_name = 'SL_CASE' AND column_name IN ('CASE_ID', 'CASE_PROGRAM', 'CASE_LAW_FIRM', 'CASE_IS_DELETED'))
         OR (table_name = 'SL_CONTACT' AND column_name IN ('CONTACT_ID', 'CONTACT_PROGRAM', 'CONTACT_TYPE', 'CONTACT_STATUS'))
         OR (table_name = 'SL_MIGRATION_SOURCE_PROVENANCE' AND column_name IN ('PROVENANCE_KEY', 'SOURCE_FINGERPRINT', 'IMPORT_SCOPE'))));
    IF v_column_count <> 39 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-005 required source or target column contract is incomplete';
    END IF;

    SELECT COUNT(*) INTO v_core_run_count
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id
      AND r.SourceSystem = 'SL-CORE'
      AND r.LegacyProgram = '1'
      AND r.MappingVersion = 'sl-core-core-liens-v1'
      AND r.Status = 'Completed'
      AND EXISTS (
          SELECT 1
          FROM liens_LegacyIdCrosswalks x
          WHERE x.TenantId = r.TenantId
            AND x.ImportRunId = r.Id
            AND x.SourceSystem = 'SL-CORE'
            AND x.SourceTable = 'SL_CASE'
            AND x.TargetEntity = 'Case');
    IF v_core_run_count <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-006 exactly one completed Program 1 core import with case crosswalks is required';
    END IF;

    SELECT r.Id, r.OrgId, r.CreatedByUserId, LOWER(r.SourceFingerprint), r.LegacyProgram
      INTO v_core_run_id, v_org_id, v_migration_user_id, v_source_fingerprint, v_legacy_program
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id
      AND r.SourceSystem = 'SL-CORE'
      AND r.LegacyProgram = '1'
      AND r.MappingVersion = 'sl-core-core-liens-v1'
      AND r.Status = 'Completed'
      AND EXISTS (
          SELECT 1
          FROM liens_LegacyIdCrosswalks x
          WHERE x.TenantId = r.TenantId
            AND x.ImportRunId = r.Id
            AND x.SourceSystem = 'SL-CORE'
            AND x.SourceTable = 'SL_CASE'
            AND x.TargetEntity = 'Case');

    SELECT COUNT(*) INTO v_contact_run_count
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id
      AND r.SourceSystem = 'SL-CORE'
      AND r.LegacyProgram = '1'
      AND r.MappingVersion = 'sl-core-contact-facility-v1'
      AND r.SourceFingerprint = v_source_fingerprint
      AND r.Status = 'Completed'
      AND EXISTS (
          SELECT 1
          FROM liens_LegacyIdCrosswalks x
          WHERE x.TenantId = r.TenantId
            AND x.ImportRunId = r.Id
            AND x.SourceSystem = 'SL-CORE'
            AND x.SourceTable = 'SL_CONTACT'
            AND x.TargetEntity = 'Contact');
    IF v_contact_run_count <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-007 exactly one completed Program 1 contact import with contact crosswalks is required';
    END IF;

    SELECT r.Id, r.OrgId INTO v_contact_run_id, v_contact_org_id
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id
      AND r.SourceSystem = 'SL-CORE'
      AND r.LegacyProgram = '1'
      AND r.MappingVersion = 'sl-core-contact-facility-v1'
      AND r.SourceFingerprint = v_source_fingerprint
      AND r.Status = 'Completed'
      AND EXISTS (
          SELECT 1
          FROM liens_LegacyIdCrosswalks x
          WHERE x.TenantId = r.TenantId
            AND x.ImportRunId = r.Id
            AND x.SourceSystem = 'SL-CORE'
            AND x.SourceTable = 'SL_CONTACT'
            AND x.TargetEntity = 'Contact');
    IF v_contact_org_id IS NULL OR v_contact_org_id <> v_org_id THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-007 completed contact import ownership does not match the core import';
    END IF;

    SELECT COUNT(*) INTO v_provenance_count
    FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE`
    WHERE PROVENANCE_KEY = 'sl-core-current'
      AND LOWER(SOURCE_FINGERPRINT) = v_source_fingerprint
      AND IMPORT_SCOPE = 'sl-core-core-liens-v1';
    IF v_provenance_count <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-008 source provenance does not match the completed core import';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_law_firm_repair;

    CREATE TEMPORARY TABLE tmp_sl_core_case_law_firm_repair AS
    SELECT classified.*,
           CASE classified.Resolution
             WHEN 'NeedsUpdate' THEN
               CASE
                 WHEN classified.NotesBefore IS NULL OR TRIM(classified.NotesBefore) = ''
                   THEN CONCAT('[legacy-meta]', CHAR(10), 'lawFirmId=', classified.TargetLawFirmId)
                 WHEN classified.MetadataMarkerCount = 0
                   THEN CONCAT(classified.NotesBefore, CHAR(10), CHAR(10),
                               '[legacy-meta]', CHAR(10), 'lawFirmId=', classified.TargetLawFirmId)
                 ELSE CONCAT(classified.NotesBefore, '; lawFirmId=', classified.TargetLawFirmId)
               END
             ELSE classified.NotesBefore
           END AS NotesAfter
    FROM (
        SELECT staged.*,
               CASE
                 WHEN staged.CaseCrosswalkId IS NULL THEN 'MissingCaseCrosswalk'
                 WHEN staged.TargetCaseId IS NULL OR staged.TargetCaseOrgId IS NULL
                      OR staged.TargetCaseOrgId <> v_org_id THEN 'InvalidTargetCase'
                 WHEN staged.LegacyLawFirmId IS NULL THEN 'NoLegacyLawFirm'
                 WHEN staged.SourceLawFirmId IS NULL THEN 'MissingOrInactiveSourceLawFirm'
                 WHEN staged.ContactCrosswalkId IS NULL THEN 'MissingLawFirmContactCrosswalk'
                 WHEN staged.TargetLawFirmId IS NULL OR staged.TargetLawFirmOrgId IS NULL
                      OR staged.TargetLawFirmOrgId <> v_org_id
                      OR staged.TargetLawFirmType IS NULL OR staged.TargetLawFirmType <> 'LawFirm'
                      OR staged.TargetLawFirmIsActive IS NULL OR staged.TargetLawFirmIsActive <> 1 THEN 'InvalidTargetLawFirm'
                 WHEN staged.MetadataMarkerCount > 1 THEN 'AmbiguousLegacyMetadata'
                 WHEN staged.MetadataMarkerCount = 0
                      AND LOCATE('lawFirmId=', COALESCE(staged.NotesBefore, '')) > 0 THEN 'UnmarkedLawFirmMetadata'
                 WHEN staged.MetadataMarkerCount = 1
                      AND LOCATE('lawFirmId=', staged.MetadataAfterMarker) > 0
                      AND staged.ExistingLawFirmId IS NULL THEN 'MalformedExistingLawFirmId'
                 WHEN staged.ExistingLawFirmId IS NOT NULL
                      AND LOWER(staged.ExistingLawFirmId) <> LOWER(staged.TargetLawFirmId) THEN 'ConflictingExistingLawFirmId'
                 WHEN staged.ExistingLawFirmId IS NOT NULL THEN 'AlreadyCorrect'
                 WHEN CHAR_LENGTH(
                     CASE
                       WHEN staged.NotesBefore IS NULL OR TRIM(staged.NotesBefore) = ''
                         THEN CONCAT('[legacy-meta]', CHAR(10), 'lawFirmId=', staged.TargetLawFirmId)
                       WHEN staged.MetadataMarkerCount = 0
                         THEN CONCAT(staged.NotesBefore, CHAR(10), CHAR(10),
                                     '[legacy-meta]', CHAR(10), 'lawFirmId=', staged.TargetLawFirmId)
                       ELSE CONCAT(staged.NotesBefore, '; lawFirmId=', staged.TargetLawFirmId)
                     END) > 4000 THEN 'NotesOverflow'
                 ELSE 'NeedsUpdate'
               END AS Resolution
        FROM (
            SELECT
                source_case.CASE_ID AS LegacyCaseId,
                source_case.CASE_LAW_FIRM AS LegacyLawFirmId,
                source_firm.CONTACT_ID AS SourceLawFirmId,
                case_x.Id AS CaseCrosswalkId,
                case_x.TargetId AS TargetCaseId,
                target_case.OrgId AS TargetCaseOrgId,
                target_case.Notes AS NotesBefore,
                contact_x.Id AS ContactCrosswalkId,
                contact_x.TargetId AS TargetLawFirmId,
                target_law_firm.OrgId AS TargetLawFirmOrgId,
                target_law_firm.ContactType AS TargetLawFirmType,
                target_law_firm.IsActive AS TargetLawFirmIsActive,
                (CHAR_LENGTH(COALESCE(target_case.Notes, ''))
                    - CHAR_LENGTH(REPLACE(COALESCE(target_case.Notes, ''), '[legacy-meta]', '')))
                    / CHAR_LENGTH('[legacy-meta]') AS MetadataMarkerCount,
                CASE
                  WHEN LOCATE('[legacy-meta]', COALESCE(target_case.Notes, '')) > 0
                    THEN SUBSTRING(
                        target_case.Notes,
                        LOCATE('[legacy-meta]', target_case.Notes) + CHAR_LENGTH('[legacy-meta]'))
                  ELSE COALESCE(target_case.Notes, '')
                END AS MetadataAfterMarker,
                NULLIF(SUBSTRING(
                    REGEXP_SUBSTR(
                        CASE
                          WHEN LOCATE('[legacy-meta]', COALESCE(target_case.Notes, '')) > 0
                            THEN SUBSTRING(
                                target_case.Notes,
                                LOCATE('[legacy-meta]', target_case.Notes) + CHAR_LENGTH('[legacy-meta]'))
                          ELSE COALESCE(target_case.Notes, '')
                        END,
                        'lawFirmId=[0-9A-Fa-f-]{36}'),
                    CHAR_LENGTH('lawFirmId=') + 1), '') AS ExistingLawFirmId
            FROM `SL-CORE`.`SL_CASE` source_case
            LEFT JOIN `SL-CORE`.`SL_CONTACT` source_firm
              ON source_firm.CONTACT_ID = source_case.CASE_LAW_FIRM
             AND source_firm.CONTACT_PROGRAM = 1
             AND source_firm.CONTACT_TYPE = 1
             AND COALESCE(source_firm.CONTACT_STATUS, 'A') = 'A'
            LEFT JOIN liens_LegacyIdCrosswalks case_x
              ON case_x.TenantId = v_tenant_id
             AND case_x.SourceSystem = 'SL-CORE'
             AND case_x.SourceTable = 'SL_CASE'
             AND case_x.LegacyId = CAST(source_case.CASE_ID AS CHAR)
             AND case_x.TargetEntity = 'Case'
             AND case_x.ImportRunId = v_core_run_id
            LEFT JOIN liens_Cases target_case
              ON target_case.Id = case_x.TargetId
             AND target_case.TenantId = v_tenant_id
            LEFT JOIN liens_LegacyIdCrosswalks contact_x
              ON contact_x.TenantId = v_tenant_id
             AND contact_x.SourceSystem = 'SL-CORE'
             AND contact_x.SourceTable = 'SL_CONTACT'
             AND contact_x.LegacyId = CAST(source_case.CASE_LAW_FIRM AS CHAR)
             AND contact_x.TargetEntity = 'Contact'
             AND contact_x.ImportRunId = v_contact_run_id
            LEFT JOIN liens_Contacts target_law_firm
              ON target_law_firm.Id = contact_x.TargetId
             AND target_law_firm.TenantId = v_tenant_id
            WHERE source_case.CASE_PROGRAM = CAST(v_legacy_program AS UNSIGNED)
              AND COALESCE(source_case.CASE_IS_DELETED, 'N') <> 'Y'
        ) staged
    ) classified;

    SELECT COUNT(*) INTO v_source_case_count
    FROM tmp_sl_core_case_law_firm_repair;
    IF v_source_case_count = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-009 no eligible Program 1 source cases were found';
    END IF;

    SELECT COUNT(*) INTO v_cases_without_legacy_law_firm
    FROM tmp_sl_core_case_law_firm_repair
    WHERE Resolution = 'NoLegacyLawFirm';
    SELECT COUNT(*) INTO v_cases_already_correct
    FROM tmp_sl_core_case_law_firm_repair
    WHERE Resolution = 'AlreadyCorrect';
    SELECT COUNT(*) INTO v_cases_needing_update
    FROM tmp_sl_core_case_law_firm_repair
    WHERE Resolution = 'NeedsUpdate';
    SELECT COUNT(*) INTO v_conflict_count
    FROM tmp_sl_core_case_law_firm_repair
    WHERE Resolution NOT IN ('NoLegacyLawFirm', 'AlreadyCorrect', 'NeedsUpdate');

    IF v_conflict_count <> 0 THEN
        SELECT Resolution, COUNT(*) AS Cases
        FROM tmp_sl_core_case_law_firm_repair
        WHERE Resolution NOT IN ('NoLegacyLawFirm', 'AlreadyCorrect', 'NeedsUpdate')
        GROUP BY Resolution
        ORDER BY Resolution;
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-010 source-to-target law-firm mapping conflicts require reconciliation';
    END IF;

    IF NOT v_apply THEN
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_law_firm_repair;
        DO RELEASE_LOCK(v_contact_lock_name);
        SET v_contact_lock_acquired = 0;
        DO RELEASE_LOCK(v_core_lock_name);
        SET v_core_lock_acquired = 0;
        SELECT 'law-firm-backfill-preflight-passed' AS Result,
               v_core_run_id AS CoreImportRunId,
               v_contact_run_id AS ContactImportRunId,
               v_source_case_count AS SourceCases,
               v_cases_without_legacy_law_firm AS CasesWithoutLegacyLawFirm,
               v_cases_already_correct AS CasesAlreadyCorrect,
               v_cases_needing_update AS CasesNeedingUpdate,
               v_conflict_count AS Conflicts;
    ELSE
        IF p_expected_updates <> v_cases_needing_update THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-011 expected update count does not match the validated repair plan';
        END IF;

        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
        START TRANSACTION;
        SET v_in_transaction = TRUE;

        UPDATE liens_Cases target_case
        INNER JOIN tmp_sl_core_case_law_firm_repair repair
          ON repair.TargetCaseId = target_case.Id
        SET target_case.Notes = repair.NotesAfter,
            target_case.UpdatedAtUtc = UTC_TIMESTAMP(6),
            target_case.UpdatedByUserId = v_migration_user_id
        WHERE repair.Resolution = 'NeedsUpdate'
          AND target_case.TenantId = v_tenant_id
          AND target_case.OrgId = v_org_id
          AND target_case.Notes <=> repair.NotesBefore;
        SET v_cases_updated = ROW_COUNT();

        IF v_cases_updated <> v_cases_needing_update THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-012 update count did not match the validated repair plan';
        END IF;

        SELECT COUNT(*) INTO v_postcondition_errors
        FROM tmp_sl_core_case_law_firm_repair repair
        INNER JOIN liens_Cases target_case
          ON target_case.Id = repair.TargetCaseId
         AND target_case.TenantId = v_tenant_id
         AND target_case.OrgId = v_org_id
        WHERE repair.Resolution IN ('NeedsUpdate', 'AlreadyCorrect')
          AND COALESCE(LOCATE(CONCAT('lawFirmId=', repair.TargetLawFirmId), target_case.Notes), 0) = 0;
        IF v_postcondition_errors <> 0 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTF-013 postcondition failed: target law-firm metadata is incomplete';
        END IF;

        COMMIT;
        SET v_in_transaction = FALSE;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_law_firm_repair;
        DO RELEASE_LOCK(v_contact_lock_name);
        SET v_contact_lock_acquired = 0;
        DO RELEASE_LOCK(v_core_lock_name);
        SET v_core_lock_acquired = 0;
        SELECT 'law-firm-backfill-applied' AS Result,
               v_core_run_id AS CoreImportRunId,
               v_contact_run_id AS ContactImportRunId,
               v_source_case_count AS SourceCases,
               v_cases_without_legacy_law_firm AS CasesWithoutLegacyLawFirm,
               v_cases_already_correct AS CasesAlreadyCorrect,
               v_cases_updated AS CasesUpdated,
               v_conflict_count AS Conflicts;
    END IF;
END$$

DELIMITER ;
