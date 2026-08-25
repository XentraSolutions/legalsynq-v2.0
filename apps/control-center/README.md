# Control Center (`apps/control-center`)

Internal operator administration portal for LegalSynq platform staff. Requires `PlatformAdmin` system role.

**Port:** 5004

## Tech

- Next.js 16.2.6 App Router, TypeScript, Tailwind CSS v4, React 18
- Local development uses the monorepo/root pnpm install path; do not add a duplicate source-tree `apps/control-center/node_modules` manually because duplicate React can break hooks. Production runtime artifacts are separate and are packaged with `package.json`, `pnpm-lock.yaml`, and `pnpm-workspace.yaml`; the app manifest pins `packageManager: pnpm@10.26.1` so Corepack does not drift during `pnpm install --production`.

## Auth

Cookie-based session (`platform_session`) validated via Identity `/auth/me`. All protected routes behind `requirePlatformAdmin()` middleware.

## API Pattern

BFF — local route handlers for auth and a few specific endpoints; all other `/api/*` requests fall through to a `fallback` rewrite to the gateway at `CONTROL_CENTER_API_BASE` (default `http://127.0.0.1:5010`).

## Key Sections

| Route | Purpose |
|---|---|
| `/` | Dashboard — system health, KPIs, recent audit events, support cases |
| `/tenants` | Tenant list and detail — status, entitlements, branding, DNS provisioning status/retries |
| `/products` | Product catalog and platform URL configuration; tenant enablement remains on tenant detail |
| `/tenant-applications` | Review-first self-registration queue with approval, decline, and DNS provisioning progress |
| `/users` | Cross-tenant user management |
| `/audit` | SynqAudit investigation — events, integrity checks, exports |
| `/monitoring` | Service health probes, uptime, alert history |
| `/notifications` | Notification templates, governance rules, delivery logs |
| `/reports` | Reports service health, templates, assignments |
| `/support` | Support case management |
| `/xenia/settings` | Xenia Assistant Settings — provider, model, reasoning effort, verbosity, output cap, and OpenAI runtime configuration |

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
