-- One-time QA release approval for the supplied SL-CORE snapshot.
--
-- Run this as an authorized QA database operator against the same MySQL server
-- as LS_QA_LIENS using "Execute SQL Script". This file has no DELIMITER commands.
-- Autocommit must be enabled; the script makes one atomic insert only after
-- acquiring a tenant-specific advisory lock.
--
-- REQUIRED: replace the two NULL values below. Do not use the migration user
-- as the approver unless that user is actually an active PlatformAdmin or
-- TenantAdmin. This script inserts nothing unless every guard passes.

USE LS_QA_LIENS;

SET @tenant_id = '019ea7f6-21e9-7421-ab54-7846cdc6bc76';
SET @org_id = '019ea7f6-283d-7891-a78b-3838cdecca0c';
SET @migration_user_id = '019ea81e-8b72-7a26-b7bc-a29be8224a71';
SET @approved_by_user_id = '019ea7f6-284d-7310-9c92-349f2d97b154';
SET @lien_amount_source = 'billing'; -- or 'purchase', per your approved policy

-- The supplied QA Identity dump is `LS_QA_IDENTITY`. Keep this explicit so the
-- dynamically qualified checks never resolve `idt_*` tables in LS_QA_LIENS.
-- Set this to NULL only when running against a different, verified QA Identity
-- schema; the candidate query below will then refuse to guess.
SET @identity_schema_override = 'LS_QA_IDENTITY';

SET @legacy_program = '1';
SET @source_fingerprint = '3adccecf8a38114a14cd500240aab2a4db3d9bf45f00945c659dc3b5252663fe';
SET @mapping_version = 'sl-core-core-liens-v1';
SET @mapping_approval_reference = CONCAT('QA-SLCORE-P1-', DATE_FORMAT(UTC_TIMESTAMP(), '%Y%m%d%H%i%s'));
SET @mapping_manifest_hash = SHA2(
  CONCAT_WS('|', @mapping_version, @tenant_id, @org_id, @migration_user_id,
                 @legacy_program, @lien_amount_source, '1=Active', '2=Settled',
                 @source_fingerprint),
  256);

-- Identity is usually a different schema from LS_QA_LIENS. Find the only
-- schema that contains the required Identity tables; refuse to guess if there
-- is none or more than one.
SELECT candidate.table_schema AS IdentitySchemaCandidate
FROM (
  SELECT table_schema
  FROM information_schema.tables
  WHERE table_type = 'BASE TABLE'
    AND table_name IN ('idt_Users', 'idt_UserTenants', 'idt_Organizations',
                       'idt_ScopedRoleAssignments', 'idt_Roles',
                       'idt_TenantProductEntitlements')
  GROUP BY table_schema
  HAVING COUNT(DISTINCT table_name) = 6
) candidate
ORDER BY candidate.table_schema;

SET @identity_schema_candidate_count = (
  SELECT COUNT(*)
  FROM (
    SELECT table_schema
    FROM information_schema.tables
    WHERE table_type = 'BASE TABLE'
      AND table_name IN ('idt_Users', 'idt_UserTenants', 'idt_Organizations',
                         'idt_ScopedRoleAssignments', 'idt_Roles',
                         'idt_TenantProductEntitlements')
    GROUP BY table_schema
    HAVING COUNT(DISTINCT table_name) = 6
  ) candidate
);
SET @discovered_identity_schema = (
  SELECT MIN(candidate.table_schema)
  FROM (
    SELECT table_schema
    FROM information_schema.tables
    WHERE table_type = 'BASE TABLE'
      AND table_name IN ('idt_Users', 'idt_UserTenants', 'idt_Organizations',
                         'idt_ScopedRoleAssignments', 'idt_Roles',
                         'idt_TenantProductEntitlements')
    GROUP BY table_schema
    HAVING COUNT(DISTINCT table_name) = 6
  ) candidate
);
SET @identity_schema_override_is_valid = (
  SELECT EXISTS (
    SELECT 1
    FROM (
      SELECT table_schema
      FROM information_schema.tables
      WHERE table_type = 'BASE TABLE'
        AND table_name IN ('idt_Users', 'idt_UserTenants', 'idt_Organizations',
                           'idt_ScopedRoleAssignments', 'idt_Roles',
                           'idt_TenantProductEntitlements')
      GROUP BY table_schema
      HAVING COUNT(DISTINCT table_name) = 6
    ) candidate
    WHERE candidate.table_schema = @identity_schema_override
  )
);
SET @identity_schema = CASE
  WHEN @identity_schema_override IS NULL AND @identity_schema_candidate_count = 1 THEN @discovered_identity_schema
  WHEN @identity_schema_override IS NOT NULL AND @identity_schema_override_is_valid = 1 THEN @identity_schema_override
  ELSE NULL
