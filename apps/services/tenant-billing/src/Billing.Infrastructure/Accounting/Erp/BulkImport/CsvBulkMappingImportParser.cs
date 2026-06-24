using System.Text;
using Billing.Domain.Accounting.Erp.BulkImport;

namespace Billing.Infrastructure.Accounting.Erp.BulkImport;

/// <summary>
/// MS-BILL-ERP-006 — concrete UTF-8 CSV adapter for the bulk
/// mapping import. RFC-4180-ish: supports quoted fields,
/// double-quote escapes inside quoted fields, and CRLF / LF /
/// bare-CR line endings. Header row is required and field tokens
/// are matched case-insensitively against a closed allowlist.
///
/// <para>
/// Hard caps:
/// </para>
/// <list type="bullet">
///   <item><c>MaxBodyBytes</c> — 1 MiB. Bodies larger than this
///         are rejected with a <c>MalformedCsvRow</c> document
///         issue and an empty row list.</item>
///   <item><c>MaxDataRows</c> — 5000. Excess rows are dropped and
///         a single <c>MalformedCsvRow</c> document issue records
///         the truncation.</item>
/// </list>
///
/// <para>
/// The parser NEVER throws on malformed input: structural errors
/// surface on
/// <see cref="ParsedCsvDocument.DocumentIssues"/> and per-row split
/// errors are flagged via <see cref="CsvParsedRow.IsMalformed"/>.
/// </para>
/// </summary>
public sealed class CsvBulkMappingImportParser : IBulkMappingImportParser
{
    public const int MaxBodyBytes = 1 * 1024 * 1024;
    public const int MaxDataRows = 5_000;

    /// <summary>
    /// Closed allowlist of header tokens. Order is irrelevant — the
    /// parser maps each row's columns by header position resolved
    /// via this dictionary.
    /// </summary>
    private static readonly string[] AllowedHeaders =
    {
        "BillingCustomerId",
        "BillingCustomerName",
        "QuickBooksCustomerId",
        "QuickBooksDisplayName",
        "ExportMode",
        "Notes",
    };

    public async Task<ParsedCsvDocument> ParseAsync(Stream csv, CancellationToken ct = default)
    {
        if (csv is null) throw new ArgumentNullException(nameof(csv));

        // Read at most MaxBodyBytes + 1 to detect oversize without
        // pulling an unbounded buffer into memory.
        var ms = new MemoryStream();
        var buffer = new byte[8192];
        var total = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var read = await csv.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
            if (total > MaxBodyBytes)
            {
                return new ParsedCsvDocument(
                    Array.Empty<CsvParsedRow>(),
                    new[]
                    {
                        new BulkImportRowIssue(
                            BulkImportRowIssueCode.MalformedCsvRow,
                            $"CSV body exceeds the {MaxBodyBytes / 1024} KB limit."),
                    });
            }
            await ms.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
        }

        var bytes = ms.ToArray();
        // Strip UTF-8 BOM if present.
        var startIndex = 0;
        if (bytes.Length >= 3
            && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            startIndex = 3;
        }
        var text = Encoding.UTF8.GetString(bytes, startIndex, bytes.Length - startIndex);

