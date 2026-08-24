-- Repairs SL-CORE case-note category provenance for imports created before
-- CN_USER_ID was included in the note mapping. Run preflight first and retain
-- the returned counts/checksum with the release approval evidence.
--
-- TRACKING: CN_USER_ID IS NULL     -> Category = 'general'
-- FEED:     CN_USER_ID IS NOT NULL -> Category = 'feed'
--
-- Usage:
--   CALL liens_reconcile_sl_core_case_note_categories(
--       '<tenant-guid>', '<64-char-source-fingerprint>', '<approval-reference>', NULL, NULL, '0');
--   CALL liens_reconcile_sl_core_case_note_categories(
--       '<tenant-guid>', '<64-char-source-fingerprint>', '<approval-reference>',
--       <preflight-eligible-notes>, '<preflight-checksum>', '1');
--
-- Error prefix: LSLNR-

DROP PROCEDURE IF EXISTS liens_reconcile_sl_core_case_note_categories;

DELIMITER $$

CREATE PROCEDURE liens_reconcile_sl_core_case_note_categories(
    IN p_tenant_id          CHAR(36),
    IN p_source_fingerprint CHAR(64),
    IN p_approval_reference VARCHAR(200),
    IN p_expected_notes     INT,
    IN p_expected_checksum  CHAR(64),
    IN p_apply              CHAR(1)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id          CHAR(36);
    DECLARE v_fingerprint        CHAR(64);
    DECLARE v_approval_reference VARCHAR(200);
    DECLARE v_apply              BOOLEAN;
    DECLARE v_lock_name          VARCHAR(64);
    DECLARE v_locked             INT DEFAULT 0;
    DECLARE v_in_transaction     BOOLEAN DEFAULT FALSE;
    DECLARE v_run_count          INT DEFAULT 0;
    DECLARE v_run_evidence_count INT DEFAULT 0;
    DECLARE v_provenance_count   INT DEFAULT 0;
    DECLARE v_source_count       INT DEFAULT 0;
    DECLARE v_crosswalk_count    INT DEFAULT 0;
    DECLARE v_conflict_count     INT DEFAULT 0;
    DECLARE v_category_updates   INT DEFAULT 0;
    DECLARE v_hash_updates       INT DEFAULT 0;
    DECLARE v_postcondition      INT DEFAULT 0;
    DECLARE v_locked_target_rows INT DEFAULT 0;
    DECLARE v_legacy_program     VARCHAR(50);
    DECLARE v_checksum           CHAR(64);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        IF v_in_transaction THEN
            ROLLBACK;
            SET v_in_transaction = FALSE;
        END IF;
        DROP TEMPORARY TABLE IF EXISTS tmp_case_note_reconciliation;
        IF v_locked = 1 THEN DO RELEASE_LOCK(v_lock_name); END IF;
        RESIGNAL;
    END;

    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    SET v_fingerprint = LOWER(TRIM(p_source_fingerprint));
    SET v_approval_reference = TRIM(p_approval_reference);
    SET v_apply = (p_apply = '1');

    IF v_tenant_id IS NULL
       OR v_tenant_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR v_fingerprint IS NULL
       OR v_fingerprint NOT REGEXP '^[0-9a-f]{64}$'
       OR NULLIF(v_approval_reference, '') IS NULL
       OR p_apply IS NULL OR p_apply NOT IN ('0', '1')
       OR (p_apply = '0' AND (p_expected_notes IS NOT NULL OR p_expected_checksum IS NOT NULL))
       OR (p_apply = '1' AND (p_expected_notes IS NULL OR p_expected_notes < 0
           OR LOWER(COALESCE(p_expected_checksum, '')) NOT REGEXP '^[0-9a-f]{64}$')) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNR-001 invalid tenant, fingerprint, approval, expected snapshot, or apply flag';
    END IF;

    IF DATABASE() NOT IN ('LS_LIENS', 'LS_QA_LIENS') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNR-002 target schema must be LS_LIENS or LS_QA_LIENS';
    END IF;

    -- Match the supported SQL importers' tenant lock so import and repair cannot overlap.
    SET v_lock_name = CONCAT('liens:slcore:', v_tenant_id);
    SELECT GET_LOCK(v_lock_name, 10) INTO v_locked;
    IF v_locked <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNR-003 could not acquire tenant reconciliation lock';
    END IF;

    START TRANSACTION;
    SET v_in_transaction = TRUE;

    SELECT
        COUNT(*),
        MAX(r.LegacyProgram),
        COALESCE(SUM(CASE
            WHEN r.ApprovalId IS NULL
                 AND LOWER(r.MappingManifestHash) REGEXP '^[0-9a-f]{64}$'
                 AND NULLIF(TRIM(r.MappingVersion), '') IS NOT NULL
                THEN 1
            WHEN r.ApprovalId IS NOT NULL AND EXISTS (
                SELECT 1
                FROM liens_LegacyImportApprovals a
                WHERE a.Id = r.ApprovalId
                  AND a.TenantId = r.TenantId
                  AND a.SourceSystem = r.SourceSystem
                  AND LOWER(a.SourceFingerprint) = LOWER(r.SourceFingerprint)
                  AND a.MappingApprovalReference = r.MappingApprovalReference
                  AND a.Status = 'Consumed'
                  AND a.ConsumedByRunId = r.Id
            ) THEN 1
            ELSE 0
        END), 0)
      INTO v_run_count, v_legacy_program, v_run_evidence_count
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id
      AND r.SourceSystem = 'SL-CORE'
      AND LOWER(r.SourceFingerprint) = v_fingerprint
      AND r.MappingApprovalReference = v_approval_reference
      AND r.Status = 'Completed'
      AND r.CompletedAtUtc IS NOT NULL
      AND EXISTS (
          SELECT 1
          FROM liens_LegacyIdCrosswalks run_x
          WHERE run_x.ImportRunId = r.Id
            AND run_x.TenantId = r.TenantId
            AND run_x.SourceSystem = 'SL-CORE'
            AND run_x.SourceTable = 'SL_CASE_NOTES'
            AND run_x.TargetEntity = 'CaseNote'
      )
    FOR UPDATE;

    IF v_run_count <> 1 OR v_run_evidence_count <> 1 OR NULLIF(v_legacy_program, '') IS NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNR-004 exactly one completed import run with approved evidence is required';
    END IF;

    SELECT COUNT(*) INTO v_provenance_count
    FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE`
    WHERE PROVENANCE_KEY = 'sl-core-current'
      AND LOWER(SOURCE_FINGERPRINT) = v_fingerprint
      AND IMPORT_SCOPE = 'sl-core-core-liens-v1';

    IF v_provenance_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNR-005 source provenance does not match the approved import';
    END IF;

    -- Lock the complete target note/crosswalk/case set before staging and checking it.
    SELECT COUNT(*) INTO v_locked_target_rows
    FROM liens_LegacyIdCrosswalks x
    WHERE x.TenantId = v_tenant_id
      AND x.SourceSystem = 'SL-CORE'
      AND x.SourceTable = 'SL_CASE_NOTES'
      AND x.TargetEntity = 'CaseNote'
    FOR UPDATE;

    SELECT COUNT(*) INTO v_locked_target_rows
    FROM liens_CaseNotes note
    INNER JOIN liens_LegacyIdCrosswalks x
       ON x.TargetId = note.Id
      AND x.TenantId = v_tenant_id
      AND x.SourceSystem = 'SL-CORE'
      AND x.SourceTable = 'SL_CASE_NOTES'
      AND x.TargetEntity = 'CaseNote'
    INNER JOIN liens_Cases target_case ON target_case.Id = note.CaseId
    FOR UPDATE;

    DROP TEMPORARY TABLE IF EXISTS tmp_case_note_reconciliation;
    CREATE TEMPORARY TABLE tmp_case_note_reconciliation AS
    SELECT
        x.Id AS CrosswalkId,
        x.TargetId AS NoteId,
        x.SourceHash AS OldSourceHash,
        CONCAT(
            'case-note-v2:',
            SHA2(CONCAT_WS('|', n.CN_ID, n.CN_CASE_ID, n.CN_NOTE, n.CN_CREATED,
                           n.CN_CREATED_BY, n.CN_IS_DELETED, n.CN_USER_ID, v_fingerprint), 256)
        ) AS NewSourceHash,
        note.Category AS CurrentCategory,
        CASE WHEN n.CN_USER_ID IS NULL THEN 'general' ELSE 'feed' END AS DesiredCategory,
        note.IsEdited,
        note.IsDeleted,
        CASE WHEN UPPER(COALESCE(n.CN_IS_DELETED, 'N')) = 'Y' THEN 1 ELSE 0 END AS DesiredIsDeleted,
        note.Content AS TargetContent,
        TRIM(n.CN_NOTE) AS SourceContent,
        note.TenantId AS NoteTenantId,
        target_case.TenantId AS CaseTenantId,
        note.CaseId AS NoteCaseId,
        case_x.TargetId AS CrosswalkCaseId
    FROM `SL-CORE`.`SL_CASE_NOTES` n
    INNER JOIN `SL-CORE`.`SL_CASE` source_case
       ON source_case.CASE_ID = n.CN_CASE_ID
      AND CAST(source_case.CASE_PROGRAM AS CHAR) = v_legacy_program
      AND COALESCE(source_case.CASE_IS_DELETED, 'N') <> 'Y'
    LEFT JOIN liens_LegacyIdCrosswalks x
       ON x.TenantId = v_tenant_id
      AND x.SourceSystem = 'SL-CORE'
      AND x.SourceTable = 'SL_CASE_NOTES'
      AND x.TargetEntity = 'CaseNote'
      AND x.LegacyId = CAST(n.CN_ID AS CHAR)
    LEFT JOIN liens_LegacyIdCrosswalks case_x
       ON case_x.TenantId = v_tenant_id
      AND case_x.SourceSystem = 'SL-CORE'
      AND case_x.SourceTable = 'SL_CASE'
      AND case_x.TargetEntity = 'Case'
      AND case_x.LegacyId = CAST(n.CN_CASE_ID AS CHAR)
    LEFT JOIN liens_CaseNotes note ON note.Id = x.TargetId
    LEFT JOIN liens_Cases target_case ON target_case.Id = note.CaseId
    WHERE NULLIF(TRIM(n.CN_NOTE), '') IS NOT NULL;

    SELECT COUNT(*) INTO v_source_count FROM tmp_case_note_reconciliation;
    SELECT COUNT(*) INTO v_crosswalk_count
    FROM liens_LegacyIdCrosswalks
    WHERE TenantId = v_tenant_id
      AND SourceSystem = 'SL-CORE'
      AND SourceTable = 'SL_CASE_NOTES'
      AND TargetEntity = 'CaseNote';

    IF v_source_count <> v_crosswalk_count THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNR-006 source/crosswalk note counts do not match';
    END IF;

    SELECT COUNT(*) INTO v_conflict_count
    FROM tmp_case_note_reconciliation
    WHERE CrosswalkId IS NULL
       OR NoteId IS NULL
       OR CrosswalkCaseId IS NULL
       OR NoteTenantId <> v_tenant_id
       OR CaseTenantId <> v_tenant_id
       OR NoteCaseId <> CrosswalkCaseId
       OR IsEdited <> 0
       OR CurrentCategory NOT IN ('general', 'feed')
       OR IsDeleted <> DesiredIsDeleted
       OR BINARY TargetContent <> BINARY SourceContent
       OR (OldSourceHash <> NewSourceHash
           AND OldSourceHash NOT REGEXP '^[0-9A-Fa-f]{64}$');

    IF v_conflict_count <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLNR-007 note ownership, edit, content, category, deletion, or provenance conflict';
    END IF;

    SELECT
        SHA2(CONCAT(
            COUNT(*), '|',
            COALESCE(SUM(CRC32(CONCAT(NoteId, '|', OldSourceHash, '|', CurrentCategory, '|',
                IsEdited, '|', IsDeleted, '|', CRC32(TargetContent), '|', DesiredCategory, '|', NewSourceHash))), 0), '|',
            COALESCE(BIT_XOR(CRC32(CONCAT(NoteId, '|', OldSourceHash, '|', CurrentCategory, '|',
                IsEdited, '|', IsDeleted, '|', CRC32(TargetContent), '|', DesiredCategory, '|', NewSourceHash))), 0)
        ), 256)
      INTO v_checksum
    FROM tmp_case_note_reconciliation;

    SELECT COUNT(*) INTO v_category_updates
    FROM tmp_case_note_reconciliation
    WHERE CurrentCategory <> DesiredCategory;

    SELECT COUNT(*) INTO v_hash_updates
    FROM tmp_case_note_reconciliation
    WHERE OldSourceHash <> NewSourceHash;

    IF v_apply THEN
        IF p_expected_notes <> v_source_count
           OR LOWER(p_expected_checksum) <> LOWER(v_checksum) THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLNR-008 apply snapshot does not match the approved preflight';
        END IF;

        UPDATE liens_CaseNotes note
        INNER JOIN tmp_case_note_reconciliation r ON r.NoteId = note.Id
        SET note.Category = r.DesiredCategory
        WHERE note.TenantId = v_tenant_id
          AND note.CaseId = r.CrosswalkCaseId
          AND note.IsEdited = 0
          AND note.IsDeleted = r.DesiredIsDeleted
          AND BINARY note.Content = BINARY r.SourceContent
          AND note.Category IN ('general', 'feed')
          AND note.Category <> r.DesiredCategory;

        UPDATE liens_LegacyIdCrosswalks x
        INNER JOIN tmp_case_note_reconciliation r ON r.CrosswalkId = x.Id
        SET x.SourceHash = r.NewSourceHash
        WHERE x.TenantId = v_tenant_id
          AND x.SourceSystem = 'SL-CORE'
          AND x.SourceTable = 'SL_CASE_NOTES'
          AND x.TargetEntity = 'CaseNote'
          AND (x.SourceHash = r.OldSourceHash)
          AND x.SourceHash <> r.NewSourceHash;

        SELECT COUNT(*) INTO v_postcondition
        FROM tmp_case_note_reconciliation r
        LEFT JOIN liens_CaseNotes note ON note.Id = r.NoteId
        LEFT JOIN liens_Cases target_case ON target_case.Id = note.CaseId
        LEFT JOIN liens_LegacyIdCrosswalks x ON x.Id = r.CrosswalkId
        WHERE note.Id IS NULL
           OR target_case.Id IS NULL
           OR x.Id IS NULL
           OR note.TenantId <> v_tenant_id
           OR target_case.TenantId <> v_tenant_id
           OR note.CaseId <> r.CrosswalkCaseId
           OR note.IsEdited <> 0
           OR note.IsDeleted <> r.DesiredIsDeleted
           OR BINARY note.Content <> BINARY r.SourceContent
           OR note.Category <> r.DesiredCategory
           OR x.TenantId <> v_tenant_id
           OR x.SourceSystem <> 'SL-CORE'
           OR x.SourceTable <> 'SL_CASE_NOTES'
           OR x.TargetEntity <> 'CaseNote'
           OR x.SourceHash <> r.NewSourceHash;

        IF v_postcondition <> 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLNR-009 apply postcondition failed';
        END IF;

        SELECT COUNT(*) INTO v_crosswalk_count
        FROM liens_LegacyIdCrosswalks
        WHERE TenantId = v_tenant_id
          AND SourceSystem = 'SL-CORE'
          AND SourceTable = 'SL_CASE_NOTES'
          AND TargetEntity = 'CaseNote';
        IF v_crosswalk_count <> v_source_count THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLNR-010 apply row-count postcondition failed';
        END IF;

        COMMIT;
        SET v_in_transaction = FALSE;
    ELSE
        ROLLBACK;
        SET v_in_transaction = FALSE;
    END IF;

    SELECT
        CASE WHEN v_apply THEN 'applied' ELSE 'preflight' END AS Status,
        v_tenant_id AS TenantId,
        v_fingerprint AS SourceFingerprint,
        v_approval_reference AS ApprovalReference,
        v_source_count AS EligibleNotes,
        v_category_updates AS CategoryUpdates,
        v_hash_updates AS SourceHashUpdates,
        v_checksum AS ExpectedChecksum;

    DROP TEMPORARY TABLE IF EXISTS tmp_case_note_reconciliation;
    DO RELEASE_LOCK(v_lock_name);
    SET v_locked = 0;
END$$

DELIMITER ;
