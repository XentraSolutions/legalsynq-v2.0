-- Emergency rollback for repair-darin-tellis-25-01967-lien-05.sql.
--
-- This reverses the reviewed repair's three business-data changes. It restores
-- the malformed imported $16,000 payment, restores the zero-dollar record's
-- No Recovery metadata, and restores lien 25-01967-05 to a $16,000 current
-- balance with no payoff amount. The lien remains closed/Settled.
--
-- WARNING: this intentionally restores the incorrect pre-repair UI behavior.
-- Use only as an approved emergency rollback. The script refuses to apply if
-- the three repaired rows no longer share the exact repair timestamp and actor,
-- which protects later legitimate edits from being overwritten.
--
-- Run the complete file in DBeaver on an explicitly selected LS_LIENS
-- connection. Leave @apply = 0 for preflight. Review every result, then copy
-- ChangesToApply and PlanChecksum into @expected_updates and
-- @expected_checksum, set @apply = 1, and rerun the complete file.
--
-- Error/reference prefix: LSLDTR-RB-

SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;

-- Required: replace this single placeholder with the active Identity user
-- approving and executing the emergency rollback.
SET @rollback_actor_user_id = '<identity-user-guid>';

SET @apply = 0;
SET @expected_updates = -1;
SET @expected_checksum = NULL;

SET @target_schema = DATABASE();
SET @tenant_id = '019f1a05-7459-7855-b46b-110a702e37a4';
SET @org_id = '019f1a05-792b-7c0e-89a0-ae24990d1f89';
SET @case_id = '6da5cccd-9d64-11f1-b823-12a7a8afef43';
SET @case_number = '25-01967';
SET @lien_id = '6e58f0ee-9d64-11f1-b823-12a7a8afef43';
SET @lien_number = '25-01967-05';
SET @bad_payment_id = '72acb13c-9d64-11f1-b823-12a7a8afef43';
SET @zero_payment_id = '01a02b21-5580-772f-af1a-a18f5d996a5c';
SET @import_run_id = '72ff0756-9d64-11f1-b823-12a7a8afef43';
SET @migration_user_id = '019f1a05-792f-74f2-b071-4fdc0d6bd30a';

SET @bad_payment_note =
    'legacyPaymentDetailId=41970; legacyCaseId=309849999999999; type=; status=4; lienStatus=2; checkAmount=; netProfit=';
SET @original_zero_payment_note = CONCAT(
    'no recovery on tis lien as payment is already processed previously',
    CHAR(10), '[legacy-meta]', CHAR(10),
    'netProfit=0.00; type=other; status=4'
);
SET @repaired_zero_payment_note = CONCAT(
    'Lien closed with zero payment; corrected by reviewed repair.',
    CHAR(10), '[legacy-meta]', CHAR(10),
    'netProfit=0.00; type=other'
);

SET @rollback_actor_user_id = LOWER(TRIM(@rollback_actor_user_id));
SET @apply = IF(@apply = 1, 1, 0);
SET @schema_ok = BINARY @target_schema = BINARY 'LS_LIENS';
SET @actor_guid_ok =
    @rollback_actor_user_id <> ''
    AND CHAR_LENGTH(@rollback_actor_user_id) = 36
    AND SUBSTRING(@rollback_actor_user_id, 9, 1) = '-'
    AND SUBSTRING(@rollback_actor_user_id, 14, 1) = '-'
    AND SUBSTRING(@rollback_actor_user_id, 19, 1) = '-'
    AND SUBSTRING(@rollback_actor_user_id, 24, 1) = '-'
    AND UNHEX(REPLACE(@rollback_actor_user_id, '-', '')) IS NOT NULL;

SELECT EXISTS (
    SELECT 1
    FROM LS_IDENTITY.idt_Users identity_user
    INNER JOIN LS_IDENTITY.idt_UserTenants user_tenant
      ON BINARY user_tenant.UserId = BINARY identity_user.Id
     AND BINARY user_tenant.TenantId = BINARY @tenant_id
     AND user_tenant.IsActive = 1
    WHERE BINARY identity_user.Id = BINARY @rollback_actor_user_id
      AND identity_user.IsActive = 1
) INTO @actor_tenant_ok;

SELECT COUNT(*) INTO @required_table_count
FROM information_schema.tables
WHERE table_schema = @target_schema
  AND table_type = 'BASE TABLE'
  AND table_name IN (
      'liens_Cases',
      'liens_Liens',
      'liens_SettlementPaymentDetails',
      'liens_LienSettlements',
      'liens_LienReductions',
      'liens_LegacyIdCrosswalks'
  );

