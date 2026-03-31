# Step 24 — Cutover Inventory

**Date:** 2026-03-30

## Legacy Write Paths (complete list)

| Service | File | Method | Table | Fields Written |
|---|---|---|---|---|
| Identity | `AdminEndpoints.cs` | Various (CreateUser, UpdateUser, etc.) | `AuditLogs` (Identity DB) | ActorName, ActorType, Action, EntityType, EntityId, MetadataJson |
| Identity | `AdminEndpoints.cs` | `AssignRole` | `AuditLogs` NOT written — canonical only | — |
| Identity | `AdminEndpoints.cs` | `RevokeRole` | `AuditLogs` NOT written — canonical only | — |

> **Note:** The exact set of AdminEndpoints methods that still call `AuditLog.Create()` requires a manual audit of the full AdminEndpoints.cs file. AssignRole and RevokeRole have been confirmed as canonical-only.

## Canonical Write Paths (current)

| Service | Event Type | File | Pattern |
|---|---|---|---|
| Identity AuthService | `user.login.succeeded` | `AuthService.cs` | fire-and-observe, idempotency key includes timestamp |
| Identity AdminEndpoints | `user.role.assigned` | `AdminEndpoints.cs:AssignRole` | fire-and-observe, idempotency key excludes timestamp |
| Identity AdminEndpoints | `user.role.revoked` | `AdminEndpoints.cs:RevokeRole` | fire-and-observe, idempotency key excludes timestamp |

## Legacy Read Paths

| Consumer | Route | Auth | Response Shape |
|---|---|---|---|
| Control Center (legacy mode) | `GET /identity/api/admin/audit` | PlatformAdmin JWT | `{items: AuditLogEntry[], totalCount, page, pageSize}` |

## Canonical Read Paths

| Consumer | Route | Auth | Response Shape |
|---|---|---|---|
| Control Center (canonical/hybrid) | `GET /audit-service/audit/events` | Currently None (QueryAuth=None) | `AuditEventQueryResponse {Items, TotalCount, Page, PageSize}` |
| Internal (direct) | `GET /audit/events/{id}` | None | Single `AuditEventRecordResponse` |
| Internal (direct) | `GET /audit/entity/{type}/{id}` | None | Paged events by entity |
| Internal (direct) | `GET /audit/actor/{actorId}` | None | Paged events by actor |
| Internal (direct) | `GET /audit/tenant/{tenantId}` | None | Paged events by tenant |

## Services: Integration Status

| Service | DI Registered | Events Emitting | Notes |
|---|---|---|---|
| Identity (AuthService) | Yes | `user.login.succeeded` | Complete |
| Identity (AdminEndpoints) | Yes (via DI parameter) | `user.role.assigned`, `user.role.revoked` | Complete |
| CareConnect | Yes | None | `IAuditEventClient` not injected into services |
| Fund | No | None | Not started |
| Documents | No | None | Not assessed |
| Platform Audit Service (self) | N/A | N/A | The service itself |

## Audit Service Configuration State

| Setting | Dev Value | Production Needed |
|---|---|---|
| `Database:Provider` | `InMemory` | `Sqlite` or `MySql` / `PostgreSQL` |
| `IngestAuth:Mode` | `None` | `ServiceToken` |
| `QueryAuth:Mode` | `None` | `Bearer` |
| `Integrity:HmacKeyBase64` | `""` (empty) | Non-empty 256-bit key |
| `Integrity:VerifyOnRead` | `false` | `true` |

## Gateway Routes

| Route Name | Gateway Path | Upstream | Status |
|---|---|---|---|
| `audit-service-health` | `GET /audit-service/health` | `audit-cluster` (:5007) | Active |
| `audit-service-info` | `GET /audit-service/audit/info` | `audit-cluster` (:5007) | Active |
| `audit-service-query` | `GET /audit-service/audit/events` | `audit-cluster` (:5007) | Active |
| `audit-service-export` | `GET /audit-service/audit/export` | `audit-cluster` (:5007) | Active |
