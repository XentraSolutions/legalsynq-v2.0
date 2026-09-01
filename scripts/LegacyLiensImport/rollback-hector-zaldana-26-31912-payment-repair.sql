-- Emergency rollback for repair-hector-zaldana-26-31912-imported-payment.sql.
--
-- WARNING: this intentionally restores the false $17,228 received amount and
-- No Recovery classification. It does not close either lien. Use only under
-- an approved rollback decision. Any later edit to the repaired payment row
-- blocks rollback.
--
-- Run the complete file in DBeaver on an explicitly selected LS_LIENS
-- connection. Leave @apply = 0 for preflight. Review every result, copy
-- ChangesToApply and PlanChecksum into the expected variables, set @apply = 1,
-- and execute the complete file again.
--
-- Error/reference prefix: LSLHZR-RB-

SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;

SET @rollback_actor_user_id = '<identity-user-guid>';
SET @apply = 0;
SET @expected_updates = -1;
SET @expected_checksum = NULL;

SET @target_schema = DATABASE();
SET @tenant_id = '019f1a05-7459-7855-b46b-110a702e37a4';
SET @org_id = '019f1a05-792b-7c0e-89a0-ae24990d1f89';
SET @case_id = '6da8ac27-9d64-11f1-b823-12a7a8afef43';
SET @case_number = '26-31912';
SET @lien_01_id = '6e64c54d-9d64-11f1-b823-12a7a8afef43';
SET @lien_02_id = '6e64c5d7-9d64-11f1-b823-12a7a8afef43';
SET @payment_id = '72a94988-9d64-11f1-b823-12a7a8afef43';
SET @import_run_id = '72ff0756-9d64-11f1-b823-12a7a8afef43';
SET @migration_user_id = '019f1a05-792f-74f2-b071-4fdc0d6bd30a';
SET @payment_note =
    'legacyPaymentDetailId=41410; legacyCaseId=31912; type=; status=4; lienStatus=2; checkAmount=; netProfit=';

SET @rollback_actor_user_id = LOWER(TRIM(@rollback_actor_user_id));
SET @apply = IF(@apply = 1, 1, 0);
SET @schema_ok = BINARY @target_schema = BINARY 'LS_LIENS';
SET @actor_guid_ok =
    @rollback_actor_user_id <> '<identity-user-guid>'
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

DROP TEMPORARY TABLE IF EXISTS tmp_hector_rollback_crosswalks;
CREATE TEMPORARY TABLE tmp_hector_rollback_crosswalks (
    SourceTable VARCHAR(100) NOT NULL,
    LegacyId VARCHAR(100) NOT NULL,
    TargetEntity VARCHAR(100) NOT NULL,
    TargetId CHAR(36) NOT NULL,
    SourceHash CHAR(64) NOT NULL,
    PRIMARY KEY (SourceTable, LegacyId)
) ENGINE=InnoDB;

INSERT INTO tmp_hector_rollback_crosswalks
    (SourceTable, LegacyId, TargetEntity, TargetId, SourceHash)
VALUES
    ('SL_CASE', '31912', 'Case',
     '6da8ac27-9d64-11f1-b823-12a7a8afef43',
     '196e4df2b292728cdb8159b5a338dd01f3ab3683c638624f3e75362d87212a5a'),
    ('SL_LEINS_MEDICAL', '70070', 'Lien',
     '6e64c54d-9d64-11f1-b823-12a7a8afef43',
     'b0a8b3a76990e504075ed9dd79e08665c5cbea57015899021bacb151d08a0e34'),
    ('SL_LEINS_MEDICAL', '73247', 'Lien',
     '6e64c5d7-9d64-11f1-b823-12a7a8afef43',
     '1a368bb1965a23502eb10e347912cf5126dfba76e701c14e29edcebf4ccedee4'),
    ('SL_LIENS_SETTLEMENT_PAYMENT_DETAILS', '41410',
     'SettlementPaymentDetail',
     '72a94988-9d64-11f1-b823-12a7a8afef43',
     'b54367af4b837356286b0e3d1fa628558930b0814ae8cebadb891e3ee2722cba');

