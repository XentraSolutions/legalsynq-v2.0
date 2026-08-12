#!/usr/bin/env python3
"""
Empty out the BuildTargetModel bodies in all Designer.cs migration files.

WHY: Pomelo 8.0.2 throws NullReferenceException in FindCollectionMapping
for ANY Property<string> in BuildTargetModel — including GUID id columns.
The bug fires in RelationalTypeMappingSource.FindMappingWithConversion
because string implements IEnumerable<char>, so providerType=string
is incorrectly treated as a collection type.

FIX: make BuildTargetModel empty so FinalizeModel works on a zero-entity
model (no NullRef). MigrationsSqlGenerator.Generate then uses the
explicit Column<T>(type:...) annotations in each Up() method to produce
the correct DDL.
"""

import re
import os

MIGRATIONS_DIR = "apps/services/xenia/Xenia.Infrastructure/Persistence/Migrations"

designer_files = [
    "20260710000001_XeniaInitial.Designer.cs",
    "20260710000002_AddAdapterCriticality.Designer.cs",
    "20260710000003_AddEmailModule.Designer.cs",
    "20260710000004_AddSoftDeleteAndSettings.Designer.cs",
    "20260710000005_AddIngestionEngine.Designer.cs",
    "20260710000006_AddDurableSyncLock.Designer.cs",
]

# Match the BuildTargetModel method body and replace it with an empty body.
# Pattern: everything between "protected override void BuildTargetModel(ModelBuilder modelBuilder)"
# and the closing brace of the method.
BTM_PATTERN = re.compile(
    r'(protected override void BuildTargetModel\(ModelBuilder modelBuilder\)\s*\{)'
    r'.*?'           # non-greedy: match the body
    r'(#pragma warning restore 612, 618\s*\n\s*\})',  # closing pragma + closing brace
    re.DOTALL
)

REPLACEMENT = r'\1\n        }\n\n        \2'


def empty_btm(path):
    with open(path) as f:
        content = f.read()

    original = content

    # Simple approach: find the method signature, then replace everything
    # from the signature's '{' to the matching '#pragma warning restore' + '}'
    # at the end of the method.
    m = BTM_PATTERN.search(content)
    if m:
        new_content = BTM_PATTERN.sub(r'\1\n        }\n\n        \2', content, count=1)
        # The replacement above nests one extra '}' so let's do it more carefully
    
    # More reliable: replace the entire method body manually
    sig = "protected override void BuildTargetModel(ModelBuilder modelBuilder)"
    idx = content.find(sig)
    if idx == -1:
        print(f"  SKIP (no BuildTargetModel found): {os.path.basename(path)}")
        return False

    # Find opening brace of the method
    brace_start = content.find('{', idx)
    if brace_start == -1:
        print(f"  SKIP (no opening brace): {os.path.basename(path)}")
        return False

    # Walk the content to find the matching closing brace
    depth = 1
    pos = brace_start + 1
    while pos < len(content) and depth > 0:
        if content[pos] == '{':
            depth += 1
        elif content[pos] == '}':
            depth -= 1
        pos += 1
    # pos now points one past the closing brace

    # Build replacement: keep sig + opening brace, then immediately close
    method_indent = "        "  # 8 spaces (inside partial class)
    body_replacement = f"{sig}\n{method_indent}{{\n{method_indent}}}"

    new_content = (
        content[:idx]
        + body_replacement
        + content[pos:]
    )

    if new_content != original:
        with open(path, 'w') as f:
            f.write(new_content)
        print(f"  EMPTIED: {os.path.basename(path)}")
        return True
    else:
        print(f"  NO CHANGE: {os.path.basename(path)}")
        return False


print("\n=== Emptying BuildTargetModel bodies in Designer.cs files ===")
total = 0
for fname in designer_files:
    full = os.path.join(MIGRATIONS_DIR, fname)
    if os.path.exists(full):
        if empty_btm(full):
            total += 1
    else:
        print(f"  MISSING: {fname}")

print(f"\nDone. {total} file(s) modified.")
