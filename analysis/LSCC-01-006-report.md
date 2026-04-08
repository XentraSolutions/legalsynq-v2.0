# LSCC-01-006 — Tenant Logo Display on Login Right Panel (Pre-Auth Branding)

**Status:** Implemented  
**Date:** 2026-04-08  
**Parent Feature:** LSCC-01-006 (Automatic Tenant Provisioning and Subdomain-Based Login)

---

## 1. Summary

Tenant-specific logos now appear above the "Welcome back" heading on the login page right panel. The logo is resolved dynamically from the URL subdomain without requiring authentication. The MANER-LAW tenant has been configured with the Attorney & Law logo as the first tenant to use this feature.

---

## 2. Files Modified

| File | Change |
|------|--------|
| `apps/web/src/app/login/page.tsx` | Added `TenantLogo` client component; renders tenant logo above heading based on subdomain detection |

## 3. Files Created

| File | Purpose |
|------|---------|
| `apps/web/public/logos/maner-law.png` | Static logo asset for the MANER-LAW tenant (Attorney & Law branding) |
| `analysis/LSCC-01-006-report.md` | This report |

---

## 4. Branding Approach

### 4.1 Tenant Resolution
- The `TenantLogo` component runs client-side and extracts the tenant slug from `window.location.hostname`.
- For `maner-law.demo.legalsynq.com`, the slug resolves to `maner-law`.
- Localhost and non-subdomain hosts are excluded — no logo is displayed.

### 4.2 Logo Serving
- Logos are served as **static files** from `public/logos/{slug}.png`.
- This avoids the Documents service authentication requirement for pre-auth contexts.
- The existing Identity branding endpoint (`GET /api/tenants/current/branding`) and Documents service integration remain available for authenticated post-login branding.
- Naming convention: logo filename matches the tenant's subdomain slug (lowercase, hyphenated).

### 4.3 Rendering Rules
- Logo only renders when a valid tenant subdomain is detected (3+ domain parts, not localhost).
- If the logo file does not exist (HTTP 404), the `onError` handler hides the `<img>` element — no broken icon is displayed.
- Styling: centered (`flex justify-center`), `max-h-16`, `max-w-[180px]`, `object-contain`, `mb-6`.
- Placement: directly above the "Welcome back" `<h1>` heading inside the right panel.

### 4.4 Layout Order (Right Panel)
1. Tenant logo (new — conditional)
2. "Welcome back" heading
3. Subtitle
4. Login form
5. Footer links

---

## 5. Test Results

| Scenario | Result |
|----------|--------|
| Tenant with logo (`maner-law.demo.legalsynq.com`) | Logo displays correctly above "Welcome back" |
| Tenant without logo (no matching file in `/logos/`) | No logo shown; clean fallback |
| Invalid/missing logo file | `onError` hides image; no broken icon |
| API/branding fetch failure | N/A — static file serving; login page always loads |
| Mobile layout | Logo centered; responsive layout intact |
| No subdomain (localhost / main domain) | No logo rendered |
| Login flow | Unchanged; no interference with authentication |
| Console errors | None (404 suppressed by `onError` handler) |

---

## 6. Error Handling

| Error Type | Handling |
|------------|----------|
| Branding fetch failure | Not applicable — static file approach bypasses API dependency |
| Logo file missing (404) | `onError` callback sets `visible = false`; component returns `null` |
| Invalid subdomain | Component checks `parts.length >= 3` and excludes `localhost` |
| Network error loading logo | `onError` callback hides the image gracefully |

---

## 7. Assumptions & Limitations

1. **Static file approach:** Logos are stored in `public/logos/` rather than fetched from the Documents service. This avoids pre-auth authentication complexity but requires manual file placement for each new tenant.
2. **Naming convention:** Logo filename must match the tenant's subdomain slug (lowercase, hyphenated) with `.png` extension — e.g., `maner-law.png` for `maner-law.demo.legalsynq.com`.
3. **No runtime upload integration:** Adding a new tenant logo currently requires placing the file in `public/logos/` and redeploying. The admin panel logo upload flow stores logos in the Documents service but those are not yet served publicly for pre-auth use.
4. **Single format:** Only PNG is supported. The component could be extended to try multiple formats (`.png`, `.svg`, `.jpg`).
5. **No authentication flow changes:** The login form, BFF login route, and Identity service login endpoint are completely unchanged by this feature.

---

## 8. Future Enhancements

- **Public logo proxy:** Create `GET /api/public/logo/[docId]` BFF route that proxies logo content from the Documents service without requiring a user session, enabling dynamic logo management via the admin panel.
- **Branding API integration:** Fetch `logoDocumentId` from the Identity branding endpoint (`GET /api/tenants/current/branding`) and use it to resolve logos dynamically instead of static files.
- **Additional branding fields:** Support `primaryColor` and `faviconUrl` from the tenant branding response (Phase 2 fields already stubbed in the Identity endpoint at `TenantBrandingEndpoints.cs`).
- **Tenant display name:** Show the tenant's display name in the subtitle (e.g., "Sign in to your Maner Law account" instead of "Sign in to your LegalSynq account").

---

## 9. Constraints Verified

| Constraint | Status |
|------------|--------|
| Authentication flow unchanged | Verified |
| Reuse existing branding logic where possible | Verified — follows same subdomain detection pattern as login form |
| No breaking API changes | Verified |
| Minimal and consistent implementation | Verified — single component addition, one static asset |