SELECT COUNT(*) INTO @matched_crosswalk_count
FROM tmp_hector_rollback_crosswalks expected
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
      AND BINARY target_case.ExternalReference = BINARY 'SL-CORE:SL_CASE:31912'
      AND target_case.ClientFirstName = 'Hector'
      AND target_case.ClientLastName = 'Zaldana'
      AND target_case.Status = 'PreDemand'
      AND target_case.DateOfIncident = '2025-02-28'
      AND target_case.OpenedAtUtc = '2026-05-04 18:14:04.000000'
      AND target_case.ClosedAtUtc IS NULL
      AND target_case.DemandAmount = 32078.00
      AND target_case.SettlementAmount IS NULL
      AND target_case.CreatedAtUtc = '2026-05-04 18:14:04.000000'
      AND target_case.UpdatedAtUtc = '2026-08-21 13:30:32.204246'
      AND BINARY target_case.CreatedByUserId = BINARY @migration_user_id
      AND BINARY target_case.UpdatedByUserId = BINARY @migration_user_id
) INTO @case_ok;

SELECT COUNT(*) INTO @open_lien_count
FROM liens_Liens lien
WHERE BINARY lien.TenantId = BINARY @tenant_id
  AND BINARY lien.OrgId = BINARY @org_id
  AND BINARY lien.CaseId = BINARY @case_id
  AND lien.Status = 'Active'
  AND lien.ClosedAtUtc IS NULL
  AND (
      (BINARY lien.Id = BINARY @lien_01_id
       AND BINARY lien.LienNumber = BINARY '26-31912-01'
       AND BINARY lien.ExternalReference = BINARY 'SL-CORE:SL_LEINS_MEDICAL:70070'
       AND lien.OriginalAmount = 17228.00
       AND lien.CurrentBalance = 17228.00
       AND lien.PurchasePrice = 4307.00
       AND lien.PayoffAmount IS NULL
       AND lien.InitialServiceDate = '2026-04-17'
       AND lien.PurchaseDate = '2026-05-15'
       AND lien.CreatedAtUtc = '2026-05-04 18:14:16.000000'
       AND lien.UpdatedAtUtc = '2026-08-21 13:30:42.104181')
      OR
      (BINARY lien.Id = BINARY @lien_02_id
       AND BINARY lien.LienNumber = BINARY '26-31912-02'
       AND BINARY lien.ExternalReference = BINARY 'SL-CORE:SL_LEINS_MEDICAL:73247'
       AND lien.OriginalAmount = 14850.00
       AND lien.CurrentBalance = 14850.00
       AND lien.PurchasePrice = 3712.50
       AND lien.PayoffAmount IS NULL
       AND lien.InitialServiceDate = '2026-08-07'
       AND lien.PurchaseDate = '2026-09-15'
       AND lien.CreatedAtUtc = '2026-08-19 20:24:54.000000'
       AND lien.UpdatedAtUtc = '2026-08-21 13:30:42.104181')
  )
  AND BINARY lien.CreatedByUserId = BINARY @migration_user_id
  AND BINARY lien.UpdatedByUserId = BINARY @migration_user_id;

SELECT COUNT(*) INTO @case_lien_count
FROM liens_Liens lien
WHERE BINARY lien.TenantId = BINARY @tenant_id
  AND BINARY lien.CaseId = BINARY @case_id;

SET @repair_timestamp = NULL;
SET @repair_actor_user_id = NULL;
SELECT COUNT(*), MAX(payment.UpdatedAtUtc), MAX(payment.UpdatedByUserId)
INTO @repaired_state_count, @repair_timestamp, @repair_actor_user_id
FROM liens_SettlementPaymentDetails payment
WHERE BINARY payment.Id = BINARY @payment_id
  AND BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
  AND BINARY payment.LienId = BINARY @lien_01_id
  AND payment.PaymentNumber = 1
  AND payment.Amount = 17228.00
  AND payment.PaymentDate IS NULL
  AND payment.Payee IS NULL
  AND payment.CheckNumber IS NULL
  AND BINARY payment.Note = BINARY @payment_note
  AND payment.IsDeleted = 1
  AND payment.CreatedAtUtc = '2026-07-16 21:47:24.000000'
  AND payment.UpdatedAtUtc > '2026-07-16 00:00:00.000000'
  AND BINARY payment.CreatedByUserId = BINARY @migration_user_id
  AND payment.UpdatedByUserId IS NOT NULL;

