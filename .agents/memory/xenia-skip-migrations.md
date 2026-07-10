---
name: Xenia SkipMigrations + schema apply
description: How to apply Xenia schema and start the service when EF tooling fails or docker exec is blocked.
---

# Xenia SkipMigrations + Schema Application

## SkipMigrations escape-hatch
`XeniaMigrationsHostedService` checks `Xenia:SkipMigrations` (env: `Xenia__SkipMigrations=true`).
When true, `MigrateAsync()` is skipped and a warning is logged. The service starts against whatever schema is already in the DB.

## When to use
- Schema was pre-applied by DBA or external migration tool
- CI smoke tests using a pre-seeded Docker DB
- Replit sandbox where `docker exec` is OCI-blocked (cannot run `mysql` inside the container)

## Applying schema when EF tools fail
EF tools (`dotnet ef database update`) crash with NullRef on Pomelo 8 + EF 8 + .NET 10 for enum properties. Use pymysql instead:
```python
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=33060, user='xenia', password='xeniatest', database='xenia_test')
cursor = conn.cursor()
cursor.execute("CREATE TABLE IF NOT EXISTS xn_platform_adapters (...)")
cursor.execute("INSERT IGNORE INTO __EFMigrationsHistory ...")
```

## Idempotent SQL script
`apps/services/xenia/Xenia.Infrastructure/Persistence/Migrations/xenia_schema_manual.sql` — apply this script to create all Xenia tables and record both migrations in `__EFMigrationsHistory`. Safe to run multiple times (IF NOT EXISTS / INSERT IGNORE).

## Docker MySQL in Replit
- Container name: `xenia-test-mysql`
- Host port: 33060 → container port 3306
- DB: xenia_test, user: xenia, password: xeniatest
- `docker exec` OCI-blocked in Replit sandbox → use host-side pymysql or mysql client via TCP to 127.0.0.1:33060
