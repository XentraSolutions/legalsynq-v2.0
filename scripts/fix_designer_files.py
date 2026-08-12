#!/usr/bin/env python3
"""
Fix Designer.cs BuildTargetModel files so that EF enum properties
that were stored as varchar (Property<string>) are now stored as int.

The Pomelo 8.0.2 bug: FindCollectionMapping NullRef fires for ANY
Property<string>(). After our enum-to-int migration, the Designer
models must match (use Property<int> for enum columns).

Strategy: for each known enum property name, change:
  b.Property<string>("PropName") → b.Property<int>("PropName")
  and remove .HasMaxLength(N) / .HasColumnType(varchar) on the same line.
"""

import re
import os

MIGRATIONS_DIR = "apps/services/xenia/Xenia.Infrastructure/Persistence/Migrations"

# All known enum property names stored as string in Designer.cs
# Derived from the complete list of enum columns across all migrations.
# Organised by "property name in C# entity class" (PascalCase, as EF records them).
ENUM_PROPERTY_NAMES = {
    # Core / adapters / configuration (M1)
    "Status",
    "AdapterType",
    "AvailabilityStatus",
    "ConfigurationStatus",
    "HealthStatus",
    "ScopeType",

    # M2
    "Criticality",

    # Email module (M3)
    "ProviderType",
    "AuthType",
    "ValidationStatus",
    "ValidationType",
    "Result",

    # Ingestion engine (M5)
    "Importance",
    "BodyType",
    "ImportStatus",
    "ProcessingState",
    "RecipientType",
    "DispatchStatus",
    "CursorType",
    "TriggerType",

    # Operations domain (M7) — but Designer.cs only exists up to M6
    # These are listed for completeness; M7/M8 have no Designer.cs files
    "AlertType",
    "Severity",
    "Mode",
}

designer_files = [
    "20260710000001_XeniaInitial.Designer.cs",
    "20260710000002_AddAdapterCriticality.Designer.cs",
    "20260710000003_AddEmailModule.Designer.cs",
    "20260710000004_AddSoftDeleteAndSettings.Designer.cs",
    "20260710000005_AddIngestionEngine.Designer.cs",
    "20260710000006_AddDurableSyncLock.Designer.cs",
]


def fix_designer_line(line):
    """
    For a single line from BuildTargetModel, if it contains
    Property<string>("KnownEnumName") change it to Property<int>.
    """
    # Check if this line has Property<string>("SomeName")
    m = re.search(r'b\.Property<string>\("([^"]+)"\)', line)
    if not m:
        # Also check nullable: Property<string?>("SomeName")
        m = re.search(r'b\.Property<string\?>\("([^"]+)"\)', line)
        if not m:
            return line, False
        prop_name = m.group(1)
        if prop_name not in ENUM_PROPERTY_NAMES:
            return line, False
        # Change string? to int?
        line = line.replace(f'Property<string?>("{ prop_name}")', f'Property<int?>("{ prop_name}")')
        line = line.replace(f'b.Property<string?>("{prop_name}")', f'b.Property<int?>("{prop_name}")')
    else:
        prop_name = m.group(1)
        if prop_name not in ENUM_PROPERTY_NAMES:
            return line, False
        line = line.replace(f'Property<string>("{prop_name}")', f'Property<int>("{prop_name}")')

    # Remove .HasMaxLength(N) from the same line
    line = re.sub(r'\.HasMaxLength\(\d+\)', '', line)

    # Remove .HasColumnType("varchar(...)") from the same line
    line = re.sub(r'\.HasColumnType\(\$?"varchar\([^)]*\)"\)', '', line)

    # Clean up extra spaces in method chains (e.g. ".IsRequired().." → ".IsRequired().")
    line = re.sub(r'\.\s+\.', '..', line)

    return line, True


def fix_designer_file(path):
    with open(path) as f:
        lines = f.readlines()

    original = ''.join(lines)
    changed_props = []

    for i, line in enumerate(lines):
        new_line, changed = fix_designer_line(line)
        if changed:
            lines[i] = new_line
            m = re.search(r'Property<int\??>\"([^"]+)\"', new_line)
            if m:
                changed_props.append(m.group(1))

    content = ''.join(lines)
    if content != original:
        with open(path, 'w') as f:
            f.write(content)
        print(f"  UPDATED: {os.path.basename(path)} → props: {changed_props}")
    else:
        print(f"  NO CHANGE: {os.path.basename(path)}")

    return content != original


print("\n=== Fixing Designer.cs BuildTargetModel files ===")
for fname in designer_files:
    full = os.path.join(MIGRATIONS_DIR, fname)
    if os.path.exists(full):
        fix_designer_file(full)
    else:
        print(f"  MISSING: {fname}")

print("\nDone.")