SELECT EXISTS (
    SELECT 1
    FROM liens_SettlementPaymentDetails payment
    WHERE BINARY payment.Id = BINARY @payment_id
      AND BINARY payment.TenantId = BINARY @tenant_id
      AND BINARY payment.CaseId = BINARY @case_id
      AND BINARY payment.LienId = BINARY @lien_01_id
      AND payment.PaymentNumber = 1
      AND payment.Amount = 17228.00
      AND payment.PaymentDate IS NULL
      AND payment.Payee IS NULL
      AND payment.CheckNumber IS NULL
      AND BINARY payment.Note = BINARY @payment_note
      AND payment.IsDeleted = 0
      AND payment.CreatedAtUtc = '2026-07-16 21:47:24.000000'
      AND payment.UpdatedAtUtc > '2026-07-16 00:00:00.000000'
      AND BINARY payment.CreatedByUserId = BINARY @migration_user_id
      AND payment.UpdatedByUserId IS NOT NULL
) INTO @rolled_back_state_ok;

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

SET @repaired_financial_state_ok =
    @active_payment_count = 0
    AND @active_payment_total = 0.00
    AND @active_no_recovery_count = 0;
SET @rolled_back_financial_state_ok =
    @active_payment_count = 1
    AND @active_payment_total = 17228.00
    AND @active_no_recovery_count = 1;

SET @context_ok =
    @case_ok = 1
    AND @case_lien_count = 2
    AND @open_lien_count = 2
    AND @matched_crosswalk_count = 4
    AND @reduction_count = 0
    AND @settlement_count = 0;

SET @preflight_ok =
    @schema_ok = 1
    AND @actor_guid_ok = 1
    AND @actor_tenant_ok = 1
    AND @required_table_count = 6
    AND @context_ok = 1
    AND ((@repaired_state_count = 1 AND @repaired_financial_state_ok = 1
          AND @rolled_back_state_ok = 0)
      OR (@repaired_state_count = 0 AND @rolled_back_financial_state_ok = 1
          AND @rolled_back_state_ok = 1));

SET @changes_to_apply = IF(@repaired_state_count = 1, 1, 0);
SET @already_rolled_back = IF(@rolled_back_state_ok = 1, 1, 0);
SET @blocking_rows = IF(@preflight_ok = 1, 0, 1);

SET @plan_checksum = SHA2(
    CONCAT_WS(
        '|',
        @target_schema, @tenant_id, @org_id, @rollback_actor_user_id,
        @case_id, @case_number, @lien_01_id, @lien_02_id, @payment_id,
        @import_run_id,
        COALESCE(DATE_FORMAT(@repair_timestamp, '%Y-%m-%d %H:%i:%s.%f'), ''),
        COALESCE(@repair_actor_user_id, ''),
        '196e4df2b292728cdb8159b5a338dd01f3ab3683c638624f3e75362d87212a5a',
        'b0a8b3a76990e504075ed9dd79e08665c5cbea57015899021bacb151d08a0e34',
        '1a368bb1965a23502eb10e347912cf5126dfba76e701c14e29edcebf4ccedee4',
        'b54367af4b837356286b0e3d1fa628558930b0814ae8cebadb891e3ee2722cba',
        CAST(@changes_to_apply AS CHAR),
        'PreDemand', '32078.00', 'NULL',
        '17228.00', 'NULL', 'NULL', 'NULL', @payment_note
    ),
    256
);

SET @apply_permitted =
    @apply = 1
    AND @preflight_ok = 1
    AND @repaired_state_count = 1
    AND @repaired_financial_state_ok = 1
    AND @changes_to_apply = 1
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
    @repair_timestamp AS ForwardRepairTimestamp,
    @repair_actor_user_id AS ForwardRepairActor,
    @plan_checksum AS PlanChecksum;

