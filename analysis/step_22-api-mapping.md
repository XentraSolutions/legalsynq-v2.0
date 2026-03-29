# Step 22 – API Mapping Layer

## Status: Complete — 0 TypeScript errors

---

## Files Created

| File | Purpose |
|------|---------|
| `src/lib/api-mappers.ts` | 7 domain mappers + 2 sub-mappers + 1 paged-response mapper; accepts `unknown` input, handles snake_case/camelCase, defensive guards throughout |

---

## Files Updated

| File | What changed |
|------|-------------|
| `src/lib/control-center-api.ts` | Every `apiClient.get/post/patch` call now fetches `unknown` and pipes through the appropriate mapper before returning |

---

## Mapping Strategy

### Input type

Every public mapper accepts `unknown`:
```ts
export function mapTenantSummary(raw: unknown): TenantSummary
```

This forces all callers to treat API responses as untrusted data. The mapper
itself casts via `asObj()` before reading any field — no cast in calling code.

### Field resolution order

For every field, the mapper tries the snake_case name first, then camelCase:

```ts
str(r, 'display_name', 'displayName', '')
//       ^^^^ priority   ^^ fallback
```

This means the same mapper works whether the backend serialises with
`System.Text.Json` (camelCase by default) or a snake_case serialiser.

### Internal helpers

| Helper | Behaviour |
|--------|---------|
| `asObj(v)` | Casts to `Record<string,unknown>`, returns `{}` on null/array/primitive |
| `asArr(v)` | Returns the value if it's an array, otherwise `[]` |
| `str(r, snake, camel, fallback, warnLabel?)` | String field with optional dev warning |
| `optStr(r, snake, camel)` | Optional string; returns `undefined` when absent |
| `num(r, snake, camel, fallback, warnLabel?)` | Number field; checks `isFinite` |
| `bool(r, snake, camel, fallback)` | Boolean; coerces 0/1/"true"/"false" |
| `oneOf(r, snake, camel, allowed, fallback, warnLabel?)` | Enum-validated string |

### `mapPagedResponse<T>(raw, mapItem)`

Generic mapper for paged list responses:
```ts
mapPagedResponse(raw, mapTenantSummary)
// → { items: TenantSummary[], totalCount, page, pageSize }
```

Normalises `total_count`/`totalCount` and `page_size`/`pageSize`.

---

## Fields Normalised

### TenantSummary / TenantDetail

| Backend field | Frontend field | Note |
|---------------|---------------|------|
| `display_name` | `displayName` | |
| `primary_contact_name` | `primaryContactName` | |
| `is_active` | `isActive` | `bool()` — coerces 0/1 |
| `user_count` | `userCount` | |
| `org_count` | `orgCount` | |
| `created_at` | `createdAtUtc` | |
| `updated_at` | `updatedAtUtc` | *(detail only)* |
| `active_user_count` | `activeUserCount` | *(detail only)* |
| `linked_org_count` | `linkedOrgCount` | *(detail only, optional)* |
| `product_entitlements[].product_code` | `productCode` | `oneOf()` validates against ProductCode union |
| `product_entitlements[].product_name` | `productName` | |
| `product_entitlements[].enabled_at` | `enabledAtUtc` | optional |

### UserSummary / UserDetail

| Backend field | Frontend field | Note |
|---------------|---------------|------|
| `first_name` | `firstName` | |
| `last_name` | `lastName` | |
| `tenant_id` | `tenantId` | |
| `tenant_code` | `tenantCode` | |
| `last_login_at` | `lastLoginAtUtc` | also checks `last_login_at_utc` |
| `tenant_display_name` | `tenantDisplayName` | fallback: `tenantCode ?? tenantId` |
| `created_at` | `createdAtUtc` | |
| `updated_at` | `updatedAtUtc` | |
| `is_locked` | `isLocked` | |
| `invite_sent_at` | `inviteSentAtUtc` | optional |

### RoleSummary / RoleDetail

| Backend field | Frontend field | Note |
|---------------|---------------|------|
| `user_count` | `userCount` | |
| `permissions[]` | `permissions: string[]` | accepts string[] or Permission[] |
| `created_at` | `createdAtUtc` | |
| `updated_at` | `updatedAtUtc` | |
| `resolved_permissions` | `resolvedPermissions` | |

### AuditLogEntry

| Backend field | Frontend field | Note |
|---------------|---------------|------|
| `actor_name` | `actorName` | |
| `actor_type` | `actorType` | `oneOf()` → 'Admin' \| 'System' |
| `entity_type` | `entityType` | |
| `entity_id` | `entityId` | |
| `metadata`/`meta` | `metadata` | accepts either key; validates as plain object |
| `created_at` | `createdAtUtc` | |

