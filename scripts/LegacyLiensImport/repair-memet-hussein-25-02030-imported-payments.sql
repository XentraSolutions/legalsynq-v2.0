-- Soft-deletes the two confirmed stale SL-CORE payment-detail artifacts for
-- Memet Hussein, case 25-02030. The imported rows describe No Recovery
-- placeholders with no receipt evidence, but their amount-to-settle value was
-- materialized as received cash.
--
-- Target UUIDs differ between QA and production. This repair therefore starts
-- from the reviewed SL-CORE case, lien, and payment-detail IDs and requires
-- exactly one tenant-scoped legacy crosswalk for every target entity. It
-- preserves the rows, notes, amounts, and crosswalks for audit and does not
-- change liens, balances, reductions, settlements, or payment numbering.
--
-- Run the complete file in DBeaver using Execute SQL Script on an explicitly
-- selected LS_QA_LIENS or approved LS_LIENS connection. Leave @apply = 0 for
-- preflight. Review every result, then copy ChangesToApply and PlanChecksum
-- into @expected_updates and @expected_checksum, set @apply = 1, and rerun.
--
-- Error/reference prefix: LSLMHP-

SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;

-- Required: replace this placeholder with the Identity user approving and
-- executing the repair.
SET @actor_user_id = '<identity-user-guid>';

-- Dry-run settings. Populate the expected assertions only for apply.
SET @apply = 0;
SET @expected_updates = -1;
SET @expected_checksum = NULL;

-- Select the tenant that belongs to the current database. The schema/tenant
-- pair is validated below so QA and production cannot be mixed accidentally.
SET @tenant_id = '019fb470-f161-7fbd-93a0-c808d43c43c3'; -- LS_QA_LIENS
-- SET @tenant_id = '019f1a05-7459-7855-b46b-110a702e37a4'; -- LS_LIENS

SET @case_number = '25-02030';
SET @legacy_case_id = '27208';
SET @target_schema = DATABASE();
SET @actor_user_id = LOWER(TRIM(@actor_user_id));
SET @apply = IF(@apply = 1, 1, 0);

SET @schema_tenant_ok =
    (@target_schema = 'LS_QA_LIENS'
     AND BINARY @tenant_id = BINARY '019fb470-f161-7fbd-93a0-c808d43c43c3')
    OR
    (@target_schema = 'LS_LIENS'
     AND BINARY @tenant_id = BINARY '019f1a05-7459-7855-b46b-110a702e37a4');

DROP TEMPORARY TABLE IF EXISTS tmp_memet_payment_repair_input;
CREATE TEMPORARY TABLE tmp_memet_payment_repair_input (
    LegacyPaymentDetailId VARCHAR(50) NOT NULL,
    LegacyLienId VARCHAR(50) NOT NULL,
    LienNumber VARCHAR(50) NOT NULL,
    ExpectedAmount DECIMAL(18,2) NOT NULL,
    ExpectedNote VARCHAR(1000) NOT NULL,
    PRIMARY KEY (LegacyPaymentDetailId),
    UNIQUE KEY UX_tmp_memet_payment_legacy_lien_id (LegacyLienId),
    UNIQUE KEY UX_tmp_memet_payment_lien_number (LienNumber)
) ENGINE=InnoDB;

INSERT INTO tmp_memet_payment_repair_input (
    LegacyPaymentDetailId,
    LegacyLienId,
    LienNumber,
    ExpectedAmount,
    ExpectedNote
) VALUES
    (
        '41411',
        '59915',
        '25-02030-02',
        1795.00,
        'legacyPaymentDetailId=41411; legacyCaseId=2720899999999999; type=; status=4; lienStatus=2; checkAmount=; netProfit='
    ),
    (
        '41412',
        '59916',
        '25-02030-01',
        1795.00,
        'legacyPaymentDetailId=41412; legacyCaseId=2720899999999999; type=; status=4; lienStatus=2; checkAmount=; netProfit='
    );

SELECT COUNT(*) INTO @input_count
FROM tmp_memet_payment_repair_input;

SELECT COUNT(*) INTO @target_table_count
FROM information_schema.tables
WHERE table_schema = @target_schema
  AND table_type = 'BASE TABLE'
  AND table_name IN (
      'liens_Cases',
      'liens_Liens',
      'liens_SettlementPaymentDetails',
      'liens_LegacyIdCrosswalks'
  );

