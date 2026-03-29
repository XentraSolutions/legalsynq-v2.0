# Step 10 – Run Control Center

## App Structure Verified

| Item | Status | Notes |
|---|---|---|
| `apps/control-center/package.json` | ✅ Exists | Scripts: `dev -p 5004`, `build`, `start -p 5004`, `type-check` |
| `apps/control-center/next.config.mjs` | ✅ Exists | Gateway rewrite (`/api/:path*` → `http://localhost:5010`) |
| `apps/control-center/tsconfig.json` | ✅ Exists | Strict mode, path alias `@/*` → `./src/*` |
| `apps/control-center/postcss.config.js` | ✅ Exists | Tailwind v4 via `@tailwindcss/postcss` |
| `apps/control-center/src/app/layout.tsx` | ✅ Exists | `RootLayout` with `<html>`, `<body className="antialiased bg-gray-50">` |
| `apps/control-center/src/app/page.tsx` | ✅ Updated | Landing page (see below) |
| Dependencies | ✅ Resolved | Shared from root `node_modules` via monorepo layout |

---

## Scripts Added / Confirmed

### `apps/control-center/package.json`

All scripts confirmed and documented with dependencies:

```json
{
  "scripts": {
    "dev":        "next dev -p 5004",
    "build":      "next build",
    "start":      "next start -p 5004",
    "type-check": "tsc --noEmit"
  },
  "dependencies": {
    "next":      "*",
    "react":     "*",
    "react-dom": "*"
  },
  "devDependencies": {
    "@tailwindcss/postcss": "*",
    "@types/node":          "*",
    "@types/react":         "*",
    "@types/react-dom":     "*",
    "autoprefixer":         "*",
    "tailwindcss":          "*",
    "typescript":           "*"
  }
}
```

`"*"` versions are intentional — packages are resolved from the root `node_modules`; pinning would diverge from root.

### Root `package.json` — new scripts

```json
"dev:web":            "cd apps/web && next dev -p 5000",
"dev:control-center": "cd apps/control-center && next dev -p 5004"
```

---

## `scripts/run-dev.sh` — Control Center Added

The monorepo startup script now launches both Next.js apps in parallel:

```bash
# Start Next.js immediately — port 5000 must open for the preview pane
echo "[web] Starting Next.js on :5000"
(cd "$ROOT/apps/web" && GATEWAY_URL=http://localhost:5010 exec "$NODE" "$ROOT/node_modules/.bin/next" dev -p 5000) &
PID_WEB=$!

# Start Control Center — port 5004
echo "[control-center] Starting Next.js on :5004"
(cd "$ROOT/apps/control-center" && GATEWAY_URL=http://localhost:5010 exec "$NODE" "$ROOT/node_modules/.bin/next" dev -p 5004) &
PID_CC=$!
```

`PID_CC` is included in both `cleanup()` and `wait` so the process is cleanly managed alongside web and .NET services.

**Startup log confirmed:**
```
[web] Starting Next.js on :5000
[control-center] Starting Next.js on :5004
▲ Next.js 14.2.35 (5000) — ✓ Ready in 2.3s
▲ Next.js 14.2.35 (5004) — ✓ Ready in 2.8s
```

---

## Homepage Updated — `apps/control-center/src/app/page.tsx`

The previous homepage was a server-side redirect to `/tenants`.

**Updated to:** A public landing page with no auth guard, confirming the app is running and providing quick navigation.

### "Control Center Running" banner

```tsx
<div className="inline-flex items-center gap-2 px-4 py-1.5 rounded-full bg-green-50 border border-green-200">
  <span className="h-2 w-2 rounded-full bg-green-500" />
  <span className="text-sm font-medium text-green-700">Control Center Running</span>
</div>
```

Green dot + "Control Center Running" text, visible immediately on page load with no authentication required.

### Navigation links

| Label | Route |
|---|---|
| All Tenants | `/tenants` |
| Tenant Users | `/tenant-users` |
| Roles & Permissions | `/roles` |

All linked pages still enforce `requirePlatformAdmin()` — auth guard intact.

### Sign in button

Direct link to `/login` so unauthenticated users have a clear path in.

---

## `README_DEV.md` Created

`apps/control-center/README_DEV.md` — contains:
- How to run (monorepo startup vs. standalone `npm run dev`)
- Expected URL: `http://localhost:5004`
- Page reference table with auth requirements
- Port layout for all services
- Architecture overview

---

## Dependencies

The app relies on the monorepo root `node_modules`. No separate install step is required:

| Package | Source |
|---|---|
| `next@14.2.35` | Root `node_modules` |
| `react@19.2.4` | Root `node_modules` |
| `react-dom@19.2.4` | Root `node_modules` |
| `tailwindcss@4.2.2` | Root `node_modules` |
| `typescript@6.0.2` | Root `node_modules` |
| `@types/react` | Root `node_modules` |

---

## How to Access in Browser

### In Replit

1. The workflow `Start application` starts both apps automatically.
2. In the preview pane, click the port selector and choose **port 5004**.
3. The landing page loads with the "Control Center Running" green badge.
4. Click "Sign in to Control Center" → `/login` to authenticate.

### Directly

```
http://localhost:5004       ← Landing page (public, no auth)
http://localhost:5004/login ← Sign in
```

### Admin credentials (dev)

```
Email:    admin@legalsynq.com
Password: Admin1234!
Tenant:   LEGALSYNQ
```

---

## Port Layout

| Service | Port |
|---|---|
| `apps/web` — tenant portal | 5000 |
| Identity.Api | 5001 |
| Fund.Api | 5002 |
| CareConnect.Api | 5003 |
| `apps/control-center` | **5004** |
| Gateway.Api | 5010 |

---

## TypeScript

Zero errors confirmed (`tsc --noEmit` clean across all `apps/control-center` files).

---

## Issues / Notes

1. **Port deviation from spec** — The spec suggested port `3001`. Port `5004` is used throughout the project for the Control Center (established across scratchpad, nav, documentation, and the startup script). `3001` was not adopted.

2. **Homepage was redirect, now landing page** — The previous `page.tsx` did `redirect('/tenants')`. This has been replaced with a public landing page so the "Control Center Running" banner is visible without authentication. All protected pages (tenants, users, roles) still require auth.

3. **Hydration warning in `apps/web` login form** — A pre-existing hydration warning appears in the browser console on the web app (port 5000) login page. This is unrelated to the Control Center and was present before this step.

4. **Dependencies declared with `"*"` versions** — Packages are resolved from root `node_modules`. Pinned versions in CC `package.json` would duplicate and potentially conflict with root. The `"*"` pattern is intentional for monorepo layout.
