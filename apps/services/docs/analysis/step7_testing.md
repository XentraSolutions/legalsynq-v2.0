# Phase 7 — Testing & Security Validation

## Test Structure

```
tests/
├── unit/
│   ├── rbac.test.ts          ← RBAC + ABAC assertion tests
│   └── errors.test.ts        ← Error hierarchy HTTP status mapping
└── integration/
    └── (future — see below)
```

## Unit Tests Implemented

### `rbac.test.ts`
- `DocReader` granted read, denied write/delete
- `DocUploader` granted read+write, denied delete
- `PlatformAdmin` granted all permissions
- Empty roles → denied all (default deny)
- Same-tenant access → allowed
- Cross-tenant access → denied for non-PlatformAdmin
- Cross-tenant access → allowed for PlatformAdmin

### `errors.test.ts`
- All error subclasses carry correct HTTP status code
- Error messages contain meaningful context
- Error codes are machine-readable strings

## Running Tests

```bash
cd apps/services/docs
npm install
npm test
npm run test:unit
```

## Integration Tests — Recommended Backlog

### Authentication Tests
```
POST /documents without token → 401
POST /documents with expired token → 401
POST /documents with invalid signature → 401
POST /documents with mock token (prod) → service should reject mock in prod
```

### Tenant Isolation Tests
```
Tenant A user cannot read Tenant B document → 404 (not 403, to avoid info leakage)
Tenant A user cannot delete Tenant B document → 404
PlatformAdmin can read any tenant's document
```

### File Validation Tests
```
Upload 0-byte file → 400 FILE_VALIDATION_ERROR
Upload file exceeding MAX_FILE_SIZE_MB → 413 FILE_TOO_LARGE
Upload .exe disguised as .pdf → 400 FILE_VALIDATION_ERROR
Upload PDF with wrong MIME header → 400 FILE_VALIDATION_ERROR
Upload valid PDF → 201
```

### Authorization Tests
```
DocReader cannot POST /documents → 403
DocReader cannot DELETE → 403
DocUploader cannot DELETE → 403
DocManager can DELETE → 204
```

### Signed URL Tests
```
Signed URL for deleted document → 404
Local token expires after configured seconds → 404 on second use
```

### Legal Hold Tests
```
DELETE document with legalHoldAt set → 403
PATCH status to ARCHIVED on legal-hold doc → allowed
```

## Security Validation Checklist

| Check | Status |
|---|---|
| All endpoints require auth (except /health) | ✓ |
| Default deny RBAC | ✓ |
| Cross-tenant isolation | ✓ |
| Storage keys not in responses | ✓ |
| Sensitive fields not in logs | ✓ |
| MIME validation | ✓ |
| File size limit | ✓ |
| Signed URL expiry | ✓ |
| Audit trail for all critical actions | ✓ |
| Audit trail immutable (DB trigger) | ✓ |
| Legal hold prevents deletion | ✓ |
| Provider swap requires no core changes | ✓ |