        return Parse(text);
    }

    /// <summary>
    /// Pure-string entry point exposed for the unit tests in the
    /// test project (the controller never calls this directly).
    /// </summary>
    public ParsedCsvDocument Parse(string text)
    {
        var documentIssues = new List<BulkImportRowIssue>();
        if (string.IsNullOrWhiteSpace(text))
        {
            documentIssues.Add(new BulkImportRowIssue(
                BulkImportRowIssueCode.MalformedCsvRow,
                "CSV body was empty."));
            return new ParsedCsvDocument(Array.Empty<CsvParsedRow>(), documentIssues);
        }

        var lines = SplitLines(text);
        if (lines.Count == 0)
        {
            documentIssues.Add(new BulkImportRowIssue(
                BulkImportRowIssueCode.MalformedCsvRow,
                "CSV body had no rows."));
            return new ParsedCsvDocument(Array.Empty<CsvParsedRow>(), documentIssues);
        }

        var header = SplitFields(lines[0]);
        var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
        {
            var token = header[i].Trim();
            if (string.IsNullOrEmpty(token)) continue;
            if (!IsAllowedHeader(token))
            {
                documentIssues.Add(new BulkImportRowIssue(
                    BulkImportRowIssueCode.MalformedCsvRow,
                    $"Unknown header column '{token}'. Allowed columns: {string.Join(", ", AllowedHeaders)}."));
                continue;
            }
            // Last-write-wins on duplicate header tokens; we record
            // a document issue so the operator notices.
            if (headerIndex.ContainsKey(token))
            {
                documentIssues.Add(new BulkImportRowIssue(
                    BulkImportRowIssueCode.MalformedCsvRow,
                    $"Duplicate header column '{token}'."));
            }
            headerIndex[token] = i;
        }

        if (!headerIndex.ContainsKey("BillingCustomerId")
            || !headerIndex.ContainsKey("QuickBooksCustomerId"))
        {
            documentIssues.Add(new BulkImportRowIssue(
                BulkImportRowIssueCode.MalformedCsvRow,
                "Header is missing one or more required columns: BillingCustomerId, QuickBooksCustomerId."));
            return new ParsedCsvDocument(Array.Empty<CsvParsedRow>(), documentIssues);
        }

        var rows = new List<CsvParsedRow>(Math.Min(lines.Count - 1, MaxDataRows));
        for (var i = 1; i < lines.Count; i++)
        {
            if (rows.Count >= MaxDataRows)
            {
                documentIssues.Add(new BulkImportRowIssue(
                    BulkImportRowIssueCode.MalformedCsvRow,
                    $"CSV row count exceeds the {MaxDataRows} row limit; remaining rows were dropped."));
                break;
            }

            var line = lines[i];
            // Skip entirely blank lines without complaining; they
            // are common at the end of operator-edited spreadsheets.
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = SplitFields(line);
            var lineNumber = i + 1; // 1-indexed; header is line 1.

            if (fields.Count == 0)
            {
                rows.Add(new CsvParsedRow(
                    LineNumber: lineNumber,
                    BillingCustomerIdRaw: null,
                    BillingCustomerName: null,
                    QuickBooksCustomerIdRaw: null,
                    QuickBooksDisplayName: null,
                    ExportModeRaw: null,
                    Notes: null,
                    IsMalformed: true));
                continue;
            }

            string? Get(string token)
            {
                if (!headerIndex.TryGetValue(token, out var idx)) return null;
                if (idx >= fields.Count) return null;
                var v = fields[idx];
                return string.IsNullOrWhiteSpace(v) ? null : v;
            }

            rows.Add(new CsvParsedRow(
                LineNumber: lineNumber,
                BillingCustomerIdRaw: Get("BillingCustomerId"),
                BillingCustomerName: Get("BillingCustomerName"),
                QuickBooksCustomerIdRaw: Get("QuickBooksCustomerId"),
                QuickBooksDisplayName: Get("QuickBooksDisplayName"),
                ExportModeRaw: Get("ExportMode"),
                Notes: Get("Notes"),
                IsMalformed: false));
        }

        return new ParsedCsvDocument(rows, documentIssues);
    }

    private static bool IsAllowedHeader(string token)
    {
        foreach (var h in AllowedHeaders)
        {
            if (string.Equals(h, token, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// Split a CSV body into logical lines, respecting CRLF / LF /
    /// bare-CR endings and tolerating embedded newlines inside
    /// double-quoted fields.
    /// </summary>
    private static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                sb.Append(ch);
                continue;
            }
            if (!inQuotes && (ch == '\r' || ch == '\n'))
            {
                lines.Add(sb.ToString());
                sb.Clear();
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
                continue;
            }
            sb.Append(ch);
        }
        if (sb.Length > 0) lines.Add(sb.ToString());
        return lines;
    }

    /// <summary>
    /// Split a single CSV record into its fields, honouring quoted
    /// fields and the "" double-quote escape inside them.
    /// </summary>
    private static List<string> SplitFields(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }
            else
            {
                if (ch == '"')
                {
                    inQuotes = true;
                }
                else if (ch == ',')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(ch);
                }
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }
}