SELECT COUNT(*) INTO @matched_crosswalk_count
FROM liens_LegacyIdCrosswalks crosswalk
WHERE BINARY crosswalk.TenantId = BINARY @tenant_id
  AND crosswalk.SourceSystem = 'SL-CORE'
  AND BINARY crosswalk.ImportRunId = BINARY @import_run_id
  AND (
      (
          crosswalk.SourceTable = 'SL_CASE'
          AND crosswalk.LegacyId = '30984'
          AND crosswalk.TargetEntity = 'Case'
          AND BINARY crosswalk.TargetId = BINARY @case_id
          AND BINARY crosswalk.SourceHash = BINARY '10f0b1bc3d39e9bc929cbede64efeaaf3d959efc5c5331348eef7df94296d458'
      )
      OR (
          crosswalk.SourceTable = 'SL_LEINS_MEDICAL'
          AND crosswalk.LegacyId = '73236'
          AND crosswalk.TargetEntity = 'Lien'
          AND BINARY crosswalk.TargetId = BINARY @lien_id
          AND BINARY crosswalk.SourceHash = BINARY '555cde76074f3e5ccfc4de6c20da09918751e5b51b675abce522ec510149a8e6'
      )
      OR (
          crosswalk.SourceTable = 'SL_LIENS_SETTLEMENT_PAYMENT_DETAILS'
          AND crosswalk.LegacyId = '41970'
          AND crosswalk.TargetEntity = 'SettlementPaymentDetail'
          AND BINARY crosswalk.TargetId = BINARY @bad_payment_id
          AND BINARY crosswalk.SourceHash = BINARY 'eec268af4fdc699fc5ff6e574f5a99ca5693682d9ea0d1b33e4338c9cd202452'
      )
  );

SELECT COUNT(*) INTO @case_row_count
FROM liens_Cases target_case
WHERE BINARY target_case.Id = BINARY @case_id
  AND BINARY target_case.TenantId = BINARY @tenant_id
  AND BINARY target_case.OrgId = BINARY @org_id
  AND BINARY target_case.CaseNumber = BINARY @case_number
  AND target_case.Status = 'CaseSettled'
  AND target_case.DemandAmount = 83000.00
  AND target_case.SettlementAmount = 33500.00
  AND target_case.ClosedAtUtc = '2026-08-21 13:30:09.000000'
  AND target_case.CreatedAtUtc = '2026-04-27 07:45:03.000000'
  AND target_case.UpdatedAtUtc = '2026-08-21 13:30:32.204246'
  AND BINARY target_case.CreatedByUserId = BINARY @migration_user_id
  AND BINARY target_case.UpdatedByUserId = BINARY @migration_user_id;

SELECT COUNT(*), COALESCE(SUM(payment.Amount), 0)
INTO @sibling_payment_count, @sibling_payment_total
FROM liens_SettlementPaymentDetails payment
WHERE BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
  AND BINARY payment.LienId <> BINARY @lien_id
  AND payment.IsDeleted = 0;

SELECT COUNT(*), COALESCE(SUM(settlement.Amount), 0)
INTO @sibling_settlement_count, @sibling_settlement_total
FROM liens_LienSettlements settlement
WHERE BINARY settlement.TenantId = BINARY @tenant_id
  AND BINARY settlement.CaseId = BINARY @case_id
  AND BINARY settlement.LienId <> BINARY @lien_id;

SELECT COUNT(*) INTO @target_reduction_count
FROM liens_LienReductions reduction
WHERE BINARY reduction.TenantId = BINARY @tenant_id
  AND BINARY reduction.CaseId = BINARY @case_id
  AND BINARY reduction.LienId = BINARY @lien_id;

SELECT COUNT(*) INTO @target_settlement_count
FROM liens_LienSettlements settlement
WHERE BINARY settlement.TenantId = BINARY @tenant_id
  AND BINARY settlement.CaseId = BINARY @case_id
  AND BINARY settlement.LienId = BINARY @lien_id;

SELECT COUNT(*), COALESCE(SUM(payment.Amount), 0)
INTO @preflight_case_payment_count, @preflight_case_payment_total
FROM liens_SettlementPaymentDetails payment
WHERE BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
  AND payment.IsDeleted = 0;

SELECT COUNT(*), COALESCE(SUM(payment.Amount), 0)
INTO @preflight_target_payment_count, @preflight_target_payment_total
FROM liens_SettlementPaymentDetails payment
WHERE BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
  AND BINARY payment.LienId = BINARY @lien_id
  AND payment.IsDeleted = 0;

SELECT COUNT(*) INTO @preflight_target_no_recovery_count
FROM liens_SettlementPaymentDetails payment
WHERE BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
  AND BINARY payment.LienId = BINARY @lien_id
  AND payment.IsDeleted = 0
  AND payment.Note LIKE '%status=4%';

