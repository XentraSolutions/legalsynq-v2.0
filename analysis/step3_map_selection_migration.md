# Step 3 — CareConnect Map & Provider Selection Migration Plan

**Date:** 2026-03-29  
**Scope:** Map display, provider marker rendering, viewport/radius search, provider selection, referral launch  
**Author:** Agent analysis

> **Note on legacy code:** The paths `/mnt/data/inspect_old_frontend` and `/mnt/data/inspect_old_backend` are not mounted in this Replit environment. Legacy feature behavior is reconstructed from: (a) the feature description in the task brief, (b) the new backend's existing geo infrastructure (which was clearly designed to support a legacy map pattern), and (c) standard patterns for Leaflet-based React provider maps. All new-codebase analysis is based on direct file inspection.

---

## 1. Executive Summary

The new LegalSynq backend is **already fully capable** of powering a map-based provider discovery experience. It has:
- A dedicated `/api/providers/map` endpoint returning up to 500 lightweight `ProviderMarkerResponse` objects with lat/lng
- A dual-mode geo search in `GetProvidersQuery`: radius search (lat/lng/radiusMiles) **and** viewport search (northLat/southLat/eastLng/westLng), mutually exclusive
- `ProviderGeoHelper` with bounding-box logic, 100-mile radius cap, 500-marker hard limit

The **entire gap is on the frontend**. The current `providers/page.tsx` is a list-only server-rendered page with no map, no geo search, no viewport tracking, and no list/map toggle. The `ProviderSearchFilters` component has no radius or geolocation controls. `careconnect-api.ts` does not expose the `/map` endpoint or geo params. `types/careconnect.ts` has no `ProviderMarker` type or geo search fields.

**What must be built:** one new `ProviderMap` client component (Leaflet), one `ProviderMapShell` orchestration component, geo param additions to the types and API client, filter additions to `ProviderSearchFilters`, and a refactored `providers/page.tsx` that supports both list and map views with URL-synced state.

**Estimated new files:** 3–4  
**Estimated modified files:** 4  
**Backend changes required:** 0

---

## 2. Files Inspected

### New codebase (all inspected directly)

| File | Key findings |
|------|-------------|
| `CareConnect.Api/Endpoints/ProviderEndpoints.cs` | `GET /api/providers` (list, paged), `GET /api/providers/map` (markers, up to 500), `GET /api/providers/{id}` (detail). Both list and map endpoints accept same `ProviderSearchParams` including all geo fields. |
| `CareConnect.Application/DTOs/ProviderMarkerResponse.cs` | Full marker DTO: `id, name, organizationName, displayLabel, markerSubtitle, city, state, addressLine1, postalCode, email, phone, acceptingReferrals, isActive, latitude, longitude (non-nullable doubles), geoPointSource, primaryCategory, categories[]` |
| `CareConnect.Application/DTOs/GetProvidersQuery.cs` | Accepts `name, categoryCode, city, state, acceptingReferrals, isActive, page, pageSize` + geo: `latitude, longitude, radiusMiles` (radius mode) OR `northLat, southLat, eastLng, westLng` (viewport mode) |
| `CareConnect.Application/Services/ProviderService.cs` | `GetMarkersAsync` calls `_providers.GetMarkersAsync` → filters WHERE lat/lng IS NOT NULL → returns `ProviderMarkerResponse` list. `SearchAsync` applies same geo filters for list mode. |
| `CareConnect.Infrastructure/Repositories/ProviderRepository.cs` | `GetMarkersAsync` filters `Latitude != null && Longitude != null`, applies bounding-box or viewport filter, takes up to `MarkerLimit` (500) ordered by name. `BuildBaseQuery` applies all filters including geo. |
| `CareConnect.Application/Helpers/ProviderGeoHelper.cs` | `BoundingBox(lat, lon, miles)` → approximation using 69mi/deg. `MarkerLimit = 500`. Radius cap = 100 miles. Validates: no mixing of radius + viewport modes. |
| `apps/web/src/lib/careconnect-api.ts` | `careConnectServerApi.providers.search()`, `.getById()`, `.getAvailability()`. `careConnectApi` (client) same methods. **Missing:** `providers.getMarkers()` on both server and client APIs. |
| `apps/web/src/types/careconnect.ts` | `ProviderSummary`, `ProviderDetail`, `ProviderSearchParams`. **Missing:** `ProviderMarker` type, geo fields in `ProviderSearchParams`. |
| `apps/web/src/components/careconnect/provider-search-filters.tsx` | Filters: name, city, state, categoryCode, acceptingReferrals checkbox. URL-synced. **Missing:** radius input, geolocation button, radiusMiles param. |
| `apps/web/src/components/careconnect/provider-card.tsx` | Link card for list view. Has `displayLabel`, `markerSubtitle`, categories, contact. **No map awareness.** |
| `apps/web/src/components/careconnect/create-referral-form.tsx` | Modal form. Accepts `providerId` + `providerName` as props. Fully works from any entry point. Ready to use from map selection. |
| `apps/web/src/app/(platform)/careconnect/providers/page.tsx` | Server Component. List-only. Reads `name, city, state, categoryCode, acceptingReferrals, page` from `searchParams`. **No `view` param, no geo params.** |
| `apps/web/src/app/(platform)/careconnect/providers/[id]/page.tsx` | Client Component. Fetches provider detail by ID. Renders `ProviderDetailCard` + `CreateReferralForm` modal. Works fine as the deep-link target from map marker clicks. |

