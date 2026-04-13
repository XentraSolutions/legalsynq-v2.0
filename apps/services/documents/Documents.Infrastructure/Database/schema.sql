-- Documents Service .NET — PostgreSQL DDL
-- Matches the Node.js (TypeScript) Documents Service schema.
-- Run this as an alternative to EF Core migrations in environments
-- where dotnet-ef tooling is unavailable.

-- ── Extension ─────────────────────────────────────────────────────────────────
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ── documents ─────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS documents (
    id                UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         UUID        NOT NULL,
    product_id        VARCHAR(100) NOT NULL,
    reference_id      VARCHAR(500) NOT NULL,
    reference_type    VARCHAR(100) NOT NULL,
    document_type_id  UUID        NOT NULL,
    title             VARCHAR(500) NOT NULL,
    description       VARCHAR(2000),
    status            VARCHAR(20)  NOT NULL DEFAULT 'DRAFT'
                        CHECK (status IN ('DRAFT','ACTIVE','ARCHIVED','DELETED','LEGAL_HOLD')),
    mime_type         VARCHAR(200) NOT NULL,
    file_size_bytes   BIGINT       NOT NULL,
    storage_key       TEXT         NOT NULL,
    storage_bucket    VARCHAR(200) NOT NULL,
    checksum          VARCHAR(200),
    current_version_id UUID,
    version_count     INT          NOT NULL DEFAULT 0,
    scan_status       VARCHAR(20)  NOT NULL DEFAULT 'PENDING'
                        CHECK (scan_status IN ('PENDING','CLEAN','INFECTED','FAILED','SKIPPED')),
    scan_completed_at TIMESTAMPTZ,
    scan_duration_ms  INT,
    scan_threats      JSONB        NOT NULL DEFAULT '[]',
    scan_engine_version VARCHAR(100),
    is_deleted        BOOLEAN      NOT NULL DEFAULT FALSE,
    deleted_at        TIMESTAMPTZ,
    deleted_by        UUID,
    retain_until      TIMESTAMPTZ,
    legal_hold_at     TIMESTAMPTZ,
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    created_by        UUID         NOT NULL,
    updated_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_by        UUID         NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_documents_tenant      ON documents (tenant_id);
CREATE INDEX IF NOT EXISTS idx_documents_product     ON documents (tenant_id, product_id) WHERE NOT is_deleted;
CREATE INDEX IF NOT EXISTS idx_documents_reference   ON documents (tenant_id, reference_id) WHERE NOT is_deleted;
CREATE INDEX IF NOT EXISTS idx_documents_status      ON documents (tenant_id, status) WHERE NOT is_deleted;

-- ── document_versions ─────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS document_versions (
    id                UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id       UUID        NOT NULL REFERENCES documents(id),
    tenant_id         UUID        NOT NULL,
    version_number    INT         NOT NULL,
    mime_type         VARCHAR(200) NOT NULL,
    file_size_bytes   BIGINT       NOT NULL,
    storage_key       TEXT         NOT NULL,
    storage_bucket    VARCHAR(200) NOT NULL,
    checksum          VARCHAR(200),
    scan_status       VARCHAR(20)  NOT NULL DEFAULT 'PENDING',
    scan_completed_at TIMESTAMPTZ,
    scan_duration_ms  INT,
    scan_threats      JSONB        NOT NULL DEFAULT '[]',
    scan_engine_version VARCHAR(100),
    label             VARCHAR(200),
    is_deleted        BOOLEAN      NOT NULL DEFAULT FALSE,
    deleted_at        TIMESTAMPTZ,
    deleted_by        UUID,
    uploaded_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    uploaded_by       UUID         NOT NULL,

    CONSTRAINT uq_version_number UNIQUE (document_id, version_number)
);

CREATE INDEX IF NOT EXISTS idx_versions_document ON document_versions (document_id, tenant_id);

-- ── document_audits ───────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS document_audits (
    id             UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id      UUID        NOT NULL,
    document_id    UUID        REFERENCES documents(id) ON DELETE SET NULL,
    event          VARCHAR(100) NOT NULL,
    actor_id       UUID,
    actor_email    VARCHAR(500),
    outcome        VARCHAR(20)  NOT NULL DEFAULT 'SUCCESS',
    ip_address     VARCHAR(50),
    user_agent     VARCHAR(1000),
    correlation_id VARCHAR(100),
    detail         JSONB,
    occurred_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_audits_document ON document_audits (document_id, tenant_id);
CREATE INDEX IF NOT EXISTS idx_audits_tenant   ON document_audits (tenant_id, occurred_at DESC);
