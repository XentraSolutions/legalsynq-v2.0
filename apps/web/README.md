# Tenant Portal (`apps/web`)

The main product application used by end users (law firms, healthcare providers, funding organisations).

**Port:** 5000 (dev proxy) → internal Next.js on 3050

## Tech

- Next.js 15.2.9 App Router, TypeScript, Tailwind CSS, React 18
- `node_modules` installed at monorepo root — `apps/web` inherits via Node resolution traversal

## Auth & Session

- Login: `POST /api/auth/login` → BFF sets `platform_session` HttpOnly cookie
- Session validation: `GET /api/auth/me` (BFF, server-side) — frontend never decodes raw JWTs
- Logout: `POST /api/auth/logout`

## BFF Pattern

All API calls from the browser go through Next.js API routes that:
1. Read `platform_session` cookie
2. Exchange it for a Bearer token
3. Forward the request to the gateway

Client code uses relative `/api/` paths — rewrite in `next.config` maps to the gateway.

## E2E Tests

See [`e2e/README.md`](e2e/README.md) for how to run them day to day (`--ui`, `--debug`,
environments, credentials setup). To add a new test, use the `create-e2e-test` skill.

## Dev Proxy (`scripts/dev-proxy.js`)

Sits in front of Next.js at port 5000. Gates browser requests until Next.js returns HTTP 200 on `/login` (warm-up guard). Serves an auto-refreshing loading page during the 30-second cold-compile window. WebSocket passthrough for HMR.

## Key Directories

```
src/
  app/                  Next.js App Router pages
    (platform)/         Route group: authenticated product pages
      careconnect/      CareConnect referrals, appointments, providers
      lien/             SynqLien cases, liens, marketplace, tasks
      fund/             SynqFund applications
      insights/         Reports catalog, viewer, builder, schedules
      tenant/           Tenant administration (users, groups, access)
    api/                BFF route handlers
  components/           Shared UI components (careconnect/, fund/, lien/, shell/)
  lib/                  API clients, service layers, auth guards
    cases/              Cases API service layer (types, api, mapper, service)
    liens/              Liens API service layer + task/workflow/notes layers
    servicing/          Servicing API service layer
    documents/          Documents API service layer
    notifications/      Notifications API service layer
    reports/            Reports API service layer (types, api, service)
    provider-mode/      Org config / sell vs manage mode
    unified-activity/   Merged audit + notification activity feed
    role-access/        buildRoleAccess() — action-level role checks
    bulk-operations/    executeBulk() framework for multi-select operations
  hooks/                useSession, useRoleAccess, useProviderMode, useSelectionState
  providers/            SessionProvider, TenantBrandingProvider
  stores/               Zustand lien store (legacy V1 prototype data)
  types/                Shared TypeScript DTOs
```

## Products & Roles

| Role | Access |
|---|---|
| `CARECONNECT_REFERRER` | Find providers, send referrals, book appointments |
| `CARECONNECT_RECEIVER` | Receive referrals, manage appointments |
| `SYNQFUND_REFERRER` | Submit funding applications |
| `SYNQFUND_FUNDER` | Review and decide on funding applications |
| `SYNQLIEN_SELLER` | Create and manage liens |
| `SYNQLIEN_BUYER` | Browse marketplace, submit offers, purchase liens |
| `SYNQLIEN_HOLDER` | View held portfolio |
| `TenantAdmin` | Tenant user/group/permission management |
| `PlatformAdmin` | All tenants + platform admin |

## Environment

`apps/web/.env.local` (gitignored):
```
NEXT_PUBLIC_ENV=development
NEXT_PUBLIC_TENANT_CODE=LEGALSYNQ
GATEWAY_URL=http://127.0.0.1:5010
```

### CareConnect common portal (AUTH-CC01)

Two additional env vars are required when hosting the CareConnect common portal on a separate hostname (e.g. `careconnect.legalsynq.com`):

| Variable | Example | Purpose |
|---|---|---|
| `CC_COMMON_PORTAL_HOSTNAME` | `careconnect.legalsynq.com` | Hostname the BFF uses to detect a common-portal request and set `resolveByEmail=true`. Must match the hostname the reverse proxy routes to this Next.js instance. |
| `NotificationsService__CareConnectPortalBaseUrl` | `https://careconnect.legalsynq.com` | Identity service config. The base URL used to build password-reset links for CC users. Set in `Identity.Api/appsettings.json` or as an environment override. |

If `CC_COMMON_PORTAL_HOSTNAME` is unset, the CC forgot-password path is silently disabled (a startup warning is logged). See `apps/gateway/README.md` for the required proxy header-stripping rules.

### SynqLien common portal

The SynqLien common portal currently has a static login shell and static funding-company landing dashboard with no auth/API integration. It is selected by the product portal subdomain config:

| Variable | Default | Purpose |
|---|---|---|
| `PORTAL_SYNQLIEN_SUBDOMAIN` | `synqlien-demo` | Subdomain that renders the SynqLien-branded `/login` layout and points future portal redirects at `/lien/dashboard`. |

Until auth integration is added, submitting the SynqLien portal login form only shows an in-page "not connected yet" notice and does not call `/api/auth/login`. The post-login `/lien/dashboard` page renders the Figma-aligned funding-company dashboard with sample values, and `/lien/liens` renders the static Offered Liens table with local search/status filters. Neither page calls dashboard or liens APIs. In full local dev, use `http://synqlien-demo.localhost:5000/login`; when running `next dev` directly, use the Next.js port instead.
