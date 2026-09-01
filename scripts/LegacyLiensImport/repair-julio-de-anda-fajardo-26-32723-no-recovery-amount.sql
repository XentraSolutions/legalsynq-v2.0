-- Corrects the reviewed imported No Recovery declaration for Julio De Anda
-- Fajardo, case 26-32723, lien 26-32723-01. SL-CORE stored the lien's $3,700
-- face amount on a status=4 declaration with no receipt evidence. The import
-- treated that amount as cash received. This repair changes only that active
-- declaration's Amount from 3700 to 0, preserving the status=4 metadata that
-- V3 uses to display No Recovery and preserving the closed case/lien state.
--
-- Run the complete file in DBeaver on an explicitly selected LS_LIENS
-- connection. Leave @apply = 0 for preflight. Review every result, copy
-- ChangesToApply and PlanChecksum into the expected variables, set @apply = 1,
-- and execute the complete file again.
--
-- The change is reversible because the row and all provenance remain intact.
-- Do not restore 3700 without a new reviewed script proving that cash was
-- actually received.
--
-- Error/reference prefix: LSLJDAF-

SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;

-- Required: active Identity user approving and executing this repair.
SET @actor_user_id = '<identity-user-guid>';

-- Dry-run defaults. Populate the expected assertions only for apply.
SET @apply = 0;
SET @expected_updates = -1;
SET @expected_checksum = NULL;

SET @target_schema = DATABASE();
SET @tenant_id = '019f1a05-7459-7855-b46b-110a702e37a4';
SET @org_id = '019f1a05-792b-7c0e-89a0-ae24990d1f89';
SET @case_id = '6daaf5dd-9d64-11f1-b823-12a7a8afef43';
SET @case_number = '26-32723';
SET @legacy_case_id = '32723';
SET @lien_id = '6e69e862-9d64-11f1-b823-12a7a8afef43';
SET @lien_number = '26-32723-01';
SET @legacy_lien_id = '72342';
SET @payment_id = '72abe00f-9d64-11f1-b823-12a7a8afef43';
SET @legacy_payment_id = '41604';
SET @import_run_id = '72ff0756-9d64-11f1-b823-12a7a8afef43';
SET @migration_user_id = '019f1a05-792f-74f2-b071-4fdc0d6bd30a';
SET @payment_note =
    'legacyPaymentDetailId=41604; legacyCaseId=32723; type=; status=4; lienStatus=2; checkAmount=; netProfit=';

SET @actor_user_id = LOWER(TRIM(@actor_user_id));
SET @apply = IF(@apply = 1, 1, 0);
SET @schema_ok = BINARY @target_schema = BINARY 'LS_LIENS';
SET @actor_guid_ok =
    @actor_user_id <> '<identity-user-guid>'
    AND CHAR_LENGTH(@actor_user_id) = 36
    AND SUBSTRING(@actor_user_id, 9, 1) = '-'
    AND SUBSTRING(@actor_user_id, 14, 1) = '-'
    AND SUBSTRING(@actor_user_id, 19, 1) = '-'
    AND SUBSTRING(@actor_user_id, 24, 1) = '-'
    AND UNHEX(REPLACE(@actor_user_id, '-', '')) IS NOT NULL;

