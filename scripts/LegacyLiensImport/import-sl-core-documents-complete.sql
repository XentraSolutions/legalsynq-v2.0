-- liens_migrate_sl_core_documents_complete
--
-- One-procedure migration of active SL-CORE Program documents into the
-- current Liens compatibility model:
--   * SL_CASE_DOCUMENT          -> liens_ServicingItems/LegacyCaseDocument
--   * SL_LIENS_MEDICAL_DOCUMENT -> liens_ServicingItems/LegacyMedicalDocument
--   * Both source tables        -> liens_LegacyIdCrosswalks
--
-- This is deliberately a SQL-only compatibility migration. It retains each
-- allowlisted legacy HTTPS URL in ServicingItem.Notes so the current Liens API
-- can list/open the document. MySQL cannot download those remote bytes, upload
-- them to the Documents service, or execute its malware-scan pipeline. Use the
-- service-owned importer instead if the binary files must be re-homed.
--
-- Approved legacy document-type mapping:
--   1  HICFA / Bill     -> HicfaOrBill
--   6  Lien Agreement   -> LienAgreement
--   10 Bills & Records  -> BillsAndRecords
--   11 Bills & Recs     -> BillsAndRecs
--   12 Medical Invoice  -> HicfaOrBill
--   14 Payoff Quote     -> Other (legacy typeId=14 remains in Notes and is
--                          recognized as a payoff statement by current APIs)
--
-- Preconditions:
--   * Connect with LS_QA_LIENS or LS_LIENS selected.
--   * The controlled SL-CORE staging schema is on the same MySQL server.
--   * Exactly one completed core import owns valid SL_CASE and
--     SL_LEINS_MEDICAL crosswalks for this tenant/source fingerprint/program.
--   * SL_MIGRATION_SOURCE_PROVENANCE still contains the source receipt used
--     by the completed core import (IMPORT_SCOPE='sl-core-core-liens-v1').
--   * No document crosswalk from this source already exists for the tenant.
--
-- Usage:
--   CALL liens_migrate_sl_core_documents_complete('<tenant-guid>', '0');
--   CALL liens_migrate_sl_core_documents_complete('<tenant-guid>', '1');
--
-- Dry run performs no permanent writes. Apply is all-or-nothing.
-- Error prefix: LSLDOC-

DROP PROCEDURE IF EXISTS liens_migrate_sl_core_documents_complete;

DELIMITER $$

