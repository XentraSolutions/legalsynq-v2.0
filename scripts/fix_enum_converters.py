#!/usr/bin/env python3
"""
Fix all EnumToStringConverter usages in Xenia EF config and migration files.
Switches all enum storage from varchar (string) to int to avoid Pomelo 8.0.2
FindCollectionMapping NullReferenceException.
"""

import re
import os

CONFIGS_DIR = "apps/services/xenia/Xenia.Infrastructure/Persistence/Configurations"
MIGRATIONS_DIR = "apps/services/xenia/Xenia.Infrastructure/Persistence/Migrations"


def fix_config_file(path):
    with open(path) as f:
        content = f.read()

    original = content

    # 1. Remove EnumToStringConverter<T> static field declaration lines
    #    Allow zero or more whitespace between > and field name (handles 'EnumToString<T>_fieldName')
    content = re.sub(
        r'[ \t]+private static readonly EnumToStringConverter<[^>]+>\s*\w+\s*=\s*new\(\);\n',
        '',
        content,
        flags=re.MULTILINE
    )

    # 2. Remove custom ValueConverter<Enum?, string?> field declaration lines (multi-line up to 6 lines)
    content = re.sub(
        r'[ \t]+private static readonly ValueConverter<[^>]*string\?>[^\n]+\n(?:[^\n]*\n){0,5}[^\n]*\);\n',
        '',
        content,
        flags=re.MULTILINE
    )

    # 3. Remove standalone .HasConversion(...) lines.
    #    Use .* (greedy, no DOTALL) so it captures everything on the line including nested ()
    content = re.sub(
        r'^[ \t]+\.HasConversion\(.*\)\n',
        '',
        content,
        flags=re.MULTILINE
    )

    # 4. Remove standalone .HasColumnType("varchar(...)") lines left from previous attempt
    content = re.sub(
        r'^[ \t]+\.HasColumnType\(\$?"varchar\([^)]*\)"\)\n',
        '',
        content,
        flags=re.MULTILINE
    )

    # 5. Remove inline .HasConversion(...).HasMaxLength(N) — greedy inside HasConversion
    content = re.sub(
        r'\.HasConversion\(.*?\)\.HasMaxLength\(\d+\)',
        '',
        content
    )

    # 6. Remove inline .HasConversion(...).HasColumnType("varchar(...)") 
    content = re.sub(
        r'\.HasConversion\(.*?\)\.HasColumnType\(\$?"varchar\([^)]*\)"\)',
        '',
        content
    )

    # 7. Remove unused imports if no ValueConversion types remain
    if 'ValueConverter' not in content and 'EnumToStringConverter' not in content:
        content = re.sub(
            r'^using Microsoft\.EntityFrameworkCore\.Storage\.ValueConversion;\n',
            '',
            content,
            flags=re.MULTILINE
        )

    # 8. Clean up triple blank lines
    content = re.sub(r'\n\n\n+', '\n\n', content)

    if content != original:
        with open(path, 'w') as f:
            f.write(content)
        print(f"  UPDATED: {os.path.basename(path)}")
    else:
        print(f"  NO CHANGE: {os.path.basename(path)}")

    return content != original


def fix_migration_column(content, col_name):
    """
    Change table.Column<string>(...) → table.Column<int>(...) for one column name.
    Handles both 'col = table.Column<string>(...)' and multi-line AddColumn<string>(...).
    """
    # Pattern 1: inline column assignment in CreateTable lambda
    pattern1 = r'(\b' + re.escape(col_name) + r'\b\s*=\s*table\.Column<)string(>)(\([^)]*\))'

    def replacer1(m):
        prefix, gt, args = m.group(1), m.group(2), m.group(3)
        args = re.sub(r',?\s*maxLength:\s*\d+', '', args)
        args = re.sub(r',?\s*type:\s*"varchar\([^"]*\)"', '', args)
        args = re.sub(r',?\s*defaultValue:\s*"[^"]*"', '', args)
        inner = args[1:-1].strip().lstrip(',').strip()
        return f'{prefix}int{gt}({inner})'

    new_content = re.sub(pattern1, replacer1, content)
    changed = new_content != content
    content = new_content

    return content, changed


def fix_migration_addcolumn(content, col_name):
    """
    Handle multi-line AddColumn<string>( name: "col", ... ) pattern.
    Changes to AddColumn<int>( ... ) removing type/maxLength/string defaultValue.
    """
    # Match the entire AddColumn block for this specific column name
    # The block spans multiple lines from AddColumn<string>( to the closing );
    pattern = (
        r'(migrationBuilder\.AddColumn<)string(>\(\s*\n'
        r'(?:[^\n]*\n)*?'
        r'\s*name:\s*"' + re.escape(col_name) + r'"'
        r'(?:[^\n]*\n)*?'
        r'[^\n]*?\);)'
    )

    def replacer_add(m):
        block = m.group(0)
        block = block.replace('AddColumn<string>', 'AddColumn<int>', 1)
        # Remove type: "varchar(...)" line
        block = re.sub(r'\s*type:\s*"varchar\([^"]*\)",?\n', '\n', block)
        # Remove maxLength: N line
        block = re.sub(r'\s*maxLength:\s*\d+,?\n', '\n', block)
        # Remove defaultValue: "..." line
        block = re.sub(r'\s*defaultValue:\s*"[^"]*",?\n', '\n', block)
        # Clean trailing commas before ); 
        block = re.sub(r',(\s*\))', r'\1', block)
        return block

    new_content = re.sub(pattern, replacer_add, content, flags=re.DOTALL)
    return new_content, new_content != content