### Gateway (inspected)

`apps/gateway/Gateway.Api/appsettings.json`: `careconnect-protected` route strips `/careconnect` prefix. All `/careconnect/api/*` routes reach CareConnect.Api correctly. No gateway changes needed.

---

## 3. Legacy Feature Inventory (Reconstructed)

Since the legacy source files are not mounted, this section reconstructs the feature set from the task description and the design artifacts already present in the new backend (which was clearly built to mirror a legacy pattern).

### A. Legacy ProviderMap component (src/components/ProviderMap.tsx — inferred)

| Feature | Behavior |
|---------|---------|
| Library | Leaflet (`react-leaflet`) |
| Data source | API call for markers (lightweight DTO) |
| Markers | Circle or custom pin per provider; color coded by `acceptingReferrals` |
| Marker click | Opens a popup / side panel with provider summary |
| Selected provider | Highlights marker; shows mini-card in panel |
| Viewport tracking | On map move/zoom end → fires new marker request with bounds |
| Geolocation button | Browser `navigator.geolocation` → re-centers map → re-fetches within radius |

### B. Legacy providers list page (src/pages/providers.tsx — inferred)

| Feature | Behavior |
|---------|---------|
| List/map toggle | Tab or button to switch between list grid and `<ProviderMap>` |
| Filter bar | Name, category, city/state, accepting referrals, radius slider |
| Shared filter state | Filters apply to both list and map views |
| Provider selection | Click card in list → navigate to detail; click marker in map → open side panel |
| Pagination | List only; map shows all markers up to limit |

### C. Legacy provider detail / new-referral pages

| Feature | Behavior |
|---------|---------|
| Deep link from map | Provider detail page receives provider ID from URL |
| Referral launch | "Create Referral" button on detail page opens form |
| Old field names | `clientName` (single string), `address` + `zip` (not split), numeric IDs |

### D. Key legacy → new field renames

| Legacy field | New field | Notes |
|-------------|-----------|-------|
| `clientName` | `clientFirstName` + `clientLastName` | Split in new backend |
| `address` | `addressLine1` | More explicit |
| `zip` | `postalCode` | Standard name |
| `categoryId` (integer) | `categoryCode` (string) | e.g. `"PT"` instead of `42` |
| Numeric int IDs | GUID strings | All IDs are now `Guid` |
| `lat` / `lng` | `latitude` / `longitude` | Full names |
| `isOpen` / `accepting` | `acceptingReferrals` | Boolean |
| Single `name` field | `displayLabel` (org ?? name) + `name` | Pre-computed in service |

---

## 4. New Capability Inventory

### Backend — what is fully ready

| Capability | Endpoint / class | Notes |
|-----------|-----------------|-------|
| Provider list search | `GET /api/providers` | Paged, name/city/state/category/accepting filters |
| Provider map markers | `GET /api/providers/map` | Up to 500 markers, only providers with lat/lng |
| Radius geo search | `latitude + longitude + radiusMiles` query params | Bounding box approx, 100-mile cap |
| Viewport geo search | `northLat + southLat + eastLng + westLng` query params | Exact 4-bound filter |
| Provider detail | `GET /api/providers/{id}` | Full `ProviderResponse` |
| Referral creation | `POST /api/referrals` | Accepts `providerId, clientFirstName, clientLastName, ...` |
| Gateway routing | YARP strips `/careconnect` prefix | No changes needed |

### Frontend — what exists

| Capability | File | Status |
|-----------|------|--------|
| Provider list rendering | `providers/page.tsx` + `ProviderCard` | ✅ Working |
| Filter bar (text-based) | `ProviderSearchFilters` | ✅ Working, needs geo additions |
| Provider detail page | `providers/[id]/page.tsx` | ✅ Working |
| Referral creation modal | `CreateReferralForm` | ✅ Working |
| BFF proxy | `/api/careconnect/[...path]` | ✅ Working |
| API client wrappers | `careconnect-api.ts` | ⚠️ Missing markers + geo params |
| TypeScript types | `types/careconnect.ts` | ⚠️ Missing `ProviderMarker` + geo search |
| Map component | — | ❌ Does not exist |
| List/map toggle | — | ❌ Does not exist |
| Radius search control | — | ❌ Does not exist |
| Geolocation button | — | ❌ Does not exist |
| Viewport-driven fetch | — | ❌ Does not exist |

---

## 5. Migration Mapping Table

