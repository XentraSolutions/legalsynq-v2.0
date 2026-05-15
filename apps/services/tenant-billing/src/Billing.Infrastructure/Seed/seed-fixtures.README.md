# Governance fixture loader (MS-BILL-ERP-FINAL)

This directory holds the **deterministic, non-production** governance
fixture seed for the ERP-007 / ERP-008 read surface. It is consumed
by an env-gated FluentMigrator profile (see below). The loader is
strictly diagnostic: it never mutates accounting tables, never
mutates replay state, and never runs in Production or Staging.

## Triggering the loader

The `GovernanceFixturesProfile` migration class executes the
`governance-fixtures.sql` script when **all** of the following are
true:

| Gate                            | Required value          |
|---------------------------------|-------------------------|
| `ASPNETCORE_ENVIRONMENT`        | `Development` or `Test` |
| `BILLING_GOVERNANCE_FIXTURES`   | `true`                  |
| FluentMigrator profile          | `governance-fixtures`   |

Any other combination is a no-op. There is no flag and no
configuration value that can run the seed in Production.

## Files

- `governance-fixtures.sql` — authoritative seed. Idempotent
  inserts only. Every row has an `IF NOT EXISTS` guard so the
  loader can be re-run safely.
- `seed-fixtures.README.md` — this document.

## Companion JSON

The same fixtures are mirrored as JSON at
`scripts/src/governance-validation/fixtures/*.json` for the
read-only TypeScript harness. The two surfaces share fixture IDs,
timestamps, fingerprints, and the deterministic test tenant id
`00000000-0000-4000-8000-0000000000a1` so that an offline
contract assertion and a live database read produce the same
visible state.

## Migration class status

The C# migration class (`GovernanceFixturesProfile.cs`) is **not
committed in this prompt**. The container running this checkout
does not have the .NET 8 SDK, so the class would ship un-compiled
and un-tested. The SQL script in this directory is the
authoritative seed; the migration class is a follow-up in
MS-BILL-ERP-FINAL-002, gated on SDK availability. A reference
skeleton appears below for the operator to lift verbatim.

```csharp
[Migration(202605140001, "Governance fixture loader (DEV/TEST only)")]
[Profile("governance-fixtures")]
public class GovernanceFixturesProfile : Migration
{
    public override void Up()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var flag = Environment.GetEnvironmentVariable("BILLING_GOVERNANCE_FIXTURES");
        if (env != "Development" && env != "Test") return;
        if (!string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase)) return;
        Execute.EmbeddedScript("governance-fixtures.sql");
    }

    public override void Down()
    {
        // Intentionally empty: deterministic fixtures are never
        // unwound. Drop the database in a non-production env if
        // a clean slate is required.
    }
}
```

## Forbidden directions

- This loader MUST NOT touch invoices, payments, adjustments,
  statements, exports, replay state, or any column owned by the
  immutable accounting projection.
- This loader MUST NOT delete rows (no `Down()` content).
- This loader MUST NOT seed cross-tenant rows.
- This loader MUST NOT register a runtime scheduler, queue, or
  outbox to refresh fixtures.