CREATE PROCEDURE liens_migrate_sl_core_documents_complete(
    IN p_tenant_id CHAR(36),
    IN p_apply     CHAR(1)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id              CHAR(36);
    DECLARE v_apply                  BOOLEAN;
    DECLARE v_orig_tz                VARCHAR(64);
    DECLARE v_tz_changed             BOOLEAN DEFAULT FALSE;
    DECLARE v_lock_name              VARCHAR(64);
    DECLARE v_locked                 INT DEFAULT 0;
    DECLARE v_in_transaction         BOOLEAN DEFAULT FALSE;

    DECLARE v_org_id                 CHAR(36);
    DECLARE v_user_id                CHAR(36);
    DECLARE v_legacy_program         VARCHAR(50);
    DECLARE v_fingerprint            CHAR(64);
    DECLARE v_core_run_id            CHAR(36);
    DECLARE v_document_run_id        CHAR(36);

    DECLARE v_table_count            INT DEFAULT 0;
    DECLARE v_provenance_count       INT DEFAULT 0;
    DECLARE v_core_run_count         INT DEFAULT 0;
    DECLARE v_existing_crosswalks    INT DEFAULT 0;
    DECLARE v_case_document_count    INT DEFAULT 0;
    DECLARE v_lien_document_count    INT DEFAULT 0;
    DECLARE v_total_document_count   INT DEFAULT 0;
    DECLARE v_unmapped_count         INT DEFAULT 0;
    DECLARE v_parent_error_count     INT DEFAULT 0;
    DECLARE v_url_error_count        INT DEFAULT 0;
    DECLARE v_notes_error_count      INT DEFAULT 0;
    DECLARE v_task_collision_count   INT DEFAULT 0;
    DECLARE v_items_inserted         INT DEFAULT 0;
    DECLARE v_crosswalks_inserted    INT DEFAULT 0;
    DECLARE v_postcondition_errors   INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        IF v_in_transaction THEN
            ROLLBACK;
            SET v_in_transaction = FALSE;
        END IF;
        DROP TEMPORARY TABLE IF EXISTS tmp_sldoc_documents;
        IF v_tz_changed THEN
            SET @@session.time_zone = v_orig_tz;
        END IF;
        IF v_locked = 1 THEN
            DO RELEASE_LOCK(v_lock_name);
        END IF;
        RESIGNAL;
    END;

    -- ---------------------------------------------------------------------
    -- 1. Parameters, target, and lock
    -- ---------------------------------------------------------------------
    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    IF v_tenant_id IS NULL
       OR v_tenant_id NOT REGEXP
          '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR p_apply IS NULL OR p_apply NOT IN ('0', '1') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLDOC-001 invalid tenant GUID or apply flag';
    END IF;
    SET v_apply = (p_apply = '1');

    IF DATABASE() NOT IN ('LS_LIENS', 'LS_QA_LIENS') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLDOC-002 target schema must be LS_LIENS or LS_QA_LIENS';
    END IF;

    SET v_orig_tz = @@session.time_zone;
    SET @@session.time_zone = '+00:00';
    SET v_tz_changed = TRUE;

    SET v_lock_name = CONCAT('liens:slcore:documents:', v_tenant_id);
    SELECT GET_LOCK(v_lock_name, 10) INTO v_locked;
    IF COALESCE(v_locked, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLDOC-003 document migration lock is already held';
    END IF;

    -- ---------------------------------------------------------------------
    -- 2. Schema and completed core-import contract
    -- ---------------------------------------------------------------------
    SELECT COUNT(*) INTO v_table_count
    FROM information_schema.tables
    WHERE (table_schema = DATABASE() AND table_type = 'BASE TABLE'
           AND table_name IN (
               'liens_Cases', 'liens_Liens', 'liens_ServicingItems',
               'liens_LegacyImportRuns', 'liens_LegacyIdCrosswalks'))
       OR (table_schema = 'SL-CORE' AND table_type = 'BASE TABLE'
           AND table_name IN (
               'SL_CASE', 'SL_LEINS_MEDICAL',
               'SL_CASE_DOCUMENT', 'SL_LIENS_MEDICAL_DOCUMENT',
               'SL_MIGRATION_SOURCE_PROVENANCE'));
    IF v_table_count <> 10 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLDOC-004 required source or target tables are unavailable';
    END IF;

    SELECT COUNT(*) INTO v_core_run_count
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id
      AND r.SourceSystem = 'SL-CORE'
      AND r.Status = 'Completed'
      AND EXISTS (
          SELECT 1
          FROM liens_LegacyIdCrosswalks x
          INNER JOIN liens_Cases c
             ON c.Id = x.TargetId
            AND c.TenantId = r.TenantId
            AND c.OrgId = r.OrgId
          WHERE x.ImportRunId = r.Id
            AND x.TenantId = r.TenantId
            AND x.SourceSystem = 'SL-CORE'
            AND x.SourceTable = 'SL_CASE'
            AND x.TargetEntity = 'Case')
      AND EXISTS (
          SELECT 1
          FROM liens_LegacyIdCrosswalks x
          INNER JOIN liens_Liens l
             ON l.Id = x.TargetId
            AND l.TenantId = r.TenantId
            AND l.OrgId = r.OrgId
          WHERE x.ImportRunId = r.Id
            AND x.TenantId = r.TenantId
            AND x.SourceSystem = 'SL-CORE'
            AND x.SourceTable = 'SL_LEINS_MEDICAL'
            AND x.TargetEntity = 'Lien');
    IF v_core_run_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLDOC-005 exactly one completed core case/lien import is required';
    END IF;

    SELECT r.Id, r.OrgId, r.CreatedByUserId, r.LegacyProgram, r.SourceFingerprint
      INTO v_core_run_id, v_org_id, v_user_id, v_legacy_program, v_fingerprint
    FROM liens_LegacyImportRuns r
    WHERE r.TenantId = v_tenant_id
      AND r.SourceSystem = 'SL-CORE'
      AND r.Status = 'Completed'
      AND EXISTS (
          SELECT 1
          FROM liens_LegacyIdCrosswalks x
          INNER JOIN liens_Cases c
             ON c.Id = x.TargetId
            AND c.TenantId = r.TenantId
            AND c.OrgId = r.OrgId
          WHERE x.ImportRunId = r.Id
            AND x.TenantId = r.TenantId
            AND x.SourceSystem = 'SL-CORE'
            AND x.SourceTable = 'SL_CASE'
            AND x.TargetEntity = 'Case')
      AND EXISTS (
          SELECT 1
          FROM liens_LegacyIdCrosswalks x
          INNER JOIN liens_Liens l
             ON l.Id = x.TargetId
            AND l.TenantId = r.TenantId
            AND l.OrgId = r.OrgId
          WHERE x.ImportRunId = r.Id
            AND x.TenantId = r.TenantId
            AND x.SourceSystem = 'SL-CORE'
            AND x.SourceTable = 'SL_LEINS_MEDICAL'
            AND x.TargetEntity = 'Lien');

    IF v_org_id IS NULL OR v_user_id IS NULL
       OR v_legacy_program NOT REGEXP '^[1-9][0-9]*$'
       OR LOWER(v_fingerprint) NOT REGEXP '^[0-9a-f]{64}$' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLDOC-006 completed core import provenance is malformed';
    END IF;

    SELECT COUNT(*) INTO v_provenance_count
    FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE`
    WHERE PROVENANCE_KEY = 'sl-core-current'
      AND LOWER(SOURCE_FINGERPRINT) = LOWER(v_fingerprint)
      AND IMPORT_SCOPE = 'sl-core-core-liens-v1';
    IF v_provenance_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLDOC-007 source receipt does not match the completed core import';
    END IF;

    SELECT COUNT(*) INTO v_existing_crosswalks
    FROM liens_LegacyIdCrosswalks
    WHERE TenantId = v_tenant_id
      AND SourceSystem = 'SL-CORE'
      AND SourceTable IN ('SL_CASE_DOCUMENT', 'SL_LIENS_MEDICAL_DOCUMENT');
    IF v_existing_crosswalks <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLDOC-008 document crosswalks already exist; reconcile instead of re-importing';
    END IF;

    -- ---------------------------------------------------------------------
    -- 3. Stage both source tables against the completed core crosswalks
    -- ---------------------------------------------------------------------
    DROP TEMPORARY TABLE IF EXISTS tmp_sldoc_documents;
    CREATE TEMPORARY TABLE tmp_sldoc_documents (
        SourceTable        VARCHAR(100) NOT NULL,
        LegacyId          BIGINT NOT NULL,
        TargetItemId      CHAR(36) NOT NULL,
        TargetCaseId      CHAR(36) NULL,
        TargetLienId      CHAR(36) NULL,
        ReferenceType     VARCHAR(10) NOT NULL,
        ReferenceId       CHAR(36) NULL,
        FileName          VARCHAR(100) NOT NULL,
        LegacyTypeId      BIGINT NULL,
        DocumentTypeId    CHAR(36) NULL,
        SourceUrl         TEXT NULL,
        TaskNumber        VARCHAR(50) NOT NULL,
        TaskType          VARCHAR(100) NOT NULL,
        Description       VARCHAR(4000) NOT NULL,
        Notes             TEXT NULL,
        SourceHash        CHAR(64) NOT NULL,
        CreatedAtUtc      DATETIME(6) NOT NULL,
        UpdatedAtUtc      DATETIME(6) NOT NULL,
        PRIMARY KEY (SourceTable, LegacyId),
        UNIQUE KEY UX_tmp_sldoc_item (TargetItemId),
        UNIQUE KEY UX_tmp_sldoc_task (TaskNumber),
        KEY IX_tmp_sldoc_case (TargetCaseId),
        KEY IX_tmp_sldoc_lien (TargetLienId)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

    INSERT INTO tmp_sldoc_documents (
        SourceTable, LegacyId, TargetItemId, TargetCaseId, TargetLienId,
        ReferenceType, ReferenceId, FileName, LegacyTypeId, DocumentTypeId,
        SourceUrl, TaskNumber, TaskType, Description, Notes, SourceHash,
        CreatedAtUtc, UpdatedAtUtc)
    SELECT
        'SL_CASE_DOCUMENT',
        d.CD_ID,
        UUID(),
        tc.Id,
        NULL,
        'Case',
        tc.Id,
        COALESCE(NULLIF(TRIM(d.CD_FILENAME), ''), CONCAT('legacy-case-document-', d.CD_ID, '.pdf')),
        d.CD_TYPE_ID,
        CASE d.CD_TYPE_ID
            WHEN 1  THEN '10000000-0000-0000-0000-000000000001'
            WHEN 6  THEN '10000000-0000-0000-0000-000000000006'
            WHEN 10 THEN '10000000-0000-0000-0000-000000000009'
            WHEN 11 THEN '10000000-0000-0000-0000-000000000010'
            WHEN 12 THEN '10000000-0000-0000-0000-000000000001'
            WHEN 14 THEN '10000000-0000-0000-0000-000000000005'
            ELSE NULL
        END,
        TRIM(d.CD_URL),
        CONCAT('DOC-SLCD-', d.CD_ID),
        'LegacyCaseDocument',
        'Case document linked from SL-CORE',
        CONCAT(
            'documentUrl=', COALESCE(TRIM(d.CD_URL), ''),
            '; url=', COALESCE(TRIM(d.CD_URL), ''),
            '; filename=', REPLACE(REPLACE(
                COALESCE(NULLIF(TRIM(d.CD_FILENAME), ''), CONCAT('legacy-case-document-', d.CD_ID, '.pdf')),
                ';', ','), '=', ':'),
            '; originalFileName=', REPLACE(REPLACE(
                COALESCE(NULLIF(TRIM(d.CD_FILENAME), ''), CONCAT('legacy-case-document-', d.CD_ID, '.pdf')),
                ';', ','), '=', ':'),
            '; typeId=', COALESCE(CAST(d.CD_TYPE_ID AS CHAR), ''),
            '; documentTypeId=', COALESCE(CASE d.CD_TYPE_ID
                WHEN 1  THEN '10000000-0000-0000-0000-000000000001'
                WHEN 6  THEN '10000000-0000-0000-0000-000000000006'
                WHEN 10 THEN '10000000-0000-0000-0000-000000000009'
                WHEN 11 THEN '10000000-0000-0000-0000-000000000010'
                WHEN 12 THEN '10000000-0000-0000-0000-000000000001'
                WHEN 14 THEN '10000000-0000-0000-0000-000000000005'
                ELSE NULL END, ''),
            '; referenceType=Case; referenceId=', COALESCE(tc.Id, ''),
            '; description=Case document linked from SL-CORE'),
        SHA2(CONCAT_WS(CHAR(31),
            'SL_CASE_DOCUMENT', CAST(d.CD_ID AS CHAR), CAST(d.CD_CASE_ID AS CHAR),
            COALESCE(d.CD_FILENAME, ''), COALESCE(CAST(d.CD_TYPE_ID AS CHAR), ''),
            COALESCE(d.CD_URL, ''), COALESCE(d.CD_STATUS, ''),
            DATE_FORMAT(d.CD_CREATED, '%Y-%m-%dT%H:%i:%s.%fZ'),
            DATE_FORMAT(d.CD_UPDATED, '%Y-%m-%dT%H:%i:%s.%fZ')), 256),
        d.CD_CREATED,
        d.CD_UPDATED
    FROM `SL-CORE`.`SL_CASE_DOCUMENT` d
    INNER JOIN `SL-CORE`.`SL_CASE` sc ON sc.CASE_ID = d.CD_CASE_ID
    LEFT JOIN liens_LegacyIdCrosswalks x
      ON x.TenantId = v_tenant_id
     AND x.SourceSystem = 'SL-CORE'
     AND x.SourceTable = 'SL_CASE'
     AND x.LegacyId = CAST(d.CD_CASE_ID AS CHAR)
     AND x.TargetEntity = 'Case'
     AND x.ImportRunId = v_core_run_id
    LEFT JOIN liens_Cases tc
      ON tc.Id = x.TargetId
     AND tc.TenantId = v_tenant_id
     AND tc.OrgId = v_org_id
    WHERE sc.CASE_PROGRAM = CAST(v_legacy_program AS UNSIGNED)
      AND COALESCE(sc.CASE_IS_DELETED, 'N') <> 'Y'
      AND d.CD_STATUS = 'A';

    INSERT INTO tmp_sldoc_documents (
        SourceTable, LegacyId, TargetItemId, TargetCaseId, TargetLienId,
        ReferenceType, ReferenceId, FileName, LegacyTypeId, DocumentTypeId,
        SourceUrl, TaskNumber, TaskType, Description, Notes, SourceHash,
        CreatedAtUtc, UpdatedAtUtc)
    SELECT
        'SL_LIENS_MEDICAL_DOCUMENT',
        d.LMD_ID,
        UUID(),
        NULL,
        tl.Id,
        'Lien',
        tl.Id,
        COALESCE(NULLIF(TRIM(d.LMD_FILENAME), ''), CONCAT('legacy-lien-document-', d.LMD_ID, '.pdf')),
        d.LMD_TYPE_ID,
        CASE d.LMD_TYPE_ID
            WHEN 1  THEN '10000000-0000-0000-0000-000000000001'
            WHEN 6  THEN '10000000-0000-0000-0000-000000000006'
            WHEN 10 THEN '10000000-0000-0000-0000-000000000009'
            WHEN 11 THEN '10000000-0000-0000-0000-000000000010'
            WHEN 12 THEN '10000000-0000-0000-0000-000000000001'
            WHEN 14 THEN '10000000-0000-0000-0000-000000000005'
            ELSE NULL
        END,
        TRIM(d.LMD_URL),
        CONCAT('DOC-SLLMD-', d.LMD_ID),
        'LegacyMedicalDocument',
        'Lien medical document linked from SL-CORE',
        CONCAT(
            'documentUrl=', COALESCE(TRIM(d.LMD_URL), ''),
            '; url=', COALESCE(TRIM(d.LMD_URL), ''),
            '; filename=', REPLACE(REPLACE(
                COALESCE(NULLIF(TRIM(d.LMD_FILENAME), ''), CONCAT('legacy-lien-document-', d.LMD_ID, '.pdf')),
                ';', ','), '=', ':'),
            '; originalFileName=', REPLACE(REPLACE(
                COALESCE(NULLIF(TRIM(d.LMD_FILENAME), ''), CONCAT('legacy-lien-document-', d.LMD_ID, '.pdf')),
                ';', ','), '=', ':'),
            '; typeId=', COALESCE(CAST(d.LMD_TYPE_ID AS CHAR), ''),
            '; documentTypeId=', COALESCE(CASE d.LMD_TYPE_ID
                WHEN 1  THEN '10000000-0000-0000-0000-000000000001'
                WHEN 6  THEN '10000000-0000-0000-0000-000000000006'
                WHEN 10 THEN '10000000-0000-0000-0000-000000000009'
                WHEN 11 THEN '10000000-0000-0000-0000-000000000010'
                WHEN 12 THEN '10000000-0000-0000-0000-000000000001'
                WHEN 14 THEN '10000000-0000-0000-0000-000000000005'
                ELSE NULL END, ''),
            '; referenceType=Lien; referenceId=', COALESCE(tl.Id, ''),
            '; description=Lien medical document linked from SL-CORE'),
        SHA2(CONCAT_WS(CHAR(31),
            'SL_LIENS_MEDICAL_DOCUMENT', CAST(d.LMD_ID AS CHAR), CAST(d.LMD_LM_ID AS CHAR),
            COALESCE(d.LMD_FILENAME, ''), COALESCE(CAST(d.LMD_TYPE_ID AS CHAR), ''),
            COALESCE(d.LMD_URL, ''), COALESCE(d.LMD_STATUS, ''),
            DATE_FORMAT(d.LMD_CREATED, '%Y-%m-%dT%H:%i:%s.%fZ'),
            DATE_FORMAT(d.LMD_UPDATED, '%Y-%m-%dT%H:%i:%s.%fZ')), 256),
        d.LMD_CREATED,
        d.LMD_UPDATED
    FROM `SL-CORE`.`SL_LIENS_MEDICAL_DOCUMENT` d
    INNER JOIN `SL-CORE`.`SL_LEINS_MEDICAL` sl ON sl.LM_ID = d.LMD_LM_ID
    INNER JOIN `SL-CORE`.`SL_CASE` sc ON sc.CASE_ID = sl.LM_CASE_ID
    LEFT JOIN liens_LegacyIdCrosswalks x
      ON x.TenantId = v_tenant_id
     AND x.SourceSystem = 'SL-CORE'
     AND x.SourceTable = 'SL_LEINS_MEDICAL'
     AND x.LegacyId = CAST(d.LMD_LM_ID AS CHAR)
     AND x.TargetEntity = 'Lien'
     AND x.ImportRunId = v_core_run_id
    LEFT JOIN liens_Liens tl
      ON tl.Id = x.TargetId
     AND tl.TenantId = v_tenant_id
     AND tl.OrgId = v_org_id
    WHERE sc.CASE_PROGRAM = CAST(v_legacy_program AS UNSIGNED)
      AND COALESCE(sc.CASE_IS_DELETED, 'N') <> 'Y'
      AND COALESCE(sl.LM_IS_DELETED, 'N') <> 'Y'
      AND d.LMD_STATUS = 'A';

    -- ---------------------------------------------------------------------
    -- 4. Complete preflight validation
    -- ---------------------------------------------------------------------
    SELECT COUNT(*) INTO v_case_document_count
    FROM tmp_sldoc_documents WHERE SourceTable = 'SL_CASE_DOCUMENT';
    SELECT COUNT(*) INTO v_lien_document_count
    FROM tmp_sldoc_documents WHERE SourceTable = 'SL_LIENS_MEDICAL_DOCUMENT';
    SET v_total_document_count = v_case_document_count + v_lien_document_count;

    IF v_total_document_count = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLDOC-009 no active in-program source documents were found';
    END IF;

    SELECT COUNT(*) INTO v_unmapped_count
    FROM tmp_sldoc_documents WHERE DocumentTypeId IS NULL;
    IF v_unmapped_count <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLDOC-010 an active source document type is not approved';
    END IF;

    SELECT COUNT(*) INTO v_parent_error_count
    FROM tmp_sldoc_documents
    WHERE ReferenceId IS NULL
       OR (ReferenceType = 'Case' AND (TargetCaseId IS NULL OR TargetLienId IS NOT NULL))
       OR (ReferenceType = 'Lien' AND (TargetLienId IS NULL OR TargetCaseId IS NOT NULL));
    IF v_parent_error_count <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLDOC-011 a source document has no valid same-tenant parent crosswalk';
    END IF;

    SELECT COUNT(*) INTO v_url_error_count
    FROM tmp_sldoc_documents
    WHERE SourceUrl IS NULL OR SourceUrl = ''
       OR LOWER(SourceUrl) NOT LIKE 'https://legal-dmm-prod.legalsynq.com/%'
       OR LOWER(SUBSTRING_INDEX(SourceUrl, '?', 1)) NOT LIKE '%.pdf'
       OR SourceUrl LIKE '%;%'
       OR SourceUrl LIKE '%=%';
    IF v_url_error_count <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLDOC-012 source URL is missing, non-PDF, non-HTTPS, or not allowlisted';
    END IF;

    SELECT COUNT(*) INTO v_notes_error_count
    FROM tmp_sldoc_documents
    WHERE Notes IS NULL OR CHAR_LENGTH(Notes) > 4000;
    IF v_notes_error_count <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLDOC-013 generated document notes exceed the target contract';
    END IF;

    SELECT COUNT(*) INTO v_task_collision_count
    FROM tmp_sldoc_documents d
    INNER JOIN liens_ServicingItems item
      ON item.TenantId = v_tenant_id AND item.TaskNumber = d.TaskNumber;
    IF v_task_collision_count <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLDOC-014 a deterministic document task number already exists';
    END IF;

    SET v_document_run_id = UUID();

    -- Mapping review is a separate result set in both modes.
    SELECT LegacyTypeId, DocumentTypeId, COUNT(*) AS DocumentCount
    FROM tmp_sldoc_documents
    GROUP BY LegacyTypeId, DocumentTypeId
    ORDER BY LegacyTypeId;

    -- ---------------------------------------------------------------------
    -- 5. Dry-run result
    -- ---------------------------------------------------------------------
    IF NOT v_apply THEN
        DROP TEMPORARY TABLE IF EXISTS tmp_sldoc_documents;
        SET @@session.time_zone = v_orig_tz;
        SET v_tz_changed = FALSE;
        DO RELEASE_LOCK(v_lock_name);
        SET v_locked = 0;

        SELECT
            'document-migration-preflight-passed' AS Result,
            v_core_run_id AS PrerequisiteCoreRunId,
            LOWER(v_fingerprint) AS SourceFingerprint,
            v_legacy_program AS LegacyProgram,
            v_case_document_count AS CaseDocumentsToInsert,
            v_lien_document_count AS LienDocumentsToInsert,
            v_total_document_count AS TotalDocumentsToInsert,
            'legacy-url-links-only' AS StoragePolicy;
    ELSE
        -- -----------------------------------------------------------------
        -- 6. Apply in one transaction
        -- -----------------------------------------------------------------
        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
        START TRANSACTION;
        SET v_in_transaction = TRUE;

        -- Re-lock the prerequisite run and re-check clean-slate state inside
        -- the transaction to close the preflight/apply race.
        SELECT Id
        FROM liens_LegacyImportRuns
        WHERE Id = v_core_run_id AND TenantId = v_tenant_id AND Status = 'Completed'
        FOR UPDATE;

        SELECT COUNT(*) INTO v_existing_crosswalks
        FROM liens_LegacyIdCrosswalks
        WHERE TenantId = v_tenant_id
          AND SourceSystem = 'SL-CORE'
          AND SourceTable IN ('SL_CASE_DOCUMENT', 'SL_LIENS_MEDICAL_DOCUMENT');
        IF v_existing_crosswalks <> 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLDOC-015 concurrent document import detected';
        END IF;

        INSERT INTO liens_LegacyImportRuns (
            Id, ApprovalId, TenantId, OrgId, SourceSystem, SourceFingerprint,
            LegacyProgram, MappingVersion, MappingManifestHash,
            MappingApprovalReference, Status, StartedAtUtc, CreatedByUserId,
            SummaryJson)
        VALUES (
            v_document_run_id, NULL, v_tenant_id, v_org_id,
            'SL-CORE', LOWER(v_fingerprint), v_legacy_program,
            'sl-core-documents-sql-v1',
            SHA2('1=HicfaOrBill|6=LienAgreement|10=BillsAndRecords|11=BillsAndRecs|12=HicfaOrBill|14=Other|legacy-url-links-only', 256),
            CONCAT('Prerequisite core import ', v_core_run_id),
            'Running', UTC_TIMESTAMP(6), v_user_id,
            JSON_OBJECT(
                'importScope', 'sl-core-documents-sql-v1',
                'prerequisiteRunId', v_core_run_id,
                'storagePolicy', 'legacy-url-links-only'));

        INSERT INTO liens_ServicingItems (
            Id, TenantId, OrgId, TaskNumber, TaskType, Description,
            Status, Priority, AssignedTo, AssignedToUserId,
            CaseId, LienId, DueDate, Notes, Resolution,
            StartedAtUtc, CompletedAtUtc, EscalatedAtUtc,
            CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, UpdatedByUserId)
        SELECT
            TargetItemId, v_tenant_id, v_org_id, TaskNumber, TaskType, Description,
            'Pending', 'Normal', 'Legacy migration', v_user_id,
            TargetCaseId, TargetLienId, NULL, Notes, NULL,
            NULL, NULL, NULL,
            CreatedAtUtc, UpdatedAtUtc, v_user_id, v_user_id
        FROM tmp_sldoc_documents;
        SET v_items_inserted = ROW_COUNT();

        INSERT INTO liens_LegacyIdCrosswalks (
            Id, TenantId, SourceSystem, SourceTable, LegacyId,
            TargetEntity, TargetId, SourceHash, ImportRunId, CreatedAtUtc)
        SELECT
            UUID(), v_tenant_id, 'SL-CORE', SourceTable, CAST(LegacyId AS CHAR),
            'ServicingItem', TargetItemId, SourceHash,
            v_document_run_id, UTC_TIMESTAMP(6)
        FROM tmp_sldoc_documents;
        SET v_crosswalks_inserted = ROW_COUNT();

        SELECT COUNT(*) INTO v_postcondition_errors
        FROM tmp_sldoc_documents d
        LEFT JOIN liens_LegacyIdCrosswalks x
          ON x.TenantId = v_tenant_id
         AND x.SourceSystem = 'SL-CORE'
         AND x.SourceTable = d.SourceTable
         AND x.LegacyId = CAST(d.LegacyId AS CHAR)
         AND x.TargetEntity = 'ServicingItem'
         AND x.TargetId = d.TargetItemId
         AND x.ImportRunId = v_document_run_id
        LEFT JOIN liens_ServicingItems item
          ON item.Id = d.TargetItemId
         AND item.TenantId = v_tenant_id
         AND item.OrgId = v_org_id
         AND item.TaskNumber = d.TaskNumber
         AND item.TaskType = d.TaskType
        WHERE x.Id IS NULL OR item.Id IS NULL
            OR (d.ReferenceType = 'Case'
                AND (NOT (item.CaseId <=> d.ReferenceId) OR item.LienId IS NOT NULL))
            OR (d.ReferenceType = 'Lien'
                AND (NOT (item.LienId <=> d.ReferenceId) OR item.CaseId IS NOT NULL));
        IF v_postcondition_errors <> 0
           OR v_items_inserted <> v_total_document_count
           OR v_crosswalks_inserted <> v_total_document_count THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLDOC-016 document migration postcondition failed';
        END IF;

        UPDATE liens_LegacyImportRuns
        SET Status = 'Completed',
            CompletedAtUtc = UTC_TIMESTAMP(6),
            SummaryJson = JSON_OBJECT(
                'importScope', 'sl-core-documents-sql-v1',
                'prerequisiteRunId', v_core_run_id,
                'storagePolicy', 'legacy-url-links-only',
                'caseDocumentsInserted', v_case_document_count,
                'lienDocumentsInserted', v_lien_document_count,
                'totalDocumentsInserted', v_total_document_count)
        WHERE Id = v_document_run_id AND TenantId = v_tenant_id;
        IF ROW_COUNT() <> 1 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLDOC-017 import run completion failed';
        END IF;

        COMMIT;
        SET v_in_transaction = FALSE;

        DROP TEMPORARY TABLE IF EXISTS tmp_sldoc_documents;
        SET @@session.time_zone = v_orig_tz;
        SET v_tz_changed = FALSE;
        DO RELEASE_LOCK(v_lock_name);
        SET v_locked = 0;

        SELECT
            'document-migration-applied' AS Result,
            v_document_run_id AS DocumentImportRunId,
            v_core_run_id AS PrerequisiteCoreRunId,
            v_case_document_count AS CaseDocumentsInserted,
            v_lien_document_count AS LienDocumentsInserted,
            v_total_document_count AS TotalDocumentsInserted,
            'legacy-url-links-only' AS StoragePolicy;
    END IF;
END$$

DELIMITER ;

-- Deploy the complete file with DBeaver "Execute SQL Script" (Alt+X).
--
-- Step 1: dry run
--   CALL liens_migrate_sl_core_documents_complete('<tenant-guid>', '0');
--
-- Step 2: apply only after accepting the counts and the type-mapping result set
--   CALL liens_migrate_sl_core_documents_complete('<tenant-guid>', '1');
