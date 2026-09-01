-- Program 1 SL_CASE_UPDATE_LOG / SL_LIENS_UPDATE_LOG import.
--
-- The controlled restore must be in the separate `SL-CORE` schema on the same
-- MySQL server as the selected LS_QA_LIENS or LS_LIENS target, matching the
-- existing complete SL-CORE stored-procedure workflow. Execute this file as a
-- script to install the procedure. It is dry-run-first and never reads a file.
--
-- Dry run:
--   CALL liens_import_program_1_update_history(
--     '<tenant-guid>', '<org-guid>', '<migration-actor-guid>', NULL,
--     0, -1, -1, -1, NULL);
--
-- Apply only after an Identity-owned release process creates a separate,
-- unconsumed approval with MappingVersion = 'sl-core-update-history-v2':
--   CALL liens_import_program_1_update_history(
--     '<tenant-guid>', '<org-guid>', '<migration-actor-guid>', '<approval-guid>',
--     1, <case-count>, <lien-count>, <excluded-count>, '<dry-run-checksum>');
--
-- Error prefix: LSLUH-

SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;

DROP PROCEDURE IF EXISTS liens_import_program_1_update_history;

DELIMITER $$

CREATE PROCEDURE liens_import_program_1_update_history(
    IN p_tenant_id CHAR(36),
    IN p_org_id CHAR(36),
    IN p_migration_user_id CHAR(36),
    IN p_approval_id CHAR(36),
    IN p_apply TINYINT,
    IN p_expected_case_events INT,
    IN p_expected_lien_events INT,
    IN p_expected_excluded_events INT,
    IN p_expected_checksum CHAR(64)
)
SQL SECURITY DEFINER
main: BEGIN
    DECLARE v_schema VARCHAR(64);
    DECLARE v_tenant_id CHAR(36);
    DECLARE v_org_id CHAR(36);
    DECLARE v_actor_id CHAR(36);
    DECLARE v_apply BOOLEAN DEFAULT FALSE;
    DECLARE v_source_fingerprint CHAR(64) DEFAULT
        '3adccecf8a38114a14cd500240aab2a4db3d9bf45f00945c659dc3b5252663fe';
    DECLARE v_mapping_version VARCHAR(100) DEFAULT 'sl-core-update-history-v2';
    DECLARE v_source_system VARCHAR(100) DEFAULT 'SL-CORE';
    DECLARE v_original_time_zone VARCHAR(64);
    DECLARE v_original_group_concat_max_len BIGINT;
    DECLARE v_session_changed BOOLEAN DEFAULT FALSE;
    DECLARE v_core_lock_name VARCHAR(255);
    DECLARE v_core_lock_acquired INT DEFAULT 0;
    DECLARE v_lock_name VARCHAR(255);
    DECLARE v_lock_acquired INT DEFAULT 0;
    DECLARE v_in_transaction BOOLEAN DEFAULT FALSE;
    DECLARE v_run_created BOOLEAN DEFAULT FALSE;
    DECLARE v_run_id CHAR(36);
    DECLARE v_run_started_at DATETIME(6);
    DECLARE v_required_tables INT DEFAULT 0;
    DECLARE v_required_columns INT DEFAULT 0;
    DECLARE v_required_indexes INT DEFAULT 0;
    DECLARE v_required_constraints INT DEFAULT 0;
    DECLARE v_required_check_clauses INT DEFAULT 0;
    DECLARE v_fk_contract INT DEFAULT 0;
    DECLARE v_provenance_count INT DEFAULT 0;
    DECLARE v_core_run_count INT DEFAULT 0;
    DECLARE v_running_run_count INT DEFAULT 0;
    DECLARE v_anchor_count INT DEFAULT 0;
    DECLARE v_anchor_errors INT DEFAULT 0;
    DECLARE v_timestamp_errors INT DEFAULT 0;
    DECLARE v_blockers INT DEFAULT 0;
    DECLARE v_pending_events INT DEFAULT 0;
    DECLARE v_case_events INT DEFAULT 0;
    DECLARE v_lien_events INT DEFAULT 0;
    DECLARE v_case_inserts INT DEFAULT 0;
    DECLARE v_lien_inserts INT DEFAULT 0;
    DECLARE v_case_skips INT DEFAULT 0;
    DECLARE v_lien_skips INT DEFAULT 0;
    DECLARE v_excluded INT DEFAULT 0;
    DECLARE v_out_of_scope INT DEFAULT 0;
    DECLARE v_blank_lien_cases INT DEFAULT 0;
    DECLARE v_approved_mismatches INT DEFAULT 0;
    DECLARE v_case_details_updates INT DEFAULT 0;
    DECLARE v_case_creations INT DEFAULT 0;
    DECLARE v_case_personal_updates INT DEFAULT 0;
    DECLARE v_case_action_count INT DEFAULT 0;
    DECLARE v_lien_creations INT DEFAULT 0;
    DECLARE v_lien_payee_creations INT DEFAULT 0;
    DECLARE v_lien_updates INT DEFAULT 0;
    DECLARE v_lien_medical_code_updates INT DEFAULT 0;
    DECLARE v_lien_medical_info_updates INT DEFAULT 0;
    DECLARE v_lien_payee_updates INT DEFAULT 0;
    DECLARE v_lien_action_count INT DEFAULT 0;
    DECLARE v_checksum CHAR(64);
    DECLARE v_approval_binding_hash CHAR(64);
    DECLARE v_approval_count INT DEFAULT 0;
    DECLARE v_approval_manifest_hash VARCHAR(128);
    DECLARE v_approval_reference VARCHAR(200);
    DECLARE v_existing_run_count INT DEFAULT 0;
    DECLARE v_existing_run_id CHAR(36);
    DECLARE v_existing_inserted_events INT DEFAULT 0;
    DECLARE v_existing_event_count INT DEFAULT 0;
    DECLARE v_existing_crosswalk_count INT DEFAULT 0;
    DECLARE v_existing_event_crosswalk_count INT DEFAULT 0;
    DECLARE v_existing_joined_count INT DEFAULT 0;
    DECLARE v_existing_planned_count INT DEFAULT 0;
    DECLARE v_existing_exception_count INT DEFAULT 0;
    DECLARE v_existing_matching_exception_count INT DEFAULT 0;
    DECLARE v_existing_matching_exception_keys INT DEFAULT 0;
    DECLARE v_inserted_events INT DEFAULT 0;
    DECLARE v_inserted_crosswalks INT DEFAULT 0;
    DECLARE v_inserted_exceptions INT DEFAULT 0;
    DECLARE v_reconciliation_errors INT DEFAULT 0;
    DECLARE v_error_message TEXT;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        GET DIAGNOSTICS CONDITION 1 v_error_message = MESSAGE_TEXT;
        IF v_in_transaction THEN
            ROLLBACK;
            SET v_in_transaction = FALSE;
        END IF;
        IF v_run_created THEN
            UPDATE liens_LegacyImportRuns
            SET Status = 'Failed',
                CompletedAtUtc = UTC_TIMESTAMP(6),
                ErrorSummary = LEFT(COALESCE(v_error_message, 'StoredProcedureFailure'), 2000)
            WHERE Id = v_run_id AND Status = 'Running';
        END IF;
        DROP TEMPORARY TABLE IF EXISTS tmp_luh_plan;
        DROP TEMPORARY TABLE IF EXISTS tmp_luh_foreign_keys;
        IF v_session_changed THEN
            SET @@session.time_zone = v_original_time_zone;
            SET @@session.group_concat_max_len = v_original_group_concat_max_len;
        END IF;
        IF v_lock_acquired = 1 THEN
            DO RELEASE_LOCK(v_lock_name);
        END IF;
        IF v_core_lock_acquired = 1 THEN
            DO RELEASE_LOCK(v_core_lock_name);
        END IF;
        RESIGNAL;
    END;

    SET v_schema = DATABASE();
    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    SET v_org_id = LOWER(TRIM(p_org_id));
    SET v_actor_id = LOWER(TRIM(p_migration_user_id));
    SET v_apply = (p_apply = 1);

    IF v_schema NOT IN ('LS_QA_LIENS', 'LS_LIENS') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-001 target schema must be LS_QA_LIENS or LS_LIENS';
    END IF;

    IF v_tenant_id IS NULL
       OR v_org_id IS NULL
       OR v_actor_id IS NULL
       OR v_tenant_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR v_org_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR v_actor_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR p_apply IS NULL OR p_apply NOT IN (0, 1) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-002 valid tenant, organization, actor, and apply flag are required';
    END IF;

    SET v_original_time_zone = @@session.time_zone;
    SET v_original_group_concat_max_len = @@session.group_concat_max_len;
    SET @@session.time_zone = '+00:00';
    SET @@session.group_concat_max_len = 8388608;
    SET v_session_changed = TRUE;

    SET v_core_lock_name = CONCAT('liens:slcore:', v_tenant_id);
    SELECT GET_LOCK(v_core_lock_name, 10) INTO v_core_lock_acquired;
    IF COALESCE(v_core_lock_acquired, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-003 Program 1 core import or backfill is already active';
    END IF;

    SET v_lock_name = CONCAT('legalsynq:luh:', v_tenant_id);
    SELECT GET_LOCK(v_lock_name, 10) INTO v_lock_acquired;
    IF COALESCE(v_lock_acquired, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-003 update-history import or compensation is already active';
    END IF;

    SELECT COUNT(*) INTO v_required_tables
    FROM information_schema.tables
    WHERE table_type = 'BASE TABLE'
      AND ((table_schema = v_schema AND table_name IN (
              'liens_LegacyUpdateEvents', 'liens_LegacyImportRuns',
              'liens_LegacyImportApprovals', 'liens_LegacyIdCrosswalks',
              'liens_LegacyImportExceptions', 'liens_Cases', 'liens_Liens'))
        OR (table_schema = 'SL-CORE' AND table_name IN (
              'SL_CASE_UPDATE_LOG', 'SL_LIENS_UPDATE_LOG', 'SL_CASE',
              'SL_LEINS_MEDICAL', 'SL_CASE_NOTES',
              'SL_MIGRATION_SOURCE_PROVENANCE')));
    IF v_required_tables <> 13 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-004 required source tables in SL-CORE or target tables are missing';
    END IF;

    SELECT COUNT(*) INTO v_required_columns
    FROM information_schema.columns
    WHERE table_schema = v_schema
      AND table_name = 'liens_LegacyUpdateEvents'
      AND (
          (column_name IN ('Id', 'TenantId', 'OrgId', 'CaseId', 'ImportRunId')
              AND LOWER(column_type) = 'char(36)' AND is_nullable = 'NO'
              AND LOWER(collation_name) = 'ascii_general_ci')
       OR (column_name = 'LienId' AND LOWER(column_type) = 'char(36)'
              AND is_nullable = 'YES' AND LOWER(collation_name) = 'ascii_general_ci')
       OR (column_name = 'Scope' AND LOWER(column_type) = 'varchar(20)'
              AND is_nullable = 'NO' AND LOWER(collation_name) LIKE 'utf8mb4%')
       OR (column_name = 'Action' AND LOWER(column_type) = 'varchar(255)'
              AND is_nullable = 'NO' AND LOWER(collation_name) LIKE 'utf8mb4%')
       OR (column_name = 'Description' AND LOWER(column_type) = 'text'
              AND is_nullable = 'YES' AND LOWER(collation_name) LIKE 'utf8mb4%')
       OR (column_name = 'ActorDisplayName' AND LOWER(column_type) = 'varchar(255)'
              AND is_nullable = 'YES' AND LOWER(collation_name) LIKE 'utf8mb4%')
       OR (column_name IN ('OccurredAtUtc', 'ImportedAtUtc')
              AND LOWER(column_type) = 'datetime(6)' AND is_nullable = 'NO'
              AND collation_name IS NULL)
       OR (column_name IN ('SourceSystem', 'SourceTable', 'LegacyId')
              AND LOWER(column_type) = 'varchar(100)' AND is_nullable = 'NO'
              AND LOWER(collation_name) LIKE 'utf8mb4%')
       OR (column_name = 'LegacySequence' AND LOWER(column_type) = 'bigint'
              AND is_nullable = 'NO' AND collation_name IS NULL)
      );
    IF v_required_columns <> 16 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-005 legacy update-event column contract is missing or incompatible';
    END IF;

    SELECT COUNT(*) INTO v_required_columns
    FROM information_schema.columns
    WHERE table_schema = 'SL-CORE'
      AND ((table_name = 'SL_CASE_UPDATE_LOG' AND column_name IN (
              'CUL_ID', 'CUL_CASE_ID', 'CUL_LIEN_ID', 'CUL_ACTION',
              'CUL_DESCRIPTION', 'CUL_UPDATED_BY', 'CUL_TIMESTAMP'))
        OR (table_name = 'SL_LIENS_UPDATE_LOG' AND column_name IN (
              'LU_ID', 'LU_CASE_ID', 'LU_LIEN_ID', 'LU_ACTION',
              'LU_DESCRIPTION', 'LU_UPDATED_BY', 'LU_TIMESTAMP'))
        OR (table_name = 'SL_CASE' AND column_name IN (
              'CASE_ID', 'CASE_PROGRAM', 'CASE_IS_DELETED'))
        OR (table_name = 'SL_LEINS_MEDICAL' AND column_name IN (
              'LM_ID', 'LM_CASE_ID', 'LM_IS_DELETED'))
        OR (table_name = 'SL_CASE_NOTES' AND column_name IN (
              'CN_NOTE', 'CN_CREATED'))
        OR (table_name = 'SL_MIGRATION_SOURCE_PROVENANCE' AND column_name IN (
              'PROVENANCE_KEY', 'SOURCE_FINGERPRINT', 'IMPORT_SCOPE',
              'TIMESTAMP_SEMANTICS')));
    IF v_required_columns <> 26 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-005 SL-CORE update-history source column contract is incomplete';
    END IF;

    SELECT COUNT(*) INTO v_required_indexes
    FROM (
        SELECT index_name
        FROM information_schema.statistics
        WHERE table_schema = v_schema
          AND table_name = 'liens_LegacyUpdateEvents'
          AND index_name IN (
              'PRIMARY', 'UX_LegacyUpdateEvents_Tenant_Source_Table_Key',
              'IX_LegacyUpdateEvents_CaseTimeline', 'IX_LegacyUpdateEvents_ImportRunId',
              'IX_LegacyUpdateEvents_LienTimeline')
        GROUP BY index_name
        HAVING
          (index_name = 'PRIMARY' AND MAX(non_unique) = 0
           AND GROUP_CONCAT(CONCAT(column_name, ':', COALESCE(collation, 'A'), ':',
                   COALESCE(CAST(sub_part AS CHAR), 'FULL'), ':', is_visible)
               ORDER BY seq_in_index SEPARATOR ',') = 'Id:A:FULL:YES')
          OR (index_name = 'UX_LegacyUpdateEvents_Tenant_Source_Table_Key' AND MAX(non_unique) = 0
           AND GROUP_CONCAT(CONCAT(column_name, ':', COALESCE(collation, 'A'), ':',
                   COALESCE(CAST(sub_part AS CHAR), 'FULL'), ':', is_visible)
               ORDER BY seq_in_index SEPARATOR ',') =
               'TenantId:A:FULL:YES,SourceSystem:A:FULL:YES,SourceTable:A:FULL:YES,LegacyId:A:FULL:YES')
          OR (index_name = 'IX_LegacyUpdateEvents_CaseTimeline' AND MIN(non_unique) = 1
           AND GROUP_CONCAT(CONCAT(column_name, ':', COALESCE(collation, 'A'), ':',
                   COALESCE(CAST(sub_part AS CHAR), 'FULL'), ':', is_visible)
               ORDER BY seq_in_index SEPARATOR ',') =
               'TenantId:A:FULL:YES,CaseId:A:FULL:YES,Scope:A:FULL:YES,OccurredAtUtc:D:FULL:YES,LegacySequence:D:FULL:YES')
          OR (index_name = 'IX_LegacyUpdateEvents_ImportRunId' AND MIN(non_unique) = 1
           AND GROUP_CONCAT(CONCAT(column_name, ':', COALESCE(collation, 'A'), ':',
                   COALESCE(CAST(sub_part AS CHAR), 'FULL'), ':', is_visible)
               ORDER BY seq_in_index SEPARATOR ',') = 'ImportRunId:A:FULL:YES')
          OR (index_name = 'IX_LegacyUpdateEvents_LienTimeline' AND MIN(non_unique) = 1
           AND GROUP_CONCAT(CONCAT(column_name, ':', COALESCE(collation, 'A'), ':',
                   COALESCE(CAST(sub_part AS CHAR), 'FULL'), ':', is_visible)
               ORDER BY seq_in_index SEPARATOR ',') =
               'TenantId:A:FULL:YES,LienId:A:FULL:YES,OccurredAtUtc:D:FULL:YES,LegacySequence:D:FULL:YES')
    ) exact_indexes;
    IF v_required_indexes <> 5 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-006 legacy update-event index contract is incomplete';
    END IF;

    SELECT COUNT(*) INTO v_required_constraints
    FROM information_schema.table_constraints
    WHERE table_schema = v_schema
      AND table_name = 'liens_LegacyUpdateEvents'
      AND ((constraint_name = 'PRIMARY' AND constraint_type = 'PRIMARY KEY')
        OR (constraint_name = 'CK_LegacyUpdateEvents_Scope' AND constraint_type = 'CHECK' AND enforced = 'YES')
        OR (constraint_name = 'CK_LegacyUpdateEvents_ScopeLien' AND constraint_type = 'CHECK' AND enforced = 'YES')
        OR (constraint_name = 'FK_liens_LegacyUpdateEvents_liens_LegacyImportRuns_ImportRunId'
            AND constraint_type = 'FOREIGN KEY'));
    SELECT COUNT(*) INTO v_fk_contract
    FROM information_schema.key_column_usage key_column
    INNER JOIN information_schema.referential_constraints reference_rule
      ON reference_rule.constraint_schema = key_column.constraint_schema
     AND reference_rule.constraint_name = key_column.constraint_name
    WHERE key_column.constraint_schema = v_schema
      AND key_column.table_name = 'liens_LegacyUpdateEvents'
      AND key_column.constraint_name = 'FK_liens_LegacyUpdateEvents_liens_LegacyImportRuns_ImportRunId'
      AND key_column.column_name = 'ImportRunId'
      AND key_column.referenced_table_name = 'liens_LegacyImportRuns'
      AND key_column.referenced_column_name = 'Id'
      AND reference_rule.delete_rule = 'RESTRICT';
    SELECT COUNT(*) INTO v_required_check_clauses
    FROM information_schema.table_constraints table_constraint
    INNER JOIN information_schema.check_constraints check_constraint
      ON check_constraint.constraint_schema = table_constraint.constraint_schema
     AND check_constraint.constraint_name = table_constraint.constraint_name
    WHERE table_constraint.table_schema = v_schema
      AND table_constraint.table_name = 'liens_LegacyUpdateEvents'
      AND table_constraint.constraint_type = 'CHECK'
      AND table_constraint.enforced = 'YES'
      AND ((table_constraint.constraint_name = 'CK_LegacyUpdateEvents_Scope'
            AND REGEXP_REPLACE(LOWER(REPLACE(REPLACE(REPLACE(
                    check_constraint.check_clause, '`', ''), '_utf8mb4', ''),
                    CONCAT(CHAR(92), CHAR(39)), CHAR(39))),
                    '[[:space:]()]', '') = 'scopein''case'',''lien''')
        OR (table_constraint.constraint_name = 'CK_LegacyUpdateEvents_ScopeLien'
            AND REGEXP_REPLACE(LOWER(REPLACE(REPLACE(REPLACE(
                    check_constraint.check_clause, '`', ''), '_utf8mb4', ''),
                    CONCAT(CHAR(92), CHAR(39)), CHAR(39))),
                    '[[:space:]()]', '') =
                'scope=''case''andlienidisnullorscope=''lien''andlienidisnotnull'));
    IF v_required_constraints <> 4 OR v_required_check_clauses <> 2 OR v_fk_contract <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-006 legacy update-event index or constraint contract is incompatible';
    END IF;

    SELECT COUNT(*) INTO v_provenance_count
    FROM `SL-CORE`.SL_MIGRATION_SOURCE_PROVENANCE
    WHERE PROVENANCE_KEY = v_mapping_version
      AND LOWER(SOURCE_FINGERPRINT) = v_source_fingerprint
      AND IMPORT_SCOPE = v_mapping_version
      AND TIMESTAMP_SEMANTICS = 'America/Los_Angeles-wall-clock';
    IF v_provenance_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-007 dedicated source provenance is missing or invalid';
    END IF;

    SELECT COUNT(DISTINCT crosswalk.ImportRunId) INTO v_core_run_count
    FROM liens_LegacyIdCrosswalks crosswalk
    INNER JOIN liens_LegacyImportRuns run ON run.Id = crosswalk.ImportRunId
    WHERE crosswalk.TenantId = v_tenant_id
      AND crosswalk.SourceSystem = v_source_system
      AND crosswalk.SourceTable IN ('SL_CASE', 'SL_LEINS_MEDICAL')
      AND run.TenantId = v_tenant_id
      AND run.OrgId = v_org_id
      AND run.SourceSystem = v_source_system
      AND run.SourceFingerprint = v_source_fingerprint
      AND run.LegacyProgram = '1'
      AND run.MappingVersion <> v_mapping_version
      AND run.Status = 'Completed';
    IF v_core_run_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-008 exactly one compatible completed Program 1 core import is required';
    END IF;

    SELECT COUNT(*) INTO v_running_run_count
    FROM liens_LegacyImportRuns
    WHERE TenantId = v_tenant_id
      AND OrgId = v_org_id
      AND SourceSystem = v_source_system
      AND SourceFingerprint = v_source_fingerprint
      AND LegacyProgram = '1'
      AND MappingVersion = v_mapping_version
      AND Status = 'Running';
    IF v_running_run_count <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-008 an unfinished update-history run requires reconciliation';
    END IF;

    SELECT COUNT(*),
           COALESCE(SUM(
               CASE
                   WHEN (
                       COALESCE(CONVERT_TZ(DATE_ADD(CN_CREATED, INTERVAL 7 HOUR), '+00:00', 'America/Los_Angeles') = CN_CREATED, 0)
                       + COALESCE(CONVERT_TZ(DATE_ADD(CN_CREATED, INTERVAL 8 HOUR), '+00:00', 'America/Los_Angeles') = CN_CREATED, 0)
                   ) <> 1 THEN 1
                   WHEN DATE_ADD(
                       CN_CREATED,
                       INTERVAL CASE
                           WHEN CONVERT_TZ(DATE_ADD(CN_CREATED, INTERVAL 7 HOUR), '+00:00', 'America/Los_Angeles') = CN_CREATED
                           THEN 7 ELSE 8 END HOUR
                   ) <> STR_TO_DATE(
                       LEFT(REGEXP_SUBSTR(CN_NOTE, '[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2} UTC'), 19),
                       '%Y-%m-%d %H:%i:%s') THEN 1
                   ELSE 0
               END), 0)
    INTO v_anchor_count, v_anchor_errors
    FROM `SL-CORE`.SL_CASE_NOTES
    WHERE CN_NOTE REGEXP '[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2} UTC';
    IF v_anchor_count <> 19 OR v_anchor_errors <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-009 embedded UTC anchors do not validate Pacific wall-clock semantics';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_luh_plan;
    DROP TEMPORARY TABLE IF EXISTS tmp_luh_foreign_keys;
    CREATE TEMPORARY TABLE tmp_luh_foreign_keys (
        SourceTable VARCHAR(100) NOT NULL,
        LegacyId VARCHAR(100) NOT NULL,
        PRIMARY KEY (SourceTable, LegacyId)
    ) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

    INSERT IGNORE INTO tmp_luh_foreign_keys (SourceTable, LegacyId)
    SELECT SourceTable, LegacyId
    FROM liens_LegacyIdCrosswalks
    WHERE TenantId <> v_tenant_id
      AND SourceSystem = v_source_system
      AND SourceTable IN ('SL_CASE', 'SL_LEINS_MEDICAL', 'SL_CASE_UPDATE_LOG', 'SL_LIENS_UPDATE_LOG')
      AND LegacyId IS NOT NULL;

    CREATE TEMPORARY TABLE tmp_luh_plan (
        SourceTable VARCHAR(100) NOT NULL,
        LegacySequence BIGINT NOT NULL,
        LegacyId VARCHAR(100) NOT NULL,
        ParentSourceTable VARCHAR(100) NOT NULL,
        ParentLegacyId VARCHAR(100) NOT NULL,
        SuppliedCaseLegacyId VARCHAR(100) NULL,
        CanonicalCaseLegacyId VARCHAR(100) NOT NULL,
        Scope VARCHAR(20) NOT NULL,
        Action VARCHAR(255) NOT NULL,
        Description TEXT NULL,
        ActorDisplayName VARCHAR(255) NULL,
        WallClock DATETIME(6) NULL,
        SourceHash VARCHAR(128) NOT NULL,
        Eligible TINYINT NOT NULL,
        OutOfScopeReason VARCHAR(100) NOT NULL,
        PacificCandidateCount TINYINT NULL,
        CanonicalCaseMappingValid TINYINT NULL,
        OccurredAtUtc DATETIME(6) NULL,
        TargetCaseId CHAR(36) COLLATE ascii_general_ci NULL,
        TargetLienId CHAR(36) COLLATE ascii_general_ci NULL,
        EventId CHAR(36) COLLATE ascii_general_ci NULL,
        Disposition VARCHAR(100) NULL,
        ErrorCode VARCHAR(100) NULL,
        Blocker VARCHAR(255) NULL,
        PRIMARY KEY (SourceTable, LegacySequence),
        KEY IX_tmp_luh_source_key (SourceTable, LegacyId),
        KEY IX_tmp_luh_parent (ParentSourceTable, ParentLegacyId),
        KEY IX_tmp_luh_canonical_case (CanonicalCaseLegacyId),
        KEY IX_tmp_luh_state (Eligible, Scope, Disposition, Blocker),
        KEY IX_tmp_luh_pending (Eligible, Disposition, Blocker, SourceTable, LegacyId)
    ) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

    INSERT INTO tmp_luh_plan (
        SourceTable, LegacySequence, LegacyId, ParentSourceTable, ParentLegacyId,
        SuppliedCaseLegacyId, CanonicalCaseLegacyId, Scope, Action,
        Description, ActorDisplayName, WallClock, SourceHash, Eligible,
        OutOfScopeReason, TargetCaseId, Disposition)
    SELECT
        'SL_CASE_UPDATE_LOG', u.CUL_ID, CAST(u.CUL_ID AS CHAR), 'SL_CASE', COALESCE(CAST(u.CUL_CASE_ID AS CHAR), ''),
        NULL, COALESCE(CAST(u.CUL_CASE_ID AS CHAR), ''), 'Case', COALESCE(u.CUL_ACTION, ''),
        u.CUL_DESCRIPTION, u.CUL_UPDATED_BY, u.CUL_TIMESTAMP,
        CONCAT('update-history-v2:', LOWER(SHA2(CONCAT_WS(CHAR(31),
            v_mapping_version, v_source_fingerprint, 'SL_CASE_UPDATE_LOG', CAST(u.CUL_ID AS CHAR),
            COALESCE(CAST(u.CUL_CASE_ID AS CHAR), ''),
            COALESCE(CAST(u.CUL_LIEN_ID AS CHAR), '<NULL>'),
            COALESCE(u.CUL_ACTION, ''), COALESCE(u.CUL_DESCRIPTION, '<NULL>'),
            COALESCE(u.CUL_UPDATED_BY, '<NULL>'),
            COALESCE(DATE_FORMAT(u.CUL_TIMESTAMP, '%Y-%m-%d %H:%i:%s.%f'), '<NULL>')), 256))),
        CASE WHEN c.CASE_PROGRAM = 1 AND UPPER(TRIM(COALESCE(c.CASE_IS_DELETED, ''))) <> 'Y' THEN 1 ELSE 0 END,
        CASE WHEN c.CASE_ID IS NULL OR c.CASE_PROGRAM IS NULL THEN 'MissingParent'
             WHEN c.CASE_PROGRAM <> 1 THEN 'OtherProgram'
             WHEN UPPER(TRIM(COALESCE(c.CASE_IS_DELETED, ''))) = 'Y' THEN 'DeletedParent'
             ELSE '' END,
        target_case.Id,
        CASE WHEN c.CASE_PROGRAM = 1 AND UPPER(TRIM(COALESCE(c.CASE_IS_DELETED, ''))) <> 'Y'
             THEN NULL
             ELSE CONCAT('OutOfScope:', CASE WHEN c.CASE_ID IS NULL OR c.CASE_PROGRAM IS NULL THEN 'MissingParent'
                  WHEN c.CASE_PROGRAM <> 1 THEN 'OtherProgram' ELSE 'DeletedParent' END) END
    FROM `SL-CORE`.SL_CASE_UPDATE_LOG u
    LEFT JOIN `SL-CORE`.SL_CASE c ON CAST(c.CASE_ID AS CHAR) = CAST(u.CUL_CASE_ID AS CHAR)
    LEFT JOIN liens_LegacyIdCrosswalks parent_walk
      ON parent_walk.TenantId = v_tenant_id
     AND parent_walk.SourceSystem = v_source_system
     AND parent_walk.SourceTable = 'SL_CASE'
     AND parent_walk.LegacyId = CAST(u.CUL_CASE_ID AS CHAR)
     AND parent_walk.TargetEntity = 'Case'
    LEFT JOIN liens_LegacyImportRuns parent_run
      ON parent_run.Id = parent_walk.ImportRunId
     AND parent_run.TenantId = v_tenant_id
     AND parent_run.OrgId = v_org_id
     AND parent_run.SourceSystem = v_source_system
     AND BINARY parent_run.SourceFingerprint = BINARY v_source_fingerprint
     AND parent_run.LegacyProgram = '1'
     AND parent_run.MappingVersion <> v_mapping_version
     AND parent_run.Status = 'Completed'
    LEFT JOIN liens_Cases target_case
      ON target_case.Id = parent_walk.TargetId
     AND target_case.TenantId = v_tenant_id
     AND target_case.OrgId = v_org_id
     AND parent_run.Id IS NOT NULL;

    INSERT INTO tmp_luh_plan (
        SourceTable, LegacySequence, LegacyId, ParentSourceTable, ParentLegacyId,
        SuppliedCaseLegacyId, CanonicalCaseLegacyId, Scope, Action,
        Description, ActorDisplayName, WallClock, SourceHash, Eligible,
        OutOfScopeReason, CanonicalCaseMappingValid, TargetCaseId, TargetLienId,
        Disposition)
    SELECT
        'SL_LIENS_UPDATE_LOG', u.LU_ID, CAST(u.LU_ID AS CHAR), 'SL_LEINS_MEDICAL', COALESCE(CAST(u.LU_LIEN_ID AS CHAR), ''),
        CAST(u.LU_CASE_ID AS CHAR), COALESCE(CAST(source_lien.LM_CASE_ID AS CHAR), ''), 'Lien',
        COALESCE(u.LU_ACTION, ''), u.LU_DESCRIPTION, u.LU_UPDATED_BY, u.LU_TIMESTAMP,
        CONCAT('update-history-v2:', LOWER(SHA2(CONCAT_WS(CHAR(31),
            v_mapping_version, v_source_fingerprint, 'SL_LIENS_UPDATE_LOG', CAST(u.LU_ID AS CHAR),
            COALESCE(CAST(u.LU_CASE_ID AS CHAR), '<NULL>'),
            COALESCE(CAST(u.LU_LIEN_ID AS CHAR), ''),
            COALESCE(u.LU_ACTION, ''), COALESCE(u.LU_DESCRIPTION, '<NULL>'),
            COALESCE(u.LU_UPDATED_BY, '<NULL>'),
            COALESCE(DATE_FORMAT(u.LU_TIMESTAMP, '%Y-%m-%d %H:%i:%s.%f'), '<NULL>')), 256))),
        CASE WHEN source_case.CASE_PROGRAM = 1
                  AND UPPER(TRIM(COALESCE(source_lien.LM_IS_DELETED, ''))) <> 'Y'
                  AND UPPER(TRIM(COALESCE(source_case.CASE_IS_DELETED, ''))) <> 'Y'
             THEN 1 ELSE 0 END,
        CASE WHEN source_lien.LM_ID IS NULL OR source_case.CASE_ID IS NULL OR source_case.CASE_PROGRAM IS NULL THEN 'MissingParent'
             WHEN source_case.CASE_PROGRAM <> 1 THEN 'OtherProgram'
             WHEN UPPER(TRIM(COALESCE(source_lien.LM_IS_DELETED, ''))) = 'Y'
               OR UPPER(TRIM(COALESCE(source_case.CASE_IS_DELETED, ''))) = 'Y' THEN 'DeletedParent'
             ELSE '' END,
        CASE WHEN foreign_case_walk.LegacyId IS NULL
                  AND case_walk.TargetEntity = 'Case'
                  AND TRIM(COALESCE(case_walk.SourceHash, '')) <> ''
                  AND case_run.Id IS NOT NULL
                  AND canonical_target_case.Id = target_lien.CaseId
             THEN 1 ELSE 0 END,
        target_lien.CaseId,
        target_lien.Id,
        CASE WHEN source_case.CASE_PROGRAM = 1
                  AND UPPER(TRIM(COALESCE(source_lien.LM_IS_DELETED, ''))) <> 'Y'
                  AND UPPER(TRIM(COALESCE(source_case.CASE_IS_DELETED, ''))) <> 'Y'
             THEN NULL
             ELSE CONCAT('OutOfScope:', CASE WHEN source_lien.LM_ID IS NULL OR source_case.CASE_ID IS NULL OR source_case.CASE_PROGRAM IS NULL THEN 'MissingParent'
                  WHEN source_case.CASE_PROGRAM <> 1 THEN 'OtherProgram' ELSE 'DeletedParent' END) END
    FROM `SL-CORE`.SL_LIENS_UPDATE_LOG u
    LEFT JOIN `SL-CORE`.SL_LEINS_MEDICAL source_lien
      ON source_lien.LM_ID = CAST(u.LU_LIEN_ID AS UNSIGNED)
     AND CAST(source_lien.LM_ID AS CHAR) = u.LU_LIEN_ID
    LEFT JOIN `SL-CORE`.SL_CASE source_case ON source_case.CASE_ID = source_lien.LM_CASE_ID
    LEFT JOIN liens_LegacyIdCrosswalks parent_walk
      ON parent_walk.TenantId = v_tenant_id
     AND parent_walk.SourceSystem = v_source_system
     AND parent_walk.SourceTable = 'SL_LEINS_MEDICAL'
     AND parent_walk.LegacyId = CAST(u.LU_LIEN_ID AS CHAR)
     AND parent_walk.TargetEntity = 'Lien'
    LEFT JOIN liens_LegacyImportRuns parent_run
      ON parent_run.Id = parent_walk.ImportRunId
     AND parent_run.TenantId = v_tenant_id
     AND parent_run.OrgId = v_org_id
     AND parent_run.SourceSystem = v_source_system
     AND BINARY parent_run.SourceFingerprint = BINARY v_source_fingerprint
     AND parent_run.LegacyProgram = '1'
     AND parent_run.MappingVersion <> v_mapping_version
     AND parent_run.Status = 'Completed'
    LEFT JOIN liens_Liens target_lien
      ON target_lien.Id = parent_walk.TargetId
     AND target_lien.TenantId = v_tenant_id
     AND target_lien.OrgId = v_org_id
     AND target_lien.CaseId IS NOT NULL
     AND parent_run.Id IS NOT NULL
    LEFT JOIN liens_LegacyIdCrosswalks case_walk
      ON case_walk.TenantId = v_tenant_id
     AND case_walk.SourceSystem = v_source_system
     AND case_walk.SourceTable = 'SL_CASE'
     AND case_walk.LegacyId = CAST(source_lien.LM_CASE_ID AS CHAR)
    LEFT JOIN liens_LegacyImportRuns case_run
      ON case_run.Id = case_walk.ImportRunId
     AND case_run.TenantId = v_tenant_id
     AND case_run.OrgId = v_org_id
     AND case_run.SourceSystem = v_source_system
     AND BINARY case_run.SourceFingerprint = BINARY v_source_fingerprint
     AND case_run.LegacyProgram = '1'
     AND case_run.MappingVersion <> v_mapping_version
     AND case_run.Status = 'Completed'
    LEFT JOIN liens_Cases canonical_target_case
      ON canonical_target_case.Id = case_walk.TargetId
     AND canonical_target_case.TenantId = v_tenant_id
     AND canonical_target_case.OrgId = v_org_id
    LEFT JOIN tmp_luh_foreign_keys foreign_case_walk
      ON foreign_case_walk.SourceTable = 'SL_CASE'
     AND foreign_case_walk.LegacyId = CAST(source_lien.LM_CASE_ID AS CHAR);

    UPDATE tmp_luh_plan
    SET PacificCandidateCount =
            COALESCE(CONVERT_TZ(DATE_ADD(WallClock, INTERVAL 7 HOUR), '+00:00', 'America/Los_Angeles') = WallClock, 0)
          + COALESCE(CONVERT_TZ(DATE_ADD(WallClock, INTERVAL 8 HOUR), '+00:00', 'America/Los_Angeles') = WallClock, 0),
        OccurredAtUtc = CASE
            WHEN CONVERT_TZ(DATE_ADD(WallClock, INTERVAL 7 HOUR), '+00:00', 'America/Los_Angeles') = WallClock
            THEN DATE_ADD(WallClock, INTERVAL 7 HOUR)
            ELSE DATE_ADD(WallClock, INTERVAL 8 HOUR) END
    WHERE Eligible = 1;

    SELECT COUNT(*) INTO v_timestamp_errors
    FROM tmp_luh_plan
    WHERE Eligible = 1 AND (WallClock IS NULL OR PacificCandidateCount <> 1);
    IF v_timestamp_errors <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-010 eligible source timestamps are missing, invalid, or ambiguous Pacific times';
    END IF;

    UPDATE tmp_luh_plan
    SET Blocker = 'MissingAction'
    WHERE Eligible = 1 AND TRIM(Action) = '';

    UPDATE tmp_luh_plan plan
    LEFT JOIN tmp_luh_foreign_keys foreign_parent
      ON foreign_parent.SourceTable = plan.ParentSourceTable
     AND foreign_parent.LegacyId = plan.ParentLegacyId
    SET plan.Blocker = 'CrossTenantCrosswalk'
    WHERE plan.Eligible = 1
      AND plan.Blocker IS NULL
      AND foreign_parent.LegacyId IS NOT NULL;

    UPDATE tmp_luh_plan plan
    LEFT JOIN tmp_luh_foreign_keys foreign_event
      ON foreign_event.SourceTable = plan.SourceTable
     AND foreign_event.LegacyId = plan.LegacyId
    SET plan.Blocker = 'CrossTenantCrosswalk'
    WHERE plan.Eligible = 1
      AND plan.Blocker IS NULL
      AND foreign_event.LegacyId IS NOT NULL;

    UPDATE tmp_luh_plan plan
    SET plan.Disposition = 'Excluded:MissingTargetCrosswalk',
        plan.ErrorCode = 'MISSING_TARGET_CROSSWALK'
    WHERE plan.Eligible = 1
      AND plan.Blocker IS NULL
      AND plan.Disposition IS NULL
      AND NOT EXISTS (
          SELECT 1 FROM liens_LegacyIdCrosswalks parent_walk
          WHERE parent_walk.TenantId = v_tenant_id
            AND parent_walk.SourceSystem = v_source_system
            AND parent_walk.SourceTable = plan.ParentSourceTable
            AND parent_walk.LegacyId = plan.ParentLegacyId
      );

    UPDATE tmp_luh_plan plan
    SET plan.Blocker = 'MalformedOrWrongParentCrosswalk'
    WHERE plan.Eligible = 1
      AND plan.Blocker IS NULL
      AND plan.Disposition IS NULL
      AND EXISTS (
          SELECT 1 FROM liens_LegacyIdCrosswalks parent_walk
          WHERE parent_walk.TenantId = v_tenant_id
            AND parent_walk.SourceSystem = v_source_system
            AND parent_walk.SourceTable = plan.ParentSourceTable
            AND parent_walk.LegacyId = plan.ParentLegacyId
            AND (parent_walk.TargetEntity <> plan.Scope OR TRIM(parent_walk.SourceHash) = '')
      );

    UPDATE tmp_luh_plan
    SET Blocker = 'InvalidTargetOwnership'
    WHERE Eligible = 1 AND Scope = 'Case'
      AND Blocker IS NULL AND Disposition IS NULL AND TargetCaseId IS NULL;

    UPDATE tmp_luh_plan
    SET Blocker = 'InvalidTargetOwnership'
    WHERE Eligible = 1 AND Scope = 'Lien'
      AND Blocker IS NULL AND Disposition IS NULL AND TargetLienId IS NULL;

    UPDATE tmp_luh_plan
    SET Disposition = 'Excluded:ApprovedCaseLienMismatch',
        ErrorCode = 'SOURCE_CASE_LIEN_MISMATCH'
    WHERE Eligible = 1 AND Scope = 'Lien'
      AND Blocker IS NULL AND Disposition IS NULL
      AND SuppliedCaseLegacyId IS NOT NULL AND TRIM(SuppliedCaseLegacyId) <> ''
      AND BINARY TRIM(SuppliedCaseLegacyId) <> BINARY CanonicalCaseLegacyId
      AND SourceTable = 'SL_LIENS_UPDATE_LOG' AND LegacySequence = 4891;

    UPDATE tmp_luh_plan
    SET Blocker = 'UnapprovedCaseLienMismatch'
    WHERE Eligible = 1 AND Scope = 'Lien'
      AND Blocker IS NULL AND Disposition IS NULL
      AND SuppliedCaseLegacyId IS NOT NULL AND TRIM(SuppliedCaseLegacyId) <> ''
      AND BINARY TRIM(SuppliedCaseLegacyId) <> BINARY CanonicalCaseLegacyId;

    UPDATE tmp_luh_plan
    SET Blocker = 'CanonicalCaseMismatch'
    WHERE Eligible = 1 AND Scope = 'Lien'
      AND Blocker IS NULL AND Disposition IS NULL
      AND CanonicalCaseMappingValid <> 1;

    UPDATE tmp_luh_plan plan
    INNER JOIN liens_LegacyIdCrosswalks event_walk
      ON event_walk.TenantId = v_tenant_id
     AND event_walk.SourceSystem = v_source_system
     AND event_walk.SourceTable = plan.SourceTable
     AND event_walk.LegacyId = plan.LegacyId
     AND event_walk.TargetEntity = 'LegacyUpdateEvent'
     AND BINARY event_walk.SourceHash = BINARY plan.SourceHash
    INNER JOIN liens_LegacyUpdateEvents update_event
      ON update_event.Id = event_walk.TargetId
     AND update_event.ImportRunId = event_walk.ImportRunId
     AND update_event.TenantId = v_tenant_id
     AND update_event.OrgId = v_org_id
     AND update_event.CaseId = plan.TargetCaseId
     AND update_event.LienId <=> plan.TargetLienId
     AND update_event.Scope = plan.Scope
     AND BINARY update_event.Action = BINARY plan.Action
     AND BINARY update_event.Description <=> BINARY plan.Description
     AND BINARY update_event.ActorDisplayName <=> BINARY plan.ActorDisplayName
     AND update_event.OccurredAtUtc = plan.OccurredAtUtc
     AND update_event.SourceSystem = v_source_system
     AND update_event.SourceTable = plan.SourceTable
     AND update_event.LegacyId = plan.LegacyId
     AND update_event.LegacySequence = plan.LegacySequence
    INNER JOIN liens_LegacyImportRuns import_run
      ON import_run.Id = update_event.ImportRunId
     AND import_run.TenantId = v_tenant_id
     AND import_run.OrgId = v_org_id
     AND import_run.SourceFingerprint = v_source_fingerprint
     AND import_run.LegacyProgram = '1'
     AND import_run.MappingVersion = v_mapping_version
     AND import_run.Status = 'Completed'
     AND import_run.CompletedAtUtc IS NOT NULL
     AND update_event.ImportedAtUtc = import_run.StartedAtUtc
    SET plan.Disposition = 'AlreadyImported',
        plan.EventId = event_walk.TargetId
    WHERE plan.Eligible = 1
      AND plan.Blocker IS NULL AND plan.Disposition IS NULL;

    SELECT COUNT(*) INTO v_pending_events
    FROM tmp_luh_plan
    WHERE Eligible = 1 AND Blocker IS NULL AND Disposition IS NULL;
    IF v_pending_events <> 0 THEN
        UPDATE tmp_luh_plan plan
        INNER JOIN liens_LegacyIdCrosswalks event_walk
          ON event_walk.TenantId = v_tenant_id
         AND event_walk.SourceSystem = v_source_system
         AND event_walk.SourceTable = plan.SourceTable
         AND event_walk.LegacyId = plan.LegacyId
        SET plan.Blocker = 'InvalidEventCrosswalk'
        WHERE plan.Eligible = 1
          AND plan.Blocker IS NULL AND plan.Disposition IS NULL;
    END IF;

    UPDATE tmp_luh_plan
    SET Disposition = 'Insert', EventId = LOWER(UUID())
    WHERE Eligible = 1 AND Blocker IS NULL AND Disposition IS NULL;

    SELECT COUNT(*) INTO v_blockers FROM tmp_luh_plan WHERE Blocker IS NOT NULL;
    IF v_blockers <> 0 THEN
        SELECT SourceTable, LegacySequence, Blocker
        FROM tmp_luh_plan WHERE Blocker IS NOT NULL
        ORDER BY SourceTable, LegacySequence LIMIT 100;
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-011 preflight contains blocking mappings or ownership violations';
    END IF;

    SELECT COUNT(*) INTO v_blank_lien_cases
    FROM tmp_luh_plan
    WHERE Eligible = 1 AND Scope = 'Lien'
      AND NOT (SourceTable = 'SL_LIENS_UPDATE_LOG' AND LegacySequence = 4891)
      AND (SuppliedCaseLegacyId IS NULL OR TRIM(SuppliedCaseLegacyId) = '');
    IF v_blank_lien_cases <> 1280 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-012 blank LU_CASE_ID total does not match the approved fingerprint';
    END IF;

    SELECT
        SUM(Eligible = 1 AND Scope = 'Case' AND Action = 'Case Details Update'),
        SUM(Eligible = 1 AND Scope = 'Case' AND Action = 'Case Created'),
        SUM(Eligible = 1 AND Scope = 'Case' AND Action = 'Personal Info Update'),
        COUNT(DISTINCT CASE WHEN Eligible = 1 AND Scope = 'Case' THEN Action END),
        SUM(Eligible = 1 AND Scope = 'Lien' AND LegacySequence <> 4891 AND Action = 'Create'),
        SUM(Eligible = 1 AND Scope = 'Lien' AND LegacySequence <> 4891 AND Action = 'Create Medical Payee'),
        SUM(Eligible = 1 AND Scope = 'Lien' AND LegacySequence <> 4891 AND Action = 'Update'),
        SUM(Eligible = 1 AND Scope = 'Lien' AND LegacySequence <> 4891 AND Action = 'Update Medical Code'),
        SUM(Eligible = 1 AND Scope = 'Lien' AND LegacySequence <> 4891 AND Action = 'Update Medical Information'),
        SUM(Eligible = 1 AND Scope = 'Lien' AND LegacySequence <> 4891 AND Action = 'Update Medical Payee'),
        COUNT(DISTINCT CASE WHEN Eligible = 1 AND Scope = 'Lien' AND LegacySequence <> 4891 THEN Action END)
    INTO v_case_details_updates, v_case_creations, v_case_personal_updates,
         v_case_action_count, v_lien_creations, v_lien_payee_creations,
         v_lien_updates, v_lien_medical_code_updates, v_lien_medical_info_updates,
         v_lien_payee_updates, v_lien_action_count
    FROM tmp_luh_plan;

    IF v_case_details_updates <> 1502
       OR v_case_creations <> 1186
       OR v_case_personal_updates <> 68
       OR v_case_action_count <> 3
       OR v_lien_creations <> 11157
       OR v_lien_payee_creations <> 2587
       OR v_lien_updates <> 1870
       OR v_lien_medical_code_updates <> 303
       OR v_lien_medical_info_updates <> 57
       OR v_lien_payee_updates <> 2
       OR v_lien_action_count <> 6 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-013 action totals do not match the approved fingerprint';
    END IF;

    SELECT COUNT(*) INTO v_approved_mismatches
    FROM tmp_luh_plan
    WHERE ErrorCode = 'SOURCE_CASE_LIEN_MISMATCH'
      AND SourceTable = 'SL_LIENS_UPDATE_LOG' AND LegacySequence = 4891;
    IF v_approved_mismatches <> 1
       OR (SELECT COUNT(*) FROM tmp_luh_plan WHERE ErrorCode = 'SOURCE_CASE_LIEN_MISMATCH') <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-014 approved anomaly set must contain only SL_LIENS_UPDATE_LOG:4891';
    END IF;

    SELECT
        SUM(Scope = 'Case' AND Disposition IN ('Insert', 'AlreadyImported')),
        SUM(Scope = 'Lien' AND Disposition IN ('Insert', 'AlreadyImported')),
        SUM(Scope = 'Case' AND Disposition = 'Insert'),
        SUM(Scope = 'Lien' AND Disposition = 'Insert'),
        SUM(Scope = 'Case' AND Disposition = 'AlreadyImported'),
        SUM(Scope = 'Lien' AND Disposition = 'AlreadyImported'),
        SUM(Eligible = 1 AND ErrorCode IS NOT NULL),
        SUM(Eligible = 0)
    INTO v_case_events, v_lien_events, v_case_inserts, v_lien_inserts,
         v_case_skips, v_lien_skips, v_excluded, v_out_of_scope
    FROM tmp_luh_plan;

    IF v_case_events <> 2756 OR v_lien_events <> 15976 OR v_excluded <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-014 imported and excluded totals do not match the approved fingerprint';
    END IF;

    SELECT LOWER(SHA2(GROUP_CONCAT(
        CONCAT(SourceTable, '|', LegacySequence, '|', SourceHash, '|',
            CASE WHEN Disposition IN ('Insert', 'AlreadyImported') THEN 'Imported' ELSE Disposition END,
            CHAR(10))
        ORDER BY BINARY SourceTable, LegacySequence SEPARATOR ''), 256))
    INTO v_checksum
    FROM tmp_luh_plan;

    SET v_approval_binding_hash = LOWER(SHA2(CONCAT_WS(CHAR(31),
        v_mapping_version,
        v_tenant_id,
        v_org_id,
        v_actor_id,
        v_source_fingerprint,
        CAST(v_case_events AS CHAR),
        CAST(v_lien_events AS CHAR),
        CAST(v_excluded AS CHAR),
        v_checksum,
        'SL_LIENS_UPDATE_LOG:4891'), 256));

    SELECT
        'DRY RUN' AS Mode,
        v_case_events AS CaseEvents,
        v_lien_events AS LienEvents,
        v_case_inserts AS CaseInserts,
        v_lien_inserts AS LienInserts,
        v_case_skips AS CaseAlreadyImported,
        v_lien_skips AS LienAlreadyImported,
        v_excluded AS ExcludedEligibleEvents,
        v_out_of_scope AS OutOfScopeEvents,
        v_checksum AS AggregateChecksum,
        v_approval_binding_hash AS ApprovalBindingHash;

    IF NOT v_apply THEN
        DROP TEMPORARY TABLE IF EXISTS tmp_luh_plan;
        DROP TEMPORARY TABLE IF EXISTS tmp_luh_foreign_keys;
        SET @@session.time_zone = v_original_time_zone;
        SET @@session.group_concat_max_len = v_original_group_concat_max_len;
        SET v_session_changed = FALSE;
        DO RELEASE_LOCK(v_lock_name);
        SET v_lock_acquired = 0;
        DO RELEASE_LOCK(v_core_lock_name);
        SET v_core_lock_acquired = 0;
        LEAVE main;
    END IF;

    IF p_approval_id IS NULL
       OR p_approval_id NOT REGEXP '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'
       OR p_expected_case_events <> v_case_events
       OR p_expected_lien_events <> v_lien_events
       OR p_expected_excluded_events <> v_excluded
       OR LOWER(COALESCE(p_expected_checksum, '')) <> v_checksum THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-015 approval ID and exact dry-run counts/checksum are required for apply';
    END IF;

    SELECT COUNT(*), MAX(MappingManifestHash), MAX(MappingApprovalReference)
    INTO v_approval_count, v_approval_manifest_hash, v_approval_reference
    FROM liens_LegacyImportApprovals
    WHERE Id = LOWER(TRIM(p_approval_id))
      AND TenantId = v_tenant_id
      AND OrgId = v_org_id
      AND SourceSystem = v_source_system
      AND SourceFingerprint = v_source_fingerprint
      AND LegacyProgram = '1'
      AND MappingVersion = v_mapping_version
      AND MigrationUserId = v_actor_id
      AND Status = 'Approved'
      AND ConsumedAtUtc IS NULL
      AND ConsumedByRunId IS NULL
      AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > UTC_TIMESTAMP(6));
    IF v_approval_count <> 1
       OR TRIM(COALESCE(v_approval_manifest_hash, '')) = ''
       OR BINARY v_approval_manifest_hash <> BINARY v_approval_binding_hash
       OR TRIM(COALESCE(v_approval_reference, '')) = '' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-016 compatible unconsumed update-history approval was not found';
    END IF;

    SELECT COUNT(*), MAX(Id),
           MAX(CAST(JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.caseEventsInserted')) AS UNSIGNED)
             + CAST(JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.lienEventsInserted')) AS UNSIGNED))
    INTO v_existing_run_count, v_existing_run_id, v_existing_inserted_events
    FROM liens_LegacyImportRuns
    WHERE TenantId = v_tenant_id
      AND OrgId = v_org_id
      AND SourceSystem = v_source_system
      AND SourceFingerprint = v_source_fingerprint
      AND LegacyProgram = '1'
      AND MappingVersion = v_mapping_version
      AND Status = 'Completed'
      AND JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.aggregateChecksum')) = v_checksum
      AND CAST(JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.caseEventsInserted')) AS UNSIGNED)
          + CAST(JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.caseEventsAlreadyImported')) AS UNSIGNED) = v_case_events
      AND CAST(JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.lienEventsInserted')) AS UNSIGNED)
          + CAST(JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.lienEventsAlreadyImported')) AS UNSIGNED) = v_lien_events
      AND CAST(JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.excluded')) AS UNSIGNED) = v_excluded;
    IF v_existing_run_count > 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-017 more than one identical completed run exists';
    END IF;
    IF v_existing_run_count = 1 THEN
        SELECT COUNT(*) INTO v_existing_event_count
        FROM liens_LegacyUpdateEvents
        WHERE ImportRunId = v_existing_run_id;

        SELECT COUNT(*) INTO v_existing_crosswalk_count
        FROM liens_LegacyIdCrosswalks
        WHERE ImportRunId = v_existing_run_id;

        SELECT COUNT(*) INTO v_existing_event_crosswalk_count
        FROM liens_LegacyIdCrosswalks
        WHERE ImportRunId = v_existing_run_id
          AND TargetEntity = 'LegacyUpdateEvent';

        SELECT COUNT(*) INTO v_existing_joined_count
        FROM liens_LegacyUpdateEvents update_event
        INNER JOIN liens_LegacyIdCrosswalks event_walk
          ON event_walk.TargetId = update_event.Id
         AND event_walk.ImportRunId = update_event.ImportRunId
         AND event_walk.TenantId = update_event.TenantId
         AND event_walk.SourceSystem = update_event.SourceSystem
         AND event_walk.SourceTable = update_event.SourceTable
         AND event_walk.LegacyId = update_event.LegacyId
         AND event_walk.TargetEntity = 'LegacyUpdateEvent'
        WHERE update_event.ImportRunId = v_existing_run_id;

        SELECT COUNT(*) INTO v_existing_planned_count
        FROM tmp_luh_plan plan
        INNER JOIN liens_LegacyUpdateEvents update_event
          ON update_event.Id = plan.EventId
         AND update_event.ImportRunId = v_existing_run_id
         AND update_event.TenantId = v_tenant_id
         AND update_event.OrgId = v_org_id
         AND update_event.CaseId = plan.TargetCaseId
         AND update_event.LienId <=> plan.TargetLienId
         AND update_event.Scope = plan.Scope
         AND BINARY update_event.Action = BINARY plan.Action
         AND BINARY update_event.Description <=> BINARY plan.Description
         AND BINARY update_event.ActorDisplayName <=> BINARY plan.ActorDisplayName
         AND update_event.OccurredAtUtc = plan.OccurredAtUtc
         AND update_event.SourceSystem = v_source_system
         AND update_event.SourceTable = plan.SourceTable
         AND update_event.LegacyId = plan.LegacyId
         AND update_event.LegacySequence = plan.LegacySequence
        INNER JOIN liens_LegacyIdCrosswalks event_walk
          ON event_walk.TargetId = update_event.Id
         AND event_walk.ImportRunId = v_existing_run_id
         AND event_walk.TenantId = v_tenant_id
         AND event_walk.SourceSystem = v_source_system
         AND event_walk.SourceTable = plan.SourceTable
         AND event_walk.LegacyId = plan.LegacyId
         AND event_walk.TargetEntity = 'LegacyUpdateEvent'
         AND BINARY event_walk.SourceHash = BINARY plan.SourceHash
        INNER JOIN liens_LegacyImportRuns import_run
          ON import_run.Id = v_existing_run_id
         AND update_event.ImportedAtUtc = import_run.StartedAtUtc
        WHERE plan.Disposition = 'AlreadyImported';

        SELECT COUNT(*) INTO v_existing_exception_count
        FROM liens_LegacyImportExceptions
        WHERE ImportRunId = v_existing_run_id;

        SELECT COUNT(*), COUNT(DISTINCT exception_row.SourceTable, exception_row.LegacyId)
        INTO v_existing_matching_exception_count, v_existing_matching_exception_keys
        FROM liens_LegacyImportExceptions exception_row
        INNER JOIN tmp_luh_plan plan
          ON BINARY plan.SourceTable = BINARY exception_row.SourceTable
         AND BINARY plan.LegacyId = BINARY exception_row.LegacyId
         AND BINARY plan.ErrorCode = BINARY exception_row.ErrorCode
         AND BINARY plan.SourceHash = BINARY exception_row.SourceHash
         AND plan.Eligible = 1
         AND plan.ErrorCode IS NOT NULL
        WHERE exception_row.ImportRunId = v_existing_run_id
          AND exception_row.TenantId = v_tenant_id
          AND BINARY exception_row.Severity = BINARY 'Warning'
          AND BINARY exception_row.Message = BINARY 'Legacy update event excluded by approved migration policy.';
        IF v_case_inserts + v_lien_inserts <> 0
           OR v_existing_inserted_events IS NULL
           OR v_existing_event_count <> v_existing_inserted_events
           OR v_existing_crosswalk_count <> v_existing_inserted_events
           OR v_existing_event_crosswalk_count <> v_existing_inserted_events
           OR v_existing_joined_count <> v_existing_inserted_events
           OR v_existing_planned_count <> v_existing_inserted_events
           OR v_existing_exception_count <> v_excluded
           OR v_existing_matching_exception_count <> v_excluded
           OR v_existing_matching_exception_keys <> v_excluded THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLUH-018 matching completed run has incomplete event or exception evidence';
        END IF;

        SELECT 'NO-OP' AS Mode, v_existing_run_id AS ImportRunId,
               v_case_events AS CaseEvents, v_lien_events AS LienEvents,
               v_excluded AS ExcludedEligibleEvents, v_checksum AS AggregateChecksum,
               v_approval_binding_hash AS ApprovalBindingHash;
        DROP TEMPORARY TABLE IF EXISTS tmp_luh_plan;
        DROP TEMPORARY TABLE IF EXISTS tmp_luh_foreign_keys;
        SET @@session.time_zone = v_original_time_zone;
        SET @@session.group_concat_max_len = v_original_group_concat_max_len;
        SET v_session_changed = FALSE;
        DO RELEASE_LOCK(v_lock_name);
        SET v_lock_acquired = 0;
        DO RELEASE_LOCK(v_core_lock_name);
        SET v_core_lock_acquired = 0;
        LEAVE main;
    END IF;

    SET v_run_id = LOWER(UUID());
    SET v_run_started_at = UTC_TIMESTAMP(6);
    INSERT INTO liens_LegacyImportRuns (
        Id, ApprovalId, TenantId, OrgId, SourceSystem, SourceFingerprint,
        LegacyProgram, MappingVersion, MappingManifestHash,
        MappingApprovalReference, Status, StartedAtUtc, CreatedByUserId)
    VALUES (
        v_run_id, LOWER(TRIM(p_approval_id)), v_tenant_id, v_org_id, v_source_system,
        v_source_fingerprint, '1', v_mapping_version, v_approval_manifest_hash,
        v_approval_reference, 'Running', v_run_started_at, v_actor_id);
    SET v_run_created = TRUE;
    COMMIT;

    START TRANSACTION;
    SET v_in_transaction = TRUE;

    SELECT COUNT(*) INTO v_reconciliation_errors
    FROM tmp_luh_plan plan
    LEFT JOIN liens_LegacyIdCrosswalks event_walk
      ON event_walk.TenantId = v_tenant_id
     AND event_walk.SourceSystem = v_source_system
     AND event_walk.SourceTable = plan.SourceTable
     AND event_walk.LegacyId = plan.LegacyId
    LEFT JOIN liens_LegacyIdCrosswalks parent_walk
      ON parent_walk.TenantId = v_tenant_id
     AND parent_walk.SourceSystem = v_source_system
     AND parent_walk.SourceTable = plan.ParentSourceTable
     AND parent_walk.LegacyId = plan.ParentLegacyId
     AND parent_walk.TargetEntity = plan.Scope
     AND parent_walk.TargetId = CASE WHEN plan.Scope = 'Case' THEN plan.TargetCaseId ELSE plan.TargetLienId END
     AND TRIM(COALESCE(parent_walk.SourceHash, '')) <> ''
    LEFT JOIN liens_LegacyImportRuns parent_run
      ON parent_run.Id = parent_walk.ImportRunId
     AND parent_run.TenantId = v_tenant_id
     AND parent_run.OrgId = v_org_id
     AND parent_run.SourceSystem = v_source_system
     AND BINARY parent_run.SourceFingerprint = BINARY v_source_fingerprint
     AND parent_run.LegacyProgram = '1'
     AND parent_run.MappingVersion <> v_mapping_version
     AND parent_run.Status = 'Completed'
    LEFT JOIN liens_Cases target_case
      ON plan.Scope = 'Case'
     AND target_case.Id = plan.TargetCaseId
     AND target_case.TenantId = v_tenant_id
     AND target_case.OrgId = v_org_id
    LEFT JOIN liens_Liens target_lien
      ON plan.Scope = 'Lien'
     AND target_lien.Id = plan.TargetLienId
     AND target_lien.TenantId = v_tenant_id
     AND target_lien.OrgId = v_org_id
     AND target_lien.CaseId = plan.TargetCaseId
    LEFT JOIN liens_LegacyIdCrosswalks canonical_case_walk
      ON plan.Scope = 'Lien'
     AND canonical_case_walk.TenantId = v_tenant_id
     AND canonical_case_walk.SourceSystem = v_source_system
     AND canonical_case_walk.SourceTable = 'SL_CASE'
     AND canonical_case_walk.LegacyId = plan.CanonicalCaseLegacyId
     AND canonical_case_walk.TargetEntity = 'Case'
     AND canonical_case_walk.TargetId = plan.TargetCaseId
     AND TRIM(COALESCE(canonical_case_walk.SourceHash, '')) <> ''
    LEFT JOIN liens_LegacyImportRuns canonical_case_run
      ON canonical_case_run.Id = canonical_case_walk.ImportRunId
     AND canonical_case_run.TenantId = v_tenant_id
     AND canonical_case_run.OrgId = v_org_id
     AND canonical_case_run.SourceSystem = v_source_system
     AND BINARY canonical_case_run.SourceFingerprint = BINARY v_source_fingerprint
     AND canonical_case_run.LegacyProgram = '1'
     AND canonical_case_run.MappingVersion <> v_mapping_version
     AND canonical_case_run.Status = 'Completed'
    LEFT JOIN liens_Cases canonical_target_case
      ON plan.Scope = 'Lien'
     AND canonical_target_case.Id = canonical_case_walk.TargetId
     AND canonical_target_case.TenantId = v_tenant_id
     AND canonical_target_case.OrgId = v_org_id
    WHERE plan.Disposition = 'Insert'
      AND (event_walk.Id IS NOT NULL
        OR parent_walk.Id IS NULL
        OR parent_run.Id IS NULL
        OR (plan.Scope = 'Case' AND target_case.Id IS NULL)
        OR (plan.Scope = 'Lien' AND (target_lien.Id IS NULL
          OR canonical_case_walk.Id IS NULL
          OR canonical_case_run.Id IS NULL
          OR canonical_target_case.Id IS NULL)));

    SELECT v_reconciliation_errors + COUNT(*) INTO v_reconciliation_errors
    FROM tmp_luh_plan plan
    INNER JOIN tmp_luh_foreign_keys foreign_event
      ON foreign_event.SourceTable = plan.SourceTable
     AND foreign_event.LegacyId = plan.LegacyId
    WHERE plan.Disposition = 'Insert';

    SELECT v_reconciliation_errors + COUNT(*) INTO v_reconciliation_errors
    FROM tmp_luh_plan plan
    INNER JOIN tmp_luh_foreign_keys foreign_parent
      ON foreign_parent.SourceTable = plan.ParentSourceTable
     AND foreign_parent.LegacyId = plan.ParentLegacyId
    WHERE plan.Disposition = 'Insert';

    SELECT v_reconciliation_errors + COUNT(*) INTO v_reconciliation_errors
    FROM tmp_luh_plan plan
    INNER JOIN tmp_luh_foreign_keys foreign_case
      ON foreign_case.SourceTable = 'SL_CASE'
     AND foreign_case.LegacyId = plan.CanonicalCaseLegacyId
    WHERE plan.Disposition = 'Insert'
      AND plan.Scope = 'Lien';
    IF v_reconciliation_errors <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-019 ownership or crosswalk state changed after preflight';
    END IF;

    UPDATE liens_LegacyImportApprovals
    SET Status = 'Consumed', ConsumedAtUtc = v_run_started_at, ConsumedByRunId = v_run_id
    WHERE Id = LOWER(TRIM(p_approval_id))
      AND TenantId = v_tenant_id
      AND OrgId = v_org_id
      AND SourceSystem = v_source_system
      AND BINARY SourceFingerprint = BINARY v_source_fingerprint
      AND LegacyProgram = '1'
      AND MappingVersion = v_mapping_version
      AND MigrationUserId = v_actor_id
      AND BINARY MappingManifestHash = BINARY v_approval_binding_hash
      AND BINARY MappingApprovalReference = BINARY v_approval_reference
      AND Status = 'Approved'
      AND ConsumedAtUtc IS NULL
      AND ConsumedByRunId IS NULL
      AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > UTC_TIMESTAMP(6));
    IF ROW_COUNT() <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-020 approval was consumed concurrently';
    END IF;

    INSERT INTO liens_LegacyUpdateEvents (
        Id, TenantId, OrgId, CaseId, LienId, Scope, Action, Description,
        ActorDisplayName, OccurredAtUtc, ImportedAtUtc, ImportRunId,
        SourceSystem, SourceTable, LegacyId, LegacySequence)
    SELECT EventId, v_tenant_id, v_org_id, TargetCaseId, TargetLienId, Scope,
           Action, Description, ActorDisplayName, OccurredAtUtc, v_run_started_at,
           v_run_id, v_source_system, SourceTable, LegacyId, LegacySequence
    FROM tmp_luh_plan WHERE Disposition = 'Insert';
    SET v_inserted_events = ROW_COUNT();

    INSERT INTO liens_LegacyIdCrosswalks (
        Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity,
        TargetId, SourceHash, ImportRunId, CreatedAtUtc)
    SELECT LOWER(UUID()), v_tenant_id, v_source_system, SourceTable,
           LegacyId, 'LegacyUpdateEvent', EventId,
           SourceHash, v_run_id, v_run_started_at
    FROM tmp_luh_plan WHERE Disposition = 'Insert';
    SET v_inserted_crosswalks = ROW_COUNT();

    INSERT INTO liens_LegacyImportExceptions (
        Id, TenantId, ImportRunId, SourceTable, LegacyId, Severity,
        ErrorCode, Message, SourceHash, CreatedAtUtc)
    SELECT LOWER(UUID()), v_tenant_id, v_run_id, SourceTable,
           LegacyId, 'Warning', ErrorCode,
           'Legacy update event excluded by approved migration policy.',
           SourceHash, v_run_started_at
    FROM tmp_luh_plan WHERE Eligible = 1 AND ErrorCode IS NOT NULL;
    SET v_inserted_exceptions = ROW_COUNT();

    SELECT COUNT(*) INTO v_reconciliation_errors
    FROM tmp_luh_plan plan
    LEFT JOIN liens_LegacyUpdateEvents update_event
      ON update_event.Id = plan.EventId
     AND update_event.ImportRunId = v_run_id
     AND update_event.TenantId = v_tenant_id
     AND update_event.OrgId = v_org_id
     AND update_event.CaseId = plan.TargetCaseId
     AND update_event.LienId <=> plan.TargetLienId
     AND update_event.Scope = plan.Scope
     AND BINARY update_event.Action = BINARY plan.Action
     AND BINARY update_event.Description <=> BINARY plan.Description
     AND BINARY update_event.ActorDisplayName <=> BINARY plan.ActorDisplayName
     AND update_event.OccurredAtUtc = plan.OccurredAtUtc
     AND update_event.ImportedAtUtc = v_run_started_at
     AND update_event.SourceSystem = v_source_system
     AND update_event.SourceTable = plan.SourceTable
     AND update_event.LegacyId = plan.LegacyId
     AND update_event.LegacySequence = plan.LegacySequence
    LEFT JOIN liens_LegacyIdCrosswalks event_walk
      ON event_walk.TargetId = update_event.Id
     AND event_walk.ImportRunId = update_event.ImportRunId
     AND event_walk.TenantId = update_event.TenantId
     AND event_walk.SourceSystem = update_event.SourceSystem
     AND event_walk.SourceTable = update_event.SourceTable
     AND event_walk.LegacyId = update_event.LegacyId
     AND event_walk.TargetEntity = 'LegacyUpdateEvent'
     AND event_walk.SourceHash = plan.SourceHash
    WHERE plan.Disposition = 'Insert'
      AND (update_event.Id IS NULL OR event_walk.Id IS NULL);

    IF v_inserted_events <> v_case_inserts + v_lien_inserts
       OR v_inserted_crosswalks <> v_inserted_events
       OR v_inserted_exceptions <> v_excluded
       OR v_reconciliation_errors <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-021 event, crosswalk, or exception reconciliation failed';
    END IF;

    UPDATE liens_LegacyImportRuns
    SET Status = 'Completed',
        CompletedAtUtc = UTC_TIMESTAMP(6),
        SummaryJson = JSON_OBJECT(
            'importScope', v_mapping_version,
            'caseEventsInserted', v_case_inserts,
            'lienEventsInserted', v_lien_inserts,
            'caseEventsAlreadyImported', v_case_skips,
            'lienEventsAlreadyImported', v_lien_skips,
            'excluded', v_excluded,
            'outOfScope', v_out_of_scope,
            'aggregateChecksum', v_checksum)
    WHERE Id = v_run_id AND Status = 'Running';
    IF ROW_COUNT() <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-022 import run could not be completed';
    END IF;

    COMMIT;
    SET v_in_transaction = FALSE;
    SET v_run_created = FALSE;

    SELECT 'APPLIED' AS Mode, v_run_id AS ImportRunId,
           v_case_inserts AS CaseEventsInserted,
           v_lien_inserts AS LienEventsInserted,
           v_case_skips AS CaseEventsAlreadyImported,
           v_lien_skips AS LienEventsAlreadyImported,
           v_excluded AS ExcludedEligibleEvents,
           v_checksum AS AggregateChecksum,
           v_approval_binding_hash AS ApprovalBindingHash;

    DROP TEMPORARY TABLE IF EXISTS tmp_luh_plan;
    DROP TEMPORARY TABLE IF EXISTS tmp_luh_foreign_keys;
    SET @@session.time_zone = v_original_time_zone;
    SET @@session.group_concat_max_len = v_original_group_concat_max_len;
    SET v_session_changed = FALSE;
    DO RELEASE_LOCK(v_lock_name);
    SET v_lock_acquired = 0;
    DO RELEASE_LOCK(v_core_lock_name);
    SET v_core_lock_acquired = 0;
END$$

DELIMITER ;

-- Leave the procedure installed for the separately approved dry-run/apply
-- sequence. Drop it after reconciliation if the database execution policy
-- requires one-time operational procedures to be removed.