| Legacy feature | Old file | New destination | Action | Reason |
|---------------|---------|----------------|--------|--------|
| Leaflet map render | `ProviderMap.tsx` | `components/careconnect/provider-map.tsx` | **Create** (adapt from Leaflet pattern) | New component needed; backend already supports it |
| Marker data fetch | `ProviderMap.tsx` (inline) | `careconnect-api.ts` → `providers.getMarkers()` | **Add** | API method missing |
| Marker DTO | Legacy `ProviderMapMarker` | `types/careconnect.ts` → `ProviderMarker` | **Create** | Type missing; shape from `ProviderMarkerResponse` |
| Accepting-referrals marker color | `ProviderMap.tsx` | `provider-map.tsx` | **Adapt** | Field name same, boolean |
| Marker popup | `ProviderMap.tsx` | `provider-map.tsx` (inline popup or mini-card) | **Create** | New; shows displayLabel + markerSubtitle + CTA |
| List/map toggle | `providers.tsx` | `components/careconnect/provider-map-shell.tsx` | **Create** | Shell manages view mode + shared filter state |
| Viewport-driven fetch | `ProviderMap.tsx` (map moveend) | `provider-map.tsx` | **Adapt** | Backend already accepts `northLat/southLat/eastLng/westLng` |
| Geolocation button | `ProviderMap.tsx` | `provider-map.tsx` | **Create** | Browser geo API → radius search |
| Radius filter | `ProviderSearchFilters` (legacy) | `provider-search-filters.tsx` | **Add** | Add radius input + geo mode toggle |
| Category filter | `providers.tsx` filter bar | `provider-search-filters.tsx` | ✅ Exists | `categoryCode` already in filters |
| Accepting-referrals filter | `providers.tsx` | `provider-search-filters.tsx` | ✅ Exists | Checkbox already there |
| Provider selection → detail | `providers.tsx` marker click | `provider-map.tsx` → `router.push()` | **Create** | Deep-link to existing `/providers/[id]` page |
| Referral from provider | `new-referral.tsx` | `providers/[id]/page.tsx` → `CreateReferralForm` | ✅ Exists | Referral form already there; map just navigates to detail |
| `clientName` → split | `new-referral.tsx` | `create-referral-form.tsx` | ✅ Migrated | Form already uses `clientFirstName`/`clientLastName` |
| Numeric IDs | Throughout legacy | All GUIDs in new types | ✅ Migrated | No action needed |
| `address` + `zip` | Legacy DTOs | `addressLine1` + `postalCode` | ✅ Migrated | Already in `ProviderMarkerResponse` |
| Category code filter | `categoryId` (int) | `categoryCode` (string) | ✅ Migrated | Backend uses code not ID |
| Geolocation lat/lng params | Legacy URL params | URL params `lat, lng, radius` | **Create** | Needs URL sync |
| Viewport params | Legacy state | URL params `nLat, sLat, eLng, wLng` | **Create** | Needs URL sync |
| Server-side marker fetch | Backend controller | `GET /api/providers/map` | ✅ Exists | Same pattern |

---

## 6. Map Display and Selection Design

### 6.1 Map library choice

**Use Leaflet via `react-leaflet`** (v4.x, compatible with React 18).

Rationale:
- The backend was built around the same Leaflet patterns from the legacy app (viewport bounds, marker limit of 500)
- `react-leaflet` v4 is a stable, well-tested React 18 wrapper
- No server-side rendering issues — Leaflet must be dynamically imported (`next/dynamic` with `ssr: false`)
- Alternative (Mapbox/Google) would require API keys and cost; Leaflet uses free OpenStreetMap tiles
- Alternative (MapLibre) is an option but adds complexity with no benefit here

### 6.2 Component location

```
apps/web/src/
  components/careconnect/
    provider-map.tsx          ← Leaflet map + markers + popup (Client Component)
    provider-map-shell.tsx    ← List/Map toggle, shared filter state, view orchestration
    provider-search-filters.tsx  ← MODIFIED: add radius + geolocation
  app/(platform)/careconnect/providers/
    page.tsx                  ← MODIFIED: delegate to ProviderMapShell
```

### 6.3 Server vs client split

| Component | Type | Why |
|-----------|------|-----|
| `providers/page.tsx` | Server → **convert to hybrid** | Needs to pass initial list data + read geo URL params |
| `ProviderMapShell` | Client Component | Manages view toggle state; receives initial data as props |
| `ProviderMap` | Client Component (dynamic) | Leaflet must not run on server |
| `ProviderSearchFilters` | Client Component (already) | URL sync, geolocation API |
| `ProviderCard` | Server Component (already) | Static, no interaction |

**Pattern:** `page.tsx` (Server Component) fetches initial list data from the backend and passes it as a prop to `ProviderMapShell`. The shell renders either the list (using the prop data) or the map (which fetches its own markers on mount/viewport change via client-side API). URL params drive both.

### 6.4 URL / query parameter strategy

All filter + view state lives in the URL so it survives refresh and supports sharing:

| URL param | Type | Description |
|-----------|------|-------------|
| `name` | string | Name filter |
| `city` | string | City filter |
| `state` | string | 2-char state filter |
| `categoryCode` | string | Category code filter |
| `acceptingReferrals` | `"true"` | Accepting-only toggle |
| `page` | number | List pagination |
| `view` | `"list"` \| `"map"` | View mode (default: `"list"`) |
| `lat` | number | Center latitude for radius search |
| `lng` | number | Center longitude for radius search |
| `radius` | number | Radius in miles (1–100) |
| `nLat`, `sLat`, `eLng`, `wLng` | number | Viewport bounds (set by map moveend) |

Radius params (`lat/lng/radius`) and viewport params (`nLat/sLat/eLng/wLng`) are mutually exclusive in the URL — only one set should be present at a time.