END;
SET @identity_schema_identifier = CASE
  WHEN @identity_schema IS NULL THEN NULL
  ELSE CONCAT('`', REPLACE(@identity_schema, '`', '``'), '`')
END;

SELECT @identity_schema AS IdentitySchemaUsed;

-- Use this result to select the approver UUID before replacing the NULL above.
SET @approver_list_sql = CASE
  WHEN @identity_schema_identifier IS NULL THEN
    'SELECT ''No unique Identity schema found. Set @identity_schema_override.'' AS Result'
  ELSE CONCAT(
    'SELECT DISTINCT u.Id AS ApprovedByUserId, u.Email, r.Name AS AdminRole ',
    'FROM ', @identity_schema_identifier, '.`idt_Users` u ',
    'INNER JOIN ', @identity_schema_identifier, '.`idt_ScopedRoleAssignments` sra ',
      'ON sra.UserId = u.Id AND sra.IsActive = 1 ',
    'INNER JOIN ', @identity_schema_identifier, '.`idt_Roles` r ON r.Id = sra.RoleId ',
    'WHERE u.IsActive = 1 AND (r.Name = ''PlatformAdmin'' OR ',
      '(r.Name = ''TenantAdmin'' AND EXISTS (SELECT 1 FROM ', @identity_schema_identifier,
      '.`idt_UserTenants` ut WHERE ut.UserId = u.Id AND ut.TenantId = @tenant_id AND ut.IsActive = 1))) ',
    'ORDER BY r.Name, u.Email')
END;
PREPARE approval_approver_list FROM @approver_list_sql;
EXECUTE approval_approver_list;
DEALLOCATE PREPARE approval_approver_list;

-- All Identity checks are dynamically qualified after safe schema discovery.
SET @identity_guard_sql = CASE
  WHEN @identity_schema_identifier IS NULL THEN
    'SELECT 0, 0, 0, 0 INTO @org_is_valid, @migration_user_is_valid, @approver_is_valid, @entitlement_is_valid'
  ELSE CONCAT(
    'SELECT ',
    'EXISTS (SELECT 1 FROM ', @identity_schema_identifier, '.`idt_Organizations` o ',
      'WHERE o.Id = @org_id AND o.TenantId = @tenant_id AND o.IsActive = 1), ',
    'EXISTS (SELECT 1 FROM ', @identity_schema_identifier, '.`idt_Users` migration_user ',
      'INNER JOIN ', @identity_schema_identifier, '.`idt_UserTenants` ut ON ut.UserId = migration_user.Id ',
      'WHERE migration_user.Id = @migration_user_id AND migration_user.IsActive = 1 ',
        'AND ut.TenantId = @tenant_id AND ut.IsActive = 1), ',
    'EXISTS (SELECT 1 FROM ', @identity_schema_identifier, '.`idt_Users` approver ',
      'INNER JOIN ', @identity_schema_identifier, '.`idt_ScopedRoleAssignments` sra ON sra.UserId = approver.Id ',
      'INNER JOIN ', @identity_schema_identifier, '.`idt_Roles` r ON r.Id = sra.RoleId ',
      'WHERE approver.Id = @approved_by_user_id AND approver.IsActive = 1 AND sra.IsActive = 1 ',
        'AND (r.Name = ''PlatformAdmin'' OR (r.Name = ''TenantAdmin'' AND EXISTS ',
          '(SELECT 1 FROM ', @identity_schema_identifier, '.`idt_UserTenants` approver_tenant ',
          'WHERE approver_tenant.UserId = approver.Id AND approver_tenant.TenantId = @tenant_id ',
            'AND approver_tenant.IsActive = 1)))), ',
    'EXISTS (SELECT 1 FROM ', @identity_schema_identifier, '.`idt_TenantProductEntitlements` entitlement ',
      'WHERE entitlement.TenantId = @tenant_id AND entitlement.ProductCode = ''SYNQ_LIENS'' ',
        'AND entitlement.Status = ''Active'') ',
    'INTO @org_is_valid, @migration_user_is_valid, @approver_is_valid, @entitlement_is_valid')