SET @repair_timestamp = NULL;
SET @repair_actor_user_id = NULL;
SELECT COUNT(*), MAX(lien.UpdatedAtUtc), MAX(lien.UpdatedByUserId)
INTO @repaired_state_count, @repair_timestamp, @repair_actor_user_id
FROM liens_Liens lien
INNER JOIN liens_SettlementPaymentDetails bad_payment
  ON BINARY bad_payment.Id = BINARY @bad_payment_id
INNER JOIN liens_SettlementPaymentDetails zero_payment
  ON BINARY zero_payment.Id = BINARY @zero_payment_id
WHERE BINARY lien.Id = BINARY @lien_id
  AND BINARY lien.TenantId = BINARY @tenant_id
  AND BINARY lien.OrgId = BINARY @org_id
  AND BINARY lien.CaseId = BINARY @case_id
  AND BINARY lien.LienNumber = BINARY @lien_number
  AND BINARY lien.ExternalReference = BINARY 'SL-CORE:SL_LEINS_MEDICAL:73236'
  AND lien.Status = 'Settled'
  AND lien.OriginalAmount = 16000.00
  AND lien.CurrentBalance = 0.00
  AND lien.PurchasePrice = 3200.00
  AND lien.PayoffAmount = 0.00
  AND lien.ClosedAtUtc = '2026-08-22 20:19:59.588613'
  AND lien.CreatedAtUtc = '2026-08-19 18:04:07.000000'
  AND BINARY lien.CreatedByUserId = BINARY @migration_user_id
  AND lien.UpdatedAtUtc > '2026-08-22 20:19:59.595772'
  AND lien.UpdatedByUserId IS NOT NULL
  AND BINARY bad_payment.TenantId = BINARY @tenant_id
  AND BINARY bad_payment.CaseId = BINARY @case_id
  AND BINARY bad_payment.LienId = BINARY @lien_id
  AND bad_payment.PaymentNumber = 1
  AND bad_payment.Amount = 16000.00
  AND bad_payment.PaymentDate IS NULL
  AND bad_payment.Payee IS NULL
  AND bad_payment.CheckNumber IS NULL
  AND BINARY bad_payment.Note = BINARY @bad_payment_note
  AND bad_payment.IsDeleted = 1
  AND bad_payment.CreatedAtUtc = '2026-08-19 18:11:42.000000'
  AND BINARY bad_payment.CreatedByUserId = BINARY @migration_user_id
  AND BINARY zero_payment.TenantId = BINARY @tenant_id
  AND BINARY zero_payment.CaseId = BINARY @case_id
  AND BINARY zero_payment.LienId = BINARY @lien_id
  AND zero_payment.PaymentNumber = 5
  AND zero_payment.Amount = 0.00
  AND zero_payment.PaymentDate = '2026-08-21'
  AND zero_payment.Payee IS NULL
  AND zero_payment.CheckNumber IS NULL
  AND BINARY zero_payment.Note = BINARY @repaired_zero_payment_note
  AND zero_payment.IsDeleted = 0
  AND zero_payment.CreatedAtUtc = '2026-08-22 20:20:00.000501'
  AND BINARY zero_payment.CreatedByUserId = BINARY @migration_user_id
  AND lien.UpdatedAtUtc = bad_payment.UpdatedAtUtc
  AND lien.UpdatedAtUtc = zero_payment.UpdatedAtUtc
  AND BINARY lien.UpdatedByUserId = BINARY bad_payment.UpdatedByUserId
  AND BINARY lien.UpdatedByUserId = BINARY zero_payment.UpdatedByUserId;

SELECT COUNT(*) INTO @already_rolled_back_state_count
FROM liens_Liens lien
INNER JOIN liens_SettlementPaymentDetails bad_payment
  ON BINARY bad_payment.Id = BINARY @bad_payment_id
INNER JOIN liens_SettlementPaymentDetails zero_payment
  ON BINARY zero_payment.Id = BINARY @zero_payment_id
WHERE BINARY lien.Id = BINARY @lien_id
  AND BINARY lien.TenantId = BINARY @tenant_id
  AND BINARY lien.OrgId = BINARY @org_id
  AND BINARY lien.CaseId = BINARY @case_id
  AND BINARY lien.LienNumber = BINARY @lien_number
  AND lien.Status = 'Settled'
  AND lien.OriginalAmount = 16000.00
  AND lien.CurrentBalance = 16000.00
  AND lien.PurchasePrice = 3200.00
  AND lien.PayoffAmount IS NULL
  AND lien.ClosedAtUtc = '2026-08-22 20:19:59.588613'
  AND lien.UpdatedAtUtc > '2026-08-22 20:20:00.006587'
  AND lien.UpdatedByUserId IS NOT NULL
  AND bad_payment.Amount = 16000.00
  AND bad_payment.IsDeleted = 0
  AND BINARY bad_payment.Note = BINARY @bad_payment_note
  AND zero_payment.Amount = 0.00
  AND zero_payment.IsDeleted = 0
  AND BINARY zero_payment.Note = BINARY @original_zero_payment_note
  AND lien.UpdatedAtUtc = bad_payment.UpdatedAtUtc
  AND lien.UpdatedAtUtc = zero_payment.UpdatedAtUtc
  AND BINARY lien.UpdatedByUserId = BINARY bad_payment.UpdatedByUserId
  AND BINARY lien.UpdatedByUserId = BINARY zero_payment.UpdatedByUserId;

