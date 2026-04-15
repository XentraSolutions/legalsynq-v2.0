# LS-LIENS-CASE-ACT-001 — Case Details Action Enablement

## Objective
Enable real edit/save functionality for Plaintiff Info and Case Tracking sections in the Case Detail → Details tab.

## Scope
- Details tab Plaintiff Info and Case Tracking edit actions only
- No changes to other tabs, nav, fonts, colors, routing

## API Discovery

### Backend Endpoint
`PUT /lien/api/liens/cases/{id}` — requires `LiensPermissions.CaseUpdate`

### Supported Fields (API-backed persistence)

**Plaintiff Info:**
| Field | API Field | Type | Validation |
|-------|-----------|------|------------|
| First Name | `clientFirstName` | required | Non-empty |
| Last Name | `clientLastName` | required | Non-empty |
| Phone Number | `clientPhone` | optional | Format: digits/spaces/parens/plus, 7-20 chars |
| Email | `clientEmail` | optional | Standard email format |
| Birthdate | `clientDob` | optional | Text (free-form, dates from API) |
| Address | `clientAddress` | optional | Text |

**Case Tracking:**
| Field | API Field | Type | Validation |
|-------|-----------|------|------------|
| Case Status | `status` | optional | Dropdown from STATUSES |
| Case Type | `title` | optional | Text |
| Date of Incident | `dateOfIncident` | optional | Text |
| Case Tracking Note | `description` | optional | Textarea |

### Unsupported Fields (NOT API-backed)
- Sex — no `sex`/`gender` field in DTO
- Tracking Follow Up — no `trackingFollowUp` field in DTO
- Current Medical Status — no `medicalStatus` field in DTO
- State of Incident — no `stateOfIncident` field in DTO
- Lead — no `lead`/`assignee` field in DTO
- Case Flags (all 5) — no flag fields in DTO

These fields display "---" in read mode and are disabled/hidden in edit mode.

## Implementation

### Edit Flow
1. User clicks pencil icon on section header (only visible when `canEdit` is true)
2. Section switches from read-only `FieldGrid` to inline edit form
3. Form prefilled with current values from `CaseDetail`
4. User edits fields, validation runs on save
5. Save calls `casesService.updateCase()` with full DTO (preserving all unchanged fields)
6. On success: `onCaseUpdated(updated)` refreshes parent `caseDetail` state, form closes, success toast
7. On failure: form stays open with user input preserved, error toast with message
8. Cancel discards changes and closes form

### Validation
- **Plaintiff**: First Name required, Last Name required, Email format checked, Phone format checked
- **Case Tracking**: No required fields (all optional in API)
- Save button disabled during save (prevents double submit)
- Cancel button disabled during save

### Role/Permission
- Edit pencil icon only rendered when `canEdit = ra.can('case:edit')` is true
- Unauthorized users see read-only view with no edit affordance

### Save Behavior
- Loading spinner on Save button during API call
- Both Save and Cancel disabled during save
- Success: UI refreshes immediately via `setCaseDetail(updated)`
- Failure: Form stays open, user input preserved, error toast with specific API error message

### Files Changed
| File | Change |
|------|--------|
| `apps/web/src/app/(platform)/lien/cases/[id]/case-detail-client.tsx` | Rewrote `DetailsTab` with edit modes, validation, save/cancel, role gating; added `canEdit` and `onCaseUpdated` props |

### Validation Results
- TypeScript: 0 errors, 0 warnings
- Save sends correct `UpdateCaseRequestDto` preserving all unedited fields
- No fake persistence — saves go through real `casesService.updateCase()` API
- Unsupported fields honestly show "---" and are not pretended to save
- Other tabs unaffected

### Code Review Adjustments
- **Case Flags**: Changed from interactive checkboxes (misleading) to disabled read-only placeholders with "Not yet supported" label
- **Date validation**: Added `dateOfIncident` format validation in `validateTracking()` — accepts MM/DD/YYYY and MMM D, YYYY formats
- **Unused state removed**: Removed 5 case flag `useState` variables that were no longer needed

### Remaining Gaps
- Sex, Tracking Follow Up, Current Medical Status, State of Incident, Lead fields need backend API support to become editable
- Case Flags need backend schema + API to become functional
- Updates table still uses TEMP fallback data
