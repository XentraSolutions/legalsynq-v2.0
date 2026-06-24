using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IProviderImportParser
{
    Task<ProviderImportParseResult> ParseAsync(Stream stream, string fileName, CancellationToken ct = default);
}