SET @context_ok =
    @case_row_count = 1
    AND @matched_crosswalk_count = 3
    AND @sibling_payment_count = 4
    AND @sibling_payment_total = 33500.00
    AND @sibling_settlement_count = 4
    AND @sibling_settlement_total = 33500.00
    AND @target_reduction_count = 0
    AND @target_settlement_count = 0;

SET @financial_state_ok =
    (
        @repaired_state_count = 1
        AND @preflight_case_payment_count = 5
        AND @preflight_case_payment_total = 33500.00
        AND @preflight_target_payment_count = 1
        AND @preflight_target_payment_total = 0.00
        AND @preflight_target_no_recovery_count = 0
    )
    OR (
        @already_rolled_back_state_count = 1
        AND @preflight_case_payment_count = 6
        AND @preflight_case_payment_total = 49500.00
        AND @preflight_target_payment_count = 2
        AND @preflight_target_payment_total = 16000.00
        AND @preflight_target_no_recovery_count = 2
    );

SET @preflight_ok =
    @schema_ok = 1
    AND @actor_guid_ok = 1
    AND @actor_tenant_ok = 1
    AND @required_table_count = 6
    AND @context_ok = 1
    AND @financial_state_ok = 1
    AND ((@repaired_state_count = 1 AND @already_rolled_back_state_count = 0)
      OR (@repaired_state_count = 0 AND @already_rolled_back_state_count = 1));

SET @changes_to_apply = IF(@repaired_state_count = 1, 3, 0);
SET @already_rolled_back = IF(@already_rolled_back_state_count = 1, 1, 0);
SET @blocking_rows = IF(@preflight_ok = 1, 0, 1);

SET @plan_checksum = SHA2(
    CONCAT_WS(
        '|',
        @target_schema, @tenant_id, @org_id, @rollback_actor_user_id,
        @case_id, @case_number, @lien_id, @lien_number,
        @bad_payment_id, @zero_payment_id, @import_run_id,
        '10f0b1bc3d39e9bc929cbede64efeaaf3d959efc5c5331348eef7df94296d458',
        '555cde76074f3e5ccfc4de6c20da09918751e5b51b675abce522ec510149a8e6',
        'eec268af4fdc699fc5ff6e574f5a99ca5693682d9ea0d1b33e4338c9cd202452',
        @repair_timestamp, @repair_actor_user_id,
        CAST(@changes_to_apply AS CHAR),
        'CaseSettled', '83000.00', '33500.00',
        '2026-08-21 13:30:09.000000', '2026-08-21 13:30:32.204246',
        'Settled', '16000.00', '0.00', '3200.00', '0.00',
        '2026-08-22 20:19:59.588613',
        'rollback:16000.00', 'rollback:NULL',
        CAST(@sibling_payment_count AS CHAR), CAST(@sibling_payment_total AS CHAR),
        CAST(@sibling_settlement_count AS CHAR), CAST(@sibling_settlement_total AS CHAR),
        CAST(@preflight_case_payment_count AS CHAR), CAST(@preflight_case_payment_total AS CHAR),
        CAST(@preflight_target_payment_count AS CHAR), CAST(@preflight_target_payment_total AS CHAR),
        CAST(@preflight_target_no_recovery_count AS CHAR),
        @bad_payment_note, @original_zero_payment_note, @repaired_zero_payment_note
    ),
    256
);

SET @apply_permitted =
    @apply = 1
    AND @preflight_ok = 1
    AND @repaired_state_count = 1
    AND @changes_to_apply = 3
    AND @expected_updates = @changes_to_apply
    AND LOWER(COALESCE(@expected_checksum, '')) = LOWER(@plan_checksum);

SELECT
    @target_schema AS TargetSchema,
    @tenant_id AS TenantId,
    @case_number AS CaseNumber,
    @schema_ok AS SchemaMatched,
    @actor_tenant_ok AS ActorTenantMatched,
    @preflight_ok AS PreflightPassed,
    @apply AS ApplyRequested,
    @apply_permitted AS ApplyPermitted,
    @changes_to_apply AS ChangesToApply,
    @already_rolled_back AS AlreadyRolledBack,
    @blocking_rows AS BlockingRows,
    @repair_timestamp AS RepairTimestamp,
    @repair_actor_user_id AS RepairActorUserId,
    @plan_checksum AS PlanChecksum;