SELECT
    @payment_id AS PaymentId,
    @lien_01_id AS LienId,
    '26-31912-01' AS LienNumber,
    17228.00 AS RestoredAmount,
    @payment_note AS RestoredNoRecoveryNote,
    @open_lien_count AS PreservedOpenLienCount;

SET @locked_actor_user_id = NULL;
SET @locked_case_id = NULL;
SET @locked_lien_01_id = NULL;
SET @locked_lien_02_id = NULL;
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
FROM tmp_hector_rollback_crosswalks expected
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
  AND BINARY identity_user.Id = BINARY @rollback_actor_user_id
  AND identity_user.IsActive = 1
FOR SHARE;

SELECT target_case.Id INTO @locked_case_id
FROM liens_Cases target_case
WHERE @apply_permitted = 1
  AND BINARY target_case.Id = BINARY @case_id
  AND BINARY target_case.TenantId = BINARY @tenant_id
  AND BINARY target_case.OrgId = BINARY @org_id
  AND BINARY target_case.CaseNumber = BINARY @case_number
  AND BINARY target_case.ExternalReference = BINARY 'SL-CORE:SL_CASE:31912'
  AND target_case.ClientFirstName = 'Hector'
  AND target_case.ClientLastName = 'Zaldana'
  AND target_case.Status = 'PreDemand'
  AND target_case.DateOfIncident = '2025-02-28'
  AND target_case.OpenedAtUtc = '2026-05-04 18:14:04.000000'
  AND target_case.ClosedAtUtc IS NULL
  AND target_case.DemandAmount = 32078.00
  AND target_case.SettlementAmount IS NULL
  AND target_case.CreatedAtUtc = '2026-05-04 18:14:04.000000'
  AND target_case.UpdatedAtUtc = '2026-08-21 13:30:32.204246'
  AND BINARY target_case.CreatedByUserId = BINARY @migration_user_id
  AND BINARY target_case.UpdatedByUserId = BINARY @migration_user_id
FOR UPDATE;

SELECT lien.Id AS LockedCaseLienId
FROM liens_Liens lien
WHERE @apply_permitted = 1
  AND BINARY lien.TenantId = BINARY @tenant_id
  AND BINARY lien.CaseId = BINARY @case_id
ORDER BY lien.Id
FOR UPDATE;

SELECT lien.Id INTO @locked_lien_01_id
FROM liens_Liens lien
WHERE @apply_permitted = 1
  AND BINARY lien.Id = BINARY @lien_01_id
  AND BINARY lien.TenantId = BINARY @tenant_id
  AND BINARY lien.CaseId = BINARY @case_id
  AND lien.Status = 'Active'
  AND lien.CurrentBalance = 17228.00
  AND lien.ClosedAtUtc IS NULL
  AND lien.UpdatedAtUtc = '2026-08-21 13:30:42.104181'
FOR UPDATE;

SELECT lien.Id INTO @locked_lien_02_id
FROM liens_Liens lien
WHERE @apply_permitted = 1
  AND BINARY lien.Id = BINARY @lien_02_id
  AND BINARY lien.TenantId = BINARY @tenant_id
  AND BINARY lien.CaseId = BINARY @case_id
  AND lien.Status = 'Active'
  AND lien.CurrentBalance = 14850.00
  AND lien.ClosedAtUtc IS NULL
  AND lien.UpdatedAtUtc = '2026-08-21 13:30:42.104181'
FOR UPDATE;

SELECT payment.Id INTO @locked_payment_id
FROM liens_SettlementPaymentDetails payment
WHERE @apply_permitted = 1
  AND BINARY payment.Id = BINARY @payment_id
  AND BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
  AND BINARY payment.LienId = BINARY @lien_01_id
  AND payment.PaymentNumber = 1
  AND payment.Amount = 17228.00
  AND payment.PaymentDate IS NULL
  AND payment.Payee IS NULL
  AND payment.CheckNumber IS NULL
  AND BINARY payment.Note = BINARY @payment_note
  AND payment.IsDeleted = 1
  AND payment.CreatedAtUtc = '2026-07-16 21:47:24.000000'
  AND payment.UpdatedAtUtc = @repair_timestamp
  AND BINARY payment.CreatedByUserId = BINARY @migration_user_id
  AND BINARY payment.UpdatedByUserId = BINARY @repair_actor_user_id