SELECT COUNT(*) INTO @payment_column_count
FROM information_schema.columns
WHERE table_schema = @target_schema
  AND table_name = 'liens_SettlementPaymentDetails'
  AND column_name IN (
      'Id', 'TenantId', 'CaseId', 'LienId', 'PaymentNumber', 'Amount',
      'PaymentDate', 'Payee', 'CheckNumber', 'Note', 'IsDeleted',
      'CreatedAtUtc', 'UpdatedAtUtc', 'CreatedByUserId', 'UpdatedByUserId'
  );

SET @actor_user_id_ok =
    @actor_user_id <> '<identity-user-guid>'
    AND CHAR_LENGTH(@actor_user_id) = 36
    AND SUBSTRING(@actor_user_id, 9, 1) = '-'
    AND SUBSTRING(@actor_user_id, 14, 1) = '-'
    AND SUBSTRING(@actor_user_id, 19, 1) = '-'
    AND SUBSTRING(@actor_user_id, 24, 1) = '-'
    AND UNHEX(REPLACE(@actor_user_id, '-', '')) IS NOT NULL;

SET @preflight_ok =
    @schema_tenant_ok = 1
    AND @input_count = 2
    AND @target_table_count = 4
    AND @payment_column_count = 15
    AND @actor_user_id_ok = 1;

DROP TEMPORARY TABLE IF EXISTS tmp_memet_payment_repair_plan;
CREATE TEMPORARY TABLE tmp_memet_payment_repair_plan AS
SELECT
    input.LegacyPaymentDetailId,
    input.LegacyLienId,
    input.LienNumber,
    input.ExpectedAmount,
    input.ExpectedNote,
    COALESCE(case_crosswalk.CrosswalkCount, 0) AS CaseCrosswalkCount,
    case_crosswalk.TargetId AS CrosswalkCaseId,
    target_case.Id AS TargetCaseId,
    target_case.TenantId AS CaseTenantId,
    target_case.CaseNumber AS TargetCaseNumber,
    COALESCE(lien_crosswalk.CrosswalkCount, 0) AS LienCrosswalkCount,
    lien_crosswalk.TargetId AS CrosswalkLienId,
    target_lien.Id AS TargetLienId,
    target_lien.TenantId AS LienTenantId,
    target_lien.CaseId AS LienCaseId,
    target_lien.LienNumber AS TargetLienNumber,
    COALESCE(payment_crosswalk.CrosswalkCount, 0) AS PaymentCrosswalkCount,
    payment_crosswalk.TargetId AS CrosswalkPaymentId,
    payment.Id AS TargetPaymentId,
    payment.TenantId AS PaymentTenantId,
    payment.CaseId AS PaymentCaseId,
    payment.LienId AS PaymentLienId,
    payment.PaymentNumber,
    payment.Amount AS PaymentAmount,
    payment.PaymentDate,
    payment.Payee,
    payment.CheckNumber,
    payment.Note AS PaymentNote,
    payment.IsDeleted AS PaymentIsDeleted,
    payment.CreatedAtUtc AS PaymentCreatedAtUtc,
    payment.UpdatedAtUtc AS PaymentUpdatedAtUtc,
    payment.CreatedByUserId AS PaymentCreatedByUserId,
    payment.UpdatedByUserId AS PaymentUpdatedByUserId,
    CAST(NULL AS CHAR(64)) AS BlockingReason,
    0 AS NeedsUpdate
FROM tmp_memet_payment_repair_input input
LEFT JOIN (
    SELECT x.LegacyId, COUNT(*) AS CrosswalkCount, MAX(x.TargetId) AS TargetId
    FROM liens_LegacyIdCrosswalks x
    WHERE BINARY x.TenantId = BINARY @tenant_id
      AND x.SourceSystem = 'SL-CORE'
      AND x.SourceTable = 'SL_CASE'
      AND x.TargetEntity = 'Case'
      AND BINARY x.LegacyId = BINARY @legacy_case_id
    GROUP BY x.LegacyId
) case_crosswalk
  ON BINARY case_crosswalk.LegacyId = BINARY @legacy_case_id
LEFT JOIN liens_Cases target_case
  ON BINARY target_case.Id = BINARY case_crosswalk.TargetId
