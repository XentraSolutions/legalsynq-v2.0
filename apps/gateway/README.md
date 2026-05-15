# Gateway

YARP reverse proxy that is the single entry point for all API traffic from both frontends.

**Port:** 5010

## Responsibilities

- JWT validation (all routes except `/health`, `/info`, public branding, and document access endpoints)
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
| `/api/liens/*` | Liens `:5002` |
| `/api/fund/*` | Fund `:5008` |
| `/api/documents/*` | Documents `:5006` |
| `/api/notifications/*` | Notifications `:5025` |
| `/api/audit/*` | Audit `:5007` |
| `/api/monitoring/*` | Monitoring `:5020` |
| `/api/reports/*` | Reports `:5029` |
| `/api/flow/*` | Flow `:5015` |

## Notes

- All downstream services independently validate JWTs — the gateway is not a single point of auth enforcement.
- Internal service-to-service calls (e.g. Identity → Notifications, Tenant → Identity) bypass the gateway and use direct HTTP with service tokens or provisioning secrets.
