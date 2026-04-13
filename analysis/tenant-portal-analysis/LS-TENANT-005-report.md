# LS-TENANT-005 — Authorization Simulator Report

**Generated**: 2026-04-13
**Status**: COMPLETE
**Build**: Clean (0 TypeScript errors)

---

## 1. Simulator Page

### Route
`/tenant/authorization/simulator`

### Layout
Split into 2 panels (responsive grid: stacked on mobile, side-by-side on desktop):

- **Left Panel** — Inputs (user selection, permission selection, resource context, request context, run button)
- **Right Panel** — Results (decision header, summary, access path, policy evaluation)

---

## 2. User Selection

### Implementation
- Searchable dropdown with avatar, name, email, and status badge
- Filters top 20 matches by name or email substring
- Prefill from URL query param `?userId=...` (linked from LS-TENANT-002 "Simulate Access" button and LS-TENANT-004 Access Explorer)
- Click-outside closes dropdown

### Data Source
- `GET /identity/api/admin/users?page=1&pageSize=500` (server-side prefetch)

---

## 3. Permission Selection

### Implementation
- Searchable dropdown with permissions grouped by product code
- Shows permission code, name, and category
- Filters by code, name, or category substring
- Sticky product header in dropdown

### Data Source
- `GET /identity/api/admin/permissions` (server-side prefetch)

---

## 4. Resource Context Editor

### Form Mode (default)
Pre-defined fields:
- `amount` (number)
- `region` (text)
- `organizationId` (text)
- `sensitivity` (text)
- `ownerId` (text)

### JSON Mode
- Raw JSON textarea with validation
- Real-time parse error indicator
- Supports arbitrary key-value structures

### Mode Toggle
- Pill-style toggle between Form and JSON modes
- Form values and JSON values maintained independently

---

## 5. Request Context Editor

### Fields
- HTTP Method (dropdown: GET, POST, PUT, DELETE, PATCH)
- Path (text input with placeholder example)

### Behavior
- Optional — empty values are excluded from the simulation request
- Sent as `requestContext: { method, path }` dictionary

---

## 6. Simulation Execution

### API Call
```
POST /identity/api/admin/authorization/simulate
```

### Payload Shape
```json
{
  "tenantId": "guid",
  "userId": "guid",
  "permissionCode": "string",
  "resourceContext": { "amount": 42000, "region": "CA" },
  "requestContext": { "method": "POST", "path": "/applications/approve" }
}
```

### Behavior
- Loading state with spinner overlay
- Error state with clear message
- API response unwrapped from `ApiResponse<T>` wrapper (`'data' in resp`)
- Last request cached for re-run

---

## 7. Simulation Result UI

### A. Decision Header
- **ALLOWED** — green gradient card with checkmark
- **DENIED** — red gradient card with X
- Shows reason text and evaluation time in milliseconds

### B. Decision Summary (3 cards)
- **Permission Present** — Yes/No (green/red)
- **Role Fallback** — Yes/No (amber/gray)
- **Policies Evaluated** — count or "None"

### C. Simulated User
- Avatar (initials), display name, email
- Role badges (purple pills)

### D. Access Path
- Visual chain: `User → Group → Role → Permission`
- Each step has source badge (Direct/Group)
- Group names link to group detail page (`/tenant/authorization/groups/{groupId}`)

### E. Policy Evaluation
- Expandable cards per matched policy
- Each card shows: policy name/code, effect badge (Allow/Deny), priority, draft indicator
- Expanded view: rule-by-rule breakdown table (field, operator, expected, actual, pass/fail)

### F. Deny Override Indicator
- Red alert banner: "Denied due to policy override"
- Shows the specific deny override policy code

---

## 8. "What-If" Simulation

### Supported Scenarios
- **Context Changes**: modify amount, region, org, or any JSON field and re-run
- **Quick Re-run**: "Re-run last simulation" button retains all previous inputs
- **Assignment Awareness**: user's roles and group memberships visible in result panel

### UX
- Inputs persist between runs (component state)
- Re-run button appears after first simulation
- Change any input field and click Run again for instant comparison

---

## 9. Integration with Other Pages

### From User Detail (LS-TENANT-002)
- "Simulate Access" button already exists (amber link)
- Navigates to `/tenant/authorization/simulator?userId={id}&tenantId={tenantId}`
- Simulator auto-prefills user selection from URL params

### From Access Explorer (LS-TENANT-004)
- "Simulate Access" link added per expanded user row
- Navigates to `/tenant/authorization/simulator?userId={id}`
- Appears alongside "View full user detail" link

