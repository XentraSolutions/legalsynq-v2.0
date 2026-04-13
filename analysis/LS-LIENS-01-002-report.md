# LS-LIENS-01-002 — Liens Gateway Registration Report

## 1. Summary

Registered the `liens` microservice in the v2 API gateway (YARP reverse proxy) so it becomes reachable through the platform's single entry point at port 5010. No business logic, auth policies, identity integration, or frontend changes were introduced.

## 2. Existing Gateway Pattern Identified

The gateway uses **YARP (Yet Another Reverse Proxy)** configured entirely via the `ReverseProxy` section of `appsettings.json`. Each service follows a consistent pattern:

| Component | Convention |
|-----------|-----------|
| **Health route** | `{service}-service-health`, Anonymous, low Order (10–60) |
| **Info route** | `{service}-service-info`, Anonymous, Order = health + 1 |
| **Protected catch-all** | `{service}-protected`, default auth (RequireAuthorization), high Order (100–160) |
| **Path transform** | `PathRemovePrefix: "/{service}"` strips the service prefix before forwarding |
| **Cluster** | `{service}-cluster` with a single `{service}-primary` destination |

All routes use `PathRemovePrefix` to strip the gateway-level prefix, forwarding the remainder to the downstream service's internal path space.

## 3. Files Changed

| File | Change |
|------|--------|
| `apps/gateway/Gateway.Api/appsettings.json` | Added 3 route definitions + 1 cluster definition for `liens` |

No changes were required in the Liens service itself — it already exposes `/health` and `/info` endpoints using the shared `Contracts` library pattern.

## 4. Gateway Registration Details

### Routes Added

```json
"liens-service-health": {
  "ClusterId": "liens-cluster",
  "AuthorizationPolicy": "Anonymous",
  "Order": 60,
  "Match": { "Path": "/liens/health" },
  "Transforms": [{ "PathRemovePrefix": "/liens" }]
}

"liens-service-info": {
  "ClusterId": "liens-cluster",
  "AuthorizationPolicy": "Anonymous",
  "Order": 61,
  "Match": { "Path": "/liens/info" },
  "Transforms": [{ "PathRemovePrefix": "/liens" }]
}

"liens-protected": {
  "ClusterId": "liens-cluster",
  "Order": 160,
  "Match": { "Path": "/liens/{**catch-all}" },
  "Transforms": [{ "PathRemovePrefix": "/liens" }]
}
```

### Cluster Added

```json
"liens-cluster": {
  "Destinations": {
    "liens-primary": {
      "Address": "http://localhost:5009"
    }
  }
}
```

## 5. External Route Contract

| External Path | Internal Path | Auth |
|--------------|---------------|------|
| `GET /liens/health` | `GET /health` (port 5009) | Anonymous |
| `GET /liens/info` | `GET /info` (port 5009) | Anonymous |
| `ANY /liens/{**path}` | `ANY /{**path}` (port 5009) | Authenticated (JWT) |

The canonical external prefix is `/liens/*`. When the BFF layer adds the Next.js `/api/` prefix, the full external contract becomes `/api/liens/*`.

## 6. Path Transform / Rewrite Behavior

All three routes use `PathRemovePrefix: "/liens"`:
- Gateway receives: `GET /liens/health`
- Transform strips: `/liens`
- Forwarded to Liens service: `GET /health`

This is identical to how all other services (fund, careconnect, documents, etc.) are configured.

## 7. Config / Environment Changes

- **Order numbers**: Health=60, Info=61, Protected=160. These follow the established gap pattern (documents=50–53/150, next available=60/160).
- **Port**: 5009, consistent with the Liens service `appsettings.json` (`"Urls": "http://0.0.0.0:5009"`).
- No environment variables added. No new config keys beyond the standard YARP route/cluster definitions.

## 8. Build Results

### Gateway
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Liens Service
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## 9. Run Results

Both services start successfully as part of the platform's `run-dev.sh` / `run-prod.sh` orchestration. The gateway binds to port 5010 and the Liens service binds to port 5009.

## 10. Gateway Forwarding Test Results

### Direct (port 5009)

| Endpoint | HTTP Status | Response |
|----------|------------|----------|
| `GET http://localhost:5009/health` | 200 | `{"status":"ok","service":"liens"}` |
| `GET http://localhost:5009/info` | 200 | `{"service":"liens","environment":"Production","version":"v1"}` |

### Via Gateway (port 5010)

| Endpoint | HTTP Status | Response |
|----------|------------|----------|
| `GET http://localhost:5010/liens/health` | 200 | `{"status":"ok","service":"liens"}` |
| `GET http://localhost:5010/liens/info` | 200 | `{"service":"liens","environment":"Production","version":"v1"}` |

Both gateway-forwarded responses match the direct responses exactly, confirming correct routing and path transformation.

## 11. Deviations

None. The implementation follows the existing pattern exactly:
- Route naming: `{service}-service-health`, `{service}-service-info`, `{service}-protected`
- Order numbering: next available slot (60/61/160)
- Transform: `PathRemovePrefix`
- Cluster naming: `{service}-cluster` / `{service}-primary`

## 12. v1 Confirmation

No v1 services or logic were introduced. The registration uses only the v2 gateway and v2 Liens service. No v1 identity, documents, notifications, or audit services were referenced.

## 13. Final Readiness Statement

| Question | Answer |
|----------|--------|
| Is Liens reachable through the gateway? | **Yes** — both `/liens/health` and `/liens/info` return correct responses via the gateway |
| Is the protected catch-all route in place? | **Yes** — `/liens/{**catch-all}` requires JWT authentication, ready for future business endpoints |
| Is the system ready for the next feature? | **Yes** — the Liens service is a first-class v2 service in the gateway, ready for endpoint development |
