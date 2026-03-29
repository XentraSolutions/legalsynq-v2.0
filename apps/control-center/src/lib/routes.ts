/**
 * Control Center route constants and builders.
 *
 * All internal links MUST use these helpers — never hardcode paths directly
 * in components or pages. This keeps path changes isolated to one file.
 *
 * All routes are host-root paths (no path prefix — this is a standalone app).
 */
export const Routes = {
  // ── Top-level sections ────────────────────────────────────────────────────

  /** /tenants — Tenants list */
  tenants: '/tenants',

  /** /tenant-users — Users across all tenants */
  tenantUsers: '/tenant-users',

  /** /roles — Roles & permissions */
  roles: '/roles',

  /** /products — Product entitlements */
  products: '/products',

  /** /support — Support tools */
  support: '/support',

  /** /audit-logs — Audit logs */
  auditLogs: '/audit-logs',

  /** /monitoring — Service health */
  monitoring: '/monitoring',

  /** /settings — Platform settings */
  settings: '/settings',

  // ── Dynamic route builders ────────────────────────────────────────────────

  /** /tenants/:id — Tenant detail */
  tenantDetail: (id: string) => `/tenants/${id}`,

  /** /tenants/:id/users — Users for a specific tenant */
  tenantUsers_: (tenantId: string) => `/tenants/${tenantId}/users`,
} as const;
