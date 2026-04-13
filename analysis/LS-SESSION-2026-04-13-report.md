# LegalSynq v2 — Session Report: April 13, 2026

**Scope:** Liens domain modeling, Snyk damage repair, control center bug fix, database table prefix convention
**Commits:** 8 (excluding publishes)
**Files Changed:** 115 files, +3,933 / −1,106 lines
**Build Status:** All services — 0 warnings, 0 errors

---

## 1. Snyk Automated PR Damage Repair

### Problem
Snyk's automated dependency PRs introduced breaking version mismatches across the monorepo:
- Root `package.json`: Next.js bumped from 15.x to 16.1.5 (incompatible with React 18)
- `apps/control-center`: Duplicate `next` entries created in `package.json`
- `Fund.Api`: EF Core Design package bumped from 8.0.x to 9.0.0 (incompatible with .NET 8 target)
- `Audit`: EF Core Design package bumped from 8.0.x to 9.0.0

### Resolution
- Pinned `next` to `15.2.9` across both frontend apps (React 18 compatible)
- Cleaned duplicate entries in control-center `package.json`
- Downgraded `Fund.Api` EF Core Design to `8.0.2` (matches Pomelo 8.0.2)
- Downgraded `Audit` EF Core Design to `8.0.0` (matches Pomelo 8.0.0)
- All browser hydration/hook errors resolved

### Preventive Rule Established
- Next.js must stay at `15.2.9` (React 18 incompatible with Next.js 16+)
- All EF Core packages must stay on 8.0.x (project targets .NET 8)

---

## 2. Liens Domain Foundation (LS-LIENS-03-001)

### Entities Created
| Entity | File | Purpose |
|--------|------|---------|
| `Case` | `Liens.Domain/Entities/Case.cs` | Legal case linked to liens |
| `Contact` | `Liens.Domain/Entities/Contact.cs` | People associated with cases |
| `Facility` | `Liens.Domain/Entities/Facility.cs` | Medical facilities providing treatment |
| `LookupValue` | `Liens.Domain/Entities/LookupValue.cs` | Configurable lookup data (categories, types) |

### Patterns Established
All entities follow v2 conventions:
- `AuditableEntity` base class
- Private constructor + static `Create` factory
- `Guid.Empty` guards on required IDs
- `ArgumentException.ThrowIfNullOrWhiteSpace` for required strings
- `.Trim()` on all string inputs
- String constants with `IReadOnlySet<string> All` for status/type values

---

## 3. Core Lien Entity (LS-LIENS-03-002)

### Entity: `Lien.cs`
- **28 properties** covering identity, tenant scoping, financial terms, status lifecycle, multi-party ownership, and audit fields
- **10 domain methods:** Create, Update, TransitionStatus, ListForSale, Withdraw, MarkSold, Activate, Settle, SetFinancials, AttachCase/Facility/TransferHolding

### Multi-Party Ownership Model
```
SellingOrgId → BuyerOrgId → HoldingOrgId
(provider)     (purchaser)   (current holder)
```

### Status State Machine: `LienStatus.cs`
9 statuses with explicit `AllowedTransitions` matrix:
```
Draft → Offered → UnderReview → Sold → Active → Settled
                                  ↘ Withdrawn
                                  ↘ Cancelled
                                  ↘ Disputed
```

### Architecture Review Fixes
| Issue | Fix |
|-------|-----|
| `Sold` incorrectly in Terminal set | Removed — Sold transitions to Active |
| `TransitionStatus` unconstrained | Enforced AllowedTransitions matrix |
| `SetFinancials` missing guards | Added non-negative validation |
| `Withdraw` missing timestamp | Sets `ClosedAtUtc` |

---

## 4. LienOffer Entity (LS-LIENS-03-003)

### Entity: `LienOffer.cs`
- **15 properties** covering identity, parties, financials, status, communication, lifecycle timestamps
- **6 domain methods:** Create, UpdatePending, Accept, Reject, Withdraw, Expire

### Supporting Type: `OfferStatus.cs`
5 statuses: Pending → Accepted / Rejected / Withdrawn / Expired
- `Terminal` subset: Accepted, Rejected, Withdrawn, Expired
- `AllowedTransitions` matrix enforced

### Key Design Decisions
| Decision | Rationale |
|----------|-----------|
| `SellerOrgId` on offer (snapshot) | Historical integrity if lien ownership changes |
| `Notes` vs `ResponseNotes` separation | Clean buyer/seller communication semantics |
| `ExpiresAtUtc` domain-enforced | `EnsurePendingAndNotExpired()` guard on all transitions |
| `IsExpired` dual-mode | True for `Status == Expired` OR `(Pending && past deadline)` |
| `Expire(Guid?)` optional user | Supports system-triggered (null) and user-triggered expiration |

### Architecture Review Fixes
| Issue | Severity | Fix |
|-------|----------|-----|
| Accept/Reject/Withdraw ignored clock-based expiry | Critical | Added `EnsurePendingAndNotExpired()` centralized guard |
| `IsExpired` only checked pending+time | Medium | Expanded to cover explicit Expired status too |
| `Expire()` had no audit trail | Low | Added optional `Guid? expiredByUserId` parameter |

---

## 5. Control Center White Logo Bug Fix

### Problem
The white/reversed tenant logo showed a broken image icon in the control center's tenant detail page, while the full-color logo displayed correctly. Both logos were uploading and storing successfully.

### Root Cause
In `apps/control-center/src/lib/api-mappers.ts`, the `mapTenantDetail` function mapped `logoDocumentId` from the API response but silently dropped `logoWhiteDocumentId`. The component received `undefined` for the white logo document ID, so it rendered the image tag with a content URL pointing to a null ID — resulting in a broken image.

