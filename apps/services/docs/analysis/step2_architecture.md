# Phase 2 — Modular Provider Architecture

## Directory Structure

```
apps/services/docs/
├── src/
│   ├── shared/               # Cross-cutting concerns
│   │   ├── config.ts         # Env validation via zod
│   │   ├── logger.ts         # Pino structured logger (HIPAA-safe redaction)
│   │   ├── errors.ts         # DocsError hierarchy
│   │   └── constants.ts      # MIME allowlist, status enums, role constants
│   │
│   ├── domain/               # Pure business layer — zero external dependencies
│   │   ├── entities/
│   │   │   ├── document.ts
│   │   │   ├── document-version.ts
│   │   │   └── document-audit.ts
│   │   └── interfaces/
│   │       ├── storage-provider.ts
│   │       ├── auth-provider.ts
│   │       ├── secrets-provider.ts
│   │       ├── file-scanner-provider.ts
│   │       └── audit-provider.ts
│   │
│   ├── infrastructure/       # Concrete implementations only
│   │   ├── storage/
│   │   │   ├── s3-storage-provider.ts
│   │   │   ├── local-storage-provider.ts
│   │   │   ├── gcs-storage-provider.ts   ← scaffold
│   │   │   └── storage-factory.ts
│   │   ├── auth/
│   │   │   ├── jwt-auth-provider.ts
│   │   │   └── auth-factory.ts
│   │   ├── secrets/
│   │   │   └── env-secrets-provider.ts   + AwsSm + GcpSm scaffolds
│   │   └── database/
│   │       ├── db.ts                     # Pool + query helpers
│   │       ├── migrate.ts                # SQL migration runner
│   │       ├── document-repository.ts
│   │       └── audit-repository.ts
│   │
│   ├── application/          # Use cases — depends on domain, uses infrastructure via DI
│   │   ├── document-service.ts
│   │   ├── audit-service.ts
│   │   └── rbac.ts
│   │
│   └── api/                  # HTTP adapter — depends only on application layer
│       ├── middleware/
│       │   ├── auth.ts
│       │   ├── correlation-id.ts
│       │   ├── file-validator.ts
│       │   └── error-handler.ts
│       └── routes/
│           ├── health.ts
│           └── documents.ts
│
├── tests/
│   ├── unit/
│   └── integration/
├── analysis/
├── Dockerfile
├── .env.example
└── package.json
```

## Provider Interfaces

| Interface | Config Key | Implementations |
|---|---|---|
| `StorageProvider` | `STORAGE_PROVIDER` | `s3`, `local`, `gcs` (scaffold) |
| `AuthProvider` | `AUTH_PROVIDER` | `jwt`, `mock` (dev only) |
| `SecretsProvider` | `SECRETS_PROVIDER` | `env`, `aws-sm` (scaffold), `gcp-sm` (scaffold) |
| `FileScannerProvider` | future | `clamav`, `aws-guardduty`, `gcp-scc` |
| `AuditProvider` | — | `DatabaseAuditProvider` (default) |

## Dependency Rules (enforced by folder structure)

- `domain/` — imports nothing from outside `shared/`
- `infrastructure/` — imports from `domain/` and `shared/` only
- `application/` — imports from `domain/`, `infrastructure/`, `shared/`
- `api/` — imports from `application/` and `shared/` only
- No AWS/GCP SDK imports allowed outside `infrastructure/`

## Configuration-Driven Provider Selection

```
STORAGE_PROVIDER=s3    → S3StorageProvider
STORAGE_PROVIDER=local → LocalStorageProvider
STORAGE_PROVIDER=gcs   → GCSStorageProvider

AUTH_PROVIDER=jwt      → JwtAuthProvider  (JWKS or symmetric)
AUTH_PROVIDER=mock     → MockAuthProvider (dev only, blocked in prod)
```

Switching providers requires **zero core code changes**.
