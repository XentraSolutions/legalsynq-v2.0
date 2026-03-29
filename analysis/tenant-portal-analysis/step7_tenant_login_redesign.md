# Step 7 — Tenant Login Page Redesign

## 1. Executive Summary

The tenant portal login page has been redesigned from a simple centered card into a modern 2-panel split-screen experience matching the LegalSynq brand.

The left panel presents the LegalSynq brand with the white logo on a deep navy background, a hero headline, four product capability bullets with orange icon containers, and a trust footer. The right panel is a clean light-background form panel preserving all existing authentication logic with improved form UX.

---

## 2. Current Login Page Findings

| Aspect | Before |
|---|---|
| Layout | Single centered card, `max-w-sm`, gray-50 background |
| Branding | Plain text heading "Sign in" only — no logo |
| Form | White card, border shadow, basic labels and inputs |
| CTA button | `bg-primary` (CSS variable, defaults to blue) |
| Error state | Plain red text paragraph |
| Password field | No show/hide toggle |
| Logo usage | None |
| Mobile behavior | Already responsive (single column) |
| Dev field | Tenant Code shown when `NEXT_PUBLIC_ENV === 'development'` |

---

## 3. Branding and Layout Decisions Applied

### Panel split
- **Left (45% / 42% XL):** `#0f1928` — the approved LegalSynq navy, matching the top bar
- **Right (flex-1):** `bg-gray-50` — light, neutral, form-focused
- Desktop: side-by-side (`lg:flex-row`)
- Mobile: right panel (form) takes full screen — left panel is `hidden lg:flex` so form is immediately accessible

### Left panel
- White logo (`/legalsynq-logo-white.png`) top-left
- Short orange horizontal rule `#f97316` above the headline
- Hero headline: "Legal-medical workflows, unified." — bold, 3xl/4xl
- Descriptor subtext in `slate-400`
- Four capability bullets — each with a small orange-tinted rounded icon container (`ri-` Remixicon icons) and `slate-300` text
- Footer: "TRUSTED BY LAW FIRMS & MEDICAL PROVIDERS" in uppercase `slate-500`, above an 8% white border
- Two subtle decorative rings (large bottom-left, small top-right) in orange at 3–4% opacity for depth without distraction

### Right panel
- "Welcome back" H1 + "Sign in to your LegalSynq account" subtext
- Labelled `Field` helper component for consistent label/hint/input composition
- Orange focus ring on all inputs: `focus:ring-[#f97316]/40`
- Password field has a show/hide toggle (`ri-eye-line` / `ri-eye-off-line`)
- Error banner: icon + message in a red bordered box (replaces plain text)
- CTA button: solid orange (`#f97316`) with loading state showing a spinning `ri-loader-4-line`
- Mobile-only logo: the dark `legalsynq-logo.png` appears above the form on small screens
- "Need access? Contact support" footer link

### Colour discipline
- Orange used only for: CTA button, icon containers, accent rule, focus rings, decorative rings
- No orange backgrounds beyond the icon containers
- No gradients (decorative rings are borders, not fills)

---

## 4. Files Created

| File | Purpose |
|---|---|
| `analysis/step7_tenant_login_redesign.md` | This report |

---

## 5. Files Modified

| File | Change |
|---|---|
| `apps/web/src/app/login/page.tsx` | Full rewrite — 2-panel layout, brand panel, form panel, mobile logo |
| `apps/web/src/app/login/login-form.tsx` | Polished UX — Field component, show/hide password, orange focus ring, spinner in button, icon error banner |

---

## 6. Responsive Behavior

| Breakpoint | Layout |
|---|---|
| Mobile (`< lg`) | Form panel only (full screen). Left panel hidden. Mobile-only logo shown above form. |
| Desktop (`≥ lg`) | 2-column split. Left panel 45%, right flex-1. |
| XL (`≥ xl`) | Left panel narrows slightly to 42% for better form breathing room. |

Form is always the immediate focal point on all screen sizes.

---

## 7. Functional Validation

- `POST /api/auth/login` called with `{ email, password, tenantCode? }` — unchanged
- `router.push('/dashboard')` on success — unchanged
- Tenant Code field still gated on `NEXT_PUBLIC_ENV === 'development'` + after-mount check to prevent hydration mismatch — unchanged
- Form `required` attributes and `noValidate` on the form element (to use custom error display) — preserved
- `autoComplete="email"` and `autoComplete="current-password"` — preserved
- Error message from server response (`err.message`) surfaced — preserved
- Loading state disables button and shows spinner — preserved

---

## 8. Remaining Limitations

- The logo appears as a broken image in the Replit screenshot tool (sandbox env doesn't resolve `/public` directly), but resolves correctly in the running Next.js app.
- If `TenantBrandingProvider` supplies a custom logo URL or primary colour, the login page does not yet consume those (login renders before session is established). Extending this would require passing branding via URL params or a public branding endpoint.
- No "forgot password" link exists — none was present before and no backend endpoint supports it yet.
- The left panel content is static (no CMS-driven copy). Adding dynamic messaging per tenant would require a public branding API endpoint.
