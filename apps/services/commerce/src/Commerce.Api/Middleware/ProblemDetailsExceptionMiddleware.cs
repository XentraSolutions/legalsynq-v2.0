using System.Text.Json;
using Commerce.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Middleware;

/// <summary>
/// Translates application/domain exceptions into RFC 7807 ProblemDetails responses.
/// Kept simple and non-invasive: no third-party middleware, no global handler hijacking.
/// </summary>
public sealed class ProblemDetailsExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public ProblemDetailsExceptionMiddleware(RequestDelegate next, ILogger<ProblemDetailsExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (ValidationException ex)
        {
            await Write(ctx, StatusCodes.Status400BadRequest, "validation_failed", "One or more validation errors occurred.",
                extra: new Dictionary<string, object?>
                {
                    ["errors"] = ex.Errors.Select(e => new { property = e.PropertyName, message = e.ErrorMessage }).ToArray()
                });
        }
        catch (NotFoundException ex)
        {
            await Write(ctx, StatusCodes.Status404NotFound, "not_found", ex.Message);
        }
        catch (DuplicateKeyException ex)
        {
            await Write(ctx, StatusCodes.Status409Conflict, "duplicate_key", ex.Message,
                extra: new Dictionary<string, object?> { ["resource"] = ex.Resource, ["key"] = ex.Key });
        }
        catch (InvalidStateTransitionException ex)
        {
            await Write(ctx, StatusCodes.Status409Conflict, "invalid_state_transition", ex.Message);
        }
        catch (InvalidRelationshipException ex)
        {
            await Write(ctx, StatusCodes.Status422UnprocessableEntity, "invalid_relationship", ex.Message);
        }
        catch (InvalidPrimaryReferenceException ex)
        {
            await Write(ctx, StatusCodes.Status422UnprocessableEntity, "invalid_primary_reference", ex.Message);
        }
        catch (PaymentProviderDisabledException ex)
        {
            await Write(ctx, StatusCodes.Status503ServiceUnavailable, "payment_provider_disabled", ex.Message,
                extra: new Dictionary<string, object?> { ["provider"] = ex.Provider });
        }
        catch (PaymentProviderConfigurationException ex)
        {
            await Write(ctx, StatusCodes.Status503ServiceUnavailable, "payment_provider_misconfigured", ex.Message,
                extra: new Dictionary<string, object?>
                {
                    ["provider"] = ex.Provider,
                    ["setting"] = ex.Setting
                });
        }
        catch (InvalidWebhookSignatureException ex)
        {
            await Write(ctx, StatusCodes.Status400BadRequest, "invalid_webhook_signature", ex.Message,
                extra: new Dictionary<string, object?> { ["provider"] = ex.Provider });
        }
        catch (PaymentProviderException ex)
        {
            await Write(ctx, StatusCodes.Status502BadGateway, "payment_provider_error", ex.Message,
                extra: new Dictionary<string, object?> { ["provider"] = ex.Provider });
        }
        catch (ProviderEventReprocessNotAllowedException ex)
        {
            await Write(ctx, StatusCodes.Status409Conflict, "provider_event_reprocess_not_allowed", ex.Message,
                extra: new Dictionary<string, object?>
                {
                    ["eventLogId"] = ex.EventLogId,
                    ["currentStatus"] = ex.CurrentStatus
                });
        }
        catch (FinancialRecordConflictException ex)
        {
            await Write(ctx, StatusCodes.Status409Conflict, "financial_record_conflict", ex.Message,
                extra: new Dictionary<string, object?>
                {
                    ["resource"] = ex.Resource,
                    ["detail"] = ex.Detail
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Commerce API");
            await Write(ctx, StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred.");
        }
    }

    private static async Task Write(HttpContext ctx, int status, string code, string detail,
        IDictionary<string, object?>? extra = null)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Title = code,
            Status = status,
            Detail = detail,
            Type = $"about:blank#{code}",
            Instance = ctx.Request.Path
        };

        if (extra is not null)
        {
            foreach (var kv in extra) problem.Extensions[kv.Key] = kv.Value;
        }

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOpts));
    }
}