FOR UPDATE;

SELECT COUNT(*), COALESCE(SUM(payment.Amount), 0)
INTO @locked_active_payment_count, @locked_active_payment_total
FROM liens_SettlementPaymentDetails payment
WHERE @apply_permitted = 1
  AND BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
  AND payment.IsDeleted = 0;

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

SELECT COUNT(*) INTO @locked_crosswalk_count
FROM tmp_hector_rollback_crosswalks expected
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

SELECT COUNT(*) INTO @locked_open_lien_count
FROM liens_Liens lien
WHERE @apply_permitted = 1
  AND BINARY lien.TenantId = BINARY @tenant_id
  AND BINARY lien.OrgId = BINARY @org_id
  AND BINARY lien.CaseId = BINARY @case_id
  AND lien.Status = 'Active'
  AND lien.ClosedAtUtc IS NULL
  AND (
      (BINARY lien.Id = BINARY @lien_01_id
       AND BINARY lien.LienNumber = BINARY '26-31912-01'
       AND BINARY lien.ExternalReference = BINARY 'SL-CORE:SL_LEINS_MEDICAL:70070'
       AND lien.OriginalAmount = 17228.00
       AND lien.CurrentBalance = 17228.00
       AND lien.PurchasePrice = 4307.00
       AND lien.PayoffAmount IS NULL
       AND lien.InitialServiceDate = '2026-04-17'
       AND lien.PurchaseDate = '2026-05-15'
       AND lien.CreatedAtUtc = '2026-05-04 18:14:16.000000'
       AND lien.UpdatedAtUtc = '2026-08-21 13:30:42.104181')
      OR
      (BINARY lien.Id = BINARY @lien_02_id
       AND BINARY lien.LienNumber = BINARY '26-31912-02'
       AND BINARY lien.ExternalReference = BINARY 'SL-CORE:SL_LEINS_MEDICAL:73247'
       AND lien.OriginalAmount = 14850.00
       AND lien.CurrentBalance = 14850.00
       AND lien.PurchasePrice = 3712.50
       AND lien.PayoffAmount IS NULL
       AND lien.InitialServiceDate = '2026-08-07'
       AND lien.PurchaseDate = '2026-09-15'
       AND lien.CreatedAtUtc = '2026-08-19 20:24:54.000000'
       AND lien.UpdatedAtUtc = '2026-08-21 13:30:42.104181')
  )
  AND BINARY lien.CreatedByUserId = BINARY @migration_user_id
  AND BINARY lien.UpdatedByUserId = BINARY @migration_user_id;

SELECT COUNT(*) INTO @locked_case_lien_count
FROM liens_Liens lien
WHERE @apply_permitted = 1
  AND BINARY lien.TenantId = BINARY @tenant_id
  AND BINARY lien.CaseId = BINARY @case_id;

SET @locked_preimages = IF(
    @apply_permitted = 1
    AND BINARY @locked_actor_user_id = BINARY @rollback_actor_user_id
    AND BINARY @locked_case_id = BINARY @case_id
    AND BINARY @locked_lien_01_id = BINARY @lien_01_id
    AND BINARY @locked_lien_02_id = BINARY @lien_02_id
    AND BINARY @locked_payment_id = BINARY @payment_id
    AND @locked_active_payment_count = 0
    AND @locked_active_payment_total = 0.00
    AND @locked_reduction_count = 0
    AND @locked_settlement_count = 0
    AND @locked_crosswalk_count = 4
    AND @locked_case_lien_count = 2
    AND @locked_open_lien_count = 2,
    1,
    0
);

SET @apply_permitted = IF(
    @apply_permitted = 1 AND @locked_preimages = 1,
    1,
    0
);
SET @rollback_timestamp = UTC_TIMESTAMP(6);

