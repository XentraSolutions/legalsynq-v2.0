using System.Text.Json;

namespace Liens.Application.DTOs;

public sealed class DIYReportConfigResponse
{
    public Guid     Id           { get; init; }
    public Guid     TenantId     { get; init; }
    public Guid     UserId       { get; init; }
    public string   Name         { get; init; } = string.Empty;
    public JsonElement Config    { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class SaveDIYReportRequest
{
    public string      Name   { get; init; } = string.Empty;
    public JsonElement Config { get; init; }
}

public sealed class DIYReportRunRequest
{
    public JsonElement Config   { get; init; }
    public int         Page     { get; init; } = 1;
    public int         Limit    { get; init; } = 50;
    public string?     SortBy   { get; init; }
    public string?     SortDir  { get; init; }
}
