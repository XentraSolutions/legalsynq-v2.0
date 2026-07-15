using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Xenia.Application.Assistant;

namespace Xenia.Infrastructure.Assistant;

internal sealed class StaticAssistantToolExecutor : IAssistantToolExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SearchDirectiveStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "at", "by", "case", "cases", "client", "clients",
        "count", "counts", "find", "for", "from", "how", "kpi", "kpis",
        "last", "latest", "law", "look", "lookup", "many", "me", "metric",
        "metrics", "number", "numbers", "of", "patient", "patients",
        "provider", "providers", "queue", "recent", "recently", "record",
        "records", "referral", "referrals", "referrer", "referrers", "search",
        "sent", "show", "status", "summary", "the", "to", "total", "totals",
        "up", "with",
    };

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

    public async Task<AssistantToolExecutionResultDto> ExecuteAsync(
        AssistantToolExecutionRequestDto request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var tool = _registry.ListToolsForAgent(request.AgentKey)
            .FirstOrDefault(t => t.ToolKey.Equals(request.ToolKey, StringComparison.OrdinalIgnoreCase));

        if (tool is null)
        {
            return Denied("unauthorized_tool", "The requested assistant tool is not allowed for this agent.");
        }

        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal is null || !HasRequiredProducts(principal, tool.RequiredProductCodes) || !HasAnyRequiredPermission(principal, tool.RequiredPermissions))
        {
            return Denied("forbidden", "You are not authorized to use this assistant tool.");
        }

        if (tool.ConfirmationRequired)
        {
            return Denied("confirmation_required", "This assistant tool requires explicit user confirmation.");
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

            return Trim(tool, new AssistantToolExecutionResultDto(
                true,
                "completed",
                outputJson,
                null,
                outputJson.Length,
                []));
        }

        if (request.ToolKey.Equals("careconnect.referral.lookup", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteCareConnectReferralLookupAsync(tool, request, ct);
        }

        if (request.ToolKey.Equals("careconnect.referral.history.lookup", StringComparison.OrdinalIgnoreCase))
            return await ExecuteCareConnectReferralHistoryLookupAsync(tool, request, ct);

        if (request.ToolKey.Equals("careconnect.referral.search", StringComparison.OrdinalIgnoreCase))
            return await ExecuteCareConnectReferralSearchAsync(tool, request, ct);

        if (request.ToolKey.Equals("careconnect.provider.search", StringComparison.OrdinalIgnoreCase))
            return await ExecuteCareConnectProviderSearchAsync(tool, request, ct);

        if (request.ToolKey.Equals("careconnect.referrer.search", StringComparison.OrdinalIgnoreCase))
            return await ExecuteCareConnectReferrerSearchAsync(tool, request, ct);

        if (request.ToolKey.Equals("careconnect.referral.queue.summary", StringComparison.OrdinalIgnoreCase))
            return await ExecuteCareConnectQueueSummaryAsync(tool, request, ct);

        return new AssistantToolExecutionResultDto(
            false,
            "adapter_unavailable",
            "{}",
            "This assistant tool is declared but its product adapter is not wired yet.",
            2,
            []);
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
            tool = tool.ToolKey,
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
                referringOrganizationName = referral.ReferringOrganizationName,
                referrerName = referral.ReferrerName,
                createdAtUtc = referral.CreatedAtUtc,
                updatedAtUtc = referral.UpdatedAtUtc,
                url = BuildReferralUrl(referral.ReferralId),
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

        return Trim(tool, new AssistantToolExecutionResultDto(
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
                    BuildReferralUrl(referral.ReferralId))
            ]));
    }

    private async Task<AssistantToolExecutionResultDto> ExecuteCareConnectReferralHistoryLookupAsync(
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

        var top = Math.Clamp(TryGetInt(request.InputJson, "top") ?? 10, 1, 25);
        var lookup = await _careConnect.LookupReferralHistoryAsync(referralId.Value, top, ct);
        if (!lookup.Succeeded || lookup.ReferralHistory is null)
        {
            return new AssistantToolExecutionResultDto(
                false,
                lookup.Status,
                "{}",
                lookup.SafeError ?? "The CareConnect referral history lookup failed.",
                2,
                []);
        }

        var history = lookup.ReferralHistory;
        var outputJson = JsonSerializer.Serialize(new
        {
            tool = tool.ToolKey,
            status = "available",
            referral = new
            {
                id = history.ReferralId,
                clientDisplayName = history.ClientDisplayName,
                providerName = history.ProviderName,
                currentStatus = history.CurrentStatus,
                url = BuildReferralUrl(history.ReferralId),
            },
            history = history.History.Select(item => new
            {
                oldStatus = item.OldStatus,
                newStatus = item.NewStatus,
                changedAtUtc = item.ChangedAtUtc,
                notes = item.Notes,
            }),
        }, JsonOptions);

        return Trim(tool, new AssistantToolExecutionResultDto(
            true,
            lookup.Status,
            outputJson,
            null,
            outputJson.Length,
            [
                new AssistantToolCitationDto(
                    "careconnect.referral",
                    history.ReferralId.ToString(),
                    $"CareConnect referral {history.ClientDisplayName}",
                    BuildReferralUrl(history.ReferralId))
            ]));
    }

    private async Task<AssistantToolExecutionResultDto> ExecuteCareConnectReferralSearchAsync(
        AssistantToolDefinitionDto tool,
        AssistantToolExecutionRequestDto request,
        CancellationToken ct)
    {
        var clientName = TryGetString(request.InputJson, "clientName")
            ?? TryGetString(request.InputJson, "patientName");
        var caseNumber = TryGetString(request.InputJson, "caseNumber");
        var providerName = TryGetString(request.InputJson, "providerName")
            ?? TryGetString(request.InputJson, "providerOrganizationName");
        var referrerName = TryGetString(request.InputJson, "referrerName")
            ?? TryGetString(request.InputJson, "lawFirmName")
            ?? TryGetString(request.InputJson, "referringOrganizationName");
        var status = TryGetString(request.InputJson, "status");
        var searchText = NormalizeSearchText(
            TryGetString(request.InputJson, "searchText"),
            clientName,
            caseNumber,
            providerName,
            referrerName,
            status);
        var top = Math.Clamp(TryGetInt(request.InputJson, "top") ?? 8, 1, 15);
        var outcome = await _careConnect.SearchReferralsAsync(new CareConnectReferralSearchRequest(
            searchText,
            clientName,
            caseNumber,
            providerName,
            referrerName,
            status,
            TryGetDateTime(request.InputJson, "createdFromUtc"),
            TryGetDateTime(request.InputJson, "createdToUtc"),
            top), ct);

        if (!outcome.Succeeded)
        {
            return new AssistantToolExecutionResultDto(
                false,
                outcome.Status,
                "{}",
                outcome.SafeError ?? "The CareConnect referral search failed.",
                2,
                []);
        }

        var outputJson = JsonSerializer.Serialize(new
        {
            tool = tool.ToolKey,
            status = outcome.Referrals.Count == 0 ? "empty" : "available",
            totalCount = outcome.TotalCount,
            filters = new
            {
                searchText,
                clientName,
                providerName,
                referrerName,
                status,
            },
            results = outcome.Referrals.Select(referral => new
            {
                type = "referral",
                id = referral.ReferralId,
                clientDisplayName = referral.ClientDisplayName,
                status = referral.Status,
                urgency = referral.Urgency,
                providerName = referral.ProviderName,
                requestedService = referral.RequestedService,
                treatmentTypeName = referral.TreatmentTypeName,
                referrerName = referral.ReferrerName,
                referringOrganizationName = referral.ReferringOrganizationName,
                createdAtUtc = referral.CreatedAtUtc,
                updatedAtUtc = referral.UpdatedAtUtc,
                url = BuildReferralUrl(referral.ReferralId),
            }),
        }, JsonOptions);

        return Trim(tool, new AssistantToolExecutionResultDto(
            true,
            outcome.Status,
            outputJson,
            outcome.SafeError,
            outputJson.Length,
            outcome.Referrals.Select(referral => new AssistantToolCitationDto(
                "careconnect.referral",
                referral.ReferralId.ToString(),
                $"CareConnect referral {referral.ClientDisplayName}",
                BuildReferralUrl(referral.ReferralId)))
                .ToList()));
    }

    private async Task<AssistantToolExecutionResultDto> ExecuteCareConnectProviderSearchAsync(
        AssistantToolDefinitionDto tool,
        AssistantToolExecutionRequestDto request,
        CancellationToken ct)
    {
        var top = Math.Clamp(TryGetInt(request.InputJson, "top") ?? 8, 1, 15);
        var outcome = await _careConnect.SearchProvidersAsync(new CareConnectProviderSearchRequest(
            TryGetString(request.InputJson, "name"),
            TryGetString(request.InputJson, "city"),
            TryGetString(request.InputJson, "state"),
            TryGetBool(request.InputJson, "acceptingReferrals"),
            top), ct);

        if (!outcome.Succeeded)
        {
            return new AssistantToolExecutionResultDto(
                false,
                outcome.Status,
                "{}",
                outcome.SafeError ?? "The CareConnect provider search failed.",
                2,
                []);
        }

        var outputJson = JsonSerializer.Serialize(new
        {
            tool = tool.ToolKey,
            status = outcome.Providers.Count == 0 ? "empty" : "available",
            totalCount = outcome.TotalCount,
            filters = new
            {
                name = TryGetString(request.InputJson, "name"),
                city = TryGetString(request.InputJson, "city"),
                state = TryGetString(request.InputJson, "state"),
                acceptingReferrals = TryGetBool(request.InputJson, "acceptingReferrals"),
            },
            results = outcome.Providers.Select(provider => new
            {
                type = "provider",
                id = provider.ProviderId,
                name = provider.Name,
                organizationName = provider.OrganizationName,
                city = provider.City,
                state = provider.State,
                acceptingReferrals = provider.AcceptingReferrals,
                isActive = provider.IsActive,
                primaryCategory = provider.PrimaryCategory,
                displayLabel = provider.DisplayLabel,
                url = BuildProviderUrl(provider.ProviderId),
            }),
        }, JsonOptions);

        return Trim(tool, new AssistantToolExecutionResultDto(
            true,
            outcome.Status,
            outputJson,
            outcome.SafeError,
            outputJson.Length,
            outcome.Providers.Select(provider => new AssistantToolCitationDto(
                "careconnect.provider",
                provider.ProviderId.ToString(),
                provider.DisplayLabel,
                BuildProviderUrl(provider.ProviderId)))
                .ToList()));
    }

    private async Task<AssistantToolExecutionResultDto> ExecuteCareConnectReferrerSearchAsync(
        AssistantToolDefinitionDto tool,
        AssistantToolExecutionRequestDto request,
        CancellationToken ct)
    {
        var referrerName = TryGetString(request.InputJson, "referrerName");
        var status = TryGetString(request.InputJson, "status");
        var searchText = NormalizeSearchText(
            TryGetString(request.InputJson, "searchText"),
            referrerName,
            status);
        var top = Math.Clamp(TryGetInt(request.InputJson, "top") ?? 6, 1, 10);
        var outcome = await _careConnect.SearchReferrersAsync(new CareConnectReferrerSearchRequest(
            searchText,
            referrerName,
            status,
            top), ct);

        if (!outcome.Succeeded)
        {
            return new AssistantToolExecutionResultDto(
                false,
                outcome.Status,
                "{}",
                outcome.SafeError ?? "The CareConnect referrer search failed.",
                2,
                []);
        }

        var outputJson = JsonSerializer.Serialize(new
        {
            tool = tool.ToolKey,
            status = outcome.Referrers.Count == 0 ? "empty" : "available",
            totalCount = outcome.TotalCount,
            results = outcome.Referrers.Select(referrer => new
            {
                type = "referrer",
                referrerName = referrer.ReferrerName,
                referrerEmail = referrer.ReferrerEmail,
                referralCount = referrer.ReferralCount,
                openReferralCount = referrer.OpenReferralCount,
                lastReferralAtUtc = referrer.LastReferralAtUtc,
                url = BuildReferrerSearchUrl(referrer.ReferrerName),
            }),
        }, JsonOptions);

        return Trim(tool, new AssistantToolExecutionResultDto(
            true,
            outcome.Status,
            outputJson,
            outcome.SafeError,
            outputJson.Length,
            outcome.Referrers.Select(referrer => new AssistantToolCitationDto(
                "careconnect.referrer",
                $"{referrer.ReferrerName}|{referrer.ReferrerEmail}",
                $"{referrer.ReferrerName} ({referrer.ReferralCount} referrals)",
                BuildReferrerSearchUrl(referrer.ReferrerName)))
                .ToList()));
    }

    private async Task<AssistantToolExecutionResultDto> ExecuteCareConnectQueueSummaryAsync(
        AssistantToolDefinitionDto tool,
        AssistantToolExecutionRequestDto request,
        CancellationToken ct)
    {
        var providerName = TryGetString(request.InputJson, "providerName");
        var referrerName = TryGetString(request.InputJson, "referrerName");
        var status = NormalizeReferralStatus(TryGetString(request.InputJson, "status"));
        var statusGroup = NormalizeStatusGroup(TryGetString(request.InputJson, "statusGroup"));
        var days = TryGetInt(request.InputJson, "days");
        var createdFromUtc = TryGetDateTime(request.InputJson, "createdFromUtc");
        var createdToUtc = TryGetDateTime(request.InputJson, "createdToUtc");
        var searchText = NormalizeSearchText(
            TryGetString(request.InputJson, "searchText"),
            providerName,
            referrerName,
            status,
            statusGroup);
        var recentTop = Math.Clamp(TryGetInt(request.InputJson, "recentTop") ?? 5, 1, 10);
        var outcome = await _careConnect.GetReferralQueueSummaryAsync(new CareConnectReferralQueueSummaryRequest(
            searchText,
            providerName,
            referrerName,
            status,
            statusGroup,
            days,
            createdFromUtc,
            createdToUtc,
            recentTop), ct);

        if (!outcome.Succeeded)
        {
            return new AssistantToolExecutionResultDto(
                false,
                outcome.Status,
                "{}",
                outcome.SafeError ?? "The CareConnect referral queue summary failed.",
                2,
                []);
        }

        var outputJson = JsonSerializer.Serialize(new
        {
            tool = tool.ToolKey,
            status = "available",
            filters = new
            {
                searchText,
                providerName,
                referrerName,
                status = outcome.AppliedStatus ?? status,
                statusGroup = outcome.AppliedStatusGroup ?? statusGroup,
                days,
                createdFromUtc = outcome.WindowFromUtc ?? createdFromUtc,
                createdToUtc = outcome.WindowToUtc ?? createdToUtc,
            },
            summary = new
            {
                totalVisibleReferrals = outcome.TotalVisibleReferrals,
                windowReferralCount = outcome.WindowReferralCount,
                matchingReferralCount = outcome.MatchingReferralCount,
                newReferralCount = outcome.NewReferralCount,
                openReferralCount = outcome.OpenReferralCount,
                closedReferralCount = outcome.ClosedReferralCount,
                windowFromUtc = outcome.WindowFromUtc,
                windowToUtc = outcome.WindowToUtc,
            },
            statusCounts = outcome.StatusCounts.Select(item => new
            {
                status = item.Status,
                count = item.Count,
            }),
            recentResults = outcome.RecentReferrals.Select(referral => new
            {
                type = "referral",
                id = referral.ReferralId,
                clientDisplayName = referral.ClientDisplayName,
                status = referral.Status,
                urgency = referral.Urgency,
                providerName = referral.ProviderName,
                updatedAtUtc = referral.UpdatedAtUtc,
                url = BuildReferralUrl(referral.ReferralId),
            }),
        }, JsonOptions);

        return Trim(tool, new AssistantToolExecutionResultDto(
            true,
            outcome.Status,
            outputJson,
            outcome.SafeError,
            outputJson.Length,
            outcome.RecentReferrals.Select(referral => new AssistantToolCitationDto(
                "careconnect.referral",
                referral.ReferralId.ToString(),
                $"CareConnect referral {referral.ClientDisplayName}",
                BuildReferralUrl(referral.ReferralId)))
                .ToList()));
    }

    private static bool HasAnyRequiredPermission(ClaimsPrincipal principal, IReadOnlyList<string> permissions)
    {
        if (permissions.Count == 0) return true;
        if (HasRole(principal, "PlatformAdmin")) return true;
        var granted = principal.FindAll("permissions").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return permissions.Any(granted.Contains);
    }

    private static string? NormalizeSearchText(string? rawSearchText, params string?[] structuredValues)
    {
        if (string.IsNullOrWhiteSpace(rawSearchText))
            return null;

        var remaining = rawSearchText.Trim();
        foreach (var value in structuredValues
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Select(value => value!.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            remaining = Regex.Replace(
                remaining,
                Regex.Escape(value),
                " ",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        var tokens = Regex.Split(remaining.ToLowerInvariant(), "[^a-z0-9]+")
            .Where(token => token.Length > 1 && !SearchDirectiveStopWords.Contains(token))
            .Distinct()
            .ToList();

        return tokens.Count == 0
            ? null
            : string.Join(' ', tokens);
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

    private static string? TryGetString(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty(propertyName, out var value) ||
                value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? TryGetInt(string? json, string propertyName)
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

            return value.TryGetInt32(out var parsed) ? parsed : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool? TryGetBool(string? json, string propertyName)
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

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateTime? TryGetDateTime(string? json, string propertyName)
    {
        var value = TryGetString(json, propertyName);
        return DateTime.TryParse(value, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static string? NormalizeReferralStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = Regex.Replace(value.Trim(), "[\\s_-]+", string.Empty)
            .ToUpperInvariant();

        return normalized switch
        {
            "NEW" => "New",
            "NEWOPENED" => "NewOpened",
            "ACCEPTED" or "RECEIVED" or "CONTACTED" => "Accepted",
            "INPROGRESS" or "SCHEDULED" => "InProgress",
            "COMPLETED" => "Completed",
            "DECLINED" => "Declined",
            "CANCELLED" or "CANCELED" => "Cancelled",
            _ => value.Trim(),
        };
    }

    private static string? NormalizeStatusGroup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = Regex.Replace(value.Trim(), "[\\s_-]+", string.Empty)
            .ToUpperInvariant();

        return normalized switch
        {
            "NEW" or "PENDING" or "INBOX" => "new",
            "OPEN" or "ACTIVE" => "open",
            "CLOSED" or "TERMINAL" or "RESOLVED" => "closed",
            _ => null,
        };
    }

    private static AssistantToolExecutionResultDto Trim(
        AssistantToolDefinitionDto tool,
        AssistantToolExecutionResultDto result)
    {
        if (result.OutputJson.Length <= tool.MaxOutputCharacters)
            return result;

        var trimmed = result.OutputJson[..tool.MaxOutputCharacters];
        return result with
        {
            OutputJson = trimmed,
            OutputCharacters = trimmed.Length,
        };
    }

    private static string BuildReferralUrl(Guid referralId)
        => $"/careconnect/referrals/{referralId}";

    private static string BuildProviderUrl(Guid providerId)
        => $"/careconnect/providers/{providerId}";

    private static string BuildReferrerSearchUrl(string referrerName)
        => $"/careconnect/referrals?search={Uri.EscapeDataString(referrerName)}";
}
