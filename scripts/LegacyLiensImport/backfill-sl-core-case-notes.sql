-- Materialize missing SL-CORE case notes into liens_CaseNotes.
--
-- The existing SL_CASE_NOTES and SL_CASE crosswalks provide the target note
-- and case IDs. Rows already materialized with the same source values are
-- unchanged; incompatible rows stop the complete apply.
--
-- Usage:
--   CALL liens_backfill_sl_core_case_notes('<tenant-guid>', -1, '0');
--   CALL liens_backfill_sl_core_case_notes('<tenant-guid>', <ChangesToApply>, '1');

SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;

DROP PROCEDURE IF EXISTS liens_backfill_sl_core_case_notes;

DELIMITER $$

CREATE PROCEDURE liens_backfill_sl_core_case_notes(
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
    DECLARE v_migration_user_id CHAR(36);
    DECLARE v_legacy_program VARCHAR(50);
    DECLARE v_source_fingerprint CHAR(64);
    DECLARE v_table_count INT DEFAULT 0;
    DECLARE v_column_count INT DEFAULT 0;
    DECLARE v_core_run_count INT DEFAULT 0;
    DECLARE v_provenance_count INT DEFAULT 0;
    DECLARE v_duplicate_note_crosswalk_count INT DEFAULT 0;
    DECLARE v_duplicate_note_target_count INT DEFAULT 0;
    DECLARE v_invalid_crosswalk_id_count INT DEFAULT 0;
    DECLARE v_expected_source_note_count INT DEFAULT 0;
    DECLARE v_staged_source_note_count INT DEFAULT 0;
    DECLARE v_crosswalk_coverage_errors INT DEFAULT 0;
    DECLARE v_changes_to_apply INT DEFAULT 0;
    DECLARE v_inserts_to_apply INT DEFAULT 0;
    DECLARE v_author_updates_to_apply INT DEFAULT 0;
    DECLARE v_conflicts INT DEFAULT 0;
    DECLARE v_rows_inserted INT DEFAULT 0;
    DECLARE v_rows_author_updated INT DEFAULT 0;
    DECLARE v_postcondition_errors INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        IF v_in_transaction THEN ROLLBACK; END IF;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_note_user_map;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_note_crosswalk;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_crosswalk;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_note_backfill;
        IF v_locked = 1 THEN DO RELEASE_LOCK(v_lock_name); END IF;
        RESIGNAL;
    END;

    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    SET v_apply = p_apply = '1';
    SET v_lock_name = CONCAT('liens:slcore:', v_tenant_id);

    IF DATABASE() NOT IN ('LS_QA_LIENS', 'LS_LIENS') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNCN-001 target schema must be LS_QA_LIENS or LS_LIENS';
    END IF;
    IF v_tenant_id IS NULL
       OR CHAR_LENGTH(v_tenant_id) <> 36
       OR SUBSTRING(v_tenant_id, 9, 1) <> '-'
       OR SUBSTRING(v_tenant_id, 14, 1) <> '-'
       OR SUBSTRING(v_tenant_id, 19, 1) <> '-'
       OR SUBSTRING(v_tenant_id, 24, 1) <> '-'
       OR UNHEX(REPLACE(v_tenant_id, '-', '')) IS NULL
       OR OCTET_LENGTH(UNHEX(REPLACE(v_tenant_id, '-', ''))) <> 16
       OR p_apply IS NULL OR p_apply NOT IN ('0', '1') OR p_expected_changes IS NULL
       OR (NOT v_apply AND p_expected_changes <> -1)
       OR (v_apply AND p_expected_changes < 0) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNCN-002 invalid tenant ID, expected change count, or apply flag';
    END IF;

    SELECT GET_LOCK(v_lock_name, 10) INTO v_locked;
    IF COALESCE(v_locked, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNCN-003 SL-CORE import or repair is already active for this tenant';
    END IF;

    SELECT COUNT(*) INTO v_table_count
    FROM information_schema.tables
    WHERE (table_schema = DATABASE() AND table_type = 'BASE TABLE'
           AND table_name IN ('liens_CaseNotes', 'liens_Cases', 'liens_LegacyIdCrosswalks', 'liens_LegacyImportRuns'))
       OR (table_schema = 'SL-CORE' AND table_type = 'BASE TABLE'
           AND table_name IN ('SL_CASE_NOTES', 'SL_CASE', 'SL_MIGRATION_SOURCE_PROVENANCE'));
    IF v_table_count <> 7 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNCN-004 required source or target tables are unavailable';
    END IF;

    SELECT COUNT(*) INTO v_column_count
    FROM information_schema.columns
    WHERE (table_schema = DATABASE() AND
           ((table_name = 'liens_CaseNotes'
             AND column_name IN ('Id', 'CaseId', 'TenantId', 'Content', 'Category', 'IsPinned',
                                 'CreatedByUserId', 'CreatedByName', 'IsEdited', 'IsDeleted', 'CreatedAtUtc'))
            OR (table_name = 'liens_Cases' AND column_name IN ('Id', 'TenantId'))
            OR (table_name = 'liens_LegacyIdCrosswalks'
                AND column_name IN ('TenantId', 'SourceSystem', 'SourceTable', 'LegacyId',
                                    'TargetEntity', 'TargetId', 'ImportRunId'))
            OR (table_name = 'liens_LegacyImportRuns'
                AND column_name IN ('Id', 'TenantId', 'SourceSystem', 'SourceFingerprint', 'LegacyProgram',
                                    'MappingVersion', 'Status', 'CreatedByUserId'))))
       OR (table_schema = 'SL-CORE' AND
           ((table_name = 'SL_CASE_NOTES'
             AND column_name IN ('CN_ID', 'CN_CASE_ID', 'CN_NOTE', 'CN_CREATED', 'CN_CREATED_BY',
                                 'CN_IS_DELETED', 'CN_USER_ID'))
            OR (table_name = 'SL_CASE' AND column_name IN ('CASE_ID', 'CASE_PROGRAM', 'CASE_IS_DELETED'))
            OR (table_name = 'SL_MIGRATION_SOURCE_PROVENANCE'
                AND column_name IN ('PROVENANCE_KEY', 'SOURCE_FINGERPRINT', 'IMPORT_SCOPE'))));
    IF v_column_count <> 41 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNCN-005 required source or target column contract is incomplete';
    END IF;

    START TRANSACTION;
    SET v_in_transaction = TRUE;

    SELECT COUNT(*) INTO v_core_run_count
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id AND r.SourceSystem = 'SL-CORE'
      AND r.MappingVersion = 'sl-core-core-liens-v1' AND r.Status = 'Completed'
      AND EXISTS (
          SELECT 1 FROM liens_LegacyIdCrosswalks x
          WHERE x.ImportRunId = r.Id AND x.TenantId = r.TenantId
            AND x.SourceSystem = 'SL-CORE' AND x.SourceTable = 'SL_CASE_NOTES'
            AND x.TargetEntity = 'CaseNote')
      AND EXISTS (
          SELECT 1 FROM liens_LegacyIdCrosswalks x
          WHERE x.ImportRunId = r.Id AND x.TenantId = r.TenantId
            AND x.SourceSystem = 'SL-CORE' AND x.SourceTable = 'SL_CASE'
            AND x.TargetEntity = 'Case')
    FOR UPDATE;
    IF v_core_run_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNCN-006 exactly one completed SL-CORE note-owning import is required';
    END IF;

    SELECT r.Id, LOWER(CAST(r.CreatedByUserId AS CHAR)), r.LegacyProgram, LOWER(r.SourceFingerprint)
      INTO v_core_run_id, v_migration_user_id, v_legacy_program, v_source_fingerprint
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id AND r.SourceSystem = 'SL-CORE'
      AND r.MappingVersion = 'sl-core-core-liens-v1' AND r.Status = 'Completed'
      AND EXISTS (
          SELECT 1 FROM liens_LegacyIdCrosswalks x
          WHERE x.ImportRunId = r.Id AND x.TenantId = r.TenantId
            AND x.SourceSystem = 'SL-CORE' AND x.SourceTable = 'SL_CASE_NOTES'
            AND x.TargetEntity = 'CaseNote')
      AND EXISTS (
          SELECT 1 FROM liens_LegacyIdCrosswalks x
          WHERE x.ImportRunId = r.Id AND x.TenantId = r.TenantId
            AND x.SourceSystem = 'SL-CORE' AND x.SourceTable = 'SL_CASE'
            AND x.TargetEntity = 'Case');

    SELECT COUNT(*) INTO v_provenance_count
    FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE`
    WHERE PROVENANCE_KEY = 'sl-core-current'
      AND HEX(LOWER(SOURCE_FINGERPRINT)) = HEX(v_source_fingerprint)
      AND HEX(IMPORT_SCOPE) = HEX('sl-core-core-liens-v1');
    IF v_provenance_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNCN-007 source provenance does not match the completed import';
    END IF;

    SELECT COUNT(*) INTO v_duplicate_note_crosswalk_count
    FROM (
        SELECT x.LegacyId
        FROM liens_LegacyIdCrosswalks x
        WHERE x.TenantId = v_tenant_id AND x.ImportRunId = v_core_run_id
          AND x.SourceSystem = 'SL-CORE' AND x.SourceTable = 'SL_CASE_NOTES'
          AND x.TargetEntity = 'CaseNote'
        GROUP BY x.LegacyId HAVING COUNT(*) <> 1
    ) AS duplicate_note_crosswalks;
    SELECT COUNT(*) INTO v_duplicate_note_target_count
    FROM (
        SELECT x.TargetId
        FROM liens_LegacyIdCrosswalks x
        WHERE x.TenantId = v_tenant_id AND x.ImportRunId = v_core_run_id
          AND x.SourceSystem = 'SL-CORE' AND x.SourceTable = 'SL_CASE_NOTES'
          AND x.TargetEntity = 'CaseNote'
        GROUP BY x.TargetId HAVING COUNT(*) <> 1
    ) AS duplicate_note_targets;
    IF v_duplicate_note_crosswalk_count <> 0 OR v_duplicate_note_target_count <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNCN-008 duplicate SL-CORE case-note crosswalk entries found';
    END IF;

    SELECT COUNT(*) INTO v_invalid_crosswalk_id_count
    FROM liens_LegacyIdCrosswalks x
    WHERE x.TenantId = v_tenant_id AND x.ImportRunId = v_core_run_id
      AND x.SourceSystem = 'SL-CORE'
      AND x.SourceTable IN ('SL_CASE_NOTES', 'SL_CASE')
      AND x.TargetEntity IN ('CaseNote', 'Case')
      AND (NULLIF(TRIM(x.LegacyId), '') IS NULL
           OR TRIM(x.LegacyId) NOT REGEXP '^[0-9]+$');
    IF v_invalid_crosswalk_id_count <> 0
       OR v_legacy_program IS NULL
       OR TRIM(v_legacy_program) NOT REGEXP '^[0-9]+$' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNCN-009 legacy case, note, or program IDs must be numeric';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_note_crosswalk;
    CREATE TEMPORARY TABLE tmp_sl_core_note_crosswalk (
        LegacyNoteId BIGINT UNSIGNED NOT NULL PRIMARY KEY,
        TargetNoteId CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
        UNIQUE KEY UX_tmp_sl_core_note_crosswalk_target (TargetNoteId)
    ) ENGINE=InnoDB;
    INSERT INTO tmp_sl_core_note_crosswalk (LegacyNoteId, TargetNoteId)
    SELECT CAST(TRIM(x.LegacyId) AS UNSIGNED), x.TargetId
    FROM liens_LegacyIdCrosswalks x
    WHERE x.TenantId = v_tenant_id AND x.ImportRunId = v_core_run_id
      AND x.SourceSystem = 'SL-CORE' AND x.SourceTable = 'SL_CASE_NOTES'
      AND x.TargetEntity = 'CaseNote';

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_crosswalk;
    CREATE TEMPORARY TABLE tmp_sl_core_case_crosswalk (
        LegacyCaseId BIGINT UNSIGNED NOT NULL PRIMARY KEY,
        TargetCaseId CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
        UNIQUE KEY UX_tmp_sl_core_case_crosswalk_target (TargetCaseId)
    ) ENGINE=InnoDB;
    INSERT INTO tmp_sl_core_case_crosswalk (LegacyCaseId, TargetCaseId)
    SELECT CAST(TRIM(x.LegacyId) AS UNSIGNED), x.TargetId
    FROM liens_LegacyIdCrosswalks x
    WHERE x.TenantId = v_tenant_id AND x.ImportRunId = v_core_run_id
      AND x.SourceSystem = 'SL-CORE' AND x.SourceTable = 'SL_CASE'
      AND x.TargetEntity = 'Case';

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_note_user_map;
    CREATE TEMPORARY TABLE tmp_sl_core_case_note_user_map (
        LegacyCreatedByName VARCHAR(250) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL PRIMARY KEY,
        V3UserId CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL
    );
    INSERT INTO tmp_sl_core_case_note_user_map (LegacyCreatedByName, V3UserId) VALUES
        ('Meagan Pugong', '01a02571-5e6b-7b80-9c08-48b919999ebd'),
        ('Magie Solleza', '01a0256f-44e9-736d-8e72-b1c751c70cc3'),
        ('Dani Manuel', '01a0256b-a824-71ba-9725-bbb62afada7d'),
        ('Maria Melchor', '01a02572-c03d-765e-a1a2-419bf1c105bf'),
        ('Maricel Tinapay', '019f1a05-792f-74f2-b071-4fdc0d6bd30a'),
        ('migration', '019f1a05-792f-74f2-b071-4fdc0d6bd30a'),
        ('system-migration', '019f1a05-792f-74f2-b071-4fdc0d6bd30a'),
        ('SuperAdmin SuperAdmin', '019f1a05-792f-74f2-b071-4fdc0d6bd30a'),
        ('Sharrel Tibay', '019f1a05-792f-74f2-b071-4fdc0d6bd30a');

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_note_backfill;
    CREATE TEMPORARY TABLE tmp_sl_core_case_note_backfill (
        TargetNoteId CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL PRIMARY KEY,
        TargetCaseId CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
        DesiredContent LONGTEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
        DesiredCategory VARCHAR(32) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
        DesiredUserId CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
        DesiredUserName TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
        DesiredIsDeleted TINYINT(1) NOT NULL,
        DesiredCreatedAtUtc DATETIME(6) NOT NULL,
        Resolution VARCHAR(32) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
        KEY IX_tmp_sl_core_case_note_backfill_resolution (Resolution)
    ) ENGINE=InnoDB;
    INSERT INTO tmp_sl_core_case_note_backfill
        (TargetNoteId, TargetCaseId, DesiredContent, DesiredCategory, DesiredUserId,
         DesiredUserName, DesiredIsDeleted, DesiredCreatedAtUtc, Resolution)
    SELECT
        note_map.TargetNoteId,
        case_map.TargetCaseId,
        TRIM(source_note.CN_NOTE) AS DesiredContent,
        CASE WHEN source_note.CN_USER_ID IS NULL THEN 'general' ELSE 'feed' END AS DesiredCategory,
        COALESCE(user_map.V3UserId, v_migration_user_id) AS DesiredUserId,
        IF(
            NULLIF(TRIM(source_note.CN_CREATED_BY), '') IS NULL
            OR CAST(TRIM(source_note.CN_CREATED_BY) AS BINARY) = CAST('migration' AS BINARY),
            'system-migration',
            TRIM(source_note.CN_CREATED_BY)
        ) AS DesiredUserName,
        CASE WHEN UPPER(TRIM(COALESCE(source_note.CN_IS_DELETED, 'N'))) = 'Y' THEN 1 ELSE 0 END AS DesiredIsDeleted,
        COALESCE(source_note.CN_CREATED, UTC_TIMESTAMP(6)) AS DesiredCreatedAtUtc,
        CASE
            WHEN CHAR_LENGTH(TRIM(source_note.CN_NOTE)) > 5000
              OR CHAR_LENGTH(TRIM(source_note.CN_CREATED_BY)) > 250
                THEN 'InvalidSource'
            WHEN target_case.Id IS NULL OR target_case.TenantId <> v_tenant_id
                THEN 'InvalidTarget'
            WHEN note.Id IS NULL THEN 'NeedsInsert'
            WHEN note.TenantId = v_tenant_id
             AND note.CaseId = case_map.TargetCaseId
             AND BINARY note.Content = BINARY TRIM(source_note.CN_NOTE)
             AND note.Category = CASE WHEN source_note.CN_USER_ID IS NULL THEN 'general' ELSE 'feed' END
             AND LOWER(CAST(note.CreatedByUserId AS CHAR)) = LOWER(COALESCE(user_map.V3UserId, v_migration_user_id))
             AND CAST(note.CreatedByName AS BINARY) = CAST(IF(
                 NULLIF(TRIM(source_note.CN_CREATED_BY), '') IS NULL
                 OR CAST(TRIM(source_note.CN_CREATED_BY) AS BINARY) = CAST('migration' AS BINARY),
                 'system-migration',
                 TRIM(source_note.CN_CREATED_BY)
             ) AS BINARY)
             AND note.IsPinned = 0 AND note.IsEdited = 0
             AND note.IsDeleted = CASE WHEN UPPER(TRIM(COALESCE(source_note.CN_IS_DELETED, 'N'))) = 'Y' THEN 1 ELSE 0 END
                THEN 'AlreadyCorrect'
            WHEN note.TenantId = v_tenant_id
             AND note.CaseId = case_map.TargetCaseId
             AND BINARY note.Content = BINARY TRIM(source_note.CN_NOTE)
             AND note.Category = CASE WHEN source_note.CN_USER_ID IS NULL THEN 'general' ELSE 'feed' END
             AND LOWER(CAST(note.CreatedByUserId AS CHAR)) = v_migration_user_id
             AND (BINARY note.CreatedByName = BINARY 'Legacy SL-CORE'
                  OR BINARY note.CreatedByName = BINARY 'migration'
                  OR BINARY note.CreatedByName = BINARY 'system-migration'
                  OR CAST(note.CreatedByName AS BINARY) = CAST(IF(
                      NULLIF(TRIM(source_note.CN_CREATED_BY), '') IS NULL
                      OR CAST(TRIM(source_note.CN_CREATED_BY) AS BINARY) = CAST('migration' AS BINARY),
                      'system-migration',
                      TRIM(source_note.CN_CREATED_BY)
                  ) AS BINARY))
             AND note.IsPinned = 0 AND note.IsEdited = 0
             AND note.IsDeleted = CASE WHEN UPPER(TRIM(COALESCE(source_note.CN_IS_DELETED, 'N'))) = 'Y' THEN 1 ELSE 0 END
                THEN 'NeedsAuthorUpdate'
            ELSE 'Conflict'
        END AS Resolution
    FROM `SL-CORE`.`SL_CASE_NOTES` source_note
    INNER JOIN `SL-CORE`.`SL_CASE` source_case
        ON source_case.CASE_ID = source_note.CN_CASE_ID
       AND source_case.CASE_PROGRAM = CAST(v_legacy_program AS UNSIGNED)
       AND UPPER(TRIM(COALESCE(source_case.CASE_IS_DELETED, 'N'))) <> 'Y'
    INNER JOIN tmp_sl_core_note_crosswalk note_map
        ON note_map.LegacyNoteId = source_note.CN_ID
    INNER JOIN tmp_sl_core_case_crosswalk case_map
        ON case_map.LegacyCaseId = source_note.CN_CASE_ID
    LEFT JOIN liens_CaseNotes note ON note.Id = note_map.TargetNoteId
    LEFT JOIN liens_Cases target_case ON target_case.Id = case_map.TargetCaseId
    LEFT JOIN tmp_sl_core_case_note_user_map user_map
        ON BINARY TRIM(source_note.CN_CREATED_BY) = BINARY user_map.LegacyCreatedByName
    WHERE NULLIF(TRIM(source_note.CN_NOTE), '') IS NOT NULL;

    SELECT COUNT(*) INTO v_expected_source_note_count
    FROM tmp_sl_core_note_crosswalk;
    SELECT COUNT(*) INTO v_staged_source_note_count
    FROM tmp_sl_core_case_note_backfill;
    SET v_crosswalk_coverage_errors = ABS(v_expected_source_note_count - v_staged_source_note_count);

    SELECT COUNT(*) INTO v_inserts_to_apply
    FROM tmp_sl_core_case_note_backfill WHERE Resolution = 'NeedsInsert';
    SELECT COUNT(*) INTO v_author_updates_to_apply
    FROM tmp_sl_core_case_note_backfill WHERE Resolution = 'NeedsAuthorUpdate';
    SET v_changes_to_apply = v_inserts_to_apply + v_author_updates_to_apply;
    SELECT COUNT(*) INTO v_conflicts
    FROM tmp_sl_core_case_note_backfill WHERE Resolution IN ('InvalidSource', 'InvalidTarget', 'Conflict');
    SET v_conflicts = v_conflicts + v_crosswalk_coverage_errors;

    IF NOT v_apply THEN
        SELECT
            v_changes_to_apply AS ChangesToApply,
            v_conflicts AS Conflicts,
            v_inserts_to_apply AS InsertsToApply,
            v_author_updates_to_apply AS AuthorUpdatesToApply,
            SUM(Resolution = 'AlreadyCorrect') AS AlreadyCorrect,
            SUM(Resolution = 'InvalidSource') AS InvalidSource,
            SUM(Resolution = 'InvalidTarget') AS InvalidTarget,
            SUM(Resolution = 'Conflict') AS ExistingTargetConflicts,
            v_crosswalk_coverage_errors AS CrosswalkCoverageErrors
        FROM tmp_sl_core_case_note_backfill;
        IF v_conflicts <> 0 THEN
            SELECT
                note_map.LegacyNoteId,
                case_map.LegacyCaseId,
                staged.TargetNoteId,
                staged.TargetCaseId,
                CAST(target_case.TenantId AS CHAR) AS TargetCaseTenantId,
                staged.Resolution,
                CASE
                    WHEN staged.Resolution = 'InvalidTarget' AND target_case.Id IS NULL
                        THEN 'MissingTargetCase'
                    WHEN staged.Resolution = 'InvalidTarget'
                        THEN 'TargetCaseTenantMismatch'
                    WHEN staged.Resolution = 'InvalidSource'
                        THEN 'InvalidLegacyNoteData'
                    ELSE 'ExistingTargetConflict'
                END AS ConflictReason
            FROM tmp_sl_core_case_note_backfill staged
            LEFT JOIN tmp_sl_core_note_crosswalk note_map
                ON note_map.TargetNoteId = staged.TargetNoteId
            LEFT JOIN tmp_sl_core_case_crosswalk case_map
                ON case_map.TargetCaseId = staged.TargetCaseId
            LEFT JOIN liens_Cases target_case
                ON target_case.Id = staged.TargetCaseId
            WHERE staged.Resolution IN ('InvalidSource', 'InvalidTarget', 'Conflict')
            ORDER BY case_map.LegacyCaseId, note_map.LegacyNoteId;
        END IF;
        ROLLBACK;
        SET v_in_transaction = FALSE;
    ELSE
        IF p_expected_changes <> v_changes_to_apply THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLNCN-010 expected change count does not match dry run';
        END IF;
        IF v_conflicts <> 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLNCN-011 case-note backfill has conflicts; no rows were inserted';
        END IF;

        INSERT INTO liens_CaseNotes
            (Id, CaseId, TenantId, Content, Category, IsPinned, CreatedByUserId, CreatedByName,
             IsEdited, IsDeleted, CreatedAtUtc, UpdatedAtUtc)
        SELECT TargetNoteId, TargetCaseId, v_tenant_id, DesiredContent, DesiredCategory, 0,
               DesiredUserId, DesiredUserName, 0, DesiredIsDeleted, DesiredCreatedAtUtc, NULL
        FROM tmp_sl_core_case_note_backfill WHERE Resolution = 'NeedsInsert';
        SET v_rows_inserted = ROW_COUNT();

        UPDATE liens_CaseNotes c
        INNER JOIN tmp_sl_core_case_note_backfill s
            ON s.TargetNoteId = c.Id AND s.Resolution = 'NeedsAuthorUpdate'
        SET c.CreatedByUserId = s.DesiredUserId, c.CreatedByName = s.DesiredUserName
        WHERE c.TenantId = v_tenant_id
          AND c.CaseId = s.TargetCaseId
          AND BINARY c.Content = BINARY s.DesiredContent
          AND c.Category = s.DesiredCategory
          AND LOWER(CAST(c.CreatedByUserId AS CHAR)) = v_migration_user_id
          AND (BINARY c.CreatedByName = BINARY 'Legacy SL-CORE'
               OR BINARY c.CreatedByName = BINARY 'migration'
               OR BINARY c.CreatedByName = BINARY 'system-migration'
               OR BINARY c.CreatedByName = BINARY s.DesiredUserName)
          AND c.IsPinned = 0 AND c.IsEdited = 0
          AND c.IsDeleted = s.DesiredIsDeleted;
        SET v_rows_author_updated = ROW_COUNT();

        SELECT COUNT(*) INTO v_postcondition_errors
        FROM tmp_sl_core_case_note_backfill staged
        LEFT JOIN liens_CaseNotes note ON note.Id = staged.TargetNoteId
        LEFT JOIN liens_Cases target_case ON target_case.Id = note.CaseId
        WHERE staged.Resolution IN ('NeedsInsert', 'NeedsAuthorUpdate')
          AND (note.Id IS NULL OR target_case.Id IS NULL
               OR note.TenantId <> v_tenant_id OR target_case.TenantId <> v_tenant_id
               OR note.CaseId <> staged.TargetCaseId
               OR BINARY note.Content <> BINARY staged.DesiredContent
               OR note.Category <> staged.DesiredCategory OR note.IsPinned <> 0
               OR LOWER(CAST(note.CreatedByUserId AS CHAR)) <> LOWER(staged.DesiredUserId)
               OR BINARY note.CreatedByName <> BINARY staged.DesiredUserName
               OR note.IsEdited <> 0 OR note.IsDeleted <> staged.DesiredIsDeleted);
        IF v_rows_inserted <> v_inserts_to_apply
           OR v_rows_author_updated <> v_author_updates_to_apply
           OR v_postcondition_errors <> 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLNCN-012 case-note backfill postcondition failed';
        END IF;

        COMMIT;
        SET v_in_transaction = FALSE;
        SELECT v_rows_inserted AS RowsInserted,
               v_rows_author_updated AS RowsAuthorUpdated,
               v_changes_to_apply AS ExpectedRowsChanged;
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_note_user_map;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_note_crosswalk;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_crosswalk;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_case_note_backfill;
    DO RELEASE_LOCK(v_lock_name);
END$$

DELIMITER ;
