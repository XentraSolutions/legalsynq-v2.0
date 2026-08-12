-- ============================================================================
-- SynqLien selling access provisioning
--
-- Run against the Identity database only, after Identity migration
-- 20260728000001_SeedSynqLienSellWorkflowPermission has been applied.
--
-- This script provisions exactly one tenant, primary seller organization, and
-- user. It does not create or modify the shared product/role/capability catalog;
-- that catalogue is deliberately owned by the reviewed Identity migration.
--
-- Before running, replace the four NULL values below with the target IDs. Do
-- not add credentials to this file. For example:
--   mysql --defaults-extra-file=/secure/identity.cnf legalsynq_identity \
--     < scripts/provision-synqlien-selling.sql
-- ============================================================================

SET @synqlien_selling_tenant_id       = '019ea7f6-21e9-7421-ab54-7846cdc6bc76'; -- e.g. '019ea7f6-21e9-7421-ab54-7846cdc6bc76'
SET @synqlien_selling_user_id         = '019ea7f6-284d-7310-9c92-349f2d97b154'; -- e.g. '019ea7f6-284d-7310-9c92-349f2d97b154'
SET @synqlien_selling_organization_id = '019ea7f6-283d-7891-a78b-3838cdecca0c'; -- e.g. '019ea7f6-283d-7891-a78b-3838cdecca0c'
SET @synqlien_selling_operator_id     = '019ea7f6-284d-7310-9c92-349f2d97b154'; -- e.g. '019ea7f6-284d-7310-9c92-349f2d97b154'

DROP PROCEDURE IF EXISTS `provision_synqlien_selling`;

DELIMITER //

