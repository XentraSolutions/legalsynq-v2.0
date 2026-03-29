# Step 13 — Tenant Isolation (Application-Layer, MySQL-Safe)

## 1. Objective

Enforce strict multi-tenant data isolation in the Docs Service **without relying on
database-level Row-Level Security (RLS)**.

PostgreSQL RLS (and MySQL's total absence of it) cannot be the last line of
defence: a misconfigured policy, a bypassed session variable, or a direct DB
connection all defeat it.  Every boundary must be enforced in application code
so the guarantee holds on any SQL database engine (PostgreSQL, MySQL, Aurora,
etc.) with any connection tool.

---

## 2. Isolation Strategy — Three Layers

```
Request ──► Route handler
                │
          Layer 1 (Route)
          assertTenantScope()
          • Pre-flight: principal.tenantId vs body.tenantId
          • Blocks obviously wrong-scoped requests early
                │
          Layer 2 (Service)
          assertDocumentTenantScope()
          • Post-load ABAC: principal.tenantId vs doc.tenantId
          • Catches code paths that bypass the route check
          • Audits admin cross-tenant access
                │
          Layer 3 (DB)
          requireTenantId() + WHERE tenant_id = ?
          • Pre-query guard: throws if tenantId is empty/null
          • SQL predicate: data never returned cross-tenant
                │
           Database
```

Each layer is **independent** — defeating one still leaves the other two active.

---

## 3. How Tenant Filtering Is Enforced

### Layer 1 — Route pre-flight (`rbac.ts: assertTenantScope`)

Called on routes where the request body includes a `tenantId` (e.g. `POST /documents`).

```typescript
assertTenantScope(principal, body.tenantId);
// Throws ForbiddenError if principal.tenantId !== body.tenantId (non-admin)
// PlatformAdmin: passes through; audit deferred to Layer 2
```

Callers are routes only.  tenantId always comes from the **verified JWT**, never
from a client-supplied header or query string.

---

### Layer 2 — Service ABAC (`tenant-guard.ts: assertDocumentTenantScope`)

Called in every `DocumentService` method that loads a document by ID, **after**
the repository returns:

```typescript
const doc = await DocumentRepository.findById(id, effectiveTenantId);
if (!doc) throw new NotFoundError('Document', id);

// Layer 2 — defence-in-depth
await assertDocumentTenantScope(ctx.principal, doc, ctx);
```

Decision table:

| `principal.tenantId` vs `doc.tenantId` | Role | Result |
|---|---|---|
| Match | Any | Allowed — no log |
| Mismatch | Non-admin | `TenantIsolationError` (403) + `TENANT_ISOLATION_VIOLATION` audit log |
| Mismatch | `PlatformAdmin` | Allowed + `ADMIN_CROSS_TENANT_ACCESS` audit log |

Error message is always generic — never reveals which tenant owns the resource.

---

### Layer 3 — DB guard (`tenant-query.ts: requireTenantId` + SQL)

**`requireTenantId(tenantId, context)`** is called at the top of every
`DocumentRepository` method before any SQL is constructed:

```typescript
async findById(id: string, tenantId: string): Promise<Document | null> {
  requireTenantId(tenantId, 'DocumentRepository.findById');  // ← guard
  const row = await queryOne(
    `SELECT * FROM documents WHERE id = $1 AND tenant_id = $2 AND is_deleted = FALSE`,
    [id, tenantId],  // ← SQL predicate
  );
  return row ? rowToDocument(row) : null;
}
```

If `tenantId` is `null`, `undefined`, or an empty string, `requireTenantId`
throws `TenantIsolationError` **before the SQL is executed** — no query runs.

---

## 4. Safeguards Against Developer Mistakes

### 4a. `requireTenantId()` — mandatory pre-query guard

| What a developer forgets | What happens |
|---|---|
| Passes `undefined` as tenantId | `TenantIsolationError` thrown before SQL runs |
| Passes `""` | `TenantIsolationError` |
| Passes `null` | `TenantIsolationError` |
| Passes a whitespace string | `TenantIsolationError` |

Error message includes the calling context (e.g. `DocumentRepository.findById`)
and is logged at `ERROR` level — immediately visible in monitoring.

### 4b. `tenantQuery()` / `tenantQueryOne()` — tenant-validated query helpers

```typescript
// Instead of:
await query('SELECT * FROM documents WHERE id = $1', [id]);  // dangerous — no tenant filter

// Use:
await tenantQuery('SELECT * FROM documents WHERE id = $1 AND tenant_id = $2', tenantId, [id, tenantId]);
```

`tenantQuery` calls `requireTenantId` before delegating to the raw `query()`.
A future developer adding a new repository method cannot accidentally omit the
check — they would have to explicitly call raw `query()` to bypass it.

### 4c. `resolveEffectiveTenantId()` — non-admin header ignored

```typescript
// Routes extract X-Admin-Target-Tenant header for all callers:
targetTenantId: req.headers['x-admin-target-tenant']

// Service resolves effective tenantId — non-admin cannot override:
const effectiveTenantId = resolveEffectiveTenantId(ctx.principal, ctx.targetTenantId);
// Non-admin: always returns principal.tenantId regardless of targetTenantId
// PlatformAdmin: returns targetTenantId if supplied
```

### 4d. SQL WHERE clause structure — tenant_id always first

By convention enforced in the repository, `tenant_id = $1` (or `$2` after `id`)
appears **before** all optional filter conditions.  This makes it impossible to
add a new filter that accidentally becomes the sole WHERE predicate.

```sql
-- Correct pattern (tenant_id anchored)
WHERE d.tenant_id = $1 AND d.is_deleted = FALSE AND d.product_id = $2

-- Never this (tenant missing):
WHERE d.product_id = $2
```

### 4e. Bug fix — `createVersion` UPDATE missing tenant_id

**Before (vulnerable):**
```sql
UPDATE documents SET current_version_id = $1, ... WHERE id = $9
```
A rogue `documentId` (same UUID from different tenant) could update the wrong
tenant's document row.

**After (fixed):**
```sql
UPDATE documents SET current_version_id = $1, ... WHERE id = $9 AND tenant_id = $10
```

---

## 5. Files Changed

| File | Type | Change |
|------|------|--------|
| `src/infrastructure/database/tenant-query.ts` | **NEW** | `requireTenantId()`, `tenantQuery()`, `tenantQueryOne()` defensive helpers |
| `src/application/tenant-guard.ts` | **NEW** | `assertDocumentTenantScope()` (ABAC + admin audit), `resolveEffectiveTenantId()` |
| `src/shared/errors.ts` | Updated | Added `TenantIsolationError` (HTTP 403, `TENANT_ISOLATION_VIOLATION`) |
| `src/shared/constants.ts` | Updated | Added `ADMIN_CROSS_TENANT_ACCESS` and `TENANT_ISOLATION_VIOLATION` audit events |
| `src/infrastructure/database/document-repository.ts` | Updated | `requireTenantId()` in every method; fixed `createVersion` UPDATE bug |
| `src/application/document-service.ts` | Updated | `resolveEffectiveTenantId()` + `assertDocumentTenantScope()` in every resource-loading method; `targetTenantId` on `RequestContext` |
| `src/application/rbac.ts` | Updated | `assertTenantScope` now logs debug when PlatformAdmin crosses tenants; cleaner comment explaining the three-layer model |
| `src/api/routes/documents.ts` | Updated | `ctx()` extracts `X-Admin-Target-Tenant` header for PlatformAdmin cross-tenant access |
| `tests/unit/tenant-isolation.test.ts` | **NEW** | 38 unit tests |

---

## 6. Admin Cross-Tenant Access Design

PlatformAdmin is the **only** role permitted to access documents across tenant
boundaries.  The path is explicit, controlled, and always audited.

### How it works

1. Admin sends `X-Admin-Target-Tenant: <targetTenantId>` header
2. `ctx()` in the route extracts the header value into `targetTenantId`
3. `resolveEffectiveTenantId()` returns `targetTenantId` (only for PlatformAdmin; others get their own tenantId)
4. `DocumentRepository.findById(id, targetTenantId)` is called with the admin's chosen tenantId
5. `assertDocumentTenantScope()` sees `principal.tenantId ≠ doc.tenantId`, detects PlatformAdmin, and emits `ADMIN_CROSS_TENANT_ACCESS` audit event

### Guarantees

- **Not the default path**: standard routes always use `principal.tenantId`
- **Explicit**: admin must set the header; it is never inferred
- **Auditable**: every cross-tenant document access is logged with actor tenantId, resource tenantId, document ID, IP, and correlationId
- **Role-checked**: non-admin tenantId override is silently rejected at `resolveEffectiveTenantId()`

---

## 7. Risks and Gaps

| Risk | Severity | Mitigation |
|------|----------|------------|
| Developer calls raw `query()` without tenant filter | Medium | `requireTenantId()` guard in every repo method; code review convention; `tenantQuery()` available as safe alternative |
| JWT tampering to spoof tenantId | High | tenantId always extracted from **verified** JWT in `auth-provider.ts` — never from request body for scope resolution |
| `PLATFORM_ADMIN` token compromise | Critical | Admin cross-tenant is always audited; SIEM alert on `ADMIN_CROSS_TENANT_ACCESS` events; short JWT TTL recommended |
| Audit log unavailable (DB down) | Medium | `assertDocumentTenantScope` awaits the audit log — if it fails, the request fails; no silent bypass |
| New repository method without `requireTenantId` | Low | Engineering convention + code review; automated linting rule (future: eslint plugin) |
| `withTransaction` queries missing tenant filter | Low — currently correct | `createVersion` bug fixed; all `client.query()` calls inside the transaction now include `tenant_id` |

---

## 8. Verification Steps

### 1. Unit tests

```bash
cd apps/services/docs

# Tenant isolation suite only
npm test -- --testPathPattern=tenant-isolation --forceExit

# Full suite (should show 161 tests, 7 suites)
npm test -- --forceExit
```

### 2. Tenant A cannot read Tenant B's document (manual)

```bash
# JWT for TENANT_A
TENANT_A_JWT="Bearer eyJ..."

# Document owned by TENANT_B
DOC_B_ID="<uuid>"

curl -s http://localhost:5005/api/v1/documents/$DOC_B_ID \
  -H "Authorization: $TENANT_A_JWT" | jq '.error'
# Expected: { "error": "NOT_FOUND", ... }
# (404 not 403 — never disclose that the document exists in another tenant)
```

### 3. Tenant A cannot delete Tenant B's document (manual)

```bash
curl -s -X DELETE http://localhost:5005/api/v1/documents/$DOC_B_ID \
  -H "Authorization: $TENANT_A_JWT" | jq '.error'
# Expected: 404 NOT_FOUND
```

### 4. Missing tenantId causes a guard error (not a DB query)

```bash
# In a test or debug context — confirm requireTenantId throws before query
node -e "
  const { requireTenantId } = require('./dist/infrastructure/database/tenant-query');
  try { requireTenantId('', 'test'); }
  catch(e) { console.log(e.code, e.statusCode); }
"
# Expected: TENANT_ISOLATION_VIOLATION 403
```

### 5. PlatformAdmin cross-tenant access is audited

```bash
# JWT for PLATFORM_ADMIN
ADMIN_JWT="Bearer eyJ..."
TARGET_TENANT="<tenant-b-uuid>"
DOC_B_ID="<doc-in-tenant-b>"

curl -s http://localhost:5005/api/v1/documents/$DOC_B_ID \
  -H "Authorization: $ADMIN_JWT" \
  -H "X-Admin-Target-Tenant: $TARGET_TENANT" | jq '.data.id'
# Expected: document returned

# Check audit trail
psql $DOCS_DB -c "
  SELECT event, actor_id, detail->>'resourceTenantId' as resource_tenant
  FROM document_audits
  WHERE event = 'ADMIN_CROSS_TENANT_ACCESS'
  ORDER BY occurred_at DESC LIMIT 5;
"
# Expected: row with ADMIN_CROSS_TENANT_ACCESS event
```

### 6. Cross-tenant violation is logged

```bash
psql $DOCS_DB -c "
  SELECT event, actor_id, outcome, occurred_at
  FROM document_audits
  WHERE event = 'TENANT_ISOLATION_VIOLATION'
  ORDER BY occurred_at DESC LIMIT 10;
"
```

### 7. Composite indexes exist (recommended DB-side addition)

```sql
-- Verify tenant_id is always indexed alongside id
EXPLAIN SELECT * FROM documents WHERE id = '<uuid>' AND tenant_id = '<uuid>';
-- Should show Index Scan, not Seq Scan

-- Recommended composite indexes (add via migration if not present):
CREATE INDEX IF NOT EXISTS idx_documents_tenant_status
  ON documents(tenant_id, status)
  WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS idx_document_versions_tenant_document
  ON document_versions(tenant_id, document_id)
  WHERE is_deleted = FALSE;
```

---

## Summary

| Layer | Mechanism | What it prevents |
|-------|-----------|------------------|
| DB pre-guard | `requireTenantId()` | Queries without tenantId fail before execution |
| SQL predicate | `WHERE tenant_id = ?` | Cross-tenant rows never returned from DB |
| Service ABAC | `assertDocumentTenantScope()` | Code paths bypassing the repo filter |
| Route pre-flight | `assertTenantScope()` | Mis-scoped requests blocked at entry |
| Admin audit trail | `ADMIN_CROSS_TENANT_ACCESS` event | Admin cross-tenant fully observable |
| Error type | `TenantIsolationError` (403) | Violations monitored separately from auth errors |
| Error message | Generic (no resource detail) | tenantId enumeration prevented |
