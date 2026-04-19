# LS-NOTIF-SENDGRID-UPDATE Report

**Status**: In Progress  
**Date**: 2026-04-19

---

## Summary

The Notifications service SendGrid integration is fully environment-driven — no
secrets or sender identity values are hardcoded.  Updating to a new SendGrid account
requires setting new environment variable / secret values and restarting the service.
No code changes are required.

---

## Configuration Changes

### Keys read by the service

| Key | Where used | Current state |
|-----|-----------|---------------|
| `SENDGRID_API_KEY` | `DependencyInjection.cs` → `SendGridAdapter` (Bearer auth header) | **Set** (secret exists) |
| `SENDGRID_FROM_EMAIL` | `DependencyInjection.cs` → `SendGridAdapter` (default from-address) | **Not set** — falls back to `noreply@legalsynq.com` |
| `SENDGRID_FROM_NAME` | `DependencyInjection.cs` → `SendGridAdapter` (default display name) | **Not set** — falls back to `LegalSynq` |
| `SENDGRID_WEBHOOK_VERIFICATION_ENABLED` | `DependencyInjection.cs` → `SendGridVerifier` | **Not set** — defaults to `false` (disabled) |
| `SENDGRID_WEBHOOK_PUBLIC_KEY` | `DependencyInjection.cs` → `SendGridVerifier` | **Not set** |

### Binding path

All keys are read in `Notifications.Infrastructure/DependencyInjection.cs`:

```csharp
var sgApiKey    = configuration["SENDGRID_API_KEY"]    ?? "";
var sgFromEmail = configuration["SENDGRID_FROM_EMAIL"] ?? "noreply@legalsynq.com";
var sgFromName  = configuration["SENDGRID_FROM_NAME"]  ?? "LegalSynq";
```

These values are passed directly into `SendGridAdapter(apiKey, fromEmail, fromName, ...)`.
Configuration is captured at service startup — a restart is required after changing values.

---

## Files Reviewed

| File | Notes |
|------|-------|
| `Notifications.Infrastructure/DependencyInjection.cs` | Config binding + adapter registration |
| `Notifications.Infrastructure/Providers/Adapters/SendGridAdapter.cs` | API call to `POST https://api.sendgrid.com/v3/mail/send`, health check via `GET /v3/scopes` |
| `Notifications.Infrastructure/Webhooks/Verifiers/SendGridVerifier.cs` | ECDsa webhook signature verification (disabled unless `SENDGRID_WEBHOOK_VERIFICATION_ENABLED=true`) |

---

## Environment Variables Used

```
SENDGRID_API_KEY                         — HS256 API key for v3 mail/send
SENDGRID_FROM_EMAIL                      — Verified sender address on the account
SENDGRID_FROM_NAME                       — Display name for outbound mail
SENDGRID_WEBHOOK_VERIFICATION_ENABLED    — true/false (default false)
SENDGRID_WEBHOOK_PUBLIC_KEY              — ECDSA public key from SendGrid Event Webhook settings
```

---

## Test Execution

Pending credential update and service restart.

---

## Validation Results

| Check | Result |
|-------|--------|
| Code reads from env vars (no hardcoding) | ✅ |
| `SendGridAdapter` passes key as Bearer token | ✅ |
| `SendGridAdapter.ValidateConfigAsync()` returns false when key is empty | ✅ |
| Health check (`GET /v3/scopes`) confirms key validity | Pending restart |
| Test `POST /v1/notifications` accepted | Pending credential update |

---

## Issues Encountered

None identified in code.  Awaiting new credentials from user.

---

## Recommendations

1. After setting new secrets, restart the Notifications service workflow.
2. Verify the sender address is verified / authenticated in the new SendGrid account
   (domain authentication or single-sender verification).
3. If the new account uses Event Webhooks, set `SENDGRID_WEBHOOK_VERIFICATION_ENABLED=true`
   and provide `SENDGRID_WEBHOOK_PUBLIC_KEY` from the SendGrid dashboard
   (Settings → Mail Settings → Event Webhooks → Signature Verification).
4. Monitor `ProviderHealthWorker` logs after restart — it polls `GET /v3/scopes`
   periodically and will log `"down"` if the key is invalid.