### 6.5 View toggle behavior

```
[List] [Map]  ← buttons in page header, read/write `view` param

List mode:
  → server-rendered page with ProviderCard grid
  → pagination
  → filters apply to server fetch

Map mode:
  → ProviderMap renders Leaflet map
  → On mount: fetch markers using current filters + viewport bounds
  → On moveend/zoomend: update nLat/sLat/eLng/wLng URL params → refetch markers
  → Filter changes: reset viewport params → refetch markers
```

### 6.6 Geolocation button

```
Button in ProviderSearchFilters:
  onClick → navigator.geolocation.getCurrentPosition(
    pos => setRadius params in URL: lat=..., lng=..., radius=25
         → navigate → triggers marker refetch
  )
```

### 6.7 Marker click → provider selection flow

```
Marker click
  → set selectedProviderId in ProviderMap state (not URL)
  → show popup with:
      displayLabel (bold)
      markerSubtitle (city, state · category)
      acceptingReferrals badge
      "View Provider" button → router.push(`/careconnect/providers/${id}`)
      "Create Referral" button (if isReferrer) → router.push(`/careconnect/providers/${id}`)
```

Deep navigation to `/careconnect/providers/{id}` is the single entry point for referral creation. The map does not open the referral form itself — it navigates to the existing detail page where the form lives. This keeps the referral flow consistent whether the user came from list or map.

### 6.8 Map-to-detail → referral flow

```
User: Clicks marker → sees popup → clicks "Create Referral"
  → router.push('/careconnect/providers/{id}')
  → Provider detail page renders with ProviderDetailCard + "Create Referral" button
  → User clicks "Create Referral" → CreateReferralForm modal opens (already exists)
  → User submits → navigated to /careconnect/referrals/{id}
```

No additional plumbing needed. The existing `providers/[id]/page.tsx` + `CreateReferralForm` handle this entirely.

### 6.9 Marker rendering specifics

Each marker:
- **Position:** `[latitude, longitude]` from `ProviderMarkerResponse`
- **Color:**
  - Green (`#16a34a`) for `acceptingReferrals: true`
  - Gray (`#6b7280`) for `acceptingReferrals: false`
- **Icon:** Leaflet `divIcon` with CSS circle (no image dependency, no SSR issues)
- **Selected state:** larger border/ring on selected marker
- **Popup content:** `displayLabel`, `markerSubtitle`, accepting badge, "View Provider" link

---

## 7. API and DTO Alignment

### 7.1 Missing API methods

`careconnect-api.ts` currently has NO method for the markers endpoint and NO geo params in search.

**Required additions:**

```typescript
// In careConnectServerApi.providers (server side):
getMarkers: (params: ProviderSearchParams = {}) =>
  serverApi.get<ProviderMarker[]>(
    `/careconnect/api/providers/map${toQs(params as Record<string, unknown>)}`,
  ),

// In careConnectApi.providers (client side):
getMarkers: (params: ProviderSearchParams = {}) =>
  apiClient.get<ProviderMarker[]>(
    `/careconnect/api/providers/map${toQs(params as Record<string, unknown>)}`,
  ),
```

### 7.2 Missing type: ProviderMarker

Add to `types/careconnect.ts`:

```typescript
export interface ProviderMarker {
  id:                string;
  name:              string;
  organizationName?: string;
  displayLabel:      string;
  markerSubtitle:    string;
  city:              string;
  state:             string;
  addressLine1:      string;
  postalCode:        string;
  email:             string;
  phone:             string;
  acceptingReferrals: boolean;
  isActive:          boolean;
  latitude:          number;   // always present (backend filters null)
  longitude:         number;   // always present
  geoPointSource?:   string;
  primaryCategory?:  string;
  categories:        string[];
}
```

### 7.3 Missing geo fields in ProviderSearchParams

```typescript
// Add to existing ProviderSearchParams:
export interface ProviderSearchParams {
  // existing:
  name?:               string;
  categoryCode?:       string;
  city?:               string;
  state?:              string;
  acceptingReferrals?: boolean;
  isActive?:           boolean;
  page?:               number;
  pageSize?:           number;
  // NEW:
  latitude?:           number;
  longitude?:          number;
  radiusMiles?:        number;
  northLat?:           number;
  southLat?:           number;
  eastLng?:            number;
  westLng?:            number;
}
```

### 7.4 ProviderSummary vs ProviderMarkerResponse alignment

| ProviderSummary (frontend) | ProviderMarkerResponse (backend JSON) | Match? |
|---------------------------|--------------------------------------|--------|
| `id` | `id` | ✅ |
| `name` | `name` | ✅ |
| `organizationName?` | `organizationName?` | ✅ |
| `displayLabel` | `displayLabel` | ✅ |
| `markerSubtitle` | `markerSubtitle` | ✅ |
| `city` | `city` | ✅ |
| `state` | `state` | ✅ |
| `postalCode` | `postalCode` | ✅ |
| `email` | `email` | ✅ |
| `phone` | `phone` | ✅ |
| `isActive` | `isActive` | ✅ |
| `acceptingReferrals` | `acceptingReferrals` | ✅ |
| `categories` | `categories` | ✅ |
| `primaryCategory?` | `primaryCategory?` | ✅ |
| `hasGeoLocation` | *(not in marker response — not needed)* | n/a |
| `latitude?` | `latitude` (non-nullable!) | ✅ (marker always has coords) |
| `longitude?` | `longitude` (non-nullable!) | ✅ |

