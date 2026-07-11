---
name: Xenia endpoint pattern
description: How to correctly resolve tenant context and actor identity in Xenia minimal-API endpoint handlers.
---

## Rule
Use `XeniaTenantContextAccessor` (sync `.Current` property) in minimal-API lambda handlers — **not** `ITenantContextResolver.ResolveAsync()` (async interface, available for middleware).

## How to apply
```csharp
// CORRECT — inject accessor, read .Current
private static async Task<IResult> MyEndpoint(
    XeniaTenantContextAccessor tenantCtx, ...)
{
    var tc = tenantCtx.Current;
    if (tc is null || !tc.IsResolved) return Results.Unauthorized();
    var tenantId = tc.TenantId;
    var actorId  = tc.ActorId; // Guid? — use this, not UserId (doesn't exist)
}

// WRONG — async resolver injected directly into handler parameter
// ITenantContextResolver tenantResolver <-- wrong in handler
```

## IXeniaTenantContext properties
- `TenantId` (Guid) — throws if not resolved
- `ActorId` (Guid?) — null for service-to-service requests
- `TenantCode` (string?) — optional
- `CorrelationId` (string?) — optional
- `IsResolved` (bool) — check before accessing TenantId

**Why:** The accessor is populated by XeniaTenantContextMiddleware before the handler runs, making it synchronously available. ResolveAsync is designed for middleware contexts, not handler lambdas.
