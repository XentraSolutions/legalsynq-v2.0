namespace Billing.Domain.Accounting.Erp.BulkImport;

/// <summary>
/// MS-BILL-ERP-006 — port for the CSV → typed-row adapter. Lives in
/// the domain layer (so the service can be unit-tested without an
/// HttpContext) but the only concrete implementation
/// (<c>CsvBulkMappingImportParser</c>) lives in the infrastructure
/// layer alongside the controller's multipart binding.
///
/// <para>
/// The parser MUST be safe to call with any operator-supplied byte
/// stream. It MUST NEVER throw on malformed input — instead it
/// returns a <see cref="ParsedCsvDocument"/> whose
/// <see cref="ParsedCsvDocument.DocumentIssues"/> list explains
/// what was rejected and whose <see cref="CsvParsedRow.IsMalformed"/>
/// flag marks individual rows that could not be safely split.
/// </para>
/// </summary>
public interface IBulkMappingImportParser
{
    /// <summary>
    /// Parse a CSV byte stream into typed rows. Implementations
    /// MUST enforce a hard byte cap and a hard row cap so a
    /// malicious or accidental upload cannot exhaust memory.
    /// </summary>
    Task<ParsedCsvDocument> ParseAsync(Stream csv, CancellationToken ct = default);
}