LEFT JOIN (
    SELECT x.LegacyId, COUNT(*) AS CrosswalkCount, MAX(x.TargetId) AS TargetId
    FROM liens_LegacyIdCrosswalks x
    WHERE BINARY x.TenantId = BINARY @tenant_id
      AND x.SourceSystem = 'SL-CORE'
      AND x.SourceTable = 'SL_LEINS_MEDICAL'
      AND x.TargetEntity = 'Lien'
    GROUP BY x.LegacyId
) lien_crosswalk
  ON BINARY lien_crosswalk.LegacyId = BINARY input.LegacyLienId
LEFT JOIN liens_Liens target_lien
  ON BINARY target_lien.Id = BINARY lien_crosswalk.TargetId
LEFT JOIN (
    SELECT x.LegacyId, COUNT(*) AS CrosswalkCount, MAX(x.TargetId) AS TargetId
    FROM liens_LegacyIdCrosswalks x
    WHERE BINARY x.TenantId = BINARY @tenant_id
      AND x.SourceSystem = 'SL-CORE'
      AND x.SourceTable = 'SL_LIENS_SETTLEMENT_PAYMENT_DETAILS'
      AND x.TargetEntity = 'SettlementPaymentDetail'
    GROUP BY x.LegacyId
) payment_crosswalk
  ON BINARY payment_crosswalk.LegacyId = BINARY input.LegacyPaymentDetailId
LEFT JOIN liens_SettlementPaymentDetails payment
  ON BINARY payment.Id = BINARY payment_crosswalk.TargetId;

ALTER TABLE tmp_memet_payment_repair_plan
    ADD PRIMARY KEY (LegacyPaymentDetailId),
    ADD KEY IX_tmp_memet_payment_repair_plan_TargetPaymentId (TargetPaymentId),
    ADD KEY IX_tmp_memet_payment_repair_plan_TargetLienId (TargetLienId);

UPDATE tmp_memet_payment_repair_plan
SET BlockingReason = CASE
        WHEN CaseCrosswalkCount <> 1 THEN 'MissingOrAmbiguousCaseCrosswalk'
        WHEN TargetCaseId IS NULL THEN 'MissingTargetCase'
        WHEN BINARY CaseTenantId <> BINARY @tenant_id
          OR BINARY TargetCaseNumber <> BINARY @case_number
            THEN 'UnexpectedTargetCase'
        WHEN LienCrosswalkCount <> 1 THEN 'MissingOrAmbiguousLienCrosswalk'
        WHEN TargetLienId IS NULL THEN 'MissingTargetLien'
        WHEN BINARY LienTenantId <> BINARY @tenant_id
          OR BINARY LienCaseId <> BINARY TargetCaseId
          OR BINARY TargetLienNumber <> BINARY LienNumber
            THEN 'UnexpectedTargetLien'
        WHEN PaymentCrosswalkCount <> 1 THEN 'MissingOrAmbiguousPaymentCrosswalk'
        WHEN TargetPaymentId IS NULL THEN 'MissingTargetPayment'
        WHEN BINARY PaymentTenantId <> BINARY @tenant_id
          OR BINARY PaymentCaseId <> BINARY TargetCaseId
          OR BINARY PaymentLienId <> BINARY TargetLienId
            THEN 'UnexpectedPaymentOwnership'
        WHEN PaymentNumber IS NULL
          OR PaymentNumber <> 1
          OR PaymentAmount IS NULL
          OR PaymentAmount <> ExpectedAmount
          OR PaymentDate IS NOT NULL
          OR Payee IS NOT NULL
          OR CheckNumber IS NOT NULL
          OR BINARY COALESCE(PaymentNote, '') <> BINARY ExpectedNote
            THEN 'UnexpectedPaymentPreimage'
        WHEN PaymentIsDeleted IS NULL
          OR PaymentIsDeleted NOT IN (0, 1)
            THEN 'InvalidDeletionState'
        ELSE NULL
    END;

UPDATE tmp_memet_payment_repair_plan
SET NeedsUpdate = CASE
        WHEN BlockingReason IS NULL AND PaymentIsDeleted = 0 THEN 1
        ELSE 0
    END;

SELECT COUNT(*) INTO @blocking_count
FROM tmp_memet_payment_repair_plan
WHERE BlockingReason IS NOT NULL;

SELECT COUNT(*) INTO @changes_to_apply
FROM tmp_memet_payment_repair_plan
WHERE BlockingReason IS NULL AND NeedsUpdate = 1;

SELECT COUNT(*) INTO @already_repaired_count
FROM tmp_memet_payment_repair_plan
WHERE BlockingReason IS NULL AND NeedsUpdate = 0 AND PaymentIsDeleted = 1;