SELECT EXISTS (
    SELECT 1
    FROM LS_IDENTITY.idt_Users identity_user
    INNER JOIN LS_IDENTITY.idt_UserTenants user_tenant
      ON BINARY user_tenant.UserId = BINARY identity_user.Id
     AND BINARY user_tenant.TenantId = BINARY @tenant_id
     AND user_tenant.IsActive = 1
    WHERE BINARY identity_user.Id = BINARY @actor_user_id
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

DROP TEMPORARY TABLE IF EXISTS tmp_julio_expected_crosswalks;
CREATE TEMPORARY TABLE tmp_julio_expected_crosswalks (
    SourceTable VARCHAR(100) NOT NULL,
    LegacyId VARCHAR(100) NOT NULL,
    TargetEntity VARCHAR(100) NOT NULL,
    TargetId CHAR(36) NOT NULL,
    SourceHash CHAR(64) NOT NULL,
    PRIMARY KEY (SourceTable, LegacyId)
) ENGINE=InnoDB;

INSERT INTO tmp_julio_expected_crosswalks
    (SourceTable, LegacyId, TargetEntity, TargetId, SourceHash)
VALUES
    ('SL_CASE', '32723', 'Case',
     '6daaf5dd-9d64-11f1-b823-12a7a8afef43',
     'eb45f49826b1c67701f5491c04fbe58031d1a13e8f1c39c6c1c2e13b01c05455'),
    ('SL_LEINS_MEDICAL', '72342', 'Lien',
     '6e69e862-9d64-11f1-b823-12a7a8afef43',
     '1c253796513c05faf9c737276be87266d44aede44071f3713513ff5b7ddb3d46'),
    ('SL_LIENS_SETTLEMENT_PAYMENT_DETAILS', '41604',
     'SettlementPaymentDetail',
     '72abe00f-9d64-11f1-b823-12a7a8afef43',
     '06ce0cad5ad864ddfd7e4583a23b4de119399457b4b2a97d6d6cf8c8c3a3266b');

SELECT COUNT(*) INTO @matched_crosswalk_count
FROM tmp_julio_expected_crosswalks expected
INNER JOIN liens_LegacyIdCrosswalks actual
  ON BINARY actual.TenantId = BINARY @tenant_id
 AND actual.SourceSystem = 'SL-CORE'
 AND BINARY actual.SourceTable = BINARY expected.SourceTable
 AND BINARY actual.LegacyId = BINARY expected.LegacyId
 AND BINARY actual.TargetEntity = BINARY expected.TargetEntity
 AND BINARY actual.TargetId = BINARY expected.TargetId
 AND BINARY actual.SourceHash = BINARY expected.SourceHash
 AND BINARY actual.ImportRunId = BINARY @import_run_id;

SELECT EXISTS (
    SELECT 1
    FROM liens_Cases target_case
    WHERE BINARY target_case.Id = BINARY @case_id
      AND BINARY target_case.TenantId = BINARY @tenant_id
      AND BINARY target_case.OrgId = BINARY @org_id
      AND BINARY target_case.CaseNumber = BINARY @case_number
      AND BINARY target_case.ExternalReference = BINARY 'SL-CORE:SL_CASE:32723'
      AND target_case.ClientFirstName = 'Julio'
      AND target_case.ClientLastName = 'De Anda Fajardo'
      AND target_case.Status = 'Closed'
      AND target_case.DateOfIncident = '2026-06-16'
      AND target_case.OpenedAtUtc = '2026-07-16 21:00:10.000000'
      AND target_case.ClosedAtUtc = '2026-08-21 13:30:09.000000'
      AND target_case.DemandAmount = 3700.00
      AND target_case.SettlementAmount IS NULL
      AND target_case.CreatedAtUtc = '2026-07-16 21:00:10.000000'
      AND target_case.UpdatedAtUtc = '2026-08-21 13:30:32.204246'
      AND BINARY target_case.CreatedByUserId = BINARY @migration_user_id
      AND BINARY target_case.UpdatedByUserId = BINARY @migration_user_id
) INTO @case_ok;

SELECT COUNT(*) INTO @case_lien_count
FROM liens_Liens lien
WHERE BINARY lien.TenantId = BINARY @tenant_id
  AND BINARY lien.CaseId = BINARY @case_id;

SELECT EXISTS (
    SELECT 1
    FROM liens_Liens lien
    WHERE BINARY lien.Id = BINARY @lien_id
      AND BINARY lien.TenantId = BINARY @tenant_id
      AND BINARY lien.OrgId = BINARY @org_id
      AND BINARY lien.CaseId = BINARY @case_id
      AND BINARY lien.LienNumber = BINARY @lien_number
      AND BINARY lien.ExternalReference = BINARY 'SL-CORE:SL_LEINS_MEDICAL:72342'
      AND lien.Status = 'Settled'
      AND lien.OriginalAmount = 3700.00
      AND lien.CurrentBalance = 0.00
      AND lien.PurchasePrice = 800.00
      AND lien.PayoffAmount IS NULL
      AND lien.InitialServiceDate = '2026-06-28'
      AND lien.PurchaseDate = '2026-07-17'
      AND lien.OpenedAtUtc = '2026-07-16 21:01:37.000000'
      AND lien.ClosedAtUtc = '2026-07-28 00:00:00.000000'
      AND lien.CreatedAtUtc = '2026-07-16 21:01:37.000000'
      AND lien.UpdatedAtUtc = '2026-08-21 13:30:42.104181'
      AND BINARY lien.CreatedByUserId = BINARY @migration_user_id
      AND BINARY lien.UpdatedByUserId = BINARY @migration_user_id
) INTO @lien_ok;

SELECT EXISTS (
    SELECT 1
    FROM liens_SettlementPaymentDetails payment
    WHERE BINARY payment.Id = BINARY @payment_id
      AND BINARY payment.TenantId = BINARY @tenant_id
      AND BINARY payment.CaseId = BINARY @case_id
      AND BINARY payment.LienId = BINARY @lien_id
      AND payment.PaymentNumber = 1
      AND payment.Amount = 3700.00
      AND payment.PaymentDate IS NULL
      AND payment.Payee IS NULL
      AND payment.CheckNumber IS NULL
      AND BINARY payment.Note = BINARY @payment_note
      AND payment.IsDeleted = 0
      AND payment.CreatedAtUtc = '2026-07-28 17:19:53.000000'
      AND payment.UpdatedAtUtc = '2026-07-28 00:00:00.000000'
      AND BINARY payment.CreatedByUserId = BINARY @migration_user_id
      AND BINARY payment.UpdatedByUserId = BINARY @migration_user_id
) INTO @initial_payment_ok;

SELECT EXISTS (
    SELECT 1
    FROM liens_SettlementPaymentDetails payment
    WHERE BINARY payment.Id = BINARY @payment_id
      AND BINARY payment.TenantId = BINARY @tenant_id
      AND BINARY payment.CaseId = BINARY @case_id
      AND BINARY payment.LienId = BINARY @lien_id
      AND payment.PaymentNumber = 1
      AND payment.Amount = 0.00
      AND payment.PaymentDate IS NULL
      AND payment.Payee IS NULL
      AND payment.CheckNumber IS NULL
      AND BINARY payment.Note = BINARY @payment_note
      AND payment.IsDeleted = 0
      AND payment.CreatedAtUtc = '2026-07-28 17:19:53.000000'
      AND payment.UpdatedAtUtc > '2026-07-28 00:00:00.000000'
      AND BINARY payment.CreatedByUserId = BINARY @migration_user_id
      AND payment.UpdatedByUserId IS NOT NULL
) INTO @final_payment_ok;

SELECT COUNT(*), COALESCE(SUM(payment.Amount), 0)
INTO @active_payment_count, @active_payment_total
FROM liens_SettlementPaymentDetails payment
WHERE BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
  AND payment.IsDeleted = 0;

SELECT COUNT(*) INTO @active_no_recovery_count
FROM liens_SettlementPaymentDetails payment
WHERE BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
  AND payment.IsDeleted = 0
  AND payment.Note LIKE '%status=4%';

SELECT COUNT(*) INTO @reduction_count
FROM liens_LienReductions reduction
WHERE BINARY reduction.TenantId = BINARY @tenant_id
  AND BINARY reduction.CaseId = BINARY @case_id;

SELECT COUNT(*) INTO @settlement_count
FROM liens_LienSettlements settlement
WHERE BINARY settlement.TenantId = BINARY @tenant_id
  AND BINARY settlement.CaseId = BINARY @case_id;

SET @context_ok =
    @case_ok = 1
    AND @case_lien_count = 1
    AND @lien_ok = 1
    AND @matched_crosswalk_count = 3
    AND @reduction_count = 0
    AND @settlement_count = 0;

SET @state_ok =
    (@initial_payment_ok = 1
     AND @final_payment_ok = 0
     AND @active_payment_count = 1
     AND @active_payment_total = 3700.00
     AND @active_no_recovery_count = 1)
    OR
    (@initial_payment_ok = 0
     AND @final_payment_ok = 1
     AND @active_payment_count = 1
     AND @active_payment_total = 0.00
     AND @active_no_recovery_count = 1);

SET @preflight_ok =
    @schema_ok = 1
    AND @actor_guid_ok = 1
    AND @actor_tenant_ok = 1
    AND @required_table_count = 6
    AND @context_ok = 1
    AND @state_ok = 1;

SET @changes_to_apply = IF(@initial_payment_ok = 1, 1, 0);
SET @already_repaired = IF(@final_payment_ok = 1, 1, 0);
SET @blocking_rows = IF(@preflight_ok = 1, 0, 1);

SET @plan_checksum = SHA2(
    CONCAT_WS(
        '|',
        @target_schema, @tenant_id, @org_id, @actor_user_id,
        @case_id, @case_number, @legacy_case_id,
        @lien_id, @lien_number, @legacy_lien_id,
        @payment_id, @legacy_payment_id, @import_run_id,
        'eb45f49826b1c67701f5491c04fbe58031d1a13e8f1c39c6c1c2e13b01c05455',
        '1c253796513c05faf9c737276be87266d44aede44071f3713513ff5b7ddb3d46',
        '06ce0cad5ad864ddfd7e4583a23b4de119399457b4b2a97d6d6cf8c8c3a3266b',
        CAST(@changes_to_apply AS CHAR),
        'Closed', '3700.00', 'NULL',
        'Settled', '3700.00', '0.00', '800.00', 'NULL',
        '1', '3700.00', '0.00', 'NULL', 'NULL', 'NULL',
        '2026-07-28 17:19:53.000000',
        '2026-07-28 00:00:00.000000',
        @payment_note
    ),
    256
);

SET @apply_permitted =
    @apply = 1
    AND @preflight_ok = 1
    AND @initial_payment_ok = 1
    AND @changes_to_apply = 1
    AND @expected_updates = @changes_to_apply
    AND LOWER(COALESCE(@expected_checksum, '')) = LOWER(@plan_checksum);

SELECT
    @target_schema AS TargetSchema,
    @tenant_id AS TenantId,
    @case_number AS CaseNumber,
    @legacy_case_id AS LegacyCaseId,
    @schema_ok AS SchemaMatched,
    @actor_tenant_ok AS ActorTenantMatched,
    @preflight_ok AS PreflightPassed,
    @apply AS ApplyRequested,
    @apply_permitted AS ApplyPermitted,
    @changes_to_apply AS ChangesToApply,
    @already_repaired AS AlreadyRepaired,
    @blocking_rows AS BlockingRows,
    @plan_checksum AS PlanChecksum;

SELECT
    @payment_id AS PaymentId,
    @lien_id AS LienId,
    @lien_number AS LienNumber,
    @legacy_payment_id AS LegacyPaymentDetailId,
    3700.00 AS IncorrectAmount,
    0.00 AS CorrectedAmount,
    @active_no_recovery_count AS ActiveNoRecoveryDeclarations,
    @initial_payment_ok AS InitialStateMatched,
    @final_payment_ok AS FinalStateMatched;

SET @locked_actor_user_id = NULL;
SET @locked_case_id = NULL;
SET @locked_lien_id = NULL;
SET @locked_payment_id = NULL;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
START TRANSACTION;

SELECT payment.Id AS LockedCasePaymentId
FROM liens_SettlementPaymentDetails payment
WHERE @apply_permitted = 1
  AND BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
ORDER BY payment.Id
FOR UPDATE;

SELECT reduction.Id AS LockedReductionId
FROM liens_LienReductions reduction
WHERE @apply_permitted = 1
  AND BINARY reduction.TenantId = BINARY @tenant_id
  AND BINARY reduction.CaseId = BINARY @case_id
ORDER BY reduction.Id
FOR UPDATE;

SELECT settlement.Id AS LockedSettlementId
FROM liens_LienSettlements settlement
WHERE @apply_permitted = 1
  AND BINARY settlement.TenantId = BINARY @tenant_id
  AND BINARY settlement.CaseId = BINARY @case_id
ORDER BY settlement.Id
FOR UPDATE;

SELECT actual.Id AS LockedCrosswalkId
FROM tmp_julio_expected_crosswalks expected
INNER JOIN liens_LegacyIdCrosswalks actual
  ON BINARY actual.TenantId = BINARY @tenant_id
 AND actual.SourceSystem = 'SL-CORE'
 AND BINARY actual.SourceTable = BINARY expected.SourceTable
 AND BINARY actual.LegacyId = BINARY expected.LegacyId
 AND BINARY actual.TargetEntity = BINARY expected.TargetEntity
 AND BINARY actual.TargetId = BINARY expected.TargetId
 AND BINARY actual.SourceHash = BINARY expected.SourceHash
 AND BINARY actual.ImportRunId = BINARY @import_run_id
WHERE @apply_permitted = 1
ORDER BY actual.Id
FOR UPDATE;

SELECT identity_user.Id INTO @locked_actor_user_id
FROM LS_IDENTITY.idt_Users identity_user
INNER JOIN LS_IDENTITY.idt_UserTenants user_tenant
  ON BINARY user_tenant.UserId = BINARY identity_user.Id
 AND BINARY user_tenant.TenantId = BINARY @tenant_id
 AND user_tenant.IsActive = 1
WHERE @apply_permitted = 1
  AND BINARY identity_user.Id = BINARY @actor_user_id
  AND identity_user.IsActive = 1
FOR SHARE;

SELECT target_case.Id INTO @locked_case_id
FROM liens_Cases target_case
WHERE @apply_permitted = 1
  AND BINARY target_case.Id = BINARY @case_id
FOR UPDATE;

SELECT lien.Id INTO @locked_lien_id
FROM liens_Liens lien
WHERE @apply_permitted = 1
  AND BINARY lien.Id = BINARY @lien_id
FOR UPDATE;

SELECT payment.Id INTO @locked_payment_id
FROM liens_SettlementPaymentDetails payment
WHERE @apply_permitted = 1
  AND BINARY payment.Id = BINARY @payment_id
FOR UPDATE;

SELECT COUNT(*) INTO @locked_crosswalk_count
FROM tmp_julio_expected_crosswalks expected
INNER JOIN liens_LegacyIdCrosswalks actual
  ON BINARY actual.TenantId = BINARY @tenant_id
 AND actual.SourceSystem = 'SL-CORE'
 AND BINARY actual.SourceTable = BINARY expected.SourceTable
 AND BINARY actual.LegacyId = BINARY expected.LegacyId
 AND BINARY actual.TargetEntity = BINARY expected.TargetEntity
 AND BINARY actual.TargetId = BINARY expected.TargetId
 AND BINARY actual.SourceHash = BINARY expected.SourceHash
 AND BINARY actual.ImportRunId = BINARY @import_run_id
WHERE @apply_permitted = 1;

SELECT COUNT(*), COALESCE(SUM(payment.Amount), 0)
INTO @locked_active_payment_count, @locked_active_payment_total
FROM liens_SettlementPaymentDetails payment
WHERE @apply_permitted = 1
  AND BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
  AND payment.IsDeleted = 0;

SELECT COUNT(*) INTO @locked_no_recovery_count
FROM liens_SettlementPaymentDetails payment
WHERE @apply_permitted = 1
  AND BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
  AND payment.IsDeleted = 0
  AND payment.Note LIKE '%status=4%';

SELECT COUNT(*) INTO @locked_reduction_count
FROM liens_LienReductions reduction
WHERE @apply_permitted = 1
  AND BINARY reduction.TenantId = BINARY @tenant_id
  AND BINARY reduction.CaseId = BINARY @case_id;

SELECT COUNT(*) INTO @locked_settlement_count
FROM liens_LienSettlements settlement
WHERE @apply_permitted = 1
  AND BINARY settlement.TenantId = BINARY @tenant_id
  AND BINARY settlement.CaseId = BINARY @case_id;

SELECT EXISTS (
    SELECT 1
    FROM liens_Cases target_case
    INNER JOIN liens_Liens lien
      ON BINARY lien.Id = BINARY @lien_id
    INNER JOIN liens_SettlementPaymentDetails payment
      ON BINARY payment.Id = BINARY @payment_id
    WHERE @apply_permitted = 1
      AND BINARY target_case.Id = BINARY @case_id
      AND BINARY target_case.TenantId = BINARY @tenant_id
      AND BINARY target_case.OrgId = BINARY @org_id
      AND BINARY target_case.CaseNumber = BINARY @case_number
      AND BINARY target_case.ExternalReference = BINARY 'SL-CORE:SL_CASE:32723'
      AND target_case.ClientFirstName = 'Julio'
      AND target_case.ClientLastName = 'De Anda Fajardo'
      AND target_case.Status = 'Closed'
      AND target_case.ClosedAtUtc = '2026-08-21 13:30:09.000000'
      AND target_case.DemandAmount = 3700.00
      AND target_case.SettlementAmount IS NULL
      AND target_case.UpdatedAtUtc = '2026-08-21 13:30:32.204246'
      AND BINARY lien.TenantId = BINARY @tenant_id
      AND BINARY lien.OrgId = BINARY @org_id
      AND BINARY lien.CaseId = BINARY @case_id
      AND BINARY lien.LienNumber = BINARY @lien_number
      AND lien.Status = 'Settled'
      AND lien.OriginalAmount = 3700.00
      AND lien.CurrentBalance = 0.00
      AND lien.PurchasePrice = 800.00
      AND lien.PayoffAmount IS NULL
      AND lien.ClosedAtUtc = '2026-07-28 00:00:00.000000'
      AND lien.UpdatedAtUtc = '2026-08-21 13:30:42.104181'
      AND BINARY payment.TenantId = BINARY @tenant_id
      AND BINARY payment.CaseId = BINARY @case_id
      AND BINARY payment.LienId = BINARY @lien_id
      AND payment.PaymentNumber = 1
      AND payment.Amount = 3700.00
      AND payment.PaymentDate IS NULL
      AND payment.Payee IS NULL
      AND payment.CheckNumber IS NULL
      AND BINARY payment.Note = BINARY @payment_note
      AND payment.IsDeleted = 0
      AND payment.CreatedAtUtc = '2026-07-28 17:19:53.000000'
      AND payment.UpdatedAtUtc = '2026-07-28 00:00:00.000000'
      AND BINARY payment.CreatedByUserId = BINARY @migration_user_id
      AND BINARY payment.UpdatedByUserId = BINARY @migration_user_id
) INTO @locked_initial_state_ok;

SET @locked_preimages = IF(
    @apply_permitted = 1
    AND BINARY @locked_actor_user_id = BINARY @actor_user_id
    AND BINARY @locked_case_id = BINARY @case_id
    AND BINARY @locked_lien_id = BINARY @lien_id
    AND BINARY @locked_payment_id = BINARY @payment_id
    AND @locked_crosswalk_count = 3
    AND @locked_active_payment_count = 1
    AND @locked_active_payment_total = 3700.00
    AND @locked_no_recovery_count = 1
    AND @locked_reduction_count = 0
    AND @locked_settlement_count = 0
    AND @locked_initial_state_ok = 1,
    1,
    0
);

SET @apply_permitted = IF(
    @apply_permitted = 1 AND @locked_preimages = 1,
    1,
    0
);
SET @repair_timestamp = UTC_TIMESTAMP(6);

UPDATE liens_SettlementPaymentDetails payment
SET payment.Amount = 0.00,
    payment.UpdatedAtUtc = @repair_timestamp,
    payment.UpdatedByUserId = @actor_user_id
WHERE @apply_permitted = 1
  AND BINARY payment.Id = BINARY @payment_id
  AND BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
  AND BINARY payment.LienId = BINARY @lien_id
  AND payment.PaymentNumber = 1
  AND payment.Amount = 3700.00
  AND payment.PaymentDate IS NULL
  AND payment.Payee IS NULL
  AND payment.CheckNumber IS NULL
  AND BINARY payment.Note = BINARY @payment_note
  AND payment.IsDeleted = 0
  AND payment.CreatedAtUtc = '2026-07-28 17:19:53.000000'
  AND payment.UpdatedAtUtc = '2026-07-28 00:00:00.000000'
  AND BINARY payment.CreatedByUserId = BINARY @migration_user_id
  AND BINARY payment.UpdatedByUserId = BINARY @migration_user_id;
SET @rows_updated = ROW_COUNT();

SELECT EXISTS (
    SELECT 1
    FROM liens_SettlementPaymentDetails payment
    WHERE BINARY payment.Id = BINARY @payment_id
      AND BINARY payment.TenantId = BINARY @tenant_id
      AND BINARY payment.CaseId = BINARY @case_id
      AND BINARY payment.LienId = BINARY @lien_id
      AND payment.PaymentNumber = 1
      AND payment.Amount = 0.00
      AND payment.PaymentDate IS NULL
      AND payment.Payee IS NULL
      AND payment.CheckNumber IS NULL
      AND BINARY payment.Note = BINARY @payment_note
      AND payment.IsDeleted = 0
      AND payment.CreatedAtUtc = '2026-07-28 17:19:53.000000'
      AND payment.UpdatedAtUtc = @repair_timestamp
      AND BINARY payment.CreatedByUserId = BINARY @migration_user_id
      AND BINARY payment.UpdatedByUserId = BINARY @actor_user_id
) INTO @repaired_payment_ok;

SELECT COUNT(*), COALESCE(SUM(payment.Amount), 0)
INTO @post_active_payment_count, @post_active_payment_total
FROM liens_SettlementPaymentDetails payment
WHERE BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
  AND payment.IsDeleted = 0;

SELECT COUNT(*) INTO @post_active_no_recovery_count
FROM liens_SettlementPaymentDetails payment
WHERE BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
  AND payment.IsDeleted = 0
  AND payment.Amount = 0.00
  AND payment.Note LIKE '%status=4%';

SELECT EXISTS (
    SELECT 1
    FROM liens_Cases target_case
    WHERE BINARY target_case.Id = BINARY @case_id
      AND BINARY target_case.TenantId = BINARY @tenant_id
      AND BINARY target_case.CaseNumber = BINARY @case_number
      AND target_case.Status = 'Closed'
      AND target_case.ClosedAtUtc = '2026-08-21 13:30:09.000000'
      AND target_case.DemandAmount = 3700.00
      AND target_case.SettlementAmount IS NULL
      AND target_case.UpdatedAtUtc = '2026-08-21 13:30:32.204246'
) INTO @post_case_unchanged;

SELECT EXISTS (
    SELECT 1
    FROM liens_Liens lien
    WHERE BINARY lien.Id = BINARY @lien_id
      AND BINARY lien.TenantId = BINARY @tenant_id
      AND BINARY lien.CaseId = BINARY @case_id
      AND BINARY lien.LienNumber = BINARY @lien_number
      AND lien.Status = 'Settled'
      AND lien.OriginalAmount = 3700.00
      AND lien.CurrentBalance = 0.00
      AND lien.PurchasePrice = 800.00
      AND lien.PayoffAmount IS NULL
      AND lien.ClosedAtUtc = '2026-07-28 00:00:00.000000'
      AND lien.UpdatedAtUtc = '2026-08-21 13:30:42.104181'
) INTO @post_lien_unchanged;

SET @postcondition_errors = IF(
    @repaired_payment_ok = 1
    AND @rows_updated = 1
    AND @post_active_payment_count = 1
    AND @post_active_payment_total = 0.00
    AND @post_active_no_recovery_count = 1
    AND @post_case_unchanged = 1
    AND @post_lien_unchanged = 1,
    0,
    1
);

SET @apply_permitted = IF(
    @apply_permitted = 1
    AND @rows_updated = 1
    AND @postcondition_errors = 0,
    1,
    0
);

-- MySQL supports prepared COMMIT but not prepared ROLLBACK. A rejected apply
-- prepares a harmless SELECT and reaches the literal rollback below.
SET @conditional_commit_sql = IF(@apply_permitted = 1, 'COMMIT', 'SELECT 1');
PREPARE julio_repair_conditional_commit FROM @conditional_commit_sql;
EXECUTE julio_repair_conditional_commit;
DEALLOCATE PREPARE julio_repair_conditional_commit;
ROLLBACK;

SELECT
    @apply_permitted AS ApplyPermitted,
    @changes_to_apply AS ExpectedUpdates,
    @locked_preimages AS LockedPreimages,
    @rows_updated AS RowsUpdated,
    @postcondition_errors AS PostconditionErrors,
    CASE
        WHEN @apply = 0 THEN 'Dry run complete: no changes were written.'
        WHEN @preflight_ok <> 1 THEN 'No changes written: preflight failed.'
        WHEN @expected_updates <> @changes_to_apply
            THEN 'No changes written: expected count does not match dry run.'
        WHEN LOWER(COALESCE(@expected_checksum, '')) <> LOWER(@plan_checksum)
            THEN 'No changes written: expected checksum does not match dry run.'
        WHEN @locked_preimages <> 1
            THEN 'No changes written: reviewed rows changed after preflight.'
        WHEN @rows_updated <> 1 OR @postcondition_errors <> 0
            THEN 'No changes written: update or postcondition validation failed.'
        ELSE 'Applied successfully: Amount Received is zero and No Recovery remains active.'
    END AS Result;
