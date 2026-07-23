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

### SynqLien funding common portal

The SynqLien funding-company common portal uses the same Identity-backed `platform_session` cookie as CareConnect common portal login, but it serves buyer-side SynqLien users from `/funding/*`.

| Variable | Example / Default | Purpose |
|---|---|---|
| `SYNQLIEN_COMMON_PORTAL_HOSTNAME` | `synqlien-demo.localhost` | Hostname the BFF uses to detect SynqLien common-portal login and send `resolveByEmail=true` with `portalProductCode=SYNQ_LIENS`. Root `/` redirects to `/funding/dashboard`. |
| `PORTAL_SYNQLIEN_SUBDOMAIN` | `synqlien-demo` | Subdomain that renders the SynqLien-branded `/login` layout and defaults successful login to `/funding/dashboard`. |

Use the same hostname as the Liens buyer-offer email CTA:

```bash
SYNQLIEN_COMMON_PORTAL_HOSTNAME=synqlien-demo.localhost
PORTAL_SYNQLIEN_SUBDOMAIN=synqlien-demo
```

Eligibility is enforced in Identity and again in the web route layout: users must have SynqLien product access and `SYNQ_LIENS:SYNQLIEN_BUYER`, may also have `SYNQ_LIENS:SYNQLIEN_HOLDER`, and must not have platform/tenant system roles or `SYNQ_LIENS:SYNQLIEN_SELLER`.

Implemented routes:

| Route | Purpose |
|---|---|
| `/funding/dashboard` | Funding dashboard with KPI summary, pending offers, acquisition pipeline, provider performance, and Offer Inbox. |
| `/funding/offered-liens` | Server-rendered offered-liens list with search, status filters, pagination, and API-authorized row actions. |
| `/selling/public/{token}` | Public, token-gated buyer offer page opened from `New Lien Offer` emails; rendered by `apps/web` from Liens JSON without a `platform_session` cookie. Includes accept/decline buttons that record the buyer response without finalizing sale. |
| `/api/lien/api/liens/selling/public/{token}` | Public BFF compatibility path for the Liens JSON data endpoint; kept for direct API callers and older integrations. Response actions post through `/api/lien/api/liens/selling/public/{token}/accept` and `/api/lien/api/liens/selling/public/{token}/decline`, with a public fallback through `/api/liens/api/liens/selling/public/{token}/{action}` for local/dev gateway rewrites. |

The frontend is API-ready but does not include mock rows. Server components target the future Liens endpoints through the gateway:

| Frontend server request | Liens service endpoint after gateway prefix removal |
|---|---|
| `/liens/api/liens/selling/buyer/dashboard?range=last7Days\|last30Days\|custom&from=&to=` | `/api/liens/selling/buyer/dashboard` |
| `/liens/api/liens/selling/buyer/liens?status=&search=&page=&pageSize=` | `/api/liens/selling/buyer/liens` |

Until those backend endpoints exist, the funding portal converts only `404`, `501`, and `204` responses into semantic empty states. `401`, `403`, and `5xx` remain auth/error states.
