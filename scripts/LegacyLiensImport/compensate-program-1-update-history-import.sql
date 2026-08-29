-- Guarded pre-exposure compensation for one Program 1 update-history import run.
--
-- This script removes only LegacyUpdateEvent crosswalks and events owned by the
-- specified run. It retains the import run and exceptions, then marks the run
-- RolledBack with non-sensitive rollback evidence in SummaryJson.
--
-- Do not use after imported history has been exposed to users. Disable the
-- LegacyUpdateHistory read path, run with @apply = 0, review the preflight,
-- copy its counts/checksum into the expected variables, obtain approval, set
-- both confirmations and @apply to 1, then execute the complete file again.
--
-- Error/reference prefix: LSLUH-RB-

SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;

SET @apply = 0;
SET @confirm_reads_disabled = 0;
SET @confirm_pre_exposure = 0;
SET @import_run_id = '<legacy-update-history-import-run-guid>';
SET @tenant_id = '<tenant-guid>';
SET @org_id = '<organization-guid>';
SET @rollback_actor_user_id = '<identity-user-guid>';
SET @expected_case_events = -1;
SET @expected_lien_events = -1;
SET @expected_crosswalks = -1;
SET @expected_checksum = NULL;

SET @expected_source_fingerprint =
    '3adccecf8a38114a14cd500240aab2a4db3d9bf45f00945c659dc3b5252663fe';
SET @expected_mapping_version = 'sl-core-update-history-v2';

DROP PROCEDURE IF EXISTS compensate_program_1_update_history_import;

DELIMITER $$