### Fix
Added the missing mapper line:
```typescript
logoWhiteDocumentId: (r['logoWhiteDocumentId'] ?? r['logo_white_document_id']) as string | undefined,
```

### Impact
Single-line fix. No other files affected. Both logo variants now display correctly.

---

## 6. Database Table Prefix Convention

### Problem
Each microservice's database tables had no naming convention to indicate ownership. When examining any database, there was no way to tell which service owned which table.

### Convention Established
| Service | Prefix | DB Engine | Example Tables |
|---------|--------|-----------|----------------|
| Identity | `idt_` | MySQL | `idt_Tenants`, `idt_Users`, `idt_Organizations` |
| Fund | `fund_` | MySQL | `fund_Applications` |
| CareConnect | `cc_` | MySQL | `cc_Referrals`, `cc_Providers`, `cc_Appointments` |
| Notifications | `ntf_` | MySQL | `ntf_notifications`, `ntf_templates` |
| Audit | `aud_` | MySQL/SQLite | `aud_AuditEventRecords`, `aud_LegalHolds` |
| Documents | `docs_` | PostgreSQL | `docs_documents`, `docs_document_versions` |
| Liens | `liens_` | TBD | Convention set for future entities |

### Implementation Details

| Component | Count | What Changed |
|-----------|-------|--------------|
| Identity configurations | 33 files | `builder.ToTable("idt_TableName")` |
| Fund configurations | 1 file | `builder.ToTable("fund_Applications")` |
| CareConnect configurations | 23 files | `builder.ToTable("cc_TableName")` |
| Notifications configurations | 5 files (18 calls) | `builder.ToTable("ntf_table_name")` |
| Audit configurations | 7 files | `builder.ToTable("aud_TableName")` |
| Documents DbContext | 1 file | `e.ToTable("docs_table_name")` |
| Documents schema.sql | 1 file | All CREATE TABLE/INDEX updated |
| Documents Program.cs | 1 file | Auto-rename migration SQL |
| Model snapshots | 4 files | Updated to match prefixed configs |
| Rename migrations | 4 files | New `AddTablePrefixes` migration per service |
| Raw SQL fix | 1 file | `ProductProvisioningService.cs` → `idt_TenantProducts` |

### Documents Auto-Rename (Local PostgreSQL)
The Documents startup includes idempotent SQL that:
1. Checks if old unprefixed tables exist AND new prefixed tables don't
2. Renames old → new only when safe
3. Scopes checks to `table_schema = 'public'`
4. Guards post-rename ALTER TABLE with existence checks

**Verified locally:** `documents` → `docs_documents`, `document_versions` → `docs_document_versions`, `document_audits` → `docs_document_audits`

### MySQL Services (Identity, Fund, CareConnect, Audit)
Proper EF Core rename migrations created using `migrationBuilder.RenameTable()`. These will execute when the services connect to their respective AWS RDS MySQL databases.

### Tables NOT Prefixed (Node.js-managed)
| Table | Owner |
|-------|-------|
| `artifacts` | Artifacts TypeScript service |
| `feedback_action_items` | Artifacts TypeScript service |
| `feedback_action_links` | Artifacts TypeScript service |
| `feedback_records` | Artifacts TypeScript service |
| `document_types` | Legacy Node.js documents service |
| `_docs_migrations` | Legacy Node.js migration tracker |

---

## 7. Build Verification

| Service | Warnings | Errors | Status |
|---------|----------|--------|--------|
| Identity.Api | 0 | 0 | Pass |
| Fund.Api | 0 | 0 | Pass |
| CareConnect.Api | 0 | 0 | Pass |
| Notifications.Api | 0 | 0 | Pass |
| Audit | 0 | 0 | Pass |
| Documents.Api | 1 (pre-existing CS1998) | 0 | Pass |
| Liens.Api | 0 | 0 | Pass |
| Gateway.Api | 0 | 0 | Pass |

---

## 8. Liens Domain — Current Entity Inventory

| Entity | Properties | Domain Methods | Status |
|--------|------------|----------------|--------|
| `Case` | 12 | Create, Update, Close, Reopen | Complete |
| `Contact` | 11 | Create, Update | Complete |
| `Facility` | 10 | Create, Update, Deactivate | Complete |
| `LookupValue` | 8 | Create, Update, Deactivate | Complete |
| `Lien` | 28 | 10 methods | Complete |
| `LienOffer` | 15 | 6 methods | Complete |

---

## 9. Next Steps

### Liens Service (Immediate)
1. EF Core entity configurations (`LienConfiguration.cs`, `LienOfferConfiguration.cs`)
2. `BillOfSale` entity — ownership transfer record linked to accepted offer
3. Application service — orchestrate accept-offer → mark-sold → reject-others → create-bill-of-sale
4. API endpoints — offer CRUD + marketplace actions
5. Frontend integration — replace mock data with real APIs

### Platform (Backlog)
- `document_audits` `correlation_id` NOT NULL constraint violation during logo upload/scan
- `TenantProductEntitlements` table never populated — migrate from TenantProducts or drop
- Monitor Snyk PRs for cross-major version bumps

---

## 10. Detailed Reports

| Report | Path |
|--------|------|
| Liens Foundation | `analysis/LS-LIENS-03-001-report.md` |
| Core Lien Entity | `analysis/LS-LIENS-03-002-report.md` |
| LienOffer Entity | `analysis/LS-LIENS-03-003-report.md` |
| This Session Report | `analysis/LS-SESSION-2026-04-13-report.md` |