The `ProviderMarker` type intentionally has non-nullable `latitude`/`longitude` because the backend marker endpoint filters to only providers with coordinates. `ProviderSummary` keeps them optional for the list endpoint which may include providers without geo.

### 7.5 CreateReferralRequest — old vs new

| Legacy field | New field | Notes |
|-------------|-----------|-------|
| `clientName: string` | `clientFirstName + clientLastName` | Split — form already handles this |
| `categoryId: number` | `categoryCode: string` | Category filter param — `ProviderSearchFilters` uses `categoryCode` correctly |
| `providerId: number` | `providerId: string` (GUID) | Form gets GUID from `ProviderMarker.id` — no issue |
| `address`, `city`, `zip` | Not in referral DTO (in Provider record) | Referral only needs `providerId` |

No changes needed to `CreateReferralRequest` or `CreateReferralForm` for the map migration.

---

## 8. Required File Changes

### CREATE: `apps/web/src/components/careconnect/provider-map.tsx`

**Status:** Create new  
**Type:** Client Component (`'use client'`)  
**Dependencies:** `react-leaflet`, `leaflet` (npm install)  
**Risk:** Medium — Leaflet SSR must be handled via `next/dynamic` wrapper

**What it does:**
- Renders a `MapContainer` (Leaflet) with OpenStreetMap tiles
- Accepts `markers: ProviderMarker[]`, `selectedId: string | null`, `onSelect: (id: string) => void`, `onViewportChange: (bounds: ViewportBounds) => void`, `isReferrer: boolean` as props
- Renders one `CircleMarker` per provider, green or gray by `acceptingReferrals`
- On marker click: calls `onSelect(marker.id)` + shows `Popup` with mini-card
- Popup has "View Provider" button (always) and "Create Referral" button (if `isReferrer`)
- On `moveend` / `zoomend`: calls `onViewportChange({ northLat, southLat, eastLng, westLng })` with `map.getBounds()`
- Does NOT fetch its own data — parent passes markers as props

### CREATE: `apps/web/src/components/careconnect/provider-map-shell.tsx`

**Status:** Create new  
**Type:** Client Component (`'use client'`)  
**Dependencies:** `react-leaflet`, `next/dynamic`, `next/navigation`  
**Risk:** Low — orchestration component

**What it does:**
- Owns `view: 'list' | 'map'` state (read from URL `view` param, written back on toggle)
- Owns `markers: ProviderMarker[]` state (fetched client-side for map mode)
- Accepts `initialProviders: PagedResponse<ProviderSummary>` as a prop (from server component)
- In list mode: renders the `initialProviders` data using `ProviderCard` grid (no additional fetch)
- In map mode: fetches markers via `careConnectApi.providers.getMarkers(filters)` on mount + on URL param changes
- Dynamically imports `ProviderMap` (SSR disabled)
- Renders view toggle buttons in the header
- Renders `ProviderSearchFilters` (shared between both modes)
- Handles `onViewportChange` from map → updates URL params `nLat/sLat/eLng/wLng`
- Handles `onSelect` from map → `router.push('/careconnect/providers/{id}')`

### MODIFY: `apps/web/src/app/(platform)/careconnect/providers/page.tsx`

**Status:** Modify  
**Change type:** Moderate refactor  
**Risk:** Low — additive

**Changes:**
- Add `view`, `lat`, `lng`, `radius`, `nLat`, `sLat`, `eLng`, `wLng` to `searchParams` type
- Keep server-side list fetch for initial data (only used in list mode but always fetched server-side for SSR)
- Pass `initialProviders` + all search params to `<ProviderMapShell>` instead of rendering list directly
- `ProviderMapShell` becomes the single rendering entry point

### MODIFY: `apps/web/src/lib/careconnect-api.ts`

**Status:** Modify  
**Change type:** Additive — no breaking changes  
**Risk:** None

**Add:**
```typescript
// Both careConnectServerApi.providers and careConnectApi.providers:
getMarkers: (params: ProviderSearchParams = {}) =>
  serverApi.get<ProviderMarker[]>(`/careconnect/api/providers/map${toQs(...)}`),
```
Import `ProviderMarker` from types.

### MODIFY: `apps/web/src/types/careconnect.ts`

**Status:** Modify  
**Change type:** Additive — no breaking changes  
**Risk:** None

**Add:**
- `ProviderMarker` interface (as specified in section 7.2)
- Geo fields to `ProviderSearchParams` (as specified in section 7.3)

### MODIFY: `apps/web/src/components/careconnect/provider-search-filters.tsx`

**Status:** Modify  
**Change type:** Moderate — adds new controls  
**Risk:** Low

**Add:**
- "My location" geolocation button (calls `navigator.geolocation.getCurrentPosition`)
- Radius input (1–100 miles, default 25) — only shown when location is active
- When location set: write `lat={lat}&lng={lng}&radius={miles}` to URL, clear `nLat/sLat/eLng/wLng`
- When location cleared: remove `lat/lng/radius` from URL
- Existing name/city/state/category/accepting filters unchanged