SELECT
    @lien_id AS LienId,
    @lien_number AS LienNumber,
    @bad_payment_id AS RestoredPaymentId,
    16000.00 AS RestoredPaymentAmount,
    @zero_payment_id AS RestoredNoRecoveryPaymentId,
    16000.00 AS RestoredCurrentBalance,
    NULL AS RestoredPayoffAmount,
    'Settled' AS PreservedLienStatus,
    '2026-08-22 20:19:59.588613' AS PreservedClosedAtUtc;

SET @locked_actor_user_id = NULL;
SET @locked_case_id = NULL;
SET @locked_lien_id = NULL;
SET @locked_bad_payment_id = NULL;
SET @locked_zero_payment_id = NULL;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
START TRANSACTION;

SELECT payment.Id AS LockedCasePaymentId
FROM liens_SettlementPaymentDetails payment
WHERE @apply_permitted = 1
  AND payment.TenantId = @tenant_id
  AND payment.CaseId = @case_id
ORDER BY payment.Id
FOR UPDATE;

SELECT settlement.Id AS LockedCaseSettlementId
FROM liens_LienSettlements settlement
WHERE @apply_permitted = 1
  AND settlement.TenantId = @tenant_id
  AND settlement.CaseId = @case_id
ORDER BY settlement.Id
FOR UPDATE;

SELECT reduction.Id AS LockedTargetReductionId
FROM liens_LienReductions reduction
WHERE @apply_permitted = 1
  AND reduction.TenantId = @tenant_id
  AND reduction.CaseId = @case_id
  AND reduction.LienId = @lien_id
FOR UPDATE;

SELECT crosswalk.Id AS LockedCrosswalkId
FROM liens_LegacyIdCrosswalks crosswalk
WHERE @apply_permitted = 1
  AND crosswalk.TenantId = @tenant_id
  AND crosswalk.SourceSystem = 'SL-CORE'
  AND crosswalk.ImportRunId = @import_run_id
  AND crosswalk.TargetId IN (@case_id, @lien_id, @bad_payment_id)
ORDER BY crosswalk.Id
FOR UPDATE;

SELECT identity_user.Id INTO @locked_actor_user_id
FROM LS_IDENTITY.idt_Users identity_user
INNER JOIN LS_IDENTITY.idt_UserTenants user_tenant
  ON user_tenant.UserId = identity_user.Id
 AND user_tenant.TenantId = @tenant_id
 AND user_tenant.IsActive = 1
WHERE @apply_permitted = 1
  AND identity_user.Id = @rollback_actor_user_id
  AND identity_user.IsActive = 1
FOR SHARE;

SELECT target_case.Id INTO @locked_case_id
FROM liens_Cases target_case
WHERE @apply_permitted = 1
  AND target_case.Id = @case_id
  AND target_case.TenantId = @tenant_id
  AND target_case.OrgId = @org_id
  AND target_case.CaseNumber = @case_number
  AND target_case.Status = 'CaseSettled'
  AND target_case.DemandAmount = 83000.00
  AND target_case.SettlementAmount = 33500.00
  AND target_case.ClosedAtUtc = '2026-08-21 13:30:09.000000'
  AND target_case.CreatedAtUtc = '2026-04-27 07:45:03.000000'
  AND target_case.UpdatedAtUtc = '2026-08-21 13:30:32.204246'
  AND target_case.CreatedByUserId = @migration_user_id
  AND target_case.UpdatedByUserId = @migration_user_id
FOR UPDATE;

SELECT lien.Id INTO @locked_lien_id
FROM liens_Liens lien
WHERE @apply_permitted = 1
  AND lien.Id = @lien_id
  AND lien.TenantId = @tenant_id
FOR UPDATE;

SELECT payment.Id INTO @locked_bad_payment_id
FROM liens_SettlementPaymentDetails payment
WHERE @apply_permitted = 1
  AND payment.Id = @bad_payment_id
  AND payment.TenantId = @tenant_id
FOR UPDATE;

SELECT payment.Id INTO @locked_zero_payment_id
FROM liens_SettlementPaymentDetails payment
WHERE @apply_permitted = 1
  AND payment.Id = @zero_payment_id
  AND payment.TenantId = @tenant_id
FOR UPDATE;

SELECT COUNT(*), COALESCE(SUM(payment.Amount), 0)
INTO @locked_sibling_payment_count, @locked_sibling_payment_total
FROM liens_SettlementPaymentDetails payment
WHERE @apply_permitted = 1
  AND payment.TenantId = @tenant_id
  AND payment.CaseId = @case_id
  AND payment.LienId <> @lien_id
  AND payment.IsDeleted = 0;

