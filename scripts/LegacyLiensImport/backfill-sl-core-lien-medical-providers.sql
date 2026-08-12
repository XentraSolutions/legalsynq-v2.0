-- Backfill Program 1 medical providers at their actual relationship level:
-- one LegacyMedicalFacilityInfo servicing item per imported medical lien.
-- A case may legitimately have multiple providers across its separate liens.

DROP PROCEDURE IF EXISTS liens_backfill_sl_core_case_medical_providers;
DROP PROCEDURE IF EXISTS liens_backfill_sl_core_lien_medical_providers;

DELIMITER $$

CREATE PROCEDURE liens_backfill_sl_core_lien_medical_providers(
    IN p_tenant_id CHAR(36),
    IN p_expected_changes INT,
    IN p_apply CHAR(1)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id CHAR(36);
    DECLARE v_apply BOOLEAN;
    DECLARE v_core_lock VARCHAR(64);
    DECLARE v_contact_lock VARCHAR(64);
    DECLARE v_core_locked INT DEFAULT 0;
    DECLARE v_contact_locked INT DEFAULT 0;
    DECLARE v_in_transaction BOOLEAN DEFAULT FALSE;
    DECLARE v_core_run_id CHAR(36);
    DECLARE v_contact_run_id CHAR(36);
    DECLARE v_org_id CHAR(36);
    DECLARE v_contact_org_id CHAR(36);
    DECLARE v_user_id CHAR(36);
    DECLARE v_fingerprint CHAR(64);
    DECLARE v_table_count INT DEFAULT 0;
    DECLARE v_core_runs INT DEFAULT 0;
    DECLARE v_contact_runs INT DEFAULT 0;
    DECLARE v_provenance_count INT DEFAULT 0;
    DECLARE v_source_count INT DEFAULT 0;
    DECLARE v_inserts INT DEFAULT 0;
    DECLARE v_updates INT DEFAULT 0;
    DECLARE v_conflicts INT DEFAULT 0;
    DECLARE v_applied_inserts INT DEFAULT 0;
    DECLARE v_applied_updates INT DEFAULT 0;
    DECLARE v_postcondition_errors INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        IF v_in_transaction THEN ROLLBACK; END IF;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_lien_provider_repair;
        IF v_contact_locked = 1 THEN DO RELEASE_LOCK(v_contact_lock); END IF;
        IF v_core_locked = 1 THEN DO RELEASE_LOCK(v_core_lock); END IF;
        RESIGNAL;
    END;

    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    SET v_apply = p_apply = '1';
    SET v_core_lock = CONCAT('liens:slcore:', v_tenant_id);
    SET v_contact_lock = CONCAT('liens:slcore:contacts:', v_tenant_id);

    IF DATABASE() <> 'LS_LIENS' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTP-001 target schema must be LS_LIENS';
    END IF;
    IF v_tenant_id IS NULL
       OR v_tenant_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR p_apply IS NULL OR p_apply NOT IN ('0', '1') OR p_expected_changes IS NULL
       OR (NOT v_apply AND p_expected_changes <> -1) OR (v_apply AND p_expected_changes < 0) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTP-002 invalid tenant ID, expected change count, or apply flag';
    END IF;

    SELECT GET_LOCK(v_core_lock, 10) INTO v_core_locked;
    IF COALESCE(v_core_locked, 0) <> 1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTP-003 core migration or repair is already active'; END IF;
    SELECT GET_LOCK(v_contact_lock, 10) INTO v_contact_locked;
    IF COALESCE(v_contact_locked, 0) <> 1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTP-004 contact migration or repair is already active'; END IF;

    SELECT COUNT(*) INTO v_table_count
    FROM information_schema.tables
    WHERE (table_schema = DATABASE() AND table_type = 'BASE TABLE'
           AND table_name IN ('liens_Liens','liens_Contacts','liens_ServicingItems','liens_LegacyIdCrosswalks','liens_LegacyImportRuns'))
       OR (table_schema = 'SL-CORE' AND table_type = 'BASE TABLE'
           AND table_name IN ('SL_LEINS_MEDICAL','SL_LEINS_MEDICAL_INFORMATION_FACILITY','SL_CONTACT','SL_MIGRATION_SOURCE_PROVENANCE'));
    IF v_table_count <> 9 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTP-005 required source or target tables are unavailable'; END IF;

    SELECT COUNT(*) INTO v_core_runs
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id AND r.SourceSystem = 'SL-CORE' AND r.LegacyProgram = '1'
      AND r.MappingVersion = 'sl-core-core-liens-v1' AND r.Status = 'Completed'
      AND EXISTS (SELECT 1 FROM liens_LegacyIdCrosswalks x WHERE x.TenantId = r.TenantId
                  AND x.ImportRunId = r.Id AND x.SourceSystem = 'SL-CORE'
                  AND x.SourceTable = 'SL_LEINS_MEDICAL' AND x.TargetEntity = 'Lien');
    IF v_core_runs <> 1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTP-006 exactly one completed Program 1 core import with lien crosswalks is required'; END IF;

    SELECT r.Id, r.OrgId, r.CreatedByUserId, LOWER(r.SourceFingerprint)
      INTO v_core_run_id, v_org_id, v_user_id, v_fingerprint
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id AND r.SourceSystem = 'SL-CORE' AND r.LegacyProgram = '1'
      AND r.MappingVersion = 'sl-core-core-liens-v1' AND r.Status = 'Completed';

    SELECT COUNT(*) INTO v_contact_runs
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id AND r.SourceSystem = 'SL-CORE' AND r.LegacyProgram = '1'
      AND r.MappingVersion = 'sl-core-contact-facility-v1' AND LOWER(r.SourceFingerprint) = v_fingerprint
      AND r.Status = 'Completed'
      AND EXISTS (SELECT 1 FROM liens_LegacyIdCrosswalks x WHERE x.TenantId = r.TenantId
                  AND x.ImportRunId = r.Id AND x.SourceSystem = 'SL-CORE'
                  AND x.SourceTable = 'SL_CONTACT' AND x.TargetEntity = 'Contact');
    IF v_contact_runs <> 1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTP-007 exactly one completed Program 1 contact import with contact crosswalks is required'; END IF;

    SELECT r.Id, r.OrgId INTO v_contact_run_id, v_contact_org_id
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id AND r.SourceSystem = 'SL-CORE' AND r.LegacyProgram = '1'
      AND r.MappingVersion = 'sl-core-contact-facility-v1' AND LOWER(r.SourceFingerprint) = v_fingerprint
      AND r.Status = 'Completed';
    IF v_contact_org_id IS NULL OR v_contact_org_id <> v_org_id THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTP-007 contact import ownership does not match the core import'; END IF;

    SELECT COUNT(*) INTO v_provenance_count FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE`
    WHERE PROVENANCE_KEY = 'sl-core-current' AND LOWER(SOURCE_FINGERPRINT) = v_fingerprint
      AND IMPORT_SCOPE = 'sl-core-core-liens-v1';
    IF v_provenance_count <> 1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTP-008 source provenance does not match the completed core import'; END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_lien_provider_repair;
    CREATE TEMPORARY TABLE tmp_sl_core_lien_provider_repair AS
    SELECT staged.*,
           CASE WHEN Resolution IN ('NeedsInsert','NeedsUpdate') THEN
                CASE WHEN ExistingNotes IS NULL OR TRIM(ExistingNotes) = '' THEN CONCAT('medicalProviderId=', TargetProviderId)
                     ELSE CONCAT(ExistingNotes, '; medicalProviderId=', TargetProviderId) END
                ELSE ExistingNotes END AS NotesAfter
    FROM (
        SELECT source_rows.*,
               CASE
                 WHEN LienCrosswalkId IS NULL THEN 'MissingLienCrosswalk'
                 WHEN TargetLienId IS NULL OR TargetLienOrgId IS NULL OR TargetLienOrgId <> v_org_id OR TargetCaseId IS NULL THEN 'InvalidTargetLien'
                 WHEN LegacyProviderCount > 1 THEN 'AmbiguousLegacyMedicalProvidersForLien'
                 WHEN SourceProviderId IS NULL THEN 'MissingOrInactiveSourceMedicalProvider'
                 WHEN ContactCrosswalkId IS NULL THEN 'MissingMedicalProviderContactCrosswalk'
                 WHEN TargetProviderId IS NULL OR TargetProviderOrgId IS NULL OR TargetProviderOrgId <> v_org_id
                   OR TargetProviderType <> 'Provider' OR TargetProviderIsActive <> 1 THEN 'InvalidTargetMedicalProvider'
                 WHEN ExistingTaskCount > 1 THEN 'AmbiguousTargetMedicalFacilityInfoTasks'
                 WHEN ExistingTaskCount = 1 AND ExistingTaskOrgId <> v_org_id THEN 'InvalidTargetMedicalFacilityInfoTask'
                 WHEN TaskNumberOwnerId IS NOT NULL AND (ExistingTaskId IS NULL OR TaskNumberOwnerId <> ExistingTaskId) THEN 'TaskNumberCollision'
                 WHEN ExistingProviderCount > 1 THEN 'DuplicateMedicalProviderMetadata'
                 WHEN ExistingProviderCount = 1 AND ExistingProviderId IS NULL THEN 'MalformedExistingMedicalProviderId'
                 WHEN ExistingProviderId IS NOT NULL AND LOWER(ExistingProviderId) <> LOWER(TargetProviderId) THEN 'ConflictingExistingMedicalProviderId'
                 WHEN ExistingProviderId IS NOT NULL THEN 'AlreadyCorrect'
                 WHEN ExistingTaskCount = 0 THEN 'NeedsInsert'
                 WHEN CHAR_LENGTH(CASE WHEN ExistingNotes IS NULL OR TRIM(ExistingNotes) = '' THEN CONCAT('medicalProviderId=', TargetProviderId)
                                        ELSE CONCAT(ExistingNotes, '; medicalProviderId=', TargetProviderId) END) > 4000 THEN 'NotesOverflow'
                 ELSE 'NeedsUpdate'
               END AS Resolution
        FROM (
            SELECT source_map.*, source_provider.CONTACT_ID AS SourceProviderId,
                   contact_x.Id AS ContactCrosswalkId, contact_x.TargetId AS TargetProviderId,
                   target_provider.OrgId AS TargetProviderOrgId, target_provider.ContactType AS TargetProviderType,
                   target_provider.IsActive AS TargetProviderIsActive,
                   COALESCE(existing_task.TaskCount, 0) AS ExistingTaskCount, existing_task.TaskId AS ExistingTaskId,
                   existing_task.OrgId AS ExistingTaskOrgId, existing_task.Notes AS ExistingNotes,
                   (CHAR_LENGTH(LOWER(COALESCE(existing_task.Notes, ''))) - CHAR_LENGTH(REPLACE(LOWER(COALESCE(existing_task.Notes, '')), 'medicalproviderid=', ''))) / CHAR_LENGTH('medicalProviderId=') AS ExistingProviderCount,
                   NULLIF(SUBSTRING(REGEXP_SUBSTR(COALESCE(existing_task.Notes, ''), 'medicalProviderId=[0-9A-Fa-f-]{36}'), CHAR_LENGTH('medicalProviderId=') + 1), '') AS ExistingProviderId,
                   task_number_owner.Id AS TaskNumberOwnerId
            FROM (
                SELECT lm.LM_ID AS LegacyLienId,
                       COUNT(DISTINCT NULLIF(NULLIF(TRIM(CAST(info.LMI_MEDICAL_PROVIDER AS CHAR)), ''), '0')) AS LegacyProviderCount,
                       MIN(NULLIF(NULLIF(TRIM(CAST(info.LMI_MEDICAL_PROVIDER AS CHAR)), ''), '0')) AS LegacyProviderId,
                       lien_x.Id AS LienCrosswalkId, lien_x.TargetId AS TargetLienId,
                       target_lien.OrgId AS TargetLienOrgId, target_lien.CaseId AS TargetCaseId,
                       CONCAT('SLCORE-LMFI-', lm.LM_ID) AS TaskNumber
                FROM `SL-CORE`.`SL_LEINS_MEDICAL` lm
                INNER JOIN `SL-CORE`.`SL_LEINS_MEDICAL_INFORMATION_FACILITY` info ON info.LMI_LM_ID = lm.LM_ID
                LEFT JOIN liens_LegacyIdCrosswalks lien_x ON lien_x.TenantId = v_tenant_id AND lien_x.ImportRunId = v_core_run_id
                    AND lien_x.SourceSystem = 'SL-CORE' AND lien_x.SourceTable = 'SL_LEINS_MEDICAL'
                    AND lien_x.LegacyId = CAST(lm.LM_ID AS CHAR) AND lien_x.TargetEntity = 'Lien'
                LEFT JOIN liens_Liens target_lien ON target_lien.Id = lien_x.TargetId AND target_lien.TenantId = v_tenant_id
                WHERE COALESCE(lm.LM_IS_DELETED, 'N') <> 'Y'
                  AND NULLIF(NULLIF(TRIM(CAST(info.LMI_MEDICAL_PROVIDER AS CHAR)), ''), '0') IS NOT NULL
                GROUP BY lm.LM_ID, lien_x.Id, lien_x.TargetId, target_lien.OrgId, target_lien.CaseId
            ) source_map
            LEFT JOIN `SL-CORE`.`SL_CONTACT` source_provider ON source_provider.CONTACT_ID = source_map.LegacyProviderId
                AND source_provider.CONTACT_PROGRAM = 1 AND source_provider.CONTACT_TYPE = 2 AND COALESCE(source_provider.CONTACT_STATUS, 'A') = 'A'
            LEFT JOIN liens_LegacyIdCrosswalks contact_x ON contact_x.TenantId = v_tenant_id AND contact_x.ImportRunId = v_contact_run_id
                AND contact_x.SourceSystem = 'SL-CORE' AND contact_x.SourceTable = 'SL_CONTACT'
                AND contact_x.LegacyId = source_map.LegacyProviderId AND contact_x.TargetEntity = 'Contact'
            LEFT JOIN liens_Contacts target_provider ON target_provider.Id = contact_x.TargetId AND target_provider.TenantId = v_tenant_id
            LEFT JOIN (
                SELECT LienId, COUNT(*) AS TaskCount, MIN(Id) AS TaskId, MIN(OrgId) AS OrgId, MIN(Notes) AS Notes
                FROM liens_ServicingItems WHERE TenantId = v_tenant_id AND TaskType = 'LegacyMedicalFacilityInfo'
                GROUP BY LienId
            ) existing_task ON existing_task.LienId = source_map.TargetLienId
            LEFT JOIN liens_ServicingItems task_number_owner ON task_number_owner.TenantId = v_tenant_id AND task_number_owner.TaskNumber = source_map.TaskNumber
        ) source_rows
    ) staged;

    SELECT COUNT(*) INTO v_source_count FROM tmp_sl_core_lien_provider_repair;
    IF v_source_count = 0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTP-009 no eligible source medical-provider links were found'; END IF;
    SELECT COUNT(*) INTO v_inserts FROM tmp_sl_core_lien_provider_repair WHERE Resolution = 'NeedsInsert';
    SELECT COUNT(*) INTO v_updates FROM tmp_sl_core_lien_provider_repair WHERE Resolution = 'NeedsUpdate';
    SELECT COUNT(*) INTO v_conflicts FROM tmp_sl_core_lien_provider_repair WHERE Resolution NOT IN ('NeedsInsert','NeedsUpdate','AlreadyCorrect');
    IF v_conflicts <> 0 THEN
        SELECT Resolution, COUNT(*) AS Liens FROM tmp_sl_core_lien_provider_repair
        WHERE Resolution NOT IN ('NeedsInsert','NeedsUpdate','AlreadyCorrect') GROUP BY Resolution ORDER BY Resolution;
    END IF;

    IF NOT v_apply THEN
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_lien_provider_repair;
        DO RELEASE_LOCK(v_contact_lock); SET v_contact_locked = 0; DO RELEASE_LOCK(v_core_lock); SET v_core_locked = 0;
        SELECT 'lien-medical-provider-backfill-preflight-passed' AS Result, v_core_run_id AS CoreImportRunId,
               v_contact_run_id AS ContactImportRunId, v_source_count AS SourceLienProviderLinks,
               v_inserts AS TasksToInsert, v_updates AS TasksToUpdate, (v_inserts + v_updates) AS ChangesToApply,
               v_conflicts AS Conflicts;
    ELSE
        IF p_expected_changes <> v_inserts + v_updates THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTP-011 expected change count does not match the validated repair plan'; END IF;
        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; START TRANSACTION; SET v_in_transaction = TRUE;
        INSERT INTO liens_ServicingItems (Id,TenantId,OrgId,TaskNumber,TaskType,Description,Status,Priority,AssignedTo,AssignedToUserId,CaseId,LienId,DueDate,Notes,Resolution,StartedAtUtc,CompletedAtUtc,EscalatedAtUtc,CreatedByUserId,UpdatedByUserId,CreatedAtUtc,UpdatedAtUtc)
        SELECT UUID(),v_tenant_id,v_org_id,TaskNumber,'LegacyMedicalFacilityInfo','Legacy medical facility information','Pending','Normal','system',NULL,TargetCaseId,TargetLienId,NULL,NotesAfter,NULL,NULL,NULL,NULL,v_user_id,v_user_id,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)
        FROM tmp_sl_core_lien_provider_repair WHERE Resolution = 'NeedsInsert';
        SET v_applied_inserts = ROW_COUNT();
        UPDATE liens_ServicingItems task
        INNER JOIN tmp_sl_core_lien_provider_repair repair ON repair.ExistingTaskId = task.Id
        SET task.Notes = repair.NotesAfter, task.UpdatedAtUtc = UTC_TIMESTAMP(6), task.UpdatedByUserId = v_user_id
        WHERE repair.Resolution = 'NeedsUpdate' AND task.TenantId = v_tenant_id AND task.OrgId = v_org_id AND task.Notes <=> repair.ExistingNotes;
        SET v_applied_updates = ROW_COUNT();
        IF v_applied_inserts <> v_inserts OR v_applied_updates <> v_updates THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTP-012 applied row count did not match the validated repair plan'; END IF;
        SELECT COUNT(*) INTO v_postcondition_errors FROM tmp_sl_core_lien_provider_repair repair
        INNER JOIN liens_ServicingItems task ON task.LienId = repair.TargetLienId AND task.TenantId = v_tenant_id AND task.OrgId = v_org_id AND task.TaskType = 'LegacyMedicalFacilityInfo'
        WHERE repair.Resolution IN ('NeedsInsert','NeedsUpdate','AlreadyCorrect') AND LOCATE(CONCAT('medicalProviderId=', repair.TargetProviderId), task.Notes) = 0;
        IF v_postcondition_errors <> 0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTP-013 postcondition failed: target lien medical-provider metadata is incomplete'; END IF;
        COMMIT; SET v_in_transaction = FALSE;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_lien_provider_repair;
        DO RELEASE_LOCK(v_contact_lock); SET v_contact_locked = 0; DO RELEASE_LOCK(v_core_lock); SET v_core_locked = 0;
        SELECT CASE WHEN v_conflicts = 0 THEN 'lien-medical-provider-backfill-applied'
                    ELSE 'lien-medical-provider-backfill-applied-with-conflicts' END AS Result,
               v_core_run_id AS CoreImportRunId,
               v_contact_run_id AS ContactImportRunId, v_source_count AS SourceLienProviderLinks,
               v_applied_inserts AS TasksInserted, v_applied_updates AS TasksUpdated,
               v_conflicts AS UnresolvedConflicts;
    END IF;
END$$

DELIMITER ;
