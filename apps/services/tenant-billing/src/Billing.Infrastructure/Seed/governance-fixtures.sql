-- ============================================================
-- MS-BILL-ERP-FINAL — Deterministic governance seed fixtures
--
-- Authoritative SQL seed for the governance read surface in
-- non-production environments. Loaded by a FluentMigrator
-- profile (see seed-fixtures.README.md) only when ALL of:
--
--   ASPNETCORE_ENVIRONMENT in (Development, Test)
--   BILLING_GOVERNANCE_FIXTURES = true
--
-- The seed is IDEMPOTENT: every INSERT uses a deterministic
-- primary key from the fixture JSON under
-- scripts/src/governance-validation/fixtures/, and every row
-- is preceded by an existence check.
--
-- The seed is STRICTLY READ-SURFACE: it inserts ONLY rows that
-- are visible to the ERP-007 / ERP-008 governance read paths.
-- It does NOT touch the immutable accounting tables (invoices,
-- payments, adjustments, statements). It does NOT touch
-- replay/exports state. It does NOT mutate any tenant-owned
-- accounting figure.
--
-- Production safety: this file is NEVER executed in the
-- Production or Staging profile. The migration loader gates
-- execution on the environment AND the explicit env var.
-- ============================================================

-- Tenant scope: every row below is owned by the deterministic
-- test tenant id 00000000-0000-4000-8000-0000000000a1. Cross-
-- tenant aggregation is forbidden; the read endpoints are
-- session-scoped to this tenant only.

-- 1. Replay scenarios -----------------------------------------
INSERT INTO governance_replay_scenarios
    (export_id, invoice_id, fingerprint_prefix, window_days,
     outcome, replay_count, first_exported_utc, last_replay_utc,
     tenant_id)
SELECT '11111111-1111-4111-8111-111111111101',
       '22222222-2222-4222-8222-222222222201',
       'abcd1234ef56', 7, 'success', 2,
       '2026-01-02T08:00:00Z', '2026-01-04T08:00:00Z',
       '00000000-0000-4000-8000-0000000000a1'
WHERE NOT EXISTS (
    SELECT 1 FROM governance_replay_scenarios
    WHERE export_id = '11111111-1111-4111-8111-111111111101'
);

INSERT INTO governance_replay_scenarios
    (export_id, invoice_id, fingerprint_prefix, window_days,
     outcome, replay_count, first_exported_utc, last_replay_utc,
     tenant_id)
SELECT '11111111-1111-4111-8111-111111111102',
       '22222222-2222-4222-8222-222222222202',
       '00ff11ee22dd', 7, 'success', 5,
       '2026-01-02T09:00:00Z', '2026-01-09T09:00:00Z',
       '00000000-0000-4000-8000-0000000000a1'
WHERE NOT EXISTS (
    SELECT 1 FROM governance_replay_scenarios
    WHERE export_id = '11111111-1111-4111-8111-111111111102'
);

-- 2. Duplicate-export suppression -----------------------------
INSERT INTO governance_duplicate_exports
    (export_id, invoice_id, fingerprint_prefix,
     duplicate_of_export_id, detected_utc, outcome, tenant_id)
SELECT '11111111-1111-4111-8111-111111111201',
       '22222222-2222-4222-8222-222222222201',
       'abcd1234ef56',
       '11111111-1111-4111-8111-111111111101',
       '2026-01-03T08:00:00Z', 'duplicate-suppressed',
       '00000000-0000-4000-8000-0000000000a1'
WHERE NOT EXISTS (
    SELECT 1 FROM governance_duplicate_exports
    WHERE export_id = '11111111-1111-4111-8111-111111111201'
);

-- 3. Unresolved customer mappings -----------------------------
INSERT INTO governance_unresolved_mappings
    (mapping_id, billing_customer_id, qbo_customer_id, status,
     age_days, first_seen_utc, tenant_id)
SELECT '33333333-3333-4333-8333-333333333301',
       '44444444-4444-4444-8444-444444444401',
       NULL, 'unresolved', 5, '2026-01-09T00:00:00Z',
       '00000000-0000-4000-8000-0000000000a1'
WHERE NOT EXISTS (
    SELECT 1 FROM governance_unresolved_mappings
    WHERE mapping_id = '33333333-3333-4333-8333-333333333301'
);

INSERT INTO governance_unresolved_mappings
    (mapping_id, billing_customer_id, qbo_customer_id, status,
     age_days, first_seen_utc, tenant_id)