---

## 10. Validation Rules

### Required Fields
- `userId` — required (Run button disabled without selection)
- `permissionCode` — required (Run button disabled without selection)

### JSON Validation
- Real-time JSON parse validation in JSON mode
- Error indicator shown below textarea
- Invalid JSON excluded from request (falls back to no context)

### Tenant Scope
- `tenantId` derived from session/server-side user list
- No cross-tenant simulation possible (enforced by backend)

---

## 11. Security

- Route protected by tenant admin layout guard (`requireTenantAdmin()`)
- Server-side data fetch uses `serverApi` with JWT-forwarded cookie
- Client-side simulation call uses `apiClient` with BFF proxy (credentials: include)
- Backend enforces: `PlatformAdmin` or `TenantAdmin` role check, tenant boundary validation
- No real access mutation — simulation is read-only

---

## 12. Performance

- Users and permissions prefetched server-side (no client waterfall)
- No caching of simulation results (each run is fresh)
- Dropdown results limited to 20 users / 30 permissions for responsiveness
- Click-outside handlers for dropdown cleanup
- No debounce needed (simulation triggered by explicit button click)

---

## 13. Types Added

### `apps/web/src/types/tenant.ts`
| Type | Description |
|------|-------------|
| `SimulationRequest` | `{ tenantId, userId, permissionCode, resourceContext?, requestContext?, draftPolicy?, excludePolicyIds? }` |
| `DraftPolicyInput` | `{ policyCode, name, description?, priority, effect, rules[] }` |
| `DraftRuleInput` | `{ field, operator, value, logicalGroup? }` |
| `SimulationResult` | `{ allowed, permissionPresent, roleFallbackUsed, permissionCode, policyDecision, reason, mode, user, permissionSources[], evaluationElapsedMs }` |
| `PolicyDecisionResult` | `{ evaluated, policyVersion, denyOverrideApplied, denyOverridePolicyCode?, matchedPolicies[] }` |
| `SimulatedMatchedPolicy` | `{ policyCode, policyName?, effect, priority, evaluationOrder, result, isDraft, ruleResults[] }` |
| `SimulatedRuleResult` | `{ field, operator, expected, actual?, passed }` |
| `UserIdentitySummary` | `{ userId, tenantId, email, displayName, roles[], permissions[] }` |
| `SimPermissionSourceEntry` | `{ permissionCode, source, viaRole?, groupId?, groupName? }` |

---

## 14. API Integration

### Server-side (prefetch)
| Method | Endpoint |
|--------|----------|
| `getAdminUsers(1, 500)` | `GET /identity/api/admin/users?page=1&pageSize=500` |
| `getPermissions()` | `GET /identity/api/admin/permissions` |
| `getUsers()` | `GET /identity/api/users` (for tenantId resolution) |

### Client-side
| Method | Endpoint |
|--------|----------|
| `simulateAuthorization(body)` | `POST /identity/api/admin/authorization/simulate` |

---

## 15. Files Created/Modified

### New Files
| File | Purpose |
|------|---------|
| `apps/web/src/app/(platform)/tenant/authorization/simulator/SimulatorClient.tsx` | Main client component — split-panel simulator with inputs, results, policy visualization |

### Modified Files
| File | Change |
|------|--------|
| `apps/web/src/app/(platform)/tenant/authorization/simulator/page.tsx` | Replaced placeholder with functional server component |
| `apps/web/src/types/tenant.ts` | Added 9 simulation-related types |
| `apps/web/src/lib/tenant-api.ts` | Added `simulateAuthorization` to `tenantClientApi` |
| `apps/web/src/app/(platform)/tenant/authorization/access/AccessExplainabilityClient.tsx` | Added "Simulate Access" link in user explorer expanded rows |

---

## 16. Build Status

```
TypeScript: 0 errors
Next.js: compiles successfully
No regressions to existing routes
LS-TENANT-001 navigation and guards intact
LS-TENANT-002 user management + Simulate Access button intact
LS-TENANT-003 group management intact
LS-TENANT-004 access explainability intact + new Simulate link
```

---

## 17. Authorization Loop Complete

```
See (LS-TENANT-001/002/003) → Understand (LS-TENANT-004) → Predict (LS-TENANT-005) → Act (assign/revoke)
```

The simulator completes the predictive layer of the tenant authorization workflow, allowing admins to verify access decisions before making changes.
