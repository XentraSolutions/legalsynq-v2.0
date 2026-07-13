using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Xenia.Application.Assistant;

namespace Xenia.Infrastructure.Assistant;

internal sealed class StaticAssistantToolExecutor : IAssistantToolExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAssistantToolRegistry _registry;
    private readonly ICareConnectAssistantSource _careConnect;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public StaticAssistantToolExecutor(
        IAssistantToolRegistry registry,
        ICareConnectAssistantSource careConnect,
        IHttpContextAccessor httpContextAccessor)
    {
        _registry = registry;
        _careConnect = careConnect;
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

        if (request.ToolKey.Equals("tenant.context.summary", StringComparison.OrdinalIgnoreCase))
        {
            var outputJson = JsonSerializer.Serialize(new
            {
                status = "available",
                note = "Tenant page context received. Sensitive record details must be fetched server-side by authorized tools.",
                input = SafeJsonObject(request.InputJson),
                context = SafeJsonObject(request.ContextJson),
            }, JsonOptions);

            if (outputJson.Length > tool.MaxOutputCharacters)
                outputJson = outputJson[..tool.MaxOutputCharacters];

            return Task.FromResult(new AssistantToolExecutionResultDto(
                true,
                "completed",
                outputJson,
                null,
                outputJson.Length,
                []));
        }

        if (request.ToolKey.Equals("careconnect.referral.lookup", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteCareConnectReferralLookupAsync(tool, request, ct);
        }

        return Task.FromResult(new AssistantToolExecutionResultDto(
            false,
            "adapter_unavailable",
            "{}",
            "This assistant tool is declared but its product adapter is not wired yet.",
            2,
            []));
    }

    private static AssistantToolExecutionResultDto Denied(string status, string safeError)
        => new(false, status, "{}", safeError, 2, []);

    private async Task<AssistantToolExecutionResultDto> ExecuteCareConnectReferralLookupAsync(
        AssistantToolDefinitionDto tool,
        AssistantToolExecutionRequestDto request,
        CancellationToken ct)
    {
        var referralId = TryGetGuid(request.InputJson, "referralId");
        if (referralId is null || referralId == Guid.Empty)
        {
            return new AssistantToolExecutionResultDto(
                false,
                "invalid_input",
                "{}",
                "The CareConnect referral id is missing or invalid.",
                2,
                []);
        }

        var lookup = await _careConnect.LookupReferralAsync(referralId.Value, ct);
        if (!lookup.Succeeded || lookup.Referral is null)
        {
            return new AssistantToolExecutionResultDto(
                false,
                lookup.Status,
                "{}",
                lookup.SafeError ?? "The CareConnect referral lookup failed.",
                2,
                []);
        }

        var referral = lookup.Referral;
        var outputJson = JsonSerializer.Serialize(new
        {
            status = "available",
            referral = new
            {
                id = referral.ReferralId,
                clientDisplayName = referral.ClientDisplayName,
                status = referral.Status,
                urgency = referral.Urgency,
                providerName = referral.ProviderName,
                requestedService = referral.RequestedService,
                treatmentTypeName = referral.TreatmentTypeName,
                caseNumber = referral.CaseNumber,
                referringOrganizationName = referral.ReferringOrganizationName,
                referrerName = referral.ReferrerName,
                createdAtUtc = referral.CreatedAtUtc,
                updatedAtUtc = referral.UpdatedAtUtc,
            },
            recentHistory = referral.History.Select(item => new
            {
                oldStatus = item.OldStatus,
                newStatus = item.NewStatus,
                changedAtUtc = item.ChangedAtUtc,
                notes = item.Notes,
            }),
            note = lookup.SafeError,
        }, JsonOptions);

        if (outputJson.Length > tool.MaxOutputCharacters)
            outputJson = outputJson[..tool.MaxOutputCharacters];

        return new AssistantToolExecutionResultDto(
            true,
            lookup.Status,
            outputJson,
            lookup.SafeError,
            outputJson.Length,
            [
                new AssistantToolCitationDto(
                    "careconnect.referral",
                    referral.ReferralId.ToString(),
                    $"CareConnect referral {referral.ClientDisplayName}",
                    $"/careconnect/referrals/{referral.ReferralId}")
            ]);
    }

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

    private static Guid? TryGetGuid(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String &&
                   Guid.TryParse(value.GetString(), out var parsed)
                ? parsed
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