SELECT SHA2(
    COALESCE(
        GROUP_CONCAT(
            CONCAT_WS(
                '|',
                TargetPaymentId,
                TargetCaseId,
                TargetLienId,
                LegacyPaymentDetailId,
                LegacyLienId,
                CAST(PaymentNumber AS CHAR),
                CAST(PaymentAmount AS CHAR),
                COALESCE(DATE_FORMAT(PaymentDate, '%Y-%m-%d'), ''),
                COALESCE(Payee, ''),
                COALESCE(CheckNumber, ''),
                PaymentNote,
                CAST(PaymentIsDeleted AS CHAR),
                COALESCE(DATE_FORMAT(PaymentCreatedAtUtc, '%Y-%m-%d %H:%i:%s.%f'), ''),
                COALESCE(DATE_FORMAT(PaymentUpdatedAtUtc, '%Y-%m-%d %H:%i:%s.%f'), ''),
                COALESCE(PaymentCreatedByUserId, ''),
                COALESCE(PaymentUpdatedByUserId, '')
            )
            ORDER BY TargetPaymentId
            SEPARATOR '\n'
        ),
        ''
    ),
    256
) INTO @plan_checksum
FROM tmp_memet_payment_repair_plan
WHERE BlockingReason IS NULL AND NeedsUpdate = 1;

SET @apply_permitted =
    @apply = 1
    AND @preflight_ok = 1
    AND @blocking_count = 0
    AND @expected_updates = @changes_to_apply
    AND LOWER(COALESCE(@expected_checksum, '')) = LOWER(@plan_checksum);

SELECT
    @target_schema AS TargetSchema,
    @tenant_id AS TenantId,
    @case_number AS CaseNumber,
    @legacy_case_id AS LegacyCaseId,
    @schema_tenant_ok AS SchemaTenantMatched,
    @preflight_ok AS PreflightPassed,
    @apply AS ApplyRequested,
    @apply_permitted AS ApplyPermitted,
    @changes_to_apply AS ChangesToApply,
    @already_repaired_count AS AlreadyRepaired,
    @blocking_count AS BlockingRows,
    @plan_checksum AS PlanChecksum;

SELECT
    TargetPaymentId AS PaymentId,
    TargetLienId AS LienId,
    LienNumber,
    LegacyPaymentDetailId,
    LegacyLienId,
    PaymentAmount,
    PaymentDate,
    Payee,
    CheckNumber,
    PaymentIsDeleted,
    CaseCrosswalkCount,
    LienCrosswalkCount,
    PaymentCrosswalkCount,
    BlockingReason,
    NeedsUpdate
FROM tmp_memet_payment_repair_plan
ORDER BY LienNumber;

START TRANSACTION;

SELECT COUNT(*) INTO @locked_preimages
FROM liens_SettlementPaymentDetails payment
INNER JOIN tmp_memet_payment_repair_plan plan
  ON BINARY plan.TargetPaymentId = BINARY payment.Id
WHERE @apply_permitted = 1
  AND plan.BlockingReason IS NULL
  AND plan.NeedsUpdate = 1
  AND BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY plan.TargetCaseId
  AND BINARY payment.LienId = BINARY plan.TargetLienId
  AND payment.PaymentNumber = plan.PaymentNumber
  AND payment.Amount = plan.PaymentAmount
  AND payment.PaymentDate <=> plan.PaymentDate
  AND payment.Payee <=> plan.Payee
  AND payment.CheckNumber <=> plan.CheckNumber
  AND BINARY COALESCE(payment.Note, '') = BINARY COALESCE(plan.PaymentNote, '')
  AND payment.IsDeleted = 0
  AND payment.CreatedAtUtc <=> plan.PaymentCreatedAtUtc
  AND payment.UpdatedAtUtc <=> plan.PaymentUpdatedAtUtc
  AND BINARY COALESCE(payment.CreatedByUserId, '') = BINARY COALESCE(plan.PaymentCreatedByUserId, '')
  AND BINARY COALESCE(payment.UpdatedByUserId, '') = BINARY COALESCE(plan.PaymentUpdatedByUserId, '')
FOR UPDATE;

SET @apply_permitted =
    @apply_permitted = 1
    AND @locked_preimages = @changes_to_apply;

UPDATE liens_SettlementPaymentDetails payment
INNER JOIN tmp_memet_payment_repair_plan plan
  ON BINARY plan.TargetPaymentId = BINARY payment.Id