def fix_migration_file(path, enum_cols_by_table, addcol_cols=None):
    with open(path) as f:
        content = f.read()

    original = content
    changed_cols = []

    for table_name, cols in enum_cols_by_table.items():
        for col in cols:
            content, chg = fix_migration_column(content, col)
            if chg:
                changed_cols.append(f"{table_name}.{col}")

    # Handle AddColumn<string> patterns (used by M2)
    if addcol_cols:
        for col in addcol_cols:
            content, chg = fix_migration_addcolumn(content, col)
            if chg:
                changed_cols.append(f"AddColumn.{col}")

    if content != original:
        with open(path, 'w') as f:
            f.write(content)
        print(f"  SAVED: {os.path.basename(path)} — cols: {changed_cols}")
    else:
        print(f"  NO CHANGE: {os.path.basename(path)}")

    return content != original


# ─── Run config fixes ─────────────────────────────────────────────────────────

print("\n=== Fixing EF config files ===")
config_files = [
    "PlatformAdapterConfiguration.cs",
    "XeniaModuleConfiguration.cs",
    "XeniaConfigurationEntryConfiguration.cs",
    "AutomationRuntimeStateRecordConfiguration.cs",
    "AutomationVersionRecordConfiguration.cs",
    "AutomationConfigurationEntryConfiguration.cs",
    "AutomationRegistrationConfiguration.cs",
    "AutomationExecutionRecordConfiguration.cs",
    "AutomationDeadLetterRecordConfiguration.cs",
    "AutomationScheduleRecordConfiguration.cs",
    "EmailSourceConfiguration.cs",
    "EmailProviderSettingsConfiguration.cs",
    "EmailValidationHistoryConfiguration.cs",
    "EmailMessageConfiguration.cs",
    "EmailMessageRecipientConfiguration.cs",
    "EmailAttachmentReferenceConfiguration.cs",
    "EmailSyncStateConfiguration.cs",
    "EmailIngestionRunConfiguration.cs",
    "EmailOperationalAlertConfiguration.cs",
    "EmailRetentionRunConfiguration.cs",
]

for fname in config_files:
    full = os.path.join(CONFIGS_DIR, fname)
    if os.path.exists(full):
        fix_config_file(full)
    else:
        print(f"  MISSING: {fname}")


# ─── Migration column tables ──────────────────────────────────────────────────

M1_COLS = {
    "xn_modules": ["status"],
    "xn_platform_adapters": ["adapter_type", "configuration_status", "availability_status", "health_status"],
    "xn_configuration": ["scope_type"],
}

M2_COLS = {}  # M2 uses AddColumn, not Column<string> inside CreateTable
M2_ADDCOL = ["criticality"]

M3_COLS = {
    "xn_email_sources": ["provider_type", "auth_type", "status", "health_status", "validation_status"],
    "xn_email_provider_settings": ["provider_type"],
    "xn_email_validation_history": ["provider_type", "result"],
}

M5_COLS = {
    "xn_email_messages": ["provider_type", "importance", "body_type", "import_status", "processing_state"],
    "xn_email_recipients": ["recipient_type"],
    "xn_email_attachment_references": ["dispatch_status"],
    "xn_email_sync_state": ["provider_type", "cursor_type"],
    "xn_email_ingestion_runs": ["trigger_type", "status"],
}

M7_COLS = {
    "xn_email_operational_alerts": ["provider_type", "alert_type", "severity", "status"],
    "xn_email_retention_runs": ["mode", "status"],
}

M8_COLS = {
    "xn_automation_registry": ["lifecycle_status"],
    "xn_automation_versions": ["status"],
    "xn_automation_configuration": ["scope_type"],
    "xn_automation_runtime_state": ["global_state", "tenant_state", "lifecycle_state", "health_state"],
    "xn_automation_executions": ["trigger_type", "status"],
    "xn_automation_dead_letters": ["trigger_type", "status"],
    "xn_automation_schedules": ["schedule_type", "misfire_policy", "concurrency_policy"],
}

print("\n=== Fixing migration files ===")

migrations = [
    ("20260710000001_XeniaInitial.cs",           M1_COLS, None),
    ("20260710000002_AddAdapterCriticality.cs",  M2_COLS, M2_ADDCOL),
    ("20260710000003_AddEmailModule.cs",         M3_COLS, None),
    ("20260710000005_AddIngestionEngine.cs",     M5_COLS, None),
    ("20260710000007_AddOperationsDomain.cs",    M7_COLS, None),
    ("20260710000008_AddDurableAutomationState.cs", M8_COLS, None),
]

for fname, cols, addcols in migrations:
    full = os.path.join(MIGRATIONS_DIR, fname)
    if os.path.exists(full):
        fix_migration_file(full, cols, addcols)
    else:
        print(f"  MISSING: {fname}")

print("\nDone.")
