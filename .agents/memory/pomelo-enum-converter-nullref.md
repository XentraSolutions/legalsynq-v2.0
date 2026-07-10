---
name: Pomelo enum converter NullRef
description: HasConversion<string>() on enum properties crashes Pomelo 8 + EF 8 + .NET 10 during migration and service startup. Fix and workarounds documented here.
---

# Pomelo enum converter NullRef — EF 8 + Pomelo + .NET 10

## The rule
Never use `HasConversion<string>()` on enum properties when using Pomelo MySQL with EF 8 and .NET 10. Always use an explicit `EnumToStringConverter<T>()` instance.

## Why
`HasConversion<string>()` triggers `RelationalTypeMappingSource.FindMappingWithConversion` which internally calls `FindCollectionMapping` because `string : IEnumerable<char>`. Inside `FindCollectionMapping`, Pomelo's `FindMapping(char)` returns `null` (MySQL has no native char scalar type). The calling code dereferences this null without a guard → `NullReferenceException`.

This crashes:
- `dotnet ef database update` (in `MySqlMigrator.GenerateUpSql`)
- `dotnet ef migrations script` (same path)
- Service `MigrateAsync()` at startup (same path through `Migrator.GenerateUpSql`)

It does NOT crash normal read/write EF operations or InMemory tests.

## How to apply
In every EF entity type configuration file that uses enum properties:
```csharp
// ❌ Do not use — NullRef in Pomelo 8 + EF 8 + .NET 10
builder.Property(e => e.MyEnum).HasConversion<string>()...

// ✅ Use explicit instance — bypasses generic type-mapper lookup
private static readonly EnumToStringConverter<MyEnum> _myEnumConverter = new();
builder.Property(e => e.MyEnum).HasConversion(_myEnumConverter)...
```

Store converters as static fields on the configuration class (one per enum type used in that entity). `EnumToStringConverter<T>` is in `Microsoft.EntityFrameworkCore.Storage.ValueConversion`.

## Found in
`Xenia.Infrastructure/Persistence/Configurations/PlatformAdapterConfiguration.cs` — fixed all 4 enum properties (AdapterType, Criticality, ConfigurationStatus, AvailabilityStatus, HealthStatus — shared `_adapterStatusConverter` for the status trio).

## Enum CLR default and sentinels
If you have a database-generated default and an enum property, EF warns about sentinel conflicts. Fix: make the CLR default enum value (= 0) the one that matches the database default. E.g. if the DB default is `'Optional'`, set `Optional = 0` in the enum.
