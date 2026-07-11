---
name: Xenia MySQL index key-length limits
description: Three M5 indexes exceed MySQL 8 InnoDB's 3072-byte key limit under utf8mb4 (4 bytes/char); prefix workarounds applied.
---

## Rule

When adding indexes to tables in the `xenia` schema that include large VARCHAR columns, verify total key size:
- utf8mb4: 4 bytes per character
- InnoDB max key length: 3072 bytes
- CHAR(36) = 144 bytes; VARCHAR(N) full = N×4+2 bytes

**Why:** Three indexes in `20260710000005_AddIngestionEngine.cs` exceeded the limit:

| Index | Column | Raw bytes | Fix |
|---|---|---|---|
| `ux_email_messages_provider_unique` | `provider_message_id VARCHAR(1024)` | 144+144+4+4096=4388 | prefix `(600)` → 2692 ✓ |
| `ix_email_messages_internet_message_id` | `internet_message_id VARCHAR(998)` | 144+3992=4136 | prefix `(700)` → 2944 ✓ |
| `ix_email_attachments_provider_id` | `provider_attachment_id VARCHAR(1024)` | 144+144+4096=4384 | prefix `(600)` → 2692 ✓ |

Also: `xn_email_messages.reply_to_addresses` was `VARCHAR(2000)`, which contributed to the 65535-byte MySQL row-size limit being exceeded. Changed to `TEXT` (off-page storage, doesn't count toward row limit). Same for `provider_metadata_json VARCHAR(8000)` → `TEXT`.

**How to apply:** For any future Xenia index on a column wider than ~750 chars in utf8mb4, use a column prefix: `INDEX (col(N))` where N×4 + sum of other key column sizes ≤ 3072.
