-- Controlled, tenant-scoped repair for metadata-only SL-CORE settlement rows
-- omitted by completed imports that required a nonblank SLS_SETTLE_AMOUNT.
--
-- Deploy with DBeaver "Execute SQL Script" (Alt+X), not Execute Statement.
-- The procedure requires the immutable source `SL-CORE` schema and the target
-- Liens schema on the same MySQL 8 server. Preflight (`p_apply = '0'`) performs
-- no permanent writes. Apply requires the exact preflight assertions.
--
-- The repair inserts only source rows where SLS_SETTLE_AMOUNT is blank and
-- SLS_REDUCTION_AMOUNT or SLS_TOTAL_SETTLED_AMOUNT is present. Target settlement
-- rows use Amount = 0 and Status = 'Pending'. Rows with a reduction amount and
-- a valid SLS_REDUCTION_DATE also create canonical lien reductions. Rows with a
-- blank source reduction date remain preserved as settlement metadata for the
-- API fallback. A nonblank invalid source reduction date blocks the repair; the
-- repair never substitutes a settlement or workbook date.

SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;

DROP PROCEDURE IF EXISTS liens_backfill_sl_core_settlement_metadata;

DELIMITER $$

CREATE PROCEDURE liens_backfill_sl_core_settlement_metadata(
    IN p_tenant_id                       CHAR(36),
    IN p_legacy_program                  VARCHAR(50),
    IN p_approval_reference              VARCHAR(200),
    IN p_expected_source_rows            INT,
    IN p_expected_distinct_liens         INT,
    IN p_expected_blank_reduction_dates INT,
    IN p_expected_reduction_total        DECIMAL(20,4),
    IN p_expected_checksum               CHAR(64),
    IN p_apply                           CHAR(1)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id                CHAR(36);
    DECLARE v_legacy_program           VARCHAR(50);
    DECLARE v_approval_reference       VARCHAR(200);
    DECLARE v_apply                    BOOLEAN;
    DECLARE v_lock_name                VARCHAR(64);
    DECLARE v_lock_acquired            INT DEFAULT 0;
    DECLARE v_in_transaction           BOOLEAN DEFAULT FALSE;
    DECLARE v_group_concat_changed     BOOLEAN DEFAULT FALSE;
    DECLARE v_original_group_concat_len BIGINT DEFAULT 0;
    DECLARE v_target_table_count       INT DEFAULT 0;
    DECLARE v_source_table_count       INT DEFAULT 0;
    DECLARE v_core_run_count           INT DEFAULT 0;
    DECLARE v_core_run_id              CHAR(36);
    DECLARE v_org_id                   CHAR(36);
    DECLARE v_migration_user_id        CHAR(36);
    DECLARE v_source_fingerprint       CHAR(64);
    DECLARE v_provenance_count         INT DEFAULT 0;
    DECLARE v_completed_repair_runs    INT DEFAULT 0;
    DECLARE v_repair_run_id            CHAR(36);
    DECLARE v_source_rows              INT DEFAULT 0;
    DECLARE v_distinct_liens           INT DEFAULT 0;
    DECLARE v_existing_rows            INT DEFAULT 0;
    DECLARE v_rows_to_insert           INT DEFAULT 0;
    DECLARE v_rows_inserted            INT DEFAULT 0;
    DECLARE v_crosswalks_inserted      INT DEFAULT 0;
    DECLARE v_postcondition_errors     INT DEFAULT 0;
    DECLARE v_reduction_rows           INT DEFAULT 0;
    DECLARE v_blank_reduction_dates    INT DEFAULT 0;
    DECLARE v_invalid_reduction_dates  INT DEFAULT 0;
    DECLARE v_eligible_reduction_rows  INT DEFAULT 0;
    DECLARE v_existing_reductions      INT DEFAULT 0;
    DECLARE v_reductions_to_insert     INT DEFAULT 0;
    DECLARE v_reductions_inserted      INT DEFAULT 0;
    DECLARE v_reduction_crosswalks_inserted INT DEFAULT 0;
    DECLARE v_reduction_postcondition_errors INT DEFAULT 0;
    DECLARE v_reduction_total          DECIMAL(20,4) DEFAULT 0;
    DECLARE v_total_settled_total      DECIMAL(20,4) DEFAULT 0;
    DECLARE v_checksum                 CHAR(64);
    DECLARE v_mapping_manifest_hash    CHAR(64);
    DECLARE v_error_message            VARCHAR(128);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        IF v_in_transaction THEN
            ROLLBACK;
            SET v_in_transaction = FALSE;
        END IF;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_reductions_missing;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_settlement_metadata_missing;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_settlement_metadata;
        IF v_group_concat_changed THEN
            SET @@session.group_concat_max_len = v_original_group_concat_len;
        END IF;
        IF v_lock_acquired = 1 THEN
            DO RELEASE_LOCK(v_lock_name);
        END IF;
        RESIGNAL;
    END;

    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    SET v_legacy_program = TRIM(p_legacy_program);
    SET v_approval_reference = NULLIF(TRIM(p_approval_reference), '');
    SET v_apply = p_apply = '1';
    SET v_lock_name = CONCAT('liens:slcore:', v_tenant_id);

    IF v_tenant_id IS NULL
       OR v_tenant_id NOT REGEXP
          '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR v_legacy_program IS NULL
       OR CHAR_LENGTH(v_legacy_program) = 0
       OR CHAR_LENGTH(v_legacy_program) > 50
       OR p_apply IS NULL
       OR p_apply NOT IN ('0', '1') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-001 invalid tenant, program, or apply flag';
    END IF;

    IF v_apply AND (
        v_approval_reference IS NULL
        OR CHAR_LENGTH(v_approval_reference) > 200
        OR p_expected_source_rows IS NULL
        OR p_expected_source_rows < 0
        OR p_expected_distinct_liens IS NULL
        OR p_expected_distinct_liens < 0
        OR p_expected_blank_reduction_dates IS NULL
        OR p_expected_blank_reduction_dates < 0
        OR p_expected_reduction_total IS NULL
        OR p_expected_checksum IS NULL
        OR LOWER(TRIM(p_expected_checksum)) NOT REGEXP '^[0-9a-f]{64}$'
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-002 apply requires the exact preflight assertions and approval reference';
    END IF;

    SELECT GET_LOCK(v_lock_name, 10) INTO v_lock_acquired;
    IF v_lock_acquired <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-003 the tenant core-import lock is unavailable';
    END IF;

    SET v_original_group_concat_len = @@session.group_concat_max_len;
    SET @@session.group_concat_max_len = GREATEST(v_original_group_concat_len, 10485760);
    SET v_group_concat_changed = TRUE;

    SELECT COUNT(*) INTO v_target_table_count
    FROM information_schema.tables
    WHERE table_schema = DATABASE()
      AND table_type = 'BASE TABLE'
      AND table_name IN (
          'liens_Cases', 'liens_Liens', 'liens_LienSettlements',
          'liens_LienReductions',
          'liens_LegacyIdCrosswalks', 'liens_LegacyImportRuns'
      );

    SELECT COUNT(*) INTO v_source_table_count
    FROM information_schema.tables
    WHERE table_schema = 'SL-CORE'
      AND table_type = 'BASE TABLE'
      AND table_name IN (
          'SL_LIENS_SETTLEMENT', 'SL_MIGRATION_SOURCE_PROVENANCE'
      );

    IF DATABASE() NOT IN ('LS_QA_LIENS', 'LS_LIENS')
       OR v_target_table_count <> 6
       OR v_source_table_count <> 2 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-004 required target/source schema is unavailable or unapproved';
    END IF;

    SELECT COUNT(*), MAX(r.Id), MAX(r.OrgId), MAX(r.CreatedByUserId),
           MAX(LOWER(r.SourceFingerprint))
    INTO v_core_run_count, v_core_run_id, v_org_id, v_migration_user_id,
         v_source_fingerprint
    FROM liens_LegacyImportRuns r
    WHERE BINARY r.TenantId = BINARY v_tenant_id
      AND r.SourceSystem = 'SL-CORE'
      AND BINARY r.LegacyProgram = BINARY v_legacy_program
      AND r.MappingVersion = 'sl-core-core-liens-v1'
      AND r.Status = 'Completed'
      AND EXISTS (
          SELECT 1
          FROM liens_LegacyIdCrosswalks x
          WHERE BINARY x.TenantId = BINARY r.TenantId
            AND BINARY x.ImportRunId = BINARY r.Id
            AND x.SourceSystem = 'SL-CORE'
            AND x.SourceTable = 'SL_LEINS_MEDICAL'
            AND x.TargetEntity = 'Lien'
      );

    IF v_core_run_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-005 exactly one completed core import is required';
    END IF;

    SELECT COUNT(*) INTO v_provenance_count
    FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE` p
    WHERE p.PROVENANCE_KEY = 'sl-core-current'
      AND BINARY LOWER(p.SOURCE_FINGERPRINT) =
          BINARY v_source_fingerprint
      AND p.IMPORT_SCOPE = 'sl-core-core-liens-v1';

    IF v_provenance_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-006 source provenance does not match the completed import';
    END IF;

    SELECT COUNT(*) INTO v_completed_repair_runs
    FROM liens_LegacyImportRuns r
    WHERE BINARY r.TenantId = BINARY v_tenant_id
      AND r.SourceSystem = 'SL-CORE'
      AND BINARY LOWER(r.SourceFingerprint) = BINARY v_source_fingerprint
      AND BINARY r.LegacyProgram = BINARY v_legacy_program
      AND r.MappingVersion = 'sl-core-settlement-metadata-backfill-v3'
      AND r.Status = 'Completed';

    IF v_completed_repair_runs > 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-007 multiple completed settlement-metadata repairs found';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_reductions_missing;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_settlement_metadata_missing;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_settlement_metadata;

    CREATE TEMPORARY TABLE tmp_sl_core_settlement_metadata AS
    SELECT
        src.SLS_ID AS LegacySettlementId,
        UUID() AS TargetSettlementId,
        UUID() AS TargetReductionId,
        lien_x.TargetId AS TargetLienId,
        target_lien.CaseId AS TargetCaseId,
        target_lien.TenantId AS TargetLienTenantId,
        target_lien.OrgId AS TargetLienOrgId,
        target_case.Id AS ExistingTargetCaseId,
        target_case.TenantId AS TargetCaseTenantId,
        target_case.OrgId AS TargetCaseOrgId,
        src.ReductionAmountText,
        src.ReductionAmountNormalized,
        CASE
          WHEN src.ReductionDateText IS NULL THEN NULL
          WHEN src.ReductionDateFormat IS NOT NULL
           AND src.ReductionDateYear BETWEEN 1000 AND 9999
           AND src.ReductionDateMonth BETWEEN 1 AND 12
           AND src.ReductionDateDay BETWEEN 1 AND
               CASE
                 WHEN src.ReductionDateMonth = 2 THEN
                   CASE
                     WHEN MOD(src.ReductionDateYear, 400) = 0
                       OR (MOD(src.ReductionDateYear, 4) = 0
                           AND MOD(src.ReductionDateYear, 100) <> 0)
                     THEN 29
                     ELSE 28
                   END
                 WHEN src.ReductionDateMonth IN (4, 6, 9, 11) THEN 30
                 ELSE 31
               END
          THEN DATE(STR_TO_DATE(
              src.ReductionDateValueText,
              CASE src.ReductionDateFormat
                WHEN 'ymd' THEN '%Y-%m-%d'
                ELSE '%c/%e/%Y'
              END))
          ELSE NULL
        END AS ReductionDate,
        src.ReductionDateText,
        src.TotalSettledAmountText,
        src.TotalSettledAmountNormalized,
        CASE
          WHEN src.SettlementDateText IS NULL THEN NULL
          WHEN src.SettlementDateText REGEXP '^[0-9]{4}-[0-9]{2}-[0-9]{2}'
          THEN DATE(STR_TO_DATE(LEFT(src.SettlementDateText, 10), '%Y-%m-%d'))
          WHEN src.SettlementDateText REGEXP '^[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}'
          THEN DATE(STR_TO_DATE(SUBSTRING_INDEX(src.SettlementDateText, ' ', 1), '%c/%e/%Y'))
          ELSE NULL
        END AS SettlementDate,
        src.SettlementDateText,
        LEFT(CONCAT(
            'legacySettlementId=', src.SLS_ID,
            '; reductionAmount=', COALESCE(src.SLS_REDUCTION_AMOUNT, ''),
            '; reductionDate=', COALESCE(src.SLS_REDUCTION_DATE, ''),
            '; totalSettledAmount=', COALESCE(src.SLS_TOTAL_SETTLED_AMOUNT, '')),
            1000) AS ExpectedNote,
        SHA2(CONCAT_WS('|',
            src.SLS_ID, src.SLS_LIENS_ID, src.SLS_REDUCTION_AMOUNT,
            src.SLS_REDUCTION_DATE, src.SLS_SETTLE_AMOUNT,
            src.SLS_SETTLE_DATE, src.SLS_TOTAL_SETTLED_AMOUNT,
            src.SLS_PAYMENT_NUMBER, src.SLS_CREATED, src.SLS_UPDATED,
            v_source_fingerprint), 256) AS ExpectedSourceHash,
        SHA2(CONCAT_WS('|',
            'reduction-v1', src.SLS_ID, src.SLS_LIENS_ID,
            src.SLS_REDUCTION_AMOUNT, src.SLS_REDUCTION_DATE,
            v_source_fingerprint), 256) AS ExpectedReductionSourceHash,
        src.SLS_CREATED AS SourceCreatedAtUtc,
        src.SLS_UPDATED AS SourceUpdatedAtUtc,
        settlement_x.Id AS ExistingCrosswalkId,
        settlement_x.TargetEntity AS ExistingTargetEntity,
        settlement_x.TargetId AS ExistingTargetId,
        settlement_x.SourceHash AS ExistingSourceHash,
        settlement_x.ImportRunId AS ExistingImportRunId,
        target_settlement.Id AS ExistingSettlementId,
        target_settlement.TenantId AS ExistingTenantId,
        target_settlement.CaseId AS ExistingCaseId,
        target_settlement.LienId AS ExistingLienId,
        target_settlement.Amount AS ExistingAmount,
        target_settlement.SettlementDate AS ExistingSettlementDate,
        target_settlement.Status AS ExistingStatus,
        target_settlement.Note AS ExistingNote,
        target_settlement.IsDeleted AS ExistingIsDeleted,
        reduction_x.Id AS ExistingReductionCrosswalkId,
        reduction_x.TargetEntity AS ExistingReductionTargetEntity,
        reduction_x.TargetId AS ExistingReductionTargetId,
        reduction_x.SourceHash AS ExistingReductionSourceHash,
        reduction_x.ImportRunId AS ExistingReductionImportRunId,
        target_reduction.Id AS ExistingReductionId,
        target_reduction.TenantId AS ExistingReductionTenantId,
        target_reduction.CaseId AS ExistingReductionCaseId,
        target_reduction.LienId AS ExistingReductionLienId,
        target_reduction.ReductionDate AS ExistingReductionDate,
        target_reduction.Amount AS ExistingReductionAmount,
        target_reduction.Note AS ExistingReductionNote,
        target_reduction.IsDeleted AS ExistingReductionIsDeleted
    FROM (
        SELECT
            formatted.*,
            CASE formatted.ReductionDateFormat
              WHEN 'ymd' THEN CAST(SUBSTRING(formatted.ReductionDateValueText, 1, 4) AS UNSIGNED)
              WHEN 'mdy' THEN CAST(SUBSTRING_INDEX(formatted.ReductionDateValueText, '/', -1) AS UNSIGNED)
              ELSE NULL
            END AS ReductionDateYear,
            CASE formatted.ReductionDateFormat
              WHEN 'ymd' THEN CAST(SUBSTRING(formatted.ReductionDateValueText, 6, 2) AS UNSIGNED)
              WHEN 'mdy' THEN CAST(SUBSTRING_INDEX(formatted.ReductionDateValueText, '/', 1) AS UNSIGNED)
              ELSE NULL
            END AS ReductionDateMonth,
            CASE formatted.ReductionDateFormat
              WHEN 'ymd' THEN CAST(SUBSTRING(formatted.ReductionDateValueText, 9, 2) AS UNSIGNED)
              WHEN 'mdy' THEN CAST(SUBSTRING_INDEX(
                  SUBSTRING_INDEX(formatted.ReductionDateValueText, '/', 2), '/', -1) AS UNSIGNED)
              ELSE NULL
            END AS ReductionDateDay
        FROM (
            SELECT
                normalized.*,
                CASE
                  WHEN normalized.ReductionDateText REGEXP
                       '^[0-9]{4}-[0-9]{2}-[0-9]{2}([ T](0[0-9]|1[0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,6})?)?$'
                  THEN 'ymd'
                  WHEN normalized.ReductionDateText REGEXP
                       '^[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}([ T]([0-1]?[0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9])?$'
                  THEN 'mdy'
                  ELSE NULL
                END AS ReductionDateFormat,
                CASE
                  WHEN normalized.ReductionDateText REGEXP
                       '^[0-9]{4}-[0-9]{2}-[0-9]{2}([ T](0[0-9]|1[0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\\.[0-9]{1,6})?)?$'
                  THEN LEFT(normalized.ReductionDateText, 10)
                  WHEN normalized.ReductionDateText REGEXP
                       '^[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}([ T]([0-1]?[0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9])?$'
                  THEN SUBSTRING_INDEX(
                      REPLACE(normalized.ReductionDateText, 'T', ' '), ' ', 1)
                  ELSE NULL
                END AS ReductionDateValueText
            FROM (
                SELECT
                    s.*,
                    NULLIF(TRIM(CAST(s.SLS_SETTLE_AMOUNT AS CHAR)), '') AS SettlementAmountText,
                    NULLIF(TRIM(CAST(s.SLS_REDUCTION_AMOUNT AS CHAR)), '') AS ReductionAmountText,
                    NULLIF(TRIM(REPLACE(REPLACE(CAST(s.SLS_REDUCTION_AMOUNT AS CHAR), ',', ''), '$', '')), '')
                        AS ReductionAmountNormalized,
                    NULLIF(TRIM(CAST(s.SLS_REDUCTION_DATE AS CHAR)), '') AS ReductionDateText,
                    NULLIF(TRIM(CAST(s.SLS_TOTAL_SETTLED_AMOUNT AS CHAR)), '') AS TotalSettledAmountText,
                    NULLIF(TRIM(REPLACE(REPLACE(CAST(s.SLS_TOTAL_SETTLED_AMOUNT AS CHAR), ',', ''), '$', '')), '')
                        AS TotalSettledAmountNormalized,
                    NULLIF(TRIM(CAST(s.SLS_SETTLE_DATE AS CHAR)), '') AS SettlementDateText
                FROM `SL-CORE`.`SL_LIENS_SETTLEMENT` s
            ) normalized
        ) formatted
    ) src
    INNER JOIN liens_LegacyIdCrosswalks lien_x
      ON BINARY lien_x.TenantId = BINARY v_tenant_id
     AND lien_x.SourceSystem = 'SL-CORE'
     AND lien_x.SourceTable = 'SL_LEINS_MEDICAL'
     AND BINARY lien_x.LegacyId = BINARY CAST(src.SLS_LIENS_ID AS CHAR)
     AND lien_x.TargetEntity = 'Lien'
     AND BINARY lien_x.ImportRunId = BINARY v_core_run_id
    LEFT JOIN liens_Liens target_lien
      ON BINARY target_lien.Id = BINARY lien_x.TargetId
    LEFT JOIN liens_Cases target_case
      ON BINARY target_case.Id = BINARY target_lien.CaseId
    LEFT JOIN liens_LegacyIdCrosswalks settlement_x
      ON BINARY settlement_x.TenantId = BINARY v_tenant_id
     AND settlement_x.SourceSystem = 'SL-CORE'
     AND settlement_x.SourceTable = 'SL_LIENS_SETTLEMENT'
     AND BINARY settlement_x.LegacyId = BINARY CAST(src.SLS_ID AS CHAR)
    LEFT JOIN liens_LienSettlements target_settlement
      ON BINARY target_settlement.Id = BINARY settlement_x.TargetId
    LEFT JOIN liens_LegacyIdCrosswalks reduction_x
      ON BINARY reduction_x.TenantId = BINARY v_tenant_id
     AND reduction_x.SourceSystem = 'SL-CORE'
     AND reduction_x.SourceTable = 'SL_LIENS_SETTLEMENT_REDUCTION'
     AND BINARY reduction_x.LegacyId = BINARY CAST(src.SLS_ID AS CHAR)
    LEFT JOIN liens_LienReductions target_reduction
      ON BINARY target_reduction.Id = BINARY reduction_x.TargetId
    WHERE src.SettlementAmountText IS NULL
      AND (src.ReductionAmountText IS NOT NULL
           OR src.TotalSettledAmountText IS NOT NULL);

    ALTER TABLE tmp_sl_core_settlement_metadata
        ADD PRIMARY KEY (LegacySettlementId),
        ADD INDEX IX_tmp_settlement_metadata_lien (TargetLienId),
        ADD INDEX IX_tmp_settlement_metadata_crosswalk (ExistingCrosswalkId),
        ADD INDEX IX_tmp_settlement_metadata_reduction_crosswalk (ExistingReductionCrosswalkId);

    SELECT COUNT(*), COUNT(DISTINCT TargetLienId)
    INTO v_source_rows, v_distinct_liens
    FROM tmp_sl_core_settlement_metadata;

    IF v_source_rows = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-008 no metadata-only settlement rows matched the completed import';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_settlement_metadata
        WHERE TargetLienTenantId IS NULL
           OR BINARY TargetLienTenantId <> BINARY v_tenant_id
           OR BINARY TargetLienOrgId <> BINARY v_org_id
           OR TargetCaseId IS NULL
           OR ExistingTargetCaseId IS NULL
           OR BINARY TargetCaseTenantId <> BINARY v_tenant_id
           OR BINARY TargetCaseOrgId <> BINARY v_org_id
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-009 lien/case crosswalk ownership is invalid';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_settlement_metadata
        WHERE (ReductionAmountText IS NOT NULL AND (
                  ReductionAmountNormalized IS NULL
                  OR ReductionAmountNormalized NOT REGEXP '^-?[0-9]+(\\.[0-9]+)?$'
                  OR CAST(ReductionAmountNormalized AS DECIMAL(30,8))
                     NOT BETWEEN -99999999999999.9999 AND 99999999999999.9999
              ))
           OR (TotalSettledAmountText IS NOT NULL AND (
                  TotalSettledAmountNormalized IS NULL
                  OR TotalSettledAmountNormalized NOT REGEXP '^-?[0-9]+(\\.[0-9]+)?$'
                  OR CAST(TotalSettledAmountNormalized AS DECIMAL(30,8))
                     NOT BETWEEN -99999999999999.9999 AND 99999999999999.9999
              ))
           OR (SettlementDateText IS NOT NULL AND SettlementDate IS NULL)
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-010 invalid metadata amount or settlement date';
    END IF;

    SELECT
        COALESCE(SUM(ReductionDateText IS NULL), 0),
        COALESCE(SUM(
            ReductionDateText IS NOT NULL AND ReductionDate IS NULL
        ), 0)
    INTO v_blank_reduction_dates, v_invalid_reduction_dates
    FROM tmp_sl_core_settlement_metadata
    WHERE ReductionAmountText IS NOT NULL;

    IF v_invalid_reduction_dates <> 0 THEN
        SET v_error_message = CONCAT(
            'LSLTSB-017 ', v_invalid_reduction_dates,
            ' invalid nonblank SL-CORE reduction dates');
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = v_error_message;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_settlement_metadata t
        LEFT JOIN liens_LegacyImportRuns r
          ON BINARY r.Id = BINARY t.ExistingImportRunId
        WHERE t.ExistingCrosswalkId IS NOT NULL
          AND (
              BINARY t.ExistingTargetEntity <> BINARY 'LienSettlement'
              OR BINARY t.ExistingSourceHash <> BINARY t.ExpectedSourceHash
              OR r.Id IS NULL
              OR BINARY r.TenantId <> BINARY v_tenant_id
              OR BINARY r.SourceSystem <> BINARY 'SL-CORE'
              OR BINARY LOWER(r.SourceFingerprint) <> BINARY v_source_fingerprint
              OR BINARY r.LegacyProgram <> BINARY v_legacy_program
              OR BINARY r.Status <> BINARY 'Completed'
              OR r.MappingVersion NOT IN (
                  'sl-core-core-liens-v1',
                  'sl-core-settlement-metadata-backfill-v1',
                  'sl-core-settlement-metadata-backfill-v2',
                  'sl-core-settlement-metadata-backfill-v3'
              )
              OR t.ExistingTargetId IS NULL
              OR t.ExistingSettlementId IS NULL
              OR BINARY t.ExistingTenantId <> BINARY v_tenant_id
              OR BINARY t.ExistingCaseId <> BINARY t.TargetCaseId
              OR BINARY t.ExistingLienId <> BINARY t.TargetLienId
              OR t.ExistingAmount <> 0
              OR NOT (t.ExistingSettlementDate <=> t.SettlementDate)
              OR BINARY t.ExistingStatus <> BINARY 'Pending'
              OR BINARY COALESCE(t.ExistingNote, '') <> BINARY t.ExpectedNote
              OR t.ExistingIsDeleted <> 0
          )
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-011 existing settlement crosswalk or target conflicts with source';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_settlement_metadata t
        INNER JOIN liens_LienSettlements s
          ON BINARY s.TenantId = BINARY v_tenant_id
         AND BINARY s.LienId = BINARY t.TargetLienId
         AND BINARY s.Note LIKE BINARY CONCAT('legacySettlementId=', t.LegacySettlementId, ';%')
        WHERE t.ExistingCrosswalkId IS NULL
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-012 uncrosswalked target settlement already carries a source identity';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_settlement_metadata t
        LEFT JOIN liens_LegacyImportRuns r
          ON BINARY r.Id = BINARY t.ExistingReductionImportRunId
        WHERE t.ReductionAmountText IS NOT NULL
          AND t.ExistingReductionCrosswalkId IS NOT NULL
          AND (
              BINARY t.ExistingReductionTargetEntity <> BINARY 'LienReduction'
              OR BINARY t.ExistingReductionSourceHash <> BINARY t.ExpectedReductionSourceHash
              OR r.Id IS NULL
              OR BINARY r.TenantId <> BINARY v_tenant_id
              OR BINARY r.SourceSystem <> BINARY 'SL-CORE'
              OR BINARY LOWER(r.SourceFingerprint) <> BINARY v_source_fingerprint
              OR BINARY r.LegacyProgram <> BINARY v_legacy_program
              OR BINARY r.Status <> BINARY 'Completed'
              OR r.MappingVersion NOT IN (
                  'sl-core-core-liens-v1',
                  'sl-core-settlement-metadata-backfill-v2',
                  'sl-core-settlement-metadata-backfill-v3'
              )
              OR t.ExistingReductionTargetId IS NULL
              OR t.ExistingReductionId IS NULL
              OR BINARY t.ExistingReductionTenantId <> BINARY v_tenant_id
              OR BINARY t.ExistingReductionCaseId <> BINARY t.TargetCaseId
              OR BINARY t.ExistingReductionLienId <> BINARY t.TargetLienId
              OR NOT (t.ExistingReductionDate <=> t.ReductionDate)
              OR t.ExistingReductionAmount <>
                 CAST(t.ReductionAmountNormalized AS DECIMAL(20,4))
              OR BINARY COALESCE(t.ExistingReductionNote, '') <> BINARY t.ExpectedNote
              OR t.ExistingReductionIsDeleted <> 0
          )
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-013 existing reduction crosswalk or target conflicts with source';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_settlement_metadata t
        INNER JOIN liens_LienReductions r
          ON BINARY r.TenantId = BINARY v_tenant_id
         AND BINARY r.LienId = BINARY t.TargetLienId
         AND (
             BINARY r.Note LIKE BINARY CONCAT('legacySettlementId=', t.LegacySettlementId, ';%')
             OR (
                 BINARY r.CaseId = BINARY t.TargetCaseId
                 AND r.ReductionDate = t.ReductionDate
                 AND r.Amount = CAST(t.ReductionAmountNormalized AS DECIMAL(20,4))
             )
         )
        WHERE t.ReductionAmountText IS NOT NULL
          AND t.ExistingReductionCrosswalkId IS NULL
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-014 uncrosswalked target reduction already carries a source identity';
    END IF;

    SELECT COUNT(*) INTO v_existing_rows
    FROM tmp_sl_core_settlement_metadata
    WHERE ExistingCrosswalkId IS NOT NULL;

    SET v_rows_to_insert = v_source_rows - v_existing_rows;

    SELECT
        COUNT(*),
        COALESCE(SUM(
            ReductionDateText IS NOT NULL AND ReductionDate IS NOT NULL
        ), 0),
        COALESCE(SUM(
            ReductionDateText IS NOT NULL
            AND ReductionDate IS NOT NULL
            AND ExistingReductionCrosswalkId IS NOT NULL
        ), 0)
    INTO v_reduction_rows, v_eligible_reduction_rows, v_existing_reductions
    FROM tmp_sl_core_settlement_metadata
    WHERE ReductionAmountText IS NOT NULL;

    SET v_reductions_to_insert =
        v_eligible_reduction_rows - v_existing_reductions;

    SELECT
        COALESCE(SUM(CASE
            WHEN ReductionAmountNormalized IS NULL THEN 0
            ELSE CAST(ReductionAmountNormalized AS DECIMAL(20,4))
        END), 0),
        COALESCE(SUM(CASE
            WHEN TotalSettledAmountNormalized IS NULL THEN 0
            ELSE CAST(TotalSettledAmountNormalized AS DECIMAL(20,4))
        END), 0),
        LOWER(SHA2(GROUP_CONCAT(
            ExpectedSourceHash ORDER BY LegacySettlementId SEPARATOR '|'
        ), 256))
    INTO v_reduction_total, v_total_settled_total, v_checksum
    FROM tmp_sl_core_settlement_metadata;

    IF v_completed_repair_runs = 1 AND
       (v_rows_to_insert > 0 OR v_reductions_to_insert > 0) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-015 completed v3 repair exists but new missing rows were detected';
    END IF;

    IF v_apply AND (
        p_expected_source_rows <> v_source_rows
        OR p_expected_distinct_liens <> v_distinct_liens
        OR p_expected_blank_reduction_dates <> v_blank_reduction_dates
        OR p_expected_reduction_total <> v_reduction_total
        OR BINARY LOWER(TRIM(p_expected_checksum)) <> BINARY v_checksum
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'LSLTSB-016 source changed after preflight';
    END IF;

    IF NOT v_apply THEN
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_settlement_metadata;
        SET @@session.group_concat_max_len = v_original_group_concat_len;
        SET v_group_concat_changed = FALSE;
        DO RELEASE_LOCK(v_lock_name);
        SET v_lock_acquired = 0;

        SELECT
            'settlement-metadata-backfill-preflight-passed' AS Result,
            v_core_run_id AS CompletedImportRunId,
            v_source_fingerprint AS SourceFingerprint,
            v_source_rows AS SourceRows,
            v_distinct_liens AS DistinctLiens,
            v_existing_rows AS ExistingRows,
            v_rows_to_insert AS RowsToInsert,
            v_reduction_rows AS ReductionRows,
            v_blank_reduction_dates AS BlankReductionDates,
            v_invalid_reduction_dates AS InvalidReductionDates,
            v_eligible_reduction_rows AS EligibleCanonicalReductionRows,
            v_existing_reductions AS ExistingReductionRows,
            v_reductions_to_insert AS ReductionRowsToInsert,
            v_reduction_total AS ReductionTotal,
            v_total_settled_total AS TotalSettledMetadataTotal,
            v_checksum AS ExpectedChecksum;
    ELSE
        START TRANSACTION;
        SET v_in_transaction = TRUE;

        CREATE TEMPORARY TABLE tmp_sl_core_settlement_metadata_missing AS
        SELECT
            t.*,
            GREATEST(COALESCE(existing.MaxPaymentNumber, 0), 0)
              + ROW_NUMBER() OVER (
                    PARTITION BY t.TargetLienId
                    ORDER BY t.LegacySettlementId
                ) AS PaymentNumber
        FROM tmp_sl_core_settlement_metadata t
        LEFT JOIN (
            SELECT TenantId, LienId, MAX(PaymentNumber) AS MaxPaymentNumber
            FROM liens_LienSettlements
            WHERE BINARY TenantId = BINARY v_tenant_id
            GROUP BY TenantId, LienId
        ) existing
          ON BINARY existing.TenantId = BINARY v_tenant_id
         AND BINARY existing.LienId = BINARY t.TargetLienId
        WHERE t.ExistingCrosswalkId IS NULL;

        CREATE TEMPORARY TABLE tmp_sl_core_reductions_missing AS
        SELECT *
        FROM tmp_sl_core_settlement_metadata
        WHERE ReductionAmountText IS NOT NULL
          AND ReductionDateText IS NOT NULL
          AND ReductionDate IS NOT NULL
          AND ExistingReductionCrosswalkId IS NULL;

        IF EXISTS (
            SELECT 1
            FROM tmp_sl_core_settlement_metadata_missing
            WHERE PaymentNumber < 1 OR PaymentNumber > 2147483647
        ) THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLTSB-018 generated payment number is out of range';
        END IF;

        IF v_rows_to_insert > 0 OR v_reductions_to_insert > 0 THEN
            SET v_repair_run_id = UUID();
            SET v_mapping_manifest_hash = SHA2(CONCAT_WS('|',
                'sl-core-settlement-metadata-backfill-v3',
                v_tenant_id,
                v_legacy_program,
                v_source_fingerprint,
                v_source_rows,
                v_distinct_liens,
                v_reduction_total,
                v_reduction_rows,
                v_blank_reduction_dates,
                v_invalid_reduction_dates,
                v_checksum,
                v_approval_reference), 256);

            INSERT INTO liens_LegacyImportRuns (
                Id, ApprovalId, TenantId, OrgId, SourceSystem,
                SourceFingerprint, LegacyProgram, MappingVersion,
                MappingManifestHash, MappingApprovalReference, Status,
                StartedAtUtc, CreatedByUserId
            ) VALUES (
                v_repair_run_id, NULL, v_tenant_id, v_org_id, 'SL-CORE',
                v_source_fingerprint, v_legacy_program,
                'sl-core-settlement-metadata-backfill-v3',
                v_mapping_manifest_hash, v_approval_reference, 'Running',
                UTC_TIMESTAMP(6), v_migration_user_id
            );

            IF v_rows_to_insert > 0 THEN
                INSERT INTO liens_LienSettlements (
                    Id, TenantId, CaseId, LienId, PaymentNumber, Amount,
                    SettlementDate, Status, Note, IsDeleted,
                    CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, UpdatedByUserId
                )
                SELECT
                    TargetSettlementId, v_tenant_id, TargetCaseId, TargetLienId,
                    PaymentNumber, 0, SettlementDate, 'Pending', ExpectedNote, 0,
                    COALESCE(SourceCreatedAtUtc, UTC_TIMESTAMP(6)),
                    COALESCE(SourceUpdatedAtUtc, SourceCreatedAtUtc, UTC_TIMESTAMP(6)),
                    v_migration_user_id, v_migration_user_id
                FROM tmp_sl_core_settlement_metadata_missing;
                SET v_rows_inserted = ROW_COUNT();

                INSERT INTO liens_LegacyIdCrosswalks (
                    Id, TenantId, SourceSystem, SourceTable, LegacyId,
                    TargetEntity, TargetId, SourceHash, ImportRunId, CreatedAtUtc
                )
                SELECT
                    UUID(), v_tenant_id, 'SL-CORE', 'SL_LIENS_SETTLEMENT',
                    CAST(LegacySettlementId AS CHAR), 'LienSettlement',
                    TargetSettlementId, ExpectedSourceHash, v_repair_run_id,
                    UTC_TIMESTAMP(6)
                FROM tmp_sl_core_settlement_metadata_missing;
                SET v_crosswalks_inserted = ROW_COUNT();
            END IF;

            IF v_reductions_to_insert > 0 THEN
                INSERT INTO liens_LienReductions (
                    Id, TenantId, CaseId, LienId, ReductionDate, Amount,
                    Note, IsDeleted, CreatedAtUtc, UpdatedAtUtc,
                    CreatedByUserId, UpdatedByUserId
                )
                SELECT
                    TargetReductionId, v_tenant_id, TargetCaseId, TargetLienId,
                    ReductionDate,
                    CAST(ReductionAmountNormalized AS DECIMAL(18,4)),
                    ExpectedNote, 0,
                    COALESCE(SourceCreatedAtUtc, UTC_TIMESTAMP(6)),
                    COALESCE(SourceUpdatedAtUtc, SourceCreatedAtUtc, UTC_TIMESTAMP(6)),
                    v_migration_user_id, v_migration_user_id
                FROM tmp_sl_core_reductions_missing;
                SET v_reductions_inserted = ROW_COUNT();

                INSERT INTO liens_LegacyIdCrosswalks (
                    Id, TenantId, SourceSystem, SourceTable, LegacyId,
                    TargetEntity, TargetId, SourceHash, ImportRunId, CreatedAtUtc
                )
                SELECT
                    UUID(), v_tenant_id, 'SL-CORE',
                    'SL_LIENS_SETTLEMENT_REDUCTION',
                    CAST(LegacySettlementId AS CHAR), 'LienReduction',
                    TargetReductionId, ExpectedReductionSourceHash,
                    v_repair_run_id, UTC_TIMESTAMP(6)
                FROM tmp_sl_core_reductions_missing;
                SET v_reduction_crosswalks_inserted = ROW_COUNT();
            END IF;

            IF v_rows_inserted <> v_rows_to_insert
               OR v_crosswalks_inserted <> v_rows_to_insert THEN
                SIGNAL SQLSTATE '45000'
                    SET MESSAGE_TEXT = 'LSLTSB-019 settlement insert counts differ from the validated plan';
            END IF;

            IF v_reductions_inserted <> v_reductions_to_insert
               OR v_reduction_crosswalks_inserted <> v_reductions_to_insert THEN
                SIGNAL SQLSTATE '45000'
                    SET MESSAGE_TEXT = 'LSLTSB-020 reduction insert counts differ from the validated plan';
            END IF;
        END IF;

        SELECT COUNT(*) INTO v_postcondition_errors
        FROM tmp_sl_core_settlement_metadata t
        LEFT JOIN liens_LegacyIdCrosswalks x
          ON BINARY x.TenantId = BINARY v_tenant_id
         AND x.SourceSystem = 'SL-CORE'
         AND x.SourceTable = 'SL_LIENS_SETTLEMENT'
         AND BINARY x.LegacyId = BINARY CAST(t.LegacySettlementId AS CHAR)
         AND x.TargetEntity = 'LienSettlement'
         AND BINARY x.SourceHash = BINARY t.ExpectedSourceHash
        LEFT JOIN liens_LienSettlements s
          ON BINARY s.Id = BINARY x.TargetId
         AND BINARY s.TenantId = BINARY v_tenant_id
        WHERE x.Id IS NULL
           OR s.Id IS NULL
           OR BINARY s.CaseId <> BINARY t.TargetCaseId
           OR BINARY s.LienId <> BINARY t.TargetLienId
           OR s.Amount <> 0
           OR NOT (s.SettlementDate <=> t.SettlementDate)
           OR BINARY s.Status <> BINARY 'Pending'
           OR BINARY COALESCE(s.Note, '') <> BINARY t.ExpectedNote
           OR s.IsDeleted <> 0;

        IF v_postcondition_errors <> 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLTSB-021 settlement postcondition failed';
        END IF;

        SELECT COUNT(*) INTO v_reduction_postcondition_errors
        FROM tmp_sl_core_settlement_metadata t
        LEFT JOIN liens_LegacyIdCrosswalks x
          ON BINARY x.TenantId = BINARY v_tenant_id
         AND x.SourceSystem = 'SL-CORE'
         AND x.SourceTable = 'SL_LIENS_SETTLEMENT_REDUCTION'
         AND BINARY x.LegacyId = BINARY CAST(t.LegacySettlementId AS CHAR)
         AND x.TargetEntity = 'LienReduction'
         AND BINARY x.SourceHash = BINARY t.ExpectedReductionSourceHash
        LEFT JOIN liens_LienReductions r
          ON BINARY r.Id = BINARY x.TargetId
         AND BINARY r.TenantId = BINARY v_tenant_id
        WHERE t.ReductionAmountText IS NOT NULL
          AND t.ReductionDateText IS NOT NULL
          AND t.ReductionDate IS NOT NULL
          AND (
              x.Id IS NULL
              OR r.Id IS NULL
              OR BINARY r.CaseId <> BINARY t.TargetCaseId
              OR BINARY r.LienId <> BINARY t.TargetLienId
              OR r.ReductionDate <> t.ReductionDate
              OR r.Amount <> CAST(t.ReductionAmountNormalized AS DECIMAL(20,4))
              OR BINARY COALESCE(r.Note, '') <> BINARY t.ExpectedNote
              OR r.IsDeleted <> 0
          );

        IF v_reduction_postcondition_errors <> 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'LSLTSB-022 reduction postcondition failed';
        END IF;

        IF v_repair_run_id IS NOT NULL THEN
            UPDATE liens_LegacyImportRuns
            SET Status = 'Completed',
                CompletedAtUtc = UTC_TIMESTAMP(6),
                SummaryJson = JSON_OBJECT(
                    'sourceRows', v_source_rows,
                    'distinctLiens', v_distinct_liens,
                    'existingRows', v_existing_rows,
                    'rowsInserted', v_rows_inserted,
                    'reductionRows', v_reduction_rows,
                    'blankReductionDates', v_blank_reduction_dates,
                    'invalidReductionDates', v_invalid_reduction_dates,
                    'eligibleCanonicalReductionRows', v_eligible_reduction_rows,
                    'existingReductionRows', v_existing_reductions,
                    'reductionRowsInserted', v_reductions_inserted,
                    'reductionTotal', v_reduction_total,
                    'totalSettledMetadataTotal', v_total_settled_total,
                    'sourceChecksum', v_checksum,
                    'runner', 'settlement-metadata-backfill-v3'
                )
            WHERE BINARY Id = BINARY v_repair_run_id
              AND BINARY TenantId = BINARY v_tenant_id
              AND Status = 'Running';

            IF ROW_COUNT() <> 1 THEN
                SIGNAL SQLSTATE '45000'
                    SET MESSAGE_TEXT = 'LSLTSB-023 repair-run completion failed';
            END IF;
        END IF;

        COMMIT;
        SET v_in_transaction = FALSE;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_reductions_missing;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_settlement_metadata_missing;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_settlement_metadata;
        SET @@session.group_concat_max_len = v_original_group_concat_len;
        SET v_group_concat_changed = FALSE;
        DO RELEASE_LOCK(v_lock_name);
        SET v_lock_acquired = 0;

        SELECT
            CASE
              WHEN v_rows_to_insert = 0 AND v_reductions_to_insert = 0
              THEN 'settlement-metadata-backfill-already-complete'
              ELSE 'settlement-metadata-backfill-applied'
            END AS Result,
            v_core_run_id AS CompletedImportRunId,
            v_repair_run_id AS RepairImportRunId,
            v_source_rows AS SourceRows,
            v_distinct_liens AS DistinctLiens,
            v_existing_rows AS ExistingRowsBeforeApply,
            v_rows_inserted AS RowsInserted,
            v_blank_reduction_dates AS SkippedReductionRowsWithBlankDate,
            v_invalid_reduction_dates AS InvalidReductionDates,
            v_eligible_reduction_rows AS EligibleCanonicalReductionRows,
            v_existing_reductions AS ExistingReductionRowsBeforeApply,
            v_reductions_inserted AS ReductionRowsInserted,
            v_reduction_total AS ReductionTotal,
            v_total_settled_total AS TotalSettledMetadataTotal,
            v_checksum AS AppliedChecksum;
    END IF;
END$$

DELIMITER ;

-- Preflight: no permanent writes. Retain the five assertion values.
-- CALL liens_backfill_sl_core_settlement_metadata(
--   '<tenant-guid>', '1', NULL, NULL, NULL, NULL, NULL, NULL, '0');
--
-- Apply only after approval. Copy SourceRows, DistinctLiens,
-- BlankReductionDates, ReductionTotal, and ExpectedChecksum exactly from the
-- preflight result.
-- CALL liens_backfill_sl_core_settlement_metadata(
--   '<tenant-guid>', '1', '<change-or-approval-id>',
--   <source-rows>, <distinct-liens>, <blank-reduction-dates>,
--   <reduction-total>, '<checksum>', '1');