SELECT COUNT(*), COALESCE(SUM(settlement.Amount), 0)
INTO @locked_sibling_settlement_count, @locked_sibling_settlement_total
FROM liens_LienSettlements settlement
WHERE @apply_permitted = 1
  AND settlement.TenantId = @tenant_id
  AND settlement.CaseId = @case_id
  AND settlement.LienId <> @lien_id;

SELECT COUNT(*) INTO @locked_target_reduction_count
FROM liens_LienReductions reduction
WHERE @apply_permitted = 1
  AND reduction.TenantId = @tenant_id
  AND reduction.CaseId = @case_id
  AND reduction.LienId = @lien_id;

SELECT COUNT(*) INTO @locked_target_settlement_count
FROM liens_LienSettlements settlement
WHERE @apply_permitted = 1
  AND settlement.TenantId = @tenant_id
  AND settlement.CaseId = @case_id
  AND settlement.LienId = @lien_id;

SELECT COUNT(*) INTO @locked_crosswalk_count
FROM liens_LegacyIdCrosswalks crosswalk
WHERE @apply_permitted = 1
  AND crosswalk.TenantId = @tenant_id
  AND crosswalk.SourceSystem = 'SL-CORE'
  AND crosswalk.ImportRunId = @import_run_id
  AND (
      (
          crosswalk.SourceTable = 'SL_CASE'
          AND crosswalk.LegacyId = '30984'
          AND crosswalk.TargetEntity = 'Case'
          AND crosswalk.TargetId = @case_id
          AND BINARY crosswalk.SourceHash = BINARY '10f0b1bc3d39e9bc929cbede64efeaaf3d959efc5c5331348eef7df94296d458'
      )
      OR (
          crosswalk.SourceTable = 'SL_LEINS_MEDICAL'
          AND crosswalk.LegacyId = '73236'
          AND crosswalk.TargetEntity = 'Lien'
          AND crosswalk.TargetId = @lien_id
          AND BINARY crosswalk.SourceHash = BINARY '555cde76074f3e5ccfc4de6c20da09918751e5b51b675abce522ec510149a8e6'
      )
      OR (
          crosswalk.SourceTable = 'SL_LIENS_SETTLEMENT_PAYMENT_DETAILS'
          AND crosswalk.LegacyId = '41970'
          AND crosswalk.TargetEntity = 'SettlementPaymentDetail'
          AND crosswalk.TargetId = @bad_payment_id
          AND BINARY crosswalk.SourceHash = BINARY 'eec268af4fdc699fc5ff6e574f5a99ca5693682d9ea0d1b33e4338c9cd202452'
      )
  );

SELECT COUNT(*) INTO @locked_repaired_state_count
FROM liens_Liens lien
INNER JOIN liens_SettlementPaymentDetails bad_payment
  ON bad_payment.Id = @bad_payment_id
INNER JOIN liens_SettlementPaymentDetails zero_payment
  ON zero_payment.Id = @zero_payment_id
WHERE @apply_permitted = 1
  AND lien.Id = @lien_id
  AND lien.TenantId = @tenant_id
  AND lien.OrgId = @org_id
  AND lien.CaseId = @case_id
  AND lien.LienNumber = @lien_number
  AND lien.Status = 'Settled'
  AND lien.OriginalAmount = 16000.00
  AND lien.CurrentBalance = 0.00
  AND lien.PurchasePrice = 3200.00
  AND lien.PayoffAmount = 0.00
  AND lien.ClosedAtUtc = '2026-08-22 20:19:59.588613'
  AND lien.CreatedAtUtc = '2026-08-19 18:04:07.000000'
  AND lien.CreatedByUserId = @migration_user_id
  AND lien.UpdatedAtUtc = @repair_timestamp
  AND lien.UpdatedByUserId = @repair_actor_user_id
  AND bad_payment.TenantId = @tenant_id
  AND bad_payment.CaseId = @case_id
  AND bad_payment.LienId = @lien_id
  AND bad_payment.PaymentNumber = 1
  AND bad_payment.Amount = 16000.00
  AND bad_payment.PaymentDate IS NULL
  AND bad_payment.Payee IS NULL
  AND bad_payment.CheckNumber IS NULL
  AND BINARY bad_payment.Note = BINARY @bad_payment_note
  AND bad_payment.IsDeleted = 1
  AND bad_payment.CreatedAtUtc = '2026-08-19 18:11:42.000000'
  AND bad_payment.CreatedByUserId = @migration_user_id
  AND bad_payment.UpdatedAtUtc = @repair_timestamp
  AND bad_payment.UpdatedByUserId = @repair_actor_user_id
  AND zero_payment.TenantId = @tenant_id
  AND zero_payment.CaseId = @case_id
  AND zero_payment.LienId = @lien_id
  AND zero_payment.PaymentNumber = 5
  AND zero_payment.Amount = 0.00
  AND zero_payment.PaymentDate = '2026-08-21'
  AND zero_payment.Payee IS NULL
  AND zero_payment.CheckNumber IS NULL
  AND BINARY zero_payment.Note = BINARY @repaired_zero_payment_note
  AND zero_payment.IsDeleted = 0
  AND zero_payment.CreatedAtUtc = '2026-08-22 20:20:00.000501'
  AND zero_payment.CreatedByUserId = @migration_user_id
  AND zero_payment.UpdatedAtUtc = @repair_timestamp
  AND zero_payment.UpdatedByUserId = @repair_actor_user_id;

