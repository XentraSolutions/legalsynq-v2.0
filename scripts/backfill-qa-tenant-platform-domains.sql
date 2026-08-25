-- Backfill existing QA tenant platform domains before enabling .nonprod host
-- labelling for newly-created tenants.
--
-- Intended use:
--   1. Run the PREVIEW query and review the rows.
--   2. Run the APPLY statement in the tenant database only after review.
--
-- Default legacy QA host shape:
--   {tenant.Subdomain}.legalsynq.net
--
-- The APPLY statement is idempotent:
--   - skips tenants that already have an active primary Subdomain domain
--   - skips tenants where the computed host already exists

-- PREVIEW
SELECT
    t.Id AS TenantId,
    t.Code,
    t.Subdomain,
    CONCAT(LOWER(TRIM(t.Subdomain)), '.legalsynq.net') AS BackfillHost
FROM tenant_Tenants t
WHERE t.Subdomain IS NOT NULL
  AND TRIM(t.Subdomain) <> ''
  AND NOT EXISTS (
      SELECT 1
      FROM tenant_Domains d
      WHERE d.TenantId = t.Id
        AND d.DomainType = 'Subdomain'
        AND d.Status = 'Active'
        AND d.IsPrimary = 1
  )
  AND NOT EXISTS (
      SELECT 1
      FROM tenant_Domains d
      WHERE d.Host = CONCAT(LOWER(TRIM(t.Subdomain)), '.legalsynq.net')
        AND d.Status = 'Active'
  )
ORDER BY t.Code;

-- APPLY
-- INSERT INTO tenant_Domains
--     (Id, TenantId, Host, DomainType, Status, IsPrimary, CreatedAtUtc, UpdatedAtUtc)
-- SELECT
--     UUID(),
--     t.Id,
--     CONCAT(LOWER(TRIM(t.Subdomain)), '.legalsynq.net'),
--     'Subdomain',
--     'Active',
--     1,
--     UTC_TIMESTAMP(6),
--     UTC_TIMESTAMP(6)
-- FROM tenant_Tenants t
-- WHERE t.Subdomain IS NOT NULL
--   AND TRIM(t.Subdomain) <> ''
--   AND NOT EXISTS (
--       SELECT 1
--       FROM tenant_Domains d
--       WHERE d.TenantId = t.Id
--         AND d.DomainType = 'Subdomain'
--         AND d.Status = 'Active'
--         AND d.IsPrimary = 1
--   )
--   AND NOT EXISTS (
--       SELECT 1
--       FROM tenant_Domains d
--       WHERE d.Host = CONCAT(LOWER(TRIM(t.Subdomain)), '.legalsynq.net')
--         AND d.Status = 'Active'
--   );
