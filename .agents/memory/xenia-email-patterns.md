---
name: Xenia email module patterns
description: Key design decisions and gotchas from XENIA-P1-T2 email module implementation.
---

## Domain entity base class

`EmailValidationHistory` (immutable audit entity) MUST extend `AuditableEntityBase`, not implement `IAuditableEntity` directly. `IAuditableEntity` requires `UpdatedAtUtc` + `SetUpdatedAt` — extending the base class satisfies both without manually implementing them.

**Why:** `IAuditableEntity` has four members; implementing it directly on a class without `UpdatedAtUtc` yields `CS0535` at build time.

## Secret reference pattern

- `EmailSource.SecretReferenceId` stores an opaque reference key (e.g. `vault://org/secret-name`). Never stores the actual credential.
- `ISecretReferenceService.ResolveAsync()` returns the actual value at runtime.
- Dev stub: `UnavailableSecretReferenceService` always returns `Unavailable`.
- DTO exposes only `HasSecretReference: bool` — never the ref ID.

**Why:** Prevents credential leakage in API responses, logs, or DB dumps. Also passes security audit.

## SSRF guard

`SsrfGuard.IsHostSafe(host)` must be called by all connectors before any network attempt. Blocks: loopback, 10.x, 192.168.x, 172.16–31.x, 169.254.x, metadata.google.internal.
Result code on block: `EmailValidationResult.SsrfBlocked`.

## TLS enforcement

All connectors reject `UseTls = false` with `TlsRequired`. This is non-negotiable even if the caller supplies `useTls: false`.

## OAuth2 deferral

OAuth2 full flow (grant + token exchange + refresh) deferred to XENIA-P1-T3. Connectors return `ValidatorUnavailable` for OAuth2 auth type. Domain model (`EmailAuthType.OAuth2`) is complete — only the live flow is deferred.

## EF enum conversion

Use `EnumToStringConverter<T>()` explicit instances in every EF entity configuration. `HasConversion<string>()` crashes at Pomelo 8 + EF 8 + .NET 10. See `pomelo-enum-converter-nullref.md`.

## Migration tooling

`dotnet ef migrations add` crashes in the Replit .NET 10 environment (missing designer). Author migrations manually. Apply schema via pymysql against `xenia_db` on `127.0.0.1:3306` (user `xenia`, password `xenia` in dev). Production: EF `MigrateAsync()` on service startup.

## Connector registry

`EmailSourceConnectorRegistry` is Singleton (connectors are stateless). Individual connectors are Transient. Registry throws `InvalidOperationException` on duplicate provider registration to catch misconfiguration early.
