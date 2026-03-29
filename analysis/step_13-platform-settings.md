# Step 13 — Platform Settings & Feature Flags

## Status: Complete — 0 TypeScript errors

---

## Summary

Implemented the **Platform Settings & Feature Flags** page inside `apps/control-center`.
The page is gated behind `requirePlatformAdmin()` and serves 5 mock settings via
`controlCenterServerApi.settings.list()`.  Mutations flow through a `'use server'`
action and an optimistic-UI client component that reverts on failure.

---

## Files Changed

### Modified

| File | Change |
|------|--------|
| `apps/control-center/src/types/control-center.ts` | Replaced old `PlatformSetting` shape with spec-aligned type |
| `apps/control-center/src/lib/control-center-api.ts` | Added `PlatformSetting` import, `MOCK_SETTINGS_STORE`, and `settings` namespace |

### Created

| File | Purpose |
|------|---------|
| `apps/control-center/src/app/settings/page.tsx` | Server component — loads settings, renders shell + panel |
| `apps/control-center/src/app/settings/actions.ts` | `'use server'` action — `updateSetting(key, value)` |
| `apps/control-center/src/components/settings/platform-settings-panel.tsx` | Client component — toggles + text/number inputs with optimistic UI |

---

## Type Change

**Old `PlatformSetting`** (pre-step-13):
```ts
interface PlatformSetting {
  key:          string;
  value:        string;
  description:  string;
  isSecret:     boolean;
  updatedAtUtc: string;
}
```

**New `PlatformSetting`** (step-13 spec):
```ts
interface PlatformSetting {
  key:          string;
  label:        string;
  value:        string | number | boolean;
  type:         'boolean' | 'string' | 'number';
  description?: string;
  editable:     boolean;
}
```

---

## Mock Data (`MOCK_SETTINGS_STORE`)

| key | type | default value | editable |
|-----|------|---------------|----------|
| `allowTenantSelfSignup` | boolean | `false` | true |
| `enableCareConnectMap` | boolean | `true` | true |
| `enableSynqPayoutBeta` | boolean | `false` | true |
| `supportEmailAddress` | string | `support@legalsynq.com` | true |
| `defaultSessionTimeoutMinutes` | number | `60` | true |

All five entries carry a `description` field.

---

## API Methods

```ts
controlCenterServerApi.settings.list()
// Returns Promise<PlatformSetting[]>
// TODO: replace with GET /identity/api/admin/settings

controlCenterServerApi.settings.update(key, value)
// Returns Promise<PlatformSetting>
// Throws if key unknown or setting not editable
// TODO: replace with POST /identity/api/admin/settings
```

The in-memory store (`MOCK_SETTINGS_STORE`) mutates on `update()` calls so toggles
persist for the lifetime of the Next.js process — identical to how entitlement overrides
work in the tenants API stub.

---

## Server Action

```ts
// apps/control-center/src/app/settings/actions.ts
'use server';
export async function updateSetting(
  key:   string,
  value: string | number | boolean,
): Promise<UpdateSettingResult>
```

Wraps `controlCenterServerApi.settings.update`, returns `{ success, setting?, error? }`.

---

## Client Component: `PlatformSettingsPanel`

**Sections:**
- **Feature Flags** — three boolean settings rendered as toggle switches
- **System Configuration** — one string field + one number field with Save buttons

**UX behaviour:**
| Scenario | Behaviour |
|----------|-----------|
| Toggle click | Optimistic flip → server action → revert on error |
| Save click | Disabled if draft === current value; optimistic apply → revert on error |
| In-flight | Spinner overlay on toggle / "Saving…" text on button; control disabled |
| Success | Green "Saved" badge inline with label, auto-dismisses on next interaction |
| Error | Red inline message beneath the affected control |

**No `session` prop leak** — `CCShell` receives only `userEmail={session.email}`, consistent
with all other pages.

---

## Page Layout (`/settings`)

```
/settings
├── Header: "Platform Settings" + description
├── Stats bar: "5 settings total | 3 feature flags | 2 configuration values"
└── PlatformSettingsPanel
    ├── Section: Feature Flags
    │   ├── allowTenantSelfSignup   [toggle — OFF]
    │   ├── enableCareConnectMap    [toggle — ON]
    │   └── enableSynqPayoutBeta    [toggle — OFF]
    └── Section: System Configuration
        ├── supportEmailAddress     [text input + Save]
        └── defaultSessionTimeoutMinutes  [number input + Save]
```

---

## TypeScript Verification

```
cd apps/control-center && tsc --noEmit
# → 0 errors, 0 warnings
```

---

## TODO Markers

```ts
// TODO: replace with GET/POST /identity/api/admin/settings
```

Present on:
- `MOCK_SETTINGS_STORE` block in `control-center-api.ts`
- `settings.list()` method
- `settings.update()` method
- `/settings` page JSDoc

---

## Patterns Followed

- `requirePlatformAdmin()` guard — identical to all prior steps
- `controlCenterServerApi.*` for all data access — no direct fetch/import from `apps/web`
- `'use server'` action wraps the API call, returns a typed result union
- Client component owns optimistic state; server component owns data loading
- `CCShell userEmail={session.email}` — matches steps 5–12 exactly