---

## 9. Recommended Implementation Order

### Step 1: Types and API client (no UI impact, zero risk)
1. Add `ProviderMarker` to `types/careconnect.ts`
2. Add geo fields to `ProviderSearchParams`
3. Add `getMarkers()` to both `careConnectServerApi` and `careConnectApi` in `careconnect-api.ts`

**Test:** `curl http://localhost:5010/careconnect/api/providers/map -H "Authorization: Bearer <token>"` returns marker array.

### Step 2: Install Leaflet packages
```bash
npm install react-leaflet leaflet --legacy-peer-deps
npm install -D @types/leaflet --legacy-peer-deps
```
Verify no peer dep conflicts with React 18.

### Step 3: Build ProviderMap component
Build `provider-map.tsx` in isolation. Props-driven, no internal fetching.
Test by temporarily rendering it in the providers page with hardcoded dummy markers.

### Step 4: Build ProviderMapShell component
Wire the shell: view toggle, marker fetch, viewport → URL sync, list → props from server.

### Step 5: Refactor providers page
Convert `page.tsx` to pass data to `ProviderMapShell` instead of rendering directly. Add geo URL params to `searchParams` type.

### Step 6: Extend ProviderSearchFilters
Add geolocation button + radius input. Connect to URL params.

### Step 7: Validate end-to-end flows
- List mode with text filters: confirm server re-render
- Map mode default: confirm markers fetch and render
- Viewport pan: confirm URL updates + new markers fetch
- Geolocation: confirm radius search
- Marker click: confirm navigation to `/providers/{id}`
- "Create Referral" from popup: confirm navigation + form opens

---

## 10. Risks and Assumptions

| Risk | Severity | Mitigation |
|------|----------|-----------|
| No providers have lat/lng data seeded | High | Marker endpoint returns empty array silently — map shows blank. Seed script must populate lat/lng for at least one provider. |
| Leaflet CSS must be imported globally | Medium | Add `import 'leaflet/dist/leaflet.css'` to the Leaflet dynamic import wrapper or the layout. Without it, map tiles render but controls are unstyled. |
| Leaflet default marker icon images 404 in Next.js | Medium | Use `divIcon` (CSS-only markers) instead of image-based markers. Avoids the classic Next.js Leaflet image path bug entirely. |
| React 18 strict mode + Leaflet | Low | `react-leaflet` v4 is React 18 compatible. Strict mode double-mount may cause minor map flicker but no crash. |
| Viewport params and radius params conflict | Medium | Backend returns 400 if both are sent. Frontend must ensure only one set is active at a time. Enforced in `ProviderMapShell` by clearing conflicting params on mode change. |
| `next/dynamic` with SSR false in Server Component context | Medium | `ProviderMap` must be `next/dynamic`-imported inside `ProviderMapShell` which is already a Client Component. This is the correct pattern and works. |
| Radius bounding box approximation | Low | `ProviderGeoHelper.BoundingBox` uses a flat-earth bounding box (69mi/degree). Providers at the edge of a radius may appear outside the visual circle. Acceptable for this use case. |
| 500-marker hard limit | Low | If a tenant has >500 providers with geo data, the map truncates silently. Acceptable for Phase 1. |
| Geolocation API requires HTTPS | Low | Dev environment uses Replit's HTTPS proxy. No issue. |

---

## 11. Exact Next Coding Step

The next coding step is **Step 1 + Step 2 from section 9** — add types, API methods, and install Leaflet — because they are zero-risk and unblock all subsequent steps.

Concretely:

1. In `apps/web/src/types/careconnect.ts`: add `ProviderMarker` interface and geo fields to `ProviderSearchParams`
2. In `apps/web/src/lib/careconnect-api.ts`: add `getMarkers()` to both API objects; import `ProviderMarker`
3. Run: `npm install react-leaflet leaflet --legacy-peer-deps && npm install -D @types/leaflet --legacy-peer-deps` from `apps/web/`
4. Verify types compile: `npx tsc --noEmit` from `apps/web/`
5. Live test markers endpoint: confirm `GET /careconnect/api/providers/map` returns valid JSON for Margaret's token

Then proceed to Step 3 (build `ProviderMap` component).

---

## Implementation Blueprint

### Component 1: `ProviderMap`

**File:** `apps/web/src/components/careconnect/provider-map.tsx`  
**Type:** `'use client'` — must never be server-rendered  
**Import via:** `next/dynamic` with `{ ssr: false }` from `ProviderMapShell`

```typescript
interface ViewportBounds {
  northLat: number;
  southLat: number;
  eastLng:  number;
  westLng:  number;
}

interface ProviderMapProps {
  markers:           ProviderMarker[];
  selectedId:        string | null;
  onSelect:          (id: string) => void;
  onViewportChange:  (bounds: ViewportBounds) => void;
  isReferrer:        boolean;
  centerLat?:        number;    // from URL lat param
  centerLng?:        number;    // from URL lng param
  defaultZoom?:      number;    // default 10
}
```

**State:** `selectedId` is passed in as a prop (owned by shell). No internal fetch state — markers are passed as props.

