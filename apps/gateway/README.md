# Gateway

YARP reverse proxy that is the single entry point for all API traffic from both frontends.

**Port:** 5010

## Responsibilities

- JWT validation (all routes except `/health`, `/info`, public branding, document access endpoints, and token-gated public buyer links)
- Request routing to downstream services via YARP
- Tenant context propagation (`X-Tenant-Code`, `X-Tenant-Id` headers forwarded)
- Correlation ID forwarding

## Key Files

```
Gateway.Api/
  Program.cs          JWT auth setup + YARP pipeline
  appsettings.json    YARP routes/clusters + JWT config
```

## Routes (summary)

| Prefix | Upstream service |
|---|---|
| `/api/auth/*` | Identity `:5001` |
| `/api/users/*` | Identity `:5001` |
| `/api/tenants/*` | Identity / Tenant `:5001 / :5005` |
| `/api/careconnect/*` | CareConnect `:5003` |
| `/liens/*` | Liens `:5009` |
| `/api/fund/*` | Fund `:5008` |
| `/api/documents/*` | Documents `:5006` |
| `/api/notifications/*` | Notifications `:5025` |
| `/api/audit/*` | Audit `:5007` |
| `/api/monitoring/*` | Monitoring `:5020` |
| `/api/reports/*` | Reports `:5029` |
| `/api/flow/*` | Flow `:5015` |

## Notes

- All downstream services independently validate JWTs — the gateway is not a single point of auth enforcement.
- SynqLien buyer offer emails open `/selling/public/{token}` in the tenant web app, which forwards to the gateway path `/liens/api/liens/selling/public/{token}`. That gateway route is anonymous; the Liens service validates the opaque buyer access token and expiry.
- Internal service-to-service calls (e.g. Identity → Notifications, Tenant → Identity) bypass the gateway and use direct HTTP with service tokens or provisioning secrets.
- For `systemd` deployments that use `EnvironmentFile=`, keep YARP cluster and destination override keys underscore-only, for example `ReverseProxy__Clusters__identity_cluster__Destinations__identity_primary__Address`.

## Required header rules — CareConnect common portal (AUTH-CC01)

The CareConnect forgot-password flow relies on two headers being trustworthy when they reach the BFF and Identity service. The reverse proxy **must** enforce these rules before traffic reaches the Next.js BFF:

| Header | Rule | Reason |
|---|---|---|
| `x-forwarded-host` | Strip from external requests; set to the actual incoming hostname | BFF uses this to detect `CC_COMMON_PORTAL_HOSTNAME` and set `resolveByEmail=true`. Spoofing it from outside would bypass tenant resolution. |
| `X-Ls-Internal-Source` | Strip from external requests; never set on external traffic | Identity requires this header to accept `resolveByEmail=true`. The BFF always adds it for its own internal calls. External callers must not be able to inject it. |

Without these rules in place, an external caller could spoof either header and trigger cross-tenant email-based user lookup.