SELECT '33333333-3333-4333-8333-333333333302',
       '44444444-4444-4444-8444-444444444402',
       NULL, 'unresolved', 32, '2025-12-13T00:00:00Z',
       '00000000-0000-4000-8000-0000000000a1'
WHERE NOT EXISTS (
    SELECT 1 FROM governance_unresolved_mappings
    WHERE mapping_id = '33333333-3333-4333-8333-333333333302'
);

-- 4. Remediation history (immutable) --------------------------
-- The remediation_id rows are append-only. The loader inserts
-- them ONCE; subsequent runs are no-ops thanks to the
-- existence check. This preserves the audit-trail invariant.
INSERT INTO governance_remediation_history
    (remediation_id, mapping_id, actor_role, performed_utc,
     kind, outcome, notes_redacted_hash, tenant_id)
SELECT '55555555-5555-4555-8555-555555555501',
       '33333333-3333-4333-8333-333333333302',
       'tenant-admin', '2025-12-20T12:00:00Z',
       'manual-mapping-attached', 'resolved',
       'deadbeefcafebabe1234567890abcdefdeadbeefcafebabe1234567890abcdef',
       '00000000-0000-4000-8000-0000000000a1'
WHERE NOT EXISTS (
    SELECT 1 FROM governance_remediation_history
    WHERE remediation_id = '55555555-5555-4555-8555-555555555501'
);

-- 5. Drift indicators -----------------------------------------
INSERT INTO governance_drift_indicators
    (indicator_id, kind, billing_value, downstream_value,
     absolute_drift_minor, observed_utc, severity, tenant_id)
SELECT '66666666-6666-4666-8666-666666666601',
       'balance-divergence', '1234.56', '1234.50', 6,
       '2026-01-09T06:00:00Z', 'low',
       '00000000-0000-4000-8000-0000000000a1'
WHERE NOT EXISTS (
    SELECT 1 FROM governance_drift_indicators
    WHERE indicator_id = '66666666-6666-4666-8666-666666666601'
);

INSERT INTO governance_drift_indicators
    (indicator_id, kind, billing_value, downstream_value,
     absolute_drift_minor, observed_utc, severity, tenant_id)
SELECT '66666666-6666-4666-8666-666666666602',
       'missing-downstream-record', 'exists', 'missing', NULL,
       '2026-01-09T07:00:00Z', 'high',
       '00000000-0000-4000-8000-0000000000a1'
WHERE NOT EXISTS (
    SELECT 1 FROM governance_drift_indicators
    WHERE indicator_id = '66666666-6666-4666-8666-666666666602'
);

-- 6. Audit trail (append-only) --------------------------------
INSERT INTO governance_audit_trail
    (audit_id, actor_role, action, occurred_utc, window_days,
     tenant_id)
SELECT '77777777-7777-4777-8777-777777777701',
       'tenant-admin', 'governance-summary-viewed',
       '2026-01-09T08:00:00Z', 7,
       '00000000-0000-4000-8000-0000000000a1'
WHERE NOT EXISTS (
    SELECT 1 FROM governance_audit_trail
    WHERE audit_id = '77777777-7777-4777-8777-777777777701'
);

INSERT INTO governance_audit_trail
    (audit_id, actor_role, action, occurred_utc, window_days,
     tenant_id)
SELECT '77777777-7777-4777-8777-777777777702',
       'tenant-admin', 'governance-export-csv-downloaded',
       '2026-01-09T08:01:00Z', 7,
       '00000000-0000-4000-8000-0000000000a1'
WHERE NOT EXISTS (
    SELECT 1 FROM governance_audit_trail
    WHERE audit_id = '77777777-7777-4777-8777-777777777702'
);

INSERT INTO governance_audit_trail
    (audit_id, actor_role, action, occurred_utc, window_days,
     tenant_id)
SELECT '77777777-7777-4777-8777-777777777703',
       'tenant-admin', 'remediation-mapping-attached',
       '2025-12-20T12:00:00Z', NULL,
       '00000000-0000-4000-8000-0000000000a1'
WHERE NOT EXISTS (
    SELECT 1 FROM governance_audit_trail
    WHERE audit_id = '77777777-7777-4777-8777-777777777703'
);

-- ============================================================
-- End of seed. The loader logs sha256 of this file at startup
-- so the operator can correlate seeded fixtures with the
-- evidence manifest produced by the harness.
-- ============================================================