CREATE PROCEDURE compensate_program_1_update_history_import(
    IN p_apply TINYINT,
    IN p_confirm_reads_disabled TINYINT,
    IN p_confirm_pre_exposure TINYINT,
    IN p_import_run_id CHAR(36),
    IN p_tenant_id CHAR(36),
    IN p_org_id CHAR(36),
    IN p_rollback_actor_user_id CHAR(36),
    IN p_expected_case_events INT,
    IN p_expected_lien_events INT,
    IN p_expected_crosswalks INT,
    IN p_expected_checksum CHAR(64),
    IN p_expected_source_fingerprint CHAR(64),
    IN p_expected_mapping_version VARCHAR(100)
)
main: BEGIN
    DECLARE v_schema VARCHAR(64);
    DECLARE v_lock_name VARCHAR(255);
    DECLARE v_lock_acquired INT DEFAULT 0;
    DECLARE v_required_tables INT DEFAULT 0;
    DECLARE v_run_count INT DEFAULT 0;
    DECLARE v_case_events INT DEFAULT 0;
    DECLARE v_lien_events INT DEFAULT 0;
    DECLARE v_crosswalks INT DEFAULT 0;
    DECLARE v_crosswalk_errors INT DEFAULT 0;
    DECLARE v_deleted_crosswalks INT DEFAULT 0;
    DECLARE v_deleted_events INT DEFAULT 0;
    DECLARE v_updated_runs INT DEFAULT 0;
    DECLARE v_checksum CHAR(64);
    DECLARE v_original_time_zone VARCHAR(64);
    DECLARE v_original_group_concat_max_len BIGINT;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SET @@session.time_zone = v_original_time_zone;
        SET @@session.group_concat_max_len = v_original_group_concat_max_len;
        IF v_lock_acquired = 1 THEN
            DO RELEASE_LOCK(v_lock_name);
        END IF;
        RESIGNAL;
    END;

    SET v_schema = DATABASE();
    SET v_original_time_zone = @@session.time_zone;
    SET v_original_group_concat_max_len = @@session.group_concat_max_len;
    SET @@session.time_zone = '+00:00';
    SET @@session.group_concat_max_len = 4194304;

    IF v_schema NOT IN ('LS_QA_LIENS', 'LS_LIENS') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-RB-001 target schema must be LS_QA_LIENS or LS_LIENS';
    END IF;

    IF p_import_run_id IS NULL
       OR p_import_run_id = '<legacy-update-history-import-run-guid>'
       OR p_tenant_id IS NULL
       OR p_tenant_id = '<tenant-guid>'
       OR p_org_id IS NULL
       OR p_org_id = '<organization-guid>'
       OR p_rollback_actor_user_id IS NULL
       OR p_rollback_actor_user_id = '<identity-user-guid>'
       OR UNHEX(REPLACE(p_import_run_id, '-', '')) IS NULL
       OR UNHEX(REPLACE(p_tenant_id, '-', '')) IS NULL
       OR UNHEX(REPLACE(p_org_id, '-', '')) IS NULL
       OR UNHEX(REPLACE(p_rollback_actor_user_id, '-', '')) IS NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-RB-002 explicit run, tenant, organization, and actor GUIDs are required';
    END IF;

    SET v_lock_name = CONCAT('legalsynq:luh:', LOWER(p_tenant_id));
    SELECT GET_LOCK(v_lock_name, 0) INTO v_lock_acquired;
    IF COALESCE(v_lock_acquired, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-RB-003 a legacy update-history import or compensation is already active';
    END IF;

    SELECT COUNT(*) INTO v_required_tables
    FROM information_schema.tables
    WHERE table_schema = v_schema
      AND table_type = 'BASE TABLE'
      AND table_name IN (
          'liens_LegacyImportRuns',
          'liens_LegacyImportExceptions',
          'liens_LegacyIdCrosswalks',
          'liens_LegacyUpdateEvents'
      );

    IF v_required_tables <> 4 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-RB-004 required legacy-import tables are unavailable';
    END IF;

    SELECT COUNT(*) INTO v_run_count
    FROM liens_LegacyImportRuns run
    WHERE BINARY run.Id = BINARY p_import_run_id
      AND BINARY run.TenantId = BINARY p_tenant_id
      AND BINARY run.OrgId = BINARY p_org_id
      AND run.SourceSystem = 'SL-CORE'
      AND run.LegacyProgram = '1'
      AND BINARY run.SourceFingerprint = BINARY p_expected_source_fingerprint
      AND BINARY run.MappingVersion = BINARY p_expected_mapping_version
      AND run.Status = 'Completed'
      AND (run.SummaryJson IS NULL OR JSON_VALID(run.SummaryJson) = 1);

    IF v_run_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-RB-005 completed fingerprint-bound update-history run was not found';
    END IF;

    SELECT COUNT(*) INTO v_case_events
    FROM liens_LegacyUpdateEvents update_event
    WHERE BINARY update_event.ImportRunId = BINARY p_import_run_id
      AND BINARY update_event.TenantId = BINARY p_tenant_id
      AND BINARY update_event.OrgId = BINARY p_org_id
      AND update_event.SourceSystem = 'SL-CORE'
      AND update_event.SourceTable = 'SL_CASE_UPDATE_LOG'
      AND update_event.Scope = 'Case'
      AND update_event.LienId IS NULL;

    SELECT COUNT(*) INTO v_lien_events
    FROM liens_LegacyUpdateEvents update_event
    WHERE BINARY update_event.ImportRunId = BINARY p_import_run_id
      AND BINARY update_event.TenantId = BINARY p_tenant_id
      AND BINARY update_event.OrgId = BINARY p_org_id
      AND update_event.SourceSystem = 'SL-CORE'
      AND update_event.SourceTable = 'SL_LIENS_UPDATE_LOG'
      AND update_event.Scope = 'Lien'
      AND update_event.LienId IS NOT NULL;

    IF v_case_events + v_lien_events <> (
        SELECT COUNT(*)
        FROM liens_LegacyUpdateEvents update_event
        WHERE BINARY update_event.ImportRunId = BINARY p_import_run_id
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-RB-006 run owns unexpected or cross-tenant update events';
    END IF;

    SELECT COUNT(*) INTO v_crosswalks
    FROM liens_LegacyIdCrosswalks crosswalk
    WHERE BINARY crosswalk.ImportRunId = BINARY p_import_run_id
      AND BINARY crosswalk.TenantId = BINARY p_tenant_id
      AND crosswalk.SourceSystem = 'SL-CORE'
      AND crosswalk.TargetEntity = 'LegacyUpdateEvent';

    SELECT COUNT(*) INTO v_crosswalk_errors
    FROM liens_LegacyIdCrosswalks crosswalk
    LEFT JOIN liens_LegacyUpdateEvents update_event
      ON BINARY update_event.Id = BINARY crosswalk.TargetId
     AND BINARY update_event.ImportRunId = BINARY crosswalk.ImportRunId
     AND BINARY update_event.TenantId = BINARY crosswalk.TenantId
     AND BINARY update_event.SourceSystem = BINARY crosswalk.SourceSystem
     AND BINARY update_event.SourceTable = BINARY crosswalk.SourceTable
     AND BINARY update_event.LegacyId = BINARY crosswalk.LegacyId
    WHERE BINARY crosswalk.ImportRunId = BINARY p_import_run_id
      AND crosswalk.TargetEntity = 'LegacyUpdateEvent'
      AND update_event.Id IS NULL;

    IF v_crosswalk_errors <> 0 OR v_crosswalks <> v_case_events + v_lien_events THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-RB-007 event crosswalk ownership or cardinality is invalid';
    END IF;

    SELECT SHA2(GROUP_CONCAT(row_hash ORDER BY SourceTable, LegacySequence, LegacyId SEPARATOR ''), 256)
    INTO v_checksum
    FROM (
        SELECT
            update_event.SourceTable,
            update_event.LegacySequence,
            update_event.LegacyId,
            SHA2(CONCAT_WS(CHAR(31),
                LOWER(update_event.Id),
                LOWER(update_event.TenantId),
                LOWER(update_event.OrgId),
                LOWER(update_event.CaseId),
                COALESCE(LOWER(update_event.LienId), '<null>'),
                update_event.Scope,
                update_event.Action,
                COALESCE(SHA2(update_event.Description, 256), '<null>'),
                COALESCE(SHA2(update_event.ActorDisplayName, 256), '<null>'),
                DATE_FORMAT(update_event.OccurredAtUtc, '%Y-%m-%dT%H:%i:%s.%fZ'),
                DATE_FORMAT(update_event.ImportedAtUtc, '%Y-%m-%dT%H:%i:%s.%fZ'),
                LOWER(update_event.ImportRunId),
                update_event.SourceSystem,
                update_event.SourceTable,
                update_event.LegacyId,
                update_event.LegacySequence
            ), 256) AS row_hash
        FROM liens_LegacyUpdateEvents update_event
        WHERE BINARY update_event.ImportRunId = BINARY p_import_run_id
    ) checksummed_events;

    SELECT
        v_schema AS TargetSchema,
        p_import_run_id AS ImportRunId,
        v_case_events AS CaseEventsToDelete,
        v_lien_events AS LienEventsToDelete,
        v_crosswalks AS CrosswalksToDelete,
        v_checksum AS PlanChecksum,
        (SELECT COUNT(*) FROM liens_LegacyImportExceptions exception_row
         WHERE BINARY exception_row.ImportRunId = BINARY p_import_run_id) AS ExceptionsRetained,
        'Import run retained and marked RolledBack' AS RunDisposition;

    IF p_apply <> 1 THEN
        SET @@session.time_zone = v_original_time_zone;
        SET @@session.group_concat_max_len = v_original_group_concat_max_len;
        DO RELEASE_LOCK(v_lock_name);
        LEAVE main;
    END IF;

    IF p_confirm_reads_disabled <> 1 OR p_confirm_pre_exposure <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-RB-008 reads-disabled and pre-exposure confirmations are required';
    END IF;

    IF p_expected_case_events < 0
       OR p_expected_lien_events < 0
       OR p_expected_crosswalks < 0
       OR p_expected_checksum IS NULL
       OR CHAR_LENGTH(p_expected_checksum) <> 64
       OR p_expected_case_events <> v_case_events
       OR p_expected_lien_events <> v_lien_events
       OR p_expected_crosswalks <> v_crosswalks
       OR BINARY LOWER(p_expected_checksum) <> BINARY LOWER(v_checksum) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-RB-009 expected counts or checksum do not match the reviewed preflight';
    END IF;

    START TRANSACTION;

    DELETE FROM liens_LegacyIdCrosswalks
    WHERE BINARY ImportRunId = BINARY p_import_run_id
      AND BINARY TenantId = BINARY p_tenant_id
      AND SourceSystem = 'SL-CORE'
      AND TargetEntity = 'LegacyUpdateEvent';
    SET v_deleted_crosswalks = ROW_COUNT();

    DELETE FROM liens_LegacyUpdateEvents
    WHERE BINARY ImportRunId = BINARY p_import_run_id
      AND BINARY TenantId = BINARY p_tenant_id
      AND BINARY OrgId = BINARY p_org_id;
    SET v_deleted_events = ROW_COUNT();

    UPDATE liens_LegacyImportRuns
    SET Status = 'RolledBack',
        SummaryJson = JSON_SET(
            COALESCE(NULLIF(SummaryJson, ''), JSON_OBJECT()),
            '$.rollback', JSON_OBJECT(
                'rolledBackAtUtc', DATE_FORMAT(UTC_TIMESTAMP(6), '%Y-%m-%dT%H:%i:%s.%fZ'),
                'rolledBackByUserId', LOWER(p_rollback_actor_user_id),
                'eventsDeleted', v_deleted_events,
                'crosswalksDeleted', v_deleted_crosswalks,
                'preflightChecksum', LOWER(v_checksum),
                'exceptionsRetained', TRUE
            )
        )
    WHERE BINARY Id = BINARY p_import_run_id
      AND BINARY TenantId = BINARY p_tenant_id
      AND BINARY OrgId = BINARY p_org_id
      AND Status = 'Completed';
    SET v_updated_runs = ROW_COUNT();

    IF v_deleted_events <> v_case_events + v_lien_events
       OR v_deleted_crosswalks <> v_crosswalks
       OR v_updated_runs <> 1
       OR EXISTS (
           SELECT 1 FROM liens_LegacyUpdateEvents
           WHERE BINARY ImportRunId = BINARY p_import_run_id
       )
       OR EXISTS (
           SELECT 1 FROM liens_LegacyIdCrosswalks
           WHERE BINARY ImportRunId = BINARY p_import_run_id
             AND TargetEntity = 'LegacyUpdateEvent'
       ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLUH-RB-010 compensation postcondition failed';
    END IF;

    COMMIT;

    SET @@session.time_zone = v_original_time_zone;
    SET @@session.group_concat_max_len = v_original_group_concat_max_len;
    DO RELEASE_LOCK(v_lock_name);

    SELECT
        'RolledBack' AS Status,
        v_deleted_events AS EventsDeleted,
        v_deleted_crosswalks AS CrosswalksDeleted,
        'Import run and exceptions retained' AS EvidenceDisposition;
END$$

DELIMITER ;

CALL compensate_program_1_update_history_import(
    @apply,
    @confirm_reads_disabled,
    @confirm_pre_exposure,
    @import_run_id,
    @tenant_id,
    @org_id,
    @rollback_actor_user_id,
    @expected_case_events,
    @expected_lien_events,
    @expected_crosswalks,
    @expected_checksum,
    @expected_source_fingerprint,
    @expected_mapping_version
);

DROP PROCEDURE IF EXISTS compensate_program_1_update_history_import;