CREATE PROCEDURE `provision_synqlien_selling`(
    IN p_tenant_id CHAR(36),
    IN p_user_id CHAR(36),
    IN p_organization_id CHAR(36),
    IN p_operator_id CHAR(36)
)
BEGIN
    DECLARE v_count INT DEFAULT 0;
    DECLARE v_changes INT DEFAULT 0;
    DECLARE v_last_changes INT DEFAULT 0;
    DECLARE v_product_id CHAR(36) DEFAULT NULL;
    DECLARE v_seller_role_id CHAR(36) DEFAULT NULL;
    DECLARE v_law_firm_type_id CHAR(36) DEFAULT NULL;
    DECLARE v_existing_org_id CHAR(36) DEFAULT NULL;
    DECLARE v_existing_status VARCHAR(20) DEFAULT NULL;
    DECLARE v_existing_source VARCHAR(20) DEFAULT NULL;
    DECLARE v_locked_user_id CHAR(36) DEFAULT NULL;
    DECLARE v_tenant_access_changed INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF p_tenant_id IS NULL OR p_user_id IS NULL OR p_organization_id IS NULL OR p_operator_id IS NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Set tenant, user, organization, and operator IDs before running this script.';
    END IF;

    START TRANSACTION;

    -- Lock the target user before assessing grants. This serializes concurrent
    -- provisioning attempts for the same user, including rows that do not yet
    -- exist in the access or role-assignment tables.
    SELECT u.`Id` INTO v_locked_user_id
    FROM `idt_Users` u
    WHERE u.`Id` = p_user_id
      AND u.`IsActive` = 1
    FOR UPDATE;

    IF v_locked_user_id IS NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Target user is missing or inactive.';
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM `idt_Tenants` t
    WHERE t.`Id` = p_tenant_id
      AND t.`IsActive` = 1;

    IF v_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Target tenant is missing or inactive.';
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM `idt_UserTenants` ut
    WHERE ut.`UserId` = p_user_id
      AND ut.`TenantId` = p_tenant_id
      AND ut.`IsActive` = 1;

    IF v_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Target user is not active in the target tenant.';
    END IF;

    SELECT COUNT(DISTINCT u.`Id`) INTO v_count
    FROM `idt_Users` u
    INNER JOIN `idt_ScopedRoleAssignments` sra
        ON sra.`UserId` = u.`Id`
       AND sra.`IsActive` = 1
       AND sra.`ScopeType` = 'GLOBAL'
    INNER JOIN `idt_Roles` r ON r.`Id` = sra.`RoleId`
    WHERE u.`Id` = p_operator_id
      AND u.`IsActive` = 1
      AND (
          r.`Name` = 'PlatformAdmin'
          OR (r.`Name` = 'TenantAdmin' AND sra.`TenantId` = p_tenant_id)
      );

    IF v_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Operator must be an active PlatformAdmin or tenant-scoped TenantAdmin.';
    END IF;

    -- Assert the centrally migrated authorization catalogue. Direct tenant
    -- provisioning must never invent, reactivate, or retarget global policy.
    SELECT p.`Id` INTO v_product_id
    FROM `idt_Products` p
    WHERE p.`Code` = 'SYNQ_LIENS'
      AND p.`IsActive` = 1;

    IF v_product_id IS NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Active SYNQ_LIENS product is missing; apply the Identity migration first.';
    END IF;

    SELECT pr.`Id` INTO v_seller_role_id
    FROM `idt_ProductRoles` pr
    WHERE pr.`ProductId` = v_product_id
      AND pr.`Code` = 'SYNQLIEN_SELLER'
      AND pr.`IsActive` = 1;

    IF v_seller_role_id IS NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Active SYNQLIEN_SELLER role is missing; apply the Identity migration first.';
    END IF;

    SELECT ot.`Id` INTO v_law_firm_type_id
    FROM `idt_OrganizationTypes` ot
    WHERE ot.`Code` = 'LAW_FIRM'
      AND ot.`IsActive` = 1;

    IF v_law_firm_type_id IS NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Active LAW_FIRM organization type is missing.';
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM `idt_ProductOrganizationTypeRules` r
    WHERE r.`ProductId` = v_product_id
      AND r.`ProductRoleId` = v_seller_role_id
      AND r.`OrganizationTypeId` = v_law_firm_type_id
      AND r.`IsActive` = 1;

    IF v_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'SYNQLIEN_SELLER does not have an active LAW_FIRM eligibility rule.';
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM `idt_Capabilities` c
    INNER JOIN `idt_RoleCapabilities` rc
        ON rc.`CapabilityId` = c.`Id`
       AND rc.`ProductRoleId` = v_seller_role_id
    WHERE c.`ProductId` = v_product_id
      AND c.`IsActive` = 1
      AND c.`Code` IN (
        'SYNQ_LIENS.lien:create',
        'SYNQ_LIENS.lien:offer',
        'SYNQ_LIENS.lien:read:own',
        'SYNQ_LIENS.lien_sale:read',
        'SYNQ_LIENS.lien_sale:create',
        'SYNQ_LIENS.lien_sale:update',
        'SYNQ_LIENS.lien_sale:publish',
        'SYNQ_LIENS.lien_sale:withdraw',
        'SYNQ_LIENS.lien_sale:view_analytics',
        'SYNQ_LIENS.lien:sell'
      );

    IF v_count <> 10 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Required SynqLien seller capabilities are incomplete; apply the Identity migration first.';
    END IF;

    -- A seller must be a primary member of the exact tenant-owned LAW_FIRM
    -- organization. Provider mode is never changed here; manage mode cannot
    -- use the Liens selling endpoints and is rejected explicitly.
    SELECT COUNT(*) INTO v_count
    FROM `idt_Organizations` o
    INNER JOIN `idt_UserOrganizationMemberships` uom
        ON uom.`OrganizationId` = o.`Id`
       AND uom.`UserId` = p_user_id
       AND uom.`IsActive` = 1
       AND uom.`IsPrimary` = 1
    WHERE o.`Id` = p_organization_id
      AND o.`TenantId` = p_tenant_id
      AND o.`OrganizationTypeId` = v_law_firm_type_id
      AND o.`IsActive` = 1
      AND LOWER(COALESCE(o.`ProviderMode`, 'sell')) <> 'manage';

    IF v_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Target organization must be the active primary LAW_FIRM seller and not be in manage mode.';
    END IF;

    -- Tenant-scoped JWT context resolves the earliest active tenant-owned or
    -- global-org membership. Require this seller to have exactly one active
    -- membership across that full candidate set, so the JWT org_id is guaranteed
    -- to be the target organization.
    SELECT COUNT(*) INTO v_count
    FROM `idt_UserOrganizationMemberships` uom
    INNER JOIN `idt_Organizations` o ON o.`Id` = uom.`OrganizationId`
    WHERE uom.`UserId` = p_user_id
      AND uom.`IsActive` = 1
      AND (o.`TenantId` = p_tenant_id OR o.`TenantId` IS NULL);

    IF v_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Target user must have exactly one active tenant or global organization membership.';
    END IF;

    -- Do not overwrite another organization’s direct product grant. The product
    -- access schema permits only one row per tenant/user/product, so a conflict
    -- must be reconciled through the Identity API before retrying this script.
    SET v_existing_org_id = NULL;
    SET v_existing_status = NULL;
    SET v_existing_source = NULL;
    SELECT upa.`OrganizationId`, upa.`AccessStatus`, upa.`SourceType`
      INTO v_existing_org_id, v_existing_status, v_existing_source
    FROM `idt_UserProductAccess` upa
    WHERE upa.`TenantId` = p_tenant_id
      AND upa.`UserId` = p_user_id
      AND upa.`ProductCode` = 'SYNQ_LIENS'
    FOR UPDATE;

    IF v_existing_status IS NOT NULL AND NOT (
        v_existing_org_id <=> p_organization_id
        AND v_existing_source = 'Direct'
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Conflicting SynqLien user-product grant must be reconciled through the Identity API.';
    END IF;

    -- Lock the complete seller-role index range. UserRoleAssignments has no
    -- database uniqueness constraint for this scope, so this locking read is
    -- required to serialize a concurrent seller-role insertion for this user.
    SELECT ura.`Id`
    FROM `idt_UserRoleAssignments` ura
    WHERE ura.`TenantId` = p_tenant_id
      AND ura.`UserId` = p_user_id
      AND ura.`RoleCode` = 'SYNQLIEN_SELLER'
    FOR UPDATE;

    SELECT COUNT(*) INTO v_count
    FROM `idt_UserRoleAssignments` ura
    WHERE ura.`TenantId` = p_tenant_id
      AND ura.`UserId` = p_user_id
      AND ura.`RoleCode` = 'SYNQLIEN_SELLER'
      AND ura.`AssignmentStatus` = 'Active'
      AND NOT (
        ura.`ProductCode` = 'SYNQ_LIENS'
        AND ura.`OrganizationId` <=> p_organization_id
        AND ura.`SourceType` = 'Direct'
      );

    IF v_count <> 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Conflicting active SYNQLIEN_SELLER assignment must be reconciled through the Identity API.';
    END IF;

    -- Runtime entitlement gate. EffectiveAccessService reads idt_TenantProducts,
    -- not idt_TenantProductEntitlements.
    UPDATE `idt_TenantProducts`
    SET `IsEnabled` = 1
    WHERE `TenantId` = p_tenant_id
      AND `ProductId` = v_product_id
      AND `IsEnabled` = 0;
    SET v_last_changes = ROW_COUNT();
    SET v_changes = v_changes + v_last_changes;
    SET v_tenant_access_changed = v_tenant_access_changed + v_last_changes;

    INSERT INTO `idt_TenantProducts` (`TenantId`, `ProductId`, `IsEnabled`)
    SELECT p_tenant_id, v_product_id, 1
    WHERE NOT EXISTS (
        SELECT 1
        FROM `idt_TenantProducts` tp
        WHERE tp.`TenantId` = p_tenant_id
          AND tp.`ProductId` = v_product_id
    );
    SET v_last_changes = ROW_COUNT();
    SET v_changes = v_changes + v_last_changes;
    SET v_tenant_access_changed = v_tenant_access_changed + v_last_changes;

    -- Keep the newer access-source entitlement record consistent with the
    -- runtime idt_TenantProducts gate so management/reporting paths agree.
    UPDATE `idt_TenantProductEntitlements`
    SET `Status` = 'Active',
        `EnabledAtUtc` = UTC_TIMESTAMP(6),
        `DisabledAtUtc` = NULL,
        `UpdatedAtUtc` = UTC_TIMESTAMP(6),
        `UpdatedByUserId` = p_operator_id
    WHERE `TenantId` = p_tenant_id
      AND `ProductCode` = 'SYNQ_LIENS'
      AND `Status` <> 'Active';
    SET v_last_changes = ROW_COUNT();
    SET v_changes = v_changes + v_last_changes;
    SET v_tenant_access_changed = v_tenant_access_changed + v_last_changes;

    INSERT INTO `idt_TenantProductEntitlements`
        (`Id`, `TenantId`, `ProductCode`, `Status`, `EnabledAtUtc`, `DisabledAtUtc`,
         `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`)
    SELECT UUID(), p_tenant_id, 'SYNQ_LIENS', 'Active', UTC_TIMESTAMP(6), NULL,
           UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), p_operator_id, p_operator_id
    WHERE NOT EXISTS (
        SELECT 1
        FROM `idt_TenantProductEntitlements` tpe
        WHERE tpe.`TenantId` = p_tenant_id
          AND tpe.`ProductCode` = 'SYNQ_LIENS'
    );
    SET v_last_changes = ROW_COUNT();
    SET v_changes = v_changes + v_last_changes;
    SET v_tenant_access_changed = v_tenant_access_changed + v_last_changes;

    UPDATE `idt_OrganizationProducts`
    SET `IsEnabled` = 1,
        `EnabledAtUtc` = COALESCE(`EnabledAtUtc`, UTC_TIMESTAMP(6))
    WHERE `OrganizationId` = p_organization_id
      AND `ProductId` = v_product_id
      AND `IsEnabled` = 0;
    SET v_changes = v_changes + ROW_COUNT();

    INSERT INTO `idt_OrganizationProducts`
        (`OrganizationId`, `ProductId`, `IsEnabled`, `EnabledAtUtc`, `GrantedByUserId`)
    SELECT p_organization_id, v_product_id, 1, UTC_TIMESTAMP(6), p_operator_id
    WHERE NOT EXISTS (
        SELECT 1
        FROM `idt_OrganizationProducts` op
        WHERE op.`OrganizationId` = p_organization_id
          AND op.`ProductId` = v_product_id
    );
    SET v_changes = v_changes + ROW_COUNT();

    -- Reactivate only the exact target direct product grant. A conflicting scope
    -- was rejected above rather than being silently retargeted.
    UPDATE `idt_UserProductAccess`
    SET `AccessStatus` = 'Granted',
        `GrantedAtUtc` = UTC_TIMESTAMP(6),
        `RevokedAtUtc` = NULL,
        `UpdatedAtUtc` = UTC_TIMESTAMP(6),
        `UpdatedByUserId` = p_operator_id
    WHERE `TenantId` = p_tenant_id
      AND `UserId` = p_user_id
      AND `ProductCode` = 'SYNQ_LIENS'
      AND `OrganizationId` <=> p_organization_id
      AND `SourceType` = 'Direct'
      AND (`AccessStatus` <> 'Granted' OR `RevokedAtUtc` IS NOT NULL);
    SET v_changes = v_changes + ROW_COUNT();

    INSERT INTO `idt_UserProductAccess`
        (`Id`, `TenantId`, `UserId`, `ProductCode`, `AccessStatus`, `OrganizationId`,
         `SourceType`, `GrantedAtUtc`, `RevokedAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`,
         `CreatedByUserId`, `UpdatedByUserId`)
    SELECT UUID(), p_tenant_id, p_user_id, 'SYNQ_LIENS', 'Granted', p_organization_id,
           'Direct', UTC_TIMESTAMP(6), NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6),
           p_operator_id, p_operator_id
    WHERE NOT EXISTS (
        SELECT 1
        FROM `idt_UserProductAccess` upa
        WHERE upa.`TenantId` = p_tenant_id
          AND upa.`UserId` = p_user_id
          AND upa.`ProductCode` = 'SYNQ_LIENS'
    );
    SET v_changes = v_changes + ROW_COUNT();

    -- Reject duplicate target role records rather than compounding them. A single
    -- target-scope record may be restored from Removed to Active.
    SELECT COUNT(*) INTO v_count
    FROM `idt_UserRoleAssignments` ura
    WHERE ura.`TenantId` = p_tenant_id
      AND ura.`UserId` = p_user_id
      AND ura.`ProductCode` = 'SYNQ_LIENS'
      AND ura.`RoleCode` = 'SYNQLIEN_SELLER'
      AND ura.`OrganizationId` <=> p_organization_id
      AND ura.`SourceType` = 'Direct';

    IF v_count > 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Duplicate target SYNQLIEN_SELLER assignments must be reconciled through the Identity API.';
    END IF;

    UPDATE `idt_UserRoleAssignments`
    SET `AssignmentStatus` = 'Active',
        `AssignedAtUtc` = UTC_TIMESTAMP(6),
        `RemovedAtUtc` = NULL,
        `UpdatedAtUtc` = UTC_TIMESTAMP(6),
        `UpdatedByUserId` = p_operator_id
    WHERE `TenantId` = p_tenant_id
      AND `UserId` = p_user_id
      AND `ProductCode` = 'SYNQ_LIENS'
      AND `RoleCode` = 'SYNQLIEN_SELLER'
      AND `OrganizationId` <=> p_organization_id
      AND `SourceType` = 'Direct'
      AND (`AssignmentStatus` <> 'Active' OR `RemovedAtUtc` IS NOT NULL);
    SET v_changes = v_changes + ROW_COUNT();

    INSERT INTO `idt_UserRoleAssignments`
        (`Id`, `TenantId`, `UserId`, `ProductCode`, `RoleCode`, `AssignmentStatus`,
         `OrganizationId`, `SourceType`, `AssignedAtUtc`, `RemovedAtUtc`, `CreatedAtUtc`,
         `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`)
    SELECT UUID(), p_tenant_id, p_user_id, 'SYNQ_LIENS', 'SYNQLIEN_SELLER', 'Active',
           p_organization_id, 'Direct', UTC_TIMESTAMP(6), NULL, UTC_TIMESTAMP(6),
           UTC_TIMESTAMP(6), p_operator_id, p_operator_id
    WHERE NOT EXISTS (
        SELECT 1
        FROM `idt_UserRoleAssignments` ura
        WHERE ura.`TenantId` = p_tenant_id
          AND ura.`UserId` = p_user_id
          AND ura.`ProductCode` = 'SYNQ_LIENS'
          AND ura.`RoleCode` = 'SYNQLIEN_SELLER'
          AND ura.`OrganizationId` <=> p_organization_id
          AND ura.`SourceType` = 'Direct'
    );
    SET v_changes = v_changes + ROW_COUNT();

    -- Existing JWTs embed access_version. Advance it only when a grant changed,
    -- then leave token refresh/login to Identity's normal validation flow.
    IF v_tenant_access_changed > 0 THEN
        UPDATE `idt_Users` u
        INNER JOIN `idt_UserTenants` ut
            ON ut.`UserId` = u.`Id`
           AND ut.`TenantId` = p_tenant_id
           AND ut.`IsActive` = 1
        SET u.`AccessVersion` = u.`AccessVersion` + 1,
            u.`UpdatedAtUtc` = UTC_TIMESTAMP(6)
        WHERE u.`IsActive` = 1;
    ELSEIF v_changes > 0 THEN
        UPDATE `idt_Users`
        SET `AccessVersion` = `AccessVersion` + 1,
            `UpdatedAtUtc` = UTC_TIMESTAMP(6)
        WHERE `Id` = p_user_id;
    END IF;

    -- Local identity audit trail for every successful invocation. CURRENT_USER()
    -- is the authoritative database actor; the verified application operator is
    -- preserved as asserted metadata. The external audit-service event cannot be
    -- emitted atomically from a standalone SQL file.
    INSERT INTO `idt_AuditLogs`
        (`Id`, `ActorName`, `ActorType`, `Action`, `EntityType`, `EntityId`, `MetadataJson`, `CreatedAtUtc`)
    VALUES
        (UUID(), CONCAT('db:', CURRENT_USER()), 'Database', 'synqlien.selling.provisioned',
         'User', p_user_id,
         JSON_OBJECT(
             'tenantId', p_tenant_id,
             'organizationId', p_organization_id,
             'assertedOperatorId', p_operator_id,
             'databaseActor', CURRENT_USER(),
             'productCode', 'SYNQ_LIENS',
             'roleCode', 'SYNQLIEN_SELLER',
             'changedRows', v_changes
         ), UTC_TIMESTAMP(6));

    COMMIT;

    SELECT
        'SynqLien selling access provisioned.' AS `Result`,
        v_changes AS `ChangedRows`,
        p_tenant_id AS `TenantId`,
        p_user_id AS `UserId`,
        p_organization_id AS `OrganizationId`;
END//

DELIMITER ;

CALL `provision_synqlien_selling`(
    @synqlien_selling_tenant_id,
    @synqlien_selling_user_id,
    @synqlien_selling_organization_id,
    @synqlien_selling_operator_id
);

DROP PROCEDURE IF EXISTS `provision_synqlien_selling`;