UPDATE liens_SettlementPaymentDetails payment
SET payment.IsDeleted = 0,
    payment.UpdatedAtUtc = @rollback_timestamp,
    payment.UpdatedByUserId = @rollback_actor_user_id
WHERE @apply_permitted = 1
  AND BINARY payment.Id = BINARY @payment_id
  AND BINARY payment.TenantId = BINARY @tenant_id
  AND BINARY payment.CaseId = BINARY @case_id
  AND BINARY payment.LienId = BINARY @lien_01_id
  AND payment.PaymentNumber = 1
  AND payment.Amount = 17228.00
  AND payment.PaymentDate IS NULL
  AND payment.Payee IS NULL
  AND payment.CheckNumber IS NULL
  AND BINARY payment.Note = BINARY @payment_note
  AND payment.IsDeleted = 1
  AND payment.CreatedAtUtc = '2026-07-16 21:47:24.000000'
  AND payment.UpdatedAtUtc = @repair_timestamp
  AND BINARY payment.CreatedByUserId = BINARY @migration_user_id
  AND BINARY payment.UpdatedByUserId = BINARY @repair_actor_user_id;
SET @rows_updated = ROW_COUNT();

SELECT EXISTS (
    SELECT 1
    FROM liens_SettlementPaymentDetails payment
    WHERE BINARY payment.Id = BINARY @payment_id
      AND BINARY payment.TenantId = BINARY @tenant_id
      AND BINARY payment.CaseId = BINARY @case_id
      AND BINARY payment.LienId = BINARY @lien_01_id
      AND payment.PaymentNumber = 1
      AND payment.Amount = 17228.00
      AND payment.PaymentDate IS NULL
      AND payment.Payee IS NULL
      AND payment.CheckNumber IS NULL
      AND BINARY payment.Note = BINARY @payment_note
      AND payment.IsDeleted = 0
      AND payment.CreatedAtUtc = '2026-07-16 21:47:24.000000'
      AND payment.UpdatedAtUtc = @rollback_timestamp
      AND BINARY payment.CreatedByUserId = BINARY @migration_user_id
      AND BINARY payment.UpdatedByUserId = BINARY @rollback_actor_user_id
) INTO @rollback_row_ok;

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
  AND payment.Note LIKE '%status=4%';

SELECT COUNT(*) INTO @post_open_lien_count
FROM liens_Liens lien
WHERE BINARY lien.TenantId = BINARY @tenant_id
  AND BINARY lien.OrgId = BINARY @org_id
  AND BINARY lien.CaseId = BINARY @case_id
  AND lien.Status = 'Active'
  AND lien.ClosedAtUtc IS NULL
  AND (
      (BINARY lien.Id = BINARY @lien_01_id
       AND lien.CurrentBalance = 17228.00
       AND lien.PayoffAmount IS NULL)
      OR
      (BINARY lien.Id = BINARY @lien_02_id
       AND lien.CurrentBalance = 14850.00
       AND lien.PayoffAmount IS NULL)
  )
  AND lien.UpdatedAtUtc = '2026-08-21 13:30:42.104181'
  AND BINARY lien.UpdatedByUserId = BINARY @migration_user_id;

SET @postcondition_errors = IF(
    @rollback_row_ok = 1
    AND @rows_updated = 1
    AND @post_active_payment_count = 1
    AND @post_active_payment_total = 17228.00
    AND @post_active_no_recovery_count = 1
    AND @post_open_lien_count = 2,
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

SET @conditional_commit_sql = IF(@apply_permitted = 1, 'COMMIT', 'SELECT 1');
PREPARE hector_rollback_conditional_commit FROM @conditional_commit_sql;
EXECUTE hector_rollback_conditional_commit;
DEALLOCATE PREPARE hector_rollback_conditional_commit;
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
            THEN 'No changes written: repaired row changed after preflight.'
        WHEN @rows_updated <> 1 OR @postcondition_errors <> 0
            THEN 'No changes written: update or postcondition validation failed.'
        ELSE 'Rollback applied: false receipt and No Recovery were restored.'
    END AS Result;