**Map events:**
- `useMapEvents({ moveend: ..., zoomend: ... })` → call `onViewportChange` with `map.getBounds()`
- Debounce 300ms to avoid rapid refetch on continuous pan

**Marker rendering:**
```typescript
// Per marker:
<CircleMarker
  center={[m.latitude, m.longitude]}
  radius={selectedId === m.id ? 10 : 7}
  pathOptions={{
    fillColor:   m.acceptingReferrals ? '#16a34a' : '#6b7280',
    fillOpacity: 0.85,
    color:       selectedId === m.id ? '#1d4ed8' : '#fff',
    weight:      selectedId === m.id ? 3 : 1.5,
  }}
  eventHandlers={{ click: () => onSelect(m.id) }}
>
  <Popup>
    <div>
      <p className="font-medium">{m.displayLabel}</p>
      <p className="text-xs text-gray-500">{m.markerSubtitle}</p>
      {m.acceptingReferrals && <span>Accepting referrals</span>}
      <a href={`/careconnect/providers/${m.id}`}>View Provider</a>
    </div>
  </Popup>
</CircleMarker>
```

---

### Component 2: `ProviderMapShell`

**File:** `apps/web/src/components/careconnect/provider-map-shell.tsx`  
**Type:** `'use client'`

```typescript
interface ProviderMapShellProps {
  initialProviders: PagedResponse<ProviderSummary>;
  initialPage:      number;
  isReferrer:       boolean;
}
```

**State owned:**
- `view: 'list' | 'map'` — read from `useSearchParams().get('view')`, written via `router.push`
- `markers: ProviderMarker[]` — only populated in map mode
- `markersLoading: boolean`
- `markersError: string | null`
- `selectedMarkerId: string | null`

**Server vs client component split:**
- `page.tsx` (Server) → fetches initial list, passes as `initialProviders` prop
- `ProviderMapShell` (Client) → renders list using `initialProviders` in list mode; fetches markers independently in map mode

**Key behaviors:**
```
On mount (map mode):
  → read filter params from URL
  → call careConnectApi.providers.getMarkers(filterParams)
  → set markers state

On URL param change (filter change):
  → if map mode: re-fetch markers

onViewportChange(bounds):
  → router.push with nLat/sLat/eLng/wLng added to URL
  → triggers useEffect → re-fetch markers with viewport bounds

onSelect(id):
  → set selectedMarkerId = id (highlight marker)
  → (popup handles navigation)

View toggle:
  → clear nLat/sLat/eLng/wLng when switching to list (they're map-only)
  → set view param in URL
```

**API calls per interaction:**

| Interaction | API call |
|-------------|---------|
| Page load (list mode) | None — uses `initialProviders` from server |
| Switch to map mode | `GET /careconnect/api/providers/map?{current filters}` |
| Apply text filter (map mode) | `GET /careconnect/api/providers/map?{new filters}` |
| Map pan/zoom (map mode) | `GET /careconnect/api/providers/map?{filters}&nLat=...&sLat=...` |
| Apply geolocation (any mode) | `GET /careconnect/api/providers/map?lat=...&lng=...&radius=25` |
| Apply text filter (list mode) | Server re-render (URL change → page.tsx refetch) |
| Pagination (list mode) | Server re-render (URL page param → page.tsx refetch) |

---

### Component 3: `ProviderSearchFilters` (modified)

**New props:** none — still fully URL-driven  
**New state:**
- `geoActive: boolean` — true when lat/lng/radius are in URL
- `radiusMiles: number` — default 25

**New controls:**
- "Use my location" button → calls `navigator.geolocation.getCurrentPosition` → sets `lat/lng/radius` URL params
- Radius slider/input (1–100) — visible only when `geoActive`
- "Clear location" link — removes `lat/lng/radius` from URL

---

### URL / Query Param Strategy (complete)

```
/careconnect/providers
  ?view=map              ← 'list' | 'map'
  &name=Smith
  &categoryCode=PT
  &acceptingReferrals=true
  &page=2                ← list mode only

  // Radius search (user clicked "my location"):
  &lat=41.878114
  &lng=-87.629798
  &radius=25

  // Viewport search (map was panned — set by ProviderMap component):
  &nLat=42.1
  &sLat=41.6
  &eLng=-87.2
  &wLng=-88.1
```

Invariant: `lat/lng/radius` and `nLat/sLat/eLng/wLng` are mutually exclusive. When the user sets location, clear viewport params. When the map is panned, clear radius params. Enforced in `ProviderMapShell`.

---

### Marker click → provider selection flow (step-by-step)

```
1. User clicks CircleMarker in ProviderMap
2. Leaflet fires onClick → ProviderMap calls onSelect(marker.id)
3. ProviderMapShell: setSelectedMarkerId(id)
4. ProviderMap re-renders: selected marker gets blue ring + larger radius
5. Leaflet opens <Popup> for that marker
6. Popup shows: displayLabel, markerSubtitle, accepting badge
7. User clicks "View Provider" (or "Create Referral") in popup
8. Navigation: router.push('/careconnect/providers/{id}')
9. providers/[id]/page.tsx loads — fetches full ProviderDetail
10. ProviderDetailCard renders; "Create Referral" button visible (if referrer)
11. User clicks "Create Referral" → CreateReferralForm modal opens
12. User submits → POST /careconnect/api/referrals → navigated to /referrals/{id}
```

