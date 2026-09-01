-- Restore the state of incident for cases in an already-completed SL-CORE import.
--
-- Legacy source: SL_CASE.CASE_ACCIDENT_STATE
-- V2 target:     liens_Cases.IncidentState
--
-- Usage:
--   CALL liens_backfill_sl_core_incident_state('<tenant-guid>', -1, '0');
--   CALL liens_backfill_sl_core_incident_state('<tenant-guid>', <ChangesToApply>, '1');

-- SL-CORE uses utf8mb4_0900_ai_ci while historical Liens schemas may use
-- utf8mb4_general_ci. Cross-schema text matches below compare HEX byte strings.
SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;

DROP PROCEDURE IF EXISTS liens_backfill_sl_core_incident_state;

DELIMITER $$

CREATE PROCEDURE liens_backfill_sl_core_incident_state(
    IN p_tenant_id CHAR(36),
    IN p_expected_changes INT,
    IN p_apply CHAR(1)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id CHAR(36);
    DECLARE v_apply BOOLEAN;
    DECLARE v_lock_name VARCHAR(64);
    DECLARE v_locked INT DEFAULT 0;
    DECLARE v_in_transaction BOOLEAN DEFAULT FALSE;
    DECLARE v_core_run_id CHAR(36);
    DECLARE v_org_id CHAR(36);
    DECLARE v_migration_user_id CHAR(36);
    DECLARE v_legacy_program VARCHAR(50);
    DECLARE v_source_fingerprint CHAR(64);
    DECLARE v_table_count INT DEFAULT 0;
    DECLARE v_column_count INT DEFAULT 0;
    DECLARE v_core_run_count INT DEFAULT 0;
    DECLARE v_provenance_count INT DEFAULT 0;
    DECLARE v_changes_to_apply INT DEFAULT 0;
    DECLARE v_conflicts INT DEFAULT 0;
    DECLARE v_rows_updated INT DEFAULT 0;
    DECLARE v_postcondition_errors INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        IF v_in_transaction THEN ROLLBACK; END IF;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_incident_state;
        IF v_locked = 1 THEN DO RELEASE_LOCK(v_lock_name); END IF;
        RESIGNAL;
    END;

    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    SET v_apply = p_apply = '1';
    SET v_lock_name = CONCAT('liens:slcore:', v_tenant_id);

    IF DATABASE() NOT IN ('LS_QA_LIENS', 'LS_LIENS') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTIS-001 target schema must be LS_QA_LIENS or LS_LIENS';
    END IF;
    IF v_tenant_id IS NULL
       OR v_tenant_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR p_apply IS NULL OR p_apply NOT IN ('0', '1') OR p_expected_changes IS NULL
       OR (NOT v_apply AND p_expected_changes <> -1) OR (v_apply AND p_expected_changes < 0) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTIS-002 invalid tenant ID, expected change count, or apply flag';
    END IF;

    SELECT GET_LOCK(v_lock_name, 10) INTO v_locked;
    IF COALESCE(v_locked, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTIS-003 SL-CORE import or repair is already active';
    END IF;

    SELECT COUNT(*) INTO v_table_count
    FROM information_schema.tables
    WHERE (table_schema = DATABASE() AND table_type = 'BASE TABLE'
           AND table_name IN ('liens_Cases', 'liens_LegacyIdCrosswalks', 'liens_LegacyImportRuns'))
       OR (table_schema = 'SL-CORE' AND table_type = 'BASE TABLE'
           AND table_name IN ('SL_CASE', 'SL_MIGRATION_SOURCE_PROVENANCE'));
    IF v_table_count <> 5 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTIS-004 required source or target tables are unavailable';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE() AND table_name = 'liens_Cases'
          AND column_name = 'IncidentState'
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTIS-005 apply 20260825160000_AddLegacyReportParityFields before this backfill';
    END IF;

    SELECT COUNT(*) INTO v_column_count
    FROM information_schema.columns
    WHERE (table_schema = DATABASE() AND
           ((table_name = 'liens_Cases' AND column_name IN ('Id', 'TenantId', 'OrgId', 'IncidentState', 'UpdatedAtUtc', 'UpdatedByUserId'))
         OR (table_name = 'liens_LegacyIdCrosswalks' AND column_name IN ('TenantId', 'SourceSystem', 'SourceTable', 'LegacyId', 'TargetEntity', 'TargetId', 'ImportRunId'))
         OR (table_name = 'liens_LegacyImportRuns' AND column_name IN ('Id', 'TenantId', 'OrgId', 'SourceSystem', 'SourceFingerprint', 'LegacyProgram', 'MappingVersion', 'Status', 'CreatedByUserId'))))
       OR (table_schema = 'SL-CORE' AND
           ((table_name = 'SL_CASE' AND column_name IN ('CASE_ID', 'CASE_ACCIDENT_STATE', 'CASE_PROGRAM', 'CASE_IS_DELETED'))
         OR (table_name = 'SL_MIGRATION_SOURCE_PROVENANCE' AND column_name IN ('PROVENANCE_KEY', 'SOURCE_FINGERPRINT', 'IMPORT_SCOPE'))));
    IF v_column_count <> 29 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTIS-005 required source or target column contract is incomplete';
    END IF;

    SELECT COUNT(*) INTO v_core_run_count
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id AND r.SourceSystem = 'SL-CORE'
      AND r.MappingVersion = 'sl-core-core-liens-v1' AND r.Status = 'Completed'
      AND EXISTS (SELECT 1 FROM liens_LegacyIdCrosswalks x WHERE x.ImportRunId = r.Id
                  AND x.TenantId = r.TenantId AND x.SourceSystem = 'SL-CORE'
                  AND x.SourceTable = 'SL_CASE' AND x.TargetEntity = 'Case');
    IF v_core_run_count <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTIS-006 exactly one completed SL-CORE core import is required';
    END IF;

    SELECT r.Id, r.OrgId, r.CreatedByUserId, r.LegacyProgram, LOWER(r.SourceFingerprint)
      INTO v_core_run_id, v_org_id, v_migration_user_id, v_legacy_program, v_source_fingerprint
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id AND r.SourceSystem = 'SL-CORE'
      AND r.MappingVersion = 'sl-core-core-liens-v1' AND r.Status = 'Completed';

    SELECT COUNT(*) INTO v_provenance_count
    FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE`
    WHERE PROVENANCE_KEY = 'sl-core-current'
      AND HEX(LOWER(SOURCE_FINGERPRINT)) = HEX(v_source_fingerprint)
      AND HEX(IMPORT_SCOPE) = HEX('sl-core-core-liens-v1');
    IF v_provenance_count <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTIS-007 source provenance does not match the completed core import';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_incident_state;
    CREATE TEMPORARY TABLE tmp_sl_core_incident_state AS
    SELECT source.CASE_ID AS LegacyCaseId, target.Id AS TargetCaseId,
           LEFT(NULLIF(TRIM(source.CASE_ACCIDENT_STATE), ''), 100) AS SourceIncidentState,
           target.IncidentState AS ExistingIncidentState,
           CASE
             WHEN NULLIF(TRIM(source.CASE_ACCIDENT_STATE), '') IS NULL THEN 'NoSourceIncidentState'
             WHEN target.Id IS NULL OR target.TenantId <> v_tenant_id OR target.OrgId <> v_org_id THEN 'InvalidTarget'
             WHEN target.IncidentState IS NULL OR TRIM(target.IncidentState) = '' THEN 'NeedsUpdate'
             WHEN HEX(target.IncidentState) = HEX(LEFT(NULLIF(TRIM(source.CASE_ACCIDENT_STATE), ''), 100)) THEN 'AlreadyCorrect'
             ELSE 'Conflict'
           END AS Resolution
    FROM `SL-CORE`.`SL_CASE` source
    INNER JOIN liens_LegacyIdCrosswalks x ON x.TenantId = v_tenant_id AND x.ImportRunId = v_core_run_id
        AND x.SourceSystem = 'SL-CORE' AND x.SourceTable = 'SL_CASE' AND x.TargetEntity = 'Case'
        AND HEX(x.LegacyId) = HEX(CAST(source.CASE_ID AS CHAR))
    LEFT JOIN liens_Cases target ON target.Id = x.TargetId
    WHERE HEX(CAST(source.CASE_PROGRAM AS CHAR)) = HEX(v_legacy_program)
      AND UPPER(TRIM(COALESCE(source.CASE_IS_DELETED, 'N'))) <> 'Y';

    SELECT COUNT(*) INTO v_changes_to_apply FROM tmp_sl_core_incident_state WHERE Resolution = 'NeedsUpdate';
    SELECT COUNT(*) INTO v_conflicts FROM tmp_sl_core_incident_state WHERE Resolution IN ('InvalidTarget', 'Conflict');

    IF NOT v_apply THEN
        SELECT Resolution, COUNT(*) AS RowCount
        FROM tmp_sl_core_incident_state GROUP BY Resolution ORDER BY Resolution;
        SELECT v_changes_to_apply AS ChangesToApply, v_conflicts AS Conflicts;
    ELSE
        IF p_expected_changes <> v_changes_to_apply THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTIS-008 expected change count does not match dry run';
        END IF;
        IF v_conflicts <> 0 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTIS-009 incident-state backfill has conflicts; no rows were changed';
        END IF;

        START TRANSACTION;
        SET v_in_transaction = TRUE;
        UPDATE liens_Cases target
        INNER JOIN tmp_sl_core_incident_state staged ON staged.TargetCaseId = target.Id
            AND staged.Resolution = 'NeedsUpdate'
        SET target.IncidentState = staged.SourceIncidentState,
            target.UpdatedAtUtc = UTC_TIMESTAMP(6), target.UpdatedByUserId = v_migration_user_id;
        SET v_rows_updated = ROW_COUNT();

        SELECT COUNT(*) INTO v_postcondition_errors
        FROM tmp_sl_core_incident_state staged
        LEFT JOIN liens_Cases target ON target.Id = staged.TargetCaseId
        WHERE staged.Resolution = 'NeedsUpdate'
          AND NOT (target.IncidentState <=> staged.SourceIncidentState);
        IF v_rows_updated <> v_changes_to_apply OR v_postcondition_errors <> 0 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTIS-010 incident-state backfill postcondition failed';
        END IF;
        COMMIT;
        SET v_in_transaction = FALSE;
        SELECT v_rows_updated AS RowsUpdated, v_changes_to_apply AS ExpectedRowsUpdated;
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_incident_state;
    DO RELEASE_LOCK(v_lock_name);
END$$

DELIMITER ;
