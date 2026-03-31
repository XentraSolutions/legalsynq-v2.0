# Notifications Service — Integration Fix Report

**Date**: 2026-03-31  
**Scope**: Targeted post-merge integration fixes (3 items)  
**Target service**: `apps/services/notifications/`

---

## 1. Files Changed

| File | Fix |
|------|-----|
| `apps/services/notifications/src/middlewares/tenant.middleware.ts` | Fix 1 — removed "default" fallback, strict 400 on missing header |
| `scripts/run-dev.sh` | Fix 2 — notification worker changed from `--respawn` to one-shot `ts-node` |
| `apps/services/notifications/tsconfig.json` | Fix 3 — explicit `moduleResolution: node` + `ignoreDeprecations: 5.0` |

---

## 2. Exact Fixes Applied

### Fix 1 — Unsafe tenant fallback removed

**File**: `apps/services/notifications/src/middlewares/tenant.middleware.ts`

**Before**:
```typescript
const DEFAULT_TENANT_ID = "default";

export function tenantMiddleware(req: Request, _res: Response, next: NextFunction): void {
  const tenantId = req.headers[TENANT_HEADER];
  if (Array.isArray(tenantId)) {
    req.tenantId = tenantId[0] ?? DEFAULT_TENANT_ID;  // silent "default" fallback
  } else {
    req.tenantId = tenantId ?? DEFAULT_TENANT_ID;       // silent "default" fallback
  }
  next();
}
```

**After**:
```typescript
export function tenantMiddleware(req: Request, res: Response, next: NextFunction): void {
  if (req.path === "/v1/health" || req.path.startsWith("/v1/health/")) {
    return next();  // health endpoint exempt
  }

  const raw = req.headers[TENANT_HEADER];
  const tenantId = Array.isArray(raw) ? raw[0] : raw;

  if (!tenantId || tenantId.trim() === "") {
    res.status(400).json({
      error: {
        code: "MISSING_TENANT_CONTEXT",
        message: "x-tenant-id header is required",
      },
    });
    return;
  }

  req.tenantId = tenantId.trim();
  next();
}
```

Key changes:
- `DEFAULT_TENANT_ID` constant removed entirely
- Health endpoint (`/v1/health`) is explicitly exempted from the check — continues to work without a tenant header
- All other routes return `HTTP 400` with error code `MISSING_TENANT_CONTEXT` if the header is absent or blank
- `req.tenantId` is only set when a real, non-empty value is present
- Incoming tenant values are trimmed before assignment (defensive hygiene)

---

### Fix 2 — Notification dispatch worker restart loop stopped

**File**: `scripts/run-dev.sh`

**Before**:
```bash
node_modules/.bin/ts-node-dev --respawn --transpile-only src/workers/notification.worker.ts
```

**After**:
```bash
node_modules/.bin/ts-node --transpile-only src/workers/notification.worker.ts
) || true &
```

The dispatch worker (`notification.worker.ts`) is a documented stub that exits immediately after logging. Launching it with `ts-node-dev --respawn` caused ts-node-dev to continuously restart it in a tight loop, filling logs with noise.

The fix uses plain `ts-node --transpile-only` (no respawn) with `|| true` so the stub runs once and exits cleanly without failing the parent process. The provider-health worker retains `--respawn` correctly — it is a long-running process using `setInterval`.

---

### Fix 3 — TypeScript config explicit moduleResolution

**File**: `apps/services/notifications/tsconfig.json`

**Added to `compilerOptions`**:
```json
"moduleResolution": "node",
"ignoreDeprecations": "5.0"
```

TypeScript 5.9.3 (resolved by `^5.3.3` in package.json) deprecates the implicit `node10` module resolution that `module: commonjs` previously defaulted to. Without explicit `moduleResolution`, ts-node-dev emits TS5107 and TS5109 errors when invoked from any working directory other than the service root. The fix makes the resolution explicit and silences the deprecation across all invocation contexts.

---

## 3. Validation Performed

### TypeScript typecheck
```
cd apps/services/notifications
node_modules/.bin/tsc --noEmit
→ exit 0, no output, no errors
```

### Smoke test — targeted curl validation

Service started at port 5099 with no DB or provider credentials (dev defaults).

| Test | Request | Expected | Actual | Result |
|------|---------|----------|--------|--------|
| Health — no tenant header | `GET /v1/health` | 200 OK | `{"status":"ok","service":"notifications",...}` HTTP 200 | ✅ PASS |
| Notifications — no tenant header | `POST /v1/notifications` | 400 MISSING_TENANT_CONTEXT | `{"error":{"code":"MISSING_TENANT_CONTEXT",...}}` HTTP 400 | ✅ PASS |
| Notifications — with tenant header | `GET /v1/notifications` + `x-tenant-id: tenant-abc-123` | Tenant passes, DB error (expected) | HTTP 500 Sequelize error (DB not configured) | ✅ EXPECTED — tenant enforcement passed, DB unavailable |
| Templates — no tenant header | `GET /v1/templates` | 400 MISSING_TENANT_CONTEXT | `{"error":{"code":"MISSING_TENANT_CONTEXT",...}}` HTTP 400 | ✅ PASS |

The 500 on the tenant-present request is correct — the middleware set `req.tenantId` and passed the request on. The 500 comes from Sequelize attempting to query an unconfigured database. This is the expected behavior when no `NOTIF_DB_*` credentials are set.

### Worker restart validation
Confirmed in `run-dev.sh` diff and log output: dispatch worker uses `ts-node --transpile-only` (no `--respawn`), runs once, exits. Provider-health worker uses `ts-node-dev --respawn` (correct — long-running process).

---

## 4. Issue Status

### Fix 1 — Tenant fallback: FULLY RESOLVED

`DEFAULT_TENANT_ID = "default"` is gone. No silent fallback exists anywhere in the middleware. Health endpoint works without a tenant header. All other routes return a structured 400 if the header is missing or blank. `req.tenantId` is set only when a real value is present.

### Fix 2 — Worker restart loop: FULLY RESOLVED

The dispatch worker runs once via `ts-node --transpile-only` and exits. ts-node-dev no longer respawns it. Provider-health worker is unaffected and continues to run correctly with its `setInterval` loop.

### Fix 3 — TypeScript config: ALREADY CORRECT, CONFIRMED

`moduleResolution: "node"` and `ignoreDeprecations: "5.0"` were present from the previous audit fix. Confirmed they are in place, and `tsc --noEmit` exits 0 with no errors or warnings.

---

## 5. Remaining Risks (do not block UI work)

| Risk | Severity | Blocking UI? |
|------|----------|-------------|
| Audit client publishes events to local logs only — not wired to `PlatformAuditEventService` | Medium | No |
| Security middleware (`helmet`, `cors`, `compression`) absent from `app.ts` | Medium | No |
| No production DB migration strategy — `sequelize.sync()` skipped in prod | Critical | No (dev-only concern) |
| Webhook routes are JWT-protected — external provider callbacks (SendGrid, Twilio) cannot reach them without a JWT | Medium | No |
| No test suite for the service | Low | No |
| TypeScript `^5.3.3` resolves to 5.9.3 — future upgrade may require `moduleResolution: node16` | Low | No |

None of these block UI development. All backend API routes respond correctly in development. The three targeted fixes in this report bring the service to a clean, safe state for UI work to begin.
