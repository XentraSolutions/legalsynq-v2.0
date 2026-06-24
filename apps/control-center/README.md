# Control Center (`apps/control-center`)

Internal operator administration portal for LegalSynq platform staff. Requires `PlatformAdmin` system role.

**Port:** 5004

## Tech

- Next.js 15.2.9 App Router, TypeScript, Tailwind CSS v4, React 18
- `node_modules` at monorepo root — must NOT have a local `node_modules` (duplicate React causes `useReducer` null errors)

## Auth

Cookie-based session (`platform_session`) validated via Identity `/auth/me`. All protected routes behind `requirePlatformAdmin()` middleware.

## API Pattern

BFF — local route handlers for auth and a few specific endpoints; all other `/api/*` requests fall through to a `fallback` rewrite to the gateway at `CONTROL_CENTER_API_BASE` (default `http://127.0.0.1:5010`).

## Key Sections

| Route | Purpose |
|---|---|
| `/` | Dashboard — system health, KPIs, recent audit events, support cases |
| `/tenants` | Tenant list and detail — status, entitlements, branding |
| `/users` | Cross-tenant user management |
| `/audit` | SynqAudit investigation — events, integrity checks, exports |
| `/monitoring` | Service health probes, uptime, alert history |
| `/notifications` | Notification templates, governance rules, delivery logs |
| `/reports` | Reports service health, templates, assignments |
| `/support` | Support case management |

## Key Files

```
src/
  lib/
    env.ts                  Centralised env var access
    session.ts              Server session (reads cookie → Identity /auth/me)
    auth-guards.ts          requirePlatformAdmin() — server component guard
    control-center-api.ts   API client for all backend calls
  middleware.ts             Route protection (redirects unauthenticated to /login)
```

## Environment

`apps/control-center/.env.local` (gitignored):
```
CONTROL_CENTER_API_BASE=http://127.0.0.1:5010
```