SET @locked_preimages = IF(
    @apply_permitted = 1
    AND @locked_case_id = @case_id
    AND @locked_lien_id = @lien_id
    AND @locked_bad_payment_id = @bad_payment_id
    AND @locked_zero_payment_id = @zero_payment_id
    AND @locked_actor_user_id = @rollback_actor_user_id
    AND @locked_sibling_payment_count = 4
    AND @locked_sibling_payment_total = 33500.00
    AND @locked_sibling_settlement_count = 4
    AND @locked_sibling_settlement_total = 33500.00
    AND @locked_target_reduction_count = 0
    AND @locked_target_settlement_count = 0
    AND @locked_crosswalk_count = 3
    AND @locked_repaired_state_count = 1,
    3,
    0
);
SET @apply_permitted = IF(
    @apply_permitted = 1 AND @locked_preimages = 3,
    1,
    0
);
SET @rollback_timestamp = UTC_TIMESTAMP(6);

UPDATE liens_SettlementPaymentDetails payment
SET payment.IsDeleted = 0,
    payment.UpdatedAtUtc = @rollback_timestamp,
    payment.UpdatedByUserId = @rollback_actor_user_id
WHERE @apply_permitted = 1
  AND payment.Id = @bad_payment_id
  AND payment.TenantId = @tenant_id
  AND payment.CaseId = @case_id
  AND payment.LienId = @lien_id
  AND payment.PaymentNumber = 1
  AND payment.Amount = 16000.00
  AND payment.PaymentDate IS NULL
  AND payment.Payee IS NULL
  AND payment.CheckNumber IS NULL
  AND BINARY payment.Note = BINARY @bad_payment_note
  AND payment.IsDeleted = 1
  AND payment.CreatedAtUtc = '2026-08-19 18:11:42.000000'
  AND payment.CreatedByUserId = @migration_user_id
  AND payment.UpdatedAtUtc = @repair_timestamp
  AND payment.UpdatedByUserId = @repair_actor_user_id;
SET @bad_payment_rows_updated = ROW_COUNT();

UPDATE liens_SettlementPaymentDetails payment
SET payment.Note = @original_zero_payment_note,
    payment.UpdatedAtUtc = @rollback_timestamp,
    payment.UpdatedByUserId = @rollback_actor_user_id
WHERE @apply_permitted = 1
  AND payment.Id = @zero_payment_id
  AND payment.TenantId = @tenant_id
  AND payment.CaseId = @case_id
  AND payment.LienId = @lien_id
  AND payment.PaymentNumber = 5
  AND payment.Amount = 0.00
  AND payment.PaymentDate = '2026-08-21'
  AND payment.Payee IS NULL
  AND payment.CheckNumber IS NULL
  AND BINARY payment.Note = BINARY @repaired_zero_payment_note
  AND payment.IsDeleted = 0
  AND payment.CreatedAtUtc = '2026-08-22 20:20:00.000501'
  AND payment.CreatedByUserId = @migration_user_id
  AND payment.UpdatedAtUtc = @repair_timestamp
  AND payment.UpdatedByUserId = @repair_actor_user_id;
SET @zero_payment_rows_updated = ROW_COUNT();

UPDATE liens_Liens lien
SET lien.CurrentBalance = 16000.00,
    lien.PayoffAmount = NULL,
    lien.UpdatedAtUtc = @rollback_timestamp,
    lien.UpdatedByUserId = @rollback_actor_user_id
WHERE @apply_permitted = 1
  AND lien.Id = @lien_id
  AND lien.TenantId = @tenant_id
  AND lien.OrgId = @org_id
  AND lien.CaseId = @case_id
  AND lien.LienNumber = @lien_number
  AND lien.Status = 'Settled'
  AND lien.OriginalAmount = 16000.00
  AND lien.CurrentBalance = 0.00
  AND lien.PurchasePrice = 3200.00
  AND lien.PayoffAmount = 0.00
  AND lien.ClosedAtUtc = '2026-08-22 20:19:59.588613'
  AND lien.CreatedAtUtc = '2026-08-19 18:04:07.000000'
  AND lien.CreatedByUserId = @migration_user_id
  AND lien.UpdatedAtUtc = @repair_timestamp
  AND lien.UpdatedByUserId = @repair_actor_user_id;
