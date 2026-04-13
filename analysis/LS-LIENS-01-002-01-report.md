# LS-LIENS-01-002-01 — Gateway Auth Hardening Report

## 1. Summary

Removed premature authentication enforcement from the `liens-protected` catch-all route in the gateway. The route previously inherited the gateway's default `RequireAuthorization()` behavior (JWT required), but identity integration has not been implemented for Liens yet. This correction aligns the gateway configuration with the current delivery stage.

## 2. File Changed

| File | Change |
|------|--------|
| `apps/gateway/Gateway.Api/appsettings.json` | Added `"AuthorizationPolicy": "Anonymous"` to the `liens-protected` route |

No other files were modified.

## 3. Previous Auth Behavior

The `liens-protected` route had no explicit `AuthorizationPolicy` property. Because the gateway applies `app.MapReverseProxy().RequireAuthorization()` globally, all routes without an explicit `AuthorizationPolicy: "Anonymous"` override default to requiring a valid JWT. This meant any request to `/liens/{anything}` (other than `/liens/health` and `/liens/info`) would be rejected with 401 unless a valid JWT was provided — even though the Liens service has no identity integration yet.

## 4. New Route Configuration

```json
"liens-protected": {
  "ClusterId": "liens-cluster",
  "AuthorizationPolicy": "Anonymous",
  "Order": 160,
  "Match": {
    "Path": "/liens/{**catch-all}"
  },
  "Transforms": [
    { "PathRemovePrefix": "/liens" }
  ]
}
```

## 5. Approach: `AuthorizationPolicy: "Anonymous"`

Set `AuthorizationPolicy` to `"Anonymous"` rather than removing the property entirely. Rationale:

- The gateway defaults to `RequireAuthorization()` for all YARP routes (see `Program.cs` line 58)
- Removing the property would leave the route at the default (authenticated), which is the opposite of the intent
- `"Anonymous"` is the established convention used by all other anonymous routes in the gateway (health, info, identity-login, documents-access, etc.)
- This makes the intent explicit and reviewable

## 6. Preserved Components

| Component | Status |
|-----------|--------|
| Route `liens-protected` | Preserved |
| Route `liens-service-health` | Unchanged |
| Route `liens-service-info` | Unchanged |
| Cluster `liens-cluster` | Unchanged |
| Destination `liens-primary` (port 5009) | Unchanged |
| Path transform `PathRemovePrefix: "/liens"` | Unchanged |
| External route contract `/liens/*` | Unchanged |
| Order numbers (60, 61, 160) | Unchanged |

## 7. Build Results

### Gateway
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Liens Service
No changes made; previous build remains valid (0W/0E).

## 8. Run Results

Both services start successfully as part of the platform orchestration. Gateway binds to port 5010, Liens binds to port 5009.

## 9. Gateway Test Results

| Route | HTTP Status | Response | Auth Required? |
|-------|-----------|----------|---------------|
| `GET /liens/health` | 200 | `{"status":"ok","service":"liens"}` | No (Anonymous) |
| `GET /liens/info` | 200 | `{"service":"liens","environment":"Production","version":"v1"}` | No (Anonymous) |
| `GET /liens/test` | 404 | Empty body (from Liens service) | No (Anonymous) |

The catch-all sample (`/liens/test`) returned 404 from the downstream Liens service — not 401 from the gateway. This confirms:
- The request was forwarded successfully through the gateway
- No authentication gate blocked the request
- The 404 is the Liens service's response to an undefined route

## 10. Auth Assumption Confirmation

- No route in the gateway requires auth for Liens traffic
- The Liens service has no JWT middleware, no auth middleware, no identity coupling
- Gateway and service are now consistent: both treat all Liens routes as anonymous

## 11. v1 Confirmation

No v1 services, logic, or patterns were introduced. The change is a single property addition in the existing v2 gateway configuration.

## 12. Final Readiness Statement

| Question | Answer |
|----------|--------|
| Is LS-LIENS-01-002 now complete? | **Yes** — Liens is registered in the gateway with correct anonymous routing, health/info/catch-all routes, and no premature auth enforcement |
| Is the system ready for LS-LIENS-02-001? | **Yes** — the gateway registration is clean and the service boundary is consistent with the current delivery stage |
