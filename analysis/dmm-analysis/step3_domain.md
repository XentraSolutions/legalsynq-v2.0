# Phase 3 — Secure Domain Model

## Entities

### Document
Core aggregate root. No provider-specific fields.

| Field | Type | Purpose |
|---|---|---|
| `id` | UUID | Primary key |
| `tenantId` | UUID | Tenant isolation — all queries filter by this |
| `productId` | string | Which LegalSynq product owns this document |
| `referenceId` | string | External ID (caseId, patientId, matterId, lienId) |
| `referenceType` | string | Discriminator: CASE / PATIENT / MATTER / LIEN |
| `documentTypeId` | UUID → DocumentType | Classification |
| `status` | enum | DRAFT / ACTIVE / ARCHIVED / DELETED / LEGAL_HOLD |
| `storageKey` | string | Internal storage path — **never exposed to clients** |
| `checksum` | SHA-256 | File integrity verification |
| `currentVersionId` | UUID | Points to latest DocumentVersion |
| `retainUntil` | timestamp | Retention policy hook |
| `legalHoldAt` | timestamp | Legal hold — blocks deletion |

### DocumentVersion
Immutable once inserted. Supports full version history.

| Field | Type | Purpose |
|---|---|---|
| `versionNumber` | int | Monotonically increasing per document |
| `scanStatus` | enum | PENDING / CLEAN / INFECTED / SKIPPED |
| `checksum` | SHA-256 | Per-version integrity |
| `label` | string | Human-friendly label (e.g. "Final v1") |

### DocumentAudit
INSERT-only. DB trigger prevents UPDATE/DELETE.

| Field | Type | Purpose |
|---|---|---|
| `event` | enum | One of 14 AuditEvent values |
| `actorId` | UUID | Who performed the action |
| `actorRoles` | string[] | Roles at time of action (immutable snapshot) |
| `correlationId` | string | Request trace ID |
| `outcome` | enum | SUCCESS / DENIED / ERROR |
| `detail` | JSONB | Event-specific metadata (no PII) |

### DocumentType
Multi-tenant classification taxonomy.

- `tenantId = null` → global type available to all tenants
- Per-tenant types override or extend global set

## Design Rules

1. **No provider-specific logic** in any entity
2. **No external schema coupling** — entities do not mirror any other service's tables
3. **Soft delete everywhere** — `is_deleted + deleted_at + deleted_by`
4. **Retention-ready** — `retain_until` and `legal_hold_at` fields present; enforcement in service layer
5. **SHA-256 checksums** on every document and version for integrity verification
6. **Tenant isolation enforced at query level** — all SELECT/UPDATE/DELETE include `AND tenant_id = $n`

## HIPAA-Aligned Design Notes

- No PHI stored in audit `detail` JSONB
- No raw IP addresses — field present for compliance but should be hashed/truncated in production
- Storage keys are opaque internal paths, never derivable from document metadata
- Soft delete + retention enables legal hold workflows
