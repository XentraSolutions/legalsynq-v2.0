# Step 4: CareConnect Provider Map — Implementation Report

## 1. Files Created

| File | Purpose |
|------|---------|
| `apps/web/src/components/careconnect/provider-map.tsx` | Leaflet map with `CircleMarker` rendering, popups, viewport tracking via `MapEventTracker`, and provider selection |
| `apps/web/src/components/careconnect/provider-map-shell.tsx` | Client-side orchestration: view toggle, marker fetch, URL-sync, list/map rendering |

## 2. Files Modified

| File | Change |
|------|--------|
| `apps/web/src/types/careconnect.ts` | Added `ProviderMarker` interface; added geo fields (`latitude`, `longitude`, `radiusMiles`, `northLat`, `southLat`, `eastLng`, `westLng`) to `ProviderSearchParams` |
| `apps/web/src/lib/careconnect-api.ts` | Added `providers.getMarkers()` to both `careConnectServerApi` (server) and `careConnectApi` (client); added `ProviderMarker` to imports |
| `apps/web/src/app/(platform)/careconnect/providers/page.tsx` | Added geo URL param extraction; replaced direct list rendering with `ProviderMapShell`; preserved server-side list fetch |
| `apps/web/src/components/careconnect/provider-search-filters.tsx` | Added "Use my location" geolocation button, radius input with live URL sync, and "Clear location" control |
| `package.json` (root) | Installed `react-leaflet@^4`, `leaflet@^1`, `@types/leaflet` |

## 3. Key Implementation Details

### Architecture

- **URL is the single source of truth** for all filter and view state. Both the server component (providers page) and all client components read exclusively from URL params.
- **Server Component** (`providers/page.tsx`) continues to fetch the initial provider list for the list view — no extra round-trip on page load.
- **Client Component** (`provider-map-shell.tsx`) fetches map markers on mount/param-change in map mode via the BFF proxy (`/api/careconnect/api/providers/map`).

### Leaflet Integration

- `provider-map.tsx` is **never SSR'd** — imported via `dynamic(() => ..., { ssr: false })` in the shell. This avoids the `window is not defined` crash Leaflet causes in Node.js.
- Uses `CircleMarker` (SVG, no external images) instead of the default `Marker` icon, which requires Next.js to resolve PNG assets that fail in App Router.
- Leaflet CSS imported with `import 'leaflet/dist/leaflet.css'` at the top of `provider-map.tsx`. Next.js bundles it alongside the dynamically loaded chunk.
- `MapEventTracker` inner component uses `useMap()` + `useMapEvents()` (react-leaflet hooks) to debounce `moveend`/`zoomend` events (350ms) before updating viewport URL params.

### Viewport → URL Flow

Map pan/zoom fires a debounced callback:
```
moveend/zoomend → 350ms debounce → handleViewportChange →
  params.set(nLat/sLat/eLng/wLng) → router.replace → Next.js re-render →
  shell effect triggers → getMarkers(bounds) → new markers
```
Viewport params (`nLat/sLat/eLng/wLng`) and radius params (`lat/lng/radius`) are **mutually exclusive** — switching between them clears the other set.

### Geolocation Flow

"Use my location" button:
1. Calls `navigator.geolocation.getCurrentPosition()`
2. Writes `lat`, `lng`, `radius` to URL and switches to `view=map`
3. Map centers on the coordinate (zoom 11) and fetches markers within the radius

Radius is editable live — changes immediately update the `radius` URL param and trigger a new marker fetch.

### Server/Client Import Boundary Fix

`careconnect-api.ts` exports both `careConnectServerApi` (depends on `next/headers`) and `careConnectApi`. When imported in a Client Component, webpack tries to bundle `next/headers` and fails.

**Fix**: `provider-map-shell.tsx` imports `apiClient` directly from `@/lib/api-client` (no server-only code) and builds the query string with `URLSearchParams` inline. All other client components (`create-referral-form.tsx`, `booking-panel.tsx`) follow the same pattern as an existing convention.

### Provider Selection

Clicking a `CircleMarker` sets `selectedMarkerId` in shell state, which re-renders the map with a larger, blue-ringed circle. The popup contains:
- Provider name and subtitle
- Accepting/not accepting referrals badge
- "View Provider →" link
- "Create Referral →" link (shown only to referrers when provider accepts referrals)

Both links navigate to `/careconnect/providers/{id}` (the existing provider detail page where referral creation lives).

## 4. Blockers and Assumptions

### Assumptions

- **Backend `/api/providers/map` is already implemented** and returns `ProviderMarker[]` with `latitude`, `longitude`, `displayLabel`, `markerSubtitle`, `acceptingReferrals`. Confirmed from prior analysis.
- **All providers in the map endpoint have valid lat/lng**. The backend `ProviderGeoHelper` filters to only providers with geocoded locations.
- `isReferrer` is hardcoded to `true` in the providers page. The full session check (`session.productRoles.includes(ProductRole.CareConnectReferrer)`) already guards the page — all users who reach it are referrers.

### Known Limitations (pre-existing, not introduced here)

1. **Missing availability endpoint** — `GET /providers/{id}/availability` returns 404. The booking flow on the provider detail page is already broken before this work.
2. **StandardUser 403 on referral creation** — a policy gap in the CareConnect service blocks `StandardUser` role. Tracked in `step3_frontend_integration.md`.
3. **`AppointmentResponse` field mismatches** — DTO field names differ from what the frontend expects. Pre-existing issue.

### No backend changes were made.
