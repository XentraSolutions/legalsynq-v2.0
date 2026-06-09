# Monitoring Service

Platform health monitoring, uptime aggregation, alerting, and service status tracking.

**Port:** 5020

## Responsibilities

- Monitored entity registry (services, databases, external dependencies)
- Active health probe execution (HTTP, TCP, database)
- Uptime record aggregation and SLA tracking
- Alert rule management and alert history
- Latency tracking and degradation detection
- Real-time status dashboard feed (used by Control Center)

## Layer Structure

```
Monitoring.Api/            Endpoints, middleware, Program.cs (port 5020)
Monitoring.Application/    Probe orchestration, alert evaluation, uptime aggregation
Monitoring.Domain/         MonitoredEntity, UptimeRecord, Alert, AlertRule
Monitoring.Infrastructure/ DbContext (MonitoringDb), HTTP probe adapter, EF migrations
```

## Key Endpoint Groups

| Prefix | Description |
|---|---|
| `/api/monitoring/entities` | Monitored entity CRUD |
| `/api/monitoring/health` | Current health snapshot |
| `/api/monitoring/uptime` | Uptime records and SLA stats |
| `/api/monitoring/alerts` | Alert rule management |
| `/api/monitoring/alerts/history` | Alert history log |
| `/api/monitoring/latency` | Latency data by entity |

## Auth

All endpoints require either `Bearer` token (user-facing) or an internal service token. The Control Center calls these endpoints via the gateway with a `PlatformAdmin` session.

## Database

`MonitoringDb` (MySQL).

## Notes

- All backend services register themselves as monitored entities on startup
- Reports service is also registered as a monitored entity and its health is reflected in the Control Center Monitoring view
- Probe results are stored per-check and aggregated into daily uptime windows