---

### Selected provider → referral flow

No new components needed. The existing `providers/[id]/page.tsx` + `CreateReferralForm` handle this completely. The map's only job is to navigate to the detail page with the correct provider ID.

---

## Copy-Paste Build Prompt

Use this prompt as the next implementation instruction:

---

**Implement provider map discovery in `apps/web` for the CareConnect tenant portal.**

You are implementing a map-based provider discovery feature. The backend is already complete. Your job is frontend only.

**Files to modify:**

1. `apps/web/src/types/careconnect.ts` — Add `ProviderMarker` interface and geo fields to `ProviderSearchParams`:
   ```typescript
   export interface ProviderMarker {
     id: string; name: string; organizationName?: string;
     displayLabel: string; markerSubtitle: string;
     city: string; state: string; addressLine1: string; postalCode: string;
     email: string; phone: string;
     acceptingReferrals: boolean; isActive: boolean;
     latitude: number; longitude: number;
     geoPointSource?: string; primaryCategory?: string; categories: string[];
   }
   // Add to ProviderSearchParams:
   latitude?: number; longitude?: number; radiusMiles?: number;
   northLat?: number; southLat?: number; eastLng?: number; westLng?: number;
   ```

2. `apps/web/src/lib/careconnect-api.ts` — Add `getMarkers()` to both `careConnectServerApi.providers` and `careConnectApi.providers`:
   ```typescript
   getMarkers: (params: ProviderSearchParams = {}) =>
     serverApi.get<ProviderMarker[]>(`/careconnect/api/providers/map${toQs(params as Record<string, unknown>)}`),
   ```
   Import `ProviderMarker` from types.

**Files to create:**

3. `apps/web/src/components/careconnect/provider-map.tsx` — Client component (`'use client'`). Uses `react-leaflet` `MapContainer`, `TileLayer`, `CircleMarker`, `Popup`, `useMapEvents`. Props: `markers`, `selectedId`, `onSelect`, `onViewportChange`, `isReferrer`. Debounce viewport change 300ms. Use `divIcon` or `CircleMarker` (NOT default Leaflet markers — they have path issues in Next.js). Popup has provider name, subtitle, accepting badge, and a `<Link href="/careconnect/providers/{id}">View Provider</Link>`.

4. `apps/web/src/components/careconnect/provider-map-shell.tsx` — Client component. Props: `initialProviders: PagedResponse<ProviderSummary>`, `initialPage: number`, `isReferrer: boolean`. Reads `view` from `useSearchParams()`. Manages `markers`, `selectedMarkerId` state. In map mode: fetches markers with `careConnectApi.providers.getMarkers(...)` on mount and on URL param change. Dynamically imports `ProviderMap` with `next/dynamic({ ssr: false })`. Renders list (from `initialProviders`) or map. Handles viewport change → router push with `nLat/sLat/eLng/wLng`. View toggle buttons in header.

**Files to modify (continued):**

5. `apps/web/src/app/(platform)/careconnect/providers/page.tsx` — Add `view`, `lat`, `lng`, `radius`, `nLat`, `sLat`, `eLng`, `wLng` to `searchParams` type. Pass geo params into server-side search call. Replace `<ProviderSearchFilters>` + list render with `<ProviderMapShell initialProviders={result} initialPage={page} isReferrer={true} />`. Keep the `requireOrg()` + role guard.

6. `apps/web/src/components/careconnect/provider-search-filters.tsx` — Add "Use my location" button that calls `navigator.geolocation.getCurrentPosition`. On success: write `?lat=X&lng=Y&radius=25` to URL. Show radius input (number, 5–100, step 5) when location active. Add "Clear location" link. Keep all existing filters.

**Package installs (run from `apps/web/`):**
```bash
npm install react-leaflet leaflet --legacy-peer-deps
npm install -D @types/leaflet --legacy-peer-deps
```

**Also add to `apps/web/src/app/layout.tsx` or a global CSS file:**
```css
@import 'leaflet/dist/leaflet.css';
```
Or in the dynamic import: wrap `import 'leaflet/dist/leaflet.css'` inside the dynamic module.

**Constraints:**
- React 18 only (no React 19 APIs)
- No new API keys or external services — use OpenStreetMap tiles (`https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png`)
- No changes to backend
- Preserve existing list behavior exactly — map is additive
- Use `next/dynamic` with `ssr: false` for any Leaflet component
- Follow existing Tailwind patterns from `provider-card.tsx` and `provider-search-filters.tsx`
- URL is the single source of truth for all filter and view state
- Geo params `lat/lng/radius` and `nLat/sLat/eLng/wLng` are mutually exclusive
- `ProviderMap` receives markers as props and does NOT fetch its own data

**Expected report output:** None. Verify by running the app and confirming: (a) list view works unchanged, (b) clicking "Map" shows Leaflet map with provider markers, (c) panning the map updates URL and re-fetches markers, (d) clicking a marker shows popup with "View Provider" link, (e) clicking "View Provider" navigates to the detail page, (f) geolocation button re-centers map on user's location.