SET payment.IsDeleted = 1,
    payment.UpdatedAtUtc = UTC_TIMESTAMP(6),
    payment.UpdatedByUserId = @actor_user_id
WHERE @apply_permitted = 1
  AND plan.BlockingReason IS NULL
  AND plan.NeedsUpdate = 1
  AND BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY plan.TargetCaseId
  AND BINARY payment.LienId = BINARY plan.TargetLienId
  AND payment.PaymentNumber = plan.PaymentNumber
  AND payment.Amount = plan.PaymentAmount
  AND payment.PaymentDate <=> plan.PaymentDate
  AND payment.Payee <=> plan.Payee
  AND payment.CheckNumber <=> plan.CheckNumber
  AND BINARY COALESCE(payment.Note, '') = BINARY COALESCE(plan.PaymentNote, '')
  AND payment.IsDeleted = 0
  AND payment.CreatedAtUtc <=> plan.PaymentCreatedAtUtc
  AND payment.UpdatedAtUtc <=> plan.PaymentUpdatedAtUtc
  AND BINARY COALESCE(payment.CreatedByUserId, '') = BINARY COALESCE(plan.PaymentCreatedByUserId, '')
  AND BINARY COALESCE(payment.UpdatedByUserId, '') = BINARY COALESCE(plan.PaymentUpdatedByUserId, '');

SET @rows_updated = ROW_COUNT();

SELECT COUNT(*) INTO @postcondition_errors
FROM liens_SettlementPaymentDetails payment
INNER JOIN tmp_memet_payment_repair_plan plan
  ON BINARY plan.TargetPaymentId = BINARY payment.Id
WHERE @apply_permitted = 1
  AND plan.BlockingReason IS NULL
  AND plan.NeedsUpdate = 1
  AND (
      payment.IsDeleted <> 1
      OR BINARY payment.TenantId <> BINARY @tenant_id
      OR BINARY payment.CaseId <> BINARY plan.TargetCaseId
      OR BINARY payment.LienId <> BINARY plan.TargetLienId
      OR payment.PaymentNumber <> plan.PaymentNumber
      OR payment.Amount <> plan.PaymentAmount
      OR NOT (payment.PaymentDate <=> plan.PaymentDate)
      OR NOT (payment.Payee <=> plan.Payee)
      OR NOT (payment.CheckNumber <=> plan.CheckNumber)
      OR BINARY COALESCE(payment.Note, '') <> BINARY COALESCE(plan.PaymentNote, '')
      OR BINARY payment.UpdatedByUserId <> BINARY @actor_user_id
  );

SET @apply_permitted =
    @apply_permitted = 1
    AND @rows_updated = @changes_to_apply
    AND @postcondition_errors = 0;

-- Commit only after every locked preimage and postcondition matches. Any
-- failed check, including a concurrent change after preflight, rolls back.
SET @transaction_end_sql = IF(@apply_permitted = 1, 'COMMIT', 'ROLLBACK');
PREPARE memet_payment_repair_transaction_end FROM @transaction_end_sql;
EXECUTE memet_payment_repair_transaction_end;
DEALLOCATE PREPARE memet_payment_repair_transaction_end;

SELECT
    @apply_permitted AS ApplyPermitted,
    @changes_to_apply AS ExpectedUpdates,
    @locked_preimages AS LockedPreimages,
    @rows_updated AS RowsUpdated,
    @postcondition_errors AS PostconditionErrors,
    CASE
        WHEN @apply = 0 THEN 'Dry run complete: no changes were written.'
        WHEN @preflight_ok <> 1 THEN 'No changes written: preflight failed.'
        WHEN @blocking_count <> 0 THEN 'No changes written: resolve blocking rows.'
        WHEN @expected_updates <> @changes_to_apply
            THEN 'No changes written: expected count does not match dry run.'
        WHEN LOWER(COALESCE(@expected_checksum, '')) <> LOWER(@plan_checksum)
            THEN 'No changes written: expected checksum does not match dry run.'
        WHEN @locked_preimages <> @changes_to_apply
            THEN 'No changes written: payment rows changed after preflight.'
        WHEN @rows_updated <> @changes_to_apply OR @postcondition_errors <> 0
            THEN 'No changes written: update or postcondition validation failed.'
        ELSE 'Applied successfully: reviewed payment artifacts were soft-deleted.'
    END AS Result;
