using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Xenia.Application.Assistant;

namespace Xenia.Infrastructure.Assistant;

internal sealed class StaticAssistantToolExecutor : IAssistantToolExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAssistantToolRegistry _registry;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public StaticAssistantToolExecutor(
        IAssistantToolRegistry registry,
        IHttpContextAccessor httpContextAccessor)
    {
        _registry = registry;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<AssistantToolExecutionResultDto> ExecuteAsync(
        AssistantToolExecutionRequestDto request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var tool = _registry.ListToolsForAgent(request.AgentKey)
            .FirstOrDefault(t => t.ToolKey.Equals(request.ToolKey, StringComparison.OrdinalIgnoreCase));

        if (tool is null)
        {
            return Task.FromResult(Denied("unauthorized_tool", "The requested assistant tool is not allowed for this agent."));
        }

        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal is null || !HasRequiredProducts(principal, tool.RequiredProductCodes) || !HasAnyRequiredPermission(principal, tool.RequiredPermissions))
        {
            return Task.FromResult(Denied("forbidden", "You are not authorized to use this assistant tool."));
        }

        if (tool.ConfirmationRequired)
        {
            return Task.FromResult(Denied("confirmation_required", "This assistant tool requires explicit user confirmation."));
        }

        var outputJson = request.ToolKey.Equals("tenant.context.summary", StringComparison.OrdinalIgnoreCase)
            ? JsonSerializer.Serialize(new
            {
                status = "available",
                note = "Tenant page context received. Sensitive record details must be fetched server-side by authorized tools.",
                input = SafeJsonObject(request.InputJson),
                context = SafeJsonObject(request.ContextJson),
            }, JsonOptions)
            : "{}";

        if (!request.ToolKey.Equals("tenant.context.summary", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new AssistantToolExecutionResultDto(
                false,
                "adapter_unavailable",
                outputJson,
                "This assistant tool is declared but its product adapter is not wired yet.",
                outputJson.Length));
        }

        if (outputJson.Length > tool.MaxOutputCharacters)
            outputJson = outputJson[..tool.MaxOutputCharacters];

        return Task.FromResult(new AssistantToolExecutionResultDto(
            true,
            "completed",
            outputJson,
            null,
            outputJson.Length));
    }

    private static AssistantToolExecutionResultDto Denied(string status, string safeError)
        => new(false, status, "{}", safeError, 2);

    private static bool HasAnyRequiredPermission(ClaimsPrincipal principal, IReadOnlyList<string> permissions)
    {
        if (permissions.Count == 0) return true;
        if (HasRole(principal, "PlatformAdmin")) return true;
        var granted = principal.FindAll("permissions").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return permissions.Any(granted.Contains);
    }

    private static bool HasRequiredProducts(ClaimsPrincipal principal, IReadOnlyList<string> productCodes)
    {
        if (productCodes.Count == 0) return true;
        if (HasRole(principal, "PlatformAdmin")) return true;

        var granted = principal.FindAll("product_codes")
            .Concat(principal.FindAll("enabled_products"))
            .Select(c => NormalizeProductCode(c.Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var role in principal.FindAll("product_roles"))
        {
            var productCode = role.Value.Split(':', 2)[0];
            if (!string.IsNullOrWhiteSpace(productCode))
                granted.Add(NormalizeProductCode(productCode));
        }

        return productCodes.All(code => granted.Contains(NormalizeProductCode(code)));
    }

    private static bool HasRole(ClaimsPrincipal principal, string role)
        => principal.IsInRole(role)
           || principal.HasClaim("role", role)
           || principal.HasClaim(ClaimTypes.Role, role);

    private static string NormalizeProductCode(string code)
    {
        var normalized = code.Trim().Replace("_", "", StringComparison.OrdinalIgnoreCase).Replace("-", "", StringComparison.OrdinalIgnoreCase);
        return normalized.ToUpperInvariant() switch
        {
            "SYNQAI" or "XENIA" => "XENIA",
            "SYNQLIEN" or "SYNQLIENS" => "SYNQLIEN",
            "SYNQCARECONNECT" or "CARECONNECT" => "CARECONNECT",
            _ => normalized.ToUpperInvariant(),
        };
    }

    private static string SafeJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "{}";
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? json : "{}";
        }
        catch (JsonException)
        {
            return "{}";
        }
    }
}
