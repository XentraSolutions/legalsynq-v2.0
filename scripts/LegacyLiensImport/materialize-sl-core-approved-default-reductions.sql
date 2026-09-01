-- Materializes canonical lien reductions for the approved 192-row SL-CORE
-- reduction cohort after the metadata-only backfill completed successfully.
--
-- Business approval authorizes 2026-04-27 as the default ReductionDate for
-- this exact cohort. SL-CORE supplied blank reduction dates, so the canonical
-- note and crosswalk explicitly identify the date as a business-approved
-- default. This procedure does not modify SL-CORE or the preserved metadata
-- settlements.
--
-- Deploy with DBeaver "Execute SQL Script" (Alt+X) on an explicitly selected
-- LS_QA_LIENS or approved LS_LIENS connection. Run preflight first and copy
-- every returned assertion into apply.
--
-- Error prefix: LSLADR-

SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;

-- Intentionally do not drop an installed version here. If this version name
-- already exists, deployment stops without removing the reviewed procedure.
DELIMITER $$

CREATE PROCEDURE liens_materialize_sl_core_approved_default_reductions_v1(
    IN p_tenant_id                CHAR(36),
    IN p_metadata_backfill_run_id CHAR(36),
    IN p_approved_default_date    DATE,
    IN p_approval_reference       VARCHAR(200),
    IN p_expected_source_rows     INT,
    IN p_expected_distinct_liens  INT,
    IN p_expected_existing_rows   INT,
    IN p_expected_rows_to_insert  INT,
    IN p_expected_reduction_total DECIMAL(20,4),
    IN p_expected_checksum        CHAR(64),
    IN p_apply                    CHAR(1)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id                 CHAR(36);
    DECLARE v_metadata_backfill_run_id  CHAR(36);
    DECLARE v_approval_reference        VARCHAR(200);
    DECLARE v_approved_default_date     DATE;
    DECLARE v_apply                     BOOLEAN;
    DECLARE v_lock_name                 VARCHAR(64);
    DECLARE v_lock_acquired             INT DEFAULT 0;
    DECLARE v_in_transaction            BOOLEAN DEFAULT FALSE;
    DECLARE v_group_concat_changed      BOOLEAN DEFAULT FALSE;
    DECLARE v_original_group_concat_len BIGINT DEFAULT 0;
    DECLARE v_target_table_count        INT DEFAULT 0;
    DECLARE v_source_table_count        INT DEFAULT 0;
    DECLARE v_metadata_run_count        INT DEFAULT 0;
    DECLARE v_completed_repair_runs     INT DEFAULT 0;
    DECLARE v_completed_repair_run_id   CHAR(36);
    DECLARE v_provenance_count          INT DEFAULT 0;
    DECLARE v_source_fingerprint        CHAR(64);
    DECLARE v_legacy_program            VARCHAR(50);
    DECLARE v_org_id                    CHAR(36);
    DECLARE v_migration_user_id         CHAR(36);
    DECLARE v_source_rows               INT DEFAULT 0;
    DECLARE v_distinct_liens            INT DEFAULT 0;
    DECLARE v_blank_source_dates        INT DEFAULT 0;
    DECLARE v_existing_rows             INT DEFAULT 0;
    DECLARE v_rows_to_insert            INT DEFAULT 0;
    DECLARE v_reduction_total           DECIMAL(20,4) DEFAULT 0;
    DECLARE v_checksum                  CHAR(64);
    DECLARE v_repair_run_id             CHAR(36);
    DECLARE v_mapping_manifest_hash     CHAR(64);
    DECLARE v_rows_inserted             INT DEFAULT 0;
    DECLARE v_crosswalks_inserted       INT DEFAULT 0;
    DECLARE v_postcondition_errors      INT DEFAULT 0;
    DECLARE v_locked_settlement_rows    INT DEFAULT 0;
    DECLARE v_locked_lien_rows          INT DEFAULT 0;
    DECLARE v_locked_reduction_rows     INT DEFAULT 0;
    DECLARE v_unrelated_reduction_rows  INT DEFAULT 0;
    DECLARE v_post_rows                 INT DEFAULT 0;
    DECLARE v_post_distinct_liens       INT DEFAULT 0;
    DECLARE v_post_reduction_total      DECIMAL(20,4) DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        IF v_in_transaction THEN
            ROLLBACK;
            SET v_in_transaction = FALSE;
        END IF;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_approved_default_reductions;
        IF v_group_concat_changed THEN
            SET @@session.group_concat_max_len = v_original_group_concat_len;
        END IF;
        IF v_lock_acquired = 1 THEN
            DO RELEASE_LOCK(v_lock_name);
        END IF;
        RESIGNAL;
    END;

    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    SET v_metadata_backfill_run_id = LOWER(TRIM(p_metadata_backfill_run_id));
    SET v_approval_reference = TRIM(p_approval_reference);
    SET v_approved_default_date = p_approved_default_date;
    SET v_apply = p_apply = '1';
    SET v_lock_name = CONCAT('liens:slcore:', v_tenant_id);

    IF v_tenant_id IS NULL
       OR v_tenant_id NOT REGEXP
          '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR BINARY v_tenant_id <>
          BINARY '019fb470-f161-7fbd-93a0-c808d43c43c3'
       OR v_metadata_backfill_run_id IS NULL
       OR BINARY v_metadata_backfill_run_id <>
          BINARY '0ab1aa20-9e22-11f1-9a38-0a971fa4811b'
       OR v_approved_default_date IS NULL
       OR v_approved_default_date <> DATE('2026-04-27')
       OR NULLIF(v_approval_reference, '') IS NULL
       OR CHAR_LENGTH(v_approval_reference) > 200
       OR v_approval_reference NOT REGEXP '^[A-Za-z0-9][A-Za-z0-9._:/-]*$'
       OR p_apply IS NULL
       OR p_apply NOT IN ('0', '1') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-001 invalid controlled cohort, approved date, approval reference, or apply flag';
    END IF;

    IF (NOT v_apply AND (
            p_expected_source_rows IS NOT NULL
            OR p_expected_distinct_liens IS NOT NULL
            OR p_expected_existing_rows IS NOT NULL
            OR p_expected_rows_to_insert IS NOT NULL
            OR p_expected_reduction_total IS NOT NULL
            OR p_expected_checksum IS NOT NULL
        ))
       OR (v_apply AND (
            p_expected_source_rows IS NULL OR p_expected_source_rows < 0
            OR p_expected_distinct_liens IS NULL OR p_expected_distinct_liens < 0
            OR p_expected_existing_rows IS NULL OR p_expected_existing_rows < 0
            OR p_expected_rows_to_insert IS NULL OR p_expected_rows_to_insert < 0
            OR p_expected_reduction_total IS NULL
            OR LOWER(COALESCE(p_expected_checksum, '')) NOT REGEXP '^[0-9a-f]{64}$'
        )) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-002 expected assertions must be null for preflight and exact for apply';
    END IF;

    IF DATABASE() NOT IN ('LS_QA_LIENS', 'LS_LIENS') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-003 target schema must be LS_QA_LIENS or LS_LIENS';
    END IF;

    SELECT GET_LOCK(v_lock_name, 10) INTO v_lock_acquired;
    IF COALESCE(v_lock_acquired, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-004 the tenant import lock is unavailable';
    END IF;

    SET v_original_group_concat_len = @@session.group_concat_max_len;
    SET @@session.group_concat_max_len = GREATEST(v_original_group_concat_len, 10485760);
    SET v_group_concat_changed = TRUE;

    IF v_apply THEN
        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
        START TRANSACTION;
        SET v_in_transaction = TRUE;
    END IF;

    SELECT COUNT(*) INTO v_target_table_count
    FROM information_schema.tables
    WHERE table_schema = DATABASE()
      AND table_type = 'BASE TABLE'
      AND table_name IN (
          'liens_Cases', 'liens_Liens', 'liens_LienSettlements',
          'liens_LienReductions', 'liens_LegacyIdCrosswalks',
          'liens_LegacyImportRuns'
      );

    SELECT COUNT(*) INTO v_source_table_count
    FROM information_schema.tables
    WHERE table_schema = 'SL-CORE'
      AND table_type = 'BASE TABLE'
      AND table_name IN (
          'SL_LIENS_SETTLEMENT', 'SL_MIGRATION_SOURCE_PROVENANCE'
      );

    IF v_target_table_count <> 6 OR v_source_table_count <> 2 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-005 required target or SL-CORE tables are unavailable';
    END IF;

    SELECT
        COUNT(*),
        MAX(r.SourceFingerprint),
        MAX(r.LegacyProgram),
        MAX(r.OrgId),
        MAX(r.CreatedByUserId)
    INTO
        v_metadata_run_count,
        v_source_fingerprint,
        v_legacy_program,
        v_org_id,
        v_migration_user_id
    FROM liens_LegacyImportRuns r
    WHERE BINARY r.Id = BINARY v_metadata_backfill_run_id
      AND BINARY r.TenantId = BINARY v_tenant_id
      AND r.SourceSystem = 'SL-CORE'
      AND r.MappingVersion = 'sl-core-settlement-metadata-backfill-v3'
      AND r.Status = 'Completed'
      AND r.CompletedAtUtc IS NOT NULL;

    IF v_metadata_run_count <> 1
       OR NULLIF(v_source_fingerprint, '') IS NULL
       OR NULLIF(v_legacy_program, '') IS NULL
       OR v_org_id IS NULL
       OR v_migration_user_id IS NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-006 completed metadata backfill run is unavailable or invalid';
    END IF;

    SELECT COUNT(*) INTO v_provenance_count
    FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE` p
    WHERE p.PROVENANCE_KEY = 'sl-core-current'
      AND BINARY LOWER(p.SOURCE_FINGERPRINT) =
          BINARY LOWER(v_source_fingerprint)
      AND p.IMPORT_SCOPE = 'sl-core-core-liens-v1';

    IF v_provenance_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-007 SL-CORE provenance does not match the metadata backfill';
    END IF;

    SELECT COUNT(*), MAX(r.Id)
    INTO v_completed_repair_runs, v_completed_repair_run_id
    FROM liens_LegacyImportRuns r
    WHERE BINARY r.TenantId = BINARY v_tenant_id
      AND r.SourceSystem = 'SL-CORE'
      AND BINARY LOWER(r.SourceFingerprint) =
          BINARY LOWER(v_source_fingerprint)
      AND r.LegacyProgram = v_legacy_program
      AND r.MappingVersion = 'sl-core-approved-default-reduction-date-v1'
      AND r.Status = 'Completed'
      AND r.CompletedAtUtc IS NOT NULL;

    IF v_completed_repair_runs > 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-008 multiple completed approved-date repair runs found';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_approved_default_reductions;

    CREATE TEMPORARY TABLE tmp_sl_core_approved_default_reductions AS
    SELECT
        source_x.LegacyId AS LegacySettlementId,
        source_x.SourceHash AS MetadataSourceHash,
        metadata_settlement.Id AS MetadataSettlementId,
        metadata_settlement.TenantId AS MetadataTenantId,
        metadata_settlement.CaseId AS TargetCaseId,
        metadata_settlement.LienId AS TargetLienId,
        metadata_settlement.Amount AS MetadataAmount,
        metadata_settlement.Status AS MetadataStatus,
        metadata_settlement.Note AS MetadataNote,
        metadata_settlement.IsDeleted AS MetadataIsDeleted,
        target_lien.TenantId AS LienTenantId,
        target_lien.OrgId AS LienOrgId,
        target_lien.CaseId AS LienCaseId,
        target_case.TenantId AS CaseTenantId,
        target_case.OrgId AS CaseOrgId,
        source_row.SourceSettlementAmountText,
        source_row.SourceReductionDateText,
        source_row.ReductionAmountText,
        source_row.ReductionAmount,
        source_row.ExpectedMetadataNote,
        v_approved_default_date AS ApprovedReductionDate,
        UUID() AS TargetReductionId,
        LEFT(CONCAT(
            'sourceLegacySettlementId=', source_x.LegacyId,
            '; reductionDateSource=business-approved-default',
            '; approvedDefaultDate=', DATE_FORMAT(v_approved_default_date, '%Y-%m-%d'),
            '; approvalReference=', v_approval_reference,
            '; authoritativeSourceReductionDate=<blank>',
            '; metadataBackfillRunId=', v_metadata_backfill_run_id
        ), 1000) AS ExpectedReductionNote,
        LOWER(SHA2(CONCAT_WS(
            '|',
            'approved-default-reduction-date-v1',
            source_x.SourceHash,
            source_x.LegacyId,
            metadata_settlement.CaseId,
            metadata_settlement.LienId,
            CAST(source_row.ReductionAmount AS DECIMAL(20,4)),
            DATE_FORMAT(v_approved_default_date, '%Y-%m-%d'),
            v_approval_reference,
            v_source_fingerprint
        ), 256)) AS ExpectedReductionSourceHash,
        approved_x.Id AS ExistingCrosswalkId,
        approved_x.TargetEntity AS ExistingTargetEntity,
        approved_x.TargetId AS ExistingTargetId,
        approved_x.SourceHash AS ExistingSourceHash,
        approved_x.ImportRunId AS ExistingImportRunId,
        existing_reduction.Id AS ExistingReductionId,
        existing_reduction.TenantId AS ExistingReductionTenantId,
        existing_reduction.CaseId AS ExistingReductionCaseId,
        existing_reduction.LienId AS ExistingReductionLienId,
        existing_reduction.ReductionDate AS ExistingReductionDate,
        existing_reduction.Amount AS ExistingReductionAmount,
        existing_reduction.Note AS ExistingReductionNote,
        existing_reduction.IsDeleted AS ExistingReductionIsDeleted,
        existing_run.MappingVersion AS ExistingRunMappingVersion,
        existing_run.Status AS ExistingRunStatus,
        existing_run.MappingApprovalReference AS ExistingRunApprovalReference
    FROM liens_LegacyIdCrosswalks source_x
    INNER JOIN liens_LienSettlements metadata_settlement
      ON BINARY metadata_settlement.Id = BINARY source_x.TargetId
    INNER JOIN (
        SELECT
            normalized.SLS_ID,
            normalized.SourceSettlementAmountText,
            normalized.SourceReductionDateText,
            normalized.ReductionAmountText,
            CASE
              WHEN normalized.ReductionAmountText REGEXP
                   '^[0-9]+(\\.[0-9]{1,4})?$'
              THEN CAST(normalized.ReductionAmountText AS DECIMAL(20,4))
              ELSE NULL
            END AS ReductionAmount,
            LEFT(CONCAT(
                'legacySettlementId=', normalized.SLS_ID,
                '; reductionAmount=', COALESCE(normalized.SLS_REDUCTION_AMOUNT, ''),
                '; reductionDate=', COALESCE(normalized.SLS_REDUCTION_DATE, ''),
                '; totalSettledAmount=', COALESCE(normalized.SLS_TOTAL_SETTLED_AMOUNT, '')
            ), 1000) AS ExpectedMetadataNote
        FROM (
            SELECT
                s.*,
                NULLIF(TRIM(CAST(s.SLS_SETTLE_AMOUNT AS CHAR)), '')
                    AS SourceSettlementAmountText,
                NULLIF(TRIM(CAST(s.SLS_REDUCTION_DATE AS CHAR)), '')
                    AS SourceReductionDateText,
                NULLIF(TRIM(REPLACE(REPLACE(
                    CAST(s.SLS_REDUCTION_AMOUNT AS CHAR), ',', ''), '$', '')), '')
                    AS ReductionAmountText
            FROM `SL-CORE`.`SL_LIENS_SETTLEMENT` s
        ) normalized
    ) source_row
      ON BINARY CAST(source_row.SLS_ID AS CHAR) = BINARY source_x.LegacyId
    INNER JOIN liens_Liens target_lien
      ON BINARY target_lien.Id = BINARY metadata_settlement.LienId
    INNER JOIN liens_Cases target_case
      ON BINARY target_case.Id = BINARY metadata_settlement.CaseId
    LEFT JOIN liens_LegacyIdCrosswalks approved_x
      ON BINARY approved_x.TenantId = BINARY v_tenant_id
     AND approved_x.SourceSystem = 'SL-CORE'
     AND approved_x.SourceTable =
         'SL_LIENS_SETTLEMENT_REDUCTION_APPROVED_DEFAULT_DATE'
     AND BINARY approved_x.LegacyId = BINARY source_x.LegacyId
    LEFT JOIN liens_LienReductions existing_reduction
      ON BINARY existing_reduction.Id = BINARY approved_x.TargetId
    LEFT JOIN liens_LegacyImportRuns existing_run
      ON BINARY existing_run.Id = BINARY approved_x.ImportRunId
    WHERE BINARY source_x.TenantId = BINARY v_tenant_id
      AND BINARY source_x.ImportRunId = BINARY v_metadata_backfill_run_id
      AND source_x.SourceSystem = 'SL-CORE'
      AND source_x.SourceTable = 'SL_LIENS_SETTLEMENT'
      AND source_x.TargetEntity = 'LienSettlement';

    SELECT
        COUNT(*),
        COUNT(DISTINCT TargetLienId),
        COALESCE(SUM(SourceReductionDateText IS NULL), 0)
    INTO v_source_rows, v_distinct_liens, v_blank_source_dates
    FROM tmp_sl_core_approved_default_reductions;

    IF v_source_rows <> 192
       OR v_distinct_liens <> 192
       OR v_blank_source_dates <> 192 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-009 controlled cohort must remain 192 rows, 192 liens, and 192 blank source dates';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_approved_default_reductions t
        WHERE t.MetadataTenantId IS NULL
           OR BINARY t.MetadataTenantId <> BINARY v_tenant_id
           OR t.MetadataAmount <> 0
           OR t.MetadataStatus <> 'Pending'
           OR t.MetadataIsDeleted <> 0
           OR BINARY t.LienTenantId <> BINARY v_tenant_id
           OR BINARY t.CaseTenantId <> BINARY v_tenant_id
           OR BINARY t.LienOrgId <> BINARY v_org_id
           OR BINARY t.CaseOrgId <> BINARY v_org_id
           OR BINARY t.LienCaseId <> BINARY t.TargetCaseId
           OR t.SourceSettlementAmountText IS NOT NULL
           OR t.SourceReductionDateText IS NOT NULL
           OR t.ReductionAmountText IS NULL
           OR t.ReductionAmount IS NULL
           OR t.ReductionAmount <= 0
           OR t.ReductionAmount > 99999999999999.9999
           OR BINARY COALESCE(t.MetadataNote, '') <>
              BINARY t.ExpectedMetadataNote
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-010 source, metadata settlement, lien, or case validation failed';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_approved_default_reductions t
        WHERE t.ExistingCrosswalkId IS NOT NULL
          AND (
              t.ExistingTargetEntity <> 'LienReduction'
              OR BINARY t.ExistingSourceHash <>
                 BINARY t.ExpectedReductionSourceHash
              OR t.ExistingTargetId IS NULL
              OR t.ExistingReductionId IS NULL
              OR BINARY t.ExistingReductionTenantId <> BINARY v_tenant_id
              OR BINARY t.ExistingReductionCaseId <> BINARY t.TargetCaseId
              OR BINARY t.ExistingReductionLienId <> BINARY t.TargetLienId
              OR t.ExistingReductionDate <> t.ApprovedReductionDate
              OR t.ExistingReductionAmount <> t.ReductionAmount
              OR BINARY COALESCE(t.ExistingReductionNote, '') <>
                 BINARY t.ExpectedReductionNote
              OR t.ExistingReductionIsDeleted <> 0
              OR t.ExistingRunMappingVersion <>
                 'sl-core-approved-default-reduction-date-v1'
              OR t.ExistingRunStatus <> 'Completed'
              OR BINARY t.ExistingRunApprovalReference <>
                 BINARY v_approval_reference
              OR v_completed_repair_runs <> 1
              OR BINARY t.ExistingImportRunId <>
                 BINARY v_completed_repair_run_id
          )
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-011 existing approved-date crosswalk or reduction conflicts';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_approved_default_reductions t
        INNER JOIN liens_LienReductions r
          ON BINARY r.TenantId = BINARY v_tenant_id
         AND BINARY r.LienId = BINARY t.TargetLienId
         AND r.IsDeleted = 0
        WHERE t.ExistingReductionId IS NULL
           OR BINARY r.Id <> BINARY t.ExistingReductionId
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-012 a cohort lien already has an unrelated canonical reduction';
    END IF;

    SELECT COUNT(*) INTO v_existing_rows
    FROM tmp_sl_core_approved_default_reductions
    WHERE ExistingCrosswalkId IS NOT NULL;

    IF v_existing_rows > 0 AND v_completed_repair_runs <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-013 existing approved-date rows require one completed repair run';
    END IF;

    SET v_rows_to_insert = v_source_rows - v_existing_rows;

    SELECT
        COALESCE(SUM(ReductionAmount), 0),
        LOWER(SHA2(GROUP_CONCAT(
            ExpectedReductionSourceHash
            ORDER BY LegacySettlementId SEPARATOR '|'
        ), 256))
    INTO v_reduction_total, v_checksum
    FROM tmp_sl_core_approved_default_reductions;

    IF v_reduction_total <> 467303.5100 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-014 controlled reduction total must remain 467303.5100';
    END IF;

    IF v_completed_repair_runs = 1 AND v_rows_to_insert <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-015 completed approved-date repair is missing derived rows';
    END IF;

    IF v_apply AND (
        p_expected_source_rows <> v_source_rows
        OR p_expected_distinct_liens <> v_distinct_liens
        OR p_expected_existing_rows <> v_existing_rows
        OR p_expected_rows_to_insert <> v_rows_to_insert
        OR p_expected_reduction_total <> v_reduction_total
        OR BINARY LOWER(p_expected_checksum) <> BINARY v_checksum
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLADR-016 validated cohort changed after preflight';
    END IF;

    IF NOT v_apply THEN
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_approved_default_reductions;
        SET @@session.group_concat_max_len = v_original_group_concat_len;
        SET v_group_concat_changed = FALSE;
        DO RELEASE_LOCK(v_lock_name);
        SET v_lock_acquired = 0;

        SELECT
            'approved-default-reductions-preflight-passed' AS Result,
            v_metadata_backfill_run_id AS MetadataBackfillRunId,
            v_source_fingerprint AS SourceFingerprint,
            v_approved_default_date AS ApprovedDefaultReductionDate,
            v_approval_reference AS ApprovalReference,
            v_source_rows AS SourceRows,
            v_distinct_liens AS DistinctLiens,
            v_blank_source_dates AS BlankSourceReductionDates,
            v_existing_rows AS ExistingRows,
            v_rows_to_insert AS RowsToInsert,
            v_reduction_total AS ReductionTotal,
            v_checksum AS ExpectedChecksum;
    ELSE
        SELECT COUNT(*) INTO v_locked_settlement_rows
        FROM liens_LienSettlements s
        INNER JOIN tmp_sl_core_approved_default_reductions t
          ON BINARY t.MetadataSettlementId = BINARY s.Id
        FOR UPDATE;

        SELECT COUNT(*) INTO v_locked_lien_rows
        FROM liens_Liens l
        INNER JOIN tmp_sl_core_approved_default_reductions t
          ON BINARY t.TargetLienId = BINARY l.Id
        FOR UPDATE;

        IF v_locked_settlement_rows <> v_source_rows
           OR v_locked_lien_rows <> v_source_rows THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLADR-017 controlled settlement or lien lock count changed';
        END IF;

        -- Lock the tenant's indexed reduction range, including its insertion
        -- gaps, so a normal concurrent writer cannot add a reduction between
        -- the conflict check and this transaction's inserts.
        SELECT COUNT(*) INTO v_locked_reduction_rows
        FROM liens_LienReductions r
             FORCE INDEX (IX_liens_LienReductions_TenantId_LienId)
        WHERE r.TenantId =
              CONVERT(v_tenant_id USING ascii) COLLATE ascii_general_ci
        FOR UPDATE;

        SELECT COUNT(*) INTO v_unrelated_reduction_rows
        FROM tmp_sl_core_approved_default_reductions t
        INNER JOIN liens_LienReductions r
          ON BINARY r.TenantId = BINARY v_tenant_id
         AND BINARY r.LienId = BINARY t.TargetLienId
         AND r.IsDeleted = 0
        WHERE t.ExistingReductionId IS NULL
           OR BINARY r.Id <> BINARY t.ExistingReductionId;

        IF v_unrelated_reduction_rows <> 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLADR-018 a cohort lien gained an unrelated canonical reduction';
        END IF;

        IF v_rows_to_insert > 0 THEN
            SET v_repair_run_id = UUID();
            SET v_mapping_manifest_hash = LOWER(SHA2(CONCAT_WS(
                '|',
                'sl-core-approved-default-reduction-date-v1',
                v_tenant_id,
                v_metadata_backfill_run_id,
                DATE_FORMAT(v_approved_default_date, '%Y-%m-%d'),
                v_approval_reference,
                v_source_fingerprint,
                v_source_rows,
                v_distinct_liens,
                v_reduction_total,
                v_checksum
            ), 256));

            INSERT INTO liens_LegacyImportRuns (
                Id, ApprovalId, TenantId, OrgId, SourceSystem,
                SourceFingerprint, LegacyProgram, MappingVersion,
                MappingManifestHash, MappingApprovalReference, Status,
                StartedAtUtc, CreatedByUserId
            ) VALUES (
                v_repair_run_id, NULL, v_tenant_id, v_org_id, 'SL-CORE',
                v_source_fingerprint, v_legacy_program,
                'sl-core-approved-default-reduction-date-v1',
                v_mapping_manifest_hash, v_approval_reference, 'Running',
                UTC_TIMESTAMP(6), v_migration_user_id
            );

            INSERT INTO liens_LienReductions (
                Id, TenantId, CaseId, LienId, ReductionDate, Amount,
                Note, IsDeleted, CreatedAtUtc, UpdatedAtUtc,
                CreatedByUserId, UpdatedByUserId
            )
            SELECT
                t.TargetReductionId,
                v_tenant_id,
                t.TargetCaseId,
                t.TargetLienId,
                t.ApprovedReductionDate,
                t.ReductionAmount,
                t.ExpectedReductionNote,
                0,
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6),
                v_migration_user_id,
                v_migration_user_id
            FROM tmp_sl_core_approved_default_reductions t
            WHERE t.ExistingCrosswalkId IS NULL;
            SET v_rows_inserted = ROW_COUNT();

            INSERT INTO liens_LegacyIdCrosswalks (
                Id, TenantId, SourceSystem, SourceTable, LegacyId,
                TargetEntity, TargetId, SourceHash, ImportRunId, CreatedAtUtc
            )
            SELECT
                UUID(),
                v_tenant_id,
                'SL-CORE',
                'SL_LIENS_SETTLEMENT_REDUCTION_APPROVED_DEFAULT_DATE',
                t.LegacySettlementId,
                'LienReduction',
                t.TargetReductionId,
                t.ExpectedReductionSourceHash,
                v_repair_run_id,
                UTC_TIMESTAMP(6)
            FROM tmp_sl_core_approved_default_reductions t
            WHERE t.ExistingCrosswalkId IS NULL;
            SET v_crosswalks_inserted = ROW_COUNT();

            IF v_rows_inserted <> v_rows_to_insert
               OR v_crosswalks_inserted <> v_rows_to_insert THEN
                SIGNAL SQLSTATE '45000'
                    SET MESSAGE_TEXT = 'LSLADR-019 insert counts differ from the approved plan';
            END IF;
        END IF;

        SELECT COUNT(*) INTO v_postcondition_errors
        FROM tmp_sl_core_approved_default_reductions t
        LEFT JOIN liens_LegacyIdCrosswalks x
          ON BINARY x.TenantId = BINARY v_tenant_id
         AND x.SourceSystem = 'SL-CORE'
         AND x.SourceTable =
             'SL_LIENS_SETTLEMENT_REDUCTION_APPROVED_DEFAULT_DATE'
         AND BINARY x.LegacyId = BINARY t.LegacySettlementId
         AND x.TargetEntity = 'LienReduction'
         AND BINARY x.SourceHash = BINARY t.ExpectedReductionSourceHash
        LEFT JOIN liens_LienReductions r
          ON BINARY r.Id = BINARY x.TargetId
         AND BINARY r.TenantId = BINARY v_tenant_id
        WHERE x.Id IS NULL
           OR r.Id IS NULL
           OR BINARY r.CaseId <> BINARY t.TargetCaseId
           OR BINARY r.LienId <> BINARY t.TargetLienId
           OR r.ReductionDate <> t.ApprovedReductionDate
           OR r.Amount <> t.ReductionAmount
           OR BINARY COALESCE(r.Note, '') <>
              BINARY t.ExpectedReductionNote
           OR r.IsDeleted <> 0;

        IF v_postcondition_errors <> 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLADR-020 approved-date reduction postcondition failed';
        END IF;

        SELECT
            COUNT(*),
            COUNT(DISTINCT r.LienId),
            COALESCE(SUM(r.Amount), 0)
        INTO
            v_post_rows,
            v_post_distinct_liens,
            v_post_reduction_total
        FROM tmp_sl_core_approved_default_reductions t
        INNER JOIN liens_LegacyIdCrosswalks x
          ON BINARY x.TenantId = BINARY v_tenant_id
         AND x.SourceSystem = 'SL-CORE'
         AND x.SourceTable =
             'SL_LIENS_SETTLEMENT_REDUCTION_APPROVED_DEFAULT_DATE'
         AND BINARY x.LegacyId = BINARY t.LegacySettlementId
         AND x.TargetEntity = 'LienReduction'
         AND BINARY x.SourceHash = BINARY t.ExpectedReductionSourceHash
        INNER JOIN liens_LienReductions r
          ON BINARY r.Id = BINARY x.TargetId
         AND BINARY r.TenantId = BINARY v_tenant_id
         AND r.IsDeleted = 0;

        SELECT COUNT(*) INTO v_unrelated_reduction_rows
        FROM tmp_sl_core_approved_default_reductions t
        INNER JOIN liens_LienReductions r
          ON BINARY r.TenantId = BINARY v_tenant_id
         AND BINARY r.LienId = BINARY t.TargetLienId
         AND r.IsDeleted = 0
        LEFT JOIN liens_LegacyIdCrosswalks x
          ON BINARY x.TenantId = BINARY v_tenant_id
         AND x.SourceSystem = 'SL-CORE'
         AND x.SourceTable =
             'SL_LIENS_SETTLEMENT_REDUCTION_APPROVED_DEFAULT_DATE'
         AND BINARY x.LegacyId = BINARY t.LegacySettlementId
         AND x.TargetEntity = 'LienReduction'
         AND BINARY x.TargetId = BINARY r.Id
         AND BINARY x.SourceHash = BINARY t.ExpectedReductionSourceHash
        WHERE x.Id IS NULL;

        IF v_post_rows <> 192
           OR v_post_distinct_liens <> 192
           OR v_post_reduction_total <> 467303.5100
           OR v_unrelated_reduction_rows <> 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLADR-021 approved cohort count, total, or exclusivity postcondition failed';
        END IF;

        IF v_repair_run_id IS NOT NULL THEN
            UPDATE liens_LegacyImportRuns
            SET Status = 'Completed',
                CompletedAtUtc = UTC_TIMESTAMP(6),
                SummaryJson = JSON_OBJECT(
                    'metadataBackfillRunId', v_metadata_backfill_run_id,
                    'approvedDefaultReductionDate',
                        DATE_FORMAT(v_approved_default_date, '%Y-%m-%d'),
                    'approvalReference', v_approval_reference,
                    'sourceRows', v_source_rows,
                    'distinctLiens', v_distinct_liens,
                    'blankSourceReductionDates', v_blank_source_dates,
                    'existingRows', v_existing_rows,
                    'rowsInserted', v_rows_inserted,
                    'reductionTotal', v_reduction_total,
                    'sourceChecksum', v_checksum,
                    'runner', 'approved-default-reduction-date-v1'
                )
            WHERE BINARY Id = BINARY v_repair_run_id
              AND BINARY TenantId = BINARY v_tenant_id
              AND Status = 'Running';

            IF ROW_COUNT() <> 1 THEN
                SIGNAL SQLSTATE '45000'
                    SET MESSAGE_TEXT = 'LSLADR-022 repair-run completion failed';
            END IF;
        END IF;

        COMMIT;
        SET v_in_transaction = FALSE;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_approved_default_reductions;
        SET @@session.group_concat_max_len = v_original_group_concat_len;
        SET v_group_concat_changed = FALSE;
        DO RELEASE_LOCK(v_lock_name);
        SET v_lock_acquired = 0;

        SELECT
            CASE
              WHEN v_rows_to_insert = 0
              THEN 'approved-default-reductions-already-complete'
              ELSE 'approved-default-reductions-applied'
            END AS Result,
            v_repair_run_id AS RepairImportRunId,
            v_approved_default_date AS ApprovedDefaultReductionDate,
            v_source_rows AS SourceRows,
            v_distinct_liens AS DistinctLiens,
            v_existing_rows AS ExistingRowsBeforeApply,
            v_rows_inserted AS RowsInserted,
            v_reduction_total AS ReductionTotal,
            v_checksum AS AppliedChecksum;
    END IF;
END$$

DELIMITER ;

-- Preflight: replace the approval reference with the real approved change ID.
-- CALL liens_materialize_sl_core_approved_default_reductions_v1(
--   '019fb470-f161-7fbd-93a0-c808d43c43c3',
--   '0ab1aa20-9e22-11f1-9a38-0a971fa4811b',
--   '2026-04-27',
--   '<change-or-approval-id>',
--   NULL, NULL, NULL, NULL, NULL, NULL,
--   '0');
--
-- Apply: copy all six assertion values from the immediately preceding
-- preflight result. Do not use stale or sample values.
-- CALL liens_materialize_sl_core_approved_default_reductions_v1(
--   '019fb470-f161-7fbd-93a0-c808d43c43c3',
--   '0ab1aa20-9e22-11f1-9a38-0a971fa4811b',
--   '2026-04-27',
--   '<same-change-or-approval-id>',
--   <source-rows>, <distinct-liens>, <existing-rows>, <rows-to-insert>,
--   <reduction-total>, '<checksum>',
--   '1');
