-- Restore legacy creator display names for an already-completed SL-CORE import.
--
-- The legacy dump only stores free-text creator values. This procedure writes
-- them to ImportedCreatedByName; it never substitutes them for CreatedByUserId.
--
-- Usage:
--   CALL liens_backfill_sl_core_imported_creators('<tenant-guid>', -1, '0');
--   CALL liens_backfill_sl_core_imported_creators('<tenant-guid>', <ChangesToApply>, '1');

-- SL-CORE uses utf8mb4_0900_ai_ci while historical Liens schemas may use
-- utf8mb4_general_ci. Cross-schema text matches below compare HEX byte strings.
SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;

DROP PROCEDURE IF EXISTS liens_backfill_sl_core_imported_creators;

DELIMITER $$

CREATE PROCEDURE liens_backfill_sl_core_imported_creators(
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
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_imported_creators;
        IF v_locked = 1 THEN DO RELEASE_LOCK(v_lock_name); END IF;
        RESIGNAL;
    END;

    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    SET v_apply = p_apply = '1';
    SET v_lock_name = CONCAT('liens:slcore:', v_tenant_id);

    IF DATABASE() NOT IN ('LS_QA_LIENS', 'LS_LIENS') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTCB-001 target schema must be LS_QA_LIENS or LS_LIENS';
    END IF;
    IF v_tenant_id IS NULL
       OR v_tenant_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR p_apply IS NULL OR p_apply NOT IN ('0', '1') OR p_expected_changes IS NULL
       OR (NOT v_apply AND p_expected_changes <> -1) OR (v_apply AND p_expected_changes < 0) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTCB-002 invalid tenant ID, expected change count, or apply flag';
    END IF;

    SELECT GET_LOCK(v_lock_name, 10) INTO v_locked;
    IF COALESCE(v_locked, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTCB-003 SL-CORE import or repair is already active';
    END IF;

    SELECT COUNT(*) INTO v_table_count
    FROM information_schema.tables
    WHERE (table_schema = DATABASE() AND table_type = 'BASE TABLE'
           AND table_name IN ('liens_Cases', 'liens_Liens', 'liens_LegacyIdCrosswalks', 'liens_LegacyImportRuns'))
       OR (table_schema = 'SL-CORE' AND table_type = 'BASE TABLE'
           AND table_name IN ('SL_CASE', 'SL_LEINS_MEDICAL', 'SL_MIGRATION_SOURCE_PROVENANCE'));
    IF v_table_count <> 7 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTCB-004 required source or target tables are unavailable';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE() AND table_name = 'liens_Liens'
          AND column_name = 'ImportedCreatedByName'
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTCB-005 apply 20260825180000_AddLienImportedCreatedByName before this backfill';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE() AND table_name = 'liens_Cases'
          AND column_name = 'ImportedCreatedByName'
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTCB-005 apply 20260825160000_AddLegacyReportParityFields before this backfill';
    END IF;

    SELECT COUNT(*) INTO v_column_count
    FROM information_schema.columns
    WHERE (table_schema = DATABASE() AND
           ((table_name = 'liens_Cases' AND column_name IN ('Id', 'TenantId', 'OrgId', 'ImportedCreatedByName', 'UpdatedAtUtc', 'UpdatedByUserId'))
         OR (table_name = 'liens_Liens' AND column_name IN ('Id', 'TenantId', 'OrgId', 'ImportedCreatedByName', 'UpdatedAtUtc', 'UpdatedByUserId'))
         OR (table_name = 'liens_LegacyIdCrosswalks' AND column_name IN ('TenantId', 'SourceSystem', 'SourceTable', 'LegacyId', 'TargetEntity', 'TargetId', 'ImportRunId'))
         OR (table_name = 'liens_LegacyImportRuns' AND column_name IN ('Id', 'TenantId', 'OrgId', 'SourceSystem', 'SourceFingerprint', 'LegacyProgram', 'MappingVersion', 'Status', 'CreatedByUserId'))))
       OR (table_schema = 'SL-CORE' AND
           ((table_name = 'SL_CASE' AND column_name IN ('CASE_ID', 'CASE_CREATE_BY', 'CASE_PROGRAM', 'CASE_IS_DELETED'))
         OR (table_name = 'SL_LEINS_MEDICAL' AND column_name IN ('LM_ID', 'LM_CREATE_BY', 'LM_PROGRAM', 'LM_IS_DELETED'))
         OR (table_name = 'SL_MIGRATION_SOURCE_PROVENANCE' AND column_name IN ('PROVENANCE_KEY', 'SOURCE_FINGERPRINT', 'IMPORT_SCOPE'))));
    IF v_column_count <> 39 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTCB-005 required source or target column contract is incomplete';
    END IF;

    SELECT COUNT(*) INTO v_core_run_count
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id AND r.SourceSystem = 'SL-CORE'
      AND r.MappingVersion = 'sl-core-core-liens-v1' AND r.Status = 'Completed'
      AND EXISTS (SELECT 1 FROM liens_LegacyIdCrosswalks x WHERE x.ImportRunId = r.Id
                  AND x.TenantId = r.TenantId AND x.SourceSystem = 'SL-CORE'
                  AND x.SourceTable = 'SL_CASE' AND x.TargetEntity = 'Case')
      AND EXISTS (SELECT 1 FROM liens_LegacyIdCrosswalks x WHERE x.ImportRunId = r.Id
                  AND x.TenantId = r.TenantId AND x.SourceSystem = 'SL-CORE'
                  AND x.SourceTable = 'SL_LEINS_MEDICAL' AND x.TargetEntity = 'Lien');
    IF v_core_run_count <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTCB-006 exactly one completed SL-CORE core import is required';
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
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTCB-007 source provenance does not match the completed core import';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_imported_creators;
    CREATE TEMPORARY TABLE tmp_sl_core_imported_creators AS
    SELECT 'Case' AS EntityType, source.CASE_ID AS LegacyId, target.Id AS TargetId,
           LEFT(NULLIF(TRIM(source.CASE_CREATE_BY), ''), 100) AS SourceCreatedBy,
           target.ImportedCreatedByName AS ExistingCreatedBy,
           CASE
             WHEN NULLIF(TRIM(source.CASE_CREATE_BY), '') IS NULL THEN 'NoSourceCreator'
             WHEN target.Id IS NULL OR target.TenantId <> v_tenant_id OR target.OrgId <> v_org_id THEN 'InvalidTarget'
             WHEN target.ImportedCreatedByName IS NULL OR TRIM(target.ImportedCreatedByName) = '' THEN 'NeedsUpdate'
             WHEN HEX(target.ImportedCreatedByName) = HEX(LEFT(NULLIF(TRIM(source.CASE_CREATE_BY), ''), 100)) THEN 'AlreadyCorrect'
             ELSE 'Conflict'
           END AS Resolution
    FROM `SL-CORE`.`SL_CASE` source
    INNER JOIN liens_LegacyIdCrosswalks x ON x.TenantId = v_tenant_id AND x.ImportRunId = v_core_run_id
        AND x.SourceSystem = 'SL-CORE' AND x.SourceTable = 'SL_CASE' AND x.TargetEntity = 'Case'
        AND HEX(x.LegacyId) = HEX(CAST(source.CASE_ID AS CHAR))
    LEFT JOIN liens_Cases target ON target.Id = x.TargetId
    WHERE HEX(CAST(source.CASE_PROGRAM AS CHAR)) = HEX(v_legacy_program)
      AND UPPER(TRIM(COALESCE(source.CASE_IS_DELETED, 'N'))) <> 'Y'
    UNION ALL
    SELECT 'Lien', source.LM_ID, target.Id,
           LEFT(NULLIF(TRIM(source.LM_CREATE_BY), ''), 100),
           target.ImportedCreatedByName,
           CASE
             WHEN NULLIF(TRIM(source.LM_CREATE_BY), '') IS NULL THEN 'NoSourceCreator'
             WHEN target.Id IS NULL OR target.TenantId <> v_tenant_id OR target.OrgId <> v_org_id THEN 'InvalidTarget'
             WHEN target.ImportedCreatedByName IS NULL OR TRIM(target.ImportedCreatedByName) = '' THEN 'NeedsUpdate'
             WHEN HEX(target.ImportedCreatedByName) = HEX(LEFT(NULLIF(TRIM(source.LM_CREATE_BY), ''), 100)) THEN 'AlreadyCorrect'
             ELSE 'Conflict'
           END
    FROM `SL-CORE`.`SL_LEINS_MEDICAL` source
    INNER JOIN liens_LegacyIdCrosswalks x ON x.TenantId = v_tenant_id AND x.ImportRunId = v_core_run_id
        AND x.SourceSystem = 'SL-CORE' AND x.SourceTable = 'SL_LEINS_MEDICAL' AND x.TargetEntity = 'Lien'
        AND HEX(x.LegacyId) = HEX(CAST(source.LM_ID AS CHAR))
    LEFT JOIN liens_Liens target ON target.Id = x.TargetId
    WHERE HEX(CAST(source.LM_PROGRAM AS CHAR)) = HEX(v_legacy_program)
      AND UPPER(TRIM(COALESCE(source.LM_IS_DELETED, 'N'))) <> 'Y';

    SELECT COUNT(*) INTO v_changes_to_apply FROM tmp_sl_core_imported_creators WHERE Resolution = 'NeedsUpdate';
    SELECT COUNT(*) INTO v_conflicts FROM tmp_sl_core_imported_creators WHERE Resolution IN ('InvalidTarget', 'Conflict');

    IF NOT v_apply THEN
        SELECT EntityType, Resolution, COUNT(*) AS RowCount
        FROM tmp_sl_core_imported_creators GROUP BY EntityType, Resolution ORDER BY EntityType, Resolution;
        SELECT v_changes_to_apply AS ChangesToApply, v_conflicts AS Conflicts;
    ELSE
        IF p_expected_changes <> v_changes_to_apply THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTCB-008 expected change count does not match dry run';
        END IF;
        IF v_conflicts <> 0 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTCB-009 creator backfill has conflicts; no rows were changed';
        END IF;

        START TRANSACTION;
        SET v_in_transaction = TRUE;
        UPDATE liens_Cases target
        INNER JOIN tmp_sl_core_imported_creators staged ON staged.EntityType = 'Case'
            AND staged.TargetId = target.Id AND staged.Resolution = 'NeedsUpdate'
        SET target.ImportedCreatedByName = staged.SourceCreatedBy,
            target.UpdatedAtUtc = UTC_TIMESTAMP(6), target.UpdatedByUserId = v_migration_user_id;
        SET v_rows_updated = ROW_COUNT();
        UPDATE liens_Liens target
        INNER JOIN tmp_sl_core_imported_creators staged ON staged.EntityType = 'Lien'
            AND staged.TargetId = target.Id AND staged.Resolution = 'NeedsUpdate'
        SET target.ImportedCreatedByName = staged.SourceCreatedBy,
            target.UpdatedAtUtc = UTC_TIMESTAMP(6), target.UpdatedByUserId = v_migration_user_id;
        SET v_rows_updated = v_rows_updated + ROW_COUNT();

        SELECT COUNT(*) INTO v_postcondition_errors
        FROM tmp_sl_core_imported_creators staged
        LEFT JOIN liens_Cases cases ON staged.EntityType = 'Case' AND cases.Id = staged.TargetId
        LEFT JOIN liens_Liens liens ON staged.EntityType = 'Lien' AND liens.Id = staged.TargetId
        WHERE staged.Resolution = 'NeedsUpdate'
          AND NOT ((staged.EntityType = 'Case' AND cases.ImportedCreatedByName <=> staged.SourceCreatedBy)
                OR (staged.EntityType = 'Lien' AND liens.ImportedCreatedByName <=> staged.SourceCreatedBy));
        IF v_rows_updated <> v_changes_to_apply OR v_postcondition_errors <> 0 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTCB-010 creator backfill postcondition failed';
        END IF;
        COMMIT;
        SET v_in_transaction = FALSE;
        SELECT v_rows_updated AS RowsUpdated, v_changes_to_apply AS ExpectedRowsUpdated;
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_imported_creators;
    DO RELEASE_LOCK(v_lock_name);
END$$

DELIMITER ;
