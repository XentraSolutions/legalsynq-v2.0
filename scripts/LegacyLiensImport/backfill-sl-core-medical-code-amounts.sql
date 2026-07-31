-- Controlled, tenant-scoped backfill for the SL-CORE Program 1 medical-code
-- amount rows omitted from the core header import.
--
-- Deploy with DBeaver "Execute SQL Script" (Alt+X), not Execute Statement.
-- The procedure requires the source `SL-CORE` schema and target `LS_QA_LIENS`
-- schema to be on the same MySQL server. It performs a dry run for p_apply =
-- '0' and inserts data only for p_apply = '1'.
--
-- This procedure never updates liens_Liens amounts or deletes crosswalks. It
-- creates deterministic LegacyMedicalCode servicing records, which the Liens
-- API uses to calculate the Billing Amount and Purchase Amount grid columns.
-- Only rows with SL_LEINS_MEDICAL_CODE.LMC_STATUS = 'A' are active source
-- data and are included in validation, totals, and servicing-item inserts.

USE LS_LIENS;

DROP PROCEDURE IF EXISTS liens_backfill_sl_core_medical_code_amounts;

DELIMITER $$

CREATE PROCEDURE liens_backfill_sl_core_medical_code_amounts(
    IN p_tenant_id CHAR(36),
    IN p_apply CHAR(1)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_id CHAR(36);
    DECLARE v_apply BOOLEAN;
    DECLARE v_lock_name VARCHAR(64);
    DECLARE v_lock_acquired INT DEFAULT 0;
    DECLARE v_in_transaction BOOLEAN DEFAULT FALSE;
    DECLARE v_table_count INT DEFAULT 0;
    DECLARE v_provenance_count INT DEFAULT 0;
    DECLARE v_completed_run_count INT DEFAULT 0;
    DECLARE v_import_run_id CHAR(36);
    DECLARE v_org_id CHAR(36);
    DECLARE v_migration_user_id CHAR(36);
    DECLARE v_source_code_count INT DEFAULT 0;
    DECLARE v_existing_backfill_count INT DEFAULT 0;
    DECLARE v_tasks_to_insert INT DEFAULT 0;
    DECLARE v_tasks_inserted INT DEFAULT 0;
    DECLARE v_total_billing DECIMAL(20,2) DEFAULT 0;
    DECLARE v_total_purchase DECIMAL(20,2) DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        IF v_in_transaction THEN
            ROLLBACK;
        END IF;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_backfill_codes;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_backfill_raw;
        IF v_lock_acquired = 1 THEN
            DO RELEASE_LOCK(v_lock_name);
        END IF;
        RESIGNAL;
    END;

    SET v_tenant_id = LOWER(TRIM(p_tenant_id));
    SET v_apply = p_apply = '1';
    SET v_lock_name = CONCAT('LSLTB:medcodes:', v_tenant_id);

    IF v_tenant_id IS NULL
       OR v_tenant_id NOT REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
       OR p_apply IS NULL
       OR p_apply NOT IN ('0', '1') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTB-001 invalid tenant ID or apply flag';
    END IF;

    SELECT GET_LOCK(v_lock_name, 10) INTO v_lock_acquired;
    IF v_lock_acquired <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTB-002 tenant backfill is already running';
    END IF;

    SELECT COUNT(*) INTO v_table_count
    FROM information_schema.tables
    WHERE (table_schema = DATABASE() AND table_name IN (
              'liens_Liens', 'liens_ServicingItems',
              'liens_LegacyIdCrosswalks', 'liens_LegacyImportRuns'))
       OR (table_schema = 'SL-CORE' AND table_name IN (
              'SL_LEINS_MEDICAL_CODE', 'SL_MIGRATION_SOURCE_PROVENANCE'));
    IF v_table_count <> 6 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTB-003 required target or source tables are unavailable';
    END IF;

    SELECT COUNT(*) INTO v_provenance_count
    FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE`
    WHERE PROVENANCE_KEY = 'sl-core-current'
      AND LOWER(SOURCE_FINGERPRINT) = '3adccecf8a38114a14cd500240aab2a4db3d9bf45f00945c659dc3b5252663fe'
      AND IMPORT_SCOPE = 'sl-core-core-liens-v1';
    IF v_provenance_count <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTB-004 source provenance does not match the approved SL-CORE dump';
    END IF;

    SELECT COUNT(*) INTO v_completed_run_count
    FROM liens_LegacyImportRuns
    WHERE TenantId = v_tenant_id
      AND SourceSystem = 'SL-CORE'
      AND SourceFingerprint = '3adccecf8a38114a14cd500240aab2a4db3d9bf45f00945c659dc3b5252663fe'
      AND LegacyProgram = '1'
      AND MappingVersion = 'sl-core-core-liens-v1'
      AND Status = 'Completed';
    IF v_completed_run_count <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTB-005 exactly one completed Program 1 core import is required';
    END IF;

    SELECT Id, OrgId, CreatedByUserId
    INTO v_import_run_id, v_org_id, v_migration_user_id
    FROM liens_LegacyImportRuns
    WHERE TenantId = v_tenant_id
      AND SourceSystem = 'SL-CORE'
      AND SourceFingerprint = '3adccecf8a38114a14cd500240aab2a4db3d9bf45f00945c659dc3b5252663fe'
      AND LegacyProgram = '1'
      AND MappingVersion = 'sl-core-core-liens-v1'
      AND Status = 'Completed';

    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_backfill_raw;
    DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_backfill_codes;

    CREATE TEMPORARY TABLE tmp_sl_core_backfill_raw AS
    SELECT
        mc.LMC_ID AS LegacyMedicalCodeId,
        x.TargetId AS TargetLienId,
        l.CaseId AS TargetCaseId,
        NULLIF(TRIM(mc.LMC_CODE), '') AS LegacyCode,
        NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_MEDICARE_COST, ',', ''), '$', '')), '') AS MedicareText,
        NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_BILLING_AMOUNT, ',', ''), '$', '')), '') AS BillingText,
        NULLIF(TRIM(REPLACE(REPLACE(mc.LMC_PURCHASE_AMOUNT, ',', ''), '$', '')), '') AS PurchaseText
    FROM `SL-CORE`.`SL_LEINS_MEDICAL_CODE` mc
    INNER JOIN liens_LegacyIdCrosswalks x
      ON x.TenantId = v_tenant_id
     AND x.SourceSystem = 'SL-CORE'
     AND x.SourceTable = 'SL_LEINS_MEDICAL'
     AND x.TargetEntity = 'Lien'
     AND x.ImportRunId = v_import_run_id
     AND x.LegacyId = CAST(mc.LMC_LM_ID AS CHAR)
    INNER JOIN liens_Liens l
      ON l.Id = x.TargetId
     AND l.TenantId = v_tenant_id
     AND l.OrgId = v_org_id
    WHERE UPPER(TRIM(COALESCE(mc.LMC_STATUS, ''))) = 'A';

    SELECT COUNT(*) INTO v_source_code_count
    FROM tmp_sl_core_backfill_raw;
    IF v_source_code_count = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTB-006 no source medical-code rows matched the completed import';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_backfill_raw
        WHERE BillingText IS NULL
           OR PurchaseText IS NULL
           OR BillingText NOT REGEXP '^[0-9]+([.][0-9]{1,2})?$'
           OR PurchaseText NOT REGEXP '^[0-9]+([.][0-9]{1,2})?$'
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTB-007 legacy medical-code amount is blank or invalid';
    END IF;

    CREATE TEMPORARY TABLE tmp_sl_core_backfill_codes AS
    SELECT
        mapped.*,
        CONCAT(
          'legacySource=SL-CORE:SL_LEINS_MEDICAL_CODE:', mapped.LegacyMedicalCodeId, '; ',
          'code=', COALESCE(mapped.SafeCode, ''), '; ',
          'description=; ',
          'medicareCost=', COALESCE(CAST(mapped.MedicareAmount AS CHAR), ''), '; ',
          'billingAmount=', CAST(mapped.BillingAmount AS CHAR), '; ',
          'purchaseAmount=', CAST(mapped.PurchaseAmount AS CHAR), '; ',
          'payee=; outboundCheckNumber=') AS ExpectedNotes
    FROM (
        SELECT
            LegacyMedicalCodeId,
            TargetLienId,
            TargetCaseId,
            CONCAT('SLCORE-LMC-', LegacyMedicalCodeId) AS TaskNumber,
            CASE
              WHEN LegacyCode IS NULL THEN NULL
              ELSE REPLACE(REPLACE(LegacyCode, ';', ' '), '=', ' ')
            END AS SafeCode,
            CASE
              WHEN MedicareText REGEXP '^[0-9]+([.][0-9]{1,2})?$' THEN CAST(MedicareText AS DECIMAL(20,2))
              ELSE NULL
            END AS MedicareAmount,
            CAST(BillingText AS DECIMAL(20,2)) AS BillingAmount,
            CAST(PurchaseText AS DECIMAL(20,2)) AS PurchaseAmount
        FROM tmp_sl_core_backfill_raw
    ) mapped;

    IF EXISTS (
        SELECT 1
        FROM tmp_sl_core_backfill_codes t
        LEFT JOIN liens_Liens l
          ON l.Id = t.TargetLienId
         AND l.TenantId = v_tenant_id
         AND l.OrgId = v_org_id
        WHERE l.Id IS NULL
           OR t.TargetCaseId IS NULL
           OR CHAR_LENGTH(t.TaskNumber) > 50
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTB-008 target lien ownership or task mapping is invalid';
    END IF;

    -- Do not blend manually entered legacy-code tasks with a deterministic
    -- restored source backfill. That would make displayed totals ambiguous.
    IF EXISTS (
        SELECT 1
        FROM liens_ServicingItems s
        INNER JOIN (SELECT DISTINCT TargetLienId FROM tmp_sl_core_backfill_codes) t
          ON t.TargetLienId = s.LienId
        WHERE s.TenantId = v_tenant_id
          AND s.TaskType = 'LegacyMedicalCode'
          AND s.TaskNumber NOT LIKE 'SLCORE-LMC-%'
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTB-009 manual legacy medical-code tasks require reconciliation';
    END IF;

    -- Existing deterministic rows are allowed only when they still point at
    -- the same target lien/case and retain their source identity marker.
    IF EXISTS (
        SELECT 1
        FROM liens_ServicingItems s
        INNER JOIN tmp_sl_core_backfill_codes t
          ON t.TaskNumber = s.TaskNumber
         AND s.TenantId = v_tenant_id
        WHERE s.TaskType <> 'LegacyMedicalCode'
           OR NOT (s.LienId <=> t.TargetLienId)
           OR NOT (s.CaseId <=> t.TargetCaseId)
           OR COALESCE(s.Notes, '') <> t.ExpectedNotes
    ) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTB-010 existing backfill task conflicts with the source mapping';
    END IF;

    SELECT COUNT(*) INTO v_existing_backfill_count
    FROM liens_ServicingItems s
    INNER JOIN tmp_sl_core_backfill_codes t
      ON t.TaskNumber = s.TaskNumber
     AND s.TenantId = v_tenant_id;

    SET v_tasks_to_insert = v_source_code_count - v_existing_backfill_count;
    SELECT COALESCE(SUM(BillingAmount), 0), COALESCE(SUM(PurchaseAmount), 0)
    INTO v_total_billing, v_total_purchase
    FROM tmp_sl_core_backfill_codes;

    IF NOT v_apply THEN
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_backfill_codes;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_backfill_raw;
        DO RELEASE_LOCK(v_lock_name);
        SET v_lock_acquired = 0;
        SELECT 'backfill-preflight-passed' AS Result,
               v_import_run_id AS CompletedImportRunId,
               v_source_code_count AS SourceMedicalCodes,
               v_existing_backfill_count AS ExistingBackfillTasks,
               v_tasks_to_insert AS TasksToInsert,
               v_total_billing AS TotalBilling,
               v_total_purchase AS TotalPurchase;
    ELSE
        START TRANSACTION;
        SET v_in_transaction = TRUE;

        INSERT INTO liens_ServicingItems
          (Id, TenantId, OrgId, TaskNumber, TaskType, Description, Status,
           Priority, AssignedTo, AssignedToUserId, CaseId, LienId, DueDate,
           Notes, Resolution, StartedAtUtc, CompletedAtUtc, EscalatedAtUtc,
           CreatedByUserId, UpdatedByUserId, CreatedAtUtc, UpdatedAtUtc)
        SELECT
          UUID(), v_tenant_id, v_org_id, t.TaskNumber, 'LegacyMedicalCode',
          CASE WHEN t.SafeCode IS NULL THEN 'Legacy medical code entry' ELSE CONCAT('Medical code ', t.SafeCode) END,
          'Pending', 'Normal', 'system', NULL, t.TargetCaseId, t.TargetLienId,
          NULL,
          t.ExpectedNotes,
          NULL, NULL, NULL, NULL,
          v_migration_user_id, v_migration_user_id, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
        FROM tmp_sl_core_backfill_codes t
        LEFT JOIN liens_ServicingItems s
          ON s.TenantId = v_tenant_id
         AND s.TaskNumber = t.TaskNumber
        WHERE s.Id IS NULL;
        SET v_tasks_inserted = ROW_COUNT();

        IF v_tasks_inserted <> v_tasks_to_insert THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LSLTB-011 insert count did not match the validated backfill plan';
        END IF;

        COMMIT;
        SET v_in_transaction = FALSE;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_backfill_codes;
        DROP TEMPORARY TABLE IF EXISTS tmp_sl_core_backfill_raw;
        DO RELEASE_LOCK(v_lock_name);
        SET v_lock_acquired = 0;
        SELECT 'backfill-applied' AS Result,
               v_import_run_id AS CompletedImportRunId,
               v_source_code_count AS SourceMedicalCodes,
               v_existing_backfill_count AS ExistingBackfillTasks,
               v_tasks_inserted AS TasksInserted,
               v_total_billing AS TotalBilling,
               v_total_purchase AS TotalPurchase;
    END IF;
END$$

DELIMITER ;

-- Run a preflight first. It performs no permanent writes.
-- CALL LS_QA_LIENS.liens_backfill_sl_core_medical_code_amounts('<tenant-guid>', '0');
--
-- Apply only after the preflight reports the expected count and totals.
-- CALL LS_QA_LIENS.liens_backfill_sl_core_medical_code_amounts('<tenant-guid>', '1');