END;
PREPARE approval_identity_guards FROM @identity_guard_sql;
EXECUTE approval_identity_guards;
DEALLOCATE PREPARE approval_identity_guards;

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
WHERE @approved_by_user_id IS NOT NULL
  AND @approved_by_user_id REGEXP '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'
  AND @lien_amount_source IN ('billing', 'purchase')
  AND COALESCE(@approval_autocommit, 0) = 1
  AND COALESCE(@approval_lock_acquired, 0) = 1
  AND @active_approval_count = 0
  AND @identity_schema IS NOT NULL
  AND @org_is_valid = 1
  AND @migration_user_is_valid = 1
  AND @approver_is_valid = 1
  AND @entitlement_is_valid = 1
  AND EXISTS (
    SELECT 1
    FROM `SL-CORE`.`SL_MIGRATION_SOURCE_PROVENANCE` provenance
    WHERE provenance.PROVENANCE_KEY = 'sl-core-current'
      AND LOWER(provenance.SOURCE_FINGERPRINT) = @source_fingerprint
      AND provenance.IMPORT_SCOPE = 'sl-core-core-liens-v1');

SET @approval_rows_inserted = ROW_COUNT();
SELECT RELEASE_LOCK(CONCAT('LSLTI:approval:', @tenant_id, ':SL-CORE'))
INTO @approval_lock_released;

SELECT
  @approval_rows_inserted AS ApprovalRowsInserted,
  @active_approval_count AS ExistingActiveApprovals,
  CASE
    WHEN @approval_rows_inserted = 1 THEN 'Approval created. Run importer preflight with apply = 0.'
    WHEN @identity_schema_override IS NOT NULL AND @identity_schema_override_is_valid <> 1 THEN 'No row created: Identity schema override is not a valid Identity-schema candidate.'
    WHEN @identity_schema IS NULL THEN 'No row created: select one listed Identity schema candidate and set identity_schema_override.'
    WHEN COALESCE(@approval_autocommit, 0) <> 1 THEN 'No row created: enable autocommit and execute the script again.'
    WHEN COALESCE(@approval_lock_acquired, 0) <> 1 THEN 'No row created: another approval release is in progress; wait and retry.'
    WHEN @active_approval_count <> 0 THEN 'No row created: an active approval already exists; do not create another.'
    WHEN @approved_by_user_id IS NULL THEN 'No row created: set ApprovedByUserId to an authorized admin UUID.'
    WHEN @lien_amount_source NOT IN ('billing', 'purchase') THEN 'No row created: set lien amount source to billing or purchase.'
    WHEN @org_is_valid <> 1 THEN 'No row created: the organization is not active for this tenant.'
    WHEN @migration_user_is_valid <> 1 THEN 'No row created: the migration user is not active in this tenant.'
    WHEN @approver_is_valid <> 1 THEN 'No row created: the approver is not an authorized active administrator.'
    WHEN @entitlement_is_valid <> 1 THEN 'No row created: the tenant has no active SYNQ_LIENS entitlement.'
    ELSE 'No row created: the controlled SL-CORE staging receipt is missing or does not match the dump.'
  END AS Result;

SELECT Id, TenantId, OrgId, LegacyProgram, LienAmountSource,
       LegacyStatusOneTarget, LegacyStatusTwoTarget, MigrationUserId,
       ApprovedByUserId, Status, ApprovedAtUtc, ExpiresAtUtc
FROM liens_LegacyImportApprovals
WHERE TenantId = @tenant_id AND SourceSystem = 'SL-CORE'
ORDER BY ApprovedAtUtc DESC;
