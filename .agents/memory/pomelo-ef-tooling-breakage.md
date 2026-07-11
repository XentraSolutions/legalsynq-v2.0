---
name: Pomelo 8.0.2 EF tooling breakage
description: dotnet ef database update is permanently broken for the Xenia schema under Pomelo 8.0.2; use the raw-SQL runner instead.
---

## Rule

Never use `dotnet ef database update` for Xenia migrations while Pomelo 8.0.2 is pinned. Use `scripts/apply_xenia_migrations_raw.py` instead.

**Why:** `MySqlMigrator.GenerateUpSql` calls `FinalizeModel` on the live `XeniaDbContext` model, which internally triggers `FindCollectionMapping` for every property whose CLR type is `string`. Under Pomelo 8.0.2 this throws `NullReferenceException` unconditionally — the exception is not related to migration content. The same crash fires even on a trivially empty migration. This is a Pomelo bug fixed in 8.0.3+.

The workaround (`apply_xenia_migrations_raw.py`) applies all 8 migrations as raw MySQL DDL via pymysql, then writes to `__EFMigrationsHistory` directly.

**How to apply:** When any Xenia migration needs to be applied to a MySQL target:
1. Run `python3 scripts/apply_xenia_migrations_raw.py`
2. The script is idempotent — already-applied migrations are skipped.
3. To restore `dotnet ef database update`, upgrade Pomelo to ≥ 8.0.3.

**Designer.cs workaround:** All `*Migration.Designer.cs` `BuildTargetModel()` bodies have been emptied to ~20-line stubs via `scripts/empty_designer_target_models.py`. This prevents EF tool from re-triggering the NullRef during model snapshot validation.