### PlatformSetting

| Backend field | Frontend field | Note |
|---------------|---------------|------|
| `value` | `value` | coerced to `boolean`/`number`/`string` based on `type` field |
| `description` | `description` | optional |

### MonitoringSummary

| Backend field | Frontend field | Note |
|---------------|---------------|------|
| `system.last_checked_at` | `system.lastCheckedAtUtc` | |
| `integrations[].latency_ms` | `latencyMs` | optional |
| `integrations[].last_checked_at` | `lastCheckedAtUtc` | |
| `alerts[].created_at` | `createdAtUtc` | |

### SupportCase / SupportCaseDetail / SupportNote

| Backend field | Frontend field | Note |
|---------------|---------------|------|
| `tenant_id` | `tenantId` | |
| `tenant_name` | `tenantName` | |
| `user_id` | `userId` | optional |
| `user_name` | `userName` | optional |
| `created_at` | `createdAtUtc` | |
| `updated_at` | `updatedAtUtc` | |
| `case_id` | `caseId` | *(SupportNote)* |
| `created_by` | `createdBy` | *(SupportNote)* |

---

## Error Handling Strategy

| Scenario | Behaviour |
|----------|---------|
| Missing required string field | Falls back to `''`; dev `console.warn` fires once |
| Missing required number field | Falls back to `0`; dev `console.warn` fires once |
| Invalid enum value | Falls back to safe default; dev `console.warn` fires once |
| null / undefined input to mapper | `asObj()` returns `{}` — all fields use fallbacks |
| Non-array `items` in paged response | `asArr()` returns `[]` — zero items, no crash |
| Non-object metadata in audit log | `metadata` is set to `undefined`, not crash |

Dev-mode warning pattern (never fires in production):
```ts
if (process.env.NODE_ENV !== 'production') {
  console.warn(`[api-mappers] mapTenantSummary.displayName: expected string at "display_name"/"displayName", got null. Using fallback "".`);
}
```

---

## `control-center-api.ts` Changes

All API calls changed from:
```ts
return apiClient.get<TenantSummary>(path);
```
to:
```ts
const raw = await apiClient.get<unknown>(path);
return mapPagedResponse(raw, mapTenantSummary);
```

This decouples the HTTP layer (returns `unknown`) from the domain layer (typed
via mappers), which is the correct architecture for resilient API integration.

**Function signatures are completely unchanged** — all callers (pages, server actions)
continue to work without modification.

---

## TODOs for Backend Integration

All mapper functions carry:
```ts
// TODO: replace manual mappers with generated types from OpenAPI spec
```

Once the Identity service exposes a Swagger/OpenAPI spec:
1. Generate TypeScript types with `openapi-typescript`
2. Replace `RawTenant`, `RawUser` etc. with generated request/response types
3. Mappers become thin adapters (possibly just type assertions)

---

## Independence Validation

- Zero imports from `apps/web`
- `api-mappers.ts` imports only: `@/types/control-center`
- `control-center-api.ts` imports only: `@/lib/api-client`, `@/lib/api-mappers`, `@/types/control-center`
- No new npm dependencies

---

## Any Issues or Assumptions

1. **`created_at` vs `createdAtUtc`** — The mapper tries `created_at` first. If
   the backend returns a proper ISO timestamp in either field, the value is preserved
   as-is. We do not parse or re-format timestamps — the UI renders them verbatim.

2. **`roles.list()` dual envelope** — The backend may return `RoleSummary[]` directly
   or wrapped in `{ items: [...], totalCount: N }`. The mapper handles both:
   ```ts
   if (Array.isArray(raw)) return raw.map(mapRoleSummary);
   const paged = mapPagedResponse(raw, mapRoleSummary);
   return paged.items;
   ```
   Same pattern applied to `settings.list()`.

3. **`UserSummary.lastLoginAtUtc` double-try** — Some backends may return
   `last_login_at` and others `last_login_at_utc`. Both are checked:
   ```ts
   optStr(r, 'last_login_at', 'lastLoginAtUtc') ?? optStr(r, 'last_login_at_utc', 'lastLoginAtUtc')
   ```

4. **Enum fallbacks are non-null** — Every `oneOf()` call has an explicit fallback
   so the UI never receives `undefined` for a typed enum field. This is a deliberate
   "best-effort display" strategy — show something meaningful rather than crashing.

5. **`ProductCode` validation** — `mapEntitlement` calls `oneOf()` against the exact
   six known product codes. If the backend introduces a new product not in the union,
   the mapper will fall back to `'SynqFund'` and log a dev warning. The union type in
   `types/control-center.ts` should be updated when new products are added.