SET @lien_rows_updated = ROW_COUNT();

SET @rows_updated =
    @bad_payment_rows_updated
    + @zero_payment_rows_updated
    + @lien_rows_updated;

SELECT COUNT(*) INTO @restored_rows_ok
FROM liens_Liens lien
INNER JOIN liens_SettlementPaymentDetails bad_payment
  ON bad_payment.Id = @bad_payment_id
INNER JOIN liens_SettlementPaymentDetails zero_payment
  ON zero_payment.Id = @zero_payment_id
WHERE lien.Id = @lien_id
  AND lien.TenantId = @tenant_id
  AND lien.CaseId = @case_id
  AND lien.Status = 'Settled'
  AND lien.OriginalAmount = 16000.00
  AND lien.CurrentBalance = 16000.00
  AND lien.PurchasePrice = 3200.00
  AND lien.PayoffAmount IS NULL
  AND lien.ClosedAtUtc = '2026-08-22 20:19:59.588613'
  AND lien.UpdatedAtUtc = @rollback_timestamp
  AND lien.UpdatedByUserId = @rollback_actor_user_id
  AND bad_payment.Amount = 16000.00
  AND bad_payment.IsDeleted = 0
  AND BINARY bad_payment.Note = BINARY @bad_payment_note
  AND bad_payment.UpdatedAtUtc = @rollback_timestamp
  AND bad_payment.UpdatedByUserId = @rollback_actor_user_id
  AND zero_payment.Amount = 0.00
  AND zero_payment.IsDeleted = 0
  AND BINARY zero_payment.Note = BINARY @original_zero_payment_note
  AND zero_payment.UpdatedAtUtc = @rollback_timestamp
  AND zero_payment.UpdatedByUserId = @rollback_actor_user_id;

SELECT COUNT(*), COALESCE(SUM(payment.Amount), 0)
INTO @case_active_payment_count, @case_active_payment_total
FROM liens_SettlementPaymentDetails payment
WHERE payment.TenantId = @tenant_id
  AND payment.CaseId = @case_id
  AND payment.IsDeleted = 0;

SELECT COUNT(*), COALESCE(SUM(payment.Amount), 0)
INTO @target_active_payment_count, @target_active_payment_total
FROM liens_SettlementPaymentDetails payment
WHERE payment.TenantId = @tenant_id
  AND payment.CaseId = @case_id
  AND payment.LienId = @lien_id
  AND payment.IsDeleted = 0;

SELECT COUNT(*) INTO @target_active_no_recovery_count
FROM liens_SettlementPaymentDetails payment
WHERE payment.TenantId = @tenant_id
  AND payment.CaseId = @case_id
  AND payment.LienId = @lien_id
  AND payment.IsDeleted = 0
  AND payment.Note LIKE '%status=4%';

SET @postcondition_errors = IF(
    @restored_rows_ok = 1
    AND @rows_updated = 3
    AND @case_active_payment_count = 6
    AND @case_active_payment_total = 49500.00
    AND @target_active_payment_count = 2
    AND @target_active_payment_total = 16000.00
    AND @target_active_no_recovery_count = 2,
    0,
    1
);

SET @apply_permitted = IF(
    @apply_permitted = 1
    AND @rows_updated = 3
    AND @postcondition_errors = 0,
    1,
    0
);

SET @conditional_commit_sql = IF(@apply_permitted = 1, 'COMMIT', 'SELECT 1');
PREPARE darin_rollback_conditional_commit FROM @conditional_commit_sql;
EXECUTE darin_rollback_conditional_commit;
DEALLOCATE PREPARE darin_rollback_conditional_commit;
ROLLBACK;

SELECT
    @apply_permitted AS ApplyPermitted,
    @changes_to_apply AS ExpectedUpdates,
    @locked_preimages AS LockedPreimages,
    @rows_updated AS RowsUpdated,
    @postcondition_errors AS PostconditionErrors,
    CASE
        WHEN @apply = 0 THEN 'Dry run complete: no changes were written.'
        WHEN @preflight_ok <> 1 THEN 'No changes written: rollback preflight failed.'
        WHEN @expected_updates <> @changes_to_apply
            THEN 'No changes written: expected count does not match dry run.'
        WHEN LOWER(COALESCE(@expected_checksum, '')) <> LOWER(@plan_checksum)
            THEN 'No changes written: expected checksum does not match dry run.'
        WHEN @locked_preimages <> 3
            THEN 'No changes written: repaired rows changed after preflight.'
        WHEN @rows_updated <> 3 OR @postcondition_errors <> 0
            THEN 'No changes written: rollback update or postcondition failed.'
        ELSE 'Rollback applied: the reviewed pre-repair Darin state was restored.'
    END AS Result;
