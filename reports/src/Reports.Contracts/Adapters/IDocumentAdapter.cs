namespace Reports.Contracts.Adapters;

public interface IDocumentAdapter
{
    Task<string> StoreReportAsync(string tenantId, string fileName, byte[] content, string mimeType, CancellationToken ct = default);
    Task<byte[]?> RetrieveReportAsync(string documentId, CancellationToken ct = default);
}
