SET @tenant_id = '019f1a05-7459-7855-b46b-110a702e37a4';
SET @org_id = '019f1a05-792b-7c0e-89a0-ae24990d1f89';
SET @migration_user_id = '019f1a05-792f-74f2-b071-4fdc0d6bd30a';
SET @approved_by_user_id = '019f1a05-792f-74f2-b071-4fdc0d6bd30a';
SET @lien_amount_source = 'Billing'; -- Change to exactly 'billing' or 'purchase'.

SET @legacy_program = '1';
SET @source_fingerprint = '3adccecf8a38114a14cd500240aab2a4db3d9bf45f00945c659dc3b5252663fe';
SET @mapping_version = 'sl-core-core-liens-v1';
SET @mapping_approval_reference = CONCAT('QA-SLCORE-P1-', DATE_FORMAT(UTC_TIMESTAMP(), '%Y%m%d%H%i%s'));
SET @mapping_manifest_hash = SHA2(
  CONCAT_WS('|', @mapping_version, @tenant_id, @org_id, @migration_user_id,
                 @legacy_program, @lien_amount_source, '1=Active', '2=Settled',
                 @source_fingerprint),
  256);

-- Identity is a separate database on the same MySQL server, not LS_QA_LIENS.
SELECT
  EXISTS (
    SELECT 1
    FROM LS_IDENTITY.idt_Organizations o
    WHERE o.Id = @org_id
      AND o.TenantId = @tenant_id
      AND o.OrgType = 'LIEN_OWNER'
      AND o.IsActive = 1
  ),
  EXISTS (
    SELECT 1
    FROM LS_IDENTITY.idt_Users u
    INNER JOIN LS_IDENTITY.idt_UserTenants ut
      ON ut.UserId = u.Id
     AND ut.TenantId = @tenant_id
     AND ut.IsActive = 1
    WHERE u.Id = @migration_user_id
      AND u.IsActive = 1
  ),
  EXISTS (
    SELECT 1
    FROM LS_IDENTITY.idt_Users u
    INNER JOIN LS_IDENTITY.idt_UserTenants ut
      ON ut.UserId = u.Id
     AND ut.TenantId = @tenant_id
     AND ut.IsActive = 1
    INNER JOIN LS_IDENTITY.idt_ScopedRoleAssignments sra
      ON sra.UserId = u.Id
     AND sra.IsActive = 1
    INNER JOIN LS_IDENTITY.idt_Roles r
      ON r.Id = sra.RoleId
    WHERE u.Id = @approved_by_user_id
      AND u.IsActive = 1
      AND r.Name = 'TenantAdmin'
  ),
  EXISTS (
    SELECT 1
    FROM LS_IDENTITY.idt_TenantProductEntitlements entitlement
    WHERE entitlement.TenantId = @tenant_id
      AND entitlement.ProductCode = 'SYNQ_LIENS'
      AND entitlement.Status = 'Active'
  ),
  EXISTS (
    SELECT 1
    FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE` provenance
    WHERE provenance.PROVENANCE_KEY = 'sl-core-current'
      AND LOWER(provenance.SOURCE_FINGERPRINT) = @source_fingerprint
      AND provenance.IMPORT_SCOPE = 'sl-core-core-liens-v1'
  )
INTO @org_is_valid, @migration_user_is_valid, @approver_is_valid,
     @entitlement_is_valid, @provenance_is_valid;

SELECT @@autocommit INTO @approval_autocommit;
SELECT GET_LOCK(CONCAT('LSLTI:approval:', @tenant_id, ':SL-CORE'), 10)
INTO @approval_lock_acquired;

SELECT COUNT(*) INTO @active_approval_count
FROM liens_LegacyImportApprovals
WHERE TenantId = @tenant_id
  AND SourceSystem = 'SL-CORE'
  AND Status = 'Approved'
  AND ConsumedAtUtc IS NULL
  AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > UTC_TIMESTAMP(6));

INSERT INTO liens_LegacyImportApprovals
  (Id, TenantId, OrgId, SourceSystem, SourceFingerprint, LegacyProgram,
   MappingVersion, MappingManifestHash, MappingApprovalReference,
   LienAmountSource, LegacyStatusOneTarget, LegacyStatusTwoTarget,
   MigrationUserId, ApprovedByUserId, Status, ApprovedAtUtc, ExpiresAtUtc,
   ConsumedAtUtc, ConsumedByRunId)
SELECT
  UUID(), @tenant_id, @org_id, 'SL-CORE', @source_fingerprint, @legacy_program,
  @mapping_version, @mapping_manifest_hash, @mapping_approval_reference,
  @lien_amount_source, 'Active', 'Settled', @migration_user_id,
  @approved_by_user_id, 'Approved', UTC_TIMESTAMP(6),
  DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 2 HOUR), NULL, NULL
FROM DUAL
WHERE @lien_amount_source IN ('billing', 'purchase')
  AND COALESCE(@approval_autocommit, 0) = 1
  AND COALESCE(@approval_lock_acquired, 0) = 1
  AND @active_approval_count = 0
  AND @org_is_valid = 1
  AND @migration_user_is_valid = 1
  AND @approver_is_valid = 1
  AND @entitlement_is_valid = 1
  AND @provenance_is_valid = 1;

SET @approval_rows_inserted = ROW_COUNT();
SELECT RELEASE_LOCK(CONCAT('LSLTI:approval:', @tenant_id, ':SL-CORE'))
INTO @approval_lock_released;

SELECT
  @approval_rows_inserted AS ApprovalRowsInserted,
  @active_approval_count AS ExistingActiveApprovals,
  CASE
    WHEN @approval_rows_inserted = 1 THEN 'Approval created. Run importer preflight with apply = 0 within two hours.'
    WHEN @lien_amount_source NOT IN ('billing', 'purchase') THEN 'No row created: set lien amount source to billing or purchase.'
    WHEN COALESCE(@approval_autocommit, 0) <> 1 THEN 'No row created: enable autocommit and execute the script again.'
    WHEN COALESCE(@approval_lock_acquired, 0) <> 1 THEN 'No row created: another approval release is in progress; wait and retry.'
    WHEN @active_approval_count <> 0 THEN 'No row created: an active approval already exists; do not create another.'
    WHEN @org_is_valid <> 1 THEN 'No row created: the expected active LIEN_OWNER organization was not found for the tenant.'
    WHEN @migration_user_is_valid <> 1 THEN 'No row created: the expected migration user is not active in this tenant.'
    WHEN @approver_is_valid <> 1 THEN 'No row created: the expected approver is not an active TenantAdmin in this tenant.'
    WHEN @entitlement_is_valid <> 1 THEN 'No row created: the tenant has no active SYNQ_LIENS entitlement.'
    ELSE 'No row created: the controlled SL-CORE staging receipt is missing or does not match the dump.'
  END AS Result;

SELECT Id, TenantId, OrgId, LegacyProgram, LienAmountSource,
       LegacyStatusOneTarget, LegacyStatusTwoTarget, MigrationUserId,
       ApprovedByUserId, Status, ApprovedAtUtc, ExpiresAtUtc
FROM liens_LegacyImportApprovals
WHERE TenantId = @tenant_id
  AND SourceSystem = 'SL-CORE'
ORDER BY ApprovedAtUtc DESC;
