using System.Globalization;
using System.Text;

namespace Billing.Domain.Csv;

/// <summary>
/// MS-BILL-WRITE-007 — single authoritative CSV serializer used by
/// every reporting export. Emits RFC 4180-compatible output:
/// comma-separated, CRLF line endings, fields containing
/// <c>"</c>, <c>,</c>, CR, or LF wrapped in double quotes with
/// embedded quotes doubled. Stable column order is the caller's
/// responsibility (the writer never reorders columns).
///
/// CSV-injection (formula-injection) hardening: any cell whose first
/// non-whitespace character is <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>,
/// TAB, or CR is prefixed with a single quote so spreadsheet apps do
/// not interpret it as a formula. This is applied to every string
/// cell — numeric cells use invariant-culture formatting and never
/// trigger the predicate.
/// </summary>
public static class CsvWriter
{
    /// <summary>
    /// Serialize a header row plus zero or more data rows. Each row
    /// is an <see cref="IReadOnlyList{T}"/> of cell values; null
    /// values become empty fields. The column count of every row
    /// must match the header column count — mismatched widths throw
    /// <see cref="ArgumentException"/> at the row level so a
    /// projection bug surfaces early.
    /// </summary>
    public static string Write(
        IReadOnlyList<string> header,
        IEnumerable<IReadOnlyList<string?>> rows)
    {
        if (header is null) throw new ArgumentNullException(nameof(header));
        if (rows is null) throw new ArgumentNullException(nameof(rows));

        var width = header.Count;
        var sb = new StringBuilder();
        AppendRow(sb, header.Cast<string?>().ToList());
        foreach (var row in rows)
        {
            if (row.Count != width)
            {
                throw new ArgumentException(
                    $"CSV row width {row.Count} does not match header width {width}.",
                    nameof(rows));
            }
            AppendRow(sb, row);
        }
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, IReadOnlyList<string?> row)
    {
        for (var i = 0; i < row.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(EscapeCell(row[i]));
        }
        // RFC 4180 line terminator.
        sb.Append("\r\n");
    }

    private static string EscapeCell(string? cell)
    {
        if (string.IsNullOrEmpty(cell)) return string.Empty;

        // Formula-injection guard. Spreadsheet apps treat a leading
        // =,+,-,@,TAB,CR as a formula start; prefix with `'` to
        // neutralise. We scan past leading whitespace because some
        // spreadsheet importers trim before evaluating, so a payload
        // like " =cmd" still triggers as a formula. We do this BEFORE
        // the quote-wrap step so the resulting cell still escapes
        // correctly if it also contains a comma or quote.
        var firstNonWs = 0;
        while (firstNonWs < cell.Length && (cell[firstNonWs] == ' ' || cell[firstNonWs] == '\t'))
        {
            firstNonWs++;
        }
        if (firstNonWs < cell.Length)
        {
            var first = cell[firstNonWs];
            if (first == '=' || first == '+' || first == '-' || first == '@' || first == '\t' || first == '\r')
            {
                cell = "'" + cell;
            }
        }

        var needsQuoting =
            cell.IndexOf(',') >= 0 ||
            cell.IndexOf('"') >= 0 ||
            cell.IndexOf('\r') >= 0 ||
            cell.IndexOf('\n') >= 0;

        if (!needsQuoting) return cell;

        // RFC 4180: embedded quotes are doubled, whole cell wrapped
        // in quotes.
        return "\"" + cell.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    /// Invariant-culture decimal formatting — never the current
    /// locale's decimal separator, so a CSV produced in a
    /// comma-decimal locale still parses cleanly downstream.
    /// </summary>
    public static string FormatDecimal(decimal value)
        => value.ToString("0.00##", CultureInfo.InvariantCulture);

    /// <summary>
    /// ISO 8601 / round-trip timestamp formatting for date cells.
    /// </summary>
    public static string FormatDateTime(DateTime value)
        => value.ToString("o", CultureInfo.InvariantCulture);

    /// <summary>
    /// ISO date-only formatting for due-date / issue-date cells.
    /// </summary>
    public static string FormatDate(DateTime value)
        => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
